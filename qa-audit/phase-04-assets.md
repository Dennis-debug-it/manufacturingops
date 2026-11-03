# Phase 4 — Assets

Tested live + source, 2026-08-02.

## Findings
### F4-01 — HIGH — `/assets` returns nginx **403 Forbidden** on direct load / refresh / bookmark
- **Category**: Functional / Routing / Deployment
- **Reproduce**: Navigate directly to `https://app.ecosysdigital.co.ke/assets` (or open the Assets page and hit browser refresh). Result: raw `403 Forbidden — nginx/1.24.0 (Ubuntu)` page instead of the app.
- **Root cause**: SPA client route `/assets` (`src/app/router.tsx:260 → AssetsPage`) collides with the Vite static build directory `dist/assets/` (JS/CSS chunks). nginx `try_files $uri /index.html` matches the existing `/assets` **directory**, attempts a directory index, finds none, returns 403 (autoindex off). `/assets/` (trailing slash) also 403s. `/clients` and `/` correctly return 200.
- **Impact**: The **entire Assets module is inaccessible** by direct URL, refresh, or bookmark. The Dashboard "Active Assets" quick-action (`DashboardPage.tsx:42 → /assets`) and Onboarding "Register your first asset" (`OnboardingPage.tsx:80 → /assets`) break on any full page load. Only in-app soft navigation (React Router, no reload) works.
- **Fix**: Set Vite `build.assetsDir` to a non-colliding name (e.g. `static/`) so chunks live under `/static/`, freeing the `/assets` route; **or** rename the SPA route; **or** add an explicit nginx rule that serves the SPA for `/assets` exactly. Verify no other SPA route collides with a static dir/file name.

### F4-02 — MEDIUM — Live frontend nginx is missing all SPA security headers (deployment drift)
- **Category**: Security / Config management
- **Evidence**: SPA HTML responses (`GET /`, `/clients`) return **no** `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, or `Strict-Transport-Security`. The committed `ecosys-serviceops-frontend/nginx.conf` **does** define CSP + nosniff + referrer + HSTS — so the running frontend is not using the committed config. (Security headers currently seen on `/api/*` come from the .NET middleware, not the SPA host.)
- **Impact**: The document that actually executes JavaScript has zero protective headers → clickjacking possible (no frame-ancestors/XFO), no CSP defense-in-depth for the localStorage-stored JWT. Also indicates config drift between repo and production.
- **Fix**: Deploy the committed nginx.conf (or reconcile the live config); verify headers on the HTML document, not just the API. Supersedes/expands X-02.

## Positives (API layer)
- `GET /api/assets` tenant-scoped (returns only this tenant's assets — 0 here, consistent with dashboard).
- Unknown asset id → 404 (no leak); `search` param SQLi parameterized (200, no error).
- `GET /api/asset-categories` → 200.

## Not tested (no asset data in tenant)
- Asset lifecycle transitions, maintenance history, attachments, QR codes, bulk actions — tenant has 0 assets; needs seeded data.
