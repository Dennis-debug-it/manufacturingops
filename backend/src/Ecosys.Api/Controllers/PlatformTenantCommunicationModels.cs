using Ecosys.Shared.Contracts.Integration;

namespace Ecosys.Api.Controllers;

public sealed record TenantEmailSettingsResponse(
    Guid Id,
    Guid TenantId,
    bool UsePlatformDefaults,
    bool OverrideSmtpSettings,
    EmailDeliveryMode DeliveryMode,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? SmtpPasswordMasked,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? ApiEndpoint,
    string? ApiKeyMasked,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries,
    bool EnableTenantEmailNotifications,
    DateTime? LastTestedAt,
    string? LastError);

public sealed record TenantEmailSettingsRequest(
    bool UsePlatformDefaults,
    bool OverrideSmtpSettings,
    EmailDeliveryMode DeliveryMode,
    string? SmtpHost,
    int? SmtpPort,
    string? SmtpUsername,
    string? SmtpPasswordSecret,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    bool EnableSslTls,
    EmailSecureMode SecureMode,
    string? ApiEndpoint,
    string? ApiKeySecret,
    string? ApiProviderName,
    int TimeoutSeconds,
    int MaxRetries,
    bool EnableTenantEmailNotifications);

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

public sealed record TenantCommunicationSettingsRequest(
    TenantEmailSettingsRequest EmailSettings,
    IReadOnlyCollection<TenantNotificationSettingRequest> NotificationSettings,
    IReadOnlyCollection<TenantNotificationRecipientRequest> Recipients);

public sealed record TenantCommunicationActionRequest(string? TestRecipientEmail);

public sealed record TenantCommunicationActionResponse(
    bool Success,
    DateTime? LastTestedAt,
    string? LastError);
