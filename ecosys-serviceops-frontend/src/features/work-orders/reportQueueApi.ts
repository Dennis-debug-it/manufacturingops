import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

export type ReportStatus =
  | 'AwaitingGeneration'
  | 'Draft'
  | 'UnderReview'
  | 'Approved'
  | 'Sent'
  | 'Acknowledged'

export interface ReportQueueRow {
  workOrderId: string
  srNo: string | null
  woNo: string
  client: string
  site: string
  asset: string
  technician: string
  completedAt: string | null
  reportStatus: ReportStatus
  daysSinceCompletion: number | null
}

export interface ReportQueueCounts {
  awaitingGeneration: number
  underReview: number
  approvedNotSent: number
  sentNotAcknowledged: number
}

export interface ReportQueueResponse {
  items: ReportQueueRow[]
  counts: ReportQueueCounts
  total: number
  page: number
  pageSize: number
}

export function useReportQueue(status: ReportStatus | 'All', page: number, pageSize = 25) {
  return useQuery({
    queryKey: ['work-orders', 'report-queue', status, page, pageSize],
    queryFn: () =>
      api.get<ReportQueueResponse>('/api/workorders/report-queue', {
        status: status === 'All' ? undefined : status,
        page,
        pageSize,
      }),
    staleTime: 15_000,
  })
}

const invalidateQueue = (queryClient: ReturnType<typeof useQueryClient>) =>
  queryClient.invalidateQueries({ queryKey: ['work-orders', 'report-queue'] })

export function useGenerateQueuedReport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workOrderId: string) => api.post<ReportQueueRow>(`/api/workorders/${workOrderId}/report/generate`, {}),
    onSuccess: () => void invalidateQueue(queryClient),
  })
}

export function useApproveQueuedReport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workOrderId: string) => api.post<ReportQueueRow>(`/api/workorders/${workOrderId}/report/approve`, {}),
    onSuccess: () => void invalidateQueue(queryClient),
  })
}

export function useSendQueuedReport() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (workOrderId: string) => api.post<ReportQueueRow>(`/api/workorders/${workOrderId}/report/send`, {}),
    onSuccess: () => void invalidateQueue(queryClient),
  })
}

export interface ReportBatchResult {
  processed: number
  matched: number
}

export function useGenerateReportBatch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ids: string[]) => api.post<ReportBatchResult>('/api/workorders/report/generate-batch', { ids }),
    onSuccess: () => void invalidateQueue(queryClient),
  })
}

export function useSendReportBatch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (ids: string[]) => api.post<ReportBatchResult>('/api/workorders/report/send-batch', { ids }),
    onSuccess: () => void invalidateQueue(queryClient),
  })
}

/** Downloads a single report PDF (blob) and triggers a browser save. */
export async function downloadReportPdf(workOrderId: string, woNo: string) {
  const response = await apiClient.get(`/api/workorders/${workOrderId}/service-report/pdf`, { responseType: 'blob' })
  triggerBlobDownload(response.data as Blob, `${woNo}-service-report.pdf`)
}

/** Downloads a ZIP of the selected reports. */
export async function downloadReportsZip(ids: string[]) {
  const response = await apiClient.get('/api/workorders/report/download-zip', {
    params: { ids: ids.join(',') },
    responseType: 'blob',
  })
  triggerBlobDownload(response.data as Blob, `service-reports-${new Date().toISOString().slice(0, 10)}.zip`)
}

function triggerBlobDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()
  URL.revokeObjectURL(url)
}
