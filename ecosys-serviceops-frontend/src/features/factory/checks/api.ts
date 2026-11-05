import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import { performAction } from '../../tech/offline/outbox'

export type CheckStatus = 'Due' | 'Completed' | 'Missed'

export interface CheckRunSummary {
  id: string
  scheduleId: string
  scheduleName: string
  isRoute: boolean
  sequence: number
  assetId: string
  assetCode: string
  assetName: string
  orgNodeId: string
  periodKey: string
  shiftName: string | null
  dueFrom: string
  dueBy: string
  status: CheckStatus
  completedAt: string | null
  completedByName: string | null
  failedCount: number
}

export interface CheckQuestion {
  id: string
  sectionName: string | null
  prompt: string
  type: string
  unit: string | null
  minValue: number | null
  maxValue: number | null
  required: boolean
  photoRequired: boolean
  noteRequiredOnFail: boolean
  failAction: string
  value: string | null
  passed: boolean | null
  note: string | null
  raisedWorkOrderId: string | null
}

export interface CheckRunDetail { run: CheckRunSummary; questions: CheckQuestion[]; notes: string | null; signed: boolean }

export interface CheckSubmission {
  answers: { questionId: string; value: string | null; note: string | null }[]
  completedAt: string
  notes: string | null
  signatureDataUrl: string | null
}

export interface CheckSubmissionResult { failed: number; raisedWorkOrders: string[]; warnings: string[] }

export interface CheckTemplate {
  id: string
  name: string
  kind: string
  assetCategoryId: string | null
  assetCategoryName: string | null
  version: number
  questionCount: number
  suggestedFrequency: string | null
}

export interface CheckSchedule {
  id: string
  name: string
  templateId: string
  templateName: string
  frequency: 'PerShift' | 'Daily'
  orgNodeId: string
  orgNodeName: string | null
  isRoute: boolean
  isActive: boolean
  assets: { assetId: string; assetCode: string; assetName: string }[]
}

export interface SaveCheckSchedule {
  name: string
  templateId: string
  frequency: string
  orgNodeId: string
  isRoute: boolean
  assetIds: string[]
  isActive?: boolean | undefined
}

export interface CheckShiftSummary {
  scheduleId: string
  scheduleName: string
  periodKey: string
  shiftName: string | null
  dueFrom: string
  total: number
  completed: number
  missed: number
  failedItems: number
}

export const FREQUENCY_LABELS: Record<string, string> = { PerShift: 'Every shift', Daily: 'Every day' }

export function useDueChecks(orgNodeId?: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'checks-due', orgNodeId ?? null],
    queryFn: () => api.get<CheckRunSummary[]>('/api/factory/checks/due', { orgNodeId }),
    staleTime: 30_000,
    refetchInterval: 120_000,
  })
}

export function useCheckRun(id: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'check-run', id],
    queryFn: () => api.get<CheckRunDetail>(`/api/factory/checks/runs/${id}`),
    enabled: Boolean(id),
  })
}

/** Sends a finished check, or keeps it in the outbox (dated when it was done) when there's no signal. */
export function useSubmitCheck(run: Pick<CheckRunSummary, 'id' | 'assetCode' | 'scheduleName'> | undefined) {
  const queryClient = useQueryClient()
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ['factory', 'checks-due'] })
    void queryClient.invalidateQueries({ queryKey: ['factory', 'check-run'] })
    void queryClient.invalidateQueries({ queryKey: ['factory', 'check-summary'] })
    void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
  }
  return async (submission: CheckSubmission) => {
    if (!run) throw new Error('No check')
    const result = await performAction<CheckSubmissionResult>(
      { kind: 'running-check', label: `${run.scheduleName} · ${run.assetCode}`, workOrderId: null, method: 'POST', url: `/api/factory/checks/runs/${run.id}/submit`, body: submission },
      refresh)
    if (result.queued) {
      // Show it as done in the cached list straight away, so the operator moves on to the next machine.
      queryClient.setQueriesData<CheckRunSummary[]>({ queryKey: ['factory', 'checks-due'] }, (rows) =>
        rows?.map((row) => (row.id === run.id ? { ...row, status: 'Completed', completedAt: submission.completedAt } : row)))
    }
    refresh()
    return result
  }
}

/** Loads each due check's questions while there is signal, so they open on the factory floor without it. */
export function usePrefetchDueRuns(rows: CheckRunSummary[] | undefined) {
  const queryClient = useQueryClient()
  useEffect(() => {
    if (!rows || !navigator.onLine) return
    for (const row of rows.filter((x) => x.status === 'Due').slice(0, 60)) {
      void queryClient.prefetchQuery({
        queryKey: ['factory', 'check-run', row.id],
        queryFn: () => api.get<CheckRunDetail>(`/api/factory/checks/runs/${row.id}`),
        staleTime: 10 * 60_000,
      })
    }
  }, [rows, queryClient])
}

export function useCheckSummary(days: number, orgNodeId?: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'check-summary', days, orgNodeId ?? null],
    queryFn: () => api.get<CheckShiftSummary[]>('/api/factory/checks/summary', { days, orgNodeId }),
    staleTime: 60_000,
  })
}

export function useCheckTemplates() {
  return useQuery({
    queryKey: ['factory', 'check-templates'],
    queryFn: () => api.get<CheckTemplate[]>('/api/factory/checks/templates'),
    staleTime: 5 * 60_000,
  })
}

export function useCheckSchedules(includeInactive: boolean) {
  return useQuery({
    queryKey: ['factory', 'check-schedules', includeInactive],
    queryFn: () => api.get<CheckSchedule[]>('/api/factory/checks/schedules', { includeInactive }),
  })
}

export function useSaveCheckSchedule() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...payload }: SaveCheckSchedule & { id?: string | undefined }) =>
      id ? api.put<CheckSchedule>(`/api/factory/checks/schedules/${id}`, payload) : api.post<CheckSchedule>('/api/factory/checks/schedules', payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['factory', 'check-schedules'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'checks-due'] })
    },
  })
}
