# Ecosys ServiceOps — Frontend V5 Spec Audit Report

Audited: 2026-07-02
Scope: `C:\ecosys\ecosys-serviceops-frontend\src` (the active, tracked-in-progress V5 rebuild)
Not in scope for compliance scoring: `ecosys-serviceops-frontend/frontend-backup/` — a 277-file copy of the **pre-rebuild** frontend that git shows as wholesale-deleted from the old tree (`git status` lists 244 `D` paths under the old `src/auth`, `src/modules`, `src/pages`, etc.) and re-created verbatim under `frontend-backup/`. It is not imported by `vite.config.ts`, not part of the build, and not routed to. It is referenced in this report only where it explains *why* something looks unusually mature (e.g. the Playwright suite) or is a pointer to prior business logic worth mining during rebuild.

## 1. Executive Summary

This is a **very early-stage rebuild**, not a feature-complete system with a few gaps. The project has been reset: the entire previous frontend (277 files, presumably close to spec-complete given its Playwright coverage) was moved to `frontend-backup/` and a brand-new `src/` tree was started from scratch. The new tree currently contains **56 source files**.

What exists is a genuinely well-built **scaffold**: the routing table enumerates essentially every URL in the V5 spec (tenant, portal, and platform), the design system primitives (`DataTable`, `StatusBadge`, `FormDrawer`, `ConfirmDialog`, etc.) are implemented to a solid standard, the brand colours/fonts/stack match the spec exactly, and the build is clean (`tsc -b` and `vite build` both succeed, `eslint` is clean).

What is missing is almost all **feature substance**. Of ~100 routes, only work orders (list + a dynamic-but-generic detail page), the dashboard, login, and a few public pages have bespoke implementations — and even those are thin (e.g. all 8 Work Order Detail tabs except Summary are literal `EmptyState` placeholders). Every other route — all of Maintenance, Clients/Sites/Assets, Inventory, Team/Time&Attendance, Contracts, Finance, Reports, all Settings sub-pages, all of CRM/Tenants/Platform Finance/Platform Users, and the entire Client Portal — is rendered by one generic `createModulePage()` factory that produces a title, a search box, and an empty/stub table. No domain forms, no RBAC gating, no offline sync wiring, and no report/signature/checklist logic exist yet.

**Overall completion estimate: ~12–15% of the V5 spec.**
- Foundation/shell/design-system layer: ~80% complete and solid.
- Routing skeleton: ~95% complete (paths exist, nearly all resolve).
- Actual feature/business logic per phase: ~3–5% complete (work orders list only).
- RBAC enforcement in UI: ~0% (hook exists, is never called).
- Offline/PWA runtime behaviour: ~10% (schema exists, nothing is wired to it, and the custom service worker is dead code under the current PWA build mode).

System health: the code that exists is clean, typed, and builds without errors or warnings. The risk is entirely one of *scope remaining*, not of technical debt in what's been built so far.

## 2. Build Status

| Check | Result |
|---|---|
| `npx tsc -b` (also `--force`) | ✅ Pass, zero errors |
| `npm run build` (`tsc -b && vite build`) | ✅ Pass — 2209 modules transformed, built in ~11.5s, PWA precache generated (21 entries, 588.61 KiB) |
| `npm run lint` (`eslint .`) | ✅ Pass, zero errors/warnings |

No compilation errors, no lint violations. The codebase is small enough that a clean build is not yet a strong signal of maturity — it mainly confirms the scaffold is internally consistent.

## 3. Stack Compliance

`package.json` dependencies vs. spec (all present, all at or above the minimum specified major version):

