using System.Security.Claims;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Security;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

public sealed class ServiceOpsPlanTests
{
    [Theory]
    [InlineData("essentials", "operations,finance,maintenance")]
    [InlineData("professional", "operations,finance,maintenance,inventory,advanced")]
    [InlineData("enterprise", "operations,finance,maintenance,inventory,advanced,enterprise_tools")]
    [InlineData("suite", "")]
    public void EditionCodesDetermineModules_RegardlessOfDisplayNames(string code, string expected)
    {
        var modules = ServiceOpsPlans.Modules(new LicensePlan { PlanCode = code, DisplayName = "Misleading display name" });
        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), modules);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(ServiceOpsPlans.WhatsAppNotificationsAddOn, false)]
    [InlineData(ServiceOpsPlans.WhatsAppBusinessAddOn, true)]
    public void WhatsAppBusinessAddOn_AddsTheInbox_OnAnyEdition(string? addOn, bool hasInbox)
    {
        var modules = ServiceOpsPlans.Modules(new LicensePlan { PlanCode = ServiceOpsPlans.Essentials }, addOn);
        Assert.Equal(hasInbox, modules.Contains(ServiceOpsPlans.WhatsAppBusiness));
    }

    [Theory]
    [InlineData("Technician", SeatTypes.Field)]
    [InlineData("technician", SeatTypes.Field)]
    [InlineData("Viewer", SeatTypes.Free)]
    [InlineData("User", SeatTypes.Free)]
    [InlineData("Admin", SeatTypes.Office)]
    [InlineData("Dispatcher", SeatTypes.Office)]
    [InlineData("finance", SeatTypes.Office)]
    [InlineData("MaterialsController", SeatTypes.Office)]
    public void RolesTakeTheRightSeat(string role, string seat) => Assert.Equal(seat, SeatTypes.For(role));

    [Theory]
    // Essentials: the whole work-order-to-invoice loop.
    [InlineData("essentials", "WorkOrders", "/api/workorders", true)]
    [InlineData("essentials", "TenantFinance", "/api/finance/rate-cards", true)]
    [InlineData("essentials", "Clients", "/api/clients", true)]
    // Maintenance is in every edition: it is the product.
    [InlineData("essentials", "PreventiveMaintenance", "/api/preventive-maintenance", true)]
    [InlineData("essentials", "Assets", "/api/assets", true)]
    [InlineData("essentials", "Contracts", "/api/contracts", true)]
    // Professional adds inventory and the advanced tools.
    [InlineData("essentials", "Materials", "/api/materials", false)]
    [InlineData("essentials", "PurchaseOrders", "/api/purchase-orders", false)]
    [InlineData("essentials", "ApprovalMatrix", "/api/approvals/pending", false)]
    [InlineData("essentials", "Settings", "/api/settings/email-intake", false)]
    [InlineData("essentials", "AdvancedSettings", "/api/settings/inventory", false)]
    [InlineData("professional", "PreventiveMaintenance", "/api/preventive-maintenance", true)]
    [InlineData("professional", "Materials", "/api/materials", true)]
    [InlineData("professional", "ApprovalMatrix", "/api/approvals/pending", true)]
    [InlineData("professional", "Settings", "/api/settings/email-intake", true)]
    // Enterprise adds API keys, webhooks and monitoring.
    [InlineData("professional", "Integrations", "/api/settings/api-keys", false)]
    [InlineData("professional", "Settings", "/api/settings/monitoring-webhooks", false)]
    [InlineData("enterprise", "Integrations", "/api/settings/api-keys", true)]
    [InlineData("enterprise", "Settings", "/api/settings/monitoring-webhooks", true)]
    // The two-way WhatsApp inbox needs the WhatsApp Business add-on, whatever the edition.
    [InlineData("enterprise", "WhatsApp", "/api/whatsapp/conversations", false)]
    // The sales CRM needs Operations and Finance, which every edition includes.
    [InlineData("essentials", "SalesCrm", "/api/crm/opportunities", true)]
    [InlineData("essentials", "SalesTenders", "/api/crm/tenders", true)]
    [InlineData("professional", "SalesCrm", "/api/crm/summary", true)]
    [InlineData("essentials", "WorkOrders", "/api/workorders/from-opportunity", true)]
    public async Task ActualLicenseMiddleware_EnforcesEditionForMatchedApiIncludingAliases(string planCode, string controller, string path, bool allowed)
    {
        var tenantId = Guid.NewGuid();
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.Method = "GET";
        http.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(TenantClaimTypes.TenantId, tenantId.ToString()),
            new Claim(TenantClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        ], "test"));
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller }), "test"));
        var tenantContext = new HttpTenantContext(new HttpContextAccessor { HttpContext = http });
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Plan test", CompanyName = "Plan test", Slug = "plan-test", Country = "Kenya", IsActive = true });
        var plan = new LicensePlan { PlanCode = planCode, DisplayName = "Plan test" };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active" });
        await db.SaveChangesAsync();
        var reachedEndpoint = false;
        var middleware = new TenantLicenseEnforcementMiddleware(_ => { reachedEndpoint = true; return Task.CompletedTask; });
        var guard = new LicenseGuardService(db);
        if (allowed)
            await middleware.InvokeAsync(http, tenantContext, guard, db);
        else
        {
            var error = await Assert.ThrowsAsync<LicenseException>(() => middleware.InvokeAsync(http, tenantContext, guard, db));
            Assert.Equal(403, error.StatusCode);
            Assert.StartsWith("license_module_", error.ErrorCode);
        }
        Assert.Equal(allowed, reachedEndpoint);
    }

    [Theory]
    [InlineData("essentials", 1)]
    [InlineData("professional", 1)]
    [InlineData("enterprise", null)]
    public async Task OnlyEnterprise_HasUnlimitedBranches(string planCode, int? expected)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenantId = Guid.NewGuid();
        var plan = new LicensePlan { PlanCode = planCode, DisplayName = planCode };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense { TenantId = tenantId, LicensePlan = plan, Status = "Active" });
        await db.SaveChangesAsync();

        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(tenantId);

        Assert.Equal(expected, snapshot.MaxBranches);
        Assert.Equal(planCode == "enterprise", snapshot.MonitoringIntegration);
    }

    [Fact]
    public void MonthlyValue_IsSeatsTimesPricesPlusTheAddOn()
    {
        var plan = new LicensePlan { PlanCode = "professional", OfficeSeatMonthlyPrice = 2000m, OfficeSeatAnnualPrice = 20000m, FieldSeatMonthlyPrice = 800m };
        var addOn = new LicenseAddOn { Code = ServiceOpsPlans.WhatsAppNotificationsAddOn, MonthlyPrice = 1500m, AnnualPrice = 12000m };
        var license = new TenantLicense { LicensePlan = plan, OfficeSeats = 3, FieldSeats = 5, WhatsAppAddOn = addOn.Code, BillingCycle = BillingCycles.Monthly };

        Assert.Equal(3 * 2000m + 5 * 800m + 1500m, LicensePricing.MonthlyValue(license, [addOn]));

        // Annual: the annual price over twelve months; a zero annual price (field seats here) falls back to monthly.
        license.BillingCycle = BillingCycles.Annual;
        Assert.Equal(decimal.Round(3 * 20000m / 12m + 5 * 800m + 12000m / 12m, 2), LicensePricing.MonthlyValue(license, [addOn]));
    }

    [Fact]
    public void MonthlyValue_UsesThePriceAgreedWithTheTenant_AndFallsBackToTheEditionPerSeatType()
    {
        var plan = new LicensePlan { PlanCode = "professional", OfficeSeatMonthlyPrice = 2000m, FieldSeatMonthlyPrice = 800m };
        var license = new TenantLicense { LicensePlan = plan, OfficeSeats = 3, FieldSeats = 5, BillingCycle = BillingCycles.Monthly };

        // Only the office seat has an agreed price; field seats stay on the edition's price.
        license.OfficeSeatPriceOverride = 1500m;
        Assert.Equal(1500m, LicensePricing.OfficeSeatPrice(license));
        Assert.Equal(800m, LicensePricing.FieldSeatPrice(license));
        Assert.Equal(2000m, LicensePricing.EditionOfficeSeatPrice(license));
        Assert.Equal(3 * 1500m + 5 * 800m, LicensePricing.MonthlyValue(license, []));

        // An agreed price of zero is a free seat, not "unset".
        license.FieldSeatPriceOverride = 0m;
        Assert.Equal(3 * 1500m, LicensePricing.MonthlyValue(license, []));

        license.OfficeSeatPriceOverride = null;
        license.FieldSeatPriceOverride = null;
        Assert.Equal(3 * 2000m + 5 * 800m, LicensePricing.MonthlyValue(license, []));
    }
}
