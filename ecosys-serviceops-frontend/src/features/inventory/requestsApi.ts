import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'

/** Which record a queue row is. Each keeps its own actions; the queue only lists them together. */
export type InventoryRequestSource = 'PartsRequest' | 'IssueSlip' | 'Restock'

/** One flow for every request. Rejected and Cancelled end it early. */
export type InventoryRequestStage = 'Requested' | 'Approved' | 'Issued' | 'Completed' | 'Rejected' | 'Cancelled'

export interface InventoryRequestLine {
  id: string
  materialItemId: string | null
  itemCode: string | null
  itemName: string | null
  unitOfMeasure: string | null
  quantityRequested: number
  /** Job parts requests only; null until approved. */
  quantityApproved: number | null
  quantityIssued: number
  quantityReturned: number
}

export interface InventoryRequest {
  id: string
  source: InventoryRequestSource
  /** Null for a job parts request, which is known by its work order. */
  number: string | null
  stage: InventoryRequestStage
  /** The record's own status. */
  status: string
  workOrderId: string | null
  workOrderNumber: string | null
  requestedByName: string | null
  branchId: string | null
  branchName: string | null
  notes: string | null
  rejectionReason: string | null
  value: number
  createdAt: string
  lines: InventoryRequestLine[]
}

export const sourceLabels: Record<InventoryRequestSource, string> = {
  PartsRequest: 'Job parts request',
  IssueSlip: 'Issue slip',
  Restock: 'Restock (retired)',
}

export function requestReference(request: InventoryRequest) {
  return request.number ?? (request.workOrderNumber ? `Parts · ${request.workOrderNumber}` : 'Parts request')
}

const queueKey = ['inventory-requests'] as const

export function useInventoryRequests() {
  return useQuery({
    queryKey: queueKey,
    queryFn: () => api.get<InventoryRequest[]>('/api/inventory-requests'),
    staleTime: 15_000,
    refetchInterval: 60_000,
  })
}

function invalidateAfterRequestAction(queryClient: QueryClient, touchesStock: boolean) {
  void queryClient.invalidateQueries({ queryKey: queueKey })
  void queryClient.invalidateQueries({ queryKey: ['parts-requests'] })
  void queryClient.invalidateQueries({ queryKey: ['material-issues'] })
  void queryClient.invalidateQueries({ queryKey: ['material-requests'] })
  if (touchesStock) {
    void queryClient.invalidateQueries({ queryKey: ['materials'] })
    void queryClient.invalidateQueries({ queryKey: ['stock-ledger'] })
    void queryClient.invalidateQueries({ queryKey: ['work-orders'] })
  }
}

function useRequestAction<TPayload>(request: (payload: TPayload) => Promise<unknown>, touchesStock = false) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: request,
    onSuccess: () => invalidateAfterRequestAction(queryClient, touchesStock),
  })
}

/** Approve: job parts requests take per-line approved quantities; issue slips and restock approve as asked. */
export function useApproveRequest() {
  return useRequestAction((payload: { request: InventoryRequest; quantities?: Record<string, number> }) => {
    const { request, quantities } = payload
    if (request.source === 'PartsRequest') {
      return api.patch(endpoints.partsRequests.approve(request.id), {
        items: quantities ? Object.entries(quantities).map(([partsRequestItemId, quantityApproved]) => ({ partsRequestItemId, quantityApproved })) : undefined,
      })
    }
    if (request.source === 'IssueSlip') return api.post(endpoints.materialIssues.approve(request.id))
    return api.post(endpoints.materialRequests.approve(request.id))
  })
}

export function useRejectRequest() {
  return useRequestAction((payload: { request: InventoryRequest; reason: string }) => {
    const { request, reason } = payload
    if (request.source === 'PartsRequest') return api.patch(endpoints.partsRequests.reject(request.id), { reason })
    return api.post(endpoints.materialIssues.reject(request.id), { reason })
  })
}

export function useCancelRequest() {
  return useRequestAction((request: InventoryRequest) => api.post(endpoints.materialIssues.cancel(request.id)))
}

/**
 * Hand the parts over. Issue slips and restock requests take per-line quantities; a job parts
 * request dispatches its approved quantities, optionally from another branch.
 */
export function useIssueRequest() {
  return useRequestAction((payload: { request: InventoryRequest; quantities: Record<string, number>; sourceBranchId?: string | null }) => {
    const { request, quantities, sourceBranchId } = payload
    if (request.source === 'PartsRequest') {
      return api.patch(endpoints.partsRequests.dispatch(request.id), { sourceBranchId: sourceBranchId || null })
    }
    if (request.source === 'IssueSlip') {
      return api.post(endpoints.materialIssues.issue(request.id), {
        lines: Object.entries(quantities).map(([materialIssueLineId, quantityIssued]) => ({ materialIssueLineId, quantityIssued })),
      })
    }
    return api.post(endpoints.materialRequests.issue(request.id), {
      lines: Object.entries(quantities).map(([materialRequestLineId, quantityIssued]) => ({ materialRequestLineId, quantityIssued })),
    })
  }, true)
}

/** Stock coming back to stores against an issue slip. */
export function useReturnRequest() {
  return useRequestAction((payload: { request: InventoryRequest; quantities: Record<string, number> }) => {
    const { request, quantities } = payload
    const lines = Object.entries(quantities)
      .filter(([, quantity]) => quantity > 0)
      .map(([materialIssueLineId, quantityReturned]) => ({
        materialIssueLineId,
        quantityReturned,
        expectedQuantityReturned: request.lines.find((line) => line.id === materialIssueLineId)?.quantityReturned ?? 0,
      }))
    return api.post(endpoints.materialIssues.return(request.id), { lines })
  }, true)
}

/** Close a retired restock request: what was used on the job and what came back. */
export function useCloseRestockRequest() {
  return useRequestAction((payload: { request: InventoryRequest; lines: Record<string, { used: number; returned: number }> }) =>
    api.post(endpoints.materialRequests.close(payload.request.id), {
      lines: Object.entries(payload.lines).map(([materialRequestLineId, value]) => ({
        materialRequestLineId,
        quantityUsed: value.used,
        quantityReturned: value.returned,
      })),
    }), true)
}
