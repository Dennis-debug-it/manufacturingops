using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ecosys.Infrastructure.Data;

namespace Ecosys.Api.Controllers;

[ApiController]
[EnableRateLimiting("AuthEndpoints")]
[Route("api/auth")]
public sealed class AuthController(
    IMvpAuthService authService,
    ITenantContext tenantContext,
    IUserSessionService userSessionService,
    AppDbContext dbContext,
    IAuditLogService auditLogService,
    IPasswordResetService passwordResetService,
    IPasswordHasher<Domain.Entities.User> passwordHasher,
    ITenantSecurityPolicyService tenantSecurityPolicyService) : ControllerBase
{
    [HttpPost("signup")]
    public async Task<ActionResult<SignupResponse>> Signup([FromBody] SignupRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.SignupAsync(
            request.FullName,
            request.Email,
            request.Password,
            request.CompanyName,
            request.Industry,
            request.Country,
            cancellationToken);

        return Ok(new SignupResponse(
            result.Token,
            result.TenantId,
            result.UserId,
            result.CompanyName,
            result.Role,
            result.JobTitle,
            MapPermissions(result.Permissions, IsPlatformRole(result.Role))));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, cancellationToken);

        return Ok(new LoginResponse(
            result.Token,
            new LoginUserDto(result.UserId, result.FullName, result.Email, result.Role, result.JobTitle, result.Department, result.MustChangePassword, MapPermissions(result.Permissions, IsPlatformRole(result.Role))),
            new LoginTenantDto(result.TenantId, result.CompanyName, result.Country, result.Industry, result.LogoUrl, result.ShowPoweredByEcosys)));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var message = await passwordResetService.RequestAsync(request.Email, cancellationToken);
        return Ok(new MessageResponse(message));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<MessageResponse>> ResetPassword([FromBody] SelfServiceResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var message = await passwordResetService.ResetAsync(request.Token, request.NewPassword, request.ConfirmPassword, cancellationToken);
        return Ok(new MessageResponse(message));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<MessageResponse>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            throw new BusinessRuleException("Current password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new BusinessRuleException("New password is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            throw new BusinessRuleException("Confirm password is required.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new BusinessRuleException("New password and confirmation password must match.");
        }

        var userId = tenantContext.GetRequiredUserId();
        var sessionId = tenantContext.GetRequiredSessionId();
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedException("User context was not found.");

        var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            throw new BusinessRuleException("The current password is incorrect.");
        }

        await tenantSecurityPolicyService.ValidatePasswordAsync(user.TenantId, request.NewPassword, cancellationToken);

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;

        await dbContext.SaveChangesAsync(cancellationToken);
        await userSessionService.RevokeOthersForUserAsync(
            user.Id,
            sessionId,
            "Password changed.",
            cancellationToken);

        await auditLogService.LogAsync(
            user.TenantId,
            user.Id,
            "auth.password.changed",
            nameof(Domain.Entities.User),
            user.Id.ToString(),
            $"Password changed for '{user.Email}'.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        return Ok(new MessageResponse("Your password has been changed successfully."));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var sessionId = tenantContext.GetRequiredSessionId();
        await userSessionService.LogoutAsync(tenantContext.GetRequiredSessionId(), cancellationToken);

        if (!tenantContext.IsSuperAdmin && tenantContext.TenantId.HasValue && tenantContext.UserId.HasValue)
        {
            await auditLogService.LogAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId.Value,
                "Logout",
                nameof(Domain.Entities.UserSession),
                sessionId.ToString(),
                $"User '{tenantContext.Email}' signed out.",
                cancellationToken);
        }

        return Ok();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedContextResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = tenantContext.GetRequiredUserId();
        var user = await dbContext.Users
            .Include(x => x.Tenant)
            .Include(x => x.Permission)
            .Include(x => x.BranchAssignments)
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User context was not found.");

        var isPlatformUser = IsPlatformRole(user.Role);

        if (isPlatformUser)
        {
            return Ok(new AuthenticatedContextResponse(
                new AuthenticatedUserDto(
                    user.Id,
                    user.FullName,
                    user.Email,
                    user.Role,
                    user.JobTitle,
                    user.Department,
                    user.HasAllBranchAccess,
                    user.DefaultBranchId,
                    user.MustChangePassword,
                    MapPermissions(user.Permission, true)),
                new AuthenticatedTenantDto(
                    user.TenantId,
                    user.Tenant?.CompanyName ?? "Platform",
                    user.Tenant?.Country ?? "Platform",
                    user.Tenant?.Industry,
                    user.Tenant?.LogoUrl,
                    user.Tenant?.PrimaryColor ?? "#0F4C81",
                    user.Tenant?.SecondaryColor ?? "#F4B942"),
                []));
        }

        var branches = user.HasAllBranchAccess
            ? await dbContext.Branches
                .Where(x => x.TenantId == user.TenantId && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new AuthenticatedBranchDto(x.Id, x.Name, x.Code, x.Location, x.IsActive))
                .ToListAsync(cancellationToken)
            : await dbContext.Branches
                .Where(x => x.TenantId == user.TenantId && x.IsActive && user.BranchAssignments.Select(a => a.BranchId).Contains(x.Id))
                .OrderBy(x => x.Name)
                .Select(x => new AuthenticatedBranchDto(x.Id, x.Name, x.Code, x.Location, x.IsActive))
                .ToListAsync(cancellationToken);

        return Ok(new AuthenticatedContextResponse(
            new AuthenticatedUserDto(
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                user.JobTitle,
                user.Department,
                user.HasAllBranchAccess,
                user.DefaultBranchId,
                user.MustChangePassword,
                MapPermissions(user.Permission, isPlatformUser)),
            new AuthenticatedTenantDto(
                user.TenantId,
                user.Tenant?.CompanyName ?? string.Empty,
                user.Tenant?.Country ?? string.Empty,
                user.Tenant?.Industry,
                user.Tenant?.LogoUrl,
                user.Tenant?.PrimaryColor ?? "#0F4C81",
                user.Tenant?.SecondaryColor ?? "#F4B942"),
            branches));
    }

    private static PermissionResponse MapPermissions(UserPermissionsModel permissions, bool isPlatformOwner) =>
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
            CanViewPlatformTenants = isPlatformOwner,
            CanCreatePlatformTenants = isPlatformOwner,
            CanEditPlatformTenants = isPlatformOwner,
            CanUpdatePlatformTenantStatus = isPlatformOwner,
            CanDeactivatePlatformTenants = isPlatformOwner
        };

    private static PermissionResponse MapPermissions(Domain.Entities.UserPermission? permission, bool isPlatformOwner) =>
        permission is null
            ? new PermissionResponse
            {
                CanViewPlatformTenants = isPlatformOwner,
                CanCreatePlatformTenants = isPlatformOwner,
                CanEditPlatformTenants = isPlatformOwner,
                CanUpdatePlatformTenantStatus = isPlatformOwner,
                CanDeactivatePlatformTenants = isPlatformOwner
            }
            : new PermissionResponse
            {
                CanViewWorkOrders = permission.CanViewWorkOrders,
                CanCreateWorkOrders = permission.CanCreateWorkOrders,
                CanAssignWorkOrders = permission.CanAssignWorkOrders,
                CanCompleteWorkOrders = permission.CanCompleteWorkOrders,
                CanViewKnowledge = permission.CanViewKnowledge,
                CanManageKnowledge = permission.CanManageKnowledge,
                CanViewMaterials = permission.CanViewMaterials,
                CanRequestMaterials = permission.CanRequestMaterials,
                CanManageMaterials = permission.CanManageMaterials,
                CanApproveMaterials = permission.CanApproveMaterials,
                CanIssueMaterials = permission.CanIssueMaterials,
                CanManageAssets = permission.CanManageAssets,
                CanManageSettings = permission.CanManageSettings,
                CanViewReports = permission.CanViewReports,
                CanViewFinance = permission.CanViewFinance,
                CanManageFinance = permission.CanManageFinance,
                CanViewFinanceRates = permission.CanViewFinanceRates,
                CanManageFinanceRates = permission.CanManageFinanceRates,
                CanViewFinanceApprovals = permission.CanViewFinanceApprovals,
                CanManageFinanceApprovals = permission.CanManageFinanceApprovals,
                CanViewBillableWork = permission.CanViewBillableWork,
                CanManageBillableWork = permission.CanManageBillableWork,
                CanOverrideMaterialStock = permission.CanOverrideMaterialStock,
                CanViewPlatformTenants = isPlatformOwner,
                CanCreatePlatformTenants = isPlatformOwner,
                CanEditPlatformTenants = isPlatformOwner,
                CanUpdatePlatformTenantStatus = isPlatformOwner,
                CanDeactivatePlatformTenants = isPlatformOwner
            };

    private static bool IsPlatformRole(string role) =>
        AppRoles.PlatformRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public sealed record SignupRequest(
    string FullName,
    string Email,
    string Password,
    string CompanyName,
    string? Industry,
    string Country);

