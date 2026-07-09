import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

// TODO(backend): Only a platform-admin-scoped expenses endpoint exists (PlatformFinanceController.expenses).
// It is NOT tenant-scoped, and reusing it here would leak cross-tenant data, so tenant-level Expenses and
// Payables stay UI-only stubs until a dedicated tenant-scoped endpoint exists (confirmed against
// FRONTEND_BUILD_PLAN.md §12.1). Every hook below returns typed empty/default data.

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
    queryFn: async (): Promise<ExpenseListItem[]> => [],
    staleTime: Infinity,
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
    queryFn: async (): Promise<ExpenseDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
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
    mutationFn: async (_payload: CreateExpensePayload): Promise<ExpenseListItem> => {
      throw new Error('Expense claims cannot be submitted yet — no tenant-scoped Expenses endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'expenses'] }),
  })
}

export function useDecideExpense(_id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_decision: { approve: boolean }): Promise<void> => {
      throw new Error('Expense approvals cannot be persisted yet — no tenant-scoped Expenses endpoint exists.')
    },
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
    queryFn: async (): Promise<PayableListItem[]> => [],
    staleTime: Infinity,
  })
}

export interface PayableDetail extends PayableListItem {
  notes: string | null
  paymentHistory: Array<{ paidAt: string; amount: number; method: string; reference: string | null }>
}

export function usePayable(id: string | undefined) {
  return useQuery({
    queryKey: ['finance', 'payables', id],
    queryFn: async (): Promise<PayableDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
  })
}

export function useRecordPayablePayment(_id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: { amount: number; method: string; reference?: string | undefined }): Promise<void> => {
      throw new Error('Payments cannot be recorded yet — no tenant-scoped Payables endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['finance', 'payables'] }),
  })
}
