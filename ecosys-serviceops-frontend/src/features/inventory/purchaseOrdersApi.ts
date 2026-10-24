import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

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
}

export interface PurchaseOrderDetail extends PurchaseOrderListItem {
  supplierId: string
  notes: string | null
  lines: PurchaseOrderLine[]
  receivingHistory: Array<{ receivedAt: string; receivedBy: string; itemName: string; quantity: number }>
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
  lines: Array<{ itemName: string; quantityOrdered: number; unitCost: number }>
  notes?: string | undefined
  projectId?: string | null
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
    mutationFn: (payload: { lineId: string; quantity: number }) =>
      api.post<void>(`/api/purchase-orders/${poId}/receive`, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
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
}

export function useCreateSupplier() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreateSupplierPayload) => api.post<SupplierListItem>('/api/suppliers', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'suppliers'] }),
  })
}
