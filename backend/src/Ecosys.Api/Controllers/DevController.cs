using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Api.Controllers;

[ApiController]
[Authorize]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/dev")]
public sealed class DevController(
    AppDbContext dbContext,
    ITenantContext tenantContext,
    IWebHostEnvironment environment,
    IDocumentNumberingService documentNumberingService,
    IPreventiveMaintenancePlanService preventiveMaintenancePlanService,
    IStockLedgerService stockLedgerService,
    IUserAccessService userAccessService) : TenantAwareControllerBase(tenantContext)
{
    [HttpPost("seed")]
    public async Task<ActionResult<DevSeedResponse>> Seed(CancellationToken cancellationToken)
    {
        userAccessService.EnsureAdmin();

        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant was not found.");

        var clients = new List<Client>();
        if (!await dbContext.Clients.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            clients.AddRange(
            [
                new Client { TenantId = TenantId, ClientName = $"{tenant.CompanyName} HQ", ClientType = "Corporate", Email = tenant.Email, Phone = tenant.Phone, Location = "Main Campus", ContactPerson = "Operations Lead", SlaPlan = "Gold", IsActive = true },
                new Client { TenantId = TenantId, ClientName = $"{tenant.CompanyName} Branch", ClientType = "Branch", Email = tenant.Email, Phone = tenant.Phone, Location = "Branch Office", ContactPerson = "Branch Admin", SlaPlan = "Silver", IsActive = true }
            ]);
            dbContext.Clients.AddRange(clients);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            clients = await dbContext.Clients.Where(x => x.TenantId == TenantId && x.IsActive).Take(2).ToListAsync(cancellationToken);
        }

        var technicians = new List<Technician>();
        if (!await dbContext.Technicians.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            technicians.AddRange(
            [
                new Technician { TenantId = TenantId, FullName = "Alex Njoroge", Email = $"alex.{TenantId}@ecosys.local", Phone = "0700000001", SkillCategory = "Electrical", AssignmentGroup = "Field", IsActive = true },
                new Technician { TenantId = TenantId, FullName = "Mary Achieng", Email = $"mary.{TenantId}@ecosys.local", Phone = "0700000002", SkillCategory = "HVAC", AssignmentGroup = "Field", IsActive = true }
            ]);
            dbContext.Technicians.AddRange(technicians);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            technicians = await dbContext.Technicians.Where(x => x.TenantId == TenantId && x.IsActive).Take(2).ToListAsync(cancellationToken);
        }

        var assets = new List<Asset>();
        if (!await dbContext.Assets.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            assets.AddRange(
            [
                new Asset
                {
                    TenantId = TenantId,
                    ClientId = clients[0].Id,
                    AssetName = "Backup Generator",
                    AssetCode = $"GEN-{DateTime.UtcNow:MMddHHmm}",
                    AssetType = "Power",
                    Location = "Main Campus",
                    Manufacturer = "Caterpillar",
                    Model = "CAT-500",
                    InstallationDate = DateTime.UtcNow.AddYears(-1),
                    RecommendedPmFrequency = "Monthly",
                    AutoSchedulePm = true,
                    NextPmDate = DateTime.UtcNow.AddDays(14),
                    Status = "Active"
                },
                new Asset
                {
                    TenantId = TenantId,
                    ClientId = clients[Math.Min(1, clients.Count - 1)].Id,
                    AssetName = "HVAC Chiller",
                    AssetCode = $"HVAC-{DateTime.UtcNow:MMddHHmm}",
                    AssetType = "HVAC",
                    Location = "Branch Office",
                    Manufacturer = "Daikin",
                    Model = "DK-220",
                    InstallationDate = DateTime.UtcNow.AddMonths(-8),
                    RecommendedPmFrequency = "Quarterly",
                    AutoSchedulePm = true,
                    NextPmDate = DateTime.UtcNow.AddDays(21),
                    Status = "Active"
                }
            ]);
            dbContext.Assets.AddRange(assets);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var asset in assets)
            {
                await preventiveMaintenancePlanService.SyncForAssetAsync(asset, cancellationToken);
            }
        }
        else
        {
            assets = await dbContext.Assets.Where(x => x.TenantId == TenantId && x.Status != "Inactive").Take(2).ToListAsync(cancellationToken);
        }

        if (!await dbContext.MaterialItems.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            var materials = new[]
            {
                new MaterialItem { TenantId = TenantId, ItemCode = "FLT-001", ItemName = "Oil Filter", Category = "Consumables", UnitOfMeasure = "Piece", QuantityOnHand = 0, ReorderLevel = 5, UnitCost = 12.50m, IsActive = true },
                new MaterialItem { TenantId = TenantId, ItemCode = "BLT-002", ItemName = "Drive Belt", Category = "Spare Parts", UnitOfMeasure = "Piece", QuantityOnHand = 0, ReorderLevel = 5, UnitCost = 30m, IsActive = true }
            };
            dbContext.MaterialItems.AddRange(materials);
            await dbContext.SaveChangesAsync(cancellationToken);

            await stockLedgerService.RecordReceiptAsync(materials[0], null, 20, UserId, "Development seed stock", "DEV-SEED", cancellationToken: cancellationToken);
            await stockLedgerService.RecordReceiptAsync(materials[1], null, 4, UserId, "Development seed stock", "DEV-SEED", cancellationToken: cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!await dbContext.WorkOrders.AnyAsync(x => x.TenantId == TenantId, cancellationToken))
        {
            dbContext.WorkOrders.AddRange(
            [
                new WorkOrder
                {
                    TenantId = TenantId,
                    ClientId = clients[0].Id,
                    AssetId = assets[0].Id,
                    WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.WorkOrder, cancellationToken),
                    Title = "Inspect generator noise",
                    Description = "Investigate abnormal sound during startup.",
                    Priority = "High",
                    Status = "Assigned",
                    AssignedTechnicianId = technicians[0].Id,
                    DueDate = DateTime.UtcNow.AddDays(2)
                },
                new WorkOrder
                {
                    TenantId = TenantId,
                    ClientId = clients[Math.Min(1, clients.Count - 1)].Id,
                    AssetId = assets[Math.Min(1, assets.Count - 1)].Id,
                    WorkOrderNumber = await documentNumberingService.GenerateAsync(TenantId, null, DocumentTypes.WorkOrder, cancellationToken),
                    Title = "Quarterly HVAC preventive maintenance",
                    Description = "Standard PM checklist.",
                    Priority = "Medium",
                    Status = "Open",
                    DueDate = DateTime.UtcNow.AddDays(7),
                    IsPreventiveMaintenance = true
                }
            ]);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new DevSeedResponse("Seed data created or confirmed for the current tenant."));
    }
}

public sealed record DevSeedResponse(string Message);
