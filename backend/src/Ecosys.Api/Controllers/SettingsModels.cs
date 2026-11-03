namespace Ecosys.Api.Controllers;

public sealed record CompanySettingsRequest(
    string CompanyName,
    string? LegalName,
    string? TradingName,
    string? PostalAddress,
    string? PhysicalAddress,
    string? Website,
    string? VatNumber,
    string? RegistrationNumber,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? Address,
    string? TimeZoneId,
    string? PrimaryColor,
    string? SecondaryColor,
    bool ShowPoweredByEcosys);

public sealed record CompanySettingsResponse(
    string CompanyName,
    string? LegalName,
    string? TradingName,
    string? PostalAddress,
    string? PhysicalAddress,
    string? Website,
    string? VatNumber,
    string? RegistrationNumber,
    string Email,
    string? Phone,
    string Country,
    string? Industry,
    string? Address,
    string? TimeZoneId,
    /// <summary>Effective zone actually in force — the tenant override when set, else the platform default.</summary>
    string EffectiveTimeZoneId,
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys,
    /// <summary>The workspace code staff enter alongside a username to sign in — needed by anyone
    /// without an email address, since a username alone doesn't say which tenant to look in.</summary>
    string WorkspaceCode);

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
    string? AllowedSenderDomains,
    bool? AutoReplyEnabled = null);

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
    string? LastError,
    bool AutoReplyEnabled = true,
    /// <summary>True once the mailbox has been read at least once (intake handles emails arriving after that).</summary>
    bool IsReading = false);

/// <summary>Blank fields fall back to the saved settings, so a saved mailbox can be re-tested.</summary>
public sealed record EmailIntakeTestRequest(string? Host, int Port, bool? UseSsl = null, string? Username = null, string? Password = null);

public sealed record EmailIntakeTestResponse(bool Success, DateTime? LastCheckedAt, string? LastError);

public sealed record EmailIntakeMessageResponse(
    Guid Id,
    DateTime ReceivedAt,
    string FromAddress,
    string? FromName,
    string Subject,
    string Outcome,
    string? Detail,
    Guid? WorkOrderId,
    string? WorkOrderNumber,
    bool AutoReplySent);

public sealed record EmailIntakeCheckResponse(int Handled, DateTime? LastCheckedAt, bool IsConnectionHealthy, string? LastError);

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

public sealed record IntakeRuleTemplateResponse(string Key, string Name, string Description, string CriteriaJson, string ActionsJson);

public sealed record IntakeRulePreviewRequest(string? Subject, string? Body, string? FromAddress, string? FromName);

public sealed record IntakeRulePreviewResponse(Guid? RuleId, string? RuleName, string Result, string? Title, string? Priority, string? WorkOrderType);

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
