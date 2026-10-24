using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The single platform-operated demo workspace used to show prospects the product. It lives in production
/// next to real tenants, so everything here is pinned to one fixed tenant id and one slug: the reset can only
/// ever touch that tenant, outbound email is disabled for it, and its license is perpetual so no billing or
/// trial worker ever acts on it.
/// </summary>
public static class DemoWorkspace
{
    public static readonly Guid TenantId = Guid.Parse("de300000-0000-4000-8000-000000000001");
    public const string Slug = "ecosys-demo";
    public const string CompanyName = "Ecosys Demo";
    public const string ResetAuditAction = "demo.workspace.reset";

    /// <summary>The same placeholder domain UsersController gives username-only accounts; nothing is ever mailed there.</summary>
    internal const string NoEmailDomain = "no-email.invalid";
}

public sealed record DemoWorkspaceLogin(string Username, string FullName, string Role, string Shows);

public sealed record DemoWorkspacePortalContact(Guid Id, string FullName, string ClientName);

public sealed record DemoWorkspaceStatus(
    bool Exists,
    Guid TenantId,
    string CompanyName,
    string Workspace,
    DateTime? LastResetAt,
    string? LastResetBy,
    int Clients,
    int WorkOrders,
    int Quotations,
    int Invoices,
    IReadOnlyCollection<DemoWorkspaceLogin> Logins,
    DemoWorkspacePortalContact? PortalContact);

/// <summary>Returned once, right after a reset. The password is not stored anywhere in clear text.</summary>
public sealed record DemoWorkspaceResetResult(DemoWorkspaceStatus Status, string Password);

public interface IDemoWorkspaceService
{
    Task<DemoWorkspaceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the demo workspace, or wipes and re-seeds it, and rotates the shared demo password.</summary>
    Task<DemoWorkspaceResetResult> ResetAsync(Guid? actorUserId, string? actorName, CancellationToken cancellationToken = default);

