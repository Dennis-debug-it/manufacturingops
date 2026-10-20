import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { WorkOrder } from '../work-orders/types'
import type {
  SalesActivity,
  SalesOpportunityDetail,
  SalesOpportunityListItem,
  SalesPipelineSummary,
  SalesStage,
  UpsertOpportunityPayload,
} from './types'

const crmKey = ['crm'] as const

export function usePipelineSummary() {
  return useQuery({ queryKey: [...crmKey, 'summary'], queryFn: () => api.get<SalesPipelineSummary>(endpoints.crm.summary), staleTime: 30_000 })
}

export function useOpportunities(params?: { stage?: string | undefined; search?: string | undefined; clientId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'opportunities', params ?? {}],
    queryFn: () => api.get<SalesOpportunityListItem[]>(endpoints.crm.opportunities, { stage: params?.stage, search: params?.search, clientId: params?.clientId }),
    staleTime: 30_000,
  })
}

export function useOpportunity(id: string | undefined) {
  return useQuery({
    queryKey: [...crmKey, 'opportunity', id],
    queryFn: () => api.get<SalesOpportunityDetail>(endpoints.crm.opportunity(id ?? '')),
    enabled: Boolean(id),
  })
}

function useInvalidateCrm() {
  const queryClient = useQueryClient()
  return () => void queryClient.invalidateQueries({ queryKey: crmKey })
}

export function useCreateOpportunity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertOpportunityPayload) => api.post<SalesOpportunityDetail>(endpoints.crm.opportunities, payload),
    onSuccess: invalidate,
  })
}

export function useUpdateOpportunity(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertOpportunityPayload) => api.put<SalesOpportunityDetail>(endpoints.crm.opportunity(id), payload),
    onSuccess: invalidate,
  })
}

export function useChangeStage() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: ({ id, stage, lostReason }: { id: string; stage: SalesStage; lostReason?: string | null }) =>
      api.post<SalesOpportunityDetail>(endpoints.crm.stage(id), { stage, lostReason: lostReason ?? null }),
    onSuccess: invalidate,
  })
}

export function useDeleteOpportunity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.crm.opportunity(id)),
    onSuccess: invalidate,
  })
}

export function useAddActivity(opportunityId: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: { type: string; summary: string; dueAt?: string | null }) =>
      api.post<SalesActivity>(endpoints.crm.activities(opportunityId), payload),
    onSuccess: invalidate,
  })
}

export function useCompleteActivity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (activityId: string) => api.post<SalesActivity>(endpoints.crm.completeActivity(activityId)),
    onSuccess: invalidate,
  })
}

export function useConvertToClient(opportunityId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<SalesOpportunityDetail>(endpoints.crm.convertToClient(opportunityId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['clients'] })
    },
  })
}

export function useConvertToQuotation(opportunityId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ quotationId: string; quoteNumber: string }>(endpoints.crm.convertToQuotation(opportunityId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
    },
  })
}

export interface CreateWorkOrderFromOpportunityPayload {
  opportunityId: string
  title: string | null
  description: string | null
  priority: string
  workType: string
  dueDate: string | null
  siteId: string | null
}

export function useCreateWorkOrderFromOpportunity() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWorkOrderFromOpportunityPayload) => api.post<WorkOrder>(endpoints.crm.createWorkOrder, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    },
  })
}
