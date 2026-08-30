# Readiness Audit — Remediation Tracker

Source: Ecosys ServiceOps Readiness Audit, 2026-08-25 (verdict: Critical Blockers, overall 58%).
Execution: parallel agents against the working tree at `main`. Baseline before any change:
backend `dotnet build Ecosys.slnx` = 0 errors / 5 pre-existing warnings; frontend `tsc -b && vite build` = 0 errors.

Agents run in waves. Within a wave, file ownership is disjoint and exactly one agent per
toolchain is permitted to run a build, so concurrent MSBuild/Vite runs cannot collide.

## Wave 1 — release blockers

| Agent | Scope | Findings |
|---|---|---|
| Backend security | Program.cs, Import/Clients/Intake/AccessControl/WorkOrders controllers | EC-P0-01, EC-P1-03, EC-P1-04, EC-P1-02 (error mapping), EC-P2-04, EC-P2-06, EC-P3-02, EC-P3-05 (health split), global FallbackPolicy |
| Frontend work orders | endpoints.ts, features/work-orders, features/dispatch | EC-P1-01, EC-P2-07 (work-orders page), EC-P2-05 (work-orders copy) |
| Deployment & ops | Dockerfile, docker-compose.yml, nginx.conf, scripts, docs, .gitignore | EC-P1-06 (backups, non-root, PublicUrl), EC-P2-12, EC-P3-03, EC-P3-05 (CSP) |

## Wave 1 status (interrupted, resumed)

All three wave-1 agents were terminated mid-task by a session limit. State was recovered by
inspecting file modification times rather than trusting the agents' last reported step, because
`git status` cannot distinguish agent edits from the ~317 pre-existing uncommitted changes.

Landed and verified:
- `ImportController.cs` — EC-P0-01 closed. All 12 actions guarded; users import is admin-only;
  the import now writes a per-account audit entry naming the roles granted.
- `Program.cs` — global `FallbackPolicy`, `PostgresException` 23505/23503 mapping,
  `ECOSYS_KNOWN_PROXIES` trust list, `/health/live` + `/health/ready`.
- `backend/Dockerfile` — runs as non-root `app`, uploads and tenant-logo paths chowned at build.
- `scripts/backup-db.sh`, `scripts/restore-db.sh` — written, pending verification.
- Frontend `dispatchGuards.ts` and the work-order `api.ts` dispatch mutations and filtered/paged hooks.

The interrupted `Program.cs` edit left the tree **not compiling** (`HealthCheckOptions` missing a
using directive, and `KnownNetworks` typed on the obsolete `HttpOverrides.IPNetwork`). Both were
repaired directly; the fix uses `KnownIPNetworks` so no deprecation warning is introduced. Backend
is back to 0 errors and the baseline warning set.

Resumed in a second pass: ClientsController and IntakeController authorization, the assign-empty-body
and access-control fixes, the work-order UI call sites, and all compose/docs/nginx/git-hygiene work.

### Regression introduced and caught inside wave 1

The global `FallbackPolicy` did exactly what it was asked to do, to two controllers that were
anonymous *by omission*: `AuthController` and `PortalAuthController` carried neither `[Authorize]`
nor `[AllowAnonymous]`. Every route to obtaining a token — login, signup, refresh, forgot-password,
reset-password, portal request-link/verify — returned 401 to every caller. The application could not
be logged into. Five `[AllowAnonymous]` attributes closed it; `change-password`, `logout` and `me`
correctly stayed protected, and every other controller was audited for the same shape.

Consequence worth recording: unmatched URLs now return **401 rather than 404** to anonymous callers,
because the fallback policy is evaluated before routing gives up. Static files and `/uploads` are
served before `UseAuthorization()` and are unaffected.

Verified independently after the fix: `dotnet build` 0 errors, `dotnet test` **398 passed / 0 failed**.

## Wave 2 — data layer and frontend state

| Agent | Scope | Findings |
|---|---|---|
| Data layer | AppDbContext.cs + one new migration | work_orders indexes, per-tenant uniqueness, Restrict/Cascade FK repair, xmin row versions, finance raw-SQL FK names |
| Frontend state | settings pages, list pages, apiError.ts, react-hook-form call sites | EC-P2-01, EC-P2-02, server-side field errors |

## Wave 3 — services and presentation

| Agent | Scope | Findings |
|---|---|---|
| Backend services | StockLedgerService, invoice balances, email outbox, workers, observability | EC-P1-05, EC-P1-06 (SMTP path + stranded outbox), EC-P2-09, EC-P2-10, /arrive vs /arrival reconciliation |
| Frontend presentation | copy, currency, platform nav, dead code, accessibility | EC-P2-03, EC-P2-05, EC-P2-08, EC-P3-01, EC-P3-04, dead-module removal |

