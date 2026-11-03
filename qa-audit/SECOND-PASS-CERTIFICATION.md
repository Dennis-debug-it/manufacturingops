# Ecosys ServiceOps — Second-Pass Enterprise Certification (Gap Closure)

**Date:** 2026-08-02 · **Scope:** only the gaps left open by the first audit (Phases A–J + IDOR).
**Method:** live black-box (app.ecosysdigital.co.ke) + white-box source review (`C:\ecosys`).
**Data constraint:** the available tenant is data-sparse (0 assets, 0 PM plans, 0 KB articles, 1 client, 8 work orders). Where live proof was impossible this is marked **Unable to verify due to insufficient tenant data**. Destructive platform actions (tenant suspend/delete/impersonate) were **not executed** on the live system by policy.

---

## Executive Summary
This pass closes the operational, accessibility, and business-logic gaps the first audit deferred. The system is **more operationally mature than the first pass suggested** — it ships 8 background workers, a DB-backed health check, idempotent PM generation, and consistently policy-gated platform endpoints. The remaining gaps are concentrated in **observability and backup/DR** (operational readiness), plus a handful of **accessibility** defects on authenticated screens. No new Critical or High security issues were found. **New findings: 0 Critical · 0 High · 5 Medium · 4 Low.**

Two first-pass concerns are now **retracted after verification**: (1) the health endpoint *does* check Postgres (`AddDbContextCheck<AppDbContext>`), and (2) PM does not double-generate — cadence is advanced on completion (`WorkOrderLifecycleServices.cs:692`).

---

## Coverage Matrix

| Phase | Status | Basis |
|---|---|---|
| A — Assets lifecycle/QR/attachments/bulk/concurrency | **Unable to verify (no tenant data)** | 0 assets; source shows tenant-scoping + soft-delete (per first pass). Concurrency gap = existing F5-01/F7-01. |
| B — Preventive Maintenance | **Verified (source)** | Automated `PmSchedulerWorker`, idempotent, overdue-notify dedup, cadence advance. 1 new finding (cadence granularity). |
| C — Reporting accuracy/exports | **Partial** | Tenant+date scoping verified; numeric accuracy/large-export perf **unable to verify (no data)**. New finding: no PDF export. CSV formula-injection = existing F8-01. |
| D — Knowledge Base + AI | **Unable to verify (no articles)** | Markdown XSS already verified (first pass). Versioning/restore/AI quality not exercisable. |
| E — Command Centre | **Verified (source, read-only live)** | Platform policies gated; impersonation gated. Destructive flows not run on live. |
| F — Accessibility (WCAG 2.1 AA) | **Verified (login + authenticated)** | Login AA-clean; new violations on authenticated screens. |
| G — Responsive | **Verified (320 / 1280)** | No horizontal overflow; no wide culprits. |
| H — Observability | **Verified (source)** | Health/DB check good; tracing/metrics/structured logging absent. |
| I — Backup / Recovery | **Verified (source/deploy)** | Migrate-on-startup + no backup job. |
| J — Performance | **Partial** | Dashboard efficient (first pass); list pagination gap = existing F3-02. Load test **unable to verify (no data)**. |
| Multi-tenant IDOR | **Verified (main API) / Potential Risk (portal)** | 65/92 controllers TenantAware; portal/public scope manually. |

---

## New Findings (this pass only)

### H-1 — MEDIUM — No structured logging, distributed tracing, or metrics
- **Evidence:** No `OpenTelemetry`, `Serilog`, `prometheus`, or App Insights packages in any `.csproj`. `Program.cs` uses default console logging; `TraceIdentifier` appears only inside error ProblemDetails payloads (lines 288/312/328), not as a propagated correlation ID, and is not emitted to logs in a structured/queryable form. No `/metrics` endpoint.
- **Risk:** In production you cannot correlate a user-reported error to logs, trace a slow request across the 8 workers + API, or alert on golden signals. Weak monitoring readiness.
- **Recommendation:** Add OpenTelemetry (traces + metrics) with OTLP export, structured JSON logging (Serilog), and a correlation-ID middleware that stamps every log line and response.
- **Code:** `src/Ecosys.Api/Program.cs`; `src/*/*.csproj`. **Confidence: High.**

