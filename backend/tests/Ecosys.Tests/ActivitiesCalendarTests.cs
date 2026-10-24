using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Ecosys.Shared.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Release 3 step 3b: activities, minutes, action items, reminders, My calendar and the feed (design section 6).</summary>
public sealed class ActivitiesCalendarTests
{
    private static readonly string[] Everyone = [PermissionNames.ActivitiesView];

    // ── Notifications ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Schedule_NotifiesStaffAttendeesOnly_NeverTheOrganiserOrOutsiders()
    {
        var harness = await Harness.CreateAsync();
        var consultant = new Consultant { TenantId = harness.TenantId, FirmName = "Duct & Co", Email = "qs@duct.test" };
        harness.Db.Consultants.Add(consultant);
        await harness.Db.SaveChangesAsync();

        var activity = await harness.Activities.ScheduleAsync(harness.Viewer(harness.Alice), Meeting(harness, attendees:
        [
            new ActivityAttendeeInput(harness.Bob, null, null, null, null),
            new ActivityAttendeeInput(null, consultant.Id, null, null, null),
            new ActivityAttendeeInput(null, null, null, "Site agent", "agent@outside.test"),
            // The organiser is implied, so listing her again is ignored.
            new ActivityAttendeeInput(harness.Alice, null, null, null, null),
        ]));

        Assert.Equal(3, activity.Attendees.Count);
        var row = await harness.Db.Notifications.SingleAsync();
        Assert.Equal((harness.Bob, ActivityNotificationKeys.Scheduled, activity.Id), (row.UserId, row.Key, row.EntityId));
        Assert.Equal($"/activities/{activity.Id}", row.Url);
        // R4: one email, to the staff attendee, with no invite attached; outsiders are recorded only.
        var email = Assert.Single(harness.Outbox.Queued);
        Assert.Equal("bob@acme.test", email.RecipientEmail);
        Assert.Null(email.Attachments);
    }

