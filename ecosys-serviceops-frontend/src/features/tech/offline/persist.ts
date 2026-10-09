import { openDB } from 'idb'
import type { Query, QueryClient, QueryKey } from '@tanstack/react-query'
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import { persistQueryClient } from '@tanstack/react-query-persist-client'

/**
 * Keeps what a technician needs in the field on the phone (their jobs, each job's details,
 * checklist and work so far, the previous visit on follow-ups, their clock status and the parts
 * list) so the app opens and shows it with no signal. Only these queries are stored, only for the
 * signed-in user, and it is wiped on sign-out.
 */

const DB_NAME = 'ecosys-tech-cache'
const STORE = 'kv'
const KEY = 'query-cache'
/** Stored data older than this is not restored. */
export const OFFLINE_MAX_AGE = 3 * 24 * 60 * 60 * 1000

const dbPromise = () => openDB(DB_NAME, 1, { upgrade: (database) => { database.createObjectStore(STORE) } })

const persister = createAsyncStoragePersister({
  key: KEY,
  throttleTime: 1000,
  storage: {
    getItem: async (key) => ((await (await dbPromise()).get(STORE, key)) as string | undefined) ?? null,
    setItem: async (key, value) => { await (await dbPromise()).put(STORE, value, key) },
    removeItem: async (key) => { await (await dbPromise()).delete(STORE, key) },
  },
})

/** The technician app's queries worth having offline. */
export function isOfflineQueryKey(key: QueryKey): boolean {
  const [scope, second, third] = key as unknown[]
  if (scope === 'work-orders') {
    return second === 'tech-jobs' || third === 'tech' || third === 'execution' || third === 'follow-up-source-report' || third === 'parts-requests'
  }
  if (scope === 'technician') return second === 'me'
  // The plan's modules, which every screen checks before it opens.
  if (scope === 'tenant-license') return true
  if (scope === 'attendance') return second === 'me' || second === 'leave-types'
  // The unfiltered parts list, so parts used can still be picked offline.
  if (scope === 'picker') return second === 'materials' && third === ''
  return false
}

/** Starts storing (and restores) the technician's offline data; returns the stop function. */
export function startOfflinePersistence(queryClient: QueryClient, userId: string): () => void {
  const [unsubscribe] = persistQueryClient({
    queryClient,
    persister,
    maxAge: OFFLINE_MAX_AGE,
    // Another user's stored data is discarded rather than restored.
    buster: userId,
    dehydrateOptions: {
      shouldDehydrateQuery: (query: Query) => query.state.status === 'success' && isOfflineQueryKey(query.queryKey),
    },
  })
  return unsubscribe
}

/** Removes the stored copy (sign-out). */
export async function clearOfflineCache() {
  await persister.removeClient()
}
