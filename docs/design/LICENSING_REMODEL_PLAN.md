# Licensing Remodel Plan

Status: proposed · 2026-10-24

## Summary

Replace module licensing (Operations / Finance / Suite) with **editions × seat types + usage packs**:

- **Three editions** — Essentials, Professional, Enterprise. Every edition runs the complete loop
  (win work → do it → bill it); higher editions add depth, control and scale.
- **Two paid seat types** — Office and Field (technician), with free Viewer/Auditor and portal users.
- **Usage packs** only where Ecosys carries a real marginal cost (messaging, AI, storage).
- **CRM and Projects** are built into the editions, not sold as add-ons.

Why: module splits leave customers with half a workflow, and they create cross-module seams in the code
(`WorkOrderBilling` needs both modules; `Portal`, `Import` and `Settings` are classified per route).
Flat seat pricing makes each technician cost as much as a manager, which encourages login sharing and
undermines time & attendance, GPS and audit data.

## 1. Editions

| Area | Essentials | Professional | Enterprise |
|---|---|---|---|
| Work orders, dispatch, technician mobile app | ✓ | ✓ | ✓ |
| Clients, sites, client portal | ✓ | ✓ | ✓ |
| Quotations, invoices, credit notes | ✓ | ✓ | ✓ |
| Basic reports & dashboard | ✓ | ✓ | ✓ |
| WhatsApp notifications from the shared Ecosys number (outbound only) | ✓ | ✓ | ✓ |
| CRM — lead capture, lead → client, quotation follow-up | ✓ | ✓ | ✓ |
| CRM — pipeline board, stages, owners, activities, forecast | | ✓ | ✓ |
| Projects — phases, linked work orders, budget vs actual, milestone invoicing | | ✓ | ✓ |
| Preventive maintenance, PM templates, assets, asset categories, systems | | ✓ | ✓ |
| Contracts, SLA policies, escalation rules | | ✓ | ✓ |
| Inventory — materials, vans, purchase orders, parts, stock transfers | | ✓ | ✓ |
| Workflows, intake rules/protocols, email intake | | ✓ | ✓ |
| Time & attendance, CSAT | | ✓ | ✓ |
| WhatsApp inbox on the tenant's own platform-managed number (two-way) | | ✓ | ✓ |
| Expenses & payables, credit control, approvals, scheduled reports | | ✓ | ✓ |
| Multiple branches | | | ✓ |
| API keys & webhooks, monitoring integration | | | ✓ |
| Approval matrix, custom report templates, advanced roles | | | ✓ |
| Cross-branch / cross-project profitability reporting | | | ✓ |

The disputed boundary is inventory. It sits in Professional because stock control signals a firm that has
outgrown Essentials. Revisit it after the first ten Essentials sales.

## 2. Seats

| Seat | Roles (`AppRoles`) | Price |
|---|---|---|
| Office | `Admin`, `Manager`, `Dispatcher`, `finance` (FinanceUser), `MaterialsController` | Full rate |
| Field | `Technician` | ~40–50% of the office rate |
| Free | `Viewer` (incl. legacy `User`), `Auditor`; client-portal contacts | Unlimited |

- Each edition has a minimum number of office seats (proposed: Essentials 2, Professional 3, Enterprise 10).
- Seats are counted as they are today: active users, pending invitees included. Deactivated users are not counted.
- A role change re-checks seats: promoting a technician to dispatcher takes an office seat.
- Sales staff use office seats. There is no separate CRM seat type.

## 3. Usage packs

Each edition includes a monthly allowance. Top-ups are sold in packs. There is no hard stop mid-job:
going over the allowance raises a warning first, then blocks only *new* sends or uploads.

