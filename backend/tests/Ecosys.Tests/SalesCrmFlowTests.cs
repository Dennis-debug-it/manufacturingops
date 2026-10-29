using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Domain.Enums;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>The CRM hand-off: a prospect deal is won, becomes a client, and turns into work orders.</summary>
public sealed class SalesCrmFlowTests
{
    [Fact]
    public async Task WonProspect_BecomesClient_AndConvertsToLinkedWorkOrder()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);

        var created = await crm.CreateOpportunity(
            new UpsertSalesOpportunityRequest(
                "Rooftop solar for warehouse", null, null,
                "Baraka Logistics", "Wanjiru Otieno", "wanjiru@baraka.test", "+254700000200", "Mombasa Road",
                "Referral", WorkOrderTypes.NewInstallation, "20 kWp grid-tie system.",
                1_250_000m, "KES", null, null, null),
            CancellationToken.None);
        var opportunity = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value);
        Assert.Equal(SalesStages.Lead, opportunity.Stage);
        Assert.Equal(10, opportunity.Probability);
        Assert.Equal(actor.Id, opportunity.OwnerUserId);

        var wo = CreateWorkOrdersController(dbContext, tenant.Id, actor);

        // Not won yet: no work order.
        await Assert.ThrowsAsync<BusinessRuleException>(() => wo.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(opportunity.Id, null, null, null, null, null, null), CancellationToken.None));

        // Entered with only a company name, the deal already has a client record: a prospect,
        // kept out of client pickers until the deal is won.
        var prospect = await dbContext.Clients.SingleAsync(x => x.Id == opportunity.ClientId);
        Assert.True(prospect.IsProspect);
        Assert.Equal("Baraka Logistics", prospect.ClientName);
        Assert.Equal("Wanjiru Otieno", prospect.ContactPerson);

        await crm.ChangeStage(opportunity.Id, new ChangeSalesStageRequest(SalesStages.Won, null), CancellationToken.None);
        var client = await dbContext.Clients.SingleAsync(x => x.Id == opportunity.ClientId);
        Assert.False(client.IsProspect);
        Assert.Single(await dbContext.Clients.ToListAsync());

        var result = await wo.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(opportunity.Id, null, null, WorkOrderPriorities.High, null, null, null),
            CancellationToken.None);
        var workOrder = Assert.IsType<WorkOrderResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(client.Id, workOrder.ClientId);
        Assert.Equal("Rooftop solar for warehouse", workOrder.Title);
        Assert.Equal(WorkOrderTypes.NewInstallation, workOrder.WorkType);
        Assert.Equal(opportunity.Id, workOrder.SalesOpportunityId);

        var detail = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<OkObjectResult>(
            (await crm.GetOpportunity(opportunity.Id, CancellationToken.None)).Result).Value);
        Assert.Equal(SalesStages.Won, detail.Stage);
        Assert.Single(detail.WorkOrders);
        Assert.Contains(detail.Activities, x => x.Summary.Contains("Work order"));
    }

    [Fact]
    public async Task MarkingLost_RequiresReason()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        var created = await crm.CreateOpportunity(
            new UpsertSalesOpportunityRequest("Generator AMC", null, null, "Kilele Hotel", null, null, null, null,
                null, null, null, 300_000m, "KES", null, null, null),
            CancellationToken.None);
        var id = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            crm.ChangeStage(id, new ChangeSalesStageRequest(SalesStages.Lost, " "), CancellationToken.None));

        await crm.ChangeStage(id, new ChangeSalesStageRequest(SalesStages.Lost, "Went with a cheaper vendor"), CancellationToken.None);
        var stored = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == id);
        Assert.Equal(SalesStages.Lost, stored.Stage);
        Assert.Equal(0, stored.Probability);
        Assert.NotNull(stored.LostAt);
    }

    [Fact]
    public async Task PipelineTotals_ConvertForeignDeals_AndFlagCurrenciesWithoutARate()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        await crm.CreateOpportunity(Deal("Local job", 100_000m, "KES"), CancellationToken.None);
        await crm.CreateOpportunity(Deal("Donor-funded job", 1_000m, "USD"), CancellationToken.None);

        var before = Ok<SalesPipelineSummary>(await crm.GetSummary(CancellationToken.None));
        Assert.Equal("KES", before.ReportingCurrency);
        Assert.Equal(100_000m, before.OpenValue);
        var missing = Assert.Single(before.UnconvertedCurrencies);
        Assert.Equal("USD", missing.Currency);
        Assert.Equal(1_000m, missing.Value);

        var settings = Ok<SalesCurrencySettings>(await crm.SetExchangeRate(new SetSalesExchangeRateRequest("usd", 129.5m), CancellationToken.None));
        Assert.Empty(settings.MissingCurrencies);
        Assert.Equal(129.5m, Assert.Single(settings.Rates).RateToReporting);

        var after = Ok<SalesPipelineSummary>(await crm.GetSummary(CancellationToken.None));
        Assert.Empty(after.UnconvertedCurrencies);
        Assert.Equal(229_500m, after.OpenValue);
        Assert.Equal(22_950m, after.WeightedValue); // both at Lead, 10%

        // The rate lands in the same history Finance › Exchange Rates shows.
        var stored = await dbContext.ExchangeRates.SingleAsync();
        Assert.Equal(("USD", "KES"), (stored.BaseCurrency, stored.QuoteCurrency));
    }

    [Fact]
    public async Task RateEnteredTheOtherWayRound_IsInverted()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        dbContext.ExchangeRates.Add(new ExchangeRate { TenantId = tenant.Id, BaseCurrency = "KES", QuoteCurrency = "EUR", Rate = 0.008m, EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
        await dbContext.SaveChangesAsync();
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        await crm.CreateOpportunity(Deal("EU-funded job", 800m, "EUR"), CancellationToken.None);

        var summary = Ok<SalesPipelineSummary>(await crm.GetSummary(CancellationToken.None));
        Assert.Equal(100_000m, summary.OpenValue);
    }

    [Fact]
    public async Task Owner_CanBeReassigned_ButOnlyToAnActiveCrmUser()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var manager = new User { TenantId = tenant.Id, FullName = "Grace Manager", Email = "grace@acme.test", PasswordHash = "hash", Role = AppRoles.Manager, IsActive = true };
        var technician = new User { TenantId = tenant.Id, FullName = "Tom Tech", Email = "tom@acme.test", PasswordHash = "hash", Role = AppRoles.Technician, IsActive = true };
        dbContext.Users.AddRange(manager, technician);
        await dbContext.SaveChangesAsync();
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        var created = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(
            (await crm.CreateOpportunity(Deal("Chiller service", 50_000m, "KES"), CancellationToken.None)).Result).Value);

        var owners = Ok<IReadOnlyCollection<SalesOwnerOption>>(await crm.ListOwners(CancellationToken.None));
        Assert.Contains(owners, x => x.Id == manager.Id);
        Assert.DoesNotContain(owners, x => x.Id == technician.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => crm.ReassignOwner(created.Id, new ReassignSalesOwnerRequest(technician.Id), CancellationToken.None));

        var reassigned = Ok<SalesOpportunityDetailResponse>(await crm.ReassignOwner(created.Id, new ReassignSalesOwnerRequest(manager.Id), CancellationToken.None));
        Assert.Equal(manager.Id, reassigned.OwnerUserId);
        Assert.Equal("Grace Manager", reassigned.OwnerName);
        Assert.Contains(reassigned.Activities, x => x.Summary == "Owner changed from Sales Admin to Grace Manager.");
    }

    [Fact]
    public async Task Tender_BidThenWon_WinsItsOpportunity()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var tenders = CreateTendersController(dbContext, tenant.Id, actor);

        var created = Assert.IsType<SalesTenderDetailResponse>(Assert.IsType<CreatedAtActionResult>((await tenders.Create(
            new UpsertSalesTenderRequest("rfq", "KPLC/RFQ/2026/114", "Substation HVAC maintenance", null, "Kenya Power",
                "Procurement Desk", "tenders@kplc.test", null, null, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(10),
                "Supplier portal", true, 50_000m, "Two substations, 12 months.", 2_400_000m, "KES", null),
            CancellationToken.None)).Result).Value);
        Assert.Equal("RFQ", created.Type);
        Assert.Equal(SalesTenderStatuses.Received, created.Status);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            tenders.ChangeStatus(created.Id, new ChangeSalesTenderStatusRequest(SalesTenderStatuses.NoBid, null, null), CancellationToken.None));

        var bid = Ok<SalesTenderDetailResponse>(await tenders.CreateOpportunity(created.Id, CancellationToken.None));
        Assert.NotNull(bid.OpportunityId);
        Assert.Equal(SalesTenderStatuses.Preparing, bid.Status);

        await tenders.ChangeStatus(created.Id, new ChangeSalesTenderStatusRequest(SalesTenderStatuses.Submitted, null, 2_250_000m), CancellationToken.None);
        var won = Ok<SalesTenderDetailResponse>(await tenders.ChangeStatus(created.Id, new ChangeSalesTenderStatusRequest(SalesTenderStatuses.Won, "Award letter received", null), CancellationToken.None));
        Assert.Equal(SalesStages.Won, won.OpportunityStage);

        var opportunity = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == bid.OpportunityId);
        Assert.Equal(2_250_000m, opportunity.EstimatedValue);
        Assert.Equal("Kenya Power", opportunity.CompanyName);
        Assert.Equal("Tender", opportunity.Source);

        var summary = Ok<SalesPipelineSummary>(await CreateCrmController(dbContext, tenant.Id, actor).GetSummary(CancellationToken.None));
        Assert.Equal(0, summary.OpenTenders);
    }

    [Fact]
    public async Task Access_FollowsTheCrmCapability_NotTheRole()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);

        // A manager whose role has had CRM switched off in the matrix is refused…
        var managerWithoutCrm = CreateCrmController(dbContext, tenant.Id, actor, new ScopedTenantContext(tenant.Id, actor.Id, AppRoles.Manager));
        await Assert.ThrowsAsync<ForbiddenException>(() => managerWithoutCrm.GetSummary(CancellationToken.None));

        // …a read-only grant can look but not change anything…
        var reader = CreateCrmController(dbContext, tenant.Id, actor, new ScopedTenantContext(tenant.Id, actor.Id, AppRoles.Viewer, PermissionNames.CrmView));
        Ok<SalesPipelineSummary>(await reader.GetSummary(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => reader.CreateOpportunity(Deal("Not allowed", 1m, "KES"), CancellationToken.None));
        var readerTenders = CreateTendersController(dbContext, tenant.Id, actor, new ScopedTenantContext(tenant.Id, actor.Id, AppRoles.Viewer, PermissionNames.CrmView));
        await Assert.ThrowsAsync<ForbiddenException>(() => readerTenders.Delete(Guid.NewGuid(), CancellationToken.None));

        // …and a dispatcher granted CRM manage works the pipeline like anyone else.
        var dispatcher = CreateCrmController(dbContext, tenant.Id, actor, new ScopedTenantContext(tenant.Id, actor.Id, AppRoles.Dispatcher, PermissionNames.CrmManage));
        Assert.IsType<CreatedAtActionResult>((await dispatcher.CreateOpportunity(Deal("Dispatcher-sourced lead", 10_000m, "KES"), CancellationToken.None)).Result);
    }

    [Fact]
    public async Task Owners_AreWhoeverCanManageTheCrm_ByRoleGroupOrOverride()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        User NewUser(string name, string role) => new() { TenantId = tenant.Id, FullName = name, Email = $"{Guid.NewGuid():N}@acme.test", PasswordHash = "hash", Role = role, IsActive = true };
        var manager = NewUser("Manager by role", AppRoles.Manager);
        var deniedManager = NewUser("Manager overridden off", AppRoles.Manager);
        var groupDispatcher = NewUser("Dispatcher via group", AppRoles.Dispatcher);
        var overrideTech = NewUser("Technician via override", AppRoles.Technician);
        var plainDispatcher = NewUser("Plain dispatcher", AppRoles.Dispatcher);
        var viewer = NewUser("Read-only viewer", AppRoles.Viewer);
        var group = new PermissionGroup { TenantId = tenant.Id, Name = "Sales desk" };
        dbContext.AddRange(manager, deniedManager, groupDispatcher, overrideTech, plainDispatcher, viewer, group);
        dbContext.GroupCapabilities.Add(new GroupCapability { TenantId = tenant.Id, PermissionGroupId = group.Id, Capability = PermissionNames.CrmManage });
        dbContext.UserPermissionGroups.Add(new UserPermissionGroup { TenantId = tenant.Id, PermissionGroupId = group.Id, UserId = groupDispatcher.Id });
        dbContext.UserPermissionOverrides.AddRange(
            new UserPermissionOverride { TenantId = tenant.Id, UserId = overrideTech.Id, Capability = PermissionNames.CrmManage, IsAllowed = true },
            new UserPermissionOverride { TenantId = tenant.Id, UserId = deniedManager.Id, Capability = PermissionNames.CrmManage, IsAllowed = false });
        await dbContext.SaveChangesAsync();

        var owners = Ok<IReadOnlyCollection<SalesOwnerOption>>(await CreateCrmController(dbContext, tenant.Id, actor).ListOwners(CancellationToken.None))
            .Select(x => x.FullName).ToList();

        Assert.Equal(["Dispatcher via group", "Manager by role", "Sales Admin", "Technician via override"], owners);
    }

    [Fact]
    public async Task RoleDefaults_GiveCrmToTheRolesThatHadIt()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, _) = await SeedAsync(dbContext);
        var matrix = (await new RolePermissionService(dbContext).GetMatrixAsync(tenant.Id))
            .ToDictionary(x => x.Role, x => (x.Permissions.CanViewCrm, x.Permissions.CanManageCrm), StringComparer.OrdinalIgnoreCase);

        Assert.Equal((true, true), matrix[AppRoles.Manager]);
        Assert.Equal((true, true), matrix[AppRoles.FinanceUser]);
        Assert.Equal((true, false), matrix[AppRoles.Viewer]);
        Assert.Equal((false, false), matrix[AppRoles.Dispatcher]);
        Assert.Equal((false, false), matrix[AppRoles.Technician]);
        Assert.Equal((false, false), matrix[AppRoles.MaterialsController]);
    }

    [Fact]
    public async Task MovingATenderDeadline_ResetsItsReminders()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var tenders = CreateTendersController(dbContext, tenant.Id, actor);
        UpsertSalesTenderRequest Request(DateTime deadline) => new("RFQ", "REF-1", "Generator spares", null, "Kilele Hotel", null, null, null,
            DateTime.UtcNow.AddDays(-1), null, deadline, null, false, null, null, 100m, "KES", null);
        var deadline = DateTime.UtcNow.AddDays(2);
        var created = Assert.IsType<SalesTenderDetailResponse>(Assert.IsType<CreatedAtActionResult>((await tenders.Create(Request(deadline), CancellationToken.None)).Result).Value);
        var stored = await dbContext.SalesTenders.SingleAsync(x => x.Id == created.Id);
        stored.DeadlineReminderStage = 2;
        await dbContext.SaveChangesAsync();

        await tenders.Update(created.Id, Request(deadline) with { Title = "Generator spares (rev 2)" }, CancellationToken.None);
        Assert.Equal(2, (await dbContext.SalesTenders.SingleAsync(x => x.Id == created.Id)).DeadlineReminderStage);

        await tenders.Update(created.Id, Request(deadline.AddDays(7)), CancellationToken.None);
        Assert.Equal(0, (await dbContext.SalesTenders.SingleAsync(x => x.Id == created.Id)).DeadlineReminderStage);
    }

    [Fact]
    public async Task FollowUps_ListOpenOnesOnOpenDeals_SoonestFirst()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        var open = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>((await crm.CreateOpportunity(Deal("Chiller service", 50_000m, "KES"), CancellationToken.None)).Result).Value);
        var closed = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>((await crm.CreateOpportunity(Deal("Lift AMC", 80_000m, "KES"), CancellationToken.None)).Result).Value);

        var now = DateTime.UtcNow;
        var later = Ok<SalesActivityResponse>(await crm.AddActivity(open.Id, new CreateSalesActivityRequest("FollowUp", "Send revised quote", null, now.AddDays(2)), CancellationToken.None));
        var overdue = Ok<SalesActivityResponse>(await crm.AddActivity(open.Id, new CreateSalesActivityRequest("Call", "Call the facilities manager", null, now.AddDays(-1)), CancellationToken.None));
        var done = Ok<SalesActivityResponse>(await crm.AddActivity(open.Id, new CreateSalesActivityRequest("FollowUp", "Book site survey", null, now.AddDays(-2)), CancellationToken.None));
        await crm.CompleteActivity(done.Id, CancellationToken.None);
        await crm.AddActivity(open.Id, new CreateSalesActivityRequest("Note", "Prefers email", null, null), CancellationToken.None);
        await crm.AddActivity(closed.Id, new CreateSalesActivityRequest("FollowUp", "Chase decision", null, now.AddDays(-3)), CancellationToken.None);
        await crm.ChangeStage(closed.Id, new ChangeSalesStageRequest(SalesStages.Lost, "Budget cut"), CancellationToken.None);

        var list = Ok<IReadOnlyCollection<SalesFollowUpItem>>(await crm.ListFollowUps(null, CancellationToken.None));
        Assert.Equal(new[] { overdue.Id, later.Id }, list.Select(x => x.Id));
        Assert.All(list, x => Assert.Equal("Chiller service", x.OpportunityTitle));
        Assert.Equal("Prospect Ltd", list.First().PartyName);

        Assert.Empty(Ok<IReadOnlyCollection<SalesFollowUpItem>>(await crm.ListFollowUps(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(2, Ok<IReadOnlyCollection<SalesFollowUpItem>>(await crm.ListFollowUps(actor.Id, CancellationToken.None)).Count);

        // The lost deal's follow-up is not overdue work.
        Assert.Equal(1, Ok<SalesPipelineSummary>(await crm.GetSummary(CancellationToken.None)).OverdueFollowUps);
    }

    [Fact]
    public async Task Report_CountsResultsInThePeriod_AndForecastsOpenDealsByCloseMonth()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        async Task<Guid> Create(string title, decimal value, string? source = null)
        {
            var created = await crm.CreateOpportunity(Deal(title, value, "KES") with { Source = source }, CancellationToken.None);
            return Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;
        }

        var wonId = await Create("Won deal", 100_000m, "Referral");
        var oldWinId = await Create("Won long ago", 999_000m, "Referral");
        var lostId = await Create("Lost on price", 40_000m, "Website");
        var lost2Id = await Create("Also lost on price", 60_000m, "Website");
        var openId = await Create("Open deal", 200_000m);
        await Create("Open, no date", 50_000m);
        await crm.ChangeStage(wonId, new ChangeSalesStageRequest(SalesStages.Won, null), CancellationToken.None);
        await crm.ChangeStage(oldWinId, new ChangeSalesStageRequest(SalesStages.Won, null), CancellationToken.None);
        await crm.ChangeStage(lostId, new ChangeSalesStageRequest(SalesStages.Lost, "Price"), CancellationToken.None);
        await crm.ChangeStage(lost2Id, new ChangeSalesStageRequest(SalesStages.Lost, " price "), CancellationToken.None);

        var now = DateTime.UtcNow;
        var won = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == wonId);
        won.CreatedAt = won.WonAt!.Value.AddDays(-10);
        (await dbContext.SalesOpportunities.SingleAsync(x => x.Id == oldWinId)).WonAt = now.AddYears(-1);
        (await dbContext.SalesOpportunities.SingleAsync(x => x.Id == openId)).ExpectedCloseDate = new DateTime(now.Year, now.Month, 15, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        await dbContext.SaveChangesAsync();

        var reports = new SalesReportsController(dbContext, new FakeTenantContext(tenant.Id, actor.Id, actor.Email), new NoOpUserAccessService())
        {
            ControllerContext = CreateControllerContext(actor)
        };
        var report = Ok<SalesReport>(await reports.Get(now.AddDays(-30), now, null, CancellationToken.None));

        Assert.Equal((1, 100_000m), (report.WonCount, report.WonValue));
        Assert.Equal((2, 100_000m), (report.LostCount, report.LostValue));
        Assert.Equal(33.3m, report.WinRate);
        Assert.Equal(10m, report.AverageDaysToWin);
        Assert.Equal((2, 250_000m), (report.OpenCount, report.OpenValue));

        var reason = Assert.Single(report.LostReasons);
        Assert.Equal((2, 100_000m), (reason.Count, reason.Value));

        var referral = Assert.Single(report.Sources, x => x.Source == "Referral");
        Assert.Equal((1, 100_000m, 0), (referral.WonCount, referral.WonValue, referral.LostCount));
        Assert.Equal(0m, Assert.Single(report.Sources, x => x.Source == "Website").WinRate);

        var nextMonth = report.Forecast.Single(x => x.Month == new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1));
        Assert.Equal((1, 200_000m), (nextMonth.Count, nextMonth.Value));
        Assert.Equal(1, report.Forecast.Single(x => x.Key == "none").Count);
        Assert.Equal(SalesReportsController.ForecastMonths + 3, report.Forecast.Count);

        // Someone else's deals only.
        Assert.Equal(0, Ok<SalesReport>(await reports.Get(now.AddDays(-30), now, Guid.NewGuid(), CancellationToken.None)).OpenCount);
    }

    [Fact]
    public async Task AnsweringAQuotation_WinsTheDeal_OrSetsAFollowUp()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        dbContext.Clients.Add(new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel" });
        await dbContext.SaveChangesAsync();
        var clientId = (await dbContext.Clients.SingleAsync()).Id;

        async Task<(Guid Deal, Guid Quote)> DealWithSentQuote(string title, decimal total)
        {
            var created = await crm.CreateOpportunity(Deal(title, 10_000m, "KES") with { ClientId = clientId, CompanyName = null }, CancellationToken.None);
            var id = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;
            await crm.ConvertToQuotation(id, CancellationToken.None);
            var quote = await dbContext.TenantQuotations.SingleAsync(x => x.SalesOpportunityId == id);
            quote.Status = "Sent";
            quote.TotalAmount = total;
            await dbContext.SaveChangesAsync();
            return (id, quote.Id);
        }

        var quotes = new TenantQuotationController(
            dbContext, new FakeTenantContext(tenant.Id, actor.Id, actor.Email), new NoOpDocumentNumberingService(), null!,
            new AuditLogService(dbContext, new HttpContextAccessor()), new NoOpUserAccessService(), null!, null!, null!, null!, null!)
        {
            ControllerContext = CreateControllerContext(actor)
        };

        var accepted = await DealWithSentQuote("Chiller replacement", 480_000m);
        await quotes.AcceptQuotation(accepted.Quote, CancellationToken.None);
        var won = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == accepted.Deal);
        Assert.Equal((SalesStages.Won, 480_000m, 100), (won.Stage, won.EstimatedValue, won.Probability));
        Assert.NotNull(won.WonAt);

        var declined = await DealWithSentQuote("Lift modernisation", 900_000m);
        await quotes.RejectQuotation(declined.Quote, new RejectQuotationRequest("Over budget"), CancellationToken.None);
        var stillOpen = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == declined.Deal);
        Assert.Equal(SalesStages.Proposal, stillOpen.Stage);
        var followUp = Assert.Single(await dbContext.SalesActivities.Where(x => x.OpportunityId == declined.Deal && x.DueAt != null).ToListAsync());
        Assert.Contains("declined", followUp.Summary);
        Assert.Contains(await dbContext.SalesActivities.Where(x => x.OpportunityId == declined.Deal).ToListAsync(), x => x.Summary.Contains("Over budget"));
    }

    [Fact]
    public async Task QuotesAwaitingAnswer_ShowOtherQuotesOnlyToFinance()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, actor) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, actor);
        var client = new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel" };
        dbContext.Clients.Add(client);
        var created = await crm.CreateOpportunity(Deal("Chiller service", 50_000m, "KES"), CancellationToken.None);
        var dealId = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;
        TenantQuotation Quote(string number, Guid? deal, string status = "Sent", int sentDaysAgo = 1) => new()
        {
            TenantId = tenant.Id, ClientId = client.Id, QuoteNumber = number, Status = status, TotalAmount = 1_000m,
            QuoteDate = DateTime.UtcNow.AddDays(-sentDaysAgo), SentAt = DateTime.UtcNow.AddDays(-sentDaysAgo), ValidUntil = DateTime.UtcNow.AddDays(30),
            SalesOpportunityId = deal, SentByUserId = actor.Id,
        };
        dbContext.TenantQuotations.AddRange(Quote("QT-DEAL", dealId), Quote("QT-OTHER", null, sentDaysAgo: 5), Quote("QT-DRAFT", dealId, "Draft"));
        await dbContext.SaveChangesAsync();

        var all = Ok<IReadOnlyCollection<SalesQuoteAwaitingAnswer>>(await crm.ListQuotesAwaitingAnswer(null, CancellationToken.None));
        Assert.Equal(["QT-OTHER", "QT-DEAL"], all.Select(x => x.QuoteNumber).ToArray());
        Assert.Equal("Chiller service", all.Last().OpportunityTitle);
        Assert.Equal("Sales Admin", all.First().OwnerName);

        var salesOnly = CreateCrmController(dbContext, tenant.Id, actor, new ScopedTenantContext(tenant.Id, actor.Id, AppRoles.Viewer, PermissionNames.CrmView));
        Assert.Equal("QT-DEAL", Assert.Single(Ok<IReadOnlyCollection<SalesQuoteAwaitingAnswer>>(await salesOnly.ListQuotesAwaitingAnswer(null, CancellationToken.None))).QuoteNumber);
    }

    [Fact]
    public async Task OwnDealsOnly_HidesOtherPeoplesDeals_ButNotFromManagers()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        var rep = new User { TenantId = tenant.Id, FullName = "Rep One", Email = "rep@acme.test", PasswordHash = "hash", Role = AppRoles.FinanceUser, IsActive = true };
        dbContext.Users.Add(rep);
        await dbContext.SaveChangesAsync();
        var crm = CreateCrmController(dbContext, tenant.Id, admin);
        async Task<Guid> DealOwnedBy(string title, User owner)
        {
            var created = await crm.CreateOpportunity(Deal(title, 10_000m, "KES") with { OwnerUserId = owner.Id }, CancellationToken.None);
            var id = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;
            await crm.AddActivity(id, new CreateSalesActivityRequest("Call", $"Call about {title}", null, DateTime.UtcNow.AddDays(-1)), CancellationToken.None);
            return id;
        }
        var mine = await DealOwnedBy("Rep's deal", rep);
        var theirs = await DealOwnedBy("Admin's deal", admin);

        SalesCrmController As(params string[] permissions) =>
            CreateCrmController(dbContext, tenant.Id, rep, new ScopedTenantContext(tenant.Id, rep.Id, AppRoles.FinanceUser, permissions));
        var repView = As(PermissionNames.CrmView, PermissionNames.CrmManage);

        // Default: everyone sees the whole pipeline.
        Assert.Equal(2, Ok<IReadOnlyCollection<SalesOpportunityListItem>>(await repView.ListOpportunities(null, null, null, null, CancellationToken.None)).Count);

        Ok<SalesSettingsResponse>(await crm.UpdateSettings(new UpdateSalesSettingsRequest(null, "own", true), CancellationToken.None));
        repView = As(PermissionNames.CrmView, PermissionNames.CrmManage);

        Assert.Equal(mine, Assert.Single(Ok<IReadOnlyCollection<SalesOpportunityListItem>>(await repView.ListOpportunities(null, null, null, null, CancellationToken.None))).Id);
        Assert.Equal(mine, Assert.Single(Ok<IReadOnlyCollection<SalesFollowUpItem>>(await repView.ListFollowUps(null, CancellationToken.None))).OpportunityId);
        Assert.Equal(1, Ok<SalesPipelineSummary>(await repView.GetSummary(CancellationToken.None)).OpenCount);
        await Assert.ThrowsAsync<NotFoundException>(() => repView.GetOpportunity(theirs, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => repView.ChangeStage(theirs, new ChangeSalesStageRequest(SalesStages.Qualified, null), CancellationToken.None));
        var repWorkOrders = CreateWorkOrdersController(dbContext, tenant.Id, rep, new ScopedTenantContext(tenant.Id, rep.Id, AppRoles.FinanceUser, PermissionNames.CrmView, PermissionNames.CanCreateWorkOrders));
        await Assert.ThrowsAsync<NotFoundException>(() => repWorkOrders.CreateFromOpportunity(
            new CreateWorkOrderFromOpportunityRequest(theirs, null, null, null, null, null, null), CancellationToken.None));

        var repReports = new SalesReportsController(dbContext, new ScopedTenantContext(tenant.Id, rep.Id, AppRoles.FinanceUser, PermissionNames.CrmView), new NoOpUserAccessService());
        Assert.Equal(1, Ok<SalesReport>(await repReports.Get(null, null, admin.Id, CancellationToken.None)).OpenCount);

        // A sales manager still sees everything.
        var manager = As(PermissionNames.CrmView, PermissionNames.CrmManage, PermissionNames.CrmViewAll);
        Assert.Equal(2, Ok<IReadOnlyCollection<SalesOpportunityListItem>>(await manager.ListOpportunities(null, null, null, null, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task SystemCreatedDeals_GoToTheBestAvailableOwner()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        var accountManager = new User { TenantId = tenant.Id, FullName = "Account Manager", Email = "am@acme.test", PasswordHash = "hash", Role = AppRoles.Manager, IsActive = true };
        var salesLead = new User { TenantId = tenant.Id, FullName = "Sales Lead", Email = "lead@acme.test", PasswordHash = "hash", Role = AppRoles.Manager, IsActive = true };
        var leaver = new User { TenantId = tenant.Id, FullName = "Left the company", Email = "gone@acme.test", PasswordHash = "hash", Role = AppRoles.Manager, IsActive = false };
        var client = new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel" };
        var otherClient = new Client { TenantId = tenant.Id, ClientName = "Nyali Mall" };
        dbContext.AddRange(accountManager, salesLead, leaver, client, otherClient);
        dbContext.Contracts.Add(new Contract
        {
            TenantId = tenant.Id, ClientId = client.Id, ContractNumber = "CT-1", Name = "Chiller AMC", Status = ContractStatus.Active,
            StartDate = DateTime.UtcNow.AddMonths(-11), EndDate = DateTime.UtcNow.AddMonths(1), AccountManagerUserId = accountManager.Id,
        });
        await dbContext.SaveChangesAsync();
        var resolver = new SalesOwnerResolver(dbContext, new RolePermissionService(dbContext));

        // An inactive preferred owner is skipped; the client's contract account manager is next.
        Assert.Equal(accountManager.Id, (await resolver.ResolveAsync(tenant.Id, [leaver.Id], client.Id))!.Id);
        // No client history: falls back to an admin until a default sales owner is set.
        Assert.Equal(admin.Id, (await resolver.ResolveAsync(tenant.Id, [], otherClient.Id))!.Id);
        dbContext.TenantSalesSettings.Add(new TenantSalesSettings { TenantId = tenant.Id, DefaultOwnerUserId = salesLead.Id });
        await dbContext.SaveChangesAsync();
        Assert.Equal(salesLead.Id, (await resolver.ResolveAsync(tenant.Id, [], otherClient.Id))!.Id);
        // Whoever owns the client's live deal beats the default.
        dbContext.SalesOpportunities.Add(new SalesOpportunity { TenantId = tenant.Id, Title = "Open deal", ClientId = otherClient.Id, Stage = SalesStages.Proposal, OwnerUserId = accountManager.Id });
        await dbContext.SaveChangesAsync();
        Assert.Equal(accountManager.Id, (await resolver.ResolveAsync(tenant.Id, [], otherClient.Id))!.Id);
    }

    [Fact]
    public async Task Technician_FlagsALeadFromTheirJob_ForTheClientsSalesOwner()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        var techUser = new User { TenantId = tenant.Id, FullName = "Juma Fundi", Email = "juma@acme.test", PasswordHash = "hash", Role = AppRoles.Technician, IsActive = true };
        var technician = new Technician { TenantId = tenant.Id, UserId = techUser.Id, FullName = "Juma Fundi", Email = "juma@acme.test", IsActive = true };
        var client = new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel" };
        var asset = new Asset { TenantId = tenant.Id, ClientId = client.Id, AssetName = "Chiller 2", AssetCode = "CH-2" };
        var job = new WorkOrder { TenantId = tenant.Id, ClientId = client.Id, AssetId = asset.Id, WorkOrderNumber = "WO-0042", Title = "Chiller service", AssignedTechnicianId = technician.Id };
        var otherJob = new WorkOrder { TenantId = tenant.Id, ClientId = client.Id, WorkOrderNumber = "WO-0043", Title = "Generator PM" };
        dbContext.AddRange(techUser, technician, client, asset, job, otherJob);
        await dbContext.SaveChangesAsync();

        TechnicianSalesLeadsController Controller(string[] modules) => new(
            dbContext, new ScopedTenantContext(tenant.Id, techUser.Id, AppRoles.Technician), new NoOpUserAccessService(),
            new SalesReminderTests.FixedLicenseGuard(modules), new SalesOwnerResolver(dbContext, new RolePermissionService(dbContext)),
            new AuditLogService(dbContext, new HttpContextAccessor()));
        var withCrm = Controller([ServiceOpsPlans.Operations, ServiceOpsPlans.Finance]);

        var flagged = Ok<TechnicianSalesLeadResponse>(await withCrm.Flag(job.Id, new FlagSalesLeadRequest("Compressor near end of life, quote a replacement"), CancellationToken.None));
        Assert.Equal("Sales Admin", flagged.OwnerName);
        var lead = await dbContext.SalesOpportunities.SingleAsync(x => x.Id == flagged.Id);
        Assert.Equal((SalesStages.Lead, client.Id, job.Id, "Technician"), (lead.Stage, lead.ClientId, lead.SourceWorkOrderId, lead.Source));
        Assert.StartsWith("Chiller 2: Compressor", lead.Title);
        Assert.Contains(await dbContext.SalesActivities.Where(x => x.OpportunityId == lead.Id).ToListAsync(), x => x.DueAt != null && x.CompletedAt == null);

        // Only from their own jobs, only with something to say, only on a plan with the CRM.
        await Assert.ThrowsAsync<ForbiddenException>(() => withCrm.Flag(otherJob.Id, new FlagSalesLeadRequest("Generator is old"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => withCrm.Flag(job.Id, new FlagSalesLeadRequest("  "), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => Controller([ServiceOpsPlans.Operations]).Flag(job.Id, new FlagSalesLeadRequest("New pump"), CancellationToken.None));
    }

    /// <summary>Every CRM query on Postgres, so a query EF can only run in memory fails here rather than in production.</summary>
    [Fact]
    public async Task Postgres_CrmQueriesTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            var (tenant, admin) = await SeedAsync(dbContext);
            var rep = new User { TenantId = tenant.Id, FullName = "Rep One", Email = $"rep-{Guid.NewGuid():N}@acme.test", PasswordHash = "hash", Role = AppRoles.Manager, IsActive = true };
            var techUser = new User { TenantId = tenant.Id, FullName = "Juma Fundi", Email = $"juma-{Guid.NewGuid():N}@acme.test", PasswordHash = "hash", Role = AppRoles.Technician, IsActive = true };
            var technician = new Technician { TenantId = tenant.Id, UserId = techUser.Id, FullName = "Juma Fundi", Email = techUser.Email, IsActive = true };
            var client = new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel" };
            dbContext.AddRange(rep, techUser, technician, client);
            await dbContext.SaveChangesAsync();
            var job = new WorkOrder { TenantId = tenant.Id, ClientId = client.Id, WorkOrderNumber = "WO-0042", Title = "Chiller service", AssignedTechnicianId = technician.Id };
            var contract = new Contract
            {
                TenantId = tenant.Id, ClientId = client.Id, ContractNumber = "CT-1", Name = "Chiller AMC", Status = ContractStatus.ExpiringSoon,
                StartDate = DateTime.UtcNow.AddYears(-1), EndDate = DateTime.UtcNow.Date.AddDays(20), RenewalNoticeDays = 30, ContractValue = 900_000m, AccountManagerUserId = rep.Id,
            };
            dbContext.AddRange(job, contract);
            await dbContext.SaveChangesAsync();

            var crm = CreateCrmController(dbContext, tenant.Id, admin);
            var created = await crm.CreateOpportunity(Deal("Lift modernisation", 500_000m, "KES") with { ClientId = client.Id, CompanyName = null, OwnerUserId = rep.Id }, CancellationToken.None);
            var dealId = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(created.Result).Value).Id;
            await crm.AddActivity(dealId, new CreateSalesActivityRequest("Call", "Call procurement", null, DateTime.UtcNow.AddDays(-1)), CancellationToken.None);
            await crm.ConvertToQuotation(dealId, CancellationToken.None);
            var quote = await dbContext.TenantQuotations.SingleAsync(x => x.SalesOpportunityId == dealId);
            quote.Status = "Sent";
            quote.ValidUntil = DateTime.UtcNow.AddDays(1);
            dbContext.TenantQuotations.Add(new TenantQuotation
            {
                TenantId = tenant.Id, ClientId = client.Id, QuoteNumber = "QT-OTHER", Status = "Sent", QuoteDate = DateTime.UtcNow.AddDays(-5),
                ValidUntil = DateTime.UtcNow.AddDays(10), SentByUserId = admin.Id,
            });
            await dbContext.SaveChangesAsync();

            // Owners, renewals and reminders, then the technician's lead.
            Assert.NotNull(await new SalesOwnerResolver(dbContext, new RolePermissionService(dbContext)).ResolveAsync(tenant.Id, [Guid.NewGuid()], client.Id));
            var push = new SalesReminderTests.RecordingPushQueue();
            var run = await SalesReminderTests.CreateService(dbContext, push).RunAsync(DateTime.UtcNow);
            Assert.Equal(1, run.RenewalDealsOpened);
            Assert.True(run.QuoteReminders >= 1);
            contract.Status = ContractStatus.Renewed;
            await dbContext.SaveChangesAsync();
            Assert.Equal(1, (await SalesReminderTests.CreateService(dbContext, push).RunAsync(DateTime.UtcNow.AddHours(1))).RenewalDealsClosed);
            var leads = new TechnicianSalesLeadsController(
                dbContext, new ScopedTenantContext(tenant.Id, techUser.Id, AppRoles.Technician), new NoOpUserAccessService(),
                new SalesReminderTests.FixedLicenseGuard([ServiceOpsPlans.Operations, ServiceOpsPlans.Finance]), new SalesOwnerResolver(dbContext, new RolePermissionService(dbContext)),
                new AuditLogService(dbContext, new HttpContextAccessor()));
            Ok<TechnicianSalesLeadResponse>(await leads.Flag(job.Id, new FlagSalesLeadRequest("Compressor is tired"), CancellationToken.None));

            // Every list, both seeing everything and limited to one's own deals.
            Ok<SalesSettingsResponse>(await crm.UpdateSettings(new UpdateSalesSettingsRequest(rep.Id, "Own", true), CancellationToken.None));
            var repCrm = CreateCrmController(dbContext, tenant.Id, rep, new ScopedTenantContext(tenant.Id, rep.Id, AppRoles.Manager, PermissionNames.CrmView, PermissionNames.CrmManage));
            foreach (var view in new[] { crm, repCrm })
            {
                Ok<IReadOnlyCollection<SalesOpportunityListItem>>(await view.ListOpportunities("Open", "lift", null, client.Id, CancellationToken.None));
                Ok<SalesPipelineSummary>(await view.GetSummary(CancellationToken.None));
                Assert.NotEmpty(Ok<IReadOnlyCollection<SalesFollowUpItem>>(await view.ListFollowUps(rep.Id, CancellationToken.None)));
                Ok<IReadOnlyCollection<SalesQuoteAwaitingAnswer>>(await view.ListQuotesAwaitingAnswer(rep.Id, CancellationToken.None));
                Ok<SalesOpportunityDetailResponse>(await view.GetOpportunity(dealId, CancellationToken.None));
            }
            Assert.Equal(2, Ok<IReadOnlyCollection<SalesQuoteAwaitingAnswer>>(await crm.ListQuotesAwaitingAnswer(null, CancellationToken.None)).Count);
            var reports = new SalesReportsController(dbContext, new ScopedTenantContext(tenant.Id, rep.Id, AppRoles.Manager, PermissionNames.CrmView), new NoOpUserAccessService());
            Ok<SalesReport>(await reports.Get(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, null, CancellationToken.None));
            Ok<SalesSettingsResponse>(await crm.GetSettings(CancellationToken.None));

            // Answering the quote wins the deal.
            var quotes = new TenantQuotationController(
                dbContext, new FakeTenantContext(tenant.Id, admin.Id, admin.Email), new NoOpDocumentNumberingService(), null!,
                new AuditLogService(dbContext, new HttpContextAccessor()), new NoOpUserAccessService(), null!, null!, null!, null!, null!)
            {
                ControllerContext = CreateControllerContext(admin)
            };
            await quotes.AcceptQuotation(quote.Id, CancellationToken.None);
            Assert.Equal(SalesStages.Won, (await dbContext.SalesOpportunities.AsNoTracking().SingleAsync(x => x.Id == dealId)).Stage);
        });
    }

    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_sales_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var adminDb = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(adminDb, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
                await setup.Database.MigrateAsync();
            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(adminDb, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task ProspectDeals_ShareOneProspectClient_HiddenFromClientPickers()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        dbContext.Clients.Add(new Client { TenantId = tenant.Id, ClientName = "Kilele Hotel", IsActive = true });
        await dbContext.SaveChangesAsync();
        var crm = CreateCrmController(dbContext, tenant.Id, admin);
        async Task<SalesOpportunityDetailResponse> Create(string title, string company) =>
            Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(
                (await crm.CreateOpportunity(Deal(title, 1_000m, "KES") with { CompanyName = company }, CancellationToken.None)).Result).Value);

        var first = await Create("Solar", "Baraka Logistics");
        var second = await Create("Solar phase 2", "  baraka   LOGISTICS ");
        var existing = await Create("Chiller", "kilele hotel");
        Assert.Equal(first.ClientId, second.ClientId);
        Assert.True(first.ClientIsProspect);
        Assert.False(existing.ClientIsProspect);

        // A prospect shared with another deal is not renamed under it: the renamed deal gets its own.
        await crm.UpdateOpportunity(second.Id, Deal("Solar phase 2", 1_000m, "KES") with { CompanyName = "Baraka Freight" }, CancellationToken.None);
        Assert.Equal(3, await dbContext.Clients.CountAsync());
        // Renaming it again renames that prospect in place, leaving nothing behind.
        await crm.UpdateOpportunity(second.Id, Deal("Solar phase 2", 1_000m, "KES") with { CompanyName = "Baraka Group" }, CancellationToken.None);
        Assert.Equal(3, await dbContext.Clients.CountAsync());

        var clients = new ClientsController(dbContext, new FakeTenantContext(tenant.Id, admin.Id, admin.Email), new NoOpUserAccessService(), new AuditLogService(dbContext, new HttpContextAccessor()));
        IReadOnlyCollection<ClientResponse> List(string? kind) =>
            Assert.IsAssignableFrom<IReadOnlyCollection<ClientResponse>>(Assert.IsType<OkObjectResult>(clients.GetAll(null, "all", null, null, CancellationToken.None, kind).Result).Value);
        Assert.Equal(["Kilele Hotel"], List(null).Select(x => x.ClientName).ToArray());
        Assert.All(List("prospects"), x => Assert.True(x.IsProspect));
        Assert.Equal(2, List("prospects").Count);
        Assert.Equal(3, List("all").Count);

        // Making it a client (from the deal) promotes the prospect in place.
        await crm.ConvertToClient(first.Id, CancellationToken.None);
        Assert.Equal(2, List(null).Count);
    }

    [Fact]
    public async Task DealContacts_BecomeClientContacts_WithoutDuplicates()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        var crm = CreateCrmController(dbContext, tenant.Id, admin);
        async Task<SalesOpportunityDetailResponse> Create(string title, string contact, string? email) =>
            Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(
                (await crm.CreateOpportunity(Deal(title, 1_000m, "KES") with { CompanyName = "Baraka Logistics", ContactName = contact, ContactEmail = email }, CancellationToken.None)).Result).Value);

        var first = await Create("Solar", "Wanjiru Otieno", "Wanjiru@Baraka.test");
        // Same person, spelt differently: matched on email.
        var second = await Create("Solar phase 2", "Wanjiru O.", "wanjiru@baraka.test");
        var contact = Assert.Single(first.Contacts!);
        Assert.Equal(contact.Id, Assert.Single(second.Contacts!).Id);
        Assert.True(contact.IsPrimary);

        var withBuyer = Ok<SalesOpportunityDetailResponse>(await crm.AddContact(first.Id, new AddDealContactRequest(null, "Peter Kamau", null, "+254700000300", "Procurement lead", "procurement"), CancellationToken.None));
        Assert.Equal(2, withBuyer.Contacts!.Count);
        Assert.Contains(withBuyer.Contacts!, x => x.FullName == "Peter Kamau" && x.Role == "Procurement" && !x.IsPrimary);

        var contacts = new ClientContactsController(dbContext, new FakeTenantContext(tenant.Id, admin.Id, admin.Email), new NoOpUserAccessService());
        var list = Ok<IReadOnlyCollection<ClientContactResponse>>(await contacts.List(first.ClientId!.Value, CancellationToken.None));
        Assert.Equal(["Wanjiru Otieno", "Peter Kamau"], list.Select(x => x.FullName).ToArray());

        var afterRemove = Ok<SalesOpportunityDetailResponse>(await crm.RemoveContact(first.Id, contact.Id, CancellationToken.None));
        Assert.Equal("Peter Kamau", Assert.Single(afterRemove.Contacts!).FullName);
    }

    [Theory]
    [InlineData("Baraka Logistics Ltd.", "baraka logistics")]
    [InlineData("The Kilele Hotel (Kenya) Limited", "kilele hotel")]
    [InlineData("  ACME   Co ", "acme")]
    public void CompanyNames_CompareWithoutCaseOrLegalSuffixes(string name, string key) =>
        Assert.Equal(key, ClientMatching.NameKey(name));

    [Fact]
    public async Task PossibleDuplicates_AreFound_AndAProspectMergesIntoTheRealClient()
    {
        await using var dbContext = CreateDbContext();
        var (tenant, admin) = await SeedAsync(dbContext);
        var real = new Client { TenantId = tenant.Id, ClientName = "Baraka Logistics Limited", Email = "accounts@baraka.co.ke", Phone = "+254 700 000 200", IsActive = true };
        var other = new Client { TenantId = tenant.Id, ClientName = "Nyali Mall", IsActive = true };
        dbContext.AddRange(real, other);
        await dbContext.SaveChangesAsync();

        Assert.Equal(real.Id, Assert.Single(await ClientMatching.FindAsync(dbContext, tenant.Id, "baraka logistics", null, null, null, CancellationToken.None)).Id);
        Assert.Equal("Same email domain (baraka.co.ke)", Assert.Single(await ClientMatching.FindAsync(dbContext, tenant.Id, "BLL", "wanjiru@baraka.co.ke", null, null, CancellationToken.None)).Reason);
        Assert.Equal(real.Id, Assert.Single(await ClientMatching.FindAsync(dbContext, tenant.Id, null, null, "0700000200", null, CancellationToken.None)).Id);
        Assert.Empty(await ClientMatching.FindAsync(dbContext, tenant.Id, "Someone", "x@gmail.com", null, null, CancellationToken.None));

        // A prospect entered under another name, with a deal and a contact, folds into the real client.
        var crm = CreateCrmController(dbContext, tenant.Id, admin);
        var deal = Assert.IsType<SalesOpportunityDetailResponse>(Assert.IsType<CreatedAtActionResult>(
            (await crm.CreateOpportunity(Deal("Fleet depot solar", 1_000m, "KES") with { CompanyName = "BLL Group", ContactName = "Wanjiru Otieno" }, CancellationToken.None)).Result).Value);
        var prospectId = deal.ClientId!.Value;
        dbContext.ClientContacts.Add(new ClientContact { TenantId = tenant.Id, ClientId = real.Id, FullName = "wanjiru otieno", Phone = "0700" });
        await dbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => ClientMatching.MergeProspectAsync(dbContext, tenant.Id, real.Id, other.Id, CancellationToken.None));
        await ClientMatching.MergeProspectAsync(dbContext, tenant.Id, prospectId, real.Id, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.False(await dbContext.Clients.AnyAsync(x => x.Id == prospectId));
        var merged = Ok<SalesOpportunityDetailResponse>(await crm.GetOpportunity(deal.Id, CancellationToken.None));
        Assert.Equal(real.Id, merged.ClientId);
        // The same person already at the client is reused, not duplicated.
        Assert.Equal("wanjiru otieno", Assert.Single(merged.Contacts!).FullName);
        Assert.Single(await dbContext.ClientContacts.Where(x => x.ClientId == real.Id).ToListAsync());
    }

    private static UpsertSalesOpportunityRequest Deal(string title, decimal value, string currency) =>
        new(title, null, null, "Prospect Ltd", null, null, null, null, null, null, null, value, currency, null, null, null);

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsType<T>(Assert.IsType<OkObjectResult>(result.Result).Value, exactMatch: false);

    private static SalesTendersController CreateTendersController(AppDbContext dbContext, Guid tenantId, User actor, ITenantContext? tenantContext = null) =>
        new(
            dbContext,
            tenantContext ?? new FakeTenantContext(tenantId, actor.Id, actor.Email),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpLicenseGuardService(),
            new RolePermissionService(dbContext))
        {
            ControllerContext = CreateControllerContext(actor)
        };

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ecosys-sales-crm-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(Tenant Tenant, User Actor)> SeedAsync(AppDbContext dbContext)
    {
        var tenant = new Tenant
        {
            Name = "Acme",
            Slug = $"acme-{Guid.NewGuid():N}"[..12],
            CompanyName = "Acme Facilities",
            Email = "ops@acme.test",
            Country = "Kenya",
            Status = "Active",
            LicenseStatus = "Active",
            IsActive = true
        };
        var actor = new User
        {
            TenantId = tenant.Id,
            FullName = "Sales Admin",
            Email = $"sales-{Guid.NewGuid():N}@acme.test",
            PasswordHash = "hash",
            Role = AppRoles.Admin,
            JobTitle = "Admin",
            IsActive = true,
            HasAllBranchAccess = true
        };
        dbContext.AddRange(tenant, actor);
        await dbContext.SaveChangesAsync();
        return (tenant, actor);
    }

    private static ControllerContext CreateControllerContext(User actor) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                new Claim(ClaimTypes.Name, actor.FullName),
                new Claim(ClaimTypes.Email, actor.Email)
            ], "Test"))
        }
    };

    private static SalesCrmController CreateCrmController(AppDbContext dbContext, Guid tenantId, User actor, ITenantContext? tenantContext = null) =>
        new(
            dbContext,
            tenantContext ?? new FakeTenantContext(tenantId, actor.Id, actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpLicenseGuardService(),
            new RolePermissionService(dbContext))
        {
            ControllerContext = CreateControllerContext(actor)
        };

    private static WorkOrdersController CreateWorkOrdersController(AppDbContext dbContext, Guid tenantId, User actor, ITenantContext? tenantContext = null) =>
        new(
            dbContext,
            tenantContext ?? new FakeTenantContext(tenantId, actor.Id, actor.Email),
            new NoOpDocumentNumberingService(),
            new AuditLogService(dbContext, new HttpContextAccessor()),
            new NoOpUserAccessService(),
            new NoOpBranchAccessService(),
            new NoOpLicenseGuardService(),
            new NoOpWorkOrderLifecycleService(),
            new NoOpAssignmentWorkflowService(),
            new NoOpPmChecklistService(),
            new NoOpSlaService(),
            new NoOpWorkflowService())
        {
            ControllerContext = CreateControllerContext(actor)
        };

    /// <summary>A non-admin caller holding exactly the given capabilities, as the login claims would carry them.</summary>
    private sealed class ScopedTenantContext(Guid tenantId, Guid userId, string role, params string[] permissions) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => "staff@acme.test";
        public string? Role => role;
        public string? JobTitle => role;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => false;
        public bool HasRole(string candidate) => string.Equals(candidate, role, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => permissions.Contains(permissionName, StringComparer.OrdinalIgnoreCase);
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId, string email) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => Guid.NewGuid();
        public string? Email => email;
        public string? Role => AppRoles.Admin;
        public string? JobTitle => "Admin";
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => true;
        public bool HasRole(string role) => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);
        public bool HasPermission(string permissionName) => true;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => SessionId!.Value;
    }


    private sealed class NoOpWorkflowService : IWorkflowService
    {
        public Task<WorkflowDefinition?> GetActiveWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<WorkflowDefinition?>(null);
        public Task<IReadOnlyCollection<WorkflowStatus>> GetActiveWorkflowStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkflowStatus>>([]);
        public Task EnsureDefaultWorkflowAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkflowStatus> ApplyStatusTransitionAsync(WorkOrder workOrder, Guid targetWorkflowStatusId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpSlaService : ISlaService
    {
        public Task ApplyDefinitionAsync(WorkOrder workOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> RefreshWorkOrderAsync(Guid workOrderId, DateTime nowUtc, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private sealed class NoOpUserAccessService : IUserAccessService
    {
        public void EnsureAdmin() { }
        public void EnsureAdminOrPermission(string permissionName) { }
        public void EnsureAdminOrAnyPermission(params string[] permissionNames) { }
        public void EnsureTenantOperationalAccess() { }
    }

    private sealed class NoOpBranchAccessService : IBranchAccessService
    {
        public Task EnsureCanAccessBranchAsync(Guid tenantId, Guid branchId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<Guid>> GetAccessibleBranchIdsAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
        public Task<BranchQueryScope> GetQueryScopeAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BranchQueryScope(false, requestedBranchId, [], true));
        public Task<Guid?> ResolveBranchIdForWriteAsync(Guid tenantId, Guid? requestedBranchId, CancellationToken cancellationToken = default) =>
            Task.FromResult(requestedBranchId);
    }

    private sealed class NoOpDocumentNumberingService : IDocumentNumberingService
    {
        public Task<string> GenerateAsync(Guid tenantId, Guid? branchId, string documentType, CancellationToken cancellationToken = default) =>
            Task.FromResult("WO-TEST-001");

        public Task<NumberingSetting> UpsertAsync(Guid tenantId, Guid? branchId, string documentType, string prefix, long nextNumber, int paddingLength, string resetFrequency, bool includeYear, bool includeMonth, bool isActive, CancellationToken cancellationToken = default, string? suffix = null, string yearFormat = "YYYY", string separator = "-", bool isLocked = false) =>
            throw new NotSupportedException();
    }

    private sealed class NoOpLicenseGuardService : ILicenseGuardService
    {
        public Task<TenantLicenseSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LicenseUsageSnapshot> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PlatformLicenseUsageSnapshot>> GetPlatformUsageAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<PlatformLicenseUsageSnapshot>>([]);
        public Task EnsureTenantCanMutateAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureFeatureEnabledAsync(Guid tenantId, string featureName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TenantLicense> GetOrCreateTenantLicenseAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpWorkOrderLifecycleService : IWorkOrderLifecycleService
    {
        public Task<WorkOrderEvent> AddCommentAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCommentRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ArriveAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> AssignAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, Guid? technicianId, Guid? assignmentGroupId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ChangeStatusAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderStatusChangeRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> CompleteAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, WorkOrderCompletionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> DepartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, Ecosys.Infrastructure.Services.WorkOrderLocationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WorkOrderEvent>> GetEventsAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderEvent>>([]);
        public string NormalizeStatus(string status) => WorkOrderStatuses.Normalize(status);
        public Task RecordCreatedAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, string workOrderNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkOrder> StartAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> HoldAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, string? holdType, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrder> ResumeAsync(Guid tenantId, Guid workOrderId, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpAssignmentWorkflowService : IWorkOrderAssignmentWorkflowService
    {
        public Task ApplyAssignmentsAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, WorkOrderAssignmentUpdateRequest request, bool isReassignment, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<WorkOrderAssignmentHistory>> GetHistoryAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<WorkOrderAssignmentHistory>>([]);
        public Task<bool> HasAcceptedTechnicianAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> HasArrivalAsync(Guid tenantId, Guid workOrderId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RecordArrivalAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianArrivalRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordDepartureAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDepartureRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordInTransitAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianInTransitRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RecordTechnicianResponseAsync(Guid tenantId, Guid workOrderId, Guid? actorUserId, TechnicianDispatchResponseRequest request, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpPmChecklistService : IPmWorkOrderChecklistService
    {
        public Task<int> AttachPmTemplateToWorkOrderAsync(Guid workOrderId, Guid pmTemplateId, Guid tenantId, Guid? actorUserId = null, bool replaceExisting = false, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task EnsureRequiredChecklistCompletedAsync(Guid workOrderId, Guid tenantId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PmTemplate> GetActiveTemplateAsync(Guid tenantId, Guid pmTemplateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WorkOrderChecklistItem> UpdateChecklistItemAsync(Guid workOrderId, Guid itemId, Guid tenantId, Guid actorUserId, string? responseValue, string? remarks, bool isCompleted, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
