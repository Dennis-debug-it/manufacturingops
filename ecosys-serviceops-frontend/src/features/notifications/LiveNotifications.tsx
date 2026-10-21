import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { apiBaseUrl } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { getAuthToken } from '../../shared/api/sessionStore'
import { useAuth } from '../../shared/hooks/useAuth'
import { notificationKeys } from './api'
import type { UnreadCount } from './types'

// SignalR hands handlers whatever the hub sent; each subscriber types its own arguments.
type LiveHandler = (...args: never[]) => void
const asHubHandler = (handler: LiveHandler) => handler as unknown as (...args: unknown[]) => void

interface LiveNotificationsValue {
  /** True while the SignalR connection is up; the bell polls the unread count while it is not. */
  live: boolean
  /** Whether this session has a bell at all (tenant staff, not platform staff). */
  enabled: boolean
  /**
   * Listens for another hub event on the same connection (e.g. the dashboard's `DashboardChanged`),
   * across reconnects. Returns the unsubscribe function, so it fits straight into a `useEffect`.
   */
  subscribe: (event: string, handler: LiveHandler) => () => void
}

const LiveNotificationsContext = createContext<LiveNotificationsValue>({ live: false, enabled: false, subscribe: () => () => undefined })

/** Until the first connection, and between reconnect attempts that the client gave up on. */
const START_RETRY_MS = 30_000

/**
 * One SignalR connection per tab (R1). The hub puts the connection in the user's own group and
 * their tenant's group from the token, so it only ever hears about this user's notifications and
 * this tenant's changes. Events refresh React Query rather than carrying state of their own: the
 * API stays the source of truth. Other features listen through `subscribe` instead of connecting again.
 */
export function LiveNotificationsProvider({ children }: { children: ReactNode }) {
  const { user, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const [live, setLive] = useState(false)
  const connectionRef = useRef<HubConnection | null>(null)
  const listenersRef = useRef(new Set<{ event: string; handler: LiveHandler }>())
  const userId = isAuthenticated && user && user.role !== 'super_admin' && user.role !== 'platform_sales' ? user.userId : null

  useEffect(() => {
    if (!userId) return undefined

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}${endpoints.hubs.serviceOps}`, {
        accessTokenFactory: () => getAuthToken() ?? '',
        withCredentials: false,
      })
      // Keep trying with backoff (capped at 30 s); polling covers the gap meanwhile.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: ({ previousRetryCount }) => Math.min(30_000, 1_000 * 2 ** previousRetryCount) })
      .configureLogging(LogLevel.None)
      .build()
    connectionRef.current = connection
    for (const { event, handler } of listenersRef.current) connection.on(event, asHubHandler(handler))

    const refreshLists = () => void queryClient.invalidateQueries({ queryKey: notificationKeys.lists })
    connection.on('NotificationCreated', refreshLists)
    connection.on('UnreadCountChanged', (count: number) => {
      queryClient.setQueryData<UnreadCount>(notificationKeys.unreadCount, { count })
      // Read or archived in another tab: the open panel should follow.
      refreshLists()
    })
    connection.onreconnecting(() => setLive(false))
    connection.onreconnected(() => {
      setLive(true)
      // Anything sent while disconnected was missed; fetch it.
      void queryClient.invalidateQueries({ queryKey: notificationKeys.all })
    })

    let stopped = false
    let retry: ReturnType<typeof setTimeout> | undefined
    // The automatic reconnect only covers a connection that was once up; a failed start retries here.
    const start = async () => {
      try {
        await connection.start()
        if (!stopped) setLive(true)
      } catch {
        if (!stopped) retry = setTimeout(() => void start(), START_RETRY_MS)
      }
    }
    connection.onclose(() => {
      setLive(false)
      if (!stopped) retry = setTimeout(() => void start(), START_RETRY_MS)
    })
    void start()

    return () => {
      stopped = true
      clearTimeout(retry)
      setLive(false)
      connectionRef.current = null
      void connection.stop()
    }
  }, [queryClient, userId])

  const subscribe = useCallback((event: string, handler: LiveHandler) => {
    const listener = { event, handler }
    listenersRef.current.add(listener)
    connectionRef.current?.on(event, asHubHandler(handler))
    return () => {
      listenersRef.current.delete(listener)
      connectionRef.current?.off(event, asHubHandler(handler))
    }
  }, [])

  const value = useMemo(() => ({ live, enabled: Boolean(userId), subscribe }), [live, userId, subscribe])
  return <LiveNotificationsContext.Provider value={value}>{children}</LiveNotificationsContext.Provider>
}

export function useLiveNotifications() {
  return useContext(LiveNotificationsContext)
}
