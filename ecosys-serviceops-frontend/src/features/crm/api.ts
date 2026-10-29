import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { WorkOrder } from '../work-orders/types'
import type {
  SalesActivity,
  SalesCurrencySettings,
  SalesFollowUpItem,
  SalesOwnerOption,
  SalesTenderDetail,
  SalesTenderListItem,
  TenderStatus,
  UpsertTenderPayload,
  SalesOpportunityDetail,
  SalesOpportunityListItem,
  SalesPipelineSummary,
  SalesStage,
  UpsertOpportunityPayload,
} from './types'

const crmKey = ['crm'] as const

export function usePipelineSummary() {
  return useQuery({ queryKey: [...crmKey, 'summary'], queryFn: () => api.get<SalesPipelineSummary>(endpoints.crm.summary), staleTime: 30_000 })
}

export function useOpportunities(params?: { stage?: string | undefined; search?: string | undefined; clientId?: string | undefined; ownerUserId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'opportunities', params ?? {}],
    queryFn: () => api.get<SalesOpportunityListItem[]>(endpoints.crm.opportunities, { stage: params?.stage, search: params?.search, clientId: params?.clientId, ownerUserId: params?.ownerUserId }),
    staleTime: 30_000,
  })
}

export function useOpportunity(id: string | undefined) {
  return useQuery({
    queryKey: [...crmKey, 'opportunity', id],
    queryFn: () => api.get<SalesOpportunityDetail>(endpoints.crm.opportunity(id ?? '')),
    enabled: Boolean(id),
  })
}

function useInvalidateCrm() {
  const queryClient = useQueryClient()
  return () => void queryClient.invalidateQueries({ queryKey: crmKey })
}

export function useCreateOpportunity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertOpportunityPayload) => api.post<SalesOpportunityDetail>(endpoints.crm.opportunities, payload),
    onSuccess: invalidate,
  })
}

export function useUpdateOpportunity(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertOpportunityPayload) => api.put<SalesOpportunityDetail>(endpoints.crm.opportunity(id), payload),
    onSuccess: invalidate,
  })
}

export function useChangeStage() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: ({ id, stage, lostReason }: { id: string; stage: SalesStage; lostReason?: string | null }) =>
      api.post<SalesOpportunityDetail>(endpoints.crm.stage(id), { stage, lostReason: lostReason ?? null }),
    onSuccess: invalidate,
  })
}

export function useDeleteOpportunity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.crm.opportunity(id)),
    onSuccess: invalidate,
  })
}

export function useAddActivity(opportunityId: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: { type: string; summary: string; dueAt?: string | null }) =>
      api.post<SalesActivity>(endpoints.crm.activities(opportunityId), payload),
    onSuccess: invalidate,
  })
}

export function useFollowUps(params?: { ownerUserId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'follow-ups', params ?? {}],
    queryFn: () => api.get<SalesFollowUpItem[]>(endpoints.crm.followUps, { ownerUserId: params?.ownerUserId }),
    staleTime: 30_000,
  })
}

export function useCompleteActivity() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (activityId: string) => api.post<SalesActivity>(endpoints.crm.completeActivity(activityId)),
    onSuccess: invalidate,
  })
}

export function useConvertToClient(opportunityId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<SalesOpportunityDetail>(endpoints.crm.convertToClient(opportunityId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['clients'] })
    },
  })
}

export function useConvertToQuotation(opportunityId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<{ quotationId: string; quoteNumber: string }>(endpoints.crm.convertToQuotation(opportunityId)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['quotations'] })
    },
  })
}

export interface CreateWorkOrderFromOpportunityPayload {
  opportunityId: string
  title: string | null
  description: string | null
  priority: string
  workType: string
  dueDate: string | null
  siteId: string | null
}

export function useCreateWorkOrderFromOpportunity() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWorkOrderFromOpportunityPayload) => api.post<WorkOrder>(endpoints.crm.createWorkOrder, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
    },
  })
}

// Owners

