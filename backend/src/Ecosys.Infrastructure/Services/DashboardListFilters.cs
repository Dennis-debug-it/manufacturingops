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

/// <summary>
/// The invoice list's <c>age</c> filter, shared with the finance widgets so each figure links to
/// exactly the invoices it sums. Every age is a receivable (an issued invoice — not a draft,
/// proforma or void — with a balance); <c>overdue</c> is one past its due date, and the buckets split
/// those by how far, against the start of the tenant's day.
/// </summary>
public static class ReceivableAges
{
    public const string Overdue = "overdue";
    public const string Current = "current";

    /// <summary>The ageing buckets in order: "Not yet due", then 1–30 … 90+ days past due.</summary>
    public static readonly IReadOnlyList<(string Key, string Label, int FromDays, int? ToDays, string? Tone)> Buckets =
    [
        (Current, "Not yet due", 0, null, null),
        ("1-30", "1–30 days", 1, 30, "warning"),
        ("31-60", "31–60 days", 31, 60, "warning"),
        ("61-90", "61–90 days", 61, 90, "danger"),
        ("90-plus", "90+ days", 91, null, "danger"),
    ];

    public static IQueryable<TenantInvoice> Receivables(IQueryable<TenantInvoice> query) =>
        query.Where(x => x.DocumentType == "Invoice" && x.Status != "Draft" && x.Status != "Void" && x.Balance > 0);

    /// <summary>Whole days past due, counting any part of a day: due yesterday afternoon is 1 day overdue.</summary>
    public static int DaysOverdue(DateTime dueDate, DateTime todayStartUtc) =>
        dueDate >= todayStartUtc ? 0 : (int)Math.Ceiling((todayStartUtc - dueDate).TotalDays);

    public static string BucketOf(DateTime dueDate, DateTime todayStartUtc)
    {
        var days = DaysOverdue(dueDate, todayStartUtc);
        return Buckets.Last(x => days >= x.FromDays).Key;
    }

    public static IQueryable<TenantInvoice> Apply(IQueryable<TenantInvoice> query, string age, DateTime todayStartUtc)
    {
        var key = age.Trim().ToLowerInvariant();
        var receivables = Receivables(query);
        if (key == Overdue)
            return receivables.Where(x => x.DueDate < todayStartUtc);
        if (key == Current)
            return receivables.Where(x => x.DueDate >= todayStartUtc);
        var bucket = Buckets.FirstOrDefault(x => x.Key == key && x.Key != Current);
        if (bucket.Key is null)
            throw new BusinessRuleException("Age filter must be overdue, current, 1-30, 31-60, 61-90 or 90-plus.");
        // DaysOverdue >= From  <=>  due < todayStart - (From - 1) days;  DaysOverdue <= To  <=>  due >= todayStart - To days.
        var newest = todayStartUtc.AddDays(-(bucket.FromDays - 1));
        var filtered = receivables.Where(x => x.DueDate < newest);
        if (bucket.ToDays is { } to)
        {
            var oldest = todayStartUtc.AddDays(-to);
            filtered = filtered.Where(x => x.DueDate >= oldest);
        }
        return filtered;
    }
}

/// <summary>
/// The purchase-order list's <c>state</c> filter, shared with the "Open purchase orders" widget: open
/// is not yet fully received (draft, sent or partly received); late is sent or partly received and
/// past its expected date.
/// </summary>
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

/// <summary>The project register's <c>mine</c> filter, shared with the "My projects" widget.</summary>
public static class ProjectListFilters
{
    /// <summary>Managed by the user, or the user is on the team (directly or as a technician).</summary>
    public static Expression<Func<Project, bool>> InvolvesUser(Guid userId) =>
        x => x.ProjectManagerUserId == userId || x.Members.Any(m => m.UserId == userId || (m.Technician != null && m.Technician.UserId == userId));
}
