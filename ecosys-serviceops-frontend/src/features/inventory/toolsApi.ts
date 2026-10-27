import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../shared/api/apiClient'

export type ToolStatus = 'InStore' | 'CheckedOut' | 'UnderRepair' | 'Lost' | 'Retired'
export type ToolCondition = 'Good' | 'Fair' | 'Damaged'
export const toolConditions: ToolCondition[] = ['Good', 'Fair', 'Damaged']
export const toolStatusLabels: Record<ToolStatus, string> = {
  InStore: 'In Store',
  CheckedOut: 'Checked Out',
  UnderRepair: 'Under Repair',
  Lost: 'Lost',
  Retired: 'Retired',
}

export interface ToolListItem {
  id: string
  toolCode: string
  name: string
  category: string | null
  serialNumber: string | null
  status: ToolStatus
  condition: ToolCondition
  homeBranchId: string | null
  homeBranchName: string | null
  purchaseCost: number | null
  nextCalibrationDue: string | null
  /** Overdue, DueSoon (within 14 days), Ok, or null when the tool needs no calibration. */
  calibrationState: 'Overdue' | 'DueSoon' | 'Ok' | null
  holderTechnicianId: string | null
  holderName: string | null
  checkedOutAt: string | null
  expectedReturnAt: string | null
  returnOverdue: boolean
  awaitingAcknowledgement: boolean
  workOrderId: string | null
  workOrderNumber: string | null
}

export interface ToolAssignment {
  id: string
  technicianId: string
  technicianName: string | null
  workOrderId: string | null
  workOrderNumber: string | null
  checkedOutAt: string
  checkedOutByName: string | null
  expectedReturnAt: string | null
  conditionOut: ToolCondition
  notes: string | null
  acknowledgedAt: string | null
  returnedAt: string | null
  returnedByName: string | null
  conditionIn: ToolCondition | null
  returnNotes: string | null
}

export interface ToolCalibration {
  id: string
  calibratedOn: string
  nextDueOn: string | null
  result: 'Pass' | 'Fail'
  certificateNumber: string | null
  performedBy: string | null
  notes: string | null
}

export interface ToolDetail {
  tool: ToolListItem
  manufacturer: string | null
  model: string | null
  purchaseDate: string | null
  calibrationIntervalDays: number | null
  lastCalibratedOn: string | null
  notes: string | null
  history: ToolAssignment[]
  calibrations: ToolCalibration[]
}

export interface ToolSummary {
  total: number
  inStore: number
  checkedOut: number
  underRepair: number
  lost: number
  calibrationOverdue: number
  calibrationDueSoon: number
  returnOverdue: number
  /** Tools still out with technicians who have been deactivated (leavers). */
  heldByInactiveTechnicians: number
  valueCheckedOut: number
  /** Checked out but not yet confirmed by the technician. */
  awaitingAcknowledgement: number
}

export interface ToolFilters {
  status?: ToolStatus | 'All' | undefined
  technicianId?: string | undefined
  search?: string | undefined
  attention?: 'calibration' | 'overdue-return' | 'unacknowledged' | undefined
}

export interface UpsertToolPayload {
  toolCode: string
  name: string
  category?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  purchaseDate?: string | null
  purchaseCost?: number | null
  homeBranchId?: string | null
  calibrationIntervalDays?: number | null
  nextCalibrationDue?: string | null
  condition?: ToolCondition | null
  notes?: string | null
}

const base = '/api/tools'

export function useTools(filters: ToolFilters, enabled = true) {
  return useQuery({
    queryKey: ['tools', 'list', filters],
    queryFn: () => api.get<ToolListItem[]>(base, {
      status: filters.status || undefined,
      technicianId: filters.technicianId || undefined,
      search: filters.search || undefined,
      attention: filters.attention || undefined,
    }),
    staleTime: 15_000,
    enabled,
  })
}

export function useToolSummary() {
  return useQuery({ queryKey: ['tools', 'summary'], queryFn: () => api.get<ToolSummary>(`${base}/summary`), staleTime: 15_000 })
}

export function useTool(id: string | undefined) {
  return useQuery({ queryKey: ['tools', 'detail', id], queryFn: () => api.get<ToolDetail>(`${base}/${id}`), enabled: Boolean(id) })
}

export function useMyTools(enabled = true) {
  return useQuery({ queryKey: ['tools', 'mine'], queryFn: () => api.get<ToolListItem[]>(`${base}/mine`), staleTime: 30_000, enabled })
}

function useToolMutation<TPayload>(request: (payload: TPayload) => Promise<ToolDetail>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: request,
    onSuccess: (detail) => {
      queryClient.setQueryData(['tools', 'detail', detail.tool.id], detail)
      void queryClient.invalidateQueries({ queryKey: ['tools'] })
    },
  })
}

export const useCreateTool = () => useToolMutation((payload: UpsertToolPayload) => api.post<ToolDetail>(base, payload))
export const useUpdateTool = (id: string) => useToolMutation((payload: UpsertToolPayload) => api.put<ToolDetail>(`${base}/${id}`, payload))
export const useCheckOutTool = (id: string) =>
  useToolMutation((payload: { technicianId: string; expectedReturnAt?: string | null; condition?: ToolCondition | null; notes?: string | null; overrideCalibration?: boolean }) =>
    api.post<ToolDetail>(`${base}/${id}/check-out`, payload))
export const useCheckInTool = (id: string) =>
  useToolMutation((payload: { condition: ToolCondition; notes?: string | null; sendToRepair?: boolean }) => api.post<ToolDetail>(`${base}/${id}/check-in`, payload))
export const useSetToolStatus = (id: string) =>
  useToolMutation((payload: { status: 'InStore' | 'UnderRepair' | 'Lost' | 'Retired'; notes?: string | null }) => api.post<ToolDetail>(`${base}/${id}/status`, payload))
export const useRecordToolCalibration = (id: string) =>
  useToolMutation((payload: { calibratedOn: string; nextDueOn?: string | null; result: 'Pass' | 'Fail'; certificateNumber?: string | null; performedBy?: string | null; notes?: string | null }) =>
    api.post<ToolDetail>(`${base}/${id}/calibrations`, payload))

export function useAcknowledgeTool() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => api.post<void>(`${base}/${id}/acknowledge`),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['tools'] }),
  })
}
