import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../../shared/api/apiClient'

/* ── Periods ─────────────────────────────────────────────────────────────── */

export type PeriodPreset = 'month' | 'lastMonth' | 'quarter' | 'ytd' | 'last12' | 'custom'

export const PERIOD_LABELS: Record<PeriodPreset, string> = {
  month: 'This month', lastMonth: 'Last month', quarter: 'Last 3 months', ytd: 'Year to date', last12: 'Last 12 months', custom: 'Custom',
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** The inclusive [from, to] dates for a preset, as yyyy-mm-dd. */
export function periodDates(preset: PeriodPreset, custom: { from: string; to: string }, today = new Date()): { from: string; to: string } {
  const y = today.getFullYear()
  const m = today.getMonth()
  switch (preset) {
    case 'month': return { from: iso(new Date(y, m, 1)), to: iso(today) }
    case 'lastMonth': return { from: iso(new Date(y, m - 1, 1)), to: iso(new Date(y, m, 0)) }
    case 'quarter': return { from: iso(new Date(y, m - 2, 1)), to: iso(today) }
    case 'ytd': return { from: iso(new Date(y, 0, 1)), to: iso(today) }
    case 'last12': return { from: iso(new Date(y, m - 11, 1)), to: iso(today) }
    default: return custom
  }
}

/* ── KPIs ────────────────────────────────────────────────────────────────── */

export interface CostBreakdown { id: string | null; name: string; labour: number; parts: number; contractor: number; total: number; downtimeHours: number; productionLost: number; downtimeValue: number }

export interface FactoryKpiReport {
  scopeName: string
  from: string
  to: string
  productionUnit: string
  currency: string
  assets: { total: number; operational: number; standby: number; down: number; byCriticality: Record<string, number> }
  reliability: { breakdowns: number; mtbfHours: number | null; mttrHours: number | null; repeatFailures: number; topFailing: { assetId: string; assetCode: string; assetName: string; breakdowns: number; downtimeHours: number }[] }
  planning: { pmCompliancePercent: number | null; pmDue: number; pmOnTime: number; overdueJobs: number; backlogHours: number; backlogWeeks: number | null; plannedJobs: number; reactiveJobs: number; plannedPercent: number | null }
  window: { windows: number; jobsPlanned: number; jobsDone: number; completionPercent: number | null; carriedOver: number; oldestCarryover: number; breakdownsOnDeferredAssets: number }
  overhaul: { items: number; done: number; scopeCompletePercent: number | null; budget: number | null; actualCost: number; recommissioned: number; awaitingRecommissioning: number }
  downtime: { hours: number; productionLost: number; valueLost: number | null; peakHours: number; lowHours: number; peakProductionLost: number; lowProductionLost: number }
  costs: {
    labour: number; parts: number; contractor: number; total: number; downtimeValue: number | null; production: number; costPerUnit: number | null
    byFactory: CostBreakdown[]; bySection: CostBreakdown[]; byCostCentre: CostBreakdown[]; topAssets: CostBreakdown[]
    byMonth: { id: string | null; name: string; amount: number }[]
    budget: { annualBudget: number; budgetForPeriod: number; actual: number; variancePercent: number | null; lines: { factory: string; costCentre: string | null; category: string; budgetForPeriod: number; actual: number }[] }
  }
  spares: { consumptionValue: number; issueLines: number; criticalStockOuts: number; criticalBelowMinimum: number; stockValue: number }
  people: { loggedHours: number; overtimeHours: number; overtimePercent: number | null; utilisationPercent: number | null; jobsCompleted: number; certificatesExpiring: number; certificatesExpired: number }
  energy: { energyKwh: number; fuel: number; fuelUnit: string | null; production: number; kwhPerUnit: number | null; fuelPerUnit: number | null }
  compliance: { statutoryDueSoon: number; statutoryOverdue: number; calibrationsOverdue: number; nonConformitiesOpen: number; nonConformitiesOverdue: number; nextAuditOn: string | null; nextAudit: string | null }
}

export interface FactoryComparisonRow {
  factoryId: string; factoryName: string; downtimeHours: number; productionLost: number; breakdowns: number; mttrHours: number | null; pmCompliancePercent: number | null
  costTotal: number; production: number; costPerUnit: number | null; kwhPerUnit: number | null; fuelPerUnit: number | null; criticalStockOuts: number; statutoryOverdue: number; nonConformitiesOpen: number
}

export interface PilotReport {
  scopeName: string; baselineFrom: string; baselineTo: string; from: string; to: string
  measures: { measure: string; unit: string; baseline: number | null; current: number | null; changePercent: number | null; lowerIsBetter: boolean }[]
}

export interface KpiSchedule { id: string; name: string; orgNodeId: string | null; orgNodeName: string | null; frequency: string; dayOfMonth: number | null; dayOfWeek: string | null; timeOfDay: string; dateRangePreset: string; recipients: string; isActive: boolean; lastRunAt: string | null; nextRunAt: string | null; lastRunStatus: string | null }

export function useKpis(orgNodeId: string, from: string, to: string) {
  return useQuery({ queryKey: ['factory', 'kpis', orgNodeId, from, to], queryFn: () => api.get<FactoryKpiReport>('/api/factory/kpis', { orgNodeId: orgNodeId || undefined, from, to }), enabled: Boolean(from && to) })
}

export function useComparison(from: string, to: string, enabled: boolean) {
  return useQuery({ queryKey: ['factory', 'kpis', 'compare', from, to], queryFn: () => api.get<FactoryComparisonRow[]>('/api/factory/kpis/compare', { from, to }), enabled: enabled && Boolean(from && to) })
}

export function usePilot(params: { orgNodeId: string; baselineFrom: string; baselineTo: string; from: string; to: string } | null) {
  return useQuery({
    queryKey: ['factory', 'kpis', 'pilot', params],
    queryFn: () => api.get<PilotReport>('/api/factory/kpis/pilot', { ...params!, orgNodeId: params!.orgNodeId || undefined }),
    enabled: Boolean(params),
  })
}

export function useKpiSchedules() {
  return useQuery({ queryKey: ['factory', 'kpis', 'schedules'], queryFn: () => api.get<KpiSchedule[]>('/api/factory/kpis/schedules') })
}

/* ── Costs ───────────────────────────────────────────────────────────────── */

export interface CostSummaryRow { id: string | null; name: string; labour: number; parts: number; contractor: number; total: number; downtimeHours: number; productionLost: number; downtimeValue: number | null }
export interface CostSummary { scopeName: string; from: string; to: string; currency: string; productionUnit: string; groupBy: string; rows: CostSummaryRow[]; totals: CostSummaryRow }
export interface JobCosts { workOrderId: string; labour: number; parts: number; contractor: number; total: number; downtimeHours: number; productionLost: number; downtimeValue: number | null; productionUnit: string; costCentre: string | null }
export interface CostSettings { currency: string; productionUnit: string; productionUnitValue: number | null; vatRatePercent: number; peakSeasonMonths: number[]; personalDataRetentionYears: number; dataResidency: string | null }
export interface CostCentre { id: string; factoryId: string; factoryName: string; code: string; name: string; orgNodeId: string | null; orgNodeName: string | null; isActive: boolean; externalId: string | null; externalSystem: string | null }
export interface Budget { id: string; factoryId: string; factoryName: string; costCentreId: string | null; costCentreName: string | null; year: number; category: string; amount: number; notes: string | null }

export const COST_GROUPS = [{ id: 'section', label: 'Section' }, { id: 'factory', label: 'Factory' }, { id: 'costcentre', label: 'Cost centre' }, { id: 'asset', label: 'Asset' }, { id: 'job', label: 'Job' }, { id: 'month', label: 'Month' }] as const
export const BUDGET_CATEGORIES = ['Labour', 'Parts', 'Contractor'] as const

export function useCostSummary(orgNodeId: string, from: string, to: string, groupBy: string) {
  return useQuery({ queryKey: ['factory', 'costs', 'summary', orgNodeId, from, to, groupBy], queryFn: () => api.get<CostSummary>('/api/factory/costs', { orgNodeId: orgNodeId || undefined, from, to, groupBy }) })
}

export function useJobCosts(workOrderId: string) {
  return useQuery({ queryKey: ['factory', 'costs', 'job', workOrderId], queryFn: () => api.get<JobCosts>(`/api/factory/work-orders/${workOrderId}/costs`) })
}

export function useCostSettings() {
  return useQuery({ queryKey: ['factory', 'costs', 'settings'], queryFn: () => api.get<CostSettings>('/api/factory/costs/settings') })
}

export function useCostCentres() {
  return useQuery({ queryKey: ['factory', 'costs', 'centres'], queryFn: () => api.get<CostCentre[]>('/api/factory/costs/cost-centres') })
}

export function useBudgets(year: number) {
  return useQuery({ queryKey: ['factory', 'costs', 'budgets', year], queryFn: () => api.get<Budget[]>('/api/factory/costs/budgets', { year }) })
}

/* ── Compliance framework ────────────────────────────────────────────────── */

export interface StandardTemplate { key: string; code: string; name: string; description: string; requirements: number }
export interface Requirement { id: string; clause: string; title: string; guidance: string | null; evidenceSources: string; sequence: number }
export interface Standard { id: string; code: string; name: string; description: string | null; templateKey: string | null; isActive: boolean; requirements: Requirement[] }
export interface EvidenceSection { source: string; title: string; summary: string; count: number; headers: string[]; rows: string[][] }
export interface EvidencePack {
  standardId: string; standardName: string; factoryId: string; factoryName: string; from: string; to: string; generatedAt: string; covered: number
  requirements: { requirementId: string; clause: string; title: string; guidance: string | null; hasEvidence: boolean; sections: EvidenceSection[]; documents: string[]; openNonConformities: number }[]
}

export type NonConformityStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
export interface NonConformity {
  id: string; number: string; factoryId: string; factoryName: string; standardId: string | null; standardName: string | null; requirementId: string | null; requirementClause: string | null
  auditEventId: string | null; source: string; severity: 'Major' | 'Minor' | 'Observation'; finding: string; rootCause: string | null; correctiveAction: string | null
  ownerUserId: string | null; ownerName: string | null; dueDate: string | null; status: NonConformityStatus; workOrderId: string | null; workOrderNumber: string | null
  closureEvidence: string | null; resolvedAt: string | null; closedAt: string | null; closedByName: string | null; raisedByName: string | null; raisedAt: string
}

export interface AuditEvent { id: string; factoryId: string; factoryName: string; standardId: string | null; standardName: string | null; kind: string; scheduledFor: string; auditor: string | null; status: 'Planned' | 'Done' | 'Cancelled'; outcome: string | null; notes: string | null; completedAt: string | null; findings: number }
export interface WasteRecord { id: string; factoryId: string; factoryName: string; disposedOn: string; wasteType: string; quantity: number; unit: string; contractorId: string | null; handlerName: string; licenceNumber: string | null; consignmentNote: string | null; attachmentId: string | null; notes: string | null; recordedByName: string | null }
export interface FoodSafetyRecord {
  id: string; factoryId: string; factoryName: string; kind: 'Cleaning' | 'MetalDetector' | 'GlassCheck'; doneAt: string; assetId: string | null; assetCode: string | null; assetName: string | null; area: string | null
  passed: boolean; method: string | null; ferrousRejected: boolean | null; nonFerrousRejected: boolean | null; stainlessRejected: boolean | null; itemsChecked: number | null; itemsDamaged: number | null
  notes: string | null; doneByName: string; verifiedByName: string | null; correctiveWorkOrderId: string | null; correctiveWorkOrderNumber: string | null
}
export interface GlassItem { id: string; factoryId: string; location: string; description: string; material: string; quantity: number; isActive: boolean }
export interface DataSubject { userId: string | null; technicianId: string | null; name: string; email: string; isActive: boolean; kind: string }
export interface Consent { id: string; userId: string | null; technicianId: string | null; subjectName: string; purpose: string; givenAt: string; method: string; withdrawnAt: string | null; recordedByName: string | null }

export const WASTE_TYPES = ['Used oil', 'Scrap metal', 'Batteries', 'E-waste', 'Fluorescent tubes', 'Chemical containers', 'Other']
export const AUDIT_KINDS = ['Self-assessment', 'Internal audit', 'Surveillance audit', 'Certification audit', 'Recertification audit']

const fw = '/api/factory/compliance-framework'

export function useStandardTemplates() {
  return useQuery({ queryKey: ['factory', 'framework', 'templates'], queryFn: () => api.get<StandardTemplate[]>(`${fw}/templates`), staleTime: 10 * 60_000 })
}

export function useEvidenceSources() {
  return useQuery({ queryKey: ['factory', 'framework', 'sources'], queryFn: () => api.get<Record<string, string>>(`${fw}/evidence-sources`), staleTime: 10 * 60_000 })
}

export function useStandards() {
  return useQuery({ queryKey: ['factory', 'framework', 'standards'], queryFn: () => api.get<Standard[]>(`${fw}/standards`) })
}

export function useEvidence(standardId: string | null, factoryId: string, from: string, to: string) {
  return useQuery({
    queryKey: ['factory', 'framework', 'evidence', standardId, factoryId, from, to],
    queryFn: () => api.get<EvidencePack>(`${fw}/standards/${standardId}/evidence`, { factoryId, from, to }),
    enabled: Boolean(standardId && factoryId),
  })
}

export function useNonConformities(status: string) {
  return useQuery({ queryKey: ['factory', 'framework', 'ncs', status], queryFn: () => api.get<NonConformity[]>(`${fw}/non-conformities`, { status: status || undefined }) })
}

export function useAudits() {
  return useQuery({ queryKey: ['factory', 'framework', 'audits'], queryFn: () => api.get<AuditEvent[]>(`${fw}/audits`) })
}

export function useWaste(from: string, to: string) {
  return useQuery({ queryKey: ['factory', 'framework', 'waste', from, to], queryFn: () => api.get<WasteRecord[]>(`${fw}/waste`, { from, to }) })
}

export function useFoodSafety(kind: string, from: string, to: string) {
  return useQuery({ queryKey: ['factory', 'framework', 'food', kind, from, to], queryFn: () => api.get<FoodSafetyRecord[]>(`${fw}/food-safety`, { kind: kind || undefined, from, to }) })
}

export function useGlassRegister(factoryId: string) {
  return useQuery({ queryKey: ['factory', 'framework', 'glass', factoryId], queryFn: () => api.get<GlassItem[]>(`${fw}/glass-register`, { factoryId }), enabled: Boolean(factoryId) })
}

export function useDataSubjects(enabled: boolean) {
  return useQuery({ queryKey: ['factory', 'dpa', 'people'], queryFn: () => api.get<DataSubject[]>('/api/factory/data-protection/people'), enabled })
}

export function useConsents(enabled: boolean) {
  return useQuery({ queryKey: ['factory', 'dpa', 'consents'], queryFn: () => api.get<Consent[]>('/api/factory/data-protection/consents'), enabled })
}

/** Any Phase 6 call; refreshes costs, KPIs, the framework and jobs afterwards. */
export function useFactoryAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put' | 'delete'; path: string; body?: unknown }) => {
      if (method === 'put') return api.put<unknown>(path, body)
      if (method === 'delete') return api.delete<unknown>(path)
      return api.post<unknown>(path, body ?? {})
    },
    onSuccess: () => {
      for (const key of ['kpis', 'costs', 'framework', 'dpa', 'job', 'jobs']) void queryClient.invalidateQueries({ queryKey: ['factory', key] })
    },
  })
}

/** Downloads a file (Excel, PDF) with the signed-in user's token. */
export async function downloadFile(path: string, params: Record<string, string | undefined>, fallbackName: string) {
  const response = await apiClient.get<Blob>(path, { params, responseType: 'blob' })
  const disposition = String(response.headers['content-disposition'] ?? '')
  const name = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1] ?? fallbackName
  const url = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = url
  link.download = decodeURIComponent(name)
  link.click()
  setTimeout(() => URL.revokeObjectURL(url), 60_000)
}

export const fwPath = fw
