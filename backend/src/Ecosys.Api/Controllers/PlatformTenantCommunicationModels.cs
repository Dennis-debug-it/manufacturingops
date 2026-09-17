namespace Ecosys.Api.Controllers;

/// <summary>
/// Identity only — SMTP transport is global platform infrastructure with no per-tenant override (not
/// even via impersonation), so there is nothing transport-related left to show or edit here. What
/// differentiates a tenant's outbound mail is this sender identity, layered on the platform's transport.
/// </summary>
public sealed record TenantEmailSettingsResponse(
    Guid Id,
    Guid TenantId,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    bool EnableTenantEmailNotifications,
    DateTime? LastTestedAt,
    string? LastError);

public sealed record TenantNotificationSettingResponse(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled,
    bool SmsEnabled,
    bool IsActive);

public sealed record TenantNotificationSettingRequest(
    string NotificationKey,
    bool EmailEnabled,
    bool InAppEnabled,
    bool SmsEnabled,
    bool IsActive);

public sealed record TenantNotificationRecipientResponse(
    string RecipientGroup,
    string Email,
    bool IsActive);

public sealed record TenantNotificationRecipientRequest(
    string RecipientGroup,
    string Email,
    bool IsActive);

public sealed record TenantNotificationSettingsResponse(
    IReadOnlyCollection<TenantNotificationSettingResponse> NotificationSettings,
    IReadOnlyCollection<TenantNotificationRecipientResponse> Recipients);

public sealed record TenantNotificationSettingsRequest(
    IReadOnlyCollection<TenantNotificationSettingRequest> NotificationSettings,
    IReadOnlyCollection<TenantNotificationRecipientRequest> Recipients);

public sealed record TenantCommunicationSettingsResponse(
    TenantEmailSettingsResponse EmailSettings,
    IReadOnlyCollection<TenantNotificationSettingResponse> NotificationSettings,
    IReadOnlyCollection<TenantNotificationRecipientResponse> Recipients);

public sealed record TenantCommunicationActionRequest(string? TestRecipientEmail);

public sealed record TenantCommunicationActionResponse(
    bool Success,
    DateTime? LastTestedAt,
    string? LastError);
