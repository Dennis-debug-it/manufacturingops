import { api } from '../lib/api'
import type {
  UpdateBillingStatusInput,
  UpsertBillableItemInput,
  UpsertTimeLogInput,
  WorkOrderBillableItemRecord,
  WorkOrderTimeLogRecord,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const workOrderBillingService = {
  // Billable Items
  async getBillableItems(workOrderId: string, signal?: AbortSignal): Promise<WorkOrderBillableItemRecord[]> {
    const response = await api.get<unknown>(`/api/work-orders/${workOrderId}/billable-items`, { signal })
    return asArray<WorkOrderBillableItemRecord>(response)
  },
  addBillableItem(workOrderId: string, input: UpsertBillableItemInput) {
    return api.post<WorkOrderBillableItemRecord>(`/api/work-orders/${workOrderId}/billable-items`, input)
  },
  updateBillableItem(workOrderId: string, itemId: string, input: UpsertBillableItemInput) {
    return api.put<WorkOrderBillableItemRecord>(`/api/work-orders/${workOrderId}/billable-items/${itemId}`, input)
  },
  deleteBillableItem(workOrderId: string, itemId: string) {
    return api.delete<void>(`/api/work-orders/${workOrderId}/billable-items/${itemId}`)
  },
  async autoPopulateBillableItems(workOrderId: string): Promise<WorkOrderBillableItemRecord[]> {
    const response = await api.post<unknown>(`/api/work-orders/${workOrderId}/billable-items/auto-populate`)
    return asArray<WorkOrderBillableItemRecord>(response)
  },
  updateBillingStatus(workOrderId: string, input: UpdateBillingStatusInput) {
    return api.put<void>(`/api/work-orders/${workOrderId}/billing-status`, input)
  },

  // Time Logs
  async getTimeLogs(workOrderId: string, signal?: AbortSignal): Promise<WorkOrderTimeLogRecord[]> {
    const response = await api.get<unknown>(`/api/work-orders/${workOrderId}/time-logs`, { signal })
    return asArray<WorkOrderTimeLogRecord>(response)
  },
  addTimeLog(workOrderId: string, input: UpsertTimeLogInput) {
    return api.post<WorkOrderTimeLogRecord>(`/api/work-orders/${workOrderId}/time-logs`, input)
  },
  updateTimeLog(workOrderId: string, logId: string, input: UpsertTimeLogInput) {
    return api.put<WorkOrderTimeLogRecord>(`/api/work-orders/${workOrderId}/time-logs/${logId}`, input)
  },
  deleteTimeLog(workOrderId: string, logId: string) {
    return api.delete<void>(`/api/work-orders/${workOrderId}/time-logs/${logId}`)
  },
}
