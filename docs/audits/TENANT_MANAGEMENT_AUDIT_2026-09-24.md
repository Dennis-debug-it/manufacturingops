# Ecosys ServiceOps — Tenant Management Audit

**Date:** 2026-09-24 · **Scope:** tenant lifecycle, licensing, seats, modules, invitations/email, settings ownership, isolation, platform tenant UI, tests
**Codebase state audited:** `main` @ `a92b4a1` **plus the uncommitted email-identity work in the working tree** (13 modified files, `AddManagedEmailProvisioningStatus` migration, `TenantEmailIdentityTests.cs`). Findings about email refer to the working tree.
**Production behaviour changed:** none. One new test file added: `backend/tests/Ecosys.Tests/TenantManagementAuditTests.cs`.

---

## 1. Executive summary

**Verdict: mostly functional, with significant gaps. Not production-ready for self-serve commercial SaaS.**

The creation → invitation → first login → daily use path works, and the core building blocks are sound:
- Tenant creation is transactional. Email failure does not destroy the tenant.
- Per-request license enforcement reads the DB, with no stale caches.
- Tenant data isolation relies on global EF query filters and held up in every path I checked.
- A seat-limit mechanism already exists.

The problems are in **how many places own the same fact** and in **authorization of platform lifecycle actions**:

| # | Question | Answer |
|---|---|---|
| 1 | Coherent tenant architecture? | **No.** One entity model, but four controllers and three background writers each change a *different subset* of the lifecycle fields. |
| 2 | One source of truth for tenant status? | **No.** `Tenant.Status`, `Tenant.IsActive`, `Tenant.LicenseStatus` and `TenantLicense.Status` are four fields. Enforcement reads two of them; the platform UI reads the other two. |
| 3 | One source of truth for licensing? | **Mostly.** `TenantLicense` + `LicensePlan` is the only enforced model. `Tenant.PlanName/MaxUsers/MaxBranches/TrialEndsAt/SubscriptionEndsAt/LicenseStatus` are denormalised copies that drift. |
| 4 | Modules enforced on the backend? | **Yes, for Operations/Finance** (middleware, allow-by-default for unclassified controllers). The **AdvancedReports and ClientPortal flags are never enforced**. |
| 5 | Max users configurable? | **API only.** `TenantLicense.MaxUsersOverride` can be set via `POST/PUT /api/platform/tenants`, but **no UI sends it**. Commercial plans default to unlimited. |
| 6 | Max users enforced? | **Yes on 4 of 5 creation paths** (`LicenseGuardService.EnsureCanCreateUserAsync`). **CSV bulk import bypasses it.** The platform add-user path can race. |
| 7 | Tenant/platform settings separated? | **Largely yes** after prior clean-up. Leftover orphan platform endpoints remain (§10). |
| 8 | Suspension/reactivation reliable? | **No.** Proven defect reproduces the production "license is suspended after reactivation" report (§7). |
| 9 | Onboarding end-to-end? | **Works** when mail delivery works. The platform UI cannot show invitation status after creation. |
| 10 | Email invitations reliable? | **No.** In the working tree, tenant mail is still sent *From* the unprovisioned `{slug}@serviceops.ecosysdigital.co.ke`, even while its status is `Pending` or `Rejected`. |
| 11 | Tenant isolation secure? | **Yes for tenant↔tenant.** **No for platform role separation:** read-only/support/finance platform roles can suspend, deactivate, activate and renew tenants. |
| 12 | Duplicate/obsolete implementations? | **Yes.** Three license-status controllers, two tenant-creation paths, two frontend tenant API clients, two orphan backend projects. |
| 13 | All UI controls backed by working behaviour? | **No.** Impersonate, "Activate tenant" (Account tab), "Delete tenant" and "Verify SMTP" are broken or misleading (§12). |
| 14 | Tests prove business behaviour? | **Partly.** The lifecycle tests are good. Every platform-tenant controller test injects a no-op license guard, so seats/limits were never exercised there. The provisioning Playwright spec is fully mocked. |
| 15 | UX production-suitable? | **Not yet.** The detail page has placeholder text, duplicated lifecycle buttons across tabs, mojibake (`â€”`), and no seats-used/limit view. |

**Proven by failing audit tests (9):** P0-01 authorization, P1-01 suspension incident, P1-02 badge drift, P1-03 extend-trial downgrade, P1-04 PATCH Trial lockout, P1-05 payment on TrialExpired, P1-06 unlimited seats.

---

## 2. Architecture map

```
FRONTEND (ecosys-serviceops-frontend/src)
  platform/tenants/TenantsPage.tsx ──┐   (imports hooks from ../api — platform/api.ts)
  platform/tenants/TenantDetailPage.tsx ─┤
  platform/tenants/LicenseTermDialog.tsx │
  platform/tenants/NewWorkspaceUserDrawer│
  platform/LicensingPage.tsx ────────────┤  platform/api.ts  (ACTIVE client)
  platform/tenants/api.ts  ── DUPLICATE, partially unused client (useTenants/useCreateTenant/... redefined)
  shared/hooks/useEntitlements.ts ──> GET /api/tenant/license  (module gating in tenant UI)
  features/settings/AccountSettingsPage ──> GET /api/settings/license
  shared/api/attachInterceptors.ts + licenseWarningStore.ts  (X-License-Warning → one banner)
  shared/api/sessionStore.ts: openImpersonationTab()  ── NO CALLERS
          │
          ▼
API (backend/src/Ecosys.Api/Controllers)
  PlatformTenantsController          /api/platform/tenants/**            (1,897 lines; lifecycle, users, create, impersonate)
  PlatformTenantLicensesController   /api/platform/tenant-licenses/**    (legacy; no UI caller)
  PlatformLicensingController        /api/platform/licensing/**          (plans + "subscriptions" = TenantLicense rows)
  PlatformTenantCommunicationSettingsController /api/platform/tenants/{id}/email-settings|notification-settings
  PlatformSupportToolsController     /api/platform/support/tenants/{id}/**
  TenantLicenseController            /api/tenant/license                  (tenant read)
  UsersController / ImportController /api/users, /api/import/users/commit (tenant user creation)
  AuthController                     /api/auth/signup  (ANONYMOUS tenant creation — no UI caller)
          │
          ▼
SERVICES (Ecosys.Infrastructure/Services)
  LicenseGuardService (ILicenseGuardService)  ← single enforcement service: snapshot, usage, limits, modules
  TenantLicenseEnforcementMiddleware          ← every authenticated tenant /api/* request
  ServiceOpsPlans                              ← module ↔ controller map
  MvpAuthService                               ← login, signup, impersonation token
  UserCredentialDeliveryService / PasswordLinkTokens ← invitations, resets
  TenantEmailIdentity / SmtpEmailSender / EmailOutboxService
  PlatformBillingLifecycleService              ← payment → license extension
          │
          ▼
DOMAIN (Ecosys.Domain/Entities)            DATABASE (PostgreSQL)
  Tenant ─1:1─ TenantLicense ─N:1─ LicensePlan     tenants, tenant_licenses (unique TenantId), license_plans
  Tenant ─1:N─ User (global unique Email)          users
  Tenant ─1:1─ EmailSetting (managed identity)     email_settings
  TenantModuleSetting (settings JSON, NOT entitlements)

BACKGROUND WORKERS (run once at every API start, then every 24h)
  TrialLifecycleWorker          Trial → TrialExpiringSoon (7d) → TrialExpired
  SubscriptionLifecycleWorker   Active + past grace → Suspended  (does NOT touch Tenant.Status/IsActive)
  EmailOutbox dispatcher

ORPHANS (in Ecosys.slnx, not referenced by Ecosys.Api)
  src/Ecosys.Platform  (second Tenant, EmailSetting, AppUser, TenantSetting entities + contracts)
  src/Ecosys.ServiceOps
```

### Entity field inventory (tenant lifecycle–relevant)

