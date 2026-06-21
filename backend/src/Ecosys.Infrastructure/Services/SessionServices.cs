using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Ecosys.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecosys.Infrastructure.Services;

public interface IUserSessionService
{
    UserSession StartSession(User user, string? jwtId);
    Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default);
    Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default);
}

internal sealed class UserSessionService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor) : IUserSessionService
{
    public UserSession StartSession(User user, string? jwtId)
    {
        var now = DateTime.UtcNow;
        var session = new UserSession
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            JwtId = jwtId,
            LoginAt = now,
            LastSeenAt = now,
            IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            IsRevoked = false
        };

        dbContext.UserSessions.Add(session);
        return session;
    }

    public async Task<bool> TouchAsync(Guid sessionId, TimeSpan minimumInterval, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null || session.IsRevoked || session.LogoutAt.HasValue)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if (session.LastSeenAt >= now.Subtract(minimumInterval))
        {
            return true;
        }

        session.LastSeenAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException("Session was not found.");

        if (!session.LogoutAt.HasValue)
        {
            var now = DateTime.UtcNow;
            session.LogoutAt = now;
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.LastSeenAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<UserSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        dbContext.UserSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);

    public async Task<int> RevokeAllForUserAsync(Guid userId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await dbContext.UserSessions
            .Where(x => x.UserId == userId && !x.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.LastSeenAt = now;
            session.LogoutAt ??= now;
            if (!string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(session.UserAgent))
            {
                session.UserAgent = reason;
            }
        }

        if (sessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return sessions.Count;
    }

    public async Task<int> RevokeOthersForUserAsync(Guid userId, Guid currentSessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await dbContext.UserSessions
            .Where(x => x.UserId == userId && x.Id != currentSessionId && !x.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = now;
            session.LastSeenAt = now;
            session.LogoutAt ??= now;
            if (!string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(session.UserAgent))
            {
                session.UserAgent = reason;
            }
        }

        if (sessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return sessions.Count;
    }

    public static bool IsActive(UserSession session, DateTime utcNow) =>
        !session.IsRevoked
        && !session.LogoutAt.HasValue
        && session.LastSeenAt >= utcNow.AddMinutes(-15);
}

public interface IPlatformBootstrapService
{
    Task EnsurePlatformAdminAsync(CancellationToken cancellationToken = default);
}

internal sealed class PlatformBootstrapService(
    AppDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<PlatformAdminOptions> platformAdminOptions) : IPlatformBootstrapService
{
    public async Task EnsurePlatformAdminAsync(CancellationToken cancellationToken = default)
    {
        var configuredEmail = platformAdminOptions.Value.Email.Trim().ToLowerInvariant();
        var configuredFullName = string.IsNullOrWhiteSpace(platformAdminOptions.Value.FullName)
            ? "Platform SuperAdmin"
            : platformAdminOptions.Value.FullName.Trim();
        var configuredPassword = platformAdminOptions.Value.Password;

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == PlatformConstants.RootTenantId, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = PlatformConstants.RootTenantId,
                Name = "Ecosys Platform",
                Slug = "ecosys-platform",
                CompanyName = "Ecosys Platform",
                Email = configuredEmail,
                ContactEmail = configuredEmail,
                Country = "Platform",
                Industry = "SaaS",
                Status = "Active",
                LicenseStatus = "Active",
                PlanName = "Platform",
                IsActive = true
            };
            dbContext.Tenants.Add(tenant);
        }
        else
        {
            tenant.Name = string.IsNullOrWhiteSpace(tenant.Name) ? tenant.CompanyName : tenant.Name;
            tenant.Slug = string.IsNullOrWhiteSpace(tenant.Slug) ? "ecosys-platform" : tenant.Slug;
            tenant.Email = string.IsNullOrWhiteSpace(tenant.Email) ? configuredEmail : tenant.Email;
            tenant.ContactEmail = string.IsNullOrWhiteSpace(tenant.ContactEmail) ? configuredEmail : tenant.ContactEmail;
            tenant.Status = string.IsNullOrWhiteSpace(tenant.Status) ? "Active" : tenant.Status;
            tenant.LicenseStatus = string.IsNullOrWhiteSpace(tenant.LicenseStatus) ? "Active" : tenant.LicenseStatus;
            tenant.PlanName ??= "Platform";
            tenant.IsActive = true;
        }

        var user = await dbContext.Users
            .Include(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == PlatformConstants.SuperAdminUserId, cancellationToken);

        user ??= await dbContext.Users
            .Include(x => x.Permission)
            .Where(x => x.TenantId == PlatformConstants.RootTenantId && AppRoles.PlatformRoles.Contains(x.Role))
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = PlatformConstants.SuperAdminUserId,
                TenantId = tenant.Id,
                FullName = configuredFullName,
                Email = configuredEmail,
                Role = AppRoles.SuperAdmin,
                JobTitle = "Platform SuperAdmin",
                IsActive = true,
                HasAllBranchAccess = true
            };
            user.PasswordHash = passwordHasher.HashPassword(user, configuredPassword);
            user.Permission = new UserPermission();
            dbContext.Users.Add(user);
        }
        else
        {
            user.TenantId = tenant.Id;
            user.Role = AppRoles.SuperAdmin;
            user.IsActive = true;
            user.HasAllBranchAccess = true;
            user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? configuredFullName : user.FullName;
            // Only update email/password if they look like auto-generated placeholders — never
            // overwrite real operator-set credentials (enforced by the security hardening test).
            var isPlaceholderEmail = string.IsNullOrWhiteSpace(user.Email)
                || user.Email.EndsWith("@ecosys.local", StringComparison.OrdinalIgnoreCase);
            if (isPlaceholderEmail)
                user.Email = configuredEmail;
            if (string.IsNullOrWhiteSpace(user.PasswordHash) || isPlaceholderEmail)
                user.PasswordHash = passwordHasher.HashPassword(user, configuredPassword);
            user.JobTitle = string.IsNullOrWhiteSpace(user.JobTitle) ? "Platform SuperAdmin" : user.JobTitle;
            user.Permission ??= new UserPermission();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
