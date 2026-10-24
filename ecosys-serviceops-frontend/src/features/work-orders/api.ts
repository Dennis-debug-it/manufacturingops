import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { usePermission } from '../../shared/hooks/usePermission'
import type { PagedResult } from '../../shared/types/pagination'
import type {
  ReportContent,
  RelatedWorkOrders,
  RepeatVisitCheck,
  WorkOrder,
  WorkOrderAssignmentHistoryItem,
  WorkOrderChecklistItem,
  WorkOrderDocument,
  WorkOrderEvent,
  WorkOrderExecutionBundle,
  WorkOrderNote,
  WorkOrderServiceReportPreview,
  WorkOrderSignature,
  WorkOrderSlaStatus,
  WorkOrderTemplate,
} from './types'

export interface WorkOrderListFilters {
  status?: string
  workType?: string
  technicianId?: string
  assignmentGroupId?: string
  unassigned?: boolean
  state?: string
  search?: string
  fromDate?: string
  toDate?: string
}

const workOrdersKey = (filters?: WorkOrderListFilters) => ['work-orders', filters ?? {}] as const

export function useWorkOrders(filters?: WorkOrderListFilters, options: { enabled?: boolean } | boolean = true) {
  return useQuery({
    enabled: typeof options === 'boolean' ? options : options.enabled ?? true,
    queryKey: workOrdersKey(filters),
    queryFn: () =>
      api.get<WorkOrder[]>(endpoints.workOrders.list, {
        status: filters?.status,
        workType: filters?.workType,
        technicianId: filters?.technicianId,
        assignmentGroupId: filters?.assignmentGroupId,
        unassigned: filters?.unassigned,
        search: filters?.search,
        fromDate: filters?.fromDate,
        toDate: filters?.toDate,
      }),
    staleTime: 30_000,
  })
}

export function useWorkOrdersPaged(filters: WorkOrderListFilters, page: number, pageSize: number, options?: { enabled?: boolean }) {
  return useQuery({ queryKey: ['work-orders', 'paged', filters, page, pageSize], queryFn: () => api.get<PagedResult<WorkOrder>>(endpoints.workOrders.list, { ...filters, page, pageSize }), enabled: options?.enabled ?? true, staleTime: 30_000, placeholderData: keepPreviousData })
}

export function useWorkOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id],
    queryFn: () => api.get<WorkOrder>(endpoints.workOrders.detail(id ?? '')),
    enabled: Boolean(id),
    staleTime: 15_000,
  })
}

export function useWorkOrderSla(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'sla'],
    queryFn: () => api.get<WorkOrderSlaStatus>(endpoints.workOrders.sla(id ?? '')),
    enabled: Boolean(id),
    staleTime: 15_000,
  })
}

export interface CreateWorkOrderPayload {
  clientId: string
  branchId?: string | null
  siteId?: string | null
  systemId?: string | null
  assetId?: string | null
  assignmentGroupId?: string | null
  assignmentType?: string | null
  assignedTechnicianId?: string | null
  assignedTechnicianIds?: string[] | null
  leadTechnicianId?: string | null
  assignmentNotes?: string | null
  title: string
  description?: string | null
  priority: string
  dueDate?: string | null
  workType: string
  isPreventiveMaintenance: boolean
  pmTemplateId?: string | null
  notifyClient?: boolean
  clientContactEmail?: string | null
  templateId?: string | null
  parentWorkOrderId?: string | null
  followUpOfWorkOrderId?: string | null
  projectId?: string | null
}

export interface CompleteWorkOrderPayload {
  workDoneNotes: string
  completedAt?: string | null
  technicianId?: string | null
  assignmentGroupId?: string | null
  reportSummary?: string | null
  answersJson?: string | null
  adminOverride?: boolean
  overrideReason?: string | null
}

function invalidateWorkOrders(queryClient: ReturnType<typeof useQueryClient>, id?: string) {
  void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  if (id) {
    void queryClient.invalidateQueries({ queryKey: ['work-orders', id] })
  }
}

export function useCreateWorkOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWorkOrderPayload) => api.post<WorkOrder>(endpoints.workOrders.create, payload),
    onSuccess: () => invalidateWorkOrders(queryClient),
  })
}

