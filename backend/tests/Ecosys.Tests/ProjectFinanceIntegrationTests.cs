using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ecosys.Tests;

/// <summary>
/// Projects and Finance stay in step: invoices Projects raises keep the project's client, currency and amount;
/// a project's work orders bill to the project; credit notes reduce what the project has billed; closing notices
/// voided billing; and costs in another currency are converted (or refused) rather than added at face value.
/// </summary>
public sealed partial class ProjectsModuleTests
{
    [Fact]
    public async Task ProjectInvoice_KeepsItsClientCurrencyAndAmount_ButOtherInvoicesCanChange()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var invoice = await RaiseCertifiedValuationInvoiceAsync(harness, project.Summary.Id, 100_000m);

        Task Edit(Guid clientId, string currency, decimal subtotal) =>
            ProjectFinanceRules.EnsureProjectInvoiceEditAllowedAsync(harness.Db, harness.TenantId, invoice, clientId, currency, subtotal);

        await Edit(invoice.ClientId, "kes", 100_000m);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => Edit(invoice.ClientId, "KES", 90_000m));
        Assert.Contains("raised from project", error.Message);
        Assert.Contains("valuation", error.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Edit(harness.OtherClientId, "KES", 100_000m));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Edit(invoice.ClientId, "USD", 100_000m));

        // An invoice finance created and linked to the project itself stays fully editable.
        var manual = new TenantInvoice { TenantId = harness.TenantId, ClientId = harness.ClientId, ProjectId = project.Summary.Id, InvoiceNumber = "INV-M", Subtotal = 5_000m, Currency = "KES" };
        harness.Db.TenantInvoices.Add(manual);
        await harness.Db.SaveChangesAsync();
        await ProjectFinanceRules.EnsureProjectInvoiceEditAllowedAsync(harness.Db, harness.TenantId, manual, harness.ClientId, "KES", 7_500m);
    }

    [Fact]
    public async Task WorkOrderInvoice_BillsToTheWorkOrdersProject_InTheProjectsCurrency()
    {
        var harness = await Harness.CreateAsync();
        var project = Ok(await harness.Controller.Create(new UpsertProjectRequest(
            "Mombasa fit-out", harness.ClientId, null, null, null, harness.ManagerUserId, null, 50_000m, "USD", null, null, null), CancellationToken.None));
        var workOrder = harness.AddWorkOrder(harness.ClientId);
        workOrder.ProjectId = project.Summary.Id;
        harness.Db.WorkOrderBillableItems.Add(new WorkOrderBillableItem
        {
            TenantId = harness.TenantId, WorkOrderId = workOrder.Id, ItemType = "Service", Description = "Commissioning", Quantity = 1, UnitRate = 800m,
            LineTotal = 800m, TaxRate = 16, TaxAmount = 128m, GrandTotal = 928m,
        });
        await harness.Db.SaveChangesAsync();

        await InvoiceController(harness).GenerateFromWorkOrder(workOrder.Id, CancellationToken.None);

        var invoice = await harness.Db.TenantInvoices.SingleAsync(x => x.TenantId == harness.TenantId);
        Assert.Equal((project.Summary.Id, "USD"), (invoice.ProjectId, invoice.Currency));
    }

    [Fact]
    public async Task CreditNotes_AgainstProjectInvoices_ReduceWhatTheProjectHasBilled()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var invoice = await RaiseCertifiedValuationInvoiceAsync(harness, project.Summary.Id, 100_000m);
        harness.Db.CreditNotes.AddRange(
            new CreditNote { TenantId = harness.TenantId, ClientId = invoice.ClientId, InvoiceId = invoice.Id, CreditNoteNumber = "CN-1", Status = "Issued", Subtotal = 20_000m },
            new CreditNote { TenantId = harness.TenantId, ClientId = invoice.ClientId, InvoiceId = invoice.Id, CreditNoteNumber = "CN-2", Status = "Draft", Subtotal = 5_000m },
            new CreditNote { TenantId = harness.TenantId, ClientId = invoice.ClientId, InvoiceId = invoice.Id, CreditNoteNumber = "CN-3", Status = "Void", Subtotal = 7_000m });
        await harness.Db.SaveChangesAsync();

        var entity = await harness.Db.Projects.SingleAsync(x => x.Id == project.Summary.Id);
        var financials = await new ProjectFinancialsService(harness.Db).ComputeAsync(harness.TenantId, entity);

        Assert.Equal(80_000m, financials.Invoiced);
    }

    [Fact]
    public async Task Closing_IsBlockedWhileAValuationsInvoiceIsVoided_UntilItIsRaisedAgain()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var invoice = await RaiseCertifiedValuationInvoiceAsync(harness, project.Summary.Id, 100_000m);
        invoice.Status = "Void";
        await harness.Db.SaveChangesAsync();
        var closed = await harness.Db.ProjectStages.SingleAsync(x => x.Category == ProjectStageCategories.Closed);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Done"), CancellationToken.None));
        Assert.Contains("was voided and hasn't been raised again", error.Message);

        var valuation = await harness.Db.ProjectValuations.SingleAsync(x => x.ProjectId == project.Summary.Id);
        Ok(await CommercialController(harness, harness.TenantContext).RaiseValuationInvoice(project.Summary.Id, valuation.Id, CancellationToken.None));
        Ok(await harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Done"), CancellationToken.None));
    }

    [Fact]
    public async Task CostsInAnotherCurrency_AreConvertedWithTheExchangeRates_OrRefusedWithoutOne()
    {
        var harness = await Harness.CreateAsync();
        var project = Ok(await harness.Controller.Create(new UpsertProjectRequest(
            "Kampala rollout", harness.ClientId, null, null, null, harness.ManagerUserId, null, 50_000m, "USD", null, null, null), CancellationToken.None));
        var projectId = project.Summary.Id;

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => ProjectFinanceRules.EnsureCostCurrencyAsync(harness.Db, harness.TenantId, projectId, "KES"));
        Assert.Contains("Add an exchange rate", refused.Message);

        // Stored as USD → KES 130: a KES cost converts into the USD project by dividing.
        harness.Db.ExchangeRates.Add(new ExchangeRate { TenantId = harness.TenantId, BaseCurrency = "USD", QuoteCurrency = "KES", Rate = 130m, EffectiveFrom = DateTime.UtcNow.AddMonths(-1) });
        await harness.Db.SaveChangesAsync();
        await ProjectFinanceRules.EnsureCostCurrencyAsync(harness.Db, harness.TenantId, projectId, "KES");
        await ProjectFinanceRules.EnsureCostCurrencyAsync(harness.Db, harness.TenantId, projectId, "usd");

        harness.Db.SupplierBills.AddRange(
            new SupplierBill { TenantId = harness.TenantId, ProjectId = projectId, BillNumber = "BILL-1", Amount = 130_000m, Currency = "KES" },
            new SupplierBill { TenantId = harness.TenantId, ProjectId = projectId, BillNumber = "BILL-2", Amount = 400m, Currency = "USD" },
            new SupplierBill { TenantId = harness.TenantId, ProjectId = projectId, BillNumber = "BILL-3", Amount = 900m, Currency = "GBP" });
        await harness.Db.SaveChangesAsync();

        var entity = await harness.Db.Projects.SingleAsync(x => x.Id == projectId);
        var financials = await new ProjectFinancialsService(harness.Db).ComputeAsync(harness.TenantId, entity);

        Assert.Equal(1_400m, financials.Categories.Single(x => x.Category == ProjectCostCategories.Subcontractor).Actual);
        Assert.Equal(["GBP"], financials.UnconvertedCurrencies);
    }

    [Fact]
    public async Task ProjectCurrency_IsFixedOnceTheProjectHasInvoices()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        await RaiseCertifiedValuationInvoiceAsync(harness, project.Summary.Id, 100_000m);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.Update(project.Summary.Id, new UpsertProjectRequest(
            project.Summary.Name, harness.ClientId, null, null, null, harness.ManagerUserId, null, 1_000_000m, "USD", null, null, null), CancellationToken.None));
        Assert.Contains("can't change", error.Message);
    }

    /// <summary>Takes a valuation through submit and certify, raises its invoice from the project, and returns it.</summary>
    private static async Task<TenantInvoice> RaiseCertifiedValuationInvoiceAsync(Harness harness, Guid projectId, decimal amount)
    {
        var commercial = CommercialController(harness, harness.TenantContext);
        var valuation = Ok(await commercial.CreateValuation(projectId, new UpsertValuationRequest(SiteToday, amount, 0, null), CancellationToken.None));
        Ok(await commercial.SubmitValuation(projectId, valuation.Id, CancellationToken.None));
        Ok(await commercial.CertifyValuation(projectId, valuation.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, amount), CancellationToken.None));
        Ok(await commercial.RaiseValuationInvoice(projectId, valuation.Id, CancellationToken.None));
        return await harness.Db.TenantInvoices.SingleAsync(x => x.ProjectValuationId == valuation.Id);
    }

    /// <summary>The invoice controller with only what the work-order invoice path uses.</summary>
    private static TenantInvoiceController InvoiceController(Harness harness) =>
        new(harness.Db, harness.TenantContext, new FakeNumbering(), null!, new NoOpAuditLogService(), new UserAccessService(harness.TenantContext),
            null!, null!, null!, null!, NullLogger<TenantInvoiceController>.Instance, null!, null!);
}
