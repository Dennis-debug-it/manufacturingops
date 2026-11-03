# Ecosys ServiceOps — Vision vs. Built Audit Report
**Date:** 2026-06-03  
**Auditor:** Claude (read-only inspection, no changes made)  
**Scope:** Full codebase — frontend (`ecosys-serviceops-frontend/`), backend (`backend/src/`), migrations, tests, build artefacts  

---

## A. Executive Summary

**Overall Alignment Score: 72 / 100**

The product is substantially built and meaningfully aligned with the original ServiceOps vision. The core domain — multi-tenancy, work orders, clients, sites, assets, PM, materials, knowledge centre, technician execution, reports, and audit — is present both in the database schema and in the API layer. The frontend is a React/Vite/TypeScript SPA with a real sidebar, role-based routing, and a clean enterprise aesthetic that reflects the intended dark teal/lime brand direction.

However, several structural gaps reduce the score materially. The work order **create form is missing site and asset selection** — these fields exist on the backend type and the detail page but are absent from the creation modal, meaning every new work order starts without a linked site or asset. The **dashboard has a live EF Core query translation error** that fires on every load, producing a silent empty fallback rather than real metrics. The **frontend build fails** in the Linux sandbox environment due to a missing `@rolldown/binding-linux-x64-gnu` native binary (platform mismatch — this is a sandbox limitation, not necessarily a CI failure on Windows), while TypeScript type-checking passes cleanly. The **backend build log shows a "Build FAILED"** due to missing SDK workload resolver directories — again likely a Windows SDK path issue in the sandbox, not a code defect. Phase-2 finance/SLA modules are wired into the sidebar and routing, meaning they are accessible to tenanted users who have the `canViewFinance` permission, which is premature given the agreed simplified direction. A **stale Next.js skeleton frontend** exists at `backend/frontend/` and, while it has only one `.tsx` file, the presence of a full `node_modules` tree there is confusing dead weight.

---

## B. Vision vs. Built Matrix

