import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { ContractDetail, ContractListItem } from './types'

export interface ContractListFilters {
  status?: string
  renewalRisk?: string
  clientId?: string
  contractType?: string
  expiringWithinDays?: number
}

export function useContracts(filters?: ContractListFilters, enabled = true) {
  return useQuery({
    enabled,
    queryKey: ['contracts', filters ?? {}],
    queryFn: () => api.get<ContractListItem[]>(endpoints.contracts.list, { status: filters?.status, renewalRisk: filters?.renewalRisk, clientId: filters?.clientId, contractType: filters?.contractType, expiringWithinDays: filters?.expiringWithinDays }),
    staleTime: 30_000,
  })
}

export function useContract(id: string | undefined) {
  return useQuery({
    queryKey: ['contracts', id],
    queryFn: () => api.get<ContractDetail>(endpoints.contracts.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertContractPayload {
  contractNumber: string
  clientId: string
  name: string
  contractType: string
  status: string
  startDate: string
  endDate: string
  billingModel: string
  currency: string
  contractValue?: number | null
  renewalNoticeDays: number
  description?: string | null
  gracePeriodDays: number
  allowPreferentialRatesDuringGracePeriod: boolean
  allowSlaDuringGracePeriod: boolean
  gracePeriodAllowsSla: boolean
  pauseSlaOnHold: boolean
  responseTimeHours?: number | null
  resolutionTimeHours?: number | null
  siteIds: string[]
  assetIds: string[]
  assetCategories?: Array<{ assetCategoryId: string | null; assetCategoryName: string | null }>
  slaMatrix?: Array<{ priority: string; responseTimeHours: number | null; resolutionTimeHours: number | null }>
}

export function useCreateContract() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertContractPayload) => api.post<ContractDetail>(endpoints.contracts.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['contracts'] }),
  })
}

function useContractLifecycleAction(id: string, url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload?: { note?: string | null; reason?: string | null }) => api.post<ContractDetail>(url, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['contracts'] })
      void queryClient.invalidateQueries({ queryKey: ['contracts', id] })
    },
  })
}

export const useSubmitContractApproval = (id: string) => useContractLifecycleAction(id, endpoints.contracts.submitApproval(id))
export const useApproveContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.approve(id))
export const useActivateContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.activate(id))
export const useStartContractRenewal = (id: string) => useContractLifecycleAction(id, endpoints.contracts.startRenewal(id))
export const useSuspendContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.suspend(id))
export const useResumeContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.resume(id))
export const useTerminateContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.terminate(id))
export const useCancelContract = (id: string) => useContractLifecycleAction(id, endpoints.contracts.cancel(id))
