import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { WorkOrder } from '../work-orders/types'

// Keys live under 'work-orders' so every work-order mutation's invalidation refreshes them too.
export function useTechJobs() {
  return useQuery({
    queryKey: ['work-orders', 'tech-jobs'],
    queryFn: () => api.get<WorkOrder[]>(endpoints.technicianJobs.list),
    staleTime: 15_000,
  })
}

export function useTechJob(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'tech'],
    queryFn: () => api.get<WorkOrder>(endpoints.technicianJobs.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useMyTechnician() {
  return useQuery({
    queryKey: ['technician', 'me'],
    queryFn: () => api.get<{ technicianId: string; fullName: string }>(endpoints.technicianJobs.me),
    staleTime: 5 * 60_000,
    retry: false,
  })
}