| Package | Spec | Installed | Status |
|---|---|---|---|
| react / react-dom | 18+ | 19.1.1 (resolved 19.2.5) | ✅ (newer major, no spec violation — "18+") |
| typescript | 5+ | ~5.8.3 | ✅ |
| vite | 5+ | ^5.4.19 | ✅ |
| vite-plugin-pwa | present | ^1.0.2 (resolved 1.3.0) | ✅ |
| tailwindcss | 3+ | ^3.4.17 | ✅ |
| react-router-dom | 6+ | ^7.8.2 | ✅ (newer major) |
| @tanstack/react-query | 5+ | ^5.83.0 | ✅ |
| axios | present | ^1.11.0 | ✅ |
| lucide-react | present | ^0.539.0 | ✅ |
| react-hook-form | present | ^7.61.1 | ✅ |
| zod | present | ^4.0.14 | ✅ |
| date-fns | present | ^4.1.0 | ✅ |
| react-hot-toast | present | ^2.6.0 | ✅ |
| clsx / tailwind-merge | present | ^2.1.1 / ^3.3.1 | ✅ |
| @tanstack/react-virtual | present | ^3.13.12 | ✅ |
| idb | present | ^8.0.3 | ✅ |
| @fontsource/inter, @fontsource/jetbrains-mono | present | ^5.2.6 each | ✅ |

No extra runtime dependencies beyond spec. One dev-only addition not mentioned in the spec, `@playwright/test` (^1.54.2), which is reasonable e2e tooling and not a compliance issue — but see §11 on the state of the actual specs.

**Brand colours** — `tailwind.config.js:6-19` matches the spec exactly:
```
primary: { DEFAULT: '#0F3B5E', light: '#1A5276' }, accent: '#2E86AB',
success: '#27AE60', warning: '#F39C12', danger: '#E74C3C',
gray: { 900: '#2C3E50', 700: '#5D6D7E', 500: '#85929E', 200: '#D5D8DC', 50: '#F8F9FA' }
```
✅ Exact match.

**Font imports** — `src/styles/globals.css:1-3`:
```css
@import '@fontsource/inter/400.css';
@import '@fontsource/inter/600.css';
@import '@fontsource/jetbrains-mono/400.css';
```
✅ Exact match.

## 4. Project Structure Compliance

| Spec dir | Exists | Notes |
|---|---|---|
| `src/app/` | ✅ | `App.tsx`, `providers.tsx`, `router.tsx` |
| `src/shared/` | ✅ | api, components, config, contexts, email, hooks, layout, pages, types, utils |
| `src/features/` | ✅ | Only `auth/`, `dashboard/`, `public/`, `work-orders/` exist — no `maintenance/`, `clients/`, `inventory/`, `team/`, `contracts/`, `finance/`, `reports/`, `settings/` feature folders yet (those routes are served entirely by the generic `ModulePage` factory, not dedicated feature modules) |
| `src/portal/` | ✅ | `PortalApp.tsx`, `auth/PortalLoginPage.tsx`, `layout/PortalShell.tsx` — no portal feature pages |
| `src/platform/` | ✅ | `PlatformApp.tsx`, `layout/PlatformShell.tsx` — no platform feature pages |
| `src/pwa/` | ✅ | `sw.ts`, `offline/db.ts`, `offline/hooks.ts` |
| `src/styles/` | ✅ | `globals.css` |

Required shared components (`AppShell.tsx, PageHeader.tsx, DataTable.tsx, StatusBadge.tsx, FormDrawer.tsx, ConfirmDialog.tsx, EmptyState.tsx, DetailLayout.tsx, SectionHeader.tsx, MobileActionBar.tsx, Spinner.tsx, ErrorBoundary.tsx, Sidebar.tsx, Topbar.tsx, BottomNav.tsx`): **all 15 exist**, all in `src/shared/components/` or `src/shared/layout/`. ✅

Required utilities (`cn.ts, dates.ts, format.ts`): **all 3 exist** in `src/shared/utils/`. ✅

Required hooks (`useAuth.ts, useTenant.ts, usePermission.ts, useDebounce.ts, useOnlineStatus.ts`): **all 5 exist** in `src/shared/hooks/`. ✅ (but see §6 — `usePermission` is dead code, never imported outside its own file)

Extra, not-in-spec but reasonable additions: `src/shared/api/sessionStore.ts`, `src/shared/api/platformApiClient.ts`, `src/shared/email/templatePreview.tsx` + `variableRegistry.ts`, `src/shared/pages/ModulePage.tsx` (the generic scaffold factory — this is effectively acting as a stand-in for dozens of not-yet-built feature pages).

## 5. Phase-by-Phase Feature Audit

Legend: ✓ Done, ⚠ Partial (route/shell exists but not to spec), ✗ Missing.

### Phase 1 — Foundation

