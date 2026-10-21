# Release 3: Shared platform — design for review

Status: **draft for review. Nothing here is built.**
Scope: the four items the Projects design (section 13) deferred because they serve every module, not
just Projects:

1. An in-app notification inbox (the bell).
2. Calendar & Activities: meetings, minutes and action items.
3. WhatsApp reminders.
4. Role dashboards across modules.

Each part can ship on its own. Part 1 comes first because parts 2–4 send notifications through it.

---

## 0. Decisions needed

| # | Question | Recommendation |
|---|---|---|
| R1 | Live updates for the bell: a SignalR connection, or polling every 60 s? | **SignalR**, with polling as the fallback. The hub already exists; only the frontend client is missing. |
| R2 | How long are notifications kept? | **90 days**, then deleted. Read ones can be archived by the user before that. |
| R3 | Meeting action items: a new shared `ActionItem` list, or create `ProjectTask`s? | **New `ActionItem`**, shown on the linked record (a project's Tasks tab lists both, clearly labelled). Meetings also happen for clients and contracts, which have no task list. |
| R4 | Calendar sync with Google/Outlook: one-way feed, or two-way? | **One-way**: a private calendar feed (ICS link) each user can subscribe to, plus `.ics` invites by email. Two-way sync needs OAuth apps with Google and Microsoft and is a project of its own. |
| R5 | WhatsApp number: each tenant connects its own WhatsApp Business account, or one shared Ecosys number? | **Each tenant's own account.** The existing `WhatsAppSetting` is already shaped for it, messages carry the tenant's name, and Meta bills the tenant directly. |
| R6 | WhatsApp for staff only, or clients too? | **Staff first** (supervisors, PMs, technicians). Client messages (e.g. "your technician is on the way") come later, under the same opt-in rules. |
| R7 | SMS fallback (e.g. Africa's Talking) when WhatsApp fails? | **Not now.** Add it as a fourth channel later if tenants ask. |
| R8 | Can users rearrange their own dashboard? | **No.** Layouts are set per role in code, and a tenant admin can hide widgets for a role. Personal layouts add storage and support cost for little gain. |

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

- **Schedule:** the organiser picks the time, attendees and the linked record. Staff get an in-app
  notification and an email with an `.ics` invite. External attendees (consultants, client contacts,
  other emails) get the email with the `.ics` only. Moving or cancelling the meeting sends an
  update or cancellation `.ics` with the same UID.
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

Each user can create a private feed URL (`/api/calendar/feed/{token}.ics`) to subscribe to from
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

### 4.1 Provider

Use the **Meta WhatsApp Cloud API** with each tenant's own business account (R5). The existing
`WhatsAppSetting` holds the business account id, phone number id, access token and webhook verify
token. It gains `AppSecret` (to check webhook signatures). The access token is encrypted at rest with
the same protection as other stored credentials.

```csharp
public interface IWhatsAppSender
{
    Task<WhatsAppSendResult> SendTemplateAsync(Guid tenantId, string toE164, string templateName,
        string language, IReadOnlyList<string> bodyParameters, CancellationToken cancellationToken);
}
```

Messages go through an outbox table (`WhatsAppOutboxMessage`), like email, so a slow or failing API
never blocks a request. Retries back off, with a final failure logged on the notification.

### 4.2 Rules that come from WhatsApp itself

- **Templates:** a business can only start a conversation with a pre-approved template. Ecosys ships
  a small set of *utility* templates the tenant submits for approval from the WhatsApp settings page
  (with suggested wording), for example:
  - `muster_missing`: "Today's muster roll for {{1}} hasn't been marked yet."
  - `task_due`
  - `valuation_certified`
  - `retention_due`
  - `meeting_reminder`
  - `work_order_assigned`
- **Opt-in:** a user must agree, in Ecosys, to receive WhatsApp messages on a confirmed number.
  - The number is stored in E.164 format and confirmed with a one-time code sent by WhatsApp.
  - Opting out is one tap, and replying "STOP" also opts out (handled through the webhook).
- **Cost:** Meta charges the tenant per conversation. The settings page says so, and a tenant chooses
  which keys may use WhatsApp at all. By default only `project.muster-missing`, `work-order.assigned`
  and `activity.reminder` can.

### 4.3 Webhook

`POST /api/whatsapp/webhook/{tenantSlug}` checks the `X-Hub-Signature-256` signature with the app
secret, then records delivery status (sent, delivered, read, failed) on the outbox row and handles
STOP. The GET verification handshake uses the verify token. Inbound chat (the existing conversations
inbox) stays behind its feature flag. It isn't part of this release.

### 4.4 Data changes

- `User`: `WhatsAppNumber` (E.164), `WhatsAppVerifiedAt`, `WhatsAppOptedInAt`, `WhatsAppOptedOutAt`.
- `WhatsAppSetting`: `AppSecret`, plus `AllowedNotificationKeys` (JSON).
- `WhatsAppTemplate`: `NotificationKey`, plus the approval status read back from Meta.

---

## 5. Part 4: Role dashboards

### 5.1 Approach

- A **widget catalogue** in code. Each widget has a key, a title, the module(s) and permission it
  needs, and one backend query.
- A **default layout per role**, also in code. A tenant admin can hide widgets for a role (stored in
  the `dashboards` module setting), but can't add custom queries (R8).
- The dashboard page asks `GET /api/dashboard/layout` for the caller's widgets. It then loads each one
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
  - **Activities:** the `.ics` UID stays stable across updates and cancellation; minutes lock on
    publish; action items carry the linked record.
  - **Calendar:** no source the user lacks the module or permission for appears; the feed token
    can't reach another user.
  - **WhatsApp:** webhook signature checked; STOP opts out; nothing is sent without opt-in or for a
    key the tenant hasn't allowed; outbox retries.
  - **Dashboards:** each widget returns 403 without its module or permission; layout hiding works.

---

## 7. Releases

| Step | Contents | Depends on |
|---|---|---|
| 3a | Dispatcher, `Notification` table, bell, SignalR client, office preferences page, row-based push, housekeeping. Existing senders moved onto it | — |
| 3b | Activities, attendees, minutes, action items, `.ics` invites, reminders, My calendar, calendar feed | 3a |
| 3c | Widget catalogue, role layouts, tenant hiding, dashboard page | 3a for "action items due"; otherwise independent |
| 3d | WhatsApp sender, outbox, webhook, opt-in and number check, templates, per-key allow-list | 3a; a tenant with an approved WhatsApp Business account to test against |

3c and 3d can run in parallel once 3a is in. 3d needs a real WhatsApp Business account and Meta
template approval, which takes days and is outside our control. Start that paperwork early.

---

## 8. Not doing

- Two-way Google/Outlook sync (R4). Room booking and resource calendars.
- Inbound WhatsApp chat as a support channel. It stays behind its existing flag.
- SMS (R7).
- User-built dashboards or custom report queries (R8).
- Notification digests (one daily email). Worth adding later, using the inbox rows as the source.