export function useAssignWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { technicianId?: string | null; assignmentGroupId?: string | null; notes?: string | null }) =>
      api.post<WorkOrder>(endpoints.workOrders.assign(id), payload),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export function useStartWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<WorkOrder>(endpoints.workOrders.start(id)),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export function useCompleteWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CompleteWorkOrderPayload) =>
      api.post<WorkOrder>(endpoints.workOrders.complete(id), payload),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export function useAcknowledgeWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { acknowledgedByName: string; comments?: string | null }) =>
      api.post<WorkOrder>(endpoints.workOrders.acknowledge(id), payload),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

// Timeline (events + comments)
export function useWorkOrderEvents(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'events'],
    queryFn: () => api.get<WorkOrderEvent[]>(endpoints.workOrders.events(id ?? '')),
    enabled: Boolean(id),
    refetchInterval: 30_000,
  })
}

export function useAddWorkOrderComment(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (message: string) => api.post<WorkOrderEvent>(endpoints.workOrders.comments(id), { message }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'events'] })
    },
  })
}

// Notes (internal vs client-visible)
export function useWorkOrderNotes(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'notes'],
    queryFn: () => api.get<WorkOrderNote[]>(endpoints.workOrders.notes(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useAddWorkOrderNote(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { content: string; isClientVisible: boolean }) =>
      api.post<WorkOrderNote>(endpoints.workOrders.notes(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'notes'] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'events'] })
    },
  })
}

// Related work orders (same site / same asset)
export function useRelatedWorkOrders(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'related'],
    queryFn: () => api.get<RelatedWorkOrders>(endpoints.workOrders.related(id ?? '')),
    enabled: Boolean(id),
  })
}

// Linked documents (quote, service report, invoices, material requests)
export function useWorkOrderDocuments(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'documents'],
    queryFn: () => api.get<WorkOrderDocument[]>(endpoints.workOrders.documents(id ?? '')),
    enabled: Boolean(id),
  })
}

// Repeat visit check
export function useRepeatVisitCheck(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'repeat-visit-check'],
    queryFn: () => api.get<RepeatVisitCheck>(endpoints.workOrders.repeatVisitCheck(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useRepeatVisitCheckForAsset(assetId: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', 'repeat-visit-check', assetId],
    queryFn: () => api.get<RepeatVisitCheck>(endpoints.workOrders.repeatVisitCheckForAsset, { assetId }),
    enabled: Boolean(assetId),
  })
}

// Assignment
export function useAssignmentHistory(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'assignment-history'],
    queryFn: () => api.get<WorkOrderAssignmentHistoryItem[]>(endpoints.workOrders.assignmentHistory(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useReassignWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { assignmentGroupId?: string | null; technicianIds?: string[] | null; leadTechnicianId?: string | null; notes?: string | null }) =>
      api.post<WorkOrder>(endpoints.workOrders.reassign(id), payload),
    onSuccess: () => {
      invalidateWorkOrders(queryClient, id)
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'assignment-history'] })
    },
  })
}

/**
 * Technician dispatch lifecycle (WorkOrderAssignmentsController + WorkOrderExecutionController).
 *
 * These write `work_order_technician_assignments`, which is what the Start and Complete guards
 * read — `POST /{id}/arrive` on WorkOrdersController writes `work_orders.ArrivalAt` instead and
 * does *not* satisfy the completion guard, so the arrival milestone goes through
 * `POST /{id}/arrival` here.
 *
 * Every mutation carries the work order id in its variables: the same actions are taken from the
 * work-order detail page and from the dispatcher's daily-route list, where the target work order
 * isn't known until the user acts.
 */
export type TechnicianDispatchResponse = 'Accepted' | 'Declined'

/** `TechnicianResponseActionRequest(Guid TechnicianId, string Response, string? Notes)`. */
export interface TechnicianResponseVariables {
  id: string
  technicianId: string
  response: TechnicianDispatchResponse
  notes?: string | null
}

