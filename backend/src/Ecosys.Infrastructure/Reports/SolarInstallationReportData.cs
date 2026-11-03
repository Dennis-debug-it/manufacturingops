using System.Globalization;

namespace Ecosys.Infrastructure.Reports;

public sealed record SolarReportAnswer(string QuestionText, string? Response, string? Remarks);
public sealed record SolarReportPhoto(string Caption, byte[]? ImageBytes);
public sealed record SolarReportSignature(string SignerName, string? SignerRole, DateTime CapturedAt, byte[]? ImageBytes);

/// <summary>Everything the report needs from the job, gathered by the API (dates already in local time).</summary>
public sealed record SolarReportSource(
    string CompanyName,
    string? CompanyPhone,
    string? CompanyEmail,
    byte[]? LogoBytes,
    string WorkOrderNumber,
    string? ClientName,
    string? SiteName,
    string? SiteCounty,
    string? LeadTechnicianName,
    DateTime? CompletedAt,
    DateTime Now,
    IReadOnlyCollection<SolarReportAnswer> Answers,
    IReadOnlyCollection<SolarReportPhoto> Photos,
    SolarReportSignature? TechnicianSignature,
    SolarReportSignature? ClientSignature);

/// <summary>
/// The report's values by placeholder key (the same keys as the client's Word template), plus photos and
/// signatures. Built once and handed to both the PDF and the Word renderer so they always agree.
/// </summary>
public sealed class SolarInstallationReportData
{
    public const string Empty = "—";

    private readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    public required string CompanyName { get; init; }
    public byte[]? LogoBytes { get; init; }
    public required IReadOnlyList<(string Caption, byte[]? ImageBytes)> SlotPhotos { get; init; }
    public required IReadOnlyList<SolarReportPhoto> OtherPhotos { get; init; }
    public SolarReportSignature? TechnicianSignature { get; init; }
    public SolarReportSignature? ClientSignature { get; init; }
    /// <summary>String numbers (1-based) that have any data; empty strings are left out of the table.</summary>
    public required IReadOnlyList<int> Strings { get; init; }
    /// <summary>Fail notes the technician added to test results, by test key prefix.</summary>
    public required IReadOnlyDictionary<string, string> TestRemarks { get; init; }

    public IReadOnlyDictionary<string, string> Values => values;

    /// <summary>The value, or an empty string.</summary>
    public string Get(string key) => values.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>The value, or a dash for table cells.</summary>
    public string Show(string key) => values.TryGetValue(key, out var value) && value.Length > 0 ? value : Empty;

    public bool Has(string key) => Get(key).Length > 0;

    /// <summary>The template's "All tests met the acceptance criteria", unless a test was marked Fail.</summary>
    public string TestsSentence => SolarInstallationTemplate.Tests.Any(t => Get($"{t.Key}_result").Equals("Fail", StringComparison.OrdinalIgnoreCase))
        ? "All tests met the acceptance criteria except those marked Fail in Section 4"
        : "All tests met the acceptance criteria";

    public static SolarInstallationReportData Create(SolarReportSource source)
    {
        var answersByPrompt = source.Answers
            .Where(x => !string.IsNullOrWhiteSpace(x.QuestionText))
            .GroupBy(x => Normalize(x.QuestionText))
            .ToDictionary(x => x.Key, x => x.Last());

        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var remarks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in SolarInstallationTemplate.Fields)
        {
            answersByPrompt.TryGetValue(Normalize(field.Prompt), out var answer);
            var value = Clean(answer?.Response);
            if (field.Type == "date" && value.Length > 0 && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                value = FormatDate(parsed);
            }
            data[field.Key] = value;
            if (field.Type == "passfail" && !string.IsNullOrWhiteSpace(answer?.Remarks))
            {
                remarks[field.Key[..^"_result".Length]] = answer.Remarks.Trim();
            }
        }

        // Peak size from the modules when the technician didn't enter it.
        if (data["system_size_kwp"].Length == 0
            && TryNumber(data["module_qty"], out var qty) && TryNumber(data["module_wattage"], out var watts))
        {
            data["system_size_kwp"] = (qty * watts / 1000m).ToString("0.##", CultureInfo.InvariantCulture);
        }

        var reportDate = source.CompletedAt ?? source.Now;
        data["client_name"] = Clean(source.ClientName);
        data["site_name"] = Clean(source.SiteName);
        data["site_county"] = Clean(source.SiteCounty);
        data["project_ref"] = source.WorkOrderNumber;
        data["report_date"] = FormatDate(reportDate);
        if (data["commissioning_date"].Length == 0)
        {
            data["commissioning_date"] = FormatDate(reportDate);
        }
        data["support_phone"] = Clean(source.CompanyPhone);
        data["support_email"] = Clean(source.CompanyEmail);
        data["plexus_rep_name"] = Clean(source.TechnicianSignature?.SignerName) is { Length: > 0 } techName ? techName : Clean(source.LeadTechnicianName);
        if (data["plexus_rep_title"].Length == 0)
        {
            data["plexus_rep_title"] = Clean(source.TechnicianSignature?.SignerRole);
        }
        data["client_rep_name"] = Clean(source.ClientSignature?.SignerName);
        data["client_rep_title"] = Clean(source.ClientSignature?.SignerRole);
        data["handover_date"] = FormatDate(source.ClientSignature?.CapturedAt ?? reportDate);

        var strings = Enumerable.Range(1, SolarInstallationTemplate.StringCount)
            .Where(i => new[] { "qty", "cfg", "kwp", "vmp", "imp", "voc", "mppt" }.Any(part => data[$"s{i}_{part}"].Length > 0))
            .ToList();

        // Photos are matched to the report's slots by caption ("PV Panels", "Earthing", ...); the first
        // match fills the slot and anything else marked for the report goes under "Additional photos".
        var remaining = source.Photos.ToList();
        var bySlot = new Dictionary<string, SolarReportPhoto>();
        // Exact captions first, then prefixes, longest slot name first so "Earthing test results – 3.2 Ω"
        // goes to "Earthing Test Results" rather than "Earthing".
        foreach (var exact in new[] { true, false })
        {
            foreach (var slot in SolarInstallationTemplate.PhotoSlots.OrderByDescending(x => x.Caption.Length))
            {
                if (bySlot.ContainsKey(slot.Key)) continue;
                var name = Normalize(slot.Caption);
                var match = remaining.FirstOrDefault(p => exact ? Normalize(p.Caption) == name : Normalize(p.Caption).StartsWith(name, StringComparison.Ordinal));
                if (match is null) continue;
                bySlot[slot.Key] = match;
                remaining.Remove(match);
            }
        }
        var slotPhotos = SolarInstallationTemplate.PhotoSlots
            .Select(slot => (slot.Caption, bySlot.TryGetValue(slot.Key, out var photo) ? photo.ImageBytes : null))
            .ToList();

        var result = new SolarInstallationReportData
        {
            CompanyName = source.CompanyName,
            LogoBytes = source.LogoBytes,
            SlotPhotos = slotPhotos,
            OtherPhotos = remaining,
            TechnicianSignature = source.TechnicianSignature,
            ClientSignature = source.ClientSignature,
            Strings = strings,
            TestRemarks = remarks,
        };
        foreach (var (key, value) in data)
        {
            result.values[key] = value;
        }
        return result;
    }

    public static string FormatDate(DateTime value) => value.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string Normalize(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
