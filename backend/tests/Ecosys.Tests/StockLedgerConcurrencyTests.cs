using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// EC-P1-05 regression cover. The defect was a bare read-modify-write: <c>RecordAsync</c> read
/// <c>QuantityOnHand</c>, checked the negative-stock guard against that read and wrote the new
/// balance back, with the caller saving later — so two concurrent issues of the same part both
/// passed the guard, one update was lost and the immutable <c>stock_movements.BalanceAfter</c>
/// ledger recorded a figure that was never true. The fix is the <c>xmin</c> tokens on the three
/// stock entities plus <see cref="IStockLedgerService.ExecuteAsync"/>, which puts the guard, the
/// write and the caller's save inside one serializable transaction. These tests cover the parts
/// that are observable without a live PostgreSQL: the unit-of-work boundary and the 409 the
/// service raises when the race is detected.
/// </summary>
public sealed class StockLedgerConcurrencyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task ExecuteAsync_PersistsTheMovementAndTheBalanceTogether()
    {
        var databaseName = $"ecosys-stock-{Guid.NewGuid():N}";
        var branchId = Guid.NewGuid();
        Guid materialId;

        await using (var db = CreateDbContext(databaseName))
        {
            var material = SeedMaterialWithBranchStock(db, branchId, 10m);
            materialId = material.Id;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var ledger = new StockLedgerService(db);
            var material = await db.MaterialItems.SingleAsync(x => x.Id == materialId);

            // The delegate deliberately does not save: ExecuteAsync owns the unit of work, so the
            // guard's read and the balance it wrote back reach the database as one commit.
            await ledger.ExecuteAsync(ct =>
                ledger.RecordIssueAsync(material, branchId, 4m, null, "Issued on a job", "WO-1", cancellationToken: ct));
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var stock = await db.BranchMaterialStocks.SingleAsync(x => x.MaterialId == materialId && x.BranchId == branchId);
            var movement = await db.StockMovements.SingleAsync(x => x.MaterialId == materialId);

            Assert.Equal(6m, stock.QuantityOnHand);
            Assert.Equal(6m, await db.MaterialItems.Where(x => x.Id == materialId).Select(x => x.QuantityOnHand).SingleAsync());
            Assert.Equal(6m, movement.BalanceAfter);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RejectsAnIssueThatWouldOversellTheBranch()
    {
        var databaseName = $"ecosys-stock-{Guid.NewGuid():N}";
        var branchId = Guid.NewGuid();
        Guid materialId;

        await using (var db = CreateDbContext(databaseName))
        {
            var material = SeedMaterialWithBranchStock(db, branchId, 3m);
            materialId = material.Id;
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext(databaseName))
        {
            var ledger = new StockLedgerService(db);
            var material = await db.MaterialItems.SingleAsync(x => x.Id == materialId);

            // A guard failure is the caller's doing, not a race: it must stay a 400 business rule
            // violation rather than being swallowed by the concurrency translation below.
            await Assert.ThrowsAsync<BusinessRuleException>(() => ledger.ExecuteAsync(ct =>
                ledger.RecordIssueAsync(material, branchId, 5m, null, "Issued on a job", "WO-1", cancellationToken: ct)));
        }

        await using (var db = CreateDbContext(databaseName))
        {
            Assert.False(await db.StockMovements.AnyAsync(x => x.MaterialId == materialId));
            Assert.Equal(3m, await db.BranchMaterialStocks
                .Where(x => x.MaterialId == materialId && x.BranchId == branchId)
                .Select(x => x.QuantityOnHand)
                .SingleAsync());
        }
    }

    [Fact]
    public async Task ExecuteAsync_TranslatesALostConcurrencyRaceIntoA409()
    {
        await using var db = CreateDbContext($"ecosys-stock-{Guid.NewGuid():N}");
        var ledger = new StockLedgerService(db);

        // What PostgreSQL raises when the xmin token on the stock row has moved under us: the whole
        // attempt rolled back, so the caller is told to reload and send the request again.
        var contention = await Assert.ThrowsAsync<StockContentionException>(() => ledger.ExecuteAsync(
            _ => throw new DbUpdateConcurrencyException("The stock row was changed by someone else.")));

        Assert.Equal(409, contention.StatusCode);
        Assert.Equal("CONCURRENCY_CONFLICT", contention.ErrorCode);
    }

    private static AppDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    private static MaterialItem SeedMaterialWithBranchStock(AppDbContext db, Guid branchId, decimal quantityOnHand)
    {
        var material = new MaterialItem
        {
            TenantId = TenantId,
            ItemCode = $"PRT-{Guid.NewGuid():N}"[..12],
            ItemName = "Drive Belt",
            UnitOfMeasure = "Piece",
            QuantityOnHand = quantityOnHand,
            ReorderLevel = 2m,
            UnitCost = 30m,
            IsActive = true,
        };

        db.MaterialItems.Add(material);
        db.BranchMaterialStocks.Add(new BranchMaterialStock
        {
            TenantId = TenantId,
            BranchId = branchId,
            MaterialId = material.Id,
            QuantityOnHand = quantityOnHand,
            ReorderLevel = 2m,
            UnitCost = 30m,
        });

        return material;
    }
}
