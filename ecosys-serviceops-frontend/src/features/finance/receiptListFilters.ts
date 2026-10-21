import type { UrlFilterSpec } from '../../shared/utils/urlFilters'

/** `?status=Confirmed&from=YYYY-MM-DD`: the dashboard's "Cash received this month". */
export const receiptListFilterSpec: UrlFilterSpec<'status' | 'from'> = {
  status: { param: 'status', values: ['Confirmed', 'Reversed'] },
  from: { param: 'from' },
}

/** Receipts with that status, dated on or after `from` (by calendar date; '' leaves either open). */
export function filterReceipts<T extends { status: string; paymentDate: string }>(rows: T[], filters: { status: string; from: string }): T[] {
  const from = /^\d{4}-\d{2}-\d{2}$/.test(filters.from) ? filters.from : ''
  return rows.filter((row) => (!filters.status || row.status === filters.status) && (!from || row.paymentDate.slice(0, 10) >= from))
}
