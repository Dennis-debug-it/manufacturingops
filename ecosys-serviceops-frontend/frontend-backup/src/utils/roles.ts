import type { ApiRole } from '../types/api'
import type { KnownRole, Role } from '../types/app'
import type { PlatformRole } from '../types/platform'
import type { ApiPermissions } from '../types/api'

export type CanonicalPlatformRole = 'PlatformOwner' | 'PlatformAdmin' | 'SupportAdmin'
export type NormalizedAccessRole = KnownRole | 'unknown'

export const PLATFORM_ROLE_OPTIONS: Array<{
  value: CanonicalPlatformRole
  label: string
  description: string
}> = [
  {
    value: 'PlatformOwner',
    label: 'Platform Owner',
    description: 'Highest authority for platform settings, tenants, platform users, licenses, branding, templates, audit logs, and global configuration.',
  },
  {
    value: 'PlatformAdmin',
    label: 'Platform Admin',
    description: 'Operational administrator for onboarding, workspace requests, tenants, support actions, reports, and selected platform settings.',
  },
  {
    value: 'SupportAdmin',
    label: 'Support Admin',
    description: 'Support/helpdesk role for onboarding assistance, user access troubleshooting, system health checks, and credential resends.',
  },
]

const PLATFORM_ROLE_KEYS = new Set<KnownRole>(['superadmin', 'platformsuperadmin', 'platformowner', 'platformadmin', 'supportadmin'])
const KNOWN_ROLE_KEYS = new Set<KnownRole>([
  'superadmin',
  'platformsuperadmin',
  'platformowner',
  'platformadmin',
  'supportadmin',
  'tenantadmin',
  'admin',
  'technician',
  'user',
])

export function normalizeAppRole(role: string): ApiRole {
  const normalized = role.trim().toLowerCase().replace(/[\s_-]+/g, '')
  if (!normalized) return 'user'

  if (normalized === 'superadmin') return 'superadmin'
  if (normalized === 'platformsuperadmin') return 'platformsuperadmin'
  if (normalized === 'platformowner') return 'platformowner'
  if (normalized === 'platformadmin') return 'platformadmin'
  if (normalized === 'supportadmin' || normalized === 'support') return 'supportadmin'
  if (normalized === 'tenantadmin') return 'tenantadmin'
  if (normalized === 'admin') return 'admin'
  if (normalized === 'technician') return 'technician'

  return normalized as ApiRole
}

export function normalizeRole(role: string): NormalizedAccessRole {
  const normalized = normalizeAppRole(role)
  return KNOWN_ROLE_KEYS.has(normalized as KnownRole) ? (normalized as KnownRole) : 'unknown'
}

export function isKnownRole(role: Role) {
  return normalizeRole(String(role)) !== 'unknown'
}

export function isPlatformRole(role: Role) {
  return PLATFORM_ROLE_KEYS.has(normalizeRole(String(role)) as KnownRole)
}

type WorkOrderWorkspacePermissions = {
  canCreateWorkOrders?: boolean
  canAssignWorkOrders?: boolean
  canCompleteWorkOrders?: boolean
}

export type DashboardViewKey = 'operations' | 'contracts' | 'materials' | 'finance' | 'my-jobs'
export type DashboardViewMode = 'full' | 'limited' | 'my-jobs'
export type DashboardFocus = 'admin' | 'technician' | 'contracts' | 'finance' | 'materials' | 'operations' | 'general'

export type DashboardAccessProfile = {
  focus: DashboardFocus
  allowedViews: DashboardViewKey[]
  defaultView: DashboardViewKey
  canViewOperations: boolean
  canViewContracts: boolean
  canViewMaterials: boolean
  canViewFinance: boolean
}

function normalizeText(value?: string | null) {
  return `${value ?? ''}`.trim().toLowerCase()
}

function hasFinanceDashboardAccess(permissions?: Partial<ApiPermissions> | null) {
  return Boolean(
    permissions?.canViewFinance
    || permissions?.canManageFinance
    || permissions?.canViewFinanceRates
    || permissions?.canManageFinanceRates
    || permissions?.canViewFinanceApprovals
    || permissions?.canManageFinanceApprovals
    || permissions?.canViewBillableWork
    || permissions?.canManageBillableWork,
  )
}

function hasMaterialsDashboardAccess(permissions?: Partial<ApiPermissions> | null) {
  return Boolean(
    permissions?.canViewMaterials
    || permissions?.canRequestMaterials
    || permissions?.canManageMaterials
    || permissions?.canApproveMaterials
    || permissions?.canIssueMaterials,
  )
}

