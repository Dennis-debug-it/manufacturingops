import type { ApiBranch, ApiPermissions, DashboardSummary } from '../types/api'

const emptyPermissions: ApiPermissions = {
  canViewWorkOrders: false,
  canCreateWorkOrders: false,
  canAssignWorkOrders: false,
  canCompleteWorkOrders: false,
  canViewKnowledge: false,
  canManageKnowledge: false,
  canViewMaterials: false,
  canRequestMaterials: false,
  canManageMaterials: false,
  canApproveMaterials: false,
  canIssueMaterials: false,
  canManageAssets: false,
  canManageSettings: false,
  canViewReports: false,
  canViewFinance: false,
  canManageFinance: false,
  canViewFinanceRates: false,
  canManageFinanceRates: false,
  canViewFinanceApprovals: false,
  canManageFinanceApprovals: false,
  canViewBillableWork: false,
  canManageBillableWork: false,
  canOverrideMaterialStock: false,
  canViewPlatformTenants: false,
  canCreatePlatformTenants: false,
  canEditPlatformTenants: false,
  canUpdatePlatformTenantStatus: false,
  canDeactivatePlatformTenants: false,
}

export const emptyDashboardSummary: DashboardSummary = {
  openWorkOrders: 0,
  closedWorkOrders: 0,
  overdueWorkOrders: 0,
  assets: 0,
  clients: 0,
  materialsLowStock: 0,
  unassignedWorkOrders: 0,
  assignedToGroup: 0,
  assignedToTechnicians: 0,
  awaitingAcceptance: 0,
  techniciansOnSite: 0,
  contractsExpiringIn90Days: 0,
  contractsExpiringIn30Days: 0,
  criticalRenewalRiskContracts: 0,
  expiredContracts: 0,
  renewalInProgressContracts: 0,
  workOrdersByGroup: [],
  technicianWorkload: [],
}

export function asArray<T>(value: unknown): T[] {
  return Array.isArray(value) ? (value as T[]) : []
}

export function asRecord(value: unknown): Record<string, unknown> | null {
  if (!value || typeof value !== 'object' || Array.isArray(value)) {
    return null
  }

  return value as Record<string, unknown>
}

export function asString(value: unknown, fallback = ''): string {
  if (typeof value === 'string') {
    return value
  }

  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value)
  }

  return fallback
}

export function asNullableString(value: unknown): string | null {
  const next = asString(value)
  return next || null
}

export function asBoolean(value: unknown, fallback = false): boolean {
  if (typeof value === 'boolean') {
    return value
  }

  return fallback
}

export function asNumber(value: unknown, fallback = 0): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value
  }

  return fallback
}

export function pickRecord(...values: unknown[]): Record<string, unknown> | null {
  for (const value of values) {
    const record = asRecord(value)
    if (record) {
      return record
    }
  }

  return null
}

