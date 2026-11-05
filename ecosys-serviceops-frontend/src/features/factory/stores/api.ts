import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import { performAction } from '../../tech/offline/outbox'
import type { JobDetail } from '../jobs/types'

/* ── Stores and stock ─────────────────────────────────────────────────────── */

export type StockState = 'Ok' | 'Reorder' | 'BelowMin' | 'Out' | 'None'

export const STOCK_STATE_LABELS: Record<StockState, string> = { Ok: 'OK', Reorder: 'Reorder', BelowMin: 'Below minimum', Out: 'Out of stock', None: 'None held' }

export interface Store {
  id: string
  factoryId: string
  factoryName: string
  code: string
  name: string
  location: string | null
  isMain: boolean
  isActive: boolean
  partsInStock: number
  stockValue: number
  toReorder: number
  criticalOut: number
}

export interface StoreBin { id: string; code: string; description: string | null; isActive: boolean }

export interface StockRow {
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  category: string | null
  isCriticalSpare: boolean
  isSerialised: boolean
  isRepairable: boolean
  isChemical: boolean
  binId: string | null
  bin: string | null
  onHand: number
  reserved: number
  available: number
  minLevel: number
  reorderPoint: number
  maxLevel: number | null
  averageCost: number
  value: number
  state: StockState
  lastCountedAt: string | null
}

export interface StoreMovement {
  id: string
  at: string
  storeId: string
  storeName: string
  materialItemId: string
  itemCode: string
  itemName: string
  type: string
  change: number
  balanceAfter: number
  unitCost: number | null
  reference: string | null
  serials: string | null
  reason: string | null
  workOrderId: string | null
  by: string | null
}

export interface StockValuation {
  currency: string
  total: number
  stores: { storeId: string; storeName: string; factoryName: string; lines: number; value: number; criticalValue: number }[]
  categories: { category: string; lines: number; value: number }[]
}

export interface SparePartSummary {
  id: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  unitCost: number | null
  isCriticalSpare: boolean
  onCriticalAsset: boolean
  isSerialised: boolean
  isRepairable: boolean
  isChemical: boolean
  groupOnHand: number
  groupReserved: number
  storesHolding: number
}

export interface SpareUnit {
  id: string
  materialItemId: string
  itemCode: string
  itemName: string
  serialNumber: string
  status: 'InStore' | 'Installed' | 'AwaitingRepair' | 'AtRepair' | 'Scrapped'
  storeId: string | null
  storeName: string | null
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  repairSupplierId: string | null
  repairSupplierName: string | null
  repairReference: string | null
  sentForRepairAt: string | null
  expectedBackAt: string | null
  repairCount: number
  totalRepairCost: number
  unitCost: number | null
}

export const UNIT_STATUS_LABELS: Record<SpareUnit['status'], string> = {
  InStore: 'In store', Installed: 'Fitted', AwaitingRepair: 'Waiting for repair', AtRepair: 'At repairer', Scrapped: 'Scrapped',
}

export interface CatalogueRow {
  id: string
  supplierId: string
  supplierName: string
  materialItemId: string
  partCode: string
  partName: string
  vendorPartNumber: string | null
  unitPrice: number
  currency: string
  leadTimeDays: number | null
  minOrderQuantity: number | null
  validUntil: string | null
  isPreferred: boolean
}

export interface SparePartDetail {
  id: string
  itemCode: string
  itemName: string
  category: string | null
  unitOfMeasure: string
  purchaseUnitOfMeasure: string | null
  purchaseUnitFactor: number
  unitCost: number | null
  barcode: string | null
  preferredSupplierId: string | null
  isCriticalSpare: boolean
  onCriticalAsset: boolean
  isSerialised: boolean
  isRepairable: boolean
  externalId: string | null
  externalSystem: string | null
  isChemical: boolean
  chemicalKind: string | null
  hazardClass: string | null
  foodGradeClass: string | null
  sdsAttachmentId: string | null
  sdsIssuedOn: string | null
  stores: { storeId: string; storeName: string; factoryId: string; factoryName: string; bin: string | null; onHand: number; reserved: number; available: number; minLevel: number; reorderPoint: number; maxLevel: number | null; averageCost: number }[]
  whereUsed: { id: string; assetId: string; assetCode: string; assetName: string; criticality: string | null; location: string | null; quantityFitted: number; position: string | null; notes: string | null }[]
  units: SpareUnit[]
  prices: CatalogueRow[]
}