function hasOperationsDashboardAccess(permissions?: Partial<ApiPermissions> | null) {
  return Boolean(
    permissions?.canViewWorkOrders
    || permissions?.canCreateWorkOrders
    || permissions?.canAssignWorkOrders
    || permissions?.canCompleteWorkOrders,
  )
}

function hasTitleFocus(title: string, keywords: string[]) {
  return keywords.some((keyword) => title.includes(keyword))
}

function hasContractDashboardAccess(role: Role, title?: string | null, permissions?: Partial<ApiPermissions> | null) {
  const normalizedTitle = normalizeText(title)
  const normalizedRole = normalizeRole(String(role))

  return Boolean(
    normalizedRole === 'tenantadmin'
    || normalizedRole === 'admin'
    || hasFinanceDashboardAccess(permissions)
    || hasOperationsDashboardAccess(permissions)
    || hasTitleFocus(normalizedTitle, ['contract', 'commercial', 'renewal', 'account manager', 'client success'])
  )
}

function hasFinanceTitleAccess(title?: string | null) {
  const normalizedTitle = normalizeText(title)
  return hasTitleFocus(normalizedTitle, ['finance', 'billing', 'accounting', 'accounts', 'revenue'])
}

function hasMaterialsTitleAccess(title?: string | null) {
  const normalizedTitle = normalizeText(title)
  return hasTitleFocus(normalizedTitle, ['materials', 'store', 'inventory', 'warehouse', 'stock'])
}

function hasOperationsTitleAccess(title?: string | null) {
  const normalizedTitle = normalizeText(title)
  return hasTitleFocus(normalizedTitle, ['operations', 'service', 'dispatch', 'supervisor', 'coordinator', 'field', 'manager'])
}

export function isDashboardViewKey(value: unknown): value is DashboardViewKey {
  return value === 'operations' || value === 'contracts' || value === 'materials' || value === 'finance' || value === 'my-jobs'
}

export function dashboardViewPath(view: DashboardViewKey) {
  return `/dashboard?view=${view}`
}

function uniqueViews(views: Array<DashboardViewKey | null | undefined>) {
  const result: DashboardViewKey[] = []
  for (const view of views) {
    if (!view || result.includes(view)) continue
    result.push(view)
  }
  return result
}

export function getDashboardAccessProfile(
  role: Role,
  options?: { title?: string | null; permissions?: Partial<ApiPermissions> | null },
): DashboardAccessProfile {
  const normalizedRole = normalizeRole(String(role))
  const title = normalizeText(options?.title)
  const permissions = options?.permissions ?? null

  const technicianOnly = isTechnicianOnlyWorkspaceUser(role, options?.title, permissions)
  const isAdmin = normalizedRole === 'tenantadmin' || normalizedRole === 'admin'
  const canViewFinance = isAdmin || hasFinanceDashboardAccess(permissions) || hasFinanceTitleAccess(options?.title)
  const canViewMaterials = isAdmin || hasMaterialsDashboardAccess(permissions) || hasMaterialsTitleAccess(options?.title) || canViewFinance
  const canViewOperations = isAdmin || hasOperationsDashboardAccess(permissions) || hasOperationsTitleAccess(options?.title)
  const canViewContracts = isAdmin || hasContractDashboardAccess(role, options?.title, permissions)

  if (technicianOnly) {
    return {
      focus: 'technician',
      allowedViews: ['my-jobs'],
      defaultView: 'my-jobs',
      canViewOperations,
      canViewContracts,
      canViewMaterials,
      canViewFinance,
    }
  }

  let focus: DashboardFocus = 'general'
  if (isAdmin) {
    focus = 'admin'
  } else if (hasTitleFocus(title, ['contract', 'commercial', 'renewal']) && canViewContracts) {
    focus = 'contracts'
  } else if (canViewFinance) {
    focus = 'finance'
  } else if (hasMaterialsTitleAccess(options?.title) || hasMaterialsDashboardAccess(permissions)) {
    focus = 'materials'
  } else if (canViewOperations) {
    focus = 'operations'
  }

  const allowedViews = (() => {
    switch (focus) {
      case 'admin':
        return ['operations', 'contracts', 'materials', 'finance'] as DashboardViewKey[]
      case 'contracts':
        return uniqueViews([
          'contracts',
          canViewFinance ? 'finance' : null,
          canViewOperations ? 'operations' : null,
        ] as Array<DashboardViewKey | null | undefined>)
      case 'finance':
        return uniqueViews([
          'finance',
          'contracts',
          'materials',
          canViewOperations ? 'operations' : null,
        ] as Array<DashboardViewKey | null | undefined>)
      case 'materials':
        return uniqueViews([
          'materials',
          'operations',
        ] as Array<DashboardViewKey | null | undefined>)
      case 'operations':
        return uniqueViews([
          'operations',
          'materials',
          'contracts',
          canViewFinance ? 'finance' : null,
        ] as Array<DashboardViewKey | null | undefined>)
      default:
        return uniqueViews([
          canViewOperations ? 'operations' : null,
          canViewMaterials ? 'materials' : null,
          canViewContracts ? 'contracts' : null,
          canViewFinance ? 'finance' : null,
        ] as Array<DashboardViewKey | null | undefined>)
    }
  })()

  const fallbackViews = allowedViews.length > 0 ? allowedViews : ['operations' as DashboardViewKey]
  const defaultView = fallbackViews[0]

  return {
    focus,
    allowedViews: fallbackViews,
    defaultView,
    canViewOperations,
    canViewContracts,
    canViewMaterials,
    canViewFinance,
  }
}

