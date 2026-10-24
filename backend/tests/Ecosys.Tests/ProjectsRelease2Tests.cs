using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests;

/// <summary>Release 2: muster roll, rates, valuations, retention, withholding, financials and reminders.</summary>
public sealed partial class ProjectsModuleTests
{
    private static readonly DateOnly SiteToday = new(2026, 10, 21);

    // ── Licensing ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ProjectSite", "/api/project-site/123/muster", "projects")]
    [InlineData("ProjectCommercial", "/api/projects/123/valuations", "projects")]
    [InlineData("ProjectCommercial", "/api/projects/123/wage-sheet", "projects")]
    [InlineData("PortalProjects", "/api/portal/projects/123/valuations", "projects")]
    [InlineData("Projects", "/api/projects/from-quotation/123", "projects")]
    [InlineData("Projects", "/api/projects/123/create-maintenance-contract", "projects")]
    [InlineData("WithholdingCertificates", "/api/invoices/123/withholding-certificates", "finance")]
    public void Release2Routes_RequireTheirModules(string controller, string path, string expected)
    {
        Assert.True(ServiceOpsPlans.IsClassified(controller));
        Assert.Equal(expected.Split(','), ServiceOpsPlans.RequiredModules(controller, path));
    }

    // ── Calculations ───────────────────────────────────────────────────────────

    [Fact]
    public void Labour_UsesTheRateInEffectOnEachDay_AndLeavesUnratedDaysUncosted()
    {
        var worker = Guid.NewGuid();
        var entries = new[]
        {
            Mark(worker, new DateOnly(2026, 10, 1), ProjectMusterStatuses.Present),  // before any rate
            Mark(worker, new DateOnly(2026, 10, 5), ProjectMusterStatuses.Present),  // 800
            Mark(worker, new DateOnly(2026, 10, 6), ProjectMusterStatuses.HalfDay),  // 400
            Mark(worker, new DateOnly(2026, 10, 12), ProjectMusterStatuses.Present), // 1000
            Mark(worker, new DateOnly(2026, 10, 13), ProjectMusterStatuses.Absent),
        };
        var rates = new[]
        {
            new ProjectWorkerRate { ProjectWorkerId = worker, EffectiveFrom = new DateOnly(2026, 10, 5), DailyRate = 800 },
            new ProjectWorkerRate { ProjectWorkerId = worker, EffectiveFrom = new DateOnly(2026, 10, 10), DailyRate = 1000 },
        };

        var labour = Assert.Single(ProjectLabour.Summarize(entries, rates));

        Assert.Equal(3.5m, labour.ManDays);
        Assert.Equal(1m, labour.UncostedDays);
        Assert.Equal(2200m, labour.Cost);
    }

    [Fact]
    public void Valuation_CapsRetention_RecoversTheAdvanceFromWorkOnly_AndChainsPreviousCertificates()
    {
        var project = new Project { RetentionPercent = 10, RetentionCapAmount = 50_000, AdvancePaymentAmount = 100_000, AdvanceRecoveryPercent = 20 };

        var first = ProjectValuationCalculator.Compute(project, 300_000, 50_000, null, 0);
        Assert.Equal(350_000, first.GrossValueToDate);
        Assert.Equal(35_000, first.RetentionToDate);
        Assert.Equal(60_000, first.AdvanceRecoveredToDate);      // 20% of 300k work; materials on site excluded
        Assert.Equal(255_000, first.AmountDue);

        // The certificate came back lower than calculated; the next valuation chains from what was certified.
        var certifiedFirst = new ProjectValuation { WorkDoneToDate = 300_000, AdvanceRecoveredToDate = 60_000, CertifiedAmount = 250_000 };
        var second = ProjectValuationCalculator.Compute(project, 600_000, 0, certifiedFirst, 250_000);
        Assert.Equal(50_000, second.RetentionToDate);              // 60k capped at 50k
        Assert.Equal(100_000, second.AdvanceRecoveredToDate);      // 60k + min(60k, 40k outstanding)
        Assert.Equal(250_000, second.PreviouslyCertified);
        Assert.Equal(600_000 - 50_000 - 100_000 - 250_000, second.AmountDue);
    }

