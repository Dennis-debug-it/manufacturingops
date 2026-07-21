import { differenceInMinutes } from '../../shared/utils/dates'

export interface WorkOrderRow {
  id: string
  woNo: string
  title: string
  client: string
  clientId: string
  site: string
  siteId: string
  asset: string
  type: string
  priority: string
  status: string
  assignedTo: string
  assignedTechnicianId: string
  dueDate: string
  scheduledStartAt: string | null
  createdAt: string
  lastUpdate: string
  slaResolutionDeadline: string | null
  slaResponseBreached: boolean
  slaResolutionBreached: boolean
  latitude: number | null
  longitude: number | null
  estimatedHours: number
  isUnassigned: boolean
  availableTransitions: string[]
  childWorkOrderCount: number
  holdReason: string | null
  holdNote: string | null
  isRepeatVisit: boolean
  repeatVisitCount: number
}

export type SlaStatusKey = 'on-track' | 'at-risk' | 'breached' | 'no-sla'

export function resolveSlaStatusKey(deadline: string | null, breached: boolean): SlaStatusKey {
  if (breached) return 'breached'
  if (!deadline) return 'no-sla'
  const minutesRemaining = differenceInMinutes(deadline, new Date())
  return minutesRemaining <= 120 ? 'at-risk' : 'on-track'
}

export const slaStatusLabels: Record<SlaStatusKey, string> = {
  'on-track': 'On track',
  'at-risk': 'At risk',
  breached: 'Breached',
  'no-sla': 'No SLA',
}

/** Kanban columns, in board order. */
export const KANBAN_STATUSES = ['New', 'Assigned', 'In Progress', 'On Hold', 'Completed', 'Closed'] as const

const asText = (value: unknown) => (typeof value === 'string' ? value : value == null ? '—' : String(value))
const asId = (value: unknown) => (typeof value === 'string' ? value : '')
const asNumber = (value: unknown) => (typeof value === 'number' ? value : null)

const pick = (row: Record<string, unknown>, keys: string[]) => {
  for (const key of keys) {
    const value = row[key]
    if (typeof value === 'string' && value.trim()) {
      return value
    }
  }
  return '—'
}

export function normalizeWorkOrders(payload: unknown): WorkOrderRow[] {
  const records = Array.isArray(payload)
    ? payload
    : payload && typeof payload === 'object'
      ? (payload as { items?: unknown[]; data?: unknown[] }).items ?? (payload as { data?: unknown[] }).data ?? []
      : []

  return records
    .filter((record): record is Record<string, unknown> => Boolean(record && typeof record === 'object'))
    .map((row, index) => ({
      id: asText(row.id ?? row.workOrderId ?? `${index}`),
      woNo: pick(row, ['workOrderNumber', 'woNo', 'woNumber', 'workOrderNo', 'number', 'code']),
      title: pick(row, ['title']),
      client: pick(row, ['clientName', 'client', 'customerName']),
      clientId: asId(row.clientId),
      site: pick(row, ['siteName', 'site']),
      siteId: asId(row.siteId),
      asset: pick(row, ['assetName', 'asset']),
      type: pick(row, ['workType', 'type']),
      priority: pick(row, ['priority']),
      status: pick(row, ['status', 'state']),
      assignedTo: pick(row, ['assignmentSummary', 'assignedTechnicianName', 'assignedTo', 'technicianName', 'assignee']),
      assignedTechnicianId: asId(row.assignedTechnicianId),
      dueDate: pick(row, ['dueDate', 'dueAt']),
      scheduledStartAt: typeof row.scheduledStartAt === 'string' ? row.scheduledStartAt : null,
      createdAt: typeof row.createdAt === 'string' ? row.createdAt : '',
      lastUpdate: pick(row, ['updatedAt', 'lastUpdate', 'lastUpdatedAt']),
      slaResolutionDeadline: typeof row.slaResolutionDeadline === 'string' ? row.slaResolutionDeadline : null,
      slaResponseBreached: Boolean(row.slaResponseBreached),
      slaResolutionBreached: Boolean(row.slaResolutionBreached),
      latitude: asNumber(row.siteLatitude),
      longitude: asNumber(row.siteLongitude),
      estimatedHours: asNumber(row.estimatedLabourHours) ?? 0,
      isUnassigned: Boolean(row.isUnassigned),
      availableTransitions: Array.isArray(row.availableTransitions) ? row.availableTransitions.filter((v): v is string => typeof v === 'string') : [],
      childWorkOrderCount: typeof row.childWorkOrderCount === 'number' ? row.childWorkOrderCount : 0,
      holdReason: typeof row.holdReason === 'string' ? row.holdReason : null,
      holdNote: typeof row.holdNote === 'string' ? row.holdNote : null,
      isRepeatVisit: Boolean(row.isRepeatVisit),
      repeatVisitCount: typeof row.repeatVisitCount === 'number' ? row.repeatVisitCount : 0,
    }))
}

export interface WorkOrderFilterValues {
  search: string
  status: string
  technicianId: string
  clientId: string
  siteId: string
  priority: string
  slaStatus: string
  unassignedOnly: boolean
  fromDate: string
  toDate: string
}

export function filterWorkOrderRows(rows: WorkOrderRow[], filters: WorkOrderFilterValues): WorkOrderRow[] {
  const query = filters.search.trim().toLowerCase()

  return rows.filter((row) => {
    if (query && ![row.woNo, row.client, row.site, row.asset, row.type, row.assignedTo, row.status, row.title]
      .some((value) => value.toLowerCase().includes(query))) {
      return false
    }
    if (filters.status && row.status !== filters.status) return false
    if (filters.technicianId && row.assignedTechnicianId !== filters.technicianId) return false
    if (filters.clientId && row.clientId !== filters.clientId) return false
    if (filters.siteId && row.siteId !== filters.siteId) return false
    if (filters.priority && row.priority !== filters.priority) return false
    if (filters.slaStatus && resolveSlaStatusKey(row.slaResolutionDeadline, row.slaResponseBreached || row.slaResolutionBreached) !== filters.slaStatus) return false
    if (filters.unassignedOnly && !row.isUnassigned) return false
    if (filters.fromDate) {
      const reference = row.dueDate !== '—' ? row.dueDate : row.createdAt
      if (!reference || new Date(reference) < new Date(filters.fromDate)) return false
    }
    if (filters.toDate) {
      const reference = row.dueDate !== '—' ? row.dueDate : row.createdAt
      if (!reference || new Date(reference) > new Date(`${filters.toDate}T23:59:59`)) return false
    }
    return true
  })
}
