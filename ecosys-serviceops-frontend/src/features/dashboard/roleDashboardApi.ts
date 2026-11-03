import { useEffect, useRef } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { getErrorStatus } from '../../shared/api/apiError'
import { useLiveNotifications } from '../notifications/LiveNotifications'
import { widgetKeysForAreas } from './layoutModel'
import type { DashboardAdminSettings, DashboardLayout, DashboardLayoutItem, DashboardWidgetData, DashboardWidgetInfo } from './roleDashboardTypes'

const layoutKey = ['dashboard', 'layout'] as const
const widgetKey = (key: string) => ['dashboard', 'widget', key] as const

/** While the live connection is down, visible widgets refetch this often instead. */
export const DASHBOARD_POLL_MS = 120_000

export function useDashboardLayout() {
  return useQuery({
    queryKey: layoutKey,
    queryFn: () => api.get<DashboardLayout>(endpoints.dashboard.layout),
    staleTime: 60_000,
  })
}

export function useSaveDashboardLayout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (items: DashboardLayoutItem[]) => api.put<DashboardLayout>(endpoints.dashboard.layout, { items }),
    onSuccess: (layout) => queryClient.setQueryData(layoutKey, layout),
  })
}

export function useResetDashboardLayout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.delete<DashboardLayout>(endpoints.dashboard.layout),
    onSuccess: (layout) => queryClient.setQueryData(layoutKey, layout),
  })
}

/**
 * One widget, loaded on its own so a slow one doesn't hold up the page. The server recomputes it
 * when its data changes (and at least every five minutes); `refresh` forces that now. Live
 * `DashboardChanged` events refetch it (see `useDashboardLiveUpdates`); while the connection is down
 * it polls instead. A 403 (module or permission gone) isn't retried.
 */
export function useDashboardWidget(key: string, branchId: string) {
  const queryClient = useQueryClient()
  const { live } = useLiveNotifications()
  const queryKey = [...widgetKey(key), branchId] as const
  const fetchWidget = (refresh: boolean) =>
    api.get<DashboardWidgetData>(
      endpoints.dashboard.widget(key),
      { ...(branchId ? { branchId } : {}), ...(refresh ? { refresh: true } : {}) },
      { suppressGlobalErrorToast: true },
    )
  const query = useQuery({
    queryKey,
    queryFn: () => fetchWidget(false),
    staleTime: 60_000,
    refetchInterval: live ? false : DASHBOARD_POLL_MS,
    retry: (failureCount, error) => getErrorStatus(error) !== 403 && getErrorStatus(error) !== 404 && failureCount < 2,
  })
  const refresh = async () => {
    const data = await fetchWidget(true)
    queryClient.setQueryData(queryKey, data)
  }
  return { ...query, refresh }
}

/**
 * Refetches the widgets whose data changed, on the bell's SignalR connection: the server names the
 * changed areas, and each widget's `areas` (from the layout) says whether it reads one. After a
 * reconnect every widget refetches, since changes made meanwhile were not heard.
 */
export function useDashboardLiveUpdates(available: DashboardWidgetInfo[]) {
  const queryClient = useQueryClient()
  const { live, subscribe } = useLiveNotifications()
  const availableRef = useRef(available)
  useEffect(() => {
    availableRef.current = available
  }, [available])

  useEffect(
    () =>
      subscribe('DashboardChanged', (areas: string[]) => {
        for (const key of widgetKeysForAreas(availableRef.current, areas ?? [])) {
          void queryClient.invalidateQueries({ queryKey: widgetKey(key) })
        }
      }),
    [queryClient, subscribe],
  )

  const wasLive = useRef(live)
  useEffect(() => {
    if (live && !wasLive.current) void queryClient.invalidateQueries({ queryKey: ['dashboard', 'widget'] })
    wasLive.current = live
  }, [live, queryClient])
}

const settingsKey = ['dashboard', 'settings'] as const

export function useDashboardAdminSettings() {
  return useQuery({
    queryKey: settingsKey,
    queryFn: () => api.get<DashboardAdminSettings>(endpoints.dashboard.settings),
  })
}

export function useSetRoleHiddenWidgets() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ role, hiddenKeys }: { role: string; hiddenKeys: string[] }) =>
      api.put<DashboardAdminSettings>(endpoints.dashboard.roleHidden(role), { hiddenKeys }),
    onSuccess: (settings) => {
      queryClient.setQueryData(settingsKey, settings)
      void queryClient.invalidateQueries({ queryKey: layoutKey })
    },
  })
}