| Feature/Component | Status | Notes |
|---|---|---|
| apiClient.ts (auth headers, interceptors) | ✓ Done | `src/shared/api/apiClient.ts:30-55` — bearer token injection, 401 → clear session + redirect to `/login`, toast on error. `platformApiClient.ts` duplicates the same interceptor logic against `/api/platform` base. |
| Auth context / session bootstrap | ✓ Done | `AuthContext.tsx` — login, logout, `GET /api/auth/me` bootstrap, token persisted via `sessionStore.ts` (localStorage) |
| usePermission / RBAC hook | ⚠ Partial | Hook (`usePermission.ts`) defines a full role→action map for all 8 roles, but is **never called anywhere in the codebase** except its own definition (verified via grep) |
| PWA / offline infrastructure | ⚠ Partial | See §9 — schema and hooks exist, nothing consumes them, and the custom `sw.ts` is not actually built into `dist/` under the current `generateSW` PWA mode |
| Work Orders list `/work-orders` | ⚠ Partial | Real page (`WorkOrdersPage.tsx`), single `GET /api/workorders` query, defensive field-normalisation. Tabs "All Work Orders" / "Report Queue" exist but Report Queue is an `EmptyState`. **WhatsApp Inbox tab is missing entirely.** No Status/Priority/Client/Site/Technician/Due-date/Source filter controls — only a single free-text search box. Technician auto-filter exists (`row.assignedTo === user.fullName`) but does not hide the filter bar (there is no filter bar to hide) |
| New WO FormDrawer + Zod | ✗ Missing | "New Work Order" button (`WorkOrdersPage.tsx:125`) is a plain `<button>` with no `onClick` — does nothing. `FormDrawer` component is never imported by any feature page |
| Report Queue stat strip + batch actions | ✗ Missing | Tab renders a bare `EmptyState` |
| Work Order Detail `/work-orders/:id` | ⚠ Partial | Real page, correct 8-tab shape (Summary, Timeline, Assignment, Work Execution, Evidence, Materials, Signatures, Report). Summary tab dynamically dumps whatever JSON keys the API returns as a generic grid — not designed fields. **All 7 other tabs are literal `<EmptyState>` placeholders** (`WorkOrderDetailPage.tsx:79-111`). No PM checklist, no evidence grid/IndexedDB, no materials/parts search, no signature pad, no report lifecycle/edit sections, no follow-up-WO dialog |
| Dashboard `/dashboard` | ⚠ Partial | Real page, single `GET /api/dashboard/summary` call, renders whatever scalar keys the payload has as a generic stat grid (max 8) and two derived tables. Not a designed 8-stat strip; no Upcoming PM, Contracts Expiring, Overdue WOs, Billable Summary, or WhatsApp alert widgets specifically |
| Executive Dashboard `/dashboard/executive` | ✗ Missing | Generic `ModulePage` shell only (no rows/columns configured — falls to plain `EmptyState`) |

### Phase 2 — Operations

| Feature/Component | Status | Notes |
|---|---|---|
| Maintenance Calendar / Schedules / Schedule Detail / Compliance / Engine | ✗ Missing | All 5 routes exist and resolve, all render the generic `ModulePage` shell with no columns/rows/tabs configured (`router.tsx:38-42`). No calendar grid, no 3-step schedule wizard, no coverage badges, no compliance tables/export, no engine health banner |
| Clients / Sites / Assets list + detail | ✗ Missing | Routes exist (`/clients`, `/clients/:id`, `/sites/:id`, `/assets/:id`, plus wizard routes) but all are generic `ModulePage` shells. No tri-tab Clients/Sites/Assets switcher, no 7-tab client detail, no 5-tab site/asset detail, no credit-status banner, no warranty badge, no Client Setup Wizard, no Zod forms |
| Inventory (9 sub-pages) | ✗ Missing | All routes present, all generic shells. No 3-tab part detail, no location-view toggle, no PO receiving workflow, no restock approval workflow |
| Team (Users/Technicians/Roles/Skills/Groups) | ✗ Missing | Routes present, generic shells only |
| Time & Attendance (Live/Timesheets/Leave/Reports) | ✗ Missing | Routes present, generic shells. No live board, no auto-refresh, no clock-event audit trail, no mobile clock/geo-fencing/QR, no leave approval workflow |

