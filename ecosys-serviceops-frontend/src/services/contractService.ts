import { api } from '../lib/api'
import type {
  ContractDetailRecord,
  ContractLifecycleEventRecord,
  ContractListItem,
  ContractRateCardRecord,
  ContractReminderRuleRecord,
  ContractRenewInput,
  ContractRenewalResult,
  UpsertContractInput,
  UpsertContractRateCardInput,
  UpsertContractRateRuleInput,
  UpsertContractReminderRuleInput,
  WorkOrderPricingRecord,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

type ContractListFilters = {
  status?: string | null
  renewalRisk?: string | null
  ownerUserId?: string | null
  clientId?: string | null
  contractType?: string | null
  expiringWithinDays?: number | null
}

export const contractService = {
  async list(filters?: ContractListFilters, signal?: AbortSignal): Promise<ContractListItem[]> {
    const response = await api.get<unknown>('/api/contracts', {
      query: {
        status: filters?.status || undefined,
        renewalRisk: filters?.renewalRisk || undefined,
        ownerUserId: filters?.ownerUserId || undefined,
        clientId: filters?.clientId || undefined,
        contractType: filters?.contractType || undefined,
        expiringWithinDays: filters?.expiringWithinDays ?? undefined,
      },
      signal,
    })
    return asArray<ContractListItem>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<ContractDetailRecord>(`/api/contracts/${id}`, { signal })
  },

  create(input: UpsertContractInput) {
    return api.post<ContractDetailRecord>('/api/contracts', input)
  },

  update(id: string, input: UpsertContractInput) {
    return api.put<ContractDetailRecord>(`/api/contracts/${id}`, input)
  },

  archive(id: string) {
    return api.delete<void>(`/api/contracts/${id}`)
  },

  getLifecycle(id: string, signal?: AbortSignal) {
    return api.get<ContractLifecycleEventRecord[]>(`/api/contracts/${id}/lifecycle`, { signal })
  },

  submitApproval(id: string, note?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/submit-approval`, { note: note || null, reason: null })
  },

  approve(id: string, note?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/approve`, { note: note || null, reason: null })
  },

  activate(id: string, note?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/activate`, { note: note || null, reason: null })
  },

  startRenewal(id: string, note?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/start-renewal`, { note: note || null, reason: null })
  },

  renew(id: string, input: ContractRenewInput) {
    return api.post<ContractRenewalResult>(`/api/contracts/${id}/renew`, input)
  },

  suspend(id: string, reason?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/suspend`, { note: null, reason: reason || null })
  },

  resume(id: string, note?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/resume`, { note: note || null, reason: null })
  },

  terminate(id: string, terminationDate?: string | null, reason?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/terminate`, { terminationDate: terminationDate || null, reason: reason || null })
  },

  cancel(id: string, reason?: string | null) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/cancel`, { note: null, reason: reason || null })
  },

  recalculateLifecycle(id: string) {
    return api.post<ContractDetailRecord>(`/api/contracts/${id}/recalculate-lifecycle`)
  },

  getReminderRules(id: string, signal?: AbortSignal) {
    return api.get<ContractReminderRuleRecord[]>(`/api/contracts/${id}/reminder-rules`, { signal })
  },

  createReminderRule(id: string, input: UpsertContractReminderRuleInput) {
    return api.post<ContractReminderRuleRecord>(`/api/contracts/${id}/reminder-rules`, input)
  },

  updateReminderRule(id: string, ruleId: string, input: UpsertContractReminderRuleInput) {
    return api.put<ContractReminderRuleRecord>(`/api/contracts/${id}/reminder-rules/${ruleId}`, input)
  },

  getRateCards(contractId: string, signal?: AbortSignal) {
    return api.get<ContractRateCardRecord[]>(`/api/contracts/${contractId}/rate-cards`, { signal })
  },

  createRateCard(contractId: string, input: UpsertContractRateCardInput) {
    return api.post<ContractRateCardRecord>(`/api/contracts/${contractId}/rate-cards`, input)
  },

  updateRateCard(contractId: string, rateCardId: string, input: UpsertContractRateCardInput) {
    return api.put<ContractRateCardRecord>(`/api/contracts/${contractId}/rate-cards/${rateCardId}`, input)
  },

  createRateRule(contractId: string, rateCardId: string, input: UpsertContractRateRuleInput) {
    return api.post<ContractRateCardRecord>(`/api/contracts/${contractId}/rate-cards/${rateCardId}/rules`, input)
  },

  updateRateRule(contractId: string, rateCardId: string, ruleId: string, input: UpsertContractRateRuleInput) {
    return api.put<ContractRateCardRecord>(`/api/contracts/${contractId}/rate-cards/${rateCardId}/rules/${ruleId}`, input)
  },

  archiveRateRule(contractId: string, rateCardId: string, ruleId: string) {
    return api.delete<void>(`/api/contracts/${contractId}/rate-cards/${rateCardId}/rules/${ruleId}`)
  },

  listClientContracts(clientId: string, signal?: AbortSignal) {
    return api.get<ContractListItem[]>(`/api/clients/${clientId}/contracts`, { signal })
  },

  listSiteContracts(siteId: string, signal?: AbortSignal) {
    return api.get<ContractListItem[]>(`/api/sites/${siteId}/contracts`, { signal })
  },

  listAssetContracts(assetId: string, signal?: AbortSignal) {
    return api.get<ContractListItem[]>(`/api/assets/${assetId}/contracts`, { signal })
  },

  getWorkOrderPricing(workOrderId: string, signal?: AbortSignal) {
    return api.get<WorkOrderPricingRecord>(`/api/workorders/${workOrderId}/pricing`, { signal })
  },

  recalculateWorkOrderPricing(workOrderId: string) {
    return api.post<WorkOrderPricingRecord>(`/api/workorders/${workOrderId}/recalculate-pricing`)
  },
}
