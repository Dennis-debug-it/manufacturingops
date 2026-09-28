import { describe, it, expect } from 'vitest'
import {
  buildCreateTenantPayload,
  DEFAULT_LICENSED_USERS,
  LICENSED_USERS_INVALID,
  LICENSED_USERS_REQUIRED,
  parseLicensedUsers,
  summarizeSeats,
  type CreateTenantForm,
} from './licensedUsers'

const validForm: CreateTenantForm = {
  companyName: ' Acme Services ',
  slug: 'acme',
  planCode: 'suite',
  adminFullName: 'Jane Admin',
  adminEmail: 'jane@acme.test',
  country: 'Kenya',
  timeZoneId: 'Africa/Nairobi',
  maxUsers: String(DEFAULT_LICENSED_USERS),
}

describe('buildCreateTenantPayload', () => {
  it('defaults Licensed Users to 10 and sends it as maxUsers', () => {
    expect(DEFAULT_LICENSED_USERS).toBe(10)
    const result = buildCreateTenantPayload(validForm)
    expect(result.ok).toBe(true)
    if (!result.ok) return
    expect(result.payload.maxUsers).toBe(10)
    expect(typeof result.payload.maxUsers).toBe('number')
    expect(result.payload.companyName).toBe('Acme Services')
  })

  it('rejects a missing licensed-user count with a clear message', () => {
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
