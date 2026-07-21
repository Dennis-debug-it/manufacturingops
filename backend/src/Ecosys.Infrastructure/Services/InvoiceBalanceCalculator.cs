using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

/// <summary>
/// Single source of truth for invoice payment state and client running balances.
/// An invoice's paid amount is the sum of its <b>active</b> <see cref="PaymentAllocation"/> rows
/// (from both payments and applied credit notes). All flows that change payments, allocations or
/// credit-note applications must route through here so balances and statuses stay consistent.
/// </summary>
public static class InvoiceBalanceCalculator
{
    /// <summary>
    /// Recomputes <see cref="TenantInvoice.AmountPaid"/>, <see cref="TenantInvoice.Balance"/> and
    /// <see cref="TenantInvoice.Status"/> from active allocations. Draft and Void invoices keep
    /// their status. The invoice is mutated in place; the caller owns SaveChanges.
    /// </summary>
    public static async Task RecalcInvoiceAsync(AppDbContext db, TenantInvoice invoice, CancellationToken ct)
    {
        var allocated = await db.PaymentAllocations
            .Where(a => a.InvoiceId == invoice.Id && a.Status == "Active")
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        invoice.AmountPaid = allocated;
        invoice.Balance = invoice.TotalAmount - allocated;

        if (invoice.Status is "Draft" or "Void")
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
    /// Recomputes a client's running balance as the sum of open (non-void) invoice balances.
    /// Mutates the client in place; the caller owns SaveChanges.
    /// </summary>
    public static async Task RecalcClientBalanceAsync(AppDbContext db, Client client, CancellationToken ct)
    {
        var outstanding = await db.TenantInvoices
            .Where(i => i.ClientId == client.Id && i.Status != "Void")
            .SumAsync(i => (decimal?)i.Balance, ct) ?? 0m;

        client.CurrentBalance = outstanding;
    }

    /// <summary>
    /// Creates active allocation rows for a money source (Payment or CreditNote) against the given
    /// pre-validated invoices, then recomputes each invoice. Caller owns client-balance recompute
    /// and the surrounding validation (ownership, currency, caps).
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
                Status = "Active",
            });
        }

        await db.SaveChangesAsync(ct);

        foreach (var (invoice, _) in items)
            await RecalcInvoiceAsync(db, invoice, ct);
    }

    /// <summary>
    /// Reverses all active allocations for a money source (e.g. when a payment is reversed or a
    /// credit note voided), recomputes each affected invoice, and returns the invoices touched so
    /// the caller can recompute client balances. Caller owns SaveChanges.
    /// </summary>
    public static async Task<IReadOnlyList<TenantInvoice>> ReverseAllocationsForSourceAsync(
        AppDbContext db, Guid tenantId, string sourceType, Guid sourceId, CancellationToken ct)
    {
        var allocations = await db.PaymentAllocations
            .Where(a => a.TenantId == tenantId && a.SourceType == sourceType && a.SourceId == sourceId && a.Status == "Active")
            .ToListAsync(ct);

        if (allocations.Count == 0)
            return [];

        foreach (var allocation in allocations)
            allocation.Status = "Reversed";
        await db.SaveChangesAsync(ct);

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
            .Where(a => a.TenantId == tenantId && a.SourceType == AllocationSourceTypes.Payment && a.Status == "Active")
            .Join(db.TenantPayments.Where(p => p.ClientId == clientId && p.Status == "Confirmed"),
                a => a.SourceId, p => p.Id, (a, _) => a.Amount)
            .SumAsync(x => (decimal?)x, ct) ?? 0m;

        var creditNoteBalance = await db.CreditNotes
            .Where(c => c.TenantId == tenantId && c.ClientId == clientId
                        && (c.Status == "Issued" || c.Status == "PartiallyApplied"))
            .SumAsync(c => (decimal?)c.Balance, ct) ?? 0m;

        return (confirmedPayments - paymentAllocated) + creditNoteBalance;
    }
}
