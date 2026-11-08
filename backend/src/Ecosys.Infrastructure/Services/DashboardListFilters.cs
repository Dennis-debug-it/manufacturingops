using System.Linq.Expressions;
using Ecosys.Domain.Entities;
using Ecosys.Shared.Errors;

namespace Ecosys.Infrastructure.Services;

/// <summary>The start of the tenant's local day, in UTC: what "overdue" and "late" are measured against.</summary>
public static class TenantDay
{
    public static DateTime StartUtc(TimeZoneInfo zone, DateTime utcNow)
    {
        var localToday = TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone).Date;
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), zone);
    }
}

public static class PurchaseOrderListStates
{
    public const string Open = "open";
    public const string Late = "late";

    public static readonly string[] OpenStatuses = ["Draft", "Sent", "PartiallyReceived"];

    public static IQueryable<PurchaseOrder> Apply(IQueryable<PurchaseOrder> query, string state, DateTime todayStartUtc) =>
        state.Trim().ToLowerInvariant() switch
        {
            Open => query.Where(x => OpenStatuses.Contains(x.Status)),
            Late => query.Where(x => OpenStatuses.Contains(x.Status) && x.Status != "Draft" && x.ExpectedAt != null && x.ExpectedAt < todayStartUtc),
            _ => throw new BusinessRuleException("State filter must be open or late."),
        };
}

