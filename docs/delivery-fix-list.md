# Delivery Fix List

## Must Fix Before Pilot
- [x] Repair the public workspace-request / lead submission flow
  - Area: Backend onboarding
  - Root cause found: ASP.NET record validation metadata on `CreatePlatformLeadRequest` was attached to properties instead of constructor parameters, causing a runtime `500` before controller logic ran.
  - Fix applied: Updated record validation attributes, added backend regression coverage, and re-verified live `POST /api/public/leads` locally.
  - Files touched: `backend/src/Ecosys.Api/Controllers/PlatformLeadsController.cs`, `backend/tests/Ecosys.Tests/PlatformLeadFlowTests.cs`
  - Verification completed: `dotnet test backend/tests/Ecosys.Tests/Ecosys.Tests.csproj`, live `Invoke-RestMethod` POST to `http://localhost:5072/api/public/leads`

- [x] Stabilize SLA enforcement startup query
  - Area: Backend runtime stability
  - Problem: The SLA background worker failed on startup because EF Core could not translate `string.Equals(..., StringComparison.OrdinalIgnoreCase)` in the work-order query.
  - Fix applied: Rewrote the worker filter to use a translatable status exclusion query and verified the worker starts without throwing.
  - Files touched: `backend/src/Ecosys.Infrastructure/Services/SlaServices.cs`
  - Verification completed: local API startup logs now show the SLA worker query executing successfully against `work_orders`

- [ ] Stabilize tenant signup/login browser flow
  - Area: Full stack auth/runtime
  - Problem: Tenant-targeted Playwright runs failed before protected-page checks could execute.
  - Expected fix: Make tenant auto-signup/login reliable from the browser and confirm dashboard/settings load.
  - Files likely involved: `ecosys-serviceops-frontend/tests/e2e/helpers/auth.ts`, `ecosys-serviceops-frontend/src/lib/api.ts`, `backend/src/Ecosys.Api/Program.cs`, `backend/src/Ecosys.Api/Controllers/AuthController.cs`
  - Test required: Rerun tenant smoke, responsive, copy, and email-intake Playwright specs

- [ ] Clear the frontend lint gate
  - Area: Frontend quality
  - Problem: `npm run lint` fails with 34 errors across hooks, effect state updates, refresh boundaries, and purity rules.
  - Expected fix: Remove lint violations or align config intentionally; the pilot branch should be lint-clean.
  - Files likely involved: `ecosys-serviceops-frontend/src/App.tsx`, `src/auth/AuthContext.tsx`, `src/components/layout/AppShell.tsx`, `src/components/ui/DataTable.tsx`, `src/hooks/useAsyncData.ts`, `src/modules/platform-v2/*`, `src/modules/work-orders/*`, `src/pages/platform/settings/components/*`
  - Test required: `npm run lint`

- [ ] Remove placeholder-only tenant detail sections or implement them
  - Area: Platform tenants UX
  - Problem: Branding, users, modules, numbering, templates, audit trail, and danger zone still show follow-up-sprint placeholders.
  - Expected fix: Either wire these sections properly or hide them for pilot to avoid false promises.
  - Files likely involved: `ecosys-serviceops-frontend/src/modules/platform-v2/PlatformTenantsPage.tsx`
  - Test required: Platform tenant detail smoke and manual UX review

- [ ] Add tenant site management or explicitly cut it from pilot scope
  - Area: Contractor operations
  - Problem: Backend site support exists, but no tenant route/page was found.
  - Expected fix: Deliver a real tenant sites page with API wiring, or remove site-dependent promises from work-order flows and pilot docs.
  - Files likely involved: `backend/src/Ecosys.Api/Controllers/SitesController.cs`, `ecosys-serviceops-frontend/src/App.tsx`, `ecosys-serviceops-frontend/src/services/siteService.ts`, new tenant module/page files
  - Test required: CRUD smoke for sites and work-order site linkage validation

- [ ] Hide or disable unsupported Email API mode
  - Area: Email setup
  - Problem: UI offers Email API delivery, but backend throws `NotSupportedException`.
  - Expected fix: Remove API mode from pilot UI or implement it fully.
  - Files likely involved: `backend/src/Ecosys.Infrastructure/Integrations/SmtpEmailSender.cs`, `ecosys-serviceops-frontend/src/modules/settings/AdminSettingsPages.tsx`, `ecosys-serviceops-frontend/src/pages/platform/settings/components/EmailNotificationSettingsPanel.tsx`
  - Test required: Email settings save/test flow with SMTP-only pilot profile

- [ ] Validate real SMTP delivery end-to-end
  - Area: Notifications
  - Problem: Queue/log code exists, but real delivery was not proven in this audit.
  - Expected fix: Test invite, resend credentials, password reset, and work-order notifications with a real mailbox.
  - Files likely involved: `backend/src/Ecosys.Infrastructure/Integrations/SmtpEmailSender.cs`, `backend/src/Ecosys.Infrastructure/Services/EmailOutboxServices.cs`, `backend/src/Ecosys.Infrastructure/Services/UserCredentialDeliveryService.cs`
  - Test required: Real SMTP smoke run plus log verification

- [ ] Remove internal/commercial wording from tenant-facing UI
  - Area: UX / copy
  - Problem: Terms such as `SLA plan` appear directly in tenant flows.
  - Progress update: The client drawer label in `ClientsPage` now uses `Response targets`; a broader copy sweep is still required across tenant-facing screens.
  - Files touched so far: `ecosys-serviceops-frontend/src/modules/clients/ClientsPage.tsx`
  - Remaining verification: Customer-facing copy audit spec and manual content review

- [ ] Fix text encoding issues
  - Area: Frontend polish
  - Problem: Visible artifacts like `Â·` and `tenantâ€™s` make the product feel broken.
  - Expected fix: Normalize file encoding and replace corrupted strings.
  - Files likely involved: `ecosys-serviceops-frontend/src/modules/platform-v2/PlatformTenantsPage.tsx` and any affected JSX/TSX files
  - Test required: Manual UI pass and copy audit

- [ ] Verify migration state against the live schema
  - Area: Database
  - Problem: Migration history is split and risky; runtime behavior suggests schema/model mismatch is possible.
  - Expected fix: Confirm all intended migrations are applied cleanly, especially around audit logs and newer AppDb migrations.
  - Files likely involved: `backend/src/Ecosys.Infrastructure/Migrations/`, `backend/src/Ecosys.Infrastructure/Migrations/AppDb/`, `backend/src/Ecosys.Infrastructure/Data/AppDbContext.cs`
  - Test required: Clean database migration run and smoke retest

## Should Fix During Pilot
- [ ] Harden Command Centre overview/dashboard test hooks and consistency
- [ ] Add feature-level end-to-end coverage for work-order creation, assignment, arrival/departure, and completion
- [ ] Add explicit empty/loading/error states anywhere still relying on implicit defaults
- [ ] Tighten mobile layout behavior for tenant workspace pages after tenant runtime is stable
- [ ] Review platform role descriptions for clarity and consistency
- [ ] Improve reporting usefulness beyond summary cards and basic tables
- [ ] Reduce settings-page density where the UI still feels too technical

## Can Defer
- [ ] Deeper platform finance workflows beyond light billing mapping
- [ ] CRM-like enrichment beyond simple lead capture and follow-up
- [ ] Factory-specific mode switching and terminology abstraction if contractor pilot ships first
- [ ] Non-core assistant/KIP experiences