| # | Area | Original Vision | What Exists in Code | UI Available? | Backend Available? | Status | Evidence |
|---|------|----------------|---------------------|---------------|--------------------|--------|----------|
| 1 | Multi-tenant foundation | Platform owner / command centre; tenant workspace; tenant admin; tenant user; roles & permissions | `Tenant`, `User`, `UserPermission`, `TenantLicense` entities; `PlatformOnlyRoute`, `AdminOnlyRoute`, `TenantOnlyRoute` guards; `PLATFORM_ROLES` / `TENANT_USER_ROLES` constants | Yes | Yes | **Complete** | `App.tsx` lines 120–200; `backend/src/Ecosys.Domain/Entities/Tenant.cs`, `User.cs`, `UserPermission.cs`; `Ecosys.Api/Controllers/PlatformTenantsController.cs` |
| 2 | Tenant branding | Tenant-level logo / name in sidebar | `tenantLogoUrl` passed to `AppShell` → `Sidebar`; logo rendered if present | Yes | Yes | **Complete** | `Sidebar.tsx` lines 52–57; `AppShell.tsx` line 140 |
| 3 | Client management | Create, list, view clients; no SLA blocking | `ClientsPage.tsx`, `ClientDetailPage.tsx`; `ClientsController.cs`; `Client` entity | Yes | Yes | **Partial** | `ClientsPage.tsx` line 387 — SLA definition dropdown present in create form (optional, not blocking); `ClientsController.cs` |
| 4 | Site management | Sites linked to clients; create/list/view | `SitesPage.tsx`, `SiteDetailPage.tsx`; `SitesController.cs`; `Site` entity with full address fields | Yes | Yes | **Complete** | `modules/sites/SitesPage.tsx`; `modules/clients/SiteDetailPage.tsx`; `Ecosys.Domain/Entities/Site.cs` |
| 5 | Asset management | Create individually; category/type; serial number; client/site mapping; PM frequency; last PM date | `AssetsPage.tsx` with create/edit form; `pmFrequencyValue`, `pmFrequencyUnit`, `lastPmDate` fields in form state; `AssetsController.cs`; `Asset` entity with all fields | Yes | Yes | **Complete** | `AssetsPage.tsx` lines 48–80; `Ecosys.Domain/Entities/Asset.cs`; `AssetsController.cs` |
| 6 | Bulk asset import (PM fields) | Import captures PM frequency, last service date, serial number | `ImportController.cs` — template columns include `LastPmServiceDate`, `PmFrequency`, `PmFrequencyUnit`; parser validates and maps all three; `importService.ts` calls `/api/import/assets` | Yes | Yes | **Complete** | `ImportController.cs` lines 55–57, 292–302, 367–372, 772–789 |
| 7 | Work order — core fields | Title, description, client, site, asset, work type, priority, due date, assignment group | WO create modal has: client, title, description, work type, checklist, assignment group, priority, due date. **Site and asset are absent from the create form** | Partial | Yes | **Partial** | `WorkOrdersPage.tsx` lines 35–65 — `siteId`, `assetId` not in `WorkOrderDraft` type; detail page shows site/asset and passes them on update via `buildWorkOrderUpdateInput()` |
| 8 | WO category / work type | New Installation, Corrective Maintenance, Preventive Maintenance | All three (plus Inspection/Assessment) in create form dropdown | Yes | Yes | **Complete** | `WorkOrdersPage.tsx` lines 500–518; `WorkOrderWorkTypeTests.cs` |
| 9 | WO assignment group | Dropdown in create and detail screens | Assignment group dropdown in create modal (conditional on `canManageAssignments`); shown in detail page assignment section | Yes | Yes | **Complete** | `WorkOrdersPage.tsx` lines 532–539; `WorkOrderDetailPage.tsx` line 99 |
| 10 | Technician assignment (multi) | Assign one or more technicians | `selectedTechnicianIds: string[]` in detail page; `WorkOrderTechnicianAssignment` entity; `WorkOrderAssignmentsController.cs` | Yes | Yes | **Complete** (detail only — not in create form) | `WorkOrderDetailPage.tsx` lines 97, 129; `Ecosys.Domain/Entities/WorkOrderTechnicianAssignment.cs` |
| 11 | Simplified WO lifecycle | New/Assigned → Start Work; In Progress → Hold / Complete; On Hold → Resume; Closed → no actions | `resolveWorkflowActions()` returns exactly these four actions and no others; `Reassign`, `Escalate`, `Change Priority` do not exist | Yes | Yes | **Complete** | `workOrderWorkflowUi.tsx` lines 122–138 |
| 12 | On Hold — reason + note | Required hold reason (Pending Client / Pending Materials / Other); optional note; resume returns to In Progress | `HoldReasonModal` component; `holdReasonOptions` enum; backend `WorkOrder.HoldReason`, `HoldNote`; resume endpoint | Yes | Yes | **Complete** | `workOrderWorkflowUi.tsx` — `holdReasonOptions`; `WorkOrdersController.cs` — `/hold` and `/resume` endpoints |
| 13 | WO progress notes | Multi-day job notes/comments/updates | `progressComment` state field; `workOrderService.saveExecutionNotes()`; `WorkOrderEvent` entity; `/events` and `/add-comment` endpoints | Yes | Yes | **Complete** | `WorkOrderDetailPage.tsx` lines 83–84; `WorkOrdersController.cs` line 470–475 |
| 14 | Materials used/requested on WO | Request, approve, issue, record usage; stock deducted | `MaterialsSection` in WO detail; `materialIssueService`; `MaterialIssuesController.cs`; `BranchMaterialStock`; `StockMovement` entity | Yes | Yes | **Partial** | `MaterialsPage.tsx` — issue flow present; stock movement logic in `StockTransfersController.cs`; approval step not clearly surfaced in UI flow |
| 15 | Evidence photos on WO | Upload/capture photos; flag for report | `EvidenceSection` component; photo upload in detail page; `WorkOrderPhotoEvidence` entity; `includeInReport` flag shown in UI | Yes | Yes | **Complete** | `workOrderWorkflowUi.tsx` lines 550, `EvidenceSection` component; `Ecosys.Domain/Entities/WorkOrderPhotoEvidence.cs` |
| 16 | Technician + client signatures | Capture both signatures; appear in report | `WorkOrderSignaturePad.tsx`; `WorkOrderSignature` entity; both signer name/role/signature fields in detail page | Yes | Yes | **Complete** | `WorkOrderDetailPage.tsx` lines 68–76; `WorkOrderSignaturePad.tsx`; `Ecosys.Domain/Entities/WorkOrderSignature.cs` |
| 17 | Service report generation | Generate and download PDF report | `ServiceReportSection` component with "Generate report" and "Download PDF" buttons; `WorkOrderExecutionController.cs`; `ReportsController.cs` | Yes | Yes | **Complete** | `workOrderWorkflowUi.tsx` lines 708–749; `WorkOrderExecutionController.cs` |
| 18 | Technician execution flow | View assigned jobs, start, progress, photos, materials, signatures, complete | `TechJobPage.tsx` (729 lines) — full flow: checklist, materials, photos, signatures, progress comment, complete | Yes | Yes | **Complete** | `modules/tech/TechJobPage.tsx`; `TechJobsPage.tsx`; `/tech/jobs/:id` routes |
| 19 | Preventive maintenance | PM templates, schedules, frequency, generate WO, link to assets | `PreventiveMaintenancePage.tsx`; `TemplatesPage.tsx`; `PmTemplatesController.cs`; `PreventiveMaintenanceController.cs` with `/generate-workorder` endpoint | Yes | Yes | **Complete** | `PreventiveMaintenancePage.tsx` lines 176–184; `PreventiveMaintenanceController.cs` line 113–184 |
| 20 | PM templates (Generator/HVAC/Solar) | Starter template packs for common industries | `SystemTemplatePack` entity; `PlatformStarterPacksController.cs`; `StarterPacksController.cs`; migration `20260525203635_AddStarterTemplatePacks` | Yes | Yes | **Complete** | `Ecosys.Domain/Entities/SystemTemplatePack.cs`; `PlatformStarterPacksController.cs` |
| 21 | Knowledge Centre | Upload manuals/documents; list articles; link to assets/clients; Q&A/chat over docs | `KnowledgeCentrePage.tsx` — article list, document upload, search; `KnowledgeDocumentsController.cs` with chunking/processing; `KipController.cs` for AI Q&A | Yes | Yes | **Partial** | `KnowledgeCentrePage.tsx` lines 34–57; `KnowledgeDocumentsController.cs`; `KipController.cs`; AI Q&A present but depth of implementation not fully verified |
| 22 | Reports and analytics | WO performance, technician productivity, asset reliability, PM compliance | `ReportsPage.tsx` with four tabs; `ReportsController.cs`; `reportService.ts` | Yes | Yes | **Complete** | `modules/reports/ReportsPage.tsx`; `Ecosys.Api/Controllers/ReportsController.cs` |
| 23 | Audit trail | Status transitions, assignment changes, materials, key actions logged | `WorkOrderEvent` entity; `AuditLog` entity; `auditLogService.LogAsync()` called on create/update/status transitions; `AuditLogsController.cs` | Yes | Yes | **Complete** | `WorkOrdersController.cs` lines 261, 389, 556, 597; `Ecosys.Domain/Entities/AuditLog.cs` |
| 24 | Tenant/company settings | Admin settings: users, branches, permissions, asset categories, email templates, data import | `SettingsPage.tsx` + child pages: `UsersPage.tsx`, `BranchesPage.tsx`, `PermissionsPage.tsx`, `AssetCategoriesSettingsPage.tsx`, `EmailTemplatesSettingsPage.tsx`, `DataImportSettingsPage.tsx` | Yes | Yes | **Complete** | `modules/settings/`; `backend/src/Ecosys.Api/Controllers/SettingsController.cs` |
| 25 | Platform Command Centre | Owner oversight: tenants, users, licenses, reports, audit logs, settings | `/platform` routes: `PlatformTenantsPage`, `PlatformUsersPage`, `PlatformAuditLogsPage`, `PlatformReportsPage`, `PlatformSettingsPage` | Yes | Yes | **Complete** | `App.tsx` lines 418–436; `modules/platform-v2/`; `PlatformController.cs` |
| 26 | Dashboard | Operational metrics for daily use | `DashboardPage.tsx`; `DashboardController.cs` with open/closed/overdue WOs, asset count, client count, low stock, assignments by group, technician workload | Yes | Yes | **Partial** | Dashboard controller has a live EF query translation error (line 66 in run log) — falls back to zero-count summary; `BuildSummaryAsync()` uses `.ToList()` in-memory after the fix but the error is suppressed silently |
| 27 | SLA / Contract / Billing | Phase 2 — must NOT block Phase 1 | `SLAManagementPage.tsx`; `SlaDefinitionsController.cs`; `TenantFinancePage.tsx` with `TenantFinanceOverviewPage`, `TenantPreferentialRatesPage`, `TenantFinanceApprovalsPage`, `TenantBillableWorkPage`; Finance route wired into sidebar for `canViewFinance` users | Yes (exposed in nav) | Yes | **Wrong direction** | `App.tsx` line 91 — Finance in `routeCatalog` for all `TENANT_USER_ROLES`; `modules/tenant-finance/`; `modules/sla/SLAManagementPage.tsx` |

