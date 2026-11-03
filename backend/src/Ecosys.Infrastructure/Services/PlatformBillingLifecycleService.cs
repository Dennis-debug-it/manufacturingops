using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Links the platform's own billing documents (subscription invoices/receipts) to the tenant's
/// subscription lifecycle: dispatching an invoice by email as soon as it exists, extending and
/// reactivating the subscription when a payment lands, and notifying the tenant at each step.
/// Grace-period warnings and suspension are driven separately by <see cref="SubscriptionLifecycleWorker"/>
/// since those are date-driven rather than triggered by a specific document.
/// </summary>
public interface IPlatformBillingLifecycleService
{
    Task QueueInvoiceIssuedEmailAsync(PlatformInvoice invoice, Guid? triggeredByUserId, CancellationToken cancellationToken = default);
    Task ApplyPaymentReceivedAsync(PlatformPayment payment, PlatformInvoice? invoice, Guid? triggeredByUserId, CancellationToken cancellationToken = default);
    Task QueueGracePeriodWarningEmailAsync(TenantLicense license, DateTime graceEndsAt, CancellationToken cancellationToken = default);
    Task QueueSubscriptionSuspendedEmailAsync(TenantLicense license, CancellationToken cancellationToken = default);
}

internal sealed class PlatformBillingLifecycleService(
    AppDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    IEmailSubjectRuleService emailSubjectRuleService,
    IEmailOutboxService emailOutboxService) : IPlatformBillingLifecycleService
{
    public async Task QueueInvoiceIssuedEmailAsync(PlatformInvoice invoice, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        Tenant? tenant = invoice.TenantId.HasValue
            ? await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == invoice.TenantId.Value, cancellationToken)
            : null;

        var recipient = NormalizeEmail(invoice.CustomerEmail) ?? NormalizeEmail(tenant?.ContactEmail) ?? NormalizeEmail(tenant?.Email);
        if (recipient is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = invoice.InvoiceNumber,
            ["CompanyName"] = invoice.CustomerName,
            ["TotalAmount"] = $"{invoice.Currency} {invoice.Total:N2}",
            ["DueDate"] = invoice.DueDate?.ToString("yyyy-MM-dd") ?? "on receipt",
        };

        await QueuePlatformEmailAsync(
            "platform.invoice.issued",
            "platform-invoice-issued",
            recipient,
            invoice.CustomerName,
            invoice.TenantId,
            values,
            triggeredByUserId,
            "PlatformInvoice",
            invoice.Id,
            $"platform-invoice-issued:{invoice.Id}",
            cancellationToken);
    }

    public async Task ApplyPaymentReceivedAsync(PlatformPayment payment, PlatformInvoice? invoice, Guid? triggeredByUserId, CancellationToken cancellationToken = default)
    {
        var tenantId = invoice?.TenantId ?? payment.TenantId;
        Tenant? tenant = tenantId.HasValue
            ? await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId.Value, cancellationToken)
            : null;

        await QueueReceiptEmailAsync(payment, invoice, tenant, triggeredByUserId, cancellationToken);

        if (tenantId is null || tenantId == PlatformConstants.RootTenantId)
        {
            return;
        }

        var license = await dbContext.TenantLicenses
            .Include(x => x.Tenant)
            .Include(x => x.LicensePlan)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId.Value, cancellationToken);
        if (license is null)
        {
            return;
        }

        // An unrecognized or Custom cycle (no fixed length) falls back to a monthly-equivalent step.
        BillingCycles.TryGetFixedCycleDays(BillingCycles.Monthly, out var monthlyDays);
        var cycleDays = BillingCycles.TryGetFixedCycleDays(license.BillingCycle, out var days) ? days : monthlyDays;
        var invoiceTotal = invoice?.Total ?? payment.Amount;
        var fraction = invoiceTotal <= 0 ? 1d : Math.Min(1d, (double)(payment.Amount / invoiceTotal));
        var extensionDays = cycleDays * fraction;

        var now = DateTime.UtcNow;
        var baseline = license.ExpiresAt ?? license.NextBillingDate ?? license.TrialEndsAt ?? now;
        var newExpiry = baseline.AddDays(extensionDays);
        if (newExpiry < now)
        {
            // A partial payment against a badly overdue account shouldn't leave the tenant still
            // expired right after being "extended" — floor at now so they get a fresh grace window.
            newExpiry = now;
        }

        license.ExpiresAt = newExpiry;
        license.NextBillingDate = newExpiry;
        license.OverdueWarningSentAt = null;

        // Payment converts any trial (including a lapsed TrialExpired one — TM-P1-05), an expired term and
        // a non-payment suspension to Active. A manual suspension is an operator decision that payment
        // must not override; its term is still extended so Reactivate can lift it later.
        var wasSuspendedOrLapsed = TenantLifecycleState.IsAnyTrialStatus(license.Status)
            || string.Equals(license.Status, LicenseStatuses.Expired, StringComparison.OrdinalIgnoreCase)
            || (TenantLifecycleState.IsSuspended(license) && !TenantLifecycleState.IsManualSuspension(license));

        if (wasSuspendedOrLapsed)
        {
            license.Status = LicenseStatuses.Active;
            license.SuspendedAt = null;
            license.SuspensionReason = null;
        }

        if (license.Tenant is not null)
        {
            TenantLifecycleState.Sync(license.Tenant, license, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await QueueExtensionEmailAsync(license, wasSuspendedOrLapsed, triggeredByUserId, cancellationToken);
    }

    public async Task QueueGracePeriodWarningEmailAsync(TenantLicense license, DateTime graceEndsAt, CancellationToken cancellationToken = default)
    {
        var tenant = license.Tenant;
        var recipient = NormalizeEmail(tenant?.ContactEmail) ?? NormalizeEmail(tenant?.Email);
        if (recipient is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = tenant?.CompanyName ?? tenant?.Name ?? "Customer",
            ["GraceEndsAt"] = graceEndsAt.ToString("yyyy-MM-dd"),
        };

        await QueuePlatformEmailAsync(
            "platform.subscription.grace-warning",
            "platform-subscription-grace-warning",
            recipient,
            tenant?.CompanyName,
            license.TenantId,
            values,
            null,
            "TenantLicense",
            license.Id,
            $"platform-subscription-grace-warning:{license.Id}:{license.ExpiresAt:yyyyMMdd}",
            cancellationToken);
    }

    public async Task QueueSubscriptionSuspendedEmailAsync(TenantLicense license, CancellationToken cancellationToken = default)
    {
        var tenant = license.Tenant;
        var recipient = NormalizeEmail(tenant?.ContactEmail) ?? NormalizeEmail(tenant?.Email);
        if (recipient is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = tenant?.CompanyName ?? tenant?.Name ?? "Customer",
            ["SuspendedAt"] = (license.SuspendedAt ?? DateTime.UtcNow).ToString("yyyy-MM-dd"),
        };

        await QueuePlatformEmailAsync(
            "platform.subscription.suspended",
            "platform-subscription-suspended",
            recipient,
            tenant?.CompanyName,
            license.TenantId,
            values,
            null,
            "TenantLicense",
            license.Id,
            $"platform-subscription-suspended:{license.Id}:{license.ExpiresAt:yyyyMMdd}",
            cancellationToken);
    }

    private async Task QueueReceiptEmailAsync(PlatformPayment payment, PlatformInvoice? invoice, Tenant? tenant, Guid? triggeredByUserId, CancellationToken cancellationToken)
    {
        var recipient = NormalizeEmail(invoice?.CustomerEmail) ?? NormalizeEmail(tenant?.ContactEmail) ?? NormalizeEmail(tenant?.Email);
        if (recipient is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["PaymentNumber"] = payment.PaymentNumber,
            ["CompanyName"] = invoice?.CustomerName ?? tenant?.CompanyName ?? tenant?.Name ?? "Customer",
            ["Amount"] = $"{payment.Currency} {payment.Amount:N2}",
            ["InvoiceNumber"] = invoice?.InvoiceNumber ?? "N/A",
            ["PaidAt"] = payment.PaidAt.ToString("yyyy-MM-dd"),
        };

        await QueuePlatformEmailAsync(
            "platform.payment.received",
            "platform-payment-receipt",
            recipient,
            invoice?.CustomerName ?? tenant?.CompanyName,
            invoice?.TenantId ?? tenant?.Id,
            values,
            triggeredByUserId,
            "PlatformPayment",
            payment.Id,
            $"platform-payment-receipt:{payment.Id}",
            cancellationToken);
    }

    private async Task QueueExtensionEmailAsync(TenantLicense license, bool reactivated, Guid? triggeredByUserId, CancellationToken cancellationToken)
    {
        var tenant = license.Tenant;
        var recipient = NormalizeEmail(tenant?.ContactEmail) ?? NormalizeEmail(tenant?.Email);
        if (recipient is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = tenant?.CompanyName ?? tenant?.Name ?? "Customer",
            ["PlanName"] = license.LicensePlan?.DisplayName ?? "your plan",
            ["NewExpiryDate"] = license.ExpiresAt?.ToString("yyyy-MM-dd") ?? "-",
            ["ActionWord"] = reactivated ? "reactivated" : "extended",
        };

        await QueuePlatformEmailAsync(
            "platform.subscription.extended",
            "platform-subscription-extended",
            recipient,
            tenant?.CompanyName,
            license.TenantId,
            values,
            triggeredByUserId,
            "TenantLicense",
            license.Id,
            $"platform-subscription-extended:{license.Id}:{license.ExpiresAt:yyyyMMddHHmmss}",
            cancellationToken);
    }

    private async Task QueuePlatformEmailAsync(
        string eventKey,
        string templateKey,
        string recipientEmail,
        string? recipientName,
        Guid? subjectTenantId,
        IReadOnlyDictionary<string, string?> values,
        Guid? triggeredByUserId,
        string documentType,
        Guid documentId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var platformEmail = await dbContext.EmailSettings.SingleOrDefaultAsync(x => x.TenantId == PlatformConstants.RootTenantId, cancellationToken);
        if (platformEmail is null || string.IsNullOrWhiteSpace(platformEmail.Host))
        {
            return;
        }

        var template = await emailTemplateService.RenderPlatformTemplateAsync(templateKey, values, cancellationToken);
        if (!template.Enabled)
        {
            return;
        }

        var finalSubject = await emailSubjectRuleService.BuildFinalSubjectAsync(
            subjectTenantId,
            eventKey,
            template.Subject,
            recipientName,
            cancellationToken);

        await emailOutboxService.QueueEmailAsync(
            new QueueEmailRequest(
                PlatformConstants.RootTenantId,
                eventKey,
                templateKey,
                recipientEmail,
                recipientName,
                template.SenderNameOverride ?? platformEmail.SenderName,
                platformEmail.SenderAddress,
                template.ReplyToOverride ?? platformEmail.ReplyToEmail,
                finalSubject,
                template.HtmlBody,
                template.TextBody,
                triggeredByUserId,
                DocumentType: documentType,
                DocumentId: documentId,
                TriggeredAutomatically: true,
                IdempotencyKey: idempotencyKey),
            cancellationToken);
    }

    private static string? NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
