import type { UrlFilterSpec } from '../../shared/utils/urlFilters'
import type { ContractListItem } from './types'

/** `?expiring=30|90`: live contracts ending within that many days, as the dashboard counts them. */
export const contractListFilterSpec: UrlFilterSpec<'expiring'> = {
  expiring: { param: 'expiring', values: ['30', '60', '90'] },
}

const endedStatuses = new Set(['expired', 'terminated', 'cancelled'])

export function filterContracts(rows: ContractListItem[], expiring: string): ContractListItem[] {
  if (!expiring) return rows
  const days = Number(expiring)
  return rows.filter((row) => !endedStatuses.has(row.status.toLowerCase()) && row.daysToExpiry >= 0 && row.daysToExpiry <= days)
}
