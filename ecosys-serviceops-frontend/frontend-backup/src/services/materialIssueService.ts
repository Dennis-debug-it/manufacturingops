import { api } from '../lib/api'
import type { CreateMaterialIssueInput, MaterialIssueRecord } from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const materialIssueService = {
  async list(branchId?: string | null, signal?: AbortSignal): Promise<MaterialIssueRecord[]> {
    const response = await api.get<unknown>('/api/material-issues', {
      query: {
        branchId: branchId && branchId !== 'all' ? branchId : undefined,
      },
      signal,
    })
    return asArray<MaterialIssueRecord>(response)
  },
  create(input: CreateMaterialIssueInput) {
    return api.post<MaterialIssueRecord>('/api/material-issues', input)
  },
  approve(id: string) {
    return api.post<MaterialIssueRecord>(`/api/material-issues/${id}/approve`)
  },
  reject(id: string, reason?: string | null) {
    return api.post<MaterialIssueRecord>(`/api/material-issues/${id}/reject`, { reason: reason || null })
  },
  issue(id: string) {
    return api.post<MaterialIssueRecord>(`/api/material-issues/${id}/issue`)
  },
  cancel(id: string) {
    return api.post<MaterialIssueRecord>(`/api/material-issues/${id}/cancel`)
  },
  returnIssue(id: string, lines: Array<{ materialIssueLineId: string; quantityReturned: number }>) {
    return api.post<MaterialIssueRecord>(`/api/material-issues/${id}/return`, { lines })
  },
}
