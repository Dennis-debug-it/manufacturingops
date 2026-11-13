using System.Security.Claims;
using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class VisitorTests
{
    // ------------------------------------------------------------------ licensing

    [Fact]
    public void TheAddOns_AddTheirModules_ToAnyEdition()
    {
        var plan = new LicensePlan { PlanCode = ServiceOpsPlans.Essentials };
        Assert.DoesNotContain(ServiceOpsPlans.Visitors, ServiceOpsPlans.Modules(plan));
        Assert.Contains(ServiceOpsPlans.Visitors, ServiceOpsPlans.Modules(plan, null, "visitors"));
        Assert.Equal([ServiceOpsPlans.Visitors, ServiceOpsPlans.Security], ServiceOpsPlans.ParseOperationsAddOns("security, visitors, nonsense"));
        Assert.Equal("visitors,security", ServiceOpsPlans.NormalizeOperationsAddOns(["Security", "visitors", "visitors"]));
        Assert.Null(ServiceOpsPlans.NormalizeOperationsAddOns([]));
        Assert.Throws<BusinessRuleException>(() => ServiceOpsPlans.NormalizeOperationsAddOns(["payroll"]));

        Assert.Equal([ServiceOpsPlans.Visitors], ServiceOpsPlans.RequiredModules("Visits", "/api/visitors/board"));
        Assert.Empty(ServiceOpsPlans.RequiredModules("AccessZones", "/api/access-zones"));
        Assert.Throws<LicenseException>(() => ServiceOpsPlans.EnsureModules(ServiceOpsPlans.Modules(plan), ServiceOpsPlans.RequiredModules("Visits", "/api/visitors/board")));
    }

    [Fact]
    public async Task ThePlatform_TurnsTheAddOnsOnAndOff()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var plan = new LicensePlan { PlanCode = ServiceOpsPlans.Professional, DisplayName = "Professional" };
        db.LicensePlans.Add(plan);
        db.TenantLicenses.Add(new TenantLicense { TenantId = site.TenantId, LicensePlanId = plan.Id, Status = "Active" });
        await db.SaveChangesAsync();
        var controller = new PlatformOperationsModulesController(db, site.Context, FactoryTestSupport.Audit(db));

        var on = Ok(await controller.Set(site.TenantId, new SetPlatformOperationsModulesRequest(["visitors"]), default));
        Assert.Equal(["visitors"], on.Enabled);
        var snapshot = await new LicenseGuardService(db).GetSnapshotAsync(site.TenantId);
        Assert.Contains(ServiceOpsPlans.Visitors, snapshot.Modules);
        Assert.Contains(ServiceOpsPlans.Inventory, snapshot.Modules);

        Ok(await controller.Set(site.TenantId, new SetPlatformOperationsModulesRequest([]), default));
        Assert.DoesNotContain(ServiceOpsPlans.Visitors, (await new LicenseGuardService(db).GetSnapshotAsync(site.TenantId)).Modules);
    }

    [Fact]
    public void Managers_RunVisitorManagement_ByDefault()
    {
        Assert.Contains(PermissionNames.VisitorsManage, PermissionNames.CapabilityOnly);
        Assert.Contains(PermissionNames.VisitorsGate, PermissionNames.All);
    }

    // ------------------------------------------------------------------ approval

    [Fact]
    public async Task AHost_InvitingTheirOwnVisitor_IsApproved_AndAWalkIn_WaitsForTheHost()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var visitors = Service(db, dispatcher);

        var invited = await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Jane Wanjiru", "0712 345 678", Company: "Unilever")]), site.Host);
        Assert.Equal((VisitStatuses.Approved, "VIS-000001", "Grace Host"), (invited.Status, invited.VisitNumber, invited.HostName));
        Assert.Empty(dispatcher.Requests);

        var walkIn = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "delivery", [new VisitorDetails("Peter Otieno", "+254 722 000 111")], HostUserId: site.Host.UserId, WalkIn: true), site.Gate);
        Assert.Equal((VisitStatuses.PendingApproval, VisitSources.WalkIn, VisitPurposes.Delivery), (walkIn.Status, walkIn.Source, walkIn.Purpose));
        var request = Assert.Single(dispatcher.Requests);
        Assert.Equal((VisitorService.ApprovalKey, site.Host.UserId), (request.Key, request.UserIds.Single()));

        var attendee = await db.VisitAttendees.SingleAsync(x => x.VisitId == walkIn.Id);
        var waiting = await visitors.CheckAsync(site.TenantId, attendee.Id, new GateConfirmation(), DateTime.UtcNow);
        Assert.Equal(["approval"], waiting.Issues.Select(x => x.Code));
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(), site.Manager));

        await Assert.ThrowsAsync<ForbiddenException>(() => visitors.DecideAsync(site.TenantId, walkIn.Id, true, null, site.Gate));
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.DecideAsync(site.TenantId, walkIn.Id, false, null, site.Host));
        Assert.Equal(VisitStatuses.Approved, (await visitors.DecideAsync(site.TenantId, walkIn.Id, true, null, site.Host)).Status);

        await visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(IdVerified: true), site.Gate);
        Assert.Equal(VisitStatuses.OnSite, (await db.Visits.SingleAsync(x => x.Id == walkIn.Id)).Status);
        Assert.Equal(VisitorService.ArrivedKey, dispatcher.Requests.Last().Key);
    }

    [Fact]
    public async Task WalkIns_GoStraightIn_WhenTheFactoryDoesNotAskHosts()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        (await db.FactorySettings.SingleAsync()).WalkInsNeedHostApproval = false;
        await db.SaveChangesAsync();

        var walkIn = await Service(db).CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Delivery", [new VisitorDetails("Peter Otieno")], HostUserId: site.Host.UserId, WalkIn: true), site.Gate);

        Assert.Equal(VisitStatuses.Approved, walkIn.Status);
    }

    // ------------------------------------------------------------------ gate checks

    [Fact]
    public async Task TheProcessingHall_NeedsInductionPpeAndAnEscort_AndAnInductionGivenAtTheGateIsRemembered()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var hall = await db.AccessZones.SingleAsync(x => x.Name == "Processing hall");
        var visit = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Audit", [new VisitorDetails("Amina Hassan", Company: "KEBS")], ZoneIds: [hall.Id]), site.Host);
        var attendee = await db.VisitAttendees.SingleAsync(x => x.VisitId == visit.Id);

        var before = await visitors.CheckAsync(site.TenantId, attendee.Id, new GateConfirmation(), DateTime.UtcNow);
        Assert.Equal(["induction", "ppe", "escort"], before.Issues.Select(x => x.Code));
        Assert.Equal(["Hairnet", "Dust coat", "Safety boots"], before.Requirements.Ppe);
        Assert.False(before.Overridable);

        // A supervisor cannot wave someone through without an induction or an escort.
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(PpeConfirmed: true, OverrideReason: "VIP"), site.Manager));

        await visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(true, true, RecordInduction: true, EscortUserId: site.Host.UserId), site.Gate);
        var saved = await db.VisitAttendees.SingleAsync(x => x.Id == attendee.Id);
        Assert.Equal((true, true, "Grace Host", (string?)null), (saved.InductionConfirmed, saved.PpeConfirmed, saved.EscortName, saved.OverrideReason));
        var induction = await db.TrainingRecords.SingleAsync();
        Assert.Equal(("Amina Hassan", "KEBS", "Induction", DateTime.UtcNow.Date.AddYears(1)), (induction.PersonName, induction.Company, induction.Category, induction.ExpiresOn!.Value));

        // Her next visit only needs PPE and the escort.
        var again = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Audit", [new VisitorDetails("amina hassan", Company: "KEBS")], ZoneIds: [hall.Id]), site.Host);
        var next = await db.VisitAttendees.SingleAsync(x => x.VisitId == again.Id);
        var check = await visitors.CheckAsync(site.TenantId, next.Id, new GateConfirmation(PpeConfirmed: true, EscortUserId: site.Host.UserId), DateTime.UtcNow);
        Assert.True(check.CanEnter);
        Assert.True(check.Requirements.HasCurrentInduction);
    }

    [Fact]
    public async Task AContractor_WithExpiredInsurance_IsTurnedAway_AndNobodyCanOverrideIt()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var contractor = new Contractor { TenantId = site.TenantId, Name = "Boilerworks Ltd" };
        db.Contractors.Add(contractor);
        db.ContractorDocuments.AddRange(
            new ContractorDocument { TenantId = site.TenantId, ContractorId = contractor.Id, DocumentType = ContractorDocumentTypes.Insurance, ExpiresOn = DateTime.UtcNow.Date.AddDays(-1) },
            new ContractorDocument { TenantId = site.TenantId, ContractorId = contractor.Id, DocumentType = ContractorDocumentTypes.TaxCompliance, ExpiresOn = DateTime.UtcNow.Date.AddDays(90) });
        await db.SaveChangesAsync();

        var visit = await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Contractor work", [new VisitorDetails("Sam Kiprono")], ContractorId: contractor.Id), site.Host);
        var attendee = await db.VisitAttendees.Include(x => x.Visitor).SingleAsync(x => x.VisitId == visit.Id);
        Assert.Equal(contractor.Id, attendee.Visitor!.ContractorId);

        var check = await visitors.CheckAsync(site.TenantId, attendee.Id, new GateConfirmation(), DateTime.UtcNow);
        var issue = Assert.Single(check.Issues);
        Assert.Equal(("contractor", false), (issue.Code, issue.Overridable));
        Assert.Contains("insurance has expired", issue.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(OverrideReason: "Urgent boiler repair"), site.Manager));
    }

    [Fact]
    public async Task ABlockedVisitor_CanOnlyBeLetInBySupervisor_WithAReason()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var visit = await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("John Mwangi", "0700111222")]), site.Host);
        var attendee = await db.VisitAttendees.SingleAsync(x => x.VisitId == visit.Id);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.SetBlockedAsync(site.TenantId, attendee.VisitorId, true, " ", site.Manager));
        await visitors.SetBlockedAsync(site.TenantId, attendee.VisitorId, true, "Caught with green leaf in 2025", site.Manager);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(), site.Gate));
        Assert.Contains("A supervisor can let them in", ex.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(), site.Manager));

        await visitors.CheckInAsync(site.TenantId, attendee.Id, new GateConfirmation(OverrideReason: "Board chair vouched for him"), site.Manager);
        var saved = await db.VisitAttendees.SingleAsync(x => x.Id == attendee.Id);
        Assert.Equal(("blocked: Board chair vouched for him", "Pat Manager"), (saved.OverrideReason, saved.OverriddenByName));
    }

    [Fact]
    public async Task TheSubstation_NeedsAnApprovedPermit_ValidNow()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var substation = await db.AccessZones.SingleAsync(x => x.Name == "Electrical substation");
        var job = new WorkOrder { TenantId = site.TenantId, WorkOrderNumber = "WO-7", Title = "Transformer oil test", Status = WorkOrderStatuses.Approved };
        var permit = new WorkPermit
        {
            TenantId = site.TenantId, PermitNumber = "PTW-000001", WorkOrderId = job.Id, PermitType = PermitTypes.Electrical, Status = PermitStatuses.Requested,
            ValidFrom = DateTime.UtcNow.AddHours(-1), ValidTo = DateTime.UtcNow.AddHours(6),
        };
        db.AddRange(job, permit);
        await db.SaveChangesAsync();

        var noPermit = await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Contractor work", [new VisitorDetails("Ali Noor")], ZoneIds: [substation.Id]), site.Host);
        var a = await db.VisitAttendees.SingleAsync(x => x.VisitId == noPermit.Id);
        var confirmed = new GateConfirmation(PpeConfirmed: true, RecordInduction: true, EscortUserId: site.Host.UserId);
        Assert.Contains("need a permit", Assert.Single((await visitors.CheckAsync(site.TenantId, a.Id, confirmed, DateTime.UtcNow)).Issues).Message);

        var withPermit = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Contractor work", [new VisitorDetails("Ali Noor")], ZoneIds: [substation.Id], WorkOrderId: job.Id, WorkPermitId: permit.Id), site.Host);
        var b = await db.VisitAttendees.SingleAsync(x => x.VisitId == withPermit.Id);
        Assert.Contains("requested, not approved", Assert.Single((await visitors.CheckAsync(site.TenantId, b.Id, confirmed, DateTime.UtcNow)).Issues).Message);

        permit.Status = PermitStatuses.Approved;
        await db.SaveChangesAsync();
        Assert.True((await visitors.CheckAsync(site.TenantId, b.Id, confirmed, DateTime.UtcNow)).CanEnter);
        Assert.Contains("not now", Assert.Single((await visitors.CheckAsync(site.TenantId, b.Id, confirmed, DateTime.UtcNow.AddHours(8))).Issues, x => x.Code == "permit").Message);

        var other = new WorkOrder { TenantId = site.TenantId, WorkOrderNumber = "WO-8", Title = "Other", Status = WorkOrderStatuses.Approved };
        db.Add(other);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Contractor work", [new VisitorDetails("Ali Noor")], WorkOrderId: other.Id, WorkPermitId: permit.Id), site.Host));
    }

    // ------------------------------------------------------------------ in and out

    [Fact]
    public async Task LeavingByBadge_CompletesTheVisit_OnceNobodyIsInside()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var visit = await visitors.CreateAsync(site.TenantId,
            new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One"), new VisitorDetails("Ben Two")], ExpectedFrom: DateTime.UtcNow.AddMinutes(-5), ExpectedTo: DateTime.UtcNow.AddMinutes(-1)), site.Host);
        var people = await db.VisitAttendees.OrderBy(x => x.BadgeNumber).ToListAsync();
        Assert.Equal(["VIS-000001-1", "VIS-000001-2"], people.Select(x => x.BadgeNumber));
        Assert.Equal(2, people.Select(x => x.BadgeToken).Distinct().Count());

        // The visit window has passed: a late arrival is a supervisor's call.
        var late = await visitors.CheckAsync(site.TenantId, people[0].Id, new GateConfirmation(), DateTime.UtcNow);
        Assert.Equal(("schedule", true), (late.Issues.Single().Code, late.Overridable));
        await visitors.CheckInAsync(site.TenantId, people[0].Id, new GateConfirmation(OverrideReason: "Traffic"), site.Manager);
        await visitors.CheckInAsync(site.TenantId, people[1].Id, new GateConfirmation(OverrideReason: "Traffic"), site.Manager);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckInAsync(site.TenantId, people[1].Id, new GateConfirmation(OverrideReason: "x"), site.Manager));

        var badge = await visitors.FindBadgeAsync(site.TenantId, $"https://ops.example/visitors/badge/{people[0].BadgeToken}");
        Assert.Equal(people[0].Id, badge!.Id);
        Assert.Null(await visitors.FindBadgeAsync(site.TenantId, "short"));

        await visitors.CheckOutAsync(site.TenantId, people[0].Id, site.Gate);
        Assert.Equal(VisitStatuses.OnSite, (await db.Visits.SingleAsync()).Status);
        await visitors.CheckOutAsync(site.TenantId, people[1].Id, site.Gate);
        var done = await db.Visits.SingleAsync();
        Assert.Equal(VisitStatuses.Completed, done.Status);
        Assert.NotNull(done.CompletedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckOutAsync(site.TenantId, people[1].Id, site.Gate));
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CheckAsync(site.TenantId, people[1].Id, new GateConfirmation(), DateTime.UtcNow));
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CancelAsync(site.TenantId, visit.Id, null, site.Host));
    }

    [Fact]
    public async Task TheRollCall_MarksPeopleAccountedFor_AndStartsAfresh()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Ann One"), new VisitorDetails("Ben Two")]), site.Host);
        var people = await db.VisitAttendees.OrderBy(x => x.BadgeNumber).ToListAsync();
        await visitors.CheckInAsync(site.TenantId, people[0].Id, new GateConfirmation(), site.Gate);

        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.MarkAccountedAsync(site.TenantId, people[1].Id, true, site.Gate));
        await visitors.MarkAccountedAsync(site.TenantId, people[0].Id, true, site.Gate);
        Assert.Equal("Gate Officer", (await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).AccountedForByName);

        Assert.Equal(1, await visitors.ResetRollCallAsync(site.TenantId, [site.FactoryId]));
        Assert.Null((await db.VisitAttendees.SingleAsync(x => x.Id == people[0].Id)).AccountedForAt);
    }

    // ------------------------------------------------------------------ identity and retention

    [Fact]
    public async Task IdNumbers_AreKeptMaskedAndHashed_AndFindTheReturningVisitor()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Interview", [new VisitorDetails("Mary Achieng", "0712345678", IdType: "National ID", IdNumber: "2345 6789")]), site.Host);

        var mary = await db.Visitors.SingleAsync();
        Assert.Equal(("+254712345678", "••••6789", 64), (mary.Phone, mary.IdNumberMasked, mary.IdNumberHash!.Length));
        Assert.DoesNotContain("23456789", mary.IdNumberHash);
        Assert.Equal(mary.IdNumberHash, VisitorIdentity.Hash(site.TenantId, "23456789"));
        Assert.NotEqual(mary.IdNumberHash, VisitorIdentity.Hash(Guid.NewGuid(), "23456789"));

        // Same ID, new phone and married name: the same person.
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Interview", [new VisitorDetails("Mary Achieng Otieno", "+254 733 000 000", IdNumber: "23456789")]), site.Host);
        var again = await db.Visitors.SingleAsync();
        Assert.Equal(("Mary Achieng Otieno", "+254733000000"), (again.FullName, again.Phone));

        // Same phone and name: the same person; same phone, another name: someone else.
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("mary achieng otieno", "0733000000")]), site.Host);
        await visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("Tom Otieno", "0733000000")]), site.Host);
        Assert.Equal(2, await db.Visitors.CountAsync());

        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.CreateAsync(site.TenantId, new NewVisit(site.FactoryId, "Meeting", [new VisitorDetails("X Y", "123")]), site.Host));
    }

    [Fact]
    public async Task VisitorsNotSeenWithinTheRetentionPeriod_LoseTheirDetails_ButBlockedOnesAreKept()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var visitors = Service(db);
        var old = DateTime.UtcNow.AddMonths(-13);
        db.Visitors.AddRange(
            new Visitor { TenantId = site.TenantId, FullName = "Old Visitor", Phone = "+254700000001", IdNumberHash = "h", IdNumberMasked = "••••1234", LastVisitAt = old },
            new Visitor { TenantId = site.TenantId, FullName = "Banned Visitor", Phone = "+254700000002", IsBlocked = true, BlockedReason = "Theft", LastVisitAt = old },
            new Visitor { TenantId = site.TenantId, FullName = "Recent Visitor", LastVisitAt = DateTime.UtcNow.AddMonths(-2) });
        await db.SaveChangesAsync();

        Assert.Equal(1, await visitors.AnonymiseAsync(site.TenantId, DateTime.UtcNow));

        var gone = await db.Visitors.SingleAsync(x => x.AnonymisedAt != null);
        Assert.Equal(("Visitor (details removed)", (string?)null, (string?)null), (gone.FullName, gone.Phone, gone.IdNumberHash));
        Assert.Equal(0, await visitors.AnonymiseAsync(site.TenantId, DateTime.UtcNow));
    }

    [Fact]
    public async Task ZonesAreSeededOncePerFactory()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db, seedZones: false);
        var visitors = Service(db);
        Assert.Equal(10, await visitors.SeedZonesAsync(site.TenantId, site.FactoryId));
        Assert.Equal(2, await db.AccessZones.CountAsync(x => x.Level == AccessZoneLevels.Public));
        await Assert.ThrowsAsync<BusinessRuleException>(() => visitors.SeedZonesAsync(site.TenantId, site.FactoryId));
    }

    [Fact]
    public async Task OnlyGateStaff_RegisterWalkIns_OrVisitsForSomeoneElse()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var host = Controller(db, new FactoryTenantContext(site.TenantId, site.Host.UserId, isAdmin: false));
        await Assert.ThrowsAsync<ForbiddenException>(() => host.Create(new CreateVisitRequest(site.FactoryId, "Meeting", [new VisitorDetailsRequest("A Visitor")], WalkIn: true), default));
        await Assert.ThrowsAsync<ForbiddenException>(() => host.Create(new CreateVisitRequest(site.FactoryId, "Meeting", [new VisitorDetailsRequest("A Visitor")], HostUserId: site.Manager.UserId), default));
        var mine = Ok(await host.Create(new CreateVisitRequest(site.FactoryId, "Meeting", [new VisitorDetailsRequest("A Visitor")]), default));
        Assert.True(mine.IAmHost);

        // A host sees only their own visits, and not the gate desk.
        Assert.False(Ok(host.Me()).CanGate);
        await Assert.ThrowsAsync<ForbiddenException>(() => host.OnSite(null, default));
        var gate = Controller(db, new FactoryTenantContext(site.TenantId, site.Gate.UserId, isAdmin: false, PermissionNames.VisitorsGate));
        Ok(await gate.Create(new CreateVisitRequest(site.FactoryId, "Delivery", [new VisitorDetailsRequest("B Driver")], HostUserId: site.Manager.UserId, WalkIn: true), default));
        Assert.Single(Ok(await host.Board(null, default)).Visits);
        Assert.Equal(2, Ok(await gate.Board(null, default)).Visits.Count);
    }

    [Fact]
    public async Task BadgesPrint_ForApprovedVisits_WithTheTokenInTheQrCode()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await SeedAsync(db);
        var hall = await db.AccessZones.SingleAsync(x => x.Name == "Processing hall");
        var gate = WithUser(new VisitsController(db, site.Context, FactoryTestSupport.Scope(db, site.Context), Service(db), FactoryTestSupport.Settings(db),
            FactoryTestSupport.Audit(db), new VisitorBadgePdfRenderer()));
        var visit = Ok(await gate.Create(new CreateVisitRequest(site.FactoryId, "Contractor work", [new VisitorDetailsRequest("Ann One"), new VisitorDetailsRequest("Ben Two")], ZoneIds: [hall.Id]), default));

        var pdf = Assert.IsType<FileContentResult>(await gate.Badges(visit.Id, null, default));
        Assert.Equal(("application/pdf", $"badges-{visit.VisitNumber}.pdf"), (pdf.ContentType, pdf.FileDownloadName));
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.FileContents, 0, 4));

        var badge = await Service(db).FindBadgeAsync(site.TenantId, $"VISITOR:{visit.Attendees[0].BadgeToken}");
        Assert.Equal(visit.Attendees[0].Id, badge!.Id);
    }

    // ------------------------------------------------------------------ PostgreSQL

    [Fact]
    public async Task Postgres_VisitorQueries_AllTranslate()
    {
        await FactoryPostgres.RunAsync("visitors", async connection =>
        {
            var (db, context) = await FactoryPostgres.SeedAsync(connection);
            await using var _ = db;
            var tenantId = context.TenantId!.Value;
            var factory = new OrgNode { TenantId = tenantId, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
            db.Add(factory);
            await db.SaveChangesAsync();
            var zones = WithUser(new AccessZonesController(db, context, FactoryTestSupport.Scope(db, context), Service(db), FactoryTestSupport.Audit(db)));
            var seeded = Ok(await zones.Seed(new SeedAccessZonesRequest(factory.Id), default));
            var hall = seeded.Single(x => x.Name == "Processing hall");
            Ok(await zones.Update(hall.Id, new SaveAccessZoneRequest(factory.Id, "Processing hall", "Restricted", true, true, false, "Hairnet, hairnet, Dust coat"), default));
            Assert.Equal("Hairnet, Dust coat", (await db.AccessZones.SingleAsync(x => x.Id == hall.Id)).RequiredPpe);

            var visits = Controller(db, context);
            var visit = Ok(await visits.Create(new CreateVisitRequest(factory.Id, "Audit",
                [new VisitorDetailsRequest("Amina Hassan", "0712345678", Company: "KEBS", IdNumber: "12345678"), new VisitorDetailsRequest("Joe Kim", Company: "KEBS")],
                ZoneIds: [hall.Id], VehicleRegistration: "kca 123a"), default));
            Assert.Equal(("KCA123A", VisitStatuses.Approved), (visit.VehicleRegistration, visit.Status));
            var amina = visit.Attendees.Single(x => x.FullName == "Amina Hassan");
            var check = Ok(await visits.Check(amina.Id, new GateConfirmationRequest(), default));
            Assert.Equal(["induction", "ppe", "escort"], check.Issues.Select(x => x.Code));
            var inside = Ok(await visits.CheckIn(amina.Id, new GateConfirmationRequest(true, true, true, context.UserId), default));
            Assert.Equal(AttendeeStates.OnSite, inside.Attendees.Single(x => x.Id == amina.Id).State);
            Assert.True(Ok(await visits.Check(visit.Attendees.Single(x => x.FullName == "Joe Kim").Id, new GateConfirmationRequest(PpeConfirmed: true, RecordInduction: true, EscortUserId: context.UserId), default)).CanEnter);

            var board = Ok(await visits.Board(factory.Id, default));
            Assert.Equal((1, 1), (board.OnSite, board.Expected));
            Assert.Single(Ok(await visits.Register(null, null, null, null, "kebs", default)));
            Assert.Single(Ok(await visits.Register(null, null, null, null, "kca 123", default)));
            Assert.Single(Ok(await visits.Register(null, null, null, VisitStatuses.OnSite, "712345", default)));
            Assert.Equal(amina.Id, Ok(await visits.Badge(amina.BadgeToken, default)).Attendee.Id);
            var onSite = Assert.Single(Ok(await visits.OnSite(null, default)));
            Assert.Equal(["Processing hall"], onSite.Zones);
            Assert.IsType<NoContentResult>(await visits.Accounted(amina.Id, new AccountedRequest(true), default));
            Assert.Equal(1, Ok(await visits.ResetRollCall(new ResetRollCallRequest(), default)));
            Assert.Equal("Amina Hassan", Assert.Single(Ok(await visits.People("12345678", false, default))).FullName);
            Assert.Single(Ok(await visits.People(null, false, default)));
            Ok(await visits.Block(amina.VisitorId, new BlockVisitorRequest(true, "Test"), default));
            Assert.Single(Ok(await visits.People(null, true, default)));
            Assert.NotEmpty(Ok(await visits.Staff("adm", default)));

            Ok(await visits.CheckOut(amina.Id, default));
            var report = Ok(await visits.Report(null, null, null, default));
            Assert.Equal((1, 0), (report.Entries, report.Overstays));
            Assert.Equal("Audit", Assert.Single(report.ByPurpose).Purpose);

            await using var anonymous = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            Assert.Equal(0, await Service(anonymous).AnonymiseAsync(tenantId, DateTime.UtcNow));
            Assert.Equal(1, await Service(anonymous).AnonymiseAsync(tenantId, DateTime.UtcNow.AddMonths(13)));
        });
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Site(Guid TenantId, Guid FactoryId, VisitActor Host, VisitActor Gate, VisitActor Manager, FactoryTenantContext Context);

    private static async Task<Site> SeedAsync(AppDbContext db, bool seedZones = true)
    {
        var (tenant, admin) = await FactoryTestSupport.SeedTenantAsync(db);
        var host = FactoryTestSupport.NewUser(tenant.Id, "grace@kericho.test");
        host.FullName = "Grace Host";
        var gate = FactoryTestSupport.NewUser(tenant.Id, "gate@kericho.test");
        gate.FullName = "Gate Officer";
        var manager = FactoryTestSupport.NewUser(tenant.Id, "pat@kericho.test", AppRoles.Manager);
        manager.FullName = "Pat Manager";
        var factory = new OrgNode { TenantId = tenant.Id, Level = OrgNodeLevels.Factory, Name = "Chelal", Code = "CHL" };
        db.AddRange(host, gate, manager, factory, new FactorySettings { TenantId = tenant.Id });
        await db.SaveChangesAsync();
        if (seedZones) await Service(db).SeedZonesAsync(tenant.Id, factory.Id);
        return new Site(tenant.Id, factory.Id, new VisitActor(host.Id, host.FullName), new VisitActor(gate.Id, gate.FullName), new VisitActor(manager.Id, manager.FullName, true),
            new FactoryTenantContext(tenant.Id, admin.Id));
    }

    private static VisitorService Service(AppDbContext db, INotificationDispatcher? dispatcher = null)
    {
        var context = new FactoryTenantContext(Guid.Empty, Guid.Empty);
        return new VisitorService(db, FactoryTestSupport.Settings(db), new ContractorService(db, FactoryTestSupport.WorkOrders(db, context)), dispatcher);
    }

    private static VisitsController Controller(AppDbContext db, ITenantContext context) =>
        WithUser(new VisitsController(db, context, FactoryTestSupport.Scope(db, context), Service(db), FactoryTestSupport.Settings(db), FactoryTestSupport.Audit(db)));

    private static T WithUser<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Desk Officer")], "test")) }
        };
        return controller;
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Requests { get; } = [];

        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
