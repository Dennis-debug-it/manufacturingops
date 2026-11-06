using Ecosys.Api.Controllers;
using Ecosys.Domain.Entities;
using Ecosys.Infrastructure.Data;
using Ecosys.Infrastructure.Services;
using Ecosys.Shared.Auth;
using Ecosys.Shared.Contracts.Integration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Ecosys.Tests;

/// <summary>Release 3 step 3a: the dispatcher, the bell's API, row-based push and housekeeping.</summary>
public sealed class NotificationInboxTests
{
    // ── Dispatcher ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatcher_AppliesEachRecipientsPreferencePerChannel()
    {
        var harness = await Harness.CreateAsync();
        // Alice has no saved row (key defaults: bell, push and email). Bob wants push only. Carol wants nothing.
        harness.Db.UserNotificationPreferences.AddRange(
            Preference(harness, harness.Bob, inApp: false, push: true, email: false),
            Preference(harness, harness.Carol, inApp: false, push: false, email: false));
        await harness.Db.SaveChangesAsync();

        await harness.Dispatcher.NotifyAsync(Request(harness, [harness.Alice, harness.Bob, harness.Carol]));

        var alice = await harness.Db.Notifications.SingleAsync(x => x.UserId == harness.Alice);
        Assert.Null(alice.ReadAt);
        Assert.Null(alice.ArchivedAt);
        Assert.NotNull(alice.PushPendingSince);
        Assert.Equal("/activities", alice.Url);

        // A push-only row exists for the push worker but never shows in the bell.
        var bob = await harness.Db.Notifications.SingleAsync(x => x.UserId == harness.Bob);
        Assert.NotNull(bob.PushPendingSince);
        Assert.NotNull(bob.ArchivedAt);
        Assert.False(await harness.Db.Notifications.AnyAsync(x => x.UserId == harness.Carol));

        var email = Assert.Single(harness.Outbox.Queued);
        Assert.Equal("alice@acme.test", email.RecipientEmail);
        Assert.Equal("Alice Wanjiru", Assert.Single(harness.Templates.Rendered).Values["RecipientName"]);

        // Only the bell row is announced live, with the recipient's new unread count.
        Assert.Equal([(harness.Alice, 1)], harness.Live.Counts);
        Assert.Equal(alice.Id, Assert.Single(harness.Live.Created).Notification.Id);
    }

    [Fact]
    public async Task Dispatcher_SkipsOtherTenantsAndInactiveUsers_AndHonoursChannelLimit()
    {
        var harness = await Harness.CreateAsync();
        var inactive = await harness.AddUserAsync(harness.TenantId, "Gone", isActive: false);
        var foreign = await harness.AddUserAsync(harness.OtherTenantId, "Foreign");

        await harness.Dispatcher.NotifyAsync(Request(harness, [harness.Alice, inactive, foreign]) with
        {
            Channels = NotificationChannels.InApp | NotificationChannels.Push,
        });

        Assert.Equal(harness.Alice, (await harness.Db.Notifications.IgnoreQueryFilters().SingleAsync()).UserId);
        Assert.Empty(harness.Outbox.Queued);
    }

    [Fact]
    public async Task Dispatcher_UsesKeyDefaults_AndPassesExtraChannelsOnlyWhatTheKeyAllows()
    {
        var harness = await Harness.CreateAsync();
        var whatsApp = new RecordingChannel(NotificationChannels.WhatsApp);
        var dispatcher = harness.CreateDispatcher([whatsApp]);
        harness.Db.UserNotificationPreferences.Add(Preference(harness, harness.Bob, inApp: true, push: true, email: true, whatsApp: true));
        await harness.Db.SaveChangesAsync();

        // An email-only key never reaches the bell or push, whatever the preference says.
        await dispatcher.NotifyAsync(Request(harness, [harness.Alice], key: "invoice.sent"));
        Assert.False(await harness.Db.Notifications.AnyAsync());
        Assert.Single(harness.Outbox.Queued);

        // A variation is not a WhatsApp key, so the hook is not called even though Bob turned WhatsApp on.
        await dispatcher.NotifyAsync(Request(harness, [harness.Bob]));
        Assert.Empty(whatsApp.Deliveries);

        // A task-due reminder is (step 3d), but only for a user who turned it on: WhatsApp is never a default.
        harness.Db.UserNotificationPreferences.Add(Preference(harness, harness.Bob, inApp: true, push: true, email: true, whatsApp: true, key: ActivityNotificationKeys.ActionItemDue));
        await harness.Db.SaveChangesAsync();
        await dispatcher.NotifyAsync(Request(harness, [harness.Alice, harness.Bob], key: ActivityNotificationKeys.ActionItemDue));
        Assert.Equal(harness.Bob, Assert.Single(whatsApp.Deliveries).UserId);
    }

