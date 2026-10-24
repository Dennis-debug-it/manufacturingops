import {
  addDays,
  eachDayOfInterval,
  endOfMonth,
  endOfWeek,
  format,
  isAfter,
  parseISO,
  startOfDay,
  startOfMonth,
  startOfWeek,
} from 'date-fns'
import type { CalendarItem } from './types'

export type CalendarMode = 'month' | 'week' | 'agenda'

/** How many days the agenda shows from the cursor. */
export const AGENDA_DAYS = 21

/** Phones start on the agenda; wider screens on the month grid. */
export function defaultCalendarMode(viewportWidth: number): CalendarMode {
  return viewportWidth < 640 ? 'agenda' : 'month'
}

/** The days a view shows, and the UTC range to ask the server for (end exclusive). */
export function calendarRange(mode: CalendarMode, cursor: Date): { days: Date[]; from: string; to: string } {
  const start = mode === 'month' ? startOfWeek(startOfMonth(cursor)) : mode === 'week' ? startOfWeek(cursor) : startOfDay(cursor)
  const end = mode === 'month' ? endOfWeek(endOfMonth(cursor)) : mode === 'week' ? endOfWeek(cursor) : addDays(start, AGENDA_DAYS - 1)
  const days = eachDayOfInterval({ start, end })
  return { days, from: startOfDay(start).toISOString(), to: addDays(startOfDay(end), 1).toISOString() }
}

/**
 * The local days (yyyy-MM-dd) an item covers. All-day items carry their date as UTC midnight, so the
 * date part is read as it is; timed items are placed on the viewer's local days.
 */
export function itemDayKeys(item: Pick<CalendarItem, 'startsAt' | 'endsAt' | 'allDay'>): string[] {
  if (item.allDay) {
    const first = parseISO(item.startsAt.slice(0, 10))
    const last = parseISO(item.endsAt.slice(0, 10))
    return eachDayOfInterval({ start: first, end: isAfter(last, first) ? last : first }).map((day) => format(day, 'yyyy-MM-dd'))
  }
  const start = new Date(item.startsAt)
  const end = new Date(item.endsAt)
  // A meeting ending exactly at midnight doesn't spill into the next day.
  const lastMoment = isAfter(end, start) ? new Date(end.getTime() - 1) : start
  return eachDayOfInterval({ start: startOfDay(start), end: startOfDay(lastMoment) }).map((day) => format(day, 'yyyy-MM-dd'))
}

/** Items by local day, all-day ones first, then by start time. */
export function groupByDay(items: CalendarItem[]): Map<string, CalendarItem[]> {
  const map = new Map<string, CalendarItem[]>()
  for (const item of items) {
    for (const key of itemDayKeys(item)) {
      const bucket = map.get(key)
      if (bucket) bucket.push(item)
      else map.set(key, [item])
    }
  }
  for (const bucket of map.values()) {
    bucket.sort((a, b) => Number(b.allDay) - Number(a.allDay) || a.startsAt.localeCompare(b.startsAt) || a.title.localeCompare(b.title))
  }
  return map
}

/** "All day" or "09:30–10:30" in the viewer's local time. */
export function itemTimeLabel(item: Pick<CalendarItem, 'startsAt' | 'endsAt' | 'allDay'>): string {
  if (item.allDay) return 'All day'
  const start = new Date(item.startsAt)
  const end = new Date(item.endsAt)
  return isAfter(end, start) ? `${format(start, 'HH:mm')}–${format(end, 'HH:mm')}` : format(start, 'HH:mm')
}

// ── Activity form times ───────────────────────────────────────────────────────

export interface ActivityTimesDraft {
  date: string
  endDate: string
  startTime: string
  endTime: string
  allDay: boolean
}

/**
 * The API's start and end from the form. An all-day activity sends its dates as UTC midnight; a timed
 * one is read in the viewer's local time. A timed end before the start rolls to the next day.
 */
export function toActivityTimes(draft: ActivityTimesDraft): { startsAt: string; endsAt: string } {
  if (draft.allDay) {
    const endDate = draft.endDate && draft.endDate >= draft.date ? draft.endDate : draft.date
    return { startsAt: `${draft.date}T00:00:00.000Z`, endsAt: `${endDate}T00:00:00.000Z` }
  }
  const start = new Date(`${draft.date}T${draft.startTime || '09:00'}`)
  let end = new Date(`${draft.date}T${draft.endTime || draft.startTime || '10:00'}`)
  if (end < start) end = addDays(end, 1)
  return { startsAt: start.toISOString(), endsAt: end.toISOString() }
}

export function fromActivityTimes(startsAt: string, endsAt: string, allDay: boolean): ActivityTimesDraft {
  if (allDay) return { date: startsAt.slice(0, 10), endDate: endsAt.slice(0, 10), startTime: '09:00', endTime: '10:00', allDay: true }
  const start = new Date(startsAt)
  const end = new Date(endsAt)
  return { date: format(start, 'yyyy-MM-dd'), endDate: format(start, 'yyyy-MM-dd'), startTime: format(start, 'HH:mm'), endTime: format(end, 'HH:mm'), allDay: false }
}

// ── Calendar feed ─────────────────────────────────────────────────────────────

/** The feed link as a full https URL, from the API base (absolute, or relative to this site). */
export function feedUrl(apiBaseUrl: string, path: string, origin: string): string {
  const base = /^https?:\/\//i.test(apiBaseUrl) ? apiBaseUrl : `${origin}${apiBaseUrl.startsWith('/') ? apiBaseUrl : ''}`
  return `${base.replace(/\/+$/, '')}${path}`
}

/** The same link with the webcal scheme, which opens the subscribe dialog in Outlook and Apple Calendar. */
export function webcalUrl(url: string): string {
  return url.replace(/^https?:\/\//i, 'webcal://')
}

// ── "Raise variation from this" ─────────────────────────────────────────────

const VARIATION_FLAG = 'newVariation'
const MAX_PREFILL = 1500

/** The project link that opens the variation form prefilled with a title and description. */
export function variationPrefillUrl(projectId: string, title: string, description: string): string {
  const params = new URLSearchParams({
    tab: 'variations',
    [VARIATION_FLAG]: '1',
    vTitle: title.slice(0, 200),
    vDescription: description.slice(0, MAX_PREFILL),
  })
  return `/projects/${projectId}?${params.toString()}`
}

/** The prefill a project page was opened with, if any. */
export function readVariationPrefill(params: URLSearchParams): { title: string; description: string } | null {
  if (params.get(VARIATION_FLAG) !== '1') return null
  return { title: params.get('vTitle') ?? '', description: params.get('vDescription') ?? '' }
}