---

## C. Critical Gaps (P0 / P1)

### P0 — Blocking usability

**C1. Work order create form is missing Site and Asset selection.**  
Every work order created via the UI starts with no site and no asset. The `WorkOrderDraft` type (`WorkOrdersPage.tsx` lines 35–65) does not include `siteId` or `assetId`. The detail page receives and displays these fields correctly (read from backend), and the update path preserves them via `buildWorkOrderUpdateInput()`. But because creation has no site/asset picker, users must go to the detail page and manually link them post-creation — a broken two-step UX that is not guided. This is a clear regression against the vision.

**Evidence:** `WorkOrdersPage.tsx` — `WorkOrderDraft` type, create modal fields (lines 468–580). Contrast with `WorkOrderDetailPage.tsx` line 1460–1464 where `siteId`, `systemId`, `assetId` are mapped on update.

**C2. Dashboard silently fails with a live EF Core query translation error.**  
`DashboardController.GetSummary()` catches the exception and returns `DashboardSummaryDto.Empty` — so the dashboard loads but shows all zeroes. The underlying error is an `InvalidOperationException` from EF Core: a `.OrderByDescending(x => x.Count)` on a grouped in-memory projection after a `ToList()` boundary was introduced, but a residual query path still attempts to translate it server-side.

**Evidence:** `backend/ecosys-api.stdout.log` — live exception stack trace ending at `DashboardController.cs:line 66`. The fix is in `BuildSummaryAsync()` which already materializes to `.ToList()` in some paths — one grouping/ordering call still needs to be evaluated in-memory.

