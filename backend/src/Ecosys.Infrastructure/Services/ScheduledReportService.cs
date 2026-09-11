using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Threading;

namespace Ecosys.Infrastructure.Services;

// ── Interface ────────────────────────────────────────────────────────────────

public interface IScheduledReportService
{
    Task<IReadOnlyList<ScheduledReport>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<ScheduledReport> CreateAsync(Guid tenantId, CreateScheduledReportRequest request, CancellationToken ct = default);
    Task<ScheduledReport> UpdateAsync(Guid tenantId, Guid id, CreateScheduledReportRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ScheduledReport> ToggleAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ScheduledReport> RunNowAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task RunDueReportsAsync(CancellationToken ct = default);
    Task<byte[]> GenerateReportAsync(ScheduledReport report, CancellationToken ct = default);
    Task SendReportEmailAsync(ScheduledReport report, byte[] reportBytes, CancellationToken ct = default);
    Task<DateTime> ComputeNextRunAtAsync(ScheduledReport report);
}

public sealed record CreateScheduledReportRequest(
    string Name,
    string ReportType,
    string Frequency,
    int? DayOfMonth,
    string? DayOfWeek,
    string TimeOfDay,
    string DateRangePreset,
    string? GroupBy,
    string? ClientId,
    string Recipients);

// ── Implementation ───────────────────────────────────────────────────────────

public sealed class ScheduledReportService(
    AppDbContext db,
    IEmailOutboxService emailOutbox,
    ILogger<ScheduledReportService> logger) : IScheduledReportService
{
    // ── CRUD ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ScheduledReport>> ListAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await db.ScheduledReports
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    private static readonly HashSet<string> ValidReportTypes = new(StringComparer.OrdinalIgnoreCase)
        { "Revenue", "Aging", "ClientStatement", "WorkOrderSummary", "Invoices" };

    private static readonly HashSet<string> ValidFrequencies = new(StringComparer.OrdinalIgnoreCase)
        { "Daily", "Weekly", "Monthly", "Quarterly" };

    private static void ValidateRequest(CreateScheduledReportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new Shared.Errors.BusinessRuleException("Report name is required.");

        if (!ValidReportTypes.Contains(req.ReportType))
            throw new Shared.Errors.BusinessRuleException($"Unknown report type '{req.ReportType}'.");

        if (!ValidFrequencies.Contains(req.Frequency))
            throw new Shared.Errors.BusinessRuleException($"Unknown frequency '{req.Frequency}'.");

        var emails = req.Recipients.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (emails.Length == 0)
            throw new Shared.Errors.BusinessRuleException("At least one recipient email is required.");

        foreach (var email in emails)
        {
            if (!email.Contains('@'))
                throw new Shared.Errors.BusinessRuleException($"Invalid recipient email address: '{email}'.");
        }
    }

    public async Task<ScheduledReport> CreateAsync(Guid tenantId, CreateScheduledReportRequest req, CancellationToken ct = default)
    {
        ValidateRequest(req);
        var report = MapToEntity(tenantId, req);
        report.NextRunAt = await ComputeNextRunAtAsync(report);
        db.ScheduledReports.Add(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<ScheduledReport> UpdateAsync(Guid tenantId, Guid id, CreateScheduledReportRequest req, CancellationToken ct = default)
    {
        ValidateRequest(req);
        var report = await LoadOrThrowAsync(tenantId, id, ct);
        report.Name = req.Name.Trim();
        report.ReportType = req.ReportType;
        report.Frequency = req.Frequency;
        report.DayOfMonth = req.DayOfMonth;
        report.DayOfWeek = req.DayOfWeek;
        report.TimeOfDay = req.TimeOfDay;
        report.DateRangePreset = req.DateRangePreset;
        report.GroupBy = req.GroupBy;
        report.ClientId = req.ClientId;
        report.Recipients = req.Recipients.Trim();
        report.NextRunAt = await ComputeNextRunAtAsync(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var report = await LoadOrThrowAsync(tenantId, id, ct);
        db.ScheduledReports.Remove(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ScheduledReport> ToggleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var report = await LoadOrThrowAsync(tenantId, id, ct);
        report.IsActive = !report.IsActive;
        if (report.IsActive)
            report.NextRunAt = await ComputeNextRunAtAsync(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<ScheduledReport> RunNowAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var report = await LoadOrThrowAsync(tenantId, id, ct);
        await ExecuteReportAsync(report, ct);
        await db.SaveChangesAsync(ct);
        return report;
    }

    // ── Scheduled runner ─────────────────────────────────────────────────────

    public async Task RunDueReportsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Load candidate IDs only; we claim each one atomically before working on it
        // so that multiple app instances cannot double-send the same report.
        var dueIds = await db.ScheduledReports
            .Where(x => x.IsActive && x.NextRunAt.HasValue && x.NextRunAt <= now)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // Sentinel used during claim: a far-future NextRunAt that won't match <= now,
        // preventing a second app instance from claiming the same report.
        var claimedSentinel = new DateTime(9999, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var id in dueIds)
        {
            // Atomic claim: set NextRunAt to a far-future sentinel ONLY IF it is still <= now.
            // A second instance sees NextRunAt = 9999 and its WHERE clause fails → 0 rows → skip.
            var claimed = await db.ScheduledReports
                .Where(x => x.Id == id && x.IsActive && x.NextRunAt.HasValue && x.NextRunAt <= now)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.NextRunAt, claimedSentinel)
                          .SetProperty(x => x.LastRunStatus, "Pending"),
                    ct);

            if (claimed == 0)
                continue;

            // Re-load the freshly-claimed entity for execution
            var report = await db.ScheduledReports.FindAsync([id], ct);
            if (report is null) continue;

            try
            {
                await ExecuteReportAsync(report, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled report {Id} ({Name}) failed.", report.Id, report.Name);
                report.LastRunAt = now;
                report.LastRunStatus = "Failed";
                report.NextRunAt = await ComputeNextRunAtAsync(report);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task ExecuteReportAsync(ScheduledReport report, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        report.LastRunStatus = "Pending";

        try
        {
            var bytes = await GenerateReportAsync(report, ct);
            await SendReportEmailAsync(report, bytes, ct);
            report.LastRunAt = now;
            report.LastRunStatus = "Success";
            report.NextRunAt = await ComputeNextRunAtAsync(report);
        }
        catch
        {
            report.LastRunAt = now;
            report.LastRunStatus = "Failed";
            report.NextRunAt = await ComputeNextRunAtAsync(report);
            throw;
        }
    }

    // ── PDF generation ───────────────────────────────────────────────────────

    public async Task<byte[]> GenerateReportAsync(ScheduledReport report, CancellationToken ct = default)
    {
        var (start, end) = ResolveDateRange(report.DateRangePreset);

        return report.ReportType switch
        {
            "Revenue" => await GenerateRevenueReportAsync(report, start, end, ct),
            "Aging" => await GenerateAgingReportAsync(report, start, end, ct),
            "ClientStatement" => await GenerateClientStatementAsync(report, start, end, ct),
            "WorkOrderSummary" => await GenerateWorkOrderSummaryAsync(report, start, end, ct),
            "Invoices" => await GenerateInvoicesReportAsync(report, start, end, ct),
            _ => throw new InvalidOperationException($"Unknown report type: {report.ReportType}")
        };
    }

    private async Task<byte[]> GenerateRevenueReportAsync(ScheduledReport report, DateTime start, DateTime end, CancellationToken ct)
    {
        var invoices = await db.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == report.TenantId && x.Status != "Void"
                && x.InvoiceDate >= start && x.InvoiceDate <= end)
            .ToListAsync(ct);

        var groupBy = report.GroupBy ?? "month";
        var rows = groupBy.ToLower() switch
        {
            "client" => invoices.GroupBy(x => x.Client?.ClientName ?? "Unknown")
                .Select(g => new ReportTableRow(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance), g.Count()))
                .OrderByDescending(r => r.Invoiced).ToList(),
            _ => invoices.GroupBy(x => $"{x.InvoiceDate.Year}-{x.InvoiceDate.Month:D2}")
                .Select(g => new ReportTableRow(g.Key, g.Sum(x => x.TotalAmount), g.Sum(x => x.AmountPaid), g.Sum(x => x.Balance), g.Count()))
                .OrderBy(r => r.Group).ToList()
        };

        return await BuildTablePdfAsync(report.TenantId, ct,
            $"Revenue Report — {FormatDateRange(start, end)}",
            new[] { "Period/Group", "Invoiced", "Collected", "Outstanding", "Count" },
            rows.Select(r => new[] { r.Group, $"{r.Invoiced:N2}", $"{r.Collected:N2}", $"{r.Outstanding:N2}", r.Count.ToString() }).ToList(),
            $"Date range: {FormatDateRange(start, end)}  |  Group by: {groupBy}");
    }

    private async Task<byte[]> GenerateAgingReportAsync(ScheduledReport report, DateTime start, DateTime end, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var invoices = await db.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == report.TenantId && x.Balance > 0 && x.Status != "Void" && x.Status != "Paid")
            .ToListAsync(ct);

        var rows = invoices
            .GroupBy(x => x.Client?.ClientName ?? "Unknown")
            .Select(g =>
            {
                var current = g.Where(x => x.DueDate >= now).Sum(x => x.Balance);
                var d1to30 = g.Where(x => x.DueDate < now && (now - x.DueDate).TotalDays <= 30).Sum(x => x.Balance);
                var d31to60 = g.Where(x => (now - x.DueDate).TotalDays > 30 && (now - x.DueDate).TotalDays <= 60).Sum(x => x.Balance);
                var d61to90 = g.Where(x => (now - x.DueDate).TotalDays > 60 && (now - x.DueDate).TotalDays <= 90).Sum(x => x.Balance);
                var d90plus = g.Where(x => (now - x.DueDate).TotalDays > 90).Sum(x => x.Balance);
                return new[] { g.Key, $"{current:N2}", $"{d1to30:N2}", $"{d31to60:N2}", $"{d61to90:N2}", $"{d90plus:N2}", $"{current + d1to30 + d31to60 + d61to90 + d90plus:N2}" };
            })
            .OrderByDescending(r => decimal.TryParse(r[6], out var v) ? v : 0)
            .ToList();

        return await BuildTablePdfAsync(report.TenantId, ct,
            $"Aging Analysis — {now:yyyy-MM-dd}",
            new[] { "Client", "Current", "1–30 days", "31–60 days", "61–90 days", "90+ days", "Total" },
            rows,
            $"As of {now:yyyy-MM-dd}. Amounts in KES.");
    }

    private async Task<byte[]> GenerateClientStatementAsync(ScheduledReport report, DateTime start, DateTime end, CancellationToken ct)
    {
        var tenantId = report.TenantId;
        IQueryable<TenantInvoice> invQuery = db.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == tenantId && x.InvoiceDate >= start && x.InvoiceDate <= end);

        if (Guid.TryParse(report.ClientId, out var clientGuid))
            invQuery = invQuery.Where(x => x.ClientId == clientGuid);

        var invoices = await invQuery.OrderBy(x => x.InvoiceDate).ToListAsync(ct);
        var rows = invoices.Select(x => new[]
        {
            x.InvoiceDate.ToString("yyyy-MM-dd"),
            x.InvoiceNumber,
            x.Client?.ClientName ?? "",
            x.Status,
            $"{x.TotalAmount:N2}",
            $"{x.AmountPaid:N2}",
            $"{x.Balance:N2}"
        }).ToList();

        var clientLabel = invoices.FirstOrDefault()?.Client?.ClientName ?? "All Clients";
        return await BuildTablePdfAsync(report.TenantId, ct,
            $"Client Statement — {clientLabel} — {FormatDateRange(start, end)}",
            new[] { "Date", "Invoice #", "Client", "Status", "Total", "Paid", "Balance" },
            rows,
            $"Date range: {FormatDateRange(start, end)}  |  Total invoiced: {invoices.Sum(x => x.TotalAmount):N2}  |  Balance: {invoices.Sum(x => x.Balance):N2}");
    }

    private async Task<byte[]> GenerateWorkOrderSummaryAsync(ScheduledReport report, DateTime start, DateTime end, CancellationToken ct)
    {
        var wos = await db.WorkOrders
            .Where(x => x.TenantId == report.TenantId && x.CreatedAt >= start && x.CreatedAt <= end)
            .ToListAsync(ct);

        var total = wos.Count;
        var completed = wos.Count(x => x.Status == "Completed");
        var completionRate = total > 0 ? Math.Round((double)completed / total * 100, 1) : 0.0;
        var avgHours = wos
            .Where(x => x.CompletedAt.HasValue)
            .Select(x => (x.CompletedAt!.Value - x.CreatedAt).TotalHours)
            .DefaultIfEmpty(0)
            .Average();

        var byStatus = wos.GroupBy(x => x.Status)
            .Select(g => new[] { g.Key, g.Count().ToString(), "" })
            .ToList();

        var summaryRows = new List<string[]>
        {
            new[] { "Total Work Orders", total.ToString(), "" },
            new[] { "Completed", completed.ToString(), "" },
            new[] { "Completion Rate", $"{completionRate}%", "" },
            new[] { "Avg. Completion Time", $"{avgHours:N1} hrs", "" },
        };
        summaryRows.AddRange(byStatus.Select(r => new[] { $"  Status: {r[0]}", r[1], "" }));

        return await BuildTablePdfAsync(report.TenantId, ct,
            $"Work Order Summary — {FormatDateRange(start, end)}",
            new[] { "Metric", "Value", "" },
            summaryRows,
            $"Date range: {FormatDateRange(start, end)}");
    }

    private async Task<byte[]> GenerateInvoicesReportAsync(ScheduledReport report, DateTime start, DateTime end, CancellationToken ct)
    {
        var invoices = await db.TenantInvoices
            .Include(x => x.Client)
            .Where(x => x.TenantId == report.TenantId && x.InvoiceDate >= start && x.InvoiceDate <= end)
            .OrderBy(x => x.InvoiceDate)
            .ToListAsync(ct);

        var rows = invoices.Select(x => new[]
        {
            x.InvoiceDate.ToString("yyyy-MM-dd"),
            x.InvoiceNumber,
            x.Client?.ClientName ?? "",
            x.Status,
            $"{x.TotalAmount:N2}",
            $"{x.AmountPaid:N2}",
            $"{x.Balance:N2}"
        }).ToList();

        return await BuildTablePdfAsync(report.TenantId, ct,
            $"Invoice List — {FormatDateRange(start, end)}",
            new[] { "Date", "Invoice #", "Client", "Status", "Total", "Paid", "Balance" },
            rows,
            $"Date range: {FormatDateRange(start, end)}  |  Count: {invoices.Count}  |  Total: {invoices.Sum(x => x.TotalAmount):N2}");
    }

    // ── Email dispatch ───────────────────────────────────────────────────────

    public async Task SendReportEmailAsync(ScheduledReport report, byte[] reportBytes, CancellationToken ct = default)
    {
        var (start, end) = ResolveDateRange(report.DateRangePreset);
        var dateRangeLabel = FormatDateRange(start, end);
        var subject = $"{report.Name} — {dateRangeLabel}";
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == report.TenantId, ct);
        var generatedAt = TenantTimeService.Format(DateTime.UtcNow, tenant?.TimeZoneId, tenant?.Country);
        var body = $"<p>Please find attached your scheduled <strong>{report.ReportType}</strong> report for <strong>{dateRangeLabel}</strong>.</p>"
            + $"<p>Report: <strong>{report.Name}</strong><br/>Generated: {generatedAt}</p>"
            + "<p>This email was sent automatically by Ecosys Scheduled Reports.</p>";

        var recipients = report.Recipients
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fileName = $"report_{report.ReportType.ToLower()}_{DateTime.UtcNow:yyyyMMdd}.pdf";
        var attachmentBase64 = Convert.ToBase64String(reportBytes);
        var htmlBodyWithAttachment = body + $"<!-- attachment:{fileName}:{attachmentBase64} -->";

        foreach (var recipientEmail in recipients)
        {
            await emailOutbox.QueueEmailAsync(
                new QueueEmailRequest(
                    report.TenantId,
                    "scheduled-report",
                    "scheduled-report",
                    recipientEmail,
                    null,
                    null,
                    null,
                    null,
                    subject,
                    htmlBodyWithAttachment,
                    $"Please find your scheduled {report.ReportType} report for {dateRangeLabel} attached.",
                    null),
                ct);
        }

        logger.LogInformation("Queued scheduled report email for {Name} to {Count} recipients.", report.Name, recipients.Count);
    }

    // ── NextRunAt computation ────────────────────────────────────────────────

    public Task<DateTime> ComputeNextRunAtAsync(ScheduledReport report)
    {
        var now = DateTime.UtcNow;
        var time = ParseTime(report.TimeOfDay ?? "08:00");
        DateTime next;

        switch (report.Frequency)
        {
            case "Daily":
                next = now.Date.Add(time);
                if (next <= now) next = next.AddDays(1);
                break;

            case "Weekly":
            {
                var targetDow = ParseDayOfWeek(report.DayOfWeek ?? "Monday");
                next = now.Date.Add(time);
                var daysUntil = ((int)targetDow - (int)now.DayOfWeek + 7) % 7;
                if (daysUntil == 0 && next <= now) daysUntil = 7;
                next = now.Date.AddDays(daysUntil).Add(time);
                break;
            }

            case "Quarterly":
            case "Monthly":
            {
                var dom = Math.Clamp(report.DayOfMonth ?? 1, 1, 28);
                next = new DateTime(now.Year, now.Month, dom, 0, 0, 0, DateTimeKind.Utc).Add(time);
                if (next <= now)
                {
                    var months = report.Frequency == "Quarterly" ? 3 : 1;
                    next = next.AddMonths(months);
                    var clamped = Math.Min(dom, DateTime.DaysInMonth(next.Year, next.Month));
                    next = new DateTime(next.Year, next.Month, clamped, 0, 0, 0, DateTimeKind.Utc).Add(time);
                }
                break;
            }

            default:
                next = now.AddDays(1);
                break;
        }

        return Task.FromResult(next);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<ScheduledReport> LoadOrThrowAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        return await db.ScheduledReports
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Scheduled report {id} not found.");
    }

    private static ScheduledReport MapToEntity(Guid tenantId, CreateScheduledReportRequest req) => new()
    {
        TenantId = tenantId,
        Name = req.Name.Trim(),
        ReportType = req.ReportType,
        Frequency = req.Frequency,
        DayOfMonth = req.DayOfMonth,
        DayOfWeek = req.DayOfWeek,
        TimeOfDay = req.TimeOfDay,
        DateRangePreset = req.DateRangePreset,
        GroupBy = req.GroupBy,
        ClientId = req.ClientId,
        Recipients = req.Recipients.Trim(),
        IsActive = true
    };

    private static (DateTime start, DateTime end) ResolveDateRange(string? preset)
    {
        var now = DateTime.UtcNow;
        return (preset ?? "LastMonth") switch
        {
            "LastWeek" => (now.Date.AddDays(-(int)now.DayOfWeek - 6), now.Date.AddDays(-(int)now.DayOfWeek)),
            "LastMonth" => (new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1),
                            new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1)),
            "LastQuarter" => ComputeLastQuarter(now),
            "LastYear" => (new DateTime(now.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                           new DateTime(now.Year - 1, 12, 31, 23, 59, 59, DateTimeKind.Utc)),
            "YTD" => (new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc), now),
            _ => (now.Date.AddMonths(-1), now)
        };
    }

