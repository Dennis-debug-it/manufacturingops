import {
  addDays as addDaysFn,
  differenceInDays as differenceInDaysFn,
  differenceInHours as differenceInHoursFn,
  differenceInMinutes as differenceInMinutesFn,
  format,
  formatDistanceToNowStrict,
  isPast as isPastFn,
  isToday as isTodayFn,
  subMonths as subMonthsFn,
} from 'date-fns'
import { toZonedDate } from './timezone'

const isValidDate = (value: Date) => !Number.isNaN(value.getTime())

let tenantTimeZone = 'Africa/Nairobi'

/** Set from the authenticated tenant, never from the browser timezone. */
export function setTenantTimeZone(timeZoneId?: string | null, country?: string | null) {
  const candidate = timeZoneId || (country?.toLowerCase() === 'kenya' ? 'Africa/Nairobi' : 'UTC')
  try {
    new Intl.DateTimeFormat('en-GB', { timeZone: candidate }).format()
    tenantTimeZone = candidate
  } catch {
    tenantTimeZone = 'UTC'
  }
}

function parseTimestamp(value: string | Date) {
  // Existing API DateTime values are UTC even when older serializers omit Z.
  return new Date(typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value) && !/(Z|[+-]\d{2}:?\d{2})$/i.test(value) ? `${value}Z` : value)
}

export const formatDate = (value: string | Date, pattern = 'dd MMM yyyy') => {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    const [year, month, day] = value.split('-').map(Number)
    const dateOnly = new Date(year!, month! - 1, day!)
    return isValidDate(dateOnly) ? format(dateOnly, pattern) : '—'
  }
  const date = parseTimestamp(value)
  if (!isValidDate(date)) return '—'
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: tenantTimeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(date)
  const part = (type: string) => Number(parts.find((item) => item.type === type)?.value)
  return format(new Date(part('year'), part('month') - 1, part('day')), pattern)
}
export const formatDateTime = (value: string | Date) => {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) return formatDate(value)
  const date = parseTimestamp(value)
  return isValidDate(date) ? new Intl.DateTimeFormat('en-GB', {
    timeZone: tenantTimeZone, day: '2-digit', month: 'short', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZoneName: 'short',
  }).format(date) : '—'
}
export const formatTime = (value: string | Date, pattern = 'HH:mm') => {
  const date = parseTimestamp(value)
  return isValidDate(date) ? format(toZonedDate(date), pattern) : '—'
}
export const toWorkspaceDate = (value: string | Date) => toZonedDate(parseTimestamp(value))
export const formatRelative = (value: string | Date) => {
  const date = parseTimestamp(value)
  return isValidDate(date) ? formatDistanceToNowStrict(date, { addSuffix: true }) : '—'
}
export const daysUntil = (value: string | Date) => differenceInDaysFn(new Date(value), new Date())
export const daysOverdue = (value: string | Date) => Math.max(0, -differenceInDaysFn(new Date(value), new Date()))
export const daysAgo = (value: string | Date) => differenceInDaysFn(new Date(), new Date(value))
export const formatDuration = (minutes: number) => {
  const hours = Math.floor(minutes / 60)
  const mins = minutes % 60
  return `${hours}h ${String(mins).padStart(2, '0')}m`
}
export const ageInDays = (value: string | Date) => differenceInDaysFn(new Date(), new Date(value))
export const isToday = (value: string | Date) => isTodayFn(new Date(value))
export const isPast = (value: string | Date) => isPastFn(new Date(value))
export const addDays = (value: string | Date, amount: number) => addDaysFn(new Date(value), amount)
export const subMonths = (value: string | Date, amount: number) => subMonthsFn(new Date(value), amount)
export const differenceInDays = (left: string | Date, right: string | Date) => differenceInDaysFn(new Date(left), new Date(right))
export const differenceInHours = (left: string | Date, right: string | Date) => differenceInHoursFn(new Date(left), new Date(right))
export const differenceInMinutes = (left: string | Date, right: string | Date) => differenceInMinutesFn(new Date(left), new Date(right))
