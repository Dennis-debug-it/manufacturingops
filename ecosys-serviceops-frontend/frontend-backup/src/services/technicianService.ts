import { api } from '../lib/api'
import type { TechnicianRecord } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const technicianService = {
  async list(branchId?: string | null, signal?: AbortSignal): Promise<TechnicianRecord[]> {
    const response = await api.get<unknown>('/api/technicians', {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
    return asArray<TechnicianRecord>(response)
  },
}
