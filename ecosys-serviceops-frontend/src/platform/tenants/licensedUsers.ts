import type { CreateTenantPayload } from '../api'

/**
 * Seats for a tenant: `officeSeats` and `fieldSeats` on the tenant create/update and `/license/seats`
 * requests, stored on TenantLicense and reported back with `seatLimit` (their total). Technicians take a
 * field seat, Viewers are free, every other active workspace user (pending invitees included) takes an
 * office seat; deactivated users and client-portal contacts take none.
 */

/** Starting value for office seats; editions carry only a minimum of their own. */
export const DEFAULT_LICENSED_USERS = 10
export const DEFAULT_FIELD_SEATS = 10

export const LICENSED_USERS_REQUIRED = 'Office seats are required.'
export const LICENSED_USERS_INVALID = 'Office seats must be a whole number of at least 1.'
export const FIELD_SEATS_INVALID = 'Field seats must be a whole number of 0 or more.'

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

export type Edition = 'essentials' | 'professional' | 'enterprise'

export function editionLabel(code: string | null | undefined): string {
  return code === 'essentials' ? 'Essentials' : code === 'professional' ? 'Professional' : code === 'enterprise' ? 'Enterprise' : (code ?? '—')
}

export type CreateTenantForm = {
  companyName: string
  slug: string
  planCode: Edition
  adminFullName: string
  adminEmail: string
  country: string
  timeZoneId: string
  /** Office seats as typed; sent as `officeSeats`. */
  maxUsers: string
  /** Field (technician) seats as typed; sent as `fieldSeats`. */
  fieldSeats: string
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
  const fieldSeats = Number(form.fieldSeats.trim() || '0')
  if (!Number.isInteger(fieldSeats) || fieldSeats < 0) return { ok: false, error: FIELD_SEATS_INVALID }

  return {
    ok: true,
    payload: {
      companyName: form.companyName.trim(),
      slug: form.slug.trim(),
      planCode: form.planCode,
      officeSeats: seats.value,
      fieldSeats,
      adminFullName: form.adminFullName.trim(),
      adminEmail: form.adminEmail.trim(),
      country: form.country.trim(),
      timeZoneId: form.timeZoneId.trim(),
    },
  }
}
