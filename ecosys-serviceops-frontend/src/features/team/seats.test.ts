import { describe, expect, it } from 'vitest'
import { countSeatsUsed, seatTypeFor } from './seats'

describe('seats', () => {
  it('maps roles to the seat they take', () => {
    expect(seatTypeFor('Technician')).toBe('field')
    expect(seatTypeFor('Viewer')).toBe('free')
    expect(seatTypeFor('User')).toBe('free')
    expect(seatTypeFor('Dispatcher')).toBe('office')
    expect(seatTypeFor('finance')).toBe('office')
  })

  it('counts only active accounts', () => {
    expect(countSeatsUsed([
      { role: 'Admin', isActive: true },
      { role: 'Technician', isActive: true },
      { role: 'Technician', isActive: false },
      { role: 'Viewer', isActive: true },
    ])).toEqual({ office: 1, field: 1 })
  })
})
