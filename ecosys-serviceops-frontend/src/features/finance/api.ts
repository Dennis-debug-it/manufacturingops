import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  FinanceApprovalRule,
  Payment,
  PreferentialRate,
  RateCard,
} from './types'

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

export interface ClientCreditBalance {
  clientId: string
  clientName: string | null
  currentBalance: number
  availableCredit: number
  currency: string
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

export interface RevenueReportRow {
  group: string
  invoiced: number
  collected: number
  outstanding: number
  count: number
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

export interface CreateQuotationPayload {
  clientId: string
  quoteDate: string
  validUntil: string
  subject?: string | null
  notes?: string | null
  currency?: string
  lines: Array<{ description: string; itemType: string; quantity: number; unit: string; unitRate: number; taxRate: number }>
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

export interface ClientCreditSummary {
  currentBalance: number
  creditLimit: number | null
  availableCredit: number | null
  overdueAmount: number
  oldestInvoiceDate: string | null
  termName: string | null
}
