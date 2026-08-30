import type { WorkOrderListFilters } from './api'
import { hasAcceptedTechnician, hasRecordedArrival, readAssignmentGuardState } from './dispatchGuards'

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
  /** Server Start guard: at least one persisted technician assignment in an accepted state. */
  hasAcceptedTechnician: boolean
  /** Server Complete guard: at least one persisted technician assignment with an arrival recorded. */
  hasRecordedArrival: boolean
}

/**
 * SLA filter values. These are sent verbatim as the `slaStatus` query param — the server's
 * `ApplySlaStatusFilter` strips non-letters before matching, so `on-track`/`at-risk`/`breached`/
 * `no-sla` are exactly the four literals it accepts and anything else is a 400.
 */
export type SlaStatusKey = 'on-track' | 'at-risk' | 'breached' | 'no-sla'

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
      hasAcceptedTechnician: hasAcceptedTechnician(readAssignmentGuardState(row.technicianAssignments)),
      hasRecordedArrival: hasRecordedArrival(readAssignmentGuardState(row.technicianAssignments)),
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

/**
 * Translates the URL-backed filter state into the query params `WorkOrdersController.GetAll`
 * accepts, so the list is filtered in the database instead of being downloaded whole and filtered
 * in the browser.
 *
 * Two semantics shift with the move to the server and are intentional:
 * - `fromDate`/`toDate` bound `CreatedAt` server-side (the only date the endpoint filters on),
 *   where the old client-side pass used the due date and fell back to created.
 * - `search` matches WO number, title, client, asset and the assignment summary server-side; site
 *   and work type are no longer part of the free-text match.
 *
 * Empty strings are dropped rather than sent, because the server treats a present-but-blank
 * `status`/`priority`/`slaStatus` as a value to normalize and rejects what it cannot parse.
 */
export function toWorkOrderListFilters(filters: WorkOrderFilterValues): WorkOrderListFilters {
  const trimmedSearch = filters.search.trim()
  return {
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.priority ? { priority: filters.priority } : {}),
    ...(filters.technicianId ? { technicianId: filters.technicianId } : {}),
    ...(filters.clientId ? { clientId: filters.clientId } : {}),
    ...(filters.siteId ? { siteId: filters.siteId } : {}),
    ...(filters.slaStatus ? { slaStatus: filters.slaStatus } : {}),
    ...(filters.unassignedOnly ? { unassigned: true } : {}),
    ...(trimmedSearch ? { search: trimmedSearch } : {}),
    ...(filters.fromDate ? { fromDate: `${filters.fromDate}T00:00:00` } : {}),
    ...(filters.toDate ? { toDate: `${filters.toDate}T23:59:59` } : {}),
  }
}
