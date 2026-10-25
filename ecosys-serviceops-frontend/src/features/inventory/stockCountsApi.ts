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
}

export interface StockCountLine {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  category: string | null
  /** The book balance when the count was opened. */
  expectedQuantity: number
  countedQuantity: number | null
  variance: number | null
  unitCost: number | null
  varianceValue: number | null
  notes: string | null
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

export const useCreateStockCount = () =>
  useStockCountMutation((payload: { branchId: string | null; category: string | null; notes: string | null }) => api.post<StockCountDetail>(base, payload))

export const useSaveStockCountLines = (id: string) =>
  useStockCountMutation((lines: Array<{ lineId: string; countedQuantity: number | null; notes: string | null }>) =>
    api.put<StockCountDetail>(`${base}/${id}/lines`, { lines }))

export const usePostStockCount = (id: string) => useStockCountMutation(() => api.post<StockCountDetail>(`${base}/${id}/post`), true)

export const useCancelStockCount = (id: string) => useStockCountMutation(() => api.post<StockCountDetail>(`${base}/${id}/cancel`))
