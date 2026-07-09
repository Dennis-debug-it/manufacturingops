import { api } from '../lib/api'
import type {
  FinanceApprovalRuleRecord,
  PreferentialRateRecord,
  RateCardRecord,
  TenantBillableWorkRecord,
  TenantFinanceOverview,
  UpsertFinanceApprovalRuleInput,
  UpsertPreferentialRateInput,
  UpsertRateCardInput,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const tenantFinanceService = {
  getSummary(signal?: AbortSignal) {
    return api.get<TenantFinanceOverview>('/api/finance/summary', { signal })
  },
  async listPreferentialRates(signal?: AbortSignal): Promise<PreferentialRateRecord[]> {
    const response = await api.get<unknown>('/api/finance/preferential-rates', { signal })
    return asArray<PreferentialRateRecord>(response)
  },
  createPreferentialRate(input: UpsertPreferentialRateInput) {
    return api.post<PreferentialRateRecord>('/api/finance/preferential-rates', input)
  },
  updatePreferentialRate(id: string, input: UpsertPreferentialRateInput) {
    return api.put<PreferentialRateRecord>(`/api/finance/preferential-rates/${id}`, input)
  },
  approvePreferentialRate(id: string) {
    return api.post<PreferentialRateRecord>(`/api/finance/preferential-rates/${id}/approve`)
  },
  async listApprovalRules(signal?: AbortSignal): Promise<FinanceApprovalRuleRecord[]> {
    const response = await api.get<unknown>('/api/finance/approval-rules', { signal })
    return asArray<FinanceApprovalRuleRecord>(response)
  },
  createApprovalRule(input: UpsertFinanceApprovalRuleInput) {
    return api.post<FinanceApprovalRuleRecord>('/api/finance/approval-rules', input)
  },
  updateApprovalRule(id: string, input: UpsertFinanceApprovalRuleInput) {
    return api.put<FinanceApprovalRuleRecord>(`/api/finance/approval-rules/${id}`, input)
  },
  async listBillableWork(signal?: AbortSignal): Promise<TenantBillableWorkRecord[]> {
    const response = await api.get<unknown>('/api/finance/billable-work', { signal })
    return asArray<TenantBillableWorkRecord>(response)
  },
  async getRateCards(signal?: AbortSignal): Promise<RateCardRecord[]> {
    const response = await api.get<unknown>('/api/finance/rate-cards', { signal })
    return asArray<RateCardRecord>(response)
  },
  createRateCard(input: UpsertRateCardInput) {
    return api.post<RateCardRecord>('/api/finance/rate-cards', input)
  },
  updateRateCard(id: string, input: UpsertRateCardInput) {
    return api.put<RateCardRecord>(`/api/finance/rate-cards/${id}`, input)
  },
  deactivateRateCard(id: string) {
    return api.delete<void>(`/api/finance/rate-cards/${id}`)
  },
}