/** `MarkInTransitRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? InTransitAt, string? Notes)`. */
export interface MarkInTransitVariables {
  id: string
  technicianId: string
  latitude?: number | null
  longitude?: number | null
  inTransitAt?: string | null
  notes?: string | null
}

/** `ArrivalActionRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? ArrivedAt)`. */
export interface RecordArrivalVariables {
  id: string
  technicianId: string
  latitude?: number | null
  longitude?: number | null
  arrivedAt?: string | null
}

/** `DepartureActionRequest(Guid TechnicianId, decimal? Latitude, decimal? Longitude, DateTime? DepartedAt)`. */
export interface RecordDepartureVariables {
  id: string
  technicianId: string
  latitude?: number | null
  longitude?: number | null
  departedAt?: string | null
}

function invalidateDispatchState(queryClient: ReturnType<typeof useQueryClient>, id: string) {
  invalidateWorkOrders(queryClient, id)
  void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'visits'] })
  void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'assignment-history'] })
  void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'events'] })
  void queryClient.invalidateQueries({ queryKey: ['dispatch'] })
}

/** One trip to site: travel start → arrival → departure. A multi-day job has one per day/trip. */
export interface WorkOrderVisit {
  id: string
  technicianId: string
  technicianName: string
  travelStartedAt: string | null
  arrivedAt: string | null
  departedAt: string | null
  travelMinutes: number
  onSiteMinutes: number
  notes: string | null
}

/** `Idle` → en route → `Travelling` → arrival → `OnSite` → departure → `Idle` (next trip). */
export type VisitState = 'Idle' | 'Travelling' | 'OnSite'

export interface WorkOrderVisitTechnicianSummary {
  technicianId: string
  technicianName: string
  state: VisitState
  visitCount: number
  daysOnJob: number
  travelMinutes: number
  onSiteMinutes: number
}

export interface WorkOrderVisits {
  visits: WorkOrderVisit[]
  technicians: WorkOrderVisitTechnicianSummary[]
  totalTravelMinutes: number
  totalOnSiteMinutes: number
}

export function useWorkOrderVisits(id: string) {
  return useQuery({
    queryKey: ['work-orders', id, 'visits'],
    queryFn: () => api.get<WorkOrderVisits>(endpoints.workOrders.visits(id)),
    enabled: Boolean(id),
  })
}

export function useRecordTechnicianResponse() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, technicianId, response, notes }: TechnicianResponseVariables) =>
      api.post<WorkOrder>(endpoints.workOrders.technicianResponse(id), { technicianId, response, notes: notes ?? null }),
    onSuccess: (_data, variables) => invalidateDispatchState(queryClient, variables.id),
  })
}

export function useMarkTechnicianInTransit() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, technicianId, latitude, longitude, inTransitAt, notes }: MarkInTransitVariables) =>
      api.post<WorkOrder>(endpoints.workOrders.inTransit(id), {
        technicianId,
        latitude: latitude ?? null,
        longitude: longitude ?? null,
        inTransitAt: inTransitAt ?? null,
        notes: notes ?? null,
      }),
    onSuccess: (_data, variables) => invalidateDispatchState(queryClient, variables.id),
  })
}

export function useRecordTechnicianArrival() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, technicianId, latitude, longitude, arrivedAt }: RecordArrivalVariables) =>
      api.post<WorkOrder>(endpoints.workOrders.arrival(id), {
        technicianId,
        latitude: latitude ?? null,
        longitude: longitude ?? null,
        arrivedAt: arrivedAt ?? null,
      }),
    onSuccess: (_data, variables) => invalidateDispatchState(queryClient, variables.id),
  })
}

export function useRecordTechnicianDeparture() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, technicianId, latitude, longitude, departedAt }: RecordDepartureVariables) =>
      api.post<WorkOrder>(endpoints.workOrders.departure(id), {
        technicianId,
        latitude: latitude ?? null,
        longitude: longitude ?? null,
        departedAt: departedAt ?? null,
      }),
    onSuccess: (_data, variables) => invalidateDispatchState(queryClient, variables.id),
  })
}

