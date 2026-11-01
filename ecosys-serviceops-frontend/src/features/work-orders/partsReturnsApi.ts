import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

export type PartsReturnStatus = 'Pending' | 'Received' | 'Rejected'

/**
 * What happens on receipt (PartsReturnsController.Receive):
 * - Unused: back on the shelf, and off the job's usage so it isn't billed.
 * - Faulty: off the job's usage, but not restocked (held for supplier return / write-off).
 * - Removed: an old part taken out of the client's equipment; recorded only.
 */
export type PartsReturnCondition = 'Unused' | 'Faulty' | 'Removed'

export interface PartsReturnLine {
  id: string
  materialItemId: string
  itemCode: string | null
  itemName: string
  workOrderMaterialUsageId: string | null
  quantity: number
  condition: PartsReturnCondition
  notes: string | null
}

export interface PartsReturn {
  id: string
  returnNumber: string
  workOrderId: string
  workOrderNumber: string | null
  status: PartsReturnStatus
  requestedByName: string | null
  requestedAt: string
  processedByName: string | null
  processedAt: string | null
  notes: string | null
  rejectionReason: string | null
  lines: PartsReturnLine[]
}

export interface ReturnablePart {
  workOrderMaterialUsageId: string
  materialItemId: string
  itemCode: string | null
  itemName: string
  unitOfMeasure: string | null
  quantityUsed: number
  pendingReturnQuantity: number
  returnableQuantity: number
  usedAt: string
}

export interface CreatePartsReturnPayload {
  notes: string | null
  lines: Array<{
    workOrderMaterialUsageId: string | null
    materialItemId: string | null
    quantity: number
    condition: PartsReturnCondition
    notes: string | null
  }>
}

function invalidateReturns(queryClient: ReturnType<typeof useQueryClient>, workOrderId?: string) {
  void queryClient.invalidateQueries({ queryKey: ['parts-returns'] })
  // Receiving puts unused parts back on the shelf.
  void queryClient.invalidateQueries({ queryKey: ['materials'] })
  if (workOrderId) {
    // Receiving changes the job's material usage (and so billing and the execution bundle).
    void queryClient.invalidateQueries({ queryKey: ['work-orders', workOrderId] })
  }
}

export function usePartsReturnsForWorkOrder(workOrderId: string) {
  return useQuery({
    queryKey: ['parts-returns', 'work-order', workOrderId],
    queryFn: () => api.get<PartsReturn[]>(endpoints.workOrders.partsReturns(workOrderId)),
    enabled: Boolean(workOrderId),
  })
}

export function useReturnableParts(workOrderId: string, enabled: boolean) {
  return useQuery({
    queryKey: ['parts-returns', 'returnable', workOrderId],
    queryFn: () => api.get<ReturnablePart[]>(endpoints.workOrders.returnableParts(workOrderId)),
    enabled: Boolean(workOrderId) && enabled,
  })
}

export function usePartsReturns(status: string) {
  return useQuery({
    queryKey: ['parts-returns', 'list', status],
    queryFn: () => api.get<PartsReturn[]>(endpoints.partsReturns.list, { status }),
  })
}

export function useCreatePartsReturn(workOrderId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePartsReturnPayload) => api.post<PartsReturn>(endpoints.workOrders.partsReturns(workOrderId), payload),
    onSuccess: () => invalidateReturns(queryClient, workOrderId),
  })
}

export function useReceivePartsReturn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<PartsReturn>(endpoints.partsReturns.receive(id), {}),
    onSuccess: (result) => invalidateReturns(queryClient, result.workOrderId),
  })
}

export function useRejectPartsReturn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => api.post<PartsReturn>(endpoints.partsReturns.reject(id), { reason }),
    onSuccess: (result) => invalidateReturns(queryClient, result.workOrderId),
  })
}
