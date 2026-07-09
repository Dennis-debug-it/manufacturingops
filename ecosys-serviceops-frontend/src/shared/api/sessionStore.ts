import type { AuthSession } from '../types/auth'

const storageKey = 'ecosys.serviceops.session'
const tokenKey = 'ecosys.serviceops.token'
// Tab-scoped (sessionStorage, never shared across tabs) so an impersonation session
// never overwrites the platform admin's own session in localStorage.
const impersonationKey = 'ecosys.serviceops.impersonation'
// Short-lived, single-use relay for handing an impersonation session to a brand-new
// tab. localStorage (not sessionStorage) is used deliberately here — it's the only
// storage guaranteed to be synchronously visible to a tab opened via window.open,
// regardless of browser-specific session-storage-cloning behavior. Each entry is
// consumed (read once, then deleted) within milliseconds of the new tab loading.
const impersonationRelayPrefix = 'ecosys.serviceops.impersonation.relay.'

let session: AuthSession | null = null
let authToken: string | null = null
let logoutHandler: (() => void) | null = null

const hasWindow = () => typeof window !== 'undefined'

export function setAuthToken(token: string | null) {
  authToken = token
  if (!hasWindow()) {
    return
  }
  if (token) {
    window.localStorage.setItem(tokenKey, token)
  } else {
    window.localStorage.removeItem(tokenKey)
  }
}

export function getAuthToken() {
  const impersonating = getImpersonationSession()
  if (impersonating) {
    return impersonating.token
  }
  if (authToken) {
    return authToken
  }
  if (!hasWindow()) {
    return null
  }
  const stored = window.localStorage.getItem(tokenKey)
  authToken = stored
  return stored
}

export function setSession(nextSession: AuthSession | null) {
  session = nextSession
  if (!hasWindow()) {
    return
  }
  if (nextSession) {
    window.localStorage.setItem(storageKey, JSON.stringify(nextSession))
  } else {
    window.localStorage.removeItem(storageKey)
  }
}

export function getSession(): AuthSession | null {
  if (session) {
    return session
  }
  if (!hasWindow()) {
    return null
  }
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) {
    return null
  }
  try {
    session = JSON.parse(stored) as AuthSession
    authToken = session.token
    return session
  } catch {
    window.localStorage.removeItem(storageKey)
    return null
  }
}

export function clearSession() {
  session = null
  authToken = null
  if (!hasWindow()) {
    return
  }
  window.localStorage.removeItem(storageKey)
  window.localStorage.removeItem(tokenKey)
}

export function setLogoutHandler(handler: (() => void) | null) {
  logoutHandler = handler
}

export function triggerLogout() {
  logoutHandler?.()
}

// Impersonation sessions live in sessionStorage only — scoped to a single tab, never
// written to localStorage, so starting one can never clobber the platform admin's own
// (or any other tab's) session.
export function setImpersonationSession(nextSession: AuthSession | null) {
  if (!hasWindow()) {
    return
  }
  if (nextSession) {
    window.sessionStorage.setItem(impersonationKey, JSON.stringify(nextSession))
  } else {
    window.sessionStorage.removeItem(impersonationKey)
  }
}

export function getImpersonationSession(): AuthSession | null {
  if (!hasWindow()) {
    return null
  }
  const stored = window.sessionStorage.getItem(impersonationKey)
  if (!stored) {
    return null
  }
  try {
    return JSON.parse(stored) as AuthSession
  } catch {
    window.sessionStorage.removeItem(impersonationKey)
    return null
  }
}

export function clearImpersonationSession() {
  if (!hasWindow()) {
    return
  }
  window.sessionStorage.removeItem(impersonationKey)
}

/**
 * Hand off an impersonation session to a brand-new tab. Only an opaque, single-use
 * relay ID is put in the new tab's URL (as a fragment, so it's never sent to the
 * server or logged in a Referer header) — the actual token/session lives in a
 * localStorage entry the new tab reads once and immediately deletes on
 * /impersonate-entry (see ImpersonationEntryPage). The token itself never appears
 * in the URL.
 */
export function openImpersonationTab(nextSession: AuthSession) {
  if (!hasWindow()) {
    return
  }
  const relayId = window.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`
  window.localStorage.setItem(impersonationRelayPrefix + relayId, JSON.stringify(nextSession))
  window.open(`/impersonate-entry#${relayId}`, '_blank')
}

export function consumeImpersonationRelay(relayId: string): AuthSession | null {
  if (!hasWindow()) {
    return null
  }
  const key = impersonationRelayPrefix + relayId
  const stored = window.localStorage.getItem(key)
  window.localStorage.removeItem(key)
  if (!stored) {
    return null
  }
  try {
    return JSON.parse(stored) as AuthSession
  } catch {
    return null
  }
}
