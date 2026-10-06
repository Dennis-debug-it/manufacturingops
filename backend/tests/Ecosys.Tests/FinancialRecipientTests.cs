using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class FinancialRecipientTests
{
    [Fact]
    public async Task ResolveAsync_AlwaysSendsToTheClient_AndOnlyCopiesTheTenantsFinanceAddresses()
    {
        // Tenant recipients are tenant-wide. They used to replace the client as "To", so every client's
        // invoice went to the tenant's own inbox and never reached the client.
        await using var dbContext = CreateDbContext();
        var tenantA = NewTenant("Tenant A");
        var tenantB = NewTenant("Tenant B");
        var clientA = new Client { TenantId = tenantA.Id, ClientName = "Client A", Email = "accounts@client-a.com" };
        var clientB = new Client { TenantId = tenantB.Id, ClientName = "Client B", Email = "accounts@client-b.com" };
        dbContext.Tenants.AddRange(tenantA, tenantB);
        dbContext.Clients.AddRange(clientA, clientB);
        dbContext.TenantNotificationRecipients.AddRange(
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "Billing", Email = "billing-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "cc", Email = "finance-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "Receipt CC", Email = "receipts-a@example.com" },
            new TenantNotificationRecipient { TenantId = tenantA.Id, RecipientGroup = "cc", Email = "accounts@client-a.com" },
            new TenantNotificationRecipient { TenantId = tenantB.Id, RecipientGroup = "Billing", Email = "billing-b@example.com" });
        await dbContext.SaveChangesAsync();

        var service = new FinancialRecipientResolver(dbContext);
        var invoice = await service.ResolveAsync(tenantA.Id, clientA.Id, "Invoice");
        var receipt = await service.ResolveAsync(tenantA.Id, clientA.Id, "Receipt");
        var otherTenant = await service.ResolveAsync(tenantB.Id, clientB.Id, "Invoice");

        Assert.Equal(["accounts@client-a.com"], invoice.To);
        Assert.Equal(["billing-a@example.com", "finance-a@example.com"], invoice.Cc);
        Assert.Contains("receipts-a@example.com", receipt.Cc);
        Assert.Equal(["accounts@client-b.com"], otherTenant.To);
        Assert.Equal(["billing-b@example.com"], otherTenant.Cc);
    }

    [Fact]
    public async Task ResolveAsync_ClientWithoutEmail_HasNoMainRecipient()
    {
        // The tenant's own addresses never stand in for the client: the send fails loudly instead.
        await using var dbContext = CreateDbContext();
        var tenant = NewTenant("Tenant");
        var client = new Client { TenantId = tenant.Id, ClientName = "Client" };
        dbContext.Tenants.Add(tenant);
        dbContext.Clients.Add(client);
        dbContext.TenantNotificationRecipients.Add(new TenantNotificationRecipient { TenantId = tenant.Id, RecipientGroup = "cc", Email = "finance@example.com" });
        await dbContext.SaveChangesAsync();

        var recipients = await new FinancialRecipientResolver(dbContext).ResolveAsync(tenant.Id, client.Id, "Invoice");

        Assert.Empty(recipients.To);
    }

    [Fact]
    public async Task ResolveAsync_SendsToClientEmail_WhenNoTenantRecipientsExist()
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