    [Fact]
    public async Task WorkOrderAssignmentPush_HonoursThePushPreference()
    {
        var harness = await Harness.CreateAsync();
        harness.Db.UserNotificationPreferences.Add(Preference(harness, harness.Bob, inApp: true, push: false, email: true, key: AssignmentNotifications.Key));
        var workOrder = new WorkOrder { TenantId = harness.TenantId, WorkOrderNumber = "WO-0042", Title = "Chiller trip", Priority = "High", Status = "New" };
        await harness.Db.SaveChangesAsync();

        await AssignmentNotifications.NotifyAsync(harness.Dispatcher, workOrder,
        [
            new Technician { TenantId = harness.TenantId, UserId = harness.Alice, FullName = "Alice" },
            new Technician { TenantId = harness.TenantId, UserId = harness.Bob, FullName = "Bob" },
            new Technician { TenantId = harness.TenantId, FullName = "No login" },
        ], CancellationToken.None);

        var rows = await harness.Db.Notifications.ToDictionaryAsync(x => x.UserId);
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows[harness.Alice].PushPendingSince);
        Assert.Null(rows[harness.Bob].PushPendingSince);
        Assert.Equal($"/tech/jobs/{workOrder.Id}", rows[harness.Bob].Url);
        Assert.Equal("WO-0042", rows[harness.Bob].Title[^7..]);
        // The assignment email goes to the technician record from the workflow service, not from here.
        Assert.Empty(harness.Outbox.Queued);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EscalationReassignPush_HonoursThePushPreference(bool pushEnabled)
    {
        var harness = await Harness.CreateAsync();
        harness.Db.UserNotificationPreferences.Add(new UserNotificationPreference
        {
            TenantId = harness.TenantId, UserId = harness.Bob, NotificationKey = AssignmentNotifications.Key, PushEnabled = pushEnabled,
        });
        var client = new Client { TenantId = harness.TenantId, ClientName = "Acme", IsActive = true };
        var technician = new Technician { TenantId = harness.TenantId, UserId = harness.Bob, FullName = "Bob", IsActive = true };
        var workOrder = new WorkOrder { TenantId = harness.TenantId, ClientId = client.Id, WorkOrderNumber = "WO-7", Title = "Generator", Priority = "Medium", Status = "New" };
        harness.Db.AddRange(client, technician, workOrder);
        harness.Db.EscalationRules.Add(new EscalationRule
        {
            TenantId = harness.TenantId, Name = "To Bob", IsActive = true, TriggerCondition = EscalationConditions.UnassignedAfterMins,
            TriggerValue = 60, Action = EscalationActions.ReassignToUserId, ActionValue = harness.Bob.ToString(), CooldownMins = 60,
        });
        await harness.Db.SaveChangesAsync();
        workOrder.CreatedAt = DateTime.UtcNow.AddHours(-3);
        await harness.Db.SaveChangesAsync();
        var service = new EscalationRuleService(harness.Db, harness.Outbox, NullLogger<EscalationRuleService>.Instance, harness.Dispatcher);

        Assert.Equal(1, await service.EvaluateAllTenantsAsync(DateTime.UtcNow));

        // The bell entry either way; the push only when it is on. The escalation email is unchanged.
        var row = await harness.Db.Notifications.SingleAsync();
        Assert.Equal((harness.Bob, AssignmentNotifications.Key, workOrder.Id), (row.UserId, row.Key, row.EntityId));
        Assert.Null(row.ArchivedAt);
        Assert.Equal(pushEnabled, row.PushPendingSince.HasValue);
        Assert.Equal("bob@acme.test", Assert.Single(harness.Outbox.Queued).RecipientEmail);
    }

