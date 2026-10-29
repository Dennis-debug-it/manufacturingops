import { useMemo } from 'react'
import { useAuth } from './useAuth'
import type { AppRole, AuthUser, PermissionSet } from '../types/auth'

/**
 * RBAC convention (see FRONTEND_BUILD_PLAN.md P1-3 / P8-4):
 *
 * 1. For actions the backend already computes per-user (`AuthUser.permissions`,
 *    a `PermissionSet` returned by `/api/auth/login` and `/api/auth/me`), map the
 *    frontend action string to the matching `PermissionSet` key in
 *    `permissionSetLookup` below. This is authoritative — it reflects the same
 *    `PermissionNames.*` checks the backend controllers enforce, so the button
 *    visibility matches what the server will actually allow.
 * 2. For coarser, module/nav-level actions with no backend-computed equivalent
 *    (e.g. `dashboard.view`, `clients.view`), fall back to the static
 *    `rolePermissions` role -> action-string map.
 * 3. Every new action string introduced anywhere in the app MUST be added to one
 *    of the two maps in the same change — do not call `usePermission('some.new.string')`
 *    without a corresponding entry here.
 */

const rolePermissions: Record<AppRole, string[]> = {
  admin: ['*'],
  manager: [
    'dashboard.view',
    'work-orders.view',
    'work-orders.edit',
    'clients.view',
    'clients.edit',
    'inventory.view',
    'inventory.edit',
    'team.view',
    'team.manage',
    'finance.view',
    'reports.view',
    'settings.view',
    'contracts.view',
    'contracts.manage',
    'contracts.approve',
    'maintenance.view',
    'maintenance.manage',
    'maintenance.trigger',
    'whatsapp.view',
    'dispatch.view',
  ],
  dispatcher: [
    'dashboard.view',
    'work-orders.view',
    'work-orders.edit',
    'clients.view',
    'clients.edit',
    'inventory.view',
    'inventory.edit',
    'maintenance.view',
    'maintenance.trigger',
    'team.view',
    'whatsapp.view',
    'dispatch.view',
  ],
  technician: [
    'dashboard.view',
    'work-orders.view',
    'work-orders.edit',
    'inventory.view',
    'inventory.consume',
    'maintenance.view',
  ],
  // Stores and logistics. Stock, issuing, purchase orders and transfers are gated by the real
  // materials permissions (permissionSetLookup); these are the menu-level views.
  materials_controller: [
    'dashboard.view',
    'work-orders.view',
    'inventory.view',
    'inventory.edit',
    'inventory.consume',
    'maintenance.view',
  ],
  finance: [
    'dashboard.view',
    'clients.view',
    'clients.edit',
    'finance.view',
    'reports.view',
    'contracts.view',
  ],
  viewer: [
    'dashboard.view',
    'work-orders.view',
    'clients.view',
    'inventory.view',
    'team.view',
    'finance.view',
    'reports.view',
    'settings.view',
    'maintenance.view',
    'contracts.view',
    'whatsapp.view',
  ],
  super_admin: ['*'],
  platform_sales: [
    'platform.dashboard.view',
    'platform.crm.view',
    'platform.crm.manage',
    'platform.tenants.view',
    'platform.finance.view',
  ],
}

