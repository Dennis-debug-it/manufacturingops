import { describe, expect, it } from 'vitest'
import { describeTrend } from './api'

describe('describeTrend', () => {
  it('says when a rising gauge reaches its next limit', () => {
    expect(describeTrend({ readings: 12, slopePerDay: 0.8, heading: 'Rising', approachingLevel: 'Warning', approachingLimit: 70, daysToLimit: 4.2 }, '°C'))
      .toBe('Rising 0.80 °C a day; reaches the warning limit (70 °C) in about 4 days at this rate.')
    expect(describeTrend({ readings: 12, slopePerDay: -2, heading: 'Falling', approachingLevel: 'Alarm', approachingLimit: 6, daysToLimit: 0.4 }, 'bar'))
      .toBe('Falling 2.0 bar a day; reaches the alarm limit (6.0 bar) within a day at this rate.')
  })

  it('handles steady, harmless and too-few readings', () => {
    expect(describeTrend({ readings: 9, slopePerDay: 0, heading: 'Steady', approachingLevel: null, approachingLimit: null, daysToLimit: null }, '°C')).toBe('Steady over the last two weeks.')
    expect(describeTrend({ readings: 9, slopePerDay: -1.5, heading: 'Falling', approachingLevel: null, approachingLimit: null, daysToLimit: null }, '°C')).toBe('Falling 1.5 °C a day, away from its limits.')
    expect(describeTrend({ readings: 3, slopePerDay: null, heading: null, approachingLevel: null, approachingLimit: null, daysToLimit: null }, '°C')).toBe('Not enough readings for a trend (3 of 5).')
  })
})
