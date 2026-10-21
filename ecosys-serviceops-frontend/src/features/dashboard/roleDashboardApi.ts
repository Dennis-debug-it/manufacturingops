import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { getErrorStatus } from '../../shared/api/apiError'
import type { DashboardAdminSettings, DashboardLayout, DashboardLayoutItem, DashboardWidgetData } from './roleDashboardTypes'

const layoutKey = ['dashboard', 'layout'] as const

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
 * One widget, loaded on its own so a slow one doesn't hold up the page. The server caches each for
 * five minutes; `refresh` asks it to recompute. A 403 (module or permission gone) isn't retried.
 */
export function useDashboardWidget(key: string, branchId: string) {
  const queryClient = useQueryClient()
  const queryKey = ['dashboard', 'widget', key, branchId] as const
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
    retry: (failureCount, error) => getErrorStatus(error) !== 403 && getErrorStatus(error) !== 404 && failureCount < 2,
  })
  const refresh = async () => {
    const data = await fetchWidget(true)
    queryClient.setQueryData(queryKey, data)
  }
  return { ...query, refresh }
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
