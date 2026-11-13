import { describe, expect, it } from 'vitest'
import { badgeToken, findOfflineBadge, toLocalInput, type GateIndex } from './api'
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

  it('finds a scanned badge or pass in the device copy when there is no signal', () => {
    const index: GateIndex = {
      generatedAt: '2026-11-13T08:00:00Z',
      people: [{ attendeeId: 'a1', badgeToken: 'personToken12345', fullName: 'Ann One', company: null, isBlocked: false, badgeNumber: 'VIS-000001-1', visitNumber: 'VIS-000001',
        visitStatus: 'OnSite', factoryName: 'Chelal', hostName: 'Grace', purpose: 'Meeting', expectedTo: '2026-11-13T14:00:00Z', checkedInAt: '2026-11-13T07:00:00Z', zones: [] }],
      passes: [{ passId: 'p1', badgeToken: 'passToken1234567', passNumber: 'PASS-00001', fullName: 'Sam Kiprono', company: 'Boilerworks', isBlocked: false, factoryName: 'Chelal', hostName: 'Grace', zones: ['Boiler house'] }],
    }
    expect(findOfflineBadge(index, 'VISITOR:personToken12345')).toEqual({ kind: 'person', person: index.people[0] })
    expect(findOfflineBadge(index, 'passToken1234567')).toEqual({ kind: 'pass', pass: index.passes[0] })
    expect(findOfflineBadge(index, 'VISITOR:somethingElse123')).toBeNull()
    expect(findOfflineBadge(undefined, 'VISITOR:personToken12345')).toBeNull()
  })
})
