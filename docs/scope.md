# Ecosys Factory Maintenance — Scope of Work

Nov 3, 2026 · Draft for review

This document scopes the whole build described in [requirements.md](requirements.md): architecture, what is reused from Ecosys ServiceOps, every phase with its deliverables and exit criteria, and what is deliberately left out.

Decisions taken so far:

| Decision | Choice |
|---|---|
| Tech stack | Same as ServiceOps: .NET 10 API, EF Core + PostgreSQL, React 19 + Vite + TypeScript + Tailwind, TanStack Query |
| Mobile | Installable PWA (one codebase), offline via IndexedDB + outbox sync |
| Hosting | Provider-agnostic Docker deployment. Kenya or international cloud chosen on cost at go-live; nothing in the build may depend on one provider |
| Scope | The full product (Phases 0–8 below), delivered in sellable increments |

---

## 1. Starting point: what ServiceOps gives us

ServiceOps (`Dennis-debug-it/ecosys-serviceops`, `main` @ `248e221`) is a mature field-service platform. It is built for a **service company maintaining its clients' assets** (clients, sites, contracts, quotations, invoices). Factory Maintenance is **in-house maintenance of the factory's own assets**: no clients, no billing, but deeper asset hierarchy, production-driven scheduling, and compliance.

So the platform layer transfers almost entirely; the domain layer is partly reusable as patterns and code, but the data model must be rebuilt around the factory hierarchy.

### 1.1 Reuse as-is (platform layer)

| Capability | ServiceOps source | Notes |
|---|---|---|
| Multi-tenancy with query filters | `ITenantEntity`, `AppDbContext` global filters, `HttpTenantContext` | Strict tenant isolation already enforced at the ORM level |
| Auth, sessions, password policy, refresh tokens | `AuthController`, `MvpAuthService`, `SessionServices`, `PasswordServices` | |
| Roles and fine-grained permissions | `RolePermission`, `UserPermission`, `DynamicAccessService` | Extend scope from branch to factory/section |
| Audit trail | `AuditLog`, `AuditLogService` | Requirement: every record change |
| Notifications (in-app, email outbox, web push, WhatsApp) | `NotificationDispatcher`, `EmailOutboxServices`, `WebPush`, `Services/WhatsApp` | SMS channel to be added |
| Offline outbox with idempotency and field timestamps | `features/tech/offline/*`, `IdempotencyFilter`, `OccurredAtMiddleware` | Core of the offline requirement |
| PWA shell, persisted query cache | `vite-plugin-pwa`, `react-query-persist-client`, `idb` | |
| File storage and attachments | `FileStorageService`, `Attachment` | Add versioning for manuals/drawings |
| PDF rendering | QuestPDF (`QuestPdfRenderer`) | Job cards, window packs, evidence packs |
| Excel import | `ImportController`, `ImportBatch`, ExcelDataReader | Bulk asset onboarding |
| Scheduled reports | `ScheduledReport`, `ScheduledReportService` | Requirement: emailed reports to managers/boards |
| Approval workflows | `ApprovalMatrix`, `WorkflowDefinition`, `ApprovalWorkflowService` | Configurable approvals |
| API keys and webhooks | `TenantApiKey`, `TenantWebhook` | Integration readiness (Phase 8 builds on this) |
| Exchange rates, multi-currency, time zones | `ExchangeRate`, `TenantTimeZoneService` | KES/USD |
| Background workers with leases | `BackgroundWorkLease`, `PmSchedulerWorker` pattern | |
| Platform admin, licensing, tenant lifecycle | `Platform*` controllers, editions/seat model | Re-price for factory editions |
| Real-time updates | SignalR hubs | Dashboards, dispatch |
| Deployment | `docker-compose.yml`, nginx, backup container, `DEPLOYMENT.md`, `VPS_SETUP.md` | Already provider-agnostic |
| Design system and brand | `.agents/skills`, shared components | |
| Test harness | xUnit tests, Playwright e2e | |

### 1.2 Reuse as a starting point, then adapt (domain layer)

