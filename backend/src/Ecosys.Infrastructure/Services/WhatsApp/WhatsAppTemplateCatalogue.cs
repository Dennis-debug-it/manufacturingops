namespace Ecosys.Infrastructure.Services;

public static class WhatsAppAudiences
{
    public const string Staff = "Staff";
    public const string Client = "Client";
    /// <summary>The one-time code that confirms a number; sent to staff or client contacts.</summary>
    public const string Verification = "Verification";
}

/// <summary>Keys for client-facing WhatsApp messages (design 4.2, R6). Not dispatcher keys: no bell, no preferences.</summary>
public static class ClientWhatsAppKeys
{
    public const string TechnicianOnTheWay = "technician-on-the-way";
    public const string WorkOrderCompleted = "work-order-completed";
    public const string InvoiceIssued = "invoice-issued";
    public const string ValuationCertified = "valuation-certified-client";
}

/// <param name="Name">The template name submitted to Meta.</param>
/// <param name="Parameters">
/// The template values that fill {{2}}, {{3}}… in order. {{1}} is always the tenant's name (the number is
/// shared, so every message says who it's from), except for the verification code, whose text Meta fixes.
/// </param>
/// <param name="Body">The exact text submitted to Meta (docs/WHATSAPP_SETUP.md is generated from this list).</param>
public sealed record WhatsAppTemplateDefinition(
    string Key,
    string Name,
    string Audience,
    string Label,
    string Category,
    IReadOnlyList<string> Parameters,
    string Body,
    string? Footer = null,
    string Language = "en")
{
    public bool IsAuthentication => Category == "AUTHENTICATION";
}

/// <summary>
/// The templates Ecosys submits and maintains for the shared number (design 4.2), keyed by notification
/// key. Keyed by string so keys from other steps (e.g. <c>activity.reminder</c>) map here without a
/// compile-time dependency: a key only sends once its <see cref="NotificationKeyDescriptor"/> entry lists
/// <see cref="NotificationChannels.WhatsApp"/> (staff) or a client hook queues it.
/// </summary>
public static class WhatsAppTemplateCatalogue
{
    public const string VerificationKey = "whatsapp.verification";
    public const string TenantNameParameter = "TenantName";

    private const string ClientFooter = "Sent by Ecosys for the business named above. Reply STOP to stop these messages.";
    private const string StaffFooter = "Ecosys reminders. Reply STOP to stop.";

