# Test Suite Baseline — 2026-06-03

## Summary

The Playwright E2E suite cannot be executed from the Linux sandbox (no live backend, no
browser runtime available). This document records the suite inventory, last known run
evidence, and the steps required to establish a green baseline.

---

## Backend unit tests

| File | Area covered |
|------|--------------|
| `WorkOrderLifecycleStateTests.cs` | Status transitions, hold/resume/complete |
| `WorkOrderExecutionFlowTests.cs` | Evidence, signatures, materials, service report |
| `WorkOrderPhase3Tests.cs` | Assignment, multi-technician, lead selection |
| `TenantIsolationTests.cs` | Cross-tenant data leakage prevention |
| `PreventiveMaintenanceFlowTests.cs` | PM plan creation, WO generation |
| `PmTemplateServiceTests.cs` | Template CRUD, versioning, checklist copy |
| `ImportControllerTests.cs` | CSV import: clients, assets (PM fields), users |
| `KnowledgeCentreFlowTests.cs` | Document upload, chunk processing, article CRUD |
| `TechnicianJobsControllerTests.cs` | Tech job list, start, checklist, complete |
| `SecurityHardeningTests.cs` | Auth, lockout, session expiry |
| `WorkOrderWorkTypeTests.cs` | New Installation, PM, Corrective, Inspection |
| `TemplateWorkflowTests.cs` | Starter packs, template installation |
| + 6 others | Platform leads, reports, email outbox, phase 2 governance |

**To run:** `dotnet test backend/tests/Ecosys.Tests/`  
**Expected:** All pass. No known failures in code as of this audit.

---

## Playwright E2E tests (frontend)

| Spec file | Coverage |
|-----------|----------|
| `auth-smoke.spec.ts` | Login, logout, session |
| `auth-password-flows.spec.ts` | Forgot password, reset, invite link |
| `auth-clickability.spec.ts` | All nav items clickable post-login |
| `auth-tenant-onboarding.spec.ts` | New tenant signup → first login |
| `client-site-asset-workorder.spec.ts` | Full CSAWO creation flow via API helpers |
| `client-tenant-details.spec.ts` | Client detail, site list, asset list |
| `mobile-technician.spec.ts` | Tech job list, start, checklist, complete (mobile viewport) |
| `knowledge-centre.spec.ts` | Upload document, list, search |
| `core-reports.spec.ts` | WO performance, technician, PM compliance tabs |
| `audit-communication.spec.ts` | Audit log entries, email log |
| `command-centre.spec.ts` | Platform tenant list, licenses, audit logs |
| `email-intake.spec.ts` | Email → work order intake protocol |
| `email-to-workorder.spec.ts` | Full email-to-WO flow |
| `get-started.spec.ts` | Signup page and onboarding |
| `platform-email-notifications.spec.ts` | Platform email governance |
| `platform-real-email.spec.ts` | Real email delivery verification |
| `customer-facing-copy.spec.ts` | Copy/labels visible to clients |
| `backend-smoke.spec.ts` | Backend health, key API endpoints responding |

**To run:**
```bash
# From ecosys-serviceops-frontend/
npx playwright install --with-deps
npx playwright test --reporter=list
```

**Requirements:**
- Backend running on `http://localhost:5000` (or configured `E2E_API_URL`)
- Seeded test tenant with known credentials (see `tests/e2e/helpers/auth.ts`)
- PostgreSQL accessible

---

## Known issues to resolve before baseline

1. **`backend-smoke.spec.ts`** — previously passed (evidence in `artifacts/playwright-inline-api.out.log`
   showing live EF queries against backend). Should pass once dashboard query bug is fixed.

2. **`client-site-asset-workorder.spec.ts`** — tests the full create flow. Now that the WO
   create form accepts `siteId` and `assetId`, this spec should be updated to assert those
   fields are persisted (currently it creates via API helpers, not the UI form).

3. **`mobile-technician.spec.ts`** — mobile viewport tests. Verify no scroll trap regression
   after the AppShell `cleanupBodyInteractivity()` calls.

4. **Deleted stale spec** (`backend/frontend/tests/e2e/workorders.spec.ts`) — targeted the
   old Next.js app at `/serviceops/workorders` with mocked API routes and `data-testid`
   values that do not exist in the current app. Removed as part of Phase 4 cleanup
   (was blocked by NTFS permissions; use `CLEANUP_MANUAL_STEPS.ps1` to delete the file).

---

## Acceptance target

>80 % of Playwright specs green on a clean staging environment with seeded data.
Backend unit tests: 100% pass required before any deploy.
