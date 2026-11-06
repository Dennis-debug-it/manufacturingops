import { describe, expect, it } from 'vitest'
import type { PersistedClient } from '@tanstack/react-query-persist-client'
import { isOfflineQueryKey, keepLastGoodData } from './persist'

describe('offline cache', () => {
  it('stores a query whose refresh failed as its last good data', () => {
    const client = {
      buster: 'u1',
      timestamp: 1,
      clientState: {
        mutations: [],
        queries: [
          { queryKey: ['work-orders', 'tech-jobs'], queryHash: 'a', state: { status: 'error', data: [{ id: 'wo-1' }], error: new Error('Network Error'), fetchStatus: 'fetching', dataUpdatedAt: 5 } },
          { queryKey: ['technician', 'me'], queryHash: 'b', state: { status: 'success', data: { technicianId: 't1' }, error: null, fetchStatus: 'idle', dataUpdatedAt: 5 } },
        ],
      },
    } as unknown as PersistedClient

    const [jobs, me] = keepLastGoodData(client).clientState.queries
    expect(jobs!.state).toMatchObject({ status: 'success', error: null, fetchStatus: 'idle', data: [{ id: 'wo-1' }], dataUpdatedAt: 5 })
    expect(me).toBe(client.clientState.queries[1])
    // The original is left alone.
    expect(client.clientState.queries[0]!.state.status).toBe('error')
  })

  it('keeps only the technician app queries', () => {
    expect(isOfflineQueryKey(['work-orders', 'tech-jobs'])).toBe(true)
    expect(isOfflineQueryKey(['work-orders', 'wo-1', 'tech'])).toBe(true)
    expect(isOfflineQueryKey(['work-orders', 'list'])).toBe(false)
    expect(isOfflineQueryKey(['attendance', 'me', 'status'])).toBe(true)
    expect(isOfflineQueryKey(['branches'])).toBe(false)
    // A site supervisor's sites, rosters and muster days, so the roll can be marked offline.
  })
})