// Checklist
export function useChecklist(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'checklist'],
    queryFn: () => api.get<WorkOrderChecklistItem[]>(endpoints.workOrders.checklist(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useUpdateChecklistItem(workOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { itemId: string; responseValue?: string | null; remarks?: string | null; isCompleted?: boolean | null }) =>
      api.put<WorkOrderChecklistItem>(endpoints.workOrders.checklistItem(workOrderId, payload.itemId), {
        responseValue: payload.responseValue,
        remarks: payload.remarks,
        isCompleted: payload.isCompleted,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-orders', workOrderId, 'checklist'] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders', workOrderId, 'execution'] })
    },
  })
}

// Execution bundle (notes, photos, materials, signatures, progress updates, report preview)
export function useExecutionBundle(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'execution'],
    queryFn: () => api.get<WorkOrderExecutionBundle>(endpoints.workOrders.executionBundle(id ?? '')),
    enabled: Boolean(id),
  })
}

function invalidateExecution(queryClient: ReturnType<typeof useQueryClient>, id: string) {
  void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'execution'] })
}

export function useSaveExecutionNotes(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { findings?: string | null; workDone?: string | null; progressComment?: string | null }) =>
      api.post<WorkOrderExecutionBundle>(endpoints.workOrders.executionNotes(id), payload),
    onSuccess: () => invalidateExecution(queryClient, id),
  })
}

export function useUploadPhoto(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { file: File; caption?: string; category?: string }) => {
      const formData = new FormData()
      formData.append('file', payload.file)
      if (payload.caption) formData.append('caption', payload.caption)
      if (payload.category) formData.append('category', payload.category)
      return api.post<WorkOrderExecutionBundle>(endpoints.workOrders.photos(id), formData)
    },
    onSuccess: () => invalidateExecution(queryClient, id),
  })
}

export function useAddMaterialUsage(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { materialItemId: string; quantityUsed: number; assetId?: string | null; chargeable?: boolean; notes?: string | null }) =>
      api.post<WorkOrderExecutionBundle>(endpoints.workOrders.materialsUsed(id), {
        materialItemId: payload.materialItemId,
        assetId: payload.assetId ?? null,
        quantityUsed: payload.quantityUsed,
        unitCost: null,
        chargeable: payload.chargeable ?? true,
        notes: payload.notes ?? null,
        usedAt: null,
      }),
    onSuccess: () => {
      invalidateExecution(queryClient, id)
      // Logging usage issues stock from the branch.
      void queryClient.invalidateQueries({ queryKey: ['picker', 'materials'] })
    },
  })
}

export function useUpdatePhoto(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { photoId: string; caption?: string | null; category?: string | null; includeInReport: boolean }) =>
      api.put<WorkOrderExecutionBundle>(endpoints.workOrders.photo(id, payload.photoId), {
        caption: payload.caption,
        category: payload.category,
        includeInReport: payload.includeInReport,
      }),
    onSuccess: () => {
      invalidateExecution(queryClient, id)
      // Captions and "include in report" show in the service report.
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'service-report'] })
    },
  })
}

export function useCaptureSignature(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { signatureType: string; signerName: string; signerRole?: string | null; signatureDataUrl: string; comment?: string | null }) =>
      api.post<WorkOrderExecutionBundle>(endpoints.workOrders.signatures(id), payload),
    onSuccess: () => invalidateExecution(queryClient, id),
  })
}

export function useSignatures(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'signatures'],
    queryFn: () => api.get<WorkOrderSignature[]>(endpoints.workOrders.signatures(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useDeleteSignature(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (signatureId: string) => api.delete<void>(endpoints.workOrders.signature(id, signatureId)),
    onSuccess: () => {
      invalidateExecution(queryClient, id)
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'signatures'] })
    },
  })
}

// Service report
/** Which report a job produces: a client's own layout (with a Word download and photo captions to use) or the standard one. */
export interface ServiceReportLayout {
  layout: string | null
  wordAvailable: boolean
  photoCaptions: string[]
}

export function useServiceReportLayout(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'service-report-layout'],
    queryFn: () => api.get<ServiceReportLayout>(endpoints.workOrders.serviceReportLayout(id ?? ''), undefined, { suppressGlobalErrorToast: true }),
    enabled: Boolean(id),
    staleTime: 10 * 60_000,
    retry: false,
  })
}

