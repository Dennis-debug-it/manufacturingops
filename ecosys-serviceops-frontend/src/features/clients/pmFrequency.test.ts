import { describe, expect, it } from 'vitest'
import { parsePmFrequencyLabel } from './pmFrequency'

describe('parsePmFrequencyLabel', () => {
  it.each([
    ['Monthly', 1, 'months'],
    ['Quarterly', 1, 'quarters'],
    ['Semi-Annual', 6, 'months'],
    ['Annually', 1, 'years'],
    ['Every 2 weeks', 2, 'weeks'],
    ['3 months', 3, 'months'],
    ['1-Year', 1, 'years'],
  ])('parses %s', (label, value, unit) => {
    expect(parsePmFrequencyLabel(label)).toEqual({ value, unit })
  })

  it.each(['', 'when it breaks', '0 months', null])('rejects %s', (label) => {
    expect(parsePmFrequencyLabel(label)).toBeNull()
  })
})
