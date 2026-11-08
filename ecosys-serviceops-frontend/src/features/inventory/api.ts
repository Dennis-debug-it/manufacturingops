import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PagedResult } from '../../shared/types/pagination'
import type { Branch, BranchStockSummary, Material, MaterialStockLocations, MaterialIssue, MaterialRequest, StockLedgerEntry, StockMovement, StockTransfer } from './types'

export interface MaterialFilters {
  lowStockOnly?: boolean | undefined
  branchId?: string | undefined
  search?: string | undefined
  status?: 'active' | 'inactive' | 'all' | undefined
}

export function useMaterials(filters?: MaterialFilters) {
  return useQuery({
    queryKey: ['materials', filters ?? {}],
    queryFn: () => api.get<Material[]>(endpoints.materials.list, { lowStockOnly: filters?.lowStockOnly, branchId: filters?.branchId, search: filters?.search, status: filters?.status }),
    staleTime: 30_000,
  })
}

export function useMaterial(id: string | undefined) {
  return useQuery({
    queryKey: ['materials', id],
    queryFn: () => api.get<Material>(endpoints.materials.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useLowStockMaterials(branchId?: string) {
  return useQuery({
    queryKey: ['materials', 'low-stock', branchId ?? ''],
    queryFn: () => api.get<Material[]>(endpoints.materials.lowStock, { branchId }),
    staleTime: 30_000,
  })
}

export function useMaterialStockLocations(id: string | undefined) {
  return useQuery({
    queryKey: ['materials', id, 'stock-locations'],
    queryFn: () => api.get<MaterialStockLocations>(endpoints.materials.stock(id ?? '')),
    enabled: Boolean(id),
  })
}

/** Categories and units already in use, offered as suggestions so the catalogue stays consistent. */
export function useCatalogueSuggestions() {
  const { data } = useMaterials({ status: 'all' })
  const categories = [...new Set((data ?? []).map((item) => item.category?.trim()).filter((value): value is string => Boolean(value)))].sort()
  const units = [...new Set((data ?? []).map((item) => item.unitOfMeasure.trim()).filter(Boolean))].sort()
  return { categories, units }
}

export function useBranchStockSummary() {
  return useQuery({
    queryKey: ['materials', 'branch-summary'],
    queryFn: () => api.get<BranchStockSummary[]>(endpoints.materials.branchSummary),
    staleTime: 30_000,
  })
}

export function useMaterialMovements(id: string | undefined, branchId?: string) {
  return useQuery({
    queryKey: ['materials', id, 'movements', branchId ?? ''],
    queryFn: () => api.get<StockMovement[]>(endpoints.materials.movements(id ?? ''), { branchId }),
    enabled: Boolean(id),
  })
}

export interface UpsertMaterialPayload {
  itemCode: string
  itemName: string
  category?: string | null
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  unitCost?: number | null
  branchId?: string | null
  barcode?: string | null
  preferredSupplierId?: string | null
  reorderQuantity?: number | null
}

export function useCreateMaterial() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertMaterialPayload) => api.post<Material>(endpoints.materials.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['materials'] }),
  })
}

export function useUpdateMaterial(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertMaterialPayload) => api.put<Material>(endpoints.materials.update(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['materials', id] })
    },
  })
}

export function useDeleteMaterial() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete<void>(endpoints.materials.delete(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['materials'] }),
  })
}

export function useReplenishMaterial(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { branchId?: string | null; quantity: number; unitCost?: number | null; reason?: string | null; referenceNumber?: string | null; batchOrSerial?: string | null }) =>
      api.post<Material>(endpoints.materials.replenish(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['materials', id, 'movements'] })
      void queryClient.invalidateQueries({ queryKey: ['stock-ledger'] })
    },
  })
}

export function useAdjustMaterial(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { branchId?: string | null; quantityChange: number; reason: string; batchOrSerial?: string | null }) =>
      api.post<Material>(endpoints.materials.adjust(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['materials', id, 'movements'] })
      void queryClient.invalidateQueries({ queryKey: ['stock-ledger'] })
    },
  })
}

// Material Requests (restock/job requests)
export function useMaterialRequests(branchId?: string) {
  return useQuery({
    queryKey: ['material-requests', branchId ?? ''],
    queryFn: () => api.get<MaterialRequest[]>(endpoints.materialRequests.list, { branchId }),
    staleTime: 30_000,
  })
}

function useMaterialRequestAction(url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload?: unknown) => api.post<MaterialRequest>(url, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['material-requests'] }),
  })
}

