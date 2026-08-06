import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { CircleCheck, Coffee, MapPin, CircleOff, Plus, Download } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { downloadCsv } from '../../shared/utils/csv'
import {
  useAttendanceLive,
  useAttendanceReportSummary,
  useCreateLeaveRequest,
  useDecideLeaveRequest,
  useLeaveRequests,
  useTimesheetDetail,
  useTimesheets,
  type AttendanceLiveEntry,
  type LeaveRequestItem,
  type LeaveRequestStatus,
  type TimesheetListItem,
  type TimesheetView,
} from './timeAttendance/api'
import { useTechnicians } from './api'
import { usePermission } from '../../shared/hooks/usePermission'
import { TeamTabNav } from './components/TeamTabNav'


export function LiveAttendancePage() {
  const { data, isLoading } = useAttendanceLive()
  const summary = data?.summary
  const entries = data?.entries ?? []

  const columns: DataTableColumn<AttendanceLiveEntry>[] = [
    { key: 'technicianName', header: 'Technician', cell: (row) => <span className="font-medium text-slate-900">{row.technicianName}</span> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'clockInAt', header: 'Clocked In At', cell: (row) => (row.clockInAt ? formatDateTime(row.clockInAt) : '—') },
    { key: 'currentWorkOrderNumber', header: 'Current Job', cell: (row) => row.currentWorkOrderNumber ?? '—' },
    { key: 'lastLocation', header: 'Last Location', cell: (row) => row.lastLocation ?? '—' },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Live Attendance" subtitle="Real-time clock status across the technician team." />
      <TeamTabNav />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Clocked In" value={summary?.clockedIn ?? 0} icon={<CircleCheck className="h-5 w-5 text-emerald-500" />} tone="success" />
        <StatCard label="On Break" value={summary?.onBreak ?? 0} icon={<Coffee className="h-5 w-5 text-amber-500" />} tone="warning" />
        <StatCard label="Travelling" value={summary?.travelling ?? 0} icon={<MapPin className="h-5 w-5 text-sky-500" />} />
        <StatCard label="Not Clocked In" value={summary?.notClockedIn ?? 0} icon={<CircleOff className="h-5 w-5 text-slate-400" />} />
      </div>
      <DataTable
        columns={columns}
        data={entries}
        loading={isLoading}
        emptyTitle="No technicians yet"
        emptyDescription="Technicians will appear here as they are added to the team."
      />
    </div>
  )
}

const viewOptions: Array<{ id: TimesheetView; label: string }> = [
  { id: 'week', label: 'This Week' },
  { id: 'month', label: 'This Month' },
  { id: 'custom', label: 'Custom Range' },
]

