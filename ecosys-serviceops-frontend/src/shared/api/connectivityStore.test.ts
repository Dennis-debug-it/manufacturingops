import { describe, it, expect, vi } from 'vitest'
import { setConnectivityOffline, isConnectivityOffline, subscribeConnectivity } from './connectivityStore'

describe('connectivityStore', () => {
  it('starts online', () => {
    // Whatever a previous test left it as, an explicit reset keeps this test independent.
    setConnectivityOffline(false)
    expect(isConnectivityOffline()).toBe(false)
  })

  it('reports offline once set, and notifies subscribers exactly once', () => {
    setConnectivityOffline(false)
    const listener = vi.fn()
    const unsubscribe = subscribeConnectivity(listener)

    setConnectivityOffline(true)

    expect(isConnectivityOffline()).toBe(true)
    expect(listener).toHaveBeenCalledTimes(1)
    unsubscribe()
  })

  it('does not notify subscribers again for a repeated identical state (no redundant re-renders)', () => {
    setConnectivityOffline(true)
    const listener = vi.fn()
    const unsubscribe = subscribeConnectivity(listener)

    setConnectivityOffline(true)
    setConnectivityOffline(true)

    expect(listener).not.toHaveBeenCalled()
    unsubscribe()
  })

  it('clears back to online and notifies once', () => {
    setConnectivityOffline(true)
    const listener = vi.fn()
    const unsubscribe = subscribeConnectivity(listener)

    setConnectivityOffline(false)

    expect(isConnectivityOffline()).toBe(false)
    expect(listener).toHaveBeenCalledTimes(1)
    unsubscribe()
  })

  it('stops notifying after unsubscribe', () => {
    setConnectivityOffline(false)
    const listener = vi.fn()
    const unsubscribe = subscribeConnectivity(listener)
    unsubscribe()

    setConnectivityOffline(true)

    expect(listener).not.toHaveBeenCalled()
  })
})
