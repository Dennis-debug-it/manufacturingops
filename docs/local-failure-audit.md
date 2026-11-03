# Local Failure Audit Report - Ecosys ServiceOps

**Date:** 2026-09-11 (updated same day, follow-up session)
**Status:** STABLE (pending PostgreSQL runtime verification)
**Environment:** Local development (Windows 10 Pro, No Docker)

---

## Follow-Up Session: Root-Cause Fixes for Remaining Test Failures

Starting point: 472 passed / 34 failed / 1 skipped (from the restoration work below).
**End state: 504 passed / 2 failed / 1 skipped.** The 2 remaining failures require a live
PostgreSQL connection (`Npgsql...SocketException: Connection refused`) — a genuine environment
blocker, not a code defect. Everything else is root-cause fixed and verified.

### P0 — Live RBAC bug: every non-Admin user's `/me` response used stale, wrong permissions

**Root cause:** `AuthController.GetCurrentUser` (`GET /api/auth/me`) mapped its response from
`user.Permission` — the legacy, no-longer-maintained per-user row — while `Login` correctly computed
permissions from the tenant's role-permission matrix (`MvpAuthService.ResolvePermissionsAsync` →
`DynamicAccessService.ResolveLegacyClaimsAsync`, which is itself correctly wired to
`IRolePermissionService`, group grants, and per-user overrides). Every user who loaded `/me` (e.g. a
page refresh) got permissions unrelated to their actual role.

**Fix:**
- Added `IMvpAuthService.ResolveCurrentPermissionsAsync(User, ct)` — exposes the same resolution
  `Login` uses (`backend/src/Ecosys.Infrastructure/Services/MvpAuthService.cs`).
- `AuthController.GetCurrentUser` now calls it instead of reading `user.Permission`
  (`backend/src/Ecosys.Api/Controllers/AuthController.cs`); removed the now-dead
  `MapPermissions(UserPermission?, bool)` overload and the now-unnecessary
  `.Include(x => x.Permission)`.
- Verified: all 20 `RoleLoginTests` pass (were 9 failing).

### P1 — Live bug: tenant SMTP-mode switching endpoints were unreachable by any caller

**Root cause:** `SettingsController.EnsurePlatformEmailAccess()` required `tenantContext.IsSuperAdmin`,
but `TenantAwareControllerBase.EnsureTenantOperationalAccess()` (which every access to `TenantId`
goes through) unconditionally **rejects** `IsSuperAdmin` callers (EC-020, a deliberate tenant-isolation
guard). The two checks were mutually exclusive — `GetEmailNotifications`, `UpdateEmailNotifications`,
and `TestEmailNotifications` could never succeed for anyone. Separately, `ApplyEmailNotificationSettings`
never persisted `request.UsePlatformDefaults` at all, and `ValidateEmailNotificationRequest` ignored it
too — so even bypassing the auth issue, switching between platform mail and a tenant's own SMTP would
silently fail to take effect.

**Fix:**
- `EnsurePlatformEmailAccess()` now checks `tenantContext.ImpersonatedByUserId.HasValue` (a platform
  admin impersonating the tenant) instead of the contradictory `IsSuperAdmin`.
- `ApplyEmailNotificationSettings` now sets `settings.UsePlatformDefaults` /
  `settings.OverrideSmtpSettings` from the request; `ValidateEmailNotificationRequest` now skips the
  own-SMTP-host requirement when `UsePlatformDefaults` is set.
  (`backend/src/Ecosys.Api/Controllers/SettingsController.cs`)
- Verified: all 7 `TenantEmailDeliveryModeTests` pass (were 7 failing).

### P2 — Test-only fixes (no production behavior change)

