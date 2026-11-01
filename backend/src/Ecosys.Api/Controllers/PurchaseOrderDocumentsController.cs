using System.Net;
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
/// A purchase order as a document: the PDF (in the workspace's purchase-order design) and sending it
/// to the supplier by email. The design existed for the document designer's preview, but no real
/// order could be printed or sent.
/// </summary>
[ApiController]
[Authorize]
[Route("api/purchase-orders/{id:guid}")]
public sealed class PurchaseOrderDocumentsController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IUserAccessService userAccessService,
    IPdfRenderer pdfRenderer,
    IWebHostEnvironment environment,
    IEmailOutboxService emailOutboxService,
    IApprovalWorkflowService approvalWorkflowService,
    IAuditLogService auditLogService) : TenantAwareControllerBase(tenantContext)
{
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrAnyPermission(PermissionNames.CanViewMaterials, PermissionNames.CanManageMaterials);
        var (order, tenant) = await LoadAsync(id, cancellationToken);
        return File(Render(order, tenant), "application/pdf", $"{order.PoNumber}.pdf");
    }

    /// <summary>
    /// Emails the PDF to the supplier (or the address given) and, for a draft, marks it Sent. An order
    /// still waiting for approval cannot go to the supplier.
    /// </summary>
    [HttpPost("email")]
    public async Task<ActionResult<PurchaseOrderEmailResponse>> Email(Guid id, [FromBody] PurchaseOrderEmailRequest request, CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdminOrPermission(PermissionNames.CanManageMaterials);
        var (order, tenant) = await LoadAsync(id, cancellationToken);
        if (order.Status == "Cancelled")
            throw new BusinessRuleException("A cancelled purchase order cannot be sent.");
        if (await approvalWorkflowService.GetActiveForDocumentAsync(TenantId, ApprovalDocumentTypes.PurchaseOrder, order.Id, cancellationToken) is not null)
            throw new BusinessRuleException("This purchase order is awaiting approval and cannot be sent to the supplier yet.");

        var to = (string.IsNullOrWhiteSpace(request.To) ? order.Supplier?.Email : request.To)?.Trim();
        if (string.IsNullOrWhiteSpace(to) || !MailAddress.TryCreate(to, out _))
            throw new BusinessRuleException($"{order.Supplier?.SupplierName ?? "The supplier"} has no valid email address. Enter one to send to, or add it on the supplier.");
        var cc = (request.Cc ?? [])
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (cc.FirstOrDefault(x => !MailAddress.TryCreate(x, out _)) is { } badCc)
            throw new BusinessRuleException($"{badCc} is not a valid email address.");

        var company = PdfTemplateHelper.CompanyName(tenant);
        var subject = $"Purchase order {order.PoNumber} from {company}";
        var greeting = string.IsNullOrWhiteSpace(order.Supplier?.ContactPerson) ? "Hello" : $"Dear {order.Supplier!.ContactPerson}";
        var message = string.IsNullOrWhiteSpace(request.Message)
            ? $"Please find attached purchase order {order.PoNumber}. Kindly confirm availability and the delivery date."
            : request.Message.Trim();
        var expected = order.ExpectedAt is { } date ? $"Requested delivery: {date:dd MMM yyyy}." : null;
        var html = $"<p>{WebUtility.HtmlEncode(greeting)},</p><p>{WebUtility.HtmlEncode(message).Replace("\n", "<br>")}</p>"
            + (expected is null ? string.Empty : $"<p>{WebUtility.HtmlEncode(expected)}</p>")
            + $"<p>Regards,<br>{WebUtility.HtmlEncode(company)}</p>";
        var text = $"{greeting},\n\n{message}\n\n{(expected is null ? string.Empty : expected + "\n\n")}Regards,\n{company}";

        await emailOutboxService.QueueEmailAsync(new QueueEmailRequest(
            TenantId, "purchase-order.sent", "purchase-order.sent",
            to, order.Supplier?.ContactPerson ?? order.Supplier?.SupplierName,
            null, null, null,
            subject, html, text, UserId,
            Attachments: [new EmailAttachment($"{order.PoNumber}.pdf", Render(order, tenant), "application/pdf")],
            CcAddresses: cc,
            DocumentType: "PurchaseOrder",
            DocumentId: order.Id), cancellationToken);

        var markedSent = false;
        if (request.MarkSent && order.Status == "Draft")
        {
            order.Status = "Sent";
            order.UpdatedAt = DateTime.UtcNow;
            markedSent = true;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(TenantId, UserId, "inventory.po.emailed", nameof(PurchaseOrder), order.Id.ToString(),
            $"Purchase order {order.PoNumber} emailed to {to}{(cc.Count > 0 ? $" (cc {string.Join(", ", cc)})" : string.Empty)}{(markedSent ? " and marked Sent" : string.Empty)}.", cancellationToken);

        return Ok(new PurchaseOrderEmailResponse(to, cc, order.Status));
    }

    private async Task<(PurchaseOrder Order, Tenant? Tenant)> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Lines).ThenInclude(x => x.MaterialItem)
            .SingleOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Purchase order not found.");
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken);
        return (order, tenant);
    }

    private byte[] Render(PurchaseOrder order, Tenant? tenant)
    {
        var config = PdfTemplateHelper.ResolveForDocument(tenant?.ReportTemplateConfigJson, tenant?.DocumentDesignsJson, "purchase-order");
        var lines = order.Lines
            .OrderBy(x => x.CreatedAt)
            .Select(x => new InvoiceLinePdfModel(
                x.MaterialItem?.ItemCode is { } code ? $"{x.ItemName} ({code})" : x.ItemName,
                x.MaterialItemId is null ? "Item" : "Part",
                x.QuantityOrdered,
                x.MaterialItem?.UnitOfMeasure ?? "each",
                x.UnitCost,
                x.LineTotal,
                0,
                x.LineTotal))
            .ToList();
        var supplier = order.Supplier;
        return pdfRenderer.RenderPurchaseOrderPdf(new PurchaseOrderPdfModel(
            order.PoNumber,
            order.Status,
            order.OrderedAt,
            order.ExpectedAt,
            supplier?.SupplierName ?? "Supplier",
            supplier?.Address,
            supplier?.Email,
            PdfTemplateHelper.CompanyName(tenant),
            order.Notes,
            lines,
            order.TotalAmount,
            0,
            order.TotalAmount,
            order.Currency,
            PdfTemplateHelper.CompanyName(tenant),
            PdfTemplateHelper.CompanyAddress(tenant),
            PdfTemplateHelper.CompanyPhone(tenant),
            tenant?.LogoUrl,
            config?.ReportPrimaryColor ?? tenant?.PrimaryColor ?? "#0F4C81",
            FontFamily: config?.FontFamily ?? "Helvetica",
            FooterText: PdfTemplateHelper.ResolveFooterText(config),
            WatermarkText: config?.WatermarkText,
            TermsText: config?.ShowTerms == true ? config.DefaultTermsText : null,
            TemplateConfig: config,
            Company: PdfTemplateHelper.CompanyDetails(tenant, environment),
            SupplierContact: supplier is null ? null : new PdfContactDetails(supplier.ContactPerson, supplier.Phone)));
    }
}

/// <param name="To">Defaults to the supplier's email.</param>
/// <param name="MarkSent">Marks a draft Sent once queued (default).</param>
public sealed record PurchaseOrderEmailRequest(string? To = null, IReadOnlyList<string>? Cc = null, string? Message = null, bool MarkSent = true);

public sealed record PurchaseOrderEmailResponse(string To, IReadOnlyList<string> Cc, string Status);
