# Licensing Remodel Plan

Status: agreed · 2026-10-24

**Builds on `claude/great-wozniak-22dw4x`** (not yet merged into `main`), which already contains:
Projects Releases 1–2 (`docs/PROJECTS_MODULE_DESIGN.md`) and the shared-platform Release 3
(`docs/PLATFORM_RELEASE3_DESIGN.md`): notification inbox, Calendar & Activities with minutes and action
items, WhatsApp reminders from the shared Ecosys number (`docs/WHATSAPP_SETUP.md`), and role dashboards.
That branch licenses Projects as a `projects` module listed on any plan and WhatsApp as a
`TenantLicense.WhatsAppEnabled` flag. This plan replaces both with editions and the WhatsApp add-on.

## Summary

Replace module licensing (Operations / Finance / Suite) with **editions × seat types + usage packs**:

- **Three editions** — Essentials, Professional, Enterprise. Every edition runs the complete loop
  (win work → do it → bill it); higher editions add depth, control and scale.
- **Two paid seat types** — Office and Field (technician), with free Viewer/Auditor and portal users.
- **Usage packs** only where Ecosys carries a real marginal cost (AI, storage).
- **CRM and Projects** are built into the editions, not sold as add-ons.
- **WhatsApp** is the one optional add-on, run and paid for by the platform.

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
| CRM — lead capture, lead → client, quotation follow-up | ✓ | ✓ | ✓ |
| CRM — pipeline board, stages, owners, activities, forecast | | ✓ | ✓ |
| Projects — milestones, tasks, variations, linked work orders and materials, site diary, muster roll, budget and costs, valuations, retention, client portal (built) | | ✓ | ✓ |
| Preventive maintenance, PM templates, assets, asset categories, systems | | ✓ | ✓ |
| Contracts, SLA policies, escalation rules | | ✓ | ✓ |
| Inventory — materials, vans, purchase orders, parts, stock transfers | | ✓ | ✓ |
| Workflows, intake rules/protocols, email intake | | ✓ | ✓ |
| Time & attendance, CSAT | | ✓ | ✓ |
| Expenses & payables, credit control, approvals, scheduled reports | | ✓ | ✓ |
| Multiple branches | | | ✓ |
| API keys & webhooks, monitoring integration | | | ✓ |
| Approval matrix, custom report templates, advanced roles | | | ✓ |
| Cross-branch / cross-project profitability reporting | | | ✓ |

WhatsApp is not in any edition. It is an optional add-on available on every edition (§4).

Inventory and preventive maintenance/assets sit entirely in Professional. Essentials has no light version
of either: stock control and planned maintenance are what a firm upgrades for. Revisit this after the first
ten Essentials sales.

## 2. Seats

| Seat | Roles (`AppRoles`) | Price |
|---|---|---|
| Office | `Admin`, `Manager`, `Dispatcher`, `finance` (FinanceUser), `MaterialsController` | Full rate |
| Field | `Technician` | ~40–50% of the office rate |
| Free | `Viewer` (incl. legacy `User`), `Auditor`; client-portal contacts | Unlimited |

- Each edition has a minimum number of office seats: Essentials 2, Professional 3, Enterprise 10. These
  are the starting values and the platform can change them (§6).
- Seats are counted as they are today: active users, pending invitees included. Deactivated users are not counted.
- A role change re-checks seats: promoting a technician to dispatcher takes an office seat.
- Sales staff use office seats. There is no separate CRM seat type.

## 3. Usage packs

Each edition includes a monthly allowance. Top-ups are sold in packs. There is no hard stop mid-job:
going over the allowance raises a warning first, then blocks only *new* sends or uploads.

| Meter | Measured at | Note |
|---|---|---|
| Kip AI queries | `KipController.Query` | Kip is currently a stub, so the meter ships with Kip. |
| Storage (GB) | `FileStorageService` upload | Photos, reports, attachments |
| Email-intake mailboxes | Email intake settings | Count of connected mailboxes |

## 4. WhatsApp — optional add-on, run by the platform

WhatsApp is the only add-on in the model. It is off by default and a tenant can turn it on with any
edition. Tenants that don't take it pay nothing for it and see no WhatsApp screens or settings.

