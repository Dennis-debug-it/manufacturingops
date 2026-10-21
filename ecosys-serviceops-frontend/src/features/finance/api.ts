import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  AgingRow,
  CreditTerm,
  FinanceApprovalRule,
  FinanceDashboard,
  InvoiceDetail,
  InvoiceListItem,
  Payment,
  PreferentialRate,
  QuotationDetail,
  QuotationListItem,
  RateCard,
} from './types'

export function useFinanceDashboard(options?: { enabled?: boolean }) {
  return useQuery({ queryKey: ['finance', 'dashboard'], queryFn: () => api.get<FinanceDashboard>(endpoints.finance.dashboard), staleTime: 30_000, enabled: options?.enabled ?? true })
}

// Invoices
export function useInvoices(params?: { status?: string; documentType?: string; clientId?: string; search?: string }) {
  return useQuery({
    queryKey: ['invoices', params ?? {}],
    queryFn: () => api.get<InvoiceListItem[]>(endpoints.invoices.list, { status: params?.status, documentType: params?.documentType, clientId: params?.clientId, search: params?.search }),
    staleTime: 30_000,
  })
}

export function useInvoice(id: string | undefined) {
  return useQuery({
    queryKey: ['invoices', id],
    queryFn: () => api.get<InvoiceDetail>(endpoints.invoices.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateInvoicePayload {
  clientId: string
  invoiceDate: string
  dueDate: string
  notes?: string | null
  currency?: string
  projectId?: string | null
  lines: Array<{ description: string; itemType: string; quantity: number; unit: string; unitRate: number; taxRate: number }>
}

export interface BuildProformaInvoicePayload {
  quotationIds: string[]
  invoiceDate: string
  dueDate: string
  notes?: string | null
  termsAndConditions?: string | null
  currency?: string | null
}

export function useCreateInvoice() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateInvoicePayload) => api.post<InvoiceDetail>(endpoints.invoices.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['invoices'] }),
  })
}

export function useBuildProformaInvoice() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: BuildProformaInvoicePayload) => api.post<InvoiceDetail>(endpoints.invoices.buildProformaFromQuotations, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
  })
}

export function useSendInvoice(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<InvoiceDetail>(endpoints.invoices.send(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', id] })
    },
  })
}

export function useConvertProformaToInvoice(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<InvoiceDetail>(endpoints.invoices.convertToFinalInvoice(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', id] })
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
    },
  })
}

export function useVoidInvoice(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason?: string) => api.post<InvoiceDetail>(endpoints.invoices.void(id), { reason }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', id] })
    },
  })
}

export function useRecordPayment(invoiceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { amount: number; paymentDate: string; method: string; reference?: string | null; notes?: string | null }) =>
      api.post<Payment>(endpoints.invoices.payments(invoiceId), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', invoiceId] })
    },
  })
}

export function useUpdateInvoice(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateInvoicePayload) => api.put<InvoiceDetail>(endpoints.invoices.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', id] })
    },
  })
}

export function useReversePayment(invoiceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (paymentId: string) => api.delete<void>(endpoints.invoices.reversePayment(paymentId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices', invoiceId] })
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
    },
  })
}

export interface PaymentListItem extends Payment {
  invoiceNumber: string | null
  clientName: string | null
  amountAllocated: number
  unallocatedAmount: number
}

export interface PaymentAllocationInput {
  invoiceId: string
  amount: number
}

export interface ReceivePaymentPayload {
  clientId: string
  amount: number
  paymentDate: string
  method: string
  reference?: string | null
  notes?: string | null
  allocations?: PaymentAllocationInput[]
}

/** Receive a payment from a client and allocate it across one or more invoices. */
export function useReceivePayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: ReceivePaymentPayload) => api.post<Payment>(endpoints.finance.receivePayment, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })
}

/** Allocate (or top up) an existing payment's unallocated balance across invoices. */
export function useAllocatePayment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ paymentId, allocations }: { paymentId: string; allocations: PaymentAllocationInput[] }) =>
      api.post<Payment>(endpoints.finance.allocatePayment(paymentId), { allocations }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['payments'] })
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })
}

export interface ClientCreditBalance {
  clientId: string
  clientName: string | null
  currentBalance: number
  availableCredit: number
  currency: string
}