export interface SavePartPayload {
  itemCode: string
  itemName: string
  unitOfMeasure: string
  category?: string | null
  purchaseUnitOfMeasure?: string | null
  purchaseUnitFactor?: number | null
  barcode?: string | null
  unitCost?: number | null
  reorderQuantity?: number | null
  preferredSupplierId?: string | null
  isCriticalSpare: boolean
  isSerialised: boolean
  isRepairable: boolean
  externalId?: string | null
  externalSystem?: string | null
  isChemical: boolean
  chemicalKind?: string | null
  hazardClass?: string | null
  foodGradeClass?: string | null
  sdsAttachmentId?: string | null
  sdsIssuedOn?: string | null
}

export interface AssetPartRow {
  id: string
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  isCritical: boolean
  quantityFitted: number
  position: string | null
  notes: string | null
  availableInFactory: number
  availableInGroup: number
}

export interface JobReservation {
  id: string
  storeId: string
  storeName: string
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  quantity: number
  issuedQuantity: number
  status: 'Reserved' | 'Kitted' | 'Issued' | 'Released'
  reservedByName: string | null
  kittedAt: string | null
  kittedByName: string | null
}

export interface JobSpares {
  workOrderId: string
  factoryId: string | null
  defaultStoreId: string | null
  defaultStoreName: string | null
  reservations: JobReservation[]
  used: { id: string; materialItemId: string; itemCode: string; itemName: string; unitOfMeasure: string; isSerialised: boolean; quantity: number; unitCost: number | null; cost: number; notes: string | null; usedAt: string }[]
  planned: { materialItemId: string; description: string; quantity: number }[]
  partsCost: number
  installedOnAsset: { id: string; materialItemId: string; itemName: string; serialNumber: string }[]
}

export interface SpareShortage { workOrderId: string; workOrderNumber: string; materialItemId: string; itemCode: string; itemName: string; short: number; unitOfMeasure: string; isCritical: boolean }

export interface StoreTransfer {
  id: string
  transferNumber: string
  fromStoreId: string
  fromStoreName: string
  fromFactoryName: string
  toStoreId: string
  toStoreName: string
  toFactoryName: string
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  quantity: number
  serialNumbers: string | null
  status: 'Requested' | 'Dispatched' | 'Received' | 'Rejected' | 'Cancelled'
  isInterFactory: boolean
  reason: string | null
  requestedByName: string | null
  requestedAt: string
  dispatchedAt: string | null
  dispatchedByName: string | null
  receivedAt: string | null
  receivedByName: string | null
  rejectReason: string | null
}

export interface StoreCountSummary {
  id: string
  countNumber: string
  storeId: string
  storeName: string
  status: 'Open' | 'Posted' | 'Cancelled'
  criticalOnly: boolean
  isBlind: boolean
  lines: number
  counted: number
  startedByName: string | null
  startedAt: string
  postedAt: string | null
  varianceValue: number | null
}

export interface StoreCountDetail {
  count: StoreCountSummary
  lines: { id: string; materialItemId: string; itemCode: string; itemName: string; unitOfMeasure: string; expected: number | null; counted: number | null; variance: number | null; varianceValue: number | null; notes: string | null }[]
}

export interface ReorderRow {
  storeId: string
  storeName: string
  factoryId: string
  factoryName: string
  materialItemId: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  isCriticalSpare: boolean
  onHand: number
  reserved: number
  available: number
  minLevel: number
  reorderPoint: number
  state: StockState
  suggestedQuantity: number
  onRequisition: number
  onOrder: number
  supplierId: string | null
  supplierName: string | null
  unitPrice: number | null
  currency: string | null
  leadTimeDays: number | null
  donorStoreId: string | null
  donorStoreName: string | null
  donorSpare: number | null
}

export type RequisitionStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Ordered' | 'Cancelled'

export interface RequisitionSummary {
  id: string
  requisitionNumber: string
  factoryId: string
  factoryName: string
  storeId: string | null
  storeName: string | null
  status: RequisitionStatus
  reason: string | null
  neededBy: string | null
  requestedByName: string | null
  createdAt: string
  decidedByName: string | null
  decidedAt: string | null
  lines: number
  totals: { currency: string; amount: number }[]
}

export interface RequisitionPurchaseOrder {
  id: string
  poNumber: string
  supplierId: string
  supplierName: string | null
  status: string
  currency: string
  total: number
  lines: { id: string; materialItemId: string | null; itemName: string; quantityOrdered: number; quantityReceived: number; unitCost: number }[]
}

export interface RequisitionDetail {
  requisition: RequisitionSummary
  rejectReason: string | null
  lines: { id: string; materialItemId: string | null; itemCode: string | null; description: string; quantity: number; supplierId: string | null; supplierName: string | null; unitPrice: number | null; currency: string; workOrderId: string | null; purchaseOrderId: string | null }[]
  purchaseOrders: RequisitionPurchaseOrder[]
}

