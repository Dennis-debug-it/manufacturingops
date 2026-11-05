import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'

/* ── PM plans ─────────────────────────────────────────────────────────────── */

export interface PmPlanSummary {
  id: string
  name: string
  assetId: string
  assetCode: string
  assetName: string
  assetCriticality: string | null
  orgNodeId: string
  orgNodeName: string | null
  triggerType: 'Calendar' | 'Meter'
  frequency: string
  scheduleMode: 'Fixed' | 'Floating'
  nextDueDate: string | null
  nextMeterDue: number | null
  meterValue: number | null
  meterUnit: string | null
  lastDoneAt: string | null
  priority: string
  estimatedHours: number | null
  needsLineStop: boolean
  isRoute: boolean
  routeAssetCount: number
  basis: string | null
  status: string
  deferReason: string | null
  deferredAt: string | null
  deferRisk: boolean
  openJobId: string | null
  openJobNumber: string | null
}

export interface PmEvent { kind: string; occurredAt: string; dueDate: string | null; dueMeterValue: number | null; onTime: boolean | null; reason: string | null; actorName: string | null; workOrderId: string | null; workOrderNumber: string | null }

export interface PmPlanDetail {
  plan: PmPlanSummary
  pmTemplateId: string | null
  pmTemplateName: string | null
  frequencyUnit: string
  frequencyInterval: number
  meterInterval: number | null
  tolerancePercent: number
  daysBeforeDue: number
  autoSchedule: boolean
  routeAssets: { assetId: string; assetCode: string; assetName: string }[]
  events: PmEvent[]
}

export interface SavePmPlan {
  name: string
  assetId: string
  pmTemplateId: string | null
  triggerType: 'Calendar' | 'Meter'
  frequencyUnit: string
  frequencyInterval: number
  nextDueDate: string | null
  meterId: string | null
  meterInterval: number | null
  nextMeterDue: number | null
  scheduleMode: string
  tolerancePercent: number
  daysBeforeDue: number
  priority: string
  estimatedHours: number | null
  needsLineStop: boolean
  basis: string | null
  routeAssetIds: string[] | null
  autoSchedule: boolean
}

export interface PmForecastWeek {
  weekStart: string
  jobs: number
  hours: number
  lineStopHours: number
  capacityHours: number
  items: { planId: string; name: string; assetCode: string | null; due: string; hours: number; needsLineStop: boolean; estimated: boolean }[]
}

export interface PmCompliance {
  due: number
  onTime: number
  late: number
  skipped: number
  overdue: number
  percent: number | null
  plans: { planId: string; name: string; assetCode: string | null; due: number; onTime: number; late: number; skipped: number; overdue: number }[]
}

export interface ChecklistTemplate { id: string; name: string; workType: string; isActive: boolean }

const refreshPm = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'pm'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
}

export function usePmPlans(filters: { orgNodeId?: string | undefined; search?: string | undefined; includeInactive?: boolean | undefined; assetId?: string | undefined }) {
  return useQuery({
    queryKey: ['factory', 'pm', 'plans', filters],
    queryFn: () => api.get<PmPlanSummary[]>('/api/factory/pm/plans', filters),
    staleTime: 30_000,
  })
}

export function usePmPlan(id: string | null) {
  return useQuery({
    queryKey: ['factory', 'pm', 'plan', id],
    queryFn: () => api.get<PmPlanDetail>(`/api/factory/pm/plans/${id}`),
    enabled: Boolean(id),
  })
}

export function useSavePmPlan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...payload }: SavePmPlan & { id?: string | undefined }) =>
      id ? api.put<PmPlanDetail>(`/api/factory/pm/plans/${id}`, payload) : api.post<PmPlanDetail>('/api/factory/pm/plans', payload),
    onSuccess: () => refreshPm(queryClient),
  })
}

export function usePmAction(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ action, body }: { action: 'skip' | 'defer' | 'active' | 'generate'; body?: unknown }) =>
      api.post<PmPlanDetail | { workOrderId: string; workOrderNumber: string }>(`/api/factory/pm/plans/${id}/${action}`, body ?? {}),
    onSuccess: () => refreshPm(queryClient),
  })
}

