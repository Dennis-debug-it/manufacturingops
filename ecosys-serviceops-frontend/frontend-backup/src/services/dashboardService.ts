import { api } from '../lib/api'
import type { DashboardSummary } from '../types/api'
import { normalizeDashboardSummary } from '../utils/apiDefaults'

export const dashboardService = {
  async getSummary(branchId?: string | null, signal?: AbortSignal): Promise<DashboardSummary> {
    const response = await api.get<unknown>('/api/dashboard/summary', {
      query: { branchId: branchId && branchId !== 'all' ? branchId : undefined },
      signal,
    })
    return normalizeDashboardSummary(response)
  },
}
