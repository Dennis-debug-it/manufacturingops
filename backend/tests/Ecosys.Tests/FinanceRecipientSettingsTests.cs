using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>
/// Finance CC addresses are owned by the tenant (Settings → Finance Defaults); the platform view of
/// them is read-only. Saving replaces the list, and every address ends up copied — never the "To".
/// </summary>
public sealed class FinanceRecipientSettingsTests
{
    [Fact]
    public async Task Tenant_SavesItsFinanceCcList_AndInvoicesStillGoToTheClient()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenantId = Guid.NewGuid();
        var client = new Client { TenantId = tenantId, ClientName = "Client", Email = "accounts@client.com" };
        db.Clients.Add(client);
        // A legacy "Billing" row set from the platform side — it used to replace the client as "To".
        db.TenantNotificationRecipients.Add(new TenantNotificationRecipient { TenantId = tenantId, RecipientGroup = "Billing", Email = "old@tenant.com" });
        await db.SaveChangesAsync();
        var controller = new AdvancedSettingsController(db, new AdminTenantContext(tenantId), new AllowAllAccess(), null!, null!);

        var legacy = Assert.IsAssignableFrom<IReadOnlyCollection<FinanceRecipientDto>>(Assert.IsType<OkObjectResult>((await controller.GetFinanceRecipients(CancellationToken.None)).Result).Value);
        Assert.Equal([new FinanceRecipientDto("old@tenant.com", "All")], legacy);

        await controller.UpdateFinanceRecipients([
            new FinanceRecipientDto(" Finance@Tenant.com ", "All"),
            new FinanceRecipientDto("receipts@tenant.com", "Receipt"),
            new FinanceRecipientDto("", "All"),
        ], CancellationToken.None);
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.UpdateFinanceRecipients([new FinanceRecipientDto("not-an-email", "All")], CancellationToken.None));

        var invoice = await new FinancialRecipientResolver(db).ResolveAsync(tenantId, client.Id, "Invoice");
        var receipt = await new FinancialRecipientResolver(db).ResolveAsync(tenantId, client.Id, "Receipt");
        Assert.Equal(["accounts@client.com"], invoice.To);
        Assert.Equal(["finance@tenant.com"], invoice.Cc);
        Assert.Equal(["finance@tenant.com", "receipts@tenant.com"], receipt.Cc);
    }

    private sealed class AdminTenantContext(Guid tenantId) : ITenantContext
    {
        private readonly Guid userId = Guid.NewGuid();
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public string? Email => "admin@tenant.com";
        public string? Role => AppRoles.Admin;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => Guid.Empty;
    }

    private sealed class AllowAllAccess : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }
}
