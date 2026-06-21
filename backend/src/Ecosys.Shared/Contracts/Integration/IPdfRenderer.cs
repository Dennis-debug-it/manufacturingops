namespace Ecosys.Shared.Contracts.Integration;

public interface IPdfRenderer
{
    byte[] RenderWorkOrderReportPdf(string title, IReadOnlyCollection<string> sections);
    byte[] RenderWorkOrderReportPdf(WorkOrderReportPdfModel report);
    byte[] RenderInvoicePdf(InvoicePdfModel model);
    byte[] RenderReceiptPdf(ReceiptPdfModel model);
    byte[] RenderClientStatementPdf(ClientStatementPdfModel model);
}

public sealed record InvoicePdfModel(
    string InvoiceNumber,
    string Status,
    DateTime InvoiceDate,
    DateTime DueDate,
    string ClientName,
    string? ClientAddress,
    string? ClientEmail,
    string? Notes,
    IReadOnlyList<InvoiceLinePdfModel> Lines,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    string Currency,
    string CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? LogoUrl,
    string PrimaryColor
);

public sealed record InvoiceLinePdfModel(
    string Description,
    string ItemType,
    decimal Quantity,
    string Unit,
    decimal UnitRate,
    decimal LineTotal,
    decimal TaxRate,
    decimal GrandTotal
);

public sealed record ReceiptPdfModel(
    string ReceiptNumber,
    string InvoiceNumber,
    string ClientName,
    DateTime PaymentDate,
    decimal Amount,
    string Currency,
    string Method,
    string? Reference,
    string CompanyName,
    string? LogoUrl,
    string PrimaryColor
);

public sealed record ClientStatementPdfModel(
    string ClientName,
    string? ClientEmail,
    string? ClientAddress,
    DateTime StatementDate,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal OpeningBalance,
    decimal TotalInvoiced,
    decimal TotalPaid,
    decimal ClosingBalance,
    IReadOnlyList<StatementInvoiceRow> Invoices,
    IReadOnlyList<StatementPaymentRow> Payments,
    string CompanyName,
    string? CompanyAddress,
    string? CompanyPhone,
    string? LogoUrl,
    string PrimaryColor
);

public sealed record StatementInvoiceRow(
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    decimal Amount,
    decimal Paid,
    decimal Balance,
    string Status,
    bool IsOverdue
);

public sealed record StatementPaymentRow(
    string PaymentRef,
    DateTime PaymentDate,
    string Method,
    decimal Amount
);

public sealed record WorkOrderReportPdfModel(
    string CompanyName,
    string? LogoUrl,
    byte[]? LogoBytes,          // Pre-fetched logo image bytes for embedding; null if no logo or fetch failed
    string PrimaryColor,
    string SecondaryColor,
    bool ShowPoweredByEcosys,
    string? CompanyPhone,       // Shown in report footer
    string? CompanyEmail,       // Shown in report footer
    string? CompanyAddress,     // Shown in report footer
    string WorkOrderNumber,
    string Title,
    string? ClientName,
    string? SiteLabel,
    string? AssetName,
    string? AssetDetails,
    string? TechnicianTeam,
    string? TemplateName,
    int? TemplateVersion,
    string? ReportedProblem,
    string? Findings,
    string? WorkDone,
    string GeneratedAtLabel,
    IReadOnlyCollection<WorkOrderReportPdfTimestamp> Timestamps,
    IReadOnlyCollection<WorkOrderReportPdfChecklistSection> ChecklistSections,
    IReadOnlyCollection<WorkOrderReportPdfMaterial> Materials,
    IReadOnlyCollection<WorkOrderReportPdfPhotoGroup> PhotoGroups,
    IReadOnlyCollection<WorkOrderReportPdfSignature> Signatures,
    /// <summary>Tenant report template config; null = use defaults.</summary>
    ReportTemplateConfig? TemplateConfig = null);

public sealed record WorkOrderReportPdfTimestamp(string Label, string? Value);
public sealed record WorkOrderReportPdfChecklistSection(string SectionName, IReadOnlyCollection<WorkOrderReportPdfChecklistItem> Items);
public sealed record WorkOrderReportPdfChecklistItem(string Question, string InputType, string? ResponseValue, string? Unit, string? Remarks, bool IsRequired, bool IsCompleted, bool EvidenceRequired, string? EvidenceUrl);
public sealed record WorkOrderReportPdfMaterial(string Name, decimal QuantityUsed, string UnitOfMeasure, decimal? UnitCost, bool Chargeable, string? Notes);
public sealed record WorkOrderReportPdfPhotoGroup(string Category, IReadOnlyCollection<WorkOrderReportPdfPhoto> Photos);
public sealed record WorkOrderReportPdfPhoto(string Caption, string? PublicUrl, byte[]? ImageBytes);
public sealed record WorkOrderReportPdfSignature(string SignatureType, string SignerName, string? SignerRole, string? Comment, string CapturedAtLabel, byte[]? ImageBytes);
