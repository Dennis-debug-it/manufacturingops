import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { apiBaseUrl } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { getAuthToken } from '../../shared/api/sessionStore'
import { useAuth } from '../../shared/hooks/useAuth'
import { notificationKeys } from './api'
import type { UnreadCount } from './types'

interface LiveNotificationsValue {
  /** True while the SignalR connection is up; the bell polls the unread count while it is not. */
  live: boolean
  /** Whether this session has a bell at all (tenant staff, not platform staff). */
  enabled: boolean
}

const LiveNotificationsContext = createContext<LiveNotificationsValue>({ live: false, enabled: false })

/** Until the first connection, and between reconnect attempts that the client gave up on. */
const START_RETRY_MS = 30_000

/**
 * One SignalR connection per tab (R1). The hub puts the connection in the user's own group from
 * the token, so it only ever hears about this user's notifications. Events refresh React Query
 * rather than carrying state of their own: the API stays the source of truth.
 */
export function LiveNotificationsProvider({ children }: { children: ReactNode }) {
  const { user, isAuthenticated } = useAuth()
  const queryClient = useQueryClient()
  const [live, setLive] = useState(false)
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
      void connection.stop()
    }
  }, [queryClient, userId])

  return <LiveNotificationsContext.Provider value={{ live, enabled: Boolean(userId) }}>{children}</LiveNotificationsContext.Provider>
}

export function useLiveNotifications() {
  return useContext(LiveNotificationsContext)
}