export function useServiceReport(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'service-report'],
    queryFn: () => api.get<WorkOrderServiceReportPreview>(endpoints.workOrders.serviceReport(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useGenerateServiceReport(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<WorkOrderServiceReportPreview>(endpoints.workOrders.generateServiceReport(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'service-report'] })
      invalidateExecution(queryClient, id)
    },
  })
}

export function useReportContent(id: string) {
  return useQuery({
    queryKey: ['work-orders', id, 'report-content'],
    queryFn: () => api.get<ReportContent>(endpoints.workOrders.reportContent(id)),
  })
}

export function useUpdateReportContent(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { reportedProblem: string | null; findings: string | null; workDone: string | null }) =>
      api.put<ReportContent>(endpoints.workOrders.reportContent(id), payload),
    onSuccess: (content) => {
      queryClient.setQueryData(['work-orders', id, 'report-content'], content)
      void queryClient.invalidateQueries({ queryKey: ['work-orders', id, 'service-report'] })
      void queryClient.invalidateQueries({ queryKey: ['work-orders', 'report-queue'] })
    },
  })
}

// Lifecycle transitions
function useLifecycleAction(id: string, url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload?: Record<string, unknown>) => api.post<WorkOrder>(url, payload),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export const usePauseWorkOrder = (id: string) => useLifecycleAction(id, endpoints.workOrders.pause(id))
export const useResumeWorkOrder = (id: string) => useLifecycleAction(id, endpoints.workOrders.resume(id))
export const useCloseWorkOrder = (id: string) => useLifecycleAction(id, endpoints.workOrders.close(id))
export const useCancelWorkOrder = (id: string) => useLifecycleAction(id, endpoints.workOrders.cancel(id))

// View-switcher (Kanban/Calendar/Map) and bulk-action support
export function usePatchWorkOrderStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: string }) =>
      api.patch<WorkOrder>(endpoints.workOrders.patchStatus(id), { status }),
    onSuccess: (_data, variables) => invalidateWorkOrders(queryClient, variables.id),
  })
}

/** Admin action: send a completed or closed job back for more work (status "Reopened"). */
export function useReopenWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.patch<WorkOrder>(endpoints.workOrders.patchStatus(id), { status: 'Reopened' }),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export function usePatchWorkOrderWorkflowStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, workflowStatusId }: { id: string; workflowStatusId: string }) =>
      api.patch<WorkOrder>(endpoints.workOrders.patchStatus(id), { workflowStatusId }),
    onSuccess: (_data, variables) => invalidateWorkOrders(queryClient, variables.id),
  })
}

export interface BulkOperationFailure {
  workOrderId: string
  reason: string
}

export interface BulkOperationResult {
  succeeded: string[]
  failed: BulkOperationFailure[]
}

export function useBulkAssignWorkOrders() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { workOrderIds: string[]; technicianId: string; notes?: string | null }) =>
      api.post<BulkOperationResult>(endpoints.workOrders.bulkAssign, payload),
    onSuccess: () => invalidateWorkOrders(queryClient),
  })
}

export function useBulkCloseWorkOrders() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { workOrderIds: string[] }) =>
      api.post<BulkOperationResult>(endpoints.workOrders.bulkClose, payload),
    onSuccess: () => invalidateWorkOrders(queryClient),
  })
}

// Id-agnostic lifecycle mutations for list-level views (Kanban cards, table row quick actions)
// where the target work order isn't known until the user acts, so a fixed-id hook won't do.
export function useAssignWorkOrderById() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, technicianId, notes }: { id: string; technicianId: string; notes?: string | null }) =>
      api.post<WorkOrder>(endpoints.workOrders.assign(id), { technicianId, notes }),
    onSuccess: (_data, variables) => invalidateWorkOrders(queryClient, variables.id),
  })
}

export function useStartWorkOrderById() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<WorkOrder>(endpoints.workOrders.start(id)),
    onSuccess: (_data, id) => invalidateWorkOrders(queryClient, id),
  })
}

