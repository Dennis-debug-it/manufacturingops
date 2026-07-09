export const formatCurrency = (amount: number, currency = 'KES', locale = 'en-KE') =>
  new Intl.NumberFormat(locale, {
    style: 'currency',
    currency,
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
