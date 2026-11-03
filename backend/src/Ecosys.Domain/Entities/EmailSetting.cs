using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EmailSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Provider { get; set; } = "Smtp";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? ReplyToEmail { get; set; }
    /// <summary>Reply-To for finance mail (invoices, receipts, statements, quotations); falls back to <see cref="ReplyToEmail"/>.</summary>
    public string? FinanceReplyToEmail { get; set; }
    /// <summary>Reply-To for operations mail (work orders, SLA, PM, ETA, contracts); falls back to <see cref="ReplyToEmail"/>.</summary>
    public string? OperationsReplyToEmail { get; set; }
    public string? Password { get; set; }
    public string? EncryptedSecret { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    /// <summary>Platform-managed address; legacy custom sender configuration is retained separately.</summary>
    public string? ManagedEmailAddress { get; set; }
    /// <summary>
    /// Whether the mail transport actually accepts <see cref="ManagedEmailAddress"/> as a sender. Generating
    /// the address only writes this row; the mail server must separately know the address (catch-all,
    /// alias or forwarder on the serviceops domain), so a new identity starts <c>Pending</c> and only a
    /// successful send or sender probe proves it. See <see cref="ManagedEmailStatuses"/>.
    /// </summary>
    public string ManagedEmailStatus { get; set; } = ManagedEmailStatuses.Pending;
    public DateTime? ManagedEmailStatusCheckedAt { get; set; }
    public string? ManagedEmailStatusDetail { get; set; }
    public bool InboundEnabled { get; set; } = true;
    /// <summary>
    /// Tenant rows only: send through the tenant's own mail server (<see cref="Host"/>, <see cref="Port"/>,
    /// <see cref="Username"/>, <see cref="EncryptedSecret"/>) as <see cref="CustomFromAddress"/> instead of
    /// the platform transport and managed address. Off by default; platform mail is used while it is off.
    /// </summary>
    public bool UseCustomSmtp { get; set; }
    /// <summary>The From address used with the tenant's own SMTP server — one that server accepts as a sender.</summary>
    public string? CustomFromAddress { get; set; }
    public string? EmailSignature { get; set; }
    public bool IsConfigured { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public string? LastError { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}

public static class ManagedEmailStatuses
{
    /// <summary>Generated in the database; not yet proven to be accepted by the mail server.</summary>
    public const string Pending = "Pending";
    /// <summary>The mail server accepted the address as a sender (probe or real send).</summary>
    public const string Verified = "Verified";
    /// <summary>The mail server refused the address as a sender — mail-server provisioning is missing.</summary>
    public const string Rejected = "Rejected";
}
