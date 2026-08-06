using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;

namespace Ecosys.Tests;

/// <summary>
/// Locks in the status-filter expansion that lets the Work Orders list filter run as a DB-level
/// IN-list while still matching legacy/synonym stored statuses.
/// </summary>
public sealed class WorkOrderStatusFilterTests
{
    [Fact]
    public void RawStatusesMatching_IncludesTheCanonicalStatusItself()
    {
        var matches = WorkOrderStatuses.RawStatusesMatching(WorkOrderStatuses.InProgress);
        Assert.Contains(WorkOrderStatuses.InProgress, matches);
    }

    [Theory]
    [InlineData("Completed", "Ready to Close")]
    [InlineData("New", "Draft")]
    [InlineData("Client Accepted", "Acknowledged")]
    [InlineData("On Hold", "Paused")]
    [InlineData("On Hold", "Awaiting Materials")]
    public void RawStatusesMatching_IncludesLegacySynonyms(string canonical, string legacySynonym)
    {
        var matches = WorkOrderStatuses.RawStatusesMatching(canonical);

        Assert.Contains(canonical, matches);
        Assert.Contains(legacySynonym, matches);
    }

    [Fact]
    public void RawStatusesMatching_EveryReturnedValueNormalizesToTheRequestedStatus()
    {
        var canonical = WorkOrderStatuses.Normalize("Completed");
        var matches = WorkOrderStatuses.RawStatusesMatching("Completed");

        Assert.All(matches, raw => Assert.Equal(canonical, WorkOrderStatuses.Normalize(raw)));
    }

    [Fact]
    public void RawStatusesMatching_Throws_ForUnknownStatus()
    {
        Assert.Throws<BusinessRuleException>(() => WorkOrderStatuses.RawStatusesMatching("Not A Real Status"));
    }
}
