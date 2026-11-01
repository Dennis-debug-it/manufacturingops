import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { portalApi, portalApiClient } from './api/portalApiClient'
import { endpoints } from '../shared/api/endpoints'
import { formatCurrency } from '../shared/utils/format'

/** What the tenant allows this portal to show and do (Settings → Client Portal). Enforced by the API too. */
export interface PortalSettings {
  allowRequestSubmission: boolean
  allowQuoteResponses: boolean
  showInvoices: boolean
  showContracts: boolean
  showAssetRegister: boolean
  welcomeMessage: string | null
  /** True when the tenant licenses Projects and shows it in the portal. */
  showProjects?: boolean
}

export function usePortalSettings() {
  return useQuery({ queryKey: ['portal', 'settings'], queryFn: () => portalApi.get<PortalSettings>(endpoints.portal.settings), staleTime: 5 * 60_000 })
}

export interface PortalCurrencyAmount {
  currency: string
  amount: number
}

/** Money is never summed across currencies; each currency is shown on its own. */
export function formatCurrencyAmounts(amounts: PortalCurrencyAmount[], fallbackCurrency: string) {
  return amounts.length === 0
    ? formatCurrency(0, fallbackCurrency)
    : amounts.map((entry) => formatCurrency(entry.amount, entry.currency)).join(' · ')
}

export interface PortalDashboardSummary {
  openWorkOrders: number
  overdueWorkOrders: number
  assetCount: number
  upcomingPmCount: number
  /** One entry per currency with an outstanding balance; empty when nothing is owed or invoices are hidden. */
  outstandingBalances: PortalCurrencyAmount[]
  pendingQuotations: number
  /** The tenant's default currency, used to render a zero balance. */
  currency: string
  /** Assets whose warranty has not yet expired (including those expiring soon). */
  assetsUnderWarranty: number
}

export function usePortalDashboardSummary() {
  return useQuery({ queryKey: ['portal', 'dashboard'], queryFn: () => portalApi.get<PortalDashboardSummary>(endpoints.portal.dashboardSummary), staleTime: 30_000 })
}

export interface PortalInvoiceCurrencyTotals {
  currency: string
  invoiced: number
  paid: number
  outstanding: number
}

export interface PortalReportSummary {
  totalRequests: number
  openRequests: number
  completedRequests: number
  closedRequests: number
  cancelledRequests: number
  overdueRequests: number
  currency: string
  invoiceTotals: PortalInvoiceCurrencyTotals[]
}

export function usePortalReportSummary() {
  return useQuery({ queryKey: ['portal', 'reports'], queryFn: () => portalApi.get<PortalReportSummary>(endpoints.portal.reportSummary), staleTime: 30_000 })
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

export interface PortalWorkOrderNote {
  id: string
  content: string
  createdByUserName: string | null
  createdAt: string
}

export function usePortalWorkOrderNotes(id: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'work-orders', id, 'notes'],
    queryFn: () => portalApi.get<PortalWorkOrderNote[]>(endpoints.portal.workOrderNotes(id ?? '')),
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
  /** Worked out by the server on the workspace's date; null when no warranty is recorded. */
  warrantyStatus: 'Under Warranty' | 'Expiring Soon' | 'Out of Warranty' | null
  /** Days of cover left including today; null when out of warranty or unknown. */
  warrantyDaysLeft: number | null
}

export function usePortalAssets(enabled = true) {
  return useQuery({ queryKey: ['portal', 'assets'], queryFn: () => portalApi.get<PortalAssetListItem[]>(endpoints.portal.assets), staleTime: 60_000, enabled })
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
  /** The client's signed acceptance: the name typed as the signature, and when. */
  acceptedByName?: string | null
  acceptedByTitle?: string | null
  acceptedAt?: string | null
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
    mutationFn: (signature: { signedName: string; title: string | null; agreed: boolean }) =>
      portalApi.post<PortalQuotationDetail>(endpoints.portal.quotationAccept(id), signature),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['portal', 'quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['portal', 'dashboard'] })
    },
  })
}

export function useRejectPortalQuotation(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (reason: string) => portalApi.post<PortalQuotationDetail>(endpoints.portal.quotationReject(id), { reason }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['portal', 'quotations'] })
      void queryClient.invalidateQueries({ queryKey: ['portal', 'dashboard'] })
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

export interface PortalProjectListItem {
  id: string
  projectNumber: string
  name: string
  siteName: string | null
  stageName: string
  stageCategory: string
  currency: string
  contractValue: number
  progressPercent: number
  plannedProgressPercent: number
  plannedStartDate: string | null
  plannedEndDate: string | null
}

