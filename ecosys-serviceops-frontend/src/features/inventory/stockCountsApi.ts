import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

export type StockCountStatus = 'Open' | 'Posted' | 'Cancelled'

export interface StockCountSummary {
  id: string
  countNumber: string
  branchId: string | null
  branchName: string | null
  category: string | null
  status: StockCountStatus
  lineCount: number
  countedLines: number
  varianceLines: number
  startedByName: string | null
  createdAt: string
  postedAt: string | null
  /** The counted branch's name. */
  locationName: string | null
  isBlind: boolean
  recountLines: number
}

export interface StockCountLine {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  category: string | null
  /** The book balance when the count was opened; null while a blind count is open (for counters). */
  expectedQuantity: number | null
  countedQuantity: number | null
  variance: number | null
  unitCost: number | null
  varianceValue: number | null
  notes: string | null
  barcode: string | null
  needsRecount: boolean
}

export interface StockCountDetail {
  id: string
  countNumber: string
  branchId: string | null
  branchName: string | null
  category: string | null
  status: StockCountStatus
  notes: string | null
  startedByName: string | null
  createdAt: string
  postedByName: string | null
  postedAt: string | null
  lines: StockCountLine[]
  netVarianceValue: number
  locationName: string | null
  isBlind: boolean
  /** True when this viewer is not shown book quantities (an open blind count). */
  expectedHidden: boolean
  /** Variances in either direction; posting above Settings → Inventory's approval limit needs an approver. */
  grossVarianceValue: number
}

const base = '/api/stock-counts'

export function useStockCounts(status: StockCountStatus | 'All') {
  return useQuery({
    queryKey: ['stock-counts', status],
    queryFn: () => api.get<StockCountSummary[]>(base, status === 'All' ? undefined : { status }),
    staleTime: 15_000,
  })
}

export function useStockCount(id: string | undefined) {
  return useQuery({
    queryKey: ['stock-counts', 'detail', id],
    queryFn: () => api.get<StockCountDetail>(`${base}/${id}`),
    enabled: Boolean(id),
  })
}

function useStockCountMutation<TPayload>(request: (payload: TPayload) => Promise<StockCountDetail>, touchesStock = false) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: request,
    onSuccess: (detail) => {
      queryClient.setQueryData(['stock-counts', 'detail', detail.id], detail)
      void queryClient.invalidateQueries({ queryKey: ['stock-counts'] })
      if (touchesStock) {
        void queryClient.invalidateQueries({ queryKey: ['materials'] })
        void queryClient.invalidateQueries({ queryKey: ['stock-ledger'] })
      }
    },
  })
}

export interface CreateStockCountPayload {
  branchId: string | null
  category: string | null
  notes: string | null
  /** Only parts held (or with a reorder level) at the location. */
  onlyStocked: boolean
  /** Hide book quantities from counters until posted. */
  blind: boolean
}

export const useCreateStockCount = () =>
  useStockCountMutation((payload: CreateStockCountPayload) => api.post<StockCountDetail>(base, payload))

export interface SaveStockCountLinePayload {
  lineId: string
  countedQuantity: number | null
  notes: string | null
  /** Sets or clears the recount mark; omitted leaves it (a changed count clears it). */
  needsRecount?: boolean
}

export const useSaveStockCountLines = (id: string) =>
  useStockCountMutation((lines: SaveStockCountLinePayload[]) => api.put<StockCountDetail>(`${base}/${id}/lines`, { lines }))

/** Adds a part found on the shelf that the count did not list. */
export const useAddStockCountLine = (id: string) =>
  useStockCountMutation((payload: { materialItemId: string; countedQuantity?: number | null }) => api.post<StockCountDetail>(`${base}/${id}/lines`, payload))

export const usePostStockCount = (id: string) => useStockCountMutation(() => api.post<StockCountDetail>(`${base}/${id}/post`), true)

export const useCancelStockCount = (id: string) => useStockCountMutation(() => api.post<StockCountDetail>(`${base}/${id}/cancel`))
