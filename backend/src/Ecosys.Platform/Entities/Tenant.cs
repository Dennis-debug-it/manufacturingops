using Ecosys.Platform.Enums;
using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class Tenant : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Plan { get; set; } = "Standard";
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;
    public bool IsActive { get; set; } = true;

    public TenantSetting? Settings { get; set; }
    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    public ICollection<NumberSequence> NumberSequences { get; set; } = new List<NumberSequence>();
}
