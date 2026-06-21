import { useSyncExternalStore } from 'react'
import { getDatabase, initializeDatabase, subscribeDatabase } from '../services/storage'
import type { AppDatabase } from '../types/app'

initializeDatabase()

function getAppStoreSnapshot() {
  return getDatabase()
}

export function useAppStore<T>(selector: (database: AppDatabase) => T) {
  const snapshot = useSyncExternalStore(subscribeDatabase, getAppStoreSnapshot, getAppStoreSnapshot)
  return selector(snapshot)
}

export function getAppSnapshot() {
  return getAppStoreSnapshot()
}