    [Theory]
    [InlineData(50, 5_000, 5_000)]
    [InlineData(0, 0, 10_000)]
    [InlineData(100, 10_000, 0)]
    public void Retention_IsReleasedInTwoParts(decimal completionShare, decimal completionDue, decimal finalDue)
    {
        var project = new Project
        {
            RetentionPercent = 10, RetentionReleaseAtCompletionPercent = completionShare, DefectsLiabilityMonths = 6,
            HandedOverAt = DateTime.UtcNow.AddMonths(-7),
        };
        var valuations = new[] { new ProjectValuation { Status = ProjectValuationStatuses.Invoiced, RetentionToDate = 10_000, PeriodEnd = SiteToday } };

        var position = ProjectFinancialsService.RetentionPosition(project, valuations, []);
        Assert.Equal(10_000, position.Held);
        Assert.Equal(completionDue, position.CompletionDue);
        Assert.Equal(finalDue, position.FinalDue);

        // After the completion part is released, only the balance is left, and never more than is held.
        var released = new[] { new ProjectRetentionRelease { Stage = ProjectRetentionStages.PracticalCompletion, Amount = completionDue } };
        var after = ProjectFinancialsService.RetentionPosition(project, valuations, completionDue > 0 ? released : []);
        Assert.Equal(10_000 - completionDue, after.Held);
        Assert.Equal(0m, after.CompletionDue);
        Assert.True(after.CompletionDue + after.FinalDue <= after.Held);
    }

    [Fact]
    public void Retention_IsNotDueBeforeHandover_AndTheBalanceWaitsForTheDefectsPeriod()
    {
        var valuations = new[] { new ProjectValuation { Status = ProjectValuationStatuses.Certified, RetentionToDate = 10_000, PeriodEnd = SiteToday } };
        var notHandedOver = new Project { RetentionPercent = 10, DefectsLiabilityMonths = 12 };
        Assert.Equal((10_000m, 0m, 0m), ProjectFinancialsService.RetentionPosition(notHandedOver, valuations, []));

        var justHandedOver = new Project { RetentionPercent = 10, DefectsLiabilityMonths = 12, HandedOverAt = DateTime.UtcNow.AddDays(-1) };
        Assert.Equal((10_000m, 5_000m, 0m), ProjectFinancialsService.RetentionPosition(justHandedOver, valuations, []));
    }

    // ── Muster roll ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Muster_SavesOneMarkPerWorkerPerDay_AndReplaysAreHarmless()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        await MoveToAsync(harness, project.Summary.Id, ProjectStageCategories.Active);
        var site = SiteController(harness, harness.TenantContext);
        var worker = Ok(await site.AddWorker(project.Summary.Id, new UpsertProjectWorkerRequest("Peter Otieno", "0712000000", "Mason", null, null), CancellationToken.None));

        var request = new SaveMusterRequest([new SaveMusterMark(worker.Id, "Present", null)]);
        Ok(await site.SaveMuster(project.Summary.Id, SiteToday, request, CancellationToken.None));
        var replay = Ok(await site.SaveMuster(project.Summary.Id, SiteToday, request, CancellationToken.None));
        var changed = Ok(await site.SaveMuster(project.Summary.Id, SiteToday, new SaveMusterRequest([new SaveMusterMark(worker.Id, "HalfDay", null)]), CancellationToken.None));

        Assert.Equal(1m, replay.ManDays);
        Assert.Equal(0.5m, changed.ManDays);
        Assert.Equal(1, await harness.Db.ProjectMusterEntries.CountAsync());
        await Assert.ThrowsAsync<BusinessRuleException>(() => site.SaveMuster(project.Summary.Id, SiteToday.AddDays(1), request, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => site.SaveMuster(project.Summary.Id, SiteToday,
            new SaveMusterRequest([new SaveMusterMark(worker.Id, "Present", null), new SaveMusterMark(worker.Id, "Absent", null)]), CancellationToken.None));
    }

    [Fact]
    public async Task Muster_IsMarkedOnlyByTheProjectsSupervisors_AndLocksAfterTheCorrectionWindow()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var worker = Ok(await SiteController(harness, harness.TenantContext).AddWorker(project.Summary.Id,
            new UpsertProjectWorkerRequest("Mary Wanjiru", null, "Electrician", null, null), CancellationToken.None));

        var supervisorId = Guid.NewGuid();
        harness.Db.Users.Add(new User { Id = supervisorId, TenantId = harness.TenantId, FullName = "Site Supervisor", Email = "super@acme.test", Role = AppRoles.Technician, IsActive = true });
        await harness.Db.SaveChangesAsync();
        var asSupervisor = SiteController(harness, Context(harness, supervisorId, AppRoles.Technician, PermissionNames.ProjectsMusterMark));
        var marks = new SaveMusterRequest([new SaveMusterMark(worker.Id, "Present", null)]);

