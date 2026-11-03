import { describe, it, expect } from 'vitest'
import {
  buildCreateTenantPayload,
  DEFAULT_FIELD_SEATS,
  DEFAULT_LICENSED_USERS,
  FIELD_SEATS_INVALID,
  LICENSED_USERS_INVALID,
  LICENSED_USERS_REQUIRED,
  parseLicensedUsers,
  summarizeSeats,
  type CreateTenantForm,
} from './licensedUsers'

const validForm: CreateTenantForm = {
  companyName: ' Acme Services ',
  slug: 'acme',
  planCode: 'professional',
  adminFullName: 'Jane Admin',
  adminEmail: 'jane@acme.test',
  country: 'Kenya',
  timeZoneId: 'Africa/Nairobi',
  maxUsers: String(DEFAULT_LICENSED_USERS),
  fieldSeats: String(DEFAULT_FIELD_SEATS),
}

describe('buildCreateTenantPayload', () => {
  it('defaults office and field seats to 10 and sends them as numbers', () => {
    expect(DEFAULT_LICENSED_USERS).toBe(10)
    const result = buildCreateTenantPayload(validForm)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.payload.officeSeats).toBe(10)
    expect(result.payload.fieldSeats).toBe(10)
    expect(typeof result.payload.officeSeats).toBe('number')
    expect(result.payload.companyName).toBe('Acme Services')
  })

  it('allows zero field seats and rejects negative or fractional ones', () => {
    const zero = buildCreateTenantPayload({ ...validForm, fieldSeats: '0' })
    expect(zero.ok && zero.payload.fieldSeats).toBe(0)
    expect(buildCreateTenantPayload({ ...validForm, fieldSeats: '-1' })).toEqual({ ok: false, error: FIELD_SEATS_INVALID })
    expect(buildCreateTenantPayload({ ...validForm, fieldSeats: '1.5' })).toEqual({ ok: false, error: FIELD_SEATS_INVALID })
  })

  it('rejects a missing office seat count with a clear message', () => {
    expect(buildCreateTenantPayload({ ...validForm, maxUsers: '' })).toEqual({ ok: false, error: LICENSED_USERS_REQUIRED })
    expect(buildCreateTenantPayload({ ...validForm, maxUsers: '   ' })).toEqual({ ok: false, error: LICENSED_USERS_REQUIRED })
  })

  it.each(['0', '-1', '-10', '2.5', 'abc'])('rejects %s', (value) => {
    expect(buildCreateTenantPayload({ ...validForm, maxUsers: value })).toEqual({ ok: false, error: LICENSED_USERS_INVALID })
  })
})

describe('parseLicensedUsers', () => {
  it('accepts whole numbers of at least 1', () => {
    expect(parseLicensedUsers('1')).toEqual({ ok: true, value: 1 })
    expect(parseLicensedUsers(' 25 ')).toEqual({ ok: true, value: 25 })
  })
})

describe('summarizeSeats', () => {
  it('reports licensed, current and remaining seats', () => {
    expect(summarizeSeats(4, 10)).toEqual({ licensed: 10, current: 4, remaining: 6, isFull: false, isOverLimit: false })
  })

  it('is full at the limit', () => {
    expect(summarizeSeats(10, 10)).toMatchObject({ remaining: 0, isFull: true, isOverLimit: false })
  })

  it('never reports negative remaining seats when a limit was lowered below usage', () => {
    expect(summarizeSeats(12, 10)).toMatchObject({ remaining: 0, isFull: true, isOverLimit: true })
  })

  it('reports no remaining seats figure before a license exists', () => {
    expect(summarizeSeats(3, null)).toMatchObject({ licensed: null, remaining: null, isFull: false })
  })
})
