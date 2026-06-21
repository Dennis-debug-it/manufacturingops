using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class EmailIntakeSetting : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public bool IsEnabled { get; set; }
    public string? IntakeEmailAddress { get; set; }
    public string MailboxProvider { get; set; } = "IMAP";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 993;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public Guid? DefaultClientId { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public Guid? DefaultAssignmentGroupId { get; set; }
    public string DefaultPriority { get; set; } = "Medium";
    public bool CreateWorkOrderFromUnknownSender { get; set; }
    public string? SubjectParsingRules { get; set; }
    public string? AllowedSenderDomains { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public bool IsConnectionHealthy { get; set; }
    public string? LastError { get; set; }

    public Tenant? Tenant { get; set; }
    public Client? DefaultClient { get; set; }
    public Branch? DefaultBranch { get; set; }
    public AssignmentGroup? DefaultAssignmentGroup { get; set; }
}
