using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PmTemplateQuestion : AuditableEntity
{
    public Guid PmTemplateId { get; set; }
    public Guid? SectionId { get; set; }
    public string? SectionName { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? HelperText { get; set; }
    public string ResponseType { get; set; } = "text";
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public bool IsRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public bool TechnicianRemarksAllowed { get; set; } = true;
    public bool RequiresNoteOnFail { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? OptionsJson { get; set; }

    /// <summary>What a failed answer does in a running check or inspection: None, WorkOrder or WindowDefect.</summary>
    public string? FailAction { get; set; }
    /// <summary>A reading that is also recorded on the asset's meter of this type (e.g. RunHours, Temperature).</summary>
    public string? FeedsMeterType { get; set; }

    public PmTemplate? PmTemplate { get; set; }
    public PmTemplateSection? Section { get; set; }
}