/** Action strings backed by a real per-user `PermissionSet` field from the backend. */
const permissionSetLookup: Partial<Record<string, keyof PermissionSet>> = {
  'work-orders.create': 'canCreateWorkOrders',
  'work-orders.assign': 'canAssignWorkOrders',
  'work-orders.start': 'canCompleteWorkOrders',
  'work-orders.complete': 'canCompleteWorkOrders',
  'knowledge.view': 'canViewKnowledge',
  'knowledge.manage': 'canManageKnowledge',
  'materials.view': 'canViewMaterials',
  'materials.request': 'canRequestMaterials',
  'materials.manage': 'canManageMaterials',
  'materials.approve': 'canApproveMaterials',
  'materials.issue': 'canIssueMaterials',
  'assets.manage': 'canManageAssets',
  'settings.manage': 'canManageSettings',
  'reports.view': 'canViewReports',
  'finance.view': 'canViewFinance',
  'finance.manage': 'canManageFinance',
  'finance.rates.view': 'canViewFinanceRates',
  'finance.rates.manage': 'canManageFinanceRates',
  'finance.approvals.view': 'canViewFinanceApprovals',
  'finance.approvals.manage': 'canManageFinanceApprovals',
  'finance.billable-work.view': 'canViewBillableWork',
  'finance.billable-work.manage': 'canManageBillableWork',
  'platform.tenants.view': 'canViewPlatformTenants',
  'platform.tenants.create': 'canCreatePlatformTenants',
  'platform.tenants.edit': 'canEditPlatformTenants',
  'platform.tenants.status': 'canUpdatePlatformTenantStatus',
  'platform.tenants.deactivate': 'canDeactivatePlatformTenants',
  'inventory.requests.approve': 'canApproveMaterials',
  'inventory.requests.issue': 'canIssueMaterials',
  'parts-requests.create': 'canRequestMaterials',
  'parts-requests.approve': 'canApproveMaterials',
  'parts-requests.dispatch': 'canIssueMaterials',
  'parts-returns.receive': 'canIssueMaterials',
  'inventory.issues.approve': 'canApproveMaterials',
  'inventory.issues.issue': 'canIssueMaterials',
  'inventory.transfers.manage': 'canManageMaterials',
  'inventory.materials.manage': 'canManageMaterials',
  'pm-templates.manage': 'canManageSettings',
  'crm.view': 'canViewCrm',
  'crm.manage': 'canManageCrm',
}

/**
 * Work order execution actions (arrive/depart, execution notes, materials-used,
 * photo/signature capture, pause/resume/cancel, timeline comments) only require
 * `EnsureTenantOperationalAccess` server-side — any tenant role except `viewer`.
 * There is no dedicated `PermissionSet` field for this, so it's role-gated here.
 * Closing a work order is deliberately absent: the server only lets tenant admins move a work
 * order from Completed to Closed, and admins already pass every check above.
 */
const operationalRoles: AppRole[] = ['admin', 'manager', 'dispatcher', 'technician', 'super_admin']
const executeActions = new Set([
  'work-orders.execute',
  'work-orders.arrive',
  'work-orders.depart',
  'work-orders.hold',
  'work-orders.resume',
  'work-orders.cancel',
  'work-orders.timeline.comment',
  'work-orders.evidence.upload',
  'work-orders.signatures.capture',
  'work-orders.materials.log',
  'work-orders.report.generate',
  'parts-requests.deliver',
  'parts-returns.create',
])

/**
 * Action strings that are backend capabilities with no `PermissionSet` field. The server sends the
 * granted ones in `AuthUser.capabilities` (role defaults adjusted by Access Control), so they are
 * checked there rather than against the static role map.
 */
const capabilityActions = new Set([
  'projects.view',
  'projects.manage',
  'projects.variations.submit',
  'projects.variations.approve',
  'projects.close',
  'projects.settings',
  'projects.muster.mark',
  'projects.financials.view',
  'projects.financials.manage',
  'activities.view',
  'activities.manage',
  'calendar.team',
  'crm.view_all',
])

/** The same check as usePermission, for code that isn't a component (e.g. route access rules). */
export function hasPermission(user: AuthUser | null | undefined, action: string): boolean {
  if (!user) {
    return false
  }

  if (user.role === 'admin' || user.role === 'super_admin') {
    return true
  }

  const permissionSetKey = permissionSetLookup[action]
  if (permissionSetKey) {
    return Boolean(user.permissions[permissionSetKey])
  }

  if (capabilityActions.has(action)) {
    return user.capabilities?.includes(action) ?? false
  }

  if (executeActions.has(action)) {
    return operationalRoles.includes(user.role)
  }

  const permissions = rolePermissions[user.role]
  return permissions.includes('*') || permissions.includes(action)
}

export function usePermission(action: string): boolean {
  const { user } = useAuth()
  return useMemo(() => hasPermission(user, action), [action, user])
}