| ServiceOps feature | Adaptation for factories |
|---|---|
| `Asset`, `AssetCategory`, `OperationalSystem` | Drop client ownership; add hierarchy (group → factory → section → asset → component), criticality, lifecycle events, lost-production rate, asset-class templates |
| `WorkOrder` + lifecycle services | New status set (requested → approved → planned → assigned → in progress → completed → verified → closed), new types (statutory, project/modification, safety), new hold reasons (window, shutdown, contractor), failure codes, rework links, child jobs |
| `PreventiveMaintenancePlan`, `PmTemplate*`, `PmSchedulerWorker` | Add meter triggers (run hours, kg made tea), fixed vs floating, compliance window, defer/skip with reason, "needs line stop" → window queue. Fix the known gap that the worker does not advance `NextPmDate` |
| `PmTemplateQuestion` checklists | Becomes the general form builder (pass/fail, readings, photos, signatures), with failed item → auto work order |
| `SlaDefinition`, `SlaRule`, escalation | Priority × criticality matrix |
| `PublicWorkOrderController` (tokenised public link) | Basis for the operator QR fault-report portal (no full login) |
| Technicians, assignment groups, leave, attendance, `UserCertification` | Trades, shifts, rosters, on-call, certification expiry, labour rates incl. overtime |
| Inventory (materials, stock movements, transfers, counts, parts requests/returns, purchase orders, suppliers) | Multiple stores and bins, reservation/kitting, where-used parts lists, serialised and repairable spares, group-wide visibility |
| Role dashboards | Group / factory / section dashboards with the KPI set in requirements §6 |
| `Branch` (with parent branch) | Template for factory and group levels |

### 1.3 Not carried over

Clients, client portal, contracts-as-sold, quotations, invoices, credit notes, payments, withholding certificates, Sales CRM, platform CRM/leads, projects-as-sold, vans, dispatch map, CSAT, knowledge/AI (Kip) — unless a later customer needs them. The legacy `Ecosys.ServiceOps` and `Ecosys.Platform` projects in ServiceOps (old domain, not referenced by the API) are not copied.

### 1.4 How the code is shared

**Decision: fork ServiceOps with its full git history, then prune what factories do not need, keeping the build and tests green after every removal. Extract a shared `Ecosys.Platform` package after Phase 6, once both products are stable.**

- ServiceOps is tightly coupled (`Asset` requires a `ClientId`; `Branch` links to vans, stock and numbering; `AppDbContext` is ~5,000 lines). A selective copy would not compile until those links were cut by hand, without a working app to test against. Pruning a working, tested app is mechanical and safer.
- The shared history means platform fixes can be ported with `git cherry-pick` (in either direction) until the shared package exists. The fork point and every ported fix are recorded in `docs/serviceops-provenance.md`.
- Extracting a shared package now would block both products on refactoring ServiceOps first.

Fork rules:

1. Fork point: ServiceOps `main` @ `248e221` (2026-11-01). ServiceOps history is merged into this repository's history.
2. Prune one module per commit; each commit builds and passes the remaining tests.
3. Removing every out-of-scope module (§1.3) is a Phase 0 exit criterion, not optional clean-up.
4. Rename to the factory model as we go (branch → factory, site → section, assets lose client ownership).
5. Once pruning is done, the ~290 ServiceOps migrations are squashed into one fresh baseline migration.
6. Port platform fixes from ServiceOps at least every two weeks while the codebases are still close.

---

## 2. Architecture

```
PWA (React, offline)  ──HTTPS──  nginx  ──  Ecosys.Api (.NET 10)  ──  PostgreSQL
  IndexedDB cache                  │          ├─ background workers (PM, alerts, sync, reports)
  outbox + delta sync              │          ├─ SignalR
  camera QR scanning               │          └─ notification gateways (email, SMS, WhatsApp, push)
                                   └── object storage (local volume or S3-compatible)
```

Solution layout (mirrors ServiceOps):

- `backend/src/Ecosys.Domain` — factory entities and enums
- `backend/src/Ecosys.Infrastructure` — EF Core, services, workers, integrations
- `backend/src/Ecosys.Api` — controllers, offline middleware, hubs
- `backend/src/Ecosys.Shared` — auth, errors, options (copied)
- `backend/tests/Ecosys.Tests`
- `frontend/` — React PWA with feature folders (`assets`, `work-orders`, `pm`, `window`, `overhaul`, `inventory`, `team`, `safety`, `compliance`, `dashboards`, `scan`, `operator`)

