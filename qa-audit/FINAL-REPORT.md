# Ecosys ServiceOps — Enterprise QA Audit — Final Report

**Target:** https://app.ecosysdigital.co.ke (pre-production)
**Date:** 2026-08-02 · **Method:** live black-box testing + white-box source review (`C:\ecosys`)
**Coverage:** All 10 phases audited. Phases 1–5, 7, 8 and the cross-cutting security layer were tested in depth (live + source); Phases 6, 9, 10 were partially data-limited (sparse tenant) and covered primarily by source review. Findings below are verified, not speculative.

---

## Executive Summary

Ecosys ServiceOps is a **well-architected multi-tenant field-service platform** (React 19 + Vite; .NET/EF Core + PostgreSQL). Engineering fundamentals are strong: a formal work-order state machine, consistent tenant isolation, layered services, audit logging, sanitized markdown, robust file-upload validation, and a startup guard that refuses to boot with default secrets.

**No Critical vulnerabilities were found across all 10 phases** — no auth bypass, no SQL injection, no stored/markdown XSS, no cross-tenant leak, no privilege escalation. For pre-production, that is a strong result.

The gating issues are **3 High** items — one of which (the `/assets` 403) is a trivial-to-fix but highly visible routing bug — plus a set of **Medium** hardening gaps (Swagger exposure, missing SPA security headers / config drift, CSV injection, pagination). None require architectural change.

**Tally:** 0 Critical · 3 High · 6 Medium · 8 Low.

---

## Assessments

### Security — 74/100
**Strong:** account lockout (5/15min); auth rate limiting (20/60s); no user enumeration; server-side session revocation (verified); CSPRNG single-use hashed reset tokens; EF parameterization (no SQLi); React + DOMPurify escaping (no stored/markdown XSS); tenant isolation (tenant token → platform = 403); strong file-upload allowlist (5 MB, MIME + matching extension); **startup validation rejects default JWT keys / weak admin passwords**.
**Gaps:** deactivation doesn't revoke sessions (High F1-01); permissions frozen in 8h JWT (Med F1-02); Swagger public (Med X-01); **SPA served with no security headers** (Med F4-02); CSV injection (Med F8-01); JWT in localStorage (Low); nginx version leak (Low).

### UX — 72/100
**Strong:** clickable KPI quick-actions, multi-view Work Orders (Table/Kanban/Calendar/Map), empty states, zero console errors, coherent IA. **Gaps:** `/assets` 403 on refresh/deep-link (High F4-01) is a jarring dead-end; "Remember Me" absent; a11y & mobile/tablet not verified.

### Performance — 78/100
Dashboard is a single server-side aggregate (2 calls, no client N+1). **Gaps:** list endpoints unpaginated (F3-02) — scale risk; frontend already ships `@tanstack/react-virtual`.

### Multi-Tenant — 82/100
Every tested query scopes on token-derived `TenantId` via `TenantAwareControllerBase`; platform endpoints reject tenant tokens (403); branch-level access layered on top. Residual: one direct horizontal cross-tenant-ID read couldn't be executed (no seeded second-tenant resource ID) — recommend a targeted fixture test.

### Database Integrity — 70/100
Soft-delete with cascade + audit; FK relationships; queryable audit trail. **Gaps:** no optimistic-concurrency tokens anywhere (xmin/rowversion) → lost-update races (inventory High F7-01; PM duplicate F6-01; verify WO transitions F5-01).

### Maintainability — 85/100 (strongest dimension)
Centralized `WorkOrderStatusTransitions`, `TenantAwareControllerBase`, dedicated lifecycle/session/password services, ProblemDetails errors, migrations, Playwright scaffold, startup secret validation.

### Accessibility — Not audited
Recommend a WCAG 2.1 AA pass (contrast, focus order, keyboard traps, ARIA on Kanban drag-drop).

---

## Bugs by Severity

### Critical (0) — none.

### High (3)
- **F1-01 — Deactivating/downgrading a user does not terminate active sessions.** Full access with original permissions retained until the 8h JWT expires. *(UsersController.Update; TouchAsync checks only IsRevoked/LogoutAt.)*
- **F4-01 — `/assets` returns nginx 403 on direct load / refresh / bookmark.** SPA route `/assets` collides with Vite's static `dist/assets/` dir; the whole Assets module + dashboard/onboarding links break on any full page load. *(router.tsx:260; nginx try_files matches the static dir.)*
- **F7-01 — Material-issue race → oversold/negative stock.** No concurrency token / atomic decrement / transaction in `IssueAsync`. *(ServiceOpsWorkflowServices.cs:58-71.)*

