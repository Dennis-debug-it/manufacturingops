/** Tenant roles a user can be assigned. 'User' is a legacy alias treated as Viewer. */
export type TenantUserRole = 'Admin' | 'Manager' | 'Dispatcher' | 'Technician' | 'MaterialsController' | 'finance' | 'Viewer' | 'User'

export interface PermissionSetFields {
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
}

export interface TeamUser {
  id: string
  fullName: string
  email: string
  /** Sign-in handle unique within the tenant. */
  username: string | null
  /** False when `email` is a system-generated placeholder for a user with no real address. */
  hasEmail: boolean
  phoneNumber: string | null
  role: TenantUserRole
  jobTitle: string | null
  department: string | null
  isActive: boolean
  hasAllBranchAccess: boolean
  defaultBranchId: string | null
  defaultBranchName: string | null
  branchIds: string[]
  linkedTechnicianId: string | null
  assignmentGroups: Array<{ id: string; name: string; isLead: boolean }>
  createdAt: string
  updatedAt: string | null
  permissions: PermissionSetFields
  invitationStatus?: 'Invited' | 'Expired' | 'Active'
  invitationExpiresAt?: string | null
  mustChangePassword: boolean
  lastCredentialSentAt: string | null
}

export interface Technician {
  id: string
  branchId: string | null
  branchName: string | null
  fullName: string
  email: string
  phone: string | null
  skillCategory: string | null
  assignmentGroup: string | null
  lastKnownLatitude: number | null
  lastKnownLongitude: number | null
  lastLocationAt: string | null
  isTrackingActive: boolean
  activeWorkOrderId: string | null
  isActive: boolean
  createdAt: string
  /** The sign-in account behind this technician, if any. */
  userId?: string | null
}

export interface CertificationDefinition {
  id: string
  name: string
  issuer: string | null
  description: string | null
  renewalPeriodMonths: number | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
}

export interface TeamManagementCertificationAssignment {
  certificationId: string
  certificationName: string
  issuer: string | null
  certificateNumber: string | null
  issuedOn: string | null
  expiresOn: string | null
  notes: string | null
  status: 'Valid' | 'ExpiringSoon' | 'Expired'
}

export interface TeamManagementProfile {
  userId: string
  technicianId: string | null
  fullName: string
  email: string
  role: string
  isUserActive: boolean
  isWorkforceActive: boolean
  branchId: string | null
  branchName: string | null
  availabilityStatus: 'Available' | 'Limited' | 'Unavailable' | 'NotReady'
  availabilityNote: string | null
  availabilityUntil: string | null
  availabilityUpdatedAt: string | null
  assignmentGroups: Array<{ id: string; name: string; isLead: boolean }>
  certifications: TeamManagementCertificationAssignment[]
  activeWorkOrderCount: number
  pendingResponseCount: number
  onSiteCount: number
  isTrackingActive: boolean
  lastLocationAt: string | null
  readinessStatus: 'Ready' | 'Limited' | 'Unavailable'
  readinessReasons: string[]
}

export interface TeamManagementOverview {
  profiles: TeamManagementProfile[]
  certifications: CertificationDefinition[]
}

export interface AssignmentGroupMember {
  id: string
  assignmentGroupId: string
  userId: string | null
  technicianId: string
  memberName: string | null
  email: string | null
  isLead: boolean
  isActive: boolean
  addedAt: string
}

export interface AssignmentGroup {
  id: string
  name: string
  description: string | null
  skillArea: string | null
  branchId: string | null
  branchName: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string | null
  members: AssignmentGroupMember[]
}