Design rules that apply from Phase 1, because retrofitting them means reworking the data model:

1. **Stable external IDs** on assets, parts, cost centres, vendors, users (`ExternalId`, `ExternalSystem`) for ERP mapping.
2. **Hierarchy as data**, not as fixed columns: a configurable `OrgNode` tree (group, factory, section, sub-section) with levels that can be hidden. Sections ship as the tea default set and are editable.
3. **Meters are first-class**: `Meter` + `MeterReading` per asset (run hours, kg made tea, odometer, temperature, vibration, pressure), used by PM, condition monitoring and costing.
4. **Offline-ready records**: client-generated GUIDs, `RowVersion` for optimistic concurrency, `UpdatedAt` index for delta sync, soft deletes (tombstones) so deletions sync.
5. **API-first**: every UI action is a documented API call (OpenAPI already in the stack).
6. **No hard-coded tea**: tea is a template pack (asset classes, sections, PM routines, KPIs in kg made tea). The production unit (`kg made tea`) is a tenant setting so sugar or dairy can use their own.
7. **Hosting-neutral**: object storage behind an interface (local disk or S3-compatible), SMTP/SMS/WhatsApp behind gateways, no cloud-proprietary services. The same images run on a Kenyan host, an international cloud, or on-premises.

### 2.1 Offline and sync design

Builds on the ServiceOps outbox (idempotency keys, `X-Occurred-At`), extended for a CMMS:

- **Download sets**: per user, the assets of their factory/sections (with history summary), assigned and open jobs, the current window job pack, checklists, parts lists, and reference data. Refreshed by delta sync (`?since=`).
- **Offline actions**: scan, view, report fault, start/complete job, checklist answers, readings, parts used, hours, photos, signatures, shift handover notes.
- **Conflicts**: field-level merge where safe (e.g. two technicians adding notes or parts); for status changes the server is the arbiter, and a rejected change is shown back to the user with the server state. Every conflict is logged.
- **Low data**: photo compression on device before upload, gzip, delta payloads only.
- **Shared-device mode**: quick user switch with PIN on one phone; outbox entries stay tied to the user who made them.
- **Scanning**: camera QR/barcode via the browser `BarcodeDetector` API with a JS fallback library for older Android browsers. NFC (Web NFC on Android Chrome) as an option.

### 2.2 Hosting

Docker Compose for the first customers (as ServiceOps today). The deployment is containerised so it can move to a managed Kubernetes or a managed PostgreSQL service later without code changes. Hosting options to price at go-live:

| Option | For | Watch |
|---|---|---|
| Kenyan data centre / local cloud | Data residency under the Kenya DPA 2019, latency, local billing in KES | Price, managed database availability, backup off-site |
| International cloud (EU or South Africa region) | Price, managed services, maturity | Cross-border transfer obligations under the DPA |
| On-premises (enterprise tier) | Groups that insist on it | Delivered in Phase 8 from the same release |

---

## 3. Phased scope

Phases follow requirements §9: Phases 1–6 are the sellable core; 7–8 are expansion. Phase 0 is added for the platform carve-out. Each phase ends with a usable release to the pilot factory.

Sizes are indicative for a small team (2 developers plus part-time QA/design) and will be re-estimated after Phase 0.

### Phase 0 — Platform foundation (≈2–3 weeks)

- Fork ServiceOps into this repo with history (§1.4); record the fork point
- Prune out-of-scope modules one at a time, green after each: finance (quotations, invoices, credit notes, payments, withholding, expenses), Sales CRM and platform CRM/leads, client portal, projects, vans and dispatch, CSAT, Kip AI and knowledge, legacy `Ecosys.ServiceOps` and `Ecosys.Platform` projects
- Rename to the factory model: branch → factory (with group as parent), site → section; assets lose client ownership
- Squash migrations into a fresh baseline; keep tenant, user, role, permission, audit, notification, file, import, PDF, scheduled reports, webhooks, API keys
- `OrgNode` hierarchy (group → factory → section → sub-section) and permission scoping down to factory and section
- PWA shell, login, offline cache and outbox wired up; shared-device mode
- CI (build, unit tests, e2e smoke), Docker Compose deployment, backups
- Tenant settings: production unit (kg made tea), currency, time zone, maintenance window defaults