| Entity.Field | Written by | Read by enforcement? | Notes |
|---|---|---|---|
| `Tenant.Status` (Active/Trial/Suspended/Inactive) | PlatformTenants (Apply, SetStatus, Renew, ExtendTrial), PlatformTenantLicenses | **No** | Drives all platform UI buttons and badges. Not written by workers, payment, or the licensing controller. |
| `Tenant.IsActive` | PlatformTenants, PlatformTenantLicenses, PlatformLicensing (status actions) | **Yes** (middleware, login, impersonation) | Not written by workers or payment. |
| `Tenant.LicenseStatus` | PlatformTenants, both workers, payment, PlatformLicensing (status actions) | **No** | The platform UI **prefers it over** `TenantLicense.Status` (`ResolveLicenseStatus`). Not written by PlatformTenantLicenses. |
| `TenantLicense.Status` | All of the above | **Yes** (suspension, read-only) | The real entitlement state. |
| `Tenant.PlanName` | PlatformTenants | No | Copy of `LicensePlan.DisplayName`. `PlatformLicensing.UpdateSubscription` changes the plan without updating it. |
| `Tenant.MaxUsers / MaxBranches` | PlatformTenants.ApplyTenant | **No** | Displayed as "Max users" on the platform detail page. Enforcement uses `TenantLicense.MaxUsersOverride ?? LicensePlan.MaxUsers`. |
| `TenantLicense.MaxUsersOverride` | PlatformTenants.ApplyTenant only | **Yes** | The real per-tenant seat limit. No UI sets it. |
| `LicensePlan.MaxUsers` | PlatformLicensing plans | **Yes** (fallback) | `null` for operations/finance/suite (migration `20260905061718`). |
| `Tenant.TrialEndsAt` vs `TenantLicense.TrialEndsAt/ExpiresAt` | both | License only | Duplicated. |
| `Tenant.SubscriptionStartsAt/EndsAt` vs `TenantLicense.StartsAt/ExpiresAt` | both | License only | Duplicated. `PlatformLicensing` and payment don't sync the Tenant copy. |
| `LicensePlan.MaxAssets / MonthlyWorkOrders` | plans | **Never** | `EnsureCanCreateAssetAsync` and `EnsureCanCreateWorkOrderAsync` have zero callers. |
| `LicensePlan.AdvancedReports / ClientPortal` (+ overrides) | plans | **Never** | Only `EmailIngestion` and `MonitoringIntegration` are checked, and only by `IntakeController`. |
| `TenantLicense.BillingCycle / NextBillingDate` | lifecycle actions | No | Informational. No auto-renew or payment collection exists. |
| `User.IsActive / MustChangePassword / InviteToken*` | users, invitations | Yes | `InviteTokenHash/ExpiresAt` look superseded by `PasswordResetTokens` (`PasswordLinkTokens`). **Verify before removal.** |

---

## 3. Tenant lifecycle

### 3.1 Actual lifecycle (as implemented)

```
                    POST /api/platform/tenants (UI)          POST /api/auth/signup (anonymous, no UI)
                          │ Status=Trial, License=Trial(plan)          │ Status=Active, License created lazily = "Trial" plan (MaxUsers 5)
                          ▼                                            ▼
                     ┌─────────┐   TrialLifecycleWorker (daily/startup)
                     │  Trial  │──────────────────────────────► License=TrialExpired (read-only), Tenant.Status stays "Trial"
                     └────┬────┘
      Activate(term) ─────┤ Extend trial (once, +14d) ←── also offered to PAID Active tenants (P1-03)
                          ▼
                     ┌─────────┐   SubscriptionLifecycleWorker: ExpiresAt+grace passed
                     │ Active  │──────────────────────────────► License=Suspended, Tenant.Status STAYS "Active" (P1-01)
                     └────┬────┘                                   UI: shows Active + Suspend button, no Reactivate
          Suspend(reason) │  ▲ Reactivate (only if Tenant.Status=="Suspended" && not expired)
                          ▼  │ Renew(term)  ← the only action that fixes every state
                     ┌──────────┐
                     │Suspended │  Tenant.IsActive=false → middleware "Tenant access is deactivated"
                     └────┬─────┘
       Deactivate / DELETE│ (DELETE is a soft deactivate; UI calls it "permanent delete")
                          ▼
                     ┌──────────┐  License=Cancelled, all Assets set Inactive (not restored on Renew)
                     │ Inactive │
                     └──────────┘
  Payment recorded (PlatformFinance) → License Active for Trial/TrialExpiringSoon/Suspended/Expired
                                        (NOT TrialExpired; never touches Tenant.Status/IsActive) (P1-05)
```

### 3.2 Inconsistencies vs. intended lifecycle

1. **No single transition function.** `SetTenantStatusAsync`, `RenewLicense`, `ExtendLicense`, `ExtendTrial`, `ApplyTenantAsync`, `PlatformTenantLicensesController.UpdateStatusAsync`, `PlatformLicensingController.UpdateSubscriptionStatus`/`UpdateSubscription`, both workers and `ApplyPaymentReceivedAsync` all write status fields independently.
2. **Suspension has two meanings with no field to tell them apart.** Manual suspension sets `Tenant.Status=Suspended` + `IsActive=false`. Automatic (non-payment) suspension sets only `TenantLicense.Status`. Reactivate only understands the first. Payment only undoes the second.
3. **"Expired" and "Cancelled" are not tenant statuses**, only license statuses, so the platform UI cannot show them as the primary state.
4. Trial "expiring soon" is 7 days in `TrialLifecycleWorker` but 3 days in `PlatformTenantsController.TrialExpiringSoonDays`.
5. Deactivation sets every asset to `Inactive` (`PlatformTenantsController.cs:1152`), and nothing restores them on Renew/Activate. That is a data side effect of a lifecycle toggle.
6. No archive/delete lifecycle exists. `DELETE` is a soft deactivate (fine), but the UI says "permanently deletes".

---

## 4. Findings

Priority: **P0** security/data-integrity · **P1** major functional · **P2** UX/maintainability · **P3** polish.
✅ = proven by a new failing test in `TenantManagementAuditTests.cs`.

### P0

**TM-P0-01 ✅ Read-only, support and finance platform staff can suspend, deactivate, activate, renew and extend tenants**
- **Evidence:** `PlatformTenantsController` is class-level `[Authorize(Policy="PlatformAccess")]` (`PlatformTenantsController.cs:22`). `PlatformAccess` = `AppRoles.PlatformRoles`, which includes `ReadOnlyAuditor`, `Auditor`, `Support`, `SupportAdmin`, `PlatformFinance` and `FinanceAdmin` (`Program.cs:175`, `AppRoles.cs:38-51`). Only Create/Update/Impersonate/user actions add `PlatformAdminAccess`. The following have no admin policy: `UpdateStatus`, `Activate`, `RenewLicense`, `ExtendLicense`, `ReactivateLicense`, `Suspend`, `DeactivatePost`, `DELETE`, `ExtendTrial`. `PlatformTenantLicensesController` exposes `POST activate|suspend` under `PlatformReadOnlyAccess` (`PlatformTenantLicensesController.cs:12`).
- **Impact:** a read-only auditor account can take any customer offline or grant a paid license term.
- **Root cause:** a class-level policy that is too broad, with per-action opt-in to stricter policies.
- **Remediation:** make `PlatformAdminAccess` the class default on both controllers and add `PlatformReadOnlyAccess` explicitly on GETs. Consider `PlatformFinanceAccess` for Renew/Extend if finance staff should keep those.
- **Test:** `Audit_TenantLifecycleMutations_RequireAnAdminLevelPlatformPolicy` (fails, listing the 9 + 2 actions).

**TM-P0-02 Anonymous self-service tenant creation endpoint is live**
- **Evidence:** `POST /api/auth/signup` (`AuthController.cs:34`) has no `[Authorize]`, and there is no fallback policy (`Program.cs` has none). It creates an **Active** tenant plus an admin with a caller-chosen password (`MvpAuthService.cs:112-200`). Its only consumers are the e2e helpers (`tests/e2e/helpers/auth.ts`) and `smoke-test.ps1`. The production UI has no caller.
- **Impact:** anyone on the internet can create workspaces (rate-limited only). It bypasses platform provisioning, plan choice, invitation flow and email identity checks. Tenants created this way get `PlanName = "Ecosys ServiceOps Suite"` but a lazily created "Trial" `LicensePlan` (MaxUsers 5): contradictory state.
- **Remediation:** gate behind a config flag that is off in Production, or restrict to Development/test hosts. Move e2e helpers to platform provisioning.
- **Test:** an integration test that signup returns 404/403 when the flag is off.

### P1