- **`TimeZoneAndResetExpiryTests` (5 failures):** `Tenant.TimeZoneId` is a required column with an
  "Africa/Nairobi" default (always has been — not a regression). The test tried to persist a tenant
  with `TimeZoneId = null`, which a real FK/NOT NULL constraint would never allow. Fixed the test's
  seed helper to simulate "no tenant setting" via no matching row (which is what
  `GetTimeZoneIdAsync`'s fallback actually branches on) instead of an invalid null column value.
- **`MaterialInventoryClosureTests` (10 failures) + one `MaterialRequestsController` case:**
  `MaterialIssue.RequestedByUserId` / `MaterialRequest.RequestedByUserId` are required FKs, so the
  controllers' `.Include(x => x.RequestedByUser)` compiles to an inner join. The tests' seed helpers
  never created a matching `User` row, so every seeded issue/request was silently dropped from every
  query. A real Postgres FK constraint would never allow this to occur in production. Added the
  missing `User` seed to both helpers.
- **`RequestCorrelationTests` + `SecurityHardeningTests` (2 failures, now pass the code path but still
  need Postgres):** their `WebApplicationFactory` test hosts run with `UseEnvironment("Production")`
  but never set `App:PublicUrl`, so `StartupSecurityValidation.ValidatePublicAppOptions` aborted
  startup before the test even reached its assertion. Added a valid
  `App:PublicUrl = https://ecosys-tests.example.com` setting to both factories. They now fail only on
  the (expected, pre-existing) missing local PostgreSQL connection.

### Files changed (this follow-up session)

- `backend/src/Ecosys.Infrastructure/Services/MvpAuthService.cs` — added `ResolveCurrentPermissionsAsync`
- `backend/src/Ecosys.Api/Controllers/AuthController.cs` — `GetCurrentUser` uses it; removed dead code
- `backend/src/Ecosys.Api/Controllers/SettingsController.cs` — fixed `EnsurePlatformEmailAccess`,
  `ApplyEmailNotificationSettings`, `ValidateEmailNotificationRequest`
- `backend/src/Ecosys.Infrastructure/Services/EmailOutboxServices.cs` — restored
  `ReleaseAbandonedClaimsAsync` (prior sub-session, see below)
- `backend/src/Ecosys.Api/appsettings.json` — fixed dead `AuthSecurity` config keys (prior sub-session)
- `backend/tests/Ecosys.Tests/TenantEmailDeliveryModeTests.cs` — fixture now simulates impersonation
- `backend/tests/Ecosys.Tests/TimeZoneAndResetExpiryTests.cs` — seed helper fix
- `backend/tests/Ecosys.Tests/MaterialInventoryClosureTests.cs` — added missing `User` seeds
- `backend/tests/Ecosys.Tests/RequestCorrelationTests.cs`,
  `backend/tests/Ecosys.Tests/SecurityHardeningTests.cs` — added `App:PublicUrl` test setting

### Known environment limitation (unchanged)

**No local PostgreSQL.** The last 2 test failures, and full end-to-end runtime verification (starting
the API, exercising it from the frontend), remain blocked on this. See "Known Blockers" below — still
accurate. Also: **the machine's C: drive is at ~99.7% full (796MB free of 238GB)**, unrelated to this
repo, and caused at least one build failure mid-session (freed by clearing `bin`/`obj`). Worth the
user's attention before further build/test work.

---

## Original Session Report (Restoration of Reverted Security-Hardening Cluster)

---

## Current Status

### System State
- **Backend Build:** ✅ SUCCEEDS (All projects compile without errors)
- **Frontend Build:** Not yet tested
- **Database:** ❌ PostgreSQL NOT RUNNING (blocking full integration tests)
- **API Runtime:** ❌ Cannot start (depends on PostgreSQL)

---

## Critical Issues Fixed

### Issue 1: Missing `IRuntimeMetrics` Dependency Injection (P0)
**Symptom:**  
```
System.InvalidOperationException:
Unable to resolve service for type 'Ecosys.Api.Observability.IRuntimeMetrics'
while attempting to activate 'Ecosys.Api.Controllers.PlatformHealthController'.
```

**Root Cause:**  
The `IRuntimeMetrics` interface was defined in `Ecosys.Api.Observability` but never registered in the DI container. When `PlatformHealthController` attempted to inject it, the container had no registration.

**Fix Applied:**  
1. **Moved `IRuntimeMetrics` and `RuntimeMetrics` to `Ecosys.Shared.Observability`**
   - File: `/c/ecosys/backend/src/Ecosys.Shared/Observability/RuntimeMetrics.cs` (NEW)
   - Reason: Avoid circular dependency (Infrastructure → Api would create cycle)
   - Infrastructure can now reference Shared without issues

2. **Registered in DependencyInjection.cs**
   - Added: `services.AddSingleton<IRuntimeMetrics, RuntimeMetrics>();`
   - Location: `Ecosys.Infrastructure.DependencyInjection.AddInfrastructure()`
   - Lifetime: Singleton (correct for process-wide metrics)

3. **Updated all imports**
   - `PlatformHealthController.cs` → uses `Ecosys.Shared.Observability`
   - `RequestMetricsMiddleware.cs` → imports `Ecosys.Shared.Observability`
   - `Program.cs` → added `using Ecosys.Api.Observability` for middleware reference

### Issue 2: Missing `RequestMetricsMiddleware` Registration (P2)
**Symptom:**  
Infrastructure was built to record HTTP request metrics but the middleware was never registered in the request pipeline.

**Root Cause:**  
`RequestMetricsMiddleware` exists and is designed to integrate with `IRuntimeMetrics`, but was never added to `app.Use()` in Program.cs.

**Fix Applied:**  
- Added: `app.UseMiddleware<RequestMetricsMiddleware>();` in Program.cs
- Position: After authentication/authorization but before controllers (captures all routed requests)
- Order: After TenantLicenseEnforcementMiddleware, before authorization

---

## Compilation Status

### Backend Build Results
```
Before Fixes:
  - Errors: 2 (circular dependency issues)
  - Warnings: 4 (unread parameters)

After Fixes:
  - Errors: 0 ✅
  - Warnings: 4 (cosmetic - unread constructor params, not blocking)
    • WorkOrderLifecycleServices.cs: auditLogService unread
    • UserCredentialDeliveryService.cs: httpContextAccessor unread
    • PasswordServices.cs: httpContextAccessor unread
    • MvpAuthService.cs: rolePermissionService unread
```

**Note:** Warnings are deliberate parameter injections (possibly for future use or dependency clarity). Not fixing as per repair rules (don't create churn).

---

## Test Compilation Status

### Test Project Build: ❌ FAILS
```
Error Summary:
  - Total Errors: 32
  - Total Warnings: 1

Root Categories:
  1. Missing/Renamed Methods (9 errors)
     - CreateWorkspaceUser (not found)
     - ResendWorkspaceUserInvitation (not found)
     - ReleaseAbandonedClaimsAsync (not found)
  
  2. Missing Types (8 errors)
     - PasswordResetUnavailableException
     - CreatePlatformTenantUserRequest
     - PlatformTenantUserCreatedResponse
     - PlatformTenantCredentialDeliveryResponse
  
  3. Wrong Constructor Signatures (13 errors)
     - Controllers expect different parameter counts
     - Services expect different parameters
  
  4. Wrong Method Signatures (2 errors)
     - ImpersonateTenantAdminAsync (wrong argument count)
     - GetAll (wrong argument count)
```

**Analysis:**  
These appear to be from a partial refactoring or merge conflict. The test code references API methods/DTOs that either:
- Were renamed during a recent commit
- Were moved to different classes
- Are in a different namespace

**Action Taken:**  
None yet (tests don't block backend execution). Once PostgreSQL is available, actual runtime errors can be diagnosed.

---

## Known Blockers

### Blocker 1: No PostgreSQL Connection
**Severity:** P0 - Blocks all runtime testing

**Status:** ❌ Not running locally  
**Expected:** `Host=localhost;Port=5432;Database=ecosys_serviceops`  
**Command to Fix:** (User must start PostgreSQL locally)
```bash
# Option 1: If PostgreSQL is installed locally
pg_ctl start

# Option 2: If using WSL/VM
sudo service postgresql start

# Option 3: Verify connection
psql -h localhost -U postgres -c "SELECT version();"
```

**Impact:**  
- Cannot run `dotnet run` for backend API
- Cannot run integration tests
- Cannot test actual API endpoints
- Frontend cannot communicate with backend

### Blocker 2: Frontend Dependencies
**Status:** Not yet tested  
**Expected:** Node.js + npm installed

---

## Architecture Fixes Made

### Dependency Graph (After Fixes)
```
Api (Controllers)
  ↓ references
Infrastructure (Services, DbContext, DI)
  ↓ references
Shared (Interfaces, DTOs, Options, Observability)
  ↓ references
Domain (Entities)

✅ NO circular dependencies
✅ IRuntimeMetrics accessible to both Api and Infrastructure
```

---

## Files Changed

### Modified Files
1. `/c/ecosys/backend/src/Ecosys.Infrastructure/DependencyInjection.cs`
   - Added: `using Ecosys.Shared.Observability;`
   - Added: `services.AddSingleton<IRuntimeMetrics, RuntimeMetrics>();`

2. `/c/ecosys/backend/src/Ecosys.Api/Program.cs`
   - Added: `using Ecosys.Api.Observability;`
   - Added: `app.UseMiddleware<RequestMetricsMiddleware>();` (after auth, before controllers)

3. `/c/ecosys/backend/src/Ecosys.Api/Controllers/PlatformHealthController.cs`
   - Changed: `using Ecosys.Api.Observability;` → `using Ecosys.Shared.Observability;`

4. `/c/ecosys/backend/src/Ecosys.Api/Observability/RequestMetricsMiddleware.cs`
   - Added: `using Ecosys.Shared.Observability;`

### New Files
1. `/c/ecosys/backend/src/Ecosys.Shared/Observability/RuntimeMetrics.cs`
   - Interface: `IRuntimeMetrics`
   - Implementation: `RuntimeMetrics`
   - Record: `RequestWindowStats`

---

## Next Steps to Full Stability

### Phase 1: Get Database Running (User Action Required)
1. Start PostgreSQL locally (see Blocker 1 above)
2. Verify connection with: `psql -h localhost -U postgres -c "SELECT 1;"`
3. Create database if needed: `createdb -U postgres ecosys_serviceops`

### Phase 2: Test Backend Startup
```bash
cd /c/ecosys/backend
dotnet run --project src/Ecosys.Api
# Should see: Application started. Press Ctrl+C to shut down.
```

### Phase 3: Smoke Test API Endpoints
```bash
curl http://localhost:5072/health
curl http://localhost:5072/api/platform/health  # Requires auth

# Login endpoint (public)
curl -X POST http://localhost:5072/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"platform.owner@ecosys.test","password":"PlatformAdmin123!"}'
```

### Phase 4: Build Frontend
```bash
cd /c/ecosys/ecosys-serviceops-frontend
npm install
npm run build
npm run dev
```

### Phase 5: Trace "Unexpected error" Messages
Once frontend is running, reproduce errors and trace:
- Browser console logs
- Network tab (API responses)
- Backend logs for 5xx errors

### Phase 6: Fix Test Compilation
Once root API changes are understood, update tests to match current signatures.

---

## Failure Inventory

### Issue Summary

| Severity | Area | Symptom | Root Cause | Status |
|----------|------|---------|-----------|--------|
| P0 | DI | `IRuntimeMetrics` not registered | Missing registration + circular dependency | ✅ FIXED |
| P0 | Database | No PostgreSQL | Environment blocker | ⏳ BLOCKED |
| P1 | Testing | 32 test compilation errors | API signature mismatches | ⏳ PENDING |
| P2 | Metrics | Request metrics not recorded | Middleware not registered | ✅ FIXED |
| P3 | Warnings | 4 unread parameters in constructors | Legacy parameters | ⏸️ COSMETIC |

---

## Deployment Readiness

### Current Assessment: **NOT READY FOR DEPLOYMENT**

**Why:**
1. ❌ Cannot verify runtime behavior (no PostgreSQL)
2. ❌ Test suite fails to compile (indicates breaking API changes)
3. ❌ Frontend not tested
4. ✅ Build succeeds
5. ✅ DI configuration correct
6. ✅ Critical dependencies registered

**Path to Readiness:**
1. Get PostgreSQL running locally
2. Start API and verify health endpoint
3. Verify platform health endpoint works
4. Test login flow
5. Test key features (work orders, clients, etc.)
6. Fix or update failing tests
7. Full frontend testing

**Estimated Effort:** 2-4 hours (depends on PostgreSQL setup and test fixes)

---

## Notes for Next Debugging Session

1. **Test Compilation Errors Are Not a Blocker**
   - Focus on runtime behavior first
   - Tests can be debugged once API is running
   
2. **Known Breaking Changes**
   - Some API methods may have been removed/renamed
   - Check git history for recent controller changes:
     ```bash
     git log --oneline -- "src/Ecosys.Api/Controllers/Platform*.cs" | head -10
     ```

3. **Middleware Order Matters**
   - RequestMetricsMiddleware must run AFTER RequestServices are available
   - Must run BEFORE authorization so it sees all requests
   - Current position is correct

4. **PostgreSQL Connection String**
   - Configured in: `/c/ecosys/.claude/launch.json`
   - Also in environment: `ConnectionStrings__DefaultConnection`
   - Must match actual PostgreSQL setup

5. **Frontend Environment**
   - Backend URL: `http://127.0.0.1:5072` (from `.env`)
   - Verify this matches where you start the backend

---

**Report Generated:** 2026-09-11 17:30 UTC  
**Next Review:** After PostgreSQL setup and first API test
