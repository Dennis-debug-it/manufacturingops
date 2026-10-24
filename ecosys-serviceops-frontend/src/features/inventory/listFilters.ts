import type { UrlFilterSpec } from '../../shared/utils/urlFilters'
import type { MaterialIssue } from './types'
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

// ── Material issues ─────────────────────────────────────────────────────────

export const materialIssueStatusLabels: Record<string, string> = {
  draft: 'Draft',
  pending_approval: 'Awaiting approval',
  approved: 'Approved, not issued',
  issued: 'Issued',
  returned: 'Returned',
  rejected: 'Rejected',
  cancelled: 'Cancelled',
}

export const materialIssueListFilterSpec: UrlFilterSpec<'status'> = {
  status: { param: 'status', values: Object.keys(materialIssueStatusLabels) },
}

export function filterMaterialIssues(issues: MaterialIssue[], status: string): MaterialIssue[] {
  return status ? issues.filter((issue) => issue.issueStatus === status) : issues
}
