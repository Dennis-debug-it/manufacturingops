import { api } from '../lib/api'
import type { WorkOrder } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const technicianJobsService = {
  async list(signal?: AbortSignal): Promise<WorkOrder[]> {
    const response = await api.get<unknown>('/api/technician/jobs', { signal })
    return asArray<WorkOrder>(response)
  },

  get(id: string, signal?: AbortSignal) {
    return api.get<WorkOrder>(`/api/technician/jobs/${id}`, { signal })
  },
}
