# Ecosys ServiceOps Pre-Deployment Test Guide

This test suite provides:
- frontend E2E smoke tests with Playwright
- backend smoke checks for critical API endpoints
- fail-fast guards for console errors, 404s, 401s after login, and API 500s
- screenshots, video, and trace artifacts on failure

## 1. Install dependencies

```bash
npm install
npx playwright install
```

## 2. Set environment variables

Required:
- `VITE_API_BASE_URL` or `E2E_API_BASE_URL` (backend URL, default `http://localhost:5072`)

Optional:
- `E2E_BASE_URL` (frontend URL used by Playwright, default `http://127.0.0.1:4173`)
- `E2E_TENANT_EMAIL` and `E2E_TENANT_PASSWORD` (existing tenant user)
- `E2E_SUPERADMIN_EMAIL` and `E2E_SUPERADMIN_PASSWORD` (for platform owner + finance tests)
- `E2E_SIGNUP_PASSWORD` (password used for generated signup accounts)
- `PLAYWRIGHT_SKIP_BACKEND_PRECHECK=1` to bypass the backend health precheck for mock-only local runs

If tenant credentials are not provided, tests auto-create a tenant account with random signup data.

Before running `npx playwright test`, make sure the backend is reachable at `http://localhost:5072` or the configured `E2E_API_BASE_URL`. The suite now performs a `/health` precheck and fails early with a clear setup message if the API is unavailable.

## 3. Run tests before deployment

```bash
npm run test:e2e
```

Other modes:

```bash
npm run test:e2e:headed
npm run test:e2e:ui
npm run test:e2e:report
```

## 4. What is covered

- App load
- Login and signup
- Dashboard
- Clients
- Assets
- Work orders
- Materials
- Settings
- Assignment groups
- Platform owner pages (Command Centre, tenants, licenses, users, audit logs, settings)
- Finance pages (quotations, invoices, payments, revenue, expenses)
- Backend smoke checks for tenant and platform API endpoints

## 5. Failure artifacts

On test failure, Playwright stores:
- screenshot
- video
- trace

Use:

```bash
npm run test:e2e:report
```

to inspect full failure details before deployment.
