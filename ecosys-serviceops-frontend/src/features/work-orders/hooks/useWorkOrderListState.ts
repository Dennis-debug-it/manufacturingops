import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'
import type { WorkOrderFilterValues } from '../listModel'

export type ViewMode = 'table' | 'kanban' | 'calendar' | 'map'

export interface WorkOrderListState extends WorkOrderFilterValues {
  view: ViewMode
}

const PARAM_KEYS: Record<keyof WorkOrderListState, string> = {
  view: 'view',
  search: 'q',
  status: 'status',
  technicianId: 'technicianId',
  clientId: 'clientId',
  siteId: 'siteId',
  priority: 'priority',
  slaStatus: 'slaStatus',
  unassignedOnly: 'unassigned',
  state: 'state',
  fromDate: 'from',
  toDate: 'to',
  showAll: 'all',
}

const FILTER_KEYS: (keyof WorkOrderFilterValues)[] = [
  'search',
  'status',
  'technicianId',
  'clientId',
  'siteId',
  'priority',
  'slaStatus',
  'unassignedOnly',
  'state',
  'fromDate',
  'toDate',
]

const VIEW_MODES: ViewMode[] = ['table', 'kanban', 'calendar', 'map']

export function useWorkOrderListState() {
  const [searchParams, setSearchParams] = useSearchParams()

  const state = useMemo<WorkOrderListState>(() => {
    const rawView = searchParams.get(PARAM_KEYS.view)
    const view = (VIEW_MODES as string[]).includes(rawView ?? '') ? (rawView as ViewMode) : 'table'
    return {
      view,
      search: searchParams.get(PARAM_KEYS.search) ?? '',
      status: searchParams.get(PARAM_KEYS.status) ?? '',
      technicianId: searchParams.get(PARAM_KEYS.technicianId) ?? '',
      clientId: searchParams.get(PARAM_KEYS.clientId) ?? '',
      siteId: searchParams.get(PARAM_KEYS.siteId) ?? '',
      priority: searchParams.get(PARAM_KEYS.priority) ?? '',
      slaStatus: searchParams.get(PARAM_KEYS.slaStatus) ?? '',
      unassignedOnly: searchParams.get(PARAM_KEYS.unassignedOnly) === '1',
      state: searchParams.get(PARAM_KEYS.state) ?? '',
      fromDate: searchParams.get(PARAM_KEYS.fromDate) ?? '',
      toDate: searchParams.get(PARAM_KEYS.toDate) ?? '',
      showAll: searchParams.get(PARAM_KEYS.showAll) === '1',
    }
  }, [searchParams])

  const update = useCallback(
    (patch: Partial<WorkOrderListState>) => {
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev)
          for (const key of Object.keys(patch) as (keyof WorkOrderListState)[]) {
            const value = patch[key]
            const paramKey = PARAM_KEYS[key]
            if (value === '' || value === false || value == null) {
              next.delete(paramKey)
            } else if (value === true) {
              next.set(paramKey, '1')
            } else {
              next.set(paramKey, String(value))
            }
          }
          return next
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  const setView = useCallback((view: ViewMode) => update({ view }), [update])

  const clearFilters = useCallback(() => {
    update({
      ...(Object.fromEntries(FILTER_KEYS.map((key) => [key, key === 'unassignedOnly' ? false : ''])) as Partial<WorkOrderListState>),
      showAll: false,
    })
  }, [update])

  const activeFilterCount = FILTER_KEYS.filter((key) => key !== 'search' && Boolean(state[key])).length

  return { state, update, setView, clearFilters, activeFilterCount }
}
