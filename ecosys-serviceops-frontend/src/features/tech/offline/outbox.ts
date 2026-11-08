import { openDB, type DBSchema, type IDBPDatabase } from 'idb'
import { apiClient } from '../../../shared/api/apiClient'
import { normalizeError } from '../../../shared/api/apiError'

/**
 * The technician app's offline outbox. A job action taken without a connection (or while earlier
 * actions are still waiting) is stored here on the phone and sent, in order, once the connection is
 * back. Every entry carries its own id as the `Idempotency-Key`, so a replay the server already
 * applied isn't applied twice, and the time it happened as `X-Occurred-At`, so arrival, start,
 * completion and clock times are the field times rather than the sync time.
 */

export type OutboxKind =
  | 'respond' | 'in-transit' | 'arrive' | 'depart' | 'start' | 'hold' | 'resume' | 'complete'
  | 'notes' | 'checklist' | 'photo' | 'signature' | 'material'
  | 'clock-in' | 'clock-out' | 'clock-status'
  /** New work the technician spotted on a job, passed to sales as a lead. */
  | 'sales-lead'
  /** A step on a factory job (start, hold, complete…), a handover note, labour time or a ticked job-plan step. */
  | 'factory-job'
  /** A completed running check. */
  | 'running-check'

export interface OutboxFile {
  blob: Blob
  name: string
  fields: Record<string, string>
}

export interface OutboxEntry {
  /** Also the Idempotency-Key. */
  id: string
  userId: string
  kind: OutboxKind
  /** What the technician did, as shown in the sync list ("Arrival on WO-0042"). */
  label: string
  /** The job it belongs to; null for clock actions. */
  workOrderId: string | null
  method: 'POST' | 'PUT' | 'PATCH'
  url: string
  body?: unknown
  /** Multipart upload (photos): sent as form data with the file. */
  file?: OutboxFile
  /** Extra details the offline view needs (e.g. a material's name), never sent. */
  meta?: Record<string, unknown>
  occurredAt: string
  createdAt: string
  status: 'pending' | 'failed'
  error?: string
  attempts: number
}

export type NewOutboxAction = Omit<OutboxEntry, 'id' | 'userId' | 'occurredAt' | 'createdAt' | 'status' | 'attempts' | 'error'>

interface OutboxDB extends DBSchema {
  outbox: { key: string; value: OutboxEntry; indexes: { byUser: string } }
}

const DB_NAME = 'ecosys-tech-outbox'
let dbPromise: Promise<IDBPDatabase<OutboxDB>> | null = null

function db() {
  dbPromise ??= openDB<OutboxDB>(DB_NAME, 1, {
    upgrade(database) {
      const store = database.createObjectStore('outbox', { keyPath: 'id' })
      store.createIndex('byUser', 'userId')
    },
  })
  return dbPromise
}

// ── In-memory mirror (what the UI reads) ─────────────────────────────────────────────────────────

type SyncState = 'idle' | 'syncing'
let entries: OutboxEntry[] = []
let currentUserId: string | null = null
let syncState: SyncState = 'idle'
const listeners = new Set<() => void>()
let snapshot: { entries: OutboxEntry[]; syncState: SyncState } = { entries, syncState }

function emit() {
  snapshot = { entries, syncState }
  listeners.forEach((listener) => listener())
}