export function useCompleteWorkOrderById() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...payload }: { id: string } & CompleteWorkOrderPayload) =>
      api.post<WorkOrder>(endpoints.workOrders.complete(id), payload),
    onSuccess: (_data, variables) => invalidateWorkOrders(queryClient, variables.id),
  })
}

export function useCloseWorkOrderById() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<WorkOrder>(endpoints.workOrders.close(id)),
    onSuccess: (_data, id) => invalidateWorkOrders(queryClient, id),
  })
}

// Duplicate
export function useDuplicateWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<WorkOrder>(endpoints.workOrders.duplicate(id)),
    onSuccess: () => invalidateWorkOrders(queryClient),
  })
}

// Convert quote -> work order
export function useCreateWorkOrderFromQuote() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (quoteId: string) => api.post<WorkOrder>(endpoints.workOrders.fromQuote, { quoteId }),
    onSuccess: () => invalidateWorkOrders(queryClient),
  })
}

// Sub-tasks / child work orders
export function useWorkOrderChildren(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id, 'children'],
    queryFn: () => api.get<WorkOrder[]>(endpoints.workOrders.children(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useCreateSubtask(parentWorkOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateWorkOrderPayload) =>
      api.post<WorkOrder>(endpoints.workOrders.create, { ...payload, parentWorkOrderId }),
    onSuccess: () => {
      invalidateWorkOrders(queryClient, parentWorkOrderId)
      void queryClient.invalidateQueries({ queryKey: ['work-orders', parentWorkOrderId, 'children'] })
    },
  })
}

// Hold / resume (waiting states)
export function useHoldWorkOrder(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { holdType: string; reason?: string | null }) =>
      api.patch<WorkOrder>(endpoints.workOrders.hold(id), payload),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

export function useResumeFromHold(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.patch<WorkOrder>(endpoints.workOrders.resume(id)),
    onSuccess: () => invalidateWorkOrders(queryClient, id),
  })
}

// Work order templates
export function useWorkOrderTemplates(params?: { jobType?: string; search?: string; activeOnly?: boolean }) {
  const canCreate = usePermission('work-orders.create')
  return useQuery({
    queryKey: ['work-order-templates', params ?? {}],
    queryFn: () =>
      api.get<WorkOrderTemplate[]>(endpoints.workOrderTemplates.list, {
        jobType: params?.jobType,
        search: params?.search,
        activeOnly: params?.activeOnly,
      }),
    staleTime: 30_000,
    // The API only lists templates to people who can create work orders.
    enabled: canCreate,
  })
}

export function useWorkOrderTemplate(id: string | undefined) {
  return useQuery({
    queryKey: ['work-order-templates', id],
    queryFn: () => api.get<WorkOrderTemplate>(endpoints.workOrderTemplates.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface UpsertWorkOrderTemplatePayload {
  name: string
  jobType: string
  description?: string | null
  estimatedDurationMins?: number | null
  defaultPriority: string
  requiredSkills?: string[] | null
  checklistTemplateId?: string | null
  defaultMaterials?: Array<{ materialId: string; quantity: number }> | null
  isActive: boolean
}

function invalidateWorkOrderTemplates(queryClient: ReturnType<typeof useQueryClient>, id?: string) {
  void queryClient.invalidateQueries({ queryKey: ['work-order-templates'] })
  if (id) {
    void queryClient.invalidateQueries({ queryKey: ['work-order-templates', id] })
  }
}

export function useCreateWorkOrderTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkOrderTemplatePayload) => api.post<WorkOrderTemplate>(endpoints.workOrderTemplates.create, payload),
    onSuccess: () => invalidateWorkOrderTemplates(queryClient),
  })
}

export function useUpdateWorkOrderTemplate(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertWorkOrderTemplatePayload) => api.put<WorkOrderTemplate>(endpoints.workOrderTemplates.update(id), payload),
    onSuccess: () => invalidateWorkOrderTemplates(queryClient, id),
  })
}

export function useDeleteWorkOrderTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.workOrderTemplates.delete(id)),
    onSuccess: () => invalidateWorkOrderTemplates(queryClient),
  })
}
