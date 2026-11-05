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
  'canManageCrm',
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

/** Where an office URL lands for a technician-only user (old links, bookmarks, notification emails). */
export function techPathFor(pathname: string): string {
  const jobId = pathname.match(/^\/work-orders\/([^/]+)/)?.[1]
  if (jobId) return `/tech/jobs/${jobId}`
  if (pathname === '/my-time' || pathname.startsWith('/team/time-attendance')) return '/tech/time'
  const articleId = pathname.match(/^\/knowledge\/([^/]+)/)?.[1]
  if (articleId) return `/tech/guides/${articleId}`
  if (pathname.startsWith('/knowledge')) return '/tech/guides'
  if (pathname.startsWith('/help')) return '/tech/help'
  if (pathname === '/scan' || pathname.startsWith('/scan/')) return `/tech${pathname}`
  const assetId = pathname.match(/^\/assets\/([^/]+)/)?.[1]
  if (assetId) return `/tech/assets/${assetId}`
  if (pathname === '/notifications') return '/tech/notifications'
  if (pathname === '/me' || pathname.startsWith('/settings/account') || pathname.startsWith('/settings/notifications') || pathname.startsWith('/inventory/vans')) return '/tech/account'
  return '/tech'
}