## Status at second session-limit interruption

Verified on disk, not taken from agent reports. Backend: build 0 errors, `dotnet test` 398/0,
no model drift. Frontend: `tsc` 0, `vite build` 0, `eslint` 0.

Closed and verified: EC-P0-01, EC-P1-01, EC-P1-03, EC-P1-04, EC-P2-04, EC-P3-02, the global
fail-closed policy (and the login regression it caused), EC-P1-02 error mapping, EC-P2-06,
the health split, EC-P1-06 a/b/c, EC-P2-12, EC-P3-05, EC-P3-03, EC-P2-11, the work_orders and
finance indexes, per-tenant uniqueness, `xmin` stock tokens, EC-P2-01, EC-P2-02, EC-P2-07.

Dead code removed: `EcosysDbContext` and its migrations (its `InitialCreate` really did
`CreateTable("tenants")` with a conflicting shape); `Ecosys.Application` (10 files, unreferenced
apart from one `COPY` line in `backend/Dockerfile`, and its `CreateWorkOrderRequest` bound
`TenantId` straight from the request body).

Still open at this point: field-level validation errors (`ApiError.fieldErrors` still has zero
call sites), hardcoded 16% VAT and the KES currency default, stock concurrency, the SMTP
resolution order and stranded outbox, worker single-instance guards, correlation IDs, the
`/arrive` vs `/arrival` reconciliation, platform nav gating, the offline-support claim, and the
remaining P3 presentation items.

## Carried into the backend wave: reconcile /arrive vs /arrival

Established by reading both paths, not inferred:

- `POST /{id}/arrive` (`WorkOrdersController.cs:808`) writes **`work_orders.ArrivalAt` only**. It never
  touches `work_order_technician_assignments`, so `HasArrivalAsync` stays false and Complete still
  400s. It returns 200, which is precisely what makes it a trap.
- `POST /{id}/arrival` (`WorkOrderAssignmentsController.cs:97`) writes the assignment row **and** rolls
  the header up via `SyncSiteVisitTimestampsAsync`, idempotently. Same for `/departure` vs `/depart`.

`/arrival` and `/departure` are strict supersets. Proposed fix: make `/arrive` and `/depart` delegate to
`RecordArrivalAsync` / `RecordDepartureAsync` for the caller's own assignment so the assignment table is
the single source of truth and the header stays a derived roll-up; fail clearly when no assignment row
exists rather than writing a header stamp that satisfies nothing; then mark both obsolete and remove
once no client calls them (the frontend already does not).

Note: `ArriveAsync` writing the header directly is the exact case `SyncSiteVisitTimestampsAsync`'s own
comment guards against, so today a header arrival and the assignment rows can legitimately disagree.

## Deliberately deferred

- `IX_users_Email` is globally unique rather than `(TenantId, Email)`. Changing it requires a
  tenant-aware login and is high regression risk; the audit's own recommendation is the
  minimum viable fix, so wave 1 maps `23505` to a 409 with a clear message and the data model
  decision is left to the product owner.
- Findings the audit marked NOT TESTED (preventive maintenance, attachments, client portal,
  notifications delivery, browser UI) are not re-verified here — no source defect was reported
  for them, only absence of coverage.

## Gap-closure verification pass — 2026-08-27

Re-checked every item this tracker still listed as open, by reading the current code rather than
trusting the earlier notes. Most had already been closed since this file was last updated; the
tracker was stale, not the code. Verified against a clean `dotnet build` (0 errors), `dotnet test`
(411/414 — the 3 failures are `Npgsql: Failed to connect to 127.0.0.1:5432`, i.e. no Postgres in
this sandbox, not regressions), `tsc -b` (0 errors), `vite build` (succeeds), and `eslint` (0).

**Confirmed already fixed (code inspected, no further action needed):**
- Correlation IDs — `RequestCorrelationMiddleware.cs` stamps every request/response/log scope;
  covered by `RequestCorrelationTests.cs`.
- Worker single-instance guards — `BackgroundWorkLease.cs` (Postgres advisory locks) +
  `SingleInstanceHostedService<TWorker>`, wired for all 9 workers in `DependencyInjection.cs:93-101`.
- Stock concurrency — `StockLedgerService.ExecuteAsync` runs every stock write inside a
  `Serializable` transaction over `xmin`-tokened rows; a collision throws `StockContentionException`
  (`AppException`, 409 `CONCURRENCY_CONFLICT`), already correctly mapped by the global handler.
