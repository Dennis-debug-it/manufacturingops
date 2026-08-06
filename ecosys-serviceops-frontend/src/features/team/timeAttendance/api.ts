import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'

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
    queryFn: () => api.get<AttendanceLiveResponse>('/api/attendance/live'),
    staleTime: 15_000,
    refetchInterval: 30_000,
  })
}

export interface ClockActionPayload {
  technicianId: string
  location?: string | undefined
}

export function useClockIn() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: ClockActionPayload) => api.post<AttendanceLiveEntry>('/api/attendance/clock-in', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance'] }),
  })
}

export function useClockOut() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: ClockActionPayload) => api.post<void>('/api/attendance/clock-out', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance'] }),
  })
}

export function useSetAttendanceStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { technicianId: string; status: Exclude<AttendanceStatus, 'NotClockedIn'>; location?: string | undefined }) =>
      api.post<void>('/api/attendance/status', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance'] }),
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

export function useTimesheets(view: TimesheetView, from?: string, to?: string) {
  return useQuery({
    queryKey: ['team', 'time-attendance', 'timesheets', view, from ?? '', to ?? ''],
    queryFn: () => api.get<TimesheetListItem[]>('/api/attendance/timesheets', { view, from, to }),
    staleTime: 30_000,
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
    queryFn: () => api.get<TimesheetDetail>(`/api/attendance/timesheets/${id}`),
    enabled: Boolean(id),
    staleTime: 30_000,
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
    queryFn: () => api.get<LeaveRequestItem[]>('/api/attendance/leave', { status: status ?? 'All' }),
    staleTime: 30_000,
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
    mutationFn: (payload: CreateLeaveRequestPayload) => api.post<LeaveRequestItem>('/api/attendance/leave', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['team', 'time-attendance', 'leave'] }),
  })
}

export function useDecideLeaveRequest(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (decision: { approve: boolean; note?: string }) =>
      api.post<void>(`/api/attendance/leave/${id}/decision`, decision),
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
    queryFn: () => api.get<AttendanceReportSummary>('/api/attendance/reports/summary'),
    staleTime: 60_000,
  })
}

export interface TimeAttendanceSettings {
  roundingMinutes: number
  overtimeThresholdHoursPerDay: number
  graceMinutes: number
  autoClockOutAfterHours: number
  leaveTypes: Array<{ code: string; name: string; isPaid: boolean; annualAllowanceDays: number }>
}

export function useTimeAttendanceSettings() {
  return useQuery({
    queryKey: ['settings', 'time-attendance'],
    queryFn: () => api.get<TimeAttendanceSettings>('/api/settings/time-attendance'),
    staleTime: 60_000,
  })
}

export function useSaveTimeAttendanceSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (settings: TimeAttendanceSettings) => api.put<void>('/api/settings/time-attendance', settings),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['settings', 'time-attendance'] }),
  })
}

// ── Technician self-service ──────────────────────────────────────────────────
// These act on the signed-in user's own linked technician profile.

export interface LeaveType {
  code: string
  name: string
  isPaid: boolean
  annualAllowanceDays: number
}

export function useLeaveTypes() {
  return useQuery({
    queryKey: ['attendance', 'leave-types'],
    queryFn: () => api.get<LeaveType[]>('/api/attendance/leave-types'),
    staleTime: 5 * 60_000,
  })
}

export function useMyLeave() {
  return useQuery({
    queryKey: ['attendance', 'me', 'leave'],
    queryFn: () => api.get<LeaveRequestItem[]>('/api/attendance/leave/mine'),
    staleTime: 30_000,
  })
}

export interface ApplyForLeavePayload {
  leaveType: string
  startDate: string
  endDate: string
  reason?: string | undefined
}

export function useApplyForLeave() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: ApplyForLeavePayload) => api.post<LeaveRequestItem>('/api/attendance/leave/mine', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['attendance', 'me', 'leave'] }),
  })
}

export interface MyAttendanceStatus {
  technicianId: string
  technicianName: string
  status: AttendanceStatus
  clockInAt: string | null
  lastLocation: string | null
}

export function useMyAttendanceStatus() {
  return useQuery({
    queryKey: ['attendance', 'me', 'status'],
    queryFn: () => api.get<MyAttendanceStatus>('/api/attendance/me/status'),
    staleTime: 15_000,
  })
}

export function useClockInMine() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { location?: string } = {}) => api.post<MyAttendanceStatus>('/api/attendance/me/clock-in', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['attendance', 'me'] }),
  })
}

export function useClockOutMine() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => api.post<MyAttendanceStatus>('/api/attendance/me/clock-out'),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['attendance', 'me'] }),
  })
}

export function useSetMyStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { status: Exclude<AttendanceStatus, 'NotClockedIn'>; location?: string }) =>
      api.post<MyAttendanceStatus>('/api/attendance/me/status', payload),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['attendance', 'me'] }),
  })
}

export function useMyTimesheet(view: TimesheetView = 'week', from?: string, to?: string) {
  return useQuery({
    queryKey: ['attendance', 'me', 'timesheet', view, from ?? '', to ?? ''],
    queryFn: () => api.get<TimesheetDetail>('/api/attendance/timesheets/mine', { view, from, to }),
    staleTime: 30_000,
  })
}