export interface RequisitionLinePayload { materialItemId: string | null; quantity: number; description?: string | null; supplierId?: string | null; unitPrice?: number | null; currency?: string | null; workOrderId?: string | null }

export interface ChemicalRow {
  id: string
  itemCode: string
  itemName: string
  unitOfMeasure: string
  kind: string | null
  hazardClass: string | null
  foodGradeClass: string | null
  sdsAttachmentId: string | null
  sdsIssuedOn: string | null
  sdsStatus: 'Missing' | 'Review due' | 'Current'
  onHand: number
  stores: string[]
}

export interface LubricantLogRow { at: string; lubricant: string; foodGradeClass: string | null; quantity: number; unitOfMeasure: string; workOrderNumber: string | null; assetCode: string | null; assetName: string | null; by: string | null }

export const CHEMICAL_KINDS = ['Lubricant', 'Water treatment', 'Cleaning', 'Fuel', 'Other']
export const FOOD_GRADES = ['H1', 'H2', 'H3']

const base = '/api/factory/stores'

export const refreshStores = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'stores'] })
}

export function useStores(includeInactive = false) {
  return useQuery({ queryKey: ['factory', 'stores', 'list', includeInactive], queryFn: () => api.get<Store[]>(base, { includeInactive }), staleTime: 30_000 })
}

export function useStoreBins(storeId: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'bins', storeId], queryFn: () => api.get<StoreBin[]>(`${base}/${storeId}/bins`), enabled: Boolean(storeId) })
}

export function useStock(storeId: string | null, search: string, filter: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'stock', storeId, search, filter], queryFn: () => api.get<StockRow[]>(`${base}/${storeId}/stock`, { search: search || undefined, filter }), enabled: Boolean(storeId) })
}

export function useMovements(filters: { storeId?: string | null; materialItemId?: string | null; workOrderId?: string | null }, enabled = true) {
  return useQuery({
    queryKey: ['factory', 'stores', 'movements', filters],
    queryFn: () => api.get<StoreMovement[]>(`${base}/movements`, { storeId: filters.storeId ?? undefined, materialItemId: filters.materialItemId ?? undefined, workOrderId: filters.workOrderId ?? undefined, take: 100 }),
    enabled,
  })
}

export function useValuation(enabled = true) {
  return useQuery({ queryKey: ['factory', 'stores', 'valuation'], queryFn: () => api.get<StockValuation>(`${base}/valuation`), enabled })
}

export function useSpareParts(search: string, filter: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'parts', search, filter], queryFn: () => api.get<SparePartSummary[]>(`${base}/parts`, { search: search || undefined, filter }) })
}

export function useSparePart(id: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'part', id], queryFn: () => api.get<SparePartDetail>(`${base}/parts/${id}`), enabled: Boolean(id) })
}

export function useAssetParts(assetId: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'asset-parts', assetId], queryFn: () => api.get<AssetPartRow[]>(`/api/factory/assets/${assetId}/parts`) })
}

export function useUnits(status: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'units', status], queryFn: () => api.get<SpareUnit[]>(`${base}/units`, { status: status ?? undefined }) })
}

export function useUnitHistory(id: string | null) {
  return useQuery({
    queryKey: ['factory', 'stores', 'unit-history', id],
    queryFn: () => api.get<{ type: string; at: string; by: string | null; workOrderNumber: string | null; assetCode: string | null; storeName: string | null; cost: number | null; note: string | null }[]>(`${base}/units/${id}/history`),
    enabled: Boolean(id),
  })
}

export function useJobSpares(workOrderId: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'job', workOrderId], queryFn: () => api.get<JobSpares>(`/api/factory/work-orders/${workOrderId}/spares`) })
}

export function useTransfers(status: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'transfers', status], queryFn: () => api.get<StoreTransfer[]>(`${base}/transfers`, { status: status || undefined }) })
}

export function useCounts() {
  return useQuery({ queryKey: ['factory', 'stores', 'counts'], queryFn: () => api.get<StoreCountSummary[]>(`${base}/counts`) })
}

export function useCount(id: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'count', id], queryFn: () => api.get<StoreCountDetail>(`${base}/counts/${id}`), enabled: Boolean(id) })
}

export function useReorder(factoryId: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'reorder', factoryId], queryFn: () => api.get<ReorderRow[]>(`${base}/reorder`, { factoryId: factoryId || undefined }) })
}

