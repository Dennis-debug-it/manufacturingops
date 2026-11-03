using Ecosys.Platform.Enums;
using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class NumberSequence : TenantEntity
{
    public string EntityType { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int Padding { get; set; } = 6;
    public int CurrentNumber { get; set; }
    public NumberResetFrequency ResetFrequency { get; set; } = NumberResetFrequency.Never;
    public DateTime? LastResetUtc { get; set; }

    public Tenant? Tenant { get; set; }
}
