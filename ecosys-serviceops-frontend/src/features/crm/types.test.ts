import { describe, expect, it } from 'vitest'
import { daysUntilDeadline, opportunityParty } from './types'

describe('daysUntilDeadline', () => {
  const now = new Date(2026, 9, 20, 15, 30)

  it('counts calendar days, not 24-hour blocks', () => {
    expect(daysUntilDeadline(new Date(2026, 9, 20, 9, 0).toISOString(), now)).toBe(0)
    expect(daysUntilDeadline(new Date(2026, 9, 21, 8, 0).toISOString(), now)).toBe(1)
    expect(daysUntilDeadline(new Date(2026, 9, 27, 23, 59).toISOString(), now)).toBe(7)
  })

  it('goes negative once the deadline has passed', () => {
    expect(daysUntilDeadline(new Date(2026, 9, 17, 12, 0).toISOString(), now)).toBe(-3)
  })
})

describe('opportunityParty', () => {
  it('prefers the client, then the prospect company, then the contact', () => {
    expect(opportunityParty({ clientName: 'Kilele Hotel', companyName: 'Old name', contactName: 'Amina' })).toBe('Kilele Hotel')
    expect(opportunityParty({ clientName: null, companyName: 'Baraka Logistics', contactName: 'Amina' })).toBe('Baraka Logistics')
    expect(opportunityParty({ clientName: null, companyName: null, contactName: 'Amina' })).toBe('Amina')
  })
})