export function TimesheetsPage() {
  const [view, setView] = useState<TimesheetView>('week')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const { data, isLoading } = useTimesheets(view, from, to)

  const columns: DataTableColumn<TimesheetListItem>[] = [
    { key: 'technicianName', header: 'Technician', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/team/time-attendance/timesheets/${row.id}`}>{row.technicianName}</Link> },
    { key: 'periodLabel', header: 'Period', cell: (row) => row.periodLabel },
    { key: 'totalHours', header: 'Total Hours', cell: (row) => row.totalHours.toFixed(1) },
    { key: 'overtimeHours', header: 'Overtime', cell: (row) => row.overtimeHours.toFixed(1) },
    { key: 'leaveHours', header: 'Leave', cell: (row) => row.leaveHours.toFixed(1) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Timesheets" subtitle="Week, month, and custom-range timesheet views per technician." />
      <div className="flex flex-wrap items-center gap-2 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        {viewOptions.map((option) => (
          <button
            key={option.id}
            type="button"
            onClick={() => setView(option.id)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${view === option.id ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {option.label}
          </button>
        ))}
        {view === 'custom' ? (
          <div className="flex items-center gap-2">
            <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            <span className="text-sm text-slate-500">to</span>
            <input type="date" value={to} onChange={(event) => setTo(event.target.value)} className="h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No timesheets yet"
        emptyDescription="Timesheets appear once technicians clock time or take approved leave in this period."
      />
    </div>
  )
}

export function TimesheetDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading } = useTimesheetDetail(id)

  return (
    <div className="space-y-6">
      <PageHeader
        title={data?.technicianName ?? 'Timesheet Detail'}
        subtitle="Daily breakdown and audit trail."
        breadcrumbs={[{ label: 'Timesheets', to: '/team/time-attendance/timesheets' }, { label: data?.periodLabel ?? 'Detail' }]}
      />
      {isLoading ? null : !data ? (
        <EmptyState title="No timesheet detail yet" description="No clock activity recorded for this technician in the selected period." />
      ) : (
        <>
          <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
            <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-4 py-3 font-medium text-slate-600">Date</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Clock In</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Clock Out</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Break (min)</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Total Hours</th>
                  <th className="px-4 py-3 font-medium text-slate-600">Note</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.dailyBreakdown.map((day) => (
                  <tr key={day.date}>
                    <td className="px-4 py-3 text-slate-700">{formatDate(day.date)}</td>
                    <td className="px-4 py-3 text-slate-700">{day.clockInAt ? formatDateTime(day.clockInAt) : '—'}</td>
                    <td className="px-4 py-3 text-slate-700">{day.clockOutAt ? formatDateTime(day.clockOutAt) : '—'}</td>
                    <td className="px-4 py-3 text-slate-700">{day.breakMinutes}</td>
                    <td className="px-4 py-3 font-medium text-slate-900">{day.totalHours.toFixed(1)}</td>
                    <td className="px-4 py-3 text-slate-700">{day.note ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Audit Trail</h2>
            <ul className="mt-4 space-y-3">
              {data.auditTrail.map((entry, index) => (
                <li key={index} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                  <span className="text-slate-700">{entry.action} — <span className="text-slate-500">{entry.actor}</span></span>
                  <span className="text-slate-500">{formatDateTime(entry.at)}</span>
                </li>
              ))}
            </ul>
          </div>
        </>
      )}
    </div>
  )
}

const leaveStatusOptions: Array<LeaveRequestStatus | 'All'> = ['All', 'Pending', 'Approved', 'Rejected']

export function LeavePage() {
  const canManage = usePermission('team.manage')
  const [statusFilter, setStatusFilter] = useState<LeaveRequestStatus | 'All'>('All')
  const { data, isLoading } = useLeaveRequests(statusFilter)
  const { data: technicians } = useTechnicians()
  const createLeave = useCreateLeaveRequest()
  const [showNewRequest, setShowNewRequest] = useState(false)
  const [technicianId, setTechnicianId] = useState('')
  const [leaveType, setLeaveType] = useState('Annual Leave')
  const [startDate, setStartDate] = useState('')
  const [endDate, setEndDate] = useState('')
  const [reason, setReason] = useState('')

  const submitLeave = async () => {
    if (!technicianId) return toast.error('Select a technician')
    await createLeave.mutateAsync({ technicianId, leaveType, startDate, endDate, reason: reason || undefined })
    toast.success('Leave request submitted')
    setShowNewRequest(false)
    setTechnicianId('')
    setStartDate('')
    setEndDate('')
    setReason('')
  }

  const columns: DataTableColumn<LeaveRequestItem>[] = [
    { key: 'technicianName', header: 'Technician', cell: (row) => <span className="font-medium text-slate-900">{row.technicianName}</span> },
    { key: 'leaveType', header: 'Type', cell: (row) => row.leaveType },
    { key: 'startDate', header: 'From', cell: (row) => formatDate(row.startDate) },
    { key: 'endDate', header: 'To', cell: (row) => formatDate(row.endDate) },
    { key: 'totalDays', header: 'Days', cell: (row) => row.totalDays },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    ...(canManage
      ? [{ key: 'actions', header: '', cell: (row) => <LeaveRowActions request={row} /> } satisfies DataTableColumn<LeaveRequestItem>]
      : []),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Leave"
        subtitle="Leave requests, approvals, and balances."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowNewRequest(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Leave Request
            </button>
          ) : undefined
        }
      />
      <div className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        {leaveStatusOptions.map((status) => (
          <button
            key={status}
            type="button"
            onClick={() => setStatusFilter(status)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${statusFilter === status ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No leave requests yet"
        emptyDescription="Leave requests will appear here once submitted."
      />

      <FormDrawer open={showNewRequest} title="New Leave Request" onClose={() => setShowNewRequest(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Technician</label>
            <select value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option value="">Select a technician…</option>
              {(technicians ?? []).map((t) => <option key={t.id} value={t.id}>{t.fullName}</option>)}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Leave Type</label>
            <select value={leaveType} onChange={(event) => setLeaveType(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option>Annual Leave</option>
              <option>Sick Leave</option>
              <option>Unpaid Leave</option>
            </select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Start Date</label>
              <input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">End Date</label>
              <input type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Reason (optional)</label>
            <textarea value={reason} onChange={(event) => setReason(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!technicianId.trim() || !startDate || !endDate || createLeave.isPending}
            onClick={() => void submitLeave()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {createLeave.isPending ? 'Submitting…' : 'Submit Request'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

function LeaveRowActions({ request }: { request: LeaveRequestItem }) {
  const decide = useDecideLeaveRequest(request.id)

  if (request.status !== 'Pending') {
    return null
  }

  const respond = async (approve: boolean) => {
    await decide.mutateAsync({ approve })
    toast.success(approve ? 'Leave approved' : 'Leave rejected')
  }

  return (
    <div className="flex justify-end gap-2">
      <button type="button" disabled={decide.isPending} onClick={() => void respond(true)} className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-100 disabled:opacity-60">
        Approve
      </button>
      <button type="button" disabled={decide.isPending} onClick={() => void respond(false)} className="rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50 disabled:opacity-60">
        Reject
      </button>
    </div>
  )
}

export function TimeAttendanceReportsPage() {
  const { data } = useAttendanceReportSummary()

  const exportCsv = () => {
    if (!data) return toast.error('No report data to export yet')
    // Route through the shared helper so cells are quote-escaped and formula-injection-neutralized.
    const rows = [
      { Metric: 'Period', Value: data.periodLabel },
      { Metric: 'Total Worked Hours', Value: data.totalWorkedHours },
      { Metric: 'Total Overtime Hours', Value: data.totalOvertimeHours },
      { Metric: 'Total Scheduled Hours', Value: data.totalScheduledHours },
      { Metric: 'Leave Days Taken', Value: data.leaveDaysTaken },
      { Metric: 'Late Arrivals', Value: data.lateArrivals },
    ]
    downloadCsv(`attendance-report-${data.periodLabel.replace(/\s+/g, '-').toLowerCase()}.csv`, rows)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Time & Attendance Reports"
        subtitle="Payroll-ready hours, overtime, and leave summaries."
        primaryAction={
          <button
            type="button"
            onClick={exportCsv}
            className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            <Download className="h-4 w-4" />Export
          </button>
        }
      />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <StatCard label={data?.periodLabel ?? 'This Month'} value={`${(data?.totalWorkedHours ?? 0).toFixed(0)}h worked`} />
        <StatCard label="Overtime Hours" value={(data?.totalOvertimeHours ?? 0).toFixed(0)} tone="warning" />
        <StatCard label="Leave Days Taken" value={data?.leaveDaysTaken ?? 0} />
        <StatCard label="Scheduled Hours" value={(data?.totalScheduledHours ?? 0).toFixed(0)} />
        <StatCard label="Late Arrivals" value={data?.lateArrivals ?? 0} tone="danger" />
      </div>
      <EmptyState title="More reports coming soon" description="Per-technician payroll exports and attendance trend charts are planned for a future release." />
    </div>
  )
}
