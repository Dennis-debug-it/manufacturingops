import type { AuthUser, PermissionSet } from '../types/auth'

/** Any of these marks a user as office staff rather than a field technician. */
const MANAGEMENT_PERMISSIONS: (keyof PermissionSet)[] = [
  'canCreateWorkOrders',
  'canAssignWorkOrders',
  'canManageKnowledge',
  'canManageMaterials',
  'canApproveMaterials',
  'canIssueMaterials',
  'canManageAssets',
  'canManageSettings',
  'canViewReports',
  'canViewFinance',
  'canManageFinance',
  'canViewFinanceRates',
  'canManageFinanceRates',
  'canViewFinanceApprovals',
  'canManageFinanceApprovals',
  'canViewBillableWork',
  'canManageBillableWork',
  'canOverrideMaterialStock',
]

/**
 * Mirrors the API's IsTechnicianOnlyWorkspaceUser: someone who can complete work orders but holds
 * no management permission. The API refuses these users the office work-order screens and serves
 * their jobs through /api/technician/jobs instead, so they get the /tech workspace.
 */
export function isTechnicianWorkspaceUser(user: AuthUser | null | undefined): boolean {
  if (!user || user.role === 'admin' || user.role === 'super_admin' || user.role === 'platform_sales') {
    return false
  }
  if (MANAGEMENT_PERMISSIONS.some((key) => user.permissions[key])) {
    return false
  }
  return Boolean(user.permissions.canCompleteWorkOrders)
}
