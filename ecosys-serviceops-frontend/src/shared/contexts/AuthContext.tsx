import type { ReactNode } from 'react'
import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { endpoints } from '../api/endpoints'
import { api } from '../api/apiClient'
import { normalizeError } from '../api/apiError'
import { clearOfflineCache } from '../../features/tech/offline/persist'
import {
  clearImpersonationSession,
  clearSession,
  getImpersonationSession,
  getSession,
  setAuthToken,
  setImpersonationSession,
  setLogoutHandler,
  setSession,
  watchOtherTabs,
} from '../api/sessionStore'
import { isDemoTenant } from '../demoWorkspace'
import { setTenantTimeZone } from '../utils/dates'
import { normalizeRole } from '../utils/normalizeRole'
import type { AuthSession, LoginRequest, LoginResponse, MeResponse } from '../types/auth'

export interface AuthContextValue {
  user: AuthSession['user'] | null
  tenant: AuthSession['tenant'] | null
  branches: AuthSession['branches']
  token: string | null
  isAuthenticated: boolean
  isReady: boolean
  isImpersonating: boolean
  impersonation: AuthSession['impersonation'] | null
  login: (credentials: LoginRequest) => Promise<AuthSession>
  logout: () => Promise<void>
  refreshSession: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

function toSession(response: LoginResponse | MeResponse): AuthSession {
  setTenantTimeZone(response.tenant?.timeZoneId, response.tenant?.country)
  return {
    token: response.token,
    user: { ...response.user, role: normalizeRole(response.user.role) },
    tenant: response.tenant,
    branches: 'branches' in response ? response.branches : [],
    ...('impersonation' in response && response.impersonation ? { impersonation: response.impersonation } : {}),
  }
}

/**
 * /api/auth/me is a read-only "who am I" call — it never carries a token, and its
 * impersonation payload omits startedAt (only the backend's original impersonate
 * response has that). Rebuilding the session from a /me response must carry both
 * forward from whatever was already stored, or a routine background refresh would
 * silently blank the auth token and the impersonation start time.
 */
function mergeMeResponse(stored: AuthSession, me: MeResponse): AuthSession {
  const next = toSession(me)
  return {
    ...next,
    token: stored.token,
    ...(next.impersonation
      ? { impersonation: { ...next.impersonation, startedAt: stored.impersonation?.startedAt ?? next.impersonation.startedAt } }
      : {}),
  }
}

export interface AuthProviderProps {
  children: ReactNode
}

/**
 * Signing out forgets the cached data, including the copy kept on the phone for offline use.
 * Queued technician actions are kept: they belong to that user and sync when they sign back in.
 */
function forgetOfflineData(queryClient: QueryClient) {
  queryClient.clear()
  void clearOfflineCache()
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [session, setStateSession] = useState<AuthSession | null>(null)
  const [isReady, setIsReady] = useState(false)
  const queryClient = useQueryClient()

  useEffect(() => {
    setLogoutHandler(() => {
      setStateSession(null)
      forgetOfflineData(queryClient)
      if (getImpersonationSession()) {
        clearImpersonationSession()
      } else {
        clearSession()
        setAuthToken(null)
      }
    })

    return () => setLogoutHandler(null)
  }, [queryClient])

  // Another tab signing in as someone else (or out) replaces the session every tab shares. Follow it rather than
  // carry on as the old user, whose requests would now fail as unauthorized.
  const demoSession = isDemoTenant(session?.tenant?.tenantId)
  useEffect(() => watchOtherTabs((state) => {
    setStateSession(null)
    if (state === 'signedOut' && demoSession) {
      window.location.assign('/demo/signed-out')
    } else if (state === 'signedIn') {
      window.location.assign('/')
    } else {
      window.location.reload()
    }
  }), [demoSession])

  useEffect(() => {
    let mounted = true
    const bootstrap = async () => {
      // A freshly opened impersonation tab has already promoted its relayed session
      // into this tab's sessionStorage via ImpersonationEntryPage before this ever
      // runs, so a plain sessionStorage read is all that's needed here.
      const impersonating = getImpersonationSession()
      const stored = impersonating ?? getSession()
      if (!stored) {
        setIsReady(true)
        return
      }

      try {
        const me = await api.get<MeResponse>(endpoints.auth.me)
        if (!mounted) {
          return
        }
        const nextSession = mergeMeResponse(stored, me)
        setStateSession(nextSession)
        if (impersonating || nextSession.impersonation) {
          setImpersonationSession(nextSession)
        } else {
          setSession(nextSession)
        }
      } catch (error) {
        if (!mounted) {
          return
        }
        // No signal (a technician opening the app in the field): carry on with the stored session
        // rather than signing them out; it is checked again on the next request that gets through.
        if (normalizeError(error).isNetworkError) {
          setStateSession(stored)
          return
        }
        if (impersonating) {
          clearImpersonationSession()
        } else {
          clearSession()
        }
        setStateSession(null)
      } finally {
        if (mounted) {
          setIsReady(true)
        }
      }
    }

    void bootstrap()

    return () => {
      mounted = false
    }
  }, [])

  const login = useCallback(async (credentials: LoginRequest) => {
    const response = await api.post<LoginResponse>(endpoints.auth.login, credentials)
    const nextSession = toSession(response)
    setAuthToken(nextSession.token)
    setSession(nextSession)
    setStateSession(nextSession)
    setIsReady(true)
    return nextSession
  }, [])

  const logout = useCallback(async () => {
    const wasImpersonating = Boolean(session?.impersonation)
    const wasDemo = isDemoTenant(session?.tenant?.tenantId)
    try {
      await api.post<void>(endpoints.auth.logout, {})
    } catch {
      // Interceptors already surfaced the error, and we still clear local session.
    } finally {
      if (wasImpersonating) {
        clearImpersonationSession()
      } else {
        clearSession()
        setAuthToken(null)
      }
      setStateSession(null)
      forgetOfflineData(queryClient)
      if (typeof window !== 'undefined') {
        // A demo role opened from the platform goes back there to pick another; a demo login signed in with the
        // password (shown only once) is told how to get back in, rather than dropped at a bare sign-in form.
        if (wasDemo) {
          window.location.assign(wasImpersonating ? '/platform/demo' : '/demo/signed-out')
        } else if (wasImpersonating) {
          window.location.assign('/login')
        }
      }
    }
  }, [queryClient, session])

  const refreshSession = useCallback(async () => {
    const me = await api.get<MeResponse>(endpoints.auth.me)
    const nextSession = session ? mergeMeResponse(session, me) : toSession(me)
    if (session?.impersonation || nextSession.impersonation) {
      setImpersonationSession(nextSession)
    } else {
      setSession(nextSession)
    }
    setStateSession(nextSession)
  }, [session])

  const value = useMemo<AuthContextValue>(() => ({
    user: session?.user ?? null,
    tenant: session?.tenant ?? null,
    branches: session?.branches ?? [],
    token: session?.token ?? null,
    isAuthenticated: Boolean(session),
    isReady,
    isImpersonating: Boolean(session?.impersonation),
    impersonation: session?.impersonation ?? null,
    login,
    logout,
    refreshSession,
  }), [isReady, login, logout, refreshSession, session])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuthContext() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuthContext must be used within AuthProvider')
  }
  return context
}
