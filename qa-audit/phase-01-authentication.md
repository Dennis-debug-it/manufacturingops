# Phase 1 — Authentication (Ecosys ServiceOps)

Tested against https://app.ecosysdigital.co.ke on 2026-08-02.
Tenant account: excelonsystemske@gmail.com (Role: Admin, Tenant 9e32ecfb…).

## What works well (verified positives)
- **Account lockout**: 5 failed logins → 15-min lock (`AuthSecurityOptions`, `MvpAuthService`).
- **Login rate limiting**: 20 requests / 60s fixed window on auth endpoints (`Program.cs` `AuthEndpoints`).
- **No user enumeration**: unknown email and wrong password both return identical `401 "Invalid email or password."`.
- **Server-side session revocation**: after `POST /api/auth/logout`, the *same* JWT returns `401` on `/api/auth/me`. Verified live.
- **Fail-closed session heartbeat**: `UserSessionHeartbeatMiddleware` 401s on revoked session and 503s if validity can't be confirmed.
- **Change-password** revokes other sessions and audit-logs the event.

## Findings

### F1-01 — HIGH — Deactivating a user does not terminate active sessions
- **Category**: Security / Broken Authorization
- **Location**: `UsersController.Update` (src/Ecosys.Api/Controllers/UsersController.cs:198), `SessionServices.TouchAsync`, `UserSessionHeartbeatMiddleware`
- **Root cause**: `Update` sets `user.IsActive=false` and changes Role/Permission, then `SaveChanges` — but never revokes the user's `UserSessions`. `TouchAsync` validates only `IsRevoked`/`LogoutAt`, not `user.IsActive`. Permissions are carried in the JWT claims, not reloaded per request.
- **Impact**: A deactivated or downgraded user keeps a fully working token — with their **original** permissions — until the JWT expires (up to 8h). Defeats offboarding and compromised-account revocation.
- **Fix**: On deactivation and on any role/permission change, call `RevokeAllForUserAsync(userId)`. Additionally, `TouchAsync` should join `Users` and fail closed when `!user.IsActive`.

### F1-02 — MEDIUM — Permissions embedded in 8h JWT; changes are stale until expiry
- **Category**: Security / Authorization design
- **Location**: JWT claims (`can_*`, `finance.*`, role); `UserSessionHeartbeatMiddleware` reloads session but not permissions.
- **Root cause**: All permission flags are baked into the access token (lifetime = exp−nbf = 28800s = 8h). Authantication middleware validates session existence but reads authorization from claims.
- **Impact**: Granting/revoking a permission or changing a role has no effect for up to 8h (unless sessions are revoked, which they currently are not — see F1-01).
- **Fix**: Either revoke sessions on permission/role change, or shorten access-token lifetime + add refresh, or re-materialize permissions from DB per request.

### F1-03 — MEDIUM — "Remember Me" is a listed requirement but not implemented
- **Category**: Functional / Missing feature
- **Location**: Login page (`/`), no "Remember me" control; no refresh token in auth flow.
- **Impact**: Fixed 8h token with no refresh — users are hard-logged-out after 8h with no persistent-session option. Audit checklist item unmet.
- **Fix**: Add refresh-token flow + "Remember me" (longer refresh lifetime) or document as out-of-scope.

### F1-04 — LOW — Access token stored in localStorage (XSS-exfiltratable)
- **Category**: Security / Sensitive-data exposure
- **Location**: `localStorage['ecosys.serviceops.token']` and `…session`.
- **Root cause**: SPA stores JWT in localStorage rather than httpOnly cookie.
- **Impact**: Any XSS gives an attacker the bearer token. Mitigated by DOMPurify usage in the frontend, but the blast radius of an XSS is full account takeover for the token lifetime. Standard SPA tradeoff — flag, not a blocker.
- **Fix**: Consider httpOnly + SameSite cookie for the token, or short access token + httpOnly refresh.

## Not-yet-tested in Phase 1 (carry-over)
- Registration/signup validation edge cases (endpoint `POST /api/auth/signup` exists — self-serve tenant creation; needs abuse review).
- Forgot/reset password token entropy & single-use (code review pending: `PasswordResetToken`, `PasswordResetService`).
