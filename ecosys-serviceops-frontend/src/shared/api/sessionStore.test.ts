import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import type { AuthSession } from '../types/auth'

/**
 * Every tab shares one stored session, but each tab also keeps it in memory. These pin what happens when another
 * tab signs in as someone else, signs out, or refreshes the same user's tokens: this tab must notice, and must never
 * wipe the other user's session on its way out (the "workspace unauthorized when switching users" bug).
 */
function fakeStorage() {
  const map = new Map<string, string>()
  return {
    getItem: (key: string) => map.get(key) ?? null,
    setItem: (key: string, value: string) => void map.set(key, value),
    removeItem: (key: string) => void map.delete(key),
  }
}

const listeners: Array<(event: unknown) => void> = []
let local: ReturnType<typeof fakeStorage>

function sessionFor(userId: string, token: string): AuthSession {
  return { token, user: { userId, fullName: userId } as AuthSession['user'], tenant: { tenantId: 't1' } as AuthSession['tenant'], branches: [] }
}

/** What another tab does when it signs in: the shared storage changes underneath this tab. */
function otherTabSignsIn(userId: string, token: string) {
  local.setItem('ecosys.serviceops.token', token)
  local.setItem('ecosys.serviceops.session', JSON.stringify(sessionFor(userId, token)))
  listeners.forEach((listener) => listener({ storageArea: local, key: 'ecosys.serviceops.token' }))
}

async function loadStore() {
  vi.resetModules()
  return import('./sessionStore')
}

describe('sessionStore across tabs', () => {
  beforeEach(() => {
    local = fakeStorage()
    listeners.length = 0
    vi.stubGlobal('window', {
      localStorage: local,
      sessionStorage: fakeStorage(),
      addEventListener: (_: string, listener: (event: unknown) => void) => listeners.push(listener),
      removeEventListener: vi.fn(),
    })
  })

  afterEach(() => vi.unstubAllGlobals())

  async function signedInAs(userId: string, token: string) {
    const store = await loadStore()
    store.setAuthToken(token)
    store.setRefreshToken(`refresh-${token}`)
    store.setSession(sessionFor(userId, token))
    return store
  }

  it('reports a switch when another tab signs in as someone else, and stops acting as the old user', async () => {
    const store = await signedInAs('technician', 'tech-token')
    const superseded = vi.fn()
    store.watchOtherTabs(superseded)

    otherTabSignsIn('admin', 'admin-token')

    expect(superseded).toHaveBeenCalledWith('switched')
    expect(store.getAuthToken()).toBe('admin-token')
  })

  it('never clears the other user’s session when the stale tab ends its own', async () => {
    const store = await signedInAs('technician', 'tech-token')
    local.setItem('ecosys.serviceops.token', 'admin-token')
    local.setItem('ecosys.serviceops.session', JSON.stringify(sessionFor('admin', 'admin-token')))

    store.clearSession()

    expect(local.getItem('ecosys.serviceops.token')).toBe('admin-token')
    expect(local.getItem('ecosys.serviceops.session')).not.toBeNull()
  })

  it('adopts the same user’s refreshed tokens instead of signing out', async () => {
    const store = await signedInAs('admin', 'old-token')
    const superseded = vi.fn()
    store.watchOtherTabs(superseded)
    local.setItem('ecosys.serviceops.refresh', 'rotated-refresh')

    otherTabSignsIn('admin', 'new-token')

    expect(superseded).not.toHaveBeenCalled()
    expect(store.getAuthToken()).toBe('new-token')
    expect(store.getRefreshToken()).toBe('rotated-refresh')
  })

  it('reports a sign-out from another tab', async () => {
    const store = await signedInAs('admin', 'admin-token')
    local.removeItem('ecosys.serviceops.token')

    expect(store.syncWithOtherTabs()).toBe('signedOut')
  })

  it('still clears its own session normally', async () => {
    const store = await signedInAs('admin', 'admin-token')

    store.clearSession()

    expect(local.getItem('ecosys.serviceops.token')).toBeNull()
    expect(local.getItem('ecosys.serviceops.refresh')).toBeNull()
  })
})