export function useRequisitions(status: string) {
  return useQuery({ queryKey: ['factory', 'stores', 'requisitions', status], queryFn: () => api.get<RequisitionSummary[]>(`${base}/requisitions`, { status: status || undefined }) })
}

export function useRequisition(id: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'requisition', id], queryFn: () => api.get<RequisitionDetail>(`${base}/requisitions/${id}`), enabled: Boolean(id) })
}

export function useCatalogue(materialItemId?: string | null) {
  return useQuery({ queryKey: ['factory', 'stores', 'catalogue', materialItemId ?? null], queryFn: () => api.get<CatalogueRow[]>(`${base}/catalogue`, { materialItemId: materialItemId ?? undefined }) })
}

export function useChemicals() {
  return useQuery({ queryKey: ['factory', 'stores', 'chemicals'], queryFn: () => api.get<ChemicalRow[]>(`${base}/chemicals`) })
}

export function useLubricantLog() {
  return useQuery({ queryKey: ['factory', 'stores', 'lubricant-log'], queryFn: () => api.get<LubricantLogRow[]>(`${base}/chemicals/lubricant-log`) })
}

/** Any stores call: path is relative to /api/factory/stores unless it starts with /api. */
export function useStoresAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put' | 'delete'; path: string; body?: unknown }) => {
      const url = path.startsWith('/api') ? path : `${base}${path}`
      if (method === 'put') return api.put<unknown>(url, body)
      if (method === 'delete') return api.delete<unknown>(url)
      return api.post<unknown>(url, body ?? {})
    },
    onSuccess: () => {
      refreshStores(queryClient)
      void queryClient.invalidateQueries({ queryKey: ['factory', 'job'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'windows'] })
    },
  })
}

/**
 * Issues a part to a job through the offline outbox, like the other job actions: a technician
 * at the machine without signal records it and it syncs when the phone is back online.
 */
export function useIssuePart(job: Pick<JobDetail, 'id' | 'workOrderNumber'>) {
  const queryClient = useQueryClient()
  return async (payload: { materialItemId: string; quantity: number; storeId?: string | null; serialNumber?: string | null; notes?: string | null }, partName: string) => {
    const result = await performAction<JobDetail>(
      { kind: 'factory-job', label: `Part used: ${partName} · ${job.workOrderNumber}`, workOrderId: job.id, method: 'POST', url: `/api/factory/work-orders/${job.id}/parts`, body: payload },
      () => refreshStores(queryClient))
    if (result.data) queryClient.setQueryData(['factory', 'job', job.id], result.data)
    refreshStores(queryClient)
    void queryClient.invalidateQueries({ queryKey: ['factory', 'job', job.id] })
    return result
  }
}

/* ── Contractors ──────────────────────────────────────────────────────────── */

export interface ContractorSummary {
  id: string
  name: string
  services: string | null
  contactPerson: string | null
  phone: string | null
  email: string | null
  status: 'Approved' | 'Pending' | 'Suspended'
  isActive: boolean
  documentsExpired: number
  documentsExpiring: number
  activeAgreements: number
  openJobs: number
  canTakeWork: boolean
  blockers: string[]
}

export interface ContractorDocument { id: string; documentType: string; reference: string | null; issuedOn: string | null; expiresOn: string | null; attachmentId: string | null; notes: string | null; status: 'Valid' | 'Expiring' | 'Expired'; required: boolean }

export interface ContractorJob {
  id: string
  workOrderNumber: string
  title: string
  status: string
  workType: string
  assetId: string | null
  assetCode: string | null
  contractorId: string | null
  contractorName: string | null
  agreementId: string | null
  agreementNumber: string | null
  dueDate: string | null
  completedAt: string | null
  repairDeadline: string | null
  repairBreached: boolean
}

export interface AgreementSummary {
  id: string
  agreementNumber: string
  contractorId: string
  contractorName: string
  title: string
  factoryId: string | null
  factoryName: string | null
  startDate: string
  endDate: string
  value: number | null
  currency: string
  visitEveryMonths: number | null
  nextVisitDue: string | null
  status: 'Active' | 'Expired' | 'Cancelled'
  assets: number
  openJobs: number
}

export interface AgreementDetail {
  agreement: AgreementSummary
  scope: string | null
  responseHours: number | null
  repairHours: number | null
  renewalNoticeDays: number
  assets: { assetId: string; assetCode: string; assetName: string }[]
  jobs: ContractorJob[]
  jobsFinished: number
  jobsWithinSla: number
}

export interface ContractorInvoiceSummary { id: string; contractorId: string; contractorName: string; invoiceNumber: string; invoiceDate: string; currency: string; amount: number; status: 'Received' | 'Matched' | 'Query' | 'Approved' | 'Rejected'; lines: number; decidedByName: string | null; decidedAt: string | null }