export interface PortalProjectDetail {
  summary: PortalProjectListItem
  description: string | null
  actualStartDate: string | null
  actualEndDate: string | null
  approvedAdditions: number
  approvedOmissions: number
  milestones: Array<{ name: string; plannedDate: string | null; status: string; isOverdue: boolean }>
  variations: Array<{ variationNumber: string; kind: string; title: string; amount: number; clientReference: string | null; approvedAt: string | null }>
  documents: Array<{ id: string; fileName: string; fileSize: number; mimeType: string; createdAt: string }>
}

export function usePortalProjects() {
  return useQuery({ queryKey: ['portal', 'projects'], queryFn: () => portalApi.get<PortalProjectListItem[]>(endpoints.portal.projects), staleTime: 60_000 })
}

export function usePortalProject(id: string | undefined) {
  return useQuery({
    enabled: Boolean(id),
    queryKey: ['portal', 'projects', id],
    queryFn: () => portalApi.get<PortalProjectDetail>(endpoints.portal.project(id ?? '')),
    staleTime: 60_000,
  })
}

/** A certified (or invoiced) valuation on a project. Drafts and submitted valuations stay internal. */
export interface PortalProjectValuation {
  valuationNumber: string
  periodEnd: string
  certificateReference: string | null
  certifiedOn: string | null
  grossValueToDate: number
  retentionToDate: number
  certifiedAmount: number
}

export interface PortalProjectBilling {
  currency: string
  valuations: PortalProjectValuation[]
  invoices: PortalInvoiceListItem[]
  retentionHeld: number
}

/**
 * Certified valuations, invoices and retention held on a project. The API answers 403 when the
 * tenant has no Finance module or hides invoices in the portal, so the global error toast is
 * suppressed and callers hide the section quietly.
 */
export function usePortalProjectBilling(id: string | undefined, enabled = true) {
  return useQuery({
    enabled: Boolean(id) && enabled,
    queryKey: ['portal', 'projects', id, 'billing'],
    queryFn: () => portalApi.get<PortalProjectBilling>(endpoints.portal.projectBilling(id ?? ''), undefined, { suppressGlobalErrorToast: true }),
    staleTime: 60_000,
  })
}

/** Downloads a document the tenant has shared on this project. */
export async function downloadPortalProjectDocument(projectId: string, document: { id: string; fileName: string }) {
  const response = await portalApiClient.get(endpoints.portal.projectDocument(projectId, document.id), { responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = document.fileName
  link.click()
  URL.revokeObjectURL(url)
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

export interface UpdatePortalProfilePayload {
  fullName: string
  phone?: string | undefined
}

export function useUpdatePortalProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: UpdatePortalProfilePayload) => portalApi.put<PortalProfile>(endpoints.portal.profile, payload, { suppressGlobalErrorToast: true }),
    onSuccess: (data) => void queryClient.setQueryData(['portal', 'profile'], data),
  })
}

export interface PortalKnowledgeCategory {
  id: string
  name: string
  description: string | null
  icon: string | null
  articleCount: number
}

export function usePortalKnowledgeCategories() {
  return useQuery({
    queryKey: ['portal', 'knowledge', 'categories'],
    queryFn: () => portalApi.get<PortalKnowledgeCategory[]>(endpoints.portal.knowledgeCategories),
    staleTime: 60_000,
  })
}

export interface PortalKnowledgeArticleListItem {
  id: string
  title: string
  slug: string
  summary: string | null
  categoryId: string | null
  categoryName: string | null
  publishedAt: string | null
  viewCount: number
  helpfulCount: number
  tags: string[]
}

export function usePortalKnowledgeArticles(filters?: { categoryId?: string | undefined; search?: string | undefined }) {
  return useQuery({
    queryKey: ['portal', 'knowledge', 'articles', filters?.categoryId ?? '', filters?.search ?? ''],
    queryFn: () =>
      portalApi.get<PortalKnowledgeArticleListItem[]>(endpoints.portal.knowledgeArticles, {
        categoryId: filters?.categoryId,
        search: filters?.search,
      }),
    staleTime: 30_000,
  })
}

export interface PortalKnowledgeArticleDetail {
  id: string
  title: string
  slug: string
  summary: string | null
  body: string
  categoryId: string | null
  categoryName: string | null
  publishedAt: string | null
  viewCount: number
  helpfulCount: number
  notHelpfulCount: number
  tags: string[]
}

export function usePortalKnowledgeArticle(slug: string | undefined) {
  return useQuery({
    queryKey: ['portal', 'knowledge', 'article', slug],
    queryFn: () => portalApi.get<PortalKnowledgeArticleDetail>(endpoints.portal.knowledgeArticle(slug ?? '')),
    enabled: Boolean(slug),
  })
}

export function useSubmitPortalKnowledgeFeedback(articleId: string) {
  return useMutation({
    mutationFn: (payload: { isHelpful: boolean; comment?: string }) =>
      portalApi.post<{ message: string }>(endpoints.portal.knowledgeFeedback(articleId), payload),
  })
}
