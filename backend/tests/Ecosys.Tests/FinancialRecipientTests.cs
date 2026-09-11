using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class FinancialRecipientTests
{
    [Fact]
    public async Task ResolveAsync_UsesTenantScopedBillingRecipients_AndExcludesDuplicatesFromCc()
    {
        await using var dbContext = CreateDbContext();
        var tenantA = NewTenant("Tenant A");
        var tenantB = NewTenant("Tenant B");
        var clientA = new Client { TenantId = tenantA.Id, ClientName = "Client A", Email = "generic-a@example.com" };
        var clientB = new Client { TenantId = tenantB.Id, ClientName = "Client B", Email = "generic-b@example.com" };
        dbContext.Tenants.AddRange(tenantA, tenantB);
        dbContext.Clients.AddRange(clientA, clientB);
        dbContext.TenantNotificationRecipients.AddRange(
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "Billing", Email = "billing-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "Finance", Email = "finance-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "Invoice CC", Email = "procurement-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantB.Id, RecipientGroup = "Billing", Email = "billing-b@example.com" });
        await dbContext.SaveChangesAsync();

        var service = new FinancialRecipientResolver(dbContext);
        var recipients = await service.ResolveAsync(tenantA.Id, clientA.Id, "Invoice");
        var otherTenantRecipients = await service.ResolveAsync(tenantB.Id, clientB.Id, "Invoice");

        Assert.Equal(["billing-a@example.com", "finance-a@example.com"], recipients.To);
        Assert.Equal(["procurement-a@example.com"], recipients.Cc);
        Assert.DoesNotContain("billing-b@example.com", recipients.To);
        Assert.Equal(["billing-b@example.com"], otherTenantRecipients.To);
    }

    [Fact]
    public async Task ResolveAsync_FallsBackToClientEmail_WhenNoDedicatedBillingRecipientExists()
    {
        await using var dbContext = CreateDbContext();
        var tenant = NewTenant("Tenant");
        var client = new Client { TenantId = tenant.Id, ClientName = "Client", Email = "client@example.com" };
        dbContext.Tenants.Add(tenant);
        dbContext.Clients.Add(client);
        await dbContext.SaveChangesAsync();

        var recipients = await new FinancialRecipientResolver(dbContext).ResolveAsync(tenant.Id, client.Id, "Statement");

        Assert.Equal(["client@example.com"], recipients.To);
        Assert.Empty(recipients.Cc);
    }

    private static Tenant NewTenant(string name) => new()
    {
        Name = name,
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        CompanyName = name,
        Email = $"{name.Replace(' ', '-').ToLowerInvariant()}@example.com",
        Country = "Kenya",
        Status = "Active",
        LicenseStatus = "Active",
        PrimaryColor = "#0F4C81",
        SecondaryColor = "#F4B942",
        IsActive = true,
    };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-recipient-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }
}
