import { createSeedDatabase } from '../mock/seed'
import type { AppDatabase } from '../types/app'

const DB_KEY = 'ecosys-serviceops-db'
const DB_EVENT = 'ecosys-serviceops-db-change'
const listeners = new Set<() => void>()
let memoryDatabase: AppDatabase | null = null

function createFreshDatabase() {
  return createSeedDatabase()
}

function isAppDatabase(value: unknown): value is AppDatabase {
  if (!value || typeof value !== 'object') {
    return false
  }

  const database = value as Partial<AppDatabase>
  return (
    typeof database.version === 'number' &&
    typeof database.initializedAt === 'string' &&
    Array.isArray(database.tenants) &&
    Boolean(database.tenantData && typeof database.tenantData === 'object') &&
    Array.isArray(database.authAccounts) &&
    Array.isArray(database.sessions) &&
    Array.isArray(database.platformFeatureFlags) &&
    Array.isArray(database.systemHealth) &&
    Array.isArray(database.platformAuditLog)
  )
}

function persistDatabase(database: AppDatabase) {
  memoryDatabase = database

  if (typeof window === 'undefined') {
    return
  }

  try {
    window.localStorage.setItem(DB_KEY, JSON.stringify(database))
  } catch (error) {
    console.warn('Unable to persist Ecosys ServiceOps demo data to localStorage.', error)
  }
}

function readStoredDatabase() {
  if (memoryDatabase) {
    return memoryDatabase
  }

  if (typeof window === 'undefined') {
    return memoryDatabase
  }

  try {
    const stored = window.localStorage.getItem(DB_KEY)
    if (!stored) {
      return null
    }

    const parsed = JSON.parse(stored) as unknown
    if (!isAppDatabase(parsed)) {
      throw new Error('Stored database payload does not match the expected shape.')
    }

    memoryDatabase = parsed
    return parsed
  } catch (error) {
    console.warn('Stored Ecosys ServiceOps demo data was invalid. Resetting local workspace data.', error)
    return null
  }
}

function notify() {
  listeners.forEach((listener) => listener())
}

if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === DB_KEY) {
      memoryDatabase = null
      notify()
    }
  })

  window.addEventListener(DB_EVENT, notify)
}

export function initializeDatabase() {
  const database = readStoredDatabase()
  if (database) {
    return
  }

  persistDatabase(createFreshDatabase())
}

export function getDatabase(): AppDatabase {
  const database = readStoredDatabase()
  if (database) {
    return database
  }

  if (memoryDatabase) {
    return memoryDatabase
  }

  const freshDatabase = createFreshDatabase()
  persistDatabase(freshDatabase)
  return freshDatabase
}

export function setDatabase(next: AppDatabase) {
  persistDatabase(next)

  if (typeof window !== 'undefined') {
    window.dispatchEvent(new Event(DB_EVENT))
  } else {
    notify()
  }
}

export function updateDatabase(mutator: (database: AppDatabase) => AppDatabase) {
  const current = JSON.parse(JSON.stringify(getDatabase())) as AppDatabase
  const next = mutator(current)
  setDatabase(next)
  return next
}

export function subscribeDatabase(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function resetDatabase() {
  setDatabase(createSeedDatabase())
}