| Meter | Measured at | Note |
|---|---|---|
| WhatsApp conversations (SMS when it exists) | The platform WhatsApp sender (see §4) | Ecosys pays Meta for every conversation, so every tenant is metered. |
| Kip AI queries | `KipController.Query` | Kip is currently a stub, so the meter ships with Kip. |
| Storage (GB) | `FileStorageService` upload | Photos, reports, attachments |
| Email-intake mailboxes | Email intake settings | Count of connected mailboxes |

## 4. WhatsApp is run by the platform

Tenants no longer connect their own Meta account. Ecosys owns the WhatsApp Business setup, holds the
credentials, pays Meta, and bills tenants through the usage meter.

- **One Meta Business account, owned by Ecosys.** Ecosys registers as a Meta Tech Provider. All access
  tokens and webhook secrets live in platform settings and are never shown to tenants.
- **Shared Ecosys number (all editions):** outbound notifications only — job booked, technician on the
  way, report ready, invoice sent. Messages carry the tenant's name in an approved template. Replies to
  this number get an auto-reply pointing the client to the tenant's contact details, because a shared
  number cannot reliably route an inbound message to the right tenant.
- **Dedicated number per tenant (Professional+):** Ecosys provisions a number for the tenant under its
  own Meta account, with the tenant's display name. Inbound messages route by number to the tenant's
  inbox, so the two-way inbox, auto-reply and intake from WhatsApp all work. An extra number is a paid
  extra.
- **Templates:** Ecosys owns a standard approved template set. A tenant can request a custom template;
  the platform submits it to Meta and approves it on the tenant's behalf.
- **Control:** the platform can pause sending for a tenant (non-payment, spam complaints, quality rating
  drop) without touching the rest of the account.

## 5. Extras outside entitlements

- **Industry packs** (solar installation, HVAC, generators, electrical): checklists, report templates and
  asset categories. These build on the existing starter packs and are sold one-off or yearly. They never
  gate an API route.
- **Extra dedicated WhatsApp numbers.**
- **Paid onboarding and data migration.**

## 6. Commercial terms

- Billed monthly or annually. Annual prepay gets about 2 months free (`BillingCycles` already supports this).
- Trial is Professional for 14 days. When it ends, the tenant picks an edition. The existing grace period
  and read-only mode cover the gap.
- Prices are set in KES, with a USD price list. Tenants already have a `Currency`.

---

# Implementation

Five parts, each shippable on its own: **Licensing core → Projects → CRM → Platform WhatsApp → Usage packs**.
Rough effort for one developer: 13–18 weeks total. Platform WhatsApp and usage packs ship together
if any tenant is live on WhatsApp, because the platform starts paying for messages on day one.

## Part 1 — Licensing core (2–3 weeks)

### 1.1 Feature keys replace modules

- Replace the `operations`/`finance` module sets in
  `backend/src/Ecosys.Infrastructure/Services/ServiceOpsPlans.cs` with a `LicenseFeatures` catalogue
  (e.g. `work_orders`, `maintenance`, `inventory`, `contracts_sla`, `workflows`, `time_attendance`,
  `expenses`, `approvals`, `crm_basic`, `crm_pipeline`, `projects`, `multi_branch`, `api_webhooks`,
  `monitoring`, `approval_matrix`, `custom_reports`).
- Tag every tenant controller with exactly one feature key, or mark it shared. Keep the rule enforced by
  the classification check in `TenantLifecycleConsistencyTests`: an unclassified controller fails the build.
- An edition is a fixed set of feature keys, defined in code (`Editions.cs`) so the boundaries are
  reviewable in a pull request. `LicensePlan` stores only commercial data: code, name and prices.
- Per-tenant overrides become `tenant_license_feature_overrides (TenantLicenseId, FeatureKey, Enabled)`.
  This replaces `MaxBranchesOverride`, `MonitoringIntegrationOverride` and the unused `EmailIngestionOverride`.
- `TenantLicenseEnforcementMiddleware` checks `snapshot.Features` instead of `snapshot.Modules`. Remove
  the route-classification special cases where a single feature key now covers the route.

