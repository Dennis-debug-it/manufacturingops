# Ecosys Factory Operations — Technical Specification

Visitor Management and Security Patrol as modules beside Maintenance

Nov 13, 2026 · Draft for review

This spec covers adding two modules to the existing Ecosys Factory Maintenance codebase:
**Visitor Management** and **Security Patrol**. They are sold separately from the maintenance
editions, and they share the platform already built. It covers shared architecture, data model,
workflows, mobile behaviour, licensing and phasing. Safety & Inspections already exists inside
the Maintenance module (permits, LOTO, incidents, training, PPE: `Safety.cs`). This spec repackages
it and does not rebuild it.

Related: [requirements.md](requirements.md), [scope.md](scope.md).

---

## 1. Product structure

| Module | Status | Sold as |
|---|---|---|
| Maintenance (assets, work orders, PM, window, spares, contractors, safety, compliance) | Built (Phases 1–6), Phase 7 in progress | Editions: Essentials / Professional / Enterprise |
| **Visitor Management** | New: this spec | Add-on, any edition, or standalone |
| **Security Patrol** | New: this spec | Add-on, any edition, or standalone |
| Safety & Inspections | Built inside Maintenance | Stays in Maintenance; can be split out later as its own SKU |
| Group Operations Dashboard | Partly built (factory dashboard, KPIs) | Enterprise; gains visitor and patrol tiles |

**Standalone means** a tenant with only Security can run patrols and visitors without the
maintenance screens. The platform layer (tenant, users, roles, org tree, notifications, files,
audit, offline outbox) is always present. Maintenance-specific links (work order, permit, asset)
show only when the Maintenance module is also licensed.

---

## 2. What is reused (no new platform work)

| Need | Existing piece | How it is used |
|---|---|---|
| Factories, gates, zones | `OrgNode` (group → factory → section → sub-section) | A gate, zone or patrol area is an `OrgNode` (section or sub-section). Users scoped by `UserOrgNodeAssignment` see only their factories |
| Module gating | `ServiceOpsPlans` controller → module table, `TenantLicense` | Two new module keys, enabled by add-ons (§7) |
| Contractors and their papers | `Contractor`, `ContractorDocument` (required docs, expiry alerts) | A contractor visit cannot be approved while required documents are expired |
| Safety induction | `TrainingRecord` (`Category = "Induction"`, `ExpiresOn`) | Gate check: a current induction is required to enter production areas |
| Permit to work | `WorkPermit` linked to `WorkOrder` | A contractor's access to a hazardous area is valid only inside an approved permit's window |
| Incidents | `Incident` (kinds, severity, org node, corrective work order) | Extended with security kinds (§5.5). One register, not two |
| Shifts | `FactorySettings.ShiftsJson`, `Roster` (`People.cs`) | Guard shifts and patrol periods use the same shift definitions |
| Scheduled checks done by scanning | `CheckSchedule` / `CheckRun` (period key, due window, missed status, supervisor alert) | Patrol scheduling copies this pattern exactly: a schedule generates runs per shift, missed runs alert supervisors |
| Tags and label printing | `AssetTag`, QuestPDF label sheets | Checkpoint tags use the same numbering, printing and replace-damaged-tag flow |
| Unauthenticated, keyed links | `OperatorFaultReport` + `FactorySettings.OperatorReportKey` | Visitor self-registration and pre-registration links use the same keyed, rate-limited public route pattern |
| Offline | Outbox, idempotency keys, `X-Occurred-At`, IndexedDB cache | Patrol scans and gate check-ins work with no signal; the scan time is the device's occurred-at, not the upload time |
| Notifications | In-app, email outbox, web push, WhatsApp, SMS gateway | Host "your visitor has arrived", missed-checkpoint alerts, incident escalation |
| Real-time | SignalR `ServiceOpsHub` | Live on-site count, live patrol board |
| Approvals | `ApprovalMatrix` / workflow services | Configurable visit approval (host only, or host + security, or + safety for restricted zones) |
| Audit | `AuditLog` | Every check-in, override and scan, including who overrode a block |
| Data protection | `DataProtection` controller | Visitor retention and erasure (§8) |

---

## 3. Shared additions

Small platform additions both modules need:

1. **`AccessZone`**: a named area on the org tree with a restriction level.
   `OrgNodeId`, `Name`, `Level` (`Public` | `Controlled` | `Restricted`), `RequiresEscort`,
   `RequiresInduction`, `RequiredPpe` (list), `RequiresPermit` (bool), `Latitude`/`Longitude`/`RadiusM`
   (optional geofence). Seeded for tea: Reception (Public), Weighbridge and Leaf receiving
   (Controlled), Processing, Boiler house, Fuel store, Substation (Restricted).
