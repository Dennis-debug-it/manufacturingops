import { getOfflineDb, type SyncQueueEntry } from './db'

export type OfflineAction = Pick<SyncQueueEntry, 'type' | 'payload' | 'entityId' | 'tenantId'> & { id?: string }
export type ReplayResult = { applied: number; failed: number; conflicts: number }

const transient = (error: unknown) => {
  const status = (error as { response?: { status?: number } })?.response?.status
  return status === undefined || status === 408 || status === 429 || status >= 500
}

export async function enqueueOfflineAction(action: OfflineAction): Promise<string> {
  const db = await getOfflineDb()
  const id = action.id ?? crypto.randomUUID()
  const now = new Date().toISOString()
  await db.add('sync_queue', {
    ...action,
    id,
    createdAt: now,
    updatedAt: now,
    retryCount: 0,
    synced: false,
    status: 'pending',
    idempotencyKey: id,
  })
  if ('serviceWorker' in navigator) {
    const registration = await navigator.serviceWorker.ready
    if ('sync' in registration) await (registration as ServiceWorkerRegistration & { sync: { register: (tag: string) => Promise<void> } }).sync.register('ecosys-sync')
  }
  return id
}

export async function replayOfflineQueue(send: (entry: SyncQueueEntry) => Promise<void>, tenantId: string): Promise<ReplayResult> {
  const db = await getOfflineDb()
  const entries = (await db.getAll('sync_queue')).filter((x) => !x.synced && x.tenantId === tenantId).sort((a, b) => (a.queueId ?? 0) - (b.queueId ?? 0))
  const result: ReplayResult = { applied: 0, failed: 0, conflicts: 0 }
  for (const entry of entries) {
    await db.put('sync_queue', { ...entry, status: 'processing', updatedAt: new Date().toISOString() })
    try {
      await send(entry)
      await db.put('sync_queue', { ...entry, status: 'pending', synced: true, updatedAt: new Date().toISOString() })
      result.applied++
    } catch (error) {
      const status = (error as { response?: { status?: number } })?.response?.status
      const conflict = status === 409 || status === 412
      const next = { ...entry, status: conflict ? 'conflict' as const : 'failed' as const, retryCount: entry.retryCount + 1, lastError: String(error), updatedAt: new Date().toISOString() }
      await db.put('sync_queue', next)
      conflict ? result.conflicts++ : result.failed++
      if (transient(error)) break
    }
  }
  return result
}

export async function clearOfflineTenantData(tenantId: string) {
  const db = await getOfflineDb()
  const tx = db.transaction(['sync_queue', 'work_orders', 'work_order_details'], 'readwrite')
  for (const entry of await tx.objectStore('sync_queue').getAll()) if (entry.tenantId === tenantId) await tx.objectStore('sync_queue').delete(entry.queueId!)
  await tx.objectStore('work_orders').clear()
  await tx.objectStore('work_order_details').clear()
  await tx.done
}
