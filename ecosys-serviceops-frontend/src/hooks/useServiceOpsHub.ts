import * as signalR from '@microsoft/signalr'
import { useCallback, useEffect, useRef } from 'react'
import { getStoredAuth } from '../lib/api'

const DEFAULT_API_BASE_URL = import.meta.env.DEV
  ? 'http://localhost:5072'
  : 'https://app.ecosysdigital.co.ke'
const HUB_URL = `${(import.meta.env.VITE_API_BASE_URL || DEFAULT_API_BASE_URL).replace(/\/+$/, '')}/hubs/serviceops`

type EventCallback = (...args: unknown[]) => void

interface ServiceOpsHubHandle {
  on: (event: string, callback: EventCallback) => void
  off: (event: string) => void
}

/**
 * Connects to /hubs/serviceops with the stored JWT bearer token.
 * Calls JoinTenantGroup on connect so the client receives tenant-scoped broadcasts.
 * Auto-reconnects with exponential backoff. Cleans up on unmount.
 *
 * Usage:
 *   const hub = useServiceOpsHub()
 *   useEffect(() => {
 *     hub.on('WorkOrderUpdated', (id) => { ... })
 *     return () => hub.off('WorkOrderUpdated')
 *   }, [hub])
 */
export function useServiceOpsHub(): ServiceOpsHubHandle {
  const connectionRef = useRef<signalR.HubConnection | null>(null)
  // Keep a stable map of registered handlers so callers can swap them without re-connecting
  const handlersRef = useRef<Map<string, EventCallback>>(new Map())

  useEffect(() => {
    const auth = getStoredAuth()
    if (!auth?.token) return

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL, {
        accessTokenFactory: () => {
          // Always read the latest token from storage in case it was refreshed
          return getStoredAuth()?.token ?? auth.token
        },
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect({
        // Exponential backoff: 0 s, 2 s, 10 s, 30 s, 60 s, then every 120 s
        nextRetryDelayInMilliseconds: (ctx) => {
          const attempts = ctx.previousRetryCount
          if (attempts === 0) return 0
          if (attempts === 1) return 2_000
          if (attempts === 2) return 10_000
          if (attempts === 3) return 30_000
          if (attempts === 4) return 60_000
          return 120_000
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build()

    connectionRef.current = connection

    // Register a forwarding handler for each already-subscribed event
    handlersRef.current.forEach((cb, event) => {
      connection.on(event, cb)
    })

    async function start() {
      try {
        await connection.start()
        await connection.invoke('JoinTenantGroup')
      } catch (err) {
        // Automatic reconnect will retry; log but don't throw
        console.warn('[ServiceOpsHub] initial connect failed:', err)
      }
    }

    connection.onreconnected(async () => {
      try {
        await connection.invoke('JoinTenantGroup')
      } catch {
        // Reconnect will retry
      }
    })

    void start()

    return () => {
      // Stop without waiting — runs on unmount
      void connection.stop()
      connectionRef.current = null
    }
  }, []) // Intentionally empty: connect once per mount, stable across re-renders

  const on = useCallback((event: string, callback: EventCallback) => {
    handlersRef.current.set(event, callback)
    connectionRef.current?.on(event, callback)
  }, [])

  const off = useCallback((event: string) => {
    const cb = handlersRef.current.get(event)
    if (cb) {
      connectionRef.current?.off(event, cb)
      handlersRef.current.delete(event)
    }
  }, [])

  return { on, off }
}