---

### P1 — Significant functional gaps

**C3. Technician assignment not available in the create form.**  
Assignment group can be selected on create, but individual technician(s) cannot. Technician assignment is only possible from the work order detail page. For teams that want to fully dispatch at creation time, this requires two navigations.

**Evidence:** `WorkOrdersPage.tsx` — no `technicianId` or `selectedTechnicianIds` in `WorkOrderDraft`; draft only has `assignmentGroupId`.

**C4. Materials approval step is not clearly surfaced in the UI.**  
The `MaterialsPage.tsx` implements stock movement (in/out), material issue against a work order, and return flows. However, an explicit "approve material request" step — which sits between request and issue — is present in the backend (`MaterialRequestsController.cs`) but there is no dedicated UI panel or notification path guiding approvers through it. A user requesting materials from a work order and an approver acting on it are not connected through a discoverable UI flow.

**Evidence:** `MaterialsPage.tsx` — `issueForm` state and `materialIssueService.create()` present; no UI path from WO detail → pending request → approval → issue visible in the work order detail sections.

---

## D. Partial / Incomplete Features

**D1. Dashboard — silent zero fallback hides real data.**  
The fix for the query translation error is straightforward (move `.OrderByDescending()` to after `.ToList()`), but until fixed every tenant sees a blank dashboard. This makes the product appear broken on first login.

**D2. Client creation exposes optional SLA dropdown.**  
The create/edit form for clients shows an SLA definition picker (`ClientsPage.tsx` line 387). It is not required and does not block saving, but it introduces Phase-2 jargon on a Phase-1 screen. The SLA field should be hidden or moved to an Advanced tab.

