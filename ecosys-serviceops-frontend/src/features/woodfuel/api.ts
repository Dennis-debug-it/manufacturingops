import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import { performAction } from '../tech/offline/outbox'
import { refreshAfterSync } from '../tech/offline/hooks'

export const MEASURE_LABEL: Record<string, string> = { Weighbridge: 'Weighbridge', StackedVolume: 'Stacked cubic metres', LoadCount: 'Count of loads' }
export const RULE_LABEL: Record<string, string> = { DeductKg: 'Deduct kg for wet wood', PriceBands: 'Pay less by moisture band' }
export const FORM_LABEL: Record<string, string> = { Logs: 'Logs', Splits: 'Splits', Slabs: 'Slabs', Offcuts: 'Offcuts', Briquettes: 'Briquettes', OtherBiomass: 'Other biomass (husks, sawdust…)' }
export const SUPPLIER_TYPE_LABEL: Record<string, string> = { Farmer: 'Farmer', Merchant: 'Merchant', Cooperative: 'Cooperative', OwnWoodlot: 'Own woodlot' }
export const REASON_LABEL: Record<string, string> = { CountVariance: 'Count difference', DryingLoss: 'Drying loss', Spillage: 'Spillage or waste' }
export const DECISION_LABEL: Record<string, string> = { Accepted: 'Accepted', AcceptedWithDeduction: 'Accepted with deduction', Rejected: 'Rejected' }
export const STACK_STATUS_LABEL: Record<string, string> = { Receiving: 'Receiving', Seasoning: 'Seasoning', Ready: 'Ready', Depleted: 'Used up' }

export interface Named { id: string; name: string }
export interface Species { id: string; name: string; kgPerStackedM3: number; energyMjPerKg: number | null; isActive: boolean }
export interface PriceBand { upToPct: number; percentOfPrice: number }

export interface WoodSettings {
  factoryId: string
  defaultMeasureBasis: string
  moistureRule: string
  baseMoisturePct: number
  rejectMoisturePct: number
  deductionPctPerPoint: number
  priceBands: PriceBand[]
  defaultKgPerLoad: number | null
  burnWindowDays: number
  lowCoverDays: number
  seasoningDays: number
  targetKgPerUnit: number | null
}

export interface WoodLookups {
  factories: Named[]
  factoryId: string | null
  species: Species[]
  suppliers: Named[]
  stacks: Named[]
  assets: Named[]
  settings: WoodSettings | null
  measureBases: string[]
  moistureRules: string[]
  fuelForms: string[]
  supplierTypes: string[]
  stackStatuses: string[]
  adjustmentReasons: string[]
  canManage: boolean
  canReceive: boolean
  canIssue: boolean
}

export interface WoodSupplier {
  id: string
  name: string
  type: string
  phone: string | null
  paymentDetails: string | null
  kraPin: string | null
  idLast4: string | null
  sourceArea: string | null
  permitNumber: string | null
  permitExpiresOn: string | null
  permitExpired: boolean
  notes: string | null
  isActive: boolean
}

export interface WoodPrice {
  id: string
  supplierId: string | null
  supplier: string | null
  speciesId: string | null
  species: string | null
  fuelForm: string | null
  pricePerTonne: number
  validFrom: string
  validTo: string | null
  notes: string | null
  isActive: boolean
}

export interface WoodDelivery {
  id: string
  deliveryNumber: string
  factoryId: string
  factory: string
  receivedAt: string
  supplierId: string | null
  supplier: string
  supplierPhone: string
  supplierPaymentDetails: string
  speciesId: string | null
  species: string
  fuelForm: string
  sourceArea: string | null
  transportPermit: string | null
  supplierNoteNumber: string | null
  vehicleRegistration: string
  driverName: string | null
  measureBasis: string
  grossKg: number | null
  tareKg: number | null
  stackedM3: number | null
  loads: number | null
  kgPerLoad: number | null
  netKg: number
  averageMoisturePct: number | null
  moistureSamples: number[]
  decision: string
  decisionReason: string | null
  rejectedKg: number
  deductedKg: number
  payableKg: number
  pricePerTonne: number
  pricePercent: number
  amountPayable: number
  stackId: string | null
  stack: string
  receivedBy: string
  notes: string | null
}

export interface WoodIssue {
  id: string
  factoryId: string
  assetId: string
  asset: string
  stackId: string | null
  stack: string | null
  issuedAt: string
  shift: string | null
  kg: number
  notes: string | null
  issuedBy: string
  onMeter: boolean
}

