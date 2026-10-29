import { describe, expect, it } from 'vitest'
import { forecastLabel, presetRange } from './SalesReportsPage'

describe('presetRange', () => {
  const now = new Date(2026, 9, 29, 15, 0)

  it('counts the last N days including today', () => {
    expect(presetRange('30d', now)).toEqual({ from: '2026-09-30', to: '2026-10-29' })
    expect(presetRange('90d', now)).toEqual({ from: '2026-08-01', to: '2026-10-29' })
  })

  it('starts calendar presets at the start of the month or year', () => {
    expect(presetRange('month', now)).toEqual({ from: '2026-10-01', to: '2026-10-29' })
    expect(presetRange('year', now)).toEqual({ from: '2026-01-01', to: '2026-10-29' })
  })
})

describe('forecastLabel', () => {
  it('names month buckets and the open-ended ones', () => {
    expect(forecastLabel({ key: '2026-11' })).toBe('Nov 2026')
    expect(forecastLabel({ key: 'overdue' })).toBe('Close date passed')
    expect(forecastLabel({ key: 'none' })).toBe('No close date')
  })
})
