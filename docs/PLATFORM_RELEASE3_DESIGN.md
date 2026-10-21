# Release 3: Shared platform — design for review

Status: **decisions made (October 2026). Ready to build.**
Scope: the four items the Projects design (section 13) deferred because they serve every module, not
just Projects:

1. An in-app notification inbox (the bell).
2. Calendar & Activities: meetings, minutes and action items.
3. WhatsApp reminders.
4. Role dashboards across modules.

Each part can ship on its own. Part 1 comes first because parts 2–4 send notifications through it.

---

## 0. Decisions

| # | Question | Decision |
|---|---|---|
| R1 | Live updates for the bell | **SignalR**, with polling every 60 s as the fallback when the connection drops. The hub already exists; only the frontend client is missing. |
| R2 | How long are notifications kept? | **90 days**, then deleted. A user can archive read ones before that. |
| R3 | Meeting action items | **A new `ActionItem` list**, shown on the linked record (a project's Tasks tab lists both, clearly labelled). Meetings also happen for clients and contracts, which have no task list. |
| R4 | External calendars | **No emailed meeting invites for now.** Staff are told about meetings in the bell and by email (no `.ics` attached). The in-app calendar and the private one-way feed link stay (3.5). Two-way sync is not planned. |
| R5 | WhatsApp number | **One shared Ecosys number for now.** Ecosys owns the WhatsApp Business account and its templates, and pays Meta. Every message names the tenant. Built so a tenant can move to its own number later (4.1). |
| R6 | Who gets WhatsApp | **Staff and clients from the start**, both opt-in only (4.2). |
| R7 | SMS | **No SMS.** Email is the fallback channel. |
| R8 | Dashboard layouts | **Users can rearrange their own.** Each role has a default layout; a user can reorder, hide and add widgets they're allowed to see, and reset to the default. A tenant admin can still hide widgets for a role (5.1). |

---

## 1. What exists today

| Area | Today | Gap |
|---|---|---|
| Notification keys | `NotificationKeyDescriptor` (32 keys), `EmailNotificationRegistry`, email templates | — |
| Preferences | `UserNotificationPreference` (EmailEnabled, InAppEnabled), editable only in the technician app | No office screen. `InAppEnabled` only gates web push, and only in `ProjectNotificationService`; work-order and escalation pushes ignore it |
| Web push | `IPushNotificationQueue`, `PushNotificationWorker`, VAPID keys, `PushSubscription` | The queue is in memory (1,000 items, drops the oldest), so a restart loses queued pushes |
| Live updates | `ServiceOpsHub` at `/hubs/serviceops`, tenant groups, one event (`WorkOrderUpdated`) | No per-user group. Nothing in the frontend connects to it |
| Inbox | — | No table, no bell, no unread count |
| Calendars | Month grids for PM plans, the work-order calendar view and the scheduling board | No meetings or activities. No ICS, no external calendar |
| Meetings | Platform CRM has `CrmActivity` (platform staff only) | Nothing for tenants |
| WhatsApp | `WhatsAppSetting`, conversations, messages and templates tables, and `WhatsAppController`; turned off (`WHATSAPP_ENABLED = false`) | Sending only saves a row: there is no Meta API call, no webhook, no opt-in |
| Phone numbers | `User.Phone`, `Technician.Phone` | Not checked as E.164, no WhatsApp opt-in |
| Dashboards | `GET /api/dashboard/summary` (work orders, assets, stock, contracts), the executive dashboard, permission-gated sections | No per-role views. Nothing for Projects or receivables |

---

## 2. Part 1: Notification inbox and one dispatcher

### 2.1 One way to notify

Today each module sends its own email and push (`ProjectNotificationService`, the work-order
assignment workflow, escalations). Release 3 adds a single entry point, and the existing senders move
onto it:

```csharp
public sealed record NotificationRequest(
    Guid TenantId,
    string Key,                       // a NotificationKeyDescriptor key
    IReadOnlyCollection<Guid> UserIds,
    string Title,
    string Body,
    string? Url,                      // in-app link, e.g. /projects/{id}?tab=valuations
    string? EntityType, Guid? EntityId,
    IReadOnlyDictionary<string, string?> TemplateValues,
    string Severity = "Info");        // Info | Warning | Critical

public interface INotificationDispatcher
{
    Task NotifyAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
```

For each recipient, the dispatcher applies their preference for that key and then:
- **In-app:** writes a `Notification` row and pushes it to the user's SignalR group `user-{UserId}`.
- **Push:** marks the row for push. The push worker sends from the table (see 2.3).
- **Email:** renders the tenant template and queues it in the existing email outbox, as today.
- **WhatsApp** (part 3): queues a template message when the user opted in and the key allows it.

`ProjectNotificationService.NotifyAsync`, the work-order assignment push and the escalation push call
the dispatcher instead of the channels directly. That also fixes the pushes that ignore preferences.

### 2.2 Data

```csharp
public sealed class Notification : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; }
    public string Title { get; set; }          // max 200
    public string Body { get; set; }           // max 1000, plain text
    public string? Url { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string Severity { get; set; } = "Info";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime? PushPendingSince { get; set; }  // set when push is wanted; cleared once sent
}
// Index (TenantId, UserId, ArchivedAt, CreatedAt desc) for the bell; (PushPendingSince) for the worker.
```

`UserNotificationPreference` gains `PushEnabled` and `WhatsAppEnabled`. `InAppEnabled` then means
"show it in the bell", which matches its name. A missing row means the key's default (`NotificationKeyDescriptor`
gains `DefaultChannels`, so noisy keys can default to in-app only).

