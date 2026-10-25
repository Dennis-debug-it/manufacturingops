using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;
using Ecosys.Shared.Options;
using Microsoft.Extensions.Options;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IUserPermissionTemplateService permissionTemplateService,
    IPasswordHasher<User> passwordHasher,
    ILicenseGuardService licenseGuardService,
    ITenantSecurityPolicyService tenantSecurityPolicyService,
    IAuditLogService auditLogService,
    IUserCredentialDeliveryService credentialDeliveryService,
    ITemporaryPasswordService temporaryPasswordService,
    IOptions<AuthSecurityOptions> authSecurityOptions,
    IUserSessionService userSessionService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var users = await dbContext.Users
            .Include(x => x.Permission)
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var workforceProfiles = await LoadWorkforceProfilesAsync(users, cancellationToken);
        var invitations = await LoadLatestInvitationsAsync(users.Select(x => x.Id).ToArray(), cancellationToken);
        return Ok(users.Select(user => WithInvitation(Map(user, workforceProfiles), invitations.GetValueOrDefault(user.Id))).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users
            .Include(x => x.Permission)
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        var invitations = await LoadLatestInvitationsAsync([user.Id], cancellationToken);
        return Ok(WithInvitation(Map(user, workforceProfiles), invitations.GetValueOrDefault(user.Id)));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateRequest(request.FullName, request.CredentialDeliveryMethod);
        var transaction = await TryBeginSerializableTransactionAsync(cancellationToken);

        var normalizedRole = NormalizeRole(request.Role);
        // Only an active account consumes a seat (office or field, by role); the guard row-locks the license inside this transaction.
        await licenseGuardService.EnsureSeatsAvailableAsync(TenantId, request.IsActive ? [normalizedRole] : [], cancellationToken);
        var deliveryMethod = NormalizeCredentialDeliveryMethod(request.CredentialDeliveryMethod);

        var (normalizedEmail, hasEmail) = await ResolveEmailAsync(request.Email, excludeUserId: null, cancellationToken);
        if (!hasEmail && !string.Equals(deliveryMethod, "None", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("This user has no email address. Set credential delivery to None and either set a password or let the system generate one to share with them directly.");
        }

        var username = await ResolveUsernameAsync(request.Username, request.FullName, excludeUserId: null, cancellationToken);

        var permissions = permissionTemplateService.Merge(normalizedRole, request.JobTitle, request.Permissions?.ToModel());
        var normalizedBranchIds = NormalizeBranchIds(request.BranchIds);
        var resolvedDefaultBranchId = await ResolveDefaultBranchIdAsync(normalizedRole, normalizedBranchIds, request.DefaultBranchId, request.HasAllBranchAccess, cancellationToken);

        string? adminSuppliedPassword = null;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await tenantSecurityPolicyService.ValidatePasswordAsync(TenantId, request.Password, cancellationToken);
            adminSuppliedPassword = request.Password;
        }
        var bootstrapPassword = adminSuppliedPassword ?? temporaryPasswordService.Generate();

        var user = new User
        {
            TenantId = TenantId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            HasEmail = hasEmail,
            Username = username,
            Role = normalizedRole,
            JobTitle = request.JobTitle?.Trim(),
            Department = request.Department?.Trim(),
            IsActive = request.IsActive,
            HasAllBranchAccess = string.Equals(normalizedRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) || request.HasAllBranchAccess,
            MustChangePassword = true,
            DefaultBranchId = resolvedDefaultBranchId,
            Permission = ToEntity(permissions),
            BranchAssignments = normalizedBranchIds.Select(branchId => new UserBranchAssignment
            {
                TenantId = TenantId,
                BranchId = branchId,
                IsDefault = resolvedDefaultBranchId == branchId
            }).ToList()
        };
        user.PasswordHash = passwordHasher.HashPassword(user, bootstrapPassword);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncWorkforceProfileAsync(user, request.AssignmentGroupIds, NormalizeOptional(request.PhoneNumber), cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        // An invite link only makes sense when there's an inbox to send it to, and only when the
        // admin actually asked for one — "None" means the admin is handling delivery some other way.
        var willEmailInvite = hasEmail && !string.Equals(deliveryMethod, "None", StringComparison.OrdinalIgnoreCase);
        CredentialDeliverySummaryResponse credentialDelivery;
        if (willEmailInvite)
        {
            var deliveryResult = await SendAccountSetupAsync(user, cancellationToken);
            credentialDelivery = new CredentialDeliverySummaryResponse(deliveryResult.Success, deliveryResult.Status, deliveryResult.Message);

            if (credentialDelivery.Success)
            {
                user.LastCredentialSentAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (adminSuppliedPassword is not null)
        {
            // The admin already knows the password they just typed — nothing to reveal or send.
            credentialDelivery = new CredentialDeliverySummaryResponse(true, "PasswordSetByAdmin", "Password set. Share the username, workspace, and password with the user directly — no email was sent.");
        }
        else
        {
            // Nobody else will ever see this password: no invite link went out, and the admin didn't
            // choose one themselves. Hand it back once so it isn't lost.
            credentialDelivery = new CredentialDeliverySummaryResponse(true, "GeneratedPassword", "A temporary password was generated since no email was sent. Copy it now and share it with the user directly — it will not be shown again.", bootstrapPassword);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "User created",
            nameof(User),
            user.Id.ToString(),
            $"Created user '{DisplayIdentifier(user)}'.",
            cancellationToken);

        if (willEmailInvite && credentialDelivery.Success)
        {
            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "User invitation sent",
                nameof(User),
                user.Id.ToString(),
                $"Account setup invitation sent to '{user.Email}'.",
                cancellationToken);
        }

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, Map(user, workforceProfiles, credentialDelivery));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateUpdateRequest(request.FullName);

        var transaction = await TryBeginSerializableTransactionAsync(cancellationToken);

        var user = await dbContext.Users
            .Include(x => x.Permission)
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var normalizedRole = NormalizeRole(request.Role);
        if (id == UserId && !string.Equals(user.Role, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You cannot change your own role from this account.");
        }

        // Reactivating takes a seat; a role change on an active user takes one in the new role's pool
        // (a technician promoted to dispatcher needs an office seat).
        var takesNewSeat = request.IsActive
            && (!user.IsActive || SeatTypes.For(user.Role) != SeatTypes.For(normalizedRole));
        if (takesNewSeat)
        {
            await licenseGuardService.EnsureSeatsAvailableAsync(TenantId, [normalizedRole], cancellationToken);
        }

        var (normalizedEmail, hasEmail) = await ResolveEmailAsync(request.Email, excludeUserId: id, cancellationToken);

        // Unlike Create, a blank Username here means "the caller didn't send one" (e.g. saving
        // permissions from a form that only knows role/capabilities) rather than "generate a new
        // one" — the existing value already satisfies the uniqueness constraint, so keep it.
        var username = string.IsNullOrWhiteSpace(request.Username) && !string.IsNullOrWhiteSpace(user.Username)
            ? user.Username
            : await ResolveUsernameAsync(request.Username, request.FullName, excludeUserId: id, cancellationToken);

        var permissions = permissionTemplateService.Merge(normalizedRole, request.JobTitle, request.Permissions?.ToModel());
        var normalizedBranchIds = NormalizeBranchIds(request.BranchIds);
        var resolvedDefaultBranchId = await ResolveDefaultBranchIdAsync(normalizedRole, normalizedBranchIds, request.DefaultBranchId, request.HasAllBranchAccess, cancellationToken);

        // A corrected address means any invite/reset link already sent went to the wrong mailbox —
        // possibly someone else's. Kill those links; the admin resends to the right address.
        var emailChanged = !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            var outstandingLinks = await dbContext.PasswordResetTokens
                .Where(x => x.UserId == user.Id && x.UsedAt == null && x.SupersededAt == null && x.RevokedAt == null)
                .ToListAsync(cancellationToken);
            foreach (var link in outstandingLinks) link.RevokedAt = DateTime.UtcNow;
        }

        user.FullName = request.FullName.Trim();
        user.Email = normalizedEmail;
        user.HasEmail = hasEmail;
        user.Username = username;
        user.Role = normalizedRole;
        user.JobTitle = request.JobTitle?.Trim();
        user.Department = request.Department?.Trim();
        user.IsActive = request.IsActive;
        user.HasAllBranchAccess = string.Equals(normalizedRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) || request.HasAllBranchAccess;
        user.DefaultBranchId = resolvedDefaultBranchId;
        if (user.Permission is null)
        {
            // Added explicitly: a new child with a preset Guid key reached only through the navigation
            // is tracked as an update of a row that doesn't exist, and SaveChanges fails.
            user.Permission = new UserPermission { UserId = user.Id };
            dbContext.UserPermissions.Add(user.Permission);
        }

        ApplyPermissions(user.Permission, permissions);

        dbContext.UserBranchAssignments.RemoveRange(user.BranchAssignments);
        user.BranchAssignments = normalizedBranchIds.Select(branchId => new UserBranchAssignment
        {
            TenantId = TenantId,
            UserId = user.Id,
            BranchId = branchId,
            IsDefault = resolvedDefaultBranchId == branchId
        }).ToList();

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await tenantSecurityPolicyService.ValidatePasswordAsync(TenantId, request.Password, cancellationToken);
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncWorkforceProfileAsync(user, request.AssignmentGroupIds, NormalizeOptional(request.PhoneNumber), cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "User updated",
            nameof(User),
            user.Id.ToString(),
            $"Updated user '{DisplayIdentifier(user)}'.",
            cancellationToken);

        // Force fresh authorization: this endpoint re-applies the user's role, permission set and
        // branch access, so revoke their existing sessions (they carry the now-stale permission
        // claims baked into the JWT). Skip self-edits so an admin doesn't log themselves out.
        if (id != UserId)
        {
            await userSessionService.RevokeAllForUserAsync(id, "Account updated by administrator", cancellationToken);
        }

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        var invitations = await LoadLatestInvitationsAsync([user.Id], cancellationToken);
        return Ok(WithInvitation(Map(user, workforceProfiles), invitations.GetValueOrDefault(user.Id)));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<UserResponse>> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var transaction = await TryBeginSerializableTransactionAsync(cancellationToken);

        var user = await dbContext.Users
            .Include(x => x.Permission)
            .Include(x => x.BranchAssignments)
            .Include(x => x.DefaultBranch)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (!user.IsActive && request.IsActive)
        {
            await licenseGuardService.EnsureSeatsAvailableAsync(TenantId, [user.Role], cancellationToken);
        }

        user.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncWorkforceProfileAsync(user, null, null, cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            request.IsActive ? "User activated" : "User deactivated",
            nameof(User),
            user.Id.ToString(),
            $"{(request.IsActive ? "Activated" : "Deactivated")} user '{DisplayIdentifier(user)}'.",
            cancellationToken);

        // Terminate all active sessions on deactivation so access is revoked immediately rather
        // than lingering until the 8h JWT expires.
        if (!request.IsActive)
        {
            await userSessionService.RevokeAllForUserAsync(id, "Account deactivated", cancellationToken);
        }

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        var invitations = await LoadLatestInvitationsAsync([user.Id], cancellationToken);
        return Ok(WithInvitation(Map(user, workforceProfiles), invitations.GetValueOrDefault(user.Id)));
    }

    [HttpPost("{id:guid}/resend-credentials")]
    [HttpPost("{id:guid}/resend-invite")]
    [HttpPost("{id:guid}/resend-initial-invite")]
    public async Task<ActionResult<CredentialDeliveryResponse>> ResendCredentials(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (!user.IsActive || !user.MustChangePassword)
        {
            throw new BusinessRuleException("Initial invitations can only be resent for users pending activation.");
        }

        if (!user.HasEmail)
        {
            throw new BusinessRuleException("This user has no email address, so an invitation link cannot be sent. Use 'reset password' to generate a new temporary password instead.");
        }

        var delivery = await SendAccountSetupAsync(user, cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "users.invitation.resent",
            nameof(User),
            user.Id.ToString(),
            delivery.Success
                ? $"Initial account setup invitation sent to '{DisplayIdentifier(user)}'. Delivery status: {delivery.Status}."
                : $"Initial account setup invitation attempt for '{DisplayIdentifier(user)}' did not complete. Delivery status: {delivery.Status}.",
            severity: delivery.Success ? "Info" : "Warning",
            cancellationToken: cancellationToken);

        var message = delivery.Success
            ? delivery.Message
            : "Account setup link could not be sent. The user's access remains pending until an invitation is delivered.";

        return Ok(new CredentialDeliveryResponse(user.Id, user.FullName, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, message));
    }

    [HttpPost("{id:guid}/reset-password")]
    [HttpPost("{id:guid}/send-password-reset")]
    public async Task<ActionResult<CredentialDeliveryResponse>> ResetPassword(Guid id, [FromBody] ResetPasswordRequest? request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        if (!user.IsActive)
        {
            throw new BusinessRuleException("Passwords can only be reset for active users.");
        }

        // The admin's choice: type a password directly, or generate one and have it revealed once —
        // either way nothing is emailed, so this branch works even for users with no email address.
        // It is also allowed for users who have not finished setup yet (MustChangePassword): a
        // no-email user created without a password has no other way to receive one.
        var adminSuppliedPassword = request?.ResolvePasswordOrNull();
        var generateAndReveal = request?.GenerateAndReveal ?? false;
        if (adminSuppliedPassword is null && !generateAndReveal && user.MustChangePassword)
        {
            throw new BusinessRuleException("This user has not completed account setup yet. Resend the invitation instead of a password reset.");
        }

        if (adminSuppliedPassword is not null || generateAndReveal)
        {
            var newPassword = adminSuppliedPassword ?? temporaryPasswordService.Generate();
            await tenantSecurityPolicyService.ValidatePasswordAsync(TenantId, newPassword, cancellationToken);
            user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
            user.MustChangePassword = true;
            await dbContext.SaveChangesAsync(cancellationToken);
            await userSessionService.RevokeAllForUserAsync(user.Id, "Password reset by administrator", cancellationToken);

            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "users.password-reset.set-directly",
                nameof(User),
                user.Id.ToString(),
                adminSuppliedPassword is not null
                    ? $"Password reset directly for '{DisplayIdentifier(user)}' by an administrator. No email was sent."
                    : $"A new temporary password was generated for '{DisplayIdentifier(user)}' by an administrator. No email was sent.",
                severity: "Warning",
                cancellationToken: cancellationToken);

            var directMessage = adminSuppliedPassword is not null
                ? "Password reset. Share the new password with the user directly."
                : "A new temporary password was generated. Copy it now and share it with the user directly — it will not be shown again.";

            return Ok(new CredentialDeliveryResponse(
                user.Id, user.FullName, user.Email, true, user.LastCredentialSentAt, "PasswordResetDirectly", directMessage,
                TemporaryPassword: generateAndReveal ? newPassword : null));
        }

        if (!user.HasEmail)
        {
            throw new BusinessRuleException("This user has no email address. Generate a temporary password or set one directly instead of sending a reset email.");
        }

        var issued = await PasswordLinkTokens.IssueAsync(
            dbContext,
            user,
            PasswordLinkPurposes.PasswordReset,
            authSecurityOptions.Value,
            cancellationToken);

        var delivery = await credentialDeliveryService.SendPasswordResetLinkAsync(
            TenantId,
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
            TenantId,
            UserId,
            "users.password-reset.requested",
            nameof(User),
            user.Id.ToString(),
            delivery.Success
                ? $"Password reset link sent to '{DisplayIdentifier(user)}'. Delivery status: {delivery.Status}."
                : $"Password reset link attempt for '{DisplayIdentifier(user)}' did not complete. Delivery status: {delivery.Status}.",
            severity: delivery.Success ? "Info" : "Warning",
            cancellationToken: cancellationToken);

        var message = delivery.Success
            ? delivery.Message
            : "Password reset email could not be sent. The user's password remains unchanged.";
        return Ok(new CredentialDeliveryResponse(user.Id, user.FullName, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, message));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users
            .Include(x => x.BranchAssignments)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        user.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncWorkforceProfileAsync(user, [], null, cancellationToken);

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "User deleted",
            nameof(User),
            user.Id.ToString(),
            $"Deleted user '{DisplayIdentifier(user)}'.",
            cancellationToken);

        // Soft-delete deactivates the account — revoke active sessions so the deleted user loses
        // access immediately.
        await userSessionService.RevokeAllForUserAsync(id, "Account deleted", cancellationToken);
        return NoContent();
    }

    private static void ValidateRequest(string fullName, string? credentialDeliveryMethod)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BusinessRuleException("Full name is required.");
        }

        _ = NormalizeCredentialDeliveryMethod(credentialDeliveryMethod);
    }

    private static void ValidateUpdateRequest(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BusinessRuleException("Full name is required.");
        }
    }

    private const string NoEmailDomain = "no-email.invalid";

    /// <summary>
    /// A blank <paramref name="requestedEmail"/> means this person has no email address at all —
    /// not uncommon for field staff — so they sign in by <see cref="User.Username"/> instead. The
    /// stored placeholder is GUID-based so it can never collide with a real address and never needs
    /// the uniqueness check a real email gets.
    /// </summary>
    private async Task<(string Email, bool HasEmail)> ResolveEmailAsync(string? requestedEmail, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        // A caller that round-trips the current record unchanged (e.g. saving permissions without
        // touching the email field) hands this placeholder straight back — treat that exactly like
        // a blank submission instead of "fixing" the account back to HasEmail=true.
        if (string.IsNullOrWhiteSpace(requestedEmail) ||
            requestedEmail.Trim().EndsWith($"@{NoEmailDomain}", StringComparison.OrdinalIgnoreCase))
        {
            return ($"user-{Guid.NewGuid():N}@{NoEmailDomain}", false);
        }

        var normalizedEmail = requestedEmail.Trim().ToLowerInvariant();
        var emailExists = await dbContext.Users.AnyAsync(
            x => x.Email.ToLower() == normalizedEmail && x.Id != (excludeUserId ?? Guid.Empty),
            cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        return (normalizedEmail, true);
    }

    private static string SlugifyUsername(string seed)
    {
        var normalized = Regex.Replace(seed.Trim().ToLowerInvariant(), "[^a-z0-9]+", ".").Trim('.');
        return string.IsNullOrEmpty(normalized) ? "user" : normalized[..Math.Min(normalized.Length, 80)];
    }

    /// <summary>Unique within the tenant only — <see cref="User.Username"/> is not unique platform-wide.</summary>
    private async Task<string> GenerateUniqueUsernameAsync(string seed, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        var baseUsername = SlugifyUsername(seed);
        var username = baseUsername;
        var counter = 2;

        while (await dbContext.Users.AnyAsync(
            x => x.TenantId == TenantId && x.Username == username && x.Id != (excludeUserId ?? Guid.Empty),
            cancellationToken))
        {
            username = $"{baseUsername[..Math.Min(baseUsername.Length, 90)]}.{counter}";
            counter += 1;
        }

        return username;
    }

    private async Task<string> ResolveUsernameAsync(string? requestedUsername, string fallbackSeed, Guid? excludeUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(requestedUsername))
        {
            return await GenerateUniqueUsernameAsync(fallbackSeed, excludeUserId, cancellationToken);
        }

        var normalized = SlugifyUsername(requestedUsername);
        var exists = await dbContext.Users.AnyAsync(
            x => x.TenantId == TenantId && x.Username == normalized && x.Id != (excludeUserId ?? Guid.Empty),
            cancellationToken);
        if (exists)
        {
            throw new BusinessRuleException("Username is already in use in this tenant.");
        }

        return normalized;
    }

    /// <summary>What to show in audit trails: a no-email account's <see cref="User.Email"/> is a
    /// system-generated placeholder, so its username reads far better in log messages.</summary>
    private static string DisplayIdentifier(User user) =>
        !user.HasEmail && !string.IsNullOrWhiteSpace(user.Username) ? user.Username : user.Email;

    private static string NormalizeRole(string role)
    {
        var normalizedRole = role.Trim();
        if (string.Equals(normalizedRole, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Only SuperAdmin can create or manage SuperAdmin accounts.");
        }

        // Canonicalise against the tenant role list only. This is what stops a caller from
        // assigning a platform role name (e.g. "Finance") and inheriting platform meaning:
        // the value returned is always the tenant-side constant.
        var match = AppRoles.TenantRoles
            .FirstOrDefault(candidate => string.Equals(candidate, normalizedRole, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            return match;
        }

        throw new BusinessRuleException($"Role must be one of: {string.Join(", ", AppRoles.TenantRoles)}.");
    }

    private static List<Guid> NormalizeBranchIds(IReadOnlyCollection<Guid>? branchIds) =>
        branchIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList()
        ?? [];

    private async Task<Guid?> ResolveDefaultBranchIdAsync(
        string role,
        IReadOnlyCollection<Guid> branchIds,
        Guid? requestedDefaultBranchId,
        bool hasAllBranchAccess,
        CancellationToken cancellationToken)
    {
        if (requestedDefaultBranchId.HasValue)
        {
            var exists = await dbContext.Branches.AnyAsync(
                x => x.TenantId == TenantId && x.IsActive && x.Id == requestedDefaultBranchId.Value,
                cancellationToken);

            if (!exists)
            {
                throw new BusinessRuleException("Default branch was not found for this tenant.");
            }

            if (!hasAllBranchAccess && !string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) && !branchIds.Contains(requestedDefaultBranchId.Value))
            {
                throw new BusinessRuleException("Default branch must be one of the assigned branches.");
            }

            return requestedDefaultBranchId.Value;
        }

        if (branchIds.Count == 1)
        {
            return branchIds.First();
        }

        return null;
    }

    private async Task<Dictionary<Guid, Technician>> LoadWorkforceProfilesAsync(
        IReadOnlyCollection<User> users,
        CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return [];
        }

        var userIds = users.Select(x => x.Id).ToHashSet();
        var normalizedEmails = users
            .Select(x => x.Email.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var profiles = await dbContext.Technicians
            .Include(x => x.AssignmentGroups.Where(member => member.IsActive))
            .ThenInclude(x => x.AssignmentGroup)
            .Where(x => x.TenantId == TenantId)
            .Where(x => (x.UserId.HasValue && userIds.Contains(x.UserId.Value)) || normalizedEmails.Contains(x.Email.ToLower()))
            .ToListAsync(cancellationToken);

        var byUserId = new Dictionary<Guid, Technician>();
        foreach (var user in users)
        {
            var profile = profiles.FirstOrDefault(x => x.UserId == user.Id)
                ?? profiles.FirstOrDefault(x => string.Equals(x.Email, user.Email, StringComparison.OrdinalIgnoreCase));

            if (profile is not null)
            {
                byUserId[user.Id] = profile;
            }
        }

        return byUserId;
    }

    private async Task SyncWorkforceProfileAsync(User user, IReadOnlyCollection<Guid>? assignmentGroupIds, string? phoneNumber, CancellationToken cancellationToken)
    {
        var technician = await dbContext.Technicians
            .Include(x => x.AssignmentGroups)
            .SingleOrDefaultAsync(
                x => x.TenantId == TenantId && (x.UserId == user.Id || x.Email.ToLower() == user.Email.ToLower()),
                cancellationToken);

        var normalizedGroupIds = user.IsActive
            ? assignmentGroupIds is null
                ? technician?.AssignmentGroups.Where(x => x.IsActive).Select(x => x.AssignmentGroupId).Distinct().ToList() ?? []
                : assignmentGroupIds.Where(x => x != Guid.Empty).Distinct().ToList()
            : [];

        // A Technician-role user always needs a technician profile: it is what makes them selectable
        // when creating or assigning a work order. Other roles only get one when they join a group
        // or give a phone number for field work.
        var isTechnicianRole = string.Equals(user.Role, AppRoles.Technician, StringComparison.OrdinalIgnoreCase);
        if (technician is null && !isTechnicianRole && normalizedGroupIds.Count == 0 && string.IsNullOrWhiteSpace(phoneNumber))
        {
            return;
        }

        technician ??= new Technician
        {
            TenantId = TenantId,
            UserId = user.Id
        };

        technician.UserId = user.Id;
        technician.FullName = user.FullName;
        technician.Email = user.Email;
        technician.Phone = phoneNumber;
        technician.BranchId = ResolveWorkforceBranchId(user);
        technician.IsActive = user.IsActive;

        if (dbContext.Entry(technician).State == EntityState.Detached)
        {
            dbContext.Technicians.Add(technician);
        }

        var groups = normalizedGroupIds.Count == 0
            ? []
            : await dbContext.AssignmentGroups
                .Where(x => x.TenantId == TenantId && normalizedGroupIds.Contains(x.Id) && x.IsActive)
                .ToListAsync(cancellationToken);

        if (groups.Count != normalizedGroupIds.Count)
        {
            throw new BusinessRuleException("One or more assignment groups were not found.");
        }

        var currentMemberships = await dbContext.AssignmentGroupMembers
            .Where(x => x.TenantId == TenantId && x.TechnicianId == technician.Id)
            .ToListAsync(cancellationToken);

        dbContext.AssignmentGroupMembers.RemoveRange(currentMemberships.Where(x => !normalizedGroupIds.Contains(x.AssignmentGroupId)));

        foreach (var membership in currentMemberships.Where(x => normalizedGroupIds.Contains(x.AssignmentGroupId)))
        {
            membership.IsActive = user.IsActive;
        }

        var existingGroupIds = currentMemberships.Select(x => x.AssignmentGroupId).ToHashSet();
        foreach (var groupId in normalizedGroupIds.Where(groupId => !existingGroupIds.Contains(groupId)))
        {
            dbContext.AssignmentGroupMembers.Add(new AssignmentGroupMember
            {
                TenantId = TenantId,
                AssignmentGroupId = groupId,
                TechnicianId = technician.Id,
                IsLead = false,
                IsActive = true,
                AddedAt = DateTime.UtcNow
            });
        }

        technician.AssignmentGroup = groups.FirstOrDefault()?.Name;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?> TryBeginSerializableTransactionAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static Guid? ResolveWorkforceBranchId(User user)
    {
        if (user.DefaultBranchId.HasValue)
        {
            return user.DefaultBranchId.Value;
        }

        return user.BranchAssignments
            .OrderByDescending(x => x.IsDefault)
            .Select(x => (Guid?)x.BranchId)
            .FirstOrDefault();
    }

    private static UserPermission ToEntity(UserPermissionsModel permissions)
    {
        var entity = new UserPermission();
        ApplyPermissions(entity, permissions);
        return entity;
    }

    private static void ApplyPermissions(UserPermission entity, UserPermissionsModel permissions)
    {
        entity.CanViewWorkOrders = permissions.CanViewWorkOrders;
        entity.CanCreateWorkOrders = permissions.CanCreateWorkOrders;
        entity.CanAssignWorkOrders = permissions.CanAssignWorkOrders;
        entity.CanCompleteWorkOrders = permissions.CanCompleteWorkOrders;
        entity.CanViewKnowledge = permissions.CanViewKnowledge;
        entity.CanManageKnowledge = permissions.CanManageKnowledge;
        entity.CanViewMaterials = permissions.CanViewMaterials;
        entity.CanRequestMaterials = permissions.CanRequestMaterials;
        entity.CanManageMaterials = permissions.CanManageMaterials;
        entity.CanApproveMaterials = permissions.CanApproveMaterials;
        entity.CanIssueMaterials = permissions.CanIssueMaterials;
        entity.CanManageAssets = permissions.CanManageAssets;
        entity.CanManageSettings = permissions.CanManageSettings;
        entity.CanViewReports = permissions.CanViewReports;
        entity.CanViewFinance = permissions.CanViewFinance;
        entity.CanManageFinance = permissions.CanManageFinance;
        entity.CanViewFinanceRates = permissions.CanViewFinanceRates;
        entity.CanManageFinanceRates = permissions.CanManageFinanceRates;
        entity.CanViewFinanceApprovals = permissions.CanViewFinanceApprovals;
        entity.CanManageFinanceApprovals = permissions.CanManageFinanceApprovals;
        entity.CanViewBillableWork = permissions.CanViewBillableWork;
        entity.CanManageBillableWork = permissions.CanManageBillableWork;
        entity.CanOverrideMaterialStock = permissions.CanOverrideMaterialStock;
        entity.CanViewCrm = permissions.CanViewCrm;
        entity.CanManageCrm = permissions.CanManageCrm;
    }

    private UserResponse Map(
        User user,
        IReadOnlyDictionary<Guid, Technician> workforceProfiles,
        CredentialDeliverySummaryResponse? credentialDelivery = null)
    {
        workforceProfiles.TryGetValue(user.Id, out var workforceProfile);

        var permissions = user.Permission is null
            ? new PermissionResponse()
            : new PermissionResponse
            {
                CanViewWorkOrders = user.Permission.CanViewWorkOrders,
                CanCreateWorkOrders = user.Permission.CanCreateWorkOrders,
                CanAssignWorkOrders = user.Permission.CanAssignWorkOrders,
                CanCompleteWorkOrders = user.Permission.CanCompleteWorkOrders,
                CanViewKnowledge = user.Permission.CanViewKnowledge,
                CanManageKnowledge = user.Permission.CanManageKnowledge,
                CanViewMaterials = user.Permission.CanViewMaterials,
                CanRequestMaterials = user.Permission.CanRequestMaterials,
                CanManageMaterials = user.Permission.CanManageMaterials,
                CanApproveMaterials = user.Permission.CanApproveMaterials,
                CanIssueMaterials = user.Permission.CanIssueMaterials,
                CanManageAssets = user.Permission.CanManageAssets,
                CanManageSettings = user.Permission.CanManageSettings,
                CanViewReports = user.Permission.CanViewReports,
                CanViewFinance = user.Permission.CanViewFinance,
                CanManageFinance = user.Permission.CanManageFinance,
                CanViewFinanceRates = user.Permission.CanViewFinanceRates,
                CanManageFinanceRates = user.Permission.CanManageFinanceRates,
                CanViewFinanceApprovals = user.Permission.CanViewFinanceApprovals,
                CanManageFinanceApprovals = user.Permission.CanManageFinanceApprovals,
                CanViewBillableWork = user.Permission.CanViewBillableWork,
                CanManageBillableWork = user.Permission.CanManageBillableWork,
                CanOverrideMaterialStock = user.Permission.CanOverrideMaterialStock,
                CanViewCrm = user.Permission.CanViewCrm,
                CanManageCrm = user.Permission.CanManageCrm
            };

        return new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Username,
            user.HasEmail,
            workforceProfile?.Phone,
            user.Role,
            user.JobTitle,
            user.Department,
            user.IsActive,
            user.HasAllBranchAccess,
            user.DefaultBranchId,
            user.DefaultBranch?.Name,
            user.BranchAssignments.Select(x => x.BranchId).OrderBy(x => x).ToList(),
            workforceProfile?.Id,
            workforceProfile?.AssignmentGroups
                .Where(x => x.IsActive && x.AssignmentGroup is not null)
                .OrderByDescending(x => x.IsLead)
                .ThenBy(x => x.AssignmentGroup!.Name)
                .Select(x => new UserAssignmentGroupResponse(
                    x.AssignmentGroupId,
                    x.AssignmentGroup!.Name,
                    x.IsLead))
                .ToList() ?? [],
            user.CreatedAt,
            user.UpdatedAt,
            permissions,
            user.MustChangePassword,
            user.LastCredentialSentAt,
            credentialDelivery);
    }

    private async Task<Dictionary<Guid, PasswordResetToken>> LoadLatestInvitationsAsync(Guid[] userIds, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.PasswordResetTokens.AsNoTracking()
            .Where(x => userIds.Contains(x.UserId) && x.User != null && x.User.TenantId == TenantId
                && x.Purpose == PasswordLinkPurposes.Invitation)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return tokens.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.First());
    }

    private static UserResponse WithInvitation(UserResponse response, PasswordResetToken? invitation) => response with
    {
        InvitationStatus = !response.MustChangePassword ? "Active"
            : invitation?.ExpiresAt <= DateTime.UtcNow ? "Expired" : "Invited",
        InvitationExpiresAt = response.MustChangePassword ? invitation?.ExpiresAt : null
    };

    private static string NormalizeCredentialDeliveryMethod(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "TemporaryPassword" : value.Trim();
        return normalized switch
        {
            "InviteEmail" => "InviteEmail",
            "TemporaryPassword" => "TemporaryPassword",
            "Both" => "Both",
            "None" => "None",
            _ => throw new BusinessRuleException("Credential delivery method must be InviteEmail, TemporaryPassword, Both, or None.")
        };
    }

    private async Task<UserCredentialDeliveryResult> SendAccountSetupAsync(User user, CancellationToken cancellationToken)
    {
        var issued = await PasswordLinkTokens.IssueAsync(dbContext, user, PasswordLinkPurposes.Invitation,
            authSecurityOptions.Value, cancellationToken);

        return await credentialDeliveryService.SendPasswordResetLinkAsync(
            TenantId,
            user,
            new PasswordResetLinkDeliveryRequest(
                credentialDeliveryService.BuildInviteUrl(issued.Token),
                issued.ExpiresAt,
                PasswordLinkDeliveryPurpose.AccountSetup),
            cancellationToken);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>A blank <see cref="Email"/> means this person has no email address — they sign in with
/// <see cref="Username"/> (auto-generated from <see cref="FullName"/> when left blank) instead.</summary>
public sealed record CreateUserRequest(
    string FullName,
    string? Email,
    string? PhoneNumber,
    string? Password,
    string Role,
    string? JobTitle,
    string? Department,
    bool IsActive,
    string? CredentialDeliveryMethod,
    PermissionRequest? Permissions,
    IReadOnlyCollection<Guid>? BranchIds,
    Guid? DefaultBranchId,
    bool HasAllBranchAccess,
    IReadOnlyCollection<Guid>? AssignmentGroupIds,
    string? Username = null);

public sealed record UpdateUserRequest(
    string FullName,
    string? Email,
    string? PhoneNumber,
    string? Password,
    string Role,
    string? JobTitle,
    string? Department,
    bool IsActive,
    PermissionRequest? Permissions,
    IReadOnlyCollection<Guid>? BranchIds,
    Guid? DefaultBranchId,
    bool HasAllBranchAccess,
    IReadOnlyCollection<Guid>? AssignmentGroupIds,
    string? Username = null);

public sealed record PermissionRequest(
    bool CanViewWorkOrders,
    bool CanCreateWorkOrders,
    bool CanAssignWorkOrders,
    bool CanCompleteWorkOrders,
    bool CanViewKnowledge,
    bool CanManageKnowledge,
    bool CanViewMaterials,
    bool CanRequestMaterials,
    bool CanManageMaterials,
    bool CanApproveMaterials,
    bool CanIssueMaterials,
    bool CanManageAssets,
    bool CanManageSettings,
    bool CanViewReports,
    bool CanViewFinance,
    bool CanManageFinance,
    bool CanViewFinanceRates,
    bool CanManageFinanceRates,
    bool CanViewFinanceApprovals,
    bool CanManageFinanceApprovals,
    bool CanViewBillableWork,
    bool CanManageBillableWork,
    bool CanOverrideMaterialStock,
    bool CanViewCrm = false,
    bool CanManageCrm = false)
{
    public UserPermissionsModel ToModel() =>
        new()
        {
            CanViewWorkOrders = CanViewWorkOrders,
            CanCreateWorkOrders = CanCreateWorkOrders,
            CanAssignWorkOrders = CanAssignWorkOrders,
            CanCompleteWorkOrders = CanCompleteWorkOrders,
            CanViewKnowledge = CanViewKnowledge,
            CanManageKnowledge = CanManageKnowledge,
            CanViewMaterials = CanViewMaterials,
            CanRequestMaterials = CanRequestMaterials,
            CanManageMaterials = CanManageMaterials,
            CanApproveMaterials = CanApproveMaterials,
            CanIssueMaterials = CanIssueMaterials,
            CanManageAssets = CanManageAssets,
            CanManageSettings = CanManageSettings,
            CanViewReports = CanViewReports,
            CanViewFinance = CanViewFinance,
            CanManageFinance = CanManageFinance,
            CanViewFinanceRates = CanViewFinanceRates,
            CanManageFinanceRates = CanManageFinanceRates,
            CanViewFinanceApprovals = CanViewFinanceApprovals,
            CanManageFinanceApprovals = CanManageFinanceApprovals,
            CanViewBillableWork = CanViewBillableWork,
            CanManageBillableWork = CanManageBillableWork,
            CanOverrideMaterialStock = CanOverrideMaterialStock,
            CanViewCrm = CanViewCrm,
            CanManageCrm = CanManageCrm
        };
}

public sealed record UserAssignmentGroupResponse(Guid Id, string Name, bool IsLead);

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Email,
    string? Username,
    bool HasEmail,
    string? PhoneNumber,
    string Role,
    string? JobTitle,
    string? Department,
    bool IsActive,
    bool HasAllBranchAccess,
    Guid? DefaultBranchId,
    string? DefaultBranchName,
    IReadOnlyCollection<Guid> BranchIds,
    Guid? LinkedTechnicianId,
    IReadOnlyCollection<UserAssignmentGroupResponse> AssignmentGroups,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    PermissionResponse Permissions,
    bool MustChangePassword,
    DateTime? LastCredentialSentAt,
    CredentialDeliverySummaryResponse? CredentialDelivery,
    string? InvitationStatus = null,
    DateTime? InvitationExpiresAt = null);

public sealed record CredentialDeliverySummaryResponse(
    bool Success,
    string Status,
    string? Message,
    string? TemporaryPassword = null);

public sealed record CredentialDeliveryResponse(
    Guid Id,
    string FullName,
    string Email,
    bool Success,
    DateTime? LastCredentialSentAt,
    string Status,
    string? Message,
    string? TemporaryPassword = null);

public sealed record UpdateUserStatusRequest(bool IsActive);

/// <summary>
/// <see cref="TemporaryPassword"/>/<see cref="NewPassword"/> (an admin typing a password directly)
/// and <see cref="GenerateAndReveal"/> (the server generating one and handing it back once) are the
/// two "admin's choice" ways to set a password without emailing a reset link — useful for any user,
/// essential for one with no email address at all.
/// </summary>
public sealed record ResetPasswordRequest(string? TemporaryPassword, string? NewPassword, bool GenerateAndReveal = false)
{
    public string? ResolvePasswordOrNull()
    {
        var password = !string.IsNullOrWhiteSpace(TemporaryPassword)
            ? TemporaryPassword
            : NewPassword;

        return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }
}
