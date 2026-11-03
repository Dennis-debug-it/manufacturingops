using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Ecosys.Infrastructure.Services;

public sealed record EmailIntakeMailboxSettings(string Host, int Port, bool UseSsl, string Username, string Password);

/// <summary>New messages since the last read, plus where reading got to.</summary>
public sealed record EmailIntakeFetchResult(long UidValidity, long LastUid, IReadOnlyList<(long Uid, MimeMessage Message)> Messages);

/// <summary>Reads a workspace's intake mailbox. An interface so the intake logic can be tested without a mail server.</summary>
public interface IEmailIntakeMailbox
{
    /// <summary>Signs in and opens the inbox, to check the details work.</summary>
    Task VerifyAsync(EmailIntakeMailboxSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Messages newer than <paramref name="lastUid"/>. With no position yet (or after the mailbox was
    /// renumbered) nothing is returned and the position is set to "now": intake starts with the next
    /// email rather than importing the whole inbox.
    /// </summary>
    Task<EmailIntakeFetchResult> FetchNewAsync(EmailIntakeMailboxSettings settings, long? uidValidity, long? lastUid, int maxMessages, CancellationToken cancellationToken = default);
}

internal sealed class ImapEmailIntakeMailbox : IEmailIntakeMailbox
{
    private const int TimeoutMilliseconds = 30_000;

    public async Task VerifyAsync(EmailIntakeMailboxSettings settings, CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(settings, cancellationToken);
        await InboxOf(client).OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task<EmailIntakeFetchResult> FetchNewAsync(EmailIntakeMailboxSettings settings, long? uidValidity, long? lastUid, int maxMessages, CancellationToken cancellationToken = default)
    {
        using var client = await ConnectAsync(settings, cancellationToken);
        var inbox = InboxOf(client);
        // Read-only: the mailbox is often also read by people, so nothing is marked or moved.
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
        var currentValidity = (long)inbox.UidValidity;
        var newest = inbox.UidNext.HasValue ? (long)inbox.UidNext.Value.Id - 1 : await NewestUidAsync(inbox, cancellationToken);

        if (lastUid is null || uidValidity != currentValidity)
        {
            await client.DisconnectAsync(true, cancellationToken);
            return new EmailIntakeFetchResult(currentValidity, newest, []);
        }

        var messages = new List<(long, MimeMessage)>();
        if (newest > lastUid.Value)
        {
            // "lastUid+1:*" can return the newest message even when it is older than lastUid (IMAP
            // treats * as the highest UID), so the UIDs are filtered again here.
            var uids = (await inbox.SearchAsync(SearchQuery.Uids(new UniqueIdRange(new UniqueId((uint)(lastUid.Value + 1)), UniqueId.MaxValue)), cancellationToken))
                .Where(uid => uid.Id > lastUid.Value)
                .OrderBy(uid => uid.Id)
                .Take(maxMessages)
                .ToList();
            foreach (var uid in uids)
            {
                messages.Add((uid.Id, await inbox.GetMessageAsync(uid, cancellationToken)));
            }
        }

        await client.DisconnectAsync(true, cancellationToken);
        // Anything beyond maxMessages is picked up on the next read.
        var readTo = messages.Count > 0 ? messages[^1].Item1 : lastUid.Value;
        return new EmailIntakeFetchResult(currentValidity, readTo, messages);
    }

    private static IMailFolder InboxOf(ImapClient client) =>
        client.Inbox ?? throw new InvalidOperationException("The mailbox has no inbox folder.");

    private static async Task<long> NewestUidAsync(IMailFolder inbox, CancellationToken cancellationToken)
    {
        if (inbox.Count == 0) return 0;
        var all = await inbox.SearchAsync(SearchQuery.All, cancellationToken);
        return all.Count == 0 ? 0 : all.Max(uid => (long)uid.Id);
    }

    private static async Task<ImapClient> ConnectAsync(EmailIntakeMailboxSettings settings, CancellationToken cancellationToken)
    {
        var client = new ImapClient { Timeout = TimeoutMilliseconds };
        try
        {
            var security = settings.UseSsl
                ? (settings.Port == 143 ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect)
                : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