export function useClientCreditBalance(clientId: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'credit-balance', clientId],
    queryFn: () => api.get<ClientCreditBalance>(endpoints.finance.clientCreditBalance(clientId ?? '')),
    enabled: Boolean(clientId),
  })
}

export function useAllPayments(params?: { clientId?: string; from?: string; to?: string }) {
  return useQuery({
    queryKey: ['payments', params ?? {}],
    queryFn: () => api.get<PaymentListItem[]>(endpoints.finance.paymentsList, { clientId: params?.clientId, from: params?.from, to: params?.to }),
    staleTime: 30_000,
  })
}

export interface ClientCreditSummaryRow {
  clientId: string
  clientName: string
  currentBalance: number
  creditLimit: number | null
  availableCredit: number | null
  overdueAmount: number
  oldestInvoiceDate: string | null
  termName: string | null
}

export function useCreditSummaries() {
  return useQuery({
    queryKey: ['finance', 'credit-summaries'],
    queryFn: () => api.get<ClientCreditSummaryRow[]>(endpoints.finance.creditSummaries),
    staleTime: 30_000,
  })
}

export interface RevenueReportRow {
  group: string
  invoiced: number
  collected: number
  outstanding: number
  count: number
}

export function useRevenueReport(params?: { from?: string; to?: string; groupBy?: 'month' | 'client' }) {
  return useQuery({
    queryKey: ['finance', 'reports', 'revenue', params ?? {}],
    queryFn: () => api.get<RevenueReportRow[]>(endpoints.finance.reportsRevenue, { from: params?.from, to: params?.to, groupBy: params?.groupBy }),
    staleTime: 30_000,
  })
}

export interface StatementEntry {
  entryType: 'invoice' | 'payment'
  date: string
  reference: string
  debit: number
  credit: number
  notes: string | null
}

export interface ClientStatement {
  clientId: string
  clientName: string
  from: string | null
  to: string | null
  totalInvoiced: number
  totalPaid: number
  balance: number
  entries: StatementEntry[]
}

export function useClientStatement(params: { clientId: string | undefined; from?: string; to?: string; enabled?: boolean }) {
  return useQuery({
    queryKey: ['finance', 'client-statement', params.clientId ?? '', params.from ?? '', params.to ?? ''],
    queryFn: () => api.get<ClientStatement>(endpoints.finance.clientStatement, { clientId: params.clientId, from: params.from, to: params.to }),
    enabled: Boolean(params.clientId) && (params.enabled ?? true),
  })
}

export function useEmailClientStatement() {
  return useMutation({
    mutationFn: (payload: { clientId: string; from?: string | null; to?: string | null; overdueOnly?: boolean; message?: string | null }) =>
      api.post<{ message?: string }>(endpoints.finance.clientStatementEmail, payload),
  })
}

// Quotations
export function useQuotations(params?: { status?: string; clientId?: string; search?: string }) {
  return useQuery({
    queryKey: ['quotations', params ?? {}],
    queryFn: () => api.get<QuotationListItem[]>(endpoints.quotations.list, { status: params?.status, clientId: params?.clientId, search: params?.search }),
    staleTime: 30_000,
  })
}

export function useQuotation(id: string | undefined) {
  return useQuery({
    queryKey: ['quotations', id],
    queryFn: () => api.get<QuotationDetail>(endpoints.quotations.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreateQuotationPayload {
  clientId: string
  quoteDate: string
  validUntil: string
  subject?: string | null
  notes?: string | null
  currency?: string
  lines: Array<{ description: string; itemType: string; quantity: number; unit: string; unitRate: number; taxRate: number }>
}

export function useCreateQuotation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateQuotationPayload) => api.post<QuotationDetail>(endpoints.quotations.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['quotations'] }),
  })
}

function useQuotationAction(id: string, url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload?: unknown) => api.post<QuotationDetail>(url, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['quotations', id] })
    },
  })
}

export const useSendQuotation = (id: string) => useQuotationAction(id, endpoints.quotations.send(id))
export const useAcceptQuotation = (id: string) => useQuotationAction(id, endpoints.quotations.accept(id))
export const useRejectQuotation = (id: string) => useQuotationAction(id, endpoints.quotations.reject(id))

export function useConvertQuotationToInvoice(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<InvoiceDetail>(endpoints.quotations.convertToInvoice(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['quotations', id] })
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
  })
}