2. **`Device`**: a registered phone or gate tablet, with `OrgNodeId`, `Kind` (`Gate` | `Patrol`),
   `LastSeenAt` and a device key. Patrol scans and gate check-ins record the device, so a
   supervisor can see that a scan came from the guard's assigned handset.
3. **Two new roles** (templates, editable): *Security Officer* (gate desk, patrols, incidents) and
   *Security Supervisor* (routes, schedules, overrides, reports). Visitor *host* is any user.
4. **Group dashboard tiles**: people on site now (per factory), patrol completion %, open security
   incidents, overdue visits.

---

## 4. Visitor Management

### 4.1 Entities

```
Visitor                 one person, reused across visits
  Id, TenantId, FullName, Phone, Email?, Company?, IdType?, IdNumberMasked?, IdNumberHash?,
  PhotoAttachmentId?, ContractorId?, IsBlocked, BlockedReason?, LastVisitAt

Visit                   one visit by one or more people
  Id, TenantId, VisitNumber, FactoryId (OrgNode), Purpose (Meeting | Delivery | Contractor work |
  Inspection | Audit | Interview | Other), HostUserId, ExpectedFrom, ExpectedTo,
  Status, Source (PreRegistered | WalkIn | SelfRegistered | Recurring),
  WorkOrderId?, PermitId?, ContractorId?, PurchaseOrderId?, Notes

VisitAttendee           one person on a visit
  VisitId, VisitorId, BadgeNumber?, BadgeToken (opaque, for the QR), CheckedInAt?, CheckedOutAt?,
  CheckedInByUserId?, IdVerified, InductionConfirmed, PpeConfirmed, EscortUserId?, OverrideReason?

VisitZone               zones the visit may enter (default: reception only)
  VisitId, AccessZoneId

VisitVehicle            vehicle with the visit
  VisitId, Registration, Kind, DriverVisitorId?, EnteredAt?, ExitedAt?, GateNotes

VisitApproval           one approval step (from the approval matrix)
  VisitId, Step, ApproverUserId?, Decision, DecidedAt, Note

RecurringPass           standing access for a contractor or supplier
  VisitorId, ContractorId?, FactoryId, ValidFrom, ValidTo, Days, Zones, Status
```

### 4.2 Visit lifecycle

```
Draft ─► PendingApproval ─► Approved ─► OnSite ─► Completed
   │            │               │           │
   └──────────► Rejected        └► Cancelled / NoShow (worker, after ExpectedTo + grace)
                                            └► Overstayed (flag, still OnSite)
```

- Walk-ins start at the gate as `PendingApproval`. The host gets a push and WhatsApp notice and
  approves from their phone. A walk-in is never `OnSite` before approval, unless a supervisor
  overrides with a reason. The reason is audited.
- `OnSite` is per attendee. The visit is `Completed` when the last attendee checks out.
- A worker flags `Overstayed` attendees past `ExpectedTo` + grace and notifies host and gate.

### 4.3 Gate checks (blocking rules, evaluated at approval and again at check-in)

| Check | Rule | Override |
|---|---|---|
| Blocklist | `Visitor.IsBlocked` | Supervisor only |
| Contractor papers | Contractor `Approved` and every `ContractorDocumentTypes.Required` document current | None |
| Induction | Any zone with `RequiresInduction` needs a current induction `TrainingRecord` for this person. If missing, the gate can record an induction on the spot (video/briefing + signature) | — |
| PPE | Zone `RequiredPpe` confirmed at the gate | Supervisor |
| Permit | Zone `RequiresPermit`: visit linked to an `Approved` `WorkPermit` whose validity covers now | None |
| Work order | Contractor work visits link the work order. Its status must be planned/assigned/in progress | Supervisor |
| Escort | Zone `RequiresEscort`: an escort user must be named | — |

### 4.4 Badge

PDF badge (QuestPDF, 54×86 mm or A6) with name, photo, company, host, zones, valid-until and a
QR code holding `BadgeToken`. The QR contains no personal data. Scanning a badge at the gate on the
way out checks the attendee out. Scanning anywhere else (a guard on patrol) shows who the person is,
their host and whether they are allowed in this zone. This is the patrol ↔ visitor link (§6).

### 4.5 Pre-registration and self-registration

- **Host invite**: the host creates the visit. The visitor gets a WhatsApp/SMS/email link
  (keyed, single-use, expires at `ExpectedTo`) to add ID type, photo and vehicle, and to watch
  the induction. On arrival the gate scans the link's QR and the visitor is checked in in seconds.
