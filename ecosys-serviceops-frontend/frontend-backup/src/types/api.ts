export type ApiRole =
  | 'superadmin'
  | 'platformsuperadmin'
  | 'platformadmin'
  | 'platformowner'
  | 'supportadmin'
  | 'tenantadmin'
  | 'admin'
  | 'technician'
  | 'user'
  | (string & {})

export interface ApiPermissions {
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

export interface ApiBranch {
  id: string
  name: string
  code: string
  location?: string | null
  isActive: boolean
}

export interface ApiTenantContext {
  tenantId: string
  companyName: string
  country: string
  industry?: string | null
  logoUrl?: string | null
  primaryColor?: string
  secondaryColor?: string
  showPoweredByEcosys?: boolean
}

export interface ApiUserContext {
  userId: string
  fullName: string
  email: string
  role: string
  jobTitle?: string | null
  department?: string | null
  hasAllBranchAccess?: boolean
  defaultBranchId?: string | null
  mustChangePassword?: boolean
  permissions: ApiPermissions
}

export interface LoginResponse {
  token: string
  user?: ApiUserContext
  tenant?: ApiTenantContext
}

export interface ChangePasswordInput {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface SignupResponse {
  token: string
  tenantId: string
  userId: string
  companyName: string
  role: string
  jobTitle?: string | null
  permissions: ApiPermissions
}

export interface MeResponse {
  user: ApiUserContext
  tenant: ApiTenantContext
  branches: ApiBranch[]
}

export interface DashboardSummary {
  openWorkOrders: number
  closedWorkOrders: number
  overdueWorkOrders: number
  assets: number
  clients: number
  materialsLowStock: number
  unassignedWorkOrders: number
  assignedToGroup: number
  assignedToTechnicians: number
  awaitingAcceptance: number
  techniciansOnSite: number
  contractsExpiringIn90Days: number
  contractsExpiringIn30Days: number
  criticalRenewalRiskContracts: number
  expiredContracts: number
  renewalInProgressContracts: number
  workOrdersByGroup: Array<{
    groupName: string
    count: number
  }>
  technicianWorkload: Array<{
    technicianName: string
    activeWorkOrders: number
    pendingResponses: number
    onSiteCount: number
  }>
}

export interface WorkOrderTechnicianAssignmentRecord {
  id: string
  technicianId: string
  technicianName?: string | null
  isLead: boolean
  status: string
  assignedAt: string
  acceptedAt?: string | null
  arrivalAt?: string | null
  departureAt?: string | null
  notes?: string | null
}

export interface WorkOrderAssignmentHistoryRecord {
  id: string
  action: string
  fromGroupId?: string | null
  fromGroupName?: string | null
  toGroupId?: string | null
  toGroupName?: string | null
  fromTechnicianId?: string | null
  fromTechnicianName?: string | null
  toTechnicianId?: string | null
  toTechnicianName?: string | null
  performedByUserId?: string | null
  performedByUserName?: string | null
  performedAt: string
  notes?: string | null
}

export type WorkOrderTypeValue =
  | 'NewInstallation'
  | 'PreventiveMaintenance'
  | 'CorrectiveMaintenance'
  | 'InspectionAssessment'
  | (string & {})

export interface WorkOrder {
  id: string
  branchId?: string | null
  branchName?: string | null
  clientId: string
  clientName?: string | null
  siteId?: string | null
  siteName?: string | null
  systemId?: string | null
  systemName?: string | null
  assetId?: string | null
  assetName?: string | null
  assignmentGroupId?: string | null
  assignmentGroupName?: string | null
  workOrderNumber: string
  title: string
  description?: string | null
  priority: string
  status: string
  workType?: WorkOrderTypeValue | null
  slaStatus: string
  assignmentType: 'IndividualTechnician' | 'MultipleTechnicians' | 'AssignmentGroup' | 'Unassigned'
  assignedTechnicianId?: string | null
  assignedTechnicianName?: string | null
  assignedTechnicianIds: string[]
  leadTechnicianId?: string | null
  leadTechnicianName?: string | null
  assignmentStatus: string
  assignmentNotes?: string | null
  technicianAssignments?: WorkOrderTechnicianAssignmentRecord[] | null
  assignmentSummary?: string | null
  isUnassigned?: boolean
  dueDate?: string | null
  scheduledStartAt?: string | null
  scheduledEndAt?: string | null
  createdAt: string
  workStartedAt?: string | null
  arrivalAt?: string | null
  departureAt?: string | null
  completedAt?: string | null
  workDoneNotes?: string | null
  jobCardNotes?: string | null
  slaResponseDeadline?: string | null
  slaResolutionDeadline?: string | null
  slaResponseBreached: boolean
  slaResolutionBreached: boolean
  slaResponseBreachedAt?: string | null
  slaResolutionBreachedAt?: string | null
  acknowledgedByName?: string | null
  acknowledgementComments?: string | null
  acknowledgementDate?: string | null
  isPreventiveMaintenance: boolean
  holdReason?: string | null
  holdNote?: string | null
  pmTemplateId?: string | null
  pmTemplateName?: string | null
  pmTemplateVersion?: number | null
  preventiveMaintenancePlanId?: string | null
  checklistItems?: WorkOrderChecklistItemRecord[] | null
  billingStatus?: 'Unbilled' | 'PartiallyBilled' | 'Billed' | 'NotBillable' | string | null
  isBillable?: boolean | null
  notifyClient: boolean
  clientContactEmail?: string | null
  clientContactPerson?: string | null
  clientContactPhone?: string | null
  clientSlaPlan?: string | null
  siteCode?: string | null
  siteContactPerson?: string | null
  siteContactPhone?: string | null
  siteOperatingHours?: string | null
  siteAccessNotes?: string | null
  siteLocation?: string | null
  assetCode?: string | null
  assetType?: string | null
  assetCategoryName?: string | null
  assetSerialNumber?: string | null
  assetManufacturer?: string | null
  assetModel?: string | null
  assetLocation?: string | null
  assetWarrantyExpiryDate?: string | null
  assetLastServiceDate?: string | null
  assetNextServiceDate?: string | null
  assetWarrantyStatus?: string | null
  assetCriticality?: string | null
  availableTransitions?: string[] | null
  contractId?: string | null
  contractNumber?: string | null
  contractStatus?: string | null
  coverageStatus?: string | null
  billingModel?: string | null
  rateCardName?: string | null
  contractEndDate?: string | null
  contractRenewalRisk?: string | null
  pricingNotes?: string | null
  estimatedCharge?: number | null
  finalCharge?: number | null
  appliedSlaSource?: string | null
  appliedRateSource?: string | null
}

export type ContractType =
  | 'ComprehensiveMaintenance'
  | 'PreventiveMaintenance'
  | 'CorrectiveCallout'
  | 'WarrantySupport'
  | 'Inspection'
  | 'InstallationSupport'
  | 'SlaSupport'
  | 'AdhocPayAsYouGo'
  | (string & {})

export type ContractStatus =
  | 'Draft'
  | 'PendingApproval'
  | 'Active'
  | 'ExpiringSoon'
  | 'RenewalInProgress'
  | 'Renewed'
  | 'Expired'
  | 'Suspended'
  | 'Terminated'
  | 'Cancelled'
  | (string & {})

export type ContractBillingModel =
  | 'Included'
  | 'PreferentialRates'
  | 'TimeAndMaterials'
  | 'FixedPrice'
  | 'PerVisit'
  | 'Warranty'
  | 'ChargeableCallout'
  | 'Retainer'
  | (string & {})

export type ContractRenewalBehaviorValue =
  | 'AutoRenew'
  | 'ManualRenewalRequired'
  | 'AutoExpire'
  | (string & {})

export type ContractGracePeriodPolicyValue =
  | 'None'
  | 'PreferentialRates'
  | 'Sla'
  | 'Both'
  | (string & {})

export type CoverageStatusValue = 'Covered' | 'PartiallyCovered' | 'NotCovered' | (string & {})
export type RenewalRiskValue = 'Low' | 'Medium' | 'High' | 'Critical' | 'Expired' | (string & {})
export type PeriodTypeValue = 'Monthly' | 'Quarterly' | 'SemiAnnual' | 'Annual' | (string & {})
export type ContractLifecycleEventType =
  | 'Created'
  | 'SubmittedForApproval'
  | 'Approved'
  | 'Activated'
  | 'MarkedExpiringSoon'
  | 'ReminderSent'
  | 'RenewalStarted'
  | 'Renewed'
  | 'Expired'
  | 'Suspended'
  | 'Resumed'
  | 'Terminated'
  | 'Cancelled'
  | 'Reopened'
  | (string & {})

export type ContractRenewalStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'AwaitingClient'
  | 'AwaitingApproval'
  | 'Completed'
  | 'Lost'
  | 'Cancelled'
  | (string & {})

export interface ContractAssetCategoryInput {
  assetCategoryId?: string | null
  assetCategoryName?: string | null
}

export interface ContractWorkOrderTypeInput {
  workOrderTypeId?: string | null
  workOrderTypeName?: string | null
}

export interface ContractPmEntitlementInput {
  assetId?: string | null
  assetCategory?: string | null
  workOrderType: string
  visitsPerPeriod: number
  periodType: PeriodTypeValue
  includedVisits: number
  usedVisits: number
  effectiveFrom: string
  effectiveTo: string
  isActive: boolean
  notes?: string | null
}

export interface ContractSlaRowInput {
  priority: string
  responseTimeHours?: number | null
  resolutionTimeHours?: number | null
}

export interface UpsertContractInput {
  contractNumber: string
  clientId: string
  name: string
  contractType: ContractType
  status: ContractStatus
  startDate: string
  endDate: string
  durationMonths?: number | null
  billingModel: ContractBillingModel
  currency: string
  contractValue?: number | null
  renewalNoticeDays: number
  renewalBehavior?: ContractRenewalBehaviorValue
  description?: string | null
  includedServices?: string | null
  excludedServices?: string | null
  notes?: string | null
  renewalOwnerUserId?: string | null
  accountManagerUserId?: string | null
  internalApproverUserId?: string | null
  autoExpireEnabled: boolean
  gracePeriodDays: number
  allowPreferentialRatesDuringGracePeriod: boolean
  allowSlaDuringGracePeriod: boolean
  gracePeriodPolicy?: ContractGracePeriodPolicyValue
  responseTimeHours?: number | null
  resolutionTimeHours?: number | null
  workingHoursPolicy?: string | null
  pauseSlaOnHold: boolean
  escalationEmail?: string | null
  priorityLevel?: string | null
  gracePeriodAllowsSla: boolean
  slaMatrix: ContractSlaRowInput[]
  siteIds: string[]
  assetIds: string[]
  assetCategories: ContractAssetCategoryInput[]
  workOrderTypes: ContractWorkOrderTypeInput[]
  pmEntitlements: ContractPmEntitlementInput[]
}

export interface ContractListItem {
  id: string
  contractNumber: string
  clientId: string
  clientName?: string | null
  name: string
  contractType: ContractType
  status: ContractStatus
  startDate: string
  endDate: string
  daysToExpiry: number
  renewalRisk: RenewalRiskValue
  billingModel: ContractBillingModel
  ownerUserId?: string | null
  ownerName?: string | null
  contractValue?: number | null
}

export interface ContractSiteRecord {
  siteId: string
  siteCode?: string | null
  siteName?: string | null
}

export interface ContractAssetRecord {
  assetId: string
  assetCode?: string | null
  assetName?: string | null
}

export interface ContractAssetCategoryRecord {
  assetCategoryId?: string | null
  assetCategoryName?: string | null
}

export interface ContractWorkOrderTypeRecord {
  workOrderTypeId?: string | null
  workOrderTypeName?: string | null
}

export interface ContractPmEntitlementRecord {
  id: string
  assetId?: string | null
  assetName?: string | null
  assetCategory?: string | null
  workOrderType: string
  visitsPerPeriod: number
  periodType: PeriodTypeValue
  includedVisits: number
  usedVisits: number
  remainingVisits: number
  effectiveFrom: string
  effectiveTo: string
  isActive: boolean
  notes?: string | null
}

export interface ContractReminderRuleRecord {
  id: string
  contractId?: string | null
  daysBeforeExpiry: number
  notifyAccountManager: boolean
  notifyRenewalOwner: boolean
  notifySupervisor: boolean
  notifyFinance: boolean
  notifyTenantAdmin: boolean
  emailEnabled: boolean
  inAppEnabled: boolean
  escalationEnabled: boolean
  escalationAfterDays?: number | null
  isActive: boolean
}

export interface UpsertContractReminderRuleInput {
  daysBeforeExpiry: number
  notifyAccountManager: boolean
  notifyRenewalOwner: boolean
  notifySupervisor: boolean
  notifyFinance: boolean
  notifyTenantAdmin: boolean
  emailEnabled: boolean
  inAppEnabled: boolean
  escalationEnabled: boolean
  escalationAfterDays?: number | null
  isActive: boolean
}

export interface ContractRateRuleRecord {
  id: string
  rateCardId: string
  workOrderTypeId?: string | null
  workOrderTypeName?: string | null
  assetCategoryId?: string | null
  assetCategoryName?: string | null
  siteId?: string | null
  priority?: string | null
  billingModel: ContractBillingModel
  calloutFee?: number | null
  fixedVisitFee?: number | null
  labourHourlyRate?: number | null
  overtimeHourlyRate?: number | null
  weekendHourlyRate?: number | null
  publicHolidayHourlyRate?: number | null
  emergencyMultiplier?: number | null
  travelCharge?: number | null
  materialMarkupPercent?: number | null
  discountPercent?: number | null
  minimumBillableHours?: number | null
  includedLabourHours?: number | null
  vatApplicable: boolean
  notes?: string | null
  isActive: boolean
}

export interface ContractRateCardRecord {
  id: string
  contractId: string
  name: string
  description?: string | null
  currency: string
  isDefault: boolean
  effectiveFrom?: string | null
  effectiveTo?: string | null
  isActive: boolean
  rules: ContractRateRuleRecord[]
}

export interface UpsertContractRateCardInput {
  name: string
  description?: string | null
  currency: string
  isDefault: boolean
  effectiveFrom?: string | null
  effectiveTo?: string | null
  isActive: boolean
}

export interface UpsertContractRateRuleInput {
  workOrderTypeId?: string | null
  workOrderTypeName?: string | null
  assetCategoryId?: string | null
  assetCategoryName?: string | null
  siteId?: string | null
  priority?: string | null
  billingModel: ContractBillingModel
  calloutFee?: number | null
  fixedVisitFee?: number | null
  labourHourlyRate?: number | null
  overtimeHourlyRate?: number | null
  weekendHourlyRate?: number | null
  publicHolidayHourlyRate?: number | null
  emergencyMultiplier?: number | null
  travelCharge?: number | null
  materialMarkupPercent?: number | null
  discountPercent?: number | null
  minimumBillableHours?: number | null
  includedLabourHours?: number | null
  vatApplicable: boolean
  notes?: string | null
  isActive: boolean
}

export interface ContractLifecycleEventRecord {
  id: string
  eventType: ContractLifecycleEventType
  oldStatus?: ContractStatus | null
  newStatus?: ContractStatus | null
  note?: string | null
  reason?: string | null
  createdByUserId?: string | null
  createdByUserName?: string | null
  createdAt: string
}

export interface ContractRenewalSummaryRecord {
  id: string
  originalContractId: string
  renewedContractId?: string | null
  renewalStatus: ContractRenewalStatus
  ownerUserId?: string | null
  renewalStartedAt?: string | null
  renewalCompletedAt?: string | null
  notes?: string | null
}

export interface ContractWorkOrderSummaryRecord {
  id: string
  workOrderNumber: string
  title: string
  status: string
  coverageStatus?: string | null
  billingModel?: string | null
  estimatedCharge?: number | null
  createdAt: string
}

export interface ContractDetailRecord {
  id: string
  contractNumber: string
  clientId: string
  clientName?: string | null
  name: string
  contractType: ContractType
  status: ContractStatus
  startDate: string
  endDate: string
  durationMonths?: number | null
  billingModel: ContractBillingModel
  currency: string
  contractValue?: number | null
  renewalNoticeDays: number
  renewalBehavior: ContractRenewalBehaviorValue
  description?: string | null
  includedServices?: string | null
  excludedServices?: string | null
  notes?: string | null
  activatedAt?: string | null
  submittedForApprovalAt?: string | null
  approvedAt?: string | null
  expiryCheckedAt?: string | null
  renewalOwnerUserId?: string | null
  renewalOwnerName?: string | null
  accountManagerUserId?: string | null
  accountManagerName?: string | null
  internalApproverUserId?: string | null
  internalApproverName?: string | null
  renewalStartedAt?: string | null
  renewedFromContractId?: string | null
  renewedToContractId?: string | null
  terminationDate?: string | null
  terminationReason?: string | null
  suspensionReason?: string | null
  cancellationReason?: string | null
  lastReminderSentAt?: string | null
  nextReminderAt?: string | null
  autoExpireEnabled: boolean
  renewalRisk: RenewalRiskValue
  gracePeriodDays: number
  allowPreferentialRatesDuringGracePeriod: boolean
  allowSlaDuringGracePeriod: boolean
  gracePeriodPolicy: ContractGracePeriodPolicyValue
  responseTimeHours?: number | null
  resolutionTimeHours?: number | null
  workingHoursPolicy?: string | null
  pauseSlaOnHold: boolean
  escalationEmail?: string | null
  priorityLevel?: string | null
  gracePeriodAllowsSla: boolean
  slaMatrix: ContractSlaRowInput[]
  sites: ContractSiteRecord[]
  assets: ContractAssetRecord[]
  assetCategories: ContractAssetCategoryRecord[]
  workOrderTypes: ContractWorkOrderTypeRecord[]
  pmEntitlements: ContractPmEntitlementRecord[]
  reminderRules: ContractReminderRuleRecord[]
  rateCards: ContractRateCardRecord[]
  lifecycleEvents: ContractLifecycleEventRecord[]
  renewals: ContractRenewalSummaryRecord[]
  workOrders: ContractWorkOrderSummaryRecord[]
}

export interface ContractRenewInput {
  startDate?: string | null
  endDate?: string | null
  durationMonths?: number | null
  activateNewContract: boolean
  submitForApproval: boolean
  name?: string | null
  contractValue?: number | null
  notes?: string | null
}

export interface ContractRenewalResult {
  oldContractId: string
  newContractId: string
  oldContractNumber: string
  newContractNumber: string
  oldStatus: ContractStatus
  newStatus: ContractStatus
}

export interface WorkOrderPricingRecord {
  contractId?: string | null
  rateCardId?: string | null
  rateCardName?: string | null
  rateRuleId?: string | null
  billingModel: ContractBillingModel
  coverageStatus: CoverageStatusValue
  appliedRateSource: string
  calloutFee: number
  fixedVisitFee: number
  labourHourlyRate: number
  overtimeHourlyRate: number
  weekendHourlyRate: number
  publicHolidayHourlyRate: number
  emergencyMultiplier: number
  travelCharge: number
  materialMarkupPercent: number
  discountPercent: number
  minimumBillableHours: number
  includedLabourHours: number
  vatApplicable: boolean
  estimatedCharge: number
  warnings: string[]
}

export interface WorkOrderExecutionNotes {
  findings?: string | null
  workDone?: string | null
}

export interface WorkOrderProgressUpdateRecord {
  id: string
  message: string
  actorUserId?: string | null
  actorUserName?: string | null
  occurredAt: string
}

export interface WorkOrderPhotoRecord {
  id: string
  attachmentId: string
  fileName: string
  publicUrl?: string | null
  caption: string
  category: string
  includeInReport: boolean
  uploadedByUserId: string
  uploadedByName?: string | null
  uploadedAt: string
}

export interface WorkOrderMaterialUsageRecord {
  id: string
  materialItemId: string
  materialName?: string | null
  unitOfMeasure?: string | null
  assetId?: string | null
  assetName?: string | null
  quantityUsed: number
  unitCost?: number | null
  chargeable: boolean
  notes?: string | null
  usedByUserId: string
  usedByName?: string | null
  usedAt: string
}

export interface WorkOrderSignatureRecord {
  id: string
  signatureType: 'Technician' | 'Client' | string
  signerName: string
  signerRole?: string | null
  signatureDataUrl: string
  comment?: string | null
  capturedByUserId: string
  capturedAt: string
}

export interface WorkOrderServiceReportPreview {
  companyName: string
  workOrderNumber: string
  title: string
  clientName?: string | null
  siteLabel?: string | null
  assetName?: string | null
  assetDetails?: string | null
  technicianTeam?: string | null
  templateName?: string | null
  templateVersion?: number | null
  reportedProblem?: string | null
  findings?: string | null
  workDone?: string | null
  generatedAtLabel: string
  timestamps: Array<{ label: string; value?: string | null }>
  checklistSections: Array<{
    sectionName: string
    items: Array<{
      question: string
      inputType: string
      responseValue?: string | null
      unit?: string | null
      remarks?: string | null
      isRequired: boolean
      isCompleted: boolean
      evidenceRequired: boolean
      evidenceUrl?: string | null
    }>
  }>
  materials: Array<{
    name: string
    quantityUsed: number
    unitOfMeasure: string
    unitCost?: number | null
    chargeable: boolean
    notes?: string | null
  }>
  photoGroups: Array<{
    category: string
    photos: Array<{
      caption: string
      publicUrl?: string | null
    }>
  }>
  signatures: Array<{
    signatureType: string
    signerName: string
    signerRole?: string | null
    comment?: string | null
    capturedAtLabel: string
  }>
  showPoweredByEcosys: boolean
}

export interface WorkOrderExecutionBundle {
  notes: WorkOrderExecutionNotes
  photos: WorkOrderPhotoRecord[]
  materialUsages: WorkOrderMaterialUsageRecord[]
  signatures: WorkOrderSignatureRecord[]
  progressUpdates: WorkOrderProgressUpdateRecord[]
  reportPreview: WorkOrderServiceReportPreview
}

export interface CreateWorkOrderInput {
  clientId: string
  branchId?: string | null
  siteId?: string | null
  systemId?: string | null
  assetId?: string | null
  assignmentGroupId?: string | null
  assignmentType?: 'IndividualTechnician' | 'MultipleTechnicians' | 'AssignmentGroup' | 'Unassigned'
  assignedTechnicianId?: string | null
  assignedTechnicianIds?: string[]
  leadTechnicianId?: string | null
  assignmentNotes?: string | null
  title: string
  description?: string | null
  priority?: string | null
  dueDate?: string | null
  workType?: WorkOrderTypeValue | null
  isPreventiveMaintenance: boolean
  pmTemplateId?: string | null
  notifyClient?: boolean
  clientContactEmail?: string | null
}

export interface UpdateWorkOrderInput extends CreateWorkOrderInput {
  status?: string | null
}

export interface ClientRecord {
  id: string
  clientName: string
  clientType?: string | null
  email?: string | null
  phone?: string | null
  location?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  slaDefinitionId?: string | null
  slaDefinitionName?: string | null
  activeStartDate?: string | null
  activeDurationValue?: number | null
  activeDurationUnit?: 'days' | 'months' | 'years' | string | null
  activeEndDate?: string | null
  activeStatus?: 'active' | 'expiring' | 'expired' | 'inactive' | 'suspended' | string
  notes?: string | null
  isActive: boolean
  createdAt: string
  siteCount: number
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
}

export interface ClientSiteSummaryRecord {
  siteId: string
  siteCode: string
  siteName: string
  siteType: string
  location?: string | null
  contactPerson?: string | null
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
  status: string
  createdAt: string
  updatedAt?: string | null
}

export interface ClientSummaryRecord {
  client: ClientRecord
  siteCount: number
  systemCount: number
  assetCount: number
  openWorkOrderCount: number
  upcomingPmCount: number
  sites: ClientSiteSummaryRecord[]
}

export interface UpsertClientInput {
  clientName: string
  clientType?: string | null
  email?: string | null
  phone?: string | null
  location?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  slaDefinitionId?: string | null
  activeStartDate?: string | null
  activeDurationValue?: number | null
  activeDurationUnit?: 'days' | 'months' | 'years' | string | null
  notes?: string | null
}

export interface SlaDefinitionRecord {
  id: string
  planName: string
  description?: string | null
  isActive: boolean
  rules: SlaRuleRecord[]
  createdAt: string
  updatedAt?: string | null
}

export interface SlaRuleRecord {
  id: string
  priority: 'Critical' | 'High' | 'Medium' | 'Low' | string
  responseTargetHours: number
  resolutionTargetHours: number
  businessHoursOnly: boolean
}

export interface UpsertSlaDefinitionInput {
  planName: string
  description?: string | null
  isActive: boolean
  rules: Array<{
    priority: 'Critical' | 'High' | 'Medium' | 'Low' | string
    responseTargetHours: number
    resolutionTargetHours: number
    businessHoursOnly: boolean
  }>
}

export interface KipContext {
  screen: string
  entityType?: string | null
  entityId?: string | null
  entitySummary?: Record<string, unknown> | null
  tenantId: string
  userId: string
  userRole: string
  timestamp: string
}

export interface KipQueryInput {
  context: KipContext
  message: string
}

export interface KipQueryResponse {
  response: string
}

export interface AssetRecord {
  id: string
  branchId?: string | null
  branchName?: string | null
  clientId: string
  clientName?: string | null
  siteId?: string | null
  siteName?: string | null
  systemId?: string | null
  systemName?: string | null
  systemType?: string | null
  assetCategoryId?: string | null
  assetCategoryName?: string | null
  assetName: string
  assetCode: string
  assetType?: string | null
  ownershipType?: string | null
  location?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  installationDate?: string | null
  warrantyExpiryDate?: string | null
  recommendedPmFrequency?: string | null
  pmFrequencyValue?: number | null
  pmFrequencyUnit?: string | null
  autoSchedulePm: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  notes?: string | null
  status: string
  createdAt: string
  customFieldValues: AssetCustomFieldValueRecord[]
}

export interface UpsertAssetInput {
  clientId: string
  branchId?: string | null
  siteId?: string | null
  systemId?: string | null
  assetCategoryId?: string | null
  assetName: string
  assetCode?: string
  assetType?: string | null
  ownershipType?: string | null
  location?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  installationDate?: string | null
  warrantyExpiryDate?: string | null
  recommendedPmFrequency?: string | null
  pmFrequencyValue?: number | null
  pmFrequencyUnit?: string | null
  autoSchedulePm: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  notes?: string | null
  status?: string | null
  customFieldValues?: UpsertAssetCustomFieldValueInput[]
}

export interface AssetCustomFieldValueRecord {
  fieldDefinitionId: string
  fieldName: string
  fieldLabel: string
  fieldType: string
  unit?: string | null
  value: string
}

export interface UpsertAssetCustomFieldValueInput {
  fieldDefinitionId: string
  value?: string | null
}

export interface MaterialItem {
  id: string
  branchId?: string | null
  branchName?: string | null
  itemCode: string
  itemName: string
  category?: string | null
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  unitCost?: number | null
  isActive: boolean
  isLowStock: boolean
  createdAt: string
}

export interface UpsertMaterialInput {
  itemCode: string
  itemName: string
  category?: string | null
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  unitCost?: number | null
  branchId?: string | null
}

export interface StockMovement {
  id: string
  branchId?: string | null
  branchName?: string | null
  materialId: string
  workOrderId?: string | null
  materialRequestId?: string | null
  movementType: string
  quantity: number
  balanceAfter: number
  reason?: string | null
  referenceNumber?: string | null
  createdByUserId?: string | null
  createdAt: string
}

export interface PreventiveMaintenancePlan {
  id: string
  branchId?: string | null
  branchName?: string | null
  assetId: string
  assetName?: string | null
  clientId?: string | null
  clientName?: string | null
  pmTemplateId?: string | null
  pmTemplateName?: string | null
  frequency: string
  serviceIntervalMonths?: number | null
  autoSchedule: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  status: string
  createdAt: string
}

export interface UpsertPreventiveMaintenanceInput {
  assetId: string
  pmTemplateId?: string | null
  frequency?: string | null
  serviceIntervalMonths?: number | null
  autoSchedule: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  status?: string | null
}

export interface UserRecord {
  id: string
  fullName: string
  email: string
  phoneNumber?: string | null
  role: string
  jobTitle?: string | null
  department?: string | null
  isActive: boolean
  hasAllBranchAccess: boolean
  defaultBranchId?: string | null
  defaultBranchName?: string | null
  branchIds: string[]
  linkedTechnicianId?: string | null
  assignmentGroups: Array<{
    id: string
    name: string
    isLead: boolean
  }>
  createdAt: string
  updatedAt?: string | null
  permissions: ApiPermissions
  mustChangePassword: boolean
  lastCredentialSentAt?: string | null
  credentialDelivery?: {
    success: boolean
    status: string
    message?: string | null
  } | null
}

export interface UpsertUserInput {
  fullName: string
  email: string
  phoneNumber?: string | null
  password?: string | null
  credentialDeliveryMethod?: 'InviteEmail' | 'TemporaryPassword' | 'Both'
  role: string
  jobTitle?: string | null
  department?: string | null
  isActive?: boolean
  permissions?: ApiPermissions
  branchIds?: string[]
  defaultBranchId?: string | null
  hasAllBranchAccess: boolean
  assignmentGroupIds?: string[]
}

export interface BranchRecord {
  id: string
  tenantId: string
  parentBranchId?: string | null
  name: string
  code: string
  location?: string | null
  address?: string | null
  contactPerson?: string | null
  phone?: string | null
  email?: string | null
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface UpsertBranchInput {
  parentBranchId?: string | null
  name: string
  code: string
  location?: string | null
  address?: string | null
  contactPerson?: string | null
  phone?: string | null
  email?: string | null
  isActive: boolean
}

export interface TechnicianRecord {
  id: string
  branchId?: string | null
  branchName?: string | null
  fullName: string
  email: string
  phone?: string | null
  skillCategory?: string | null
  assignmentGroup?: string | null
  lastKnownLatitude?: number | null
  lastKnownLongitude?: number | null
  lastLocationAt?: string | null
  isTrackingActive: boolean
  activeWorkOrderId?: string | null
  isActive: boolean
  createdAt: string
}

export interface WorkOrderEventRecord {
  id: string
  eventType: string
  status?: string | null
  message: string
  actorUserId?: string | null
  actorUserName?: string | null
  oldStatus?: string | null
  newStatus?: string | null
  reason?: string | null
  note?: string | null
  latitude?: number | null
  longitude?: number | null
  occurredAt: string
}

export interface MaterialRequestLine {
  id: string
  materialItemId: string
  itemCode?: string | null
  itemName?: string | null
  quantityRequested: number
  quantityIssued: number
  quantityUsed: number
  quantityReturned: number
}

export interface MaterialRequestRecord {
  id: string
  requestNumber: string
  branchId?: string | null
  branchName?: string | null
  workOrderId: string
  workOrderNumber?: string | null
  requestedByUserId: string
  requestedByName?: string | null
  status: string
  createdAt: string
  lines: MaterialRequestLine[]
}

export interface TemplateChecklistItemRecord {
  id: string
  question: string
  type: 'text' | 'boolean' | 'number'
  required: boolean
  order: number
}

export interface TemplateRecord {
  id: string
  category: string
  name: string
  description?: string | null
  checklist: TemplateChecklistItemRecord[]
  isActive: boolean
}

export interface UpsertTemplateInput {
  name: string
  category: string
  description?: string | null
  checklist: Array<{
    question: string
    type: 'text' | 'boolean' | 'number'
    required: boolean
    order: number
  }>
  isActive: boolean
}

export interface ImportPreviewRow {
  rowNumber: number
  rawValues: Record<string, string | null>
  isValid: boolean
  errors: string[]
}

export interface ImportPreviewResponse {
  importType: string
  totalRows: number
  rows: ImportPreviewRow[]
}

export interface ImportCommitResponse {
  totalRows: number
  successfulRows: number
  failedRows: number
  skippedRows: number
  rowErrors?: Array<{
    rowNumber: number
    errors: string[]
  }>
}

export interface PreferentialRateRecord {
  id: string
  tenantId: string
  clientId: string
  clientName?: string | null
  rateName: string
  rateType: string
  itemType: 'labour' | 'material' | 'callout' | 'transport' | 'service' | 'other' | string
  unit: string
  defaultRate: number
  preferentialRate: number
  currency: string
  effectiveFrom: string
  effectiveTo?: string | null
  status: string
  requiresApproval: boolean
  createdBy?: string | null
  approvedBy?: string | null
  approvedAt?: string | null
  createdAt: string
}

export interface UpsertPreferentialRateInput {
  clientId: string
  rateName: string
  rateType: string
  itemType: 'labour' | 'material' | 'callout' | 'transport' | 'service' | 'other' | string
  unit: string
  defaultRate: number
  preferentialRate: number
  currency: string
  effectiveFrom: string
  effectiveTo?: string | null
  status: string
  requiresApproval: boolean
}

export interface FinanceApprovalRuleRecord {
  id: string
  tenantId: string
  approvalArea: 'preferential_rate' | 'invoice' | 'quotation' | 'expense' | 'material_issue' | 'billable_work' | string
  thresholdAmount: number
  currency: string
  requiredRole: string
  requiredApproverUserId?: string | null
  requiredApproverUserName?: string | null
  requiredAssignmentGroupId?: string | null
  requiredAssignmentGroupName?: string | null
  sequence: number
  isActive: boolean
  createdAt: string
}

export interface UpsertFinanceApprovalRuleInput {
  approvalArea: 'preferential_rate' | 'invoice' | 'quotation' | 'expense' | 'material_issue' | 'billable_work' | string
  thresholdAmount: number
  currency: string
  requiredRole: string
  requiredApproverUserId?: string | null
  requiredAssignmentGroupId?: string | null
  sequence: number
  isActive: boolean
}

export interface TenantBillableWorkRecord {
  workOrderId: string
  workOrderNumber?: string | null
  clientId?: string | null
  clientName?: string | null
  itemType: string
  description: string
  quantity: number
  unit: string
  defaultRate: number
  appliedRate: number
  currency: string
  totalAmount: number
  preferentialRateApplied: boolean
  source: string
}

export interface TenantFinanceOverview {
  preferentialRatesCount: number
  activePreferentialRatesCount: number
  approvalRulesCount: number
  pendingRateApprovalsCount: number
  pendingMaterialIssueApprovalsCount: number
  billableWorkItemsCount: number
  estimatedBillableAmount: number
  currency: string
}

export interface MaterialIssueLineRecord {
  id: string
  materialItemId: string
  materialItemName?: string | null
  itemCode?: string | null
  quantityRequested: number
  quantityIssued: number
  quantityReturned: number
  unit: string
  unitCost?: number | null
  notes?: string | null
}

export interface MaterialIssueRecord {
  id: string
  issueNumber: string
  branchId?: string | null
  branchName?: string | null
  workOrderId?: string | null
  workOrderNumber?: string | null
  assetId?: string | null
  assetName?: string | null
  clientId?: string | null
  clientName?: string | null
  siteId?: string | null
  siteName?: string | null
  systemId?: string | null
  systemName?: string | null
  issuedToUserId?: string | null
  issuedToUserName?: string | null
  requestedByUserId: string
  requestedByUserName?: string | null
  issuedByUserId?: string | null
  issuedByUserName?: string | null
  approvedByUserId?: string | null
  approvedByUserName?: string | null
  issueStatus: 'draft' | 'pending_approval' | 'approved' | 'issued' | 'rejected' | 'cancelled' | 'returned' | string
  notes?: string | null
  issuedAt?: string | null
  approvedAt?: string | null
  rejectedAt?: string | null
  rejectionReason?: string | null
  totalEstimatedCost: number
  totalIssuedCost: number
  createdAt: string
  lines: MaterialIssueLineRecord[]
}

export interface CreateMaterialIssueInput {
  branchId?: string | null
  workOrderId?: string | null
  assetId?: string | null
  clientId?: string | null
  siteId?: string | null
  systemId?: string | null
  issuedToUserId?: string | null
  notes?: string | null
  lines: Array<{
    materialItemId: string
    quantityRequested: number
    quantityIssued?: number | null
    notes?: string | null
  }>
}

export interface PlatformSummary {
  totalTenants: number
  activeTenants: number
  activeUsersNow: number
  loggedInToday: number
  tenantsWithActiveUsers: number
  /** Total registered users across all tenant workspaces (returned by backend if available) */
  totalUsers?: number
  /** Open platform-level issues flagged by the backend (returned if available) */
  openIssues?: number
}

export interface PlatformReportsSummary {
  totalTenants: number
  activeTenants: number
  deactivatedTenants?: number
  suspendedTenants?: number
  inactiveTenants?: number
  totalUsers: number
  activeUsers?: number
  monthlyRevenue?: number
  outstandingInvoices?: number
  totalExpenses?: number
  netProfit?: number
  openWorkOrders: number
  closedWorkOrders: number
  overdueWorkOrders: number
  totalInvoices?: number
  overdueInvoices?: number
  totalRevenue?: number
  outstandingBalance?: number
  monthlyRecurringRevenue?: number
  slaCompliancePercent?: number
  asAtMonthStartUtc: string
}

export interface PlatformTenant {
  tenantId: string
  name: string
  slug: string
  contactName?: string | null
  contactEmail?: string | null
  planName?: string | null
  licenseStatus: PlatformLicenseStatus
  userCount: number
  branchCount: number
  status: PlatformTenantStatus
  trialStartsAt?: string | null
  trialEndsAt?: string | null
  trialExtensionUsed: boolean
  trialExtendedAt?: string | null
  trialDaysRemaining?: number | null
  trialStatus: string
  createdAt: string
  /** Subscription (paid plan) start date */
  subscriptionStartsAt?: string | null
  /** Subscription (paid plan) end/renewal date */
  subscriptionEndsAt?: string | null
  /** Total work orders logged by this tenant */
  workOrderCount?: number | null
  /** Total assets registered by this tenant */
  assetCount?: number | null
  /** Timestamp of the last recorded user activity for this tenant */
  lastActivityAt?: string | null
  /** Enabled product module keys (e.g. ["work-orders","assets","pm"]) */
  modules?: string[] | null
}

export type PlatformTenantStatus = 'Active' | 'Suspended' | 'Trial' | 'Inactive'

export type PlatformLicenseStatus = 'Trial' | 'Active' | 'Expired' | 'Suspended'

export interface PlatformTenantDetail {
  tenantId: string
  name: string
  slug: string
  contactName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  country?: string | null
  industry?: string | null
  county?: string | null
  city?: string | null
  address?: string | null
  taxPin?: string | null
  status: PlatformTenantStatus
  planName?: string | null
  licenseStatus: PlatformLicenseStatus
  maxUsers?: number | null
  maxBranches?: number | null
  trialStartsAt?: string | null
  trialEndsAt?: string | null
  trialExtensionUsed: boolean
  trialExtendedAt?: string | null
  trialDaysRemaining?: number | null
  trialStatus: string
  subscriptionStartsAt?: string | null
  subscriptionEndsAt?: string | null
  createdAt: string
  updatedAt?: string | null
  createdByUserId?: string | null
  updatedByUserId?: string | null
  userCount: number
  branchCount: number
  workOrderCount: number
  activeUsersNow: number
  lastActivityAt?: string | null
  initialAdminInvitationSent?: boolean | null
  initialAdminInvitationMessage?: string | null
}

export interface PlatformTenantSummary {
  tenantId: string
  userCount: number
  branchCount: number
  workOrderCount: number
  activeUsersNow: number
  lastActivityAt?: string | null
}

export interface PlatformSession {
  userFullName: string
  email: string
  role: string
  jobTitle?: string | null
  loginAt: string
  lastSeenAt: string
  logoutAt?: string | null
  ipAddress?: string | null
  userAgent?: string | null
  isActive: boolean
}

export interface PlatformTenantActiveCount {
  tenantId: string
  activeUsersNow: number
  loggedInToday: number
  lastActivityAt?: string | null
}

export interface PlatformTenantAuditLog {
  id: string
  action: string
  actor: string
  details?: string | null
  createdAt: string
}

export interface UpsertPlatformTenantInput {
  name: string
  slug: string
  companyEmail?: string | null
  companyPhone?: string | null
  industry?: string | null
  contactName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  primaryContactName?: string | null
  primaryContactEmail?: string | null
  primaryContactPhone?: string | null
  country?: string | null
  county?: string | null
  city?: string | null
  address?: string | null
  taxPin?: string | null
  planName?: string | null
  maxUsers?: number | null
  maxBranches?: number | null
  trialEndsAt?: string | null
  subscriptionEndsAt?: string | null
  status: PlatformTenantStatus
  licenseStatus: PlatformLicenseStatus
  createDefaultAdmin?: boolean
  usePrimaryContactAsWorkspaceAdmin?: boolean
  adminFullName?: string | null
  adminEmail?: string | null
  adminPhone?: string | null
}

export interface UpdatePlatformTenantStatusInput {
  status: PlatformTenantStatus
  reason?: string | null
}

export interface LicensePlan {
  id: string
  planCode: string
  displayName: string
  maxUsers?: number | null
  maxBranches?: number | null
  maxAssets?: number | null
  monthlyWorkOrders?: number | null
  emailIngestion: boolean
  monitoringIntegration: boolean
  advancedReports: boolean
  clientPortal: boolean
  isActive: boolean
}

export interface TenantLicenseSnapshot {
  tenantId: string
  licensePlanId: string
  planCode: string
  planName: string
  status: string
  startsAt: string
  trialEndsAt?: string | null
  expiresAt?: string | null
  graceEndsAt?: string | null
  isReadOnly: boolean
  isSuspended: boolean
  isGracePeriod: boolean
  maxUsers?: number | null
  maxBranches?: number | null
  maxAssets?: number | null
  monthlyWorkOrders?: number | null
  emailIngestion: boolean
  monitoringIntegration: boolean
  advancedReports: boolean
  clientPortal: boolean
  warningMessage?: string | null
}

export interface LicenseUsageSnapshot {
  activeUsers: number
  activeBranches: number
  activeAssets: number
  monthlyWorkOrders: number
}

export interface PlatformLicenseUsageSnapshot {
  tenantId: string
  companyName: string
  planCode: string
  status: string
  activeUsers: number
  activeBranches: number
  activeAssets: number
  monthlyWorkOrders: number
  maxUsers?: number | null
  maxBranches?: number | null
  maxAssets?: number | null
  monthlyWorkOrdersLimit?: number | null
  isReadOnly: boolean
  isSuspended: boolean
  expiresAt?: string | null
  graceEndsAt?: string | null
}

export interface AssignmentGroupRecord {
  id: string
  name: string
  description?: string | null
  skillArea?: string | null
  branchId?: string | null
  branchName?: string | null
  isActive: boolean
  createdAt?: string
  updatedAt?: string | null
  members: Array<{
    id: string
    assignmentGroupId: string
    userId?: string | null
    technicianId: string
    memberName?: string | null
    email?: string | null
    isLead: boolean
    isActive: boolean
    addedAt: string
  }>
  technicianIds: string[]
}

export interface UpsertAssignmentGroupInput {
  name: string
  description?: string | null
  skillArea?: string | null
  branchId?: string | null
  isActive: boolean
  technicianIds: string[]
  members?: Array<{
    userId?: string
    technicianId?: string
    isLead: boolean
  }>
}

export interface TeamManagementNamedMembershipRecord {
  id: string
  name: string
  isLead: boolean
}

export interface TeamManagementCertificationAssignmentRecord {
  certificationId: string
  certificationName: string
  issuer?: string | null
  certificateNumber?: string | null
  issuedOn?: string | null
  expiresOn?: string | null
  notes?: string | null
  status: string
}

export interface TeamManagementProfileRecord {
  userId: string
  technicianId?: string | null
  fullName: string
  email: string
  role: string
  isUserActive: boolean
  isWorkforceActive: boolean
  branchId?: string | null
  branchName?: string | null
  availabilityStatus: string
  availabilityNote?: string | null
  availabilityUntil?: string | null
  availabilityUpdatedAt?: string | null
  assignmentGroups: TeamManagementNamedMembershipRecord[]
  certifications: TeamManagementCertificationAssignmentRecord[]
  activeWorkOrderCount: number
  pendingResponseCount: number
  onSiteCount: number
  isTrackingActive: boolean
  lastLocationAt?: string | null
  readinessStatus: string
  readinessReasons: string[]
}

export interface CertificationDefinitionRecord {
  id: string
  name: string
  issuer?: string | null
  description?: string | null
  renewalPeriodMonths?: number | null
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface TeamManagementOverviewRecord {
  profiles: TeamManagementProfileRecord[]
  certifications: CertificationDefinitionRecord[]
}

export interface UpsertCertificationDefinitionInput {
  name: string
  issuer?: string | null
  description?: string | null
  renewalPeriodMonths?: number | null
  isActive: boolean
}

export interface UpdateTeamManagementProfileInput {
  availabilityStatus?: string | null
  availabilityNote?: string | null
  availabilityUntil?: string | null
  certifications?: Array<{
    certificationId: string
    certificateNumber?: string | null
    issuedOn?: string | null
    expiresOn?: string | null
    notes?: string | null
  }>
}

export interface BrandingSettings {
  logoUrl?: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

export interface TenantSecuritySettings {
  id?: string
  tenantId?: string
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSpecialCharacter: boolean
  passwordRotationDays: number
  sessionTimeoutMinutes: number
  requireMfa: boolean
}

export interface TenantOperationsPolicySettings {
  id?: string
  tenantId?: string
  requireTechnicianSignature: boolean
  requireClientSignature: boolean
  allowRemoteCompletion: boolean
}

export interface NotificationSettings {
  id?: string
  tenantId?: string
  emailAlertsEnabled: boolean
  smsAlertsEnabled: boolean
  workOrderAssignmentAlerts: boolean
  licenseExpiryAlerts: boolean
  dailyDigestEnabled: boolean
}

export interface MonitoringSettings {
  id?: string
  tenantId?: string
  providerName: string
  endpointLabel?: string | null
  webhookSecret?: string | null
  defaultBranchId?: string | null
  defaultPriority: string
  autoCreateWorkOrders: boolean
  isEnabled: boolean
}

export interface EmailNotificationSettings {
  id: string
  tenantId: string
  isEnabled: boolean
  deliveryMode: 'Smtp' | 'Api' | 'Disabled'
  fromName: string
  fromEmail: string
  replyToEmail?: string | null
  smtpHost: string
  smtpPort: number
  enableSslTls: boolean
  secureMode: 'Auto' | 'None' | 'StartTls' | 'SslOnConnect'
  smtpUsername?: string | null
  apiEndpoint?: string | null
  apiKeyMasked?: string | null
  apiProviderName?: string | null
  timeoutSeconds: number
  maxRetries: number
  hasSecret: boolean
  isConfigured: boolean
  lastTestedAt?: string | null
  lastError?: string | null
}

export interface UpdateEmailNotificationSettingsInput {
  isEnabled: boolean
  deliveryMode: 'Smtp' | 'Api' | 'Disabled'
  fromName: string
  fromEmail: string
  replyToEmail?: string | null
  smtpHost: string
  smtpPort: number
  enableSslTls: boolean
  secureMode: 'Auto' | 'None' | 'StartTls' | 'SslOnConnect'
  smtpUsername?: string | null
  smtpPasswordSecret?: string | null
  apiEndpoint?: string | null
  apiKeySecret?: string | null
  apiProviderName?: string | null
  timeoutSeconds: number
  maxRetries: number
}

export interface EmailIntakeSettings {
  id: string
  tenantId: string
  isEnabled: boolean
  intakeEmailAddress?: string | null
  mailboxProvider: string
  host: string
  port: number
  useSsl: boolean
  username?: string | null
  hasPassword: boolean
  defaultClientId?: string | null
  defaultBranchId?: string | null
  defaultAssignmentGroupId?: string | null
  defaultPriority: string
  createWorkOrderFromUnknownSender: boolean
  subjectParsingRules?: string | null
  allowedSenderDomains?: string | null
  lastCheckedAt?: string | null
  isConnectionHealthy: boolean
  lastError?: string | null
}

export interface UpdateEmailIntakeSettingsInput {
  isEnabled: boolean
  intakeEmailAddress?: string | null
  mailboxProvider: string
  host: string
  port: number
  useSsl: boolean
  username?: string | null
  password?: string | null
  defaultClientId?: string | null
  defaultBranchId?: string | null
  defaultAssignmentGroupId?: string | null
  defaultPriority: string
  createWorkOrderFromUnknownSender: boolean
  subjectParsingRules?: string | null
  allowedSenderDomains?: string | null
}

export type IntakeProtocolSourceType = 'Email' | 'Monitoring'

export interface IntakeProtocolRecord {
  id: string
  tenantId: string
  name: string
  sourceType: IntakeProtocolSourceType
  isActive: boolean
  description?: string | null
  criteriaJson: string
  actionsJson: string
  sourceConfigJson: string
  createdAt: string
  updatedAt?: string | null
  lastTriggeredAt?: string | null
  lastTriggerStatus?: string | null
  lastError?: string | null
}

export interface UpsertIntakeProtocolInput {
  name: string
  sourceType: IntakeProtocolSourceType
  isActive: boolean
  description?: string | null
  criteriaJson: string
  actionsJson: string
  sourceConfigJson: string
}

export interface IntakeProtocolTestResponse {
  success: boolean
  lastTriggeredAt?: string | null
  lastTriggerStatus?: string | null
  lastError?: string | null
}

export interface MonitoringWebhookIntegrationRecord {
  id: string
  tenantId: string
  name: string
  toolType: string
  endpointSlug: string
  endpointUrl?: string | null
  isActive: boolean
  defaultClientId?: string | null
  defaultAssetId?: string | null
  defaultBranchId?: string | null
  defaultAssignmentGroupId?: string | null
  defaultPriority: string
  createWorkOrderOnAlert: boolean
  payloadMappingJson?: string | null
  lastReceivedAt?: string | null
  lastStatus?: string | null
  lastError?: string | null
  createdAt: string
  updatedAt?: string | null
  hasSecret: boolean
  generatedSecret?: string | null
}

export interface UpsertMonitoringWebhookIntegrationInput {
  name: string
  toolType: string
  isActive: boolean
  defaultClientId?: string | null
  defaultAssetId?: string | null
  defaultBranchId?: string | null
  defaultAssignmentGroupId?: string | null
  defaultPriority: string
  createWorkOrderOnAlert: boolean
  payloadMappingJson?: string | null
}

export interface NumberingRuleRecord {
  id: string
  documentType: string
  prefix: string
  suffix?: string | null
  nextNumber: number
  paddingLength: number
  resetPeriod: string
  includeYear?: boolean
  includeMonth?: boolean
  yearFormat?: string | null
  separator?: string | null
  isLocked?: boolean
  preview: string
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface UpdateNumberingRuleInput {
  prefix: string
  suffix?: string | null
  nextNumber: number
  paddingLength: number
  resetPeriod: string
  includeYear?: boolean
  includeMonth?: boolean
  yearFormat?: string | null
  separator?: string | null
  isActive: boolean
}

export interface EmailTemplateRecord {
  eventKey: string
  templateName: string
  subject: string
  htmlBody: string
  textBody: string
  enabled: boolean
  senderNameOverride?: string | null
  replyToOverride?: string | null
  availablePlaceholders: string[]
  requiredPlaceholders: string[]
  supportsTenantOverride: boolean
  isOverride: boolean
  source: string
  lastUpdatedBy?: string | null
  lastUpdatedAt?: string | null
}

export interface UpdateEmailTemplateInput {
  templateName: string
  subject: string
  htmlBody: string
  textBody?: string | null
  enabled: boolean
  senderNameOverride?: string | null
  replyToOverride?: string | null
}

export interface EmailTemplatePreviewResponse {
  eventKey: string
  templateName: string
  templateSubject: string
  finalSubject: string
  htmlBody: string
  textBody: string
}

export interface EmailTemplateTestResponse {
  success: boolean
  message?: string | null
}

export interface PmTemplateQuestionRecord {
  id: string
  sectionId?: string | null
  sectionName?: string | null
  sectionOrder?: number
  question: string
  helperText?: string | null
  type: 'checkbox' | 'text' | 'multilinetext' | 'boolean' | 'number' | 'reading' | 'date' | 'dropdown' | 'photo' | 'signature' | 'yesno' | 'passfail' | 'condition'
  unit?: string | null
  minValue?: number | null
  maxValue?: number | null
  defaultValue?: string | null
  required: boolean
  evidenceRequired?: boolean
  technicianRemarksAllowed?: boolean
  requiresNoteOnFail?: boolean
  order: number
  options?: string[]
}

export interface PmTemplateRecord {
  id: string
  category: string
  name: string
  description?: string | null
  code?: string | null
  workType?: 'PreventiveMaintenance' | 'BreakdownCorrective' | string
  defaultIntervalMonths?: number | null
  assetCategoryId?: string | null
  assetCategoryName?: string | null
  version?: number
  isPlatformSeed?: boolean
  sections?: Array<{ id: string; name: string; order: number }>
  isActive: boolean
  checklist: PmTemplateQuestionRecord[]
}

export interface UpsertPmTemplateInput {
  category: string
  name: string
  description?: string | null
  code?: string | null
  workType?: 'PreventiveMaintenance' | 'BreakdownCorrective' | string
  defaultIntervalMonths?: number | null
  assetCategoryId?: string | null
  isActive: boolean
  checklist: Array<{
    sectionName?: string | null
    sectionOrder?: number | null
    question: string
    helperText?: string | null
    type: 'checkbox' | 'text' | 'multilinetext' | 'boolean' | 'number' | 'reading' | 'date' | 'dropdown' | 'photo' | 'signature' | 'yesno' | 'passfail' | 'condition'
    unit?: string | null
    minValue?: number | null
    maxValue?: number | null
    defaultValue?: string | null
    required: boolean
    evidenceRequired?: boolean
    technicianRemarksAllowed?: boolean
    requiresNoteOnFail?: boolean
    order: number
    options?: string[]
  }>
}

export interface SiteRecord {
  id: string
  tenantId: string
  clientId: string
  siteCode: string
  siteName: string
  siteType: string
  status: string
  streetAddress?: string | null
  areaEstate?: string | null
  townCity?: string | null
  county?: string | null
  country?: string | null
  region?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  alternateContact?: string | null
  operatingHours?: string | null
  accessNotes?: string | null
  specialInstructions?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface UpsertSiteInput {
  siteName: string
  siteType?: string | null
  streetAddress?: string | null
  areaEstate?: string | null
  townCity?: string | null
  county?: string | null
  country?: string | null
  region?: string | null
  contactPerson?: string | null
  contactPhone?: string | null
  contactEmail?: string | null
  alternateContact?: string | null
  operatingHours?: string | null
  accessNotes?: string | null
  specialInstructions?: string | null
}

export interface SystemRecord {
  id: string
  tenantId: string
  clientId: string
  clientName?: string | null
  siteId: string
  siteName?: string | null
  systemName: string
  systemType: string
  description?: string | null
  status: string
  manufacturer?: string | null
  model?: string | null
  serialNumber?: string | null
  installationDate?: string | null
  notes?: string | null
  assetCount: number
  openWorkOrders: number
  createdAt: string
  updatedAt?: string | null
}

export interface SiteDetailRecord {
  site: SiteRecord
  clientName?: string | null
  systemCount: number
  assetCount: number
  openWorkOrders: number
  upcomingPmCount: number
  systems: Array<{
    systemId: string
    systemName: string
    systemType: string
    status: string
    manufacturer?: string | null
    model?: string | null
    serialNumber?: string | null
    installationDate?: string | null
    notes?: string | null
    assetCount: number
    openWorkOrderCount: number
    createdAt: string
    updatedAt?: string | null
  }>
  assets: Array<{
    assetId: string
    systemId?: string | null
    systemName?: string | null
    assetName: string
    assetCode: string
    assetType?: string | null
    status: string
    lastPmServiceDate?: string | null
    nextPmDueDate?: string | null
  }>
  workOrders: Array<{
    workOrderId: string
    workOrderNumber: string
    title: string
    status: string
    workType?: WorkOrderTypeValue | null
    priority: string
    systemId?: string | null
    systemName?: string | null
    assetId?: string | null
    assetName?: string | null
    createdAt: string
    dueDate?: string | null
  }>
}

export interface UpsertSystemInput {
  systemName: string
  systemType: string
  description?: string | null
  status?: string | null
  manufacturer?: string | null
  model?: string | null
  serialNumber?: string | null
  installationDate?: string | null
  notes?: string | null
}

export interface SystemDetail {
  system: SystemRecord
  assets: Array<{
    id: string
    assetName: string
    assetCode: string
    assetType?: string | null
    assetCategory?: string | null
    status: string
    lastPmServiceDate?: string | null
    nextPmDueDate?: string | null
    openWorkOrders: number
  }>
  workOrders: Array<{
    id: string
    workOrderNumber: string
    title: string
    status: string
    workType?: WorkOrderTypeValue | null
    priority: string
    assetId?: string | null
    assetName?: string | null
  }>
  upcomingPmCount: number
  relatedDocuments: Array<{
    id: string
    title: string
    fileName: string
    status: string
    uploadedAt: string
  }>
}

export interface AttachmentRecord {
  id: string
  entityType: string
  entityId: string
  fileName: string
  fileSize: number
  mimeType: string
  publicUrl: string
  uploadedByUserId: string
  createdAt: string
}

export interface WorkOrderChecklistItemRecord {
  id: string
  pmTemplateQuestionId?: string | null
  sectionName?: string | null
  sectionOrder?: number
  questionText: string
  helperText?: string | null
  inputType: 'checkbox' | 'text' | 'multilinetext' | 'number' | 'reading' | 'date' | 'dropdown' | 'photo' | 'signature' | 'yesno' | 'passfail' | 'boolean' | string
  isRequired: boolean
  sortOrder: number
  unit?: string | null
  minValue?: number | null
  maxValue?: number | null
  defaultValue?: string | null
  evidenceRequired?: boolean
  evidenceAttachmentId?: string | null
  technicianRemarksAllowed?: boolean
  requiresNoteOnFail?: boolean
  responseValue?: string | null
  remarks?: string | null
  isCompleted: boolean
  completedByUserId?: string | null
  completedAt?: string | null
  options: string[]
}

export interface KnowledgeCategoryRecord {
  id: string
  name: string
  description?: string | null
  displayOrder: number
  isActive: boolean
}

export interface UpsertKnowledgeCategoryInput {
  name: string
  description?: string | null
  displayOrder: number
}

export interface KnowledgeArticleListItem {
  id: string
  title: string
  slug: string
  summary?: string | null
  categoryId?: string | null
  categoryName?: string | null
  status: 'Draft' | 'Published' | 'Archived' | string
  visibility: 'Internal' | 'TechnicianOnly' | 'AdminOnly' | string
  publishedAt?: string | null
  updatedAt: string
  tags: string[]
}

export interface KnowledgeArticleVersionRecord {
  id: string
  versionNumber: number
  title: string
  body: string
  updatedByUserId?: string | null
  createdAt: string
}

export interface KnowledgeArticleDetail {
  id: string
  title: string
  slug: string
  summary?: string | null
  body: string
  categoryId?: string | null
  categoryName?: string | null
  status: 'Draft' | 'Published' | 'Archived' | string
  visibility: 'Internal' | 'TechnicianOnly' | 'AdminOnly' | string
  createdByUserId: string
  createdByName?: string | null
  updatedByUserId?: string | null
  updatedByName?: string | null
  createdAt: string
  updatedAt?: string | null
  publishedAt?: string | null
  sourceWorkOrderId?: string | null
  tags: string[]
  versions: KnowledgeArticleVersionRecord[]
  relatedArticles: KnowledgeArticleListItem[]
}

export interface UpsertKnowledgeArticleInput {
  title: string
  summary?: string | null
  body: string
  categoryId?: string | null
  status: 'Draft' | 'Published' | 'Archived' | string
  visibility: 'Internal' | 'TechnicianOnly' | 'AdminOnly' | string
  tags: string[]
}

export interface KnowledgeDocumentRecord {
  id: string
  tenantId: string
  title: string
  description?: string | null
  categoryId?: string | null
  categoryName?: string | null
  tags: string[]
  linkedClientId?: string | null
  linkedClientName?: string | null
  linkedSiteId?: string | null
  linkedSiteName?: string | null
  linkedSystemId?: string | null
  linkedSystemName?: string | null
  linkedAssetId?: string | null
  linkedAssetName?: string | null
  linkedWorkOrderId?: string | null
  linkedWorkOrderNumber?: string | null
  uploadedByUserId: string
  uploadedAt: string
  version: number
  status: 'draft' | 'published' | 'archived' | string
  fileName: string
  fileType: string
  fileSize: number
  downloadPath: string
  chunkCount: number
}

export interface AskKnowledgeInput {
  question: string
  contextType?: 'knowledge' | 'client' | 'site' | 'system' | 'asset' | 'work_order' | string
  contextId?: string | null
  includeDocuments: boolean
}

export interface AskKnowledgeSource {
  sourceType: 'article' | 'document' | 'document_chunk' | 'work_order' | 'asset' | 'client' | 'site' | 'system' | string
  sourceId: string
  title: string
  excerpt: string
  pageNumber?: number | null
  path: string
  status: string
}

export interface AskKnowledgeResponse {
  answer: string
  sources: AskKnowledgeSource[]
}

// ── Finance Phase 1: Rate Cards & Work Order Billing ─────────────────────────

export interface RateCardRecord {
  id: string
  tenantId: string
  name: string
  category: 'Labour' | 'Callout' | 'Service' | 'Transport' | 'Material' | 'Other' | string
  description?: string | null
  unitRate: number
  unit: 'hour' | 'visit' | 'km' | 'unit' | 'day' | 'job' | string
  skillLevel?: string | null
  timeType: 'Standard' | 'Overtime' | 'Emergency' | 'Weekend' | 'Holiday' | string
  isDefault: boolean
  taxRate?: number | null
  isActive: boolean
  createdAt: string
}

export interface UpsertRateCardInput {
  name: string
  category: string
  description?: string | null
  unitRate: number
  unit: string
  skillLevel?: string | null
  timeType: string
  isDefault: boolean
  taxRate?: number | null
}

export interface WorkOrderBillableItemRecord {
  id: string
  workOrderId: string
  itemType: 'Labour' | 'Material' | 'Callout' | 'Service' | 'Transport' | 'Other' | string
  description: string
  quantity: number
  unit: string
  unitRate: number
  rateCardId?: string | null
  lineTotal: number
  taxRate: number
  taxAmount: number
  grandTotal: number
  isManualEntry: boolean
  isBilled: boolean
  notes?: string | null
  createdAt: string
}

export interface UpsertBillableItemInput {
  itemType: string
  description: string
  quantity: number
  unit: string
  unitRate: number
  rateCardId?: string | null
  taxRate: number
  notes?: string | null
}

export interface WorkOrderTimeLogRecord {
  id: string
  workOrderId: string
  technicianId?: string | null
  technicianName?: string | null
  startedAt: string
  endedAt?: string | null
  durationMinutes: number
  notes?: string | null
  isBilled: boolean
  createdAt: string
}

export interface UpsertTimeLogInput {
  technicianId?: string | null
  technicianName?: string | null
  startedAt: string
  endedAt?: string | null
  durationMinutes: number
  notes?: string | null
}

export interface UpdateBillingStatusInput {
  billingStatus: 'Unbilled' | 'PartiallyBilled' | 'Billed' | 'NotBillable' | string
  isBillable: boolean
}

// ── Phase 1 missing types ────────────────────────────────────────────────────

// RateCardRecord defined above

// UpsertRateCardInput defined above

// WorkOrderBillableItemRecord defined above

// UpsertBillableItemInput defined above

// WorkOrderTimeLogRecord defined above

// UpsertTimeLogInput defined above

// UpdateBillingStatusInput defined above

// ── Phase 2 — Invoices ───────────────────────────────────────────────────────

export interface TenantInvoiceRecord {
  id: string
  tenantId: string
  clientId: string
  clientName?: string | null
  invoiceNumber: string
  status: string
  invoiceDate: string
  dueDate: string
  subtotal: number
  taxAmount: number
  totalAmount: number
  amountPaid: number
  balance: number
  currency: string
  createdAt: string
}

export interface TenantInvoiceLineRecord {
  id: string
  invoiceId: string
  billableItemId?: string | null
  workOrderId?: string | null
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxRate: number
  taxAmount: number
  grandTotal: number
}

export interface TenantInvoiceDetail extends TenantInvoiceRecord {
  notes?: string | null
  termsAndConditions?: string | null
  creditTermId?: string | null
  creditTermName?: string | null
  depositAmount?: number | null
  depositPaid: boolean
  depositDueDate?: string | null
  creditLimitWarning: boolean
  clientCurrentBalance: number
  clientCreditLimit?: number | null
  lines: TenantInvoiceLineRecord[]
  payments: TenantPaymentRecord[]
}

export interface CreateInvoiceInput {
  clientId: string
  invoiceDate: string
  dueDate: string
  creditTermId?: string | null
  notes?: string | null
  termsAndConditions?: string | null
  currency?: string | null
  lines: CreateInvoiceLineInput[]
}

export interface CreateInvoiceLineInput {
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  taxRate: number
  billableItemId?: string | null
  workOrderId?: string | null
}

// ── Phase 3 — Payments ───────────────────────────────────────────────────────

export interface TenantPaymentRecord {
  id: string
  tenantId: string
  invoiceId: string
  clientId: string
  paymentNumber: string
  amount: number
  paymentDate: string
  method: string
  reference?: string | null
  notes?: string | null
  status: string
  createdAt: string
}

export interface RecordPaymentInput {
  amount: number
  paymentDate: string
  method: string
  reference?: string | null
  notes?: string | null
}

// ── Phase 4 — Quotations ─────────────────────────────────────────────────────

export interface TenantQuotationRecord {
  id: string
  tenantId: string
  clientId: string
  clientName?: string | null
  quoteNumber: string
  status: string
  subject?: string | null
  quoteDate: string
  validUntil: string
  subtotal: number
  taxAmount: number
  totalAmount: number
  currency: string
  convertedToInvoiceId?: string | null
  convertedToWorkOrderId?: string | null
  createdAt: string
}

export interface TenantQuotationLineRecord {
  id: string
  quotationId: string
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxRate: number
  grandTotal: number
  rateCardId?: string | null
}

export interface TenantQuotationDetail extends TenantQuotationRecord {
  siteId?: string | null
  notes?: string | null
  rejectionReason?: string | null
  lines: TenantQuotationLineRecord[]
}

export interface UpsertQuotationLineInput {
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  taxRate: number
  rateCardId?: string | null
}

export interface UpsertQuotationInput {
  clientId: string
  siteId?: string | null
  quoteDate: string
  validUntil: string
  subject?: string | null
  notes?: string | null
  currency?: string | null
  lines: UpsertQuotationLineInput[]
}

// ── Phase 5 — Credit Terms ───────────────────────────────────────────────────

export interface CreditTermRecord {
  id: string
  tenantId: string
  name: string
  code: string
  netDays: number
  isDepositRequired: boolean
  depositPercent: number
  creditLimit?: number | null
  description?: string | null
  isDefault: boolean
  isActive: boolean
  createdAt: string
}

export interface ClientCreditSummaryRecord {
  currentBalance: number
  creditLimit?: number | null
  availableCredit?: number | null
  overdueAmount: number
  oldestInvoiceDate?: string | null
  termName?: string | null
}

// ── Finance Report Types ──────────────────────────────────────────────────────

export interface FinanceDashboard {
  totalInvoicedYtd: number
  totalCollectedYtd: number
  outstandingBalance: number
  overdueBalance: number
  quoteConversionRate: number
  totalQuotations: number
  convertedQuotations: number
  billableWorkItemsCount: number
  estimatedBillableAmount: number
  currency: string
}

export interface RevenueReportRow {
  group: string
  invoiced: number
  collected: number
  outstanding: number
  count: number
}

export interface AgingRow {
  clientId: string
  clientName: string
  current: number
  days1To30: number
  days31To60: number
  days61To90: number
  days90Plus: number
  total: number
}

export interface ClientStatementEntry {
  date: string
  reference: string
  entryType: 'invoice' | 'payment'
  debit: number
  credit: number
}

export interface ClientStatementResponse {
  clientName: string
  from: string | null
  to: string | null
  totalInvoiced: number
  totalPaid: number
  balance: number
  entries: ClientStatementEntry[]
}

// ── Notification Preference Types ─────────────────────────────────────────────

export interface UserNotificationPreference {
  notificationKey: string
  category: string
  label: string
  description: string
  emailEnabled: boolean
  inAppEnabled: boolean
}

export interface UpdateNotificationPreferenceInput {
  notificationKey: string
  emailEnabled: boolean
  inAppEnabled: boolean
}

// ── Scheduled Report Types ────────────────────────────────────────────────────

export interface ScheduledReport {
  id: string
  name: string
  reportType: string
  frequency: string
  dayOfMonth: number | null
  dayOfWeek: string | null
  timeOfDay: string
  dateRangePreset: string
  groupBy: string | null
  clientId: string | null
  recipients: string
  isActive: boolean
  lastRunAt: string | null
  nextRunAt: string | null
  lastRunStatus: string | null
  createdAt: string
  updatedAt: string | null
}

export interface CreateScheduledReportInput {
  name: string
  reportType: string
  frequency: string
  dayOfMonth?: number | null
  dayOfWeek?: string | null
  timeOfDay: string
  dateRangePreset: string
  groupBy?: string | null
  clientId?: string | null
  recipients: string
}