### 2.3 Push that survives a restart

The push worker reads `Notification` rows with `PushPendingSince` set, rather than the in-memory
queue. It sends them and clears the flag. A crash means a delayed push, never a lost one. A push older
than 24 hours is dropped as stale. The in-memory queue is kept only for the test "send me a push"
button.

### 2.4 API (Shared controller, every plan)

```
GET    /api/me/notifications?unreadOnly&before&limit   newest first, 50 per page
GET    /api/me/notifications/unread-count
POST   /api/me/notifications/{id}/read
POST   /api/me/notifications/read-all
POST   /api/me/notifications/{id}/archive
GET    /api/me/notification-preferences                 (exists; gains push/WhatsApp flags)
PUT    /api/me/notification-preferences
```

A user only ever sees their own rows. There is no admin view of other people's inboxes.

### 2.5 Frontend

- **Bell** in the office topbar and the technician app header, showing an unread badge (capped at
  "99+"). The panel lists recent notifications with relative times. Clicking one marks it read and
  opens its link. There are "Mark all read" and "View all" (a full page with an unread filter).
- **Live badge:** a single SignalR connection per tab (`@microsoft/signalr`), joining `user-{id}` on
  connect. If the connection drops, the unread count is polled every 60 s (R1).
- **Preferences page** for office users at `/settings/notifications`. It is grouped by
  `NotificationKeyDescriptor.Category`, with a toggle per channel. The technician app's existing screen
  moves onto the same API.

### 2.6 Housekeeping

A daily job deletes rows older than 90 days (R2), in batches, under the existing background-work lease.

---

## 3. Part 2: Calendar & Activities

### 3.1 What it covers

- **Activities:** meetings, site visits, inspections and calls, optionally linked to a project,
  client, contract or work order.
- **Minutes:** written after the meeting, then published to attendees.
- **Action items:** follow-ups with an owner and a due date, from a meeting or on their own.
- **My calendar:** activities plus read-only items Ecosys already knows the dates of.

### 3.2 Data

