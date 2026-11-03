import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export type PartsRequestStatus = 'Pending' | 'Approved' | 'Dispatched' | 'Delivered' | 'Rejected'

export interface PartsRequestItem {
  id: string
  inventoryItemId: string | null
  itemCode: string | null
  itemName: string | null
  description: string | null
  quantityRequested: number
  quantityApproved: number | null
}

export interface PartsRequest {
  id: string
  workOrderId: string
  workOrderNumber: string | null
  technicianId: string
  technicianName: string | null
  branchId: string | null
  branchName: string | null
  status: PartsRequestStatus
  notes: string | null
  rejectionReason: string | null
  requestedAt: string
  updatedAt: string | null
  items: PartsRequestItem[]
}

export interface PartsRequestFilters {
  status?: string | undefined
  technicianId?: string | undefined
  workOrderId?: string | undefined
  branchId?: string | undefined
  fromDate?: string | undefined
  toDate?: string | undefined
}

export interface CreatePartsRequestPayload {
  notes?: string | null
  items: Array<{ inventoryItemId?: string | null; description?: string | null; quantityRequested: number }>
}

function invalidatePartsRequests(queryClient: ReturnType<typeof useQueryClient>, workOrderId?: string) {
  void queryClient.invalidateQueries({ queryKey: ['parts-requests'] })
  // The stores queue (and its menu badge) lists these too.
  void queryClient.invalidateQueries({ queryKey: ['inventory-requests'] })
  // Dispatching moves stock.
  void queryClient.invalidateQueries({ queryKey: ['materials'] })
  if (workOrderId) {
    void queryClient.invalidateQueries({ queryKey: ['work-orders', workOrderId, 'parts-requests'] })
  }
}

export function usePartsRequestsForWorkOrder(workOrderId: string | undefined) {
  return useQuery({
    queryKey: ['work-orders', workOrderId, 'parts-requests'],
    queryFn: () => api.get<PartsRequest[]>(endpoints.workOrders.partsRequests(workOrderId ?? '')),
    enabled: Boolean(workOrderId),
  })
}

export function useCreatePartsRequest(workOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePartsRequestPayload) => api.post<PartsRequest>(endpoints.workOrders.partsRequests(workOrderId), payload),
    onSuccess: () => invalidatePartsRequests(queryClient, workOrderId),
  })
}

export function usePartsRequests(filters?: PartsRequestFilters) {
  return useQuery({
    queryKey: ['parts-requests', filters ?? {}],
    queryFn: () =>
      api.get<PartsRequest[]>(endpoints.partsRequests.list, {
        status: filters?.status,
        technicianId: filters?.technicianId,
        workOrderId: filters?.workOrderId,
        branchId: filters?.branchId,
        fromDate: filters?.fromDate,
        toDate: filters?.toDate,
      }),
    staleTime: 15_000,
  })
}

export function useApprovePartsRequest(workOrderId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { id: string; items?: Array<{ partsRequestItemId: string; quantityApproved: number }> }) =>
      api.patch<PartsRequest>(endpoints.partsRequests.approve(payload.id), { items: payload.items }),
    onSuccess: () => invalidatePartsRequests(queryClient, workOrderId),
  })
}

export function useRejectPartsRequest(workOrderId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { id: string; reason: string }) =>
      api.patch<PartsRequest>(endpoints.partsRequests.reject(payload.id), { reason: payload.reason }),
    onSuccess: () => invalidatePartsRequests(queryClient, workOrderId),
  })
}

export function useDispatchPartsRequest(workOrderId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.patch<PartsRequest>(endpoints.partsRequests.dispatch(id)),
    onSuccess: () => invalidatePartsRequests(queryClient, workOrderId),
  })
}

export function useDeliverPartsRequest(workOrderId?: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.patch<PartsRequest>(endpoints.partsRequests.deliver(id)),
    onSuccess: () => invalidatePartsRequests(queryClient, workOrderId),
  })
}
