using Ecosys.Domain.Common;

namespace Ecosys.Domain.Entities;

public static class CheckpointMethods
{
    /// <summary>A printed QR label. Cheap, but a photo of it can be scanned elsewhere; GPS and timing checks help.</summary>
    public const string Qr = "Qr";
    /// <summary>An NFC tag read by tapping the phone (Android Chrome). Its serial number is checked.</summary>
    public const string Nfc = "Nfc";
    /// <summary>Either a QR label or an NFC tag.</summary>
    public const string QrOrNfc = "QrOrNfc";

    public static readonly IReadOnlyList<string> All = [Qr, Nfc, QrOrNfc];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>A physical point a guard must reach on patrol: the weighbridge, the fuel store, a stretch of fence.</summary>
public sealed class Checkpoint : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid? AccessZoneId { get; set; }
    /// <summary>e.g. "CP-007", printed on the label.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Method { get; set; } = CheckpointMethods.QrOrNfc;
    /// <summary>Random value in the QR label ("CHECKPOINT:&lt;token&gt;").</summary>
    public string TagToken { get; set; } = string.Empty;
    /// <summary>Serial number of the NFC tag fixed at the point, once registered.</summary>
    public string? NfcSerial { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    /// <summary>How far from the point a scan may be, in metres, before it is flagged.</summary>
    public int RadiusM { get; set; } = 50;
    /// <summary>What the guard checks here, shown on the phone.</summary>
    public string? Instructions { get; set; }
    /// <summary>Yes/no questions answered at the point, one per line (e.g. "Padlock intact?"). A "no" raises a security incident.</summary>
    public string? Questions { get; set; }
    /// <summary>The guard must take a photo at the point with each scan: proof for QR-only sites, where a label could be photographed and scanned elsewhere.</summary>
    public bool RequirePhoto { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public OrgNode? Factory { get; set; }
    public AccessZone? AccessZone { get; set; }
}

/// <summary>Checkpoints walked in one patrol, in order when <see cref="Ordered"/>.</summary>
public sealed class PatrolRoute : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Ordered { get; set; } = true;
    /// <summary>A patrol is due within this many minutes of its start.</summary>
    public int MaxDurationMinutes { get; set; } = 60;
    /// <summary>Two scans closer together than this are too fast to be walked (a photographed sheet of labels).</summary>
    public int MinGapSeconds { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public OrgNode? Factory { get; set; }
    public ICollection<PatrolRouteCheckpoint> Checkpoints { get; set; } = new List<PatrolRouteCheckpoint>();
}

public sealed class PatrolRouteCheckpoint : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RouteId { get; set; }
    public Guid CheckpointId { get; set; }
    public int Sequence { get; set; }
    public bool Required { get; set; } = true;
    public PatrolRoute? Route { get; set; }
    public Checkpoint? Checkpoint { get; set; }
}

/// <summary>When a route is walked: every N minutes inside a daily window (which may run overnight), or at fixed times.</summary>
public sealed class PatrolSchedule : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid RouteId { get; set; }
    /// <summary>Every this many minutes from <see cref="WindowFrom"/>; null when <see cref="FixedTimes"/> is used.</summary>
    public int? EveryMinutes { get; set; }
    /// <summary>Start times, factory local, comma separated, e.g. "22:00,02:00".</summary>
    public string? FixedTimes { get; set; }
    public TimeOnly WindowFrom { get; set; } = new(18, 0);
    public TimeOnly WindowTo { get; set; } = new(6, 0);
    /// <summary>ISO weekdays (1 = Monday) the window starts on, comma separated.</summary>
    public string Days { get; set; } = "1,2,3,4,5,6,7";
    /// <summary>Minutes after a patrol is due before it counts as missed.</summary>
    public int GraceMinutes { get; set; } = 15;
    /// <summary>The guard who walks it; null means any guard on duty.</summary>
    public Guid? AssignedUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public PatrolRoute? Route { get; set; }
}

public static class PatrolRunStatuses
{
    public const string Due = "Due";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    /// <summary>Over time with some required checkpoints not reached.</summary>
    public const string Partial = "Partial";
    /// <summary>Over time with nothing scanned.</summary>
    public const string Missed = "Missed";

    public static readonly IReadOnlyList<string> Open = [Due, InProgress];
}

/// <summary>One patrol of a route: when it is due, who walked it and how much of it was verified.</summary>
public sealed class PatrolRun : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid RouteId { get; set; }
    /// <summary>Null for a patrol a guard started on their own.</summary>
    public Guid? ScheduleId { get; set; }
    public DateTime DueFrom { get; set; }
    public DateTime DueBy { get; set; }
    public DateTime CloseAt { get; set; }
    public Guid? GuardUserId { get; set; }
    public string? GuardName { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = PatrolRunStatuses.Due;
    public int RequiredCount { get; set; }
    /// <summary>Required checkpoints with a valid scan.</summary>
    public int ValidCount { get; set; }
    public int ScanCount { get; set; }
    public int FlaggedCount { get; set; }
    public DateTime? AlertSentAt { get; set; }
    public PatrolRoute? Route { get; set; }
    public OrgNode? Factory { get; set; }
    public ICollection<CheckpointScan> Scans { get; set; } = new List<CheckpointScan>();
}

