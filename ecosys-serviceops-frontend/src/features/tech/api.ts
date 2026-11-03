import { useMutation, useQuery, type QueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { Van, VanStockLine } from '../inventory/vansApi'
import type { KnowledgeArticleListItem } from '../knowledge/types'
import type { WorkOrder, WorkOrderExecutionBundle, WorkOrderServiceReportPreview } from '../work-orders/types'

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
    // 404 just means "no technician profile" (e.g. office staff on /me); callers handle its absence.
    queryFn: () => api.get<{ technicianId: string; fullName: string }>(endpoints.technicianJobs.me, undefined, { suppressGlobalErrorToast: true }),
    staleTime: 5 * 60_000,
    retry: false,
  })
}

// Published guides a technician can read; /search only returns published articles they may see.
export function useGuideSearch(q: string) {
  return useQuery({
    queryKey: ['knowledge-articles', 'tech-search', q],
    queryFn: () => api.get<KnowledgeArticleListItem[]>(endpoints.knowledge.search, { q: q || undefined }),
    staleTime: 30_000,
  })
}

export function useGuideSuggestions(workOrderId: string) {
  return useQuery({
    queryKey: ['knowledge-articles', 'tech-suggestions', workOrderId],
    // Optional extra on the job screen: stay quiet if this user's role can't read the knowledge base.
    queryFn: () => api.get<KnowledgeArticleListItem[]>(endpoints.knowledge.suggestionsForWorkOrder(workOrderId), undefined, { suppressGlobalErrorToast: true }),
    enabled: Boolean(workOrderId),
    staleTime: 5 * 60_000,
    retry: false,
  })
}

export function useChangeMyPassword() {
  return useMutation({
    mutationFn: (payload: { currentPassword: string; newPassword: string; confirmPassword: string }) =>
      api.post<{ message: string }>(endpoints.auth.changePassword, payload),
  })
}

/** Downloads the job's service report PDF (the API builds it on demand). */
export async function downloadServiceReport(workOrderId: string, workOrderNumber: string) {
  const response = await apiClient.get(endpoints.workOrders.serviceReportPdf(workOrderId), { responseType: 'blob', suppressGlobalErrorToast: true })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${workOrderNumber}-service-report.pdf`
  link.click()
  URL.revokeObjectURL(url)
}

/** The van assigned to this technician, if any. Quiet on 403 for roles without stock access. */
export function useMyVan(technicianId: string | undefined) {
  return useQuery({
    queryKey: ['inventory', 'vans', 'mine', technicianId],
    queryFn: async () => {
      const vans = await api.get<Van[]>(endpoints.vans.list, undefined, { suppressGlobalErrorToast: true })
      const van = vans.find((item) => item.technicianId === technicianId)
      if (!van) return null
      const stock = await api.get<VanStockLine[]>(endpoints.vans.stock(van.id), undefined, { suppressGlobalErrorToast: true })
      return { van, stock }
    },
    enabled: Boolean(technicianId),
    retry: false,
    staleTime: 30_000,
  })
}

/** The service report of the job a follow-up returns to, so the technician knows what happened last time. */
export function useFollowUpSourceReport(workOrderId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['work-orders', workOrderId, 'follow-up-source-report'],
    queryFn: () => api.get<WorkOrderServiceReportPreview>(endpoints.workOrders.followUpSourceReport(workOrderId)),
    enabled: enabled && Boolean(workOrderId),
    staleTime: 5 * 60_000,
  })
}

export async function downloadFollowUpSourceReport(workOrderId: string, originalNumber: string) {
  const response = await apiClient.get(endpoints.workOrders.followUpSourceReportPdf(workOrderId), { responseType: 'blob', suppressGlobalErrorToast: true })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${originalNumber}-service-report.pdf`
  link.click()
  URL.revokeObjectURL(url)
}

/**
 * Loads everything a job screen needs into the cache (and so onto the phone, see offline/persist.ts)
 * before the technician gets there, so a job they haven't opened yet still works without signal.
 * Keys and data match useTechJob, useExecutionBundle and useFollowUpSourceReport.
 */
export function prefetchJobForOffline(queryClient: QueryClient, job: WorkOrder) {
  const staleTime = 60_000
  void queryClient.prefetchQuery({ queryKey: ['work-orders', job.id, 'tech'], queryFn: () => api.get<WorkOrder>(endpoints.technicianJobs.detail(job.id)), staleTime })
  void queryClient.prefetchQuery({ queryKey: ['work-orders', job.id, 'execution'], queryFn: () => api.get<WorkOrderExecutionBundle>(endpoints.workOrders.executionBundle(job.id)), staleTime })
  if (job.followUpOfWorkOrderId) {
    void queryClient.prefetchQuery({ queryKey: ['work-orders', job.id, 'follow-up-source-report'], queryFn: () => api.get<WorkOrderServiceReportPreview>(endpoints.workOrders.followUpSourceReport(job.id)), staleTime: 5 * 60_000 })
  }
}
