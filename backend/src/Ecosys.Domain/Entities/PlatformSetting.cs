using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public sealed class PlatformSetting : AuditableEntity
{
    public string Category { get; set; } = string.Empty;
    public string JsonValue { get; set; } = "{}";
}
