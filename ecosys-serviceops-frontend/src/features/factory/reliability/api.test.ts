import { describe, expect, it } from 'vitest'
import { formatMtbf, suggestedCadence, type PmSuggestion } from './api'

const base: PmSuggestion = {
  assetId: 'a', assetCode: 'A', assetName: 'A', kind: 'NoPlan', reason: '', failures: 5, meanDaysBetween: 30, topProblem: null,
  triggerType: 'Calendar', frequencyUnit: 'Weeks', frequencyInterval: 3, meterId: null, meterInterval: null, meterUnit: null, planId: null, planName: null, currentInterval: null,
}

describe('reliability helpers', () => {
  it('describes the suggested cadence', () => {
    expect(suggestedCadence(base)).toBe('every 3 weeks')
    expect(suggestedCadence({ ...base, frequencyInterval: 1, frequencyUnit: 'Months' })).toBe('every 1 month')
    expect(suggestedCadence({ ...base, triggerType: 'Meter', meterInterval: 350, meterUnit: 'h' })).toBe('every 350 h')
    expect(suggestedCadence({ ...base, kind: 'Random', triggerType: null })).toBeNull()
  })

  it('formats MTBF', () => {
    expect(formatMtbf(null)).toBe('—')
    expect(formatMtbf(30)).toBe('30 h')
    expect(formatMtbf(72)).toBe('3.0 days')
    expect(formatMtbf(1748)).toBe('73 days')
  })
})
