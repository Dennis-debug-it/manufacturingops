import { useCallback, useEffect, useMemo, useSyncExternalStore } from 'react'
import { useQueryClient, type QueryClient } from '@tanstack/react-query'
import { useAuth } from '../../../shared/hooks/useAuth'
import { useExecutionBundle } from '../../work-orders/api'
import type { WorkOrder } from '../../work-orders/types'
import { useMyAttendanceStatus } from '../../team/timeAttendance/api'
import { useTechJob, useTechJobs } from '../api'
import { getOutboxSnapshot, loadOutbox, subscribeOutbox, syncOutbox } from './outbox'
import { applyToAttendance, applyToBundle, applyToJob } from './overlay'

export function useOutbox() {
  return useSyncExternalStore(subscribeOutbox, getOutboxSnapshot, getOutboxSnapshot)
}

/** Queued actions not yet sent (failed ones are shown in the sync list, not applied). */
export function usePendingActions() {
  const { entries } = useOutbox()
  return useMemo(() => entries.filter((entry) => entry.status === 'pending'), [entries])
}

/** Refreshes everything a synced action could have changed. */
export function refreshAfterSync(queryClient: QueryClient) {
  void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  void queryClient.invalidateQueries({ queryKey: ['attendance'] })
  void queryClient.invalidateQueries({ queryKey: ['picker', 'materials'] })
  void queryClient.invalidateQueries({ queryKey: ['visitors'] })
  void queryClient.invalidateQueries({ queryKey: ['security'] })
}

export function useTechJobsView() {
  const query = useTechJobs()
  const pending = usePendingActions()
  const data = useMemo(() => query.data?.map((job) => applyToJob(job, pending)), [query.data, pending])
  return { ...query, data }
}

export function useTechJobView(id: string | undefined) {
  const query = useTechJob(id)
  const pending = usePendingActions()
  const data = useMemo(() => (query.data ? applyToJob(query.data, pending) : undefined), [query.data, pending])
  return { ...query, data }
}

export function useTechBundle(id: string) {
  const query = useExecutionBundle(id)
  const pending = usePendingActions()
  const { user } = useAuth()
  const data = useMemo(() => (query.data ? applyToBundle(query.data, id, pending, user?.fullName ?? null) : undefined), [query.data, id, pending, user?.fullName])
  return { ...query, data }
}

export function useMyAttendanceView() {
  const query = useMyAttendanceStatus()
  const pending = usePendingActions()
  const data = useMemo(() => (query.data ? applyToAttendance(query.data, pending) : undefined), [query.data, pending])
  return { ...query, data }
}

export function useJobHasQueuedActions(job: Pick<WorkOrder, 'id'> | undefined) {
  const pending = usePendingActions()
  return Boolean(job && pending.some((entry) => entry.workOrderId === job.id))
}

export function useSyncNow() {
  const queryClient = useQueryClient()
  return useCallback(() => syncOutbox(() => refreshAfterSync(queryClient)), [queryClient])
}

const RETRY_EVERY_MS = 30_000

/**
 * Loads the signed-in user's queue and sends it whenever there is a chance: on start, when the
 * phone comes back online, when the app returns to the foreground, and every 30s while anything
 * is waiting. Mounted once for the whole app (clock in/out is also on the office My Time page).
 */
export function OutboxSyncAgent() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const userId = user?.userId ?? null

  useEffect(() => {
    let cancelled = false
    const sync = () => {
      if (!cancelled) void syncOutbox(() => refreshAfterSync(queryClient))
    }
    void loadOutbox(userId).then(sync)
    const onVisible = () => { if (document.visibilityState === 'visible') sync() }
    window.addEventListener('online', sync)
    document.addEventListener('visibilitychange', onVisible)
    const timer = window.setInterval(() => { if (getOutboxSnapshot().entries.some((entry) => entry.status === 'pending')) sync() }, RETRY_EVERY_MS)
    return () => {
      cancelled = true
      window.removeEventListener('online', sync)
      document.removeEventListener('visibilitychange', onVisible)
      window.clearInterval(timer)
    }
  }, [userId, queryClient])

  return null
}
