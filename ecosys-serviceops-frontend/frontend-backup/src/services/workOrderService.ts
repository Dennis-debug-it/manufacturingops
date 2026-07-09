import { api } from '../lib/api'
import type {
  CreateWorkOrderInput,
  MaterialRequestRecord,
  WorkOrderExecutionBundle,
  WorkOrderChecklistItemRecord,
  WorkOrderAssignmentHistoryRecord,
  WorkOrderEventRecord,
  UpdateWorkOrderInput,
  WorkOrder,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

function branchQuery(branchId?: string | null) {
  return { branchId: branchId && branchId !== 'all' ? branchId : undefined }
}

export const workOrderService = {
  async list(
    branchId?: string | null,
    signal?: AbortSignal,
    filters?: { workType?: string | null },
  ): Promise<WorkOrder[]> {
    const response = await api.get<unknown>('/api/workorders', {
      query: {
        ...branchQuery(branchId),
        workType: filters?.workType || undefined,
      },
      signal,
    })
    return asArray<WorkOrder>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<WorkOrder>(`/api/workorders/${id}`, { signal })
  },

  create(input: CreateWorkOrderInput) {
    return api.post<WorkOrder>('/api/workorders', input)
  },

  update(id: string, input: UpdateWorkOrderInput) {
    return api.put<WorkOrder>(`/api/workorders/${id}`, input)
  },

  updateStatus(workOrder: WorkOrder, status: string) {
    return this.update(workOrder.id, {
      clientId: workOrder.clientId,
      branchId: workOrder.branchId,
      siteId: workOrder.siteId,
      systemId: workOrder.systemId,
      assetId: workOrder.assetId,
      assignmentType: workOrder.assignmentType,
      assignmentGroupId: workOrder.assignmentGroupId,
      assignedTechnicianId: workOrder.assignedTechnicianId,
      assignedTechnicianIds: workOrder.assignedTechnicianIds,
      leadTechnicianId: workOrder.leadTechnicianId,
      assignmentNotes: workOrder.assignmentNotes,
      title: workOrder.title,
      description: workOrder.description,
      priority: workOrder.priority,
      dueDate: workOrder.dueDate,
      workType: workOrder.workType,
      status,
      isPreventiveMaintenance: workOrder.isPreventiveMaintenance,
      pmTemplateId: workOrder.pmTemplateId,
      notifyClient: workOrder.notifyClient,
      clientContactEmail: workOrder.clientContactEmail,
    })
  },

  reopen(workOrder: WorkOrder) {
    return this.updateStatus(workOrder, 'Reopened')
  },

  reject(workOrder: WorkOrder) {
    return this.updateStatus(workOrder, 'Rejected')
  },

  assign(id: string, technicianId?: string | null, assignmentGroupId?: string | null) {
    return api.post<WorkOrder>(`/api/workorders/${id}/assign`, {
      technicianId: technicianId || null,
      assignmentGroupId: assignmentGroupId || null,
      notes: null,
    })
  },

  assignGroup(id: string, input: { assignmentGroupId?: string | null; notes?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/assign-group`, input)
  },

  assignTechnicians(id: string, input: { technicianIds: string[]; leadTechnicianId?: string | null; notes?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/assign-technicians`, input)
  },

  reassign(id: string, input: { assignmentGroupId?: string | null; technicianIds: string[]; leadTechnicianId?: string | null; notes?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/reassign`, input)
  },

  async getAssignmentHistory(id: string, signal?: AbortSignal): Promise<WorkOrderAssignmentHistoryRecord[]> {
    const response = await api.get<unknown>(`/api/workorders/${id}/assignment-history`, { signal })
    return asArray<WorkOrderAssignmentHistoryRecord>(response)
  },

  technicianResponse(id: string, input: { technicianId: string; response: 'Accepted' | 'Declined'; notes?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/technician-response`, input)
  },

  start(id: string) {
    return api.post<WorkOrder>(`/api/workorders/${id}/start`)
  },

  complete(id: string, input: { workDoneNotes: string; completedAt?: string | null; technicianId?: string | null; assignmentGroupId?: string | null; reportSummary?: string | null; answersJson?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/complete`, input)
  },

  acknowledge(id: string, input: { acknowledgedByName: string; comments?: string | null; acknowledgementDate?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/acknowledge`, input)
  },

  async getEvents(id: string, signal?: AbortSignal): Promise<WorkOrderEventRecord[]> {
    const response = await api.get<unknown>(`/api/workorders/${id}/events`, { signal })
    return asArray<WorkOrderEventRecord>(response)
  },

  addComment(id: string, message: string) {
    return api.post<WorkOrderEventRecord>(`/api/workorders/${id}/comments`, { message })
  },

  async listMaterialRequests(branchId?: string | null, signal?: AbortSignal): Promise<MaterialRequestRecord[]> {
    const response = await api.get<unknown>('/api/material-requests', {
      query: branchQuery(branchId),
      signal,
    })
    return asArray<MaterialRequestRecord>(response)
  },

  requestMaterials(workOrderId: string, lines: Array<{ materialItemId: string; quantityRequested: number }>) {
    return api.post<MaterialRequestRecord>('/api/material-requests', {
      workOrderId,
      lines,
    })
  },

  approveMaterialRequest(id: string) {
    return api.post<MaterialRequestRecord>(`/api/material-requests/${id}/approve`)
  },

  issueMaterialRequest(id: string, lines: Array<{ materialRequestLineId: string; quantityIssued: number }>) {
    return api.post<MaterialRequestRecord>(`/api/material-requests/${id}/issue`, { lines })
  },

  closeMaterialRequest(id: string, lines: Array<{ materialRequestLineId: string; quantityUsed: number; quantityReturned: number }>) {
    return api.post<MaterialRequestRecord>(`/api/material-requests/${id}/close`, { lines })
  },

  recordArrival(id: string, input: { technicianId: string; latitude?: number | null; longitude?: number | null; arrivedAt?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/arrival`, input)
  },

  recordDeparture(id: string, input: { technicianId: string; latitude?: number | null; longitude?: number | null; departedAt?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/departure`, input)
  },

  markInTransit(id: string, input: { technicianId: string; latitude?: number | null; longitude?: number | null; inTransitAt?: string | null; notes?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/in-transit`, input)
  },

  getExecution(id: string, signal?: AbortSignal) {
    return api.get<WorkOrderExecutionBundle>(`/api/workorders/${id}/execution`, { signal })
  },

  saveExecutionNotes(id: string, input: { findings?: string | null; workDone?: string | null; progressComment?: string | null }) {
    return api.post<WorkOrderExecutionBundle>(`/api/workorders/${id}/execution-notes`, input)
  },

  addMaterialUsage(id: string, input: { materialItemId: string; assetId?: string | null; quantityUsed: number; unitCost?: number | null; chargeable: boolean; notes?: string | null; usedAt?: string | null }) {
    return api.post<WorkOrderExecutionBundle>(`/api/workorders/${id}/materials-used`, input)
  },

  uploadPhoto(id: string, input: { file: File; caption?: string | null; category?: string | null; includeInReport: boolean }) {
    const formData = new FormData()
    formData.append('file', input.file)
    formData.append('caption', input.caption || '')
    formData.append('category', input.category || 'Other')
    formData.append('includeInReport', String(input.includeInReport))
    return api.postForm<WorkOrderExecutionBundle>(`/api/workorders/${id}/photos`, formData)
  },

  updatePhoto(id: string, photoId: string, input: { caption?: string | null; category?: string | null; includeInReport: boolean }) {
    return api.put<WorkOrderExecutionBundle>(`/api/workorders/${id}/photos/${photoId}`, input)
  },

  captureSignature(id: string, input: { signatureType: 'Technician' | 'Client'; signerName: string; signerRole?: string | null; signatureDataUrl: string; comment?: string | null }) {
    return api.post<WorkOrderExecutionBundle>(`/api/workorders/${id}/signatures`, input)
  },

  getServiceReport(id: string, signal?: AbortSignal) {
    return api.get<WorkOrderExecutionBundle['reportPreview']>(`/api/workorders/${id}/service-report`, { signal })
  },

  generateServiceReport(id: string) {
    return api.post<WorkOrderExecutionBundle['reportPreview']>(`/api/workorders/${id}/service-report/generate`)
  },

  async downloadServiceReportPdf(id: string) {
    const { blob, fileName } = await api.download(`/api/workorders/${id}/service-report/pdf`, {
      fallbackFileName: `service-report-${id}.pdf`,
    })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName
    anchor.click()
    URL.revokeObjectURL(url)
  },

  pause(id: string, input?: { holdReason?: string | null; note?: string | null }) {
    return api.post<WorkOrder>(`/api/workorders/${id}/pause`, {
      holdReason: input?.holdReason || null,
      note: input?.note || null,
    })
  },

  resume(id: string) {
    return api.post<WorkOrder>(`/api/workorders/${id}/resume`)
  },

  close(id: string) {
    return api.post<WorkOrder>(`/api/workorders/${id}/close`)
  },

  cancel(id: string) {
    return api.post<WorkOrder>(`/api/workorders/${id}/cancel`)
  },

  attachPmTemplate(id: string, pmTemplateId: string) {
    return api.post<WorkOrder>(`/api/workorders/${id}/attach-pm-template`, { pmTemplateId })
  },

  updateChecklistItem(id: string, itemId: string, input: { responseValue?: string | null; remarks?: string | null; isCompleted: boolean }) {
    return api.put<WorkOrderChecklistItemRecord>(`/api/workorders/${id}/checklist/${itemId}`, input)
  },

  uploadChecklistEvidence(id: string, itemId: string, input: { file: File; caption?: string | null }) {
    const formData = new FormData()
    formData.append('file', input.file)
    formData.append('caption', input.caption || '')
    return api.postForm<WorkOrderChecklistItemRecord>(`/api/workorders/${id}/checklist/${itemId}/evidence`, formData)
  },

  async addJobNote(id: string, message: string) {
    return this.addComment(id, message)
  },

  async submitReport(id: string, input: { workDoneNotes: string; reportSummary?: string | null; answersJson?: string | null }) {
    return this.complete(id, input)
  },
}
