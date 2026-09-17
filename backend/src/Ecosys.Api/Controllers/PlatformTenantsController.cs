using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAccess")]
[Route("api/platform/tenants")]
public sealed class PlatformTenantsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    ILicenseGuardService licenseGuardService,
    IAuditLogService auditLogService,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService,
    IPasswordHasher<User> passwordHasher,
    IUserPermissionTemplateService permissionTemplateService,
    IUserCredentialDeliveryService credentialDeliveryService,
    IOptions<AuthSecurityOptions> authSecurityOptions,
    ITenantTimeZoneService tenantTimeZoneService,
    IMvpAuthService authService) : ControllerBase
{
    private const int TrialPeriodDays = 14;
    private const int TrialExpiringSoonDays = 3;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantListResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Tenants
            .Include(x => x.TenantLicenses)
            .ThenInclude(x => x.LicensePlan)
            .Where(x => x.Id != PlatformConstants.RootTenantId)
            .OrderBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(x => x.Id).ToArray();
        var userCounts = await dbContext.Users
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive && x.Role != AppRoles.SuperAdmin)
            .GroupBy(x => x.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var branchCounts = await dbContext.Branches
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .GroupBy(x => x.TenantId)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var response = tenants
            .Select(tenant => MapListItem(
                tenant,
                userCounts.GetValueOrDefault(tenant.Id),
                branchCounts.GetValueOrDefault(tenant.Id)))
            .ToList();

        return Ok(response);
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "PlatformAdminAccess")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> Create([FromBody] UpsertPlatformTenantRequest request, CancellationToken cancellationToken)
    {
        var adminProfile = BuildInitialAdminProfile(request);
        ValidateInitialAdminRequest(adminProfile);
        await ValidateInitialAdminAvailabilityAsync(adminProfile.Email, cancellationToken);

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        var tenant = new Tenant { CreatedByUserId = tenantContext.GetRequiredUserId() };
        dbContext.Tenants.Add(tenant);
        await ApplyTenantAsync(tenant, request, cancellationToken);
        await EnsureTenantSupportRecordsAsync(tenant, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var admin = await CreateInitialAdminAsync(tenant, adminProfile, cancellationToken);
        var invitation = await PasswordLinkTokens.IssueAsync(dbContext, admin, PasswordLinkPurposes.Invitation,
            authSecurityOptions.Value, cancellationToken);
        await WriteAuditAsync(tenant, "TenantCreated", $"Tenant '{tenant.Name}' was created.", cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        // Delivery is attempted only after the tenant and invitation have committed.
        UserCredentialDeliveryResult initialAdminDelivery;
        try
        {
            initialAdminDelivery = await credentialDeliveryService.SendPasswordResetLinkAsync(tenant.Id, admin,
                new PasswordResetLinkDeliveryRequest(credentialDeliveryService.BuildInviteUrl(invitation.Token),
                    invitation.ExpiresAt, PasswordLinkDeliveryPurpose.AccountSetup), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            initialAdminDelivery = new UserCredentialDeliveryResult(false, "DeliveryFailed",
                "The workspace and invitation are saved. Email delivery failed; resend the invitation from Users.");
            await WriteAuditAsync(tenant, "InvitationDeliveryFailed", "Initial administrator invitation delivery failed.", cancellationToken);
        }
        if (initialAdminDelivery.Success)
        {
            admin.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        try { await TryQueueTenantOnboardingEmailAsync(tenant, adminProfile, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { await WriteAuditAsync(tenant, "OnboardingEmailQueueFailed", "Optional welcome email could not be queued.", cancellationToken); }

        var response = await BuildDetailResponseAsync(
            tenant,
            cancellationToken,
            initialAdminDelivery.Success,
            initialAdminDelivery.Message);
        return CreatedAtAction(nameof(Get), new { tenantId = tenant.Id }, response);
    }

    [HttpPut("{tenantId:guid}")]
    [Authorize(Policy = "PlatformAdminAccess")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> Update(Guid tenantId, [FromBody] UpsertPlatformTenantRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();

        await ApplyTenantAsync(tenant, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(tenant, "platform.tenant.updated", $"Tenant '{tenant.Name}' was updated.", cancellationToken);

        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPatch("{tenantId:guid}/status")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> UpdateStatus(Guid tenantId, [FromBody] UpdatePlatformTenantStatusRequest request, CancellationToken cancellationToken)
    {
        var status = NormalizeTenantStatus(request.Status);
        if ((status is PlatformTenantStatuses.Suspended or PlatformTenantStatuses.Inactive) && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new BusinessRuleException("A reason is required when suspending or deactivating a tenant.");
        }

        var tenant = await SetTenantStatusAsync(tenantId, status, request.Reason, cancellationToken);
        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/activate")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> Activate(Guid tenantId, [FromBody] ActivateTenantLicenseRequest request, CancellationToken cancellationToken)
    {
        ValidateLicenseTerm(request.StartsAt, request.ExpiresAt, request.IsPerpetual, request.NextBillingDate, request.GracePeriodDays);
        var tenant = await SetTenantStatusAsync(tenantId, PlatformTenantStatuses.Active, null, cancellationToken, request);
        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/license/renew")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> RenewLicense(Guid tenantId, [FromBody] RenewTenantLicenseRequest request, CancellationToken cancellationToken)
    {
        ValidateLicenseTerm(request.StartsAt, request.ExpiresAt, request.IsPerpetual, request.NextBillingDate, request.GracePeriodDays);

        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);

        license.StartsAt = EnsureUtc(request.StartsAt);
        license.ExpiresAt = EnsureUtc(request.ExpiresAt);
        license.IsPerpetual = request.IsPerpetual;
        license.BillingCycle = BillingCycles.Normalize(request.BillingCycle);
        license.NextBillingDate = EnsureUtc(request.NextBillingDate);
        if (request.GracePeriodDays.HasValue)
        {
            license.GracePeriodDays = request.GracePeriodDays.Value;
        }
        license.CancelledAt = null;
        license.SuspendedAt = null;
        license.OverdueWarningSentAt = null;
        license.Status = LicenseStatuses.Active;

        if (request.PlanId.HasValue)
        {
            var selectedPlan = await LoadActivePlanAsync(request.PlanId.Value, cancellationToken);
            license.LicensePlanId = selectedPlan.Id;
            license.LicensePlan = selectedPlan;
            tenant.PlanName = selectedPlan.DisplayName;
        }

        tenant.Status = PlatformTenantStatuses.Active;
        tenant.IsActive = true;
        tenant.SuspendedAt = null;
        tenant.DeactivatedAt = null;
        tenant.DeactivatedByUserId = null;
        tenant.DeactivationReason = null;
        tenant.LicenseStatus = LicenseStatuses.Active;
        tenant.SubscriptionStartsAt = license.StartsAt;
        tenant.SubscriptionEndsAt = license.ExpiresAt;
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();

        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            tenant,
            "platform.tenant.license.renewed",
            $"License renewed: {DescribeTerm(license)}.",
            cancellationToken);

        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/license/extend")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> ExtendLicense(Guid tenantId, [FromBody] ExtendTenantLicenseRequest request, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);

        if (license.IsPerpetual)
        {
            throw new BusinessRuleException("This tenant's license is perpetual and has no expiry to extend.");
        }

        var newExpiresAt = EnsureUtc(request.NewExpiresAt);
        var currentExpiry = license.ExpiresAt ?? license.StartsAt;
        if (newExpiresAt <= currentExpiry)
        {
            throw new BusinessRuleException("The new expiry must be after the current expiry.");
        }

        var previousExpiry = license.ExpiresAt;
        license.ExpiresAt = newExpiresAt;
        tenant.SubscriptionEndsAt = newExpiresAt;
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();

        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            tenant,
            "platform.tenant.license.extended",
            $"License expiry extended from {DescribeDate(previousExpiry)} to {newExpiresAt:yyyy-MM-dd}.",
            cancellationToken);

        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/license/reactivate")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> ReactivateLicense(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        if (tenant.Status != PlatformTenantStatuses.Suspended)
        {
            throw new BusinessRuleException("Only a manually suspended tenant can be reactivated. Use Renew if the license itself has expired.");
        }

        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var effectiveExpiry = license.ExpiresAt ?? license.TrialEndsAt;
        if (!license.IsPerpetual && effectiveExpiry.HasValue && effectiveExpiry.Value <= now)
        {
            throw new BusinessRuleException("This tenant's license has expired. Renew or extend it before reactivating.");
        }

        var reactivated = await SetTenantStatusAsync(tenantId, PlatformTenantStatuses.Active, null, cancellationToken);
        return Ok(await BuildDetailResponseAsync(reactivated, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/suspend")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> Suspend(Guid tenantId, [FromBody] UpdatePlatformTenantStatusReasonRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await SetTenantStatusAsync(tenantId, PlatformTenantStatuses.Suspended, request?.Reason, cancellationToken);
        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/deactivate")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> DeactivatePost(Guid tenantId, [FromBody] UpdatePlatformTenantStatusReasonRequest? request, CancellationToken cancellationToken)
    {
        var tenant = await SetTenantStatusAsync(tenantId, PlatformTenantStatuses.Inactive, request?.Reason, cancellationToken);
        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpDelete("{tenantId:guid}")]
    public async Task<IActionResult> Deactivate(Guid tenantId, CancellationToken cancellationToken)
    {
        await SetTenantStatusAsync(tenantId, PlatformTenantStatuses.Inactive, "Tenant deactivated by platform owner.", cancellationToken);
        return NoContent();
    }

    [HttpGet("{tenantId:guid}/summary")]
    public async Task<ActionResult<PlatformTenantSummaryResponse>> GetSummary(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var summary = await BuildSummaryResponseAsync(tenant, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("{tenantId:guid}/extend-trial")]
    public async Task<ActionResult<PlatformTenantDetailResponse>> ExtendTrial(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        if (tenant.TrialExtensionUsed)
        {
            throw new BusinessRuleException("This tenant has already used the trial extension.");
        }

        var now = DateTime.UtcNow;
        var currentEnd = EnsureUtc(tenant.TrialEndsAt);
        var extensionBase = currentEnd.HasValue && currentEnd.Value > now
            ? currentEnd.Value
            : now;
        var extendedEnd = extensionBase.AddDays(TrialPeriodDays);
        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);

        tenant.TrialStartsAt ??= tenant.CreatedAt == default ? now : EnsureUtc(tenant.CreatedAt);
        tenant.TrialEndsAt = extendedEnd;
        tenant.TrialExtensionUsed = true;
        tenant.TrialExtendedAt = now;
        tenant.TrialExtendedByUserId = tenantContext.GetRequiredUserId();
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();
        tenant.UpdatedAt = now;

        if (tenant.Status != PlatformTenantStatuses.Suspended)
        {
            tenant.Status = PlatformTenantStatuses.Trial;
            tenant.IsActive = true;
            tenant.SuspendedAt = null;
            tenant.DeactivatedAt = null;
            tenant.DeactivatedByUserId = null;
            tenant.DeactivationReason = null;
        }

        tenant.LicenseStatus = tenant.Status == PlatformTenantStatuses.Suspended
            ? LicenseStatuses.Suspended
            : LicenseStatuses.Trial;

        license.Status = tenant.LicenseStatus;
        license.TrialEndsAt = extendedEnd;
        license.ExpiresAt = extendedEnd;
        license.SuspendedAt = tenant.Status == PlatformTenantStatuses.Suspended ? tenant.SuspendedAt : null;
        license.CancelledAt = null;
        license.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        await WriteAuditAsync(
            tenant,
            "platform.tenant.trial-extended",
            $"Tenant '{tenant.Name}' trial extended by 14 days until {extendedEnd:yyyy-MM-dd}.",
            cancellationToken);

        return Ok(await BuildDetailResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{tenantId:guid}/impersonate")]
    [Authorize(Policy = "PlatformAdminAccess")]
    public async Task<ActionResult<TenantImpersonationResponse>> Impersonate(
        Guid tenantId,
        [FromBody] ImpersonateTenantRequest? request,
        CancellationToken cancellationToken)
    {
        var actingUserId = tenantContext.GetRequiredUserId();
        var actingUser = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == actingUserId, cancellationToken)
            ?? throw new NotFoundException("Acting platform user was not found.");

        // EC-028: the reason is what makes an impersonation record answer "why", which is the whole
        // point of auditing it. The body is optional so existing callers that post nothing still
        // work — the service records "No reason was given." in that case rather than failing.
        var result = await authService.ImpersonateTenantAdminAsync(
            tenantId, actingUserId, actingUser.FullName, request?.Reason, cancellationToken);

        return Ok(new TenantImpersonationResponse(
            result.Token,
            new LoginUserDto(result.UserId, result.FullName, result.Email, result.Role, result.JobTitle, null, false, MapPermissions(result.Permissions)),
            new LoginTenantDto(
                result.TenantId,
                result.CompanyName,
                result.Country,
                result.Industry,
                result.LogoUrl,
                result.ShowPoweredByEcosys,
                await tenantTimeZoneService.GetTimeZoneIdAsync(result.TenantId, cancellationToken)),
            result.ImpersonatedByUserId,
            result.ImpersonatedByName,
            result.StartedAt));
    }

    [HttpGet("{tenantId:guid}/audit-logs")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantAuditLogResponse>>> GetAuditLogs(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var auditLogs = await dbContext.AuditLogs
            .Include(x => x.User)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new PlatformTenantAuditLogResponse(
                x.Id,
                x.Action,
                x.User != null ? x.User.FullName : "Platform Owner",
                x.Details,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(auditLogs);
    }

    [HttpGet("{tenantId:guid}/branding")]
    public async Task<ActionResult<PlatformTenantBrandingResponse>> GetBranding(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        return Ok(new PlatformTenantBrandingResponse(tenant.CompanyName, tenant.LogoUrl, tenant.PrimaryColor, tenant.SecondaryColor, tenant.ShowPoweredByEcosys));
    }

    [HttpGet("{tenantId:guid}/users")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantUserResponse>>> GetUsers(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var users = await dbContext.Users
            .Where(x => x.TenantId == tenantId && x.Role != AppRoles.SuperAdmin)
            .OrderBy(x => x.FullName)
            .Select(x => new PlatformTenantUserResponse(
                x.Id,
                x.FullName,
                x.Email,
                x.Role,
                x.IsActive,
                x.JobTitle,
                x.CreatedAt,
                x.LockedOutUntil,
                x.MustChangePassword,
                x.LastCredentialSentAt,
                dbContext.UserSessions
                    .Where(session => session.UserId == x.Id)
                    .Max(session => (DateTime?)session.LoginAt)))
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    /// <summary>
    /// Creates an additional workspace user — typically a second or third tenant admin, since tenant
    /// creation only ever seeds one. The account lives in the tenant and can only hold a tenant role;
    /// platform staff are created separately under /api/platform/users.
    /// </summary>
    [Authorize(Policy = "PlatformAdminAccess")]
    [HttpPost("{tenantId:guid}/users")]
    public async Task<ActionResult<PlatformTenantUserCreatedResponse>> CreateWorkspaceUser(
        Guid tenantId,
        [FromBody] CreatePlatformTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var fullName = NormalizeRequired(request.FullName, "Full name is required.");
        var email = NormalizeEmail(request.Email)
            ?? throw new BusinessRuleException("A valid email address is required.");
        var role = NormalizeWorkspaceRole(request.Role);

        // Seats are a tenant-level entitlement — creating from the platform side must not bypass it.
        await licenseGuardService.EnsureCanCreateUserAsync(tenant.Id, cancellationToken);

        var emailInUse = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower(), cancellationToken);
        if (emailInUse)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        var isAdmin = string.Equals(role, AppRoles.Admin, StringComparison.Ordinal);
        var permissions = isAdmin
            ? permissionTemplateService.GetFullTenantPermissions()
            : permissionTemplateService.GetDefaultPermissions(role, NormalizeOptional(request.JobTitle));

        var user = new User
        {
            TenantId = tenant.Id,
            FullName = fullName,
            Email = email,
            Phone = NormalizeOptional(request.Phone),
            Role = role,
            JobTitle = NormalizeOptional(request.JobTitle) ?? (isAdmin ? "Tenant workspace admin" : null),
            IsActive = true,
            HasAllBranchAccess = isAdmin,
            MustChangePassword = true,
            Permission = ToEntity(permissions)
        };
        user.PasswordHash = passwordHasher.HashPassword(user, GenerateTemporaryPassword());

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var delivery = await SendWorkspaceUserSetupAsync(tenant.Id, user, cancellationToken);
        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            tenant.Id,
            tenantContext.GetRequiredUserId(),
            "User created",
            nameof(User),
            user.Id.ToString(),
            $"Platform created workspace {role.ToLowerInvariant()} '{user.Email}' for tenant '{tenant.CompanyName}'.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        var message = delivery.Success
            ? $"Account setup link sent to {user.Email}."
            : "The account was created, but its setup link could not be sent. Resend it once email delivery is working.";

        return Ok(new PlatformTenantUserCreatedResponse(
            new PlatformTenantUserResponse(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.IsActive,
                user.JobTitle,
                user.CreatedAt,
                user.LockedOutUntil,
                user.MustChangePassword,
                user.LastCredentialSentAt,
                LastLoginAt: null),
            delivery.Success,
            delivery.Status,
            message));
    }

    /// <summary>
    /// Canonicalises against the tenant role list only, so a platform caller cannot drop a platform
    /// role name into a tenant workspace and have it inherit platform meaning at login.
    /// </summary>
    private static string NormalizeWorkspaceRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return AppRoles.Admin;
        }

        var trimmed = role.Trim();
        if (string.Equals(trimmed, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("SuperAdmin is a platform role and cannot be assigned inside a tenant workspace.");
        }

        var match = Array.Find(AppRoles.TenantRoles, candidate => string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new BusinessRuleException($"Role must be one of: {string.Join(", ", AppRoles.TenantRoles)}.");
    }

    /// <summary>
    /// Re-issues a workspace account-setup link. This is the only way to recover a tenant admin whose
    /// first-login invitation lapsed: they cannot sign in to request one themselves, and the tenant-side
    /// resend endpoint requires an admin already inside the workspace.
    /// </summary>
    [Authorize(Policy = "PlatformAdminAccess")]
    [HttpPost("{tenantId:guid}/users/{userId:guid}/resend-invitation")]
    public async Task<ActionResult<PlatformTenantCredentialDeliveryResponse>> ResendWorkspaceUserInvitation(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.Role != AppRoles.SuperAdmin, cancellationToken)
            ?? throw new NotFoundException("Workspace user was not found for this tenant.");

        if (!user.IsActive)
        {
            throw new BusinessRuleException("This user is deactivated. Reactivate the account before resending its setup link.");
        }

        var delivery = await SendWorkspaceUserSetupAsync(tenant.Id, user, cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            tenant.Id,
            tenantContext.GetRequiredUserId(),
            delivery.Success ? "User credentials sent" : "User credentials send failed",
            nameof(User),
            user.Id.ToString(),
            delivery.Success
                ? $"Workspace account setup link resent to '{user.Email}'."
                : $"Workspace account setup link to '{user.Email}' could not be delivered ({delivery.Status}).",
            severity: "Warning",
            cancellationToken: cancellationToken);

        var message = delivery.Success
            ? $"Account setup link sent to {user.Email}."
            : "Account setup link could not be sent. Check the platform email settings and try again.";

        return Ok(new PlatformTenantCredentialDeliveryResponse(
            user.Id,
            user.Email,
            delivery.Success,
            delivery.Status,
            message,
            user.LastCredentialSentAt));
    }

    /// <summary>
    /// Forces a password reset for a workspace user who has already completed account setup. This is
    /// the recovery path for a tenant admin who is locked out or has forgotten their password when the
    /// tenant has no other admin available to trigger the tenant-side reset — resend-invitation above
    /// only covers a user still pending their first sign-in and would otherwise be the only lever.
    /// </summary>
    [Authorize(Policy = "PlatformAdminAccess")]
    [HttpPost("{tenantId:guid}/users/{userId:guid}/reset-password")]
    public async Task<ActionResult<PlatformTenantCredentialDeliveryResponse>> ResetWorkspaceUserPassword(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.Id == userId && x.TenantId == tenantId && x.Role != AppRoles.SuperAdmin, cancellationToken)
            ?? throw new NotFoundException("Workspace user was not found for this tenant.");

        if (!user.IsActive || user.MustChangePassword)
        {
            throw new BusinessRuleException("Password reset links can only be sent to workspace users who have already completed account setup. Use resend-invitation for a user still pending activation.");
        }

        var issued = await PasswordLinkTokens.IssueAsync(dbContext, user, PasswordLinkPurposes.PasswordReset,
            authSecurityOptions.Value, cancellationToken);

        var delivery = await credentialDeliveryService.SendPasswordResetLinkAsync(
            tenant.Id,
            user,
            new PasswordResetLinkDeliveryRequest(
                credentialDeliveryService.BuildResetPasswordUrl(issued.Token),
                issued.ExpiresAt,
                PasswordLinkDeliveryPurpose.PasswordReset),
            cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            tenant.Id,
            tenantContext.GetRequiredUserId(),
            delivery.Success ? "User password reset link sent" : "User password reset link send failed",
            nameof(User),
            user.Id.ToString(),
            delivery.Success
                ? $"Password reset link sent to workspace user '{user.Email}'."
                : $"Password reset link to '{user.Email}' could not be delivered ({delivery.Status}).",
            severity: "Warning",
            cancellationToken: cancellationToken);

        var message = delivery.Success
            ? $"Password reset link sent to {user.Email}."
            : "Password reset link could not be sent. Check the platform email settings and try again.";

        return Ok(new PlatformTenantCredentialDeliveryResponse(
            user.Id,
            user.Email,
            delivery.Success,
            delivery.Status,
            message,
            user.LastCredentialSentAt));
    }

    /// <summary>
    /// Shared delivery path for both a freshly created workspace user and a resent invitation — issues a
    /// fresh account-setup token via the shared token store and hands it to the credential delivery
    /// service, matching the pattern used for the initial tenant admin invitation in <see cref="Create"/>.
    /// </summary>
    private async Task<UserCredentialDeliveryResult> SendWorkspaceUserSetupAsync(Guid tenantId, User user, CancellationToken cancellationToken)
    {
        var issued = await PasswordLinkTokens.IssueAsync(dbContext, user, PasswordLinkPurposes.Invitation,
            authSecurityOptions.Value, cancellationToken);

        return await credentialDeliveryService.SendPasswordResetLinkAsync(
            tenantId,
            user,
            new PasswordResetLinkDeliveryRequest(
                credentialDeliveryService.BuildInviteUrl(issued.Token),
                issued.ExpiresAt,
                PasswordLinkDeliveryPurpose.AccountSetup),
            cancellationToken);
    }

    [HttpGet("{tenantId:guid}/numbering")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantNumberingResponse>>> GetNumbering(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var rules = await dbContext.NumberingSettings
            .Where(x => x.TenantId == tenantId && x.BranchId == null)
            .OrderBy(x => x.DocumentType)
            .Select(x => new PlatformTenantNumberingResponse(x.Id, x.DocumentType, x.Prefix, x.Suffix, x.NextNumber, x.PaddingLength, x.ResetFrequency, x.IsActive, x.LastResetAt))
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    [HttpGet("{tenantId:guid}/document-designs")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantDocumentDesignResponse>>> GetDocumentDesigns(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var overrides = PdfTemplateHelper.ParseDesigns(tenant.DocumentDesignsJson);

        var entries = overrides.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(documentType => new PlatformTenantDocumentDesignResponse(documentType, true))
            .ToList();

        return Ok(entries);
    }

    [HttpGet("{tenantId:guid}/finance-summary")]
    public async Task<ActionResult<PlatformTenantFinanceSummaryResponse>> GetFinanceSummary(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var invoices = await dbContext.TenantInvoices
            .Where(x => x.TenantId == tenantId && x.Status != "Void")
            .ToListAsync(cancellationToken);

        var totalInvoiced = invoices.Sum(x => x.TotalAmount);
        var totalPaid = invoices.Sum(x => x.AmountPaid);
        var outstandingBalance = invoices.Sum(x => x.Balance);
        var overdueCount = invoices.Count(x => x.Balance > 0 && x.DueDate < DateTime.UtcNow);

        return Ok(new PlatformTenantFinanceSummaryResponse(invoices.Count, totalInvoiced, totalPaid, outstandingBalance, overdueCount));
    }

    [HttpGet("{tenantId:guid}/module-settings")]
    public async Task<ActionResult<IReadOnlyCollection<PlatformTenantModuleSettingResponse>>> GetModuleSettings(Guid tenantId, CancellationToken cancellationToken)
    {
        _ = await LoadManagedTenantAsync(tenantId, cancellationToken);

        var settings = await dbContext.TenantModuleSettings
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.ModuleKey)
            .Select(x => new PlatformTenantModuleSettingResponse(x.ModuleKey, x.UpdatedAt ?? x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    private async Task<Tenant> LoadManagedTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await dbContext.Tenants
            .Include(x => x.TenantLicenses)
            .ThenInclude(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.Id == tenantId && x.Id != PlatformConstants.RootTenantId, cancellationToken)
        ?? throw new NotFoundException("Tenant was not found.");

    private async Task ApplyTenantAsync(Tenant tenant, UpsertPlatformTenantRequest request, CancellationToken cancellationToken)
    {
        var isNewTenant = dbContext.Entry(tenant).State == EntityState.Added;
        var now = DateTime.UtcNow;
        var todayUtc = StartOfUtcDay(now);
        var trimmedName = NormalizeRequired(request.Name ?? request.CompanyName, "Company name is required.");
        var slug = !isNewTenant && string.IsNullOrWhiteSpace(request.Slug) ? tenant.Slug
            : await NormalizeAndValidateSlugAsync(request.Slug, trimmedName, tenant.Id, cancellationToken);
        var normalizedStatus = NormalizeTenantStatus(request.Status);
        var normalizedLicenseStatus = NormalizeLicenseStatus(request.LicenseStatus);
        var normalizedCompanyEmail = NormalizeEmail(request.CompanyEmail) ?? NormalizeEmail(request.ContactEmail);
        var normalizedContactEmail = NormalizeEmail(request.PrimaryContactEmail ?? request.ContactEmail) ?? normalizedCompanyEmail;
        var normalizedTrialEndsAt = EnsureUtc(request.TrialEndsAt);
        var normalizedSubscriptionEndsAt = EnsureUtc(request.SubscriptionEndsAt);
        var normalizedStartsAt = EnsureUtc(request.StartsAt);
        var normalizedExpiresAt = EnsureUtc(request.ExpiresAt);

        ValidatePositive(request.MaxUsers, "Max users");
        ValidatePositive(request.MaxBranches, "Max branches");
        ValidateNotInPast(normalizedTrialEndsAt, todayUtc, "Trial end date");
        ValidateNotInPast(normalizedSubscriptionEndsAt, todayUtc, "Subscription end date");
        ValidateNotInPast(normalizedStartsAt, todayUtc, "License start date");
        ValidateNotInPast(normalizedExpiresAt, todayUtc, "License expiry date");

        var companyInUse = await dbContext.Tenants.AnyAsync(
            x => x.Id != tenant.Id
                && x.Id != PlatformConstants.RootTenantId
                && x.CompanyName.ToLower() == trimmedName.ToLower(),
            cancellationToken);
        if (companyInUse)
        {
            throw new BusinessRuleException("A tenant with this company name already exists.");
        }

        LicensePlan? plan = null;
        var requestedPlanName = NormalizeOptional(request.PlanCode) ?? NormalizeOptional(request.PlanName) ?? (isNewTenant ? "suite" : null);
        if (!string.IsNullOrWhiteSpace(requestedPlanName))
        {
            plan = await dbContext.LicensePlans
                .SingleOrDefaultAsync(
                    x => x.IsActive && (x.PlanCode.ToLower() == requestedPlanName.ToLower() || x.DisplayName.ToLower() == requestedPlanName.ToLower()),
                    cancellationToken)
                ?? throw new BusinessRuleException("Plan name is invalid.");
        }

        tenant.Name = trimmedName;
        tenant.CompanyName = trimmedName;
        tenant.Slug = slug;
        tenant.ContactName = NormalizeOptional(request.PrimaryContactName ?? request.ContactName);
        tenant.ContactEmail = normalizedContactEmail;
        tenant.ContactPhone = NormalizeOptional(request.PrimaryContactPhone ?? request.ContactPhone);
        tenant.Email = normalizedCompanyEmail ?? NormalizeOptional(tenant.Email) ?? NormalizeEmail(request.AdminEmail) ?? TenantEmailIdentity.Address(slug);
        tenant.Phone = NormalizeOptional(request.CompanyPhone) ?? tenant.Phone;
        tenant.Country = NormalizeOptional(request.Country) ?? NormalizeOptional(tenant.Country) ?? "Kenya";
        tenant.TimeZoneId = TenantTimeService.NormalizeTimeZone(NormalizeOptional(request.TimeZoneId) ?? (isNewTenant ? null : tenant.TimeZoneId), tenant.Country);
        if (isNewTenant) tenant.Currency = TenantEmailIdentity.DefaultCurrency(tenant.Country);
        var emailIdentity = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);
        if (emailIdentity is not null && emailIdentity.ManagedEmailAddress is not null)
        {
            emailIdentity.ManagedEmailAddress = TenantEmailIdentity.Address(slug);
            emailIdentity.SenderAddress = emailIdentity.ManagedEmailAddress;
        }
        tenant.Industry = NormalizeOptional(request.Industry) ?? tenant.Industry;
        tenant.County = NormalizeOptional(request.County);
        tenant.City = NormalizeOptional(request.City);
        tenant.Address = NormalizeOptional(request.Address);
        tenant.TaxPin = NormalizeOptional(request.TaxPin);
        tenant.PlanName = plan?.DisplayName ?? requestedPlanName;
        tenant.MaxUsers = request.MaxUsers;
        tenant.MaxBranches = request.MaxBranches;
        tenant.CreatedByUserId ??= tenantContext.GetRequiredUserId();
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();
        tenant.CreatedAt = EnsureUtc(tenant.CreatedAt);
        tenant.UpdatedAt = EnsureUtc(tenant.UpdatedAt);
        tenant.TrialStartsAt = EnsureUtc(tenant.TrialStartsAt);
        tenant.TrialEndsAt = EnsureUtc(tenant.TrialEndsAt);
        tenant.TrialExtendedAt = EnsureUtc(tenant.TrialExtendedAt);
        tenant.SubscriptionStartsAt = EnsureUtc(tenant.SubscriptionStartsAt);
        tenant.SubscriptionEndsAt = EnsureUtc(tenant.SubscriptionEndsAt);
        tenant.SuspendedAt = EnsureUtc(tenant.SuspendedAt);
        tenant.DeactivatedAt = EnsureUtc(tenant.DeactivatedAt);

        var trialStartsAt = isNewTenant
            ? now
            : tenant.TrialStartsAt ?? (tenant.CreatedAt == default ? now : EnsureUtc(tenant.CreatedAt));
        var trialEndsAt = isNewTenant
            ? trialStartsAt.AddDays(TrialPeriodDays)
            : normalizedTrialEndsAt ?? tenant.TrialEndsAt;

        if (isNewTenant)
        {
            normalizedStatus = normalizedStatus is PlatformTenantStatuses.Suspended or PlatformTenantStatuses.Inactive
                ? normalizedStatus
                : PlatformTenantStatuses.Trial;
            normalizedLicenseStatus = normalizedStatus switch
            {
                PlatformTenantStatuses.Suspended => LicenseStatuses.Suspended,
                PlatformTenantStatuses.Inactive => LicenseStatuses.Cancelled,
                _ => LicenseStatuses.Trial
            };
            tenant.TrialExtensionUsed = false;
            tenant.TrialExtendedAt = null;
            tenant.TrialExtendedByUserId = null;
        }

        tenant.Status = normalizedStatus;
        tenant.LicenseStatus = normalizedLicenseStatus;
        tenant.TrialStartsAt = trialStartsAt;
        tenant.TrialEndsAt = trialEndsAt;
        tenant.SubscriptionStartsAt = normalizedStartsAt ?? tenant.SubscriptionStartsAt ?? trialStartsAt;
        tenant.SubscriptionEndsAt = normalizedSubscriptionEndsAt;
        tenant.IsActive = ComputeIsActive(normalizedStatus, normalizedLicenseStatus);
        tenant.SuspendedAt = normalizedStatus == PlatformTenantStatuses.Suspended ? now : null;
        tenant.DeactivatedAt = normalizedStatus == PlatformTenantStatuses.Inactive ? now : null;
        tenant.DeactivatedByUserId = normalizedStatus == PlatformTenantStatuses.Inactive ? tenantContext.GetRequiredUserId() : null;
        tenant.DeactivationReason = normalizedStatus == PlatformTenantStatuses.Inactive ? tenant.DeactivationReason : null;
        tenant.SubscriptionStartsAt = EnsureUtc(tenant.SubscriptionStartsAt);

        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);
        if (plan is not null)
        {
            license.LicensePlanId = plan.Id;
            license.LicensePlan = plan;
            tenant.PlanName = plan.DisplayName;
        }

        license.Status = normalizedLicenseStatus;
        license.StartsAt = normalizedStartsAt ?? tenant.SubscriptionStartsAt ?? license.StartsAt;
        license.TrialEndsAt = trialEndsAt;
        license.ExpiresAt = normalizedExpiresAt ?? normalizedSubscriptionEndsAt ?? trialEndsAt;
        license.MaxUsersOverride = request.MaxUsers;
        license.MaxBranchesOverride = request.MaxBranches;
        license.SuspendedAt = normalizedLicenseStatus == LicenseStatuses.Suspended ? now : null;
        license.CreatedAt = EnsureUtc(license.CreatedAt);
        license.UpdatedAt = EnsureUtc(license.UpdatedAt);
        license.StartsAt = EnsureUtc(license.StartsAt);
        license.TrialEndsAt = EnsureUtc(license.TrialEndsAt);
        license.ExpiresAt = EnsureUtc(license.ExpiresAt);
        license.SuspendedAt = EnsureUtc(license.SuspendedAt);
        license.CancelledAt = EnsureUtc(license.CancelledAt);

        if (string.IsNullOrWhiteSpace(tenant.PlanName) && license.LicensePlan is not null)
        {
            tenant.PlanName = license.LicensePlan.DisplayName;
        }
    }

    private async Task<string> NormalizeAndValidateSlugAsync(string? requestedSlug, string companyName, Guid currentTenantId, CancellationToken cancellationToken)
    {
        var slug = TenantEmailIdentity.NormalizeSlug(string.IsNullOrWhiteSpace(requestedSlug) ? companyName : requestedSlug, string.IsNullOrWhiteSpace(requestedSlug));
        var address = TenantEmailIdentity.Address(slug);
        var exists = await dbContext.Tenants.AnyAsync(x => x.Slug.ToLower() == slug && x.Id != currentTenantId, cancellationToken)
            || await dbContext.EmailIntakeSettings.AnyAsync(x => x.TenantId != currentTenantId && x.IntakeEmailAddress != null && x.IntakeEmailAddress.ToLower() == address, cancellationToken)
            || await dbContext.EmailSettings.AnyAsync(x => x.TenantId != currentTenantId &&
                (x.ManagedEmailAddress == address || x.SenderAddress.ToLower() == address), cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("This workspace URL name is already in use. Please choose another.");
        }

        return slug;
    }

    private async Task EnsureTenantSupportRecordsAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);

        if (!await dbContext.EmailSettings.AnyAsync(x => x.TenantId == tenant.Id, cancellationToken))
        {
            dbContext.EmailSettings.Add(TenantEmailIdentity.Create(tenant));
        }

        if (!await dbContext.NotificationSettings.AnyAsync(x => x.TenantId == tenant.Id, cancellationToken))
        {
            dbContext.NotificationSettings.Add(new NotificationSetting { TenantId = tenant.Id });
        }

        if (!await dbContext.MonitoringSettings.AnyAsync(x => x.TenantId == tenant.Id, cancellationToken))
        {
            dbContext.MonitoringSettings.Add(new MonitoringSetting { TenantId = tenant.Id });
        }

        if (!await dbContext.TenantSecurityPolicies.AnyAsync(x => x.TenantId == tenant.Id, cancellationToken))
        {
            dbContext.TenantSecurityPolicies.Add(new TenantSecurityPolicy { TenantId = tenant.Id });
        }
    }

    private async Task ValidateInitialAdminAvailabilityAsync(string adminEmail, CancellationToken cancellationToken)
    {
        var normalizedAdminEmail = NormalizeEmail(adminEmail)!;
        var emailInUse = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == normalizedAdminEmail.ToLower(), cancellationToken);
        if (emailInUse)
        {
            throw new BusinessRuleException("Admin email is already in use.");
        }
    }

    private async Task<User> CreateInitialAdminAsync(Tenant tenant, InitialAdminProfile adminProfile, CancellationToken cancellationToken)
    {
        var adminEmail = NormalizeEmail(adminProfile.Email)!;
        var password = GenerateTemporaryPassword();
        var permissions = permissionTemplateService.GetFullTenantPermissions();
        var adminUser = new User
        {
            TenantId = tenant.Id,
            FullName = NormalizeRequired(adminProfile.FullName, "Admin full name is required when creating a default admin."),
            Email = adminEmail,
            Phone = NormalizeOptional(adminProfile.Phone),
            Role = AppRoles.Admin,
            JobTitle = "Tenant workspace admin",
            IsActive = true,
            HasAllBranchAccess = true,
            MustChangePassword = true,
            Permission = ToEntity(permissions),
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, password);

        dbContext.Users.Add(adminUser);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(tenant.Id, tenantContext.GetRequiredUserId(), "User created", nameof(User),
            adminUser.Id.ToString(), $"Created initial tenant workspace admin '{adminUser.Email}'.", cancellationToken);
        return adminUser;
    }

    private async Task TryQueueTenantOnboardingEmailAsync(Tenant tenant, InitialAdminProfile adminProfile, CancellationToken cancellationToken)
    {
        var recipient = NormalizeEmail(adminProfile.Email) ?? NormalizeOptional(tenant.ContactEmail) ?? NormalizeOptional(tenant.Email);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return;
        }

        var platformEmail = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        if (platformEmail is null || string.IsNullOrWhiteSpace(platformEmail.Host))
        {
            return;
        }

        var template = await emailTemplateService.RenderPlatformTemplateAsync(
            "tenant-onboarding",
            EmailTemplateVariables.WithRecipientAndActorAliases(
                adminProfile.FullName,
                tenantContext.Email,
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["workspaceName"] = tenant.Name,
                    ["tenantName"] = tenant.Name,
                    ["companyName"] = tenant.CompanyName,
                    ["email"] = recipient,
                    ["loginUrl"] = BuildLoginUrl(),
                    ["supportEmail"] = platformEmail.ReplyToEmail ?? platformEmail.SenderAddress,
                    ["platformName"] = "Ecosys",
                }),
            cancellationToken);

        if (!template.Enabled)
        {
            return;
        }

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            tenant.Id,
            "tenant.onboarding",
            template.Subject,
            tenant.Name,
            cancellationToken);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                "tenant.onboarding",
                "tenant-onboarding",
                recipient,
                adminProfile.FullName,
                template.SenderNameOverride ?? platformEmail.SenderName,
                platformEmail.SenderAddress,
                template.ReplyToOverride ?? platformEmail.ReplyToEmail,
                finalSubject,
                template.HtmlBody,
                template.TextBody,
                tenantContext.UserId),
            cancellationToken);
    }

    private string BuildLoginUrl()
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return $"{origin.TrimEnd('/')}/login";
        }

        return $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/') + "/login";
    }

    private async Task<Tenant> SetTenantStatusAsync(Guid tenantId, string status, string? reason, CancellationToken cancellationToken, ActivateTenantLicenseRequest? activation = null)
    {
        var tenant = await LoadManagedTenantAsync(tenantId, cancellationToken);
        var now = DateTime.UtcNow;
        var normalizedReason = NormalizeOptional(reason);

        if ((status is PlatformTenantStatuses.Suspended or PlatformTenantStatuses.Inactive) && string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new BusinessRuleException("A reason is required when suspending or deactivating a tenant.");
        }

        var license = await licenseGuardService.GetOrCreateTenantLicenseAsync(tenant.Id, cancellationToken);
        tenant.Status = status;
        tenant.IsActive = status == PlatformTenantStatuses.Active;
        tenant.UpdatedByUserId = tenantContext.GetRequiredUserId();

        if (status == PlatformTenantStatuses.Active || status == PlatformTenantStatuses.Trial)
        {
            tenant.SuspendedAt = null;
            tenant.DeactivatedAt = null;
            tenant.DeactivatedByUserId = null;
            tenant.DeactivationReason = null;
            tenant.LicenseStatus = status == PlatformTenantStatuses.Trial
                ? LicenseStatuses.Trial
                : ResolveLicenseStatus(tenant, license) == LicenseStatuses.Expired
                    ? LicenseStatuses.Expired
                    : LicenseStatuses.Active;
            license.Status = tenant.LicenseStatus;
            license.SuspendedAt = null;

            // The activation dialog's explicit license term (Phase 4) — only applied on a genuine
            // activation, not on every internal Active/Trial transition this method also handles.
            if (activation is not null)
            {
                license.StartsAt = EnsureUtc(activation.StartsAt);
                license.ExpiresAt = EnsureUtc(activation.ExpiresAt);
                license.IsPerpetual = activation.IsPerpetual;
                license.BillingCycle = BillingCycles.Normalize(activation.BillingCycle);
                license.NextBillingDate = EnsureUtc(activation.NextBillingDate);
                if (activation.GracePeriodDays.HasValue)
                {
                    license.GracePeriodDays = activation.GracePeriodDays.Value;
                }
                license.CancelledAt = null;
                license.OverdueWarningSentAt = null;
                tenant.SubscriptionStartsAt = license.StartsAt;
                tenant.SubscriptionEndsAt = license.ExpiresAt;

                if (activation.PlanId.HasValue)
                {
                    var selectedPlan = await LoadActivePlanAsync(activation.PlanId.Value, cancellationToken);
                    license.LicensePlanId = selectedPlan.Id;
                    license.LicensePlan = selectedPlan;
                    tenant.PlanName = selectedPlan.DisplayName;
                }
            }
        }
        else if (status == PlatformTenantStatuses.Suspended)
        {
            tenant.SuspendedAt = now;
            tenant.DeactivatedAt = null;
            tenant.DeactivatedByUserId = null;
            tenant.DeactivationReason = normalizedReason;
            tenant.LicenseStatus = LicenseStatuses.Suspended;
            license.Status = LicenseStatuses.Suspended;
            license.SuspendedAt = now;
        }
        else
        {
            tenant.IsActive = false;
            tenant.DeactivatedAt = now;
            tenant.DeactivatedByUserId = tenantContext.GetRequiredUserId();
            tenant.DeactivationReason = normalizedReason;
            tenant.LicenseStatus = LicenseStatuses.Cancelled;
            license.Status = LicenseStatuses.Cancelled;
            license.CancelledAt = now;
            license.SuspendedAt = null;

            await dbContext.Assets
                .Where(x => x.TenantId == tenant.Id && x.Status != "Inactive")
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, _ => "Inactive"), cancellationToken);
        }

        license.Notes = normalizedReason;
        await dbContext.SaveChangesAsync(cancellationToken);

        var auditAction = status switch
        {
            PlatformTenantStatuses.Active => "platform.tenant.activated",
            PlatformTenantStatuses.Trial => "platform.tenant.activated",
            PlatformTenantStatuses.Suspended => "platform.tenant.suspended",
            _ => "platform.tenant.deactivated"
        };

        await WriteAuditAsync(
            tenant,
            auditAction,
            string.IsNullOrWhiteSpace(normalizedReason)
                ? $"Tenant '{tenant.Name}' status changed to {status}."
                : $"Tenant '{tenant.Name}' status changed to {status}. Reason: {normalizedReason}",
            cancellationToken);

        return tenant;
    }

    private async Task<PlatformTenantDetailResponse> BuildDetailResponseAsync(
        Tenant tenant,
        CancellationToken cancellationToken,
        bool? initialAdminInvitationSent = null,
        string? initialAdminInvitationMessage = null)
    {
        var summary = await BuildSummaryResponseAsync(tenant, cancellationToken);
        var trial = BuildTrialSnapshot(tenant);

        return new PlatformTenantDetailResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            NormalizeOptional(tenant.ContactName),
            NormalizeOptional(tenant.ContactEmail),
            NormalizeOptional(tenant.ContactPhone),
            NullIfWhiteSpace(tenant.Country),
            tenant.County,
            tenant.City,
            tenant.Address,
            tenant.TaxPin,
            ResolveTenantStatus(tenant),
            tenant.PlanName,
            ResolveLicenseStatus(tenant, tenant.TenantLicenses.OrderByDescending(x => x.CreatedAt).FirstOrDefault()),
            tenant.MaxUsers,
            tenant.MaxBranches,
            trial.StartsAt,
            tenant.TrialEndsAt,
            tenant.TrialExtensionUsed,
            tenant.TrialExtendedAt,
            trial.DaysRemaining,
            trial.Status,
            tenant.SubscriptionStartsAt,
            tenant.SubscriptionEndsAt,
            tenant.CreatedAt,
            tenant.UpdatedAt,
            tenant.SuspendedAt,
            tenant.DeactivatedAt,
            tenant.CreatedByUserId,
            tenant.UpdatedByUserId,
            summary.UserCount,
            summary.BranchCount,
            summary.WorkOrderCount,
            summary.ActiveUsersNow,
            summary.LastActivityAt,
            initialAdminInvitationSent,
            initialAdminInvitationMessage,
            tenant.TimeZoneId, tenant.Currency,
            await dbContext.EmailSettings.Where(x => x.TenantId == tenant.Id).Select(x => x.ManagedEmailAddress).SingleOrDefaultAsync(cancellationToken));
    }

    private async Task<PlatformTenantSummaryResponse> BuildSummaryResponseAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var userCount = await dbContext.Users.CountAsync(
            x => x.TenantId == tenant.Id && x.IsActive && x.Role != AppRoles.SuperAdmin,
            cancellationToken);
        var branchCount = await dbContext.Branches.CountAsync(x => x.TenantId == tenant.Id && x.IsActive, cancellationToken);
        var workOrderCount = await dbContext.WorkOrders.CountAsync(x => x.TenantId == tenant.Id, cancellationToken);

        var now = DateTime.UtcNow;
        var activeUsersNow = await dbContext.UserSessions
            .Where(x => x.TenantId == tenant.Id && !x.IsRevoked && !x.LogoutAt.HasValue && x.LastSeenAt >= now.AddMinutes(-15))
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var lastActivityAt = await dbContext.UserSessions
            .Where(x => x.TenantId == tenant.Id)
            .OrderByDescending(x => x.LastSeenAt)
            .Select(x => (DateTime?)x.LastSeenAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new PlatformTenantSummaryResponse(
            tenant.Id,
            userCount,
            branchCount,
            workOrderCount,
            activeUsersNow,
            lastActivityAt);
    }

    private async Task WriteAuditAsync(Tenant tenant, string action, string details, CancellationToken cancellationToken)
    {
        await auditLogService.LogAsync(
            tenant.Id,
            tenantContext.GetRequiredUserId(),
            action,
            nameof(Tenant),
            tenant.Id.ToString(),
            details,
            cancellationToken);
    }

    private static PlatformTenantListResponse MapListItem(Tenant tenant, int userCount, int branchCount)
    {
        var license = tenant.TenantLicenses.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        var trial = BuildTrialSnapshot(tenant);

        return new PlatformTenantListResponse(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.ContactName,
            tenant.ContactEmail,
            tenant.PlanName,
            ResolveLicenseStatus(tenant, license),
            userCount,
            branchCount,
            ResolveTenantStatus(tenant),
            trial.StartsAt,
            tenant.TrialEndsAt,
            tenant.TrialExtensionUsed,
            tenant.TrialExtendedAt,
            trial.DaysRemaining,
            trial.Status,
            tenant.CreatedAt);
    }

    private static string ResolveTenantStatus(Tenant tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant.Status))
        {
            return tenant.Status;
        }

        return tenant.IsActive ? PlatformTenantStatuses.Active : PlatformTenantStatuses.Inactive;
    }

    private static string ResolveLicenseStatus(Tenant tenant, TenantLicense? license)
    {
        if (IsTrialExpired(tenant))
        {
            return LicenseStatuses.Expired;
        }

        if (!string.IsNullOrWhiteSpace(tenant.LicenseStatus))
        {
            return tenant.LicenseStatus!;
        }

        if (license is null)
        {
            return LicenseStatuses.Trial;
        }

        if (string.Equals(license.Status, LicenseStatuses.Suspended, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseStatuses.Suspended;
        }

        if (string.Equals(license.Status, LicenseStatuses.Expired, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseStatuses.Expired;
        }

        if (string.Equals(license.Status, LicenseStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseStatuses.Cancelled;
        }

        var expiresAt = license.ExpiresAt ?? license.TrialEndsAt;
        if (expiresAt.HasValue && expiresAt.Value < DateTime.UtcNow)
        {
            return LicenseStatuses.Expired;
        }

        if (string.Equals(license.Status, LicenseStatuses.TrialExpiringSoon, StringComparison.OrdinalIgnoreCase))
            return LicenseStatuses.TrialExpiringSoon;

        return string.Equals(license.Status, LicenseStatuses.Trial, StringComparison.OrdinalIgnoreCase)
            ? LicenseStatuses.Trial
            : LicenseStatuses.Active;
    }

    private static bool ComputeIsActive(string status, string licenseStatus) =>
        (status == PlatformTenantStatuses.Active || status == PlatformTenantStatuses.Trial)
        && licenseStatus != LicenseStatuses.Suspended
        && licenseStatus != LicenseStatuses.Cancelled;

    private static TrialSnapshot BuildTrialSnapshot(Tenant tenant)
    {
        DateTime? startsAt = EnsureUtc(tenant.TrialStartsAt) ?? (tenant.CreatedAt == default ? null : EnsureUtc(tenant.CreatedAt));
        var endsAt = EnsureUtc(tenant.TrialEndsAt);
        if (!startsAt.HasValue || !endsAt.HasValue)
        {
            return new TrialSnapshot(null, null, null, "NotConfigured");
        }

        var tenantStatus = ResolveTenantStatus(tenant);
        var licenseStatus = ResolveLicenseStatusForTrial(tenant);

        if (tenantStatus == PlatformTenantStatuses.Inactive)
        {
            return new TrialSnapshot(startsAt, endsAt, null, "Inactive");
        }

        if (tenantStatus == PlatformTenantStatuses.Suspended || licenseStatus == LicenseStatuses.Suspended)
        {
            return new TrialSnapshot(startsAt, endsAt, null, "Suspended");
        }

        if (licenseStatus == LicenseStatuses.Active)
        {
            return new TrialSnapshot(startsAt, endsAt, null, "PaidActive");
        }

        if (DateTime.UtcNow > endsAt.Value)
        {
            return new TrialSnapshot(startsAt, endsAt, 0, "TrialExpired");
        }

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((endsAt.Value - DateTime.UtcNow).TotalDays));
        if (daysRemaining <= TrialExpiringSoonDays)
        {
            return new TrialSnapshot(startsAt, endsAt, daysRemaining, "TrialExpiringSoon");
        }

        return new TrialSnapshot(
            startsAt,
            endsAt,
            daysRemaining,
            tenant.TrialExtensionUsed ? "TrialExtended" : "TrialActive");
    }

    private static string ResolveLicenseStatusForTrial(Tenant tenant) =>
        string.IsNullOrWhiteSpace(tenant.LicenseStatus) ? LicenseStatuses.Trial : tenant.LicenseStatus!;

    private static bool IsTrialExpired(Tenant tenant)
    {
        var trialEndsAt = EnsureUtc(tenant.TrialEndsAt);
        if (!trialEndsAt.HasValue)
        {
            return false;
        }

        var tenantStatus = ResolveTenantStatus(tenant);
        var licenseStatus = ResolveLicenseStatusForTrial(tenant);
        return DateTime.UtcNow > trialEndsAt.Value
            && tenantStatus != PlatformTenantStatuses.Inactive
            && tenantStatus != PlatformTenantStatuses.Suspended
            && !string.Equals(licenseStatus, LicenseStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(licenseStatus, LicenseStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(licenseStatus, LicenseStatuses.Suspended, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateNotInPast(DateTime? value, DateTime todayUtc, string label)
    {
        if (value.HasValue && value.Value < todayUtc)
        {
            throw new BusinessRuleException($"{label} cannot be earlier than today.");
        }
    }

    private static void ValidatePositive(int? value, string label)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new BusinessRuleException($"{label} must be greater than zero.");
        }
    }

    private static string NormalizeRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeEmail(string? value)
    {
        var trimmed = NormalizeOptional(value);
        if (trimmed is null)
        {
            return null;
        }

        try
        {
            var mailAddress = new MailAddress(trimmed);
            return mailAddress.Address.ToLowerInvariant();
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Contact email must be a valid email address.");
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeTenantStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PlatformTenantStatuses.Active;
        }

        var normalized = value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "active" => PlatformTenantStatuses.Active,
            "suspended" => PlatformTenantStatuses.Suspended,
            "trial" => PlatformTenantStatuses.Trial,
            "inactive" => PlatformTenantStatuses.Inactive,
            _ => throw new BusinessRuleException("Status must be Active, Suspended, Trial, or Inactive.")
        };
    }

    private static string NormalizeLicenseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LicenseStatuses.Trial;
        }

        var normalized = value.Trim();
        return normalized.ToLowerInvariant() switch
        {
            "trial" => LicenseStatuses.Trial,
            "active" => LicenseStatuses.Active,
            "expired" => LicenseStatuses.Expired,
            "suspended" => LicenseStatuses.Suspended,
            "cancelled" => LicenseStatuses.Cancelled,
            _ => throw new BusinessRuleException("License status must be Trial, Active, Expired, Suspended, or Cancelled.")
        };
    }

    private async Task<LicensePlan> LoadActivePlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await dbContext.LicensePlans.SingleOrDefaultAsync(x => x.Id == planId, cancellationToken) is { IsActive: true } plan
            ? plan
            : throw new BusinessRuleException("Select an active plan.");

    /// <summary>Shared date/term validation for Activate and Renew (Phase 23).</summary>
    private static void ValidateLicenseTerm(DateTime startsAt, DateTime? expiresAt, bool isPerpetual, DateTime? nextBillingDate, int? gracePeriodDays)
    {
        if (!isPerpetual && expiresAt is null)
        {
            throw new BusinessRuleException("License expiry is required unless this is an explicit perpetual/internal license.");
        }

        if (expiresAt.HasValue && expiresAt.Value <= startsAt)
        {
            throw new BusinessRuleException("License expiry must be after the license start date.");
        }

        if (nextBillingDate.HasValue && nextBillingDate.Value < startsAt)
        {
            throw new BusinessRuleException("Next billing date cannot be before the license start date.");
        }

        if (gracePeriodDays.HasValue && gracePeriodDays.Value < 0)
        {
            throw new BusinessRuleException("Grace period cannot be negative.");
        }
    }

    private static string DescribeTerm(TenantLicense license) =>
        license.IsPerpetual
            ? $"perpetual, starting {license.StartsAt:yyyy-MM-dd}"
            : $"{license.StartsAt:yyyy-MM-dd} to {DescribeDate(license.ExpiresAt)}";

    private static string DescribeDate(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "(none)";

    private static InitialAdminProfile BuildInitialAdminProfile(UpsertPlatformTenantRequest request)
    {
        if (request.UsePrimaryContactAsWorkspaceAdmin)
        {
            return new InitialAdminProfile(
                NormalizeRequired(request.PrimaryContactName ?? request.ContactName, "Primary contact name is required."),
                NormalizeRequired(request.PrimaryContactEmail ?? request.ContactEmail, "Primary contact email is required."),
                NormalizeOptional(request.PrimaryContactPhone ?? request.ContactPhone));
        }

        return new InitialAdminProfile(
            NormalizeRequired(request.AdminFullName, "Initial admin full name is required."),
            NormalizeRequired(request.AdminEmail, "Initial admin email is required."),
            NormalizeOptional(request.AdminPhone));
    }

    private static void ValidateInitialAdminRequest(InitialAdminProfile adminProfile)
    {
        if (string.IsNullOrWhiteSpace(adminProfile.FullName))
        {
            throw new BusinessRuleException("Initial admin full name is required.");
        }

        if (string.IsNullOrWhiteSpace(adminProfile.Email))
        {
            throw new BusinessRuleException("Initial admin email is required.");
        }

        _ = NormalizeEmail(adminProfile.Email);
    }

    private static DateTime StartOfUtcDay(DateTime value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, DateTimeKind.Utc);

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue ? EnsureUtc(value.Value) : null;

    private static PermissionResponse MapPermissions(UserPermissionsModel permissions) =>
        new()
        {
            CanViewWorkOrders = permissions.CanViewWorkOrders,
            CanCreateWorkOrders = permissions.CanCreateWorkOrders,
            CanAssignWorkOrders = permissions.CanAssignWorkOrders,
            CanCompleteWorkOrders = permissions.CanCompleteWorkOrders,
            CanViewKnowledge = permissions.CanViewKnowledge,
            CanManageKnowledge = permissions.CanManageKnowledge,
            CanViewMaterials = permissions.CanViewMaterials,
            CanRequestMaterials = permissions.CanRequestMaterials,
            CanManageMaterials = permissions.CanManageMaterials,
            CanApproveMaterials = permissions.CanApproveMaterials,
            CanIssueMaterials = permissions.CanIssueMaterials,
            CanManageAssets = permissions.CanManageAssets,
            CanManageSettings = permissions.CanManageSettings,
            CanViewReports = permissions.CanViewReports,
            CanViewFinance = permissions.CanViewFinance,
            CanManageFinance = permissions.CanManageFinance,
            CanViewFinanceRates = permissions.CanViewFinanceRates,
            CanManageFinanceRates = permissions.CanManageFinanceRates,
            CanViewFinanceApprovals = permissions.CanViewFinanceApprovals,
            CanManageFinanceApprovals = permissions.CanManageFinanceApprovals,
            CanViewBillableWork = permissions.CanViewBillableWork,
            CanManageBillableWork = permissions.CanManageBillableWork,
            CanOverrideMaterialStock = permissions.CanOverrideMaterialStock,
        };

    private static UserPermission ToEntity(UserPermissionsModel permissions) =>
        new()
        {
            CanViewWorkOrders = permissions.CanViewWorkOrders,
            CanCreateWorkOrders = permissions.CanCreateWorkOrders,
            CanAssignWorkOrders = permissions.CanAssignWorkOrders,
            CanCompleteWorkOrders = permissions.CanCompleteWorkOrders,
            CanApproveMaterials = permissions.CanApproveMaterials,
            CanIssueMaterials = permissions.CanIssueMaterials,
            CanManageAssets = permissions.CanManageAssets,
            CanManageSettings = permissions.CanManageSettings,
            CanViewReports = permissions.CanViewReports,
            CanViewFinance = permissions.CanViewFinance,
            CanManageFinance = permissions.CanManageFinance,
            CanViewFinanceRates = permissions.CanViewFinanceRates,
            CanManageFinanceRates = permissions.CanManageFinanceRates,
            CanViewFinanceApprovals = permissions.CanViewFinanceApprovals,
            CanManageFinanceApprovals = permissions.CanManageFinanceApprovals,
            CanViewBillableWork = permissions.CanViewBillableWork,
            CanManageBillableWork = permissions.CanManageBillableWork,
            CanOverrideMaterialStock = permissions.CanOverrideMaterialStock,
        };

    private static string GenerateTemporaryPassword()
    {
        var buffer = RandomNumberGenerator.GetBytes(12);
        return $"Eco!{Convert.ToBase64String(buffer).Replace('+', 'A').Replace('/', 'B').TrimEnd('=').Substring(0, 12)}9";
    }

    private static string CreateRawResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static class PlatformTenantStatuses
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";
        public const string Trial = "Trial";
        public const string Inactive = "Inactive";
    }

}

public sealed record UpsertPlatformTenantRequest(
    string? Name,
    string? CompanyName,
    string? Slug,
    string? CompanyEmail,
    string? CompanyPhone,
    string? Industry,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? PrimaryContactName,
    string? PrimaryContactEmail,
    string? PrimaryContactPhone,
    string? Country,
    string? County,
    string? City,
    string? Address,
    string? TaxPin,
    string? PlanName,
    int? MaxUsers,
    int? MaxBranches,
    DateTime? TrialEndsAt,
    DateTime? SubscriptionEndsAt,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    string? Status,
    string? LicenseStatus,
    bool CreateDefaultAdmin = false,
    bool UsePrimaryContactAsWorkspaceAdmin = false,
    string? AdminFullName = null,
    string? AdminEmail = null,
    string? AdminPhone = null,
    string? TimeZoneId = null,
    string? PlanCode = null);

public sealed record UpdatePlatformTenantStatusRequest(string Status, string? Reason);
public sealed record UpdatePlatformTenantStatusReasonRequest(string? Reason);

/// <summary>
/// The license term captured by the activation dialog. Normal activation requires an explicit
/// <see cref="ExpiresAt"/>; <see cref="IsPerpetual"/> is the deliberate, platform-admin-only escape
/// hatch for an internal/demo tenant that should never expire — never the accidental result of
/// leaving <see cref="ExpiresAt"/> blank.
/// </summary>
public sealed record ActivateTenantLicenseRequest(
    DateTime StartsAt,
    DateTime? ExpiresAt,
    bool IsPerpetual,
    string? BillingCycle,
    DateTime? NextBillingDate,
    int? GracePeriodDays,
    /// <summary>Leaves the tenant's current plan untouched when null.</summary>
    Guid? PlanId = null);

/// <summary>Defines the next valid entitlement period for a tenant whose access lapsed or was cancelled.</summary>
public sealed record RenewTenantLicenseRequest(
    DateTime StartsAt,
    DateTime? ExpiresAt,
    bool IsPerpetual,
    string? BillingCycle,
    DateTime? NextBillingDate,
    int? GracePeriodDays,
    /// <summary>Leaves the tenant's current plan untouched when null.</summary>
    Guid? PlanId = null);

/// <summary>Pushes an existing, still-active entitlement's expiry further out without changing anything else.</summary>
public sealed record ExtendTenantLicenseRequest(DateTime NewExpiresAt);

public sealed record PlatformTenantListResponse(
    Guid TenantId,
    string Name,
    string Slug,
    string? ContactName,
    string? ContactEmail,
    string? PlanName,
    string LicenseStatus,
    int UserCount,
    int BranchCount,
    string Status,
    DateTime? TrialStartsAt,
    DateTime? TrialEndsAt,
    bool TrialExtensionUsed,
    DateTime? TrialExtendedAt,
    int? TrialDaysRemaining,
    string TrialStatus,
    DateTime CreatedAt);

public sealed record PlatformTenantDetailResponse(
    Guid TenantId,
    string Name,
    string Slug,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? Country,
    string? County,
    string? City,
    string? Address,
    string? TaxPin,
    string Status,
    string? PlanName,
    string LicenseStatus,
    int? MaxUsers,
    int? MaxBranches,
    DateTime? TrialStartsAt,
    DateTime? TrialEndsAt,
    bool TrialExtensionUsed,
    DateTime? TrialExtendedAt,
    int? TrialDaysRemaining,
    string TrialStatus,
    DateTime? SubscriptionStartsAt,
    DateTime? SubscriptionEndsAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? SuspendedAt,
    DateTime? DeactivatedAt,
    Guid? CreatedByUserId,
    Guid? UpdatedByUserId,
    int UserCount,
    int BranchCount,
    int WorkOrderCount,
    int ActiveUsersNow,
    DateTime? LastActivityAt,
    bool? InitialAdminInvitationSent,
    string? InitialAdminInvitationMessage,
    string TimeZoneId = "Africa/Nairobi",
    string Currency = "KES",
    string? ServiceOpsEmail = null);

public sealed record PlatformTenantSummaryResponse(
    Guid TenantId,
    int UserCount,
    int BranchCount,
    int WorkOrderCount,
    int ActiveUsersNow,
    DateTime? LastActivityAt);

/// <summary>
/// Optional body for an impersonation request. The reason is operator-supplied free text, recorded
/// verbatim in both audit entries (tenant-side and platform-side) so the record answers "why", not
/// only "who and when". Attributes sit on the constructor parameter, not the property — a
/// <c>[property:]</c> target throws at model binding on a positional record (EC-003).
/// </summary>
public sealed record ImpersonateTenantRequest([StringLength(500)] string? Reason = null);

public sealed record TenantImpersonationResponse(
    string Token,
    LoginUserDto User,
    LoginTenantDto Tenant,
    Guid ImpersonatedByUserId,
    string ImpersonatedByName,
    DateTime StartedAt);

public sealed record PlatformTenantAuditLogResponse(
    Guid Id,
    string Action,
    string Actor,
    string? Details,
    DateTime CreatedAt);

public sealed record PlatformTenantBrandingResponse(string CompanyName, string? LogoUrl, string PrimaryColor, string SecondaryColor, bool ShowPoweredByEcosys);

public sealed record PlatformTenantUserResponse(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    string? JobTitle,
    DateTime CreatedAt,
    DateTime? LockedOutUntil,
    bool MustChangePassword,
    DateTime? LastCredentialSentAt,
    /// <summary>Null means the account has never been signed into — its setup link is still pending.</summary>
    DateTime? LastLoginAt);

public sealed record CreatePlatformTenantUserRequest(
    string FullName,
    string Email,
    string? Phone,
    /// <summary>Tenant role; defaults to Admin. Platform roles are rejected.</summary>
    string? Role,
    string? JobTitle);

public sealed record PlatformTenantUserCreatedResponse(
    PlatformTenantUserResponse User,
    bool InvitationSent,
    string InvitationStatus,
    string Message);

public sealed record PlatformTenantCredentialDeliveryResponse(
    Guid UserId,
    string Email,
    bool Success,
    string Status,
    string Message,
    DateTime? LastCredentialSentAt);

public sealed record PlatformTenantNumberingResponse(Guid Id, string DocumentType, string Prefix, string? Suffix, long NextNumber, int PaddingLength, string ResetFrequency, bool IsActive, DateTime? LastResetAt);

public sealed record PlatformTenantDocumentDesignResponse(string DocumentType, bool IsCustomised);

public sealed record PlatformTenantFinanceSummaryResponse(int InvoiceCount, decimal TotalInvoiced, decimal TotalPaid, decimal OutstandingBalance, int OverdueCount);

public sealed record PlatformTenantModuleSettingResponse(string ModuleKey, DateTime UpdatedAt);

internal sealed record InitialAdminProfile(
    string FullName,
    string Email,
    string? Phone);

internal sealed record TrialSnapshot(
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? DaysRemaining,
    string Status);