**Exit:** no out-of-scope module remains; build, tests and Docker deployment pass; a tenant with two factories and their sections can be created; users scoped to one factory cannot see the other; the PWA installs and works offline for the shell.

### Phase 1 — Assets, tags and meters (≈5–6 weeks)

- Asset register: specs, location in hierarchy, manufacturer, model, serial, install date, criticality A/B/C, status, warranty with expiry alerts
- Parent–child components with history at component level
- Asset classes with tea templates: withering troughs and fans, CTC machines, rotorvanes, CFUs, fluid bed dryers, sorting/grading, packing, boilers, generators, weighbridges, compressors, transformers, small hydro; vehicles with odometer
- Lifecycle events: commissioning, transfer between sections/factories, decommissioning, disposal
- Documents with versioning (manuals, drawings)
- Meters and readings (manual entry, history, charts)
- Tags: unique asset numbers, QR/barcode label batch printing (PDF sheets), reprint, replace damaged tag, duplicate prevention
- Scan-to-open asset page (online and offline)
- Bulk import from Excel template; mobile asset survey mode (walk the floor, create asset, photo, tag)
- Stable external IDs; asset API

**Exit:** the pilot factory's real asset list is loaded and tagged; a technician with no signal can scan any tag and see the asset and its history.

### Phase 2 — Work orders and mobile execution (≈6–7 weeks)

