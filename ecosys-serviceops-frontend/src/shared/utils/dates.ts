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

const isValidDate = (value: Date) => !Number.isNaN(value.getTime())

export const formatDate = (value: string | Date, pattern = 'dd MMM yyyy') => {
  const date = new Date(value)
  return isValidDate(date) ? format(date, pattern) : '—'
}
export const formatDateTime = (value: string | Date) => {
  const date = new Date(value)
  return isValidDate(date) ? format(date, 'dd MMM yyyy, HH:mm') : '—'
}
export const formatRelative = (value: string | Date) => {
  const date = new Date(value)
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
