using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Infrastructure.Services.Factory;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ecosys.Tests.Factory;

public sealed class PeopleAndSafetyTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task A_job_needing_a_certificate_can_only_go_to_someone_holding_a_valid_one_who_is_not_on_leave_or_off()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var licence = new CertificationDefinition { TenantId = site.TenantId, Name = "Electrical installation licence", IsActive = true };
        db.CertificationDefinitions.Add(licence);
        await db.SaveChangesAsync();
        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var job = await NewJobAsync(jobs, site, "Rewire dryer panel", requiredCertificationId: licence.Id);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]), CancellationToken.None));
        Assert.Contains("Grace Sparks has no valid Electrical installation licence", error.Message);

        var people = People(db, site.Admin);
        var expired = Ok(await people.AddCertificate(site.Electrician.Id, new SaveTechnicianCertificateRequest(licence.Id, "EPRA/123", Today.AddYears(-3), Today.AddDays(-1), null), CancellationToken.None));
        Assert.Equal(CertificateStatuses.Expired, expired.Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]), CancellationToken.None));

        Ok(await people.UpdateCertificate(expired.Id, new SaveTechnicianCertificateRequest(licence.Id, "EPRA/124", Today, Today.AddYears(3), null), CancellationToken.None));
        await jobs.Act(job, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]), CancellationToken.None);

        // Leave and the roster: nobody on leave or rostered off is assigned.
        var other = await NewJobAsync(jobs, site, "Change fan belt");
        db.LeaveRequests.Add(new LeaveRequest { TenantId = site.TenantId, TechnicianId = site.Fitter.Id, LeaveTypeCode = "ANNUAL", StartDate = DateOnly.FromDateTime(Today), EndDate = DateOnly.FromDateTime(Today.AddDays(2)), TotalDays = 3, Status = "Approved" });
        await db.SaveChangesAsync();
        Assert.Contains("on leave", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(other, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]), CancellationToken.None))).Message);
        Ok2(await people.SaveRoster(new SaveRosterRequest([new RosterCell(site.Electrician.Id, DateOnly.FromDateTime(Today), RosterKinds.Off, null, null)]), CancellationToken.None));
        Assert.Contains("rostered off", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(other, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]), CancellationToken.None))).Message);
        Ok2(await people.SaveRoster(new SaveRosterRequest([new RosterCell(site.Electrician.Id, DateOnly.FromDateTime(Today), RosterKinds.OnCall, null, null)]), CancellationToken.None));
        await jobs.Act(other, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Electrician.Id]), CancellationToken.None);
    }

    [Fact]
    public async Task Time_outside_the_rostered_shift_is_overtime_at_the_overtime_rate()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var people = People(db, site.Admin);
        await people.UpdateProfile(site.Fitter.Id, new TechnicianProfileRequest(Trades.Fitter, site.Drying, 400, 1.5m, [new TechnicianSkillDto("Gearboxes", 3)]), CancellationToken.None);
        var day = new DateOnly(2026, 11, 2);
        Ok2(await people.SaveRoster(new SaveRosterRequest([new RosterCell(site.Fitter.Id, day, RosterKinds.Shift, "Day", null)]), CancellationToken.None));

        var guards = new FactoryJobGuards(db);
        var fitter = await db.Technicians.SingleAsync(x => x.Id == site.Fitter.Id);
        // Nairobi is UTC+3: 07:00 UTC is 10:00 on shift; 17:00 UTC is 20:00, after the 18:00 end.
        Assert.Equal((false, 400m), await guards.LabourAsync(fitter, new DateTime(2026, 11, 2, 7, 0, 0, DateTimeKind.Utc), 60, CancellationToken.None));
        Assert.Equal((true, 300m), await guards.LabourAsync(fitter, new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), 30, CancellationToken.None));
        // No roster entry for the next day: a rest day, so overtime.
        Assert.True((await guards.LabourAsync(fitter, new DateTime(2026, 11, 3, 7, 0, 0, DateTimeKind.Utc), 60, CancellationToken.None)).Overtime);

        var team = Ok(await people.Technicians(false, CancellationToken.None));
        var me = team.Single(x => x.Id == site.Fitter.Id);
        Assert.Equal(Trades.Fitter, me.Trade);
        Assert.Equal("Drying", me.HomeOrgNodeName);
        Assert.Equal("Gearboxes", Assert.Single(me.Skills).Skill);
    }

    [Fact]
    public async Task High_risk_work_starts_only_under_an_authorised_permit_and_finishes_only_with_locks_off_and_the_permit_closed()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var supervisor = FactoryTestSupport.NewUser(site.TenantId, "sup@chelal.test", AppRoles.Manager);
        db.Users.Add(supervisor);
        await db.SaveChangesAsync();
        var supervisorContext = new FactoryTenantContext(site.TenantId, supervisor.Id, false, PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders);

        var jobs = FactoryWorkOrderTests.Controller(db, site.Admin);
        var job = await NewJobAsync(jobs, site, "Replace burner contactor", permits: [PermitTypes.Electrical]);
        await jobs.Act(job, FactoryWorkOrderActions.Assign, new FactoryActionRequest(TechnicianIds: [site.Fitter.Id]), CancellationToken.None);
        Assert.Contains("electrical permit", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job, FactoryWorkOrderActions.Start, new FactoryActionRequest(), CancellationToken.None))).Message);

        var safety = Safety(db, site.Admin);
        var permit = Ok(await safety.RequestPermit(new RequestPermitRequest(job, PermitTypes.Electrical, "Live 415 V panel", null, null, null, "Peter Fitter"), CancellationToken.None));
        Assert.StartsWith("PTW-", permit.Permit.PermitNumber);
        Assert.Equal(SafetyController.PermitChecks[PermitTypes.Electrical].Length, permit.Checks.Count);

        // The requester can't authorise their own permit, and every precaution is ticked first.
        await Assert.ThrowsAsync<BusinessRuleException>(() => safety.Decide(permit.Permit.Id, new PermitDecisionRequest(true, null, null), CancellationToken.None));
        var supervisorSafety = Safety(db, supervisorContext);
        Assert.Contains("precautions", (await Assert.ThrowsAsync<BusinessRuleException>(() => supervisorSafety.Decide(permit.Permit.Id, new PermitDecisionRequest(true, null, null), CancellationToken.None))).Message);
        foreach (var check in permit.Checks) await safety.TickCheck(permit.Permit.Id, check.Id, new TickRequest(true), CancellationToken.None);
        var approved = Ok(await supervisorSafety.Decide(permit.Permit.Id, new PermitDecisionRequest(true, "Go ahead", null), CancellationToken.None));
        Assert.Equal(PermitStatuses.Approved, approved.Permit.Status);

        await jobs.Act(job, FactoryWorkOrderActions.Start, new FactoryActionRequest(), CancellationToken.None);

        var point = Ok(await safety.AddIsolationPoint(site.Dryer.Id, new SaveIsolationPointRequest("E1", "Electrical", "Main isolator, MCC-2 panel 4", "Dryer MCC room", "Open, lock, test dead at the motor terminals"), CancellationToken.None));
        await Assert.ThrowsAsync<BusinessRuleException>(() => safety.ApplyLock(new ApplyLockRequest(job, point.Id, "L-07", false), CancellationToken.None));
        var locked = Ok(await safety.ApplyLock(new ApplyLockRequest(job, point.Id, "L-07", true), CancellationToken.None));
        Assert.Single(Ok(await safety.Locks(false, CancellationToken.None)));

        var complete = new FactoryActionRequest(WorkDone: "Contactor replaced", ProblemCodeId: site.Code("BRD"));
        Assert.Contains("isolation lock", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job, FactoryWorkOrderActions.Complete, complete, CancellationToken.None))).Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => safety.Close(permit.Permit.Id, new PermitCloseRequest("Done"), CancellationToken.None));
        Ok(await safety.RemoveLock(locked.Id, new RemoveLockRequest(), CancellationToken.None));
        Assert.Contains("permit", (await Assert.ThrowsAsync<BusinessRuleException>(() => jobs.Act(job, FactoryWorkOrderActions.Complete, complete, CancellationToken.None))).Message);
        Ok(await safety.Close(permit.Permit.Id, new PermitCloseRequest("Panel closed, guards on, tested"), CancellationToken.None));
        await jobs.Act(job, FactoryWorkOrderActions.Complete, complete, CancellationToken.None);

        var detail = Ok(await jobs.Get(job, CancellationToken.None));
        Assert.Equal([PermitTypes.Electrical], detail.RequiredPermitTypes);
        Assert.Equal(PermitStatuses.Closed, Assert.Single(detail.Permits).Status);
        Assert.NotNull(Assert.Single(detail.Locks).RemovedAt);
    }

    [Fact]
    public async Task Confined_space_entry_needs_a_gas_test_and_a_permit_lasts_one_day_at_most()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var supervisor = FactoryTestSupport.NewUser(site.TenantId, "sup@chelal.test", AppRoles.Manager);
        db.Users.Add(supervisor);
        await db.SaveChangesAsync();
        var job = await NewJobAsync(FactoryWorkOrderTests.Controller(db, site.Admin), site, "Clean dryer plenum");
        var safety = Safety(db, site.Admin);
        await Assert.ThrowsAsync<BusinessRuleException>(() => safety.RequestPermit(new RequestPermitRequest(job, PermitTypes.ConfinedSpace, null, null, DateTime.UtcNow, DateTime.UtcNow.AddHours(30), null), CancellationToken.None));
        var permit = Ok(await safety.RequestPermit(new RequestPermitRequest(job, PermitTypes.ConfinedSpace, "Dust, heat, no ventilation", null, null, null, null), CancellationToken.None));
        Assert.Contains(PermitTypes.ConfinedSpace, (await db.WorkOrders.FindAsync(job))!.RequiredPermitTypes);
        foreach (var check in permit.Checks) await safety.TickCheck(permit.Permit.Id, check.Id, new TickRequest(true), CancellationToken.None);
        var supervisorSafety = Safety(db, new FactoryTenantContext(site.TenantId, supervisor.Id, false, PermissionNames.CanViewWorkOrders, PermissionNames.CanAssignWorkOrders));
        Assert.Contains("gas test", (await Assert.ThrowsAsync<BusinessRuleException>(() => supervisorSafety.Decide(permit.Permit.Id, new PermitDecisionRequest(true, null, null), CancellationToken.None))).Message);
        Assert.Equal("O2 20.9%, LEL 0%, CO 2 ppm, H2S 0", Ok(await supervisorSafety.Decide(permit.Permit.Id, new PermitDecisionRequest(true, null, "O2 20.9%, LEL 0%, CO 2 ppm, H2S 0"), CancellationToken.None)).GasTest);
    }

    [Fact]
    public async Task An_incident_is_reported_to_supervisors_gets_a_corrective_job_and_closes_only_with_a_root_cause_and_the_job_done()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var dispatcher = new RecordingDispatcher();
        var safety = Safety(db, site.Admin, dispatcher);
        var incident = Ok(await safety.ReportIncident(new SaveIncidentRequest(IncidentKinds.NearMiss, "Moderate", DateTime.UtcNow.AddHours(-1), null, site.Dryer.Id, null,
            "Guard left off dryer fan coupling", "Coupling guard found on the floor during the running check.", null, "Machine stopped, guard refitted", null, null, null), CancellationToken.None));
        Assert.StartsWith("INC-", incident.IncidentNumber);
        Assert.Equal(site.Drying, incident.OrgNodeId);
        Assert.Equal(SafetyController.IncidentKey, Assert.Single(dispatcher.Sent).Key);

        await Assert.ThrowsAsync<BusinessRuleException>(() => safety.CloseIncident(incident.Id, CancellationToken.None));
        var withJob = Ok(await safety.RaiseCorrectiveJob(incident.Id, new CorrectiveJobRequest(null, null), CancellationToken.None));
        var job = await db.WorkOrders.FindAsync(withJob.CorrectiveWorkOrderId);
        Assert.Equal(FactoryWorkTypes.Safety, job!.WorkType);
        Ok(await safety.UpdateIncident(incident.Id, new SaveIncidentRequest(IncidentKinds.NearMiss, "Moderate", incident.OccurredAt, null, site.Dryer.Id, null, incident.Title, incident.Description,
            null, null, "Guard bolts missing; fitter didn't refit after greasing", "Add guard check to the greasing step", null), CancellationToken.None));
        Assert.Contains("corrective job", (await Assert.ThrowsAsync<BusinessRuleException>(() => safety.CloseIncident(incident.Id, CancellationToken.None))).Message);
        await FactoryWorkOrderTests.RunToCompletionAsync(FactoryWorkOrderTests.Controller(db, site.Admin), job.Id, site);
        Assert.Equal(IncidentStatuses.Closed, Ok(await safety.CloseIncident(incident.Id, CancellationToken.None)).Status);

        var summary = Ok(await safety.IncidentSummary(null, CancellationToken.None));
        Assert.Equal(1, summary.NearMisses);
        Assert.Null(summary.DaysSinceLostTimeInjury);
    }

    [Fact]
    public async Task Certificates_alert_at_30_and_7_days_and_once_expired_and_the_workload_adds_up()
    {
        await using var db = FactoryTestSupport.CreateDbContext();
        var site = await FactoryWorkOrderTests.SeedAsync(db);
        var ticket = new CertificationDefinition { TenantId = site.TenantId, Name = "Working at heights", IsActive = true };
        db.CertificationDefinitions.Add(ticket);
        db.TechnicianCertificates.Add(new TechnicianCertificate { TenantId = site.TenantId, TechnicianId = site.Fitter.Id, CertificationDefinitionId = ticket.Id, ExpiresOn = Today.AddDays(20) });
        await db.SaveChangesAsync();
        var dispatcher = new RecordingDispatcher();
        var people = new PeopleService(db, dispatcher);

        Assert.Equal(1, await people.RunCertificateAlertsAsync(site.TenantId, Today));
        Assert.Contains(site.FitterUser.Id, dispatcher.Sent.Single().UserIds);
        Assert.Equal(0, await people.RunCertificateAlertsAsync(site.TenantId, Today.AddDays(5)));
        Assert.Equal(1, await people.RunCertificateAlertsAsync(site.TenantId, Today.AddDays(15)));
        Assert.Equal(1, await people.RunCertificateAlertsAsync(site.TenantId, Today.AddDays(21)));
        Assert.Equal(0, await people.RunCertificateAlertsAsync(site.TenantId, Today.AddDays(40)));

        var controller = People(db, site.Admin);
        Assert.Single(Ok(await controller.Certificates(CertificateStatuses.Expiring, CancellationToken.None)));

        var monday = DateOnly.FromDateTime(Today).AddDays(-(((int)Today.DayOfWeek + 6) % 7));
        Ok2(await controller.SaveRoster(new SaveRosterRequest(Enumerable.Range(0, 5).Select(i => new RosterCell(site.Fitter.Id, monday.AddDays(i), RosterKinds.Shift, "Day", null))
            .Append(new RosterCell(site.Fitter.Id, monday.AddDays(5), RosterKinds.OnCall, null, null)).ToList()), CancellationToken.None));
        Assert.Equal(6, Ok2(await controller.CopyWeek(new CopyRosterRequest(monday, 1), CancellationToken.None)));
        db.WorkOrderTimeLogs.Add(new WorkOrderTimeLog { TenantId = site.TenantId, WorkOrderId = Guid.NewGuid(), TechnicianId = site.Fitter.Id, StartedAt = monday.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), DurationMinutes = 300, LabourCost = 2000 });
        db.WorkOrderTimeLogs.Add(new WorkOrderTimeLog { TenantId = site.TenantId, WorkOrderId = Guid.NewGuid(), TechnicianId = site.Fitter.Id, StartedAt = monday.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc), DurationMinutes = 60, IsOvertime = true, LabourCost = 600 });
        await db.SaveChangesAsync();

        var workload = Ok(await controller.Workload(monday, monday.AddDays(6), null, CancellationToken.None)).Single(x => x.TechnicianId == site.Fitter.Id);
        Assert.Equal(60m, workload.RosteredHours);
        Assert.Equal(6m, workload.LoggedHours);
        Assert.Equal(1m, workload.OvertimeHours);
        Assert.Equal(2600m, workload.LabourCost);
        Assert.Equal(10m, workload.UtilisationPercent);
        Assert.Equal(1, workload.OnCallDays);
    }

    private static async Task<Guid> NewJobAsync(FactoryWorkOrdersController jobs, FactoryWorkOrderTests.Seeded site, string title, Guid? requiredCertificationId = null, string[]? permits = null)
    {
        var created = Assert.IsType<FactoryWorkOrderDetail>(Assert.IsType<CreatedAtActionResult>((await jobs.Create(
            new CreateFactoryWorkOrderRequest(title, WorkType: "Corrective", AssetId: site.Dryer.Id, RequiredCertificationId: requiredCertificationId, RequiredPermitTypes: permits), CancellationToken.None)).Result).Value);
        await jobs.Act(created.Id, FactoryWorkOrderActions.Approve, new FactoryActionRequest(), CancellationToken.None);
        return created.Id;
    }

    private static FactoryPeopleController People(AppDbContext db, FactoryTenantContext context) =>
        FactoryAssetRegisterTests.WithHttp(new FactoryPeopleController(db, context, FactoryTestSupport.Access(context), FactoryTestSupport.Scope(db, context), new PeopleService(db)));

    private static SafetyController Safety(AppDbContext db, FactoryTenantContext context, INotificationDispatcher? dispatcher = null)
    {
        var scope = FactoryTestSupport.Scope(db, context);
        var workOrders = new FactoryWorkOrderService(db, new DocumentNumberingService(db), FactoryTestSupport.Settings(db), new AssetLifecycleService(db, scope), scope, new FactorySlaService(db), new FactoryPmProgress(db), new FactoryJobGuards(db));
        return FactoryAssetRegisterTests.WithHttp(new SafetyController(db, context, FactoryTestSupport.Access(context), scope, workOrders, dispatcher));
    }

    private static T Ok<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static T Ok2<T>(ActionResult<T> result) => Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static void Ok2(ActionResult result) => Assert.IsType<NoContentResult>(result);

    private sealed class RecordingDispatcher : INotificationDispatcher
    {
        public List<NotificationRequest> Sent { get; } = [];
        public Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.CompletedTask;
        }
    }
}