| Add-on | What the tenant gets | Billing |
|---|---|---|
| **WhatsApp Notifications** | Outbound messages from the shared Ecosys number | Monthly fee + messages beyond the included bundle |
| **WhatsApp Business** | A dedicated number, two-way inbox, auto-reply, WhatsApp intake. Includes Notifications. | Higher monthly fee + messages beyond the included bundle |

Tenants no longer connect their own Meta account. Ecosys owns the WhatsApp Business setup, holds the
credentials and pays Meta. A tenant's add-on fee and message usage cover that cost.

- **One Meta Business account, owned by Ecosys.** Ecosys registers as a Meta Tech Provider. The access
  token, app secret and webhook verify token stay environment secrets, as built, and are never shown to
  tenants.
- **Shared Ecosys number (Notifications):** already built on `great-wozniak` (see Part 3). Outbound notifications only — job booked, technician on the
  way, report ready, invoice sent. Messages carry the tenant's name in an approved template. Replies to
  this number get an auto-reply pointing the client to the tenant's contact details, because a shared
  number cannot reliably route an inbound message to the right tenant.
- **Dedicated number per tenant (Business):** Ecosys provisions a number for the tenant under its
  own Meta account, with the tenant's display name. Inbound messages route by number to the tenant's
  inbox, so the two-way inbox, auto-reply and intake from WhatsApp all work. Each extra number costs
  more.
- **Templates:** Ecosys owns a standard approved template set. A tenant can request a custom template;
  the platform submits it to Meta and approves it on the tenant's behalf.
- **Control:** the platform can pause sending for a tenant (non-payment, spam complaints, quality rating
  drop) without touching the rest of the account.
- **Metering:** messages are counted per tenant against the bundle that comes with the add-on (see Part 3).
  Usage packs (§3) do not cover WhatsApp.
- **Turning it off:** sending stops, the dedicated number is released after 30 days, and the
  conversation history stays read-only.

## 5. Extras outside entitlements

- **Industry packs** (solar installation, HVAC, generators, electrical): checklists, report templates and
  asset categories. They are **free** and build on the existing starter packs. They never gate an API
  route.
- **Paid onboarding and data migration.**

## 6. Commercial terms

- Billed monthly or annually. Annual prepay gets about 2 months free (`BillingCycles` already supports this).
- Trial is Professional for 14 days. When it ends, the tenant picks an edition. The existing grace period
  and read-only mode cover the gap.
- Prices are set in KES, with a USD price list. Tenants already have a `Currency`.

### Every number is adjustable

No price, discount, allowance or minimum is hard-coded. All of them are edited on the platform
**Plans & pricing** screen, and each can be overridden for a single tenant (a negotiated deal) on that
tenant's license.

| Setting | Where | Can be overridden per tenant |
|---|---|---|
| Office seat price and field seat price (monthly, annual) for each edition | Edition | ✓ |
| Minimum office seats for each edition | Edition | ✓ |
| Annual prepay discount | Platform | ✓ |
| Usage pack size and price, and the allowance included in each edition | Usage pack / edition | ✓ |
| WhatsApp add-on monthly and annual fee, included messages, price per extra message, extra number price | Add-on | ✓ |

A price change applies to new licenses and at each tenant's next renewal. It never changes a tenant's
current term.

Starting values for WhatsApp, seeded by the migration and editable afterwards. Check them against Meta's
current rate card for Kenya before the first sale:

| | Notifications | Business |
|---|---|---|
| Monthly fee | KES 1,500 | KES 4,500 |
| Messages included per month | 500 | 1,500 |
| Price per extra message | KES 3 | KES 3 |
| Extra dedicated number | — | KES 2,500 / month |

Edition seat prices and usage packs are seeded at 0 and marked **not priced**. The platform can't assign
an edition or sell a pack until it has a price, so a tenant can never be put on something free by
mistake.

---

# Implementation

Parts 0–4, each shippable on its own. **Projects needs no build work of its own**: it is already
built, and Part 1 moves it into Professional.

| Part | What | Effort (one developer) |
|---|---|---|
| 0 | Merge `claude/great-wozniak-22dw4x` into `main` | Review and QA of that branch |
| 1 | Licensing core, including Projects and Release 3 gating | 2.5–3.5 weeks |
| 2 | CRM, reusing Activities and action items | 2–3 weeks |
| 3 | WhatsApp add-on (Notifications built; pricing, metering, Business) | 1–1.5 weeks |
| 4 | Usage packs | ~1 week |

About 7–9 developer-weeks in total.

## Part 0 — Merge the Projects branch first

