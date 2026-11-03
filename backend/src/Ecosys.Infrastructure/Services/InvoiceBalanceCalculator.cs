using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Single source of truth for invoice payment state and client running balances.
/// An invoice's paid amount is the sum of its <b>active</b> <see cref="PaymentAllocation"/> rows
/// (from both payments and applied credit notes). All flows that change payments, allocations or
/// credit-note applications must route through here so balances and statuses stay consistent.
/// <para>
/// <b>Save discipline.</b> Nothing in this class calls <c>SaveChanges</c>. Every aggregate it needs
/// (allocation sums, invoice balances) is read from the database and then <i>corrected by the
/// pending, not-yet-saved changes sitting in the <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/></i>.
/// That makes the helpers order-independent: a caller may apply allocations, recompute invoices and
/// recompute client balances in any order and finish with a single <c>SaveChangesAsync</c>, and the
/// numbers still agree. Previously the helpers saved half-way through and the client-balance query
/// then read stale invoice balances straight from the database (EC-012).
/// </para>
/// </summary>
public static class InvoiceBalanceCalculator
{
    private const string ActiveAllocation = "Active";
    private const string VoidInvoice = "Void";

    /// <summary>
    /// Recomputes <see cref="TenantInvoice.AmountPaid"/>, <see cref="TenantInvoice.Balance"/> and
    /// <see cref="TenantInvoice.Status"/> from active allocations, including allocations that are
    /// tracked but not yet saved. Draft and Void invoices keep their status. The invoice is mutated
    /// in place; the caller owns SaveChanges.
    /// </summary>
    public static async Task RecalcInvoiceAsync(AppDbContext db, TenantInvoice invoice, CancellationToken ct)
    {
        var persisted = await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoice.Id && a.Status == ActiveAllocation)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        var allocated = persisted + PendingAllocatedDelta(db, invoice.Id);

        invoice.AmountPaid = allocated;
        invoice.Balance = invoice.TotalAmount - allocated;

        if (invoice.Status is "Draft" or VoidInvoice)
            return;

