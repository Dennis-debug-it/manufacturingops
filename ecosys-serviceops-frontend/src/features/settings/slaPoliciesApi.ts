import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface SlaEscalation {
  id: string
  triggerAtPercent: number
  notifyRoles: string | null
  escalateToUserId: string | null
}

export interface SlaPolicy {
  id: string
  slaDefinitionId: string
  name: string | null
  priority: 'Critical' | 'High' | 'Medium' | 'Low'
  responseTargetMinutes: number
  resolutionTargetMinutes: number
  businessHoursOnly: boolean
  businessHoursStart: string | null
  businessHoursEnd: string | null
  escalations: SlaEscalation[]
}

export interface UpsertSlaEscalationPayload {
  triggerAtPercent: number
  notifyRoles: string | null
  escalateToUserId: string | null
}

export interface UpsertSlaPolicyPayload {
  name: string | null
  priority: string
  responseTargetMinutes: number
  resolutionTargetMinutes: number
  businessHoursOnly: boolean
  businessHoursStart: string | null
  businessHoursEnd: string | null
  escalations: UpsertSlaEscalationPayload[]
}

const slaPoliciesKey = ['settings', 'sla-policies'] as const

export function useSlaPolicies() {
  return useQuery({
    queryKey: slaPoliciesKey,
    queryFn: () => api.get<SlaPolicy[]>(endpoints.slaPolicies.list),
    staleTime: 30_000,
  })
}

export function useCreateSlaPolicy() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertSlaPolicyPayload) => api.post<SlaPolicy>(endpoints.slaPolicies.create, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: slaPoliciesKey })
    },
  })
}

export function useUpdateSlaPolicy(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertSlaPolicyPayload) => api.put<SlaPolicy>(endpoints.slaPolicies.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: slaPoliciesKey })
    },
  })
}

export function useDeleteSlaPolicy() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.slaPolicies.delete(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: slaPoliciesKey })
    },
  })
}
