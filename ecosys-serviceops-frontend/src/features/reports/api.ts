import { useQuery } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface WorkOrderPerformanceReport {
  dateRange: { from: string; to: string }
  total: number
  completed: number
  completedOnTime: number
  overdue: number
  onTimeRate: number
  avgCompletionHours: number
  /** Return visits raised with "Create follow-up". */
  followUps?: number
  followUpRate?: number
  byStatus: Array<{ status: string; count: number }>
  byPriority: Array<{ priority: string; count: number }>
  byDay: Array<{ date: string; count: number }>
}

export function useWorkOrderPerformance(params: { from?: string; to?: string; clientId?: string; branchId?: string; status?: string; priority?: string }) {
  return useQuery({
    queryKey: ['reports', 'work-order-performance', params],
    queryFn: () => api.get<WorkOrderPerformanceReport>(endpoints.reports.workOrderPerformance, params),
    staleTime: 30_000,
  })
}

export interface TechnicianProductivityRow {
  technicianId: string | null
  name: string
  totalJobs: number
  completed: number
  onTimeCount: number
  onTimeRate: number
  avgTimeOnSiteHours: number
  pmJobs: number
  correctiveJobs: number
}

export interface TechnicianProductivityReport {
  dateRange: { from: string; to: string }
  technicians: TechnicianProductivityRow[]
}

export function useTechnicianProductivity(params: { from?: string; to?: string; technicianId?: string; branchId?: string }) {
  return useQuery({
    queryKey: ['reports', 'technician-productivity', params],
    queryFn: () => api.get<TechnicianProductivityReport>(endpoints.reports.technicianProductivity, params),
    staleTime: 30_000,
  })
}

export interface AssetReliabilityRow {
  id: string
  assetName: string
  assetCode: string
  status: string
  categoryName: string | null
  clientName: string | null
  siteName: string | null
  lastPmDate: string | null
  nextPmDate: string | null
  warrantyExpiryDate: string | null
  warrantyStatus: string
  correctiveWos: number
  followUpWos?: number
  pmCompliance: number
  isRecurringFault: boolean
}

export interface AssetReliabilityReport {
  dateRange: { from: string; to: string }
  assets: AssetReliabilityRow[]
  recurringFaultCount: number
}

export function useAssetReliability(params: { from?: string; to?: string; clientId?: string; siteId?: string; categoryId?: string }) {
  return useQuery({
    queryKey: ['reports', 'asset-reliability', params],
    queryFn: () => api.get<AssetReliabilityReport>(endpoints.reports.assetReliability, params),
    staleTime: 30_000,
  })
}

