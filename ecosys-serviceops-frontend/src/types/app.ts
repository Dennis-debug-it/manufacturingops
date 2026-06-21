export type KnownRole =
  | 'superadmin'
  | 'platformsuperadmin'
  | 'platformadmin'
  | 'platformowner'
  | 'supportadmin'
  | 'tenantadmin'
  | 'admin'
  | 'technician'
  | 'user'

export type Role = KnownRole | (string & {})

export type ThemeMode = 'dark' | 'light'

export type Priority = 'Critical' | 'High' | 'Medium' | 'Low'

export type WorkOrderType =
  | 'Preventive Maintenance'
  | 'Reactive Maintenance'
  | 'New Projects'
  | 'Emergency Escalations'

export type WorkOrderStatus =
  | 'Open'
  | 'Assigned'
  | 'In Progress'
  | 'Acknowledged'
  | 'Closed'
  | 'On Hold'
  | 'Completed'
  | 'Cancelled'

export type MaterialRequestUrgency = 'Routine' | 'Priority' | 'Critical'

export type RequisitionStatus = 'Pending' | 'Approved' | 'Issued' | 'Cancelled'

export type SlaStatus = 'On Track' | 'At Risk' | 'Breached'

export type TechnicianStatus = 'Online' | 'Offline' | 'In Transit' | 'On Site' | 'Idle' | 'Overloaded'

export type TechnicianAvailability = 'Available' | 'Assigned' | 'Leave' | 'Offline'

export type AssetStatus = 'Operational' | 'Needs Attention' | 'Under Maintenance' | 'Offline'

export type InventoryAlert = 'Healthy' | 'Low' | 'Critical'

export type IntegrationStatus = 'Connected' | 'Attention' | 'Not Configured'

export type TenantStatus = 'Active' | 'Suspended'

export type SubscriptionStatus = 'Trial' | 'Active' | 'Past Due' | 'Suspended'

export type FeatureFlagStatus = 'Enabled' | 'Pilot' | 'Paused'

export type BadgeTone =
  | 'default'
  | 'info'
  | 'success'
  | 'warning'
  | 'danger'
  | 'neutral'

export interface SearchItem {
  id: string
  title: string
  subtitle: string
  path: string
  roles: Role[]
}

export interface AppSession {
  id: string
  accountId: string
  token?: string
  userId?: string
  tenantId?: string
  fullName: string
  name: string
  email: string
  role: Role
  tenantName: string
  tenantCode: string
  title: string
  branchId?: string
  defaultBranchId?: string
  avatar: string
  sessionStartedAt: string
  permissions?: UserPermissions
  branches?: AuthBranch[]
  department?: string
  hasAllBranchAccess?: boolean
  mustChangePassword: boolean
  country?: string
  industry?: string
  logoUrl?: string | null
  primaryColor?: string
  secondaryColor?: string
  faviconUrl?: string | null
}

export interface UserPermissions {
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
  canViewFinance?: boolean
  canManageFinance?: boolean
  canViewFinanceRates?: boolean
  canManageFinanceRates?: boolean
  canViewFinanceApprovals?: boolean
  canManageFinanceApprovals?: boolean
  canViewBillableWork?: boolean
  canManageBillableWork?: boolean
  canOverrideMaterialStock?: boolean
  canViewPlatformTenants?: boolean
  canCreatePlatformTenants?: boolean
  canEditPlatformTenants?: boolean
  canUpdatePlatformTenantStatus?: boolean
  canDeactivatePlatformTenants?: boolean
}

export interface AuthBranch {
  id: string
  name: string
  code: string
  location?: string | null
  isActive: boolean
}

export interface AuthAccount {
  id: string
  tenantId?: string
  userId?: string
  role: Role
  name: string
  email: string
  password: string
  title: string
  branchId?: string
  active: boolean
}

export interface SessionRecord {
  id: string
  accountId: string
  tenantId?: string
  role: Role
  startedAt: string
  lastActiveAt: string
  active: boolean
}