    [Fact]
    public async Task MoveAddRemoveAndCancel_EachTellTheRightStaff()
    {
        var harness = await Harness.CreateAsync();
        var alice = harness.Viewer(harness.Alice);
        var command = Meeting(harness, attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null)]);
        var activity = await harness.Activities.ScheduleAsync(alice, command);
        harness.Clear();

        // Moved: Bob is told. Carol is added: she gets the invitation, not the change.
        await harness.Activities.UpdateAsync(alice, activity.Id, command with
        {
            StartsAt = command.StartsAt.AddHours(2),
            EndsAt = command.EndsAt.AddHours(2),
            Attendees = [new ActivityAttendeeInput(harness.Bob, null, null, null, null), new ActivityAttendeeInput(harness.Carol, null, null, null, null)],
        });
        Assert.Equal(
            [(harness.Bob, ActivityNotificationKeys.Updated), (harness.Carol, ActivityNotificationKeys.Scheduled)],
            await harness.SentAsync());
        harness.Clear();

        // Bob taken off: he hears it is off his calendar. No time change, so Carol hears nothing.
        var withoutBob = command with { StartsAt = command.StartsAt.AddHours(2), EndsAt = command.EndsAt.AddHours(2), Attendees = [new ActivityAttendeeInput(harness.Carol, null, null, null, null)] };
        await harness.Activities.UpdateAsync(alice, activity.Id, withoutBob);
        Assert.Equal([(harness.Bob, ActivityNotificationKeys.Cancelled)], await harness.SentAsync());
        harness.Clear();

        await harness.Activities.CancelAsync(alice, activity.Id, "Client postponed");
        Assert.Equal([(harness.Carol, ActivityNotificationKeys.Cancelled)], await harness.SentAsync());
        Assert.Equal(ActivityNotificationKeys.Cancelled, Assert.Single(harness.Outbox.Queued).EventKey);
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Activities.UpdateAsync(alice, activity.Id, withoutBob));
    }

    [Fact]
    public async Task OnlyTheOrganiserOrAManagerChangesAnActivity_AndOutsidersCannotSeeIt()
    {
        var harness = await Harness.CreateAsync();
        var activity = await harness.Activities.ScheduleAsync(harness.Viewer(harness.Alice),
            Meeting(harness, attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null)]));

        // An attendee sees it but can't change it; someone not on it can't see it at all.
        Assert.Equal(activity.Id, (await harness.Activities.LoadVisibleAsync(harness.Viewer(harness.Bob), activity.Id)).Id);
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Activities.CancelAsync(harness.Viewer(harness.Bob), activity.Id, null));
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Activities.LoadVisibleAsync(harness.Viewer(harness.Carol), activity.Id));
        // A manager can, and the tenant's other activities stay out of reach for another tenant.
        await harness.Activities.CancelAsync(harness.Viewer(harness.Carol, PermissionNames.ActivitiesView, PermissionNames.ActivitiesManage), activity.Id, null);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Activities.LoadVisibleAsync(harness.Viewer(harness.Foreign, tenantId: harness.OtherTenantId), activity.Id));
        // Organising for someone else needs activities.manage.
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Activities.ScheduleAsync(harness.Viewer(harness.Bob), Meeting(harness) with { OrganizerUserId = harness.Alice }));
    }

    // ── Minutes ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishingMinutes_EmailsThePdf_AndLocksThem()
    {
        var harness = await Harness.CreateAsync();
        var alice = harness.Viewer(harness.Alice);
        var activity = await harness.Activities.ScheduleAsync(alice, Meeting(harness, startsIn: TimeSpan.FromHours(-2),
            attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null), new ActivityAttendeeInput(null, null, null, "Architect", null)]));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Activities.SaveMinutesAsync(alice, activity.Id, "Too early"));
        var bobRow = activity.Attendees.Single(x => x.UserId == harness.Bob);
        await harness.Activities.MarkHeldAsync(alice, activity.Id, [bobRow.Id]);
        await harness.Activities.SaveMinutesAsync(alice, activity.Id, "1. Ducts to be resized.\n2. Next site meeting in two weeks.");
        await harness.Activities.CreateActionItemAsync(alice, new SaveActionItemCommand("Resize ducts", harness.Bob, null, new DateOnly(2026, 11, 2), null, activity.Id, null, null));
        harness.Clear();

        var published = await harness.Activities.PublishMinutesAsync(alice, activity.Id);

        Assert.NotNull(published.MinutesPublishedAt);
        var email = Assert.Single(harness.Outbox.Queued);
        Assert.Equal((ActivityNotificationKeys.MinutesPublished, "bob@acme.test"), (email.EventKey, email.RecipientEmail));
        var pdf = Assert.Single(email.Attachments!);
        Assert.EndsWith(".pdf", pdf.Filename);
        Assert.Equal("application/pdf", pdf.ContentType);
        var rendered = string.Join("\n", harness.Pdf.Sections);
        Assert.Contains("Bob — attended", rendered);
        Assert.Contains("Architect — absent", rendered);
        Assert.Contains("Resize ducts — Bob, due 02 Nov 2026", rendered);

        // Locked: no edits, no attendance changes, no second publish. Corrections are addenda.
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Activities.SaveMinutesAsync(alice, activity.Id, "Changed"));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Activities.MarkHeldAsync(alice, activity.Id, []));
        await Assert.ThrowsAsync<BusinessRuleException>(() => harness.Activities.PublishMinutesAsync(alice, activity.Id));
        var corrected = await harness.Activities.AddAddendumAsync(alice, activity.Id, "Item 2: three weeks, not two.");
        Assert.Equal("1. Ducts to be resized.\n2. Next site meeting in two weeks.", corrected.Minutes);
        Assert.Single(corrected.Addenda);
    }

    // ── Action items ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActionItems_CarryTheLinkedRecord_AndNotifyTheirAssignee()
    {
        var harness = await Harness.CreateAsync();
        var client = new Client { TenantId = harness.TenantId, ClientName = "Westlands Mall", IsActive = true };
        harness.Db.Clients.Add(client);
        await harness.Db.SaveChangesAsync();
        var alice = harness.Viewer(harness.Alice);
        var activity = await harness.Activities.ScheduleAsync(alice, Meeting(harness) with { EntityType = "client", EntityId = client.Id });
        harness.Clear();

        var fromMeeting = await harness.Activities.CreateActionItemAsync(alice,
            new SaveActionItemCommand("Send revised quote", harness.Bob, null, new DateOnly(2026, 11, 1), null, activity.Id, ActivityEntityTypes.Project, Guid.NewGuid()));
        var ownItem = await harness.Activities.CreateActionItemAsync(alice,
            new SaveActionItemCommand("Call the landlord", harness.Alice, null, null, null, null, ActivityEntityTypes.Client, client.Id));

        // An item from a meeting takes the meeting's record, whatever the request said.
        Assert.Equal((ActivityEntityTypes.Client, client.Id), (fromMeeting.EntityType, fromMeeting.EntityId));
        Assert.Equal((ActivityEntityTypes.Client, client.Id), (ownItem.EntityType, ownItem.EntityId));
        // Bob is told; Alice assigning herself is not.
        Assert.Equal([(harness.Bob, ActivityNotificationKeys.ActionItemAssigned)], await harness.SentAsync());

        var onClient = await harness.Activities.ListActionItemsAsync(harness.Viewer(harness.Carol), new ActionItemListQuery(false, "Client", client.Id, null, null));
        Assert.Equal(2, onClient.Count);
        Assert.Equal([fromMeeting.Id], (await harness.Activities.ListActionItemsAsync(harness.Viewer(harness.Bob), new ActionItemListQuery(true, null, null, null, null))).Select(x => x.Id));
        // Without the module a record needs, its items can't be listed through it.
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Activities.ListActionItemsAsync(harness.Viewer(harness.Carol),
            new ActionItemListQuery(false, ActivityEntityTypes.Project, Guid.NewGuid(), null, null)));

        // Carol, unrelated, can't edit Bob's item; Bob can close it.
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Activities.UpdateActionItemAsync(harness.Viewer(harness.Carol), fromMeeting.Id,
            new SaveActionItemCommand("Send revised quote", harness.Bob, null, null, "Done", null, null, null)));
        var done = await harness.Activities.UpdateActionItemAsync(harness.Viewer(harness.Bob), fromMeeting.Id,
            new SaveActionItemCommand("Send revised quote", harness.Bob, null, null, "Done", null, null, null));
        Assert.NotNull(done.CompletedAt);
        Assert.Equal((ActivityEntityTypes.Client, client.Id), (done.EntityType, done.EntityId));
    }

    // ── Calendar ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calendar_NeverShowsASourceTheUserLacksTheModuleOrPermissionFor()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.AddProjectAsync(projectManager: harness.Bob);
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        harness.Db.ProjectTasks.Add(new ProjectTask { TenantId = harness.TenantId, ProjectId = project.Id, Title = "Pour slab", AssigneeUserId = harness.Bob, DueDate = day });
        var technician = new Technician { TenantId = harness.TenantId, UserId = harness.Bob, FullName = "Bob", IsActive = true };
        harness.Db.Technicians.Add(technician);
        harness.Db.WorkOrders.Add(new WorkOrder
        {
            TenantId = harness.TenantId, WorkOrderNumber = "WO-0042", Title = "Chiller service", Status = "Assigned", AssignedTechnicianId = technician.Id,
            ScheduledStartAt = DateTime.UtcNow.AddDays(2), ScheduledEndAt = DateTime.UtcNow.AddDays(2).AddHours(2),
        });
        harness.Db.ActionItems.Add(new ActionItem { TenantId = harness.TenantId, Title = "Confirm duct sizes", AssigneeUserId = harness.Bob, DueDate = day });
        await harness.Db.SaveChangesAsync();
        await harness.Activities.ScheduleAsync(harness.Viewer(harness.Alice), Meeting(harness, attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null)]));
        var from = DateTime.UtcNow.AddDays(-1);
        var to = DateTime.UtcNow.AddDays(10);

        async Task<string[]> SourcesFor(AccessViewer viewer) =>
            (await harness.Calendar.GetItemsAsync(viewer, from, to, null, null)).Select(x => x.Source).Distinct().Order().ToArray();

        var full = harness.Viewer(harness.Bob, PermissionNames.ActivitiesView, PermissionNames.CanViewWorkOrders, PermissionNames.ProjectsView);
        Assert.Equal([CalendarSources.ActionItems, CalendarSources.Activities, CalendarSources.Projects, CalendarSources.WorkOrders], await SourcesFor(full));
        // No Projects module: the project task disappears even with projects.view.
        var noProjects = harness.Viewer(harness.Bob, [PermissionNames.ActivitiesView, PermissionNames.CanViewWorkOrders, PermissionNames.ProjectsView], [ServiceOpsPlans.Operations]);
        Assert.Equal([CalendarSources.ActionItems, CalendarSources.Activities, CalendarSources.WorkOrders], await SourcesFor(noProjects));
        // No permission: work orders and activities go too, and an explicit request for them changes nothing.
        var bare = harness.Viewer(harness.Bob, [], [ServiceOpsPlans.Operations, ServiceOpsPlans.Maintenance, ServiceOpsPlans.Projects]);
        Assert.Empty(await harness.Calendar.GetItemsAsync(bare, from, to, [CalendarSources.Activities, CalendarSources.WorkOrders, CalendarSources.Projects], null));
        Assert.Equal([CalendarSources.Contracts], harness.Calendar.AvailableSources(bare));

        // The team view needs calendar.team.
        await Assert.ThrowsAsync<ForbiddenException>(() => harness.Calendar.GetItemsAsync(harness.Viewer(harness.Carol), from, to, null, [harness.Bob]));
        var team = await harness.Calendar.GetItemsAsync(harness.Viewer(harness.Carol, PermissionNames.ActivitiesView, PermissionNames.CalendarTeam), from, to, null, [harness.Bob, harness.Carol]);
        Assert.All(team, x => Assert.Equal(harness.Bob, x.UserId));
        Assert.Contains(team, x => x.Source == CalendarSources.Activities);
    }

    [Fact]
    public async Task Calendar_HidesProjectsTheViewerCannotSee()
    {
        var harness = await Harness.CreateAsync();
        var project = await harness.AddProjectAsync(projectManager: harness.Alice);
        harness.Db.ProjectMilestones.Add(new ProjectMilestone { TenantId = harness.TenantId, ProjectId = project.Id, Name = "Roof", PlannedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) });
        harness.Db.ProjectTasks.Add(new ProjectTask { TenantId = harness.TenantId, ProjectId = project.Id, Title = "Snag list", AssigneeUserId = harness.Bob, DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) });
        await harness.Db.SaveChangesAsync();
        await new ProjectSettingsService(harness.Db).SaveAsync(harness.TenantId, new ProjectSettingsModel { Visibility = ProjectVisibility.MembersOnly });
        var from = DateTime.UtcNow;
        var to = DateTime.UtcNow.AddDays(5);

        // Alice manages the project, so she sees its milestone. Bob is not on the team of a members-only project.
        var alice = await harness.Calendar.GetItemsAsync(harness.Viewer(harness.Alice, PermissionNames.ProjectsView), from, to, null, null);
        Assert.Equal($"milestone-{(await harness.Db.ProjectMilestones.SingleAsync()).Id}", Assert.Single(alice).Uid);
        Assert.Empty(await harness.Calendar.GetItemsAsync(harness.Viewer(harness.Bob, PermissionNames.ProjectsView), from, to, null, null));
    }

    // ── Feed ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FeedToken_ResolvesToItsOwnerOnly_IsStoredHashed_AndCanBeRevoked()
    {
        var harness = await Harness.CreateAsync(seedLicense: true);
        await harness.Activities.ScheduleAsync(harness.Viewer(harness.Alice), Meeting(harness, title: "Alice's board meeting"));
        await harness.Activities.ScheduleAsync(harness.Viewer(harness.Bob), Meeting(harness, title: "Bob's toolbox talk"));

        var aliceLink = await harness.Feed.CreateAsync(harness.TenantId, harness.Alice);
        var bobLink = await harness.Feed.CreateAsync(harness.TenantId, harness.Bob);
        Assert.NotEqual(aliceLink.Token, bobLink.Token);
        Assert.False(await harness.Db.CalendarFeedTokens.AnyAsync(x => x.TokenHash == aliceLink.Token));

        var aliceFeed = await harness.Feed.RenderAsync(aliceLink.Token, DateTime.UtcNow);
        Assert.Contains("SUMMARY:Alice's board meeting", aliceFeed);
        Assert.DoesNotContain("Bob's toolbox talk", aliceFeed);
        Assert.Null(await harness.Feed.RenderAsync(aliceLink.Token[..^1] + (aliceLink.Token[^1] == 'A' ? 'B' : 'A'), DateTime.UtcNow));

        // A new link replaces the old; revoking ends it. Bob's link is unaffected.
        var replaced = await harness.Feed.CreateAsync(harness.TenantId, harness.Alice);
        Assert.Null(await harness.Feed.RenderAsync(aliceLink.Token, DateTime.UtcNow));
        Assert.True(await harness.Feed.RevokeAsync(harness.TenantId, harness.Alice));
        Assert.Null(await harness.Feed.RenderAsync(replaced.Token, DateTime.UtcNow));
        Assert.Contains("Bob's toolbox talk", await harness.Feed.RenderAsync(bobLink.Token, DateTime.UtcNow));

        // A deactivated user's feed stops working.
        var bob = await harness.Db.Users.SingleAsync(x => x.Id == harness.Bob);
        bob.IsActive = false;
        await harness.Db.SaveChangesAsync();
        Assert.Null(await harness.Feed.RenderAsync(bobLink.Token, DateTime.UtcNow));
    }

    [Fact]
    public async Task FeedEvents_KeepTheirUidsAcrossUpdates_AndCarryNoDetailBeyondTitleTimeAndPlace()
    {
        var harness = await Harness.CreateAsync(seedLicense: true);
        var alice = harness.Viewer(harness.Alice);
        var command = Meeting(harness, title: "Valuation meeting, phase 2") with { Location = "Site office; gate B", Agenda = "Discuss KES 4,000,000 claim" };
        var activity = await harness.Activities.ScheduleAsync(alice, command);
        var link = await harness.Feed.CreateAsync(harness.TenantId, harness.Alice);

        var before = (await harness.Feed.RenderAsync(link.Token, DateTime.UtcNow))!;
        await harness.Activities.UpdateAsync(alice, activity.Id, command with { StartsAt = command.StartsAt.AddDays(1), EndsAt = command.EndsAt.AddDays(1), Title = "Valuation meeting (moved)" });
        var after = (await harness.Feed.RenderAsync(link.Token, DateTime.UtcNow))!;

        Assert.Contains($"UID:activity-{activity.Id}@ecosys", before);
        Assert.Contains($"UID:activity-{activity.Id}@ecosys", after);
        Assert.NotEqual(Line(before, "DTSTART"), Line(after, "DTSTART"));
        Assert.Contains("SUMMARY:Valuation meeting\\, phase 2", before);
        Assert.Contains("LOCATION:Site office\\; gate B", before);
        Assert.DoesNotContain("4,000,000", before);
        Assert.DoesNotContain("DESCRIPTION", before);

        static string Line(string ics, string name) => ics.Split("\r\n").Single(x => x.StartsWith(name + ":"));
    }

    [Fact]
    public void Ics_FoldsLongLines_AndWritesAllDayDatesAsExclusiveEnds()
    {
        var day = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        var ics = IcsCalendar.Write("Ecosys", [new CalendarItem("action-item-1", CalendarSources.ActionItems, new string('x', 120), day, day, true, null, null, null, null)], day);

        Assert.Contains("DTSTART;VALUE=DATE:20261102", ics);
        Assert.Contains("DTEND;VALUE=DATE:20261103", ics);
        Assert.All(ics.Split("\r\n"), line => Assert.True(System.Text.Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.Contains("\r\n x", ics);
    }

    // ── Reminders ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reminders_GoOncePerStartAndDueDate()
    {
        var harness = await Harness.CreateAsync();
        var alice = harness.Viewer(harness.Alice);
        var command = Meeting(harness, startsIn: TimeSpan.FromMinutes(20), attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null)]) with { ReminderMinutesBefore = 30 };
        var activity = await harness.Activities.ScheduleAsync(alice, command);
        await harness.Activities.ScheduleAsync(alice, Meeting(harness, startsIn: TimeSpan.FromHours(3)) with { ReminderMinutesBefore = 30 });
        harness.Db.ActionItems.Add(new ActionItem { TenantId = harness.TenantId, Title = "Confirm duct sizes", AssigneeUserId = harness.Bob, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) });
        harness.Db.ActionItems.Add(new ActionItem { TenantId = harness.TenantId, Title = "Later", AssigneeUserId = harness.Bob, DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5) });
        await harness.Db.SaveChangesAsync();
        harness.Clear();
        var runner = harness.Reminders();

        // The organiser and Bob for the meeting; Bob for the item due tomorrow. Not the later ones.
        Assert.Equal(2, await runner.RunAsync(DateTime.UtcNow));
        Assert.Equal(
            [(harness.Alice, ActivityNotificationKeys.Reminder), (harness.Bob, ActivityNotificationKeys.ActionItemDue), (harness.Bob, ActivityNotificationKeys.Reminder)],
            await harness.SentAsync());
        Assert.Equal(0, await runner.RunAsync(DateTime.UtcNow.AddMinutes(5)));

        // Moving the meeting earns a fresh reminder.
        await harness.Activities.UpdateAsync(alice, activity.Id, command with { StartsAt = command.StartsAt.AddMinutes(10), EndsAt = command.EndsAt.AddMinutes(10) });
        Assert.Equal(1, await runner.RunAsync(DateTime.UtcNow.AddMinutes(5)));
        Assert.Equal(3, await harness.Db.ActivityReminderLogs.CountAsync());
    }

    // ── PostgreSQL ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_ActivitiesActionItemsCalendarFeedAndReminders_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, context),
                seedTenants: true, seedLicense: true);
            var client = new Client { TenantId = harness.TenantId, ClientName = "Westlands Mall", IsActive = true };
            harness.Db.Clients.Add(client);
            var consultant = new Consultant { TenantId = harness.TenantId, FirmName = "Duct & Co" };
            harness.Db.Consultants.Add(consultant);
            await harness.Db.SaveChangesAsync();
            var alice = harness.Viewer(harness.Alice);

            var command = Meeting(harness, startsIn: TimeSpan.FromMinutes(20), attendees:
            [
                new ActivityAttendeeInput(harness.Bob, null, null, null, null),
                new ActivityAttendeeInput(null, consultant.Id, null, null, null),
                new ActivityAttendeeInput(null, null, null, null, "guest@outside.test"),
            ]) with { EntityType = ActivityEntityTypes.Client, EntityId = client.Id, ReminderMinutesBefore = 30 };
            var activity = await harness.Activities.ScheduleAsync(alice, command);
            await harness.Activities.UpdateAsync(alice, activity.Id, command with { Title = "Moved", Attendees = [new ActivityAttendeeInput(harness.Bob, null, null, null, null)] });
            Assert.Single(await harness.Activities.ListAsync(harness.Viewer(harness.Bob), new ActivityListQuery(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), null, null, null, true)));
            Assert.Single(await harness.Activities.ListAsync(harness.Viewer(harness.Carol), new ActivityListQuery(null, null, "Client", client.Id, "Scheduled", false)));
            var item = await harness.Activities.CreateActionItemAsync(alice, new SaveActionItemCommand("Resize ducts", harness.Bob, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), null, activity.Id, null, null));
            Assert.Equal(client.Id, item.EntityId);
            Assert.Single(await harness.Activities.ListActionItemsAsync(harness.Viewer(harness.Bob), new ActionItemListQuery(true, null, null, null, "Open")));

            Assert.Equal(2, await harness.Reminders().RunAsync(DateTime.UtcNow));
            Assert.Equal(0, await harness.Reminders().RunAsync(DateTime.UtcNow));

            var items = await harness.Calendar.GetItemsAsync(harness.Viewer(harness.Bob, PermissionNames.ActivitiesView, PermissionNames.CanViewWorkOrders, PermissionNames.ProjectsView, PermissionNames.CanManageSettings),
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null);
            Assert.Equal([CalendarSources.ActionItems, CalendarSources.Activities], items.Select(x => x.Source).Distinct().Order());
            await harness.Calendar.GetItemsAsync(harness.Viewer(harness.Carol, PermissionNames.ActivitiesView, PermissionNames.CalendarTeam, PermissionNames.ProjectsView, PermissionNames.CanViewWorkOrders),
                DateTime.UtcNow, DateTime.UtcNow.AddDays(30), null, [harness.Bob, harness.Alice]);

            var link = await harness.Feed.CreateAsync(harness.TenantId, harness.Bob);
            Assert.Contains($"UID:activity-{activity.Id}@ecosys", await harness.Feed.RenderAsync(link.Token, DateTime.UtcNow));

            await harness.Activities.CancelAsync(alice, activity.Id, null);
            var held = await harness.Activities.ScheduleAsync(alice, Meeting(harness, startsIn: TimeSpan.FromHours(-1), attendees: [new ActivityAttendeeInput(harness.Bob, null, null, null, null)]));
            await harness.Activities.MarkHeldAsync(alice, held.Id, held.Attendees.Select(x => x.Id).ToList());
            await harness.Activities.SaveMinutesAsync(alice, held.Id, "Agreed.");
            await harness.Activities.PublishMinutesAsync(alice, held.Id);
            await harness.Activities.AddAddendumAsync(alice, held.Id, "Correction.");
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SaveActivityCommand Meeting(Harness harness, string title = "Site meeting", TimeSpan? startsIn = null, IReadOnlyList<ActivityAttendeeInput>? attendees = null)
    {
        var start = DateTime.UtcNow.Add(startsIn ?? TimeSpan.FromDays(1));
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute, 0, DateTimeKind.Utc);
        return new SaveActivityCommand(ActivityTypes.Meeting, title, start, start.AddHours(1), false, "Site office", null, null, null, null, null, attendees ?? []);
    }

    private sealed class Harness
    {
        private Func<ITenantContext?, AppDbContext> createContext = null!;
        public AppDbContext Db { get; private set; } = null!;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OtherTenantId { get; } = Guid.NewGuid();
        public Guid Alice { get; private set; }
        public Guid Bob { get; private set; }
        public Guid Carol { get; private set; }
        public Guid Foreign { get; private set; }
        public RecordingOutbox Outbox { get; } = new();
        public RecordingPdf Pdf { get; } = new();
        public IActivityService Activities { get; private set; } = null!;
        public ICalendarService Calendar { get; private set; } = null!;
        public ICalendarFeedService Feed { get; private set; } = null!;
        private ActivityNotifier notifier = null!;

        public static Task<Harness> CreateAsync(bool seedLicense = false)
        {
            var databaseName = $"ecosys-activities-{Guid.NewGuid():N}";
            return CreateAsync(context => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options, context),
                seedTenants: true, seedLicense);
        }

        public static async Task<Harness> CreateAsync(Func<ITenantContext?, AppDbContext> createContext, bool seedTenants, bool seedLicense)
        {
            var harness = new Harness { createContext = createContext };
            harness.Db = createContext(null);
            if (seedTenants)
            {
                harness.Db.Tenants.AddRange(TestTenant(harness.TenantId), TestTenant(harness.OtherTenantId));
                await harness.Db.SaveChangesAsync();
            }
            if (seedLicense)
            {
                // Professional has every module the calendar reads; a migrated Postgres database already holds it.
                var plan = await harness.Db.LicensePlans.SingleOrDefaultAsync(x => x.PlanCode == ServiceOpsPlans.Professional);
                if (plan is null)
                {
                    plan = new LicensePlan { PlanCode = ServiceOpsPlans.Professional, DisplayName = "Professional", IsActive = true };
                    harness.Db.LicensePlans.Add(plan);
                }
                harness.Db.TenantLicenses.Add(new TenantLicense { TenantId = harness.TenantId, LicensePlanId = plan.Id, Status = "Active" });
                await harness.Db.SaveChangesAsync();
            }
            harness.Alice = await harness.AddUserAsync(harness.TenantId, "Alice", AppRoles.Manager);
            harness.Bob = await harness.AddUserAsync(harness.TenantId, "Bob", AppRoles.Technician);
            harness.Carol = await harness.AddUserAsync(harness.TenantId, "Carol", AppRoles.Dispatcher);
            harness.Foreign = await harness.AddUserAsync(harness.OtherTenantId, "Foreign", AppRoles.Manager);

            var settings = new ProjectSettingsService(harness.Db);
            var linked = new LinkedRecordService(harness.Db, settings);
            var dispatcher = new NotificationDispatcher(harness.Db, new RecordingTemplates(), new PassThroughSubjects(), harness.Outbox, new NullInboxNotifier(), [],
                NullLogger<NotificationDispatcher>.Instance);
            harness.notifier = new ActivityNotifier(harness.Db, dispatcher, linked, new UtcTimeZone());
            harness.Activities = new ActivityService(harness.Db, linked, harness.notifier, harness.Pdf, new NoOpAuditLogService());
            harness.Calendar = new CalendarService(harness.Db, linked);
            var license = new LicenseGuardService(harness.Db);
            var viewers = new AccessViewerFactory(new NoTenantContext(), license, new DynamicAccessService(harness.Db, new RolePermissionService(harness.Db)));
            harness.Feed = new CalendarFeedService(harness.Db, harness.Calendar, viewers, license);
            return harness;
        }

        public IActivityReminderRunner Reminders() =>
            new ActivityReminderRunner(Db, notifier, new UtcTimeZone(), NullLogger<ActivityReminderRunner>.Instance);

        /// <summary>A viewer with activities.view and every module unless told otherwise.</summary>
        public AccessViewer Viewer(Guid userId, params string[] permissions) =>
            Viewer(userId, permissions.Length == 0 ? Everyone : permissions, [.. ServiceOpsPlans.EditionModules(ServiceOpsPlans.Professional)]);

        public AccessViewer Viewer(Guid userId, IReadOnlyCollection<string> permissions, IReadOnlyCollection<string> modules, Guid? tenantId = null) =>
            new(tenantId ?? TenantId, userId, false, permissions.Contains, modules);

        public AccessViewer Viewer(Guid userId, Guid tenantId) => Viewer(userId, Everyone, [ServiceOpsPlans.Operations], tenantId);

        public async Task<Guid> AddUserAsync(Guid tenantId, string name, string role)
        {
            var user = new User { TenantId = tenantId, FullName = name, Email = $"{name.ToLowerInvariant()}@acme.test", Role = role, IsActive = true };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user.Id;
        }

        public async Task<Project> AddProjectAsync(Guid projectManager)
        {
            var client = new Client { TenantId = TenantId, ClientName = "Client", IsActive = true };
            var type = new ProjectType { TenantId = TenantId, Name = "Fit-out" };
            var stage = new ProjectStage { TenantId = TenantId, Name = "Active", Category = ProjectStageCategories.Active };
            var project = new Project
            {
                TenantId = TenantId, ClientId = client.Id, ProjectTypeId = type.Id, StageId = stage.Id, ProjectNumber = "PRJ-001", Name = "Westlands fit-out",
                ProjectManagerUserId = projectManager, Currency = "KES",
            };
            Db.AddRange(client, type, stage, project);
            await Db.SaveChangesAsync();
            return project;
        }

        /// <summary>Who was told what: one entry per bell row, oldest first.</summary>
        public async Task<List<(Guid UserId, string Key)>> SentAsync() =>
            (await Db.Notifications.OrderBy(x => x.CreatedAt).ThenBy(x => x.Key).ToListAsync())
                .Select(x => (x.UserId, x.Key)).OrderBy(x => x.UserId == Alice ? 0 : x.UserId == Bob ? 1 : 2).ThenBy(x => x.Key).ToList();

        public void Clear()
        {
            Db.Notifications.RemoveRange(Db.Notifications);
            Db.SaveChanges();
            Outbox.Queued.Clear();
        }
    }

    private static Tenant TestTenant(Guid id) => new()
    {
        Id = id, Name = "Tenant", Slug = $"t-{id:N}"[..14], CompanyName = "Acme Builders", Email = $"{id:N}@tenant.test", Country = "Kenya",
        Status = "Active", LicenseStatus = "Active", PrimaryColor = "#0F4C81", SecondaryColor = "#F4B942", IsActive = true,
    };

    internal sealed class RecordingPdf : IPdfRenderer
    {
        public List<string> Sections { get; } = [];
        public byte[] RenderWorkOrderReportPdf(string title, IReadOnlyCollection<string> sections)
        {
            Sections.Clear();
            Sections.Add(title);
            Sections.AddRange(sections);
            return [0x25, 0x50, 0x44, 0x46];
        }
        public byte[] RenderWorkOrderReportPdf(WorkOrderReportPdfModel report) => throw new NotSupportedException();
        public byte[] RenderInvoicePdf(InvoicePdfModel model) => throw new NotSupportedException();
        public byte[] RenderQuotationPdf(QuotationPdfModel model) => throw new NotSupportedException();
        public byte[] RenderReceiptPdf(ReceiptPdfModel model) => throw new NotSupportedException();
        public byte[] RenderClientStatementPdf(ClientStatementPdfModel model) => throw new NotSupportedException();
        public byte[] RenderPurchaseOrderPdf(PurchaseOrderPdfModel model) => throw new NotSupportedException();
    }

    internal sealed class RecordingOutbox : IEmailOutboxService
    {
        public List<QueueEmailRequest> Queued { get; } = [];
        public Task<EmailOutboxMessage> QueueEmailAsync(QueueEmailRequest request, CancellationToken cancellationToken = default)
        {
            Queued.Add(request);
            return Task.FromResult(new EmailOutboxMessage { RecipientEmail = request.RecipientEmail, Subject = request.Subject, Status = EmailOutboxStatuses.Pending });
        }
        public Task CancelAsync(Guid outboxMessageId, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyCollection<EmailOutboxMessage>> GetPendingBatchAsync(int batchSize, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<EmailOutboxMessage>>([]);
        public Task<bool> MarkSendingAsync(Guid outboxMessageId, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<int> ReleaseAbandonedClaimsAsync(TimeSpan claimTimeout, DateTime utcNow, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkSentAsync(Guid outboxMessageId, DateTime utcNow, string? providerMessageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MarkFailedAsync(Guid outboxMessageId, string? errorCategory, string? errorMessage, DateTime utcNow, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RetryAsync(Guid outboxMessageId, DateTime nextAttemptAt, string? errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingTemplates : IEmailTemplateService
    {
        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedEmailTemplate(eventKey, eventKey, eventKey, "<p>ok</p>", "ok", null, null, true));
        public Task<EmailTemplateDescriptor> GetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> GetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListPlatformTemplatesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<EmailTemplateDescriptor>> ListTenantTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RenderedEmailTemplate> RenderPlatformTemplateAsync(string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetPlatformTemplateAsync(string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> ResetTenantTemplateAsync(Guid tenantId, string eventKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SavePlatformTemplateAsync(string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EmailTemplateDescriptor> SaveTenantTemplateAsync(Guid tenantId, string eventKey, EmailTemplateUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class PassThroughSubjects : IEmailSubjectRuleService
    {
        public Task<string> BuildFinalSubjectAsync(Guid? tenantId, string eventKey, string templateSubject, string? tenantNameOverride = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(templateSubject);
        public Task<Ecosys.Shared.Options.EmailSubjectRuleOptions> GetSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Ecosys.Shared.Options.EmailSubjectRuleOptions.Default);
    }

    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LogAsync(Guid? tenantId, Guid? userId, string action, string entityName, string entityId, string? details, string severity = "Info", string? actorName = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UtcTimeZone : ITenantTimeZoneService
    {
        public Task<string> GetPlatformTimeZoneIdAsync(CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<string> GetTimeZoneIdAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult("UTC");
        public Task<TimeZoneInfo> GetTimeZoneAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(TimeZoneInfo.Utc);
        public Task<DateTime> ToTenantTimeAsync(DateTime utcValue, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(utcValue);
        public Task<DateTime> TenantTodayAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(DateTime.UtcNow.Date);
        public Task<DateTime> TenantDayEndUtcAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(DateTime.UtcNow.Date.AddDays(1).AddTicks(-1));
        public Task<string> FormatForTenantAsync(DateTime utcValue, Guid tenantId, string format = "dd MMM yyyy 'at' HH:mm", CancellationToken cancellationToken = default) =>
            Task.FromResult(utcValue.ToString(format));
        public string? NormalizeTimeZoneId(string? timeZoneId) => timeZoneId;
        public void Invalidate(Guid? tenantId = null) { }
    }

    /// <summary>The feed is anonymous: no tenant, no user.</summary>
    private sealed class NoTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public Guid? UserId => null;
        public Guid? SessionId => null;
        public string? Email => null;
        public string? Role => null;
        public string? JobTitle => null;
        public bool IsAuthenticated => false;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => false;
        public bool HasRole(string role) => false;
        public bool HasPermission(string permissionName) => false;
        public Guid GetRequiredTenantId() => throw new UnauthorizedAccessException();
        public Guid GetRequiredUserId() => throw new UnauthorizedAccessException();
        public Guid GetRequiredSessionId() => throw new UnauthorizedAccessException();
    }

    /// <summary>A freshly migrated database (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).</summary>
    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_activities_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(database).Options))
                await setup.Database.MigrateAsync();
            await body(database);
        }
        finally
        {
            await ExecuteAdminAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