    private static (DateTime, DateTime) ComputeLastQuarter(DateTime now)
    {
        var q = (now.Month - 1) / 3; // current quarter 0-based
        if (q == 0) q = 4; // wrap to previous year
        var startMonth = ((q - 1) * 3) + 1;
        var year = q == 4 ? now.Year - 1 : now.Year;
        var start = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(3).AddDays(-1));
    }

    private static string FormatDateRange(DateTime start, DateTime end) =>
        $"{start:yyyy-MM-dd} to {end:yyyy-MM-dd}";

    private static TimeSpan ParseTime(string hhmm)
    {
        if (TimeSpan.TryParse(hhmm, out var ts)) return ts;
        return new TimeSpan(8, 0, 0);
    }

    private static DayOfWeek ParseDayOfWeek(string name) => name.ToLower() switch
    {
        "sunday" => DayOfWeek.Sunday,
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        _ => DayOfWeek.Monday
    };

    // ── PDF builder (simple table) ───────────────────────────────────────────

    private async Task<byte[]> BuildTablePdfAsync(
        Guid tenantId, CancellationToken ct,
        string title,
        string[] headers,
        List<string[]> rows,
        string footer)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId, ct);
        var generatedAt = TenantTimeService.Format(DateTime.UtcNow, tenant?.TimeZoneId, tenant?.Country);
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
            container.Page(page =>
            {
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Helvetica"));

                page.Header().Column(h =>
                {
                    h.Item().Text(title).FontSize(14).SemiBold();
                    h.Item().Text($"Generated: {generatedAt}").FontSize(8).FontColor(Colors.Grey.Darken1);
                    h.Item().PaddingTop(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    // Column widths: spread evenly
                    table.ColumnsDefinition(cols =>
                    {
                        for (var i = 0; i < headers.Length; i++)
                            cols.RelativeColumn();
                    });

                    static IContainer HeaderCell(IContainer c) =>
                        c.Background(Colors.Indigo.Darken1).Padding(5).AlignMiddle();

                    table.Header(h =>
                    {
                        foreach (var header in headers)
                            h.Cell().Element(HeaderCell).Text(header).FontSize(9).SemiBold().FontColor(Colors.White);
                    });

                    var altRow = Colors.Grey.Lighten4;
                    for (var i = 0; i < rows.Count; i++)
                    {
                        var bg = i % 2 == 0 ? Colors.White : altRow;
                        IContainer DataCell(IContainer c) =>
                            c.Background(bg).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignMiddle();

                        foreach (var cell in rows[i])
                            table.Cell().Element(DataCell).Text(cell ?? "").FontSize(9);
                    }
                });

                page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text(footer).FontSize(7).FontColor(Colors.Grey.Darken1);
                    row.AutoItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(7);
                        text.CurrentPageNumber().FontSize(7);
                        text.Span(" of ").FontSize(7);
                        text.TotalPages().FontSize(7);
                    });
                });
            }))
            .GeneratePdf();
    }

    private sealed record ReportTableRow(string Group, decimal Invoiced, decimal Collected, decimal Outstanding, int Count);
}