### 1.2 Data model

- `license_plans`: rows `essentials`, `professional`, `enterprise`. Prices: monthly/annual for office
  seats, monthly/annual for field seats, and minimum office seats.
- `TenantLicense.LicensedUsers` becomes `OfficeSeats` + `FieldSeats`. Both have a CHECK constraint, like
  `CK_tenant_licenses_LicensedUsers_Positive`: office seats > 0, field seats ≥ 0.
- Drop `LicensePlan.ModulesIncluded`, `EmailIngestion`, `MonitoringIntegration` and `MaxBranches`.

### 1.3 Seat enforcement

- `LicenseGuardService.CountSeatsUsedAsync` returns `(office, field)`, grouping active users by role into
  a seat type. One helper `SeatTypes.For(role)` holds the mapping.
- `EnsureCanAddUsersAsync(tenantId, role, count)` checks the matching pool and keeps the existing
  row lock on the license.
- Call sites to update: tenant user create/reactivate, platform add-user, CSV import, and **role change**
  (new; today a role change never touches seats).
- Error codes: `license_office_seats_reached`, `license_field_seats_reached`.

### 1.4 Migration of existing tenants

One SQL migration, following the pattern of `RetireLegacyPlansAndRequireLicensedUsers`:

1. Insert the three edition plans.
2. Move every license on `operations`, `finance` or `suite` to `professional`. No tenant loses access.
   Tenants already using multiple branches, monitoring or API keys move to `enterprise` instead.
3. Split seats: `FieldSeats` = active technicians; `OfficeSeats` = max(edition minimum,
   `LicensedUsers` − field seats, active office users). No existing user may end up over a limit.
4. Convert existing overrides into `tenant_license_feature_overrides`.
5. Delete the old plans, and drop the old columns in a follow-up migration once the release is stable.

Current prices are honoured until each tenant's next renewal. Record each tenant's price-lock end date in
`Notes`, or add a `PriceLockedUntil` column.

Dry-run the migration against a production snapshot and diff the access for each tenant before running it.

### 1.5 Frontend

- `ecosys-serviceops-frontend/src/shared/utils/entitlements.ts`: map path prefixes to feature keys
  instead of the two module lists.
- `useEntitlements`: expose `hasFeature`, `edition`, `officeSeats`/`fieldSeats` and their usage.
- `navigation.ts`: hide features the edition lacks, or show them locked with an upgrade prompt.
- `features/team` (users page, new-user drawer, role change): show seat usage for each pool and the
  seat-limit errors.
- Platform screens (`PlansSettingsPage`, `LicensingPage`, `TenantDetailPage`, `LicenseTermDialog`,
  `licensedUsers.ts`): edit edition prices, set office/field seats, set feature overrides.
- Update MRR snapshots and platform billing (`MrrSnapshotWorker`, `PlatformBillingLifecycleService`) to
  calculate price as seats × rate for each seat type.

### 1.6 Tests

- Rewrite `ServiceOpsPlanTests` around editions and feature keys, with one test per edition boundary.
- Rewrite `TenantLicensedUserLimitTests` for two seat pools, including a role change between pools.
- Add a migration test: tenants on the old plans end up with access equal to or greater than before.
- Update the e2e tests `serviceops-plans.spec.ts` and `platform-tenant-management.spec.ts`.

## Part 2 — Projects (4–5 weeks; about 2 without costing)

Feature key: `projects` (Professional+).

- **Domain:** `Project` (client, site, code, name, manager, status, start/end, budget, currency);
  `ProjectPhase` (name, order, planned dates, milestone amount); nullable `WorkOrder.ProjectId` and
  `WorkOrder.ProjectPhaseId`.
- **Progress:** work orders completed out of total, per phase and for the whole project.
- **Costing:** actual cost = materials issued + labour (time & attendance hours × rate) + expenses
  tagged to the project, compared against the budget.
