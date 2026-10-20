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
    [InlineData("operations", "operations")]
    [InlineData("finance", "finance")]
    [InlineData("suite", "operations,finance")]
    [InlineData("Trial", "operations,finance")]
    [InlineData("Professional", "operations,finance")]
    public void PlanIdentifiersDetermineModules_RegardlessOfDisplayNames(string code, string expected)
    {
        var modules = ServiceOpsPlans.Modules(new LicensePlan { PlanCode = code, DisplayName = "Misleading display name", ModulesIncluded = "Core" });
        Assert.Equal(expected.Split(','), modules);
    }

    [Theory]
    [InlineData("operations", "TenantInvoice", "/api/invoices", false)]
    [InlineData("operations", "TenantInvoice", "/api/payments", false)]
    [InlineData("operations", "TenantInvoice", "/api/clients/123/credit-balance", false)]
    [InlineData("finance", "WorkOrders", "/api/workorders", false)]
    [InlineData("finance", "Sites", "/api/clients/123/sites", false)]
    [InlineData("operations", "WorkOrders", "/api/workorders", true)]
    [InlineData("finance", "TenantInvoice", "/api/invoices", true)]
    [InlineData("suite", "TenantInvoice", "/api/invoices", true)]
    [InlineData("suite", "WorkOrders", "/api/workorders", true)]
    [InlineData("finance", "TenantQuotation", "/api/quotations/123/convert-to-work-order", false)]
    [InlineData("suite", "TenantQuotation", "/api/quotations/123/convert-to-work-order", true)]
    [InlineData("operations", "WorkOrders", "/api/workorders/from-quote", false)]
    [InlineData("suite", "WorkOrders", "/api/workorders/from-quote", true)]
    [InlineData("operations", "TenantInvoice", "/api/work-orders/123/generate-invoice", false)]
    [InlineData("finance", "TenantInvoice", "/api/work-orders/123/generate-invoice", false)]
    [InlineData("suite", "TenantInvoice", "/api/work-orders/123/generate-invoice", true)]
    [InlineData("operations", "Clients", "/api/clients", true)]
    [InlineData("finance", "Clients", "/api/clients", true)]
    [InlineData("finance", "Settings", "/api/settings/email-intake", false)]
    [InlineData("operations", "Integrations", "/api/settings/payments", false)]
    // The sales CRM is a Suite feature: neither single-module plan reaches it.
    [InlineData("operations", "SalesCrm", "/api/crm/opportunities", false)]
    [InlineData("finance", "SalesCrm", "/api/crm/opportunities", false)]
    [InlineData("suite", "SalesCrm", "/api/crm/opportunities", true)]
    [InlineData("operations", "SalesTenders", "/api/crm/tenders", false)]
    [InlineData("finance", "SalesTenders", "/api/crm/tenders", false)]
    [InlineData("suite", "SalesTenders", "/api/crm/tenders", true)]
    [InlineData("Professional", "SalesCrm", "/api/crm/summary", true)]
    [InlineData("operations", "WorkOrders", "/api/workorders/from-opportunity", false)]
    [InlineData("suite", "WorkOrders", "/api/workorders/from-opportunity", true)]
    public async Task ActualLicenseMiddleware_EnforcesPlanForMatchedApiIncludingAliases(string planCode, string controller, string path, bool allowed)
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
        var plan = new LicensePlan { PlanCode = planCode, DisplayName = "Plan test", ModulesIncluded = "Core" };
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

    [Fact]
    public void ExplicitCustomPlanModules_ArePreserved()
    {
        Assert.Equal(["finance"], ServiceOpsPlans.Modules(new LicensePlan { PlanCode = "legacy-custom", ModulesIncluded = "Core,finance,AdvancedReports" }));
    }
}
