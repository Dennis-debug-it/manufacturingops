import type { CreateTenantPayload } from '../api'

/**
 * Licensed users (seats) for a tenant. The canonical API field is `maxUsers` on the tenant create/update
 * and `/license/seats` requests; the backend stores it as TenantLicense.MaxUsersOverride and reports the
 * effective value as `seatLimit`. Active workspace users (pending invitees included) consume a seat;
 * deactivated users and client-portal contacts do not.
 */

/** Starting value for Licensed Users; the ServiceOps plans carry no seat default of their own. */
export const DEFAULT_LICENSED_USERS = 10

export const LICENSED_USERS_REQUIRED = 'Licensed user limit is required.'
export const LICENSED_USERS_INVALID = 'Licensed users must be a whole number of at least 1.'

export type ParsedLicensedUsers = { ok: true; value: number } | { ok: false; error: string }

export function parseLicensedUsers(input: string): ParsedLicensedUsers {
  const trimmed = input.trim()
  if (!trimmed) return { ok: false, error: LICENSED_USERS_REQUIRED }
  const value = Number(trimmed)
  if (!Number.isInteger(value) || value < 1) return { ok: false, error: LICENSED_USERS_INVALID }
  return { ok: true, value }
}

export type SeatSummary = {
  /** Null only before the tenant has a license; every license carries an explicit limit. */
  licensed: number | null
  current: number
  /** Null when there is no license yet; never negative (usage can exceed a lowered limit). */
  remaining: number | null
  isFull: boolean
  isOverLimit: boolean
}

export function summarizeSeats(current: number, licensed: number | null | undefined): SeatSummary {
  const limit = licensed ?? null
  return {
    licensed: limit,
    current,
    remaining: limit == null ? null : Math.max(0, limit - current),
    isFull: limit != null && current >= limit,
    isOverLimit: limit != null && current > limit,
  }
}

export type CreateTenantForm = {
  companyName: string
  slug: string
  planCode: 'operations' | 'finance' | 'suite'
  adminFullName: string
  adminEmail: string
  country: string
  timeZoneId: string
  /** Licensed Users as typed; sent as `maxUsers`. */
  maxUsers: string
}

export type CreateTenantPayloadResult = { ok: true; payload: CreateTenantPayload } | { ok: false; error: string }

/** Validates the Create Tenant form and maps it to the API request. */
export function buildCreateTenantPayload(form: CreateTenantForm): CreateTenantPayloadResult {
  if (!form.companyName.trim() || !form.slug.trim()) {
    return { ok: false, error: 'Company name and workspace slug are required.' }
  }
  if (!form.adminFullName.trim() || !form.adminEmail.trim() || !form.country.trim() || !form.timeZoneId.trim()) {
    return { ok: false, error: 'Primary administrator, country and timezone are required.' }
  }
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.adminEmail.trim())) {
    return { ok: false, error: 'Enter a valid administrator email address.' }
  }
  const seats = parseLicensedUsers(form.maxUsers)
  if (!seats.ok) return seats

  return {
    ok: true,
    payload: {
      companyName: form.companyName.trim(),
      slug: form.slug.trim(),
      planCode: form.planCode,
      maxUsers: seats.value,
      adminFullName: form.adminFullName.trim(),
      adminEmail: form.adminEmail.trim(),
      country: form.country.trim(),
      timeZoneId: form.timeZoneId.trim(),
    },
  }
}