**TM-P1-01 ✅ Auto-suspended tenants cannot be reactivated from the UI (root cause of the production incident)**
- **Evidence:** `SubscriptionLifecycleWorker.cs:61-66` sets `TenantLicense.Status=Suspended` and `Tenant.LicenseStatus=Suspended` but leaves `Tenant.Status="Active"` and `IsActive=true`. The worker runs **at every API start** (`RunCycleAsync` before the timer). The platform UI decides buttons from `tenant.status` (`TenantDetailPage.tsx:397-420`): it shows "Suspend", "Renew" and "Extend" but **not** "Reactivate". `ExtendLicense` (`PlatformTenantsController.cs:222-253`) only moves `ExpiresAt` and never clears `Suspended`. `ReactivateLicense` rejects anything whose `Tenant.Status != "Suspended"` (`:259`). The middleware then returns `license_suspended` ("Tenant license is suspended. Contact Ecosys to restore access.", `LicenseAndSecurityServices.cs:527`).
- **Reproduction:** worker suspends → operator clicks **Extend** ("License extended" toast) → tenant is still blocked. Clicking Reactivate via the API gets "Only a manually suspended tenant can be reactivated". Only **Renew** fixes it. The API restarts during the recent crash-loop (`94d73ae`) each ran this worker.
- **Remediation:** one `TenantLifecycleService` owning every transition (see Phase 1). Short term: the worker must write `Tenant.Status=Suspended` + `IsActive=false` + a suspension reason (`NonPayment`), Extend must clear a non-payment suspension when the new expiry is in the future, and Reactivate must accept license-level suspension.
- **Tests:** `Audit_WorkerSuspendedTenant_IsReportedAsSuspendedToThePlatform`, `Audit_WorkerSuspendedTenant_ExtendThenReactivate_RestoresAccess` (both fail).

**TM-P1-02 ✅ Four status fields disagree, and the UI and enforcement read different ones**
- **Evidence:** `ResolveLicenseStatus` returns `Tenant.LicenseStatus` when set, ahead of `TenantLicense.Status` (`PlatformTenantsController.cs:1307-1318`). Enforcement reads `TenantLicense.Status` and `Tenant.IsActive` only. Writers that skip fields:
  - `PlatformTenantLicensesController.UpdateStatusAsync` skips `Tenant.LicenseStatus`.
  - `PlatformLicensingController.UpdateSubscriptionStatus` skips `Tenant.Status` (`:270-276`).
  - `PlatformLicensingController.UpdateSubscription` (PUT) skips all tenant fields **and can reassign a license to a different `TenantId`** (`:217`).
  - `ApplyPaymentReceivedAsync` skips `Tenant.Status/IsActive`.
  - Both workers skip `Tenant.Status/IsActive`.
- **Impact:** the platform shows "License: Active" for a blocked tenant (and the reverse). Operators act on false information.
- **Remediation:** make `TenantLicense.Status` (plus an explicit `SuspensionReason`) authoritative, derive `Tenant.LicenseStatus` or drop it, and route all writes through one service.
- **Test:** `Audit_TenantLicensesSuspend_KeepsThePlatformLicenseBadgeInSync` (fails).

**TM-P1-03 ✅ "Extend trial" silently downgrades paying customers**
- **Evidence:** the button shows whenever `!trialExtensionUsed` (`TenantDetailPage.tsx:419,553`, `TenantsPage.tsx`), including for Active paid tenants. `ExtendTrial` sets `Tenant.Status=Trial`, `License.Status=Trial` and `License.ExpiresAt = now+14d` (`PlatformTenantsController.cs:330-346`).
- **Impact:** a customer paid through next year is cut to a 14-day trial and later expires to read-only.
- **Remediation:** reject unless the license is in a trial state (backend), and hide the button otherwise (frontend).
- **Test:** `Audit_ExtendTrial_OnPaidActiveTenant_MustNotDowngradeOrShortenThePaidLicense` (fails).

**TM-P1-04 ✅ Setting status "Trial" via PATCH locks the workspace out**
- **Evidence:** `SetTenantStatusAsync` sets `tenant.IsActive = status == Active` (`:1087`), and the Trial branch never resets it. The middleware then blocks all requests (`LicenseAndSecurityServices.cs:506-512`). `ApplyTenantAsync` uses the correct `ComputeIsActive`, so the two paths disagree.
- **Test:** `Audit_PatchStatusToTrial_KeepsTheWorkspaceAccessible` (fails). No UI calls PATCH today, so exposure is API-only.

**TM-P1-05 ✅ Paying after a trial lapses does not restore access**
- **Evidence:** `PlatformBillingLifecycleService.cs:107` reactivates only `Trial`, `TrialExpiringSoon`, `Suspended` and `Expired`. `TrialLifecycleWorker` puts every lapsed trial in `TrialExpired`, which is excluded. `ExpiresAt` is extended but the status stays `TrialExpired`, so `BuildSnapshot` treats it as explicitly expired and the tenant stays read-only.
- **Test:** `Audit_PaymentOnTrialExpiredLicense_RestoresWriteAccess` (fails: status 'Expired').

**TM-P1-06 ✅ Tenants are created with unlimited seats and there is no UI to set a limit** (details in §5)
- **Test:** `Audit_TenantCreatedFromCurrentForm_HasAFiniteSeatLimit` (fails).

**TM-P1-07 CSV user import bypasses the seat limit**
- **Evidence:** `ImportController` does not inject `ILicenseGuardService` at all (constructor, `ImportController.cs`). `CommitUsers` adds users row by row with `IsActive` taken from the CSV (`:536-590`).
- **Impact:** a tenant admin can exceed licensed seats by any amount through one upload.
- **Remediation:** before commit, count rows that will be active and call a batch seat check (`EnsureCanAddUsersAsync(tenantId, n)`) inside the same serializable transaction. Reject the batch, or accept up to capacity with explicit row errors.
- **Test required:** import with limit 3, 2 in use, 5 active rows → rejected (or 1 accepted + 4 row errors).

**TM-P1-08 Seat check can race on the platform add-user path, and races surface as generic 500s**
- **Evidence:** tenant-side `UsersController.Create/Update/UpdateStatus` wrap check+insert in a `Serializable` transaction (`UsersController.cs:74,771-781`). With PostgreSQL SSI, a concurrent over-allocation aborts one transaction with `40001`. That surfaces through `DbUpdateException`/`PostgresException` as **500** "A database error occurred", not a 409 (`Program.cs:319`). `PlatformTenantsController.CreateWorkspaceUser` has **no transaction** (`:457-499`), so two concurrent platform adds at 9/10 can both succeed.
- **Remediation:** a single `EnsureCanAddUsersAsync` that takes a row lock (`SELECT … FROM tenant_licenses WHERE tenant_id=… FOR UPDATE`) inside the caller's transaction, used by every path. Map `40001` to 409 with retry.
- **Test required:** PostgreSQL integration test (Testcontainers) firing two parallel creates at limit−1. **Could not be run here: no PostgreSQL available**; EF InMemory has no transactions.

**TM-P1-09 Tenant invitation mail is still sent from an unprovisioned sender** (details in §9)

**TM-P1-10 "Impersonate admin" creates an audited session and discards it**
- **Evidence:** `useImpersonateTenant` (`platform/api.ts:368-372`) posts and ignores the token. `TenantDetailPage.tsx:554` then toasts "Impersonation session started." `openImpersonationTab` (`sessionStore.ts:178`) has **no callers**, although the entry page, banner and exit path all exist. The backend has already created a session and written two `impersonation.started` audit rows (`MvpAuthService.cs:343-395`). No reason is collected, so every record says "No reason was given."
- **Remediation:** call `openImpersonationTab(result)` on success and add a reason dialog. Add an `impersonation.ended` audit on exit/logout.
- **Test required:** Playwright: click Impersonate → new tab lands on `/dashboard` with the banner showing.

**TM-P1-11 "Activate tenant" on the Account Management tab always fails**
- **Evidence:** `TenantDetailPage.tsx:550` calls `activate.mutateAsync(undefined)`. The backend `Activate` requires an `ActivateTenantLicenseRequest` body and validates the term (`PlatformTenantsController.cs:163-169`), so the result is a 400. The button is shown for **every** non-Active status, including Suspended, where the correct action is Reactivate.
- **Remediation:** remove it. The Subscription tab already has a dialog-backed Activate.

