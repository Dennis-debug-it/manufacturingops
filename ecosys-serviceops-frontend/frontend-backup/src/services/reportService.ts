import { api } from '../lib/api'
import { asArray, asNumber, asRecord, asString } from '../utils/apiDefaults'

export type ReportFilters = {
  dateFrom?: string
  dateTo?: string
  branchId?: string
  clientId?: string
  siteId?: string
  categoryId?: string
  status?: string
  priority?: string
  technicianId?: string
}

export type WorkOrderPerformanceReport = {
  summary: {
    total: number
    completed: number
    cancelled: number
    open: number
    onTimeRate: number
    avgCompletionHours: number
  }
  byStatus: Array<{ status: string; count: number }>
  byPriority: Array<{ priority: string; count: number }>
  byDay: Array<{ date: string; created: number; completed: number }>
}

export type TechnicianProductivityReport = {
  rows: Array<{
    technicianId: string
    technicianName: string
    totalJobs: number
    completed: number
    onTimeRate: number
    avgTimeOnSiteHours: number
    pmCount: number
    correctiveCount: number
  }>
}

export type AssetReliabilityReport = {
  rows: Array<{
    assetId: string
    assetName: string
    assetCode: string
    clientName: string
    siteName: string
    correctiveWoCount: number
    pmComplianceRate: number
    warrantyExpiry: string | null
    isRecurringFault: boolean
  }>
}

export type PmComplianceReport = {
  summary: {
    activePlans: number
    duePeriod: number
    completedOnTime: number
    overdue: number
    complianceRate: number
  }
  overduePlans: Array<{
    planId: string
    assetName: string
    assetCode: string
    clientName: string
    nextPmDate: string
    daysOverdue: number
  }>
}

function buildQuery(filters: ReportFilters): Record<string, string | undefined> {
  return {
    from: filters.dateFrom,
    to: filters.dateTo,
    branchId: filters.branchId,
    clientId: filters.clientId,
    siteId: filters.siteId,
    categoryId: filters.categoryId,
    status: filters.status,
    priority: filters.priority,
    technicianId: filters.technicianId,
  }
}

function normalizeWorkOrderPerformanceReport(payload: unknown): WorkOrderPerformanceReport {
  const record = asRecord(payload)
  const total = asNumber(record?.Total)
  const completed = asNumber(record?.Completed)
  const cancelled = 0
  const open = Math.max(0, total - completed - cancelled)

  return {
    summary: {
      total,
      completed,
      cancelled,
      open,
      onTimeRate: asNumber(record?.OnTimeRate),
      avgCompletionHours: asNumber(record?.AvgCompletionHours),
    },
    byStatus: asArray<Record<string, unknown>>(record?.ByStatus).map((item) => ({
      status: asString(item.Status),
      count: asNumber(item.Count),
    })),
    byPriority: asArray<Record<string, unknown>>(record?.ByPriority).map((item) => ({
      priority: asString(item.Priority),
      count: asNumber(item.Count),
    })),
    byDay: asArray<Record<string, unknown>>(record?.ByDay).map((item) => ({
      date: asString(item.Date),
      created: asNumber(item.Count),
      completed: 0,
    })),
  }
}

function normalizeTechnicianProductivityReport(payload: unknown): TechnicianProductivityReport {
  const record = asRecord(payload)

  return {
    rows: asArray<Record<string, unknown>>(record?.Technicians).map((item) => ({
      technicianId: asString(item.TechnicianId),
      technicianName: asString(item.Name),
      totalJobs: asNumber(item.TotalJobs),
      completed: asNumber(item.Completed),
      onTimeRate: asNumber(item.OnTimeRate),
      avgTimeOnSiteHours: asNumber(item.AvgTimeOnSiteHours),
      pmCount: asNumber(item.PmJobs),
      correctiveCount: asNumber(item.CorrectiveJobs),
    })),
  }
}

function normalizeAssetReliabilityReport(payload: unknown): AssetReliabilityReport {
  const record = asRecord(payload)

  return {
    rows: asArray<Record<string, unknown>>(record?.Assets).map((item) => ({
      assetId: asString(item.Id),
      assetName: asString(item.AssetName),
      assetCode: asString(item.AssetCode),
      clientName: asString(item.ClientName),
      siteName: asString(item.SiteName),
      correctiveWoCount: asNumber(item.CorrectiveWos),
      pmComplianceRate: asNumber(item.PmCompliance),
      warrantyExpiry: asString(item.WarrantyExpiryDate) || null,
      isRecurringFault: Boolean(item.IsRecurringFault),
    })),
  }
}

function normalizePmComplianceReport(payload: unknown): PmComplianceReport {
  const record = asRecord(payload)

  return {
    summary: {
      activePlans: asNumber(record?.ActivePlans),
      duePeriod: asNumber(record?.DueInPeriod),
      completedOnTime: asNumber(record?.CompletedOnTime),
      overdue: asNumber(record?.Overdue),
      complianceRate: asNumber(record?.ComplianceRate),
    },
    overduePlans: asArray<Record<string, unknown>>(record?.OverduePlans).map((item) => ({
      planId: asString(item.Id),
      assetName: asString(item.AssetName),
      assetCode: asString(item.AssetCode),
      clientName: asString(item.ClientName),
      nextPmDate: asString(item.NextPmDate),
      daysOverdue: asNumber(item.DaysOverdue),
    })),
  }
}

export const reportService = {
  async getWorkOrderPerformance(filters: ReportFilters, signal?: AbortSignal) {
    const payload = await api.get<unknown>('/api/reports/work-order-performance', {
      query: buildQuery(filters),
      signal,
    })
    return normalizeWorkOrderPerformanceReport(payload)
  },

  async getTechnicianProductivity(filters: ReportFilters, signal?: AbortSignal) {
    const payload = await api.get<unknown>('/api/reports/technician-productivity', {
      query: buildQuery(filters),
      signal,
    })
    return normalizeTechnicianProductivityReport(payload)
  },

  async getAssetReliability(filters: ReportFilters, signal?: AbortSignal) {
    const payload = await api.get<unknown>('/api/reports/asset-reliability', {
      query: buildQuery(filters),
      signal,
    })
    return normalizeAssetReliabilityReport(payload)
  },

  async getPmCompliance(filters: ReportFilters, signal?: AbortSignal) {
    const payload = await api.get<unknown>('/api/reports/pm-compliance', {
      query: buildQuery(filters),
      signal,
    })
    return normalizePmComplianceReport(payload)
  },

  async exportWorkOrderPerformance(filters: ReportFilters) {
    const { blob, fileName } = await api.download('/api/reports/work-order-performance/export', {
      query: buildQuery(filters),
      fallbackFileName: 'work-order-performance.csv',
    })
    triggerDownload(blob, fileName)
  },

  async exportTechnicianProductivity(filters: ReportFilters) {
    const { blob, fileName } = await api.download('/api/reports/technician-productivity/export', {
      query: buildQuery(filters),
      fallbackFileName: 'technician-productivity.csv',
    })
    triggerDownload(blob, fileName)
  },

  async exportAssetReliability(filters: ReportFilters) {
    const { blob, fileName } = await api.download('/api/reports/asset-reliability/export', {
      query: buildQuery(filters),
      fallbackFileName: 'asset-reliability.csv',
    })
    triggerDownload(blob, fileName)
  },

  async exportPmCompliance(filters: ReportFilters) {
    const { blob, fileName } = await api.download('/api/reports/pm-compliance/export', {
      query: buildQuery(filters),
      fallbackFileName: 'pm-compliance.csv',
    })
    triggerDownload(blob, fileName)
  },
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}
