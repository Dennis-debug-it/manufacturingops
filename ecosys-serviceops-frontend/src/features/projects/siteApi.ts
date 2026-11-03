import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type {
  BillingTerms,
  BudgetLine,
  CostCategory,
  CostEntry,
  DiaryEntry,
  MusterDaySheet,
  MusterStatus,
  MySite,
  ProjectDetail,
  ProjectFinancials,
  ProjectWorker,
  RetentionPosition,
  RetentionStage,
  Valuation,
  ValuationPreview,
  WageSheet,
  WithholdingCertificate,
  WorkerEngagement,
  WorkerRates,
} from './types'

/**
 * Release 2 hooks: site work (roster, muster roll, diary) and the money side (rates, wage sheet,
 * budget, costs, financials, valuations, retention). Queries sit under the same ['projects'] key
 * as the workspace, so any change refreshes the project header figures too.
 */
const projectsKey = ['projects'] as const

function useProjectQuery<T>(id: string, name: string, url: string, enabled = true, params?: Record<string, string | number | boolean | null | undefined>) {
  return useQuery({
    enabled: Boolean(id) && enabled,
    queryKey: [...projectsKey, id, name, params ?? {}],
    queryFn: () => api.get<T>(url, params),
  })
}

function useProjectMutation<TPayload, TResult = unknown>(mutationFn: (payload: TPayload) => Promise<TResult>) {
  const queryClient = useQueryClient()
  return useMutation({ mutationFn, onSuccess: () => void queryClient.invalidateQueries({ queryKey: projectsKey }) })
}

// ── Site ──────────────────────────────────────────────────────────────────────

export function useMySites(enabled = true) {
  return useQuery({ enabled, queryKey: [...projectsKey, 'my-sites'], queryFn: () => api.get<MySite[]>(endpoints.projectSite.mine) })
}

export const useProjectWorkers = (id: string, includeInactive = false, enabled = true) =>
  useProjectQuery<ProjectWorker[]>(id, 'workers', endpoints.projectSite.workers(id), enabled, { includeInactive })

export interface WorkerPayload {
  fullName: string
  phone: string | null
  trade: string | null
  engagementType: WorkerEngagement
  technicianId: string | null
  isActive?: boolean
}

export const useSaveWorker = (id: string) =>
  useProjectMutation(({ workerId, ...payload }: WorkerPayload & { workerId?: string }) =>
    workerId
      ? api.put<ProjectWorker>(endpoints.projectSite.worker(id, workerId), payload)
      : api.post<ProjectWorker>(endpoints.projectSite.workers(id), payload))

export const useMusterSheet = (id: string, date: string, enabled = true) =>
  useProjectQuery<MusterDaySheet>(id, 'muster', endpoints.projectSite.muster(id), enabled && Boolean(date), { date })

export interface MusterMarkPayload {
  workerId: string
  status: MusterStatus
  note: string | null
}

export const useSaveMuster = (id: string) =>
  useProjectMutation(({ date, marks }: { date: string; marks: MusterMarkPayload[] }) =>
    api.put<MusterDaySheet>(endpoints.projectSite.muster(id), { marks }, { params: { date } }))

export const useDiary = (id: string, enabled = true) =>
  useProjectQuery<DiaryEntry[]>(id, 'diary', endpoints.projectSite.diary(id), enabled)

export interface DiaryPayload {
  entryDate: string
  workCompleted: string | null
  issuesAndDelays: string | null
  instructionsReceived: string | null
  weather: string | null
}

export const useSaveDiary = (id: string) =>
  useProjectMutation(({ entryId, ...payload }: DiaryPayload & { entryId?: string }) =>
    entryId
      ? api.put<DiaryEntry>(endpoints.projectSite.diaryEntry(id, entryId), payload)
      : api.post<DiaryEntry>(endpoints.projectSite.diary(id), payload))

export const useSubmitDiary = (id: string) =>
  useProjectMutation((entryId: string) => api.post<DiaryEntry>(endpoints.projectSite.diarySubmit(id, entryId)))