export function useConvertQuotationToWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async () => {
      const workOrder = await api.post<{ id: string; workOrderNumber: string }>(endpoints.workOrders.fromQuote, { quoteId: id })
      return { workOrderId: workOrder.id, workOrderNumber: workOrder.workOrderNumber }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['quotations', id] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    },
  })
}

// Aging
export function useAgingReport() {
  return useQuery({ queryKey: ['finance', 'reports', 'aging'], queryFn: () => api.get<AgingRow[]>(endpoints.finance.reportsAging), staleTime: 30_000 })
}

// Rate cards
export function useFinanceRateCards() {
  return useQuery({ queryKey: ['finance', 'rate-cards'], queryFn: () => api.get<RateCard[]>(endpoints.finance.rateCards), staleTime: 30_000 })
}

/**
 * The tenant's configured tax rate, taken from its default rate card.
 *
 * That card is the only tenant-scoped place this platform stores a rate: the `tenants` table has no
 * tax column, `/api/settings/*` exposes only a `vatNumber` identifier (not a rate), and the stored
 * `defaultTaxRate` on `/api/platform/settings/tax-finance` is behind the `PlatformSettingsAccess`
 * policy and unreadable by a tenant user. `null` therefore means "this workspace has not configured
 * one" — callers must show an editable control rather than substituting a literal.
 */
export function useDefaultTaxRate() {
  const { data, isLoading, isError } = useFinanceRateCards()
  const defaultCard = (data ?? []).find((card) => card.isActive && card.isDefault && card.taxRate != null)
  return { taxRate: defaultCard?.taxRate ?? null, isLoading, isError }
}

export interface UpsertRateCardPayload {
  name: string
  category: string
  description?: string | null
  unitRate: number
  unit: string
  skillLevel?: string | null
  timeType: string
  isDefault: boolean
  taxRate?: number | null
}

export function useCreateRateCard() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertRateCardPayload) => api.post<RateCard>(endpoints.finance.rateCards, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'rate-cards'] }),
  })
}

// Preferential rates
export function usePreferentialRates() {
  return useQuery({ queryKey: ['finance', 'preferential-rates'], queryFn: () => api.get<PreferentialRate[]>(endpoints.finance.preferentialRates), staleTime: 30_000 })
}

export interface UpsertPreferentialRatePayload {
  clientId: string
  rateName: string
  rateType: string
  itemType: string
  unit: string
  defaultRate: number
  preferentialRate: number
  currency: string
  effectiveFrom: string
  effectiveTo?: string | null
  status: string
  requiresApproval: boolean
}

export function useCreatePreferentialRate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertPreferentialRatePayload) => api.post<PreferentialRate>(endpoints.finance.preferentialRates, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'preferential-rates'] }),
  })
}

export function useApprovePreferentialRate(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<PreferentialRate>(endpoints.finance.approvePreferentialRate(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'preferential-rates'] }),
  })
}

// Approval rules (invoice approvals queue is derived from these + pending invoices)
export function useFinanceApprovalRules() {
  return useQuery({ queryKey: ['finance', 'approval-rules'], queryFn: () => api.get<FinanceApprovalRule[]>(endpoints.finance.approvalRules), staleTime: 30_000 })
}

// Credit terms / credit control
export function useCreditTerms() {
  return useQuery({ queryKey: ['finance', 'credit-terms'], queryFn: () => api.get<CreditTerm[]>(endpoints.finance.creditTerms), staleTime: 30_000 })
}

export interface UpsertCreditTermPayload {
  name: string
  code: string
  netDays: number
  isDepositRequired: boolean
  depositPercent: number
  creditLimit?: number | null
  description?: string | null
  isDefault: boolean
}

export function useCreateCreditTerm() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertCreditTermPayload) => api.post<CreditTerm>(endpoints.finance.creditTerms, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'credit-terms'] }),
  })
}

export interface ClientCreditSummary {
  currentBalance: number
  creditLimit: number | null
  availableCredit: number | null
  overdueAmount: number
  oldestInvoiceDate: string | null
  termName: string | null
}

export function useClientCreditSummary(clientId: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'credit-summary', clientId],
    queryFn: () => api.get<ClientCreditSummary>(endpoints.finance.clientCreditSummary(clientId ?? '')),
    enabled: Boolean(clientId),
  })
}
