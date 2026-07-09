import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  WorkOrder,
  WorkOrderAssignmentHistoryItem,
  WorkOrderChecklistItem,
  WorkOrderEvent,
  WorkOrderExecutionBundle,
  WorkOrderServiceReportPreview,
  WorkOrderSignature,
} from './types'

export interface WorkOrderListFilters {
  status?: string
  workType?: string
  technicianId?: string
  assignmentGroupId?: string
  unassigned?: boolean
  search?: string
  fromDate?: string
  toDate?: string
}

const workOrdersKey = (filters?: WorkOrderListFilters) => ['work-orders', filters ?? {}] as const

export function useWorkOrders(filters?: WorkOrderListFilters) {
  return useQuery({
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

export function useWorkOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', id],
    queryFn: () => api.get<WorkOrder>(endpoints.workOrders.detail(id ?? '')),
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
    mutationFn: (payload: { workDoneNotes: string; reportSummary?: string | null; adminOverride?: boolean; overrideReason?: string | null }) =>
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

export function useAddMaterialUsage(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { materialItemId: string; assetId?: string | null; quantityUsed: number; unitCost?: number | null; chargeable: boolean; notes?: string | null }) =>
      api.post<WorkOrderExecutionBundle>(endpoints.workOrders.materialsUsed(id), payload),
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

export function useUpdatePhoto(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { photoId: string; caption?: string | null; category?: string | null; includeInReport: boolean }) =>
      api.put<WorkOrderExecutionBundle>(endpoints.workOrders.photo(id, payload.photoId), {
        caption: payload.caption,
        category: payload.category,
        includeInReport: payload.includeInReport,
      }),
    onSuccess: () => invalidateExecution(queryClient, id),
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
