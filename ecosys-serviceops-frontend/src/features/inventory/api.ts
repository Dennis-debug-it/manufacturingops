import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { Branch, Material, MaterialIssue, MaterialRequest, StockMovement, StockTransfer } from './types'

export interface MaterialFilters {
  lowStockOnly?: boolean
  branchId?: string
  search?: string
  status?: 'active' | 'inactive' | 'all'
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
    mutationFn: (payload: { branchId?: string | null; quantity: number; unitCost?: number | null; reason?: string | null; referenceNumber?: string | null }) =>
      api.post<Material>(endpoints.materials.replenish(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['materials', id, 'movements'] })
    },
  })
}

export function useAdjustMaterial(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { branchId?: string | null; quantityChange: number; reason?: string | null }) =>
      api.post<Material>(endpoints.materials.adjust(id), payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
      void queryClient.invalidateQueries({ queryKey: ['materials', id, 'movements'] })
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
export function useMaterialIssues(branchId?: string) {
  return useQuery({
    queryKey: ['material-issues', branchId ?? ''],
    queryFn: () => api.get<MaterialIssue[]>(endpoints.materialIssues.list, { branchId }),
    staleTime: 30_000,
  })
}

export interface CreateMaterialIssuePayload {
  branchId?: string | null
  workOrderId?: string | null
  notes?: string | null
  lines: Array<{ materialItemId: string; quantityRequested: number; notes?: string | null }>
}

export function useCreateMaterialIssue() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateMaterialIssuePayload) => api.post<MaterialIssue>(endpoints.materialIssues.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['material-issues'] }),
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
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['stock-transfers'] }),
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