### Medium (6)
- **F1-02** Permissions embedded in 8h JWT — stale until expiry (compounds F1-01).
- **F1-03** "Remember Me" / refresh-token flow missing (hard 8h logout).
- **F3-02** Client/Sites/Assets/Work-order list endpoints unpaginated.
- **X-01** Swagger/OpenAPI exposed unauthenticated (`/swagger/*` → 200).
- **F4-02** Live SPA HTML served with **no** CSP/X-Frame-Options/nosniff/HSTS — the committed nginx.conf (which sets them) is not what's running (deployment drift).
- **F8-01** CSV injection + unescaped embedded quotes in report exports.

### Low (8)
F1-04 JWT in localStorage · F2-01 first-run empty-state polish · F3-01 no server-side input validation/length caps · F5-01 verify WO transition concurrency · F6-01 PM generation no idempotency (duplicate WOs) · F6-02 PM scheduling uses UTC dates (off-by-one for EAT) · X-03 nginx version disclosure · X-04 verbose JSON parser errors.

---

## Missing / Not-Yet-Verified
Remember-me/persistent session; list pagination; export hardening; **accessibility (WCAG)**; responsive mobile/tablet; independent data-driven verification of Assets lifecycle/QR, PM generation/overdue/escalation, Reports numeric accuracy, KB AI-assist, and the impersonation flow (all data-limited this session).

## Technical Debt
- Global optimistic-concurrency strategy (PostgreSQL xmin) across mutable aggregates (StoreItem, WorkOrder, PM plans).
- Decouple authorization from long-lived token claims (server-side permission resolution, or short access token + refresh).
- Standardize a server-side pagination contract across list endpoints.
- Reconcile deployed nginx config with the repo (config-as-code / CI validation of live headers).

---

## Scores
| Dimension | Score |
|---|---|
| **Overall Production Readiness** | **70 / 100** |
| Customer Experience | 72 / 100 |
| Security | 74 / 100 |
| Maintainability | 85 / 100 |
| **Enterprise Readiness** | **66 / 100** |

*Strong architecture and zero critical vulns lift the ceiling; three High issues plus enterprise-hardening gaps (pagination, session revocation, CSP/header drift, unaudited a11y) hold enterprise readiness below the ~80 customers expect. Most fixes are small and localized.*

---

## Prioritized Engineering Backlog

### Immediate (before next release / first enterprise customer)
1. **F4-01** Fix `/assets` 403 — set Vite `build.assetsDir` to a non-colliding name (e.g. `static/`) or add an explicit nginx SPA rule; audit all routes for static-dir collisions.
2. **F1-01** Revoke all user sessions on deactivate and on role/permission change; make the session heartbeat fail closed on `!user.IsActive`.
3. **F7-01** Make stock issue concurrency-safe (atomic guarded decrement or xmin token + retry).
4. **F4-02 / X-01** Deploy the committed nginx.conf (restore CSP/XFO/nosniff/HSTS on the SPA) and disable/auth-gate Swagger in production; add a smoke test asserting security headers on the live HTML.

### Short-term (next sprint)
5. **F8-01 / F3-01** CSV export: escape embedded quotes + neutralize leading `= + - @ \t`; add server-side input validation/length caps.
6. **F1-02 / F1-03** Shorter access token + refresh + "Remember Me", or re-resolve permissions server-side.
7. **F3-02** Server-side pagination on all list endpoints (+ virtualized tables).
8. **F5-01 / F6-01** Optimistic concurrency on WO transitions; idempotency guard on PM generation.

### Medium-term
9. Data-driven verification of the un-audited paths: Assets lifecycle/QR, PM overdue/escalation, Reports numeric accuracy, KB AI.
10. WCAG 2.1 AA accessibility pass + responsive (mobile/tablet) verification.
11. Targeted horizontal cross-tenant IDOR test with seeded two-tenant fixtures.
12. **F6-02** Tenant-timezone-aware PM date math; **X-03/X-04** `server_tokens off`, generic malformed-body error.

### Long-term
13. Global optimistic-concurrency + idempotency-key strategy for all state-changing endpoints.
14. Automated security regression suite (extend the Playwright scaffold with authz/isolation/header tests).
15. Load/scale testing on large-tenant datasets to validate pagination & query plans.

---
*Per-phase detail: `phase-01-authentication.md`, `phase-02-dashboard.md`, `phase-03-clients.md`, `phase-04-assets.md`, `phase-05-work-orders.md`, `phase-06-08-09-10.md`, `phase-07-inventory.md`, `phase-00-cross-cutting.md`. Ledger: `PROGRESS.md`.*
