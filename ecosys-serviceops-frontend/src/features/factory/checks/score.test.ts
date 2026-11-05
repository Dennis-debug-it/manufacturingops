import { describe, expect, it } from 'vitest'
import { scoreAnswer } from './CheckRunPage'

describe('scoreAnswer', () => {
  it('scores pass/fail answers the way the server does', () => {
    expect(scoreAnswer({ type: 'passfail', minValue: null, maxValue: null }, 'pass')).toBe(true)
    expect(scoreAnswer({ type: 'passfail', minValue: null, maxValue: null }, 'fail')).toBe(false)
    expect(scoreAnswer({ type: 'yesno', minValue: null, maxValue: null }, 'No')).toBe(false)
    expect(scoreAnswer({ type: 'passfail', minValue: null, maxValue: null }, '')).toBeNull()
  })

  it('checks readings against their limits', () => {
    const q = { type: 'reading', minValue: 10, maxValue: 20 }
    expect(scoreAnswer(q, '15')).toBe(true)
    expect(scoreAnswer(q, '25')).toBe(false)
    expect(scoreAnswer(q, '9.5')).toBe(false)
    expect(scoreAnswer({ type: 'reading', minValue: null, maxValue: null }, '99')).toBeNull()
  })
})
