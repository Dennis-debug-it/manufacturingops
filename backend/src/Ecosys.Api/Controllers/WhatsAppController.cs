using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/whatsapp")]
public sealed class WhatsAppController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IAuditLogService auditLogService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    // ── Settings ───────────────────────────────────────────────────────────────

    [HttpGet("settings")]
    public async Task<ActionResult<WhatsAppSettingsResponse>> GetSettings(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var setting = await dbContext.WhatsAppSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        return Ok(MapSettings(setting));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<WhatsAppSettingsResponse>> SaveSettings(
        [FromBody] WhatsAppSettingsRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        var setting = await dbContext.WhatsAppSettings
            .SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (setting is null)
        {
            setting = new WhatsAppSetting { TenantId = TenantId };
            dbContext.WhatsAppSettings.Add(setting);
        }

        setting.BusinessAccountId = request.BusinessAccountId?.Trim();
        setting.PhoneNumberId = request.PhoneNumberId?.Trim();
        setting.AccessToken = request.AccessToken?.Trim();
        setting.WebhookVerifyToken = request.WebhookVerifyToken?.Trim();
        setting.IsEnabled = request.IsEnabled;
        setting.AutoReplyEnabled = request.AutoReplyEnabled;
        setting.AutoReplyMessage = request.AutoReplyMessage?.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "whatsapp.settings.saved", nameof(WhatsAppSetting), setting.Id.ToString(), "WhatsApp settings updated", cancellationToken);

        return Ok(MapSettings(setting));
    }

    // ── Conversations & messages ─────────────────────────────────────────────────

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyCollection<WhatsAppConversationListItemResponse>>> ListConversations(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var conversations = await dbContext.WhatsAppConversations
            .Where(x => x.TenantId == TenantId)
            .OrderByDescending(x => x.LastMessageAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return Ok(conversations.Select(x => new WhatsAppConversationListItemResponse(
            x.Id, x.ContactName, x.ContactPhone, x.ClientId, x.LastMessagePreview, x.LastMessageAt, x.UnreadCount)).ToList());
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<WhatsAppConversationDetailResponse>> GetConversation(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var conversation = await LoadConversationAsync(id, cancellationToken);

        // Opening the thread clears the unread badge.
        if (conversation.UnreadCount != 0)
        {
            conversation.UnreadCount = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new WhatsAppConversationDetailResponse(
            conversation.Id, conversation.ContactName, conversation.ContactPhone, conversation.ClientId,
            conversation.Messages
                .OrderBy(m => m.SentAt)
                .Select(m => new WhatsAppTimelineMessageResponse(m.Id, m.Direction, m.Body, m.SentAt, m.Status, m.TemplateName))
                .ToList()));
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<WhatsAppConversationListItemResponse>> CreateConversation(
        [FromBody] CreateWhatsAppConversationRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();

        if (string.IsNullOrWhiteSpace(request.ContactPhone))
            throw new BusinessRuleException("Contact phone is required.");

        var conversation = new WhatsAppConversation
        {
            TenantId = TenantId,
            ContactName = string.IsNullOrWhiteSpace(request.ContactName) ? request.ContactPhone.Trim() : request.ContactName.Trim(),
            ContactPhone = request.ContactPhone.Trim(),
            ClientId = request.ClientId,
            LastMessagePreview = "New conversation",
            LastMessageAt = DateTime.UtcNow,
        };
        dbContext.WhatsAppConversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new WhatsAppConversationListItemResponse(
            conversation.Id, conversation.ContactName, conversation.ContactPhone, conversation.ClientId,
            conversation.LastMessagePreview, conversation.LastMessageAt, conversation.UnreadCount));
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<WhatsAppTimelineMessageResponse>> SendMessage(
        Guid id, [FromBody] SendWhatsAppMessageRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var conversation = await LoadConversationAsync(id, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Body))
            throw new BusinessRuleException("Message body is required.");

        var message = new WhatsAppMessage
        {
            TenantId = TenantId,
            ConversationId = conversation.Id,
            Direction = "Outbound",
            Body = request.Body.Trim(),
            SentAt = DateTime.UtcNow,
            Status = "Sent",
            TemplateName = string.IsNullOrWhiteSpace(request.TemplateName) ? null : request.TemplateName.Trim(),
        };
        dbContext.WhatsAppMessages.Add(message);

        conversation.LastMessagePreview = message.Body.Length > 120 ? message.Body[..120] : message.Body;
        conversation.LastMessageAt = message.SentAt;

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "whatsapp.message.sent", nameof(WhatsAppMessage), message.Id.ToString(), $"Sent WhatsApp message to {conversation.ContactPhone}", cancellationToken);

        return Ok(new WhatsAppTimelineMessageResponse(message.Id, message.Direction, message.Body, message.SentAt, message.Status, message.TemplateName));
    }

    // ── Templates ────────────────────────────────────────────────────────────────

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyCollection<WhatsAppTemplateResponse>>> ListTemplates(CancellationToken cancellationToken)
    {
        userAccessService.EnsureTenantOperationalAccess();
        var templates = await dbContext.WhatsAppTemplates
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(templates.Select(x => new WhatsAppTemplateResponse(x.Id, x.Name, x.Category, x.Language, x.Status, x.BodyPreview)).ToList());
    }

    [HttpPost("templates")]
    public async Task<ActionResult<WhatsAppTemplateResponse>> CreateTemplate(
        [FromBody] CreateWhatsAppTemplateRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("Template name is required.");

        var template = new WhatsAppTemplate
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Utility" : request.Category.Trim(),
            Language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim(),
            Status = "Pending",
            BodyPreview = request.BodyPreview?.Trim() ?? string.Empty,
        };
        dbContext.WhatsAppTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new WhatsAppTemplateResponse(template.Id, template.Name, template.Category, template.Language, template.Status, template.BodyPreview));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<WhatsAppConversation> LoadConversationAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.WhatsAppConversations
            .Include(x => x.Messages)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
        ?? throw new NotFoundException("Conversation not found.");

    private WhatsAppSettingsResponse MapSettings(WhatsAppSetting? s)
    {
        var webhookUrl = $"{Request.Scheme}://{Request.Host}/api/whatsapp/webhook/{TenantId}";
        return new WhatsAppSettingsResponse(
            s?.BusinessAccountId ?? string.Empty,
            s?.PhoneNumberId ?? string.Empty,
            s?.AccessToken ?? string.Empty,
            s?.WebhookVerifyToken ?? string.Empty,
            webhookUrl,
            s?.IsEnabled ?? false,
            s?.AutoReplyEnabled ?? false,
            s?.AutoReplyMessage ?? "Thanks for reaching out — a member of our team will respond shortly.");
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record WhatsAppSettingsResponse(
    string BusinessAccountId, string PhoneNumberId, string AccessToken, string WebhookVerifyToken,
    string WebhookUrl, bool IsEnabled, bool AutoReplyEnabled, string AutoReplyMessage);

public sealed record WhatsAppSettingsRequest(
    string? BusinessAccountId, string? PhoneNumberId, string? AccessToken, string? WebhookVerifyToken,
    bool IsEnabled, bool AutoReplyEnabled, string? AutoReplyMessage);

public sealed record WhatsAppConversationListItemResponse(
    Guid Id, string ContactName, string ContactPhone, Guid? ClientId,
    string LastMessagePreview, DateTime LastMessageAt, int UnreadCount);

public sealed record WhatsAppTimelineMessageResponse(
    Guid Id, string Direction, string Body, DateTime SentAt, string Status, string? TemplateName);

public sealed record WhatsAppConversationDetailResponse(
    Guid Id, string ContactName, string ContactPhone, Guid? ClientId,
    IReadOnlyList<WhatsAppTimelineMessageResponse> Messages);

public sealed record CreateWhatsAppConversationRequest(string ContactName, string ContactPhone, Guid? ClientId = null);

public sealed record SendWhatsAppMessageRequest(string Body, string? TemplateName = null);

public sealed record WhatsAppTemplateResponse(
    Guid Id, string Name, string Category, string Language, string Status, string BodyPreview);

public sealed record CreateWhatsAppTemplateRequest(string Name, string Category, string Language, string? BodyPreview);