    Task<string> IssuePortalSignInLinkAsync(string appBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>The user behind one of the demo's staff logins; anything else is refused.</summary>
    Task<Guid> FindLoginUserIdAsync(string? username, CancellationToken cancellationToken = default);
}

internal sealed partial class DemoWorkspaceService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IPortalAuthService portalAuthService,
    IFileStorageService fileStorageService,
    IAuditLogService auditLogService,
    ILogger<DemoWorkspaceService> logger) : IDemoWorkspaceService
{
    private static readonly Guid TenantId = DemoWorkspace.TenantId;

    /// <summary>
    /// Tenant-scoped tables the reset leaves alone: the license (so the workspace keeps its perpetual term) and
    /// the platform's own commercial records, which are not the demo's data to erase.
    /// </summary>
    private static readonly HashSet<string> PreservedTables = new(StringComparer.Ordinal)
    {
        "tenant_licenses", "platform_invoices", "platform_payments", "platform_quotations", "platform_expenses", "platform_leads",
    };

    private static readonly DemoWorkspaceLogin[] Logins =
    [
        new("admin", "Amina Otieno", AppRoles.Admin, "Everything: settings, finance, users, reports."),
        new("manager", "Brian Mwangi", AppRoles.Manager, "Operations oversight, approvals and reports."),
        new("dispatcher", "Grace Njeri", AppRoles.Dispatcher, "Job intake, scheduling and technician dispatch."),
        new("technician", "Kevin Ochieng", AppRoles.Technician, "The field view: assigned jobs, check-in, completion."),
    ];

    public async Task<DemoWorkspaceStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
        var lastReset = await dbContext.AuditLogs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == PlatformConstants.RootTenantId && x.Action == DemoWorkspace.ResetAuditAction)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.CreatedAt, x.ActorName })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return new DemoWorkspaceStatus(false, TenantId, DemoWorkspace.CompanyName, DemoWorkspace.Slug, lastReset?.CreatedAt, lastReset?.ActorName, 0, 0, 0, 0, Logins, null);
        }

        var portalContact = await dbContext.ClientPortalUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new DemoWorkspacePortalContact(x.Id, x.FullName, x.Client != null ? x.Client.ClientName : "—"))
            .FirstOrDefaultAsync(cancellationToken);

        return new DemoWorkspaceStatus(
            true,
            TenantId,
            tenant.CompanyName,
            tenant.Slug,
            lastReset?.CreatedAt,
            lastReset?.ActorName,
            await dbContext.Clients.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            await dbContext.WorkOrders.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            await dbContext.TenantQuotations.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            await dbContext.TenantInvoices.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            Logins,
            portalContact);
    }

    public async Task<DemoWorkspaceResetResult> ResetAsync(Guid? actorUserId, string? actorName, CancellationToken cancellationToken = default)
    {
        var password = GeneratePassword();
        var relational = dbContext.Database.IsRelational();
        List<string> orphanedFiles = [];

        await using (var transaction = relational ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null)
        {
            var tenant = await dbContext.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
            if (tenant is null)
            {
                await EnsureSlugIsFreeAsync(cancellationToken);
                tenant = new Tenant { Id = TenantId, CreatedByUserId = actorUserId };
                dbContext.Tenants.Add(tenant);
            }
            else
            {
                // Belt and braces: the purge below deletes every row carrying this tenant id. Refuse unless the row
                // really is the demo workspace, so a mistake elsewhere can never turn this into a tenant wipe.
                if (!string.Equals(tenant.Slug, DemoWorkspace.Slug, StringComparison.Ordinal))
                {
                    throw new BusinessRuleException($"Tenant {TenantId} is not the demo workspace; refusing to reset it.");
                }

                if (relational)
                {
                    orphanedFiles = await ReadStoredFilePathsAsync(cancellationToken);
                    await PurgeTenantDataAsync(transaction!, cancellationToken);
                }
            }

            await ConfigureTenantAsync(tenant, cancellationToken);
            var content = new DemoContent(TenantId, password, passwordHasher);
            content.AddTo(dbContext);
            await dbContext.SaveChangesAsync(cancellationToken);

            // SaveChanges stamps CreatedAt with "now" on insert; back-date the history in a second pass.
            content.ApplyCreatedDates();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }

        dbContext.ChangeTracker.Clear();
        foreach (var path in orphanedFiles)
        {
            try
            {
                await fileStorageService.DeleteAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Demo reset could not delete stored file {StoragePath}.", path);
            }
        }

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            actorUserId,
            DemoWorkspace.ResetAuditAction,
            nameof(Tenant),
            TenantId.ToString(),
            "Demo workspace reset and re-seeded; demo password rotated.",
            actorName: actorName,
            cancellationToken: cancellationToken);

        return new DemoWorkspaceResetResult(await GetStatusAsync(cancellationToken), password);
    }

    public async Task<string> IssuePortalSignInLinkAsync(string appBaseUrl, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
    {
        var contactId = await dbContext.ClientPortalUsers.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("The demo workspace has no active portal contact. Reset the demo workspace first.");

        return await portalAuthService.IssueSignInLinkAsync(contactId, appBaseUrl, ipAddress, userAgent, cancellationToken);
    }

    public async Task<Guid> FindLoginUserIdAsync(string? username, CancellationToken cancellationToken = default)
    {
        var login = Logins.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("That is not one of the demo workspace's logins.");
        return await dbContext.Users.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.Username == login.Username && x.IsActive)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("The demo workspace has not been created yet. Create it first.");
    }

    // ── Tenant + license ─────────────────────────────────────────────────────

    private async Task EnsureSlugIsFreeAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Tenants.IgnoreQueryFilters().AnyAsync(x => x.Slug == DemoWorkspace.Slug && x.Id != TenantId, cancellationToken))
        {
            throw new BusinessRuleException($"Another tenant already uses the workspace name '{DemoWorkspace.Slug}'.");
        }
    }

    private async Task ConfigureTenantAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        tenant.Name = DemoWorkspace.CompanyName;
        tenant.CompanyName = DemoWorkspace.CompanyName;
        tenant.Slug = DemoWorkspace.Slug;
        tenant.Email = $"workspace@{DemoWorkspace.Slug}.{DemoWorkspace.NoEmailDomain}";
        tenant.ContactName = "Amina Otieno";
        tenant.ContactEmail = tenant.Email;
        tenant.Phone = "+254 700 000 000";
        tenant.Country = "Kenya";
        tenant.City = "Nairobi";
        tenant.TimeZoneId = "Africa/Nairobi";
        tenant.Currency = "KES";
        tenant.Industry = "Facilities Management";
        tenant.TrialStartsAt = null;
        tenant.TrialEndsAt = null;

        var edition = await dbContext.LicensePlans.SingleAsync(x => x.PlanCode == ServiceOpsPlans.Enterprise && x.IsActive, cancellationToken);
        var license = await dbContext.TenantLicenses.IgnoreQueryFilters().Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (license is null)
        {
            license = new TenantLicense { TenantId = TenantId };
            dbContext.TenantLicenses.Add(license);
        }

        // Perpetual, active and never billed: the subscription worker only looks at licenses with an expiry, the
        // trial worker only at trials, and billing only at licenses with a next billing date.
        license.LicensePlanId = edition.Id;
        license.LicensePlan = edition;
        license.Status = LicenseStatuses.Active;
        license.IsPerpetual = true;
        if (license.StartsAt == default)
        {
            license.StartsAt = now;
        }

        license.TrialEndsAt = null;
        license.ExpiresAt = null;
        license.NextBillingDate = null;
        license.SuspendedAt = null;
        license.SuspensionReason = null;
        license.CancelledAt = null;
        license.OfficeSeats = TenantLicense.DefaultOfficeSeats;
        license.FieldSeats = TenantLicense.DefaultFieldSeats;
        license.MaxBranchesOverride = 3;
        TenantLifecycleState.Sync(tenant, license, now);
    }

    // ── Purge ────────────────────────────────────────────────────────────────

    private async Task<List<string>> ReadStoredFilePathsAsync(CancellationToken cancellationToken)
    {
        var attachments = await dbContext.Attachments.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.StoragePath != null && x.StoragePath != "")
            .Select(x => x.StoragePath!)
            .ToListAsync(cancellationToken);
        var documents = await dbContext.KnowledgeDocuments.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.StoragePath != null && x.StoragePath != "")
            .Select(x => x.StoragePath!)
            .ToListAsync(cancellationToken);
        return [.. attachments, .. documents];
    }

    /// <summary>
    /// Deletes every row owned by the demo tenant, except the tenant row, its license and platform records.
    /// The table list comes from the live schema, so tables added later are covered without touching this code.
    /// Foreign keys decide the order: a table whose rows are still referenced is retried after the others, and
    /// the whole thing runs inside the caller's transaction, so a purge that cannot finish changes nothing.
    /// </summary>
    private async Task PurgeTenantDataAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        var tables = (await dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT c.relname AS "Value"
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = c.oid
                WHERE n.nspname = current_schema() AND c.relkind = 'r' AND a.attname = 'TenantId' AND NOT a.attisdropped
                """)
            .ToListAsync(cancellationToken))
            .Where(x => !PreservedTables.Contains(x))
            .Select(RequireSafeIdentifier)
            .ToList();

        // Self-references declared RESTRICT (a job's parent job, a branch's parent branch) block a set-based delete
        // of the same table, so detach them first.
        var selfReferences = await dbContext.Database.SqlQueryRaw<string>(
                """
                SELECT c.relname || '.' || a.attname AS "Value"
                FROM pg_constraint con
                JOIN pg_class c ON c.oid = con.conrelid
                JOIN pg_namespace n ON n.oid = c.relnamespace
                JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = ANY (con.conkey)
                WHERE con.contype = 'f' AND con.confrelid = con.conrelid AND n.nspname = current_schema() AND NOT a.attnotnull
                """)
            .ToListAsync(cancellationToken);
        foreach (var reference in selfReferences)
        {
            var parts = reference.Split('.');
            var table = RequireSafeIdentifier(parts[0]);
            if (!tables.Contains(table))
            {
                continue;
            }

            var column = RequireSafeIdentifier(parts[1]);
            await dbContext.Database.ExecuteSqlRawAsync($"UPDATE \"{table}\" SET \"{column}\" = NULL WHERE \"TenantId\" = {{0}}", [TenantId], cancellationToken);
        }

        var pending = tables;
        while (pending.Count > 0)
        {
            var blocked = new List<string>();
            foreach (var table in pending)
            {
                await transaction.CreateSavepointAsync("demo_purge", cancellationToken);
                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"TenantId\" = {{0}}", [TenantId], cancellationToken);
                    await transaction.ReleaseSavepointAsync("demo_purge", cancellationToken);
                }
                // ON DELETE RESTRICT reports 23001, NO ACTION reports 23503; both mean "delete the referencing rows first".
                catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.RestrictViolation)
                {
                    await transaction.RollbackToSavepointAsync("demo_purge", cancellationToken);
                    blocked.Add(table);
                }
            }

            if (blocked.Count == pending.Count)
            {
                throw new InvalidOperationException($"Demo reset could not clear these tables because other rows still reference them: {string.Join(", ", blocked)}. Nothing was changed.");
            }

            pending = blocked;
        }
    }

    private static string RequireSafeIdentifier(string identifier) =>
        SafeIdentifier().IsMatch(identifier) ? identifier : throw new InvalidOperationException($"Unexpected table or column name '{identifier}'.");

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex SafeIdentifier();

    // Letters and digits without look-alikes (0/O, 1/l/I), plus the classes the default password policy requires.
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string all = upper + lower + digits;
        var chars = new char[14];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 3; i < chars.Length; i++)
        {
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return $"Demo-{new string(chars)}";
    }

    // ── Content ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A Kenyan facilities-maintenance company with four clients. Every date is relative to the moment of the
    /// reset so the demo always has upcoming, due and overdue work. The first client, Nairobi West Hospital, has
    /// the portal contact and the richest history so the client portal has something in every section.
    /// </summary>
    private sealed class DemoContent(Guid tenantId, string password, IPasswordHasher<User> passwordHasher)
    {
        private readonly DateTime _now = DateTime.UtcNow;
        private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);
        private readonly List<(Ecosys.Domain.Common.AuditableEntity Entity, DateTime CreatedAt)> _createdDates = [];
        private Guid _headOfficeId;
        private AppDbContext _db = null!;

        private string Next(string documentType, string prefix)
        {
            var next = _counters.GetValueOrDefault(documentType) + 1;
            _counters[documentType] = next;
            return $"{prefix}-{next:000000}";
        }

        public void AddTo(AppDbContext db)
        {
            _db = db;
            var d = _now.Date;

            // Settings the workspace needs to behave like any other tenant.
            db.EmailSettings.Add(new EmailSetting
            {
                TenantId = tenantId,
                // Off on purpose: the demo's users, clients and contacts have no real mailboxes. The outbox marks
                // anything queued for this tenant as Disabled instead of sending it.
                IsEnabled = false,
                InboundEnabled = false,
                SenderName = DemoWorkspace.CompanyName,
                SenderAddress = $"noreply@{DemoWorkspace.Slug}.{DemoWorkspace.NoEmailDomain}",
            });
            db.NotificationSettings.Add(new NotificationSetting { TenantId = tenantId });
            db.MonitoringSettings.Add(new MonitoringSetting { TenantId = tenantId });
            db.TenantSecurityPolicies.Add(new TenantSecurityPolicy { TenantId = tenantId });
            db.TenantModuleSettings.Add(new TenantModuleSetting
            {
                TenantId = tenantId,
                ModuleKey = PortalModuleSettings.ModuleKey,
                ValueJson = JsonSerializer.Serialize(new
                {
                    allowRequestSubmission = true,
                    allowQuoteResponses = true,
                    showInvoices = true,
                    showContracts = true,
                    showAssetRegister = true,
                    requireApprovalForNewContacts = false,
                    welcomeMessage = "Welcome to your Ecosys Demo service portal — track jobs, approve quotes and view invoices in one place.",
                }),
            });

            // Branches
            var nairobi = Add(db.Branches, new Branch { TenantId = tenantId, Name = "Nairobi Head Office", Code = "NBO", Location = "Westlands, Nairobi", IsActive = true });
            var mombasa = Add(db.Branches, new Branch { TenantId = tenantId, Name = "Mombasa Branch", Code = "MSA", Location = "Nyali, Mombasa", IsActive = true });
            _headOfficeId = nairobi.Id;

            // Staff logins (username + workspace name; no email address)
            var users = Logins.ToDictionary(login => login.Username, login =>
            {
                var user = new User
                {
                    TenantId = tenantId,
                    DefaultBranchId = nairobi.Id,
                    FullName = login.FullName,
                    Username = login.Username,
                    Email = $"{login.Username}.{DemoWorkspace.Slug}@{DemoWorkspace.NoEmailDomain}",
                    HasEmail = false,
                    Role = login.Role,
                    JobTitle = login.Role switch
                    {
                        AppRoles.Admin => "Managing Director",
                        AppRoles.Manager => "Operations Manager",
                        AppRoles.Dispatcher => "Service Coordinator",
                        _ => "Field Technician",
                    },
                    IsActive = true,
                    HasAllBranchAccess = login.Role != AppRoles.Technician,
                    MustChangePassword = false,
                    Permission = new UserPermission(),
                };
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                return Add(db.Users, user);
            });
            var admin = users["admin"];

            // Staff without all-branch access only see work in the branches they are assigned to.
            db.UserBranchAssignments.Add(new UserBranchAssignment { TenantId = tenantId, UserId = users["technician"].Id, BranchId = nairobi.Id, IsDefault = true });

            // Technicians: one with the technician login, three field staff without logins
            var kevin = AddTechnician(users["technician"].Id, nairobi.Id, "Kevin Ochieng", "Electrical", "+254 711 000 101");
            var peter = AddTechnician(null, nairobi.Id, "Peter Kiprono", "HVAC", "+254 711 000 102");
            var faith = AddTechnician(null, nairobi.Id, "Faith Wambui", "Electrical", "+254 711 000 103");
            var samuel = AddTechnician(null, mombasa.Id, "Samuel Mutua", "Plumbing", "+254 711 000 104");

            // Clients, sites and assets
            var hospital = AddClient("Nairobi West Hospital", "Healthcare", "Dr. Mercy Achieng", "+254 720 100 200", "Nairobi");
            var bank = AddClient("Savannah Bank", "Financial Services", "James Mwangi", "+254 720 100 300", "Nairobi");
            var mall = AddClient("Tsavo Mall", "Retail", "Lucy Wairimu", "+254 720 100 400", "Nairobi");
            var hotel = AddClient("Coral Beach Hotel", "Hospitality", "Hassan Omar", "+254 720 100 500", "Mombasa");

            var hospitalMain = AddSite(hospital, "NWH-MAIN", "Main Campus", "Mbagathi Road", "Nairobi", "Dr. Mercy Achieng");
            var hospitalClinic = AddSite(hospital, "NWH-OPC", "Outpatient Clinic", "Langata Road", "Nairobi", "Rose Adhiambo");
            var bankHq = AddSite(bank, "SVB-HQ", "Head Office", "Upper Hill", "Nairobi", "James Mwangi");
            var bankDc = AddSite(bank, "SVB-DC", "Data Centre", "Industrial Area", "Nairobi", "Paul Kariuki");
            var mallSite = AddSite(mall, "TSM-01", "Tsavo Mall", "Thika Road", "Nairobi", "Lucy Wairimu");
            var hotelSite = AddSite(hotel, "CBH-01", "Coral Beach Resort", "Nyali Beach Road", "Mombasa", "Hassan Omar");

            var generator = AddAsset(hospital, hospitalMain, nairobi, "Standby Generator 500kVA", "NWH-GEN-01", "Generator", "Caterpillar", "C15", pmDueInDays: 12);
            var ahu = AddAsset(hospital, hospitalMain, nairobi, "Air Handling Unit – Theatre 2", "NWH-AHU-02", "HVAC", "Carrier", "39HQ", pmDueInDays: 4);
            var oxygen = AddAsset(hospital, hospitalMain, nairobi, "Medical Oxygen Plant", "NWH-OXY-01", "Medical Gas", "Atlas Copco", "OGP-20", pmDueInDays: 25);
            var chiller = AddAsset(hospital, hospitalClinic, nairobi, "Chiller Unit", "NWH-CHL-01", "HVAC", "Daikin", "EWAD-TZ", pmDueInDays: 40);
            var ups = AddAsset(bank, bankDc, nairobi, "UPS 120kVA – Server Room", "SVB-UPS-01", "UPS", "Schneider", "Galaxy VX", pmDueInDays: 9);
            var crac = AddAsset(bank, bankDc, nairobi, "Precision Cooling Unit", "SVB-CRAC-01", "HVAC", "Vertiv", "Liebert PDX", pmDueInDays: 18);
            var bankLift = AddAsset(bank, bankHq, nairobi, "Passenger Lift A", "SVB-LFT-01", "Lift", "Otis", "Gen2", pmDueInDays: 33);
            var escalator = AddAsset(mall, mallSite, nairobi, "Escalator – Atrium", "TSM-ESC-01", "Escalator", "Schindler", "9300", pmDueInDays: 6);
            var firePanel = AddAsset(mall, mallSite, nairobi, "Fire Alarm Panel", "TSM-FAP-01", "Fire Safety", "Honeywell", "Notifier NFS2", pmDueInDays: 50);
            var pool = AddAsset(hotel, hotelSite, mombasa, "Pool Filtration Pumps", "CBH-PMP-01", "Pumps", "Grundfos", "CR 32", pmDueInDays: 3);
            var hotelChiller = AddAsset(hotel, hotelSite, mombasa, "Central Chiller", "CBH-CHL-01", "HVAC", "Trane", "CGAM", pmDueInDays: 21);

            foreach (var (asset, frequency) in new[] { (generator, "Monthly"), (ahu, "Monthly"), (oxygen, "Quarterly"), (ups, "Quarterly"), (crac, "Monthly"), (escalator, "Monthly"), (pool, "Monthly"), (hotelChiller, "Quarterly") })
            {
                db.PreventiveMaintenancePlans.Add(new PreventiveMaintenancePlan
                {
                    TenantId = tenantId,
                    BranchId = asset.BranchId,
                    AssetId = asset.Id,
                    SiteId = asset.SiteId,
                    Frequency = frequency,
                    FrequencyUnit = frequency,
                    FrequencyInterval = 1,
                    LastPmDate = asset.LastPmDate,
                    NextPmDate = asset.NextPmDate,
                    Status = "Active",
                });
            }

            // Contracts
            var hospitalContract = AddContract(hospital, "Comprehensive Maintenance – Nairobi West Hospital", ContractType.ComprehensiveMaintenance, BillingModel.PreferentialRates, 2_400_000m, startedMonthsAgo: 4, admin.Id);
            AddContract(bank, "Critical Power & Cooling PM – Savannah Bank", ContractType.PreventiveMaintenance, BillingModel.Included, 1_150_000m, startedMonthsAgo: 10, admin.Id);

            // Jobs — every status, several overdue, spread over the last few weeks
            AddJob(hospital, hospitalMain, ahu, null, "Theatre 2 AHU not maintaining temperature", "High", WorkOrderStatuses.New, due: d.AddDays(1), created: _now.AddHours(-3), contract: hospitalContract);
            AddJob(hospital, hospitalClinic, null, null, "Leaking tap in outpatient washrooms", "Low", WorkOrderStatuses.New, due: d.AddDays(5), created: _now.AddHours(-20));
            var genJob = AddJob(hospital, hospitalMain, generator, kevin, "Generator failed weekly load test", "Critical", WorkOrderStatuses.InProgress, due: d.AddDays(-1), created: _now.AddDays(-2), contract: hospitalContract,
                started: _now.AddHours(-5), notes: ["Technician on site. Initial finding: faulty automatic voltage regulator.", "Replacement AVR sourced from Nairobi stores; fitting this afternoon."]);
            AddJob(hospital, hospitalMain, oxygen, peter, "Oxygen plant pressure alarm", "High", WorkOrderStatuses.WaitingOnParts, due: d.AddDays(-3), created: _now.AddDays(-6), contract: hospitalContract,
                hold: ("Awaiting Spares", "Waiting for replacement pressure transducer from supplier."), notes: ["Pressure transducer ordered, ETA 2 working days."]);
            AddJob(hospital, hospitalClinic, chiller, peter, "Quarterly chiller service", "Medium", WorkOrderStatuses.Scheduled, due: d.AddDays(6), created: _now.AddDays(-4), contract: hospitalContract,
                scheduled: d.AddDays(3).AddHours(8), isPm: true);
            AddJob(hospital, hospitalMain, generator, kevin, "Monthly generator PM", "Medium", WorkOrderStatuses.Completed, due: d.AddDays(-8), created: _now.AddDays(-12), contract: hospitalContract,
                started: _now.AddDays(-9), completed: _now.AddDays(-9).AddHours(3), workDone: "Oil and filters changed, coolant topped up, battery tested at 12.7V. Load test passed at 80%.", isPm: true);
            AddJob(hospital, hospitalMain, ahu, peter, "Replace AHU belts – Theatre 2", "Medium", WorkOrderStatuses.ClientAccepted, due: d.AddDays(-14), created: _now.AddDays(-18), contract: hospitalContract,
                started: _now.AddDays(-15), completed: _now.AddDays(-15).AddHours(2), workDone: "Replaced two worn V-belts and re-tensioned. Airflow verified.");
            AddJob(hospital, hospitalMain, null, faith, "Install emergency lighting in corridor B", "Medium", WorkOrderStatuses.Closed, due: d.AddDays(-25), created: _now.AddDays(-30),
                started: _now.AddDays(-27), completed: _now.AddDays(-26), closed: _now.AddDays(-24), workDone: "Installed 12 LED emergency fittings; 3-hour discharge test passed.");
            AddJob(hospital, hospitalClinic, null, faith, "Faulty sockets in consultation room 4", "Low", WorkOrderStatuses.Closed, due: d.AddDays(-35), created: _now.AddDays(-40),
                started: _now.AddDays(-38), completed: _now.AddDays(-38).AddHours(1), closed: _now.AddDays(-36), workDone: "Replaced 4 damaged sockets and tested all circuits.");
            AddJob(hospital, hospitalMain, null, null, "Repaint plant room floor", "Low", WorkOrderStatuses.Cancelled, due: d.AddDays(-10), created: _now.AddDays(-20));

            AddJob(bank, bankDc, ups, kevin, "UPS battery alarm in server room", "Critical", WorkOrderStatuses.Assigned, due: d, created: _now.AddHours(-6));
            AddJob(bank, bankDc, crac, peter, "Monthly precision cooling PM", "Medium", WorkOrderStatuses.InProgress, due: d.AddDays(1), created: _now.AddDays(-1), started: _now.AddHours(-2), isPm: true);
            AddJob(bank, bankHq, bankLift, null, "Lift A stopping between floors", "High", WorkOrderStatuses.OnHold, due: d.AddDays(-2), created: _now.AddDays(-5),
                hold: ("Awaiting Vendor", "Awaiting lift OEM engineer visit."));
            AddJob(bank, bankHq, null, faith, "Replace lobby lighting fixtures", "Low", WorkOrderStatuses.Closed, due: d.AddDays(-20), created: _now.AddDays(-28),
                started: _now.AddDays(-22), completed: _now.AddDays(-21), closed: _now.AddDays(-19), workDone: "Replaced 18 fixtures with LED panels.");

            AddJob(mall, mallSite, escalator, null, "Escalator emergency stop triggered repeatedly", "High", WorkOrderStatuses.Open, due: d.AddDays(-1), created: _now.AddDays(-2));
            AddJob(mall, mallSite, firePanel, faith, "Annual fire alarm inspection", "Medium", WorkOrderStatuses.Assigned, due: d.AddDays(9), created: _now.AddDays(-1), isPm: true);
            AddJob(mall, mallSite, null, kevin, "Food court extractor fan noisy", "Medium", WorkOrderStatuses.Completed, due: d.AddDays(-4), created: _now.AddDays(-7),
                started: _now.AddDays(-5), completed: _now.AddDays(-5).AddHours(2), workDone: "Bearings replaced and fan balanced.");

            AddJob(hotel, hotelSite, pool, samuel, "Pool pump losing pressure", "High", WorkOrderStatuses.InProgress, due: d, created: _now.AddDays(-1), started: _now.AddHours(-1), branch: mombasa);
            AddJob(hotel, hotelSite, hotelChiller, samuel, "Chiller quarterly service", "Medium", WorkOrderStatuses.Closed, due: d.AddDays(-30), created: _now.AddDays(-34),
                started: _now.AddDays(-31), completed: _now.AddDays(-31).AddHours(5), closed: _now.AddDays(-29), workDone: "Condenser coils cleaned, refrigerant pressures within spec.", branch: mombasa, isPm: true);

            // Quotations — the hospital sees these in the portal; drafts are kept to other clients
            AddQuote(hospital, "Upgrade theatre AHU controls", "Sent", d.AddDays(-2), [("BMS controller and sensors", 1, "set", 385_000m), ("Installation and commissioning", 3, "day", 45_000m)]);
            AddQuote(hospital, "Generator ATS replacement", "Sent", d.AddDays(-5), [("Automatic transfer switch 800A", 1, "unit", 520_000m), ("Labour", 2, "day", 38_000m)]);
            AddQuote(hospital, "Emergency lighting – corridor B", "Accepted", d.AddDays(-32), [("LED emergency fittings", 12, "unit", 8_500m), ("Installation", 1, "lot", 36_000m)]);
            AddQuote(hospital, "Replace clinic chiller compressor", "Rejected", d.AddDays(-20), [("Compressor", 1, "unit", 740_000m), ("Labour", 4, "day", 42_000m)],
                rejectionReason: "Deferred to next financial year's capital budget.");
            AddQuote(mall, "Escalator handrail replacement", "Draft", d, [("Handrail kit", 2, "set", 210_000m)]);
            AddQuote(hotel, "Pool pump VFD retrofit", "Sent", d.AddDays(-1), [("Variable frequency drive 15kW", 2, "unit", 1_450m), ("Commissioning", 1, "lot", 600m)], currency: "USD");

            // Invoices and payments — paid, part-paid, open, overdue, and one in USD
            AddInvoice(hospital, "Paid", d.AddDays(-40), d.AddDays(-10), [("Emergency lighting installation", 1, "lot", 138_000m)], paid: 160_080m, method: "BankTransfer");
            AddInvoice(hospital, "PartiallyPaid", d.AddDays(-20), d.AddDays(10), [("Monthly maintenance retainer", 1, "month", 200_000m), ("AHU belts", 2, "unit", 6_500m)], paid: 120_000m, method: "MPesa");
            AddInvoice(hospital, "Overdue", d.AddDays(-45), d.AddDays(-15), [("Socket replacement – room 4", 1, "job", 14_000m)]);
            AddInvoice(hospital, "Sent", d.AddDays(-3), d.AddDays(27), [("Generator PM – monthly", 1, "visit", 32_000m)]);
            AddInvoice(bank, "Paid", d.AddDays(-25), d.AddDays(5), [("Lobby lighting upgrade", 1, "lot", 95_000m)], paid: 110_200m, method: "Cheque");
            AddInvoice(hotel, "Sent", d.AddDays(-10), d.AddDays(20), [("Chiller quarterly service", 1, "visit", 850m)], currency: "USD");

            // Client portal contact for the hospital (no mailbox — the platform issues the sign-in link)
            db.ClientPortalUsers.Add(new ClientPortalUser
            {
                TenantId = tenantId,
                ClientId = hospital.Id,
                FullName = "Dr. Mercy Achieng",
                // Shown in the portal header, so it should look real; .example is reserved and can never receive mail.
                Email = "mercy.achieng@nairobiwesthospital.example",
                Phone = "+254 720 100 200",
                IsActive = true,
                InvitedByUserId = admin.Id,
            });

            // Help centre articles the portal shows
            var category = Add(db.KnowledgeCategories, new KnowledgeCategory { TenantId = tenantId, Name = "Getting started", Description = "Using the service portal.", Icon = "BookOpen", DisplayOrder = 1, IsActive = true, IsPublic = true });
            AddArticle(category, admin.Id, "How to raise a service request", "how-to-raise-a-service-request",
                "Log a fault or request in under a minute.",
                "1. Open **Jobs** and choose **New Request**.\n2. Describe the issue and pick the affected asset if you know it.\n3. Set the priority — use *Urgent* only for safety or service-critical faults.\n\nYou can follow progress and technician updates on the request page.");
            AddArticle(category, admin.Id, "Approving or declining a quotation", "approving-or-declining-a-quotation",
                "What happens when you respond to a quote.",
                "Open **Quotes**, review the lines and total, then choose **Accept Quote** or **Decline**. If you decline, tell us why so we can revise the proposal.");

            // Numbering continues after the seeded documents, so the next job or invoice a visitor creates is unique.
            foreach (var (documentType, prefix) in new[] { (DocumentTypes.WorkOrder, "WO"), (DocumentTypes.Quotation, "QT"), (DocumentTypes.Invoice, "INV"), (DocumentTypes.Receipt, "REC"), (DocumentTypes.Contract, "CON") })
            {
                db.NumberingSettings.Add(new NumberingSetting
                {
                    TenantId = tenantId,
                    DocumentType = documentType,
                    Prefix = prefix,
                    NextNumber = _counters.GetValueOrDefault(documentType) + 1,
                    PaddingLength = 6,
                    ResetFrequency = NumberResetFrequencies.Never,
                    IsActive = true,
                });
            }
        }

        public void ApplyCreatedDates()
        {
            foreach (var (entity, createdAt) in _createdDates)
            {
                entity.CreatedAt = createdAt;
            }
        }

        private static T Add<T>(DbSet<T> set, T entity) where T : class
        {
            set.Add(entity);
            return entity;
        }

        private Technician AddTechnician(Guid? userId, Guid branchId, string name, string skill, string phone) =>
            Add(_db.Technicians, new Technician
            {
                TenantId = tenantId,
                UserId = userId,
                BranchId = branchId,
                FullName = name,
                Email = $"{name.ToLowerInvariant().Replace(' ', '.')}.{DemoWorkspace.Slug}@{DemoWorkspace.NoEmailDomain}",
                Phone = phone,
                SkillCategory = skill,
                IsActive = true,
            });

        private Client AddClient(string name, string type, string contact, string phone, string city) =>
            Add(_db.Clients, new Client
            {
                TenantId = tenantId,
                ClientName = name,
                ClientType = type,
                ContactPerson = contact,
                ContactPhone = phone,
                Phone = phone,
                Location = $"{city}, Kenya",
                IsActive = true,
            });

        private Site AddSite(Client client, string code, string name, string street, string city, string contact) =>
            Add(_db.Sites, new Site
            {
                TenantId = tenantId,
                ClientId = client.Id,
                SiteCode = code,
                SiteName = name,
                SiteType = "Main",
                Status = "Active",
                StreetAddress = street,
                TownCity = city,
                County = city,
                Country = "Kenya",
                ContactPerson = contact,
            });

        private Asset AddAsset(Client client, Site site, Branch branch, string name, string code, string type, string make, string model, int pmDueInDays) =>
            Add(_db.Assets, new Asset
            {
                TenantId = tenantId,
                BranchId = branch.Id,
                ClientId = client.Id,
                SiteId = site.Id,
                AssetName = name,
                AssetCode = code,
                AssetType = type,
                OwnershipType = "ClientOwned",
                Manufacturer = make,
                Model = model,
                SerialNumber = $"{code}-{(Math.Abs(code.GetHashCode()) % 90000) + 10000}",
                InstallationDate = _now.Date.AddYears(-3),
                WarrantyExpiryDate = _now.Date.AddMonths(8),
                LastPmDate = _now.Date.AddDays(pmDueInDays - 30),
                NextPmDate = _now.Date.AddDays(pmDueInDays),
                Status = "Active",
            });

        private Contract AddContract(Client client, string name, ContractType type, BillingModel billing, decimal value, int startedMonthsAgo, Guid ownerUserId) =>
            Add(_db.Contracts, new Contract
            {
                TenantId = tenantId,
                ClientId = client.Id,
                ContractNumber = Next(DocumentTypes.Contract, "CON"),
                Name = name,
                ContractType = type,
                Status = ContractStatus.Active,
                StartDate = _now.Date.AddMonths(-startedMonthsAgo),
                EndDate = _now.Date.AddMonths(12 - startedMonthsAgo),
                DurationMonths = 12,
                BillingModel = billing,
                Currency = "KES",
                ContractValue = value,
                RenewalNoticeDays = 60,
                Description = $"{name}. Scheduled maintenance with priority response.",
                IncludedServices = "Planned preventive maintenance, emergency callouts, remote support",
                ExcludedServices = "Major component replacement, civil works",
                RenewalOwnerUserId = ownerUserId,
                AccountManagerUserId = ownerUserId,
                ActivatedAt = _now.AddMonths(-startedMonthsAgo),
                AutoExpireEnabled = true,
                RenewalRisk = RenewalRisk.Low,
                GracePeriodDays = 7,
                ResponseTimeHours = 4,
                ResolutionTimeHours = 24,
                WorkingHoursPolicy = "BusinessHours",
                PriorityLevel = "High",
            });

        private WorkOrder AddJob(
            Client client, Site site, Asset? asset, Technician? technician, string title, string priority, string status,
            DateTime due, DateTime created, Contract? contract = null, DateTime? started = null, DateTime? completed = null,
            DateTime? closed = null, DateTime? scheduled = null, string? workDone = null, (string Reason, string Note)? hold = null,
            string[]? notes = null, Branch? branch = null, bool isPm = false)
        {
            var job = Add(_db.WorkOrders, new WorkOrder
            {
                TenantId = tenantId,
                // Every job belongs to a branch; branch-scoped staff never see jobs without one.
                BranchId = branch?.Id ?? asset?.BranchId ?? _headOfficeId,
                ClientId = client.Id,
                SiteId = site.Id,
                AssetId = asset?.Id,
                ContractId = contract?.Id,
                WorkOrderNumber = Next(DocumentTypes.WorkOrder, "WO"),
                Title = title,
                Description = $"{title}. Reported by {client.ContactPerson ?? client.ClientName}.",
                Priority = priority,
                Status = status,
                WorkType = isPm ? "PreventiveMaintenance" : "CorrectiveMaintenance",
                IsPreventiveMaintenance = isPm,
                CoverageStatus = contract is null ? null : CoverageStatus.Covered.ToString(),
                AssignmentType = technician is null ? "Unassigned" : "IndividualTechnician",
                AssignedTechnicianId = technician?.Id,
                LeadTechnicianId = technician?.Id,
                DueDate = due.AddHours(17),
                ScheduledStartAt = scheduled,
                WorkStartedAt = started,
                ArrivalAt = started,
                CompletedAt = completed,
                ClosedAt = closed,
                WorkDoneNotes = workDone,
                HoldReason = hold?.Reason,
                HoldNote = hold?.Note,
                HoldStartedAt = hold is null ? null : _now.AddDays(-1),
                NotifyClient = false,
            });
            _createdDates.Add((job, created));

            // The assignment table is the source of truth for who is on a job (the job's technician column only
            // mirrors it), so every assigned job gets one, in the state that matches the job.
            if (technician is not null)
            {
                var onSite = started is not null;
                _db.WorkOrderTechnicianAssignments.Add(new WorkOrderTechnicianAssignment
                {
                    TenantId = tenantId,
                    WorkOrderId = job.Id,
                    TechnicianId = technician.Id,
                    IsLead = true,
                    Status = onSite ? "Arrived" : status == WorkOrderStatuses.Assigned ? "Pending" : "Accepted",
                    AssignedByUserId = _db.Users.Local.First(x => x.Username == "dispatcher").Id,
                    AssignedAt = created.AddHours(1),
                    AcceptedAt = status == WorkOrderStatuses.Assigned ? null : created.AddHours(2),
                    ArrivalAt = started,
                    DepartureAt = completed,
                });
            }

            foreach (var (content, index) in (notes ?? []).Select((note, index) => (note, index)))
            {
                var note = Add(_db.WorkOrderNotes, new WorkOrderNote
                {
                    TenantId = tenantId,
                    WorkOrderId = job.Id,
                    Content = content,
                    IsClientVisible = true,
                    CreatedByUserId = _db.Users.Local.First(x => x.Username == "dispatcher").Id,
                });
                _createdDates.Add((note, (started ?? created).AddHours(index + 1)));
            }

            return job;
        }

        private void AddQuote(Client client, string subject, string status, DateTime date, (string Description, decimal Quantity, string Unit, decimal Rate)[] lines,
            string? rejectionReason = null, string currency = "KES")
        {
            var quote = Add(_db.TenantQuotations, new TenantQuotation
            {
                TenantId = tenantId,
                ClientId = client.Id,
                QuoteNumber = Next(DocumentTypes.Quotation, "QT"),
                Status = status,
                QuoteDate = date,
                ValidUntil = date.AddDays(30),
                Subject = subject,
                Currency = currency,
                RejectionReason = rejectionReason,
            });
            _createdDates.Add((quote, date));

            foreach (var line in lines)
            {
                var total = line.Quantity * line.Rate;
                var tax = Math.Round(total * 0.16m, 2);
                quote.Lines.Add(new TenantQuotationLine { Description = line.Description, Quantity = line.Quantity, Unit = line.Unit, UnitRate = line.Rate, LineTotal = total, TaxRate = 16, GrandTotal = total + tax });
                quote.Subtotal += total;
                quote.TaxAmount += tax;
            }

            quote.TotalAmount = quote.Subtotal + quote.TaxAmount;
        }

        private void AddInvoice(Client client, string status, DateTime date, DateTime due, (string Description, decimal Quantity, string Unit, decimal Rate)[] lines,
            decimal paid = 0, string method = "BankTransfer", string currency = "KES")
        {
            var invoice = Add(_db.TenantInvoices, new TenantInvoice
            {
                TenantId = tenantId,
                ClientId = client.Id,
                InvoiceNumber = Next(DocumentTypes.Invoice, "INV"),
                Status = status,
                InvoiceDate = date,
                DueDate = due,
                Currency = currency,
            });
            _createdDates.Add((invoice, date));

            foreach (var line in lines)
            {
                var total = line.Quantity * line.Rate;
                var tax = Math.Round(total * 0.16m, 2);
                invoice.Lines.Add(new TenantInvoiceLine { Description = line.Description, Quantity = line.Quantity, Unit = line.Unit, UnitRate = line.Rate, LineTotal = total, TaxRate = 16, TaxAmount = tax, GrandTotal = total + tax });
                invoice.Subtotal += total;
                invoice.TaxAmount += tax;
            }

            invoice.TotalAmount = invoice.Subtotal + invoice.TaxAmount;
            invoice.AmountPaid = Math.Min(paid, invoice.TotalAmount);
            invoice.Balance = invoice.TotalAmount - invoice.AmountPaid;
            client.CurrentBalance += invoice.Balance;

            if (invoice.AmountPaid > 0)
            {
                _db.TenantPayments.Add(new TenantPayment
                {
                    TenantId = tenantId,
                    ClientId = client.Id,
                    Invoice = invoice,
                    PaymentNumber = Next(DocumentTypes.Receipt, "REC"),
                    Amount = invoice.AmountPaid,
                    PaymentDate = date.AddDays(12),
                    Method = method,
                    Reference = method == "MPesa" ? "QHX4K2P9LM" : $"TRX-{date:yyMMdd}",
                    Status = "Confirmed",
                });
            }
        }

        private void AddArticle(KnowledgeCategory category, Guid authorId, string title, string slug, string summary, string body) =>
            _db.KnowledgeArticles.Add(new KnowledgeArticle
            {
                TenantId = tenantId,
                CategoryId = category.Id,
                Title = title,
                Slug = slug,
                Summary = summary,
                Body = body,
                Status = "Published",
                Visibility = "Public",
                CreatedByUserId = authorId,
                PublishedAt = _now.AddDays(-30),
            });
    }
}
