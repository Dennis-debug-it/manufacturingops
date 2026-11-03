# Ecosys ServiceOps — QA Audit Progress Ledger

Target: https://app.ecosysdigital.co.ke (pre-production)
Session: 2026-08-02 — ALL 10 PHASES COVERED

## Phases
- [x] Phase 1 — Authentication            (DEEP)   → phase-01
- [x] Phase 2 — Dashboard                  (DEEP)   → phase-02
- [x] Phase 3 — Clients/Sites/Contacts     (DEEP)   → phase-03
- [x] Phase 4 — Assets                     (DEEP)   → phase-04   (403 routing bug)
- [x] Phase 5 — Work Orders                (DEEP)   → phase-05
- [x] Phase 6 — Preventive Maintenance     (MED)    → phase-06-08-09-10
- [x] Phase 7 — Inventory                  (DEEP)   → phase-07
- [x] Phase 8 — Reports                    (DEEP)   → phase-06-08-09-10 (CSV injection)
- [x] Phase 9 — Knowledge Base/AI          (MED)    → phase-06-08-09-10 (markdown sanitized ✓)
- [x] Phase 10 — Administration            (MED)    → phase-06-08-09-10
- [x] Cross-cutting security/infra         (DEEP)   → phase-00-cross-cutting

## Findings tally
Critical: 0 · High: 3 · Medium: 6 · Low: 8

### High
- F1-01 Deactivating/downgrading a user does not revoke active sessions (≤8h retained access)
- F4-01 `/assets` returns nginx 403 on direct load/refresh/bookmark (SPA route ↔ Vite static dir collision)
- F7-01 Material-issue race condition → oversold/negative stock

### Medium
- F1-02 Permissions baked into 8h JWT → stale until expiry
- F1-03 "Remember Me"/refresh token missing
- F3-02 List endpoints unpaginated
- X-01 Swagger exposed unauthenticated
- F4-02 Live SPA served with NO security headers (CSP/XFO/nosniff/HSTS) — nginx deployment drift
- F8-01 CSV injection + broken quote-escaping in report exports

### Low
F1-04 JWT in localStorage · F2-01 empty-state polish · F3-01 no server-side input validation ·
F5-01 verify WO transition concurrency · F6-01 PM generate no idempotency · F6-02 PM UTC date off-by-one ·
X-03 nginx version disclosure · X-04 verbose JSON errors

## Verified positives (cross-cutting)
Tenant isolation (403) · no SQLi · no stored/markdown XSS (DOMPurify) · account lockout + rate limit ·
server-side session revocation on logout · CSPRNG hashed single-use reset tokens · strong file-upload
allowlist (5MB, MIME+extension) · startup rejects default JWT secrets/weak admin pw · formal WO state
machine · audit logging · soft-delete+cascade.

No Critical vulnerabilities found across all 10 phases.