        if (invoice.TotalAmount > 0 && allocated >= invoice.TotalAmount)
            invoice.Status = "Paid";
        else if (allocated > 0)
            invoice.Status = "PartiallyPaid";
        else if (invoice.DueDate < DateTime.UtcNow)
            invoice.Status = "Overdue";
        else
            invoice.Status = "Sent";
    }

    /// <summary>
    /// Recomputes a client's running balance as the sum of open (non-void) invoice balances,
    /// including invoice changes that are tracked but not yet saved. Mutates the client in place;
    /// the caller owns SaveChanges.
    /// </summary>
    public static async Task RecalcClientBalanceAsync(AppDbContext db, Client client, CancellationToken ct)
    {
        var persisted = await db.TenantInvoices
            .Where(i => i.ClientId == client.Id && i.Status != VoidInvoice)
            .SumAsync(i => (decimal?)i.Balance, ct) ?? 0m;

        client.CurrentBalance = persisted + PendingInvoiceBalanceDelta(db, client.Id);
    }

    /// <summary>
    /// Recomputes the running balance of every client that owns one of <paramref name="invoices"/>
    /// (plus any extra client ids supplied). Loads each client through the context so already-tracked
    /// instances held by the caller are the ones updated. The caller owns SaveChanges.
    /// </summary>
    public static async Task<IReadOnlyList<Client>> RecalcClientBalancesAsync(
        AppDbContext db,
        Guid tenantId,
        IEnumerable<TenantInvoice> invoices,
        CancellationToken ct,
        params Guid[] additionalClientIds)
    {
        var clientIds = invoices.Select(i => i.ClientId)
            .Concat(additionalClientIds)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (clientIds.Count == 0)
            return [];

        var clients = await db.Clients
            .Where(c => c.TenantId == tenantId && clientIds.Contains(c.Id))
            .ToListAsync(ct);

        foreach (var client in clients)
            await RecalcClientBalanceAsync(db, client, ct);

        return clients;
    }

    /// <summary>
    /// Creates active allocation rows for a money source (Payment or CreditNote) against the given
    /// pre-validated invoices, then recomputes each invoice. Nothing is saved here: allocations,
    /// invoices and client balances are all persisted by the caller's single SaveChanges, so the
    /// whole operation commits (or rolls back) as one unit. Caller owns client-balance recompute and
    /// the surrounding validation (ownership, currency, caps).
    /// </summary>
    public static async Task ApplyAllocationsAsync(
        AppDbContext db,
        Guid tenantId,
        string sourceType,
        Guid sourceId,
        IReadOnlyList<(TenantInvoice Invoice, decimal Amount)> items,
        CancellationToken ct)
    {
        foreach (var (invoice, amount) in items)
        {
            db.PaymentAllocations.Add(new PaymentAllocation
            {
                TenantId = tenantId,
                SourceType = sourceType,
                SourceId = sourceId,
                InvoiceId = invoice.Id,
                Amount = amount,
                AllocatedAt = DateTime.UtcNow,
                Status = ActiveAllocation,
            });
        }

        foreach (var (invoice, _) in items)
            await RecalcInvoiceAsync(db, invoice, ct);
    }

    /// <summary>
    /// Reverses all active allocations for a money source (e.g. when a payment is reversed or a
    /// credit note voided), recomputes each affected invoice, and returns the invoices touched so
    /// the caller can recompute client balances. Nothing is saved here; caller owns SaveChanges.
    /// </summary>
    public static async Task<IReadOnlyList<TenantInvoice>> ReverseAllocationsForSourceAsync(
        AppDbContext db, Guid tenantId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var allocations = await db.PaymentAllocations
            .Where(a => a.TenantId == tenantId && a.SourceType == sourceType && a.SourceId == sourceId && a.Status == ActiveAllocation)
            .ToListAsync(ct);

        if (allocations.Count == 0)
            return [];

        foreach (var allocation in allocations)
            allocation.Status = "Reversed";

        var invoiceIds = allocations.Select(a => a.InvoiceId).Distinct().ToList();
        var invoices = await db.TenantInvoices
            .Where(i => i.TenantId == tenantId && invoiceIds.Contains(i.Id))
            .ToListAsync(ct);

        foreach (var invoice in invoices)
            await RecalcInvoiceAsync(db, invoice, ct);

        return invoices;
    }

    /// <summary>
    /// Available customer credit: unallocated confirmed payments plus unapplied issued credit notes.
    /// </summary>
    public static async Task<decimal> GetAvailableCreditAsync(AppDbContext db, Guid tenantId, Guid clientId, CancellationToken ct)
    {
        var confirmedPayments = await db.TenantPayments
            .Where(p => p.TenantId == tenantId && p.ClientId == clientId && p.Status == "Confirmed")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var paymentAllocated = await db.PaymentAllocations
            .Where(a => a.TenantId == tenantId && a.SourceType == AllocationSourceTypes.Payment && a.Status == ActiveAllocation)
            .Join(db.TenantPayments.Where(p => p.ClientId == clientId && p.Status == "Confirmed"),
                a => a.SourceId, p => p.Id, (a, _) => a.Amount)
            .SumAsync(x => (decimal?)x, ct) ?? 0m;

        var creditNoteBalance = await db.CreditNotes
            .Where(c => c.TenantId == tenantId && c.ClientId == clientId
                        && (c.Status == "Issued" || c.Status == "PartiallyApplied"))
            .SumAsync(c => (decimal?)c.Balance, ct) ?? 0m;

        return (confirmedPayments - paymentAllocated) + creditNoteBalance;
    }

    // ── Pending (unsaved) change overlays ────────────────────────────────────
    // The SUM queries above run in the database and therefore only see committed rows. These helpers
    // add back what the change tracker is holding: newly added rows the SUM missed, and modified or
    // deleted rows whose committed values the SUM counted with the wrong (original) value.

    /// <summary>
    /// How much the tracked-but-unsaved allocation rows would change the active allocated total for
    /// <paramref name="invoiceId"/>.
    /// </summary>
    private static decimal PendingAllocatedDelta(AppDbContext db, Guid invoiceId)
    {
        var delta = 0m;

        foreach (var entry in db.ChangeTracker.Entries<PaymentAllocation>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            // Modified/Deleted rows were counted by the SUM at their original values — remove those.
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var original = entry.OriginalValues;
                if (original.GetValue<Guid>(nameof(PaymentAllocation.InvoiceId)) == invoiceId
                    && original.GetValue<string>(nameof(PaymentAllocation.Status)) == ActiveAllocation)
                {
                    delta -= original.GetValue<decimal>(nameof(PaymentAllocation.Amount));
                }
            }

            // Added/Modified rows carry the value that should count.
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var allocation = entry.Entity;
                if (allocation.InvoiceId == invoiceId && allocation.Status == ActiveAllocation)
                    delta += allocation.Amount;
            }
        }

        return delta;
    }

    /// <summary>
    /// How much the tracked-but-unsaved invoice rows would change the open-balance total for
    /// <paramref name="clientId"/>.
    /// </summary>
    private static decimal PendingInvoiceBalanceDelta(AppDbContext db, Guid clientId)
    {
        var delta = 0m;

        foreach (var entry in db.ChangeTracker.Entries<TenantInvoice>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                var original = entry.OriginalValues;
                if (original.GetValue<Guid>(nameof(TenantInvoice.ClientId)) == clientId
                    && original.GetValue<string>(nameof(TenantInvoice.Status)) != VoidInvoice)
                {
                    delta -= original.GetValue<decimal>(nameof(TenantInvoice.Balance));
                }
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                var invoice = entry.Entity;
                if (invoice.ClientId == clientId && invoice.Status != VoidInvoice)
                    delta += invoice.Balance;
            }
        }

        return delta;
    }
}