### Phase 3 — Business

| Feature/Component | Status | Notes |
|---|---|---|
| Contracts list + detail | ✗ Missing | Routes present, generic shells. No expiry colour-coding, no 7-tab detail, no PM-terms/billing-config/P&L tabs |
| Finance (13 sub-pages) | ✗ Missing | All routes present, all generic shells. No quote-acceptance public page, no online payment flow, no M-Pesa reconciliation, no credit-control engine UI, no aging colour-coding, no contract P&L in reports, no mobile expense capture |
| WhatsApp | ✗ Missing | No settings page content, no Inbox tab (see Phase 1 WO list), no Timeline reply composer, no template management. Only `/settings/whatsapp` route exists as an empty shell |
| Reports `/reports` | ✗ Missing | Single generic shell; spec requires 6 sub-tabs (WO, SLA, PM Compliance, Finance, Technician Performance, CSAT) — none present |

### Phase 4 — Platform

| Feature/Component | Status | Notes |
|---|---|---|
| PlatformShell (super_admin/platform_sales only, red "PLATFORM ADMIN" label) | ✓ Done | `PlatformShell.tsx:21-22, 58-59` — dark theme, rose-coloured "PLATFORM ADMIN" label present in sidebar and mobile header, gated by `ProtectedRoute allowedRoles={platformRoles}` in `router.tsx:270`, and `AppShell` explicitly returns children-only (no tenant chrome) for those roles (`AppShell.tsx:16-18`) so the two shells cannot visually mix |
| `/request-demo` public form | ⚠ Partial | Page exists with Zod-validated fields (company, name, email, phone, country, notes) matching most of spec, **but submit does not call any API** — it just navigates to `/demo-confirmation` (`RequestDemoPage.tsx:33-35`). No lead/contact/email creation |
| CRM Pipeline (kanban), Leads, Contacts, Activities, Tasks, Reports | ✗ Missing | Routes exist under `/platform/crm/*`, all generic shells. No kanban, no drag-drop, no lead detail tabs, no Mark Won/Lost dialogs, no Convert-to-Tenant flow |
| Tenant Management | ✗ Missing | `/platform/tenants` routes exist as one generic shell shared across list/new/detail. No 7-tab detail, no onboarding-tab email log, no impersonation, no typed-confirm delete |
| Platform Finance / Users / Audit / Settings | ✗ Missing | All routes exist, all generic shells |
| Client Portal shell | ⚠ Partial | `PortalApp.tsx` + `PortalShell.tsx` exist as a genuinely separate app shell (own nav, own layout) — structurally correct. But `PortalLoginPage.tsx` "magic link" form **does not call any API**; submit just navigates straight to `/portal/dashboard` (`PortalLoginPage.tsx:25-27`), i.e. there is no real magic-link auth, no email dispatch, no token verification |
| Portal routes (8 required) | ⚠ Partial | All 8 route paths exist, all render generic `ModulePage` shells. No client-friendly status mapping, no GPS tracking, no M-Pesa STK push, no CSAT survey |
| Onboarding | ✗ Missing | `/onboarding` route exists as a generic shell. No checklist, no dashboard banner/progress bar, no first-login modal, no invite-link flow |

### Phase 5 — Settings

| Feature/Component | Status | Notes |
|---|---|---|
| Tenant Settings (20 sub-pages) | ✗ Missing | All routes present (`/settings/company` through `/settings/audit-logs`), all generic `ModulePage` shells with no configured columns — render as bare `EmptyState`. No inline-editable numbering table, no PM template drag-reorder editor, no document-template tabs, no plan-locked branding fields, no M-Pesa/Stripe config forms, no notification toggle matrix |
| Platform Settings (12 sub-pages) | ✗ Missing | Only a catch-all `/platform/settings/*` route exists, resolving to a single generic `System Settings` shell (`PlatformApp.tsx:18,49`) — none of the 12 named sub-pages (identity, plans, branding, features, email-templates, email-delivery, crm, security, announcements, support, health, audit) exist as distinct routes or views |

## 6. RBAC Audit

