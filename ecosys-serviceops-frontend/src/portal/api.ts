import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portalApi } from './api/portalApiClient'
import { endpoints } from '../shared/api/endpoints'

export interface PortalDashboardSummary {
  openWorkOrders: number
  overdueWorkOrders: number
  assetCount: number
  upcomingPmCount: number
  outstandingInvoiceBalance: number
  pendingQuotations: number
}

export function usePortalDashboardSummary() {
  return useQuery({ queryKey: ['portal', 'dashboard'], queryFn: () => portalApi.get<PortalDashboardSummary>(endpoints.portal.dashboardSummary), staleTime: 30_000 })
}

export interface PortalWorkOrderListItem {
  id: string
  workOrderNumber: string
  title: string
  status: string
  priority: string
  siteName: string | null
  assetName: string | null
  dueDate: string | null
  createdAt: string
}

export interface PortalWorkOrderDetail extends PortalWorkOrderListItem {
  description: string | null
  workType: string
  scheduledStartAt: string | null
  completedAt: string | null
  workDoneNotes: string | null
}

export function usePortalWorkOrders(status?: string) {
  return useQuery({
    queryKey: ['portal', 'work-orders', status ?? ''],
    queryFn: () => portalApi.get<PortalWorkOrderListItem[]>(endpoints.portal.workOrders, status ? { status } : undefined),
    staleTime: 30_000,
  })
}

export function usePortalWorkOrder(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'work-orders', id],
    queryFn: () => portalApi.get<PortalWorkOrderDetail>(endpoints.portal.workOrder(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface CreatePortalWorkOrderPayload {
  title: string
  description?: string | undefined
  priority?: string | undefined
  siteId?: string | undefined
  assetId?: string | undefined
}

export function useCreatePortalWorkOrder() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: CreatePortalWorkOrderPayload) => portalApi.post<PortalWorkOrderDetail>(endpoints.portal.workOrders, payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['portal', 'work-orders'] }),
  })
}

export interface PortalAssetListItem {
  id: string
  assetName: string
  assetCode: string
  assetType: string | null
  siteName: string | null
  manufacturer: string | null
  model: string | null
  status: string
  warrantyExpiryDate: string | null
  nextPmDate: string | null
}

export function usePortalAssets() {
  return useQuery({ queryKey: ['portal', 'assets'], queryFn: () => portalApi.get<PortalAssetListItem[]>(endpoints.portal.assets), staleTime: 60_000 })
}

export interface PortalMaintenancePlanItem {
  id: string
  assetName: string
  frequency: string
  status: string
  lastPmDate: string | null
  nextPmDate: string | null
}

export function usePortalMaintenancePlans() {
  return useQuery({ queryKey: ['portal', 'maintenance'], queryFn: () => portalApi.get<PortalMaintenancePlanItem[]>(endpoints.portal.maintenance), staleTime: 60_000 })
}

export interface PortalInvoiceListItem {
  id: string
  invoiceNumber: string
  status: string
  invoiceDate: string
  dueDate: string
  totalAmount: number
  amountPaid: number
  balance: number
  currency: string
}

export interface PortalInvoiceLine {
  description: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  taxAmount: number
  grandTotal: number
}

export interface PortalPayment {
  id: string
  paymentDate: string
  amount: number
  paymentMethod: string
  referenceNumber: string | null
}

export interface PortalInvoiceDetail extends PortalInvoiceListItem {
  subtotal: number
  taxAmount: number
  notes: string | null
  lines: PortalInvoiceLine[]
  payments: PortalPayment[]
}

export function usePortalInvoices() {
  return useQuery({ queryKey: ['portal', 'invoices'], queryFn: () => portalApi.get<PortalInvoiceListItem[]>(endpoints.portal.invoices), staleTime: 30_000 })
}

export function usePortalInvoice(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'invoices', id],
    queryFn: () => portalApi.get<PortalInvoiceDetail>(endpoints.portal.invoice(id ?? '')),
    enabled: Boolean(id),
  })
}

export interface PortalQuotationListItem {
  id: string
  quoteNumber: string
  status: string
  quoteDate: string
  validUntil: string
  totalAmount: number
  currency: string
}

export interface PortalQuotationLine {
  description: string
  quantity: number
  unit: string
  unitRate: number
  lineTotal: number
  grandTotal: number
}

export interface PortalQuotationDetail extends PortalQuotationListItem {
  subject: string | null
  subtotal: number
  taxAmount: number
  rejectionReason: string | null
  lines: PortalQuotationLine[]
}

export function usePortalQuotations() {
  return useQuery({ queryKey: ['portal', 'quotations'], queryFn: () => portalApi.get<PortalQuotationListItem[]>(endpoints.portal.quotations), staleTime: 30_000 })
}

export function usePortalQuotation(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'quotations', id],
    queryFn: () => portalApi.get<PortalQuotationDetail>(endpoints.portal.quotation(id ?? '')),
    enabled: Boolean(id),
  })
}

export function useAcceptPortalQuotation(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => portalApi.post<PortalQuotationDetail>(endpoints.portal.quotationAccept(id)),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['portal', 'quotations'] })
    },
  })
}

export function useRejectPortalQuotation(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason?: string) => portalApi.post<PortalQuotationDetail>(endpoints.portal.quotationReject(id), { reason }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['portal', 'quotations'] })
    },
  })
}

export interface PortalContractListItem {
  id: string
  contractNumber: string
  name: string
  status: string
  contractType: string
  startDate: string
  endDate: string
}

export function usePortalContracts() {
  return useQuery({ queryKey: ['portal', 'contracts'], queryFn: () => portalApi.get<PortalContractListItem[]>(endpoints.portal.contracts), staleTime: 60_000 })
}

export interface PortalProfile {
  fullName: string
  email: string
  phone: string | null
  clientName: string
  lastLoginAt: string | null
}

export function usePortalProfile() {
  return useQuery({ queryKey: ['portal', 'profile'], queryFn: () => portalApi.get<PortalProfile>(endpoints.portal.profile), staleTime: 60_000 })
}
