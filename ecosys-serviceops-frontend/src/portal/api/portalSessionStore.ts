import type { PortalSession } from '../types/portalAuth'

// Entirely separate storage keys from the staff/platform session (ecosys.serviceops.*)
// and from impersonation (sessionStorage) — a client contact and a staff member can be
// logged into the same browser at once without colliding.
const storageKey = 'ecosys.portal.session'
const tokenKey = 'ecosys.portal.token'

let session: PortalSession | null = null
let authToken: string | null = null
let logoutHandler: (() => void) | null = null

const hasWindow = () => typeof window !== 'undefined'

export function setPortalAuthToken(token: string | null) {
  authToken = token
  if (!hasWindow()) return
  if (token) {
    window.localStorage.setItem(tokenKey, token)
  } else {
    window.localStorage.removeItem(tokenKey)
  }
}

export function getPortalAuthToken() {
  if (authToken) return authToken
  if (!hasWindow()) return null
  const stored = window.localStorage.getItem(tokenKey)
  authToken = stored
  return stored
}

export function setPortalSession(nextSession: PortalSession | null) {
  session = nextSession
  if (!hasWindow()) return
  if (nextSession) {
    window.localStorage.setItem(storageKey, JSON.stringify(nextSession))
  } else {
    window.localStorage.removeItem(storageKey)
  }
}

export function getPortalSession(): PortalSession | null {
  if (session) return session
  if (!hasWindow()) return null
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) return null
  try {
    session = JSON.parse(stored) as PortalSession
    authToken = session.token
    return session
  } catch {
    window.localStorage.removeItem(storageKey)
    return null
  }
}

export function clearPortalSession() {
  session = null
  authToken = null
  if (!hasWindow()) return
  window.localStorage.removeItem(storageKey)
  window.localStorage.removeItem(tokenKey)
}

export function setPortalLogoutHandler(handler: (() => void) | null) {
  logoutHandler = handler
}

export function triggerPortalLogout() {
  logoutHandler?.()
}
