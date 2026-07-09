export interface DashboardGroupMetric {
  groupName: string
  count: number
}

export interface DashboardTechnicianWorkload {
  technicianName: string
  activeWorkOrders: number
  pendingResponses: number
  onSiteCount: number
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
  workOrdersByGroup: DashboardGroupMetric[]
  technicianWorkload: DashboardTechnicianWorkload[]
}
