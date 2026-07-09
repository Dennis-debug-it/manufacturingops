import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { LeadDetail, LeadListItem, LeadStatus, PlatformDashboardSummary } from './types'

export function usePlatformDashboardSummary() {
  return useQuery({ queryKey: ['platform', 'dashboard-summary'], queryFn: () => api.get<PlatformDashboardSummary>(endpoints.platform.dashboardSummary), staleTime: 30_000 })
}

export function useLeads() {
  return useQuery({ queryKey: ['platform', 'leads'], queryFn: () => api.get<LeadListItem[]>(endpoints.platform.leads), staleTime: 30_000 })
}

export function useLead(id: string | undefined) {
  return useQuery({
    queryKey: ['platform', 'leads', id],
    queryFn: () => api.get<LeadDetail>(endpoints.platform.lead(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useUpdateLeadStatus(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (status: LeadStatus) => api.put<LeadDetail>(endpoints.platform.leadStatus(id), { status }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['platform', 'leads'] })
      void queryClient.invalidateQueries({ queryKey: ['platform', 'leads', id] })
    },
  })
}

export function useUpdateLeadNotes(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (notes: string) => api.put<LeadDetail>(endpoints.platform.leadNotes(id), { notes }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['platform', 'leads', id] }),
  })
}
