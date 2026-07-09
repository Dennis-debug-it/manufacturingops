export interface ContractListItem {
  id: string
  contractNumber: string
  clientId: string
  clientName: string | null
  name: string
  contractType: string
  status: string
  startDate: string
  endDate: string
  daysToExpiry: number
  renewalRisk: string
  billingModel: string
  ownerUserId: string | null
  ownerName: string | null
  contractValue: number | null
}

export interface ContractSlaRow {
  priority: string
  responseTimeHours: number | null
  resolutionTimeHours: number | null
}

export interface ContractSiteRef {
  siteId: string
  siteCode: string | null
  siteName: string | null
}

export interface ContractAssetRef {
  assetId: string
  assetCode: string | null
  assetName: string | null
}

export interface ContractAssetCategoryRef {
  assetCategoryId: string | null
  assetCategoryName: string | null
}

export interface ContractWorkOrderTypeRef {
  workOrderTypeId: string | null
  workOrderTypeName: string | null
}

export interface ContractPmEntitlement {
  id: string
  assetId: string | null
  assetName: string | null
  assetCategory: string | null
  workOrderType: string
  visitsPerPeriod: number
  periodType: string
  includedVisits: number
  usedVisits: number
  remainingVisits: number
  effectiveFrom: string
  effectiveTo: string
  isActive: boolean
  notes: string | null
}

export interface ContractReminderRule {
  id: string
  contractId: string | null
  daysBeforeExpiry: number
  notifyAccountManager: boolean
  notifyRenewalOwner: boolean
  notifySupervisor: boolean
  notifyFinance: boolean
  notifyTenantAdmin: boolean
  emailEnabled: boolean
  inAppEnabled: boolean
  escalationEnabled: boolean
  escalationAfterDays: number | null
  isActive: boolean
}

export interface ContractRateRule {
  id: string
  rateCardId: string
  workOrderTypeId: string | null
  workOrderTypeName: string | null
  assetCategoryId: string | null
  assetCategoryName: string | null
  siteId: string | null
  priority: string | null
  billingModel: string
  calloutFee: number | null
  fixedVisitFee: number | null
  labourHourlyRate: number | null
  overtimeHourlyRate: number | null
  weekendHourlyRate: number | null
  publicHolidayHourlyRate: number | null
  emergencyMultiplier: number | null
  travelCharge: number | null
  materialMarkupPercent: number | null
  discountPercent: number | null
  minimumBillableHours: number | null
  includedLabourHours: number | null
  vatApplicable: boolean
  notes: string | null
  isActive: boolean
}

export interface ContractRateCard {
  id: string
  contractId: string
  name: string
  description: string | null
  currency: string
  isDefault: boolean
  effectiveFrom: string | null
  effectiveTo: string | null
  isActive: boolean
  rules: ContractRateRule[]
}

export interface ContractLifecycleEvent {
  id: string
  eventType: string
  oldStatus: string | null
  newStatus: string | null
  note: string | null
  reason: string | null
  createdByUserId: string | null
  createdByUserName: string | null
  createdAt: string
}

export interface ContractWorkOrderRef {
  id: string
  workOrderNumber: string
  title: string
  status: string
  coverageStatus: string | null
  billingModel: string | null
  estimatedCharge: number | null
  createdAt: string
}

export interface ContractDetail {
  id: string
  contractNumber: string
  clientId: string
  clientName: string | null
  name: string
  contractType: string
  status: string
  startDate: string
  endDate: string
  durationMonths: number | null
  billingModel: string
  currency: string
  contractValue: number | null
  renewalNoticeDays: number
  renewalBehavior: string | null
  description: string | null
  includedServices: string | null
  excludedServices: string | null
  notes: string | null
  activatedAt: string | null
  submittedForApprovalAt: string | null
  approvedAt: string | null
  renewalOwnerUserId: string | null
  renewalOwnerName: string | null
  accountManagerUserId: string | null
  accountManagerName: string | null
  internalApproverUserId: string | null
  internalApproverName: string | null
  renewalStartedAt: string | null
  renewedFromContractId: string | null
  renewedToContractId: string | null
  terminationDate: string | null
  terminationReason: string | null
  suspensionReason: string | null
  cancellationReason: string | null
  autoExpireEnabled: boolean
  renewalRisk: string
  gracePeriodDays: number
  allowPreferentialRatesDuringGracePeriod: boolean
  allowSlaDuringGracePeriod: boolean
  gracePeriodPolicy: string | null
  responseTimeHours: number | null
  resolutionTimeHours: number | null
  workingHoursPolicy: string | null
  pauseSlaOnHold: boolean
  escalationEmail: string | null
  priorityLevel: string | null
  slaMatrix: ContractSlaRow[]
  sites: ContractSiteRef[]
  assets: ContractAssetRef[]
  assetCategories: ContractAssetCategoryRef[]
  workOrderTypes: ContractWorkOrderTypeRef[]
  pmEntitlements: ContractPmEntitlement[]
  reminderRules: ContractReminderRule[]
  rateCards: ContractRateCard[]
  lifecycleEvents: ContractLifecycleEvent[]
  workOrders: ContractWorkOrderRef[]
}