        // Not on the team yet: the project stays hidden.
        await Assert.ThrowsAsync<NotFoundException>(() => asSupervisor.SaveMuster(project.Summary.Id, SiteToday, marks, CancellationToken.None));

        Ok(await harness.Controller.AddMember(project.Summary.Id, new UpsertProjectMemberRequest(supervisorId, null, ProjectMemberRoles.Supervisor, null, null, null), CancellationToken.None));
        Ok(await asSupervisor.SaveMuster(project.Summary.Id, SiteToday, marks, CancellationToken.None));
        Ok(await asSupervisor.SaveMuster(project.Summary.Id, SiteToday.AddDays(-1), marks, CancellationToken.None));   // within the 1-day window
        await Assert.ThrowsAsync<BusinessRuleException>(() => asSupervisor.SaveMuster(project.Summary.Id, SiteToday.AddDays(-2), marks, CancellationToken.None));

        // A project manager can still correct a locked day.
        var sheet = Ok(await SiteController(harness, harness.TenantContext).SaveMuster(project.Summary.Id, SiteToday.AddDays(-2), marks, CancellationToken.None));
        Assert.True(sheet.IsLocked);
    }

    [Fact]
    public void MusterAndRosterResponses_CarryNoPayRates()
    {
        foreach (var type in new[] { typeof(MusterRowResponse), typeof(MusterDaySheetResponse), typeof(ProjectWorkerResponse), typeof(UpsertProjectWorkerRequest), typeof(SaveMusterMark), typeof(MySiteResponse) })
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.Contains("Rate", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Muster_SuggestsPresentForARosteredTechnicianWhoClockedInOnTheProject_WithoutCountingThemTwice()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var technician = new Technician { TenantId = harness.TenantId, FullName = "Tech One", Email = "tech@acme.test", IsActive = true };
        harness.Db.Technicians.Add(technician);
        await harness.Db.SaveChangesAsync();
        var site = SiteController(harness, harness.TenantContext);
        var worker = Ok(await site.AddWorker(project.Summary.Id, new UpsertProjectWorkerRequest("Tech One", null, null, "Permanent", technician.Id), CancellationToken.None));
        harness.Db.AttendanceEntries.Add(new AttendanceEntry { TenantId = harness.TenantId, TechnicianId = technician.Id, ProjectId = project.Summary.Id, WorkDate = SiteToday, ClockInAt = DateTime.UtcNow, Status = "ClockedIn" });
        await harness.Db.SaveChangesAsync();

        var sheet = Ok(await site.GetMuster(project.Summary.Id, SiteToday, CancellationToken.None));
        var row = Assert.Single(sheet.Rows);
        Assert.Equal((worker.Id, "Present", true), (row.WorkerId, row.Status, row.IsSuggestion));
        Assert.Equal(0m, sheet.ManDays); // a suggestion isn't a mark until the supervisor submits it

        await Assert.ThrowsAsync<BusinessRuleException>(() => site.AddWorker(project.Summary.Id, new UpsertProjectWorkerRequest("Tech One again", null, null, null, technician.Id), CancellationToken.None));
    }

    // ── Valuations ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Valuation_FlowsFromDraftToInvoice_AndAnInvoicedValuationCantBeVoided()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync(originalValue: 1_000_000);
        var notifications = new NoOpProjectNotifications();
        var commercial = CommercialController(harness, harness.TenantContext, notifications);
        var id = project.Summary.Id;
        Ok(await commercial.UpdateBillingTerms(id, new UpdateBillingTermsRequest(10, null, 50, 6, null, null), CancellationToken.None));

        var draft = Ok(await commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday, 400_000, 0, null), CancellationToken.None));
        Assert.Equal((40_000m, 360_000m), (draft.RetentionToDate, draft.AmountDue));
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday, 1, 0, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.CertifyValuation(id, draft.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, 350_000), CancellationToken.None));

        Ok(await commercial.SubmitValuation(id, draft.Id, CancellationToken.None));
        var certified = Ok(await commercial.CertifyValuation(id, draft.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, 350_000), CancellationToken.None));
        Assert.Equal(350_000m, certified.CertifiedAmount);
        Assert.Contains(notifications.Sent, x => x.Key == ProjectNotificationKeys.ValuationCertified);

        // Retention and advance terms are fixed from the first certificate on.
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.UpdateBillingTerms(id, new UpdateBillingTermsRequest(5, null, 50, 6, null, null), CancellationToken.None));

        var invoiced = Ok(await commercial.RaiseValuationInvoice(id, draft.Id, CancellationToken.None));
        var invoice = await harness.Db.TenantInvoices.SingleAsync(x => x.Id == invoiced.InvoiceId);
        Assert.Equal((350_000m, id, (Guid?)draft.Id, "Draft"), (invoice.Subtotal, invoice.ProjectId!.Value, invoice.ProjectValuationId, invoice.Status));
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.RaiseValuationInvoice(id, draft.Id, CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.VoidValuation(id, draft.Id, new VoidValuationRequest("Wrong figure"), CancellationToken.None));

        // The next valuation chains from the certified figure, not the calculated one.
        var next = Ok(await commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday.AddDays(30), 600_000, 0, null), CancellationToken.None));
        Assert.Equal(350_000m, next.PreviouslyCertified);
        Assert.Equal(600_000m - 60_000m - 350_000m, next.AmountDue);
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday, 700_000, 0, null), CancellationToken.None));

        // Once its invoice is voided, the first valuation can be invoiced again.
        invoice.Status = "Void";
        await harness.Db.SaveChangesAsync();
        Ok(await commercial.VoidValuation(id, next.Id, new VoidValuationRequest("Superseded"), CancellationToken.None));
        var reraised = Ok(await commercial.RaiseValuationInvoice(id, draft.Id, CancellationToken.None));
        Assert.NotEqual(invoice.Id, reraised.InvoiceId);
    }

    [Fact]
    public async Task RetentionRelease_WaitsForHandover_InvoicesEachPartOnce()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync(originalValue: 1_000_000);
        var commercial = CommercialController(harness, harness.TenantContext);
        var id = project.Summary.Id;
        Ok(await commercial.UpdateBillingTerms(id, new UpdateBillingTermsRequest(10, null, 50, 12, null, null), CancellationToken.None));
        var valuation = Ok(await commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday, 1_000_000, 0, null), CancellationToken.None));
        Ok(await commercial.SubmitValuation(id, valuation.Id, CancellationToken.None));
        Ok(await commercial.CertifyValuation(id, valuation.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, 900_000), CancellationToken.None));

        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.ReleaseRetention(id, new ReleaseRetentionRequest("PracticalCompletion"), CancellationToken.None));
        await MoveToAsync(harness, id, ProjectStageCategories.Completed);

        var released = Ok(await commercial.ReleaseRetention(id, new ReleaseRetentionRequest("PracticalCompletion"), CancellationToken.None));
        Assert.Equal((50_000m, 0m, 0m), (released.Held, released.CompletionDue, released.FinalDue));
        Assert.Equal(50_000m, Assert.Single(released.Releases).Amount);
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.ReleaseRetention(id, new ReleaseRetentionRequest("PracticalCompletion"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => commercial.ReleaseRetention(id, new ReleaseRetentionRequest("EndOfDefectsLiability"), CancellationToken.None));
    }

    [Fact]
    public async Task Commercial_NeedsTheFinancialsPermissions()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var viewerOnly = CommercialController(harness, Context(harness, harness.ViewerUserId, AppRoles.Viewer, PermissionNames.ProjectsView));
        var reader = CommercialController(harness, Context(harness, harness.ViewerUserId, AppRoles.FinanceUser, PermissionNames.ProjectsView, PermissionNames.ProjectsFinancialsView));

        await Assert.ThrowsAsync<ForbiddenException>(() => viewerOnly.GetFinancials(project.Summary.Id, CancellationToken.None));
        Ok(await reader.GetFinancials(project.Summary.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() => reader.CreateCostEntry(project.Summary.Id, new UpsertCostEntryRequest("Equipment", "Scaffold hire", 5_000, SiteToday), CancellationToken.None));
    }

    // ── Financials ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Financials_AddUpEachCostFromItsSource()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync(originalValue: 1_000_000);
        var id = project.Summary.Id;
        var commercial = CommercialController(harness, harness.TenantContext);
        var db = harness.Db;

        var issue = new MaterialIssue { TenantId = harness.TenantId, ProjectId = id, IssueNumber = "MI-1", IssueStatus = "issued", TotalIssuedCost = 100_000 };
        issue.Lines.Add(new MaterialIssueLine { TenantId = harness.TenantId, QuantityIssued = 10, QuantityReturned = 2, UnitCost = 10_000 });
        db.MaterialIssues.AddRange(issue,
            new MaterialIssue { TenantId = harness.TenantId, ProjectId = id, IssueNumber = "MI-2", IssueStatus = "draft", TotalIssuedCost = 999_999 });
        var worker = new ProjectWorker { TenantId = harness.TenantId, ProjectId = id, FullName = "Casual" };
        db.ProjectWorkers.Add(worker);
        db.ProjectWorkerRates.Add(new ProjectWorkerRate { TenantId = harness.TenantId, ProjectWorkerId = worker.Id, EffectiveFrom = SiteToday.AddDays(-10), DailyRate = 1_000 });
        db.ProjectMusterEntries.AddRange(
            Mark(worker.Id, SiteToday.AddDays(-11), ProjectMusterStatuses.Present, harness.TenantId, id),  // uncosted
            Mark(worker.Id, SiteToday.AddDays(-2), ProjectMusterStatuses.Present, harness.TenantId, id),
            Mark(worker.Id, SiteToday.AddDays(-1), ProjectMusterStatuses.HalfDay, harness.TenantId, id));
        db.SupplierBills.Add(new SupplierBill { TenantId = harness.TenantId, ProjectId = id, BillNumber = "B-1", Amount = 40_000, Status = "Open" });
        db.Expenses.AddRange(
            new Expense { TenantId = harness.TenantId, ProjectId = id, ClaimNumber = "E-1", Amount = 3_000, Status = "Approved", Category = "Transport" },
            new Expense { TenantId = harness.TenantId, ProjectId = id, ClaimNumber = "E-2", Amount = 9_000, Status = "Submitted", Category = "Transport" });
        db.TenantInvoices.Add(new TenantInvoice
        {
            TenantId = harness.TenantId, ClientId = harness.ClientId, ProjectId = id, InvoiceNumber = "INV-1", DocumentType = "Invoice", Status = "Sent",
            Subtotal = 200_000, TaxAmount = 32_000, TotalAmount = 232_000, Balance = 232_000, DueDate = DateTime.UtcNow.AddDays(-1), Currency = "KES",
        });
        await db.SaveChangesAsync();
        Ok(await commercial.CreateCostEntry(id, new UpsertCostEntryRequest("Equipment", "Scaffold hire", 5_000, SiteToday), CancellationToken.None));
        Ok(await commercial.CreateBudgetLine(id, new UpsertBudgetLineRequest("Materials", null, 150_000), CancellationToken.None));

        var financials = Ok(await commercial.GetFinancials(id, CancellationToken.None));
        decimal Actual(string category) => financials.Categories.Single(x => x.Category == category).Actual;

        Assert.Equal(80_000m, Actual(ProjectCostCategories.Materials));    // 100k issued less 20k returned; the draft issue isn't a cost
        Assert.Equal(1_500m, Actual(ProjectCostCategories.Labour));        // 1 + 0.5 days at 1,000
        Assert.Equal(40_000m, Actual(ProjectCostCategories.Subcontractor));
        Assert.Equal(5_000m, Actual(ProjectCostCategories.Equipment));
        Assert.Equal(3_000m, Actual(ProjectCostCategories.Other));         // approved claims only
        Assert.Equal(129_500m, financials.ActualCost);
        Assert.Equal((2.5m, 1m), (financials.ManDays, financials.UncostedDays));
        Assert.Equal(150_000m, financials.BudgetTotal);
        Assert.Equal((200_000m, 232_000m, 232_000m), (financials.Invoiced, financials.Outstanding, financials.Overdue));
        Assert.Equal(87.05m, financials.IndicativeMarginPercent);
    }

    // ── Withholding ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Withholding_SettlesPartOfTheInvoice_AndReversingRestoresTheBalance()
    {
        var harness = await Harness.CreateAsync();
        var invoice = new TenantInvoice
        {
            TenantId = harness.TenantId, ClientId = harness.ClientId, InvoiceNumber = "INV-9", DocumentType = "Invoice", Status = "Sent",
            Subtotal = 100_000, TaxAmount = 16_000, TotalAmount = 116_000, Balance = 116_000, DueDate = DateTime.UtcNow.AddDays(30), Currency = "KES",
        };
        harness.Db.TenantInvoices.Add(invoice);
        await harness.Db.SaveChangesAsync();
        var controller = new WithholdingCertificatesController(harness.Db, harness.TenantContext, new UserAccessService(harness.TenantContext), new NoOpAuditLogService());

        var certificate = Ok(await controller.Record(invoice.Id, new RecordWithholdingRequest("VAT", 2, 2_000, "KRA-0001", SiteToday, null), CancellationToken.None));
        Assert.Equal(114_000m, (await harness.Db.TenantInvoices.SingleAsync(x => x.Id == invoice.Id)).Balance);

        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Record(invoice.Id, new RecordWithholdingRequest("VAT", 2, 2_000, "KRA-0001", SiteToday, null), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => controller.Record(invoice.Id, new RecordWithholdingRequest("IncomeTax", 5, 200_000, "KRA-0002", SiteToday, null), CancellationToken.None));

        Ok(await controller.Reverse(certificate.Id, new ReverseWithholdingRequest("Issued in error"), CancellationToken.None));
        Assert.Equal(116_000m, (await harness.Db.TenantInvoices.SingleAsync(x => x.Id == invoice.Id)).Balance);
    }

    // ── Award, handover, closure ────────────────────────────────────────────────

    [Fact]
    public async Task AcceptedQuotation_IsAwardedAsAProjectOnce()
    {
        var harness = await Harness.CreateAsync();
        var quote = new TenantQuotation { TenantId = harness.TenantId, ClientId = harness.ClientId, QuoteNumber = "QT-1", Status = "Sent", Subject = "Chiller replacement", Subtotal = 2_500_000, TaxAmount = 400_000, TotalAmount = 2_900_000 };
        harness.Db.TenantQuotations.Add(quote);
        await harness.Db.SaveChangesAsync();
        var request = new AwardFromQuotationRequest(null, null, harness.ManagerUserId, null, null, null, null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.AwardFromQuotation(quote.Id, request, CancellationToken.None));
        quote.Status = "Accepted";
        await harness.Db.SaveChangesAsync();

        var project = Ok(await harness.Controller.AwardFromQuotation(quote.Id, request, CancellationToken.None));
        Assert.Equal(("Chiller replacement", 2_500_000m), (project.Summary.Name, project.Summary.OriginalValue));
        Assert.Equal((project.Summary.Id, "Converted"), (quote.ConvertedToProjectId!.Value, quote.Status));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.AwardFromQuotation(quote.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task Closing_IsBlockedByAnUninvoicedCertificate()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var commercial = CommercialController(harness, harness.TenantContext);
        var valuation = Ok(await commercial.CreateValuation(project.Summary.Id, new UpsertValuationRequest(SiteToday, 100_000, 0, null), CancellationToken.None));
        var closed = await harness.Db.ProjectStages.SingleAsync(x => x.Category == ProjectStageCategories.Closed);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Done"), CancellationToken.None));
        Assert.Contains("valuation is still in progress", error.Message);

        Ok(await commercial.SubmitValuation(project.Summary.Id, valuation.Id, CancellationToken.None));
        Ok(await commercial.CertifyValuation(project.Summary.Id, valuation.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, 100_000), CancellationToken.None));
        error = await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Done"), CancellationToken.None));
        Assert.Contains("hasn't been invoiced", error.Message);

        Ok(await commercial.RaiseValuationInvoice(project.Summary.Id, valuation.Id, CancellationToken.None));
        Ok(await harness.Controller.ChangeStage(project.Summary.Id, new ChangeProjectStageRequest(closed.Id, "Done"), CancellationToken.None));
    }

    [Fact]
    public async Task ProjectOnAnExistingRecord_MustBeOpenAndBelongToTheSameClient()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();

        Assert.Equal(project.Summary.Id, await ProjectLinks.ResolveAsync(harness.Db, harness.TenantId, project.Summary.Id, harness.ClientId));
        Assert.Null(await ProjectLinks.ResolveAsync(harness.Db, harness.TenantId, null, harness.ClientId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ProjectLinks.ResolveAsync(harness.Db, harness.TenantId, project.Summary.Id, harness.OtherClientId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => ProjectLinks.ResolveAsync(harness.Db, harness.OtherTenantId, project.Summary.Id, null));
    }

    // ── Reminders ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reminders_GoOutOncePerEntityAndDate()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.CreateProjectAsync();
        var plan = new LicensePlan { PlanCode = ServiceOpsPlans.Professional, DisplayName = "Professional" };
        harness.Db.Tenants.Add(new Tenant { Id = harness.TenantId, Name = "Acme", CompanyName = "Acme", Slug = "acme-r2", Country = "Kenya", IsActive = true });
        harness.Db.LicensePlans.Add(plan);
        harness.Db.TenantLicenses.Add(new TenantLicense { TenantId = harness.TenantId, LicensePlanId = plan.Id, LicensePlan = plan, Status = "Active" });
        harness.Db.ProjectMilestones.Add(new ProjectMilestone { TenantId = harness.TenantId, ProjectId = project.Summary.Id, Name = "Roof slab", PlannedDate = SiteToday.AddDays(-3) });
        harness.Db.ProjectMilestones.Add(new ProjectMilestone { TenantId = harness.TenantId, ProjectId = project.Summary.Id, Name = "Handover", PlannedDate = SiteToday.AddDays(2) });
        harness.Db.ProjectMilestones.Add(new ProjectMilestone { TenantId = harness.TenantId, ProjectId = project.Summary.Id, Name = "Far off", PlannedDate = SiteToday.AddDays(30) });
        await harness.Db.SaveChangesAsync();

        var notifications = new NoOpProjectNotifications();
        var settings = new ProjectSettingsService(harness.Db);
        var runner = new ProjectReminderRunner(harness.Db, new LicenseGuardService(harness.Db), settings, new ProjectFinancialsService(harness.Db), notifications,
            new FixedTimeZone(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectReminderRunner>.Instance);
        var at = new DateTime(2026, 10, 21, 6, 0, 0, DateTimeKind.Utc);

        Assert.Equal(2, await runner.RunAsync(at));
        Assert.Equal(0, await runner.RunAsync(at.AddHours(1)));
        Assert.Equal([ProjectNotificationKeys.MilestoneDue, ProjectNotificationKeys.MilestoneOverdue], notifications.Sent.Select(x => x.Key).Order().ToArray());
        Assert.All(notifications.Sent, x => Assert.Contains(harness.ManagerUserId, x.UserIds));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ProjectMusterEntry Mark(Guid workerId, DateOnly date, string status, Guid tenantId = default, Guid projectId = default) => new()
    {
        TenantId = tenantId, ProjectId = projectId, ProjectWorkerId = workerId, WorkDate = date, Status = status, DayFraction = ProjectMusterStatuses.DayFraction(status),
    };

    private static FakeTenantContext Context(Harness harness, Guid userId, string role, params string[] permissions) =>
        new(harness.TenantId, userId, role, isAdmin: false, permissions);

    private static async Task MoveToAsync(Harness harness, Guid projectId, string category)
    {
        var stage = await harness.Db.ProjectStages.SingleAsync(x => x.Category == category);
        Ok(await harness.Controller.ChangeStage(projectId, new ChangeProjectStageRequest(stage.Id, null), CancellationToken.None));
    }

    private static ProjectSiteController SiteController(Harness harness, ITenantContext context)
    {
        var settings = new ProjectSettingsService(harness.Db);
        return new ProjectSiteController(harness.Db, context, new ProjectAccessService(harness.Db, context, settings), settings, new FixedTimeZone(), new NoOpAuditLogService());
    }

    private static ProjectCommercialController CommercialController(Harness harness, ITenantContext context, IProjectNotificationService? notifications = null)
    {
        var settings = new ProjectSettingsService(harness.Db);
        var numbering = new FakeNumbering();
        return new ProjectCommercialController(harness.Db, context, new ProjectAccessService(harness.Db, context, settings), settings,
            new ProjectFinancialsService(harness.Db), new ProjectInvoiceService(harness.Db, numbering), notifications ?? new NoOpProjectNotifications(),
            numbering, new FixedTimeZone(), new NoOpAuditLogService());
    }

    /// <summary>Tenant "today" pinned to <see cref="SiteToday"/> in UTC.</summary>
    private sealed class FixedTimeZone : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.Utc);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(utcValue);
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(SiteToday.ToDateTime(TimeOnly.MinValue));
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(SiteToday.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) =>
            Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }
}