### P2

| ID | Title | Evidence | Remediation |
|---|---|---|---|
| TM-P2-01 | Platform "Max users" shows the wrong field | `TenantDetailPage.tsx:390` shows `Tenant.MaxUsers`. Enforcement uses `TenantLicense.MaxUsersOverride ?? LicensePlan.MaxUsers` (`LicenseAndSecurityServices.cs:378`). | Return the effective limit and usage from `LicenseGuardService.GetSnapshotAsync` in the detail DTO. |
| TM-P2-02 | Unenforced plan fields | `EnsureCanCreateAssetAsync`/`EnsureCanCreateWorkOrderAsync` have 0 callers. `AdvancedReports`/`ClientPortal` flags are never checked. The Finance plan has `ClientPortal=false`, but the portal is reachable. | Enforce, or hide from plan editor and deprecate. Product decision. |
| TM-P2-03 | Module enforcement is allow-by-default | `ServiceOpsPlans.RequiredModules` returns `[]` for any unlisted controller (`ServiceOpsPlans.cs:68`), e.g. Knowledge, KnowledgeDocuments, Kip, Integrations, AdvancedSettings. New controllers are ungated. | Add a startup/unit test that every tenant controller is classified (Operations, Finance, Shared). |
| TM-P2-04 | Intake webhooks ignore suspension/expiry | `IntakeController` (anonymous) checks secret + feature only. `EnsureFeatureEnabledAsync` does not call `EnsureTenantCanMutateAsync`, and the middleware doesn't run for anonymous requests. Suspended/cancelled tenants keep receiving webhook-created work orders. | Call `EnsureTenantCanMutateAsync` and check `Tenant.IsActive` in intake. |
| TM-P2-05 | "Verify SMTP" always reports success | The API returns 200 with `success:false` on failure. `TenantDetailPage.tsx:119-127` toasts "SMTP verification completed" regardless. The backend audits "was verified" even on failure. | Branch on `result.success` and show the error category. |
| TM-P2-06 | "Send test email" gives false confidence | Queues only, clears `LastError` and returns success before delivery (`PlatformTenantCommunicationSettingsController.cs:160-163`). The default recipient is the tenant's own managed address. | Report "queued", surface outbox result, require an explicit recipient. |
| TM-P2-07 | Invitation status invisible to platform | `PlatformTenantUserResponse` has no invitation state/expiry. The "Initial admin invite" cards read `initialAdminInvitationSent`, which is only populated on the Create response, so after reload they always show "Not sent"/"Check status" (`TenantDetailPage.tsx:320-322`). The tenant-side list already computes Invited/Expired (`UsersController.LoadLatestInvitationsAsync`). | Reuse that projection in the platform users endpoint and delete the three cards. |
| TM-P2-08 | Invitation delivery failure not audited on the normal path | `Create` audits `InvitationDeliveryFailed` only when delivery **throws**. `SendPasswordResetLinkAsync` normally returns `Success=false` (`PlatformTenantsController.cs:105-121`). | Audit on `!Success` too. |
| TM-P2-09 | Communication tab blank for PlatformOwner | The controller requires `PlatformSettingsAccess` (SuperAdmin, PlatformSuperAdmin, PlatformAdmin only; excludes PlatformOwner). On error the tab renders a spinner forever (`TenantDetailPage.tsx:428,463`). | Align policy and render the error state. |
| TM-P2-10 | Tenant query-filter bypass is role-only | `HttpTenantContext.IsSuperAdmin` is any platform role claim, with no root-tenant check. It is used by `AppDbContext.ShouldBypassTenantFilter`. Today every tenant role path canonicalises to tenant roles (`UsersController.NormalizeRole`, `NormalizeWorkspaceRole`), so this is **not exploitable now**, but a single future role-assignment bug would expose all tenants. | Require `TenantId == RootTenantId` in `IsSuperAdmin` (defence in depth, matching `Program.cs` `IsPlatformTenant`). |
| TM-P2-11 | Deactivation mutates asset data | `SetTenantStatusAsync` sets all assets `Inactive` (`:1152`), and nothing reverses it. | Remove; tenant access control already blocks use. |
| TM-P2-12 | Business-rule errors are 400, license errors are ProblemDetails, and races are 500 | Three error shapes (`Program.cs:262-340`). The frontend copes (`apiError.ts:210-214`), but a seat-limit race gives "Something went wrong on our end". | Map `40001`/unique violations to 409 ProblemDetails. |
| TM-P2-13 | PUT tenant is destructive on omission | `ApplyTenantAsync` overwrites `MaxUsersOverride` with `request.MaxUsers` (null clears it), resets `License.ExpiresAt` to trial end when expiry isn't sent, re-derives status from `request.Status` (null ⇒ Active) and rejects past trial dates (`:787-921`). No UI calls PUT today. | Split into PATCH-style profile edit, and route license/seat changes through lifecycle actions. |
| TM-P2-14 | User import maps "technician" to `User` role | `ImportController.NormalizeImportedRole` (`:1351`). | Map to `AppRoles.Technician`. |
| TM-P2-15 | Duplicate lifecycle buttons | Suspend/Deactivate/Extend trial appear on both the Subscription and Account Management tabs, and again as row actions on the list page. | One lifecycle panel on the detail page; list page read-only. |

### P3

- Mojibake (`â€”`, `Â·`, `Sendingâ€¦`) in 4 `.tsx` files, 11 occurrences in `TenantDetailPage.tsx` alone, plus the tenant Users table (seen in the Playwright snapshot).
- Stale placeholder copy: "Cross-tenant user listing is not exposed yet" sits directly above a working user list (`TenantDetailPage.tsx:315`). The "Notification groups" section is 8 static cards reading "Managed by tenant notification settings" (`:450-460`).
- "Delete tenant" says "permanently deletes … only shown here for platform owners". It is a soft deactivate and shown to everyone (`:561-567`, backend `:290-295`).
- Suspend/Deactivate reasons are hard-coded ("Suspended by platform owner"), and no reason input is offered (`:687,702`).
- `Tenant.Users` count excludes `SuperAdmin` but includes pending invitees. Fine for seats, but it is labelled "Users" rather than "Seats used".
- Trial "expiring soon" threshold is inconsistent (7 vs 3 days).

---

## 5. User capacity / licensed seats

| Question | Answer |
|---|---|
| Can max users currently be configured? | **Only via raw API** (`POST/PUT /api/platform/tenants` `maxUsers` → `TenantLicense.MaxUsersOverride`), or at plan level on the Licensing page (affects every tenant on that plan). **The Create Tenant form sends no seat field.** The Playwright spec asserts the exact payload with `toEqual`, locking this in (`serviceops-provisioning.spec.ts:53`). There is no edit form after creation. |
| Can it be enforced? | **Yes.** `LicenseGuardService.EnsureCanCreateUserAsync` → `EnsureWithinLimitAsync` (`LicenseAndSecurityServices.cs:306-326`) throws `LicenseException("license_users_limit_reached", 409)`. Verified by `Audit_SeatGuard_…` and `Audit_PlatformAddWorkspaceUser_IsRejectedAtTheSeatLimit` (both pass). |
| Can a tenant currently create unlimited workspace users? | **Yes, for every tenant created through the current UI.** Commercial plans have `MaxUsers = null` and the override is never set. Proven by `Audit_TenantCreatedFromCurrentForm_HasAFiniteSeatLimit` (fails). Even with a limit, **CSV import bypasses it** (TM-P1-07). |
| Does an older implementation exist? | **Yes, and it should be reused, not replaced.** Fields: `LicensePlan.MaxUsers`, `TenantLicense.MaxUsersOverride` (plus a dead copy `Tenant.MaxUsers`). Service: `LicenseGuardService`. Tenant-side UI: `AccountSettingsPage` "Max Users" (read-only, effective value). Platform UI: plan editor only. It was disconnected when the Create form was reduced to "essential fields". |

**Paths audited**