```csharp
public sealed class Activity : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Type { get; set; } = "Meeting";     // Meeting | SiteVisit | Inspection | Call
    public string Title { get; set; }
    public DateTime StartsAt { get; set; }            // UTC
    public DateTime EndsAt { get; set; }
    public bool AllDay { get; set; }
    public string? Location { get; set; }             // or a meeting link
    public Guid OrganizerUserId { get; set; }
    public string? EntityType { get; set; }           // Project | Client | Contract | WorkOrder
    public Guid? EntityId { get; set; }
    public string Status { get; set; } = "Scheduled"; // Scheduled | Held | Cancelled
    public string? Agenda { get; set; }
    public string? Minutes { get; set; }              // plain text with line breaks
    public DateTime? MinutesPublishedAt { get; set; }
    public int? ReminderMinutesBefore { get; set; } = 30;
}

public sealed class ActivityAttendee : ITenantEntity
{
    // Exactly one of UserId, ConsultantId, ClientContactId or ExternalEmail is set.
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActivityId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ConsultantId { get; set; }
    public Guid? ClientContactId { get; set; }
    public string? ExternalName { get; set; }
    public string? ExternalEmail { get; set; }
    public string Response { get; set; } = "Pending"; // Pending | Accepted | Declined (staff only)
    public bool Attended { get; set; }                // ticked when minutes are written
}

public sealed class ActionItem : AuditableEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? ActivityId { get; set; }             // the meeting it came from, if any
    public string Title { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeExternalName { get; set; } // e.g. "Consultant to confirm duct sizes"
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = "Open";      // Open | Done | Cancelled
    public string? EntityType { get; set; }           // copied from the activity, or set directly
    public Guid? EntityId { get; set; }
}
```

### 3.3 Workflows

- **Schedule:** the organiser picks the time, attendees and the linked record. Staff attendees get
  `activity.scheduled` in the bell and by email, under their preferences. No `.ics` invite is sent
  (R4). External attendees (consultants, client contacts, other emails) are recorded on the meeting
  but not emailed; the organiser invites them as they do today. Moving or cancelling the meeting
  sends `activity.updated` / `activity.cancelled` to staff attendees.
- **Reminders:** `activity.reminder` goes to staff attendees `ReminderMinutesBefore` minutes ahead.
  A worker runs every 5 minutes and logs what it sent, like `ProjectReminderLog`.
- **Minutes:** after the meeting, the organiser marks it Held, ticks who attended, writes the minutes
  and adds action items. "Publish minutes" emails a PDF (rendered with the existing `IPdfRenderer`) to
  attendees and locks the minutes. Corrections are an addendum, not an edit, as with diary entries.
- **Action items** notify their assignee (`action-item.assigned`), are reminded the day before they
  are due (`action-item.due`), and appear on:
  - the linked project's Tasks tab, client page or contract page;
  - a "My action items" list.
- **Projects:** "Site instructions received" in the diary and a meeting's minutes can both be the
  starting point for a variation ("Raise variation from this" prefills the title and description).

### 3.4 My calendar

`GET /api/calendar?from&to&sources=` merges activities the user organises or attends with read-only
items Ecosys already holds:

| Source | Items | Needs |
|---|---|---|
| Activities | The user's meetings, visits, inspections, calls | — |
| Work orders | Scheduled visits assigned to the user | operations |
| Preventive maintenance | Plans due | operations |
| Projects | Milestones (PM), task and action-item due dates | projects |
| Contracts | Expiry and renewal-notice dates | operations |

Each source is filtered by the module and permission it needs, so the calendar never shows
something the user can't open. Managers can switch to a team view (one row per person, like the
scheduling board).

The frontend reuses the existing month-grid pattern (date-fns) and adds week and agenda views. The
agenda view is the default on phones.

### 3.5 External calendars (R4)

No meeting invites are emailed for now. Each user can create a private feed URL (`/api/calendar/feed/{token}.ics`) to subscribe to from
Google or Outlook. The token is random, stored hashed, revocable, and only reveals that user's items.
The feed holds titles, times and locations only, never amounts. It updates when the external
calendar polls it (usually every few hours), which the screen says plainly.

### 3.6 API and permissions

```
GET|POST|PUT   /api/activities[/{id}]                         + /cancel, /held, /minutes, /publish-minutes
GET|POST|PUT   /api/action-items[/{id}]                       filters: mine, entityType+entityId, status
GET            /api/calendar?from&to&sources&userIds
POST|DELETE    /api/me/calendar-feed                          create or revoke my feed link
GET            /api/calendar/feed/{token}.ics                 anonymous, token-scoped
```

