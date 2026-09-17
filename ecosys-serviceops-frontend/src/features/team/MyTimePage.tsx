import { useState } from 'react'
import toast from 'react-hot-toast'
import { CalendarClock, Clock, Coffee, LogIn, LogOut, Navigation } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate, formatTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import {
  useApplyForLeave,
  useClockInMine,
  useClockOutMine,
  useLeaveTypes,
  useMyAttendanceStatus,
  useMyLeave,
  useMyTimesheet,
  useSetMyStatus,
} from './timeAttendance/api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const secondaryBtn = 'inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-50'
const primaryBtn = 'inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50'

const STATUS_LABELS: Record<string, string> = {
  ClockedIn: 'Clocked in',
  OnBreak: 'On break',
  Travelling: 'Travelling',
  NotClockedIn: 'Not clocked in',
}

function todayIso() {
  return new Date().toISOString().slice(0, 10)
}

function ClockCard() {
  const { data: status, isLoading, isError } = useMyAttendanceStatus()
  const clockIn = useClockInMine()
  const clockOut = useClockOutMine()
  const setStatus = useSetMyStatus()
  const busy = clockIn.isPending || clockOut.isPending || setStatus.isPending

  if (isLoading) return <Spinner label="Loading your clock status" />
  if (isError || !status) {
    return (
      <EmptyState
        title="Time & attendance unavailable"
        description="Your account may not be linked to a technician profile yet. Ask an administrator to link it, then try again."
      />
    )
  }

  const isClockedIn = status.status !== 'NotClockedIn'

  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex items-center justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
            <Clock className="h-4 w-4" /> Current status
          </div>
          <div className="mt-1 text-2xl font-semibold text-slate-900">{STATUS_LABELS[status.status] ?? status.status}</div>
          {status.clockInAt ? (
            <div className="mt-1 text-sm text-slate-500">Since {formatTime(status.clockInAt)}</div>
          ) : null}
        </div>
        <span className={cn('h-3 w-3 rounded-full', isClockedIn ? 'bg-emerald-500' : 'bg-slate-300')} />
      </div>

      <div className="mt-4 flex flex-wrap gap-2">
        {!isClockedIn ? (
          <button type="button" className={primaryBtn} disabled={busy} onClick={() => clockIn.mutate({}, { onSuccess: () => toast.success('Clocked in') })}>
            <LogIn className="h-4 w-4" /> Clock In
          </button>
        ) : (
          <>
            <button type="button" className={primaryBtn} disabled={busy} onClick={() => clockOut.mutate(undefined, { onSuccess: () => toast.success('Clocked out') })}>
              <LogOut className="h-4 w-4" /> Clock Out
            </button>
            {status.status !== 'OnBreak' ? (
              <button type="button" className={secondaryBtn} disabled={busy} onClick={() => setStatus.mutate({ status: 'OnBreak' }, { onSuccess: () => toast.success('On break') })}>
                <Coffee className="h-4 w-4" /> Take Break
              </button>
            ) : (
              <button type="button" className={secondaryBtn} disabled={busy} onClick={() => setStatus.mutate({ status: 'ClockedIn' }, { onSuccess: () => toast.success('Back to work') })}>
                <LogIn className="h-4 w-4" /> End Break
              </button>
            )}
            {status.status !== 'Travelling' ? (
              <button type="button" className={secondaryBtn} disabled={busy} onClick={() => setStatus.mutate({ status: 'Travelling' }, { onSuccess: () => toast.success('Travelling') })}>
                <Navigation className="h-4 w-4" /> Travelling
              </button>
            ) : null}
          </>
        )}
      </div>
    </section>
  )
}

