import type {
  AssetStatus,
  BadgeTone,
  InventoryAlert,
  Priority,
  RequisitionStatus,
  SlaStatus,
  SubscriptionStatus,
  TechnicianStatus,
  TenantStatus,
  WorkOrderStatus,
} from '../types/app'

export function priorityTone(priority: Priority): BadgeTone {
  if (priority === 'Critical') return 'danger'
  if (priority === 'High') return 'warning'
  if (priority === 'Medium') return 'info'
  return 'neutral'
}

export function statusTone(status: WorkOrderStatus): BadgeTone {
  if (status === 'Completed') return 'success'
  if (status === 'Acknowledged' || status === 'Closed') return 'success'
  if (status === 'Cancelled') return 'danger'
  if (status === 'In Progress') return 'info'
  if (status === 'On Hold') return 'warning'
  if (status === 'Assigned') return 'neutral'
  return 'default'
}

export function slaTone(status: SlaStatus): BadgeTone {
  if (status === 'Breached') return 'danger'
  if (status === 'At Risk') return 'warning'
  return 'success'
}

export function requisitionTone(status: RequisitionStatus): BadgeTone {
  if (status === 'Issued') return 'success'
  if (status === 'Approved') return 'info'
  if (status === 'Cancelled') return 'danger'
  return 'warning'
}

export function technicianTone(status: TechnicianStatus): BadgeTone {
  if (status === 'Overloaded') return 'danger'
  if (status === 'On Site' || status === 'Online') return 'success'
  if (status === 'In Transit') return 'info'
  if (status === 'Idle') return 'neutral'
  return 'default'
}

export function assetTone(status: AssetStatus): BadgeTone {
  if (status === 'Operational') return 'success'
  if (status === 'Needs Attention') return 'warning'
  if (status === 'Under Maintenance') return 'info'
  return 'danger'
}

export function inventoryAlertTone(alert: InventoryAlert): BadgeTone {
  if (alert === 'Critical') return 'danger'
  if (alert === 'Low') return 'warning'
  return 'success'
}

export function tenantTone(status: TenantStatus | SubscriptionStatus): BadgeTone {
  if (status === 'Active') return 'success'
  if (status === 'Trial') return 'info'
  if (status === 'Past Due') return 'warning'
  return 'danger'
}
