using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// The single platform-operated demo workspace used to show prospects the product: a tea factory with a few
/// months of maintenance history. It lives in production next to real tenants, so everything here is pinned
/// to one fixed tenant id and one slug: the reset can only ever touch that tenant, outbound email is disabled
/// for it, and its license is perpetual so no billing or trial worker ever acts on it.
/// </summary>
public static class DemoWorkspace
{
    /// <summary>Also hard-coded in the frontend (<c>shared/demoWorkspace.ts</c>), which shows the demo banner.</summary>
    public static readonly Guid TenantId = Guid.Parse("de300000-0000-4000-8000-000000000001");
    public const string Slug = "ecosys-demo";
    public const string CompanyName = "Kapkoros Tea Factory (Demo)";
    public const string ResetAuditAction = "demo.workspace.reset";

    /// <summary>The same placeholder domain UsersController gives username-only accounts; nothing is ever mailed there.</summary>
    internal const string NoEmailDomain = "no-email.invalid";
}

public sealed record DemoWorkspaceLogin(string Username, string FullName, string Role, string JobTitle, string Shows);

public sealed record DemoWorkspaceStatus(
    bool Exists,
    Guid TenantId,
    string CompanyName,
    string Workspace,
    DateTime? LastResetAt,
    string? LastResetBy,
    int Assets,
    int OpenJobs,
    int ClosedJobs,
    int PmPlans,
    int Vehicles,
    IReadOnlyCollection<DemoWorkspaceLogin> Logins);

/// <summary>Returned once, right after a reset. The password is not stored anywhere in clear text.</summary>
public sealed record DemoWorkspaceResetResult(DemoWorkspaceStatus Status, string Password);

public interface IDemoWorkspaceService
{
    Task<DemoWorkspaceStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates the demo workspace, or wipes and re-seeds it with dates relative to today, and rotates the shared demo password.</summary>
    Task<DemoWorkspaceResetResult> ResetAsync(Guid? actorUserId, string? actorName, CancellationToken cancellationToken = default);

    /// <summary>The user behind one of the demo's logins; anything else is refused.</summary>
    Task<Guid> FindLoginUserIdAsync(string? username, CancellationToken cancellationToken = default);
}

internal sealed partial class DemoWorkspaceService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IFileStorageService fileStorageService,
    IAuditLogService auditLogService,
    IDocumentNumberingService numbering,
    IFactorySettingsService factorySettingsService,
    IAssetClassService assetClassService,
    IFactoryAssetImportService assetImportService,
    IFailureCodeService failureCodeService,
    IMeterReadingService meterReadingService,
    IConditionMonitor conditionMonitor,
    IFactoryPmService pmService,
    IFactoryWorkOrderService workOrderService,
    IComplianceService complianceService,
    IRunningCheckService runningCheckService,
    ISparesService sparesService,
    IFleetService fleetService,
    ILogger<DemoWorkspaceService> logger) : IDemoWorkspaceService
{
    private static readonly Guid TenantId = DemoWorkspace.TenantId;

    /// <summary>
    /// Tenant-scoped tables the reset leaves alone: the license (so the workspace keeps its perpetual term) and
    /// the platform's own commercial records, which are not the demo's data to erase.
    /// </summary>
    private static readonly HashSet<string> PreservedTables = new(StringComparer.Ordinal)
    {
        "tenant_licenses", "platform_invoices", "platform_payments", "platform_quotations", "platform_expenses",
    };

    internal static readonly DemoWorkspaceLogin[] Logins =
    [
        new("admin", "Amina Chebet", AppRoles.Admin, "Factory Engineer", "Everything: setup, users, approvals, costs and reports."),
        new("manager", "Brian Kiprotich", AppRoles.Manager, "Maintenance Manager", "The factory overview, reliability, planning and approvals."),
        new("planner", "Grace Wanjiru", AppRoles.Dispatcher, "Maintenance Planner", "Raising, planning and assigning jobs; the weekly window."),
        new("technician", "Kevin Langat", AppRoles.Technician, "Electrical Technician", "The technician app: today's jobs, running checks and scanning."),
        new("stores", "Joseph Mutai", AppRoles.MaterialsController, "Storekeeper", "Stores and spares: stock, reorders and issuing parts to jobs."),
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
            return new DemoWorkspaceStatus(false, TenantId, DemoWorkspace.CompanyName, DemoWorkspace.Slug, lastReset?.CreatedAt, lastReset?.ActorName, 0, 0, 0, 0, 0, Logins);
        }

        var workOrders = dbContext.WorkOrders.IgnoreQueryFilters().Where(x => x.TenantId == TenantId);
        string[] finished = [WorkOrderStatuses.Closed, WorkOrderStatuses.Cancelled, WorkOrderStatuses.Rejected, WorkOrderStatuses.Verified];
        return new DemoWorkspaceStatus(
            true,
            TenantId,
            tenant.CompanyName,
            tenant.Slug,
            lastReset?.CreatedAt,
            lastReset?.ActorName,
            await dbContext.Assets.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            await workOrders.CountAsync(x => !finished.Contains(x.Status), cancellationToken),
            await workOrders.CountAsync(x => x.Status == WorkOrderStatuses.Closed, cancellationToken),
            await dbContext.PreventiveMaintenancePlans.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            await dbContext.VehicleProfiles.IgnoreQueryFilters().CountAsync(x => x.TenantId == TenantId, cancellationToken),
            Logins);
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
            await dbContext.SaveChangesAsync(cancellationToken);

            await new DemoFactory(this, password).BuildAsync(cancellationToken);

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
        tenant.ContactName = "Amina Chebet";
        tenant.ContactEmail = tenant.Email;
        tenant.Phone = "+254 700 000 000";
        tenant.Country = "Kenya";
        tenant.City = "Kericho";
        tenant.TimeZoneId = "Africa/Nairobi";
        tenant.Currency = "KES";
        tenant.Industry = "Tea processing";
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

    private async Task<List<string>> ReadStoredFilePathsAsync(CancellationToken cancellationToken) =>
        await dbContext.Attachments.IgnoreQueryFilters()
            .Where(x => x.TenantId == TenantId && x.StoragePath != null && x.StoragePath != "")
            .Select(x => x.StoragePath!)
            .ToListAsync(cancellationToken);

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
            .Where(x => !PreservedTables.Contains(x) && x != "tenants")
            .Select(RequireSafeIdentifier)
            .ToList();

        // Self-references (a job's parent job, a node's parent node) block a set-based delete of the same table,
        // so detach them first.
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

        // Rows the deletes removed may still be tracked from earlier in this request.
        dbContext.ChangeTracker.Clear();
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
}
