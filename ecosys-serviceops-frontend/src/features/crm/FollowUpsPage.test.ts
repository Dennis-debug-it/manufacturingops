import { describe, expect, it } from 'vitest'
import { followUpBucket } from './FollowUpsPage'

describe('followUpBucket', () => {
  const now = new Date(2026, 9, 29, 12, 0)

  it('treats a due time that has passed as overdue, even earlier today', () => {
    expect(followUpBucket(new Date(2026, 9, 28, 9, 0).toISOString(), now)).toBe('overdue')
    expect(followUpBucket(new Date(2026, 9, 29, 9, 0).toISOString(), now)).toBe('overdue')
  })

  it('groups what is still ahead by day', () => {
    expect(followUpBucket(new Date(2026, 9, 29, 16, 0).toISOString(), now)).toBe('today')
    expect(followUpBucket(new Date(2026, 9, 30, 9, 0).toISOString(), now)).toBe('week')
    expect(followUpBucket(new Date(2026, 10, 5, 9, 0).toISOString(), now)).toBe('week')
    expect(followUpBucket(new Date(2026, 10, 6, 9, 0).toISOString(), now)).toBe('later')
  })
})
