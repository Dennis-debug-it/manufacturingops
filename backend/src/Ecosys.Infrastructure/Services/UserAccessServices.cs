using System.Data;
using System.Data.Common;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

public sealed record UserPermissionsModel
{
    public bool CanViewWorkOrders { get; init; }
    public bool CanCreateWorkOrders { get; init; }
    public bool CanAssignWorkOrders { get; init; }
    public bool CanCompleteWorkOrders { get; init; }
    public bool CanViewKnowledge { get; init; }
    public bool CanManageKnowledge { get; init; }
    public bool CanViewMaterials { get; init; }
    public bool CanRequestMaterials { get; init; }
    public bool CanManageMaterials { get; init; }
    public bool CanApproveMaterials { get; init; }
    public bool CanIssueMaterials { get; init; }
    public bool CanManageAssets { get; init; }
    public bool CanManageSettings { get; init; }
    public bool CanViewReports { get; init; }
    public bool CanViewFinance { get; init; }
    public bool CanManageFinance { get; init; }
    public bool CanViewFinanceRates { get; init; }
    public bool CanManageFinanceRates { get; init; }
    public bool CanViewFinanceApprovals { get; init; }
    public bool CanManageFinanceApprovals { get; init; }
    public bool CanViewBillableWork { get; init; }
    public bool CanManageBillableWork { get; init; }
    public bool CanOverrideMaterialStock { get; init; }
    /// <summary>Granted <see cref="PermissionNames.CapabilityOnly"/> capabilities.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public bool CanViewCrm { get; init; }
    public bool CanManageCrm { get; init; }
}

public interface IUserPermissionTemplateService
{
    UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle);
    UserPermissionsModel GetFullTenantPermissions();
    UserPermissionsModel GetSuperAdminPermissions();
    UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions);
}

public interface IUserAccessService
{
    void EnsureAdmin();
    void EnsureAdminOrPermission(string permissionName);
    void EnsureAdminOrAnyPermission(params string[] permissionNames);
    void EnsureTenantOperationalAccess();
}

internal sealed class UserPermissionTemplateService : IUserPermissionTemplateService
{
    public UserPermissionsModel GetDefaultPermissions(string role, string? jobTitle)
    {
        if (string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return GetSuperAdminPermissions();
        }

        if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return GetFullTenantPermissions();
        }

        return (jobTitle ?? string.Empty).Trim() switch
        {
            "Technician" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanCompleteWorkOrders = true,
                CanViewKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true
            },
            "Supervisor" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanCreateWorkOrders = true,
                CanAssignWorkOrders = true,
                CanCompleteWorkOrders = true,
                CanViewKnowledge = true,
                CanManageKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true,
                CanViewReports = true
            },
            "Store Manager" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanViewKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true,
                CanManageMaterials = true,
                CanApproveMaterials = true,
                CanIssueMaterials = true,
                CanViewReports = true
            },
            "Finance Manager" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanViewKnowledge = true,
                CanViewReports = true,
                CanViewFinance = true,
                CanManageFinance = true,
                CanViewFinanceRates = true,
                CanManageFinanceRates = true,
                CanViewFinanceApprovals = true,
                CanManageFinanceApprovals = true,
                CanViewBillableWork = true,
                CanManageBillableWork = true
            },
            "Helpdesk Officer" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanCreateWorkOrders = true,
                CanViewKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true
            },
            "Manager" => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanViewKnowledge = true,
                CanManageKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true,
                CanViewReports = true,
                CanViewFinance = true,
                CanViewFinanceRates = true,
                CanViewFinanceApprovals = true,
                CanViewBillableWork = true
            },
            _ => new UserPermissionsModel
            {
                CanViewWorkOrders = true,
                CanViewKnowledge = true,
                CanViewMaterials = true,
                CanRequestMaterials = true
            }
        };
    }

    public UserPermissionsModel GetFullTenantPermissions() =>
        new()
        {
            CanViewWorkOrders = true,
            CanCreateWorkOrders = true,
            CanAssignWorkOrders = true,
            CanCompleteWorkOrders = true,
            CanViewKnowledge = true,
            CanManageKnowledge = true,
            CanViewMaterials = true,
            CanRequestMaterials = true,
            CanManageMaterials = true,
            CanApproveMaterials = true,
            CanIssueMaterials = true,
            CanManageAssets = true,
            CanManageSettings = true,
            CanViewReports = true,
            CanViewFinance = true,
            CanManageFinance = true,
            CanViewFinanceRates = true,
            CanManageFinanceRates = true,
            CanViewFinanceApprovals = true,
            CanManageFinanceApprovals = true,
            CanViewBillableWork = true,
            CanManageBillableWork = true,
            CanOverrideMaterialStock = true,
            Capabilities = PermissionNames.CapabilityOnly,
            CanViewCrm = true,
            CanManageCrm = true
        };

    public UserPermissionsModel GetSuperAdminPermissions() => new();

    public UserPermissionsModel Merge(string role, string? jobTitle, UserPermissionsModel? customPermissions)
    {
        if (string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return GetFullTenantPermissions();
        }

        if (string.Equals(role, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return GetSuperAdminPermissions();
        }

        return customPermissions ?? GetDefaultPermissions(role, jobTitle);
    }
}

