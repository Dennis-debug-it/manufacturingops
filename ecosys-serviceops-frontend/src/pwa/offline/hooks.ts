import { useEffect, useState } from 'react'
import { getOfflineDb } from './db'

export function useLocalData<T>(store: 'work_orders' | 'work_order_details' | 'clients' | 'sites' | 'assets' | 'parts' | 'time_entries' | 'user_profile' | 'settings', key: string | IDBKeyRange) {
  const [data, setData] = useState<T | null>(null)

  useEffect(() => {
    let active = true
    void (async () => {
      const db = await getOfflineDb()
      const value = await db.get(store, key)
      if (active) {
        setData((value ?? null) as T | null)
      }
    })()
    return () => {
      active = false
    }
  }, [key, store])

  return data
}

export function useSyncQueue() {
  const [queueLength, setQueueLength] = useState(0)

  useEffect(() => {
    let active = true
    void (async () => {
      const db = await getOfflineDb()
      const entries = await db.getAll('sync_queue')
      if (active) {
        setQueueLength(entries.filter((entry) => !entry.synced && entry.status !== 'conflict').length)
      }
    })()
    return () => {
      active = false
    }
  }, [])

  return { queueLength }
}

export function useOfflineQueueMessages(onSyncRequested: () => void) {
  useEffect(() => {
    const handler = (event: MessageEvent<{ type?: string }>) => {
      if (event.data?.type === 'ECOSYS_SYNC_REQUESTED') onSyncRequested()
    }
    navigator.serviceWorker?.addEventListener('message', handler)
    return () => navigator.serviceWorker?.removeEventListener('message', handler)
  }, [onSyncRequested])
}

export function useOfflineStatus() {
  const [online, setOnline] = useState(navigator.onLine)

  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => {
      window.removeEventListener('online', update)
      window.removeEventListener('offline', update)
    }
  }, [])

  return { online, offline: !online }
}