    private static readonly IReadOnlyList<WhatsAppTemplateDefinition> All =
    [
        // ── Staff ────────────────────────────────────────────────────────────
        new("project.muster-missing", "muster_missing", WhatsAppAudiences.Staff, "Muster roll not marked", "UTILITY",
            ["ProjectName", "WorkDate"],
            "*{{1}}*: today's muster roll for {{2}} ({{3}}) hasn't been marked yet. Please mark it in Ecosys.", StaffFooter),
        new("project.task-due", "task_due", WhatsAppAudiences.Staff, "Project task due", "UTILITY",
            ["TaskTitle", "ProjectName", "DueDate"],
            "*{{1}}*: your task \"{{2}}\" on {{3}} is due on {{4}}.", StaffFooter),
        new("project.valuation-certified", "valuation_certified", WhatsAppAudiences.Staff, "Valuation certified", "UTILITY",
            ["ValuationNumber", "ProjectName", "CertifiedAmount"],
            "*{{1}}*: valuation {{2}} on {{3}} was certified for {{4}}. It is ready to invoice in Ecosys.", StaffFooter),
        new("project.retention-due", "retention_due", WhatsAppAudiences.Staff, "Retention due for release", "UTILITY",
            ["Amount", "ProjectName", "Stage"],
            "*{{1}}*: retention of {{2}} on {{3}} is due for release at {{4}}.", StaffFooter),
        new("activity.reminder", "meeting_reminder", WhatsAppAudiences.Staff, "Meeting reminder", "UTILITY",
            ["ActivityTitle", "When", "Location"],
            "*{{1}}*: reminder, \"{{2}}\" starts at {{3}}. Location: {{4}}.", StaffFooter),
        new("action-item.due", "action_item_due", WhatsAppAudiences.Staff, "Action item due", "UTILITY",
            ["ActionItemTitle", "DueDate"],
            "*{{1}}*: your action item \"{{2}}\" is due on {{3}}.", StaffFooter),
        new("work-order.assigned", "work_order_assigned", WhatsAppAudiences.Staff, "Work order assigned", "UTILITY",
            ["WorkOrderNumber", "WorkOrderTitle", "Priority"],
            "*{{1}}*: work order {{2}} \"{{3}}\" ({{4}} priority) has been assigned to you. Open the Ecosys technician app for details.", StaffFooter),

        // ── Clients (R6) ─────────────────────────────────────────────────────
        new(ClientWhatsAppKeys.TechnicianOnTheWay, "technician_on_the_way", WhatsAppAudiences.Client, "Technician on the way", "UTILITY",
            ["TechnicianName", "WorkOrderNumber", "TrackingLink"],
            "*{{1}}*: {{2}} is on the way for job {{3}}. Track the visit here: {{4}}", ClientFooter),
        new(ClientWhatsAppKeys.WorkOrderCompleted, "work_order_completed", WhatsAppAudiences.Client, "Job completed", "UTILITY",
            ["WorkOrderNumber", "TechnicianName", "TrackingLink"],
            "*{{1}}*: job {{2}} has been completed by {{3}}. Details: {{4}}", ClientFooter),
        new(ClientWhatsAppKeys.InvoiceIssued, "invoice_issued", WhatsAppAudiences.Client, "Invoice issued", "UTILITY",
            ["InvoiceNumber", "TotalAmount", "DueDate"],
            "*{{1}}*: invoice {{2}} for {{3}} has been sent to your email. It is due on {{4}}.", ClientFooter),
        new(ClientWhatsAppKeys.ValuationCertified, "valuation_certified_client", WhatsAppAudiences.Client, "Valuation certified", "UTILITY",
            ["ValuationNumber", "ProjectName", "CertifiedAmount"],
            "*{{1}}*: valuation {{2}} on {{3}} has been certified for {{4}}.", ClientFooter),

        // ── Number confirmation ──────────────────────────────────────────────
        // Meta's authentication templates have fixed text and a copy-code button; the code fills both.
        new(VerificationKey, "ecosys_verification_code", WhatsAppAudiences.Verification, "Number confirmation code", "AUTHENTICATION",
            ["Code"],
            "{{1}} is your verification code. For your security, do not share this code.", "This code expires in 10 minutes."),
    ];

    /// <summary>What a tenant allows when it has never saved its WhatsApp settings (design 4.2).</summary>
    public static readonly IReadOnlyList<string> DefaultAllowedKeys =
        ["project.muster-missing", "work-order.assigned", "activity.reminder", ClientWhatsAppKeys.TechnicianOnTheWay];

    public static IReadOnlyList<WhatsAppTemplateDefinition> List() => All;

    /// <summary>Templates a tenant can switch on or off (not the verification code).</summary>
    public static IEnumerable<WhatsAppTemplateDefinition> Selectable() => All.Where(x => x.Audience != WhatsAppAudiences.Verification);

    public static WhatsAppTemplateDefinition? Find(string key) =>
        All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>Body parameters in template order. Meta rejects empty parameters, so a missing value becomes "-".</summary>
    public static IReadOnlyList<string> BuildParameters(WhatsAppTemplateDefinition template, string tenantName, IReadOnlyDictionary<string, string?> values)
    {
        var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
            lookup.TryAdd(name, value);
        var parameters = new List<string>();
        if (!template.IsAuthentication)
            parameters.Add(Clean(tenantName));
        foreach (var name in template.Parameters)
            parameters.Add(Clean(lookup.GetValueOrDefault(name)));
        return parameters;
    }

    /// <summary>Meta refuses newlines, tabs and runs of 4+ spaces in a parameter, and caps its length.</summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";
        var flat = string.Join(' ', value.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= 200 ? flat : flat[..199] + "…";
    }
}
