import { useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Briefcase } from 'lucide-react'
import { Link } from 'react-router-dom'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useUpdateAttendanceEntry, type TimesheetClockEntry, type TimesheetJobEntry } from './api'

const cardClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm'
const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

export function formatMinutes(minutes: number) {
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return hours > 0 ? `${hours}h ${String(rest).padStart(2, '0')}m` : `${rest}m`
}

/** Where the clocked hours went: each day's site visits, grouped by job. */
export function JobBreakdown({ jobs, linkJobs }: { jobs: TimesheetJobEntry[]; linkJobs: string | null }) {
  const days = [...new Set(jobs.map((job) => job.date))]
  return (
    <section className={cardClass} aria-label="Time by job">
      <h2 className="flex items-center gap-2 text-base font-semibold text-slate-900"><Briefcase className="h-4 w-4 text-slate-500" />Time by job</h2>
      {jobs.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">No site visits recorded in this period.</p>
      ) : (
        <div className="mt-3 space-y-3">
          {days.map((day) => (
            <div key={day}>
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">{formatDate(day)}</p>
              <ul className="mt-1 divide-y divide-slate-100">
                {jobs.filter((job) => job.date === day).map((job) => (
                  <li key={`${job.date}-${job.workOrderId}`} className="flex items-start justify-between gap-3 py-2 text-sm">
                    <div className="min-w-0">
                      {linkJobs ? (
                        <Link to={`${linkJobs}${job.workOrderId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{job.workOrderNumber}</Link>
                      ) : <span className="font-medium text-slate-900">{job.workOrderNumber}</span>}
                      <p className="truncate text-xs text-slate-500">{job.title}{job.visits > 1 ? ` · ${job.visits} visits` : ''}</p>
                    </div>
                    <div className="shrink-0 text-right">
                      <p className="font-medium text-slate-900">{formatMinutes(job.onSiteMinutes)} on site{job.inProgress ? ' so far' : ''}</p>
                      <p className="text-xs text-slate-500">{formatMinutes(job.travelMinutes)} travel</p>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  )
}

/** `datetime-local` works in the browser's time zone; the API speaks UTC. */
function toLocalInput(iso: string) {
  const date = new Date(iso)
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

function FixForm({ entry, onDone }: { entry: TimesheetClockEntry; onDone: () => void }) {
  const update = useUpdateAttendanceEntry()
  const [clockIn, setClockIn] = useState(toLocalInput(entry.clockInAt))
  const [clockOut, setClockOut] = useState(entry.clockOutAt ? toLocalInput(entry.clockOutAt) : '')
  const [breakMinutes, setBreakMinutes] = useState(String(entry.breakMinutes))
  const [note, setNote] = useState('')

  const save = async () => {
    if (!clockIn || !clockOut) return
    try {
      await update.mutateAsync({
        id: entry.id,
        clockInAt: new Date(clockIn).toISOString(),
        clockOutAt: new Date(clockOut).toISOString(),
        breakMinutes: Number(breakMinutes) || 0,
        note: note.trim() || null,
      })
      toast.success('Hours confirmed')
      onDone()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="mt-2 grid gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3 sm:grid-cols-2">
      <label className="text-xs text-slate-600">Clock in<input type="datetime-local" value={clockIn} onChange={(event) => setClockIn(event.target.value)} className={cn(fieldClass, 'mt-1 bg-white')} /></label>
      <label className="text-xs text-slate-600">Clock out<input type="datetime-local" value={clockOut} onChange={(event) => setClockOut(event.target.value)} className={cn(fieldClass, 'mt-1 bg-white')} /></label>
      <label className="text-xs text-slate-600">Break (minutes)<input type="number" min={0} value={breakMinutes} onChange={(event) => setBreakMinutes(event.target.value)} className={cn(fieldClass, 'mt-1 bg-white')} /></label>
      <label className="text-xs text-slate-600">Note (optional)<input value={note} onChange={(event) => setNote(event.target.value)} placeholder="Confirmed with the technician" className={cn(fieldClass, 'mt-1 bg-white')} /></label>
      <div className="flex gap-2 sm:col-span-2">
        <button type="button" onClick={onDone} className="inline-flex h-10 items-center rounded-lg border border-slate-300 bg-white px-4 text-sm font-medium text-slate-700">Cancel</button>
        <button type="button" onClick={() => void save()} disabled={update.isPending || !clockIn || !clockOut} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-50">
          {update.isPending ? 'Saving…' : 'Save hours'}
        </button>
      </div>
    </div>
  )
}

/** Each clock session; auto clocked-out ones are flagged until someone confirms the real hours. */
export function ClockSessions({ entries, canFix }: { entries: TimesheetClockEntry[]; canFix: boolean }) {
  const [fixing, setFixing] = useState<string | null>(null)
  const flagged = entries.filter((entry) => entry.autoClockedOut).length
  if (entries.length === 0) return null

  return (
    <section className={cardClass} aria-label="Clock sessions">
      <h2 className="text-base font-semibold text-slate-900">Clock sessions</h2>
      {flagged > 0 ? (
        <p className="mt-2 flex items-start gap-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          {flagged === 1 ? 'One session was' : `${flagged} sessions were`} closed automatically because nobody clocked out. {canFix ? 'Confirm or correct the hours.' : 'Your manager will confirm the hours.'}
        </p>
      ) : null}
      <ul className="mt-3 divide-y divide-slate-100">
        {entries.map((entry) => (
          <li key={entry.id} className="py-2 text-sm">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="text-slate-900">
                  {formatDateTime(entry.clockInAt)} → {entry.clockOutAt ? formatDateTime(entry.clockOutAt) : 'still clocked in'}
                </p>
                <p className="text-xs text-slate-500">
                  {entry.totalHours.toFixed(1)}h{entry.breakMinutes ? ` · ${entry.breakMinutes}m break` : ''}
                  {entry.autoClockedOut ? <span className="ml-1 font-medium text-amber-700">· Needs confirming</span> : null}
                </p>
              </div>
              {canFix && entry.clockOutAt && fixing !== entry.id ? (
                <button type="button" onClick={() => setFixing(entry.id)} className={cn('h-8 shrink-0 rounded-lg border px-3 text-xs font-medium', entry.autoClockedOut ? 'border-amber-300 text-amber-800' : 'border-slate-200 text-slate-600')}>
                  {entry.autoClockedOut ? 'Confirm hours' : 'Edit'}
                </button>
              ) : null}
            </div>
            {fixing === entry.id ? <FixForm entry={entry} onDone={() => setFixing(null)} /> : null}
          </li>
        ))}
      </ul>
    </section>
  )
}
