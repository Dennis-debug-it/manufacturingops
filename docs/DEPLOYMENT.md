# Ecosys ServiceOps — Production Deployment Guide

Last updated: 2026-07-11 (security-hardening branch).

## 1. Required environment variables

The app **refuses to start** if the JWT key or platform admin credentials are missing,
too short, or match a known default. Never put production values in `appsettings.json`.

| Variable | Purpose | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Must be `Production` | Disables Swagger, test seeding, enables HSTS |
| `ECOSYS_DB_CONNECTION` | PostgreSQL connection string | |
| `ECOSYS_JWT_SIGNING_KEY` | JWT HMAC key | ≥ 32 chars; generate fresh (see §2) |
| `ECOSYS_PLATFORM_ADMIN_EMAIL` | Bootstrap platform admin | Not a known default |
| `ECOSYS_PLATFORM_ADMIN_PASSWORD` | Bootstrap admin password | ≥ 12 chars, not a known default |
| `ECOSYS_PLATFORM_ADMIN_FULL_NAME` | Display name | Optional |
| `ECOSYS_CORS_ALLOWED_ORIGINS` | Comma-separated frontend origins | e.g. `https://app.ecosysdigital.co.ke`; CORS denies all if unset |
| `ECOSYS_SMTP_HOST` / `_PORT` / `_USE_SSL` / `_USERNAME` / `_PASSWORD` | Outbound email | |
| `ECOSYS_KNOWN_PROXIES` | Comma-separated reverse-proxy IPs | **Required if the proxy is not on localhost** — otherwise all users share one rate-limit bucket and audit logs record the proxy IP |

## 2. Generate the production JWT signing key

```bash
openssl rand -base64 48
```

Never reuse a key that has appeared in a repo, build output, or chat. The old dev key
(`EcosysDevelopmentSigningKey-…`) is now denylisted at startup. **Rotating the key
invalidates all active logins** — rotate during a maintenance window.

## 3. Build & publish

```bash
# Backend — always publish fresh; never copy a bin/ folder from a dev machine
dotnet publish backend/src/Ecosys.Api/Ecosys.Api.csproj -c Release -o /srv/ecosys-api

# Frontend — uses .env.production (VITE_API_BASE_URL=https://app.ecosysdigital.co.ke)
cd ecosys-serviceops-frontend && npm ci && npm run build   # output in dist/
```

Database migrations run automatically at API startup; the process aborts if they fail.

## 4. Reverse proxy (nginx) — TLS, CSP, WebSockets

The API already sends HSTS, `X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy`, and `Permissions-Policy`. The **CSP for the SPA must be set here**,
because the static frontend is served by the proxy:

```nginx
# Static frontend
location / {
    root /srv/ecosys-frontend/dist;
    try_files $uri /index.html;
    add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self' data:; connect-src 'self' wss://app.ecosysdigital.co.ke; frame-ancestors 'none'; base-uri 'self'; form-action 'self'" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header Referrer-Policy "no-referrer" always;
    add_header Strict-Transport-Security "max-age=15552000; includeSubDomains" always;
}

# API + SignalR
location /api  { proxy_pass http://127.0.0.1:5000; proxy_set_header X-Forwarded-For $remote_addr; proxy_set_header X-Forwarded-Proto $scheme; }
location /hubs {
    proxy_pass http://127.0.0.1:5000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

`style-src 'unsafe-inline'` is needed for email-template previews and component
libraries; tighten later if possible.

## 5. Post-deploy smoke checks

1. `GET /health` returns 200.
2. `GET /swagger` returns 404 (Swagger must be off in Production).
3. Login works; response headers include `Strict-Transport-Security` and `X-Content-Type-Options`.
4. 21+ rapid failed logins from one IP return `429 RATE_LIMITED`.
5. `POST /api/public/leads` more than 30×/min returns 429.
6. Attachment upload + download works (storage path writable).
7. SignalR connects (browser devtools → WS to `/hubs/serviceops`).

## 6. Known deferred items (post-launch backlog)

- Move JWTs from localStorage to httpOnly cookies (or add refresh-token rotation).
- Shorten `Jwt:ExpiryMinutes` from 480 (product decision — field-shift length).
- Hash the legacy `MonitoringSettings.WebhookSecret` at rest (needs data migration).
- Upgrade Vite 6 → 8 (dev-only esbuild advisory; does not affect production builds).
- Consider a global rate limiter in addition to per-endpoint policies.