**D3. Knowledge Centre AI Q&A (KIP) — depth unverified.**  
`KipController.cs` exists and `KnowledgeDocumentsController.cs` processes documents with chunking (`IKnowledgeDocumentProcessingService`). The frontend has `KipButton` and `KipPanel` components used in `ReportsPage.tsx`. The full ask/response loop is present in code, but quality of chunking, embedding, and retrieval accuracy was not exercised in this read-only audit.

**D4. Work order detail — side/asset linkage after creation requires manual step.**  
Covered under C1 above. Beyond being a P0, there is also no inline UI prompt in the detail page that says "this work order has no site — add one now." The user must discover the edit flow themselves.

**D5. PM auto-scheduling not triggered automatically.**  
`PreventiveMaintenancePlan.autoSchedule` field exists and is stored, but no background job or hosted service was found that fires PM work order generation on schedule. The "Generate work order" action is manual only (`pmService.generateWorkOrder()`). `backend/src/Ecosys.Infrastructure/Hosted/` directory exists but was not inventoried in this pass — **mark as not fully verified**.

---

## E. Overbuilt or Wrong-Direction Features

**E1. Finance module exposed to all tenant users with `canViewFinance`.**  
Routes `/finance/*` are in the sidebar `routeCatalog` for `TENANT_USER_ROLES` (`App.tsx` line 91). Pages include `TenantFinanceOverviewPage`, `TenantPreferentialRatesPage`, `TenantFinanceApprovalsPage`, `TenantBillableWorkPage`. These are Phase-2 commercial features. They should not be in the tenant sidebar until Phase 2 is ready and the tenant has opted in. Currently any tenant admin who grants `canViewFinance` permission immediately sees billing, preferential rates, and approvals.

**Files:** `modules/tenant-finance/` (5 pages); `App.tsx` lines 393–399.

**E2. SLA Management page is accessible.**  
`SLAManagementPage.tsx` exists and is reachable at `/sla` — though it is not listed in the `routeCatalog` navigation (so no sidebar link), it is technically accessible if navigated directly. The `SlaDefinitionsController.cs` and `SlaDefinition`, `SlaRule` entities are active in the schema. SLA computation fields (`SlaResponseDeadline`, `SlaResolutionBreached`) appear in every work order row in the live query log. This is significant Phase-2 infrastructure running in Phase 1.

**Files:** `modules/sla/SLAManagementPage.tsx`; `Ecosys.Api/Controllers/SlaDefinitionsController.cs`; migration `20260513035629_AddPhase2CSlaEngine`.

**E3. Platform Finance (subscriptions, invoices, payments, expenses) is fully built.**  
`PlatformFinancePage.tsx` with five sub-pages is wired into platform routing. This is appropriate for platform owner use only, but the level of build-out is substantial and the code surface area introduces maintenance overhead for an MVP stage.

**Files:** `modules/platform-v2/PlatformFinancePages.tsx`; `PlatformFinanceController.cs`.

**E4. Stale Next.js skeleton at `backend/frontend/`.**  
A legacy Next.js project skeleton (`next@16.2.4`, `react@19.2.4`) sits at `backend/frontend/`. It contains only one meaningful `.tsx` file and a full `node_modules` tree. It has its own Playwright test at `backend/frontend/tests/e2e/workorders.spec.ts`. This is dead code. It contributes to confusion about which frontend is canonical and inflates repo size.

**Files:** `backend/frontend/` — entire directory.

---

## F. Broken UX / Workflow Issues

**F1. Work order create modal — no site or asset picker.**  
Already reported as C1. UX impact: dispatchers create work orders, navigate away, then must return to the detail view to add site and asset. No guidance is provided for this.

**F2. Dashboard shows all-zero metrics on every load.**  
Already reported as C2. UX impact: the main operational screen appears non-functional to any user who opens the dashboard. Confidence in the product is immediately damaged.

**F3. Desktop sidebar — present and correct.**  
The sidebar renders via `lg:flex lg:min-h-screen lg:w-64 lg:shrink-0` (`Sidebar.tsx` line 96). On desktop it is a persistent left panel. On mobile it is a slide-in overlay via `fixed inset-0 z-40`. This is correctly implemented.