`great-wozniak` rewrites `ServiceOpsPlans.cs`, `LicenseAndSecurityServices.cs`, `entitlements.ts` and
`navigation.ts`, the same files Part 1 replaces. Building Part 1 on `main` first would mean a large
conflict later and Projects ungated or wrongly gated. Merge that branch, then start Part 1 from the
merged `main`.

## Part 1 — Licensing core (2.5–3.5 weeks)

Includes the add-on mechanism that WhatsApp needs, so Part 3 does not have to build it.

### 1.1 Feature keys replace modules

- Replace the `operations` / `finance` / `projects` module sets in
  `backend/src/Ecosys.Infrastructure/Services/ServiceOpsPlans.cs` with a `LicenseFeatures` catalogue
  (e.g. `work_orders`, `maintenance`, `inventory`, `contracts_sla`, `workflows`, `time_attendance`,
  `expenses`, `approvals`, `crm_basic`, `crm_pipeline`, `projects`, `multi_branch`, `api_webhooks`,
  `monitoring`, `approval_matrix`, `custom_reports`, `whatsapp_notifications`, `whatsapp_business`).
- Tag every tenant controller with exactly one feature key, or mark it shared. Keep the rule enforced by
  the classification check in `TenantLifecycleConsistencyTests`: an unclassified controller fails the build.
- An edition is a fixed set of feature keys, defined in code (`Editions.cs`) so the boundaries are
  reviewable in a pull request. `LicensePlan` stores only commercial data: code, name and prices.
- Per-tenant overrides become `tenant_license_feature_overrides (TenantLicenseId, FeatureKey, Enabled)`.
  This replaces `MaxBranchesOverride`, `MonitoringIntegrationOverride` and the unused `EmailIngestionOverride`.
- `TenantLicenseEnforcementMiddleware` checks `snapshot.Features` instead of `snapshot.Modules`. Remove
  the route-classification special cases where a single feature key now covers the route.

### 1.2 Projects and Release 3 under editions

- **Projects:** the controllers `Projects`, `ProjectSettings`, `Consultants`, `ProjectSite`,
  `ProjectCommercial` and `PortalProjects` all take the `projects` feature (Professional+). The
  `[Projects, Finance]` and `[Projects, Operations]` route rules in `RequiredModules` go away: every tab
  they protect (commercial, valuations, invoices, award from quotation, work orders, material issues,
  handover to a maintenance contract) is inside Professional anyway.
- `WithholdingCertificates` (finance-wide) joins the base invoicing feature, so it is in every edition.
- The Projects tab rule (`docs/PROJECTS_MODULE_DESIGN.md` §12: a tab shows only when its toggle is on
  **and** the tenant holds the module it needs) changes to check feature keys. The `TenantModuleSetting`
  toggles for `projects` stay as they are.
- The Projects design decision D3 ("sold on its own or with Operations/Finance") is superseded by this
  plan. Note this in that document when Part 1 merges.
- **Release 3:** the role-dashboard widget catalogue and the calendar sources declare the module(s) they
  need. Each declaration changes to a feature key, so a widget or calendar item only appears when the
  edition has its feature. Notifications, Calendar, Activities and action items stay shared (every
  edition).

### 1.3 Data model

- `license_plans`: rows `essentials`, `professional`, `enterprise`. Prices: monthly/annual for office
  seats, monthly/annual for field seats, and minimum office seats. All editable on the platform.