export function normalizePermissions(value: unknown): ApiPermissions {
  const record = asRecord(value)

  return {
    canViewWorkOrders: asBoolean(record?.canViewWorkOrders, emptyPermissions.canViewWorkOrders),
    canCreateWorkOrders: asBoolean(record?.canCreateWorkOrders, emptyPermissions.canCreateWorkOrders),
    canAssignWorkOrders: asBoolean(record?.canAssignWorkOrders, emptyPermissions.canAssignWorkOrders),
    canCompleteWorkOrders: asBoolean(record?.canCompleteWorkOrders, emptyPermissions.canCompleteWorkOrders),
    canViewKnowledge: asBoolean(record?.canViewKnowledge, emptyPermissions.canViewKnowledge),
    canManageKnowledge: asBoolean(record?.canManageKnowledge, emptyPermissions.canManageKnowledge),
    canViewMaterials: asBoolean(record?.canViewMaterials, emptyPermissions.canViewMaterials),
    canRequestMaterials: asBoolean(record?.canRequestMaterials, emptyPermissions.canRequestMaterials),
    canManageMaterials: asBoolean(record?.canManageMaterials, emptyPermissions.canManageMaterials),
    canApproveMaterials: asBoolean(record?.canApproveMaterials, emptyPermissions.canApproveMaterials),
    canIssueMaterials: asBoolean(record?.canIssueMaterials, emptyPermissions.canIssueMaterials),
    canManageAssets: asBoolean(record?.canManageAssets, emptyPermissions.canManageAssets),
    canManageSettings: asBoolean(record?.canManageSettings, emptyPermissions.canManageSettings),
    canViewReports: asBoolean(record?.canViewReports, emptyPermissions.canViewReports),
    canViewFinance: asBoolean(record?.canViewFinance, emptyPermissions.canViewFinance),
    canManageFinance: asBoolean(record?.canManageFinance, emptyPermissions.canManageFinance),
    canViewFinanceRates: asBoolean(record?.canViewFinanceRates, emptyPermissions.canViewFinanceRates),
    canManageFinanceRates: asBoolean(record?.canManageFinanceRates, emptyPermissions.canManageFinanceRates),
    canViewFinanceApprovals: asBoolean(record?.canViewFinanceApprovals, emptyPermissions.canViewFinanceApprovals),
    canManageFinanceApprovals: asBoolean(record?.canManageFinanceApprovals, emptyPermissions.canManageFinanceApprovals),
    canViewBillableWork: asBoolean(record?.canViewBillableWork, emptyPermissions.canViewBillableWork),
    canManageBillableWork: asBoolean(record?.canManageBillableWork, emptyPermissions.canManageBillableWork),
    canOverrideMaterialStock: asBoolean(record?.canOverrideMaterialStock, emptyPermissions.canOverrideMaterialStock),
    canViewPlatformTenants: asBoolean(record?.canViewPlatformTenants, emptyPermissions.canViewPlatformTenants),
    canCreatePlatformTenants: asBoolean(record?.canCreatePlatformTenants, emptyPermissions.canCreatePlatformTenants),
    canEditPlatformTenants: asBoolean(record?.canEditPlatformTenants, emptyPermissions.canEditPlatformTenants),
    canUpdatePlatformTenantStatus: asBoolean(record?.canUpdatePlatformTenantStatus, emptyPermissions.canUpdatePlatformTenantStatus),
    canDeactivatePlatformTenants: asBoolean(record?.canDeactivatePlatformTenants, emptyPermissions.canDeactivatePlatformTenants),
  }
}

export function normalizeBranches(value: unknown): ApiBranch[] {
  return asArray<Record<string, unknown>>(value).map((branch) => ({
    id: asString(branch.id),
    name: asString(branch.name),
    code: asString(branch.code),
    location: asNullableString(branch.location),
    isActive: asBoolean(branch.isActive, true),
  }))
}

export function normalizeDashboardSummary(value: unknown): DashboardSummary {
  const record = asRecord(value)

  return {
    openWorkOrders: asNumber(record?.openWorkOrders),
    closedWorkOrders: asNumber(record?.closedWorkOrders),
    overdueWorkOrders: asNumber(record?.overdueWorkOrders),
    assets: asNumber(record?.assets),
    clients: asNumber(record?.clients),
    materialsLowStock: asNumber(record?.materialsLowStock),
    unassignedWorkOrders: asNumber(record?.unassignedWorkOrders),
    assignedToGroup: asNumber(record?.assignedToGroup),
    assignedToTechnicians: asNumber(record?.assignedToTechnicians),
    awaitingAcceptance: asNumber(record?.awaitingAcceptance),
    techniciansOnSite: asNumber(record?.techniciansOnSite),
    contractsExpiringIn90Days: asNumber(record?.contractsExpiringIn90Days),
    contractsExpiringIn30Days: asNumber(record?.contractsExpiringIn30Days),
    criticalRenewalRiskContracts: asNumber(record?.criticalRenewalRiskContracts),
    expiredContracts: asNumber(record?.expiredContracts),
    renewalInProgressContracts: asNumber(record?.renewalInProgressContracts),
    workOrdersByGroup: asArray<Record<string, unknown>>(record?.workOrdersByGroup).map((item) => ({
      groupName: asString(item.groupName),
      count: asNumber(item.count),
    })),
    technicianWorkload: asArray<Record<string, unknown>>(record?.technicianWorkload).map((item) => ({
      technicianName: asString(item.technicianName),
      activeWorkOrders: asNumber(item.activeWorkOrders),
      pendingResponses: asNumber(item.pendingResponses),
      onSiteCount: asNumber(item.onSiteCount),
    })),
  }
}
