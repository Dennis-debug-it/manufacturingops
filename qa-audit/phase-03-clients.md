# Phase 3 — Clients / Sites / Contacts

Tested live 2026-08-02 as tenant Admin. Client: Equity Bank (id 652b5642…).

## Positives (verified)
- **No SQL injection**: `search=' OR '1'='1` and `Equity'; DROP TABLE clients;--` return empty/normal results — EF Core parameterization confirmed.
- **Enum whitelisting**: `status=<junk>` → 400 "Status must be active, inactive, or all."
- **By-id tenant scoping**: `GET /api/clients/{random-guid}` → 404 "Client was not found." (no 500/leak).
- **Vertical privilege escalation blocked**: tenant Admin token → `/api/platform/*` all return **403** (tenants list, tenant detail, tenant users, platform summary).
- **No stored XSS**: client named `<img src=x onerror=…>` renders as inert text; payload did not execute, no DOM injection (React escaping).

## Findings
### F3-01 — LOW — No server-side input validation / length cap on client fields
- **Category**: Data integrity / Injection (secondary)
- **Location**: `POST /api/clients` accepted `clientName = "<img src=x onerror=…>"` (201 Created), raw and unbounded.
- **Impact**: Web render is safe (React escapes), but raw HTML/formula/over-long values can bite in non-escaping sinks: CSV/XLSX export (formula injection if name starts with `= + - @`), email/PDF templates, WhatsApp messages. Also allows garbage/oversized data.
- **Fix**: Server-side validation (max length, trim, reject control chars) and neutralize leading `= + - @ \t` on CSV/XLSX export. Re-verify in Phase 8.

### F3-02 — MEDIUM — Client list endpoint is unpaginated
- **Category**: Scalability / Performance
- **Location**: `GET /api/clients?search=&status=all` returns a bare JSON array of ALL tenant clients (no `page`/`pageSize`/limit; SPA sends none).
- **Impact**: A tenant with thousands of clients loads the entire table in one payload → slow response, large DOM, memory pressure. Same pattern likely on Sites/Assets — verify.
- **Fix**: Server-side pagination + virtualized table (frontend already depends on `@tanstack/react-virtual`).

## Not yet tested
- Sites & Contacts sub-tabs CRUD; Import flow; Export format (tie to F3-01 CSV injection); filter persistence across navigation; duplicate-client prevention.