New capabilities: `activities.view` (everyone by default), `activities.manage` (organise any meeting;
managers by default; anyone can organise their own), and `calendar.team` (team view; managers and
dispatchers). Activities and action items are Shared controllers (every plan). Calendar sources follow
their own modules.

---

## 4. Part 3: WhatsApp reminders

### 4.1 Provider and the shared number (R5)

Use the **Meta WhatsApp Cloud API** with **one WhatsApp Business account owned by Ecosys**. Its
phone number id, access token, app secret and webhook verify token are platform configuration
(environment secrets), not tenant data. The existing per-tenant `WhatsAppSetting` is kept for a later
move to tenant-owned numbers: the sender takes the sending account from a resolver, which returns the
Ecosys account for every tenant today.

```csharp
public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> SendTemplateAsync(string toE164, string templateName, string language,
        IReadOnlyList<string> bodyParameters, CancellationToken cancellationToken);
}
```

Messages go through an outbox table (`WhatsAppOutboxMessage`, tenant-owned, so each tenant's usage
can be counted), like email, so a slow or failing API never blocks a request. Retries back off, with
a final failure recorded on the outbox row and shown in the tenant's WhatsApp log.

What sharing one number means:
- **Sender name:** messages arrive from "Ecosys". Every template's first parameter is the tenant's
  name, e.g. "*Acme Builders*: today's muster roll for Westlands Mall hasn't been marked yet."
- **Cost:** Meta bills Ecosys. Each tenant's outbox rows are counted per month, so the cost can be
  recovered in plan pricing. A platform setting caps messages per tenant per month (default 1,000);
  beyond it, WhatsApp is skipped and email still goes.
- **Replies:** one number serves every tenant, and one person can work with two tenants, so a reply
  can't always be tied to one tenant. For now only STOP/START are acted on; other replies are logged
  on the platform side and not shown to tenants.
- **Templates:** Ecosys submits and maintains one set of templates. Tenants don't manage templates.

### 4.2 Rules that come from WhatsApp itself

- **Templates:** a business can only start a conversation with a pre-approved template. Ecosys ships
  a small set of *utility* templates, each tied to a notification key:
  - Staff: `muster_missing`, `task_due`, `valuation_certified`, `retention_due`, `meeting_reminder`,
    `action_item_due`, `work_order_assigned`.
  - Clients (R6): `technician_on_the_way`, `work_order_completed`, `invoice_issued`,
    `valuation_certified_client`.
- **Opt-in, staff:** a user agrees in Ecosys (office settings or the technician app) to receive
  WhatsApp messages on a confirmed number. The number is stored in E.164 and confirmed with a
  one-time code sent over WhatsApp.
- **Opt-in, clients (R6):**
  - A portal user opts in from the portal the same way, with a one-time code.
  - For a client contact with no portal login, office staff can record that the contact agreed
    (who recorded it, when, and how: in person, by phone, in writing). The contact's first message
    says why they're getting it and that STOP ends it.
  - Client messages go to the contacts the client record marks for them, never to every contact.
- **Opting out:** one tap in Ecosys, or replying STOP. Because the number is shared, STOP blocks that
  phone number for every tenant (a platform-wide `WhatsAppOptOut` list), and START lifts it. This is
  what Meta expects from one sender.
- **Which keys may use WhatsApp:** a tenant chooses which keys may use it at all. By default only
  `project.muster-missing`, `work-order.assigned`, `activity.reminder` and `technician-on-the-way`
  can.

### 4.3 Webhook

One endpoint for the shared number: `POST /api/whatsapp/webhook`. It checks the
`X-Hub-Signature-256` signature with the platform app secret, records delivery status (sent,
delivered, read, failed) on the outbox row by Meta's message id, and handles STOP/START. The GET
verification handshake uses the platform verify token. Inbound chat (the existing conversations
inbox) stays behind its feature flag and isn't part of this release.

### 4.4 Data changes

- `User`: `WhatsAppNumber` (E.164), `WhatsAppVerifiedAt`, `WhatsAppOptedInAt`, `WhatsAppOptedOutAt`.
  Portal users are `User`s too, so they get the same fields.
