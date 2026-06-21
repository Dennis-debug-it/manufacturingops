import { api } from '../lib/api'
import type {
  TenantQuotationDetail,
  TenantQuotationRecord,
  UpsertQuotationInput,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const quotationService = {
  async list(params?: {
    status?: string
    clientId?: string
    search?: string
    signal?: AbortSignal
  }): Promise<TenantQuotationRecord[]> {
    const query: Record<string, string> = {}
    if (params?.status) query.status = params.status
    if (params?.clientId) query.clientId = params.clientId
    if (params?.search) query.search = params.search
    const response = await api.get<unknown>('/api/quotations', { signal: params?.signal, query })
    return asArray<TenantQuotationRecord>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<TenantQuotationDetail>(`/api/quotations/${id}`, { signal })
  },

  create(input: UpsertQuotationInput) {
    return api.post<TenantQuotationDetail>('/api/quotations', input)
  },

  update(id: string, input: UpsertQuotationInput) {
    return api.put<TenantQuotationDetail>(`/api/quotations/${id}`, input)
  },

  send(id: string) {
    return api.post<TenantQuotationDetail>(`/api/quotations/${id}/send`)
  },

  accept(id: string) {
    return api.post<TenantQuotationDetail>(`/api/quotations/${id}/accept`)
  },

  reject(id: string, reason?: string) {
    return api.post<TenantQuotationDetail>(`/api/quotations/${id}/reject`, { reason })
  },

  convertToInvoice(id: string) {
    return api.post<unknown>(`/api/quotations/${id}/convert-to-invoice`)
  },

  convertToWorkOrder(id: string) {
    return api.post<{ workOrderId: string; workOrderNumber: string }>(`/api/quotations/${id}/convert-to-work-order`)
  },

  downloadPdf(id: string) {
    return api.download(`/api/quotations/${id}/pdf`, { fallbackFileName: 'Quotation.pdf' })
  },
}