- Work order types: corrective, preventive, inspection, statutory, project/modification, safety
- Full lifecycle with configurable approvals; hold statuses (parts, window, shutdown, contractor)
- Operator request portal: scan QR and report a fault without full login (tokenised, rate-limited, photo)
- Priority × criticality SLA matrix for response and repair; escalation
- Job plan templates: tasks, tools, parts, estimated hours
- Failure codes (problem / cause / remedy) aligned with ISO 14224, seeded for tea machinery
- Rework detection: same fault on same asset within N days links and flags the previous job
- Split into child jobs, reassign, before/after photos, shift handover notes, e-signature on completion, verification step
- Labour hours and parts used captured on the job (costing comes in Phase 6)
- Offline execution of all the above; conflict handling
- Notifications: in-app, email, WhatsApp, and SMS gateway added (Africa's Talking or equivalent, behind an interface)
- **Checklist and form builder:** pass/fail, readings with limits, photos, signatures; versioned templates per asset class; a failed item raises a work order (or a "fix in window" defect); readings feed the asset's meters
- **Running checks (daily / per shift):** operator and artisan checklists scheduled per shift or per day, by section or as a route; done by scanning the asset or walking the route, fully offline; missed checks are flagged to the shift supervisor; tea templates seeded (withering fan, CTC, dryer, boiler shift log, compressor, generator)
- Basic factory dashboard: open jobs, overdue, breakdowns, top failing assets, running-check completion per shift

**Exit:** the pilot runs all breakdown and corrective work, and its daily running checks, through the system for at least two weeks, including from the floor offline.

### Phase 3 — Planned maintenance: PM, weekly window, overhaul, statutory (≈7–8 weeks)

- PM triggers: calendar, run hours, kg made tea, other meters, manufacturer recommendation
- Fixed vs floating schedules; compliance window (±%); defer/skip with mandatory reason; "defer until low crop" with risk flag on A-class assets
- PM scheduler that advances due dates correctly (fixes the known ServiceOps gap)
- Route-based PM (lubrication and inspection rounds)
- PM work orders use the Phase 2 checklist builder for their task lists
- Workload forecast and levelling across weeks
- **Weekly window:** per-factory window setting (Monday default, movable/skippable); "needs line stop" jobs auto-queue; operators log "fix in window" defects; window job pack generated ahead (jobs, technicians, reserved spares, permits, contractors) and downloaded offline; capacity check; standard weekly routine checklists (lubrication, belt and chain tension, CTC segment inspection, dryer and fluid bed cleaning, fan checks, panel inspections, boiler checks) generated into each window; handback start-up checklist and sign-off; carryover with escalation and move to overhaul list
- **Annual overhaul:** overhaul list built through the season (deferred, carryover, condition findings); scope, budget, spares and contractor planning; progress tracking during shutdown; recommissioning checklists
- **Statutory reminders:** boilers, pressure vessels, lifting equipment (OSHA 2007 / DOSH), calibration (instruments, weighbridges, scales, metal detectors), fire equipment: due dates, certificates, expiry alerts, owner, escalation when overdue
- Compliance item model (owner, due date, evidence, escalation) — first part of the compliance framework

**Exit:** a Monday window is planned, packed, executed offline and handed back through the system; PM compliance and statutory due/overdue are visible.

### Phase 4 — People and safety (≈5–6 weeks)

- Teams, trades and skills (fitters, electricians, boiler operators, instrument technicians)
- Shifts, rosters, on-call, leave; assignment respects availability and skills
- Certification register with expiry alerts (electrical licence, boiler operator, welding, working at heights); block assignment of a job needing a certificate the technician lacks
- Labour rates incl. overtime
- Workload view and productivity
- **Safety:** permit-to-work (hot work, confined space, electrical, working at heights) with approval and close-out; LOTO records with isolation points per asset; start-up safety checks before handback
- Incident and near-miss register linked to asset and corrective work order
- Training records (induction, skills) for technicians and contractors; PPE issue records

**Exit:** no high-risk job in the window can start without an approved permit; certification expiries are tracked for the whole maintenance team.

### Phase 5 — Spares, inventory and contractors (≈6–7 weeks)

- Multiple stores and bin locations; stock levels, minimums, reorder points
- Reservation and kitting against planned jobs and the window pack
- Parts lists per asset (where-used); critical spares flags linked to criticality
- Serialised and repairable spares (e.g. CTC roller segments out for re-cutting and back)
- Issues, returns, transfers, cycle counts, stock valuation; units of measure; multi-currency pricing
- Lightweight purchase requisitions and vendor catalogue
- Shared spares visibility across factories in a group, with inter-factory transfer
- **Contractors:** register with compliance documents and expiry alerts; AMCs with scope, SLAs, visit schedules and renewal alerts; job assignment; optional contractor portal; contractor invoices matched to completed work orders
- Chemicals register (lubricants, boiler water treatment) with safety data sheets; food-grade lubricant records

**Exit:** every part used on a job is issued from a store; the window pack shows reserved and kitted spares; stock-outs of critical spares raise alerts.

### Phase 6 — Costs, dashboards and compliance evidence (≈6–7 weeks)

- Cost per work order, asset, section and factory: labour, parts, contractor, downtime
- Downtime capture and costing in kg made tea lost (lost-production rate per asset); peak vs low season
- Annual budgets per factory and cost centre; budget vs actual; multi-currency and VAT
- Dashboards at group, factory and section level covering all requirement §6 KPI categories (assets, reliability, planning, window, overhaul, downtime, costs, spares, people, energy, compliance)
- Energy: kWh and firewood per kg made tea from meters
- Exports to Excel and PDF; scheduled email reports
- **Compliance framework:** configurable standards (Rainforest Alliance, Fairtrade, ISO 22000/FSSC/HACCP, OSHA statutory, ISO 9001/14001/45001/50001/55001) as lists of requirements, each mapped to the checklists, records and documents that prove it; non-conformities as corrective actions; audit calendar; one-click evidence pack per factory for a chosen period
- Waste disposal records (used oil, scrap, batteries, e-waste) with licensed contractor
- Food safety records: cleaning logs, glass and hard plastic register, metal detector checks
- Data protection: consent records, personal data export/erasure, retention settings (Kenya DPA 2019)
- Pilot results report (downtime, PM compliance, cost per kg made tea) to support go-to-market step 2

**Exit:** the pilot can produce a Rainforest Alliance maintenance evidence pack and a monthly board report without manual spreadsheet work. **This completes the sellable core.**

### Phase 7 — Expansion: condition monitoring and new markets (≈6–8 weeks)

- Thresholds on readings that raise alerts or work orders; trend charts
- Sensor/IoT ingestion endpoint (MQTT or HTTP) on the existing meter model
- Predictive groundwork: MTBF-based PM suggestions, repeat-failure analysis
- Leaf collection fleet maintenance (if confirmed in scope)
- Template packs for other industries: sugar, dairy, cement, flower farms, data centres, hospitals (biomedical, cross-sell with ECOSYS HEALTH)
- Swahili interface (i18n framework is added in Phase 0 so strings are translatable from the start)

### Phase 8 — Expansion: integrations and enterprise (≈6–8 weeks)

- ERP connectors: SAP, Oracle, Microsoft Dynamics 365 — assets, maintenance, inventory, procurement and cost exchange using the stable external IDs
- Event webhooks (work order closed, stock below minimum, asset down) productised and documented
- SCADA/PLC and energy-monitoring integration
- SSO / Active Directory (Entra ID, SAML/OIDC)
- On-premises tier: installer, licence keys, offline update bundles, remote support model
- Disaster recovery runbook and tested restores; data residency options per tenant

---

## 4. Indicative timeline

| Phase | Weeks | Cumulative |
|---|---|---|
| 0 Foundation (fork and prune) | 2–3 | ~0.75 month |
| 1 Assets, tags, meters | 5–6 | ~2.5 months |
| 2 Work orders, mobile | 6–7 | ~4 months |
| 3 PM, window, overhaul, statutory | 7–8 | ~6 months |
| 4 People and safety | 5–6 | ~7.5 months |
| 5 Spares and contractors | 6–7 | ~9 months |
| 6 Costs, dashboards, compliance | 6–7 | ~10.5 months (sellable core) |
| 7 Condition monitoring, new markets | 6–8 | ~12.5 months |
| 8 Integrations, enterprise | 6–8 | ~14.5 months |

The pilot factory can start using the product at the end of Phase 1 (asset onboarding and tagging run in parallel with Phase 2 development). The timeline should be aligned with the tea calendar: Phase 3 should land before the next low-crop shutdown so the overhaul is planned in the system.

---

## 5. Cross-cutting work (every phase)

- Tests: unit tests for rules (scheduling, SLA, sync conflicts, costing); API integration tests; Playwright e2e including offline scenarios
- Audit trail on every new entity
- Permissions checked at factory/section scope on every endpoint
- Mobile usability: large touch targets, minimal typing, glove and low-light use; tested on a low-end Android phone on a throttled connection
- API documentation kept current (OpenAPI)
- Demo/seed data: a realistic tea factory for sales demos and e2e tests
- Implementation tooling for the services revenue stream: asset survey mode, import templates, label printing

---

## 6. Out of scope (unless added)

- Native Android/iOS apps (PWA chosen)
- Billing, invoicing and client management (ServiceOps covers those for service companies)
- Full procurement/ERP functionality beyond requisitions and a vendor catalogue
- Hardware supply (tags, printers, NFC readers, sensors) — sourced as part of implementation services
- Real-time SCADA control (read-only integration only)

---

## 7. Risks

| Risk | Mitigation |
|---|---|
| Offline sync conflicts corrupt job data | Server-arbitrated status changes, idempotent outbox (proven in ServiceOps), conflict log, offline e2e tests from Phase 0 |
| Pilot asset data is incomplete or messy | Survey mode and import templates in Phase 1; budget implementation time for data collection |
| Scope is large for a small team | Strict phase exits; Phases 7–8 only after the core is in paying use |
| ServiceOps and this product drift apart | Shared history allows cherry-picking fixes; port at least fortnightly; shared package extraction after Phase 6 |
| Pruning stalls and unused code lingers | Full removal is a Phase 0 exit criterion |
| Hosting cost/residency decided late | Hosting-neutral rule (§2 rule 7) means the choice does not change the code |
| Low-end phones struggle with camera scanning | Test on target devices in Phase 1; fallback scanner library; manual asset number entry |

---

## 8. Decisions still open

From requirements §10, still needed — none of them block Phase 0:

- Pricing model (per asset, per technician, per factory) and free tier — needed before Phase 6 licensing work
- Monday window: full day everywhere, cancelled in peak flush? — needed for Phase 3
- Pilot A-class assets — needed in Phase 1 onboarding
- Statutory inspections the pilot tracks today, and how — needed for Phase 3
- Leaf collection fleet in first release? — affects Phase 7 placement
- Swahili in first release or later? — affects Phase 7 placement
- Which Rainforest Alliance requirements the pilot's last audit checked on maintenance — needed for Phase 6
- Hosting provider — needed before go-live at end of Phase 1
