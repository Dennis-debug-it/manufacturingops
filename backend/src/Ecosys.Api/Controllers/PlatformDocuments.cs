using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Puts platform finance records (quotations, invoices, receipts) into the shape <see cref="PlatformDocumentPdf"/>
/// draws, with the template designed for the type and the platform's own name and logo.
/// </summary>
internal static class PlatformDocuments
{
    /// <summary>The default active template of the type; else any active one; else the built-in look.</summary>
    public static async Task<PlatformDocumentTemplate> TemplateAsync(AppDbContext db, string type, CancellationToken ct)
    {
        var lowered = type.ToLowerInvariant();
        var candidates = await db.PlatformDocumentTemplates.AsNoTracking()
            .Where(x => x.IsActive && x.Type.ToLower() == lowered)
            .OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ToListAsync(ct);
        return candidates.FirstOrDefault() ?? PlatformDocumentPdf.BuiltInTemplate(type);
    }

    public static async Task<PlatformDocumentIssuer> IssuerAsync(AppDbContext db, IWebHostEnvironment? environment, CancellationToken ct)
    {
        var platform = await db.Tenants.AsNoTracking().IgnoreQueryFilters()
            .Where(x => x.Id == PlatformConstants.RootTenantId)
            .Select(x => new { x.CompanyName, x.Name, x.LogoUrl })
            .SingleOrDefaultAsync(ct);
        var name = !string.IsNullOrWhiteSpace(platform?.CompanyName) ? platform!.CompanyName
            : !string.IsNullOrWhiteSpace(platform?.Name) ? platform!.Name : "Ecosys";
        return new PlatformDocumentIssuer(name, PdfTemplateHelper.TryLoadLogoBytes(platform?.LogoUrl, environment));
    }

    public static async Task<byte[]> RenderAsync(AppDbContext db, IWebHostEnvironment? environment, PlatformDocumentData data, CancellationToken ct) =>
        PlatformDocumentPdf.Render(await TemplateAsync(db, data.Type, ct), data, await IssuerAsync(db, environment, ct));

    public static PlatformDocumentData FromInvoice(PlatformInvoice x) => new(
        PlatformDocumentTypes.Invoice, x.InvoiceNumber, x.IssueDate, x.DueDate.HasValue ? "Due" : null, x.DueDate,
        x.CustomerName, x.CustomerEmail, x.Currency, Lines(x.Lines.Select(l => (l.Description, l.Quantity, l.UnitPrice, l.LineTotal))),
        x.Subtotal, x.DiscountRate, x.DiscountAmount, x.TaxRate, x.TaxAmount, x.Total, x.AmountPaid, x.Balance, x.Notes, x.Status);

    public static PlatformDocumentData FromQuotation(PlatformQuotation x) => new(
        PlatformDocumentTypes.Quotation, x.QuotationNumber, x.CreatedAt, x.ValidUntil.HasValue ? "Valid until" : null, x.ValidUntil,
        x.CustomerName, x.CustomerEmail, x.Currency, Lines(x.Lines.Select(l => (l.Description, l.Quantity, l.UnitPrice, l.LineTotal))),
        x.Subtotal, x.DiscountRate, x.DiscountAmount, x.TaxRate, x.TaxAmount, x.Total, Notes: x.Notes, Status: x.Status);

    public static PlatformDocumentData FromPayment(PlatformPayment x, PlatformInvoice? invoice, string? customerName) => new(
        PlatformDocumentTypes.Receipt, x.PaymentNumber, x.PaidAt, null, null,
        invoice?.CustomerName ?? customerName ?? "Customer", invoice?.CustomerEmail, x.Currency, [],
        x.Amount, 0, 0, 0, 0, x.Amount, Balance: invoice?.Balance, Notes: x.Notes, Status: x.Status,
        PaymentMethod: x.Method, PaymentReference: x.Reference, AppliesTo: invoice?.InvoiceNumber);

    public static string FileName(string number, string customer)
    {
        var slug = new string(customer.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? $"{number}.pdf" : $"{number}-{(slug.Length > 40 ? slug[..40] : slug)}.pdf";
    }

    private static List<PlatformDocumentLine> Lines(IEnumerable<(string Description, decimal Quantity, decimal UnitPrice, decimal Total)> lines) =>
        lines.Select(l => new PlatformDocumentLine(l.Description, l.Quantity, l.UnitPrice, l.Total)).ToList();
}
