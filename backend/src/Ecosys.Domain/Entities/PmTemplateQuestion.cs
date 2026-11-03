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

    public PmTemplate? PmTemplate { get; set; }
    public PmTemplateSection? Section { get; set; }
}
