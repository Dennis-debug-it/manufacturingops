import type { AuthSession } from '../types/auth'
import { setLicenseWarning } from './licenseWarningStore'

const storageKey = 'ecosys.serviceops.session'
const tokenKey = 'ecosys.serviceops.token'
const refreshTokenKey = 'ecosys.serviceops.refresh'
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
let refreshTokenValue: string | null = null
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

export function setRefreshToken(token: string | null) {
  refreshTokenValue = token
  if (!hasWindow()) {
    return
  }
  if (token) {
    window.localStorage.setItem(refreshTokenKey, token)
  } else {
    window.localStorage.removeItem(refreshTokenKey)
  }
}

export function getRefreshToken() {
  if (refreshTokenValue) {
    return refreshTokenValue
  }
  if (!hasWindow()) {
    return null
  }
  const stored = window.localStorage.getItem(refreshTokenKey)
  refreshTokenValue = stored
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
  const mine = authToken
  session = null
  authToken = null
  refreshTokenValue = null
  // A stale read-only/expiring banner from this tenant must not bleed into whoever signs in
  // next on the same tab.
  setLicenseWarning(null)
  if (!hasWindow()) {
    return
  }
  // The stored session is shared by every tab. If another tab has since signed in as someone else,
  // this tab's session is already gone from storage, and clearing it would sign that other user out.
  const stored = window.localStorage.getItem(tokenKey)
  if (mine && stored && stored !== mine && !sameUser(readStoredSession(), mine)) {
    return
  }
  window.localStorage.removeItem(storageKey)
  window.localStorage.removeItem(tokenKey)
  window.localStorage.removeItem(refreshTokenKey)
}

function readStoredSession(): AuthSession | null {
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) {
    return null
  }
  try {
    return JSON.parse(stored) as AuthSession
  } catch {
    return null
  }
}

function sameUser(stored: AuthSession | null, token: string | null) {
  return Boolean(stored && session && token && stored.user?.userId === session.user?.userId)
}

/**
 * How this tab's session relates to the one stored for all tabs:
 * - `current`: it is the stored session (or this tab is impersonating, which never uses storage);
 * - `refreshed`: another tab refreshed the same user's tokens; this tab has now adopted them;
 * - `switched`: another tab signed in as a different user, so this tab is acting on a stale identity;
 * - `signedOut`: another tab signed out.
 * Refresh tokens rotate, so a tab still holding the old pair would otherwise fail to refresh and
 * sign everyone out.
 */
export type OtherTabState = 'current' | 'refreshed' | 'switched' | 'signedOut'

export function syncWithOtherTabs(): OtherTabState {
  if (!hasWindow() || getImpersonationSession() || !authToken) {
    return 'current'
  }
  const storedToken = window.localStorage.getItem(tokenKey)
  if (storedToken === authToken) {
    return 'current'
  }
  if (!storedToken) {
    session = null
    authToken = null
    refreshTokenValue = null
    return 'signedOut'
  }
  const stored = readStoredSession()
  if (sameUser(stored, authToken)) {
    session = stored
    authToken = storedToken
    refreshTokenValue = null
    return 'refreshed'
  }
  session = null
  authToken = null
  refreshTokenValue = null
  return 'switched'
}

/**
 * Keeps this tab in step with sign-ins and sign-outs in other tabs, instead of carrying on as
 * whoever it was when it loaded. Calls back when this tab's user is no longer the signed-in one.
 */
export function watchOtherTabs(onSuperseded: (state: 'switched' | 'signedOut' | 'signedIn') => void): () => void {
  if (!hasWindow()) {
    return () => {}
  }
  const listener = (event: StorageEvent) => {
    if (event.storageArea !== window.localStorage || (event.key !== null && event.key !== tokenKey && event.key !== storageKey)) {
      return
    }
    if (!authToken && !getImpersonationSession()) {
      // A signed-out tab (e.g. sitting on the sign-in form) follows a sign-in made in another tab.
      if (window.localStorage.getItem(tokenKey)) {
        onSuperseded('signedIn')
      }
      return
    }
    const state = syncWithOtherTabs()
    if (state === 'switched' || state === 'signedOut') {
      onSuperseded(state)
    }
  }
  window.addEventListener('storage', listener)
  return () => window.removeEventListener('storage', listener)
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
  setLicenseWarning(null)
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
