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
    public string? Password { get; set; }
    public string? EncryptedSecret { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderAddress { get; set; } = string.Empty;
    /// <summary>Platform-managed address; legacy custom sender configuration is retained separately.</summary>
    public string? ManagedEmailAddress { get; set; }
    public bool InboundEnabled { get; set; } = true;
    public string? EmailSignature { get; set; }
    public bool IsConfigured { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public string? LastError { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Tenant? Tenant { get; set; }
}
