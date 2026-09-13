// Tiny external store (not persisted — it should only ever reflect the most recent API
// response) mirroring the X-License-Warning header the backend attaches to every tenant-scoped
// response once a license is near expiry, in its grace period, expired (read-only), or suspended.
// See TenantLicenseEnforcementMiddleware on the backend for what sets this header and why.

type Listener = () => void

let warning: string | null = null
const listeners = new Set<Listener>()

/**
 * Endpoints the backend's TenantLicenseEnforcementMiddleware never runs for (auth, platform-admin,
 * health) — their responses carry no X-License-Warning header at all, so a response from one of
 * these must never be read as "no warning" and clear a warning a prior tenant-scoped response set.
 * Kept in sync with ExemptPrefixes in LicenseAndSecurityServices.cs.
 */
const EXEMPT_PREFIXES = ['/api/auth', '/api/platform', '/api/health', '/health']

export function isLicenseWarningExemptPath(path: string | undefined): boolean {
  return !path || !path.startsWith('/api/') || EXEMPT_PREFIXES.some((prefix) => path.startsWith(prefix))
}

export function setLicenseWarning(next: string | null | undefined) {
  const normalized = next?.trim() || null
  if (normalized === warning) return
  warning = normalized
  listeners.forEach((listener) => listener())
}

export function getLicenseWarning(): string | null {
  return warning
}

export function subscribeLicenseWarning(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}
