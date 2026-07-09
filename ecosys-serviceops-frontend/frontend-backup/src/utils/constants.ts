import type {
  AssetStatus,
  KnownRole,
  MaterialRequestUrgency,
  Priority,
  Role,
  SubscriptionStatus,
  TechnicianAvailability,
  TechnicianStatus,
  TenantStatus,
  WorkOrderStatus,
  WorkOrderType,
} from '../types/app'
import { isPlatformRole as isPlatformRoleValue, roleHomePath as roleHomePathValue } from './roles'

export const WORK_ORDER_TYPES: WorkOrderType[] = [
  'Preventive Maintenance',
  'Reactive Maintenance',
  'New Projects',
  'Emergency Escalations',
]

export const WORK_ORDER_STATUSES: WorkOrderStatus[] = [
  'Open',
  'Assigned',
  'In Progress',
  'Acknowledged',
  'Closed',
  'On Hold',
  'Completed',
  'Cancelled',
]

export const PRIORITIES: Priority[] = ['Critical', 'High', 'Medium', 'Low']

export const MATERIAL_URGENCIES: MaterialRequestUrgency[] = ['Routine', 'Priority', 'Critical']

export const TECHNICIAN_STATUSES: TechnicianStatus[] = [
  'Online',
  'Offline',
  'In Transit',
  'On Site',
  'Idle',
  'Overloaded',
]

export const TECHNICIAN_AVAILABILITY: TechnicianAvailability[] = [
  'Available',
  'Assigned',
  'Leave',
  'Offline',
]

export const ASSET_STATUSES: AssetStatus[] = [
  'Operational',
  'Needs Attention',
  'Under Maintenance',
  'Offline',
]

export const TENANT_STATUSES: TenantStatus[] = ['Active', 'Suspended']

export const SUBSCRIPTION_STATUSES: SubscriptionStatus[] = ['Trial', 'Active', 'Past Due', 'Suspended']

export const PLATFORM_ROLES: KnownRole[] = ['superadmin', 'platformsuperadmin', 'platformowner', 'platformadmin', 'supportadmin']
export const TENANT_ADMIN_ROLES: KnownRole[] = ['tenantadmin', 'admin']
export const TENANT_USER_ROLES: KnownRole[] = ['tenantadmin', 'admin', 'user']

export function isPlatformRole(role: Role) {
  return isPlatformRoleValue(role)
}

export function isTenantAdminRole(role: Role) {
  return TENANT_ADMIN_ROLES.includes(role as KnownRole)
}

export function isTenantWorkspaceRole(role: Role) {
  return !isPlatformRole(role)
}

export function roleHomePath(role: Role) {
  return roleHomePathValue(role)
}

export const KEYBOARD_SHORTCUTS = {
  search: '/',
  newWorkOrder: 'n',
  close: 'Escape',
}