export function usePmForecast(orgNodeId: string | undefined, weeks: number) {
  return useQuery({
    queryKey: ['factory', 'pm', 'forecast', orgNodeId ?? null, weeks],
    queryFn: () => api.get<PmForecastWeek[]>('/api/factory/pm/forecast', { orgNodeId, weeks }),
    staleTime: 60_000,
  })
}

export function usePmCompliance(orgNodeId: string | undefined, days: number) {
  return useQuery({
    queryKey: ['factory', 'pm', 'compliance', orgNodeId ?? null, days],
    queryFn: () => api.get<PmCompliance>('/api/factory/pm/compliance', { orgNodeId, days }),
    staleTime: 60_000,
  })
}

export function useChecklistTemplates() {
  return useQuery({
    queryKey: ['factory', 'checklist-templates'],
    queryFn: () => api.get<ChecklistTemplate[]>('/api/pm/templates'),
    staleTime: 5 * 60_000,
  })
}

/* ── Weekly window ────────────────────────────────────────────────────────── */

export type WindowStatus = 'Planned' | 'Packed' | 'InProgress' | 'HandedBack' | 'Skipped'

export interface WindowSummary {
  id: string
  factoryId: string
  factoryName: string
  windowDate: string
  startsAt: string
  endsAt: string
  status: WindowStatus
  jobs: number
  jobsDone: number
  plannedHours: number
  capacityHours: number
  capacitySet: boolean
  movedFromDate: string | null
  skipReason: string | null
  notes: string | null
  packedAt: string | null
  packedByName: string | null
  startedAt: string | null
  handedBackAt: string | null
  handedBackByName: string | null
  handbackNotes: string | null
}

export interface WindowJob {
  workOrderId: string
  workOrderNumber: string
  title: string
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  priority: string
  status: string
  workType: string
  estimatedHours: number | null
  carryoverCount: number
  addedAutomatically: boolean
  outcome: string | null
  isPm: boolean
  team: string[]
}

export interface HandbackItem { id: string; prompt: string; done: boolean; note: string | null; doneByName: string | null; doneAt: string | null }

export interface WindowDetail { window: WindowSummary; jobs: WindowJob[]; handbackItems: HandbackItem[] }

export interface WindowCandidate { workOrderId: string; workOrderNumber: string; title: string; assetCode: string | null; priority: string; status: string; estimatedHours: number | null; needsLineStop: boolean; dueDate: string | null }

export interface WindowPack {
  window: WindowSummary
  jobs: {
    job: WindowJob
    description: string | null
    location: string | null
    steps: { id: string; section: string | null; instruction: string; type: string; unit: string | null; done: boolean }[]
    resources: { kind: string; description: string; quantity: number }[]
    isolation: string
    spares: { reservationId: string; materialItemId: string; itemCode: string; itemName: string; unitOfMeasure: string; quantity: number; issuedQuantity: number; status: 'Reserved' | 'Kitted' | 'Issued' | 'Released'; storeName: string; location: string }[]
    shortages: { materialItemId: string; description: string; short: number }[]
  }[]
  handbackItems: HandbackItem[]
  generatedAt: string
}

export interface FactoryWindowSetting { factoryId: string; factoryName: string; enabled: boolean; day: number; overridden: boolean }

export const WINDOW_STATUS_LABELS: Record<WindowStatus, string> = {
  Planned: 'Planned', Packed: 'Packed', InProgress: 'In progress', HandedBack: 'Handed back', Skipped: 'Skipped',
}

export const DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

const refreshWindows = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'windows'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'window'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'window-pack'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
}

export function useWindows(factoryId?: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'windows', factoryId ?? null],
    queryFn: () => api.get<WindowSummary[]>('/api/factory/windows', { factoryId, past: 6 }),
    staleTime: 30_000,
  })
}

export function useWindow(id: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'window', id],
    queryFn: () => api.get<WindowDetail>(`/api/factory/windows/${id}`),
    enabled: Boolean(id),
  })
}