public sealed partial class ProjectsModuleTests
{
    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_SiteWorkValuationsFinancialsAndReminders_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenants: true);
            var project = await harness.CreateProjectAsync(originalValue: 2_000_000m);
            var id = project.Summary.Id;
            await MoveToAsync(harness, id, ProjectStageCategories.Active);
            var site = SiteController(harness, harness.TenantContext);
            var commercial = CommercialController(harness, harness.TenantContext);

            // Site: roster, muster (saved twice, as an offline replay would), diary, my sites.
            var worker = Ok(await site.AddWorker(id, new UpsertProjectWorkerRequest("Peter Otieno", "0712000000", "Mason", null, null), CancellationToken.None));
            var marks = new SaveMusterRequest([new SaveMusterMark(worker.Id, "Present", null)]);
            Ok(await site.SaveMuster(id, SiteToday.AddDays(-1), marks, CancellationToken.None));
            Ok(await site.SaveMuster(id, SiteToday, marks, CancellationToken.None));
            var sheet = Ok(await site.SaveMuster(id, SiteToday, marks, CancellationToken.None));
            Assert.Equal(1m, sheet.ManDays);
            var diary = Ok(await site.CreateDiary(id, new UpsertDiaryEntryRequest(SiteToday, "Blockwork level 2", null, "Add a lintel", "Sunny"), CancellationToken.None));
            Assert.Equal(1, diary.Headcount);
            Ok(await site.SubmitDiary(id, diary.Id, CancellationToken.None));
            Assert.Single(Ok(await site.ListDiary(id, null, null, CancellationToken.None)));
            Assert.NotNull(Ok(await site.Mine(CancellationToken.None)));

            // Office: rates, wage sheet, budget, costs, financials.
            Ok(await commercial.SetWorkerRate(id, worker.Id, new SetWorkerRateRequest(SiteToday, 1_200, null), CancellationToken.None));
            var rates = Ok(await commercial.ListWorkerRates(id, CancellationToken.None));
            Assert.Equal((1m, 1_200m), (Assert.Single(rates).UncostedDays, rates.Single().LabourCost));
            var wages = Assert.IsType<OkObjectResult>(await commercial.GetWageSheet(id, SiteToday.AddDays(-7), SiteToday, null, CancellationToken.None)).Value as WageSheetResponse;
            Assert.Equal((2m, 1_200m, 1), (wages!.TotalManDays, wages.TotalAmount, wages.WorkersWithUncostedDays));
            Assert.IsType<FileContentResult>(await commercial.GetWageSheet(id, SiteToday.AddDays(-7), SiteToday, "csv", CancellationToken.None));
            Ok(await commercial.CreateBudgetLine(id, new UpsertBudgetLineRequest("Labour", null, 50_000), CancellationToken.None));
            Ok(await commercial.CreateCostEntry(id, new UpsertCostEntryRequest("Equipment", "Mixer hire", 8_000, SiteToday), CancellationToken.None));

            // Valuation to invoice, then withholding against it.
            Ok(await commercial.UpdateBillingTerms(id, new UpdateBillingTermsRequest(10, null, 50, 6, 200_000, 10), CancellationToken.None));
            Ok(await commercial.RaiseAdvanceInvoice(id, CancellationToken.None));
            Ok(await commercial.PreviewValuation(id, null, null, null, CancellationToken.None));
            var valuation = Ok(await commercial.CreateValuation(id, new UpsertValuationRequest(SiteToday, 500_000, 20_000, null), CancellationToken.None));
            Ok(await commercial.SubmitValuation(id, valuation.Id, CancellationToken.None));
            Ok(await commercial.CertifyValuation(id, valuation.Id, new CertifyValuationRequest(null, "IPC 1", SiteToday, 410_000), CancellationToken.None));
            var invoiced = Ok(await commercial.RaiseValuationInvoice(id, valuation.Id, CancellationToken.None));
            var invoice = await harness.Db.TenantInvoices.SingleAsync(x => x.Id == invoiced.InvoiceId);
            invoice.Status = "Sent";
            await harness.Db.SaveChangesAsync();
            var withholding = new WithholdingCertificatesController(harness.Db, harness.TenantContext, new UserAccessService(harness.TenantContext), new NoOpAuditLogService());
            Ok(await withholding.Record(invoice.Id, new RecordWithholdingRequest("VAT", 2, 8_200, "KRA-1", SiteToday, null), CancellationToken.None));
            Assert.Single(Ok(await withholding.List(null, id, CancellationToken.None)));

            var financials = Ok(await commercial.GetFinancials(id, CancellationToken.None));
            Assert.Equal(1_200m + 8_000m, financials.ActualCost);
            Assert.Equal(8_200m, financials.TaxWithheld);
            Assert.Equal(520_000m, financials.CertifiedToDate);
            Assert.Equal(52_000m, financials.RetentionHeld);
            Assert.Equal(150_000m, financials.AdvanceOutstanding);
            Ok(await commercial.ListValuations(id, CancellationToken.None));
            Ok(await commercial.GetRetention(id, CancellationToken.None));

            // Portal billing view and a reminder run.
            var portal = new PortalProjectsController(harness.Db, new FakePortalContext(harness.TenantId, harness.ClientId), new ProjectSettingsService(harness.Db), null!);
            var billing = Ok(await portal.GetBilling(id, CancellationToken.None));
            Assert.Equal((410_000m, 52_000m), (Assert.Single(billing.Valuations).CertifiedAmount, billing.RetentionHeld));

            var runner = new ProjectReminderRunner(harness.Db, new LicenseGuardService(harness.Db), new ProjectSettingsService(harness.Db),
                new ProjectFinancialsService(harness.Db), new NoOpProjectNotifications(), new FixedTimeZone(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ProjectReminderRunner>.Instance);
            await runner.RunAsync(DateTime.UtcNow);
        });
    }
}
