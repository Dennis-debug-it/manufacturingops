import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { CreditNoteDetail, CreditNoteListItem } from './types'

export interface CreditNoteLinePayload {
  description: string
  itemType: string
  quantity: number
  unit: string
  unitRate: number
  taxRate: number
}

export interface CreateCreditNotePayload {
  clientId: string
  invoiceId?: string | null
  reason: string
  issueDate: string
  notes?: string | null
  currency?: string
  lines: CreditNoteLinePayload[]
}

export interface ApplyCreditNotePayload {
  allocations: Array<{ invoiceId: string; amount: number }>
}

export function useCreditNotes(params?: { status?: string; clientId?: string; search?: string }) {
  return useQuery({
    queryKey: ['credit-notes', params ?? {}],
    queryFn: () => api.get<CreditNoteListItem[]>(endpoints.creditNotes.list, { status: params?.status, clientId: params?.clientId, search: params?.search }),
    staleTime: 30_000,
  })
}

export function useCreditNote(id: string | undefined) {
  return useQuery({
    queryKey: ['credit-notes', id],
    queryFn: () => api.get<CreditNoteDetail>(endpoints.creditNotes.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useCreateCreditNote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateCreditNotePayload) => api.post<CreditNoteDetail>(endpoints.creditNotes.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['credit-notes'] }),
  })
}

/** Invalidates the credit note list, this note's detail, and any invoices/balances it touches. */
function useCreditNoteAction<T>(id: string, run: (payload: T) => Promise<CreditNoteDetail>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['credit-notes'] })
      void queryClient.invalidateQueries({ queryKey: ['credit-notes', id] })
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['finance'] })
    },
  })
}

export const useIssueCreditNote = (id: string) =>
  useCreditNoteAction<void>(id, () => api.post<CreditNoteDetail>(endpoints.creditNotes.issue(id)))

export const useVoidCreditNote = (id: string) =>
  useCreditNoteAction<string | undefined>(id, (reason) => api.post<CreditNoteDetail>(endpoints.creditNotes.void(id), { reason }))

export const useApplyCreditNote = (id: string) =>
  useCreditNoteAction<ApplyCreditNotePayload>(id, (payload) => api.post<CreditNoteDetail>(endpoints.creditNotes.apply(id), payload))
