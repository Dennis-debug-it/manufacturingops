import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { WorkOrder } from '../work-orders/types'
import type {
  SalesActivity,
  SalesCurrencySettings,
  SalesFollowUpItem,
  SalesQuoteAwaitingAnswer,
  SalesOwnerOption,
  SalesTenderDetail,
  SalesTenderListItem,
  TenderStatus,
  UpsertTenderPayload,
  SalesOpportunityDetail,
  SalesOpportunityListItem,
  SalesPipelineSummary,
  SalesReport,
  SalesTargets,
  SalesTargetRow,
  SalesAssignmentRule,
  UpsertAssignmentRulePayload,
  SalesSettings,
  SalesStage,
  SalesPipeline,
  UpsertPipelinePayload,
  DealFieldsResponse,
  UpsertDealFieldPayload,
  UpsertOpportunityPayload,
} from './types'

const crmKey = ['crm'] as const

export function usePipelineSummary(pipelineId?: string | undefined) {
  return useQuery({
    queryKey: [...crmKey, 'summary', pipelineId ?? null],
    queryFn: () => api.get<SalesPipelineSummary>(endpoints.crm.summary, { pipelineId }),
    staleTime: 30_000,
  })
}

export function useOpportunities(params?: { stage?: string | undefined; search?: string | undefined; clientId?: string | undefined; ownerUserId?: string | undefined; pipelineId?: string | undefined; field?: string | undefined; fieldValue?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'opportunities', params ?? {}],
    queryFn: () => api.get<SalesOpportunityListItem[]>(endpoints.crm.opportunities, { stage: params?.stage, search: params?.search, clientId: params?.clientId, ownerUserId: params?.ownerUserId, pipelineId: params?.pipelineId, field: params?.field, fieldValue: params?.fieldValue }),
    staleTime: 30_000,
  })
}

/** Active pipelines the plan includes (admins get every pipeline, with `available` and `isActive` to tell them apart). */
export function usePipelines() {
  return useQuery({ queryKey: [...crmKey, 'pipelines'], queryFn: () => api.get<SalesPipeline[]>(endpoints.crm.pipelines), staleTime: 5 * 60_000 })
}

export function useSavePipeline() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: UpsertPipelinePayload }) =>
      id ? api.put<SalesPipeline[]>(endpoints.crm.pipeline(id), payload) : api.post<SalesPipeline[]>(endpoints.crm.pipelines, payload),
    onSuccess: invalidate,
  })
}

export function useDeletePipeline() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<SalesPipeline[]>(endpoints.crm.pipeline(id)),
    onSuccess: invalidate,
  })
}

export function useDealFields() {
  return useQuery({ queryKey: [...crmKey, 'fields'], queryFn: () => api.get<DealFieldsResponse>(endpoints.crm.fields), staleTime: 5 * 60_000 })
}

export function useSaveDealField() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: ({ id, payload }: { id?: string | undefined; payload: UpsertDealFieldPayload }) =>
      id ? api.put<DealFieldsResponse>(endpoints.crm.field(id), payload) : api.post<DealFieldsResponse>(endpoints.crm.fields, payload),
    onSuccess: invalidate,
  })
}

export function useDeleteDealField() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<DealFieldsResponse>(endpoints.crm.field(id)),
    onSuccess: invalidate,
  })
}

export function useSalesReport(params: { from: string; to: string; ownerUserId?: string | undefined; pipelineId?: string | undefined }, options?: { enabled?: boolean }) {
  return useQuery({
    queryKey: [...crmKey, 'report', params],
    enabled: options?.enabled ?? true,
    queryFn: () => api.get<SalesReport>(endpoints.crm.reports, { from: params.from, to: params.to, ownerUserId: params.ownerUserId, pipelineId: params.pipelineId }),
    staleTime: 60_000,
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

export function useQuotesAwaitingAnswer(params?: { ownerUserId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'quotes-awaiting-answer', params ?? {}],
    queryFn: () => api.get<SalesQuoteAwaitingAnswer[]>(endpoints.crm.quotesAwaitingAnswer, { ownerUserId: params?.ownerUserId }),
    staleTime: 30_000,
  })
}

export function useAddDealContact(opportunityId: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: { contactId?: string | null; fullName?: string | null; email?: string | null; phone?: string | null; jobTitle?: string | null; role?: string | null }) =>
      api.post<SalesOpportunityDetail>(endpoints.crm.dealContacts(opportunityId), payload),
    onSuccess: invalidate,
  })
}

export function useRemoveDealContact(opportunityId: string) {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (contactId: string) => api.delete<SalesOpportunityDetail>(endpoints.crm.dealContact(opportunityId, contactId)),
    onSuccess: invalidate,
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

export function useSalesSettings() {
  return useQuery({ queryKey: [...crmKey, 'settings'], queryFn: () => api.get<SalesSettings>(endpoints.crm.settings), staleTime: 60_000 })
}

export function useUpdateSalesSettings() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: Pick<SalesSettings, 'defaultOwnerUserId' | 'dealVisibility' | 'createRenewalDeals' | 'stalledAfterDays' | 'sendWeeklySummary'>) => api.put<SalesSettings>(endpoints.crm.settings, payload),
    onSuccess: invalidate,
  })
}

export function useAssignmentRules() {
  return useQuery({ queryKey: [...crmKey, 'assignment-rules'], queryFn: () => api.get<SalesAssignmentRule[]>(endpoints.crm.assignmentRules) })
}

export function useSaveAssignmentRule() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: ({ id, ...payload }: UpsertAssignmentRulePayload & { id?: string }) =>
      id ? api.put<SalesAssignmentRule[]>(endpoints.crm.assignmentRule(id), payload) : api.post<SalesAssignmentRule[]>(endpoints.crm.assignmentRules, payload),
    onSuccess: invalidate,
  })
}

export function useDeleteAssignmentRule() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (id: string) => api.delete<SalesAssignmentRule[]>(endpoints.crm.assignmentRule(id)),
    onSuccess: invalidate,
  })
}

export function useSalesTargets(year: number, enabled = true) {
  return useQuery({ queryKey: [...crmKey, 'targets', year], queryFn: () => api.get<SalesTargets>(endpoints.crm.targets, { year }), enabled })
}

export function useSaveSalesTargets() {
  const invalidate = useInvalidateCrm()
  return useMutation({
    mutationFn: (payload: { year: number; targets: SalesTargetRow[] }) => api.put<SalesTargets>(endpoints.crm.targets, payload),
    onSuccess: invalidate,
  })
}

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

export function useTenders(params?: { type?: string | undefined; status?: string | undefined; search?: string | undefined; ownerUserId?: string | undefined; clientId?: string | undefined }) {
  return useQuery({
    queryKey: [...crmKey, 'tenders', params ?? {}],
    queryFn: () => api.get<SalesTenderListItem[]>(endpoints.crm.tenders, { type: params?.type, status: params?.status, search: params?.search, ownerUserId: params?.ownerUserId, clientId: params?.clientId }),
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
