export type TenantRole = 'admin' | 'manager' | 'dispatcher' | 'technician' | 'finance' | 'viewer'
export type PlatformRole = 'super_admin' | 'platform_sales'
export type AppRole = TenantRole | PlatformRole

export interface PermissionSet {
  canViewWorkOrders: boolean
  canCreateWorkOrders: boolean
  canAssignWorkOrders: boolean
  canCompleteWorkOrders: boolean
  canViewKnowledge: boolean
  canManageKnowledge: boolean
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
  canViewBillableWork: boolean
  canManageBillableWork: boolean
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
  role: AppRole
  jobTitle: string | null
  department: string | null
  hasAllBranchAccess: boolean
  defaultBranchId: string | null
  mustChangePassword: boolean
  permissions: PermissionSet
}

export interface AuthTenant {
  timeZoneId?: string | null
  tenantId: string
  companyName: string
  country: string
  industry: string | null
  logoUrl: string | null
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
  email: string
  password: string
}

export interface LoginResponse {
  token: string
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
