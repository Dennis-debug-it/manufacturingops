import { api } from '../lib/api'
import type { ClientCreditSummaryRecord, CreditTermRecord } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const creditTermService = {
  async list(signal?: AbortSignal): Promise<CreditTermRecord[]> {
    const response = await api.get<unknown>('/api/finance/credit-terms', { signal })
    return asArray<CreditTermRecord>(response)
  },

  create(input: {
    name: string
    code: string
    netDays: number
    isDepositRequired: boolean
    depositPercent: number
    creditLimit?: number | null
    description?: string | null
    isDefault: boolean
  }) {
    return api.post<CreditTermRecord>('/api/finance/credit-terms', input)
  },

  update(id: string, input: {
    name: string
    code: string
    netDays: number
    isDepositRequired: boolean
    depositPercent: number
    creditLimit?: number | null
    description?: string | null
    isDefault: boolean
  }) {
    return api.put<CreditTermRecord>(`/api/finance/credit-terms/${id}`, input)
  },

  setDefault(id: string) {
    return api.post<CreditTermRecord>(`/api/finance/credit-terms/${id}/set-default`)
  },

  delete(id: string) {
    return api.delete<void>(`/api/finance/credit-terms/${id}`)
  },

  getClientCreditSummary(clientId: string, signal?: AbortSignal) {
    return api.get<ClientCreditSummaryRecord>(`/api/clients/${clientId}/credit-summary`, { signal })
  },
}