export function subscribeOutbox(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getOutboxSnapshot() {
  return snapshot
}

/** Loads the signed-in technician's queued actions (each user only ever sees and sends their own). */
export async function loadOutbox(userId: string | null) {
  currentUserId = userId
  entries = userId ? (await (await db()).getAllFromIndex('outbox', 'byUser', userId)).sort(byCreated) : []
  emit()
}

function byCreated(a: OutboxEntry, b: OutboxEntry) {
  return a.createdAt.localeCompare(b.createdAt)
}

async function put(entry: OutboxEntry) {
  await (await db()).put('outbox', entry)
  entries = [...entries.filter((x) => x.id !== entry.id), entry].sort(byCreated)
  emit()
}

export async function removeOutboxEntry(id: string) {
  await (await db()).delete('outbox', id)
  entries = entries.filter((x) => x.id !== id)
  emit()
}

/** Actions still waiting to be sent; new actions queue behind them to keep the order. */
export function hasQueuedActions() {
  return entries.some((x) => x.status === 'pending')
}

// ── Sending ──────────────────────────────────────────────────────────────────────────────────────

/**
 * Sends an entry. `fromQueue` marks a replay: it carries the time the action happened and reports
 * failures in the sync list; a direct (online) send behaves like any other request.
 */
async function send<T>(entry: OutboxEntry, fromQueue: boolean): Promise<T> {
  let data: unknown = entry.body
  if (entry.file) {
    const form = new FormData()
    form.append('file', new File([entry.file.blob], entry.file.name, { type: entry.file.blob.type }))
    Object.entries(entry.file.fields).forEach(([key, value]) => form.append(key, value))
    data = form
  }
  const response = await apiClient.request<T>({
    method: entry.method,
    url: entry.url,
    data,
    headers: fromQueue ? { 'Idempotency-Key': entry.id, 'X-Occurred-At': entry.occurredAt } : { 'Idempotency-Key': entry.id },
    // Queued actions report their own failures in the sync list rather than as stray toasts.
    suppressGlobalErrorToast: fromQueue,
  })
  return response.data
}

/** Worth retrying later: the server wasn't reached or couldn't answer, rather than refusing. */
function isTransient(error: unknown) {
  const apiError = normalizeError(error)
  const status = apiError.status ?? 0
  return apiError.isNetworkError || status === 401 || status === 408 || status === 429 || status >= 500
}

export interface PerformResult<T> {
  queued: boolean
  data?: T
}

/**
 * Does a technician action now when it can, otherwise queues it. Online with nothing waiting, it is
 * sent straight away (errors behave as before); offline, or behind earlier queued actions (which
 * must reach the server first, in order), it is queued and synced as soon as possible.
 */
export async function performAction<T>(action: NewOutboxAction, onSynced?: () => void): Promise<PerformResult<T>> {
  if (!currentUserId) {
    throw new Error('Not signed in')
  }
  const now = new Date().toISOString()
  const entry: OutboxEntry = { ...action, id: crypto.randomUUID(), userId: currentUserId, occurredAt: now, createdAt: now, status: 'pending', attempts: 0 }

  if (navigator.onLine && !hasQueuedActions()) {
    try {
      return { queued: false, data: await send<T>(entry, false) }
    } catch (error) {
      if (!normalizeError(error).isNetworkError) throw error
      // The connection dropped on the way: keep it and send it when the signal is back. If the
      // server did apply it, the replay carries the same Idempotency-Key and isn't applied twice.
    }
  }

  await put(entry)
  void syncOutbox(onSynced)
  return { queued: true }
}

/** Retries a failed entry (e.g. after the office fixed the job) by putting it back in the queue. */
export async function retryOutboxEntry(id: string, onSynced?: () => void) {
  const entry = entries.find((x) => x.id === id)
  if (!entry) return
  const { error: _previousError, ...rest } = entry
  await put({ ...rest, status: 'pending' })
  void syncOutbox(onSynced)
}

export interface SyncResult {
  sent: number
  failed: OutboxEntry[]
}

let running: Promise<SyncResult> | null = null

/**
 * Sends pending entries oldest first. Stops at the first transient failure (no signal, server
 * down, session needs refreshing) and tries again later; an entry the server refuses (job
 * reassigned, already clocked in, …) is marked failed with the reason and the rest carry on.
 */
export function syncOutbox(onSynced?: () => void): Promise<SyncResult> {
  if (running) return running
  running = (async () => {
    const result: SyncResult = { sent: 0, failed: [] }
    if (!navigator.onLine || !entries.some((x) => x.status === 'pending')) return result
    syncState = 'syncing'
    emit()
    try {
      for (const entry of entries.filter((x) => x.status === 'pending')) {
        try {
          await send(entry, true)
          await removeOutboxEntry(entry.id)
          result.sent++
        } catch (error) {
          if (isTransient(error)) break
          const failed: OutboxEntry = { ...entry, status: 'failed', attempts: entry.attempts + 1, error: normalizeError(error).message }
          await put(failed)
          result.failed.push(failed)
        }
      }
    } finally {
      syncState = 'idle'
      emit()
      if (result.sent > 0 || result.failed.length > 0) onSynced?.()
    }
    return result
  })().finally(() => {
    running = null
  })
  return running
}
