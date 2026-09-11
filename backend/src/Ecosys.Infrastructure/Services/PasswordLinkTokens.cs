using System.Data;
using System.Security.Cryptography;
using System.Text;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Options;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record IssuedPasswordLink(string Token, DateTime ExpiresAt);

/// <summary>Shared issuance for account setup and password recovery, using the existing token store.</summary>
public static class PasswordLinkTokens
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string Status(PasswordResetToken? token, DateTime utcNow)
    {
        if (token is null) return "INVALID";
        if (token.RevokedAt.HasValue) return "REVOKED";
        if (token.SupersededAt.HasValue) return "SUPERSEDED";
        if (token.UsedAt.HasValue) return "USED";
        return token.ExpiresAt <= utcNow ? "EXPIRED" : "VALID";
    }

    public static async Task<IssuedPasswordLink> IssueAsync(AppDbContext db, User user, string purpose,
        AuthSecurityOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new AuthSecurityOptions();
        var now = DateTime.UtcNow;
        // Serializable issuance prevents concurrent resends leaving two usable invitations.
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        var previous = await db.PasswordResetTokens
            .Where(x => x.UserId == user.Id && x.Purpose == purpose && x.UsedAt == null && x.SupersededAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in previous) token.SupersededAt = now;
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var expires = purpose == PasswordLinkPurposes.Invitation
            ? now.AddHours(Math.Clamp(options.InvitationLifetimeHours, 1, 720))
            : now.AddMinutes(Math.Clamp(options.PasswordResetLifetimeMinutes, 1, 1440));
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id, TokenHash = Hash(raw), Purpose = purpose, CreatedAt = now, ExpiresAt = expires
        });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return new IssuedPasswordLink(raw, expires);
    }
}
