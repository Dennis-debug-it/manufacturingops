/**
 * Pure helpers for WhatsApp reminders (Release 3 design 4). The server normalises numbers too and is
 * the authority; this mirrors it so the form can show "+254 712 345 678" before anything is sent.
 */

/**
 * A number as E.164 (+digits), or null when it can't be one. A leading 0 is a national number in the
 * workspace's country (callingCode, Kenya's 254 by default): "0712 345 678" → "+254712345678".
 */
export function normalizeWhatsAppNumber(input: string, callingCode = '254'): string | null {
  const trimmed = input.trim()
  if (!trimmed) return null
  if (/[^0-9\s\-().+]/.test(trimmed)) return null

  let digits = trimmed.replace(/\D/g, '')
  if (!digits) return null

  if (trimmed.startsWith('+')) {
    // already international
  } else if (digits.startsWith('00')) {
    digits = digits.slice(2)
  } else if (digits.startsWith('0')) {
    digits = callingCode + digits.slice(1)
  } else if (!digits.startsWith(callingCode) || digits.length <= 9) {
    digits = callingCode + digits
  }

  if (digits.length < 8 || digits.length > 15 || digits.startsWith('0')) return null
  // Kenyan mobiles: +254 then 9 digits starting 7 or 1.
  if (digits.startsWith('254') && (digits.length !== 12 || !['7', '1'].includes(digits[3] ?? ''))) return null
  return `+${digits}`
}

/** "+254712345678" → "+254 712 345 678" for display; other numbers are grouped after the country code. */
export function formatWhatsAppNumber(e164: string | null | undefined): string {
  if (!e164) return ''
  const kenya = /^\+254(\d{3})(\d{3})(\d{3})$/.exec(e164)
  if (kenya) return `+254 ${kenya[1]} ${kenya[2]} ${kenya[3]}`
  return e164
}

/** Only digits, at most six: what the code box accepts. */
export function cleanVerificationCode(input: string): string {
  return input.replace(/\D/g, '').slice(0, 6)
}

/** Whole minutes and seconds left on a code, e.g. "9:05"; "0:00" once it has expired. */
export function codeTimeLeft(expiresAt: string, now: Date = new Date()): string {
  const seconds = Math.max(0, Math.floor((new Date(expiresAt).getTime() - now.getTime()) / 1000))
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

export type WhatsAppOutboxStatus = 'Pending' | 'Sending' | 'Sent' | 'Delivered' | 'Read' | 'Failed' | 'Skipped'

/** Plain words for the outbox log. */
export function outboxStatusLabel(status: WhatsAppOutboxStatus | string): string {
  switch (status) {
    case 'Pending': return 'Waiting to send'
    case 'Sending': return 'Sending'
    case 'Sent': return 'Sent'
    case 'Delivered': return 'Delivered'
    case 'Read': return 'Read'
    case 'Failed': return 'Failed'
    case 'Skipped': return 'Not sent'
    default: return status
  }
}

export const CONSENT_METHODS = [
  { value: 'InPerson', label: 'In person' },
  { value: 'Phone', label: 'By phone' },
  { value: 'InWriting', label: 'In writing' },
] as const

export function consentMethodLabel(method: string | null | undefined): string {
  if (method === 'Portal') return 'Confirmed from the portal'
  return CONSENT_METHODS.find((m) => m.value === method)?.label ?? '—'
}

/** The allowed-key list after switching one key on or off, sorted and without duplicates. */
export function toggleAllowedKey(allowed: readonly string[], key: string, on: boolean): string[] {
  const set = new Set(allowed)
  if (on) set.add(key)
  else set.delete(key)
  return [...set].sort()
}

/** Share of the monthly cap used, 0–100, for the usage bar. */
export function usagePercent(used: number, cap: number): number {
  if (cap <= 0) return 100
  return Math.min(100, Math.round((used / cap) * 100))
}
