import type { AppRole } from '../types/auth'

/**
 * The backend's tenant user model is binary: `User.Role` is only ever "Admin" or
 * "User" (see `AppRoles.TenantRoles` / `UsersController.NormalizeRole` server-side —
 * confirmed live against `/api/auth/me`, which returns `role: "Admin"` even for a
 * full-permission demo "manager" account). Real capability granularity lives in the
 * per-user `PermissionSet` (`canAssignWorkOrders`, `canManageFinance`, etc.), not in
 * the role string.
 *
 * The frontend's `AppRole` union predates this discovery and assumes six distinct
 * tenant roles (manager/dispatcher/technician/finance/viewer) for nav visibility and
 * `ProtectedRoute` gating. Rather than rewriting that shell-level plumbing, this
 * normalizes whatever the backend sends into the closest matching `AppRole` so the
 * existing shell keeps working — but any *action-level* gating should prefer
 * `usePermission()` (backed by the real `PermissionSet`) over `user.role` checks.
 */
export function normalizeRole(rawRole: string): AppRole {
  const value = rawRole.trim().toLowerCase().replace(/[\s_-]+/g, '')

  switch (value) {
    case 'admin':
      return 'admin'
    case 'manager':
      return 'manager'
    case 'dispatcher':
      return 'dispatcher'
    case 'technician':
      return 'technician'
    // Tenant finance clerk. Ecosys finance staff are a different role entirely and are handled
    // with the other platform roles below — mapping them here showed platform staff a tenant shell.
    case 'finance':
      return 'finance'
    case 'viewer':
    case 'auditor':
    case 'readonlyauditor':
      return 'viewer'
    // Generic tenant staff account with no admin flag — the seeded demo technician
    // account uses this. Default to 'technician' since operational execution access
    // (work-orders.execute) is gated by role in usePermission, and that's the most
    // common shape for a non-admin tenant user; finer-grained UI should still check
    // usePermission()/PermissionSet rather than relying on this bucket.
    case 'user':
      return 'technician'
    case 'superadmin':
    case 'platformsuperadmin':
    case 'platformowner':
    case 'platformadmin':
    case 'platformfinance':
    case 'financeadmin':
      return 'super_admin'
    case 'platformsales':
      return 'platform_sales'
    default:
      return 'viewer'
  }
}
