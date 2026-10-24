import type { UrlFilterSpec } from '../../shared/utils/urlFilters'

/**
 * The invoice list's URL filters. `age` is the server's receivables filter, the one the finance
 * dashboard widgets count with: `overdue`, `current` (not yet due) or an ageing bucket.
 */
export const invoiceAgeLabels = {
  overdue: 'Overdue',
  current: 'Not yet due',
  '1-30': '1–30 days overdue',
  '31-60': '31–60 days overdue',
  '61-90': '61–90 days overdue',
  '90-plus': '90+ days overdue',
} as const

export const invoiceStatusOptions = ['Draft', 'Sent', 'PartiallyPaid', 'Paid', 'Overdue', 'Void'] as const

export const invoiceListFilterSpec: UrlFilterSpec<'status' | 'age' | 'currency'> = {
  status: { param: 'status', values: invoiceStatusOptions },
  age: { param: 'age', values: Object.keys(invoiceAgeLabels) },
  currency: { param: 'currency' },
}

/** The API parameters for the filters that are set. */
export function invoiceListParams(filters: { status: string; age: string; currency: string }) {
  return {
    ...(filters.status ? { status: filters.status } : {}),
    ...(filters.age ? { age: filters.age } : {}),
    ...(filters.currency ? { currency: filters.currency.toUpperCase() } : {}),
  }
}