export interface NotificationItem {
  id: string
  title: string
  detail: string
  time: string
  level: 'info' | 'warning' | 'critical'
}

export interface Branch {
  id: string
  name: string
  code: string
  region: string
  city: string
  active: boolean
  siteCount: number
}

export interface CompanyProfile {
  companyName: string
  legalName: string
  supportEmail: string
  supportPhone: string
  country: string
  timezone: string
  address: string
}

export interface BrandingSettings {
  logoUrl: string
  primaryColor: string
  accentColor: string
}

export interface EmailIntegrationSettings {
  status: IntegrationStatus
  senderName: string
  senderEmail: string
  smtpHost: string
  lastCheckedAt: string
}

export interface SecuritySettings {
  sessionTimeoutHours: number
  maxConcurrentSessions: number
  mfaRequired: boolean
  passwordRotationDays: number
}

export interface InventorySettings {
  defaultStore: string
  approvalRequired: boolean
  stockAdjustmentRequiresReason: boolean
}

export interface NotificationSettings {
  criticalAlerts: boolean
  dailyDigest: boolean
  emailAcknowledgements: boolean
}

export interface WorkOrderRulesSettings {
  allowMissingClientWithReason: boolean
  allowMissingAssetWithReason: boolean
  requireAssignmentGroupBeforeDispatch: boolean
  requireClientAcknowledgementBeforeClosure: boolean
}

export interface PermissionGroup {
  id: string
  name: string
  permissions: string[]
}

export interface RolePermission {
  id: string
  role: 'Admin' | 'User'
  permissions: string[]
}

export interface NumberingRule {
  branchId: string
  branchName: string
  workOrderPrefix: string
  assetPrefix: string
  requisitionPrefix: string
  nextWorkOrderNumber: number
  nextAssetNumber: number
  nextRequisitionNumber: number
  resetRule: string
}

export interface TenantUser {
  id: string
  fullName: string
  email: string
  phone: string
  jobTitle: string
  role: 'Admin' | 'User'
  branchAccess: string[]
  permissionGroupIds: string[]
  active: boolean
  lastLoginAt?: string
}

export interface TechnicianGroup {
  id: string
  name: string
  branchIds: string[]
  supervisor: string
}

export interface AssetTemplate {
  id: string
  name: string
  category: string
  autoSchedulePm: boolean
  pmFrequencyDays: number
  checklistSummary: string
}

export interface PreventiveMaintenanceRule {
  id: string
  name: string
  assetCategory: string
  frequencyDays: number
  autoSchedule: boolean
  enabled: boolean
}

export interface ClientContact {
  id: string
  name: string
  email: string
  phone: string
  jobTitle: string
}

export interface ClientSite {
  id: string
  name: string
  branchId: string
  city: string
  region: string
  address: string
  active: boolean
}

export interface ClientRecord {
  id: string
  name: string
  branchId: string
  contacts: ClientContact[]
  sites: ClientSite[]
  slaRuleId: string
  emailIntegrationStatus: IntegrationStatus
  brandingLogoUrl: string
  notes: string
}

export interface AssetHistoryEntry {
  id: string
  workOrderId: string
  summary: string
  servicedAt: string
}

export interface AssetRecord {
  id: string
  assetCode: string
  name: string
  branchId: string
  clientId: string
  siteId: string
  category: string
  serialNumber: string
  status: AssetStatus
  pmScheduleDays: number
  autoSchedulePm: boolean
  lastServiceDate: string
  nextServiceDate: string
  templateId?: string
  maintenanceHistory: AssetHistoryEntry[]
  linkedWorkOrderIds: string[]
}

export interface TechnicianRecord {
  id: string
  fullName: string
  branchId: string
  groupId: string
  skills: string[]
  availability: TechnicianAvailability
  status: TechnicianStatus
  activeWorkOrderIds: string[]
  phone: string
}

export interface InventoryItem {
  id: string
  name: string
  sku: string
  branchId: string
  store: string
  quantity: number
  reorderLevel: number
  unit: string
  linkedWorkOrderIds: string[]
  lastUpdatedAt: string
}

