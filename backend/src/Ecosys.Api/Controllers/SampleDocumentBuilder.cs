using Ecosys.Domain.Entities;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;

namespace Ecosys.Api.Controllers;

/// <summary>
/// Builds representative sample PDFs for the document designer's "Preview with sample data" action.
/// Each document type is rendered with synthetic but realistic content so the tenant can see exactly
/// how their design config affects the finished document without needing a real record.
/// </summary>
internal static class SampleDocumentBuilder
{
    public static byte[] Render(string documentTypeSlug, Tenant tenant, ReportTemplateConfig config, IPdfRenderer renderer,
        PdfCompanyDetails? companyDetails = null)
    {
        // Real tenant identity (logo, PIN, VAT) where configured; sample values fill the gaps so the
        // preview shows where each detail appears.
        var details = (companyDetails ?? new PdfCompanyDetails()) with
        {
            Email = companyDetails?.Email ?? "accounts@yourcompany.co.ke",
            TaxPin = companyDetails?.TaxPin ?? "P051234567X",
        };
        var company = string.IsNullOrWhiteSpace(tenant.CompanyName) ? "Your Company Ltd" : tenant.CompanyName;
        var address = string.Join(", ", new[] { tenant.Address, tenant.City, tenant.Country }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        address = string.IsNullOrWhiteSpace(address) ? "Nairobi, Kenya" : address;
        var phone = string.IsNullOrWhiteSpace(tenant.Phone) ? "+254 700 000 000" : tenant.Phone;
        var primary = tenant.PrimaryColor;
        var font = config.FontFamily;
        var currency = config.DefaultCurrency;
        var footer = config.FooterText ?? config.DefaultFooterNote;
        var watermark = config.WatermarkText;

        var lines = new List<InvoiceLinePdfModel>
        {
            new("Quarterly AHU preventive maintenance", "Service", 1, "unit", 18500m, 18500m, 16m, 21460m),
            new("Air filter set (MERV 13)", "Material", 4, "pcs", 2150m, 8600m, 16m, 9976m),
            new("Call-out labour — senior technician", "Labour", 3, "hrs", 3200m, 9600m, 16m, 11136m),
        };
        const decimal subtotal = 36700m;
        const decimal tax = 5872m;
        const decimal total = 42572m;

        var now = DateTime.UtcNow;

        return documentTypeSlug switch
        {
            "purchase-order" => renderer.RenderPurchaseOrderPdf(new PurchaseOrderPdfModel(
                "PO-000078", "Approved", now, now.AddDays(14),
                "Coastline HVAC Supplies Ltd", "Mombasa Rd, Nairobi", "sales@coastlinehvac.co.ke",
                $"{company} — Main Store",
                "Please confirm availability and delivery schedule.",
                lines, subtotal, tax, total, currency,
                company, address, phone, tenant.LogoUrl, primary,
                FontFamily: font, FooterText: footer, WatermarkText: watermark,
                TermsText: config.ShowTerms ? config.DefaultTermsText : null,
                TemplateConfig: config,
                Company: details,
                SupplierContact: new PdfContactDetails("Peter Kamau", "+254 722 000 222"))),

            "service-report" => renderer.RenderWorkOrderReportPdf(BuildSampleServiceReport(company, address, phone, tenant, config, now)),

            _ => throw new BusinessRuleException($"No sample renderer for document type '{documentTypeSlug}'."),
        };
    }

    private static WorkOrderReportPdfModel BuildSampleServiceReport(
        string company, string address, string phone, Tenant tenant, ReportTemplateConfig config, DateTime now)
    {
        var checklist = new List<WorkOrderReportPdfChecklistSection>
        {
            new("Pre-service inspection", new List<WorkOrderReportPdfChecklistItem>
            {
                new("Isolate power supply", "boolean", "Yes", null, "Locked out per procedure", true, true, false, null),
                new("Static pressure (Pa)", "reading", "320", "Pa", null, true, true, false, null),
                new("Belt condition", "passfail", "Pass", null, null, true, true, false, null),
            }),
        };

        var materials = new List<WorkOrderReportPdfMaterial>
        {
            new("Air filter set (MERV 13)", 4, "pcs", 2150m, true, "Replaced loaded filters"),
            new("Drive belt A-42", 1, "pcs", 1450m, true, null),
        };

        var signatures = new List<WorkOrderReportPdfSignature>
        {
            new("Technician", "James Otieno", "Senior Technician", "Work completed and tested.", now.ToString("dd MMM yyyy HH:mm"), null),
        };

        return new WorkOrderReportPdfModel(
            company, tenant.LogoUrl, null,
            tenant.PrimaryColor, tenant.SecondaryColor, config.ShowPoweredByEcosys,
            phone, tenant.Email, address,
            "WO-000078", "AHU-03 high static pressure alarm", "Main Campus — Ward B", "AHU-03", "Rooftop air handling unit, 5,000 CFM",
            "HVAC Response Team", "Quarterly PPM Checklist", 3,
            "AHU-03 tripping on high static pressure; airflow to Ward B reduced.",
            "Filters heavily loaded; drive belt tension low; pressure sensor within calibration.",
            "Replaced filter set, re-tensioned belt, verified static pressure at 320 Pa. Unit returned to service.",
            now.ToString("yyyy-MM-dd HH:mm 'UTC'"),
            new List<WorkOrderReportPdfTimestamp>
            {
                new("Arrived on site", now.AddHours(-3).ToString("dd MMM yyyy HH:mm")),
                new("Work completed", now.AddHours(-1).ToString("dd MMM yyyy HH:mm")),
            },
            checklist, materials,
            new List<WorkOrderReportPdfPhotoGroup>(),
            signatures,
            config);
    }
}
