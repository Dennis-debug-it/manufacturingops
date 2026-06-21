import { api } from '../lib/api'
import type { CreateScheduledReportInput, ScheduledReport } from '../types/api'

export const scheduledReportService = {
  list(signal?: AbortSignal): Promise<ScheduledReport[]> {
    return api.get<ScheduledReport[]>('/api/reports/scheduled', { signal })
  },

  create(input: CreateScheduledReportInput): Promise<ScheduledReport> {
    return api.post<ScheduledReport>('/api/reports/scheduled', input)
  },

  update(id: string, input: CreateScheduledReportInput): Promise<ScheduledReport> {
    return api.put<ScheduledReport>(`/api/reports/scheduled/${id}`, input)
  },

  remove(id: string): Promise<void> {
    return api.delete<void>(`/api/reports/scheduled/${id}`)
  },

  toggle(id: string): Promise<ScheduledReport> {
    return api.put<ScheduledReport>(`/api/reports/scheduled/${id}/toggle`)
  },

  runNow(id: string): Promise<ScheduledReport> {
    return api.post<ScheduledReport>(`/api/reports/scheduled/${id}/run-now`)
  },
}