export const useApproveMaterialRequest = (id: string) => useMaterialRequestAction(endpoints.materialRequests.approve(id))
export const useIssueMaterialRequest = (id: string) => useMaterialRequestAction(endpoints.materialRequests.issue(id))
export const useCloseMaterialRequest = (id: string) => useMaterialRequestAction(endpoints.materialRequests.close(id))

// Material Issues
export function useMaterialIssues(branchId?: string, enabled = true) {
  return useQuery({
    enabled,
    queryKey: ['material-issues', branchId ?? ''],
    queryFn: () => api.get<MaterialIssue[]>(endpoints.materialIssues.list, { branchId }),
    staleTime: 30_000,
  })
}

export interface CreateMaterialIssuePayload {
  branchId?: string | null
  workOrderId?: string | null
  projectId?: string | null
  notes?: string | null
  lines: Array<{ materialItemId: string; quantityRequested: number; notes?: string | null }>
}

export function useCreateMaterialIssue() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateMaterialIssuePayload) => api.post<MaterialIssue>(endpoints.materialIssues.create, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['material-issues'] })
      void queryClient.invalidateQueries({ queryKey: ['inventory-requests'] })
      // A slip raised by someone who can issue is issued at once, which moves stock.
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
    },
  })
}

function useMaterialIssueAction(url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload?: unknown) => api.post<MaterialIssue>(url, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['material-issues'] }),
  })
}

export const useApproveMaterialIssue = (id: string) => useMaterialIssueAction(endpoints.materialIssues.approve(id))
export const useRejectMaterialIssue = (id: string) => useMaterialIssueAction(endpoints.materialIssues.reject(id))
export const useIssueMaterialIssue = (id: string) => useMaterialIssueAction(endpoints.materialIssues.issue(id))
export const useReturnMaterialIssue = (id: string) => useMaterialIssueAction(endpoints.materialIssues.return(id))
export const useCancelMaterialIssue = (id: string) => useMaterialIssueAction(endpoints.materialIssues.cancel(id))

// Stock ledger (every movement, tenant-wide)
export interface StockLedgerFilters {
  materialId?: string | undefined
  branchId?: string | undefined
  type?: string | undefined
  from?: string | undefined
  to?: string | undefined
  search?: string | undefined
  page: number
  pageSize: number
}

export function useStockLedger(filters: StockLedgerFilters) {
  return useQuery({
    queryKey: ['stock-ledger', filters],
    queryFn: () =>
      api.get<PagedResult<StockLedgerEntry>>(endpoints.stockMovements.list, {
        materialId: filters.materialId || undefined,
        branchId: filters.branchId || undefined,
        type: filters.type || undefined,
        from: filters.from || undefined,
        to: filters.to || undefined,
        search: filters.search || undefined,
        page: filters.page,
        pageSize: filters.pageSize,
      }),
    staleTime: 15_000,
    placeholderData: keepPreviousData,
  })
}

// Stock Transfers
export function useStockTransfers() {
  return useQuery({
    queryKey: ['stock-transfers'],
    queryFn: () => api.get<StockTransfer[]>(endpoints.stockTransfers.list),
    staleTime: 30_000,
  })
}

export function useCreateStockTransfer() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { fromBranchId: string; toBranchId: string; materialId: string; quantity: number; reason?: string | null }) =>
      api.post<StockTransfer>(endpoints.stockTransfers.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['stock-transfers'] }),
  })
}

function useStockTransferAction(url: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<StockTransfer>(url),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['stock-transfers'] })
      // Completing a transfer moves stock between branches.
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['stock-ledger'] })
    },
  })
}

export const useApproveStockTransfer = (id: string) => useStockTransferAction(endpoints.stockTransfers.approve(id))
export const useCompleteStockTransfer = (id: string) => useStockTransferAction(endpoints.stockTransfers.complete(id))
export const useCancelStockTransfer = (id: string) => useStockTransferAction(endpoints.stockTransfers.cancel(id))

// Branches (used as "Locations")
export function useBranches(options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: ['branches'],
    queryFn: () => api.get<Branch[]>(endpoints.branches.list),
    staleTime: 60_000,
    enabled: options.enabled ?? true,
  })
}

/** One part at one location: a branch, or "Main stores" for a tenant without branches. */
export interface StockLocationRow {
  materialId: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  locationType: 'Branch' | 'Stores'
  /** The branch; null for "Main stores". */
  locationId: string | null
  locationName: string
  quantityOnHand: number
  reorderLevel: number
  unitCost: number | null
  isLowStock: boolean
}

export function useStockByLocation() {
  return useQuery({
    queryKey: ['materials', 'stock-by-location'],
    queryFn: () => api.get<StockLocationRow[]>('/api/materials/stock-by-location'),
    staleTime: 30_000,
  })
}
