export type TenantRole = 'admin' | 'manager' | 'dispatcher' | 'technician' | 'materials_controller' | 'finance' | 'viewer'
export type PlatformRole = 'super_admin' | 'platform_sales'
export type AppRole = TenantRole | PlatformRole

export interface PermissionSet {
  canViewWorkOrders: boolean
  canCreateWorkOrders: boolean
  canAssignWorkOrders: boolean
  canCompleteWorkOrders: boolean
  canViewMaterials: boolean
  canRequestMaterials: boolean
  canManageMaterials: boolean
  canApproveMaterials: boolean
  canIssueMaterials: boolean
  canManageAssets: boolean
  canManageSettings: boolean
  canViewReports: boolean
  canViewFinance: boolean
  canManageFinance: boolean
  canViewFinanceRates: boolean
  canManageFinanceRates: boolean
  canViewFinanceApprovals: boolean
  canManageFinanceApprovals: boolean
  canOverrideMaterialStock: boolean
  canViewPlatformTenants: boolean
  canCreatePlatformTenants: boolean
  canEditPlatformTenants: boolean
  canUpdatePlatformTenantStatus: boolean
  canDeactivatePlatformTenants: boolean
}

export interface AuthUser {
  userId: string
  fullName: string
  email: string
  /** Sign-in handle unique within the tenant. Null only for accounts created before usernames existed. */
  username: string | null
  /** False when `email` is a system-generated placeholder — this account signs in with `username` instead. */
  hasEmail: boolean
  role: AppRole
  jobTitle: string | null
  department: string | null
  hasAllBranchAccess: boolean
  defaultBranchId: string | null
  mustChangePassword: boolean
  permissions: PermissionSet
  /**
   * Granted capabilities that have no `PermissionSet` field, such as `activities.view`. Sent by
   * login and /api/auth/me; absent on sessions stored before the field existed.
   */
  capabilities?: string[]
}

export interface AuthTenant {
  tenantId: string
  companyName: string
  country: string
  industry: string | null
  logoUrl: string | null
  /** Effective IANA zone the workspace's timestamps are displayed in (e.g. 'Africa/Nairobi'). */
  timeZoneId?: string | null
  /** Workspace currency configured by the tenant. Null when unset, so the client falls back. */
  defaultCurrency?: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

export interface AuthBranch {
  id: string
  name: string
  code: string
  location: string | null
  isActive: boolean
}

export interface ImpersonationMeta {
  impersonatedByUserId: string
  impersonatedByName: string
  startedAt: string
}

export interface AuthSession {
  token: string
  user: AuthUser
  tenant: AuthTenant
  branches: AuthBranch[]
  impersonation?: ImpersonationMeta
}

export interface LoginRequest {
  /** Either an email (globally unique) or a username (unique only within a tenant). */
  emailOrUsername: string
  password: string
  /** The tenant's workspace code — required only when signing in with a username, not an email. */
  workspace?: string
  rememberMe?: boolean
}

export interface LoginResponse {
  token: string
  refreshToken: string
  user: AuthUser
  tenant: AuthTenant
}

export interface MeResponse extends LoginResponse {
  branches: AuthBranch[]
  impersonation?: ImpersonationMeta
}

export interface TenantImpersonationResponse {
  token: string
  user: AuthUser
  tenant: AuthTenant
  impersonatedByUserId: string
  impersonatedByName: string
  startedAt: string
}
