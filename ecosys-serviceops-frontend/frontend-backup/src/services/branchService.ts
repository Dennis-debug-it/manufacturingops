import { api } from '../lib/api'
import type { BranchRecord, UpsertBranchInput } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const branchService = {
  async list(signal?: AbortSignal): Promise<BranchRecord[]> {
    const response = await api.get<unknown>('/api/branches', { signal })
    return asArray<BranchRecord>(response)
  },
  get(id: string, signal?: AbortSignal) {
    return api.get<BranchRecord>(`/api/branches/${id}`, { signal })
  },
  create(input: UpsertBranchInput) {
    return api.post<BranchRecord>('/api/branches', input)
  },
  update(id: string, input: UpsertBranchInput) {
    return api.put<BranchRecord>(`/api/branches/${id}`, input)
  },
}