- `ClientContact`: `WhatsAppNumber`, `WhatsAppConsentAt`, `WhatsAppConsentRecordedByUserId`,
  `WhatsAppConsentMethod`, `ReceivesWhatsApp`.
- `WhatsAppSetting` (per tenant): `AllowedNotificationKeys` (JSON). Account fields stay unused while
  the number is shared.
- New: `WhatsAppOutboxMessage` (tenant-owned), `WhatsAppOptOut` (platform-wide, by phone number),
  `WhatsAppVerificationCode` (hashed, 10-minute expiry, 5 attempts).

---

## 5. Part 4: Role dashboards

### 5.1 Approach

- A **widget catalogue** in code. Each widget has a key, a title, the module(s) and permission it
  needs, and one backend query.
- A **default layout per role**, also in code. A tenant admin can hide widgets for a role (stored in
  the `dashboards` module setting). No one can add custom queries.
- **Personal layouts (R8):** a user can reorder, hide and add widgets from the ones they're allowed to
  see, and pick a widget's size (small or wide). Their layout is saved in `UserDashboardLayout`
  (TenantId, UserId, an ordered JSON list of `{key, size}`, UpdatedAt). "Reset to default" deletes the
  row. A widget the user loses access to, or the admin hides, simply drops out of their layout; a new
  widget added to their role's default shows up at the end once.
- The dashboard page asks `GET /api/dashboard/layout` for the caller's widgets (their saved layout, or
  the role default) and saves changes with `PUT /api/dashboard/layout`. It then loads each one
  from `GET /api/dashboard/widgets/{key}` in parallel, so one slow widget doesn't hold up the page.
- Results are cached per tenant, widget and scope for 5 minutes. Anything a widget's own data changes
  can invalidate it.

### 5.2 Widgets by role

| Role | Widgets |
|---|---|
| Admin / Manager | Open and overdue work orders, SLA at risk, technician workload, PM compliance, contracts expiring, projects behind schedule, receivables overdue |
| Dispatcher | Unassigned work orders, today's visits by technician, SLA at risk, technicians on site |
| Finance | Receivables ageing (30/60/90+), cash received this month, tax withheld this month, invoices to send, project billing waiting (certified but not invoiced, retention due) |
| Project manager (has projects) | My projects (progress vs planned), pending variations, valuations awaiting certificate or invoice, today's muster roll status, uncosted labour days, action items due |
| Materials controller | Low stock, pending issues, open purchase orders |
| Viewer | Read-only versions of the manager widgets they have permission for |

The existing executive dashboard stays as it is, and gains a "Projects" row when the module is on.
Technicians keep the technician app home, plus a "My action items" card.

### 5.3 Rules

- A widget only appears when the tenant has its module and the user has its permission. The server
  enforces this as well as the layout, so a hidden widget's endpoint still returns 403.
