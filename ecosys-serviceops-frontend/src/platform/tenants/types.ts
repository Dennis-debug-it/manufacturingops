export interface PlatformTenantListItem {
  tenantId: string
  name: string
  slug: string
  contactName: string | null
  contactEmail: string | null
  planName: string | null
  licenseStatus: string
  userCount: number
  branchCount: number
  status: string
  trialStartsAt: string | null
  trialEndsAt: string | null
  trialExtensionUsed: boolean
  trialExtendedAt: string | null
  trialDaysRemaining: number | null
  trialStatus: string
  createdAt: string
}

export interface PlatformTenantDetail extends PlatformTenantListItem {
  contactPhone: string | null
  country: string | null
  county: string | null
  city: string | null
  address: string | null
  taxPin: string | null
  maxUsers: number | null
  maxBranches: number | null
  subscriptionStartsAt: string | null
  subscriptionEndsAt: string | null
  updatedAt: string | null
  suspendedAt: string | null
  deactivatedAt: string | null
  workOrderCount: number
  activeUsersNow: number
  lastActivityAt: string | null
  initialAdminInvitationSent: boolean | null
  initialAdminInvitationMessage: string | null
}

export interface PlatformTenantAuditLog {
  id: string
  action: string
  actor: string
  details: string | null
  createdAt: string
}