- **Billing:** invoice a phase milestone, or a percentage of progress, through the existing tenant invoice
  service.
- **Screens:** project list, project detail with phases and a work-order timeline, "New work order" from a
  project, and a project picker on the work-order form.
- **Enterprise:** a profitability report across projects and branches, and approval for budget changes.
- **First customer:** Plexus solar installations, with the solar checklist delivered as an industry pack.

## Part 3 — CRM (3–4 weeks)

Feature keys: `crm_basic` (all editions), `crm_pipeline` (Professional+).

- **crm_basic:**
  - `Lead` entity (source: web form / email intake / WhatsApp / manual; contact details; status; owner).
  - Convert a lead to a `Client`.
  - Quotation follow-up view based on `TenantQuotation.Status` (Draft → Sent → Accepted/Rejected/Expired),
    with reminders to chase quotes.
- **crm_pipeline:**
  - `Opportunity` (client or lead, value, expected close date, owner).
  - Configurable `PipelineStage` list, shown as a Kanban board.
  - `CrmActivity` (call, meeting, note, follow-up with due date).
  - Win/loss reasons and a weighted pipeline forecast.
  - A won opportunity converts to a project or a contract.
- **Intake:** an intake protocol action "create lead" alongside "create work order".
- This is separate from the operator-side `PlatformCrm` / `PlatformLeads`. Nothing is shared except UI
  components.

## Part 4 — Platform-run WhatsApp (1.5–2 weeks)

- Move the Meta credentials out of `WhatsAppSetting` (`BusinessAccountId`, `AccessToken`,
  `WebhookVerifyToken`) into platform settings, encrypted. `WhatsAppSetting` keeps only tenant
  preferences (enabled, auto-reply text) plus the `PhoneNumberId` of the tenant's dedicated number, now
  set by the platform.
- Tenant `PUT /api/whatsapp/settings` stops accepting credentials. New platform endpoints and screen:
  connect the Ecosys Meta account, provision or detach a tenant number, pause or resume a tenant, view
  per-tenant usage and number quality rating.
- One `PlatformWhatsAppSender` used by every send path: chooses the tenant's dedicated number, or falls
  back to the shared number for outbound notifications; applies the usage meter; logs every send.
- One platform webhook: route inbound messages by receiving `PhoneNumberId` to the tenant; on the shared
  number, send the auto-reply only.
- Template management moves to the platform, with a per-tenant request-and-approve flow.
- Migration: tenants who already entered their own credentials keep working until the platform moves
  their number into the Ecosys account (Meta number migration), then their stored token is deleted.
- Tests: tenant isolation on inbound routing, metering on every send path, and no credential ever
  returned by a tenant endpoint.

## Part 5 — Usage packs (~1 week)

- `tenant_usage_counters (TenantId, Meter, PeriodStart, Used)` and
  `tenant_usage_allowances (TenantId, Meter, Included, PurchasedTopUp)`.
- A guard `EnsureUsageAvailableAsync(tenantId, meter, amount)` at the points listed in section 3.
  Warn at 80% and 100%; block new usage only after the tenant goes over the allowance plus purchased packs.
- Platform screen to grant packs; tenant screen to see usage.

## Rollout

1. Ship Part 1 behind a configuration switch. Run the migration on staging from a production snapshot and
   review the per-tenant access diff.
2. Publish the new pricing page and help guides (`features/help/guides.ts`), and email tenants about the
   change and their price lock.
3. Turn on Part 1 in production. Ship Projects, then CRM, then platform WhatsApp with usage packs, each
   as its own release.

## Open decisions

1. Prices: the office seat rate and field seat discount for each edition, and the size and price of each
   usage pack.
2. Confirm the edition boundaries, especially inventory in Professional.
3. Industry packs: paid, or kept as free starter packs?
4. The minimum office seats for each edition.
5. WhatsApp: how many conversations each edition includes, and the price of an extra dedicated number.