- Branch scoping follows the existing dashboard (`branchId`, and the user's branch access).
- Every number links to the filtered list behind it.

---

## 6. Licensing, isolation, testing

- **Licensing:**
  - Notifications, activities, action items and the calendar are Shared, on every plan.
  - Calendar sources and dashboard widgets need their own modules.
  - WhatsApp is a tenant feature flag (`whatsapp`) on top of the plan.
  - New controllers are added to `ServiceOpsPlans` so the classification test covers them.
- **Isolation:** every new table is tenant-filtered and every write checks the tenant. The calendar
  feed token resolves to exactly one user in one tenant. The WhatsApp webhook resolves the tenant from
  the URL and checks that tenant's signature.
- **Tests:**
  - **Dispatcher:** applies preferences per channel, and the work-order and escalation pushes now
    honour them.
  - **Push:** survives a restart (row-based); stale pushes are dropped.
  - **Inbox:** shows only the caller's rows; read and read-all.
  - **Activities:** staff attendees are notified on schedule, move and cancel; minutes lock on
    publish; action items carry the linked record. The feed's event UIDs stay stable across updates.
  - **Calendar:** no source the user lacks the module or permission for appears; the feed token
    can't reach another user.
  - **WhatsApp:** webhook signature checked; STOP opts out; nothing is sent without opt-in or for a
    key the tenant hasn't allowed; outbox retries.
  - **Dashboards:** each widget returns 403 without its module or permission; admin hiding works;
    a saved personal layout can't include a widget the user isn't allowed; reset restores the default.

---

## 7. Releases

| Step | Contents | Depends on |
|---|---|---|
| 3a | Dispatcher, `Notification` table, bell, SignalR client, office preferences page, row-based push, housekeeping. Existing senders moved onto it | — |
| 3b | Activities, attendees, minutes, action items, reminders, My calendar, calendar feed | 3a |
| 3c | Widget catalogue, role layouts, tenant hiding, personal layouts, dashboard page | 3a for "action items due"; otherwise independent |
| 3d | WhatsApp sender, outbox, webhook, staff and client opt-in, number check, templates, per-key allow-list, monthly cap | 3a; the Ecosys WhatsApp Business account with approved templates to test against |

3d can be built and tested against a fake sender, but it can't go live until the Ecosys WhatsApp
Business account is verified and its templates are approved by Meta.

3c and 3d can run in parallel once 3a is in. Meta's business verification and template approval
take days and are outside our control. Start that paperwork early.

---

## 8. Not doing

- Emailed `.ics` meeting invites, for now (R4). Two-way Google/Outlook sync. Room booking and
  resource calendars.
- Inbound WhatsApp chat as a support channel. It stays behind its existing flag.
- Tenant-owned WhatsApp numbers, for now (R5). The sender is built so they can be added later.
- SMS (R7).
- Custom report queries or user-built widgets. Users arrange existing widgets only (R8).
- Notification digests (one daily email). Worth adding later, using the inbox rows as the source.

---

## 3a: what was built

Built as designed above; only the deviations and details worth knowing are listed here.

- **Channels per key.** `NotificationDescriptor` has both `Channels` (what the key can use) and
  `DefaultChannels` (what a missing preference row means). Keys sent through the dispatcher
  (`work-order.assigned` and the `project.*` keys) offer bell, push and email; every other key is an
  email its own module sends, so it offers email only and the preferences screens show only that
  toggle. No key offers WhatsApp yet, so the WhatsApp toggle stays hidden until 3d adds it to a key.
- **`NotificationRequest` gained an optional `Channels` limit.** The work-order assignment and the
  escalation reassignment send bell and push only through the dispatcher: the assignment email still
  goes to the technician record's own address (which may have no login), and the escalation sends its
  own escalation email. The assignment email now also honours the technician's `EmailEnabled` for
  `work-order.assigned` when the technician has a login.
- **Push-only rows.** A user who turns the bell off but push on still gets a `Notification` row (so
  the push survives a restart), written already read and archived so it never shows in the bell.
- **Migration.** Before this release `InAppEnabled` was the push switch, so `AddNotificationInbox`
  copies it into the new `PushEnabled` for existing rows; nobody who turned push off starts getting it.
- **Push tag.** The row does not store the old per-call push tag; the worker uses `{key}-{entityId}`
  (or `notification-{id}`), so a newer push about the same record replaces the older one on the device.
- **Test push.** `POST /api/me/push/test` sends straight to the caller's devices through the
  in-memory queue (no row), from a "Send a test" button on the office preferences page.
- **SignalR.** The hub joins `user-{userId}` in `OnConnectedAsync` from the `user_id` claim (no client
  call needed; `JoinUserGroup` exists and is idempotent). The client is `@microsoft/signalr` 10.0.0
  (matching the .NET 10 server); `LiveNotificationsProvider` keeps one connection per tab.
- **Routes.** `/settings/notifications` is now each person's own preferences (open to all staff). The
  tenant-wide matrix that lived there moved to `/settings/workspace-notifications` (admin only).
  The full inbox is `/notifications` in the office and `/tech/notifications` in the technician app.
- **Paging.** `GET /api/me/notifications` returns `{ items, hasMore }`; `before` is the last item's
  `createdAt`. `limit` defaults to 50 and is capped at 100. Read, read-all and archive return the new
  unread count and broadcast it to the user's other tabs.

---

## 3d: what was built

Built as designed in section 4; deviations and details worth knowing:

- **Client contacts are `ClientPortalUser`.** There is no `ClientContact` table, and portal users are
  `ClientPortalUser` rows, not `User`s. The 4.4 client fields (`WhatsAppNumber`, `WhatsAppConsentAt`,
  `WhatsAppConsentRecordedByUserId`, `WhatsAppConsentMethod`, `ReceivesWhatsApp`, plus
  `WhatsAppVerifiedAt`) are on `ClientPortalUser`, the client's contact list (the Client Portal tab). A
  contact who never signs in is one whose consent the office records (in person, by phone, in writing);
  a contact who confirms a code in the portal gets method `Portal`. Such a contact still needs an email.
- **Feature flag.** There was no generic tenant flag, so `whatsapp` is `TenantLicense.WhatsAppEnabled`
  (default off), exposed as `TenantLicenseSnapshot.WhatsApp` / `LicenseFeatures.WhatsApp` and set by
  Ecosys at `PUT /api/platform/whatsapp/tenants/{id}` (Platform → tenant → Users tab). The new
  controllers are Shared (every plan); the old `WhatsApp` inbox controller stays Operations and behind
  `WHATSAPP_ENABLED`.
- **Webhook URL.** One URL, `/api/whatsapp/webhook`, checked with the platform app secret (section 6's
  "tenant from the URL" predates R5). Delivery status finds its tenant through the outbox row.
  Rate-limited 600/min per IP, body capped at 512 KB.
- **Sender interface** takes the resolved account: `SendTemplateAsync(account, to, template, language,
  bodyParameters, buttonParameters, ct)`. `IWhatsAppAccountResolver` returns the shared account for every
  tenant, or null when the platform isn't configured; the worker then marks rows Skipped (no separate
  fake sender is registered).
