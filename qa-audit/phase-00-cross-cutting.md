# Cross-Cutting (Security / Infra / Multi-tenant)

Verified live 2026-08-02.

## Positives
- **Tenant isolation**: tenant Admin token → all `/api/platform/*` endpoints return **403**. Tenant data queries scoped by token-derived `TenantId` via `TenantAwareControllerBase` + `.Where(x => x.TenantId == tenantId)`; by-id lookups 404 on unknown IDs.
- **No SQL injection** (EF Core parameterization), **no stored XSS** (React escaping), **no user enumeration**, **account lockout + rate limiting**, **server-side session revocation**.
- **Good HTTP security headers**: HSTS (`max-age=15552000; includeSubDomains`), `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Permissions-Policy: camera=(), microphone=(), geolocation=()`.
- **Clean error contract**: RFC-9110 ProblemDetails with traceId; no stack traces leaked (404, 400 validation all clean).

## Findings
### X-01 — MEDIUM — Swagger/OpenAPI exposed unauthenticated
- `GET /swagger/index.html` → 200 and `GET /swagger/v1/swagger.json` → 200 with **no auth**. Full API surface (every endpoint, params, schemas) is public — reconnaissance aid for attackers.
- **Fix**: Disable Swagger in production, or gate behind auth / internal network / feature flag.

### X-02 — MEDIUM — No Content-Security-Policy header
- API/app responses have no `Content-Security-Policy`. With the JWT stored in localStorage, CSP is the key defense-in-depth against XSS token theft.
- **Fix**: Add a strict CSP (script-src 'self', object-src 'none', frame-ancestors 'none', etc.).

### X-03 — LOW — Server version disclosure
- `Server: nginx/1.24.0 (Ubuntu)` reveals exact web-server version/OS. Set `server_tokens off;` in nginx.

### X-04 — LOW — Verbose JSON parser errors
- Malformed JSON returns parser internals (`LineNumber`, `BytePositionInLine`). Harmless but unnecessary; consider a generic "Malformed request body" message.
