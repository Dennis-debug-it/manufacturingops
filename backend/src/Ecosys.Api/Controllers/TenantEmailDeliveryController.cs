using System.Net.Mail;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Settings → Email Delivery for a tenant admin: keep sending through platform mail (the default), or
/// switch to the tenant's own SMTP server. The switch only takes effect once host and From address are
/// set (<see cref="TenantEmailIdentity.UsesOwnSmtp"/>), and the password is write-only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tenant/email-delivery")]
public sealed class TenantEmailDeliveryController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    ISecretEncryptionService secretEncryptionService,
    IEmailSender emailSender,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet]
    public async Task<ActionResult<TenantEmailDeliveryResponse>> Get(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        return Ok(Map(await GetOrCreateAsync(cancellationToken)));
    }

    [HttpPut]
    public async Task<ActionResult<TenantEmailDeliveryResponse>> Update([FromBody] UpdateTenantEmailDeliveryRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var setting = await GetOrCreateAsync(cancellationToken);

        var host = request.Host?.Trim() ?? string.Empty;
        var fromAddress = request.FromAddress?.Trim().ToLowerInvariant();
        if (request.UseCustomSmtp)
        {
            if (string.IsNullOrWhiteSpace(host) || host.Length > 255)
                throw new BusinessRuleException("SMTP host is required to use your own mail server.");
            if (request.Port is < 1 or > 65535)
                throw new BusinessRuleException("SMTP port must be between 1 and 65535.");
            if (string.IsNullOrWhiteSpace(fromAddress) || fromAddress.Length > 256 || !MailAddress.TryCreate(fromAddress, out _))
                throw new BusinessRuleException("A valid From address is required to use your own mail server.");
        }

        setting.UseCustomSmtp = request.UseCustomSmtp;
        setting.Host = host;
        setting.Port = request.Port is >= 1 and <= 65535 ? request.Port : 587;
        setting.UseSsl = request.UseSsl;
        setting.Username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        setting.CustomFromAddress = string.IsNullOrWhiteSpace(fromAddress) ? null : fromAddress;
        if (request.ClearPassword)
        {
            setting.EncryptedSecret = null;
            setting.Password = null;
        }
        else if (!string.IsNullOrEmpty(request.Password))
        {
            // Blank means "keep the saved password" — the form never receives the stored secret back.
            setting.EncryptedSecret = secretEncryptionService.Encrypt(request.Password);
            setting.Password = null;
        }

        setting.LastError = null;
        setting.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "Tenant email delivery updated", nameof(EmailSetting), setting.Id.ToString(),
            setting.UseCustomSmtp ? $"Switched outbound email to own SMTP server {setting.Host}:{setting.Port} as {setting.CustomFromAddress}." : "Outbound email uses platform mail.",
            cancellationToken);
        return Ok(Map(setting));
    }

    /// <summary>
    /// Sends one real message through the tenant's saved server to <paramref name="request"/>'s address
    /// (default: the admin's own), so a wrong password or a refused From shows up here, not on a client's invoice.
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<TenantEmailDeliveryTestResponse>> Test([FromBody] TestTenantEmailDeliveryRequest? request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();
        var setting = await GetOrCreateAsync(cancellationToken);
        if (!TenantEmailIdentity.UsesOwnSmtp(setting))
        {
            throw new BusinessRuleException("Save your own SMTP server settings first. Platform mail is managed by the platform team.");
        }

        var to = string.IsNullOrWhiteSpace(request?.ToAddress) ? tenantContext.Email : request.ToAddress.Trim();
        if (string.IsNullOrWhiteSpace(to) || !MailAddress.TryCreate(to, out _) || to.EndsWith("@no-email.invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Enter an email address to send the test message to.");
        }

        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == TenantId, cancellationToken);
        var sender = TenantEmailIdentity.ResolveSender(tenant, setting)!;
        var delivery = EmailDeliveryResolution.Resolve(setting, secretEncryptionService.Decrypt(setting.EncryptedSecret) ?? setting.Password, new Ecosys.Shared.Options.SmtpOptions())!;

        TenantEmailDeliveryTestResponse result;
        try
        {
            await emailSender.SendAsync(
                new EmailMessage(to, $"{sender.FromName}: test email", $"This is a test message from {sender.FromName} sent through {setting.Host}. Your mail server settings work.", sender.FromName, sender.FromAddress, sender.ReplyTo),
                delivery,
                cancellationToken);
            setting.LastError = null;
            result = new TenantEmailDeliveryTestResponse(true, $"Test email sent to {to}.", null);
        }
        catch (EmailDeliveryException ex)
        {
            setting.LastError = $"{ex.ErrorCategory}: {ex.Message}"[..Math.Min($"{ex.ErrorCategory}: {ex.Message}".Length, 1000)];
            result = new TenantEmailDeliveryTestResponse(false, $"The mail server did not accept the test email ({ex.ErrorCategory}).", ex.Message);
        }

        setting.LastTestedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(result);
    }

    private async Task<EmailSetting> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == TenantId, cancellationToken);
        if (setting is not null) return setting;

        var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == TenantId, cancellationToken);
        setting = TenantEmailIdentity.Create(tenant);
        dbContext.EmailSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private static TenantEmailDeliveryResponse Map(EmailSetting setting) => new(
        setting.UseCustomSmtp,
        TenantEmailIdentity.UsesOwnSmtp(setting) ? "Custom" : "Platform",
        setting.Host,
        setting.Port > 0 ? setting.Port : 587,
        setting.UseSsl,
        setting.Username,
        !string.IsNullOrWhiteSpace(setting.EncryptedSecret) || !string.IsNullOrWhiteSpace(setting.Password),
        setting.CustomFromAddress,
        setting.ManagedEmailAddress,
        setting.LastTestedAt,
        setting.LastError);
}

public sealed record UpdateTenantEmailDeliveryRequest(
    bool UseCustomSmtp,
    string? Host,
    int Port,
    bool UseSsl,
    string? Username,
    string? Password,
    string? FromAddress,
    bool ClearPassword = false);

public sealed record TestTenantEmailDeliveryRequest(string? ToAddress);

public sealed record TenantEmailDeliveryTestResponse(bool Success, string Message, string? TechnicalDetail);

public sealed record TenantEmailDeliveryResponse(
    bool UseCustomSmtp,
    string ActiveMode,
    string Host,
    int Port,
    bool UseSsl,
    string? Username,
    bool HasPassword,
    string? FromAddress,
    string? ManagedEmailAddress,
    DateTime? LastTestedAt,
    string? LastError);
