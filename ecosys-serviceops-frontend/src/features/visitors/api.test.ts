import { describe, expect, it } from 'vitest'
import { badgeToken, toLocalInput } from './api'
import { canAccessModulePath } from '../../shared/utils/entitlements'

describe('visitor helpers', () => {
  it('reads the token from a scanned badge, a link or the bare code', () => {
    expect(badgeToken('VISITOR:abc123DEF456ghi789')).toBe('abc123DEF456ghi789')
    expect(badgeToken('visitor:abc123DEF456ghi789')).toBe('abc123DEF456ghi789')
    expect(badgeToken(' https://ops.example/visitors/badge/abc123DEF456ghi789 ')).toBe('abc123DEF456ghi789')
    expect(badgeToken('abc123DEF456ghi789')).toBe('abc123DEF456ghi789')
  })

  it('formats a date for a datetime-local input', () => {
    expect(toLocalInput(new Date(2026, 10, 5, 7, 3))).toBe('2026-11-05T07:03')
  })

  it('opens the Visitors screen only with the Visitor Management add-on', () => {
    expect(canAccessModulePath('/visitors', ['operations', 'finance', 'maintenance'])).toBe(false)
    expect(canAccessModulePath('/visitors', ['operations', 'finance', 'maintenance', 'visitors'])).toBe(true)
  })
})