- `usePermission(action: string): boolean` exists at `src/shared/hooks/usePermission.ts:64-74` with a role→permission-string map covering all 8 roles (`admin, manager, dispatcher, technician, finance, viewer, super_admin, platform_sales`). The map is reasonably designed (wildcard `'*'` for admin/super_admin, scoped lists for others).
- **It is never imported or called anywhere else in the codebase.** Grep across `src/` for `usePermission` returns only its own definition file. No button, route, or action is actually gated by it.
- Route-level RBAC does exist, but through a separate, simpler mechanism: `ProtectedRoute` (`src/shared/components/ProtectedRoute.tsx`) takes an `allowedRoles` array and is used to gate `/platform/*` to `platformRoles` and the tenant shell to `tenantRoles` (`router.tsx:270,274`). This works for shell-level separation but has no per-action granularity (e.g. nothing stops a `viewer` role from theoretically triggering a mutation if one existed — moot today since no mutations are wired yet).
- `Sidebar.tsx:8-16` has its own bespoke, redundant `canShowNavItem()` role check that duplicates/shadows what `usePermission` should be doing, using hardcoded role-name comparisons instead of the permission map.
- **No action buttons anywhere check permissions** — there are effectively no destructive/privileged action buttons wired to any handler yet (New Work Order, Complete, etc. are all non-functional stubs), so there is nothing to leak today, but this will need to be retrofitted once real actions are built.

**Recommendation:** wire `usePermission` into `Sidebar.tsx` (replacing `canShowNavItem`), into per-page primary-action buttons, and into any future FormDrawer/ConfirmDialog triggers, before or alongside building the actual feature forms — retrofitting RBAC after the fact across ~90 stub pages will be far more expensive than building it in from the start.

## 7. API Integration Audit

`src/shared/api/endpoints.ts` is intentionally minimal and self-documenting — it opens with a comment block stating that "TODO markers below are intentional for endpoints that exist in the domain spec but are not confirmed in the current backend snapshot," and only defines:

```
auth: login, me, logout, forgotPassword, resetPassword, changePassword
tenant: profile, brandingLogo
dashboard: summary
workOrders: list, detail(id), start(id), complete(id), close(id)
clients: list, detail(id)
sites: list(clientId)
assets: list
platform: summary, tenants, tenantSessions(id), tenantActiveCount(id)
```

Of these, only `auth.login`, `auth.me`, `auth.logout`, `dashboard.summary`, `workOrders.list`, and `workOrders.detail` are actually called anywhere in the frontend (grep-confirmed against `api.get/post/put/patch/delete` usages). `workOrders.start/complete/close`, `clients.*`, `sites.*`, `assets.list`, and most of `platform.*` are defined but unused.

For context, the backend (`backend/src/Ecosys.Api/Controllers/`) is comparatively mature — 60+ controllers exist including `WorkOrders`, `WorkOrderExecution`, `WorkOrderBilling`, `WorkOrderAssignments`, `Contracts`, `TenantFinance`, `TenantInvoice`, `Materials`, `MaterialRequests`, `MaterialIssues`, `PreventiveMaintenance`, `PmTemplates`, `Technicians`, `TeamManagement`, `PlatformLeads`, `PlatformTenants`, `PlatformFinance`, `PlatformUsers`, `PlatformSettings`, `AuditLogs`, etc. **The bottleneck is not backend availability — it's that the new frontend hasn't started consuming most of what the backend already exposes.** This is a materially different, and more favourable, situation than "backend not ready" — most of Phase 2–5 API wiring should be a matter of mapping existing endpoints, not waiting on backend work.

Endpoints stubbed/missing from the frontend's perspective (not exhaustive, illustrative of the gap): all inventory endpoints, all contract endpoints beyond none, all finance endpoints (quotes/invoices/receipts/expenses/payables/rate-cards/credit-control/aging), all team/time-attendance endpoints, all maintenance/PM-schedule endpoints, all CRM endpoints, all tenant-management endpoints beyond the list, all settings read/write endpoints, all portal endpoints.

## 8. Design System Compliance