public static class ScanValidations
{
    public const string Valid = "Valid";
    /// <summary>The same checkpoint again on this patrol; kept, not counted.</summary>
    public const string Repeat = "Repeat";
    public const string OutOfSequence = "OutOfSequence";
    public const string OutsideGeofence = "OutsideGeofence";
    public const string TooFast = "TooFast";
    /// <summary>A QR scan where the NFC tag is required, or another tag's serial.</summary>
    public const string WrongTag = "WrongTag";
    public const string NotOnRoute = "NotOnRoute";
    /// <summary>The phone's clock is ahead of the server's: the time cannot be trusted.</summary>
    public const string ClockSkew = "ClockSkew";

    public static bool Counts(string validation) => validation == Valid;
}

/// <summary>A guard's scan of a checkpoint, with where and when it happened and what the checks made of it.</summary>
public sealed class CheckpointScan : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Generated on the phone; a scan synced twice is stored once.</summary>
    public Guid ClientScanId { get; set; }
    public Guid? RunId { get; set; }
    public Guid CheckpointId { get; set; }
    public Guid GuardUserId { get; set; }
    public string GuardName { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    /// <summary>How it was read: Qr, Nfc.</summary>
    public string Method { get; set; } = CheckpointMethods.Qr;
    public string? NfcSerial { get; set; }
    /// <summary>When the phone read the tag (it may sync later).</summary>
    public DateTime ScannedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyM { get; set; }
    /// <summary>From the checkpoint, when both have a position.</summary>
    public double? DistanceM { get; set; }
    public string Validation { get; set; } = ScanValidations.Valid;
    public string? ValidationNote { get; set; }
    /// <summary>Answers to the checkpoint's questions, JSON [{"question":…,"ok":true}].</summary>
    public string? AnswersJson { get; set; }
    public string? Note { get; set; }
    /// <summary>Whether the checkpoint asked for a photo when this scan was made.</summary>
    public bool PhotoRequired { get; set; }
    /// <summary>The photo taken at the point (an <see cref="Attachment"/>); it is uploaded after the scan, so it may arrive later.</summary>
    public Guid? PhotoAttachmentId { get; set; }
    public PatrolRun? Run { get; set; }
    public Checkpoint? Checkpoint { get; set; }
}

/// <summary>What one guard shift hands to the next: open issues, keys and equipment.</summary>
public sealed class ShiftHandover : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid FactoryId { get; set; }
    public string? ShiftName { get; set; }
    public Guid OutgoingUserId { get; set; }
    public string OutgoingName { get; set; } = string.Empty;
    public Guid? IncomingUserId { get; set; }
    public string? IncomingName { get; set; }
    public DateTime HandedOverAt { get; set; }
    public string? OpenIssues { get; set; }
    public string? KeysAndEquipment { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public OrgNode? Factory { get; set; }
}

public static class IncidentCategories
{
    public const string Safety = "Safety";
    public const string Security = "Security";
}

/// <summary>Security incident kinds, kept in the same register as safety incidents.</summary>
public static class SecurityIncidentKinds
{
    public const string UnauthorisedAccess = "UnauthorisedAccess";
    public const string Theft = "Theft";
    public const string AttemptedTheft = "AttemptedTheft";
    public const string Intrusion = "Intrusion";
    public const string PerimeterDamage = "PerimeterDamage";
    public const string Tampering = "Tampering";
    public const string SuspiciousPerson = "SuspiciousPerson";
    public const string SuspiciousVehicle = "SuspiciousVehicle";
    public const string Hazard = "Hazard";
    public const string Fire = "Fire";
    /// <summary>Raised by the guard's panic button.</summary>
    public const string Emergency = "Emergency";
    /// <summary>A "no" to a checkpoint question (padlock broken, light out).</summary>
    public const string CheckpointIssue = "CheckpointIssue";

    public static readonly IReadOnlyList<string> All =
        [UnauthorisedAccess, Theft, AttemptedTheft, Intrusion, PerimeterDamage, Tampering, SuspiciousPerson, SuspiciousVehicle, Hazard, Fire, Emergency, CheckpointIssue];

    public static string? Find(string? value) => All.FirstOrDefault(x => string.Equals(x, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class SecurityResponseStatuses
{
    public const string Reported = "Reported";
    public const string Responding = "Responding";
    public const string Contained = "Contained";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> All = [Reported, Responding, Contained, Closed];
}
