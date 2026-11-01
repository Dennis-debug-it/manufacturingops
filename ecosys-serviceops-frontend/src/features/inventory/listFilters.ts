import type { UrlFilterSpec } from '../../shared/utils/urlFilters'
import type { InventoryRequest, InventoryRequestStage } from './requestsApi'
import type { PurchaseOrderStatus } from './purchaseOrdersApi'

// ── Purchase orders ─────────────────────────────────────────────────────────

export const purchaseOrderStatuses: PurchaseOrderStatus[] = ['Draft', 'Sent', 'PartiallyReceived', 'Received', 'Cancelled']

/**
 * `state` is the server's open/late filter (the dashboard's "Open purchase orders" counts);
 * `status` is one exact status. The page offers them as one row of choices, so setting one clears
 * the other.
 */
export const purchaseOrderListFilterSpec: UrlFilterSpec<'state' | 'status'> = {
  state: { param: 'state', values: ['open', 'late'] },
  status: { param: 'status', values: purchaseOrderStatuses },
}

export type PurchaseOrderChoice = 'All' | 'open' | 'late' | PurchaseOrderStatus

export function purchaseOrderChoice(filters: { state: string; status: string }): PurchaseOrderChoice {
  return (filters.state || filters.status || 'All') as PurchaseOrderChoice
}

export function purchaseOrderFiltersFor(choice: PurchaseOrderChoice): { state: string; status: string } {
  if (choice === 'open' || choice === 'late') return { state: choice, status: '' }
  return { state: '', status: choice === 'All' ? '' : choice }
}

// ── Requests queue ──────────────────────────────────────────────────────────

/** `open` (the default) is Requested + Approved: everything still waiting on stores. */
export type RequestStageFilter = 'open' | InventoryRequestStage | 'all'

export const requestStageFilters: Array<{ value: RequestStageFilter; label: string }> = [
  { value: 'open', label: 'Needs action' },
  { value: 'Requested', label: 'Requested' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Issued', label: 'Issued' },
  { value: 'Completed', label: 'Completed' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'all', label: 'All' },
]

/** Reads `?stage=` from a link (the dashboard sends Requested / Approved); anything else is `open`. */
export function requestStageFromParam(value: string | null): RequestStageFilter {
  return requestStageFilters.find((option) => option.value === value)?.value ?? 'open'
}

export function matchesRequestStage(request: Pick<InventoryRequest, 'stage'>, stage: RequestStageFilter) {
  if (stage === 'all') return true
  if (stage === 'open') return request.stage === 'Requested' || request.stage === 'Approved'
  return request.stage === stage
}
