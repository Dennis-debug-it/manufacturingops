import type { ReactNode } from 'react'
import { createContext, useContext, useEffect, useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { endpoints } from '../../shared/api/endpoints'
import { portalApi } from '../api/portalApiClient'
import {
  clearPortalSession,
  getPortalSession,
  setPortalAuthToken,
  setPortalLogoutHandler,
  setPortalSession,
} from '../api/portalSessionStore'
import type { PortalSession, PortalVerifyResponse } from '../types/portalAuth'

export interface PortalAuthContextValue {
  user: PortalSession['user'] | null
  client: PortalSession['client'] | null
  tenant: PortalSession['tenant'] | null
  isAuthenticated: boolean
  isReady: boolean
  requestLink: (email: string) => Promise<void>
  verify: (token: string) => Promise<PortalSession>
  logout: () => Promise<void>
}

const PortalAuthContext = createContext<PortalAuthContextValue | undefined>(undefined)

function toSession(response: PortalVerifyResponse, fallbackToken?: string): PortalSession {
  return {
    token: response.token || fallbackToken || '',
    user: response.user,
    client: response.client,
    tenant: response.tenant,
  }
}

export function PortalAuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setStateSession] = useState<PortalSession | null>(null)
  const [isReady, setIsReady] = useState(false)
  // Bumped whenever the signed-in contact changes (a new link is verified, or sign-out). The start-up /me
  // check was sent with whatever token was stored before; if a newer sign-in lands first, that stale answer
  // must not overwrite it — otherwise opening a new link over an old session showed the previous contact.
  const sessionVersion = useRef(0)

  useEffect(() => {
    setPortalLogoutHandler(() => {
      setStateSession(null)
      clearPortalSession()
      setPortalAuthToken(null)
    })
    return () => setPortalLogoutHandler(null)
  }, [])

  useEffect(() => {
    let mounted = true
    const bootstrap = async () => {
      const stored = getPortalSession()
      if (!stored) {
        setIsReady(true)
        return
      }

      const startedAt = sessionVersion.current
      try {
        const me = await portalApi.get<PortalVerifyResponse>(endpoints.portal.me)
        if (!mounted || sessionVersion.current !== startedAt) return
        // /me never returns a token — carry the existing one forward.
        const nextSession = toSession(me, stored.token)
        setStateSession(nextSession)
        setPortalSession(nextSession)
      } catch {
        if (!mounted || sessionVersion.current !== startedAt) return
        clearPortalSession()
        setStateSession(null)
      } finally {
        if (mounted) setIsReady(true)
      }
    }

    void bootstrap()
    return () => {
      mounted = false
    }
  }, [])

  const requestLink = async (email: string) => {
    await portalApi.post(endpoints.portal.requestLink, { email })
  }

  const verify = async (token: string) => {
    const response = await portalApi.post<PortalVerifyResponse>(endpoints.portal.verify, { token })
    const nextSession = toSession(response)
    sessionVersion.current += 1
    // Data cached for a previous contact must never show under the new one.
    queryClient.removeQueries({ queryKey: ['portal'] })
    setPortalAuthToken(nextSession.token)
    setPortalSession(nextSession)
    setStateSession(nextSession)
    setIsReady(true)
    return nextSession
  }

  const logout = async () => {
    try {
      await portalApi.post(endpoints.portal.logout, {})
    } catch {
      // interceptor already surfaced the error toast
    } finally {
      sessionVersion.current += 1
      queryClient.removeQueries({ queryKey: ['portal'] })
      clearPortalSession()
      setPortalAuthToken(null)
      setStateSession(null)
    }
  }

  const value = useMemo<PortalAuthContextValue>(() => ({
    user: session?.user ?? null,
    client: session?.client ?? null,
    tenant: session?.tenant ?? null,
    isAuthenticated: Boolean(session),
    isReady,
    requestLink,
    verify,
    logout,
  }), [isReady, session])

  return <PortalAuthContext.Provider value={value}>{children}</PortalAuthContext.Provider>
}

export function usePortalAuth() {
  const context = useContext(PortalAuthContext)
  if (!context) {
    throw new Error('usePortalAuth must be used within PortalAuthProvider')
  }
  return context
}
