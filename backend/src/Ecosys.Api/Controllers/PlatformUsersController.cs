using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize(Policy = "PlatformAdminAccess")]
[Route("api/platform/users")]
public sealed class PlatformUsersController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IPasswordHasher<User> passwordHasher,
    IUserCredentialDeliveryService credentialDeliveryService,
    ITemporaryPasswordService temporaryPasswordService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlatformUserResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await QueryPlatformUsers()
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(x => x.Id).ToArray();
        var lastLogins = await dbContext.UserSessions
            .Where(x => userIds.Contains(x.UserId))
            .GroupBy(x => x.UserId)
            .Select(group => new { UserId = group.Key, LastLoginAt = group.Max(x => x.LoginAt) })
            .ToDictionaryAsync(x => x.UserId, x => (DateTime?)x.LastLoginAt, cancellationToken);

        return Ok(users.Select(user => MapUser(user, lastLogins.GetValueOrDefault(user.Id))).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlatformUserResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        var lastLoginAt = await dbContext.UserSessions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.LoginAt)
            .Select(x => (DateTime?)x.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(MapUser(user, lastLoginAt));
    }

    [HttpPost]
    public async Task<ActionResult<PlatformUserResponse>> Create([FromBody] UpsertPlatformUserRequest request, CancellationToken cancellationToken)
    {
        var role = NormalizePlatformRole(request.Role);
        var email = NormalizeEmail(request.Email);
        ValidateRequired(request.FullName, "Full name is required.");

        var emailExists = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower(), cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        var user = new User
        {
            TenantId = PlatformConstants.RootTenantId,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = NormalizeOptional(request.Phone),
            Role = ToInternalRole(role),
            IsActive = request.Status is null || request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase),
            HasAllBranchAccess = true,
            MustChangePassword = true
        };

        var initialPassword = temporaryPasswordService.Generate();
        user.PasswordHash = passwordHasher.HashPassword(user, initialPassword);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        var delivery = await SendAccountSetupAsync(user, cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.user.created",
            nameof(User),
            user.Id.ToString(),
            $"Platform user '{user.Email}' created.",
            severity: "Info",
            cancellationToken: cancellationToken);

        if (delivery.Success)
        {
            await auditLogService.LogAsync(
                PlatformConstants.RootTenantId,
                tenantContext.GetRequiredUserId(),
                "User invitation sent",
                nameof(User),
                user.Id.ToString(),
                $"Account setup invitation sent to '{user.Email}'.",
                severity: "Info",
                cancellationToken: cancellationToken);
        }

        return CreatedAtAction(nameof(Get), new { id = user.Id }, MapUser(user, null, new PlatformCredentialDeliverySummaryResponse(delivery.Success, delivery.Status, delivery.Message)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlatformUserResponse>> Update(Guid id, [FromBody] UpsertPlatformUserRequest request, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        var role = NormalizePlatformRole(request.Role);
        var email = NormalizeEmail(request.Email);
        ValidateRequired(request.FullName, "Full name is required.");

        var emailExists = await dbContext.Users.AnyAsync(x => x.Email.ToLower() == email.ToLower() && x.Id != id, cancellationToken);
        if (emailExists)
        {
            throw new BusinessRuleException("Email is already in use.");
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Phone = NormalizeOptional(request.Phone);
        user.Role = ToInternalRole(role);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            user.IsActive = request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.user.updated",
            nameof(User),
            user.Id.ToString(),
            $"Platform user '{user.Email}' updated.",
            severity: "Info",
            cancellationToken: cancellationToken);

        var lastLoginAt = await dbContext.UserSessions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.LoginAt)
            .Select(x => (DateTime?)x.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(MapUser(user, lastLoginAt));
    }

    [HttpPost("{id:guid}/activate")]
    public Task<ActionResult<PlatformUserResponse>> Activate(Guid id, CancellationToken cancellationToken) =>
        UpdateStatusAsync(id, true, cancellationToken);

    [HttpPost("{id:guid}/deactivate")]
    public Task<ActionResult<PlatformUserResponse>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        UpdateStatusAsync(id, false, cancellationToken);

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<PlatformResetPasswordResponse>> ResetPassword(Guid id, [FromBody] PlatformResetPasswordRequest? request, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        var nextPassword = string.IsNullOrWhiteSpace(request?.TemporaryPassword)
            ? temporaryPasswordService.Generate()
            : request.TemporaryPassword.Trim();

        var delivery = await credentialDeliveryService.SendAsync(
            PlatformConstants.RootTenantId,
            user,
            new UserCredentialDeliveryRequest(
                "password-reset",
                credentialDeliveryService.BuildLoginUrl(),
                nextPassword,
                null,
                true),
            cancellationToken);

        if (delivery.Success)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, nextPassword);
            user.MustChangePassword = true;
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.user.password.reset",
            nameof(User),
            user.Id.ToString(),
            $"Platform user '{user.Email}' password reset.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        if (delivery.Success)
        {
            await auditLogService.LogAsync(
                PlatformConstants.RootTenantId,
                tenantContext.GetRequiredUserId(),
                "User invitation sent",
                nameof(User),
                user.Id.ToString(),
                $"Account setup invitation sent to '{user.Email}'.",
                severity: "Info",
                cancellationToken: cancellationToken);
        }

        return Ok(new PlatformResetPasswordResponse(user.Id, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, delivery.Message));
    }

    [HttpPost("{id:guid}/resend-credentials")]
    public async Task<ActionResult<PlatformResetPasswordResponse>> ResendCredentials(Guid id, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        var delivery = await SendAccountSetupAsync(user, cancellationToken);

        if (delivery.Success)
        {
            user.LastCredentialSentAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (delivery.Success)
        {
            await auditLogService.LogAsync(
                PlatformConstants.RootTenantId,
                tenantContext.GetRequiredUserId(),
                "User credentials sent",
                nameof(User),
                user.Id.ToString(),
                $"User credentials sent to '{user.Email}'.",
                severity: "Info",
                cancellationToken: cancellationToken);
        }

        var message = delivery.Success
            ? delivery.Message
            : "Account setup link could not be sent. The user's access remains pending until an invitation is delivered.";

        return Ok(new PlatformResetPasswordResponse(user.Id, user.Email, delivery.Success, user.LastCredentialSentAt, delivery.Status, message));
    }

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
        var expiresAt = now.AddHours(72);
        dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashPasswordLinkToken(rawToken),
            ExpiresAt = expiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return await credentialDeliveryService.SendPasswordResetLinkAsync(
            PlatformConstants.RootTenantId,
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

    [HttpPost("{id:guid}/assign-role")]
    public async Task<ActionResult<PlatformUserResponse>> AssignRole(Guid id, [FromBody] PlatformAssignRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        var role = NormalizePlatformRole(request.Role);
        user.Role = ToInternalRole(role);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            "platform.user.role.changed",
            nameof(User),
            user.Id.ToString(),
            $"Platform user '{user.Email}' role changed to {role}.",
            severity: "Warning",
            cancellationToken: cancellationToken);

        var lastLoginAt = await dbContext.UserSessions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.LoginAt)
            .Select(x => (DateTime?)x.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(MapUser(user, lastLoginAt));
    }

    private async Task<ActionResult<PlatformUserResponse>> UpdateStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var user = await QueryPlatformUsers().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Platform user was not found.");

        user.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogService.LogAsync(
            PlatformConstants.RootTenantId,
            tenantContext.GetRequiredUserId(),
            isActive ? "platform.user.activated" : "platform.user.deactivated",
            nameof(User),
            user.Id.ToString(),
            $"Platform user '{user.Email}' {(isActive ? "activated" : "deactivated")}.",
            severity: "Info",
            cancellationToken: cancellationToken);

        var lastLoginAt = await dbContext.UserSessions
            .Where(x => x.UserId == id)
            .OrderByDescending(x => x.LoginAt)
            .Select(x => (DateTime?)x.LoginAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(MapUser(user, lastLoginAt));
    }

    private IQueryable<User> QueryPlatformUsers() =>
        dbContext.Users.Where(x => x.TenantId == PlatformConstants.RootTenantId && !x.Role.Equals(AppRoles.Admin));

    private static PlatformUserResponse MapUser(User user, DateTime? lastLoginAt, PlatformCredentialDeliverySummaryResponse? credentialDelivery = null) =>
        new(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            ToPublicRole(user.Role),
            user.IsActive ? "Active" : "Inactive",
            lastLoginAt,
            user.CreatedAt,
            user.UpdatedAt,
            user.MustChangePassword,
            user.LastCredentialSentAt,
            credentialDelivery);

    private static void ValidateRequired(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BusinessRuleException(message);
        }
    }

    private static string NormalizeEmail(string? value)
    {
        ValidateRequired(value, "Email is required.");
        return value!.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePlatformRole(string? role)
    {
        ValidateRequired(role, "Role is required.");

        return role!.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "platformowner" => "PlatformOwner",
            "superadmin" => "PlatformOwner",
            "platformsuperadmin" => "PlatformOwner",
            "platformadmin" => "PlatformAdmin",
            "supportadmin" => "SupportAdmin",
            "financeadmin" => "FinanceAdmin",
            "readonlyauditor" => "ReadOnlyAuditor",
            _ => throw new BusinessRuleException("Role must be PlatformOwner, PlatformAdmin, SupportAdmin, FinanceAdmin, or ReadOnlyAuditor.")
        };
    }

    private static string ToInternalRole(string role) => role switch
    {
        "PlatformOwner" => AppRoles.SuperAdmin,
        "PlatformAdmin" => AppRoles.PlatformAdmin,
        "SupportAdmin" => AppRoles.Support,
        "FinanceAdmin" => AppRoles.Finance,
        "ReadOnlyAuditor" => AppRoles.Auditor,
        _ => AppRoles.PlatformAdmin
    };

    private static string ToPublicRole(string role)
    {
        if (role.Equals(AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) || role.Equals(AppRoles.PlatformOwner, StringComparison.OrdinalIgnoreCase))
        {
            return "PlatformOwner";
        }

        if (role.Equals(AppRoles.PlatformSuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return "PlatformOwner";
        }

        if (role.Equals(AppRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase) || role.Equals(AppRoles.PlatformAdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return "PlatformAdmin";
        }

        if (role.Equals(AppRoles.Support, StringComparison.OrdinalIgnoreCase) || role.Equals(AppRoles.SupportAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return "SupportAdmin";
        }

        if (role.Equals(AppRoles.Finance, StringComparison.OrdinalIgnoreCase) || role.Equals(AppRoles.FinanceAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return "FinanceAdmin";
        }

        return "ReadOnlyAuditor";
    }

}

public sealed record UpsertPlatformUserRequest(
    string FullName,
    string Email,
    string? Phone,
    string Role,
    string? Status,
    string? Password);

public sealed record PlatformAssignRoleRequest(string Role);

public sealed record PlatformResetPasswordRequest(string? TemporaryPassword);

public sealed record PlatformResetPasswordResponse(
    Guid Id,
    string Email,
    bool Success,
    DateTime? LastCredentialSentAt,
    string Status,
    string? Message);

public sealed record PlatformCredentialDeliverySummaryResponse(
    bool Success,
    string Status,
    string? Message);

public sealed record PlatformUserResponse(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    string Status,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool MustChangePassword,
    DateTime? LastCredentialSentAt,
    PlatformCredentialDeliverySummaryResponse? CredentialDelivery);