| Element | Status | Notes |
|---|---|---|
| Brand colours in `tailwind.config.js` | ✓ Done | Exact match, see §3 |
| `cn()` utility | ✓ Done | `src/shared/utils/cn.ts` — `twMerge(clsx(inputs))`, standard pattern |
| `StatusBadge` variants | ✓ Done | `StatusBadge.tsx:8-31` — 5 variants (success/warning/danger/info/neutral) driven by keyword matching on the status string, plus `formatStatus()` title-casing. Reasonable heuristic-based implementation; not an explicit enum-driven variant prop, so behaviour depends on string content matching expected keywords |
| `DataTable` — pagination | ✓ Done | `DataTable.tsx:226-266`, page-size selector (10/25/50), prev/next |
| `DataTable` — sort | ⚠ Partial | `DataTableColumn.sortable?: boolean` field exists in the type (`DataTable.tsx:14`) but **no sort UI or sort logic is implemented** — the flag is currently inert |
| `DataTable` — virtualisation | ✓ Done | `@tanstack/react-virtual` wired, auto-enables above 100 rows (`DataTable.tsx:57-64`, also gated again in consumer at `WorkOrdersPage.tsx:166`) |
| `DataTable` — batch select | ✓ Done | Checkbox column + `onSelectionChange`, works in both virtualised and non-virtualised render paths |
| `FormDrawer` | ⚠ Partial | Component built (overlay, slide-in panel, header/footer slots, close-on-overlay) but **unused by any feature** — no form has been wired through it yet |
| `ConfirmDialog` | ⚠ Partial | Component built, including typed-confirmation-text variant (`confirmText` prop, matches the "type tenant subdomain to delete" spec requirement) — but **unused by any feature** |
| `MobileActionBar` | ⚠ Partial | Built (2-column fixed bottom bar, `md:hidden`), **unused by any feature** |
| `DetailLayout` | ✓ Done | Tab bar synced to `?tab=` search param, used by `WorkOrderDetailPage` and available to `ModulePage` when `detailTabs` is configured — but no `ModulePage` instance actually passes `detailTabs` (grep-confirmed zero usages), so in practice only the one hand-built Work Order Detail page has tabs today |
| `SectionHeader`, `EmptyState`, `Spinner`, `PageHeader`, `ErrorBoundary` | ✓ Done | All implemented to a clean, consistent standard; used throughout |

## 9. Offline/PWA Audit

| Item | Status | Notes |
|---|---|---|
| vite-plugin-pwa config | ⚠ Partial | `vite.config.ts:9-27` — configured with `srcDir: 'src/pwa', filename: 'sw.ts'` but **no `strategies` option is set**, so it defaults to `'generateSW'`. Under `generateSW`, Workbox **auto-generates** the service worker from `workbox.globPatterns` and the custom `src/pwa/sw.ts` source is not injected/used at all. This is confirmed by the build output: `PWA v1.3.0 mode generateSW ... files generated dist/sw.js dist/workbox-9c191d2f.js` — a Workbox-generated file, not the hand-written one. **The custom `sw.ts` (`src/pwa/sw.ts`) is currently dead code** — its literal network-passthrough `fetch` handler never ships. To actually run custom offline/sync logic, the config needs `strategies: 'injectManifest'` and the custom `sw.ts` needs an `injectManifest`-compatible `precacheAndRoute(self.__WB_MANIFEST)` call, which is not present |
| IndexedDB stores (idb) | ✓ Done (schema only) | `src/pwa/offline/db.ts:14-56` defines a well-thought-out schema: `work_orders, work_order_details, clients, sites, assets, parts, sync_queue (autoIncrement), time_entries, user_profile, settings` |
| Sync queue implementation | ⚠ Partial | `SyncQueueEntry` type models 8 action kinds (`UPDATE_WO_STATUS, ADD_TIMELINE_NOTE, LOG_MATERIAL, UPLOAD_EVIDENCE, CAPTURE_SIGNATURE, CLOCK_IN, CLOCK_OUT, COMPLETE_CHECKLIST_TASK, LOG_EXPENSE`), and `useSyncQueue()` reads pending-count from the store — but **nothing ever writes to `sync_queue`**, and no background-sync/replay-on-reconnect logic exists. It's a schema and a reader with no writer and no processor |
| Connectivity indicator | ✓ Done | `Topbar.tsx:24-29` shows an "Offline" pill based on `useOnlineStatus()` (browser `navigator.onLine` events). Note this is a **different, simpler** hook than `pwa/offline/hooks.ts`'s `useOfflineStatus()` — two parallel online-detection implementations exist (`shared/hooks/useOnlineStatus.ts` and `pwa/offline/hooks.ts`), which is a minor duplication worth consolidating |
| Pre-fetch / cache-ahead strategy | ✗ Missing | No prefetching of work orders/clients/parts for offline use found anywhere |
| Push notifications | ✗ Missing | No service worker `push`/`notificationclick` handlers, no subscription flow |
| Image compression (evidence photos) | ✗ Missing | No evidence upload UI exists yet at all (Evidence tab is an `EmptyState`), so compression is moot until that's built |