### I-1 — MEDIUM — EF migrations auto-apply on startup with no rollback gate
- **Evidence:** `await dbContext.Database.MigrateAsync();` runs on every boot (`Program.cs:346`).
- **Risk:** A bad migration is applied automatically on deploy with no approval/rollback step; with >1 API replica, concurrent `MigrateAsync` can race on the same schema. No down-migration/rollback story.
- **Recommendation:** Gate migrations behind an explicit deploy step (or a single migrator job/init-container), not app startup; keep migrations backward-compatible (expand/contract) so rollback is safe.
- **Code:** `src/Ecosys.Api/Program.cs:346`. **Confidence: High.**

### I-2 — MEDIUM — No automated backup / defined RPO-RTO in the deployment
- **Evidence:** `docker-compose.yml` runs `postgres:16-alpine` with a named `pgdata` volume and a local `uploads` volume; there is **no backup service, pg_dump cron, WAL archiving, or offsite copy**. Uploads live on a local Docker volume.
- **Risk:** Volume loss = total data loss; no defined Recovery Point/Time Objective; attachment files are not backed up alongside the DB.
- **Recommendation:** Add scheduled `pg_dump`/WAL archiving to offsite/object storage, back up the uploads volume (or move attachments to S3-compatible storage), and document RPO/RTO + a tested restore runbook.
- **Code:** `docker-compose.yml`. **Confidence: High.**

### F-1 — MEDIUM — Data-table batch-select checkboxes have no accessible name
- **Evidence (live):** On `/work-orders`, 6 `<input type="checkbox" class="h-4 w-4 rounded…">` controls have no `<label>`, `aria-label`, or `aria-labelledby` (the select-all + per-row selection checkboxes in the shared DataTable). Screen readers announce them as unlabeled.
- **Reproduction:** Load `/work-orders` (Table view) → inspect the row-selection checkboxes → no accessible name.
- **Risk:** WCAG 2.1 **4.1.2 Name, Role, Value** failure — a screen-reader user cannot tell what each checkbox selects.
- **Recommendation:** Add `aria-label="Select all work orders"` / `aria-label="Select row {WO number}"` in the shared `DataTable` batch-select column.
- **Code:** `ecosys-serviceops-frontend/src/shared/components/DataTable.tsx` (batchSelect). **Confidence: High.**

### C-1 — MEDIUM — No PDF export; "Excel" export is CSV only
- **Evidence:** Reports produce `File(..., "text/csv", …)` only (`ReportsController` CSV builders). No PDF generation path found. Excel compatibility relies on CSV, which carries the formula-injection issue already logged as F8-01.
- **Risk:** Reporting requirement (PDF export, Excel-native) unmet; formal/printable reports not available.
- **Recommendation:** Add a server-side PDF renderer for key reports; ship a real `.xlsx` writer (also neutralizes CSV formula injection).
- **Code:** `src/Ecosys.Api/Controllers/ReportsController.cs`. **Confidence: Medium** (absence of PDF path from source scan).

### B-1 — LOW/MEDIUM — PM cadence granularity limited to monthly and above
- **Evidence:** PM plan frequency resolves only `Monthly(1) / Quarterly(3) / Semi-Annual(6) / Annual(12)` months (`PreventiveMaintenanceController.cs:354-372`); no Daily/Weekly/Fortnightly option. Generation math uses `AddMonths` (leap-year/month-end correctly clamped by .NET).
- **Risk:** Assets needing daily/weekly PM (common for HVAC filters, generators) cannot be scheduled at the plan level.
- **Recommendation:** Extend the frequency model to day/week intervals (the asset-level `PmFrequencyUnit` already hints at unit support — unify them).
- **Code:** `PreventiveMaintenanceController.cs:354`. **Confidence: High.**

### F-2 — LOW — Table header cells lack `scope`; one icon-only button unnamed
- **Evidence (live):** `/work-orders` `<table>` has 13 `<th>` with **0** `scope` attributes and no `<caption>`; 1 icon-only `<button class="rounded-lg p-2…"><svg></button>` has no accessible name.
- **Risk:** WCAG 1.3.1 / 4.1.2 — weaker header association for screen readers; unnamed control.
- **Recommendation:** Add `scope="col"` to header cells (and `scope="row"` where applicable) in `DataTable`; give the icon button an `aria-label`.
- **Code:** `src/shared/components/DataTable.tsx`; the icon button owner. **Confidence: High.**