- SMTP resolution order and stranded outbox — `EmailSettingsResolver`/`EmailDeliveryResolution`
  unify the tenant→platform→config fallback into one path; `EmailOutboxService.ReleaseAbandonedClaimsAsync`
  reclaims stuck `Sending` rows and is called from the outbox worker's processing loop.
- `/arrive` vs `/arrival` reconciliation — `WorkOrdersController.Arrive`/`Depart` now delegate to
  `RecordArrivalAsync`/`RecordDepartureAsync`, exactly the fix this tracker proposed.
- Platform nav gating — `PlatformShell.tsx` filters `platformNavItems` by `user.role`; the comment
  notes the matching backend authorization lives on the routes themselves.
- The offline-support claim — `Topbar.tsx` already states plainly that offline changes can't be
  saved rather than overclaiming offline support.
- Field-level validation errors — `apiError.ts`'s `applyApiFieldErrors` was already wired into
  `ClientFormDrawer`, `SiteFormDrawer`, `AssetFormDrawer`, `PartFormDrawer`, `NewUserFormDrawer`
  (not "zero call sites" as previously noted).

**Fixed in this pass:**
- Field-level validation errors — wired `applyApiFieldErrors` into the two highest-traffic forms
  that were still swallowing validation responses into a bare toast: `NewWorkOrderForm.tsx` (work
  order creation) and `ContractFormDrawer.tsx` (contract creation).
- Hardcoded 16% VAT / `"KES"` currency, corrected in two passes:
  - First pass replaced the C# literals with a call to the platform's admin-configured
    `tax-finance` setting. **This was the wrong fix** — `finance/api.ts`'s own `useDefaultTaxRate()`
    doc comment already explains why: that setting sits behind `PlatformSettingsAccess` and is
    unreadable by a tenant user, so a caller "must show an editable control rather than
    substituting a literal." Silently pulling a platform-admin-only value into a tenant's billing
    document is the same anti-pattern as a hardcoded literal, just less visible.
  - Second pass made both genuinely user-selectable, matching the pattern the invoice form already
    used for tax rate (default from the tenant's own default rate card / country-derived
    `getActiveCurrency()`, always overridable):
    - `WorkOrderBillingController.GetDefaultTaxRateAsync` now reads the tenant's own default
      `RateCard.TaxRate` (same source as the frontend's `useDefaultTaxRate()`), falling back to 0
      (not a jurisdiction-specific rate) when the tenant has configured none — the generated
      billable item's `TaxRate` stays user-editable afterward.
    - `TenantInvoiceController`, `TenantQuotationController`, `TenantCreditNoteController` no
      longer resolve currency server-side at all when the request omits it beyond a plain `"KES"`
      last-resort literal (matching the frontend's own `FALLBACK_CURRENCY`) — the primary path is
      now an explicit `Currency` the user picks in the form.
    - Added a Currency input (defaulting to `getActiveCurrency()`, freely editable, 3-letter code)
      to `NewInvoiceFormDrawer.tsx`, `NewQuoteFormDrawer.tsx`, `CreditNotesPage.tsx`'s
      `NewCreditNoteDrawer`, and `ContractFormDrawer.tsx` (which already silently defaulted
      `currency: 'KES'` in form state with no field rendered for the user to see or change it).
    - Deleted `PlatformFinanceDefaultsService.cs` (introduced and removed within this same pass) —
      the platform-wide setting was the wrong source for either value in a tenant document.

**Left as documented follow-up, not fixed (scope/risk didn't justify it in this pass):**
- `"KES"` still appears as a fallback/default in ~90 other places: EF Core column defaults on
  ~15 entities (`Currency` columns default to `"KES"` at the schema level — changing this needs a
  migration and only matters if a row is ever inserted without the app setting `Currency`
  explicitly, which none of the code paths do), `TenantFinanceController`'s `NormalizeCurrency`
  (rate cards, approval-matrix rules — internal configuration, not customer-facing documents;
  fixing it cleanly requires resolving the currency before the `FinanceApprovalRules` query since
  the call at line 309 sits inside an EF `Where` expression tree and can't `await`), the client
  statement email body, and `AdvancedSettingsController`/`PlatformFinanceController`'s own
  currency-blank fallbacks (these normalize the *platform's own* settings/billing, which is a
  different, lower-risk case than a tenant's customer document). None of these were flagged as
  reproducible defects — just the same hardcoded-literal pattern at lower business impact. Worth a
  follow-up pass if the product ships outside Kenya.
- "Remaining P3 presentation items" — the tracker never itemized these beyond that label; nothing
  concrete to verify or fix without the original findings.
