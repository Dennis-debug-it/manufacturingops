using Ecosys.Domain.Entities;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Read-only support view of a tenant's outbound email. By default a tenant sends through the
/// platform's transport as its managed sender identity; a tenant admin can switch to their own SMTP
/// server in Settings → Email Delivery, which <see cref="UsesOwnSmtp"/> reports. Nothing here edits it.
/// </summary>
public sealed record TenantEmailSettingsResponse(
    Guid Id,
    Guid TenantId,
    string? SenderName,
    string? SenderEmail,
    string? ReplyToEmail,
    bool EnableTenantEmailNotifications,
    DateTime? LastTestedAt,
    string? LastError,
    string ManagedEmailStatus = ManagedEmailStatuses.Pending,
    DateTime? ManagedEmailStatusCheckedAt = null,
    string? ManagedEmailStatusDetail = null,
    bool UsesOwnSmtp = false,
    string? OwnSmtpHost = null,
    string? OwnFromAddress = null);

/// <summary>A finance address the tenant copies on invoices/receipts/statements (set by the tenant in Finance Defaults).</summary>
public sealed record TenantNotificationRecipientResponse(
    string RecipientGroup,
    string Email,
    bool IsActive);

public sealed record TenantCommunicationSettingsResponse(
    TenantEmailSettingsResponse EmailSettings,
    IReadOnlyCollection<TenantNotificationRecipientResponse> Recipients);

public sealed record TenantCommunicationActionRequest(string? TestRecipientEmail);

public sealed record TenantCommunicationActionResponse(
    bool Success,
    DateTime? LastTestedAt,
    string? LastError);
