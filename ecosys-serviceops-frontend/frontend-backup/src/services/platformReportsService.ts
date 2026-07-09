import { api } from '../lib/api'
import type { PlatformReportsSummary } from '../types/api'

export const platformReportsService = {
  getSummary(signal?: AbortSignal) {
    return api.get<PlatformReportsSummary>('/api/platform/reports/summary', { signal })
  },
}
