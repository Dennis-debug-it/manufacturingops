import { openDB } from 'idb'
import type { Query, QueryClient, QueryKey } from '@tanstack/react-query'
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import { persistQueryClient, persistQueryClientSave, type PersistedClient, type Persister } from '@tanstack/react-query-persist-client'

/**
 * Keeps what a technician needs in the field on the phone (their jobs, each job's details,
 * checklist and work so far, the previous visit on follow-ups, their clock status, the parts
 * list and, for site supervisors, their sites' rosters and muster days) so the app opens and shows it with no signal. Only these queries are stored, only for the
 * signed-in user, and it is wiped on sign-out.
 */

const DB_NAME = 'ecosys-tech-cache'
const STORE = 'kv'
const KEY = 'query-cache'
/** Stored data older than this is not restored. */
export const OFFLINE_MAX_AGE = 3 * 24 * 60 * 60 * 1000

const dbPromise = () => openDB(DB_NAME, 1, { upgrade: (database) => { database.createObjectStore(STORE) } })

const storagePersister = createAsyncStoragePersister({
  key: KEY,
  throttleTime: 1000,
  storage: {
    getItem: async (key) => ((await (await dbPromise()).get(STORE, key)) as string | undefined) ?? null,
    setItem: async (key, value) => { await (await dbPromise()).put(STORE, value, key) },
    removeItem: async (key) => { await (await dbPromise()).delete(STORE, key) },
  },
})

/**
 * A refresh that fails when the signal drops leaves a query in error with its last data still
 * shown. Store that data as the last good copy (without the error, which isn't storable) so the
 * jobs are still there if the app is closed and reopened offline.
 */
export function keepLastGoodData(client: PersistedClient): PersistedClient {
  return {
    ...client,
    clientState: {
      ...client.clientState,
      queries: client.clientState.queries.map((query) => query.state.status === 'error'
        ? { ...query, state: { ...query.state, status: 'success', error: null, fetchStatus: 'idle' } }
        : query),
    },
  }
}

const persister: Persister = {
  persistClient: (client) => storagePersister.persistClient(keepLastGoodData(client)),
  restoreClient: () => storagePersister.restoreClient(),
  removeClient: () => storagePersister.removeClient(),
}

/** Queries stored on the phone: the technician's, once they have data (even if a later refresh failed). */
function shouldStoreQuery(query: Query) {
  return query.state.data !== undefined && isOfflineQueryKey(query.queryKey)
}

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
  // A supervisor's sites, each site's roster and the muster days they opened, so the roll can be marked offline.
  if (scope === 'projects') return second === 'my-sites' || third === 'workers' || third === 'muster'
  // The asset register copy that lets a tag be scanned with no signal, and the factory tree.
  // Plus the shift's running checks and each check's questions, and the technician's factory jobs.
  if (scope === 'factory') return ['scan-index', 'org-nodes', 'checks-due', 'check-run', 'job', 'jobs', 'failure-codes', 'windows', 'window', 'window-pack'].includes(String(second))
  return false
}

/** Starts storing (and restores) the technician's offline data; returns the stop function. */
export function startOfflinePersistence(queryClient: QueryClient, userId: string): () => void {
  const options = {
    queryClient,
    persister,
    maxAge: OFFLINE_MAX_AGE,
    // Another user's stored data is discarded rather than restored.
    buster: userId,
    dehydrateOptions: { shouldDehydrateQuery: shouldStoreQuery },
  }
  let stopped = false
  const [unsubscribe, restored] = persistQueryClient(options)
  // Saving otherwise starts with the next change after the restore; jobs that loaded while it
  // was restoring would not be stored until something else changed.
  void restored.then(() => (stopped ? undefined : persistQueryClientSave(options))).catch(() => {})
  return () => {
    stopped = true
    unsubscribe()
  }
}

/** Removes the stored copy (sign-out). */
export async function clearOfflineCache() {
  await persister.removeClient()
}
