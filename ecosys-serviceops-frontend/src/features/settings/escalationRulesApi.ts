import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export const escalationTriggerConditions = [
  'UnassignedAfterMins',
  'InProgressOverEstimatePercent',
  'StatusUnchangedMins',
  'OverdueSinceMins',
] as const

export const escalationActions = [
  'NotifyDispatcher',
  'NotifyManager',
  'NotifyTechnician',
  'ReassignToUserId',
  'ChangePriority',
] as const

export type EscalationTriggerCondition = (typeof escalationTriggerConditions)[number]
export type EscalationAction = (typeof escalationActions)[number]

export interface EscalationRule {
  id: string
  name: string
  isActive: boolean
  triggerCondition: EscalationTriggerCondition
  triggerValue: number
  action: EscalationAction
  actionValue: string | null
  cooldownMins: number
  createdAt: string
  updatedAt: string | null
}

export interface UpsertEscalationRulePayload {
  name: string
  isActive: boolean
  triggerCondition: string
  triggerValue: number
  action: string
  actionValue: string | null
  cooldownMins: number
}

const escalationRulesKey = ['settings', 'escalation-rules'] as const

export function useEscalationRules() {
  return useQuery({
    queryKey: escalationRulesKey,
    queryFn: () => api.get<EscalationRule[]>(endpoints.escalationRules.list),
    staleTime: 30_000,
  })
}

export function useCreateEscalationRule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertEscalationRulePayload) => api.post<EscalationRule>(endpoints.escalationRules.create, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: escalationRulesKey })
    },
  })
}

export function useUpdateEscalationRule(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertEscalationRulePayload) => api.put<EscalationRule>(endpoints.escalationRules.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: escalationRulesKey })
    },
  })
}

export function useDeleteEscalationRule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.escalationRules.delete(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: escalationRulesKey })
    },
  })
}
