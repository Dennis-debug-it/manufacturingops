import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

// TODO(backend): No TimeAttendanceController / TimesheetController / LeaveController / ClockController
// exists yet (confirmed against the 67-controller snapshot in FRONTEND_BUILD_PLAN.md §4.5).
// WorkOrderBillingController's `time-logs` sub-resource is per-work-order billable time, not a
// company-wide clock in/out + leave system, and must not be reused here.
// Every hook below is a typed stub returning empty/default data — swap the queryFn/mutationFn
// bodies for real `api.get`/`api.post` calls once a backend Time & Attendance API exists.

export type AttendanceStatus = 'ClockedIn' | 'OnBreak' | 'Travelling' | 'NotClockedIn'

export interface AttendanceLiveEntry {
  id: string
  technicianId: string
  technicianName: string
  status: AttendanceStatus
  clockInAt: string | null
  lastLocation: string | null
  currentWorkOrderNumber: string | null
}

export interface AttendanceLiveSummary {
  clockedIn: number
  onBreak: number
  travelling: number
  notClockedIn: number
}

export interface AttendanceLiveResponse {
  summary: AttendanceLiveSummary
  entries: AttendanceLiveEntry[]
}

export function useAttendanceLive() {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'live'],
    queryFn: async (): Promise<AttendanceLiveResponse> => ({
      summary: { clockedIn: 0, onBreak: 0, travelling: 0, notClockedIn: 0 },
      entries: [],
    }),
    staleTime: Infinity,
  })
}

export type TimesheetStatus = 'Pending' | 'Approved' | 'Flagged'

export interface TimesheetListItem {
  id: string
  technicianId: string
  technicianName: string
  periodLabel: string
  totalHours: number
  overtimeHours: number
  leaveHours: number
  status: TimesheetStatus
}

export type TimesheetView = 'week' | 'month' | 'custom'

export function useTimesheets(view: TimesheetView, _from?: string, _to?: string) {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'timesheets', view, _from ?? '', _to ?? ''],
    queryFn: async (): Promise<TimesheetListItem[]> => [],
    staleTime: Infinity,
  })
}

export interface TimesheetDailyEntry {
  date: string
  clockInAt: string | null
  clockOutAt: string | null
  breakMinutes: number
  totalHours: number
  note: string | null
}

export interface TimesheetAuditEntry {
  at: string
  actor: string
  action: string
}

export interface TimesheetDetail extends TimesheetListItem {
  dailyBreakdown: TimesheetDailyEntry[]
  auditTrail: TimesheetAuditEntry[]
}

export function useTimesheetDetail(id: string | undefined) {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'timesheets', id],
    queryFn: async (): Promise<TimesheetDetail | null> => null,
    enabled: Boolean(id),
    staleTime: Infinity,
  })
}

export type LeaveRequestStatus = 'Pending' | 'Approved' | 'Rejected'

export interface LeaveRequestItem {
  id: string
  technicianId: string
  technicianName: string
  leaveType: string
  startDate: string
  endDate: string
  totalDays: number
  status: LeaveRequestStatus
  reason: string | null
  requestedAt: string
}

export function useLeaveRequests(status?: LeaveRequestStatus | 'All') {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'leave', status ?? 'All'],
    queryFn: async (): Promise<LeaveRequestItem[]> => [],
    staleTime: Infinity,
  })
}

export interface CreateLeaveRequestPayload {
  technicianId: string
  leaveType: string
  startDate: string
  endDate: string
  reason?: string | undefined
}

export function useCreateLeaveRequest() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_payload: CreateLeaveRequestPayload): Promise<LeaveRequestItem> => {
      throw new Error('Leave requests cannot be submitted yet — no backend LeaveController exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance', 'leave'] }),
  })
}

export function useDecideLeaveRequest(_id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_decision: { approve: boolean; note?: string }): Promise<void> => {
      throw new Error('Leave approvals cannot be persisted yet — no backend LeaveController exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance', 'leave'] }),
  })
}

export interface AttendanceReportSummary {
  periodLabel: string
  totalScheduledHours: number
  totalWorkedHours: number
  totalOvertimeHours: number
  leaveDaysTaken: number
  lateArrivals: number
}

export function useAttendanceReportSummary() {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'reports', 'summary'],
    queryFn: async (): Promise<AttendanceReportSummary> => ({
      periodLabel: 'This month',
      totalScheduledHours: 0,
      totalWorkedHours: 0,
      totalOvertimeHours: 0,
      leaveDaysTaken: 0,
      lateArrivals: 0,
    }),
    staleTime: Infinity,
  })
}

export interface TimeAttendanceSettings {
  roundingMinutes: number
  overtimeThresholdHoursPerDay: number
  graceMinutes: number
  autoClockOutAfterHours: number
  leaveTypes: Array<{ code: string; name: string; isPaid: boolean; annualAllowanceDays: number }>
}

const defaultTimeAttendanceSettings: TimeAttendanceSettings = {
  roundingMinutes: 15,
  overtimeThresholdHoursPerDay: 8,
  graceMinutes: 10,
  autoClockOutAfterHours: 12,
  leaveTypes: [
    { code: 'ANNUAL', name: 'Annual Leave', isPaid: true, annualAllowanceDays: 21 },
    { code: 'SICK', name: 'Sick Leave', isPaid: true, annualAllowanceDays: 14 },
    { code: 'UNPAID', name: 'Unpaid Leave', isPaid: false, annualAllowanceDays: 0 },
  ],
}

export function useTimeAttendanceSettings() {
  return useQuery({
    queryKey: ['settings', 'time-attendance'],
    queryFn: async (): Promise<TimeAttendanceSettings> => defaultTimeAttendanceSettings,
    staleTime: Infinity,
  })
}

export function useSaveTimeAttendanceSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (_settings: TimeAttendanceSettings): Promise<void> => {
      throw new Error('Time & Attendance settings cannot be saved yet — no backend endpoint exists.')
    },
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'time-attendance'] }),
  })
}
