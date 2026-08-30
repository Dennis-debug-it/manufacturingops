/**
 * The workspace time zone every displayed timestamp is rendered in.
 *
 * The API sends instants as UTC; the browser's own zone is irrelevant — a dispatcher opening the
 * board from another country must still see the workspace's wall clock. The active zone comes from
 * the signed-in tenant (its override, else the platform default) and is set once per session.
 */

/** Used before a session exists (login, reset-password) and if the tenant sends nothing. */
export const FALLBACK_TIME_ZONE = 'Africa/Nairobi'

let activeTimeZone = FALLBACK_TIME_ZONE

const isSupported = (timeZone: string) => {
  try {
    new Intl.DateTimeFormat('en-US', { timeZone })
    return true
  } catch {
    return false
  }
}

export function setActiveTimeZone(timeZone: string | null | undefined) {
  activeTimeZone = timeZone && isSupported(timeZone) ? timeZone : FALLBACK_TIME_ZONE
}

export function getActiveTimeZone() {
  return activeTimeZone
}

/**
 * Rebuilds a Date whose *local* fields carry the wall-clock reading in `timeZone`, so date-fns
 * (which formats using local fields) prints the workspace's time rather than the browser's.
 * The result is a display shim only — never use it for arithmetic against real instants.
 */
export function toZonedDate(value: Date, timeZone = activeTimeZone): Date {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hour12: false,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).formatToParts(value)

  const field = (type: Intl.DateTimeFormatPartTypes) => Number(parts.find((part) => part.type === type)?.value ?? '0')
  // Intl renders midnight as hour 24 in some engines.
  const hour = field('hour') % 24

  const zoned = new Date(value.getTime())
  zoned.setFullYear(field('year'), field('month') - 1, field('day'))
  zoned.setHours(hour, field('minute'), field('second'), value.getMilliseconds())
  return zoned
}

/** Short label for the active zone, e.g. "EAT" or "GMT+3", for column headers and helper text. */
export function timeZoneLabel(timeZone = activeTimeZone) {
  try {
    const parts = new Intl.DateTimeFormat('en-US', { timeZone, timeZoneName: 'short' }).formatToParts(new Date())
    return parts.find((part) => part.type === 'timeZoneName')?.value ?? timeZone
  } catch {
    return timeZone
  }
}
