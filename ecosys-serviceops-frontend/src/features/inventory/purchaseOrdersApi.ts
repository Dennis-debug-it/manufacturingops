import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'

export type PurchaseOrderStatus = 'Draft' | 'Sent' | 'PartiallyReceived' | 'Received' | 'Cancelled'

export interface PurchaseOrderListItem {
  id: string
  poNumber: string
  supplierName: string
  status: PurchaseOrderStatus
  orderedAt: string
  expectedAt: string | null
  totalAmount: number
  currency: string
}

/** `state` is `open` (not yet fully received) or `late` (sent and past its expected date). */
export function usePurchaseOrders(status?: PurchaseOrderStatus | 'All', state?: 'open' | 'late') {
  return useQuery({
    queryKey: ['inventory', 'purchase-orders', status ?? 'All', state ?? ''],
    queryFn: () =>
      api.get<PurchaseOrderListItem[]>('/api/purchase-orders', {
        ...(status && status !== 'All' ? { status } : {}),
        ...(state ? { state } : {}),
      }),
    staleTime: 30_000,
  })
}

export interface PurchaseOrderLine {
  id: string
  itemName: string
  quantityOrdered: number
  quantityReceived: number
  unitCost: number
  lineTotal: number
  /** The stocked part this line buys; receiving it adds stock. Null for non-stock lines. */
  materialItemId: string | null
  itemCode: string | null
}

export interface PurchaseOrderDetail extends PurchaseOrderListItem {
  supplierId: string
  notes: string | null
  lines: PurchaseOrderLine[]
  receivingHistory: Array<{ receivedAt: string; receivedBy: string; itemName: string; quantity: number; branchName: string | null }>
}

export function usePurchaseOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['inventory', 'purchase-orders', id],
    queryFn: () => api.get<PurchaseOrderDetail>(`/api/purchase-orders/${id}`),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}

export interface CreatePurchaseOrderPayload {
  supplierId: string
  expectedAt?: string | undefined
  lines: Array<{ itemName: string; quantityOrdered: number; unitCost: number; materialItemId?: string | null }>
  notes?: string | undefined
  projectId?: string | null
  /** Three-letter code; the workspace currency when omitted. */
  currency?: string
}

export function useCreatePurchaseOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePurchaseOrderPayload) =>
      api.post<PurchaseOrderDetail>('/api/purchase-orders', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
  })
}

export function useSetPurchaseOrderStatus(poId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (status: 'Sent' | 'Cancelled') =>
      api.post<void>(`/api/purchase-orders/${poId}/status`, { status }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
  })
}

export function useReceivePurchaseOrderLine(poId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { lineId: string; quantity: number; branchId?: string | null; batchOrSerial?: string | null }) =>
      api.post<void>(`/api/purchase-orders/${poId}/receive`, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] })
      // A part-linked receipt changes stock levels and costs.
      void queryClient.invalidateQueries({ queryKey: ['materials'] })
    },
  })
}

export interface SupplierListItem {
  id: string
  supplierName: string
  contactPerson: string | null
  phone: string | null
  email: string | null
  onTimeDeliveryPercent: number
  openPurchaseOrders: number
  status: 'Active' | 'Inactive'
  leadTimeDays: number | null
}

export function useSuppliers() {
  return useQuery({
    queryKey: ['inventory', 'suppliers'],
    queryFn: () => api.get<SupplierListItem[]>('/api/suppliers'),
    staleTime: 30_000,
  })
}

export interface SupplierDetail extends SupplierListItem {
  address: string | null
  notes: string | null
  suppliedParts: Array<{ itemName: string; lastUnitCost: number; lastSuppliedAt: string }>
  recentPurchaseOrders: PurchaseOrderListItem[]
  preferredParts: Array<{ id: string; itemCode: string; itemName: string; quantityOnHand: number; reorderLevel: number }> | null
}

export function useSupplier(id: string | undefined) {
  return useQuery({
    queryKey: ['inventory', 'suppliers', id],
    queryFn: () => api.get<SupplierDetail>(`/api/suppliers/${id}`),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}

export interface CreateSupplierPayload {
  supplierName: string
  contactPerson?: string | undefined
  phone?: string | undefined
  email?: string | undefined
  address?: string | undefined
  notes?: string | undefined
  leadTimeDays?: number | null
}

export function useCreateSupplier() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateSupplierPayload) => api.post<SupplierListItem>('/api/suppliers', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'suppliers'] }),
  })
}

export function useUpdateSupplier(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateSupplierPayload) => api.put<SupplierListItem>(`/api/suppliers/${id}`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'suppliers'] }),
  })
}

export function useSetSupplierStatus(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (isActive: boolean) => api.post<void>(`/api/suppliers/${id}/status`, { isActive }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'suppliers'] }),
  })
}

export interface ReorderSuggestion {
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  quantityOnHand: number
  reorderLevel: number
  /** Outstanding on open purchase orders, across all branches. */
  onOrder: number
  suggestedQuantity: number
  unitCost: number | null
  preferredSupplierId: string | null
  preferredSupplierName: string | null
  preferredSupplierActive: boolean
  leadTimeDays: number
  isCritical: boolean
  /** Stock plus what is on order already clears the reorder level. */
  coveredByOpenOrders: boolean
}

export function useReorderSuggestions(branchId?: string) {
  return useQuery({
    queryKey: ['inventory', 'reorder-suggestions', branchId ?? ''],
    queryFn: () => api.get<ReorderSuggestion[]>('/api/purchase-orders/reorder-suggestions', { branchId: branchId || undefined }),
    staleTime: 30_000,
  })
}

export function useCreateOrdersFromSuggestions() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { items: Array<{ materialItemId: string; quantity: number; supplierId?: string | null }>; notes?: string | null }) =>
      api.post<PurchaseOrderListItem[]>('/api/purchase-orders/from-suggestions', payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] })
      void queryClient.invalidateQueries({ queryKey: ['inventory', 'reorder-suggestions'] })
    },
  })
}

/** Edits a draft purchase order (lines are replaced as a whole). A changed total restarts approval. */
export function useUpdatePurchaseOrder(poId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePurchaseOrderPayload) => api.put<PurchaseOrderDetail>(`/api/purchase-orders/${poId}`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
  })
}

export interface PurchaseOrderEmailPayload {
  /** Defaults to the supplier's email. */
  to?: string | null
  cc?: string[]
  message?: string | null
  /** Marks a draft Sent once queued (default true). */
  markSent?: boolean
}

/** Emails the PDF to the supplier; a draft is marked Sent. */
export function useEmailPurchaseOrder(poId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: PurchaseOrderEmailPayload) =>
      api.post<{ to: string; cc: string[]; status: string }>(`/api/purchase-orders/${poId}/email`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
  })
}

/** Downloads the purchase order PDF in the workspace's purchase-order design. */
export async function downloadPurchaseOrderPdf(poId: string, poNumber: string) {
  const response = await apiClient.get(`/api/purchase-orders/${poId}/pdf`, { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `${poNumber}.pdf`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
