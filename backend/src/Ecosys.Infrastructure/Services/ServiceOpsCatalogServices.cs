using Ecosys.Infrastructure.Persistence;
using Ecosys.ServiceOps.Contracts.Common;
using Ecosys.ServiceOps.Entities;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Infrastructure.Services;

internal sealed class CustomerService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext) : ICustomerService
{
    public async Task<IReadOnlyCollection<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();

        return await dbContext.Customers
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new CustomerResponse
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Email = x.Email,
                Phone = x.Phone,
                Address = x.Address,
                ContactPerson = x.ContactPerson
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerResponse> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        var code = request.Code.Trim().ToUpperInvariant();

        if (await dbContext.Customers.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new BusinessRuleException($"Customer code '{code}' already exists.");
        }

        var customer = new Customer
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Code = code,
            Email = ServiceOpsSupport.Normalize(request.Email),
            Phone = ServiceOpsSupport.Normalize(request.Phone),
            Address = ServiceOpsSupport.Normalize(request.Address),
            ContactPerson = ServiceOpsSupport.Normalize(request.ContactPerson)
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerResponse
        {
            Id = customer.Id,
            Name = customer.Name,
            Code = customer.Code,
            Email = customer.Email,
            Phone = customer.Phone,
            Address = customer.Address,
            ContactPerson = customer.ContactPerson
        };
    }
}

internal sealed class LocationService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext) : ILocationService
{
    public async Task<IReadOnlyCollection<LocationResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new LocationResponse
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                ParentLocationId = x.ParentLocationId,
                Name = x.Name,
                Code = x.Code
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationResponse> CreateAsync(LocationRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await ServiceOpsSupport.EnsureExistsAsync(dbContext.Customers, request.CustomerId, tenantId, "Customer", cancellationToken);

        if (request.ParentLocationId.HasValue)
        {
            await ServiceOpsSupport.EnsureExistsAsync(dbContext.Locations, request.ParentLocationId.Value, tenantId, "Parent location", cancellationToken);
        }

        var location = new Location
        {
            TenantId = tenantId,
            CustomerId = request.CustomerId,
            ParentLocationId = request.ParentLocationId,
            Name = request.Name.Trim(),
            Code = ServiceOpsSupport.Normalize(request.Code)
        };

        dbContext.Locations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new LocationResponse
        {
            Id = location.Id,
            CustomerId = location.CustomerId,
            ParentLocationId = location.ParentLocationId,
            Name = location.Name,
            Code = location.Code
        };
    }
}

internal sealed class AssetService(
    EcosysDbContext dbContext,
    ITenantContext tenantContext) : IAssetService
{
    public async Task<IReadOnlyCollection<AssetResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        return await dbContext.Assets
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => new AssetResponse
            {
                Id = x.Id,
                CustomerId = x.CustomerId,
                LocationId = x.LocationId,
                Name = x.Name,
                Code = x.Code,
                SerialNumber = x.SerialNumber,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AssetResponse> CreateAsync(AssetRequest request, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.GetRequiredTenantId();
        await ServiceOpsSupport.EnsureExistsAsync(dbContext.Customers, request.CustomerId, tenantId, "Customer", cancellationToken);
        await ServiceOpsSupport.EnsureExistsAsync(dbContext.Locations, request.LocationId, tenantId, "Location", cancellationToken);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await dbContext.Assets.AnyAsync(x => x.Code == code, cancellationToken))
        {
            throw new BusinessRuleException($"Asset code '{code}' already exists.");
        }

        var asset = new Asset
        {
            TenantId = tenantId,
            CustomerId = request.CustomerId,
            LocationId = request.LocationId,
            Name = request.Name.Trim(),
            Code = code,
            SerialNumber = ServiceOpsSupport.Normalize(request.SerialNumber),
            IsActive = request.IsActive
        };

        dbContext.Assets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AssetResponse
        {
            Id = asset.Id,
            CustomerId = asset.CustomerId,
            LocationId = asset.LocationId,
            Name = asset.Name,
            Code = asset.Code,
            SerialNumber = asset.SerialNumber,
            IsActive = asset.IsActive
        };
    }
}