export function useSalesOwners() {
  return useQuery({ queryKey: [...crmKey, 'owners'], queryFn: () => api.get<SalesOwnerOption[]>(endpoints.crm.owners), staleTime: 60_000 })
}

export function useReassignOpportunityOwner(opportunityId: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (ownerUserId: string) => api.post<SalesOpportunityDetail>(endpoints.crm.opportunityOwner(opportunityId), { ownerUserId }),
    onSuccess: invalidate,
  })
}

// Currency

export function useSalesCurrency() {
  return useQuery({ queryKey: [...crmKey, 'currency'], queryFn: () => api.get<SalesCurrencySettings>(endpoints.crm.currency), staleTime: 30_000 })
}

export function useSetSalesExchangeRate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { currency: string; rate: number }) => api.post<SalesCurrencySettings>(endpoints.crm.exchangeRates, payload),
    onSuccess: () => {
      // Every converted figure in the CRM moves, and Finance shows the same rate history.
      void queryClient.invalidateQueries({ queryKey: crmKey })
      void queryClient.invalidateQueries({ queryKey: ['finance', 'exchange-rates'] })
    },
  })
}

// RFPs & RFQs

export function useTenders(params?: { type?: string | undefined; status?: string | undefined; search?: string | undefined; ownerUserId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'tenders', params ?? {}],
    queryFn: () => api.get<SalesTenderListItem[]>(endpoints.crm.tenders, { type: params?.type, status: params?.status, search: params?.search, ownerUserId: params?.ownerUserId }),
    staleTime: 30_000,
  })
}

export function useTender(id: string | undefined) {
  return useQuery({
    queryKey: [...crmKey, 'tender', id],
    queryFn: () => api.get<SalesTenderDetail>(endpoints.crm.tender(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useCreateTender() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertTenderPayload) => api.post<SalesTenderDetail>(endpoints.crm.tenders, payload),
    onSuccess: invalidate,
  })
}

export function useUpdateTender(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: UpsertTenderPayload) => api.put<SalesTenderDetail>(endpoints.crm.tender(id), payload),
    onSuccess: invalidate,
  })
}

export function useChangeTenderStatus(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: { status: TenderStatus; notes?: string | null; submittedValue?: number | null }) =>
      api.post<SalesTenderDetail>(endpoints.crm.tenderStatus(id), { status: payload.status, notes: payload.notes ?? null, submittedValue: payload.submittedValue ?? null }),
    onSuccess: invalidate,
  })
}

export function useReassignTenderOwner(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (ownerUserId: string) => api.post<SalesTenderDetail>(endpoints.crm.tenderOwner(id), { ownerUserId }),
    onSuccess: invalidate,
  })
}

export function useCreateOpportunityFromTender(id: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: () => api.post<SalesTenderDetail>(endpoints.crm.tenderCreateOpportunity(id)),
    onSuccess: invalidate,
  })
}

export function useDeleteTender() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.crm.tender(id)),
    onSuccess: invalidate,
  })
}

// Tender documents (the shared attachments store)

export interface TenderDocument {
  id: string
  fileName: string
  fileSize: number
  mimeType: string
  publicUrl: string
  uploadedByUserId: string
  createdAt: string
}

const tenderDocumentsKey = (tenderId: string) => ['attachments', 'SalesTender', tenderId] as const

export function useTenderDocuments(tenderId: string) {
  return useQuery({
    queryKey: tenderDocumentsKey(tenderId),
    queryFn: () => api.get<TenderDocument[]>(endpoints.crm.documents(tenderId)),
    enabled: Boolean(tenderId),
  })
}

export function useUploadTenderDocument(tenderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (file: File) => {
      const form = new FormData()
      form.append('entityType', 'SalesTender')
      form.append('entityId', tenderId)
      form.append('file', file)
      return api.post<TenderDocument>(endpoints.crm.uploadDocument, form)
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: tenderDocumentsKey(tenderId) }),
  })
}

export function useDeleteTenderDocument(tenderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (attachmentId: string) => api.delete<void>(endpoints.crm.deleteDocument(attachmentId)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: tenderDocumentsKey(tenderId) }),
  })
}
