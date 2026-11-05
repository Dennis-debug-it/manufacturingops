import { keepPreviousData, useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import type { PagedResult } from '../../../shared/types/pagination'
import { performAction, type NewOutboxAction } from '../../tech/offline/outbox'
import type { ActionPayload, CreateJobPayload, FactoryDashboard, FailureCode, JobAction, JobDetail, JobPlan, JobSummary } from './types'
import { ACTION_LABELS } from './types'

export interface JobFilters {
  view?: 'open' | 'approval' | 'verify' | 'overdue' | 'closed' | 'all'
  orgNodeId?: string | undefined
  assetId?: string | undefined
  workType?: string | undefined
  priority?: string | undefined
  breakdown?: boolean | undefined
  mine?: boolean | undefined
  search?: string | undefined
}

export function useJobs(filters: JobFilters, page: number, pageSize: number) {
  return useQuery({
    queryKey: ['factory', 'jobs', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<JobSummary>>('/api/factory/work-orders', { ...filters, page, pageSize }),
    placeholderData: keepPreviousData,
    staleTime: 20_000,
  })
}

export function useJob(id: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'job', id],
    queryFn: () => api.get<JobDetail>(`/api/factory/work-orders/${id}`),
    enabled: Boolean(id),
  })
}

const refreshJobs = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'job'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'dashboard'] })
}

export function useCreateJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateJobPayload) => api.post<JobDetail>('/api/factory/work-orders', payload),
    onSuccess: () => refreshJobs(queryClient),
  })
}

/**
 * The job actions, handover notes, labour time and step ticks. Each goes through the offline
 * outbox, so a technician without signal can work the job and it syncs, dated when it happened,
 * once the phone is back online.
 */
export function useJobActions(job: Pick<JobDetail, 'id' | 'workOrderNumber'> | undefined) {
  const queryClient = useQueryClient()
  const run = async (label: string, request: Pick<NewOutboxAction, 'url' | 'body'>) => {
    if (!job) throw new Error('No job')
    const result = await performAction<JobDetail>(
      { kind: 'factory-job', label: `${label} · ${job.workOrderNumber}`, workOrderId: job.id, method: 'POST', ...request },
      () => refreshJobs(queryClient))
    if (result.data) queryClient.setQueryData(['factory', 'job', job.id], result.data)
    refreshJobs(queryClient)
    return result
  }
  const base = job ? `/api/factory/work-orders/${job.id}` : ''
  return {
    act: (action: JobAction, payload: ActionPayload = {}) => run(ACTION_LABELS[action], { url: `${base}/actions/${action}`, body: payload }),
    handover: (note: string) => run('Handover note', { url: `${base}/handover`, body: { note } }),
    labour: (minutes: number, notes: string | null, technicianId: string | null = null) => run('Labour time', { url: `${base}/labour`, body: { minutes, notes, technicianId } }),
    step: (stepId: string, done: boolean, remarks: string | null) => run(done ? 'Step done' : 'Step undone', { url: `${base}/steps/${stepId}`, body: { done, remarks } }),
  }
}

export function useSplitJob(parentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { title: string; description?: string | null; priority?: string | null }) =>
      api.post<JobDetail>(`/api/factory/work-orders/${parentId}/children`, payload),
    onSuccess: () => refreshJobs(queryClient),
  })
}

export function useFailureCodes(assetCategoryId?: string | null) {
  return useQuery({
    queryKey: ['factory', 'failure-codes', assetCategoryId ?? null],
    queryFn: () => api.get<FailureCode[]>('/api/failure-codes', { assetCategoryId: assetCategoryId ?? undefined }),
    staleTime: 10 * 60_000,
  })
}

export function useJobPlans(assetCategoryId?: string | null) {
  return useQuery({
    queryKey: ['factory', 'job-plans', assetCategoryId ?? null],
    queryFn: () => api.get<JobPlan[]>('/api/factory/job-plans', { assetCategoryId: assetCategoryId ?? undefined }),
    staleTime: 5 * 60_000,
  })
}

export interface TechnicianOption { id: string; fullName: string; isActive: boolean; skillCategory?: string | null }

export function useTechnicianOptions() {
  return useQuery({
    queryKey: ['factory', 'technicians'],
    queryFn: () => api.get<TechnicianOption[]>('/api/technicians'),
    staleTime: 5 * 60_000,
  })
}

export function useFactoryDashboard(orgNodeId: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'dashboard', orgNodeId ?? null],
    queryFn: () => api.get<FactoryDashboard>('/api/factory/dashboard', { orgNodeId }),
    staleTime: 60_000,
    refetchInterval: 120_000,
  })
}