**F4. Mobile scroll — AppShell and TechLayout.**  
`body.style.overflow = 'hidden'` is set when the mobile sidebar is open (`Sidebar.tsx` lines 42–46), and cleaned up on close. `cleanupBodyInteractivity()` and `dispatchUiReset()` are called on route change. The tech mobile layout uses `min-h-dvh` correctly. No scroll trap was found in code inspection, but the previously reported mobile scrolling issue may be context-dependent (e.g., inside signature pad or photo upload modals). **Recommend Playwright mobile test run to confirm.**

**F5. Work order detail page is large (1,479 lines) but not overcrowded.**  
The page uses collapsible accordion sections (`openSections` state, `SectionKey` type) so the user sees only what they expand. Default open sections are `jobDetails` and `assignment` — appropriate. The fixed mobile action bar (`position: fixed; bottom: 0`) is present for mobile use.

**F6. Form stability / page-jump while typing.**  
No `useEffect` dependency loops or uncontrolled input patterns were found in the work order create form or detail page. Input fields use controlled `onChange → setDraft()` or `setForm()` patterns throughout. `installInteractionDebugLogger()` is called in `AppRoutes` for debugging. No known cause of form disappearing was found in code — the previously reported issue may have been fixed or was environment-specific.

**F7. New user creation sends a reset-style invite link — expected, not a bug.**  
`UsersController.cs` sends a `PasswordResetLinkAsync` as the invite mechanism (`RequiresInvite()` → `SendPasswordResetLinkAsync()`). The frontend `UsersPage.tsx` confirms: "An account setup link has been sent to their email." This is the intentional design (invite-to-set-password flow). Not broken, but the wording "reset link" in some log messages is misleading. The UI copy is correct.

**F8. WO list page — technician assignment in create form.**  
Already reported as C3. Assignment group is in the create form; individual technician is not. A newly created PM work order for example has no technician until someone opens the detail and assigns.

---

## G. Test Results

### G1. Frontend TypeScript Check
```
Command: cd ecosys-serviceops-frontend && npx tsc --noEmit
Result:  PASS — no output (zero errors)
Notes:   All TypeScript types check cleanly. The missing siteId/assetId in WorkOrderDraft is not a type error because the backend accepts null for those fields.
```

### G2. Frontend Build (Vite/Rolldown)
```
Command: cd ecosys-serviceops-frontend && npm run build
Result:  FAIL
Error:   Cannot find module '@rolldown/binding-linux-x64-gnu'
Cause:   Rolldown (Vite 6 bundler) requires a platform-native binary. The node_modules were installed on Windows and are missing the Linux x64 binary. This is a sandbox environment limitation, NOT a code defect. The existing dist/ folder in the repo indicates the build was successful in its native environment.
Risk:    Low for production — likely passes on Windows CI. Would fail on any Linux CI runner unless node_modules are reinstalled.
```

### G3. Backend Build (.NET)
```
Command: dotnet build (not available in sandbox — inferred from build.log)
Result:  Build FAILED (per build.log and dotnet-build.log)
Error:   MSB4276: SDK resolver failed to resolve "Microsoft.NET.SDK.WorkloadAutoImportPropsLocator" — directory did not exist
Cause:   Missing .NET 10 SDK workload manifests in the sandbox. The path referenced is C:\Program Files\dotnet\sdk\10.0.203\Sdks\... which is the Windows host, not the Linux sandbox. Zero compilation errors reported in the log — "0 Error(s), 0 Warning(s)" before the SDK resolver failure. This is a sandbox/environment issue.
Risk:    Low for production — runtime logs show backend is running successfully (EF queries executing, API responding).
```

### G4. Backend Runtime (from log evidence)
```
Status:  Running (evidence from ecosys-api.stdout.log)
Active EF queries observed executing against PostgreSQL for work orders, clients, sites, assets.
Known runtime error: DashboardController.cs line 66 — EF Core query translation failure.
  InvalidOperationException: LINQ expression '...OrderByDescending(x => x.Count)' could not be translated.
  Fallback: Returns DashboardSummaryDto.Empty (all zeros) — error is caught and suppressed.
```

