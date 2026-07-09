import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

// TODO(backend): No PurchaseOrdersController or SuppliersController exists yet (confirmed
// against the 67-controller snapshot in FRONTEND_BUILD_PLAN.md §4.5 / backend-gap table).
// Every hook below is a typed stub returning empty/default data — swap the queryFn/mutationFn
// bodies for real `api.get`/`api.post` calls once dedicated backend endpoints exist.

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

export function usePurchaseOrders(status?: PurchaseOrderStatus | 'All') {
  return useQuery({
    queryKey: ['inventory', 'purchase-orders', status ?? 'All'],
    queryFn: async (): Promise<PurchaseOrderListItem[]> => [],
    staleTime: Infinity,
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
    queryFn: async (): Promise<PurchaseOrderDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
  })
}

export interface CreatePurchaseOrderPayload {
  supplierId: string
  expectedAt?: string | undefined
  lines: Array<{ itemName: string; quantityOrdered: number; unitCost: number }>
  notes?: string | undefined
}

export function useCreatePurchaseOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: CreatePurchaseOrderPayload): Promise<PurchaseOrderDetail> => {
      throw new Error('Purchase orders cannot be created yet — no backend PurchaseOrdersController exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'purchase-orders'] }),
  })
}

export function useReceivePurchaseOrderLine(_poId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: { lineId: string; quantity: number }): Promise<void> => {
      throw new Error('Receiving cannot be recorded yet — no backend PurchaseOrdersController exists.')
    },
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
    queryFn: async (): Promise<SupplierListItem[]> => [],
    staleTime: Infinity,
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
    queryFn: async (): Promise<SupplierDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
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
    mutationFn: async (_payload: CreateSupplierPayload): Promise<SupplierListItem> => {
      throw new Error('Suppliers cannot be created yet — no backend SuppliersController exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['inventory', 'suppliers'] }),
  })
}