## 10. What Is Complete

- Vite + React 19 + TypeScript project scaffold, builds and lints cleanly with zero errors.
- Full dependency set matches the V5 stack spec exactly, no missing or extra runtime packages.
- Tailwind brand colours and font imports match the spec byte-for-byte.
- Directory structure (`app/shared/features/portal/platform/pwa/styles`) matches spec.
- All 15 required shared/layout components exist and are implemented to a clean, reusable standard (`DataTable` with virtualisation/pagination/batch-select, `StatusBadge`, `FormDrawer`, `ConfirmDialog` with typed-confirm, `DetailLayout` with URL-synced tabs, etc.).
- All 5 required hooks and 3 required utility modules exist.
- The full V5 route table (tenant + portal + platform) is enumerated in `router.tsx`/`PlatformApp.tsx`/`PortalApp.tsx` and every path resolves to *something* (no 404s on navigation), giving a complete information-architecture skeleton to build feature pages against.
- Auth flow: login, session bootstrap via `/api/auth/me`, token storage, 401-triggered logout, and role-based shell routing (tenant vs. platform vs. portal) all function correctly end-to-end against real endpoints.
- `PlatformShell` correctly enforces the "PLATFORM ADMIN" red-label, dark-theme, super_admin/platform_sales-only shell, and never renders mixed with the tenant shell.
- Work Orders list is a real, API-backed, searchable, technician-filtered, virtualisation-ready table.

## 11. What Is Missing or Broken

- **All domain forms.** `FormDrawer` and `ConfirmDialog` are built but zero-times consumed by any feature (`src/features/**`, `src/platform/**`, `src/portal/**`) — grep-confirmed.
- **RBAC enforcement.** `usePermission.ts` is fully written but never called; no button or route action is actually permission-gated today.
- **7 of 8 Work Order Detail tabs** are `EmptyState` placeholders (`src/features/work-orders/WorkOrderDetailPage.tsx:79-111`): Timeline, Assignment, Work Execution, Evidence, Materials, Signatures, Report.
- **~90 routes render the generic `ModulePage` shell** (`src/shared/pages/ModulePage.tsx`) with no configured columns/rows/tabs, across Maintenance, Clients/Sites/Assets, Inventory, Team/Time&Attendance, Contracts, Finance, Reports, all Settings, CRM, Tenant Management, Platform Finance/Users/Audit/Settings, and all Portal pages.
- **Demo request form does not submit anywhere** — `RequestDemoPage.tsx:33-35` navigates to `/demo-confirmation` without calling an API; no lead is created.
- **Portal "magic link" login does not send a link or authenticate** — `PortalLoginPage.tsx:25-27` navigates straight to `/portal/dashboard` on submit with no API call and no session established.
- **Custom service worker is dead code** — `src/pwa/sw.ts` is not built into `dist/` because `vite.config.ts` doesn't set `strategies: 'injectManifest'`; Workbox's auto-generated worker is used instead, so none of the intended custom fetch/offline behaviour ships.
- **Sync queue has no writer** — nothing in the app ever pushes an entry into the `sync_queue` IndexedDB store, so offline mutation queuing does not function even though the schema exists.
- **No WhatsApp Inbox tab** on the Work Orders list, despite being explicitly required in Phase 1.
- **28 Playwright e2e specs exist under `tests/e2e/`** (`work-order-execution.spec.ts`, `preventive-maintenance.spec.ts`, `command-centre.spec.ts`, `client-site-asset-workorder.spec.ts`, etc.) that were almost certainly written against the old `frontend-backup/` implementation. They should be assumed **failing/stale** against the new scaffold until re-verified — none were run as part of this audit per the read-only/build-only scope, but their subject matter (checklists, evidence, PM schedules, command centre) has no corresponding UI in the new tree yet.
- **Platform Settings** has no sub-routes at all — spec requires 12 distinct pages (identity, plans, branding, features, email-templates, email-delivery, crm, security, announcements, support, health, audit); only a single catch-all shell exists.