/** The job pack, kept on the phone so the window can be worked without signal. */
export function useWindowPack(id: string | undefined, enabled = true) {
  return useQuery({
    queryKey: ['factory', 'window-pack', id],
    queryFn: () => api.get<WindowPack>(`/api/factory/windows/${id}/pack`),
    enabled: Boolean(id) && enabled,
    staleTime: 5 * 60_000,
  })
}

export function useWindowCandidates(id: string, search: string, enabled: boolean) {
  return useQuery({
    queryKey: ['factory', 'window-candidates', id, search],
    queryFn: () => api.get<WindowCandidate[]>(`/api/factory/windows/${id}/candidates`, { search: search || undefined }),
    enabled,
  })
}

export function useWindowAction(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ method = 'post', path, body }: { method?: 'post' | 'put' | 'delete'; path: string; body?: unknown }) => {
      const url = `/api/factory/windows/${id}${path}`
      if (method === 'put') return api.put<unknown>(url, body)
      if (method === 'delete') return api.delete<unknown>(url)
      return api.post<unknown>(url, body ?? {})
    },
    onSuccess: () => refreshWindows(queryClient),
  })
}

export function useFactoryWindowSettings() {
  return useQuery({
    queryKey: ['factory', 'windows', 'factories'],
    queryFn: () => api.get<FactoryWindowSetting[]>('/api/factory/windows/factories'),
  })
}

export function useSaveFactoryWindow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ factoryId, enabled, day }: { factoryId: string; enabled: boolean | null; day: number | null }) =>
      api.put<FactoryWindowSetting>(`/api/factory/windows/factories/${factoryId}`, { enabled, day }),
    onSuccess: () => refreshWindows(queryClient),
  })
}

export function useInstallRoutines() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (factoryId: string) => api.post<{ plansAdded: number }>(`/api/factory/windows/factories/${factoryId}/routines`, {}),
    onSuccess: () => { refreshWindows(queryClient); refreshPm(queryClient) },
  })
}

/* ── Overhaul ─────────────────────────────────────────────────────────────── */

export interface OverhaulItem {
  id: string
  factoryId: string
  factoryName: string
  overhaulPlanId: string | null
  overhaulPlanName: string | null
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  assetCriticality: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  workOrderStatus: string | null
  carryoverCount: number
  title: string
  description: string | null
  source: 'Carryover' | 'Deferred' | 'Finding' | 'Manual'
  priority: string
  status: 'Listed' | 'Scoped' | 'InProgress' | 'Done' | 'Dropped'
  estimatedHours: number | null
  estimatedCost: number | null
  actualCost: number | null
  sparesNote: string | null
  contractorName: string | null
  progressPercent: number
  needsRecommissioning: boolean
  recommissionedAt: string | null
  recommissionedByName: string | null
  recommissioningNotes: string | null
  addedByName: string | null
  createdAt: string
}

export interface OverhaulPlan {
  id: string
  factoryId: string
  factoryName: string
  name: string
  plannedStart: string
  plannedEnd: string
  budget: number | null
  status: 'Planning' | 'InProgress' | 'Completed'
  actualStart: string | null
  actualEnd: string | null
  notes: string | null
  items: number
  itemsDone: number
  estimatedCost: number
  actualCost: number
  estimatedHours: number
  progressPercent: number
  awaitingRecommissioning: number
}

export const OVERHAUL_SOURCE_LABELS: Record<OverhaulItem['source'], string> = { Carryover: 'Window carryover', Deferred: 'Deferred job', Finding: 'Finding', Manual: 'Added' }

const refreshOverhaul = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'overhaul'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'windows'] })
}

export function useOverhaulItems(filters: { factoryId?: string | undefined; planId?: string | undefined; includeClosed?: boolean | undefined }) {
  return useQuery({
    queryKey: ['factory', 'overhaul', 'items', filters],
    queryFn: () => api.get<OverhaulItem[]>('/api/factory/overhaul/items', filters),
  })
}