export function getDashboardViewMode(view: DashboardViewKey, profile: DashboardAccessProfile): DashboardViewMode {
  if (view === 'my-jobs') {
    return 'my-jobs'
  }

  if (profile.focus === 'admin') {
    return 'full'
  }

  if (view === profile.defaultView) {
    return 'full'
  }

  return 'limited'
}

export function getDashboardViewMeta(view: DashboardViewKey, mode: DashboardViewMode) {
  const base = {
    operations: {
      label: 'Operations',
      title: 'Operations command view',
      description: 'Daily service execution, dispatch pressure, and technician load.',
    },
    contracts: {
      label: 'Contracts',
      title: 'Contracts command view',
      description: 'Lifecycle risk, renewals, and commercial coverage.',
    },
    materials: {
      label: 'Materials',
      title: 'Materials command view',
      description: 'Stock watch, replenishment, and issue control.',
    },
    finance: {
      label: 'Finance',
      title: 'Finance command view',
      description: 'Billable work and commercial exposure.',
    },
    'my-jobs': {
      label: 'My Jobs',
      title: 'My jobs command view',
      description: 'Assigned jobs, due today, overdue items, and field actions.',
    },
  }[view]

  if (mode !== 'limited') {
    return base
  }

  return {
    ...base,
    title: `${base.title} - Limited access`,
    description: `${base.description} Limited access view.`,
  }
}

export function getDashboardHomePath(
  role: Role,
  options?: { title?: string | null; permissions?: Partial<ApiPermissions> | null },
) {
  const normalized = normalizeRole(String(role))
  if (normalized === 'unknown') return '/unsupported-role'
  if (isPlatformRole(role)) return '/platform/tenants'
  return dashboardViewPath(getDashboardAccessProfile(role, options).defaultView)
}

export function isTechnicianOnlyWorkspaceUser(
  role: Role,
  title?: string | null,
  permissions?: WorkOrderWorkspacePermissions | null,
) {
  const normalized = normalizeRole(String(role))
  const hasManagementPermissions = Boolean(
    permissions?.canCreateWorkOrders || permissions?.canAssignWorkOrders,
  )

  if (hasManagementPermissions) {
    return false
  }

  const normalizedTitle = `${title ?? ''}`.trim().toLowerCase()
  if (normalized === 'technician' || normalizedTitle === 'technician') {
    return true
  }

  return Boolean(permissions?.canCompleteWorkOrders)
}

export function roleHomePath(role: Role, options?: { title?: string | null; permissions?: Partial<ApiPermissions> | null }) {
  const normalized = normalizeRole(String(role))
  if (normalized === 'unknown') return '/unsupported-role'
  if (PLATFORM_ROLE_KEYS.has(normalized as KnownRole)) return '/platform/tenants'
  return getDashboardHomePath(role, { title: options?.title, permissions: options?.permissions })
}

export function normalizePlatformRole(role: string): CanonicalPlatformRole {
  const normalized = role.trim().toLowerCase().replace(/[\s_-]+/g, '')

  if (normalized === 'platformowner' || normalized === 'superadmin' || normalized === 'platformsuperadmin') {
    return 'PlatformOwner'
  }

  if (normalized === 'supportadmin' || normalized === 'support') {
    return 'SupportAdmin'
  }

  return 'PlatformAdmin'
}

export function getPlatformRoleLabel(role: string | PlatformRole) {
  const normalized = normalizePlatformRole(String(role))
  return PLATFORM_ROLE_OPTIONS.find((item) => item.value === normalized)?.label ?? 'Platform Admin'
}

export function getPlatformRoleDescription(role: string | PlatformRole) {
  const normalized = normalizePlatformRole(String(role))
  return PLATFORM_ROLE_OPTIONS.find((item) => item.value === normalized)?.description
    ?? 'Operational administrator for platform onboarding, tenant management, and support workflows.'
}