## 12. Partial Implementations

- **Work Orders list** (`WorkOrdersPage.tsx`): real API call and technician filtering work, but missing Status/Priority/Client/Site/Technician/Due-date/Source filters, missing WhatsApp Inbox tab, Report Queue tab is a stub, "New Work Order" button has no handler.
- **Work Order Detail** (`WorkOrderDetailPage.tsx`): correct tab structure, dynamic (unstyled) Summary field dump, all other tabs are placeholders.
- **Dashboard** (`DashboardPage.tsx`): real API call, but stat cards/tables are dynamically derived from whatever keys the payload happens to contain rather than the specified 8-stat strip + named widgets (Upcoming PM, Contracts Expiring, etc.).
- **Request Demo page**: fields and Zod validation match spec closely, but no backend submission.
- **Client Portal shell**: structurally separate app (`PortalApp.tsx`/`PortalShell.tsx`) as required, but login is non-functional and all 8 portal pages are generic shells.
- **PWA**: manifest, icons, and theme colour are configured correctly; IndexedDB schema is well-designed; none of it is functionally wired into the app (no writer, no custom SW, no prefetch).
- **RBAC**: both a full permission-map hook (`usePermission`) and a simpler role-array gate (`ProtectedRoute`) exist; the former is unused, the latter only guards at the shell/route level, not per-action.
- **DataTable sort**: `sortable` column flag defined in the type but not implemented.

## 13. Priority Fix List

1. **Decide and execute the PWA service-worker strategy** — either set `strategies: 'injectManifest'` in `vite.config.ts` and make `src/pwa/sw.ts` actually precache + handle sync, or drop the custom `sw.ts` and rely purely on `generateSW`. Currently the app silently ships neither the intended custom behaviour nor a documented fallback.
2. **Wire `usePermission` into `Sidebar.tsx`, `Topbar.tsx`, and every primary-action button** before building out the ~90 stub pages, so RBAC is designed-in rather than retrofitted across the whole app later.
3. **Build the Work Order Detail tabs for real** (Timeline, Materials, Evidence, Signatures, Report, Assignment, Execution) — this is the single highest-value, most spec-detailed feature in Phase 1 and currently the closest to done structurally.
4. **Wire `FormDrawer` + Zod to at least one real create flow** (New Work Order is the natural first candidate — the button already exists non-functionally) to establish the pattern before replicating it across Clients/Contracts/Inventory/Finance.
5. **Replace the generic `ModulePage` shell, module by module, starting with highest-traffic areas**: Clients/Sites/Assets, then Inventory, then Contracts/Finance, then Team/Time&Attendance, in that order given they're both Phase 2 priorities and have the richest existing backend controllers to wire against (`Clients`, `Sites`, `Assets`, `Materials`, `MaterialRequests`, `Contracts`, `TenantFinance`, `TenantInvoice` controllers already exist server-side).
6. **Fix the two non-functional auth-adjacent forms**: Request Demo (no lead creation) and Portal magic-link login (no real auth) — both are trust-critical, customer-facing entry points currently silently doing nothing.
7. **Expand `endpoints.ts`** to cover the many backend controllers not yet mapped (materials, contracts, finance, technicians, team management, PM templates/schedules, platform leads/tenants/finance/users/settings) — this is low-risk, mechanical work that unblocks nearly everything else.
8. **Re-run and triage the 28 Playwright specs** under `tests/e2e/` against the new scaffold to establish an honest baseline (expect most to fail) rather than leaving them silently stale.
9. **Consolidate the two parallel online-status implementations** (`shared/hooks/useOnlineStatus.ts` vs. `pwa/offline/hooks.ts#useOfflineStatus`) and implement an actual `sync_queue` writer/processor so offline mutations captured in Work Order Detail (once built) have somewhere real to go.
10. **Add DataTable sort behaviour** for the `sortable` column flag, since several spec tables (Aging, Compliance, Movements) are described as sortable/colour-coded lists.