### G-1 — LOW — Inline "Forgot password?" link below 24×24 target
- **Evidence (live @320px):** the link renders 120×**20**px. WCAG 2.1 AA has no minimum for inline text links (this is 2.5.8, WCAG 2.2), so this is advisory.
- **Recommendation:** Increase tap padding if you target WCAG 2.2. **Confidence: High.**

### IDOR-1 — POTENTIAL RISK — External-facing controllers scope tenants manually
- **Evidence:** 65/92 API controllers inherit `TenantAwareControllerBase` (automatic `TenantId` scoping). The non-inheriting, non-platform ones — `PortalController`, `PortalKnowledgeController`, `PortalAuthController`, `PublicWorkOrderController`, `CsatController`, `IntakeController` — are external-facing and must scope tenants by hand.
- **Status:** The core tenant API and platform isolation were **verified** (first pass: tenant token → `/api/platform/*` = 403). The **portal/public** paths could **not** be independently tested for cross-tenant access (no second-tenant portal data).
- **Recommendation:** Add automated cross-tenant IDOR tests for every Portal/Public/CSAT/Intake route using seeded two-tenant fixtures; confirm each query filters on a token/link-derived tenant, never a client-supplied id. **Confidence: Medium (risk, not confirmed defect).**

---

## Verified Positives (new this pass)
- **Health check probes Postgres** (`AddDbContextCheck<AppDbContext>`) — real readiness, not a liveness no-op.
- **8 background workers** present (PM, SLA, Escalation, EmailOutbox, ContractReminder, ScheduledReport, ApprovalReminder, TrialLifecycle).
- **PM automation is idempotent** — skips plans with an open WO; overdue emails deduped by daily `WorkOrderEvent`; cadence advances on completion.
- **Platform endpoints consistently policy-gated**; impersonation gated under `PlatformAccess`; suspend requires a reason.
- **CORS restricted to configured origins** (denies all if unset) — not `AllowAnyOrigin`.
- **No sensitive data logged** (spot-checked: no password/token/secret values in log statements).
- **Login screen is WCAG AA-clean** (lang, h1, labelled inputs, alt text, contrast ≥4.76) and **responsive with no overflow at 320px and 1280px**.

---

## Unable to Verify (insufficient tenant data / non-destructive policy)
Assets lifecycle, QR generation/scan, attachments, bulk import/export, concurrent-edit behavior · KB versioning/restore, AI citation/hallucination/relevance · Report numeric accuracy vs SQL, large-export performance, PDF · PM live generation end-to-end · Command Centre suspend/delete/impersonate (not executed live) · Screen-reader manual pass, 400% zoom, forced high-contrast · Load/performance under large datasets.

---

## Scores
| Dimension | Score |
|---|---|
| Enterprise Readiness | **72 / 100** |
| Accessibility | **78 / 100** |
| Performance (indicative — not load-tested) | **75 / 100** |
| Operational Readiness | **62 / 100** |

Operational readiness is the weak link: strong app-layer maturity (workers, health, gating) is undercut by missing observability (H-1) and backup/DR (I-1, I-2).

---

## Remaining Technical Debt
Observability stack (traces/metrics/structured logs) · backup + DR runbook · migration deploy gating · reporting PDF/XLSX · shared DataTable a11y (labels, `th scope`) · PM sub-monthly cadence · portal/public IDOR test coverage.

## Recommended Sprint Backlog & Effort
**Immediate (before production):**
1. Backup + tested restore (pg_dump/WAL offsite + uploads) — **3–5 d** (I-2)
2. Gate migrations out of app startup — **1–2 d** (I-1)
3. DataTable a11y: aria-labels + `th scope` — **0.5–1 d** (F-1, F-2)

**Short-term:**
4. OpenTelemetry + structured logging + correlation IDs — **4–6 d** (H-1)
5. Portal/Public cross-tenant IDOR test suite — **2–3 d** (IDOR-1)
6. Report PDF/XLSX export (+ CSV neutralization) — **3–4 d** (C-1, F8-01)

**Medium-term:**
7. PM daily/weekly cadence — **2–3 d** (B-1)
8. Seed a synthetic large-tenant dataset and complete the Unable-to-Verify phases (Assets, KB, Reports accuracy, load tests) — **3–5 d**

_Estimated total: ~3–4 engineer-weeks to close all gap items._