- **Gate kiosk self-registration** (optional): a public page per factory gate, keyed like the
  operator fault report, rate-limited. It creates a walk-in `PendingApproval` visit.

### 4.6 Emergency roll call

One tap from any security user: **everyone on site now**. It lists every `OnSite` attendee with host,
zone, phone and vehicle, plus contractors on active work orders. The list is available offline from
the gate tablet's last sync and printable. During a roll call, guards mark each person *accounted
for*, and the board updates live (SignalR). The record is kept with the incident if one is raised.

### 4.7 Reports

Visitor register (search, export), visits per day/factory/purpose, average duration, overstays,
no-shows, overrides by user, contractor hours on site (and against the work order's labour hours),
vehicle log.

---

## 5. Security Patrol

### 5.1 Entities

```
Checkpoint              a physical point
  Id, TenantId, OrgNodeId, AccessZoneId?, Code, Name, Kind (Qr | Nfc | QrAndNfc | Gps),
  TagNumber, NfcUid?, Latitude?, Longitude?, RadiusM (default 50), Instructions?,
  ChecklistTemplateId? (PmTemplate), IsActive

PatrolRoute             ordered checkpoints
  Id, TenantId, FactoryId, Name, Ordered (bool), MaxDurationMinutes, MinGapSeconds (default 20)
PatrolRouteCheckpoint   RouteId, CheckpointId, Sequence, Required (bool)

PatrolSchedule          when the route is walked (mirrors CheckSchedule)
  RouteId, ShiftName?, EveryMinutes? (e.g. every 120 min at night), FixedTimes?, ActiveDays,
  AssignedUserId? or AssignedRosterGroup?, GraceMinutes

PatrolRun               one patrol instance (mirrors CheckRun)
  Id, ScheduleId, RouteId, PeriodKey, DueFrom, DueBy, GuardUserId?, DeviceId?,
  StartedAt?, FinishedAt?, Status (Due | InProgress | Completed | Partial | Missed),
  ScannedCount, RequiredCount, Score

CheckpointScan          one scan
  Id, PatrolRunId?, CheckpointId, GuardUserId, DeviceId, Method (Qr | Nfc | Manual),
  OccurredAt (device), ReceivedAt (server), Latitude?, Longitude?, AccuracyM?,
  DistanceM?, Validation (Valid | OutOfSequence | OutsideGeofence | TooFast | Manual | Replay),
  NfcCounter?, Answers? (checklist), Note?, PhotoAttachmentId?

ShiftHandover           FactoryId, ShiftName, OutgoingUserId, IncomingUserId, HandedOverAt,
                        OpenIssues, KeysAndEquipment, Acknowledged
GuardPost (optional)    fixed posts (main gate, weighbridge) with assigned guards per shift
```

### 5.2 Verifying that a guard was really there

A printed QR can be photographed and scanned from the guardhouse. Verification is layered, and
the tenant picks the level per checkpoint:

| Layer | How | Defeats |
|---|---|---|
| Sequence | Ordered routes reject a scan ahead of an unscanned required point (recorded as `OutOfSequence`, not dropped) | Random scanning |
| Minimum gap | Two checkpoints scanned closer together than `MinGapSeconds` (or than walking distance allows) → `TooFast` | Photo-sheet scanning |
| GPS geofence | Device location within `RadiusM` of the checkpoint (accuracy recorded) → otherwise `OutsideGeofence` | Remote scanning outdoors (weak indoors; treat as a signal, not proof) |
| NFC | Tag must be read by the phone (Web NFC on Android Chrome). Plain NTAG UID is copyable | Photographs |
| **NFC with secure dynamic message (recommended for high-value points)** | NTAG 424 DNA–class tags emit a per-tap encrypted counter + CMAC in the NDEF URL; the server verifies the MAC and that the counter increased → otherwise `Replay` | Cloning and replay |
| Device binding | Scan accepted from the guard's registered `Device` | Scanning on someone else's phone |

Scans that fail validation are **kept and flagged**, never silently discarded. Supervisors see them
and their reasons. The run score is the share of required checkpoints with a `Valid` scan.

Hardware note: Web NFC works on Android Chrome only. iPhones need QR (or a later native app).
The pilot assumes Android handsets per guard post, the same assumption as the technician app.

### 5.3 Offline patrols

Patrols happen at night, around perimeters, often with no signal. The route, its checkpoints
(including geofence and NFC keys needed for local pre-checks) and the current runs are in the
device cache. Scans go into the outbox with the device's `OccurredAt`. The server re-validates
everything on sync; the device's own validation is advisory. A device whose clock is off from server
time by more than N minutes at sync is flagged, so back-dated scans are visible.

### 5.4 Missed and late patrols

A worker (the `PmSchedulerWorker` lease pattern) generates `PatrolRun`s ahead from schedules and
closes them at `DueBy + Grace`: `Missed` if nothing was scanned, `Partial` if some required points
were. Each produces a supervisor alert (push + WhatsApp/SMS, since supervisors may be off site at
night). Optional **lone-worker check**: no scan for X minutes during an `InProgress` run → alert.

### 5.5 Security incidents

Extend the existing `Incident` rather than adding a second register:

- Add `Category` (`Safety` | `Security`). Existing rows default to `Safety`.
- Add security kinds: `UnauthorisedAccess`, `Theft`, `AttemptedTheft`, `Intrusion`,
  `PerimeterDamage`, `Tampering`, `SuspiciousPerson`, `SuspiciousVehicle`, `Hazard`, `Emergency`.
  `Fire` is already there.
- Add `OwnerUserId`, `ResponseStatus` (`Reported` → `Responding` → `Contained` → `Closed`),
  `CheckpointId?`, `PatrolRunId?`, `VisitId?`, `PoliceObNumber?` (Kenya Police Occurrence Book reference).
- Evidence: photos/video via `Attachment`. Severity drives escalation (Serious/Critical → immediate
  supervisor + factory manager notification).
- A perimeter or equipment issue raises a corrective `WorkOrder` through the existing
  `CorrectiveWorkOrderId` link (only when Maintenance is licensed). Otherwise it stays a task on the incident.

### 5.6 Guard app (PWA, mobile-first)

Home: *my next patrol* (due time, route, progress), **Start patrol**, **Scan**, **Report incident**
(three taps: kind, photo, note; location captured), **Panic** (sends location to supervisors),
**Visitor lookup** (scan badge), **Handover**. Large touch targets, dark mode for night shifts,
works with gloves, Swahili and English strings.

### 5.7 Supervisor views

Live patrol board (runs due/in progress/missed, per factory), map of the last scans, route
completion heat-map by checkpoint (which points are always skipped), guard performance, incident
list, handover log. Reports: daily security report (PDF, emailed at shift end via
`ScheduledReport`), weekly/monthly completion and incidents, per guard and per factory.

### 5.8 Outsourced guards

Many factories use a security company. Guards may be users of the factory tenant with a
*Security Officer* role and a `Company` field; the guarding company is a `Contractor`. Patrol
completion per guarding company is a standard report. It is the evidence for the guarding contract.

---

## 6. Cross-module flow: contractor repairing a boiler

```
Maintenance                Visitors                       Safety            Patrol
───────────                ────────                       ──────            ──────
WO-0412 planned  ───────►  Visit (Contractor work,
contractor assigned        WO-0412, zone Boiler house)
                           │ checks: contractor papers ✓
                           │ induction ✓ (or done at gate)
                 PTW-088 approved (hot work) ─────────►  permit valid ✓
                           │ host approves
                           ▼
                           OnSite, badge (zones: Boiler)
                                                                       guard scans badge at the
                                                                       boiler house: allowed ✓
                                                                       (at fuel store: not allowed
                                                                        → security incident)
WO completed, verified ◄── contractor hours on site vs labour hours
                           checked out at gate ──────►  permit closed
```

One timeline on the work order shows the visit, permit, check-in/out and any scans of the
contractor's badge. Contractor invoice matching (`ContractorInvoice`) can compare billed
hours with hours on site.

---

## 7. Licensing and gating

The current model ties modules to editions (`ServiceOpsPlans.EditionModules`) plus one WhatsApp
add-on field on `TenantLicense`. Change:

1. Add module keys `visitors` and `security` to `ServiceOpsPlans`, with their controller sets
   (`Visits`, `Visitors`, `VisitorKiosk`, `AccessZones`, `Checkpoints`, `PatrolRoutes`,
   `PatrolRuns`, `GuardApp`, `ShiftHandovers`, `SecurityReports`). `ServiceOpsModuleClassificationTests`
   already fails for any unclassified controller.
2. Generalise add-ons: `TenantLicenseAddOn (TenantId, AddOnCode, StartsAt, EndsAt, Seats?)`. The
   existing `LicenseAddOn` catalogue gets `ModuleKey` and `PricingBasis`
   (`PerFactory` | `PerSeat` | `Flat`). `Modules(...)` returns edition modules ∪ add-on modules.
   The existing `WhatsAppAddOn` field is migrated into the new table.
3. **Standalone edition** `security_suite` (no Maintenance): platform shared controllers + `visitors`
   + `security`. Sidebar sections already hide by module.
4. Seats: guards and gate officers count as **field seats**. Visitors are not users and never
   count. Pricing per factory suits groups like KTDA better than per seat. That is a commercial decision.

Commercial note: the figures in the product proposal (≈KES 19–22M a year across 65 factories)
are planning estimates only. They are not a validated customer budget, and the licence model above
supports per-factory or per-seat pricing either way.

---

## 8. Data protection (Kenya DPA 2019)

Visitor records are personal data of people who are not users, so:

- **Minimise**: store ID type and a masked number (last 4) plus a salted hash for re-identification
  of repeat visitors. Do not store full ID numbers or ID scans by default (tenant setting, off).
- **Notice**: the pre-registration page and kiosk show a privacy notice (purpose: site security
  and safety; retention; contact). Acceptance is recorded on the visit.
- **Retention**: tenant setting, default 12 months for visits, then anonymised by a worker (name →
  "Visitor", phone/photo removed, counts kept). Incidents linked to a visit are kept for their
  own retention period.
- **Erasure/access requests**: through the existing `DataProtection` flow, extended to `Visitor`.
- **Location data**: guard GPS is collected only during an active patrol run, never in the background.
  Guards are told this in-app.
- Photos and attachments stay in the tenant's object storage under the existing access rules.

---

## 9. API and integration

- REST under `/api/visits`, `/api/visitors`, `/api/access-zones`, `/api/checkpoints`,
  `/api/patrol-routes`, `/api/patrol-runs`, `/api/patrol-scans` (batch, idempotent),
  `/api/public/visit/{key}` (pre-reg), `/api/public/kiosk/{factoryKey}`.
- Webhooks (existing `TenantWebhook`): `visit.checked_in`, `visit.checked_out`, `patrol.missed`,
  `incident.security.created`.
- Later integrations behind interfaces: turnstiles/boom barriers (relay or vendor API on
  check-in), CCTV/NVR event links on incidents, ANPR at the gate, ID-card readers. None are needed
  for the pilot.

---

## 10. Phasing

Sized like scope.md (2 developers + part-time QA/design). Each phase ships to the pilot factory.

### S1 — Shared foundation and licensing (≈2 weeks)
`AccessZone`, `Device`, roles, add-on table and migration of the WhatsApp add-on, module keys and
gating, standalone edition, tea zone seed.
**Exit:** a tenant can be given Security without Maintenance and sees only platform + empty
security screens. Classification tests pass.

### S2 — Visitor Management core (≈4–5 weeks)
Visitor, visit, attendee, approvals, walk-in and pre-registration, gate checks (blocklist,
contractor papers, induction, PPE, escort), badge PDF, check-in/out by badge scan, overstay worker,
host notifications, visitor register, emergency roll call, retention worker.
**Exit:** the pilot gate runs all visitors through the system for two weeks. A roll-call drill
lists everyone on site within one minute.

### S3 — Security Patrol core (≈5–6 weeks)
Checkpoints and tag printing, routes, schedules, run generation, guard app with QR + GPS +
sequence + gap validation, offline scans, missed/partial alerts, handover, security incidents
(Incident extension), supervisor board, daily PDF report.
**Exit:** every night patrol at the pilot is recorded for two weeks. Missed checkpoints reach
the supervisor within 15 minutes of `DueBy + Grace`, including scans made offline and synced later.

### S4 — Integration and hardening (≈3 weeks)
Permit and work-order gate rules, contractor hours vs labour hours, badge lookup on patrol and
out-of-zone incidents, NFC and secure-NFC verification, device binding and clock-skew flags,
vehicle log, recurring passes, group dashboard tiles, webhooks.
**Exit:** the §6 contractor flow runs end to end at the pilot. A replayed or cloned NFC tap
is flagged `Replay`.

### Later (not in this spec)
Turnstile/barrier and CCTV integration, ANPR, kiosk hardware, native iOS app, Fire & Emergency
(equipment, drills), Utilities, separate Safety SKU.

---

## 11. Decisions needed

| # | Question | Recommendation |
|---|---|---|
| 1 | Pricing basis for add-ons | Per factory per year, with seat caps per factory |
| 2 | NFC tag standard | Plain NTAG for normal points; secure-message NFC tags for perimeter, fuel store, finished goods |
| 3 | Guard handsets | One shared Android phone per post using shared-device mode with PIN (already in the platform) |
| 4 | ID capture | Masked number + hash only; no ID images by default |
| 5 | Pilot order | S1 → S2 (visitors) before S3: smaller, visible to management, and it brings the security team into the system |