export interface WoodCount {
  id: string
  stackId: string
  stack: string
  countedAt: string
  ledgerKg: number
  countedKg: number
  varianceKg: number
  method: string
  reason: string
  notes: string | null
  countedBy: string
}

export interface ProductionDay { id: string; factoryId: string; date: string; madeTeaKg: number; greenLeafKg: number | null; notes: string | null; enteredBy: string }

export interface StackRow { id: string; code: string; status: string; species: string | null; kg: number; averageMoisturePct: number | null; builtOn: string; readyFrom: string | null; isReady: boolean }
export interface SupplierRow { supplierId: string | null; supplier: string; deliveries: number; netKg: number; averageMoisturePct: number | null; rejected: number; rejectedKg: number; amount: number }
export interface DailyRow { date: string; receivedKg: number; issuedKg: number; madeTeaKg: number; kgPerUnit: number | null }

export interface YardSummary {
  factoryId: string
  from: string
  to: string
  stockKg: number
  unstackedKg: number
  averageDailyBurnKg: number | null
  daysOfCover: number | null
  lowCover: boolean
  lowCoverDays: number
  openingKg: number
  receivedKg: number
  issuedKg: number
  adjustmentKg: number
  closingKg: number
  rejectedKg: number
  deductedKg: number
  amountPayable: number
  averageMoisturePct: number | null
  madeTeaKg: number
  kgPerUnit: number | null
  costPerUnit: number | null
  targetKgPerUnit: number | null
  fuelUnit: string
  stacks: StackRow[]
  suppliers: SupplierRow[]
  daily: DailyRow[]
}

export interface DeliveryInput {
  factoryId: string
  supplierId: string | null
  speciesId: string | null
  fuelForm: string
  sourceArea: string | null
  transportPermit: string | null
  supplierNoteNumber: string | null
  vehicleRegistration: string | null
  driverName: string | null
  measureBasis: string
  grossKg: number | null
  tareKg: number | null
  stackedM3: number | null
  loads: number | null
  kgPerLoad: number | null
  moistureSamples: number[]
  reject: boolean
  rejectReason: string | null
  acceptAboveMoistureLimit: boolean
  pricePerTonne: number | null
  stackId: string | null
  notes: string | null
}

export interface IssueInput {
  factoryId: string
  assetId: string
  stackId: string | null
  kg: number | null
  loads: number | null
  kgPerLoad: number | null
  shift: string | null
  notes: string | null
}

const KEY = ['wood-fuel'] as const
const BASE = '/api/wood-fuel'

export function useWoodLookups(factoryId?: string) {
  return useQuery({
    queryKey: [...KEY, 'lookups', factoryId ?? ''],
    queryFn: () => api.get<WoodLookups>(`${BASE}/lookups`, factoryId ? { factoryId } : undefined),
    staleTime: 60_000,
  })
}

export function useYardSummary(factoryId: string, days: number) {
  return useQuery({
    enabled: Boolean(factoryId),
    queryKey: [...KEY, 'summary', factoryId, days],
    queryFn: () => {
      const to = new Date()
      to.setUTCHours(0, 0, 0, 0)
      to.setUTCDate(to.getUTCDate() + 1)
      const from = new Date(to)
      from.setUTCDate(from.getUTCDate() - days)
      return api.get<YardSummary>(`${BASE}/summary`, { factoryId, from: from.toISOString(), to: to.toISOString() })
    },
  })
}

export function useDeliveries(params: { factoryId: string; supplierId?: string; decision?: string; q?: string; days: number }) {
  return useQuery({
    enabled: Boolean(params.factoryId),
    queryKey: [...KEY, 'deliveries', params],
    queryFn: () => {
      const to = new Date(Date.now() + 86_400_000)
      const from = new Date(Date.now() - params.days * 86_400_000)
      return api.get<WoodDelivery[]>(`${BASE}/deliveries`, {
        factoryId: params.factoryId, supplierId: params.supplierId || undefined, decision: params.decision || undefined, q: params.q || undefined,
        from: from.toISOString(), to: to.toISOString(),
      })
    },
  })
}

export function useIssues(factoryId: string) {
  return useQuery({ enabled: Boolean(factoryId), queryKey: [...KEY, 'issues', factoryId], queryFn: () => api.get<WoodIssue[]>(`${BASE}/issues`, { factoryId }) })
}

export function useCounts(factoryId: string) {
  return useQuery({ enabled: Boolean(factoryId), queryKey: [...KEY, 'counts', factoryId], queryFn: () => api.get<WoodCount[]>(`${BASE}/counts`, { factoryId }) })
}