// ── Rates and wage sheet (office) ─────────────────────────────────────────────

export const useWorkerRates = (id: string, enabled = true) =>
  useProjectQuery<WorkerRates[]>(id, 'worker-rates', endpoints.projects.workerRates(id), enabled)

export const useSetWorkerRate = (id: string) =>
  useProjectMutation(({ workerId, ...payload }: { workerId: string; effectiveFrom: string; dailyRate: number; note: string | null }) =>
    api.post(endpoints.projects.workerRate(id, workerId), payload))

export const useWageSheet = (id: string, from: string, to: string, enabled = true) =>
  useProjectQuery<WageSheet>(id, 'wage-sheet', endpoints.projects.wageSheet(id), enabled && Boolean(from && to), { from, to })

export async function downloadWageSheet(id: string, from: string, to: string, projectNumber: string) {
  const response = await apiClient.get(endpoints.projects.wageSheet(id), { params: { from, to, format: 'csv' }, responseType: 'blob' })
  const url = URL.createObjectURL(response.data as Blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = `${projectNumber}-wages-${from}-${to}.csv`
  link.click()
  URL.revokeObjectURL(url)
}

// ── Budget, costs, financials ─────────────────────────────────────────────────

export const useBudgetLines = (id: string, enabled = true) =>
  useProjectQuery<BudgetLine[]>(id, 'budget-lines', endpoints.projects.budgetLines(id), enabled)

export const useSaveBudgetLine = (id: string) =>
  useProjectMutation(({ lineId, ...payload }: { lineId?: string; category: CostCategory; description: string | null; amount: number }) =>
    lineId ? api.put(endpoints.projects.budgetLine(id, lineId), payload) : api.post(endpoints.projects.budgetLines(id), payload))

export const useDeleteBudgetLine = (id: string) =>
  useProjectMutation((lineId: string) => api.delete(endpoints.projects.budgetLine(id, lineId)))

export const useCostEntries = (id: string, enabled = true) =>
  useProjectQuery<CostEntry[]>(id, 'cost-entries', endpoints.projects.costEntries(id), enabled)

export const useSaveCostEntry = (id: string) =>
  useProjectMutation(({ entryId, ...payload }: { entryId?: string; category: CostCategory; description: string; amount: number; incurredOn: string }) =>
    entryId ? api.put(endpoints.projects.costEntry(id, entryId), payload) : api.post(endpoints.projects.costEntries(id), payload))

export const useDeleteCostEntry = (id: string) =>
  useProjectMutation((entryId: string) => api.delete(endpoints.projects.costEntry(id, entryId)))

export const useProjectFinancials = (id: string, enabled = true) =>
  useProjectQuery<ProjectFinancials>(id, 'financials', endpoints.projects.financials(id), enabled)

// ── Billing terms, valuations, advance, retention ─────────────────────────────

export const useBillingTerms = (id: string, enabled = true) =>
  useProjectQuery<BillingTerms>(id, 'billing-terms', endpoints.projects.billingTerms(id), enabled)

export interface BillingTermsPayload {
  retentionPercent: number | null
  retentionCapAmount: number | null
  retentionReleaseAtCompletionPercent: number | null
  defectsLiabilityMonths: number | null
  advancePaymentAmount: number | null
  advanceRecoveryPercent: number | null
}

export const useSaveBillingTerms = (id: string) =>
  useProjectMutation((payload: BillingTermsPayload) => api.put<BillingTerms>(endpoints.projects.billingTerms(id), payload))

export const useRaiseAdvanceInvoice = (id: string) =>
  useProjectMutation(() => api.post<BillingTerms>(endpoints.projects.advanceInvoice(id)))

export const useValuations = (id: string, enabled = true) =>
  useProjectQuery<Valuation[]>(id, 'valuations', endpoints.projects.valuations(id), enabled)

export const useValuationPreview = (id: string, params: { workDoneToDate?: number | null; materialsOnSite?: number | null; valuationId?: string | null }, enabled = true) =>
  useProjectQuery<ValuationPreview>(id, 'valuation-preview', endpoints.projects.valuationPreview(id), enabled, params)

export interface ValuationPayload {
  periodEnd: string
  workDoneToDate: number
  materialsOnSite: number
  notes: string | null
}

export const useSaveValuation = (id: string) =>
  useProjectMutation(({ valuationId, ...payload }: ValuationPayload & { valuationId?: string }) =>
    valuationId
      ? api.put<Valuation>(endpoints.projects.valuation(id, valuationId), payload)
      : api.post<Valuation>(endpoints.projects.valuations(id), payload))

export const useSubmitValuation = (id: string) =>
  useProjectMutation((valuationId: string) => api.post<Valuation>(endpoints.projects.valuationAction(id, valuationId, 'submit')))

export interface CertifyPayload {
  consultantId: string | null
  certificateReference: string
  certifiedOn: string
  certifiedAmount: number
}

export const useCertifyValuation = (id: string) =>
  useProjectMutation(({ valuationId, ...payload }: CertifyPayload & { valuationId: string }) =>
    api.post<Valuation>(endpoints.projects.valuationAction(id, valuationId, 'certify'), payload))

export const useRaiseValuationInvoice = (id: string) =>
  useProjectMutation((valuationId: string) => api.post<Valuation>(endpoints.projects.valuationAction(id, valuationId, 'raise-invoice')))

export const useVoidValuation = (id: string) =>
  useProjectMutation(({ valuationId, reason }: { valuationId: string; reason: string }) =>
    api.post<Valuation>(endpoints.projects.valuationAction(id, valuationId, 'void'), { reason }))

export const useRetention = (id: string, enabled = true) =>
  useProjectQuery<RetentionPosition>(id, 'retention', endpoints.projects.retention(id), enabled)

export const useReleaseRetention = (id: string) =>
  useProjectMutation((stage: RetentionStage) => api.post<RetentionPosition>(endpoints.projects.retentionReleases(id), { stage }))

// ── Award, handover ───────────────────────────────────────────────────────────

export interface AwardPayload {
  name: string | null
  projectTypeId: string | null
  projectManagerUserId: string | null
  branchId: string | null
  plannedStartDate: string | null
  plannedEndDate: string | null
  templateId: string | null
}

export const useAwardQuotation = () =>
  useProjectMutation(({ quotationId, ...payload }: AwardPayload & { quotationId: string }) =>
    api.post<ProjectDetail>(endpoints.projects.fromQuotation(quotationId), payload))

export const useCreateMaintenanceContract = (id: string) =>
  useProjectMutation((payload: { name: string | null; durationMonths: number }) =>
    api.post<{ contractId: string; contractNumber: string; assetCount: number }>(endpoints.projects.maintenanceContract(id), payload))

// ── Withholding (finance-wide) ────────────────────────────────────────────────

export function useInvoiceWithholding(invoiceId: string, enabled = true) {
  return useQuery({
    enabled: Boolean(invoiceId) && enabled,
    queryKey: ['withholding', invoiceId],
    queryFn: () => api.get<WithholdingCertificate[]>(endpoints.withholding.forInvoice(invoiceId)),
  })
}

export interface WithholdingPayload {
  taxType: 'VAT' | 'IncomeTax'
  ratePercent: number | null
  amount: number
  certificateNumber: string
  certificateDate: string
  notes: string | null
}

/** Recording or reversing a certificate changes the invoice balance, so invoices refresh too. */
function useWithholdingMutation<TPayload>(mutationFn: (payload: TPayload) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['withholding'] })
      void queryClient.invalidateQueries({ queryKey: ['invoices'] })
      void queryClient.invalidateQueries({ queryKey: projectsKey })
    },
  })
}

export const useRecordWithholding = (invoiceId: string) =>
  useWithholdingMutation((payload: WithholdingPayload) => api.post<WithholdingCertificate>(endpoints.withholding.forInvoice(invoiceId), payload))

export const useReverseWithholding = () =>
  useWithholdingMutation(({ id, reason }: { id: string; reason: string }) => api.post<WithholdingCertificate>(endpoints.withholding.reverse(id), { reason }))
