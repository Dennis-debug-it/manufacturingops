import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PmPlan, PmTemplate } from './types'
import type { WorkOrder } from '../work-orders/types'

export function usePmPlans(branchId?: string) {
  return useQuery({
    queryKey: ['pm-plans', branchId ?? ''],
    queryFn: () => api.get<PmPlan[]>(endpoints.preventiveMaintenance.list, { branchId }),
    staleTime: 30_000,
  })
}

export function usePmPlansDue(branchId?: string) {
  return useQuery({
    queryKey: ['pm-plans', 'due', branchId ?? ''],
    queryFn: () => api.get<PmPlan[]>(endpoints.preventiveMaintenance.due, { branchId }),
    staleTime: 30_000,
  })
}

export interface UpsertPmPlanPayload {
  assetId: string
  pmTemplateId?: string | null
  serviceIntervalMonths?: number | null
  frequency?: string | null
  autoSchedule: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  status?: string | null
}

export function useCreatePmPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPmPlanPayload) => api.post<PmPlan>(endpoints.preventiveMaintenance.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['pm-plans'] }),
  })
}

export function useUpdatePmPlan(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPmPlanPayload) => api.put<PmPlan>(endpoints.preventiveMaintenance.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['pm-plans'] })
    },
  })
}

export function useGenerateWorkOrderNow(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<WorkOrder>(endpoints.preventiveMaintenance.generateWorkOrder(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['pm-plans'] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    },
  })
}

// PM Templates
export function usePmTemplates() {
  return useQuery({
    queryKey: ['pm-templates'],
    queryFn: () => api.get<PmTemplate[]>(endpoints.pmTemplates.list),
    staleTime: 60_000,
  })
}

export function usePmTemplate(id: string | undefined) {
  return useQuery({
    queryKey: ['pm-templates', id],
    queryFn: () => api.get<PmTemplate>(endpoints.pmTemplates.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertPmTemplatePayload {
  name: string
  category: string
  description?: string | null
  assetCategoryId?: string | null
  checklist: Array<{
    sectionName?: string | null
    sectionOrder?: number | null
    question: string
    helperText?: string | null
    type: string
    unit?: string | null
    minValue?: number | null
    maxValue?: number | null
    defaultValue?: string | null
    required: boolean
    evidenceRequired: boolean
    technicianRemarksAllowed?: boolean | null
    requiresNoteOnFail: boolean
    order: number
    options?: string[] | null
  }>
  isActive: boolean
  code?: string | null
  workType?: string
  defaultIntervalMonths?: number | null
}

export function useCreatePmTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPmTemplatePayload) => api.post<PmTemplate>(endpoints.pmTemplates.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['pm-templates'] }),
  })
}

export function useUpdatePmTemplate(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPmTemplatePayload) => api.put<PmTemplate>(endpoints.pmTemplates.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['pm-templates'] })
      void queryClient.invalidateQueries({ queryKey: ['pm-templates', id] })
    },
  })
}

export function useDuplicatePmTemplate(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<PmTemplate>(endpoints.pmTemplates.duplicate(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['pm-templates'] }),
  })
}

export function useSetPmTemplateActive(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (active: boolean) =>
      active ? api.post<PmTemplate>(endpoints.pmTemplates.activate(id)) : api.post<PmTemplate>(endpoints.pmTemplates.deactivate(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['pm-templates'] }),
  })
}

export function useDeletePmTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.pmTemplates.delete(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['pm-templates'] }),
  })
}

// PM Compliance report
export interface PmComplianceByClient {
  clientId: string
  due: number
  completed: number
  complianceRate: number
}

export interface PmOverduePlan {
  id: string
  assetName: string | null
  assetCode: string | null
  clientName: string | null
  nextPmDate: string | null
  daysOverdue: number
}

export interface PmComplianceReport {
  dateRange: { from: string; to: string }
  activePlans: number
  dueInPeriod: number
  completedOnTime: number
  overdue: number
  complianceRate: number
  byClient: PmComplianceByClient[]
  overduePlans: PmOverduePlan[]
}

export function usePmComplianceReport(params: { from?: string; to?: string; clientId?: string }) {
  return useQuery({
    queryKey: ['reports', 'pm-compliance', params],
    queryFn: () => api.get<PmComplianceReport>(endpoints.reports.pmCompliance, params),
    staleTime: 30_000,
  })
}
