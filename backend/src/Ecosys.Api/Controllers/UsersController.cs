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
using System.Security.Cryptography;

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
    Microsoft.Extensions.Options.IOptions<Ecosys.Shared.Options.AuthSecurityOptions> authSecurityOptions,
    IUserSessionService userSessionService) : TenantAwareControllerBase(tenantContext)
{
    private int InviteExpiryHours => AccountSetupExpiry.ResolveHours(authSecurityOptions.Value);
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
        return Ok(users.Select(user => Map(user, workforceProfiles)).ToList());
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
        return Ok(Map(user, workforceProfiles));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        ValidateRequest(request.FullName, request.Email, request.CredentialDeliveryMethod);
        var transaction = await TryBeginSerializableTransactionAsync(cancellationToken);

        await licenseGuardService.EnsureCanCreateUserAsync(TenantId, cancellationToken);
        var normalizedRole = NormalizeRole(request.Role);
        var deliveryMethod = NormalizeCredentialDeliveryMethod(request.CredentialDeliveryMethod);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailExists = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        var permissions = permissionTemplateService.Merge(normalizedRole, request.JobTitle, request.Permissions?.ToModel());
        var normalizedBranchIds = NormalizeBranchIds(request.BranchIds);
        var resolvedDefaultBranchId = await ResolveDefaultBranchIdAsync(normalizedRole, normalizedBranchIds, request.DefaultBranchId, request.HasAllBranchAccess, cancellationToken);
        var bootstrapPassword = temporaryPasswordService.Generate();

        var user = new User
        {
            TenantId = TenantId,
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
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

        var credentialDelivery = new CredentialDeliverySummaryResponse(false, "NotRequested", null);
        if (!string.Equals(deliveryMethod, "None", StringComparison.OrdinalIgnoreCase))
        {
            var deliveryResult = await SendAccountSetupAsync(user, cancellationToken);
            credentialDelivery = new CredentialDeliverySummaryResponse(deliveryResult.Success, deliveryResult.Status, deliveryResult.Message);

            if (credentialDelivery.Success)
            {
                user.LastCredentialSentAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "User created",
            nameof(User),
            user.Id.ToString(),
            $"Created user '{user.Email}'.",
            cancellationToken);

        if (credentialDelivery.Success)
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
        ValidateUpdateRequest(request.FullName, request.Email);

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

        if (!user.IsActive && request.IsActive)
        {
            await licenseGuardService.EnsureCanCreateUserAsync(TenantId, cancellationToken);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailExists = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail && x.Id != id, cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        var permissions = permissionTemplateService.Merge(normalizedRole, request.JobTitle, request.Permissions?.ToModel());
        var normalizedBranchIds = NormalizeBranchIds(request.BranchIds);
        var resolvedDefaultBranchId = await ResolveDefaultBranchIdAsync(normalizedRole, normalizedBranchIds, request.DefaultBranchId, request.HasAllBranchAccess, cancellationToken);

        user.FullName = request.FullName.Trim();
        user.Email = normalizedEmail;
        user.Role = normalizedRole;
        user.JobTitle = request.JobTitle?.Trim();
        user.Department = request.Department?.Trim();
        user.IsActive = request.IsActive;
        user.HasAllBranchAccess = string.Equals(normalizedRole, AppRoles.Admin, StringComparison.OrdinalIgnoreCase) || request.HasAllBranchAccess;
        user.DefaultBranchId = resolvedDefaultBranchId;
        user.Permission ??= new UserPermission();
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
            $"Updated user '{user.Email}'.",
            cancellationToken);

        // Force fresh authorization: this endpoint re-applies the user's role, permission set and
        // branch access, so revoke their existing sessions (they carry the now-stale permission
        // claims baked into the JWT). Skip self-edits so an admin doesn't log themselves out.
        if (id != UserId)
        {
            await userSessionService.RevokeAllForUserAsync(id, "Account updated by administrator", cancellationToken);
        }

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        return Ok(Map(user, workforceProfiles));
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
            await licenseGuardService.EnsureCanCreateUserAsync(TenantId, cancellationToken);
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
            $"{(request.IsActive ? "Activated" : "Deactivated")} user '{user.Email}'.",
            cancellationToken);

        // Terminate all active sessions on deactivation so access is revoked immediately rather
        // than lingering until the 8h JWT expires.
        if (!request.IsActive)
        {
            await userSessionService.RevokeAllForUserAsync(id, "Account deactivated", cancellationToken);
        }

        var workforceProfiles = await LoadWorkforceProfilesAsync([user], cancellationToken);
        return Ok(Map(user, workforceProfiles));
    }

    [HttpPost("{id:guid}/resend-credentials")]
    [HttpPost("{id:guid}/resend-invite")]
    public async Task<ActionResult<CredentialDeliveryResponse>> ResendCredentials(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var delivery = await SendAccountSetupAsync(user, cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (delivery.Success)
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

        var message = delivery.Success
            ? delivery.Message
            : "Account setup link could not be sent. The user's access remains pending until an invitation is delivered.";

        return Ok(new CredentialDeliveryResponse(user.Id, user.FullName, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, message));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<CredentialDeliveryResponse>> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var password = request.ResolvePasswordOrNull() ?? temporaryPasswordService.Generate();
        await tenantSecurityPolicyService.ValidatePasswordAsync(TenantId, password, cancellationToken);

        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("User was not found.");

        var delivery = await credentialDeliveryService.SendAsync(
            TenantId,
            user,
            new UserCredentialDeliveryRequest(
                "password-reset",
                credentialDeliveryService.BuildLoginUrl(),
                password,
                null,
                true),
            cancellationToken);

        if (delivery.Success)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.MustChangePassword = true;
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            TenantId,
            UserId,
            "Password reset",
            nameof(User),
            user.Id.ToString(),
            $"Password reset for '{user.Email}'.",
            cancellationToken);

        if (delivery.Success)
        {
            await auditLogService.LogAsync(
                TenantId,
                UserId,
                "User credentials sent",
                nameof(User),
                user.Id.ToString(),
                $"User credentials sent to '{user.Email}'.",
                cancellationToken);
        }

        return Ok(new CredentialDeliveryResponse(user.Id, user.FullName, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, delivery.Message));
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
            $"Deleted user '{user.Email}'.",
            cancellationToken);

        // Soft-delete deactivates the account — revoke active sessions so the deleted user loses
        // access immediately.
        await userSessionService.RevokeAllForUserAsync(id, "Account deleted", cancellationToken);
        return NoContent();
    }

    private static void ValidateRequest(string fullName, string email, string? credentialDeliveryMethod)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("Full name and email are required.");
        }

        _ = NormalizeCredentialDeliveryMethod(credentialDeliveryMethod);
    }

    private static void ValidateUpdateRequest(string fullName, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
        {
            throw new BusinessRuleException("Full name and email are required.");
        }
    }

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

        if (technician is null && normalizedGroupIds.Count == 0 && string.IsNullOrWhiteSpace(phoneNumber))
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
                CanOverrideMaterialStock = user.Permission.CanOverrideMaterialStock
            };

        return new UserResponse(
            user.Id,
            user.FullName,
            user.Email,
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

    private static string NormalizeCredentialDeliveryMethod(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "TemporaryPassword" : value.Trim();
        return normalized switch
        {
            "InviteEmail" => "InviteEmail",
            "TemporaryPassword" => "TemporaryPassword",
            "Both" => "Both",
            _ => throw new BusinessRuleException("Credential delivery method must be InviteEmail, TemporaryPassword, or Both.")
        };
    }

    private static bool RequiresTemporaryPassword(string credentialDeliveryMethod) =>
        string.Equals(credentialDeliveryMethod, "TemporaryPassword", StringComparison.OrdinalIgnoreCase)
        || string.Equals(credentialDeliveryMethod, "Both", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresInvite(string credentialDeliveryMethod) =>
        string.Equals(credentialDeliveryMethod, "InviteEmail", StringComparison.OrdinalIgnoreCase)
        || string.Equals(credentialDeliveryMethod, "Both", StringComparison.OrdinalIgnoreCase);

    private async Task<UserCredentialDeliveryResult> SendAccountSetupAsync(User user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var existingTokens = await dbContext.PasswordResetTokens
            .Where(x => x.UserId == user.Id && !x.UsedAt.HasValue && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var existingToken in existingTokens)
        {
            existingToken.UsedAt = now;
        }

        var rawToken = CreatePasswordLinkToken();
        var expiresAt = now.AddHours(InviteExpiryHours);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashPasswordLinkToken(rawToken),
            ExpiresAt = expiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await credentialDeliveryService.SendPasswordResetLinkAsync(
            TenantId,
            user,
            new PasswordResetLinkDeliveryRequest(
                credentialDeliveryService.BuildInviteUrl(rawToken),
                expiresAt,
                PasswordLinkDeliveryPurpose.AccountSetup),
            cancellationToken);
    }

    private static string CreatePasswordLinkToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-')
        .Replace('/', '_')
        .TrimEnd('=');

    private static string HashPasswordLinkToken(string token)
    {
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreateUserRequest(
    string FullName,
    string Email,
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
    IReadOnlyCollection<Guid>? AssignmentGroupIds);

public sealed record UpdateUserRequest(
    string FullName,
    string Email,
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
    IReadOnlyCollection<Guid>? AssignmentGroupIds);

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
    bool CanOverrideMaterialStock)
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
            CanOverrideMaterialStock = CanOverrideMaterialStock
        };
}

public sealed record UserAssignmentGroupResponse(Guid Id, string Name, bool IsLead);

public sealed record UserResponse(
    Guid Id,
    string FullName,
    string Email,
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
    CredentialDeliverySummaryResponse? CredentialDelivery);

public sealed record CredentialDeliverySummaryResponse(
    bool Success,
    string Status,
    string? Message);

public sealed record CredentialDeliveryResponse(
    Guid Id,
    string FullName,
    string Email,
    bool Success,
    DateTime? LastCredentialSentAt,
    string Status,
    string? Message);

public sealed record UpdateUserStatusRequest(bool IsActive);

public sealed record ResetPasswordRequest(string? TemporaryPassword, string? NewPassword)
{
    public string? ResolvePasswordOrNull()
    {
        var password = !string.IsNullOrWhiteSpace(TemporaryPassword)
            ? TemporaryPassword
            : NewPassword;

        return string.IsNullOrWhiteSpace(password) ? null : password.Trim();
    }
}