- Per-tenant pricing overrides on `TenantLicense`: `OfficeSeatPriceOverride`, `FieldSeatPriceOverride`,
  `MinOfficeSeatsOverride`, `AnnualDiscountOverride` (all nullable; null means use the edition's value).
- `TenantLicense.LicensedUsers` becomes `OfficeSeats` + `FieldSeats`. Both have a CHECK constraint, like
  `CK_tenant_licenses_LicensedUsers_Positive`: office seats > 0, field seats ≥ 0.
- Add-ons: `license_add_ons (Code, Name, MonthlyPrice, AnnualPrice, IncludedMessages, ExtraMessagePrice,
  ExtraNumberPrice, IsActive)` and `tenant_license_add_ons (TenantLicenseId, AddOnCode, StartedAt, EndedAt,
  PriceOverride, IncludedMessagesOverride, ExtraMessagePriceOverride)`. A tenant's features are its
  edition's features plus its active add-ons' features, minus its overrides. The only add-ons at launch
  are `whatsapp_notifications` and `whatsapp_business`.
- `TenantLicense.WhatsAppEnabled` (from `great-wozniak`) is replaced by the `whatsapp_notifications`
  add-on.
- Drop `LicensePlan.ModulesIncluded`, `EmailIngestion`, `MonitoringIntegration` and `MaxBranches`.

### 1.4 Seat enforcement

- `LicenseGuardService.CountSeatsUsedAsync` returns `(office, field)`, grouping active users by role into
  a seat type. One helper `SeatTypes.For(role)` holds the mapping.
- `EnsureCanAddUsersAsync(tenantId, role, count)` checks the matching pool and keeps the existing
  row lock on the license.
- Call sites to update: tenant user create/reactivate, platform add-user, CSV import, and **role change**
  (new; today a role change never touches seats).
- Project site workers on the muster roll are not users and take no seat.
- Error codes: `license_office_seats_reached`, `license_field_seats_reached`.

### 1.5 Migration of existing tenants

One SQL migration, following the pattern of `RetireLegacyPlansAndRequireLicensedUsers`:

1. Insert the three edition plans and the two WhatsApp add-ons.
2. Move every license on `operations`, `finance` or `suite` to `professional`, including any with
   `projects` in `ModulesIncluded`. No tenant loses access. Tenants already using multiple branches,
   monitoring or API keys move to `enterprise` instead.
3. Split seats: `FieldSeats` = active technicians; `OfficeSeats` = max(edition minimum,
   `LicensedUsers` − field seats, active office users). No existing user may end up over a limit.
4. Convert existing overrides into `tenant_license_feature_overrides`.
5. WhatsApp: `WhatsAppEnabled = true` → `whatsapp_notifications`. A tenant with the older inbox
   (`WhatsAppSetting.IsEnabled`) → `whatsapp_business`. Both are free until the tenant's renewal.
6. Delete the old plans, and drop the old columns in a follow-up migration once the release is stable.

Current prices are honoured until each tenant's next renewal. Record each tenant's price-lock end date in
`Notes`, or add a `PriceLockedUntil` column.

Dry-run the migration against a production snapshot and diff the access for each tenant before running it.

### 1.6 Frontend

- `ecosys-serviceops-frontend/src/shared/utils/entitlements.ts`: map path prefixes to feature keys
  instead of module lists (including the Projects paths added on `great-wozniak`).
- `useEntitlements`: expose `hasFeature`, `edition`, `addOns`, `officeSeats`/`fieldSeats` and their usage.
- `navigation.ts`: hide features the edition lacks, or show them locked with an upgrade prompt.
- Projects tabs, dashboard widgets and calendar sources: switch their module checks to `hasFeature`.
- `features/team` (users page, new-user drawer, role change): show seat usage for each pool and the
  seat-limit errors.
- Platform screens (`PlansSettingsPage`, `LicensingPage`, `TenantDetailPage`, `LicenseTermDialog`,
  `licensedUsers.ts`): edit edition and add-on prices, set office/field seats, turn add-ons on, set
  feature and price overrides.
- Update MRR snapshots and platform billing (`MrrSnapshotWorker`, `PlatformBillingLifecycleService`) to
  calculate price as seats × rate for each seat type, plus add-ons.

### 1.7 Tests

- Rewrite `ServiceOpsPlanTests` around editions and feature keys, with one test per edition boundary.
- Projects: the Release 1 acceptance test "a tenant without the `projects` module gets a 403 on every
  Projects endpoint" becomes "an Essentials tenant gets a 403". Remove tests for the
  `[Projects, Finance]` / `[Projects, Operations]` combinations.
- Release 3: every dashboard widget and calendar source is hidden without its feature.
- Rewrite `TenantLicensedUserLimitTests` for two seat pools, including a role change between pools.
- Add a migration test: tenants on the old plans end up with access equal to or greater than before.
- Update the e2e tests `serviceops-plans.spec.ts` and `platform-tenant-management.spec.ts`.

## Part 2 — CRM (2–3 weeks)

Feature keys: `crm_basic` (all editions), `crm_pipeline` (Professional+).

Reuse what Release 3 built rather than adding CRM copies of it: **Activities** (calls, meetings, site
visits) with minutes, **action items** (follow-ups with an owner and a due date), **My calendar**, and the
**notification dispatcher**. Release 3 activities link to a project, client, contract or work order;
add lead and opportunity as link targets.

- **crm_basic:**
  - `Lead` entity (source: web form / email intake / WhatsApp with the Business add-on / manual; contact
    details; status; owner).
  - Convert a lead to a `Client`.
  - Quotation follow-up view based on `TenantQuotation.Status` (Draft → Sent → Accepted/Rejected/Expired),
    with action items to chase quotes.
- **crm_pipeline:**
  - `Opportunity` (client or lead, value, expected close date, owner).
  - Configurable `PipelineStage` list, shown as a Kanban board.
  - Win/loss reasons and a weighted pipeline forecast.
  - A won opportunity → quotation → project, using the existing **award from quotation** in Projects;
    or → a maintenance contract.
  - CRM dashboard widgets added to the Release 3 widget catalogue.
- **Intake:** an intake protocol action "create lead" alongside "create work order".
- This is separate from the operator-side `PlatformCrm` / `PlatformLeads`. Nothing is shared except UI
  components.

## Part 3 — WhatsApp add-on (1–1.5 weeks)

**Already built on `great-wozniak`:** the Meta Cloud API sender behind `IWhatsAppSender`; the Ecosys
account in environment secrets; a resolver that picks the sending account (the Ecosys account for every
tenant today); `WhatsAppOutboxMessage` per tenant with retries; a monthly cap per tenant (default 1,000,
then email only); STOP/START handling; Ecosys-owned templates; client consent; the webhook; and the
tenant WhatsApp log. Gated today by `TenantLicense.WhatsAppEnabled`.

Remaining work:

- **Notifications add-on:** the reminder controllers (`MyWhatsApp`, `WhatsAppNotifications`,
  `ClientWhatsAppConsent`, `PortalWhatsApp`) take the `whatsapp_notifications` feature instead of the flag.
  The tenant screen offers to turn the add-on on and shows its price.
- **Metering and billing:** count outbox rows per month against the add-on's `IncludedMessages`. Messages
  beyond the bundle are billed in arrears on the next invoice instead of being skipped. The existing
  per-tenant cap becomes an optional hard cap the platform sets.
- **Business add-on:** the resolver returns the tenant's own number when it has one; the webhook routes
  inbound messages by receiving `PhoneNumberId` to that tenant; the older two-way inbox
  (`WhatsAppController`, currently in Operations) moves to `whatsapp_business`. Platform screen to
  provision or detach a number and pause or resume a tenant.
- **Old tenant credentials:** `WhatsAppSetting` stops accepting `BusinessAccountId`, `AccessToken` and
  `WebhookVerifyToken` from tenants. Tenants who entered their own keep working until the platform moves
  their number into the Ecosys account (Meta number migration), then their stored token is deleted.
- Tests: tenant isolation on inbound routing, metering on every send path, overage on the invoice, and no
  credential ever returned by a tenant endpoint.

## Part 4 — Usage packs (~1 week)

- `tenant_usage_counters (TenantId, Meter, PeriodStart, Used)` and
  `tenant_usage_allowances (TenantId, Meter, Included, PurchasedTopUp)`.
- A guard `EnsureUsageAvailableAsync(tenantId, meter, amount)` at the points listed in section 3.
  Warn at 80% and 100%; block new usage only after the tenant goes over the allowance plus purchased packs.
- Platform screen to grant packs; tenant screen to see usage.

## Rollout

1. Merge `great-wozniak` (Part 0).
2. Ship Part 1 behind a configuration switch. Run the migration on staging from a production snapshot and
   review the per-tenant access diff.
3. Publish the new pricing page and help guides (`features/help/guides.ts`), and email tenants about the
   change and their price lock.
4. Turn on Part 1 in production. Then ship CRM, the WhatsApp add-on and usage packs, each as its own
   release.

## Decisions

| # | Decision | Outcome |
|---|---|---|
| 1 | Seat prices, field seat discount, usage packs | Adjustable on the platform, with per-tenant overrides (§6). |
| 2 | Edition boundaries | As in §1. Inventory and PM/assets stay entirely in Professional. |
| 3 | Industry packs | Free. |
| 4 | Minimum office seats | Essentials 2, Professional 3, Enterprise 10, adjustable. |
| 5 | WhatsApp add-on prices | Seeded starting values (§6), adjustable, with per-tenant overrides. |

All decisions are made. Part 1 can start.
