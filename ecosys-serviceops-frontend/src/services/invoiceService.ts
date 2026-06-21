import { api } from '../lib/api'
import type {
  CreateInvoiceInput,
  RecordPaymentInput,
  TenantInvoiceDetail,
  TenantInvoiceRecord,
  TenantPaymentRecord,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const invoiceService = {
  async list(params?: {
    status?: string
    clientId?: string
    dateFrom?: string
    dateTo?: string
    search?: string
    signal?: AbortSignal
  }): Promise<TenantInvoiceRecord[]> {
    const query: Record<string, string> = {}
    if (params?.status) query.status = params.status
    if (params?.clientId) query.clientId = params.clientId
    if (params?.dateFrom) query.dateFrom = params.dateFrom
    if (params?.dateTo) query.dateTo = params.dateTo
    if (params?.search) query.search = params.search
    const response = await api.get<unknown>('/api/invoices', { signal: params?.signal, query })
    return asArray<TenantInvoiceRecord>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<TenantInvoiceDetail>(`/api/invoices/${id}`, { signal })
  },

  create(input: CreateInvoiceInput) {
    return api.post<TenantInvoiceDetail>('/api/invoices', input)
  },

  update(id: string, input: CreateInvoiceInput) {
    return api.put<TenantInvoiceDetail>(`/api/invoices/${id}`, input)
  },

  send(id: string) {
    return api.post<TenantInvoiceDetail>(`/api/invoices/${id}/send`)
  },

  void(id: string, reason?: string) {
    return api.post<TenantInvoiceDetail>(`/api/invoices/${id}/void`, { reason })
  },

  downloadPdf(id: string) {
    return api.download(`/api/invoices/${id}/pdf`, { fallbackFileName: 'Invoice.pdf' })
  },

  generateFromWorkOrder(workOrderId: string) {
    return api.post<TenantInvoiceDetail>(`/api/work-orders/${workOrderId}/generate-invoice`)
  },

  // Payments
  async listPayments(invoiceId: string, signal?: AbortSignal): Promise<TenantPaymentRecord[]> {
    const response = await api.get<unknown>(`/api/invoices/${invoiceId}/payments`, { signal })
    return asArray<TenantPaymentRecord>(response)
  },

  recordPayment(invoiceId: string, input: RecordPaymentInput) {
    return api.post<TenantPaymentRecord>(`/api/invoices/${invoiceId}/payments`, input)
  },

  reversePayment(paymentId: string) {
    return api.delete<void>(`/api/payments/${paymentId}`)
  },

  downloadReceiptPdf(paymentId: string) {
    return api.download(`/api/payments/${paymentId}/receipt-pdf`, { fallbackFileName: 'Receipt.pdf' })
  },
}