internal sealed class UserAccessService(ITenantContext tenantContext) : IUserAccessService
{
    public void EnsureAdmin()
    {
        EnsureTenantOperationalAccess();

        if (!tenantContext.IsAdmin)
        {
            throw new ForbiddenException("Admin access is required for this action.");
        }
    }

    public void EnsureAdminOrPermission(string permissionName)
    {
        EnsureTenantOperationalAccess();

        if (tenantContext.IsAdmin)
        {
            return;
        }

        if (!tenantContext.HasPermission(permissionName))
        {
            throw new ForbiddenException("You do not have permission to perform this action.");
        }
    }

    public void EnsureAdminOrAnyPermission(params string[] permissionNames)
    {
        EnsureTenantOperationalAccess();

        if (tenantContext.IsAdmin)
        {
            return;
        }

        if (permissionNames.Any(tenantContext.HasPermission))
        {
            return;
        }

        throw new ForbiddenException("You do not have permission to perform this action.");
    }

    public void EnsureTenantOperationalAccess()
    {
        // A client-portal token carries a tenant_id claim so portal queries can be scoped to that
        // tenant. That made every tenant-internal endpoint resolve a tenant for a portal caller and
        // return the whole tenant's rows (EC-001). The real boundary is the TenantStaff policy on
        // TenantAwareControllerBase; this is the defence-in-depth copy for anything that reaches a
        // tenant service without passing an MVC authorization filter.
        if (tenantContext.IsPortalClient)
        {
            throw new ForbiddenException("Client portal accounts cannot access tenant operational endpoints.");
        }

        if (tenantContext.IsSuperAdmin)
        {
            throw new ForbiddenException("SuperAdmin accounts cannot access tenant operational endpoints.");
        }
    }
}

