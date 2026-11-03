import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export interface Van {
  id: string
  name: string
  registrationNumber: string | null
  technicianId: string | null
  technicianName: string | null
  homeBranchId: string | null
  homeBranchName: string | null
  isActive: boolean
  distinctItemCount: number
  createdAt: string
}

export interface VanStockLine {
  materialId: string
  materialName: string
  materialCode: string
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  unitCost: number | null
}

export interface UpsertVanPayload {
  name: string
  registrationNumber?: string | undefined
  technicianId?: string | undefined
  homeBranchId?: string | undefined
}

export function useVans(includeInactive = false) {
  return useQuery({
    queryKey: ['inventory', 'vans', includeInactive],
    queryFn: () => api.get<Van[]>(endpoints.vans.list, { includeInactive }),
    staleTime: 30_000,
  })
}

export function useVan(id: string | undefined) {
  return useQuery({
    queryKey: ['inventory', 'vans', 'detail', id],
    queryFn: () => api.get<Van>(endpoints.vans.detail(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useVanStock(id: string | undefined) {
  return useQuery({
    queryKey: ['inventory', 'vans', id, 'stock'],
    queryFn: () => api.get<VanStockLine[]>(endpoints.vans.stock(id ?? '')),
    enabled: Boolean(id),
    staleTime: 15_000,
  })
}

export function useCreateVan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertVanPayload) => api.post<Van>(endpoints.vans.create, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'vans'] }),
  })
}

export function useUpdateVan(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpsertVanPayload) => api.put<Van>(endpoints.vans.update(id), payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'vans'] }),
  })
}

export function useDeactivateVan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.delete(endpoints.vans.deactivate(id)),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'vans'] }),
  })
}

interface VanMovePayload {
  materialId: string
  branchId: string
  quantity: number
  reason?: string | undefined
}

interface VanConsumePayload {
  materialId: string
  quantity: number
  workOrderId?: string | undefined
  reason?: string | undefined
}

function invalidateVan(queryClient: ReturnType<typeof useQueryClient>, vanId: string) {
  void queryClient.invalidateQueries({ queryKey: ['inventory', 'vans', vanId, 'stock'] })
  void queryClient.invalidateQueries({ queryKey: ['inventory', 'vans'] })
  void queryClient.invalidateQueries({ queryKey: ['materials'] })
}

export function useLoadVan(vanId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: VanMovePayload) => api.post(endpoints.vans.load(vanId), payload),
    onSuccess: () => invalidateVan(queryClient, vanId),
  })
}

export function useReturnVanStock(vanId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: VanMovePayload) => api.post(endpoints.vans.return(vanId), payload),
    onSuccess: () => invalidateVan(queryClient, vanId),
  })
}

export function useConsumeVanStock(vanId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: VanConsumePayload) => api.post(endpoints.vans.consume(vanId), payload),
    onSuccess: () => invalidateVan(queryClient, vanId),
  })
}
