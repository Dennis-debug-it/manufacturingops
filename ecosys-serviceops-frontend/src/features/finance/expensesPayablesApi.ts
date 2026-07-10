import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

export type ExpenseStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Reimbursed'

export interface ExpenseListItem {
  id: string
  claimNumber: string
  submittedByName: string
  category: string
  amount: number
  currency: string
  incurredAt: string
  status: ExpenseStatus
}

export function useExpenses(status?: ExpenseStatus | 'All') {
  return useQuery({
    queryKey: ['finance', 'expenses', status ?? 'All'],
    queryFn: () => api.get<ExpenseListItem[]>('/api/expenses', status && status !== 'All' ? { status } : undefined),
    staleTime: 30_000,
  })
}

export interface ExpenseDetail extends ExpenseListItem {
  description: string | null
  receiptUrl: string | null
  approvedByName: string | null
  approvedAt: string | null
}

export function useExpense(id: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'expenses', id],
    queryFn: () => api.get<ExpenseDetail>(`/api/expenses/${id}`),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}

export interface CreateExpensePayload {
  category: string
  amount: number
  incurredAt: string
  description?: string | undefined
}

export function useCreateExpense() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateExpensePayload) => api.post<ExpenseListItem>('/api/expenses', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'expenses'] }),
  })
}

export function useDecideExpense(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (decision: { approve: boolean }) =>
      api.post<void>(`/api/expenses/${id}/decision`, decision),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'expenses'] }),
  })
}

export type PayableStatus = 'Open' | 'PartiallyPaid' | 'Paid' | 'Overdue'

export interface PayableListItem {
  id: string
  billNumber: string
  supplierName: string
  amount: number
  amountPaid: number
  currency: string
  dueDate: string
  status: PayableStatus
}

export function usePayables(status?: PayableStatus | 'All') {
  return useQuery({
    queryKey: ['finance', 'payables', status ?? 'All'],
    queryFn: () => api.get<PayableListItem[]>('/api/payables', status && status !== 'All' ? { status } : undefined),
    staleTime: 30_000,
  })
}

export interface PayableDetail extends PayableListItem {
  notes: string | null
  paymentHistory: Array<{ paidAt: string; amount: number; method: string; reference: string | null }>
}

export function usePayable(id: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'payables', id],
    queryFn: () => api.get<PayableDetail>(`/api/payables/${id}`),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}

export interface CreatePayablePayload {
  supplierId: string
  amount: number
  dueDate: string
  notes?: string | undefined
}

export function useCreatePayable() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePayablePayload) => api.post<PayableListItem>('/api/payables', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'payables'] }),
  })
}

export function useRecordPayablePayment(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { amount: number; method: string; reference?: string | undefined }) =>
      api.post<void>(`/api/payables/${id}/payments`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'payables'] }),
  })
}