public interface IStockLedgerService
{
    Task<StockMovement> RecordReceiptAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default);
    Task<StockMovement> RecordAdjustmentAsync(MaterialItem material, Guid? branchId, decimal quantityChange, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default);
    Task<StockMovement> RecordIssueAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, bool allowNegativeStock = false, CancellationToken cancellationToken = default);
    Task<StockMovement> RecordReturnAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default);
    Task<StockMovement> RecordTransferOutAsync(MaterialItem material, Guid fromBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default);
    Task<StockMovement> RecordTransferInAsync(MaterialItem material, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default);

    /// <summary>Load stock from a branch onto a van: branch issue + van receipt (tenant total unchanged).</summary>
    Task RecordVanLoadAsync(MaterialItem material, Guid fromBranchId, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default);
    /// <summary>Return stock from a van back to a branch: van issue + branch receipt (tenant total unchanged).</summary>
    Task RecordVanReturnAsync(MaterialItem material, Guid vanId, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default);
    /// <summary>Consume stock from a van (used on a job): decrements van balance and tenant total.</summary>
    Task<StockMovement> RecordVanConsumeAsync(MaterialItem material, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one stock mutation — issue, transfer, receipt, van move, material request fulfilment —
    /// as a single atomic unit. Every Record* method above reads a balance, checks the negative-stock
    /// guard against that read and writes the new balance back; the caller then saves. Without one
    /// transaction around all of it a second caller reads the same balance between the guard and the
    /// write, both pass, one update is lost, the part is oversold and the immutable
    /// <see cref="StockMovement.BalanceAfter"/> ledger records a figure that was never true. Wrap
    /// every stock-mutating action body in this (EC-P1-05).
    /// </summary>
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when a stock balance could not be written because a concurrent caller changed it first.
/// Maps to HTTP 409 with the same <c>CONCURRENCY_CONFLICT</c> error code the global handler already
/// uses for <see cref="DbUpdateConcurrencyException"/>, so a client sees one consistent "nothing was
/// saved, send it again" signal whichever layer detected the collision (EC-P1-05).
/// </summary>
public sealed class StockContentionException(string message)
    : AppException(message), ILocalizedErrorCode
{
    public override int StatusCode => 409;
    public string ErrorCode => "CONCURRENCY_CONFLICT";
}

internal sealed class StockLedgerService(AppDbContext dbContext) : IStockLedgerService
{
    private static readonly HashSet<string> AllowedMovementTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Receipt",
        "Issue",
        "Return",
        "Adjustment",
        "TransferOut",
        "TransferIn",
        "VanLoad",
        "VanReturn",
        "VanConsume"
    };

    /// <summary>PostgreSQL <c>serialization_failure</c>.</summary>
    private const string SerializationFailure = "40001";

    /// <summary>PostgreSQL <c>deadlock_detected</c>.</summary>
    private const string DeadlockDetected = "40P01";

    /// <summary>
    /// Serializable is the isolation the guard actually needs: the check is "does the balance I just
    /// read still allow this movement?", and only serializable stops another transaction committing a
    /// balance change in between. The <c>xmin</c> tokens on MaterialItem, BranchMaterialStock and
    /// VanMaterialStock catch the single-row collision on their own; the transaction is what extends
    /// that guarantee across the several rows one movement touches — a transfer writes two branch
    /// rows, a van load writes a branch row and a van row — and across actions that save more than
    /// once, so a half-applied movement can never reach the ledger.
    /// <para>
    /// Deliberately no retry, unlike <see cref="DocumentNumberingService"/>. That service replays a
    /// counter it owns end to end; here the delegate is a controller action holding entities loaded
    /// before the transaction opened and an aggregate already staged on a request-scoped context, so
    /// replaying it would re-apply stale in-memory state that the rollback has just discarded. A
    /// stock movement is also a business document, not an idempotent reservation: silently repeating
    /// it is worse than declining it. The client gets 409 <c>CONCURRENCY_CONFLICT</c> and re-sends
    /// the whole request against fresh reads.
    /// </para>
    /// </summary>
    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
    {
        // Providers without transaction support (the in-memory provider used by tests) cannot begin
        // one, and there is no concurrency to serialise there either. An ambient transaction means an
        // outer caller already owns the unit of work — leave the boundary where they put it.
        var useTransaction = dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null;
        var transaction = useTransaction
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            await operation(cancellationToken);

            // Callers stage the aggregate and its stock movements together and save once inside the
            // delegate; this is a no-op when they did and the safety net when they did not.
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (IsConcurrencyConflict(ex))
        {
            throw new StockContentionException(
                "Another user changed this stock balance at the same moment. Nothing was saved — please reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// True when the exception chain carries an optimistic-concurrency miss (the <c>xmin</c> token
    /// moved) or a PostgreSQL serialization failure / deadlock — all three mean "this transaction
    /// lost a race and nothing it did was applied".
    /// </summary>
    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is DbException dbException
                && dbException.SqlState is SerializationFailure or DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    public Task<StockMovement> RecordReceiptAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default) =>
        RecordAsync(material, branchId, "Receipt", quantity, createdByUserId, reason, referenceNumber, workOrderId, materialRequestId, false, cancellationToken);

    public async Task<StockMovement> RecordAdjustmentAsync(MaterialItem material, Guid? branchId, decimal quantityChange, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default)
    {
        if (quantityChange == 0)
        {
            throw new BusinessRuleException("Adjustment quantity change cannot be zero.");
        }

        var allowNegativeStock = await TenantAllowsNegativeStockAsync(material.TenantId, cancellationToken);
        return await RecordAsync(material, branchId, "Adjustment", quantityChange, createdByUserId, reason, referenceNumber, workOrderId, materialRequestId, allowNegativeStock, cancellationToken);
    }

    /// <summary>
    /// Stock leaving for use — a job, an issue, a request. The tenant's "Allow Negative Stock" setting
    /// (Settings → Inventory) lets the balance go below zero here, as does a caller passing
    /// <paramref name="allowNegativeStock"/>.
    /// </summary>
    public async Task<StockMovement> RecordIssueAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, bool allowNegativeStock = false, CancellationToken cancellationToken = default)
    {
        allowNegativeStock = allowNegativeStock || await TenantAllowsNegativeStockAsync(material.TenantId, cancellationToken);
        return await IssueCoreAsync(material, branchId, quantity, createdByUserId, reason, referenceNumber, workOrderId, materialRequestId, allowNegativeStock, cancellationToken);
    }

    private readonly Dictionary<Guid, bool> negativeStockByTenant = [];

    private async Task<bool> TenantAllowsNegativeStockAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!negativeStockByTenant.TryGetValue(tenantId, out var allowed))
        {
            allowed = (await InventoryModuleSettings.LoadAsync(dbContext, tenantId, cancellationToken)).AllowNegativeStock;
            negativeStockByTenant[tenantId] = allowed;
        }

        return allowed;
    }

    /// <summary>
    /// The issue itself. Moves between locations (transfers, van loads) call this strictly: shipping
    /// stock a location does not hold would conjure it at the other end.
    /// </summary>
    private async Task<StockMovement> IssueCoreAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber, Guid? workOrderId, Guid? materialRequestId, bool allowNegativeStock, CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Issue quantity must be greater than zero.");
        }

        var stock = await EnsureStockAsync(material, branchId, cancellationToken);
        if (!allowNegativeStock && stock.QuantityOnHand < quantity)
        {
            throw new BusinessRuleException($"Insufficient stock for material '{material.ItemName}'.");
        }

        return await RecordAsync(material, branchId, "Issue", -quantity, createdByUserId, reason, referenceNumber, workOrderId, materialRequestId, allowNegativeStock, cancellationToken);
    }

    public Task<StockMovement> RecordReturnAsync(MaterialItem material, Guid? branchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, Guid? materialRequestId = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Return quantity must be greater than zero.");
        }

        return RecordAsync(material, branchId, "Return", quantity, createdByUserId, reason, referenceNumber, workOrderId, materialRequestId, false, cancellationToken);
    }

    public async Task<StockMovement> RecordTransferOutAsync(MaterialItem material, Guid fromBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default)
    {
        var movement = await IssueCoreAsync(material, fromBranchId, quantity, createdByUserId, reason, referenceNumber, null, null, false, cancellationToken);
        movement.MovementType = "TransferOut";
        return movement;
    }

    public async Task<StockMovement> RecordTransferInAsync(MaterialItem material, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default)
    {
        var movement = await RecordReceiptAsync(material, toBranchId, quantity, createdByUserId, reason, referenceNumber, cancellationToken: cancellationToken);
        movement.MovementType = "TransferIn";
        return movement;
    }

    public async Task RecordVanLoadAsync(MaterialItem material, Guid fromBranchId, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Load quantity must be greater than zero.");
        }

        // Branch side: issue from the source branch (validates branch stock, decrements branch + tenant total).
        // Typed as a van load, not an issue: the stock moved to a van, it was not used.
        var branchSide = await IssueCoreAsync(material, fromBranchId, quantity, createdByUserId, reason, referenceNumber, null, null, false, cancellationToken);
        branchSide.MovementType = "VanLoad";
        // Van side: receipt into the van (increments van + tenant total). Net tenant total change is zero.
        await RecordVanMovementAsync(material, vanId, "VanLoad", quantity, createdByUserId, reason, referenceNumber, null, false, cancellationToken);
    }

    public async Task RecordVanReturnAsync(MaterialItem material, Guid vanId, Guid toBranchId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Return quantity must be greater than zero.");
        }

        // Van side: issue from the van (validates van stock, decrements van + tenant total).
        await RecordVanMovementAsync(material, vanId, "VanReturn", -quantity, createdByUserId, reason, referenceNumber, null, false, cancellationToken);
        // Branch side: receipt back into the branch (increments branch + tenant total). Net tenant total change is zero.
        // Typed as a van return, not a receipt: nothing was bought.
        var branchSide = await RecordReceiptAsync(material, toBranchId, quantity, createdByUserId, reason, referenceNumber, cancellationToken: cancellationToken);
        branchSide.MovementType = "VanReturn";
    }

    public async Task<StockMovement> RecordVanConsumeAsync(MaterialItem material, Guid vanId, decimal quantity, Guid? createdByUserId, string? reason, string? referenceNumber = null, Guid? workOrderId = null, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("Consume quantity must be greater than zero.");
        }

        // Stock leaves the system entirely: decrement van balance and tenant total.
        var allowNegativeStock = await TenantAllowsNegativeStockAsync(material.TenantId, cancellationToken);
        return await RecordVanMovementAsync(material, vanId, "VanConsume", -quantity, createdByUserId, reason, referenceNumber, workOrderId, allowNegativeStock, cancellationToken);
    }

    private async Task<StockMovement> RecordVanMovementAsync(
        MaterialItem material,
        Guid vanId,
        string movementType,
        decimal quantityDelta,
        Guid? createdByUserId,
        string? reason,
        string? referenceNumber,
        Guid? workOrderId,
        bool allowNegativeStock,
        CancellationToken cancellationToken)
    {
        if (quantityDelta == 0)
        {
            throw new BusinessRuleException("Quantity change cannot be zero.");
        }

        var stock = await EnsureVanStockAsync(material, vanId, cancellationToken);
        var nextBalance = stock.QuantityOnHand + quantityDelta;
        if (!allowNegativeStock && nextBalance < 0)
        {
            throw new BusinessRuleException($"Insufficient stock on the van for material '{material.ItemName}'.");
        }

        stock.QuantityOnHand = nextBalance;
        if (material.UnitCost.HasValue && !stock.UnitCost.HasValue)
        {
            stock.UnitCost = material.UnitCost;
        }

        material.QuantityOnHand += quantityDelta;
        if (!allowNegativeStock && material.QuantityOnHand < 0)
        {
            throw new BusinessRuleException("Stock update would reduce stock below zero.");
        }

        var movement = new StockMovement
        {
            TenantId = material.TenantId,
            VanId = vanId,
            MaterialId = material.Id,
            WorkOrderId = workOrderId,
            MovementType = movementType,
            Quantity = Math.Abs(quantityDelta),
            QuantityChange = quantityDelta,
            BalanceAfter = stock.QuantityOnHand,
            Reason = reason?.Trim(),
            ReferenceNumber = referenceNumber?.Trim(),
            CreatedByUserId = createdByUserId
        };

        dbContext.StockMovements.Add(movement);
        return movement;
    }

    private async Task<VanMaterialStock> EnsureVanStockAsync(MaterialItem material, Guid vanId, CancellationToken cancellationToken)
    {
        var stock = await dbContext.VanMaterialStocks.SingleOrDefaultAsync(
            x => x.TenantId == material.TenantId && x.VanId == vanId && x.MaterialId == material.Id,
            cancellationToken);

        if (stock is null)
        {
            stock = new VanMaterialStock
            {
                TenantId = material.TenantId,
                VanId = vanId,
                MaterialId = material.Id,
                QuantityOnHand = 0,
                ReorderLevel = material.ReorderLevel,
                UnitCost = material.UnitCost
            };
            dbContext.VanMaterialStocks.Add(stock);
        }

        return stock;
    }

    private async Task<StockMovement> RecordAsync(
        MaterialItem material,
        Guid? branchId,
        string movementType,
        decimal quantityDelta,
        Guid? createdByUserId,
        string? reason,
        string? referenceNumber,
        Guid? workOrderId,
        Guid? materialRequestId,
        bool allowNegativeStock,
        CancellationToken cancellationToken)
    {
        if (!AllowedMovementTypes.Contains(movementType))
        {
            throw new BusinessRuleException("Invalid stock movement type.");
        }

        if (quantityDelta == 0)
        {
            throw new BusinessRuleException("Quantity change cannot be zero.");
        }

        decimal balanceAfter;
        if (branchId.HasValue)
        {
            var stock = await EnsureStockAsync(material, branchId, cancellationToken);
            var nextBalance = stock.QuantityOnHand + quantityDelta;
            if (!allowNegativeStock && nextBalance < 0)
            {
                throw new BusinessRuleException("Stock update would reduce stock below zero.");
            }

            stock.QuantityOnHand = nextBalance;
            if (material.UnitCost.HasValue && !stock.UnitCost.HasValue)
            {
                stock.UnitCost = material.UnitCost;
            }

            balanceAfter = stock.QuantityOnHand;
        }
        else
        {
            var nextBalance = material.QuantityOnHand + quantityDelta;
            if (!allowNegativeStock && nextBalance < 0)
            {
                throw new BusinessRuleException("Stock update would reduce stock below zero.");
            }

            balanceAfter = nextBalance;
        }

        material.QuantityOnHand += quantityDelta;
        if (!allowNegativeStock && material.QuantityOnHand < 0)
        {
            throw new BusinessRuleException("Stock update would reduce stock below zero.");
        }

        var movement = new StockMovement
        {
            TenantId = material.TenantId,
            BranchId = branchId,
            MaterialId = material.Id,
            WorkOrderId = workOrderId,
            MaterialRequestId = materialRequestId,
            MovementType = movementType,
            Quantity = Math.Abs(quantityDelta),
            QuantityChange = quantityDelta,
            BalanceAfter = branchId.HasValue ? balanceAfter : material.QuantityOnHand,
            Reason = reason?.Trim(),
            ReferenceNumber = referenceNumber?.Trim(),
            CreatedByUserId = createdByUserId
        };

        dbContext.StockMovements.Add(movement);
        return movement;
    }

    private async Task<BranchMaterialStock> EnsureStockAsync(MaterialItem material, Guid? branchId, CancellationToken cancellationToken)
    {
        if (!branchId.HasValue)
        {
            return new BranchMaterialStock
            {
                TenantId = material.TenantId,
                MaterialId = material.Id,
                QuantityOnHand = material.QuantityOnHand,
                ReorderLevel = material.ReorderLevel,
                UnitCost = material.UnitCost
            };
        }

        var stock = await dbContext.BranchMaterialStocks.SingleOrDefaultAsync(
            x => x.TenantId == material.TenantId && x.BranchId == branchId.Value && x.MaterialId == material.Id,
            cancellationToken);

        if (stock is null)
        {
            stock = new BranchMaterialStock
            {
                TenantId = material.TenantId,
                BranchId = branchId.Value,
                MaterialId = material.Id,
                QuantityOnHand = 0,
                ReorderLevel = material.ReorderLevel,
                UnitCost = material.UnitCost
            };
            dbContext.BranchMaterialStocks.Add(stock);
        }

        return stock;
    }
}