export function useOverhaulPlans(factoryId?: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'overhaul', 'plans', factoryId ?? null],
    queryFn: () => api.get<OverhaulPlan[]>('/api/factory/overhaul/plans', { factoryId }),
  })
}

export function useOverhaulPlan(id: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'overhaul', 'plan', id],
    queryFn: () => api.get<{ plan: OverhaulPlan; items: OverhaulItem[] }>(`/api/factory/overhaul/plans/${id}`),
    enabled: Boolean(id),
  })
}

export function useOverhaulAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put'; path: string; body?: unknown }) =>
      method === 'put' ? api.put<unknown>(`/api/factory/overhaul${path}`, body) : api.post<unknown>(`/api/factory/overhaul${path}`, body ?? {}),
    onSuccess: () => refreshOverhaul(queryClient),
  })
}

/* ── Statutory compliance ─────────────────────────────────────────────────── */

export type ComplianceStatus = 'Ok' | 'DueSoon' | 'Overdue' | 'NoDate'

export interface ComplianceItem {
  id: string
  name: string
  kind: string
  regulation: string | null
  intervalMonths: number
  orgNodeId: string
  orgNodeName: string | null
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  lastDoneAt: string | null
  dueDate: string | null
  ownerUserId: string | null
  ownerName: string | null
  provider: string | null
  raiseJobDaysBefore: number | null
  openWorkOrderId: string | null
  openWorkOrderNumber: string | null
  notes: string | null
  isActive: boolean
  lastCertificateNumber: string | null
  lastCertificateId: string | null
  status: ComplianceStatus
  daysLeft: number | null
}

export interface ComplianceRecord {
  id: string
  doneAt: string
  result: 'Pass' | 'Conditional' | 'Fail'
  validUntil: string | null
  certificateNumber: string | null
  examiner: string | null
  notes: string | null
  certificateId: string | null
  certificateFileName: string | null
  workOrderId: string | null
  recordedByName: string | null
}

export interface ComplianceSummary {
  total: number
  ok: number
  dueSoon: number
  overdue: number
  noDate: number
  byKind: { kind: string; total: number; overdue: number; dueSoon: number }[]
}

export const COMPLIANCE_KIND_LABELS: Record<string, string> = {
  StatutoryInspection: 'Statutory inspection', Calibration: 'Calibration', FireSafety: 'Fire safety', Certificate: 'Certificate or licence', Other: 'Other',
}

const refreshCompliance = (queryClient: QueryClient) => {
  void queryClient.invalidateQueries({ queryKey: ['factory', 'compliance'] })
  void queryClient.invalidateQueries({ queryKey: ['factory', 'jobs'] })
}

export function useComplianceItems(filters: { orgNodeId?: string | undefined; status?: string | undefined; kind?: string | undefined; assetId?: string | undefined }) {
  return useQuery({
    queryKey: ['factory', 'compliance', 'items', filters],
    queryFn: () => api.get<ComplianceItem[]>('/api/factory/compliance/items', filters),
    staleTime: 30_000,
  })
}

export function useComplianceSummary(orgNodeId?: string | undefined) {
  return useQuery({
    queryKey: ['factory', 'compliance', 'summary', orgNodeId ?? null],
    queryFn: () => api.get<ComplianceSummary>('/api/factory/compliance/summary', { orgNodeId }),
    staleTime: 60_000,
  })
}

export function useComplianceItem(id: string | null) {
  return useQuery({
    queryKey: ['factory', 'compliance', 'item', id],
    queryFn: () => api.get<{ item: ComplianceItem; records: ComplianceRecord[] }>(`/api/factory/compliance/items/${id}`),
    enabled: Boolean(id),
  })
}

export function useComplianceAction() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ method = 'post', path, body }: { method?: 'post' | 'put'; path: string; body?: unknown }) =>
      method === 'put' ? api.put<unknown>(`/api/factory/compliance${path}`, body) : api.post<unknown>(`/api/factory/compliance${path}`, body ?? {}),
    onSuccess: () => refreshCompliance(queryClient),
  })
}