public sealed record SignupResponse(
    string Token,
    Guid TenantId,
    Guid UserId,
    string CompanyName,
    string Role,
    string? JobTitle,
    PermissionResponse Permissions);

public sealed record LoginRequest(string Email, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record SelfServiceResetPasswordRequest(string Token, string NewPassword, string ConfirmPassword);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record MessageResponse(string Message);

public sealed record LoginResponse(
    string Token,
    LoginUserDto User,
    LoginTenantDto Tenant);

public sealed record LoginUserDto(Guid UserId, string FullName, string Email, string Role, string? JobTitle, string? Department, bool MustChangePassword, PermissionResponse Permissions);

public sealed record LoginTenantDto(
    Guid TenantId,
    string CompanyName,
    string Country,
    string? Industry,
    string? LogoUrl,
    bool ShowPoweredByEcosys);

public sealed record PermissionResponse
{
    public bool CanViewWorkOrders { get; init; }
    public bool CanCreateWorkOrders { get; init; }
    public bool CanAssignWorkOrders { get; init; }
    public bool CanCompleteWorkOrders { get; init; }
    public bool CanViewKnowledge { get; init; }
    public bool CanManageKnowledge { get; init; }
    public bool CanViewMaterials { get; init; }
    public bool CanRequestMaterials { get; init; }
    public bool CanManageMaterials { get; init; }
    public bool CanApproveMaterials { get; init; }
    public bool CanIssueMaterials { get; init; }
    public bool CanManageAssets { get; init; }
    public bool CanManageSettings { get; init; }
    public bool CanViewReports { get; init; }
    public bool CanViewFinance { get; init; }
    public bool CanManageFinance { get; init; }
    public bool CanViewFinanceRates { get; init; }
    public bool CanManageFinanceRates { get; init; }
    public bool CanViewFinanceApprovals { get; init; }
    public bool CanManageFinanceApprovals { get; init; }
    public bool CanViewBillableWork { get; init; }
    public bool CanManageBillableWork { get; init; }
    public bool CanOverrideMaterialStock { get; init; }
    public bool CanViewPlatformTenants { get; init; }
    public bool CanCreatePlatformTenants { get; init; }
    public bool CanEditPlatformTenants { get; init; }
    public bool CanUpdatePlatformTenantStatus { get; init; }
    public bool CanDeactivatePlatformTenants { get; init; }
}

public sealed record AuthenticatedContextResponse(
    AuthenticatedUserDto User,
    AuthenticatedTenantDto Tenant,
    IReadOnlyCollection<AuthenticatedBranchDto> Branches);

public sealed record AuthenticatedUserDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    string? JobTitle,
    string? Department,
    bool HasAllBranchAccess,
    Guid? DefaultBranchId,
    bool MustChangePassword,
    PermissionResponse Permissions);

public sealed record AuthenticatedTenantDto(
    Guid TenantId,
    string CompanyName,
    string Country,
    string? Industry,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor);

public sealed record AuthenticatedBranchDto(
    Guid Id,
    string Name,
    string Code,
    string? Location,
    bool IsActive);
