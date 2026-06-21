using Ecosys.Shared.Contracts.Integration;

namespace Ecosys.Api.Controllers;

public sealed record CompanySettingsRequest(
    string CompanyName,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? PrimaryColor,
    string? SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record CompanySettingsResponse(
    string CompanyName,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record EmailSettingsRequest(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string SenderName,
    string SenderAddress);

public sealed record EmailSettingsResponse(
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string SenderName,
    string SenderAddress);

public sealed record EmailNotificationSettingsRequest(
    bool IsEnabled,
    EmailDeliveryMode DeliveryMode,
    string FromName,
    string FromEmail,
    string? ReplyToEmail,
    string SmtpHost,
    int SmtpPort,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? SmtpUsername,
    string? SmtpPasswordSecret,
    string? ApiEndpoint,
    string? ApiKeySecret,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries);

public sealed record EmailNotificationSettingsResponse(
    Guid Id,
    Guid TenantId,
    bool IsEnabled,
    EmailDeliveryMode DeliveryMode,
    string FromName,
    string FromEmail,
    string? ReplyToEmail,
    string SmtpHost,
    int SmtpPort,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? SmtpUsername,
    string? ApiEndpoint,
    string? ApiKeyMasked,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries,
    bool HasSecret,
    bool IsConfigured,
    DateTime? LastTestedAt,
    string? LastError);

public sealed record EmailNotificationTestRequest(string? TestRecipientEmail);

public sealed record EmailNotificationTestResponse(bool Success, DateTime? LastTestedAt, string? LastError);

public sealed record EmailIntakeSettingsRequest(
    bool IsEnabled,
    string? IntakeEmailAddress,
    string MailboxProvider,
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    Guid? DefaultClientId,
    Guid? DefaultBranchId,
    Guid? DefaultAssignmentGroupId,
    string DefaultPriority,
    bool CreateWorkOrderFromUnknownSender,
    string? SubjectParsingRules,
    string? AllowedSenderDomains);

public sealed record EmailIntakeSettingsResponse(
    Guid Id,
    Guid TenantId,
    bool IsEnabled,
    string? IntakeEmailAddress,
    string MailboxProvider,
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    bool HasPassword,
    Guid? DefaultClientId,
    Guid? DefaultBranchId,
    Guid? DefaultAssignmentGroupId,
    string DefaultPriority,
    bool CreateWorkOrderFromUnknownSender,
    string? SubjectParsingRules,
    string? AllowedSenderDomains,
    DateTime? LastCheckedAt,
    bool IsConnectionHealthy,
    string? LastError);

public sealed record EmailIntakeTestRequest(string? Host, int Port);

public sealed record EmailIntakeTestResponse(bool Success, DateTime? LastCheckedAt, string? LastError);

public sealed record UpsertIntakeProtocolRequest(
    string Name,
    string SourceType,
    bool IsActive,
    string? Description,
    string CriteriaJson,
    string ActionsJson,
    string SourceConfigJson);

public sealed record IntakeProtocolResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string SourceType,
    bool IsActive,
    string? Description,
    string CriteriaJson,
    string ActionsJson,
    string SourceConfigJson,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastTriggeredAt,
    string? LastTriggerStatus,
    string? LastError);

public sealed record IntakeProtocolTestResponse(bool Success, DateTime? LastTriggeredAt, string? LastTriggerStatus, string? LastError);

public sealed record UpsertMonitoringWebhookIntegrationRequest(
    string Name,
    string ToolType,
    bool IsActive,
    Guid? DefaultClientId,
    Guid? DefaultAssetId,
    Guid? DefaultBranchId,
    Guid? DefaultAssignmentGroupId,
    string DefaultPriority,
    bool CreateWorkOrderOnAlert,
    string? PayloadMappingJson);

public sealed record MonitoringWebhookIntegrationResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string ToolType,
    string EndpointSlug,
    bool IsActive,
    Guid? DefaultClientId,
    Guid? DefaultAssetId,
    Guid? DefaultBranchId,
    Guid? DefaultAssignmentGroupId,
    string DefaultPriority,
    bool CreateWorkOrderOnAlert,
    string? PayloadMappingJson,
    DateTime? LastReceivedAt,
    string? LastStatus,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    bool HasSecret,
    string? GeneratedSecret)
{
    public string? EndpointUrl { get; init; }
}

public sealed record MonitoringWebhookTestResponse(bool Success, DateTime? LastReceivedAt, string? LastStatus, string? LastError);

public sealed record NumberingSettingsRequest(
    Guid? BranchId,
    string DocumentType,
    string Prefix,
    string? Suffix,
    long NextNumber,
    int PaddingLength,
    string ResetFrequency,
    bool IncludeYear,
    string? YearFormat,
    bool IncludeMonth,
    string? Separator,
    bool IsActive);

public sealed record NumberingSettingsResponse(
    Guid Id,
    Guid? BranchId,
    string? BranchName,
    string DocumentType,
    string Prefix,
    string? Suffix,
    long NextNumber,
    int PaddingLength,
    string ResetFrequency,
    bool IncludeYear,
    string YearFormat,
    bool IncludeMonth,
    string Separator,
    bool IsLocked,
    bool IsActive);

public sealed record NumberingRuleResponse(
    Guid Id,
    string DocumentType,
    string Prefix,
    string? Suffix,
    long NextNumber,
    int PaddingLength,
    string ResetPeriod,
    string YearFormat,
    bool IncludeYear,
    bool IncludeMonth,
    string Separator,
    bool IsLocked,
    string Preview,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateNumberingRuleRequest(
    string Prefix,
    string? Suffix,
    long NextNumber,
    int PaddingLength,
    string ResetPeriod,
    bool IncludeYear,
    string? YearFormat,
    bool IncludeMonth,
    string? Separator,
    bool IsActive);

public sealed record NumberingPreviewRequest(Guid? RuleId, string? DocumentType);

public sealed record NumberingPreviewResponse(Guid RuleId, string DocumentType, string Preview);

public sealed record ResetNumberingRuleRequest(long StartFrom = 1);
