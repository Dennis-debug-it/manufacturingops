import { describe, expect, it } from 'vitest'
import {
  AGENDA_DAYS,
  calendarRange,
  defaultCalendarMode,
  feedUrl,
  fromActivityTimes,
  groupByDay,
  itemDayKeys,
  readVariationPrefill,
  toActivityTimes,
  variationPrefillUrl,
  webcalUrl,
} from './logic'
import type { CalendarItem } from './types'

const item = (overrides: Partial<CalendarItem>): CalendarItem => ({
  uid: 'activity-1', source: 'activities', title: 'Site meeting', startsAt: '', endsAt: '', allDay: false,
  location: null, url: null, userId: null, status: null, ...overrides,
})

describe('calendar views', () => {
  it('opens on the agenda on phones and the month grid elsewhere', () => {
    expect(defaultCalendarMode(360)).toBe('agenda')
    expect(defaultCalendarMode(1280)).toBe('month')
  })

  it('covers whole weeks for the month grid and a fixed run of days for the agenda', () => {
    const month = calendarRange('month', new Date(2026, 9, 21))
    expect(month.days.length % 7).toBe(0)
    expect(month.days[0]!.getDay()).toBe(0)
    expect(calendarRange('week', new Date(2026, 9, 21)).days).toHaveLength(7)
    const agenda = calendarRange('agenda', new Date(2026, 9, 21, 15, 30))
    expect(agenda.days).toHaveLength(AGENDA_DAYS)
    expect(new Date(agenda.to).getTime() - new Date(agenda.from).getTime()).toBeGreaterThanOrEqual(AGENDA_DAYS * 23 * 3600_000)
  })

  it('places all-day items on their dates whatever the time zone, and timed ones on local days', () => {
    expect(itemDayKeys(item({ allDay: true, startsAt: '2026-11-02T00:00:00Z', endsAt: '2026-11-04T00:00:00Z' }))).toEqual(['2026-11-02', '2026-11-03', '2026-11-04'])
    const start = new Date(2026, 10, 2, 9, 0)
    const end = new Date(2026, 10, 3, 0, 0)
    // Ending exactly at midnight stays on the first day.
    expect(itemDayKeys(item({ startsAt: start.toISOString(), endsAt: end.toISOString() }))).toEqual(['2026-11-02'])
  })

  it('groups a day with all-day items first, then by start time', () => {
    const nine = new Date(2026, 10, 2, 9, 0).toISOString()
    const eight = new Date(2026, 10, 2, 8, 0).toISOString()
    const map = groupByDay([
      item({ uid: 'b', title: 'Nine', startsAt: nine, endsAt: nine }),
      item({ uid: 'c', title: 'Eight', startsAt: eight, endsAt: eight }),
      item({ uid: 'a', title: 'Due', allDay: true, startsAt: '2026-11-02T00:00:00Z', endsAt: '2026-11-02T00:00:00Z' }),
    ])
    expect(map.get('2026-11-02')!.map((x) => x.title)).toEqual(['Due', 'Eight', 'Nine'])
  })
})

describe('activity form times', () => {
  it('sends all-day dates as UTC midnight and never ends before the start', () => {
    expect(toActivityTimes({ date: '2026-11-02', endDate: '2026-11-01', startTime: '', endTime: '', allDay: true }))
      .toEqual({ startsAt: '2026-11-02T00:00:00.000Z', endsAt: '2026-11-02T00:00:00.000Z' })
  })

  it('rolls a timed end before the start into the next day, and round-trips', () => {
    const times = toActivityTimes({ date: '2026-11-02', endDate: '2026-11-02', startTime: '22:00', endTime: '01:00', allDay: false })
    expect(new Date(times.endsAt).getTime() - new Date(times.startsAt).getTime()).toBe(3 * 3600_000)
    const back = fromActivityTimes(times.startsAt, times.endsAt, false)
    expect(back).toMatchObject({ date: '2026-11-02', startTime: '22:00', endTime: '01:00', allDay: false })
  })
})

describe('calendar feed link', () => {
  it('builds a full URL from an absolute or relative API base', () => {
    expect(feedUrl('https://api.ecosys.test/', '/api/calendar/feed/abc.ics', 'https://app.ecosys.test')).toBe('https://api.ecosys.test/api/calendar/feed/abc.ics')
    expect(feedUrl('', '/api/calendar/feed/abc.ics', 'https://app.ecosys.test')).toBe('https://app.ecosys.test/api/calendar/feed/abc.ics')
    expect(webcalUrl('https://api.ecosys.test/api/calendar/feed/abc.ics')).toBe('webcal://api.ecosys.test/api/calendar/feed/abc.ics')
  })
})

describe('raise variation from this', () => {
  it('round-trips the prefill through the project link', () => {
    const url = variationPrefillUrl('p1', 'From meeting: Site meeting', 'Client asked for 2 extra sockets & a DB.')
    const [path, query] = url.split('?')
    expect(path).toBe('/projects/p1')
    const params = new URLSearchParams(query)
    expect(params.get('tab')).toBe('variations')
    expect(readVariationPrefill(params)).toEqual({ title: 'From meeting: Site meeting', description: 'Client asked for 2 extra sockets & a DB.' })
    expect(readVariationPrefill(new URLSearchParams('tab=variations'))).toBeNull()
  })
})
