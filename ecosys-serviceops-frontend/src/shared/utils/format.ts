import { getActiveCurrency } from './currency'

/**
 * Renders an amount in the currency it carries. Amounts that carry none — aggregates, unit costs,
 * stock values — fall back to the workspace currency resolved from the tenant's configured country
 * (see `currency.ts`), never to a literal. A null/blank `currency` is treated the same as omitting
 * it, so a record whose currency column is empty is not formatted as `NaN`-style garbage.
 */
export const formatCurrency = (amount: number, currency?: string | null, locale = 'en-KE') =>
  new Intl.NumberFormat(locale, {
    style: 'currency',
    currency: currency?.trim() || getActiveCurrency(),
    maximumFractionDigits: 2,
  }).format(amount)

export const formatNumber = (value: number, locale = 'en-KE') =>
  new Intl.NumberFormat(locale).format(value)

export const formatPercent = (value: number, locale = 'en-KE') =>
  new Intl.NumberFormat(locale, {
    style: 'percent',
    maximumFractionDigits: 1,
  }).format(value / 100)

export const formatStatus = (value: string) =>
  value
    .replace(/[_-]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/\b\w/g, (letter) => letter.toUpperCase())
