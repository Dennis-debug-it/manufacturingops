using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

/// <summary>
/// The response to a write that carried an <c>Idempotency-Key</c> header. The technician app sends
/// one with every action it queued offline; if the same action is replayed (the connection dropped
/// after the server applied it but before the phone heard back), the stored response is returned
/// instead of applying it twice.
/// </summary>
public sealed class IdempotencyRecord : AuditableEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string? ResponseBody { get; set; }
}
