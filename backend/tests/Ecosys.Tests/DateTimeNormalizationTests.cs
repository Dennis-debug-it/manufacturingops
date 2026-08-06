using Ecosys.Infrastructure.Services;

namespace Ecosys.Tests;

/// <summary>
/// Covers the local-day helper behind the PM "due today" fix (F6-02): calendar-date decisions must
/// use the operator's local day, not UTC.
/// </summary>
public sealed class DateTimeNormalizationTests
{
    [Fact]
    public void LocalToday_FallsBackToUtc_ForNullOrUnknownZone()
    {
        Assert.Equal(DateTime.UtcNow.Date, DateTimeNormalizationHelper.LocalToday(null));
        Assert.Equal(DateTime.UtcNow.Date, DateTimeNormalizationHelper.LocalToday("Not/AZone"));
    }

    [Fact]
    public void LocalToday_Nairobi_IsNeverBehindUtc()
    {
        var utcToday = DateTime.UtcNow.Date;
        var nairobiToday = DateTimeNormalizationHelper.LocalToday("Africa/Nairobi");

        // EAT is UTC+3 (no DST): its calendar day equals or leads UTC's, never trails it.
        Assert.InRange(nairobiToday, utcToday, utcToday.AddDays(1));
    }
}