### G5. Playwright E2E Tests
```
Status:  Test files exist, not executed in this audit pass (requires live backend + browser).
Coverage found:
  - tests/e2e/client-site-asset-workorder.spec.ts — full CSAWO creation flow
  - tests/e2e/auth-smoke.spec.ts
  - tests/e2e/mobile-technician.spec.ts
  - tests/e2e/knowledge-centre.spec.ts
  - tests/e2e/core-reports.spec.ts
  - tests/e2e/audit-communication.spec.ts
  - tests/backend-smoke.spec.ts
  And 12 others
Previous run artefacts in artifacts/playwright-inline-api.out.log show backend was responding to API calls correctly during last test run.
```

### G6. Backend Unit Tests
```
Location: backend/tests/Ecosys.Tests/
Test files (18): WorkOrderLifecycleStateTests.cs, WorkOrderExecutionFlowTests.cs,
  WorkOrderPhase3Tests.cs, TenantIsolationTests.cs, PreventiveMaintenanceFlowTests.cs,
  PmTemplateServiceTests.cs, ImportControllerTests.cs, KnowledgeCentreFlowTests.cs,
  TechnicianJobsControllerTests.cs, SecurityHardeningTests.cs, and others.
Status: Not executed in this audit pass (no dotnet in sandbox).
Coverage appears good for core lifecycle, tenant isolation, PM, imports, and security.
```

---

## H. Recommended Fix Plan

### Phase 1 — Restore Dashboard and Unblock Daily Use
**Priority:** Immediate (P0)

**Files affected:**
- `backend/src/Ecosys.Api/Controllers/DashboardController.cs` — lines ~130–145 (`workOrdersByGroup` and `technicianWorkload` LINQ groupings)

**Fix:** Move the `.GroupBy(...).Select(...).OrderByDescending(...)` chains to operate on the already-materialised `accessibleAssignments` and `accessibleTechnicianAssignments` lists (they are already `List<T>` from `.ToListAsync()`). The error is that a projection referencing a navigation property (`x.AssignmentGroup?.Name`) is still being attempted as SQL. This is a one-line fix per grouping.

**Acceptance criteria:** Dashboard loads and shows non-zero counts when work orders exist for the tenant.

**Risk:** Low — no schema change, no migration, isolated to one method.

**Tests to run:** `WorkOrderLifecycleStateTests.cs`; Playwright `backend-smoke.spec.ts`.

---

### Phase 2 — Fix Work Order Create Form (Site + Asset + Technician)
**Priority:** P0/P1

**Files affected:**
- `ecosys-serviceops-frontend/src/modules/work-orders/WorkOrdersPage.tsx`

**Fix:**
1. Add `siteId: string` and `assetId: string` to `WorkOrderDraft` type (lines 35–65).
2. Add a cascading site picker (filtered by selected client) and asset picker (filtered by selected site) in the create modal — after the client dropdown.
3. Add a `technicianIds: string[]` field to `WorkOrderDraft` and a multi-select (or searchable list) of technicians in the create modal — conditional on `canManageAssignments`.

**Acceptance criteria:**
- A new work order can be created with a site and asset already linked.
- A new work order can be created with at least one technician pre-assigned.
- Work orders list shows site and asset columns populated from creation.

**Risk:** Medium — requires fetching sites (filtered by clientId) and assets (filtered by siteId) as dependent dropdowns during creation. Requires site/asset service calls in the create modal.

**Tests to run:** `client-site-asset-workorder.spec.ts`; TypeScript check.

---

### Phase 3 — Hide Phase-2 Finance/SLA from Phase-1 UI
**Priority:** P1

**Files affected:**
- `ecosys-serviceops-frontend/src/App.tsx`
- `ecosys-serviceops-frontend/src/modules/clients/ClientsPage.tsx`

**Fix:**
1. Remove the Finance entry from `routeCatalog` for `TENANT_USER_ROLES` (or gate it behind a feature flag / plan check).
2. Remove or hide the SLA dropdown from the client create/edit form (or move it to an "Advanced" collapsible section with a "Phase 2" label).
3. Optionally, add a `PHASE_2_ENABLED` constant or plan-based feature flag to control visibility of finance/SLA routes.

**Acceptance criteria:**
- Tenant users see no Finance link in the sidebar.
- Client creation does not mention SLA.
- Platform admin can still access platform-level finance.