export function useSuppliers() {
  return useQuery({ queryKey: [...KEY, 'suppliers'], queryFn: () => api.get<WoodSupplier[]>(`${BASE}/suppliers`, { includeInactive: true }) })
}

export function usePrices() {
  return useQuery({ queryKey: [...KEY, 'prices'], queryFn: () => api.get<WoodPrice[]>(`${BASE}/prices`) })
}

export function useProduction(factoryId: string) {
  return useQuery({ enabled: Boolean(factoryId), queryKey: [...KEY, 'production', factoryId], queryFn: () => api.get<ProductionDay[]>(`${BASE}/production`, { factoryId }) })
}

function useWoodMutation<TVars, TResult>(mutationFn: (vars: TVars) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn, onSuccess: () => void queryClient.invalidateQueries({ queryKey: KEY }) })
}

export const useSaveSettings = () => useWoodMutation((payload: WoodSettings) => api.put<WoodSettings>(`${BASE}/settings`, payload))
export const useSaveSpecies = () => useWoodMutation(({ id, ...payload }: { id?: string; name: string; kgPerStackedM3: number; energyMjPerKg: number | null; isActive?: boolean }) =>
  id ? api.put<Species>(`${BASE}/species/${id}`, payload) : api.post<Species>(`${BASE}/species`, payload))
export const useSaveSupplier = () => useWoodMutation(({ id, ...payload }: { id?: string; name: string; type: string; phone: string | null; paymentDetails: string | null; kraPin: string | null; nationalId: string | null; sourceArea: string | null; permitNumber: string | null; permitExpiresOn: string | null; notes: string | null; isActive?: boolean }) =>
  id ? api.put<WoodSupplier>(`${BASE}/suppliers/${id}`, payload) : api.post<WoodSupplier>(`${BASE}/suppliers`, payload))
export const useSavePrice = () => useWoodMutation(({ id, ...payload }: { id?: string; supplierId: string | null; speciesId: string | null; fuelForm: string | null; pricePerTonne: number; validFrom: string; validTo: string | null; notes: string | null; isActive?: boolean }) =>
  id ? api.put<WoodPrice>(`${BASE}/prices/${id}`, payload) : api.post<WoodPrice>(`${BASE}/prices`, payload))
export const useSaveStack = () => useWoodMutation(({ id, ...payload }: { id?: string; factoryId: string; code: string; speciesId: string | null; status?: string; notes: string | null; isActive?: boolean }) =>
  id ? api.put<Named>(`${BASE}/stacks/${id}`, payload) : api.post<Named>(`${BASE}/stacks`, payload))
export const useCountStack = () => useWoodMutation((payload: { stackId: string; countedKg: number | null; stackedM3: number | null; reason: string | null; notes: string | null }) => api.post<WoodCount>(`${BASE}/counts`, payload))
export const useSaveProduction = () => useWoodMutation((payload: { factoryId: string; date: string; madeTeaKg: number; greenLeafKg: number | null; notes: string | null }) => api.put<ProductionDay>(`${BASE}/production`, payload))

/**
 * Receiving and feeding wood are the two things a yard clerk or boiler attendant does with the signal
 * coming and going, so they go through the phone's queue: sent now when there is a connection, kept
 * and sent when it is back otherwise.
 */
export async function submitDelivery(input: DeliveryInput, label: string, queryClient: QueryClient) {
  const result = await performAction<WoodDelivery>({ kind: 'wood-delivery', label, workOrderId: null, method: 'POST', url: `${BASE}/deliveries`, body: input }, () => refreshAfterSync(queryClient))
  void queryClient.invalidateQueries({ queryKey: KEY })
  return result
}

export async function submitIssue(input: IssueInput, label: string, queryClient: QueryClient) {
  const result = await performAction<WoodIssue>({ kind: 'wood-issue', label, workOrderId: null, method: 'POST', url: `${BASE}/issues`, body: input }, () => refreshAfterSync(queryClient))
  void queryClient.invalidateQueries({ queryKey: KEY })
  return result
}

export async function downloadDeliveries(factoryId: string, days: number) {
  const to = new Date(Date.now() + 86_400_000)
  const from = new Date(Date.now() - days * 86_400_000)
  const response = await apiClient.get(`${BASE}/deliveries/export`, { params: { factoryId, from: from.toISOString(), to: to.toISOString() }, responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = 'wood-deliveries.csv'
  link.click()
  URL.revokeObjectURL(url)
}