| Path | Guard | Transaction | Notes |
|---|---|---|---|
| Tenant admin create (`POST /api/users`) | ✅ | Serializable | Counts even if created inactive (conservative). |
| Tenant admin reactivate (`PUT /api/users/{id}`, `PATCH …/status`) | ✅ | Serializable | |
| Platform add workspace user | ✅ | ❌ | Race (TM-P1-08). |
| Initial admin at tenant creation | n/a | ✅ | First seat. No check needed unless the limit could be 0 (validated >0). |
| CSV import | ❌ | ❌ | TM-P1-07. |
| Resend invitation | n/a | n/a | Doesn't change seat usage; requires an active user. |
| Self-signup | n/a | ✅ | TM-P0-02. |
| Portal contacts (`ClientPortalUsers`) | n/a | n/a | Separate entity, correctly **not** counted. |

**Current counting rule:** `Users.Count(TenantId && IsActive)`. Pending invitees are `IsActive=true` so they count, and deactivated users don't. This matches the desired rule. `SuperAdmin` rows are excluded from platform display counts but not from the guard (irrelevant in practice).

**Reducing the limit below usage:** nothing is deactivated. New creates/reactivations are rejected until usage falls (`current >= limit`). This matches the desired rule.

**Cleanest path forward**
1. Keep `LicenseGuardService` as the one capacity rule. Add `EnsureCanAddUsersAsync(tenantId, count)` that row-locks `tenant_licenses` and is called inside the caller's transaction (users create/reactivate, platform add, import).
2. Treat `TenantLicense.MaxUsersOverride` as "licensed seats". Stop writing and displaying `Tenant.MaxUsers` (keep the column, deprecate).
3. Add a **Seats** field to Create Tenant and to Activate/Renew (`LicenseTermDialog`), and a `PATCH /api/platform/tenants/{id}/license/seats` action with audit (before/after).
4. Expose `seatsUsed / seatLimit` from the snapshot in list, detail and tenant Users pages. Show a specific message on `license_users_limit_reached`.
5. Decide the default for existing unlimited tenants. **Do not** backfill a limit silently. Report current usage and let the platform owner set values.

---

## 6. Licensing & modules

- **Commercial model implemented:** plan (`LicensePlan`, codes `operations` | `finance` | `suite`, plus legacy inactive Trial/Starter/Professional/Enterprise) + one `TenantLicense` per tenant (unique index) with status, term (`StartsAt/ExpiresAt/IsPerpetual`), grace period, billing cycle, and per-tenant overrides. Trial is a *status* on the license, with a 14-day term. There is one licensing system, but three controllers edit it.
- **Modules:** derived from the plan code (`ServiceOpsPlans.Modules`). Legacy plans with no explicit module list get both modules. There is **no per-tenant module assignment**: changing modules means changing plan, which is only possible in the Activate/Renew dialog.
- **Frontend gating:** `useEntitlements` → `/api/tenant/license` (exempt from suspension so the UI can still load). Navigation and actions hide unlicensed modules.
- **Backend gating:** `TenantLicenseEnforcementMiddleware` + `ServiceOpsPlans.RequiredModules` returns 403 `license_module_{x}` for classified controllers and settings routes. Hidden navigation cannot be bypassed for classified controllers. Unclassified controllers are open (TM-P2-03).
- **Module removal:** data is untouched and APIs return 403 with a clear message. Historical data is preserved.
- **Features:** `EmailIngestion`/`MonitoringIntegration` are enforced only in `IntakeController`. `AdvancedReports`/`ClientPortal` are not enforced. `MaxAssets`/`MonthlyWorkOrders`/`MaxBranches`: only branches are enforced.
- **Auto-renew:** **does not exist anywhere** (entities, migrations, workers, UI). `BillingCycle`/`NextBillingDate` are informational, and "Renew" is a manual action with an explicit term. Nothing to hide. Keep it this way until payments exist, and avoid labelling `NextBillingDate` as if charges will occur.

---

## 7. Tenant status / suspension

**Trace (suspend → reactivate):** Platform UI → `POST /api/platform/tenants/{id}/suspend` → `SetTenantStatusAsync` (writes all four fields) → DB. The next tenant request → `TenantLicenseEnforcementMiddleware` reads `Tenant.IsActive` (DB, per request) → 403 "Tenant access is deactivated". Reactivate reverses it and access returns **on the next request**. There is no JWT claim, in-memory cache, Redis or singleton holding license state (`LicenseGuardService` is scoped and reads the DB), so the manual path works. It is covered by the existing `Reactivate_*` tests.

**Where it breaks:** automatic non-payment suspension (TM-P1-01), plus the field drift in TM-P1-02.

**Messages:** a manually suspended tenant sees "Tenant access is deactivated…" (`ForbiddenException`, no error code, one de-duplicated toast). A license-suspended tenant sees "Tenant license is suspended…" as a banner via `X-License-Warning`, with the toast suppressed. **Two different messages for one business state.** De-duplication itself works (`notifyOnce`, `LICENSE_ERROR_CODES`), and the banner clears on the next healthy tenant response.

**Login:** blocked when `Tenant.IsActive=false` (`MvpAuthService.cs:293`). An auto-suspended tenant (`IsActive` still true) **can log in** and then receives 403 on every call.

---

## 8. User & invitation lifecycle

- **Mechanisms are distinct and correct:** `PasswordLinkTokens` with purposes `Invitation` (72h) and `PasswordReset`. Tokens are hashed, single-use and replaced on resend (`InvitationRemediationTests`). Platform "reset password" is refused for users still pending setup, which sends operators to "resend setup link" instead. This is good separation.
- Deactivated users cannot log in (`MvpAuthService.cs:287`). Sessions are revoked on deactivation.
- Pending users cannot log in usefully: the random temp password is never disclosed, and `PasswordChangeEnforcementMiddleware` applies.
- **Gaps:** platform user list lacks invitation state/expiry (TM-P2-07). There is no platform deactivate/reactivate of a workspace user (tenant-only). There is no audited failure on the first invite when delivery returns false (TM-P2-08). `User.InviteTokenHash/InviteTokenExpiresAt/InviteAcceptedAt` appear to be legacy (see §13). Global unique `Users.Email` means one person cannot belong to two tenants. That is a product decision to confirm.

---

## 9. Email delivery

**Architecture (working tree):**
- **Transport:** platform SMTP (root-tenant `EmailSetting` + `Smtp:*` config). Secrets are encrypted (`EncryptedSecret`) and never returned by the API (masked).
- **Tenant identity:** `EmailSetting.ManagedEmailAddress = {slug}@serviceops.ecosysdigital.co.ke`, with new `ManagedEmailStatus` Pending/Verified/Rejected.
- **From:** always the tenant managed address (`TenantEmailIdentity.ResolveSender`). **Reply-To:** tenant override or the managed address. **SMTP login:** platform account. **Envelope sender:** MailKit uses the From mailbox, so it is the tenant address too. No `Sender:` header.
- **Platform mail** (onboarding welcome, platform notices) uses the platform sender.

**The production failure mode is not fixed by the current WIP.** A new tenant's identity is created as `Pending` (`TenantEmailIdentity.Create`), and invitations are sent `From` that address regardless of status (`UserCredentialDeliveryService` WIP diff; `TenantEmailIdentity.ResolveSender` ignores status). Tests in the WIP assert this: `Invitation_SenderRejected_MarksIdentityRejected_AndKeepsTheTenantSender` and `TenantSender_NeverFallsBackToALegacySenderAddress`. The WIP adds diagnosis (MAIL FROM/RCPT probe, `Rejected` status, clear log line), but the first-admin invitation of every new tenant will still bounce until someone provisions the address on the mail server. For a Pending/Rejected identity that directly contradicts the requirement that an unverified display identity must not become the SMTP sender.

