import { openDB, type DBSchema, type IDBPDatabase } from 'idb'

export interface SyncQueueEntry {
  queueId?: number
  id: string
  type: 'UPDATE_WO_STATUS' | 'ADD_TIMELINE_NOTE' | 'LOG_MATERIAL' | 'UPLOAD_EVIDENCE' | 'CAPTURE_SIGNATURE' | 'CLOCK_IN' | 'CLOCK_OUT' | 'COMPLETE_CHECKLIST_TASK' | 'LOG_EXPENSE'
  payload: unknown
  entityId: string
  createdAt: string
  retryCount: number
  synced: boolean
}

export interface EcosysOfflineDB extends DBSchema {
  work_orders: {
    key: string
    value: { id: string; updatedAt: string; [key: string]: unknown }
  }
  work_order_details: {
    key: string
    value: { id: string; updatedAt: string; [key: string]: unknown }
  }
  clients: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  sites: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  assets: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  parts: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  sync_queue: {
    key: number
    value: SyncQueueEntry
    autoIncrement: true
  }
  time_entries: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  user_profile: {
    key: string
    value: { id: string; [key: string]: unknown }
  }
  settings: {
    key: string
    value: { key: string; value: unknown }
  }
}

let dbPromise: Promise<IDBPDatabase<EcosysOfflineDB>> | null = null

export function getOfflineDb() {
  if (!dbPromise) {
    dbPromise = openDB<EcosysOfflineDB>('ecosys-offline', 1, {
      upgrade(database) {
        database.createObjectStore('work_orders', { keyPath: 'id' })
        database.createObjectStore('work_order_details', { keyPath: 'id' })
        database.createObjectStore('clients', { keyPath: 'id' })
        database.createObjectStore('sites', { keyPath: 'id' })
        database.createObjectStore('assets', { keyPath: 'id' })
        database.createObjectStore('parts', { keyPath: 'id' })
        database.createObjectStore('sync_queue', { keyPath: 'queueId', autoIncrement: true })
        database.createObjectStore('time_entries', { keyPath: 'id' })
        database.createObjectStore('user_profile', { keyPath: 'id' })
        database.createObjectStore('settings', { keyPath: 'key' })
      },
    })
  }
  return dbPromise
}
