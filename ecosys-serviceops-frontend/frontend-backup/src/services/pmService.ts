import { api } from '../lib/api'
import type { PreventiveMaintenancePlan, UpsertPreventiveMaintenanceInput, WorkOrder } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const pmService = {
  async list(branchId?: string | null, signal?: AbortSignal): Promise<PreventiveMaintenancePlan[]> {
    const response = await api.get<unknown>('/api/preventive-maintenance', {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
    return asArray<PreventiveMaintenancePlan>(response)
  },
  async listDue(branchId?: string | null, signal?: AbortSignal): Promise<PreventiveMaintenancePlan[]> {
    const response = await api.get<unknown>('/api/preventive-maintenance/due', {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
    return asArray<PreventiveMaintenancePlan>(response)
  },
  create(input: UpsertPreventiveMaintenanceInput) {
    return api.post<PreventiveMaintenancePlan>('/api/preventive-maintenance', input)
  },
  update(id: string, input: UpsertPreventiveMaintenanceInput) {
    return api.put<PreventiveMaintenancePlan>(`/api/preventive-maintenance/${id}`, input)
  },
  generateWorkOrder(id: string) {
    return api.post<WorkOrder>(`/api/preventive-maintenance/${id}/generate-workorder`)
  },
}