**Recommended rule** (compatible with the WIP's intent):
- `Verified` → From = tenant address.
- `Pending`/`Rejected` → From = platform sender (e.g. `noreply@serviceops…`), display name "{Tenant} via Ecosys", Reply-To = tenant reply-to or contact.
- Provisioning should attempt `VerifySenderAsync` right after tenant creation, and the platform page should show identity status with a "Re-check" button. **Account-setup and password-reset mail should arguably always use the platform sender**, because they are security mail from Ecosys, not from the tenant.

**Connection test:** the WIP "Verify" now probes MAIL FROM/RCPT for the tenant address, which is good. The frontend still shows success on failure (TM-P2-05). "Send test email" is queue-only (TM-P2-06).

---

## 10. Settings ownership

| Setting | Current owner/API | Correct owner | Risk | Action |
|---|---|---|---|---|
| SMTP transport | Platform (`/api/platform/settings/email`) | Platform | — | Keep |
| Tenant sender identity / reply-to / notification prefs | Tenant (`/api/settings/email…`). Platform tab read-only (+ test/verify). | Tenant (identity provisioning = platform) | Low | Keep. Add identity status. |
| Tenant notification groups | Tenant. Platform tab shows static placeholders. | Tenant | UX | Remove placeholder |
| Document numbering | Tenant (`/api/settings/numbering-rules`). Platform read + support reset. | Tenant | Support reset is destructive but reason-gated and audited | Keep |
| **Platform numbering** (`/api/platform/settings/numbering`, `/numbering/rules`) | Root tenant only | Platform (Ecosys's own invoices) | None cross-tenant | **Orphan: no UI caller. Deprecate or re-surface under Platform Finance.** |
| **Platform tax-finance** (`/api/platform/settings/tax-finance`) | Root tenant | Platform | Frontend comments show tenant finance once tried to read it as a tenant default | **Ambiguous. Confirm it is not a tenant default. Deprecate if unused.** |
| Branding | Tenant (`/api/tenant/profile`). Platform read-only. | Tenant | — | Keep |
| Security policy | `TenantSecurityPolicy` (tenant) + platform security settings | Both (platform floor, tenant stricter) | Unclear precedence | Document precedence |
| Plans / module definitions | Platform | Platform | — | Keep |
| Per-tenant plan, term, seats | Platform (lifecycle actions) | Platform | Seats not editable | Add seats action |
| `TenantModuleSetting` (JSON blobs) | Tenant. Shown on the platform "Modules" tab. | Tenant | **Misleading:** looks like entitlements | Rename tab section "Module preferences" or move |
| Tenant finance defaults, tax, SLA, workflow | Tenant (`/api/settings/*`) | Tenant | — | Keep |

**Misplaced:** none remaining that write tenant data from platform settings. **Ambiguous:** platform tax-finance, and security-policy precedence. **Platform controls that alter tenant business data:** support "reset numbering" (guarded), tenant **deactivation setting all assets Inactive** (TM-P2-11).

---

## 11. Security / tenant isolation

- **Tenant ↔ tenant:** global query filter on every `ITenantEntity` (`AppDbContext.ApplyTenantQueryFilters`). Only one `FindAsync` in services (`ScheduledReportService.cs:176`, filter-applied). Controllers filter `x.TenantId == TenantId` explicitly in addition. The existing `TenantIsolationTests` (9 tests, pass) cover clients/sites/assets/work orders/users/reports/PM/asset-category cross-tenant IDOR. **No cross-tenant read or write path found.**
- **Anonymous requests bypass the tenant filter** (`ShouldBypassTenantFilter` when unauthenticated). All anonymous endpoints reviewed (intake, public work order, portal auth, signup) scope explicitly by secret/slug/token. This stays safe only while every anonymous endpoint does, so it is worth a test that enumerates `[AllowAnonymous]` actions.
- **Platform vs tenant:** platform policies require the root-tenant claim (`Program.cs:165-173`). Tenant admins cannot reach platform endpoints. Tenant roles cannot be set to platform roles (`NormalizeRole`/`NormalizeWorkspaceRole`/import). Platform users are always created in the root tenant.
- **Within platform:** TM-P0-01 (read-only roles mutate tenants). `PlatformLicensingController.UpdateSubscription` can move a license to another tenant (admin-only, but an integrity hazard).
- **Impersonation:** requires `PlatformAdminAccess` and an active tenant. The token carries `ImpersonatedBy*` claims, the banner and Exit exist, and start is audited in both tenant and platform logs. Refresh is disabled for impersonation sessions. **Gaps:** UI never starts it (TM-P1-10), no reason is captured, and there is no `ended` audit.
- **Secrets:** SMTP secrets encrypted at rest and never returned. Invitation/reset tokens stored hashed. No secrets found in audit text.

---

## 12. UX findings — control classification (Tenant pages)

| Control (location) | Class | Notes |
|---|---|---|
| Tenant list columns | PARTIAL | Missing modules, seats used/limit, license expiry, primary admin. Has "Days remaining" (trial only). |
| List row: Activate / Suspend / Extend trial / Deactivate | DUPLICATE, **MISLEADING** | Extend trial on paid tenants (TM-P1-03). Suspend shown for auto-suspended tenants. |
| Create tenant drawer | PARTIAL | No seats. Plan radio works. Invitation-failure toast correct. |
| Overview: stats + profile | WORKING | 17 info cards, dense but correct. No edit. |
| Branding (read-only) | WORKING | |
| Users: summary cards (4 of 6) | **BROKEN/PLACEHOLDER** | "Must change password", "Initial admin invite" and "note" are always wrong after reload. Stale "not exposed yet" copy. |
| Users: list, Add workspace user, Resend setup link, Reset password | WORKING | Seat-guarded. Delivery result toasted. |
| Modules tab | **MISLEADING** | Shows settings blobs, not entitlements. The entitlement panel is an "UnsupportedTab" link. |
| Subscription: Activate / Renew (dialog) | WORKING | Plan change is possible here only. |
| Subscription: Reactivate | PARTIAL | Hidden for auto-suspended tenants (TM-P1-01). |
| Subscription: Extend | PARTIAL | Doesn't clear suspension. |
| Subscription: Max users / branches | **MISLEADING** | Wrong field (TM-P2-01). |
| Communication: Send test email | MISLEADING | TM-P2-06 |
| Communication: Verify SMTP | **BROKEN** (false success) | TM-P2-05 |
| Communication: Notification groups | PLACEHOLDER | |
| Numbering: list + Reset to 1 | WORKING | Reason required, audited. |
| Templates (read-only) | WORKING | |
| Finance summary | WORKING / PARTIAL | Link-out placeholder. |
| Audit trail | PARTIAL | Last 25 only. Mixed action naming (`TenantCreated`, `platform.tenant.updated`, `User created`). |
| Account: Activate tenant | **BROKEN** | TM-P1-11 |
| Account: Suspend / Deactivate / Extend trial | DUPLICATE | |
| Account: Impersonate admin | **BROKEN** | TM-P1-10 |
| Account: Delete tenant | **MISLEADING** | Soft deactivate labelled permanent. |
| Encoding | BROKEN (P3) | `â€”` mojibake |

---

## 13. Dead / legacy code candidates (do not delete yet)

| Item | Evidence | Recommendation |
|---|---|---|
| `src/Ecosys.Platform`, `src/Ecosys.ServiceOps` projects | In `Ecosys.slnx`, not referenced by Api/Infrastructure. Duplicate `Tenant`/`EmailSetting` entities. | Remove from solution after confirming no deploy script builds them. |
| `platform/tenants/api.ts` | Pages import `../api` (`platform/api.ts`), and its hooks are duplicated there. | Merge into one client. |
| `PlatformTenantLicensesController` | No UI caller. Weak auth. Incomplete field sync. | Deprecate → remove. |
| `PlatformLicensingController` subscription POST/PUT/activate/suspend/cancel | UI uses GET list only. Duplicates tenant lifecycle. PUT can move a license between tenants. | Deprecate mutations. Keep read. |
| `PUT /api/platform/tenants/{id}`, `PATCH …/status` | No UI caller. Destructive semantics (TM-P2-13, TM-P1-04). | Replace with profile PATCH + lifecycle actions. |
| `POST /api/auth/signup` | Test-only consumer. | Gate/remove (TM-P0-02). |
| `Tenant.MaxUsers`, `Tenant.MaxBranches`, `Tenant.LicenseStatus`, `Tenant.TrialStartsAt/EndsAt`, `Tenant.SubscriptionStartsAt/EndsAt`, `Tenant.PlanName` | Denormalised license copies that drift. | Stop writing; derive from license; drop later. |
| `User.InviteTokenHash/InviteTokenExpiresAt/InviteAcceptedAt` | Superseded by `PasswordResetTokens` purpose `Invitation`. | Verify readers; deprecate. |
| `LicenseGuardService.EnsureCanCreateAssetAsync/WorkOrderAsync` | Zero callers. | Wire up or remove with plan fields. |
| `openImpersonationTab` + `ImpersonationEntryPage` | Unreachable. | **Wire up** (TM-P1-10), don't delete. |
| `/api/platform/settings/numbering*`, `/tax-finance` | No UI caller. | Decide (see §10). |
| `ILicenseGuardService.EnsureModuleEnabledAsync` default `throw NotSupportedException` | Interface default body. | Remove the default. |

---

## 14. Test coverage

**Commands run**

| Command | Result |
|---|---|
| `dotnet test` (baseline, before audit tests) | **567 passed, 2 failed, 1 skipped.** Both failures are `SecurityHardeningTests.Swagger_IsNotExposedOutsideDevelopment` and `RequestCorrelationTests.EveryRequest_CarriesTheSameTraceId…`, caused by `NpgsqlException: Failed to connect to 127.0.0.1:5432`. **Environmental (no PostgreSQL), unrelated to tenants.** |
| `dotnet test --filter "Audit=Defect\|Audit=Verified"` | **9 failed (all expected defect proofs), 2 passed (verified behaviour).** |
| `npm run lint` | Pass |
| `npm run build` | Pass |
| `npx vitest run` | 31/31 pass |
| `playwright test tests/e2e/serviceops-provisioning.spec.ts` (mock-only, `PLAYWRIGHT_SKIP_BACKEND_PRECHECK=1`) | **11 passed, 2 failed.** "pending and expired users both expose Resend Invite" and "managed workspace email saves only tenant-facing preferences" time out: the target buttons resolve and are enabled but never become *stable*, which suggests continuous re-render under mocked data. Pre-existing; root cause not determined. The real-backend Playwright suites could not run (no API/DB). |

**Existing tenant-related tests**

| Type | Files | Assessment |
|---|---|---|
| Unit/controller (EF InMemory) | `TenantLicenseLifecycleTests` (21), `TenantProvisioningTests` (8), `LicenseReadOnlyEnforcementTests` (8), `InvitationRemediationTests` (6), `TenantEmailIdentityTests` (12, WIP), `TenantInvitationEmailTests`, `TenantIsolationTests` (9), `SecurityHardeningTests` | Good lifecycle and isolation coverage. **All `PlatformTenantsController` tests use `NoOpLicenseGuardService`**, whose `EnsureCanCreateUserAsync` is a no-op, so seats/limits were never exercised through controllers. No workers are tested with controllers together. No authorization-policy tests. InMemory cannot test transactions or concurrency. |
| Playwright | `serviceops-provisioning.spec.ts`, `auth-tenant-onboarding.spec.ts`, `platform-*` | Provisioning spec is **fully mocked**. It proves UI wiring only and asserts the seat-less payload. |

**New audit tests** (`backend/tests/Ecosys.Tests/TenantManagementAuditTests.cs`)

| Test | Result | Finding |
|---|---|---|
| `Audit_TenantLifecycleMutations_RequireAnAdminLevelPlatformPolicy` ×2 | FAIL | P0-01 |
| `Audit_WorkerSuspendedTenant_IsReportedAsSuspendedToThePlatform` | FAIL | P1-01 |
| `Audit_WorkerSuspendedTenant_ExtendThenReactivate_RestoresAccess` | FAIL | P1-01 |
| `Audit_TenantLicensesSuspend_KeepsThePlatformLicenseBadgeInSync` | FAIL | P1-02 |
| `Audit_ExtendTrial_OnPaidActiveTenant_MustNotDowngradeOrShortenThePaidLicense` | FAIL | P1-03 |
| `Audit_PatchStatusToTrial_KeepsTheWorkspaceAccessible` | FAIL | P1-04 |
| `Audit_PaymentOnTrialExpiredLicense_RestoresWriteAccess` | FAIL | P1-05 |
| `Audit_TenantCreatedFromCurrentForm_HasAFiniteSeatLimit` | FAIL | P1-06 |
| `Audit_SeatGuard_CountsActiveAndPendingUsers_AndIgnoresDeactivatedUsers` | PASS | seat rule verified |
| `Audit_PlatformAddWorkspaceUser_IsRejectedAtTheSeatLimit` | PASS | platform path verified |

> **CI note:** the 9 defect tests will turn the backend suite red until remediated. That is intentional for the audit. If CI must stay green in the meantime, exclude them with `--filter "Audit!=Defect"`; do not delete or weaken them.

**Missing coverage (required before/with remediation):** PostgreSQL concurrency test for seats (Testcontainers); CSV import seat test; worker + lifecycle integration; authorization matrix for every platform endpoint × role; module classification completeness; anonymous-endpoint scoping; invitation expired/reused via HTTP; Playwright on a real backend for create → invite → accept → suspend → reactivate; email sender fallback by identity status.

---

## 15. Remediation plan

### Phase 0 — Security / data integrity
- **Files:** `PlatformTenantsController.cs`, `PlatformTenantLicensesController.cs`, `PlatformLicensingController.cs` (subscription PUT), `AuthController.cs`/`MvpAuthService.cs` (signup), `HttpTenantContext.cs`.
- **Behaviour:** admin-level policy on all tenant/license mutations. Signup disabled outside dev/test. `IsSuperAdmin` requires the root-tenant claim. Subscription PUT cannot change `TenantId`.
- **Tests:** P0-01 audit test green. Role × endpoint matrix. Signup-disabled test.
- **Migration:** none. **Risk:** low. Check which staff roles currently use Renew/Extend (finance?) and grant `PlatformFinanceAccess` deliberately if so.

### Phase 1 — Tenant lifecycle and licensing (one source of truth)
- **Files:** new `Ecosys.Infrastructure/Services/TenantLifecycleService.cs` (Activate, Suspend(reason, kind), Reactivate, Renew, Extend, ExtendTrial, Deactivate, ApplyPayment); callers: `PlatformTenantsController`, both workers, `PlatformBillingLifecycleService`, `PlatformLicensingController`; `LicenseAndSecurityServices.cs` (middleware message unification).
- **Behaviour:** `TenantLicense.Status` + new `SuspensionReason` (Manual/NonPayment) is authoritative. `Tenant.Status/IsActive` are set only by the service. `Tenant.LicenseStatus` is derived. ExtendTrial only for trial states. Payment reactivates `TrialExpired` and NonPayment suspensions only. Remove the asset side effect on deactivation. One user-facing message per state.
- **Tests:** P1-01…P1-05 audit tests green. Worker + service tests.
- **Migration:** add `SuspensionReason` (nullable). Backfill: `license.Status=Suspended AND tenant.Status<>'Suspended'` ⇒ NonPayment, and reconcile `Tenant.Status/IsActive`. **Review the backfill SQL against production first** (a reconcile script precedent exists: `production_reconcile_serviceops_provisioning.sql`).
- **Risk:** medium, since this touches every lifecycle path. Ship behind the audit tests.

### Phase 2 — User capacity and access
- **Files:** `LicenseAndSecurityServices.cs` (`EnsureCanAddUsersAsync` with `FOR UPDATE`), `UsersController.cs`, `ImportController.cs`, `PlatformTenantsController.cs` (add user in a transaction, new `license/seats` action, effective seats in DTOs), `Program.cs` (40001 → 409), frontend `TenantsPage`, `LicenseTermDialog`, `TenantDetailPage`, tenant Users page.
- **Behaviour:** seats are set at create/renew/edit and enforced on every path including import and concurrency. "12 / 20 seats" is shown on the platform list/detail and tenant Users page. A clear limit-reached message.
- **Tests:** P1-06 audit test green (or re-stated if "unlimited" becomes an explicit option). Import test. PostgreSQL concurrency test. Playwright seat display.
- **Migration:** none required (`MaxUsersOverride` exists). Stop writing `Tenant.MaxUsers`. **Do not backfill limits automatically.**
- **Risk:** medium (locking). Keep the lock scope to the single license row.

### Phase 3 — Tenant settings cleanup
- **Files:** `PlatformSettingsController.cs` (orphan numbering/tax endpoints), `TenantDetailPage.tsx` (Modules tab relabel, notification placeholder), `PlatformTenantCommunicationSettingsController.cs` (policy alignment).
- **Behaviour:** platform pages only show tenant-owned settings read-only or as explicit support actions.
- **Tests:** tenant cannot call platform settings (exists implicitly; add explicit). Platform read of tenant settings.
- **Migration:** none. **Risk:** low.

### Phase 4 — Email/onboarding reliability
- **Files:** `TenantEmailIdentity.cs`, `UserCredentialDeliveryService.cs`, `EmailOutboxServices.cs`, `PlatformTenantsController.Create`, communication controller, `TenantDetailPage` (identity status, verify result).
- **Behaviour:** From = tenant address only when `Verified`, otherwise the platform sender with the tenant display name and reply-to (decide whether account-setup/reset always use the platform sender). Sender probe at provisioning. Invitation status (sent/failed/expired/accepted) visible on the platform users list. Delivery failure audited. Verify/test UI truthful.
- **Tests:** adjust the WIP tests that currently assert "never fall back" for Pending/Rejected. Delivery by status matrix. Playwright verify-failure message.
- **Migration:** the WIP migration `AddManagedEmailProvisioningStatus` is sufficient. Existing tenants will be `Pending` until probed, so run a one-off probe job after deploy.
- **Risk:** low–medium. This changes visible From for unverified tenants, which is intentional.

### Phase 5 — UI cleanup
- **Files:** `TenantDetailPage.tsx` (813 lines → split per tab), `TenantsPage.tsx`, `platform/api.ts` + delete `platform/tenants/api.ts`, `sessionStore.ts` (impersonation wiring), mojibake files.
- **Behaviour:** one lifecycle panel. Remove broken "Activate tenant" and Account-tab duplicates. "Delete" renamed "Deactivate" (or real archive later). Working impersonation with reason. List columns: status, plan/modules, seats, expiry, primary admin, last activity. Encoding fixed.
- **Tests:** Playwright per control (real backend where possible).
- **Migration:** none. **Risk:** low.

### Phase 6 — Full regression suite
- PostgreSQL-backed integration project (Testcontainers) for lifecycle, seats and concurrency. Authorization matrix test. Module-classification test. Anonymous-endpoint scoping test. Real-backend Playwright: create → invite → accept → add users to limit → suspend → reactivate → renew. Fix or quarantine the 2 flaky mocked provisioning specs with a root cause. Run in CI with the `Audit` trait tests required green.

---

**Bottom line:** Ecosys has **one entitlement model** (`TenantLicense` + `LicensePlan` + `LicenseGuardService`) and **one existing seat mechanism**, which should be reused. Around them sit **several partially overlapping writers** of tenant state (three controllers, two workers, a billing service and an anonymous signup path) plus a denormalised copy of license data on `Tenant`. The new tenant UI **disconnected** seat configuration (never sent), module visibility (settings blobs shown instead of entitlements), impersonation (never started), and parts of lifecycle management (auto-suspension invisible). Consolidating writes behind a single lifecycle service and reconnecting the existing seat fields fixes most P1s without adding new mechanisms.

---

## 16. Remediation status (implemented 2026-09-24)

Phases 0, 1, 2, 3, 5 and 6 are implemented. **Phase 4 (email delivery/sender identity) was skipped at the owner's request**, together with the email-settings findings TM-P1-09, TM-P2-05, TM-P2-06 and TM-P2-09.

| Finding | Status | Change |
|---|---|---|
| TM-P0-01 read-only roles mutate tenants | **Fixed** | `PlatformAdminAccess` on every tenant/license mutation (`PlatformTenantsController`, `PlatformTenantLicensesController`). The authorization test covers 5 platform controllers. |
| TM-P0-02 anonymous signup | **Fixed** | `AuthSecurity:AllowSelfSignup` (default **false**; true only in `appsettings.Development.json` for e2e helpers). Disabled → 404. |
| TM-P2-10 filter bypass by role only | **Fixed** | `HttpTenantContext.IsSuperAdmin` now also requires the root-tenant claim. |
| Subscription PUT could move a license between tenants | **Fixed** | Rejected with 400. |
| TM-P1-01 / TM-P1-02 status drift, suspension incident | **Fixed** | `TenantLicense` is authoritative. New `TenantLicense.SuspensionReason` (Manual / NonPayment). `TenantLifecycleState.Sync` projects Status/IsActive/LicenseStatus/plan/dates/seats onto the tenant row and is called by every writer: platform actions, both license consoles, both workers, and payment. Reactivate accepts any suspension. Extend to a future date lifts a non-payment suspension. The platform badge reads the license. |
| TM-P1-03 extend-trial downgrade | **Fixed** | Only allowed for trial licenses (backend), and only shown for trial tenants (UI). |
| TM-P1-04 PATCH Trial lockout | **Fixed** | Status derived from the license. |
| TM-P1-05 payment after TrialExpired | **Fixed** | Payment lifts any trial state, Expired, and non-payment suspensions. Manual suspensions are kept. |
| TM-P1-06 unlimited seats | **Fixed** | "Licensed Users" is required on create (UI and API). `PUT /api/platform/tenants/{id}/license/seats` is audited, and lowering it never deactivates anyone. Seats are shown as used / limit on the platform list, detail and Users tab, and on the tenant Users page. |
| TM-P1-07 CSV import bypass | **Fixed** | `EnsureCanAddUsersAsync(tenant, activeRows)` runs before any row is written. |
| TM-P1-08 seat race / 500 on conflict | **Fixed** | The guard row-locks `tenant_licenses` (`FOR UPDATE`) inside the caller's transaction. The platform add-user path is transactional. Postgres `40001`/`40P01`/`23505` → 409 ProblemDetails. Proven by a real-PostgreSQL concurrency test. |
| TM-P1-10 impersonation discarded | **Fixed** | The reason dialog opens the session in a new tab via `openImpersonationTab`. Logout of an impersonation session writes `impersonation.ended` to tenant and platform logs. |
| TM-P1-11 broken "Activate tenant" | **Fixed** | Removed. One lifecycle panel (Subscription tab) plus a danger zone. |
| TM-P2-01 wrong Max users field | **Fixed** | DTOs carry the enforced `SeatLimit`, `SeatsUsed`, `Modules`, `LicenseExpiresAt`, `SuspensionReason`. |
| TM-P2-03 allow-by-default modules | **Fixed (explicit)** | `ServiceOpsPlans.SharedControllers` lists deliberately shared controllers. A test fails for any unclassified tenant controller. |
| TM-P2-04 intake ignores suspension | **Fixed** | Intake webhooks reject inactive, suspended and read-only tenants. |
| TM-P2-07 invitation status invisible | **Fixed** | Platform users list shows Active / Invited / Expired / NotSent with expiry. The broken invite summary cards were removed. |
| TM-P2-11 deactivation mutates assets | **Fixed** | Side effect removed. |
| TM-P2-12 conflict errors as 500 | **Fixed** | See TM-P1-08. |
| TM-P2-13 destructive PUT | **Fixed** | Omitted seats, status and expiry keep their current values. |
| TM-P2-14 import technician role | **Fixed** | Maps to `Technician`. |
| TM-P2-15 duplicate lifecycle buttons | **Fixed** | List rows link to the tenant ("Manage"). Lifecycle actions exist only on the detail page and require reasons. |
| P3 mojibake / placeholder copy / "permanent delete" | **Fixed** | Encoding repaired. Stale placeholder copy removed. "Delete tenant" replaced by an honest "Deactivate tenant". |
| TM-P2-02 plan limits other than users | **Removed (owner decision 2026-09-26)** | Max assets, monthly work orders, Advanced reports and Client portal were dropped from plans, per-tenant overrides, the snapshot/usage APIs and the platform/tenant UI (migration `RemoveUnusedPlanLimits`). **Correction to §4/§6:** the asset and monthly work-order limits *were* enforced (AssetsController, WorkOrdersController ×3, IntakeController), and bit only tenants on the legacy Trial/Starter/Professional plans. Those call sites now just enforce the read-only/suspension check. Licensed users is the one per-tenant limit. The existing branch limit is unchanged. |
| Orphan `Ecosys.Platform` / `Ecosys.ServiceOps` projects | **Open** | Still copied by `backend/Dockerfile`. Remove together with a deploy verification. |

**Migration:** `20260924194936_TenantLicenseSuspensionReason` adds the column and backfills. Tenant-level Suspended/Inactive that the license didn't reflect is pushed onto the license first (never unblocking an operator block), worker suspensions are tagged `NonPayment`, and every tenant row is re-projected from its license. It was verified on PostgreSQL 18 against drifted sample rows.