    // ── Inbox API ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Inbox_ShowsOnlyTheCallersOwnRows()
    {
        var harness = await Harness.CreateAsync();
        var mine = Row(harness.TenantId, harness.Alice, "Mine");
        var bobs = Row(harness.TenantId, harness.Bob, "Bob's");
        // Same user id under another tenant (defence in depth: the query checks both).
        var foreign = Row(harness.OtherTenantId, harness.Alice, "Foreign");
        var archived = Row(harness.TenantId, harness.Alice, "Archived");
        archived.ArchivedAt = DateTime.UtcNow;
        harness.Db.Notifications.AddRange(mine, bobs, foreign, archived);
        await harness.Db.SaveChangesAsync();
        var controller = harness.Controller(harness.Alice);

        var page = Ok(await controller.List(false, null, null, CancellationToken.None));
        Assert.Equal("Mine", Assert.Single(page.Items).Title);
        Assert.Equal(1, Ok(await controller.UnreadCount(CancellationToken.None)).Count);
        Assert.IsType<NotFoundResult>((await controller.MarkRead(bobs.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Archive(foreign.Id, CancellationToken.None)).Result);
        Assert.Null((await harness.Db.Notifications.IgnoreQueryFilters().SingleAsync(x => x.Id == bobs.Id)).ReadAt);
    }

    [Fact]
    public async Task Inbox_ReadReadAllAndArchive_UpdateTheCountEverywhere()
    {
        var harness = await Harness.CreateAsync();
        var rows = Enumerable.Range(0, 3).Select(i => Row(harness.TenantId, harness.Alice, $"N{i}")).ToList();
        harness.Db.Notifications.AddRange(rows);
        await harness.Db.SaveChangesAsync();
        var controller = harness.Controller(harness.Alice);

        Assert.Equal(2, Ok(await controller.MarkRead(rows[0].Id, CancellationToken.None)).Count);
        Assert.Equal(2, Ok(await controller.MarkRead(rows[0].Id, CancellationToken.None)).Count); // idempotent
        Assert.Single(Ok(await controller.List(true, null, null, CancellationToken.None)).Items, x => x.Id == rows[1].Id);

        Assert.Equal(1, Ok(await controller.Archive(rows[1].Id, CancellationToken.None)).Count);
        Assert.Equal(2, Ok(await controller.List(false, null, null, CancellationToken.None)).Items.Count);

        Assert.Equal(0, Ok(await controller.MarkAllRead(CancellationToken.None)).Count);
        Assert.Empty(Ok(await controller.List(true, null, null, CancellationToken.None)).Items);
        Assert.Equal([(harness.Alice, 2), (harness.Alice, 2), (harness.Alice, 1), (harness.Alice, 0)], harness.Live.Counts);
    }

    [Fact]
    public async Task Inbox_PagesNewestFirst()
    {
        var harness = await Harness.CreateAsync();
        var rows = Enumerable.Range(0, 5).Select(i => Row(harness.TenantId, harness.Alice, $"N{i}")).ToList();
        harness.Db.Notifications.AddRange(rows);
        await harness.Db.SaveChangesAsync();
        var start = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < rows.Count; i++)
            rows[i].CreatedAt = start.AddMinutes(i);
        await harness.Db.SaveChangesAsync();
        var controller = harness.Controller(harness.Alice);

        var first = Ok(await controller.List(false, null, 2, CancellationToken.None));
        Assert.Equal(["N4", "N3"], first.Items.Select(x => x.Title));
        Assert.True(first.HasMore);
        var last = Ok(await controller.List(false, first.Items[^1].CreatedAt, 10, CancellationToken.None));
        Assert.Equal(["N2", "N1", "N0"], last.Items.Select(x => x.Title));
        Assert.False(last.HasMore);
    }

    [Fact]
    public async Task Preferences_ExposeChannelsAndDefaults_AndAnOlderClientLeavesPushAlone()
    {
        var harness = await Harness.CreateAsync();
        var controller = new UserNotificationPreferencesController(harness.Db, harness.Context(harness.Alice));

        var all = Ok(await controller.Get(CancellationToken.None));
        var assigned = all.Single(x => x.NotificationKey == "work-order.assigned");
        Assert.Equal(["InApp", "Push", "Email"], assigned.Channels);
        Assert.True(assigned.InAppEnabled && assigned.PushEnabled && assigned.EmailEnabled);
        Assert.False(assigned.WhatsAppEnabled);
        Assert.Equal(["Email"], all.Single(x => x.NotificationKey == "invoice.sent").Channels);

        await controller.Update([new UpdateNotificationPreferenceInput("work-order.assigned", true, true, PushEnabled: false)], CancellationToken.None);
        await controller.Update([new UpdateNotificationPreferenceInput("work-order.assigned", false, true)], CancellationToken.None);

        var saved = await harness.Db.UserNotificationPreferences.SingleAsync();
        Assert.False(saved.PushEnabled);
        Assert.False(saved.EmailEnabled);
        Assert.IsType<BadRequestObjectResult>(await controller.Update([new UpdateNotificationPreferenceInput("nope", true, true)], CancellationToken.None));
    }

