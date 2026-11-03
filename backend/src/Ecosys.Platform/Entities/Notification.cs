using Ecosys.Shared.Common;

namespace Ecosys.Platform.Entities;

public sealed class Notification : TenantEntity
{
    public Guid? UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSent { get; set; }
    public DateTime? SentUtc { get; set; }
}