- **Templates** are a string-keyed catalogue in code (`WhatsAppTemplateCatalogue`), including
  `activity.reminder` and `action-item.due` for 3b. A staff key sends only when its descriptor lists
  WhatsApp; 3a's `Inbox(..., whatsApp: true)` adds it without touching `DefaultChannels`. The number
  confirmation uses an Authentication template (`ecosys_verification_code`) whose text Meta fixes, so it
  is the one template without the tenant name.
- **Opting in turns WhatsApp on** for the WhatsApp-capable keys the tenant allows (preference rows are
  created from the key defaults where missing). Staff then turn individual keys off as usual. The
  preferences screens show the WhatsApp toggle only for keys the tenant allows, and only with the flag.
- **STOP.** Confirming a number again with a code lifts a STOP on it (fresh consent); asking for a code
  is not blocked by STOP. An in-app opt-out is per person (`User.WhatsAppOptedInAt` cleared /
  `ClientPortalUser.ReceivesWhatsApp` off), not platform-wide.
- **Cap.** Counted per tenant per UTC calendar month over every outbox row except Skipped. A message over
  the cap is written as a Skipped row so the log shows it.
- **Retries.** Network errors, 429 and 5xx retry after 1, 5, 15, 60 minutes (5 attempts; 2 for codes);
  any other Meta error fails at once. A row stuck in Sending for 10 minutes is returned to the queue. A
  sent code is removed from the outbox row.
- **Client hooks.** "On the way" and "completed" hook `EtaNotificationService` (EnRoute / Completed),
  respecting the work order's `NotifyClient` but not the ETA email settings; "invoice issued" hooks
  `SendInvoice`; "valuation certified" hooks `ProjectNotificationService.ValuationCertifiedAsync`.
  Completed, invoice and valuation messages go once per contact per record.
- **Work-order assignment** now passes WhatsApp in its channel limit and sends `WorkOrderNumber`,
  `WorkOrderTitle` and `Priority` as template values.
- **Routes.** Office settings page `/settings/whatsapp-reminders`; the router's `WHATSAPP_ENABLED` filter
  now hides only `/whatsapp*` and the old `/settings/whatsapp` account page.
- **Setup** for the shared number, template texts and environment variables: `docs/WHATSAPP_SETUP.md`.
