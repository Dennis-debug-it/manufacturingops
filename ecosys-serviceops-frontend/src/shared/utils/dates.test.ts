import { describe, expect, it } from 'vitest'
import { formatDate, formatDateTime, formatTime } from './dates'

const EM_DASH = '\u2014'

describe('date formatting placeholders', () => {
  it('renders an em dash, not a replacement character, for invalid dates', () => {
    expect(formatDate('not-a-date')).toBe(EM_DASH)
    expect(formatDateTime('not-a-date')).toBe(EM_DASH)
    expect(formatTime('not-a-date')).toBe(EM_DASH)
  })

  it('still formats real dates', () => {
    expect(formatDate('2026-09-28')).toBe('28 Sep 2026')
  })
})