**Risk:** Low — UI-only change, no schema or API changes required.

**Tests to run:** TypeScript check; `auth-smoke.spec.ts`.

---

### Phase 4 — Fix Assets, PM Auto-Scheduling Verification, Stale Repo Cleanup
**Priority:** P1 / housekeeping

**Files affected:**
- `backend/frontend/` — entire directory (deletion candidate)
- `backend/src/Ecosys.Infrastructure/Hosted/` — inspect for PM auto-scheduling job

**Fix:**
1. **Stale frontend:** Remove `backend/frontend/` from the repo. Confirm `backend/frontend/tests/` Playwright spec is either migrated or deleted.
2. **PM auto-scheduling:** Verify whether a hosted service in `Ecosys.Infrastructure/Hosted/` fires PM generation. If not present, document this as a known gap for Phase 2.

**Acceptance criteria:**
- `backend/frontend/` is absent from the repo.
- PM auto-scheduling status is documented (exists or planned).

**Risk:** Low for deletion of stale directory (no active imports reference it). Medium for hosted service changes.

---

### Phase 5 — Materials Approval UI, Knowledge Centre Depth, Reports, and Test Stability
**Priority:** P2

**Files affected:**
- `ecosys-serviceops-frontend/src/modules/work-orders/WorkOrderDetailPage.tsx` (materials section)
- `ecosys-serviceops-frontend/src/modules/materials/MaterialsPage.tsx`
- `ecosys-serviceops-frontend/src/modules/knowledge/KnowledgeCentrePage.tsx`

**Fix:**
1. **Materials approval:** Add a visible "Pending approval" state to the materials section in the WO detail page, with an approve/reject action available to users with `canManageInventory` or equivalent permission.
2. **Knowledge Centre:** Validate that AI Q&A (`KipController`) returns accurate results for uploaded PDFs. If chunking quality is poor, note for Phase 2.
3. **Test suite:** Run Playwright suite against a live staging environment to establish a green baseline. Fix any flaky tests.

**Acceptance criteria:**
- Materials requested on a WO are visible in the WO detail with their approval status.
- Knowledge Centre document upload, list, and basic search work end-to-end.
- Playwright suite passes with >80% of tests green.

**Risk:** Medium for materials approval (requires UI state management change). Low for Knowledge Centre verification. High for test stability (depends on environment).

---

## I. Do Not Touch List

The following areas must not be modified without an explicit decision and confirmation from the product owner:

| Area | Reason |
|------|--------|
| `Ecosys.Infrastructure/Security/` — auth, sessions, password hashing, lockout | Tenant isolation and security hardening. Any change risks authentication breakage. |
| `Ecosys.Infrastructure/Migrations/AppDb/` — all existing migration files | Migrations are immutable once applied to any environment. Schema changes must come through new migrations only. |
| `Ecosys.Domain/Entities/Tenant.cs`, `User.cs`, `TenantLicense.cs` | Core multi-tenancy foundation. Changes here affect every tenant. |
| `modules/tenant-finance/` — all finance pages | Phase-2 scope. Should be hidden from UI but not deleted — will be activated in Phase 2. |
| `modules/sla/SLAManagementPage.tsx` and `SlaDefinitionsController.cs` | Phase-2 scope. Backend SLA computation fields are already in the schema — removing them would require a destructive migration. Leave in place, hide from navigation. |
| `PlatformFinanceController.cs` and `modules/platform-v2/PlatformFinancePages.tsx` | Platform owner billing infrastructure. Do not modify until Phase 2 commercial flows are confirmed. |
| `backend/src/Ecosys.Infrastructure/Hosted/` | Background job infrastructure — changes here affect data integrity. |
| Any `*Controller.cs` handling auth (`AuthController.cs`) | Login, token refresh, session heartbeat — zero-tolerance for regressions. |
| `WorkOrderAssignmentsController.cs` and `WorkOrderTechnicianAssignment` entity | Multi-technician assignment model. Changes here could break lead/secondary technician logic. |
| Tenant branding (`logoUrl`, `tenantName` in `AppShell`) | Tenant-specific customisation — leave as-is. |

---

*End of audit report. No changes were made to the codebase during this inspection.*