// ── BackgroundService worker ─────────────────────────────────────────────────

public sealed class ScheduledReportWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<ScheduledReportWorker> logger) : BackgroundService
{
    private int missingSchemaWarningLogged;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Scheduled report worker started.");

        // Run immediately on startup to catch anything due right away.
        await RunCycleAsync(stoppingToken);

        // Then align to clock-minute boundaries so a job set for "08:15 UTC"
        // is checked within one minute of that time regardless of process start.
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Sleep until the top of the next minute
            var msUntilNextMinute = (60 - now.Second) * 1_000 - now.Millisecond;
            if (msUntilNextMinute < 100) msUntilNextMinute += 60_000;
            try
            {
                await Task.Delay(msUntilNextMinute, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            using var scope = serviceScopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IScheduledReportService>();
            await svc.RunDueReportsAsync(ct);
            Interlocked.Exchange(ref missingSchemaWarningLogged, 0);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (PostgresException ex) when (IsMissingScheduledReportsTable(ex))
        {
            if (Interlocked.Exchange(ref missingSchemaWarningLogged, 1) == 0)
            {
                logger.LogWarning(
                    "Skipping scheduled report worker cycles because the 'scheduled_reports' table is missing. Apply pending database migrations to enable scheduled reports.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled report worker cycle failed.");
        }
    }

    private static bool IsMissingScheduledReportsTable(PostgresException ex)
        => ex.SqlState == PostgresErrorCodes.UndefinedTable
            && ex.MessageText.Contains("scheduled_reports", StringComparison.OrdinalIgnoreCase);
}
