# Ecosys ServiceOps Frontend — Build Plan

Source: `FRONTEND_V5_AUDIT_REPORT.md` (audited 2026-07-02) + direct inspection of `ecosys-serviceops-frontend/src` and `backend/src/Ecosys.Api/Controllers` (67 controllers) on 2026-07-02.

Conventions used throughout this document:
- All frontend paths are relative to `ecosys-serviceops-frontend/`.
- All backend paths are relative to `backend/src/Ecosys.Api/Controllers/`.
- Complexity: **S** = <0.5 day, **M** = 0.5–1.5 days, **L** = 2–4 days, **XL** = >4 days / needs splitting further once picked up.
- "DoD" = the per-module Definition of Done in §2. Every task inherits it; only task-specific deltas are called out under "AC" (acceptance criteria).
- 🔒 marks a task that is **backend-blocked**: the endpoint it needs does not exist in the current 67-controller snapshot. These are flagged explicitly wherever found — see §12 for the full list and recommended handling.

---

## 1. Current State Summary (from audit)

- **~12–15% of the V5 spec is complete.** Foundation/shell/design-system layer (~80%) and routing skeleton (~95%) are solid; actual feature logic (~3–5%) is not.
- **Real, working today:** login/session bootstrap (`AuthContext.tsx` against `POST /api/auth/login`, `GET /api/auth/me`), tenant vs. platform vs. portal shell routing, Work Orders list (`src/features/work-orders/WorkOrdersPage.tsx`, single `GET /api/workorders` call, client-side search + technician auto-filter), Work Order Detail Summary tab only (`src/features/work-orders/WorkOrderDetailPage.tsx`), Dashboard (`src/features/dashboard/DashboardPage.tsx`, single `GET /api/dashboard/summary` call, generic key dump).
- **~90 routes** render `createModulePage()` (`src/shared/pages/ModulePage.tsx`) — a title + search box + empty table, zero API calls, zero forms.
- **Built but zero-times used:** `FormDrawer.tsx`, `ConfirmDialog.tsx`, `MobileActionBar.tsx`, `usePermission.ts` (RBAC hook — fully written, never called outside its own file).
- **`src/shared/api/endpoints.ts`** defines 14 endpoint entries; only 6 are actually called anywhere (`auth.login`, `auth.me`, `auth.logout`, `dashboard.summary`, `workOrders.list`, `workOrders.detail`). The backend exposes **67 controllers** with several hundred routes — the bottleneck is frontend wiring, not backend availability, for most modules.
- **PWA is non-functional in practice:** `vite.config.ts` has no `strategies: 'injectManifest'`, so the hand-written `src/pwa/sw.ts` never ships (Workbox's `generateSW` output replaces it). The IndexedDB schema (`src/pwa/offline/db.ts`) is well-designed but `sync_queue` has no writer.
- **Two non-functional customer-facing forms:** Request Demo (`src/features/public/RequestDemoPage.tsx:33-35` navigates without calling an API) and Portal magic-link login (`src/portal/auth/PortalLoginPage.tsx:25-27` navigates without calling an API).
- **Backend gaps discovered during this planning pass** (not previously flagged in the audit, confirmed by reading all 67 controllers): there is **no Time & Attendance controller**, **no WhatsApp controller**, **no CRM Contacts/Activities/Tasks controller** (only `PlatformLeadsController` exists), **no Client Portal auth/data controller**, and several tenant Settings sub-pages (inventory settings, time-attendance settings, whatsapp, payments, portal visibility, executive-dashboard targets) have no corresponding backend endpoint. These are called out inline in §4–§7 and consolidated in §12.

---

## 2. Definition of Done (per module)

Every feature page/module built under this plan — list, detail, or settings — must satisfy all seven before being marked complete:

1. **Real API integration.** Every list/detail view is backed by a live backend call through `src/shared/api/apiClient.ts` (`api.get/post/put/patch/delete`) and a matching entry in `src/shared/api/endpoints.ts`. No hardcoded arrays, no `TODO` payloads, no `ModulePage` generic shell.
2. **All required tabs/views present.** Every tab/section named in the V5 route map (§3–§7 below) renders real content — not `<EmptyState>` placeholders — before the module is called done.
3. **All forms use `react-hook-form` + Zod.** Every create/edit form has a co-located `schemas.ts` (or inline `z.object` for trivial forms) wired via `@hookform/resolvers/zod`, with field-level error messages rendered.
4. **RBAC enforced via `usePermission`.** Every primary/destructive action button (`Create`, `Edit`, `Delete`, `Approve`, `Assign`, etc.) is wrapped in a `usePermission('<module>.<action>')` check that hides or disables the control. The `rolePermissions` map in `src/shared/hooks/usePermission.ts` must be extended with the specific action string before the button is wired (see Task P1-3).
5. **Loading, empty, and error states on every data fetch.** Use `<Spinner>` while `isLoading`, `<EmptyState>` (with a relevant `actionLabel` where a create action applies) when the result set is empty, and a distinct error state (`<EmptyState>` with `actionLabel="Reload"` or inline error banner) when `isError`. This applies to every `useQuery` call, not just the top-level page.
6. **Mobile layout works at 375px.** Verify with the browser preview tool at a 375×812 viewport: no horizontal scroll on the primary content, `DataTable` degrades to a scrollable/stacked layout or the page swaps to card rows, and any page with mutating actions exposes them via `<MobileActionBar>` below `md` breakpoint.
7. **Toast notifications on mutations.** Every `useMutation` triggers `toast.success(...)` on success and relies on the existing `apiClient.ts` response interceptor for error toasts (do not double-toast errors — the interceptor already calls `toast.error` on non-401 failures).

Additional standing rules:
- New feature code lives under `src/features/<module>/` (e.g. `src/features/clients/`), mirroring the `work-orders` folder structure: `<Module>Page.tsx`, `<Module>DetailPage.tsx`, `api.ts` (query/mutation hooks), `schemas.ts` (Zod), `components/` for module-local pieces. Do not add new logic into `ModulePage.tsx` — it is being retired module-by-module.
- Reuse `DataTable`, `StatusBadge`, `FormDrawer`, `ConfirmDialog`, `DetailLayout`, `SectionHeader`, `MobileActionBar`, `PageHeader`, `EmptyState`, `Spinner` from `src/shared/components/` — do not fork or duplicate them into feature folders.
- `queryKey` convention: `['<module>', ...params]` for lists, `['<module>', id]` for detail, matching the existing `['work-orders', id]` pattern in `WorkOrderDetailPage.tsx`.

---

## 3. Phase 1 — Foundation Gaps (tasks remaining)

Phase 1 must be fully complete (including RBAC wiring and the endpoint-file expansion) before Phase 2 work starts, because every later phase's tasks assume `usePermission` is live and `endpoints.ts` follows an established per-domain pattern.

### 3.1 Infrastructure (blockers for everything else)

| ID | Task | Files | Complexity | Blocks |
|---|---|---|---|---|
| P1-1 | **Expand `endpoints.ts` to the full backend surface.** Restructure into per-domain sub-objects (`clients`, `sites`, `assets`, `materials`, `materialRequests`, `materialIssues`, `stockTransfers`, `pm`, `pmTemplates`, `technicians`, `teamManagement`, `users`, `assignmentGroups`, `branches`, `contracts`, `financeInvoices`, `financeQuotations`, `financeCore`, `creditTerms`, `reports`, `scheduledReports`, `slaDefinitions`, `attachments`, `settings`, `advancedSettings`, `emailTemplates`, `platform*`) using the endpoint tables in §4–§7 of this document as the source list. Keep the existing `(id: string) =>` function pattern for parameterised routes. | `src/shared/api/endpoints.ts` | L | All P2–P5 tasks |
| P1-2 | **Establish the feature API-hook convention.** Create `src/features/work-orders/api.ts` extracting the existing inline `useQuery` calls from `WorkOrdersPage.tsx`/`WorkOrderDetailPage.tsx` into named hooks (`useWorkOrders(filters)`, `useWorkOrder(id)`, `useUpdateWorkOrder()`, etc.) using `@tanstack/react-query`. This becomes the template every later module's `api.ts` copies. | `src/features/work-orders/api.ts` (new), refactor `WorkOrdersPage.tsx`, `WorkOrderDetailPage.tsx` to consume it | M | All feature modules (pattern reference) |
| P1-3 | **Wire RBAC end-to-end.** (a) Extend `rolePermissions` in `usePermission.ts` with granular action strings per module (`work-orders.create`, `work-orders.complete`, `clients.create`, `clients.delete`, `contracts.approve`, `finance.invoices.create`, `settings.edit`, etc. — one string per mutation defined in §4–§7). (b) Replace `Sidebar.tsx`'s bespoke `canShowNavItem()` with `usePermission(item.permission)`. (c) Add `usePermission` checks to `Topbar.tsx` action affordances. (d) Document the convention (JSDoc comment block) so every subsequent task references it instead of re-deriving role logic. | `src/shared/hooks/usePermission.ts`, `src/shared/layout/Sidebar.tsx`, `src/shared/layout/Topbar.tsx` | M | DoD item 4 for every module |
| P1-4 | **Decide and implement the PWA service-worker strategy.** Set `strategies: 'injectManifest'` in the `VitePWA` config, add `precacheAndRoute(self.__WB_MANIFEST)` to `src/pwa/sw.ts`, and implement the `fetch` handler for offline GET caching of `work_orders`/`clients`/`sites`/`assets`/`parts` stores. If the team instead decides to drop the custom SW, delete `src/pwa/sw.ts` and remove `srcDir`/`filename` from the Vite config — either decision is acceptable, but the current silent-mismatch state is not. | `vite.config.ts`, `src/pwa/sw.ts` | M | P1-6 (sync queue processor needs a working SW for background sync) |
| P1-5 | **Consolidate online-status detection.** Delete `src/pwa/offline/hooks.ts#useOfflineStatus` (or `src/shared/hooks/useOnlineStatus.ts`, pick one) and repoint the other's callers. `Topbar.tsx:24-29` currently uses `useOnlineStatus`. | `src/shared/hooks/useOnlineStatus.ts`, `src/pwa/offline/hooks.ts`, `src/shared/layout/Topbar.tsx` | S | — |
| P1-6 | **Implement the `sync_queue` writer + processor.** Add a `enqueueSync(entry: SyncQueueEntry)` helper in `src/pwa/offline/db.ts` that any offline-capable mutation can call, and a processor (triggered on `online` event + on app load) that replays queued entries via `apiClient` and marks them `synced`. This has no real consumer until Work Order Execution/Evidence/Signatures (P1-11–P1-13) exist, so implement the writer/processor shape now but the first real caller is those tasks. | `src/pwa/offline/db.ts`, new `src/pwa/offline/syncProcessor.ts` | L | P1-11, P1-12, P1-13 (offline-capable execution actions) |

### 3.2 Work Orders (highest-value, most spec-detailed Phase 1 feature)

Backend: `WorkOrdersController` (`/api/workorders`), `WorkOrderExecutionController` (`/api/workorders/{id}/...`), `WorkOrderAssignmentsController` (`/api/workorders/{id}/...`), `WorkOrderBillingController` (`/api/work-orders/{id}/...` — **note the hyphen inconsistency vs. the other three controllers; verify against a live backend call before wiring, it is not a typo in this document**).

| ID | Task | Files | Endpoints | Complexity | Blocks |
|---|---|---|---|---|---|
| P1-7 | **Work Orders list filters.** Add Status/Priority/Client/Site/Technician/Due-date/Source filter controls (dropdowns + date range) above the existing search box, driven by `GET /api/workorders?status=&workType=&assignmentGroupId=&technicianId=&unassigned=&assignmentStatus=&search=&fromDate=&toDate=`. Keep the existing technician auto-filter behaviour for `technician` role. | `src/features/work-orders/WorkOrdersPage.tsx`, new `src/features/work-orders/WorkOrderFilters.tsx` | `GET /api/workorders` (query params) | M | — |
| P1-8 | **New Work Order `FormDrawer` + Zod.** Wire the currently-inert "New Work Order" button to open `FormDrawer` with a form (client/site/asset picker, work type, priority, description) validated by Zod, submitting `POST /api/workorders`. This is the pattern-establishing task for every other create-flow in the app — do this before replicating the pattern elsewhere (audit priority fix #4). | `src/features/work-orders/WorkOrderFormDrawer.tsx` (new), `src/features/work-orders/schemas.ts` (new), `WorkOrdersPage.tsx` | `POST /api/workorders` | M | P2 client/site create flows (pattern reference), P1-9 |
| P1-9 | **Report Queue tab.** Replace the `EmptyState` stub with a stat strip (counts by report status) and a filtered `DataTable` + batch actions (bulk approve/send), sourced from the same `GET /api/workorders` list filtered client-side by `reportStatus`, or a dedicated query param if the backend supports one — confirm via `WorkOrderExecutionController` `service-report` fields. | `src/features/work-orders/ReportQueueTab.tsx` (new) | `GET /api/workorders`, `GET /api/workorders/{id}/service-report` | M | — |
| P1-10 | 🔒 **WhatsApp Inbox tab.** Spec requires a WhatsApp Inbox tab on the Work Orders list — **no `WhatsAppController` or equivalent exists in the current backend.** Build the tab shell with `EmptyState` + a clear "pending backend integration" note rather than faking data; do not block other Work Orders tasks on this. Revisit once a backend endpoint exists (see §12). | `src/features/work-orders/WhatsAppInboxTab.tsx` (new, shell only) | 🔒 none available | S | — |
| P1-11 | **Timeline tab.** Real event/comment feed: `GET /api/workorders/{id}/events` for the event list, `POST /api/workorders/{id}/comments` for adding notes, rendered chronologically with actor + timestamp. | `src/features/work-orders/tabs/TimelineTab.tsx` (new) | `GET/POST .../events`, `.../comments` | M | — |
| P1-12 | **Assignment tab.** Assignment history table (`GET .../assignment-history`) plus reassign action (group picker → `POST .../assign-group`, technician picker → `POST .../assign-technicians`, combined → `POST .../reassign`) wrapped in `ConfirmDialog`. | `src/features/work-orders/tabs/AssignmentTab.tsx` (new) | `GET .../assignment-history`, `POST .../assign-group`, `.../assign-technicians`, `.../reassign` | L | — |
| P1-13 | **Work Execution tab.** Checklist rendering from `GET /api/workorders/{id}/checklist` (attach template via `POST .../attach-pm-template` if none attached), per-item completion via `PUT /api/workorders/{workOrderId}/checklist/{itemId}`, plus execution notes (`POST .../execution-notes`) and in-transit/pause/resume/close/cancel state actions. Offline-queue checklist completions via P1-6's `enqueueSync` when `navigator.onLine` is false. | `src/features/work-orders/tabs/ExecutionTab.tsx` (new) | `GET .../checklist`, `POST .../attach-pm-template`, `PUT .../checklist/{itemId}`, `POST .../execution-notes`, `.../in-transit`, `.../pause`, `.../resume`, `.../close`, `.../cancel` | XL | — |
| P1-14 | **Evidence tab.** Photo upload grid (multipart `POST .../photos`, max 5MB, jpg/png/webp per backend validation), caption/category edit (`PUT .../photos/{photoId}`), checklist-item evidence (`POST .../checklist/{itemId}/evidence`). Add client-side image compression before upload (audit flags this as entirely missing). Queue uploads offline via `sync_queue` when disconnected. | `src/features/work-orders/tabs/EvidenceTab.tsx` (new), new `src/shared/utils/imageCompression.ts` | `POST .../photos`, `PUT .../photos/{photoId}`, `POST .../checklist/{itemId}/evidence` | L | — |
| P1-15 | **Materials tab.** Parts search + usage recording (`POST .../materials-used`) against `MaterialsController` for the part picker (`GET /api/materials?search=`), plus a read view of billable time logs from `WorkOrderBillingController` (`GET /api/work-orders/{id}/time-logs`) if the role has `finance.view`. | `src/features/work-orders/tabs/MaterialsTab.tsx` (new) | `POST .../materials-used`, `GET /api/materials`, `GET /api/work-orders/{id}/time-logs` | L | — |
| P1-16 | **Signatures tab.** Signature pad (canvas → data URL) capturing technician/client signatures (`POST .../signatures`), list of captured signatures (`GET .../signatures`) with delete (`DELETE .../signatures/{signatureId}`), gated by `usePermission('work-orders.execute')`. | `src/features/work-orders/tabs/SignaturesTab.tsx` (new) | `POST/GET/DELETE .../signatures` | M | — |
| P1-17 | **Report tab.** Report lifecycle: preview (`GET .../service-report`), regenerate (`POST .../service-report/generate`), PDF download (`GET .../service-report/pdf`), status badge driven by real report status field, plus a "Complete Work Order" action (`POST .../complete`) with a follow-up-WO creation dialog. | `src/features/work-orders/tabs/ReportTab.tsx` (new) | `GET/POST .../service-report`, `GET .../service-report/pdf`, `POST .../complete` | L | — |
| P1-18 | **Billing sub-section** (embed in Materials or Report tab per final UX call). Billable items CRUD + auto-populate (`GET/POST/PUT/DELETE /api/work-orders/{id}/billable-items`, `POST .../billable-items/auto-populate`), billing status update (`PUT .../billing-status`). Gate behind `usePermission('finance.view')`. | `src/features/work-orders/tabs/BillingSection.tsx` (new) | `/api/work-orders/{id}/billable-items*`, `.../billing-status` | M | Phase 3 Finance (billable-work list surfaces these items) |

### 3.3 Dashboard

| ID | Task | Files | Endpoints | Complexity |
|---|---|---|---|---|
| P1-19 | **Rebuild Dashboard to the spec'd 8-stat strip + named widgets.** Replace the generic key-dump in `DashboardPage.tsx` with explicit stat cards (mapped from `DashboardSummaryDto` fields — WO counts, asset counts, contract counts, workload, low stock) and named widgets: Upcoming PM, Contracts Expiring, Overdue WOs, Billable Summary. WhatsApp alert widget is 🔒 backend-blocked (see P1-10) — build as a shell. | `src/features/dashboard/DashboardPage.tsx`, new `src/features/dashboard/widgets/*.tsx` | `GET /api/dashboard/summary?branchId=` | L |
| P1-20 | **Executive Dashboard `/dashboard/executive`.** Build a real page (currently a bare `ModulePage` shell) with period analysis and KPI strip. No dedicated backend endpoint confirmed beyond `DashboardController`/`ReportsController` — reuse `GET /api/dashboard/summary` plus `GET /api/reports/work-order-performance` for trend data until/unless a dedicated executive endpoint appears. | new `src/features/dashboard/ExecutiveDashboardPage.tsx`, update `router.tsx` to point at it instead of `lazyModulePage(...)` | `GET /api/dashboard/summary`, `GET /api/reports/work-order-performance` | L |

### 3.4 Auth-adjacent customer-facing forms

| ID | Task | Files | Endpoints | Complexity |
|---|---|---|---|---|
| P1-21 | **Wire Request Demo to a real lead.** Replace the no-op navigate in `RequestDemoPage.tsx:33-35` with `POST /api/public/leads` (anonymous, `PlatformLeadsController`), then navigate to `/demo-confirmation` only on success; show a toast/inline error on failure. | `src/features/public/RequestDemoPage.tsx` | `POST /api/public/leads` | S |
| P1-22 | 🔒 **Portal magic-link login.** `PortalLoginPage.tsx:25-27` needs a real magic-link send + verify flow. **No portal auth endpoint exists in the current backend** (`AuthController` only supports password login). Flag this to backend as a blocker; in the meantime, do not silently auto-navigate — show an honest "portal login is not yet available" state instead of the current fake success. See §12. | `src/portal/auth/PortalLoginPage.tsx` | 🔒 none available | S (shell fix only) |

---

## 4. Phase 2 — Operations Modules (task list)

Phase 2 cannot start meaningfully until P1-1 (`endpoints.ts` expansion), P1-2 (API hook convention), and P1-3 (RBAC wiring) land — every task below assumes those exist. Phase 2 itself should fully land (including Time & Attendance's backend-blocked shell) before Phase 3 starts, per the strict phase-gate requested.

### 4.1 Maintenance

Backend: `PreventiveMaintenanceController` (`/api/preventive-maintenance`), `PmTemplatesController` (`/api/pm/templates`).

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P2-1 | **PM Calendar `/maintenance/calendar`.** Calendar grid + list view toggle showing PM plans by due date, driven by `GET /api/preventive-maintenance?branchId=` and `GET /api/preventive-maintenance/due?branchId=`. | `src/features/maintenance/CalendarPage.tsx` (new), `api.ts` | `GET /api/preventive-maintenance`, `.../due` | Calendar and list view both present; due-soon items visually distinct | L |
| P2-2 | **PM Schedules list + 3-step wizard `/maintenance/schedules`, `/new`.** List via `GET /api/preventive-maintenance`; creation wizard (asset/site selection → frequency/checklist template → review) posts `POST /api/preventive-maintenance`. | `src/features/maintenance/SchedulesPage.tsx`, `SchedulesWizard.tsx` (new) | `GET/POST /api/preventive-maintenance` | 3-step wizard with back/next state preserved | L |
| P2-3 | **PM Schedule Detail `/maintenance/schedules/:id`.** Trigger config, assignment, and generated-WO history; edit via `PUT /api/preventive-maintenance/{id}`; manual trigger via `POST /api/preventive-maintenance/{id}/generate-workorder`. | `src/features/maintenance/ScheduleDetailPage.tsx` (new) | `PUT /api/preventive-maintenance/{id}`, `POST .../generate-workorder` | "Generate Work Order Now" button gated by `usePermission('maintenance.trigger')` | M |
| P2-4 | **PM Compliance `/maintenance/compliance`.** Compliance rates by client/asset-type/technician with sortable, colour-coded table and CSV export. | `src/features/maintenance/CompliancePage.tsx` (new) | `GET /api/reports/pm-compliance`, `.../export` | Sortable columns implemented (closes audit gap: `DataTable.sortable` flag is inert — see P8-x in §8) | M |
| P2-5 | **Engine Status `/maintenance/engine`.** Health banner + rule list for the maintenance generation engine. No dedicated "engine health" endpoint found; derive status from `GET /api/preventive-maintenance/due` (overdue count vs. total) as a proxy metric — confirm with backend before treating as authoritative. | `src/features/maintenance/EnginePage.tsx` (new) | `GET /api/preventive-maintenance/due` (proxy) | Banner clearly labels this as a derived/approximate signal, not a backend-reported health check | S |
| P2-6 | **PM Templates management** (used by P2-2's wizard and Settings `/settings/pm-templates`, build once and share). CRUD + duplicate/activate/deactivate. | `src/features/maintenance/pmTemplates/api.ts` (new, shared with P5-6) | `GET/POST/PUT /api/pm/templates`, `.../duplicate`, `.../activate`, `.../deactivate`, `DELETE` | Shared hook consumed by both `/maintenance` wizard and `/settings/pm-templates` | M |

### 4.2 Clients / Sites / Assets

Backend: `ClientsController` (`/api/clients`), `SitesController` (`/api/clients/{clientId}/sites`, `/api/sites/search`), `AssetsController` (`/api/assets`), `AssetCategoriesController` (`/api/asset-categories`), `SystemsController` (`/api/clients/{clientId}/sites/{siteId}/systems`, `/api/systems/{id}`).

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P2-7 | **Clients/Sites/Assets tri-tab switcher `/clients`, `/sites`, `/assets`.** Three real list pages sharing a tab switcher component, each with its own `DataTable`, filters, and "New" `FormDrawer`. | `src/features/clients/ClientsPage.tsx`, `src/features/sites/SitesPage.tsx`, `src/features/assets/AssetsPage.tsx` (new), shared `ModuleTabSwitcher.tsx` | `GET /api/clients?search=&status=`, `GET /api/sites/search?clientId=&q=&region=`, `GET /api/assets?branchId=&clientId=&siteId=&systemId=&categoryId=&search=&status=` | Each tab independently paginated/filterable | L |
| P2-8 | **Client create/edit `FormDrawer`.** Zod-validated form for `POST /api/clients` / `PUT /api/clients/{id}`; credit-status banner on the list row driven by `GET /api/clients/{id}/credit-summary`-style data (via `ClientsController.summary` or `CreditTermsController` client-credit-summary — confirm field source). | `src/features/clients/ClientFormDrawer.tsx`, `schemas.ts` | `POST/PUT /api/clients/{id}` | Deactivate action uses `ConfirmDialog`, calls `DELETE /api/clients/{id}` (soft-delete) | M |
| P2-9 | **Client Detail — 7 tabs `/clients/:id`.** Tabs: Overview, Contacts, Sites, Assets, Work Orders, Contracts, Documents. Overview from `GET /api/clients/{id}/summary`; Sites tab from `GET /api/clients/{clientId}/sites`; Contracts tab from `GET /api/clients/{clientId}/contracts` (Phase 3 dependency — build tab shell now, wire once Contracts module lands). | `src/features/clients/ClientDetailPage.tsx` (new), `tabs/*.tsx` | `GET /api/clients/{id}/summary`, `.../sites`, `GET /api/clients/{clientId}/contracts` | 7 tabs via `DetailLayout`, `?tab=` URL-synced (reuse existing pattern from `WorkOrderDetailPage`) | XL |
| P2-10 | **Site Detail — 5 tabs `/sites/:id`.** Summary, Systems, Assets, PM Schedules, Work Orders. `GET /api/clients/{clientId}/sites/{siteId}` for detail; Systems tab from `GET /api/clients/{clientId}/sites/{siteId}/systems`. | `src/features/sites/SiteDetailPage.tsx` (new) | `GET .../sites/{siteId}`, `GET .../systems` | 5 tabs present | L |
| P2-11 | **Asset Detail — 5 tabs `/assets/:id`.** Specifications, History, Open Work Orders, PM Schedules, Documents. Warranty badge computed from asset fields. | `src/features/assets/AssetDetailPage.tsx` (new) | `GET /api/assets/{id}`, `PUT /api/assets/{id}` | Warranty badge (`StatusBadge`) shows expiry-relative colour | L |
| P2-12 | **Client Setup Wizard `/clients/:id/sites/new`, `/sites/:id/assets/new`.** Multi-step: site creation → asset/system creation → contract linkage entry point. Reuses `FormDrawer` steps or a dedicated stepper. | `src/features/clients/ClientWizardPage.tsx` (new) | `POST /api/clients/{clientId}/sites`, `POST .../systems`, `POST /api/assets` | Wizard state persists across steps; cancel returns to Client Detail | L |
| P2-13 | **Asset Categories admin (feeds Asset create form fields).** CRUD for categories + custom fields, surfaced as a lookup in the Asset `FormDrawer`, not necessarily its own route (no dedicated `/asset-categories` route in `router.tsx` — build as a Settings-adjacent utility consumed by P2-11's form). | `src/features/assets/assetCategories/api.ts` (new) | `GET/POST/PUT/DELETE /api/asset-categories`, `.../fields` | Asset create form's "Category" field is populated from this list, not hardcoded | M |
| P2-14 | **Bulk import (Clients/Assets/Users/Branches/Materials).** Reuse `ImportController` (`GET /api/import/templates/{type}`, `POST /api/import/{type}/preview`, `POST /api/import/{type}/commit`) behind a single reusable `<BulkImportModal>` (there is a Playwright-referenced `BulkImportModal.tsx` in `frontend-backup/` worth mining for UX patterns — do not reuse its code directly since it targets the old API shape). | new `src/shared/components/BulkImportModal.tsx`, per-module wiring in Clients/Assets/Team/Inventory pages | `GET /api/import/templates/*`, `POST /api/import/*/preview`, `.../commit` | Preview step shows row-level validation errors before commit | L |

### 4.3 Inventory (9 sub-pages)

Backend: `MaterialsController` (`/api/materials`), `MaterialRequestsController` (`/api/material-requests`), `MaterialIssuesController` (`/api/material-issues`), `StockTransfersController` (`/api/stock-transfers`). No dedicated "Purchase Orders" or "Suppliers" controller was found in the 67-controller snapshot — flagged below.

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P2-15 | **Parts Catalogue `/inventory/parts`, `/new`, `/:id`.** List + create + 3-tab detail (Overview, Stock by Location, Movement History). | `src/features/inventory/parts/*.tsx` (new) | `GET/POST/PUT /api/materials`, `GET /api/materials/{id}`, `.../movements` | 3-tab detail present; movement history paginated | L |
| P2-16 | **Stock Levels `/inventory/stock` with location-view toggle.** Table/grid toggle between "by part" and "by location" views. | `src/features/inventory/StockPage.tsx` (new) | `GET /api/materials?branchId=` | Toggle persists in URL query param | M |
| P2-17 | **Locations `/inventory/locations`, `/:id`.** Warehouse/van/project locations list + detail (stock, movements, restock requests). No dedicated `/locations` backend resource was found distinct from `branchId`-scoped materials — treat "location" as `Branch` (`BranchesController`) plus material `branchId` filtering unless a richer location model is confirmed. | `src/features/inventory/locations/*.tsx` (new) | `GET/POST/PUT /api/branches`, `GET /api/materials?branchId=` | Location detail page explicitly notes it is branch-scoped, not a separate warehouse entity, until confirmed otherwise | M |
| P2-18 | **Stock Movements `/inventory/movements`.** Movement log for transfers, job usage, write-offs — aggregate `GET /api/materials/{id}/movements` per-part or (preferred) a tenant-wide movement feed if one exists; if not, compose client-side from `StockTransfersController` list + per-material movements. | `src/features/inventory/MovementsPage.tsx` (new) | `GET /api/materials/{id}/movements`, `GET /api/stock-transfers` | Sortable by date/type/part (closes `DataTable.sortable` gap) | M |
| P2-19 | 🔒 **Purchase Orders `/inventory/purchase-orders`, `/new`, `/:id`.** **No `PurchaseOrdersController` exists in the current backend.** Build the list/detail UI shell with `EmptyState` and flag to backend team; do not fake PO data. Once a backend endpoint exists, this becomes list + create + detail with line items, receiving workflow, and document attachments (reuse `AttachmentsController`). | `src/features/inventory/purchaseOrders/*.tsx` (shell only) | 🔒 none available | Shell clearly states "pending backend PO endpoint" | S (shell) |
| P2-20 | 🔒 **Suppliers `/inventory/suppliers`, `/:id`.** **No `SuppliersController` exists.** Same shell-only treatment as P2-19. | `src/features/inventory/suppliers/*.tsx` (shell only) | 🔒 none available | Shell clearly flagged | S (shell) |
| P2-21 | **Restock Requests `/inventory/restock-requests` — approval workflow.** Map to `MaterialRequestsController`: list (`GET /api/material-requests?branchId=`), approve (`POST .../approve`), issue (`POST .../issue`), close (`POST .../close`). | `src/features/inventory/RestockRequestsPage.tsx` (new) | `GET/POST /api/material-requests`, `.../approve`, `.../issue`, `.../close` | Approve/issue/close are distinct RBAC-gated actions with `ConfirmDialog` | L |
| P2-22 | **Stock Alerts `/inventory/alerts`.** Critical/low-stock list from `GET /api/materials/low-stock?branchId=`; "replenish" quick action (`POST /api/materials/{id}/replenish`). | `src/features/inventory/AlertsPage.tsx` (new) | `GET /api/materials/low-stock`, `POST .../replenish` | Replenish opens a small `FormDrawer` (quantity + note), not a bare button | M |
| P2-23 | **Inventory Reports `/inventory/reports`.** Stock valuation, usage, PO, movement, write-off reports. PO report is 🔒 blocked by P2-19; build valuation/usage/movement/write-off from `MaterialsController`/`StockTransfersController`/`MaterialIssuesController` data. | `src/features/inventory/ReportsPage.tsx` (new) | `GET /api/materials`, `.../movements`, `GET /api/stock-transfers`, `GET /api/material-issues` | Each report sub-tab independently loadable/exportable | L |
| P2-24 | **Stock Transfers (supports P2-16/P2-18, not its own nav item).** Create/approve/complete/cancel transfer flow, likely surfaced from a Location Detail or Movements action rather than a standalone page. | `src/features/inventory/stockTransfers/api.ts` (new) | `POST/GET /api/stock-transfers`, `.../{id}`, `.../approve`, `.../complete`, `.../cancel` | Transfer completion visibly updates both source and destination stock counts on next fetch | M |
| P2-25 | **Material Issues (supports Work Order Materials tab, P1-15).** Approve/reject/issue/return/cancel flow for issues raised against work orders. | `src/features/inventory/materialIssues/api.ts` (new) | `POST /api/material-issues`, `.../approve`, `.../reject`, `.../issue`, `.../return`, `.../cancel` | Consumed by P1-15's Materials tab as well as any standalone issues view | M |

### 4.4 Team

Backend: `UsersController` (`/api/users`), `TechniciansController` (`/api/technicians`), `TeamManagementController` (`/api/team-management`), `AssignmentGroupsController` (`/api/assignment-groups`). No dedicated `RolesController`/`SkillsController` found — see flags below.

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P2-26 | **Team Users `/team`, `/team/users`, `/new` (drawer), `/:id`.** List + invite `FormDrawer` + detail (profile, sessions, work history). | `src/features/team/users/*.tsx` (new) | `GET/POST/PUT /api/users`, `PATCH .../status`, `POST .../resend-credentials`, `.../reset-password`, `DELETE` | Invite flow triggers backend credential email per `CreateUserRequest` semantics; toast confirms | L |
| P2-27 | **Technicians `/team/technicians`.** List + skills/availability display, sourced from `TechniciansController` merged with `TeamManagementController.overview` for certifications/availability. | `src/features/team/technicians/*.tsx` (new) | `GET/POST/PUT/DELETE /api/technicians`, `GET /api/team-management/overview` | Availability/certification badges rendered per technician | M |
| P2-28 | 🔒 **Roles `/team/roles`.** **No `RolesController` exists** — roles are currently a fixed enum (`AppRole` in `src/shared/types/auth.ts`) rather than a backend-editable resource. Build a read-only summary page listing the 8 fixed roles and their `usePermission` action maps (i.e., surface the frontend's own `rolePermissions` object from P1-3 as a reference view) rather than faking editability. | `src/features/team/RolesPage.tsx` (new) | 🔒 none available (reads local `rolePermissions` map) | Page explicitly states roles are system-defined, not user-editable, until a backend endpoint exists | S |
| P2-29 | 🔒 **Skills `/team/skills`.** **No dedicated Skills catalogue controller** — `TeamManagementController.certifications` endpoints (`GET/POST/PUT/DELETE /api/team-management/certifications`) are the closest analogue. Build this page against certifications rather than a separate "skills" concept, and confirm naming with product before shipping. | `src/features/team/SkillsPage.tsx` (new) | `GET/POST/PUT/DELETE /api/team-management/certifications` | Page is explicitly the certification catalogue, labelled accordingly if product confirms the mapping | M |
| P2-30 | **Groups `/team/groups`.** Default assignment groups + members — direct match to `AssignmentGroupsController`. | `src/features/team/GroupsPage.tsx` (new) | `GET/POST/PUT/DELETE /api/assignment-groups`, `GET/POST/DELETE .../members` | Add/remove member updates list without full page reload (optimistic or refetch) | M |

### 4.5 Time & Attendance

🔒 **Entire module is backend-blocked.** No `TimeAttendanceController`, `TimesheetController`, `LeaveController`, or `ClockController` exists anywhere in the 67-controller snapshot. `WorkOrderBillingController` has a `time-logs` sub-resource (`/api/work-orders/{id}/time-logs`) scoped to a single work order's billable hours — that is not the same as a company-wide clock-in/out and leave system the spec describes.

| ID | Task | Files | AC delta | Complexity |
|---|---|---|---|---|
| P2-31 | 🔒 **Live Attendance `/team/time-attendance/live`.** Build the UI shell (clocked-in/travelling/break/not-clocked-in tiles) with `EmptyState` and a note that this requires a backend Time & Attendance API. Do not build fake auto-refresh polling against a non-existent endpoint. | `src/features/team/timeAttendance/LivePage.tsx` (shell) | Shell explicitly flags backend dependency | S |
| P2-32 | 🔒 **Timesheets `/team/time-attendance/timesheets`, `/:id`.** Shell only, same treatment. Once a backend exists, this is week/month/custom views + daily breakdown/audit trail. | `src/features/team/timeAttendance/TimesheetsPage.tsx` (shell) | Shell explicitly flags backend dependency | S |
| P2-33 | 🔒 **Leave `/team/time-attendance/leave`.** Shell only. Approval workflow depends on a backend leave-request resource that does not exist. | `src/features/team/timeAttendance/LeavePage.tsx` (shell) | Shell explicitly flags backend dependency | S |
| P2-34 | 🔒 **Time & Attendance Reports `/team/time-attendance/reports`.** Shell only; payroll/attendance exports need the same missing backend. `WorkOrderBillingController`'s `time-logs` can partially seed a "billable hours by technician" report as an interim substitute — build that one real sub-view if product wants partial value now. | `src/features/team/timeAttendance/ReportsPage.tsx` (partial: billable-hours sub-view real, rest shell) | Billable-hours sub-view uses real `GET /api/work-orders/{id}/time-logs` aggregation; rest explicitly flagged | M |

---

## 5. Phase 3 — Business Modules (task list)

Gate: Phase 2 fully done (including the Time & Attendance shells being merged and clearly flagged, not left mid-flight) before starting Phase 3. Contracts and Finance share `ContractsController`'s pricing hooks with Work Orders (P1-18), so confirm P1-18 landed first.

### 5.1 Contracts

Backend: `ContractsController` (`/api/contracts`) + reference sub-routes on `ClientsController`/`SitesController`/`AssetsController`/`WorkOrdersController`.

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P3-1 | **Contracts list `/contracts` — expiry colour-coding.** `DataTable` with `StatusBadge`-driven expiry colour (via `renewalRisk`/`expiringWithinDays` filter), filters for status/renewal risk/owner/client/type. | `src/features/contracts/ContractsPage.tsx` (new) | `GET /api/contracts?status=&renewalRisk=&ownerUserId=&clientId=&contractType=&expiringWithinDays=` | Expiry colour bands match spec thresholds (confirm exact day thresholds with design before hardcoding) | M |
| P3-2 | **Contract create/edit.** Full `UpsertContractRequest`-backed `FormDrawer`/wizard including rate card and reminder-rule sub-sections. | `src/features/contracts/ContractFormDrawer.tsx`, `schemas.ts` | `POST/PUT /api/contracts/{id}` | Rate card lines and reminder rules can be added inline during create | L |
| P3-3 | **Contract Detail — 7 tabs `/contracts/:id`.** Overview, SLA Terms, Billing/Rate Cards, P&L, Lifecycle/History, Documents, Reminders. | `src/features/contracts/ContractDetailPage.tsx` (new), `tabs/*.tsx` | `GET /api/contracts/{id}`, `.../lifecycle`, `.../rate-cards`, `.../reminder-rules` | 7 tabs present via `DetailLayout` | XL |
| P3-4 | **Contract lifecycle actions.** Submit-approval, approve, activate, start-renewal, renew, suspend, resume, terminate, cancel — each a `ConfirmDialog`-gated button on the Overview tab, RBAC-scoped (`usePermission('contracts.approve')` etc.). | `src/features/contracts/tabs/OverviewTab.tsx` | `POST .../submit-approval`, `.../approve`, `.../activate`, `.../start-renewal`, `.../renew`, `.../suspend`, `.../resume`, `.../terminate`, `.../cancel` | Terminate requires typed-confirmation via `ConfirmDialog`'s `confirmText` prop | L |
| P3-5 | **Contract P&L tab.** Revenue vs. cost breakdown — no single dedicated P&L endpoint was found; compose from `TenantInvoiceController` (invoices linked to contract), `WorkOrderBillingController` (billable items on contract-linked WOs), and contract rate cards. Confirm data-sourcing approach with backend before building — this is the most likely candidate for a dedicated backend aggregation endpoint being worth requesting. | `src/features/contracts/tabs/PnLTab.tsx` (new) | Composite of `/api/invoices`, `/api/work-orders/{id}/billable-items`, `/api/contracts/{id}/rate-cards` | Explicitly documents (in a code comment) that this is a client-side aggregation, not a backend-authoritative P&L figure | L |
| P3-6 | **Client/Site/Asset contract cross-links.** Wire the Contracts tab in Client Detail (P2-9), Site Detail (P2-10), Asset Detail (P2-11) to `GET /api/clients/{clientId}/contracts`, `/api/sites/{siteId}/contracts`, `/api/assets/{assetId}/contracts`. | Updates to P2-9/10/11 tab files | `GET /api/{clients,sites,assets}/{id}/contracts` | Each entity detail page's Contracts tab shows only contracts covering that entity | S |
| P3-7 | **Work Order pricing integration.** Wire `GET /api/workorders/{id}/pricing` and `POST .../recalculate-pricing` into the Billing section built in P1-18, so contract-derived rates auto-populate billable items where a contract applies. | Updates to `src/features/work-orders/tabs/BillingSection.tsx` | `GET/POST /api/workorders/{id}/pricing`, `.../recalculate-pricing` | Billable item auto-populate (P1-18) prefers contract pricing over default rate card when a contract applies | M |

### 5.2 Finance (13 sub-pages)

Backend: `TenantFinanceController` (`/api/finance`), `TenantInvoiceController` (`/api/invoices`), `TenantQuotationController` (`/api/quotations`), `CreditTermsController` (`/api/finance/credit-terms`).

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P3-8 | **Quotes `/finance/quotes`, `/:id`.** List + detail with lines, terms, send/accept/reject actions, PDF download. | `src/features/finance/quotes/*.tsx` (new) | `GET/POST/PUT /api/quotations`, `.../send`, `.../accept`, `.../reject`, `.../pdf` | Accept flow supports the "quote-acceptance public page" concept — confirm whether that public page is a separate anonymous route (`/quote-accept/:token` style) that doesn't exist in `router.tsx` yet; if required, add it here | L |
| P3-9 | **Quote → Invoice / Quote → Work Order conversion.** Two explicit conversion actions on Quote Detail. | Updates to `src/features/finance/quotes/QuoteDetailPage.tsx` | `POST /api/quotations/{id}/convert-to-invoice`, `.../convert-to-work-order` | Successful conversion navigates to the new Invoice/Work Order detail page | M |
| P3-10 | **Invoices `/finance/invoices`, `/:id`.** List + detail with lines, payment history, send/void, PDF/receipt download. | `src/features/finance/invoices/*.tsx` (new) | `GET/POST/PUT /api/invoices`, `.../send`, `.../void`, `.../pdf`, `GET/POST .../payments`, `DELETE /api/payments/{id}`, `.../receipt-pdf` | Payment recording updates invoice balance/status without full reload | XL |
| P3-11 | **Invoice Approvals `/finance/invoice-approvals`.** Approval queue for contract-linked invoices — compose from `TenantFinanceController` approval-rules (`GET/POST/PUT /api/finance/approvals`) plus invoice list filtered to a pending-approval status. | `src/features/finance/InvoiceApprovalsPage.tsx` (new) | `GET /api/finance/approvals`, `GET /api/invoices?status=` | Approval-rule-driven queue, not a hardcoded filter | M |
| P3-12 | **Receipts `/finance/receipts`.** Receipt recording/delivery — this is the payment-recording flow (`POST /api/invoices/{id}/payments`) surfaced as its own list/creation page rather than only inline on Invoice Detail. | `src/features/finance/ReceiptsPage.tsx` (new) | `GET /api/invoices/{id}/payments` (aggregate across invoices), `.../receipt-pdf` | Receipts list is genuinely cross-invoice, not just a redirect to Invoice Detail | M |
| P3-13 | 🔒 **Expenses `/finance/expenses`, `/:id`.** **No tenant-level `ExpensesController` was found** — `PlatformFinanceController.expenses` exists but is platform-admin-scoped (`/api/platform/finance/expenses`), not tenant-scoped. Build the UI shell and flag to backend; do not wire tenant Expenses to the platform-admin endpoint (would be a cross-tenant data leak). | `src/features/finance/expenses/*.tsx` (shell only) | 🔒 none available at tenant scope | Shell explicitly warns against reusing the platform-admin expenses endpoint | S (shell) |
| P3-14 | 🔒 **Payables `/finance/payables`, `/:id`.** **No tenant-level Payables/supplier-bill controller found.** Shell only, same treatment as P3-13. | `src/features/finance/payables/*.tsx` (shell only) | 🔒 none available | Shell flagged | S (shell) |
| P3-15 | **Rate Cards `/finance/rate-cards`.** Labour/material rate tables, tenant-wide (distinct from per-contract rate cards in P3-2). | `src/features/finance/RateCardsPage.tsx` (new) | `GET/POST/PUT/DELETE /api/finance/rate-cards`, `.../lookup` | Lookup preview (category/skill/time inputs → resolved rate) matches backend's `lookup` endpoint behaviour | M |
| P3-16 | **Preferential Rates `/finance/preferential-rates`.** Client-specific overrides with approval step. | `src/features/finance/PreferentialRatesPage.tsx` (new) | `GET/POST/PUT /api/finance/preferential-rates`, `.../approve` | Approve action gated by `usePermission('finance.approve')` | M |
| P3-17 | 🔒 **Exchange Rates `/finance/exchange-rates`.** **No exchange-rate controller found.** Shell only. | `src/features/finance/ExchangeRatesPage.tsx` (shell only) | 🔒 none available | Shell flagged | S (shell) |
| P3-18 | **Billable Work `/finance/billable-work`.** Completed WOs ready for billing — direct match. | `src/features/finance/BillableWorkPage.tsx` (new) | `GET /api/finance/billable-work` | "Generate Invoice" action per row calls `POST /api/work-orders/{id}/generate-invoice` | M |
| P3-19 | **Credit Control `/finance/credit-control`.** Stages/hold-state/follow-up actions — compose from `CreditTermsController`'s client credit summary (`GET /api/clients/{clientId}/credit-summary`) across all clients; escalation-stage *configuration* itself is 🔒 blocked (see P5 Settings). | `src/features/finance/CreditControlPage.tsx` (new) | `GET /api/clients/{clientId}/credit-summary` (per client, aggregated) | Page notes that stage thresholds are currently hardcoded pending Settings/Credit Control (P5) backend confirmation | L |
| P3-20 | **Aging `/finance/aging`.** Receivables aging buckets, direct match. Sortable/colour-coded per spec (closes `DataTable.sortable` gap). | `src/features/finance/AgingPage.tsx` (new) | `GET /api/finance/reports/aging` | Bucket colour thresholds documented in code comment | M |
| P3-21 | **Finance Reports `/finance/reports`.** Revenue, collections, by-client, aging sub-tabs, plus contract P&L cross-reference to P3-5. | `src/features/finance/ReportsPage.tsx` (new) | `GET /api/finance/reports/revenue`, `.../aging`, `.../client-statement`, `.../client-statement/preview-pdf`, `POST .../client-statement/email` | Each sub-tab independently loadable | L |
| P3-22 | **M-Pesa reconciliation.** No dedicated M-Pesa controller found in the 67-controller snapshot beyond `PlatformSettingsController.integrations` (platform-wide config only). Flag as 🔒 blocked for tenant-level reconciliation UI; tenant Settings → Payments (P5) is similarly blocked — see §12. | — (no page yet; tracked here for visibility) | 🔒 none available at tenant scope | — | — |

### 5.3 WhatsApp

🔒 **Entire feature is backend-blocked** (see P1-10). No settings page content, no Inbox tab, no template management can be built against real data. Do not build Settings → WhatsApp (`/settings/whatsapp`) beyond a flagged shell until backend support exists.

| ID | Task | Files | Complexity |
|---|---|---|---|
| P3-23 | 🔒 **WhatsApp Settings shell `/settings/whatsapp`.** `EmptyState` with explicit "pending backend WhatsApp integration" messaging. | `src/features/settings/WhatsAppSettingsPage.tsx` (shell) | S |

### 5.4 Reports

Backend: `ReportsController` (`/api/reports`), `ScheduledReportsController` (`/api/reports/scheduled`), `SlaDefinitionsController` (`/api/sla-definitions`).

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P3-24 | **Reports `/reports` — 6 sub-tabs.** WO Performance, SLA, PM Compliance, Finance, Technician Performance, CSAT. WO Performance/Technician Productivity/PM Compliance map directly; Finance reuses P3-21; SLA reuses `SlaDefinitionsController` compliance data (confirm exact compliance-vs-definition split with backend); CSAT is 🔒 blocked — no CSAT controller found. | `src/features/reports/ReportsPage.tsx` (new), `tabs/*.tsx` | `GET /api/reports/work-order-performance`, `.../technician-productivity`, `.../asset-reliability`, `.../pm-compliance` (each `+/export`), `GET /api/sla-definitions` | CSAT tab is an explicit shell, not faked data | XL |
| P3-25 | **CSV export wiring.** Every report sub-tab's "Export" button calls the matching `.../export` endpoint and triggers a file download (not a client-side CSV re-serialisation of already-fetched JSON — use the backend's export endpoint directly to guarantee parity). | Updates to P3-24's tab files | `GET .../work-order-performance/export`, `.../technician-productivity/export`, `.../asset-reliability/export`, `.../pm-compliance/export` | Downloaded file opens correctly in Excel/Sheets (spot-check column headers) | M |
| P3-26 | **Scheduled Reports management.** Not a distinct nav route in the current `router.tsx`, but backed by `ScheduledReportsController` — surface as a "Schedule" action on each Reports sub-tab (create/list/toggle/run-now). | `src/features/reports/ScheduledReportsPanel.tsx` (new) | `GET/POST/PUT/DELETE /api/reports/scheduled`, `.../toggle`, `.../run-now` | "Run Now" gives immediate toast feedback distinct from the scheduled-delivery toast | M |

---

## 6. Phase 4 — Platform Modules (task list)

Gate: Phase 3 fully done. Platform modules are lower end-user traffic than tenant modules but are needed for the sales/ops team to actually onboard tenants — see §10 for why this phase is not simply "last."

### 6.1 CRM

Backend: `PlatformLeadsController` (`/api/platform/leads`, plus anonymous `/api/public/leads` already wired in P1-21) is the **only** CRM-adjacent controller found. There is no Contacts, Activities, Tasks, or Pipeline-specific controller.

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P4-1 | **CRM Pipeline (kanban) `/platform/crm/pipeline`.** Drag-drop kanban grouped by `PlatformLeadResponse.status` (New, Contacted, Qualified, Demo Scheduled, Converted to Workspace, Not a Fit), drag updates call `PUT /api/platform/leads/{id}/status`. | `src/platform/crm/PipelinePage.tsx` (new) | `GET /api/platform/leads`, `PUT .../status` | Drag-drop is optimistic with rollback on API failure | L |
| P4-2 | **Leads list/detail `/platform/crm/leads`, `/new`, `/:id`.** List + detail with notes editing. "New" lead creation reuses the same `POST /api/public/leads` shape internally, or confirm whether an authenticated create path is desired (current backend only exposes anonymous public lead creation — an internal-create path may need a backend addition; flag if product wants sales reps to hand-add leads). | `src/platform/crm/LeadsPage.tsx`, `LeadDetailPage.tsx` (new) | `GET /api/platform/leads`, `.../{id}`, `PUT .../notes`, `.../status` | Detail page's notes field autosaves or has an explicit Save with toast | M |
| P4-3 | **Mark Won/Lost + Convert-to-Tenant flow.** No atomic `/convert` endpoint exists. Implement as an explicit two-step UI flow: (1) `PUT /api/platform/leads/{id}/status` → "Converted to Workspace", (2) `POST /api/platform/tenants` pre-filled from lead fields (company/contact info). Make both steps visible to the user (e.g. "Step 1: mark converted → Step 2: create tenant workspace") rather than hiding it behind one button that silently does two things — if either call fails, the user must see which step failed. | `src/platform/crm/ConvertLeadDialog.tsx` (new) | `PUT /api/platform/leads/{id}/status`, `POST /api/platform/tenants` | Failure of step 2 does not leave the lead silently marked "Converted" with no tenant — surface a retry affordance | L |
| P4-4 | 🔒 **Contacts `/platform/crm/contacts`, `/:id`.** **No backend controller.** Shell only. | `src/platform/crm/ContactsPage.tsx` (shell) | 🔒 none available | Shell flagged | S |
| P4-5 | 🔒 **Activities `/platform/crm/activities`.** **No backend controller.** Shell only. | `src/platform/crm/ActivitiesPage.tsx` (shell) | 🔒 none available | Shell flagged | S |
| P4-6 | 🔒 **Tasks `/platform/crm/tasks`.** **No backend controller.** Shell only. | `src/platform/crm/TasksPage.tsx` (shell) | 🔒 none available | Shell flagged | S |
| P4-7 | **CRM Reports `/platform/crm/reports`.** Pipeline/conversion analytics — compose from `GET /api/platform/leads` (client-side aggregation by status/date) since no dedicated CRM-reports endpoint exists; `PlatformReportsController` is platform-wide financial/tenant reporting, not CRM-funnel-specific. | `src/platform/crm/CrmReportsPage.tsx` (new) | `GET /api/platform/leads` (aggregated client-side) | Explicitly documents this is client-side aggregation pending a dedicated backend report | M |

### 6.2 Tenant Management

Backend: `PlatformTenantsController` (`/api/platform/tenants`), `PlatformTenantCommunicationSettingsController` (`/api/platform/tenants/{tenantId}/...`), `PlatformTenantLicensesController` (`/api/platform/tenant-licenses`).

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P4-8 | **Tenants list `/platform/tenants`.** Full list with license/user/branch counts, status filter, create action. | `src/platform/tenants/TenantsPage.tsx` (new) | `GET /api/platform/tenants` | — | M |
| P4-9 | **Tenant create `/platform/tenants/new`.** Wizard/form → `POST /api/platform/tenants` with optional initial admin creation. | `src/platform/tenants/TenantFormDrawer.tsx` | `POST /api/platform/tenants` | Optional "create initial admin user" toggle reflected in the request payload | M |
| P4-10 | **Tenant Detail — 7 tabs `/platform/tenants/:id`.** Overview, Users, License/Subscription, Communication Settings, Onboarding/Email Log, Audit, Danger Zone. | `src/platform/tenants/TenantDetailPage.tsx` (new), `tabs/*.tsx` | `GET /api/platform/tenants/{id}`, `.../summary`, `.../audit-logs`, `GET/PUT .../email-settings`, `.../notification-settings`, `.../communication-settings` | 7 tabs present via `DetailLayout` | XL |
| P4-11 | **Tenant lifecycle actions.** Activate, suspend (with reason), deactivate (with reason), extend-trial, status change. | Updates to Overview tab | `POST .../activate`, `.../suspend`, `.../deactivate`, `.../extend-trial`, `PATCH .../status` | Suspend/deactivate require a reason field per `UpdatePlatformTenantStatusReasonRequest` | M |
| P4-12 | **Typed-confirm tenant delete.** Uses `ConfirmDialog`'s `confirmText` prop (type the tenant subdomain to confirm), per spec requirement already noted in the audit as a component capability that exists but is unused. | Updates to Overview tab | `DELETE /api/platform/tenants/{id}` | `confirmText` matches tenant subdomain, not a generic "DELETE" string | S |
| P4-13 | **Impersonation.** No dedicated impersonation endpoint was identified in the 67-controller snapshot (`PlatformController`'s `tenants/{id}/sessions` lists sessions but does not appear to mint an impersonation token). Flag to backend before building — do not fabricate a client-side-only "impersonation" that doesn't actually authenticate as the tenant. | — | 🔒 needs backend confirmation | — | — |
| P4-14 | **Tenant Licenses cross-reference.** Surface `PlatformTenantLicensesController` (activate/suspend license) actions on the Tenant Detail License tab. | Updates to P4-10's License tab | `GET /api/platform/tenant-licenses`, `POST .../{tenantId}/activate`, `.../suspend` | — | S |

### 6.3 Platform Finance / Users / Audit

Backend: `PlatformFinanceController` (`/api/platform/finance`), `PlatformUsersController` (`/api/platform/users`), `PlatformAuditLogsController` (`/api/platform/audit-logs`).

| ID | Task | Files | Endpoints | Complexity |
|---|---|---|---|---|
| P4-15 | **Platform Finance dashboard `/platform/finance/dashboard`.** KPI dashboard. | `src/platform/finance/DashboardPage.tsx` (new) | `GET /api/platform/finance/dashboard` | M |
| P4-16 | **Platform Finance billing/invoices `/platform/finance/billing`, `/invoices`, `/new`, `/:id`.** Full invoice CRUD + send + mark-paid + void. | `src/platform/finance/InvoicesPage.tsx`, `InvoiceDetailPage.tsx` (new) | `GET/POST/PUT /api/platform/finance/invoices`, `.../send`, `.../mark-paid`, `.../void` | L |
| P4-17 | **Platform quotations sub-flow** (feeds billing — not its own nav route, embed in Finance dashboard or a Quotes tab). | `src/platform/finance/quotations/*.tsx` (new) | `GET/POST/PUT /api/platform/finance/quotations`, `.../approve`, `.../send`, `.../convert-to-invoice` | M |
| P4-18 | **Platform Finance revenue/aging `/platform/finance/revenue`, `/aging`.** Reuses `PlatformReportsController.revenue` and `PlatformFinanceController` payment/invoice data. | `src/platform/finance/RevenuePage.tsx`, `AgingPage.tsx` (new) | `GET /api/platform/reports/revenue`, `GET /api/platform/finance/invoices` (aggregated) | M |
| P4-19 | **Platform Users `/platform/users`, `/:id`.** Cross-tenant staff user management (distinct from tenant Users, P2-26). | `src/platform/users/UsersPage.tsx`, `UserDetailPage.tsx` (new) | `GET/POST/PUT /api/platform/users`, `.../activate`, `.../deactivate`, `.../reset-password`, `.../resend-credentials`, `.../assign-role` | L |
| P4-20 | **Platform Audit `/platform/audit`.** Platform-wide audit trail with filters. | `src/platform/audit/AuditPage.tsx` (new) | `GET /api/platform/audit-logs?from=&to=&actorUserId=&tenantId=&action=&entityType=&severity=` | M |

### 6.4 Client Portal

🔒 **Structurally separate app exists (`PortalApp.tsx`/`PortalShell.tsx`) but has no confirmed backend auth or data path.** No portal-specific controller (auth, client-scoped data gateway) was found among the 67 controllers. The existing tenant-side controllers (`ClientsController`, `TenantInvoiceController`, `TenantQuotationController`, `ContractsController`, `AssetsController`) are staff-authenticated (JWT with tenant user role), not client-authenticated — there is no confirmed mechanism for a portal *client contact* to authenticate and see only their own data.

**Recommendation: do not build Phase 4 Portal pages against real data until backend confirms (a) a portal auth mechanism and (b) whether portal reads reuse the tenant controllers with a client-scoped token, or need dedicated portal controllers.** Build shells + the honest-error login fix (P1-22) now; treat the rest as blocked.

| ID | Task | Files | Complexity |
|---|---|---|---|
| P4-21 | 🔒 **Portal Dashboard, Requests, Assets, Maintenance, Reports, Invoices, Quotes, Contracts, Profile — all 9 routes.** Build/keep as flagged shells (`ModulePage` with explicit "pending backend portal API" note is acceptable here, unlike tenant-side modules) until P1-22's blocker is resolved. | `src/portal/**` (shells) | S each (kept as shells) |
| P4-22 | **Re-scope once backend confirms portal auth.** When a portal auth/data path exists, re-open this section and replace shells module-by-module using the same DoD as tenant modules (§2), reusing `ClientsController`/`TenantInvoiceController`/etc. where the backend confirms client-scoped access is safe. | — | tracked for later re-planning |

### 6.5 Onboarding

No dedicated onboarding/checklist controller was found. `StarterPacksController`/`PlatformStarterPacksController` (`/api/starter-packs`, install action) is the closest real backend hook — onboarding progress itself is not persisted server-side as far as this controller snapshot shows.

| ID | Task | Files | Endpoints | AC delta | Complexity |
|---|---|---|---|---|---|
| P4-23 | **Onboarding checklist `/onboarding`.** Build as a **client-derived** checklist (e.g. "has ≥1 branch", "has ≥1 user", "has ≥1 client", "starter pack installed") computed from existing list-count endpoints (`GET /api/branches`, `/api/users`, `/api/clients`, `/api/starter-packs`) rather than a persisted checklist resource, since none exists. Document this derivation clearly in code. | `src/features/onboarding/OnboardingPage.tsx` (new) | `GET /api/branches`, `/api/users`, `/api/clients`, `/api/starter-packs` | Progress bar reflects real counts, not a hardcoded percentage | M |
| P4-24 | **Starter Pack install action.** Surface `POST /api/starter-packs/{id}/install` from the Onboarding page. | Updates to P4-23 | `GET /api/starter-packs`, `POST .../{id}/install` | Install conflict (409) shows a clear "already installed" message, not a generic error toast | S |
| P4-25 | **First-login modal + invite-link flow.** No backend invite-link/token endpoint distinct from `UsersController.resend-credentials` was found; treat "invite link" as the existing credential-email flow (P2-26) rather than a separate token-based link unless backend confirms otherwise. | `src/shared/components/FirstLoginModal.tsx` (new) | reuses `POST /api/users/{id}/resend-credentials` | Modal only shows once per user (localStorage/sessionStorage flag), not every login | M |

---

## 7. Phase 5 — Settings (task list)

Gate: Phase 4 fully done. Settings is last because most of its sub-pages configure behaviour used by earlier phases (numbering rules affect invoice/quote numbers, PM templates affect Maintenance, etc.) — building the *consuming* features first makes the settings forms easier to validate against real downstream effects, and several Settings pages are themselves backend-blocked, which is cheaper to discover last than to block Phase 2/3 on.

### 7.1 Tenant Settings (20 sub-pages)

Backend: `SettingsController` + `AdvancedSettingsController` (both on `/api/settings`), `TenantEmailTemplatesController` (`/api/settings/email-templates`), `TenantLicenseController` (`/api/tenant/license`), `IntakeProtocolsController` (`/api/settings/intake-protocols`), `AuditLogsController` (`/api/audit-logs`), `UserNotificationPreferencesController` (`/api/me/notification-preferences`).

| ID | Task | Route | Endpoints | Status | Complexity |
|---|---|---|---|---|---|
| P5-1 | Company Profile | `/settings/company` | `GET/PUT /api/settings/company` | ✅ backed | M |
| P5-2 | Branding | `/settings/branding` | `GET/PUT /api/settings/branding` | ✅ backed | M |
| P5-3 | Document Templates | `/settings/templates` | `GET/PUT /api/settings/documents`, `.../report-template` | ✅ backed | L |
| P5-4 | Email Templates | `/settings/email-templates`, `/:id` | `GET/PUT /api/settings/email-templates/{eventKey}`, `.../preview`, `.../test`, `.../reset` | ✅ backed | L |
| P5-5 | Numbering Rules | `/settings/numbering` | `GET /api/settings/numbering-rules`, `PUT .../{id}`, `POST .../reset`, `.../seed-defaults`, `.../preview` (legacy: `GET/PUT /api/settings/numbering`) | ✅ backed | M |
| P5-6 | PM Templates | `/settings/pm-templates`, `/:id` | shares `PmTemplatesController` hooks from P2-6 | ✅ backed (shared with P2-6) | S (mostly reuse) |
| P5-7 | Inventory Settings | `/settings/inventory` | — | 🔒 no dedicated tenant-level inventory-settings endpoint found | S (shell) |
| P5-8 | Time & Attendance Settings | `/settings/time-attendance` | — | 🔒 no Time & Attendance backend at all (see §4.5) | S (shell) |
| P5-9 | Email Intake | `/settings/email-intake` | `GET/PUT /api/settings/email-intake`, `.../test-connection`; plus `IntakeProtocolsController` CRUD + `.../test` | ✅ backed | L |
| P5-10 | Monitoring Intake | `/settings/monitoring` | `GET/PUT /api/settings/monitoring`, `GET/POST/PUT/DELETE /api/settings/monitoring-webhooks`, `.../rotate-secret`, `.../test` | ✅ backed | L |
| P5-11 | WhatsApp | `/settings/whatsapp` | — | 🔒 (see P3-23, already shelled) | — (done in P3-23) |
| P5-12 | Payments (M-Pesa/Stripe/bank) | `/settings/payments` | — | 🔒 no tenant-level payment-provider config endpoint found (`PlatformSettingsController.integrations` is platform-wide only) | S (shell) |
| P5-13 | Credit Control | `/settings/credit-control` | `GET/POST/PUT/DELETE /api/finance/credit-terms`, `.../set-default` | ⚠ partial — credit *terms* are backed; escalation-*stage* configuration (hold thresholds) has no confirmed endpoint, build that part as a shell | L |
| P5-14 | Portal | `/settings/portal` | — | 🔒 no portal-visibility-config endpoint found (consistent with §6.4's blocker) | S (shell) |
| P5-15 | Notifications | `/settings/notifications` | `GET/PUT /api/settings/notifications` (tenant-wide matrix), `GET/PUT /api/me/notification-preferences` (per-user) | ✅ backed — build both the tenant-wide matrix and link to per-user prefs | L |
| P5-16 | Integrations | `/settings/integrations` | — | 🔒 no tenant-level API-key/webhook management endpoint beyond `IntakeProtocolsController`/`monitoring-webhooks` (already covered by P5-9/P5-10) — treat this route as redirecting to those two pages rather than building a third redundant page, or shell it if product wants a distinct "API access" concept | S |
| P5-17 | Security | `/settings/security` | `GET/PUT /api/settings/security` | ✅ backed | M |
| P5-18 | Executive Dashboard Settings | `/settings/executive-dashboard` | — | 🔒 no metric-target/threshold endpoint found | S (shell) |
| P5-19 | Account | `/settings/account` | `GET /api/tenant/license`, `.../usage` | ⚠ partial — subscription view is backed; "danger zone" tenant self-service actions (e.g. close account) have no confirmed endpoint, shell that part | M |
| P5-20 | Audit Logs | `/settings/audit-logs` | `GET /api/audit-logs` | ✅ backed | M |
| P5-21 | Settings Overview | `/settings` | — (navigation hub only) | Links out to all 20 sub-pages with completion/status indicators | S |

### 7.2 Platform Settings (12 sub-pages)

Backend: `PlatformSettingsController` (`/api/platform/settings`), `PlatformSettingsTemplatesController` (`.../templates`), `PlatformEmailGovernanceController` (`.../email`), `PlatformEmailTemplatesController` (`.../email-templates`), `PlatformLicensingController` (`/api/platform/licensing`), `PlatformAuditLogsController`.

| ID | Task | Route | Endpoints | Status | Complexity |
|---|---|---|---|---|---|
| P5-22 | Identity | `/platform/settings/identity` | `GET/PUT /api/platform/settings/general` | ✅ backed | M |
| P5-23 | Plans | `/platform/settings/plans` | `GET/POST/PUT /api/platform/licensing/plans` | ✅ backed | M |
| P5-24 | Branding | `/platform/settings/branding` | `GET/PUT /api/platform/settings/branding` | ✅ backed | M |
| P5-25 | Features | `/platform/settings/features` | `GET/PUT /api/platform/settings/system-preferences` (best-fit proxy — confirm with backend whether this is truly "feature flags" or general prefs before treating as authoritative) | ⚠ needs confirmation | M |
| P5-26 | Email Templates | `/platform/settings/email-templates` | `GET/PUT /api/platform/settings/email-templates/{eventKey}`, `.../preview`, `.../test`, `.../reset` | ✅ backed | L |
| P5-27 | Email Delivery | `/platform/settings/email-delivery` | `GET /api/platform/settings/email/notification-rules`, `.../delivery-logs` | ✅ backed | M |
| P5-28 | CRM | `/platform/settings/crm` | — | 🔒 no CRM-settings endpoint (pipeline stage config etc.) found | S (shell) |
| P5-29 | Security | `/platform/settings/security` | `GET/PUT /api/platform/settings/security` | ✅ backed | M |
| P5-30 | Announcements | `/platform/settings/announcements` | — | 🔒 no announcements controller found | S (shell) |
| P5-31 | Support | `/platform/settings/support` | — | 🔒 no support-config controller found | S (shell) |
| P5-32 | Health | `/platform/settings/health` | — | 🔒 no dedicated system-health endpoint; `PlatformReportsController.summary` is the closest proxy but is a business-metrics summary, not infra health — do not mislabel it | S (shell, or proxy view clearly labelled) |
| P5-33 | Audit | `/platform/settings/audit` | `GET /api/platform/audit-logs` | ✅ backed (same data source as P4-20 — confirm with product whether this should just deep-link to the Platform Audit page instead of duplicating it) | S |

Note: `PlatformApp.tsx` currently has a single catch-all `settings/*` route resolving to one generic shell (`router.tsx`/`PlatformApp.tsx:49`). This task set requires **replacing that catch-all with 12 explicit routes** in `PlatformApp.tsx` before any of P5-22–P5-33 can be individually navigated to — do this route-table change as the first sub-task of Phase 5's platform-settings work.

---

## 8. Cross-Cutting Concerns (RBAC, offline/PWA, mobile, error boundaries)

These are not phase-gated the same way — some (RBAC, error boundaries) must be established in Phase 1 and then *applied* continuously; others (DataTable sort) are small fixes that unblock multiple later tasks.

| ID | Task | Files | Complexity | Notes |
|---|---|---|---|---|
| P8-1 | **`DataTable` sort implementation.** The `sortable` column flag exists in `DataTableColumn` but is inert. Implement client-side sort (click header → toggle asc/desc/none) for non-virtualised mode at minimum; virtualised mode sort is optional stretch. Several Phase 2/3 tables (Aging, Compliance, Movements) are specified as sortable. | `src/shared/components/DataTable.tsx` | M | Blocks: P2-4, P2-18, P3-20 polish (they can ship without it, but AC calls it out) |
| P8-2 | **`ErrorBoundary` coverage audit.** Confirm `ErrorBoundary.tsx` wraps each major route subtree (tenant shell, platform shell, portal shell) — currently exists as a component but its actual placement in `App.tsx`/`providers.tsx` should be verified and, if missing at the shell level, added. | `src/app/App.tsx`, `src/app/providers.tsx` | S | DoD item 5 depends on boundaries not swallowing errors silently |
| P8-3 | **`MobileActionBar` rollout.** Currently built, zero-times used. Every detail page with primary mutating actions (Work Order Detail, Client Detail, Contract Detail, Tenant Detail, Invoice Detail) should surface those actions via `MobileActionBar` below `md` breakpoint per DoD item 6. | Per-module detail pages as they're built | — (folded into each detail-page task's AC) | Not a separate task — call out in code review for every detail page |
| P8-4 | **RBAC permission-string audit.** As each phase's tasks land, add the new action strings to `usePermission.ts`'s `rolePermissions` map (started in P1-3). Keep a single source-of-truth list rather than letting each module invent ad hoc strings — track additions in this map's own commit history. | `src/shared/hooks/usePermission.ts` | ongoing | Review gate: no PR should introduce a `usePermission('some.new.string')` call without a corresponding map entry in the same PR |
| P8-5 | **Offline sync consumers.** Once P1-6 (`sync_queue` writer/processor) lands, wire it into: Execution tab checklist completion (P1-13), Evidence photo upload (P1-14), Signature capture (P1-16), Technician clock-in/out (blocked — see §4.5), Material logging (P1-15). Each offline-capable mutation should call `enqueueSync` when `!navigator.onLine` instead of calling `apiClient` directly. | `src/features/work-orders/tabs/*.tsx` | M (across several tasks) | This is the "somewhere real to go" the audit calls out as missing |
| P8-6 | **Prefetch/cache-ahead for offline use.** Once a technician opens a Work Order while online, prefetch its full detail bundle (checklist, materials catalogue subset, client/site/asset context) into IndexedDB so the Execution/Evidence/Materials tabs remain usable offline. | `src/pwa/offline/prefetch.ts` (new) | L | Depends on P1-4 (SW strategy decided) and P1-6 |
| P8-7 | **Push notifications.** No service worker `push`/`notificationclick` handlers exist. Lowest priority in this cross-cutting list — only pick up once P1-4's SW strategy is settled and a backend push-subscription endpoint is confirmed to exist (not found in the current 67-controller snapshot — flag as 🔒 blocked until confirmed). | `src/pwa/sw.ts` | L | 🔒 backend endpoint not confirmed |
| P8-8 | **28 stale Playwright specs under `tests/e2e/`.** Re-run against the new scaffold to establish an honest pass/fail baseline (expect most failing, since they target `frontend-backup/`'s old component structure). Triage: delete specs testing features not in the V5 spec, rewrite specs for features as their Phase task lands (do not do this as one big-bang task — fold spec rewrites into each feature task's "done" bar once the team has bandwidth for e2e, or track separately as a non-blocking parallel stream). | `ecosys-serviceops-frontend/tests/e2e/*.spec.ts` | XL (as a whole); do incrementally | Not a hard blocker for any phase — track as ongoing technical debt |

---

## 9. Top 10 Highest-Impact Tasks

Ranked by how much downstream work each one unblocks or de-risks, not by raw effort:

1. **P1-1 — Expand `endpoints.ts`.** Every single feature task in Phases 2–5 needs its endpoints defined here first. Nothing else can start cleanly without it.
2. **P1-2 — Establish the feature API-hook convention.** Sets the pattern (`api.ts` per module) every one of the ~120 remaining feature files will copy. Getting this wrong once means refactoring dozens of files later.
3. **P1-3 — Wire RBAC end-to-end.** Retrofitting permission checks across ~90 already-built pages is far more expensive than building it in from the start (this is the audit's own top recommendation, and it's still true).
4. **P1-8 — New Work Order `FormDrawer` + Zod.** The pattern-establishing create-flow. Every other module's create form (Clients, Contracts, Invoices, Users, Contracts...) copies this shape.
5. **P1-13 — Work Order Execution tab.** The single most complex, most spec-detailed piece of Phase 1; also the first real consumer of offline sync (P1-6), checklist logic (reused by PM, P2-1–P2-6), and evidence upload (P1-14) patterns.
6. **P2-9 — Client Detail (7 tabs).** Clients/Sites/Assets sit at the center of the domain graph — Work Orders, Contracts, Inventory usage, and Finance all reference a client. Getting this shape right unblocks cross-links in P3-6, P4-x reporting, and Portal (eventually).
7. **P3-10 — Invoices (list + detail + payment flow).** Finance is the module every stakeholder demo cares about most; invoicing is also the most technically complex single flow (lines, payments, PDF, void) and touches Contracts (P3-7), Work Orders (P1-18), and Reports (P3-21).
8. **P4-8/P4-9/P4-10 — Tenant Management (list, create, 7-tab detail).** This is how new tenants actually get onboarded — until this exists, the platform team has no self-serve way to create demo/pilot tenants, which blocks sales-facing demos of everything else.
9. **P8-4 — RBAC permission-string audit process.** Not a single task but the discipline that keeps P1-3's investment from rotting as ~150 new buttons get added across Phases 2–5.
10. **§12's backend-gap list (Time & Attendance, WhatsApp, CRM Contacts/Activities/Tasks, Portal auth, Purchase Orders/Suppliers, Expenses/Payables).** Surfacing this list *early* to the backend team is itself the highest-leverage non-code action — every week it's not raised is a week those ~15 routes stay stub-shelled instead of becoming real, and several (Portal auth, PO/Suppliers) block entire Phase 4/2 sub-modules outright.

---

## 10. Recommended Build Sequence

Goal: reach a **working, demonstrable system** as fast as possible while still respecting the strict phase gate the audit and this plan both require (each phase fully done before the next starts).

**Step 0 (½–1 day): Raise the backend-gap list.** Before writing any more feature code, send §12's list to the backend team. This doesn't block Phase 1 (none of Phase 1's tasks are backend-blocked), but the earlier it's raised, the more of Phase 2–4's blocked tasks might unblock mid-build instead of staying shells through the whole plan.

**Phase 1 (target: 2–3 weeks with 2 frontend engineers).**
1. P1-1, P1-2 in parallel (different files, no dependency between them) — day 1–3.
2. P1-3 (RBAC) — day 2–4, can overlap with P1-1/P1-2 since it only touches `usePermission.ts`/`Sidebar.tsx`/`Topbar.tsx`.
3. P1-8 (New WO FormDrawer) as soon as P1-1/P1-2 land — establishes the create-flow pattern the team will lean on for the rest of the plan.
4. P1-11 → P1-18 (Work Order Detail tabs) — sequence Timeline → Assignment → Execution → Evidence → Materials → Signatures → Report → Billing; Execution (P1-13) is the long pole, start it early and let Evidence/Signatures/Materials proceed once Execution's checklist data model is settled (they read the same work order context).
5. P1-19/P1-20 (Dashboard rebuild) can run in parallel with the WO Detail tabs — different engineer, same phase.
6. P1-4/P1-5/P1-6 (PWA/offline) can run in parallel with anything else in Phase 1 — it's infrastructure, not UI, and only becomes load-bearing once P1-13/P1-14/P1-16 need `enqueueSync`.
7. P1-21/P1-22 (demo/portal-login fixes) are small and can be slotted in whenever — do them early since they're customer-facing trust issues per the audit.
8. **Phase 1 exit criteria:** Work Order Detail's 8 tabs are all real (WhatsApp Inbox tab excepted, flagged), Dashboard shows real spec'd widgets, RBAC gates every button built so far, `endpoints.ts` covers the full backend surface.

**Phase 2 (target: 3–4 weeks, can add a 3rd engineer once patterns are stable).**
1. Clients/Sites/Assets (P2-7 → P2-14) first — everything downstream references this domain.
2. Inventory (P2-15 → P2-25) and Team (P2-26 → P2-30) can run in parallel once Clients/Sites/Assets land (different engineers, minimal cross-dependency except P2-25 feeding back into P1-15's Materials tab).
3. Maintenance (P2-1 → P2-6) can start in parallel with Inventory/Team — it depends on Assets (for PM plan targets) but not on Inventory/Team.
4. Time & Attendance (P2-31 → P2-34) — do these shells last in Phase 2, they're small and don't block anything else; don't let them stall the phase-gate.

**Phase 3 (target: 3–4 weeks).**
1. Contracts (P3-1 → P3-7) first — Finance's Billable Work (P3-18) and P&L reporting (P3-5, P3-21) both want Contracts to exist.
2. Finance (P3-8 → P3-22) is the largest single module in the plan (13 sub-pages, several XL tasks) — start Invoices (P3-10) and Quotes (P3-8/P3-9) early since they're the highest-demo-value pieces; Expenses/Payables/Exchange Rates shells (P3-13/14/17) can be done by a junior engineer or deferred to the very end of the phase since they're backend-blocked shells.
3. Reports (P3-24 → P3-26) last in Phase 3 — it aggregates data other Phase 3 (and Phase 1/2) modules produce, so building it too early means re-testing it repeatedly as sources change.
4. WhatsApp (P3-23) is a 1-hour shell task, slot in anywhere.

**Phase 4 (target: 2–3 weeks).**
1. Tenant Management (P4-8 → P4-14) first — it's the highest-impact task in this phase (see §9 #8).
2. Platform Finance/Users/Audit (P4-15 → P4-20) next, in parallel across 2 engineers.
3. CRM (P4-1 → P4-7) — Pipeline/Leads are real, Contacts/Activities/Tasks are shells; don't over-invest here relative to its actual backend support.
4. Portal (P4-21 → P4-22) and Onboarding (P4-23 → P4-25) last — Portal is mostly blocked pending backend auth confirmation from Step 0.

**Phase 5 (target: 2–3 weeks).**
1. Tenant Settings pages that are fully backed (P5-1 through P5-6, P5-9, P5-10, P5-15, P5-17, P5-20) first — knock out the ~11 real ones quickly since the pattern is simple (read/write a single settings object).
2. Platform Settings route-table replacement (the note under §7.2) is a prerequisite sub-task, do it first within this phase.
3. Blocked shells (P5-7, P5-8, P5-11 [already done in P3-23], P5-12, P5-14, P5-16, P5-18, P5-28, P5-30, P5-31, P5-32) can be batched and done quickly by one engineer while another finishes the real pages — they're all S-complexity `EmptyState` shells.

**Fastest path to a demoable system:** if the goal is "show something real to a stakeholder" before the full phase gate completes, the single most convincing demo path is **P1-8 → P1-11...P1-17 (full Work Order lifecycle) → P2-7...P2-9 (Client/Site/Asset context around it) → P3-10 (Invoice generation from a completed WO)** — this walks a work order from creation through completion, billing, and invoicing, which is the core value proposition of the product. This subset is achievable inside Phase 1+2 without touching Phase 3's full Finance module.

---

## 11. Parallelisable Work

Grouped by phase; items in the same row can be worked by different engineers simultaneously with minimal merge conflict risk (they touch disjoint files).

| Phase | Parallel track A | Parallel track B | Parallel track C |
|---|---|---|---|
| 1 | P1-1, P1-2 (infra) | P1-19, P1-20 (Dashboard) | P1-4, P1-5, P1-6 (PWA/offline) |
| 1 (after A) | P1-11 → P1-18 (WO Detail tabs, mostly sequential within itself but see note below) | P1-21, P1-22 (demo/portal fixes) | P1-3 (RBAC map extension, ongoing) |
| 2 | P2-7 → P2-14 (Clients/Sites/Assets) | P2-15 → P2-25 (Inventory) | P2-26 → P2-30 (Team) |
| 2 | P2-1 → P2-6 (Maintenance) | P2-31 → P2-34 (Time & Attendance shells) | — |
| 3 | P3-1 → P3-7 (Contracts) | P3-8, P3-9 (Quotes) then P3-10 (Invoices) | P3-13, P3-14, P3-17 (blocked shells, low priority filler) |
| 3 | P3-15, P3-16, P3-18, P3-19, P3-20 (Rate Cards/Preferential/Billable/Credit/Aging) | P3-24 → P3-26 (Reports, once above land) | P3-23 (WhatsApp shell) |
| 4 | P4-8 → P4-14 (Tenants) | P4-15 → P4-20 (Platform Finance/Users/Audit) | P4-1 → P4-7 (CRM) |
| 4 | P4-23 → P4-25 (Onboarding) | P4-21, P4-22 (Portal shells) | — |
| 5 | P5-1 → P5-6, P5-9, P5-10, P5-15, P5-17, P5-20 (real tenant settings) | P5-22 → P5-27, P5-29, P5-33 (real platform settings) | P5-7, P5-8, P5-12, P5-14, P5-16, P5-18, P5-28, P5-30, P5-31, P5-32 (all blocked shells, batch to one engineer) |

**Note on P1-11 → P1-18:** these seven tabs share one page (`WorkOrderDetailPage.tsx`) and one growing execution data model, so treat them as "parallel-with-coordination" — two engineers can each own 3–4 tabs simultaneously if they agree on the tab-content component boundary up front (each tab is already its own file per the task table, so file-level conflicts are minimal; the risk is API-shape disagreement, not merge conflicts).

---

## 12. Risk and Dependency Map

### 12.1 Backend gaps (block real implementation, not just polish)

| Area | Missing backend | Affected frontend tasks | Recommended handling |
|---|---|---|---|
| Time & Attendance | No `TimeAttendanceController`/`TimesheetController`/`LeaveController`/`ClockController` anywhere in the 67-controller snapshot | P2-31, P2-32, P2-33, P2-34, P5-8 | Raise to backend immediately (Step 0). Ship shells now; do not simulate clock data client-side. |
| WhatsApp | No `WhatsAppController` | P1-10, P3-23, P5-11, Dashboard WhatsApp widget (P1-19) | Same — raise early, ship shells. |
| CRM Contacts/Activities/Tasks | Only `PlatformLeadsController` exists; no Contacts/Activities/Tasks resource | P4-4, P4-5, P4-6 | Low priority relative to Leads/Pipeline, which are real — ship shells, don't over-invest. |
| Client Portal auth + data | No portal-specific auth or confirmed client-scoped data gateway | P1-22, all of §6.4 (P4-21, P4-22) | Highest-leverage single gap to resolve — blocks an entire phase's worth of routes (9 portal pages). Raise first. |
| Purchase Orders / Suppliers | No `PurchaseOrdersController`/`SuppliersController` | P2-19, P2-20, part of P2-23 | Raise — Inventory's PO receiving workflow is explicitly spec'd and currently has zero backend support. |
| Tenant-level Expenses/Payables | Only `PlatformFinanceController.expenses` (platform-admin-scoped) exists | P3-13, P3-14 | Do not reuse the platform endpoint for tenant data — confirm a tenant-scoped equivalent is planned. |
| Exchange Rates | No exchange-rate controller | P3-17 | Low priority — likely fine to defer. |
| Settings: Inventory, Time-Attendance, WhatsApp, Payments, Portal, Integrations (tenant-level), Executive-Dashboard targets | No confirmed endpoints | P5-7, P5-8, P5-11, P5-12, P5-14, P5-16, P5-18 | Batch these as shells (§10 Phase 5 step 3); revisit once backend confirms. |
| Settings: Platform CRM, Announcements, Support, Health | No confirmed endpoints | P5-28, P5-30, P5-31, P5-32 | Same batching. |
| Push notifications | No confirmed subscription endpoint | P8-7 | Lowest priority in this list — defer past Phase 5 if needed. |
| Tenant impersonation | `PlatformController.tenants/{id}/sessions` lists sessions but doesn't appear to mint an impersonation token | P4-13 | Confirm before building — a client-side-only fake impersonation would be a security-relevant bug, not a stub. |
| Route naming inconsistency | `WorkOrdersController`/`WorkOrderExecutionController`/`WorkOrderAssignmentsController` use `/api/workorders/...`; `WorkOrderBillingController` uses `/api/work-orders/...` (hyphenated) | P1-18, P3-7, P3-18, all Billing-related tasks | Verify against a live backend call (not just the source file) before finalising `endpoints.ts` (P1-1) — this could be a documentation typo in the controller's route attribute rather than intentional, confirm with backend before shipping. |

### 12.2 Internal (frontend-side) dependency chains

- `endpoints.ts` (P1-1) → every task in Phases 2–5. This is the single largest fan-out dependency in the plan.
- `usePermission` map (P1-3, then P8-4 ongoing) → DoD item 4 for every module, i.e. every task in Phases 2–5 is technically "not done" without its permission strings added.
- Work Order Execution tab's checklist model (P1-13) → PM Template checklist reuse in Maintenance (P2-6) and Settings PM Templates (P5-6) — if the checklist data shape changes after P1-13 ships, P2-6/P5-6 need to follow.
- `sync_queue` writer (P1-6) → P1-13, P1-14, P1-16 (offline-capable WO actions) and P8-5/P8-6 — until P1-6 lands, those three tabs should call `apiClient` directly online-only and defer offline queuing as a fast-follow, rather than blocking the whole tab on infrastructure.
- Contracts (P3-1 → P3-7) → Finance Billable Work (P3-18), Contract P&L (P3-5), Client/Site/Asset Contracts tabs (P3-6) — Contracts must land before these can show real data instead of empty cross-link tabs.
- Client Detail (P2-9) → Portal's eventual re-scoping (P4-22) will likely want to reuse the same tab components in a read-only client-scoped mode — worth designing P2-9's tabs with that reuse in mind even though Portal itself is blocked today.
- Tenant Management (P4-8 → P4-10) → practically required before the sales/onboarding team can demo *any* tenant-scoped module to a prospect using a fresh tenant, which makes it a soft dependency for stakeholder demos even though no frontend code technically imports from it.

### 12.3 Process risks

- **RBAC retrofit risk.** If P1-3 slips or is only partially done before Phase 2 starts, every subsequent module will either (a) skip RBAC and need retrofitting later (expensive, per the audit's own top warning) or (b) stall waiting for permission strings. Treat P1-3 as a true Phase 1 exit blocker, not a nice-to-have.
- **`ModulePage` half-migration risk.** As long as both `ModulePage.tsx`-based shells and dedicated `src/features/<module>/` pages coexist, it's easy to accidentally keep extending a `ModulePage` config instead of building the real page. Enforce via code review: no PR should add `columns`/`rows`/`detailTabs` to a `lazyModulePage(...)` call in `router.tsx` for a route this plan assigns a real task to — route-table entries should be swapped to point at the new component in the same PR that builds it.
- **28 stale Playwright specs risk.** Left untriaged, they'll either be ignored (false sense of security is gone, fine) or someone burns time debugging failures against intentionally-different new UI. Do the triage pass (P8-8) early enough that CI can mark them `skip`/`todo` with a reason, rather than leaving them silently red.
- **Backend confirmation latency risk.** Several 🔒-flagged tasks assume "raise to backend, get an answer." If that answer takes weeks, Phase 4 (Portal) and parts of Phase 2/3/5 could stall on shells for a long time. Track backend-gap resolution as its own lightweight backlog (not part of this frontend plan) so shell tasks have a clear trigger to become real tasks.