export interface RequisitionRecord {
  id: string
  requisitionNumber: string
  workOrderId: string
  inventoryItemId: string
  itemName: string
  branchId: string
  store: string
  quantityRequested: number
  quantityIssued: number
  urgency: MaterialRequestUrgency
  remarks: string
  status: RequisitionStatus
  requestedBy: string
  requestedAt: string
  approvedAt?: string
  issuedAt?: string
}

export interface SlaRuleRecord {
  id: string
  name: string
  priorityLevel: Priority
  responseTimeHours: number
  resolutionTimeHours: number
  escalationPath: string
  clientIds: string[]
}

export interface WorkOrderNote {
  id: string
  body: string
  author: string
  createdAt: string
}

export interface ActivityTimelineEntry {
  id: string
  type:
    | 'created'
    | 'assignment'
    | 'status'
    | 'time'
    | 'note'
    | 'material'
    | 'completion'
    | 'cancellation'
  actor: string
  title: string
  detail: string
  createdAt: string
}

export interface WorkOrderRecord {
  id: string
  workOrderNumber: string
  branchId: string
  clientId: string
  siteId: string
  assetId?: string
  type: WorkOrderType
  status: WorkOrderStatus
  priority: Priority
  title: string
  description: string
  reportedBy: string
  technicianId?: string
  technicianGroupId?: string
  slaRuleId?: string
  slaStatus: SlaStatus
  responseDueAt: string
  resolutionDueAt: string
  arrivalTime?: string
  departureTime?: string
  closureSummary?: string
  clientAcknowledgement?: string
  createdAt: string
  updatedAt: string
  completedAt?: string
  cancelledAt?: string
  cancellationReason?: string
  notes: WorkOrderNote[]
  activity: ActivityTimelineEntry[]
}

export interface AuditLogEntry {
  id: string
  tenantId?: string
  actor: string
  action: string
  entityType: string
  entityId: string
  detail: string
  createdAt: string
}

export interface TenantFeatureFlag {
  id: string
  name: string
  audience: string
  status: FeatureFlagStatus
}

export interface SystemHealthRecord {
  id: string
  service: string
  status: 'Healthy' | 'Degraded' | 'Investigating'
  latency: string
}

export interface TenantRecord {
  id: string
  name: string
  code: string
  region: string
  plan: string
  status: TenantStatus
  subscriptionStatus: SubscriptionStatus
  activeSessionCount: number
  userCount: number
  featureFlags: TenantFeatureFlag[]
  createdAt: string
}

export interface TenantSettings {
  companyProfile: CompanyProfile
  branding: BrandingSettings
  emailIntegration: EmailIntegrationSettings
  security: SecuritySettings
  inventory: InventorySettings
  notifications: NotificationSettings
  workOrderRules: WorkOrderRulesSettings
  permissionGroups: PermissionGroup[]
  rolePermissions: RolePermission[]
  numberingRules: NumberingRule[]
  technicianGroups: TechnicianGroup[]
  assetTemplates: AssetTemplate[]
  pmRules: PreventiveMaintenanceRule[]
}

export interface TenantData {
  settings: TenantSettings
  branches: Branch[]
  users: TenantUser[]
  technicians: TechnicianRecord[]
  clients: ClientRecord[]
  assets: AssetRecord[]
  inventoryItems: InventoryItem[]
  workOrders: WorkOrderRecord[]
  requisitions: RequisitionRecord[]
  slaRules: SlaRuleRecord[]
  auditLog: AuditLogEntry[]
}

export interface PlatformAuditEntry {
  id: string
  actor: string
  action: string
  detail: string
  createdAt: string
}

export interface AppDatabase {
  version: number
  initializedAt: string
  tenants: TenantRecord[]
  tenantData: Record<string, TenantData>
  authAccounts: AuthAccount[]
  sessions: SessionRecord[]
  platformFeatureFlags: TenantFeatureFlag[]
  systemHealth: SystemHealthRecord[]
  platformAuditLog: PlatformAuditEntry[]
}