    // ── Push worker and housekeeping ──────────────────────────────────────────

    [Fact]
    public async Task PushWorker_SendsFromRows_DropsStaleOnes_AndRetriesFailures()
    {
        var harness = await Harness.CreateAsync();
        var now = DateTime.UtcNow;
        var fresh = Row(harness.TenantId, harness.Alice, "Fresh", pushPendingSince: now.AddMinutes(-1));
        var stale = Row(harness.TenantId, harness.Alice, "Stale", pushPendingSince: now.AddHours(-25));
        var failing = Row(harness.TenantId, harness.Bob, "Failing", pushPendingSince: now.AddMinutes(-2));
        var sent = Row(harness.TenantId, harness.Alice, "Already sent");
        harness.Db.Notifications.AddRange(fresh, stale, failing, sent);
        await harness.Db.SaveChangesAsync();
        var sender = new RecordingPushSender { FailFor = harness.Bob };

        // A fresh context and processor: what an instance that has just restarted would see.
        await using (var db = harness.NewContext())
            Assert.Equal(2, await new NotificationPushProcessor(db, sender, NullLogger<NotificationPushProcessor>.Instance).ProcessPendingAsync(now));

        var push = Assert.Single(sender.Sent);
        Assert.Equal([harness.Alice], push.UserIds);
        Assert.Equal("Fresh", push.Message.Title);
        Assert.Equal($"action-item.assigned-{fresh.EntityId}", push.Message.Tag);
        var pending = await harness.NewContext().Notifications.Where(x => x.PushPendingSince != null).Select(x => x.Title).ToListAsync();
        Assert.Equal(["Failing"], pending);

        sender.FailFor = null;
        await using (var db = harness.NewContext())
            Assert.Equal(1, await new NotificationPushProcessor(db, sender, NullLogger<NotificationPushProcessor>.Instance).ProcessPendingAsync(now));
        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task Housekeeping_DeletesRowsOlderThan90Days_InBatches()
    {
        var harness = await Harness.CreateAsync();
        var now = DateTime.UtcNow;
        var old = Enumerable.Range(0, NotificationHousekeeping.BatchSize + 5).Select(i => Row(harness.TenantId, harness.Alice, $"Old {i}")).ToList();
        var kept = Row(harness.TenantId, harness.Alice, "Recent");
        harness.Db.Notifications.AddRange(old);
        harness.Db.Notifications.Add(kept);
        await harness.Db.SaveChangesAsync();
        foreach (var row in old)
            row.CreatedAt = now.AddDays(-91);
        kept.CreatedAt = now.AddDays(-89);
        await harness.Db.SaveChangesAsync();

        await using var db = harness.NewContext();
        Assert.Equal(old.Count, await new NotificationHousekeeping(db).DeleteExpiredAsync(now));
        Assert.Equal("Recent", (await db.Notifications.SingleAsync()).Title);
    }

    // ── PostgreSQL ─────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "PostgreSql")]
    public async Task Postgres_DispatchInboxPushAndHousekeeping_AllTranslate()
    {
        await WithPostgresAsync(async connection =>
        {
            var harness = await Harness.CreateAsync(context => PostgresContext(connection, context), seedTenants: true);
            harness.Db.UserNotificationPreferences.Add(Preference(harness, harness.Bob, inApp: false, push: true, email: false));
            await harness.Db.SaveChangesAsync();

            await harness.Dispatcher.NotifyAsync(Request(harness, [harness.Alice, harness.Bob]));
            await harness.Dispatcher.NotifyAsync(Request(harness, [harness.Alice]) with { Title = "Second", Severity = NotificationSeverities.Warning });
            Assert.Equal([(harness.Alice, 1), (harness.Alice, 2)], harness.Live.Counts);

            var controller = harness.Controller(harness.Alice);
            var page = Ok(await controller.List(true, null, 1, CancellationToken.None));
            Assert.Equal("Second", Assert.Single(page.Items).Title);
            Assert.True(page.HasMore);
            Assert.Single(Ok(await controller.List(false, page.Items[0].CreatedAt, null, CancellationToken.None)).Items);
            Assert.Equal(1, Ok(await controller.MarkRead(page.Items[0].Id, CancellationToken.None)).Count);
            Assert.Equal(1, Ok(await controller.Archive(page.Items[0].Id, CancellationToken.None)).Count);
            Assert.Equal(0, Ok(await controller.MarkAllRead(CancellationToken.None)).Count);

            var sender = new RecordingPushSender();
            await using (var db = harness.NewContext())
                Assert.Equal(3, await new NotificationPushProcessor(db, sender, NullLogger<NotificationPushProcessor>.Instance).ProcessPendingAsync(DateTime.UtcNow));
            Assert.Equal(3, sender.Sent.Count);

            await using (var db = harness.NewContext())
            {
                await db.Notifications.ExecuteUpdateAsync(s => s.SetProperty(x => x.CreatedAt, DateTime.UtcNow.AddDays(-100)));
                Assert.Equal(3, await new NotificationHousekeeping(db).DeleteExpiredAsync(DateTime.UtcNow));
            }
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static T Ok<T>(ActionResult<T> result) =>
        Assert.IsAssignableFrom<T>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static NotificationRequest Request(Harness harness, IReadOnlyCollection<Guid> userIds, string key = ActivityNotificationKeys.ActionItemAssigned) =>
        new(harness.TenantId, key, userIds, "Variation VO-001 to review", "Addition of KES 50,000.00 on PRJ-001.",
            "/activities", "ActionItem", Guid.NewGuid(),
            new Dictionary<string, string?> { ["Title"] = "Resize ducts" });

    private static UserNotificationPreference Preference(Harness harness, Guid userId, bool inApp, bool push, bool email, bool whatsApp = false,
        string key = ActivityNotificationKeys.ActionItemAssigned) => new()
    {
        TenantId = harness.TenantId, UserId = userId, NotificationKey = key,
        InAppEnabled = inApp, PushEnabled = push, EmailEnabled = email, WhatsAppEnabled = whatsApp,
    };

    private static Notification Row(Guid tenantId, Guid userId, string title, DateTime? pushPendingSince = null) => new()
    {
        TenantId = tenantId, UserId = userId, Key = ActivityNotificationKeys.ActionItemAssigned, Title = title, Body = "Body",
        EntityType = "ActionItem", EntityId = Guid.NewGuid(), PushPendingSince = pushPendingSince,
    };

    private static Tenant TestTenant(Guid id) => new()
    {
        Id = id, Name = "Tenant", Slug = $"t-{id:N}"[..14], CompanyName = "Tenant Co", Email = $"{id:N}@tenant.test", Country = "Kenya",
        Status = "Active", LicenseStatus = "Active", PrimaryColor = "#0F4C81", SecondaryColor = "#F4B942", IsActive = true,
    };

    private sealed class Harness
    {
        private Func<ITenantContext?, AppDbContext> createContext = null!;
        public AppDbContext Db { get; private set; } = null!;
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid OtherTenantId { get; } = Guid.NewGuid();
        public Guid Alice { get; private set; }
        public Guid Bob { get; private set; }
        public Guid Carol { get; private set; }
        public RecordingOutbox Outbox { get; } = new();
        public RecordingTemplates Templates { get; } = new();
        public RecordingInboxNotifier Live { get; } = new();
        public INotificationDispatcher Dispatcher { get; private set; } = null!;

        public static Task<Harness> CreateAsync()
        {
            var databaseName = $"ecosys-inbox-{Guid.NewGuid():N}";
            return CreateAsync(context => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options, context), seedTenants: false);
        }

        public static async Task<Harness> CreateAsync(Func<ITenantContext?, AppDbContext> createContext, bool seedTenants)
        {
            var harness = new Harness { createContext = createContext };
            if (seedTenants)
            {
                await using var seed = createContext(null);
                seed.Tenants.AddRange(TestTenant(harness.TenantId), TestTenant(harness.OtherTenantId));
                await seed.SaveChangesAsync();
            }

            harness.Db = createContext(null);
            harness.Alice = await harness.AddUserAsync(harness.TenantId, "Alice Wanjiru");
            harness.Bob = await harness.AddUserAsync(harness.TenantId, "Bob");
            harness.Carol = await harness.AddUserAsync(harness.TenantId, "Carol");
            harness.Dispatcher = harness.CreateDispatcher([]);
            return harness;
        }

        public async Task<Guid> AddUserAsync(Guid tenantId, string name, bool isActive = true)
        {
            var user = new User
            {
                TenantId = tenantId, FullName = name, Email = $"{name.Split(' ')[0].ToLowerInvariant()}@acme.test", Role = AppRoles.Technician, IsActive = isActive,
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
            return user.Id;
        }

        public INotificationDispatcher CreateDispatcher(IEnumerable<INotificationChannel> channels) =>
            new NotificationDispatcher(Db, Templates, new PassThroughSubjects(), Outbox, Live, channels, NullLogger<NotificationDispatcher>.Instance);

        public AppDbContext NewContext() => createContext(null);

        public ITenantContext Context(Guid userId) => new FakeTenantContext(TenantId, userId);

        /// <summary>The controller runs on a context filtered to the caller's tenant, as it does in a request.</summary>
        public NotificationsController Controller(Guid userId)
        {
            var context = Context(userId);
            return new NotificationsController(createContext(context), Live, context);
        }
    }

    private sealed class FakeTenantContext(Guid tenantId, Guid userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public Guid? SessionId => null;
        public string? Email => null;
        public string? Role => AppRoles.Technician;
        public string? JobTitle => null;
        public bool IsAuthenticated => true;
        public bool IsSuperAdmin => false;
        public bool IsAdmin => false;
        public bool HasRole(string role) => role == AppRoles.Technician;
        public bool HasPermission(string permissionName) => false;
        public Guid GetRequiredTenantId() => tenantId;
        public Guid GetRequiredUserId() => userId;
        public Guid GetRequiredSessionId() => throw new NotSupportedException();
    }

    private sealed class RecordingInboxNotifier : IInboxNotifier
    {
        public List<(Guid UserId, InboxNotification Notification)> Created { get; } = [];
        public List<(Guid UserId, int Count)> Counts { get; } = [];

        public Task NotificationCreatedAsync(Guid userId, InboxNotification notification, CancellationToken cancellationToken = default)
        {
            Created.Add((userId, notification));
            return Task.CompletedTask;
        }

        public Task UnreadCountChangedAsync(Guid userId, int unreadCount, CancellationToken cancellationToken = default)
        {
            Counts.Add((userId, unreadCount));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingChannel(NotificationChannels channel) : INotificationChannel
    {
        public NotificationChannels Channel => channel;
        public List<NotificationDelivery> Deliveries { get; } = [];

        public Task SendAsync(NotificationDelivery delivery, CancellationToken cancellationToken = default)
        {
            Deliveries.Add(delivery);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPushSender : IWebPushSender
    {
        public Guid? FailFor { get; set; }
        public List<(IReadOnlyCollection<Guid> UserIds, PushMessage Message)> Sent { get; } = [];

        public Task SendAsync(AppDbContext db, Guid tenantId, IReadOnlyCollection<Guid> userIds, PushMessage message, CancellationToken cancellationToken = default)
        {
            if (FailFor is { } failing && userIds.Contains(failing))
                throw new InvalidOperationException("Push service unavailable.");
            Sent.Add((userIds, message));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutbox : IEmailOutboxService
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
        public List<(string Key, IReadOnlyDictionary<string, string?> Values)> Rendered { get; } = [];
        public Task<RenderedEmailTemplate> RenderTenantTemplateAsync(Guid tenantId, string eventKey, IReadOnlyDictionary<string, string?> values, CancellationToken cancellationToken = default)
        {
            Rendered.Add((eventKey, values));
            return Task.FromResult(new RenderedEmailTemplate(eventKey, eventKey, eventKey, "<p>ok</p>", "ok", null, null, true));
        }
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

    private static AppDbContext PostgresContext(string connection, ITenantContext? tenantContext) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options, tenantContext);

    /// <summary>A freshly migrated database (ECOSYS_TEST_DB_CONNECTION, default localhost:5432 postgres/postgres).</summary>
    private static async Task WithPostgresAsync(Func<string, Task> body)
    {
        var server = Environment.GetEnvironmentVariable("ECOSYS_TEST_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ecosys_test;Username=postgres;Password=postgres";
        var databaseName = $"ecosys_inbox_{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(server) { Database = databaseName, Pooling = false }.ConnectionString;
        var admin = new NpgsqlConnectionStringBuilder(server) { Database = "postgres", Pooling = false }.ConnectionString;

        await ExecuteAdminAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        try
        {
            await using (var setup = PostgresContext(database, null))
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
