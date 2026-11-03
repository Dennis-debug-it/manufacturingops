using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-012 regression cover. The defect was an ordering trap: allocations were saved, invoice
/// balances were recomputed in memory only, and the client running balance was then computed with a
/// database SUM that could not see them — so <c>clients.CurrentBalance</c> stayed at the pre-payment
/// figure forever. These tests assert the invariant that actually matters:
/// <c>client.CurrentBalance == SUM(open invoice balances)</c> after the operation is persisted.
/// </summary>
public sealed class InvoiceBalanceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task PaymentAllocation_LeavesClientBalanceEqualToSumOfOpenInvoiceBalances()
    {
        var databaseName = $"ecosys-balances-{Guid.NewGuid():N}";
        Guid clientId;
        Guid invoiceId;

        await using (var db = CreateDbContext(databaseName))
        {
            var (client, invoice) = SeedClientWithInvoice(db, 5_800m);
            clientId = client.Id;
            invoiceId = invoice.Id;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var client = await db.Clients.SingleAsync(x => x.Id == clientId);
            var invoice = await db.TenantInvoices.SingleAsync(x => x.Id == invoiceId);

            // Exactly the shape the payment endpoints use: allocate, recompute the client, save once.
            await InvoiceBalanceCalculator.ApplyAllocationsAsync(
                db, TenantId, AllocationSourceTypes.Payment, Guid.NewGuid(),
                new[] { (invoice, 2_000m) }, CancellationToken.None);
            await InvoiceBalanceCalculator.RecalcClientBalanceAsync(db, client, CancellationToken.None);

            // The running balance must already be right before the save, not just after it.
            Assert.Equal(3_800m, client.CurrentBalance);
            await db.SaveChangesAsync();
        }

        await using (var verify = CreateDbContext(databaseName))
        {
            var invoice = await verify.TenantInvoices.SingleAsync(x => x.Id == invoiceId);
            Assert.Equal(2_000m, invoice.AmountPaid);
            Assert.Equal(3_800m, invoice.Balance);
            Assert.Equal("PartiallyPaid", invoice.Status);

            var client = await verify.Clients.SingleAsync(x => x.Id == clientId);
            var openBalance = await verify.TenantInvoices
                .Where(x => x.ClientId == clientId && x.Status != "Void")
                .SumAsync(x => x.Balance);

            Assert.Equal(3_800m, client.CurrentBalance);
            Assert.Equal(openBalance, client.CurrentBalance);
        }
    }

    [Fact]
    public async Task PaymentThatSettlesInvoice_ZeroesClientBalanceAndMarksInvoicePaid()
    {
        var databaseName = $"ecosys-balances-{Guid.NewGuid():N}";
        Guid clientId;
        Guid invoiceId;

        await using (var db = CreateDbContext(databaseName))
        {
            var (client, invoice) = SeedClientWithInvoice(db, 5_800m);
            clientId = client.Id;
            invoiceId = invoice.Id;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var client = await db.Clients.SingleAsync(x => x.Id == clientId);
            var invoice = await db.TenantInvoices.SingleAsync(x => x.Id == invoiceId);

            await InvoiceBalanceCalculator.ApplyAllocationsAsync(
                db, TenantId, AllocationSourceTypes.Payment, Guid.NewGuid(),
                new[] { (invoice, 5_800m) }, CancellationToken.None);
            await InvoiceBalanceCalculator.RecalcClientBalanceAsync(db, client, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var verify = CreateDbContext(databaseName))
        {
            var invoice = await verify.TenantInvoices.SingleAsync(x => x.Id == invoiceId);
            Assert.Equal("Paid", invoice.Status);
            Assert.Equal(0m, invoice.Balance);
            Assert.Equal(0m, (await verify.Clients.SingleAsync(x => x.Id == clientId)).CurrentBalance);
        }
    }

    [Fact]
    public async Task ReversingAPayment_RestoresTheClientBalance()
    {
        var databaseName = $"ecosys-balances-{Guid.NewGuid():N}";
        var sourceId = Guid.NewGuid();
        Guid clientId;
        Guid invoiceId;

        await using (var db = CreateDbContext(databaseName))
        {
            var (client, invoice) = SeedClientWithInvoice(db, 5_800m);
            clientId = client.Id;
            invoiceId = invoice.Id;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var client = await db.Clients.SingleAsync(x => x.Id == clientId);
            var invoice = await db.TenantInvoices.SingleAsync(x => x.Id == invoiceId);
            await InvoiceBalanceCalculator.ApplyAllocationsAsync(
                db, TenantId, AllocationSourceTypes.Payment, sourceId,
                new[] { (invoice, 2_000m) }, CancellationToken.None);
            await InvoiceBalanceCalculator.RecalcClientBalanceAsync(db, client, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var affected = await InvoiceBalanceCalculator.ReverseAllocationsForSourceAsync(
                db, TenantId, AllocationSourceTypes.Payment, sourceId, CancellationToken.None);
            Assert.Single(affected);

            await InvoiceBalanceCalculator.RecalcClientBalancesAsync(
                db, TenantId, affected, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        await using (var verify = CreateDbContext(databaseName))
        {
            var invoice = await verify.TenantInvoices.SingleAsync(x => x.Id == invoiceId);
            Assert.Equal(0m, invoice.AmountPaid);
            Assert.Equal(5_800m, invoice.Balance);
            Assert.Equal(5_800m, (await verify.Clients.SingleAsync(x => x.Id == clientId)).CurrentBalance);
        }
    }

    [Fact]
    public async Task RecalcClientBalance_CountsAnInvoiceThatHasNotBeenSavedYet()
    {
        await using var db = CreateDbContext($"ecosys-balances-{Guid.NewGuid():N}");
        var (client, invoice) = SeedClientWithInvoice(db, 5_800m);
        await db.SaveChangesAsync();

        // Mirrors invoice creation: the new invoice is tracked but not yet persisted when the
        // client's running balance is recomputed.
        db.TenantInvoices.Add(NewInvoice(client.Id, 1_200m));
        await InvoiceBalanceCalculator.RecalcClientBalanceAsync(db, client, CancellationToken.None);
        Assert.Equal(7_000m, client.CurrentBalance);

        await db.SaveChangesAsync();

        var openBalance = await db.TenantInvoices
            .Where(x => x.ClientId == client.Id && x.Status != "Void")
            .SumAsync(x => x.Balance);
        Assert.Equal(openBalance, client.CurrentBalance);
        Assert.Equal(5_800m, invoice.Balance);
    }

    [Fact]
    public async Task VoidingAnInvoice_DropsItFromTheClientBalance()
    {
        await using var db = CreateDbContext($"ecosys-balances-{Guid.NewGuid():N}");
        var (client, invoice) = SeedClientWithInvoice(db, 5_800m);
        db.TenantInvoices.Add(NewInvoice(client.Id, 1_200m));
        client.CurrentBalance = 7_000m;
        await db.SaveChangesAsync();

        invoice.Status = "Void";
        await InvoiceBalanceCalculator.RecalcClientBalanceAsync(db, client, CancellationToken.None);
        Assert.Equal(1_200m, client.CurrentBalance);

        await db.SaveChangesAsync();

        var openBalance = await db.TenantInvoices
            .Where(x => x.ClientId == client.Id && x.Status != "Void")
            .SumAsync(x => x.Balance);
        Assert.Equal(openBalance, client.CurrentBalance);
    }

    private static AppDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    private static (Client Client, TenantInvoice Invoice) SeedClientWithInvoice(AppDbContext db, decimal total)
    {
        var client = new Client
        {
            TenantId = TenantId,
            ClientName = "Nairobi Hospital",
            Email = $"finance-{Guid.NewGuid():N}@hospital.test",
            IsActive = true,
            CurrentBalance = total,
        };

        var invoice = NewInvoice(client.Id, total);
        db.AddRange(client, invoice);
        return (client, invoice);
    }

    private static TenantInvoice NewInvoice(Guid clientId, decimal total) => new()
    {
        TenantId = TenantId,
        ClientId = clientId,
        InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
        DocumentType = "Invoice",
        Status = "Sent",
        InvoiceDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        Subtotal = total,
        TotalAmount = total,
        AmountPaid = 0m,
        Balance = total,
        Currency = "KES",
    };
}