export interface ContractorInvoiceDetail {
  invoice: ContractorInvoiceSummary
  issues: string[]
  decisionNote: string | null
  attachmentId: string | null
  lines: { id: string; workOrderId: string | null; workOrderNumber: string | null; workOrderTitle: string | null; workOrderStatus: string | null; description: string; amount: number }[]
}

export interface ContractorDetail {
  id: string
  name: string
  services: string | null
  contactPerson: string | null
  phone: string | null
  email: string | null
  registrationNumber: string | null
  taxPin: string | null
  status: ContractorSummary['status']
  supplierId: string | null
  supplierName: string | null
  notes: string | null
  isActive: boolean
  externalId: string | null
  externalSystem: string | null
  blockers: string[]
  documents: ContractorDocument[]
  agreements: AgreementSummary[]
  jobs: ContractorJob[]
  invoices: ContractorInvoiceSummary[]
}

export const CONTRACTOR_DOCUMENT_TYPES = ['Insurance', 'Tax compliance', 'NCA registration', 'DOSH approval', 'Trade licence', 'Other']

const contractorsBase = '/api/factory/contractors'

const refreshContractors = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'contractors'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'job'] })
}

export function useContractors(includeInactive = false) {
  return useQuery({ queryKey: ['factory', 'contractors', 'list', includeInactive], queryFn: () => api.get<ContractorSummary[]>(contractorsBase, { includeInactive }), staleTime: 30_000 })
}

export function useContractor(id: string | null) {
  return useQuery({ queryKey: ['factory', 'contractors', 'detail', id], queryFn: () => api.get<ContractorDetail>(`${contractorsBase}/${id}`), enabled: Boolean(id) })
}

export function useUnbilledJobs(contractorId: string | null) {
  return useQuery({ queryKey: ['factory', 'contractors', 'unbilled', contractorId], queryFn: () => api.get<ContractorJob[]>(`${contractorsBase}/${contractorId}/unbilled-jobs`), enabled: Boolean(contractorId) })
}

export function useAgreements(status: string) {
  return useQuery({ queryKey: ['factory', 'contractors', 'agreements', status], queryFn: () => api.get<AgreementSummary[]>(`${contractorsBase}/agreements`, { status: status || undefined }) })
}

export function useAgreement(id: string | null) {
  return useQuery({ queryKey: ['factory', 'contractors', 'agreement', id], queryFn: () => api.get<AgreementDetail>(`${contractorsBase}/agreements/${id}`), enabled: Boolean(id) })
}

export function useContractorJobs(openOnly: boolean) {
  return useQuery({ queryKey: ['factory', 'contractors', 'jobs', openOnly], queryFn: () => api.get<ContractorJob[]>(`${contractorsBase}/jobs`, { openOnly }) })
}

export function useContractorInvoices(status: string) {
  return useQuery({ queryKey: ['factory', 'contractors', 'invoices', status], queryFn: () => api.get<ContractorInvoiceSummary[]>(`${contractorsBase}/invoices`, { status: status || undefined }) })
}

export function useContractorInvoice(id: string | null) {
  return useQuery({ queryKey: ['factory', 'contractors', 'invoice', id], queryFn: () => api.get<ContractorInvoiceDetail>(`${contractorsBase}/invoices/${id}`), enabled: Boolean(id) })
}

export function useContractorAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put' | 'delete'; path: string; body?: unknown }) => {
      const url = path.startsWith('/api') ? path : `${contractorsBase}${path}`
      if (method === 'put') return api.put<unknown>(url, body)
      if (method === 'delete') return api.delete<unknown>(url)
      return api.post<unknown>(url, body ?? {})
    },
    onSuccess: () => refreshContractors(queryClient),
  })
}

/** Uploads a file against a record (safety data sheet, contractor document) and returns its id. */
export async function uploadAttachment(entityType: 'MaterialItem' | 'Contractor' | 'ContractorInvoice', entityId: string, file: File) {
  const form = new FormData()
  form.append('entityType', entityType)
  form.append('entityId', entityId)
  form.append('file', file)
  const result = await api.post<{ id: string }>('/api/attachments/upload', form)
  return result.id
}

/** Opens an uploaded file with the signed-in user's token (a plain link would not carry it). */
export async function openAttachment(id: string) {
  try {
    const blob = await api.get<Blob>(`/api/attachments/${id}/download`, undefined, { responseType: 'blob' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.target = '_blank'
    link.rel = 'noopener'
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch {
    // The API client shows the error.
  }
}