function LeaveSection() {
  const { data: leaveTypes } = useLeaveTypes()
  const { data: myLeave, isLoading } = useMyLeave()
  const apply = useApplyForLeave()

  const [leaveType, setLeaveType] = useState('')
  const [startDate, setStartDate] = useState(todayIso())
  const [endDate, setEndDate] = useState(todayIso())
  const [reason, setReason] = useState('')

  const effectiveType = leaveType || leaveTypes?.[0]?.code || ''

  const submit = () => {
    if (!effectiveType) {
      toast.error('Select a leave type')
      return
    }
    if (endDate < startDate) {
      toast.error('End date cannot be before start date')
      return
    }
    apply.mutate(
      { leaveType: effectiveType, startDate, endDate, reason: reason.trim() || undefined },
      {
        onSuccess: () => {
          toast.success('Leave request submitted')
          setReason('')
        },
      },
    )
  }

  return (
    <section className="space-y-4">
      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <h2 className="text-base font-semibold text-slate-900">Apply for leave</h2>
        <p className="mt-1 text-sm text-slate-500">Submit a request for your manager to approve.</p>
        <div className="mt-4 grid gap-3 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <label className={labelClass} htmlFor="leave-type">Leave type</label>
            <select id="leave-type" className={fieldClass} value={effectiveType} onChange={(event) => setLeaveType(event.target.value)}>
              {(leaveTypes ?? []).map((type) => (
                <option key={type.code} value={type.code}>{type.name}{type.isPaid ? '' : ' (unpaid)'}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="leave-start">Start date</label>
            <input id="leave-start" type="date" className={fieldClass} value={startDate} onChange={(event) => setStartDate(event.target.value)} />
          </div>
          <div>
            <label className={labelClass} htmlFor="leave-end">End date</label>
            <input id="leave-end" type="date" className={fieldClass} value={endDate} min={startDate} onChange={(event) => setEndDate(event.target.value)} />
          </div>
          <div className="sm:col-span-2">
            <label className={labelClass} htmlFor="leave-reason">Reason (optional)</label>
            <textarea id="leave-reason" rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" value={reason} onChange={(event) => setReason(event.target.value)} />
          </div>
        </div>
        <div className="mt-4 flex justify-end">
          <button type="button" className={primaryBtn} disabled={apply.isPending} onClick={submit}>
            {apply.isPending ? 'Submitting…' : 'Submit request'}
          </button>
        </div>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <h2 className="text-base font-semibold text-slate-900">My requests</h2>
        {isLoading ? (
          <div className="mt-4"><Spinner label="Loading your leave requests" /></div>
        ) : (myLeave?.length ?? 0) === 0 ? (
          <p className="mt-3 text-sm text-slate-500">No leave requests yet.</p>
        ) : (
          <ul className="mt-3 divide-y divide-slate-100">
            {myLeave!.map((leave) => (
              <li key={leave.id} className="flex items-center justify-between gap-4 py-3">
                <div>
                  <div className="text-sm font-medium text-slate-900">{leave.leaveType} · {leave.totalDays} day{leave.totalDays === 1 ? '' : 's'}</div>
                  <div className="text-xs text-slate-500">{formatDate(leave.startDate)} → {formatDate(leave.endDate)}{leave.reason ? ` · ${leave.reason}` : ''}</div>
                </div>
                <StatusBadge status={leave.status} />
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}

function TimesheetSection() {
  const { data, isLoading } = useMyTimesheet('week')
  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex items-center gap-2 text-base font-semibold text-slate-900">
        <CalendarClock className="h-4 w-4 text-slate-500" /> My timesheet
      </div>
      {isLoading ? (
        <div className="mt-4"><Spinner label="Loading your timesheet" /></div>
      ) : !data ? (
        <p className="mt-3 text-sm text-slate-500">No timesheet data yet.</p>
      ) : (
        <>
          <p className="mt-1 text-sm text-slate-500">{data.periodLabel}</p>
          <div className="mt-4 grid grid-cols-3 gap-3">
            {[
              ['Worked', `${data.totalHours}h`],
              ['Overtime', `${data.overtimeHours}h`],
              ['Leave', `${data.leaveHours}h`],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-3 text-center">
                <div className="text-xs font-semibold uppercase tracking-wider text-slate-500">{label}</div>
                <div className="mt-1 text-lg font-semibold text-slate-900">{value}</div>
              </div>
            ))}
          </div>
          {data.dailyBreakdown.length > 0 ? (
            <ul className="mt-4 divide-y divide-slate-100">
              {data.dailyBreakdown.map((day) => (
                <li key={day.date} className="flex items-center justify-between py-2 text-sm">
                  <span className="text-slate-700">{formatDate(day.date)}</span>
                  <span className="font-medium text-slate-900">{day.totalHours}h</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="mt-3 text-sm text-slate-500">No hours recorded this week.</p>
          )}
        </>
      )}
    </section>
  )
}

export function MyTimePage() {
  return (
    <div className="space-y-4 pb-24">
      <PageHeader title="My Time & Leave" subtitle="Clock in, apply for leave, and check your hours." />
      <ClockCard />
      <LeaveSection />
      <TimesheetSection />
    </div>
  )
}
