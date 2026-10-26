import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

export interface StockValuationRow {
  location: string
  locationType: 'Branch' | 'Van' | 'Workspace'
  materialItemId: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  quantity: number
  unitCost: number | null
  value: number
}

export interface StockValuation {
  rows: StockValuationRow[]
  locations: Array<{ location: string; locationType: string; parts: number; value: number }>
  totalValue: number
}

export interface StockUsageRow {
  materialItemId: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  quantityUsed: number
  quantityReturned: number
  netQuantity: number
  jobCount: number
  unitCost: number | null
  /** Net quantity at the part's current cost. */
  netValue: number
}

export interface ReportFilters {
  branchId?: string | undefined
  from?: string | undefined
  to?: string | undefined
}

const base = '/api/inventory-reports'
const params = (filters: ReportFilters, extra: Record<string, string | undefined> = {}) => ({
  branchId: filters.branchId || undefined,
  from: filters.from || undefined,
  to: filters.to || undefined,
  ...extra,
})

export function useStockValuation(branchId?: string) {
  return useQuery({
    queryKey: ['inventory-reports', 'valuation', branchId ?? ''],
    queryFn: () => api.get<StockValuation>(`${base}/valuation`, params({ branchId })),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

export function useStockUsage(filters: ReportFilters) {
  return useQuery({
    queryKey: ['inventory-reports', 'usage', filters],
    queryFn: () => api.get<{ rows: StockUsageRow[]; totalValue: number }>(`${base}/usage`, params(filters)),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })
}

/** Downloads a report as CSV straight from the server, with the same filters as on screen. */
export async function downloadInventoryReport(report: 'valuation' | 'usage' | 'movements', filters: ReportFilters & Record<string, string | undefined>, fileName: string) {
  const response = await apiClient.get<Blob>(`${base}/${report}`, {
    params: { ...params(filters, filters), format: report === 'movements' ? undefined : 'csv' },
    responseType: 'blob',
  })
  const url = window.URL.createObjectURL(response.data)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  anchor.style.display = 'none'
  document.body.append(anchor)
  anchor.click()
  anchor.remove()
  window.URL.revokeObjectURL(url)
}
