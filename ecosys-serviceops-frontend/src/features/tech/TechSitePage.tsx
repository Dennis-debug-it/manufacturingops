import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { useSearchParams } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { AlertTriangle, CheckCheck, CloudOff, HardHat, Lock } from 'lucide-react'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { useMySites } from '../projects/siteApi'
import type { MusterRow, MusterStatus, MySite } from '../projects/types'
import { savedMessage, useTechActions } from './offline/actions'
import { refreshAfterSync, useMusterOutbox, useMusterSheetView } from './offline/hooks'
import { retryOutboxEntry } from './offline/outbox'
import { TechAddWorker } from './TechAddWorker'

/** Today and yesterday in the workspace's time zone (the server refuses future days and locks older ones). */
function siteDays() {
  const today = formatDate(new Date(), 'yyyy-MM-dd')
  const yesterday = formatDate(new Date(Date.now() - 24 * 60 * 60 * 1000), 'yyyy-MM-dd')
  return { today, yesterday }
}

const STATUS_OPTIONS: { value: MusterStatus; label: string; active: string }[] = [
  { value: 'Present', label: 'Present', active: 'border-emerald-600 bg-emerald-600 text-white' },
  { value: 'HalfDay', label: 'Half day', active: 'border-amber-500 bg-amber-500 text-white' },
  { value: 'Absent', label: 'Absent', active: 'border-slate-600 bg-slate-600 text-white' },
]

const MAN_DAYS: Record<MusterStatus, number> = { Present: 1, HalfDay: 0.5, Absent: 0 }

/**
 * The site supervisor's muster roll on the phone: pick a site and a day (today or yesterday), mark
 * each worker Present / Half day / Absent and save. Works offline (the day is saved on the phone
 * and synced later). Attendance only: no pay rates or amounts are ever shown here.
 */
export function TechSitePage() {
  const { hasModule, isLoading: licenseLoading } = useEntitlements()
  const canMark = usePermission('projects.muster.mark')
  const enabled = hasModule('projects') && canMark
  const { data: sites, isLoading, isError } = useMySites(enabled)
  const [params, setParams] = useSearchParams()
  const { today, yesterday } = siteDays()

  const markable = useMemo(() => (sites ?? []).filter((site) => site.canMarkMuster), [sites])
  const site = markable.find((item) => item.projectId === params.get('project')) ?? markable[0]
  const date = params.get('date') === yesterday ? yesterday : today

  const choose = (next: { project?: string; date?: string }) => {
    const merged = new URLSearchParams(params)
    if (next.project) merged.set('project', next.project)
    if (next.date) merged.set('date', next.date)
    setParams(merged, { replace: true })
  }

  if (licenseLoading) return <Spinner label="Loading" />
  if (!enabled) {
    return <EmptyState icon={HardHat} title="Muster roll not available" description="Marking the muster roll needs the Projects module and site supervisor access. Ask your project manager if you should have it." />
  }
  if (isLoading && !sites) return <Spinner label="Loading your sites" />
  if (isError && !sites) {
    return <EmptyState icon={CloudOff} title="Couldn't load your sites" description="Open this page once with a signal so your sites are kept on the phone." />
  }
  if (!site) {
    return <EmptyState icon={HardHat} title="No sites to mark" description="You're not a supervisor on any open project. Your project manager adds you to a project's site team." />
  }

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-lg font-semibold text-slate-900">Muster roll</h1>
        <p className="text-sm text-slate-500">Mark who is on site. Pay is worked out in the office.</p>
      </div>

      {markable.length > 1 ? (
        <label className="block text-sm font-medium text-slate-700">
          Site
          <select value={site.projectId} onChange={(event) => choose({ project: event.target.value })} className="mt-1 h-11 w-full rounded-lg border border-slate-300 bg-white px-3 text-base outline-none focus:border-[var(--app-primary)] sm:text-sm">
            {markable.map((item) => <option key={item.projectId} value={item.projectId}>{item.projectNumber} · {item.name}</option>)}
          </select>
        </label>
      ) : null}
      <SiteHeading site={site} />

      <div role="group" aria-label="Day" className="grid grid-cols-2 gap-1.5 rounded-xl bg-slate-100 p-1">
        {[{ value: today, label: 'Today' }, { value: yesterday, label: 'Yesterday' }].map((option) => (
          <button
            key={option.value}
            type="button"
            aria-pressed={date === option.value}
            onClick={() => choose({ date: option.value })}
            className={cn('h-11 rounded-lg text-sm font-medium', date === option.value ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-600')}
          >
            {option.label} <span className="font-normal text-slate-500">· {formatDate(option.value, 'EEE d MMM')}</span>
          </button>
        ))}
      </div>

      <MusterDay key={`${site.projectId}:${date}`} site={site} date={date} />
    </div>
  )
}

function SiteHeading({ site }: { site: MySite }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-white p-3">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{site.projectNumber}</p>
      <p className="text-sm font-semibold text-slate-900">{site.name}</p>
      <p className="text-xs text-slate-500">{[site.clientName, site.siteName].filter(Boolean).join(' · ')}</p>
    </div>
  )
}

function MusterDay({ site, date }: { site: MySite; date: string }) {
  const { data: sheet, isLoading, isError } = useMusterSheetView(site.projectId, date)
  const { pending, failed } = useMusterOutbox(site.projectId, date)
  const actions = useTechActions()
  const queryClient = useQueryClient()
  // Taps not yet saved, by worker; cleared once saved (the sheet, or the queued save, then shows them).
  const [edits, setEdits] = useState<Record<string, MusterStatus>>({})

  if (isLoading && !sheet) return <Spinner label="Loading the day sheet" />
  if (!sheet) {
    return (
      <EmptyState
        icon={CloudOff}
        title={isError ? "Couldn't load this day" : 'No day sheet'}
        description="Open this day once with a signal and it will be kept on the phone for marking offline."
      />
    )
  }

  const locked = sheet.isLocked
  const statusOf = (row: MusterRow) => edits[row.workerId] ?? row.status
  const rows = sheet.rows
  const counts = rows.reduce(
    (total, row) => {
      const status = statusOf(row)
      if (status) total[status]++
      else total.unmarked++
      total.manDays += status ? MAN_DAYS[status] : 0
      return total
    },
    { Present: 0, HalfDay: 0, Absent: 0, unmarked: 0, manDays: 0 },
  )
  const unsavedSuggestions = rows.some((row) => row.isSuggestion && row.status && !edits[row.workerId])
  const dirty = Object.keys(edits).length > 0 || unsavedSuggestions
  const marks = rows.flatMap((row) => {
    const status = statusOf(row)
    return status ? [{ workerId: row.workerId, status, note: row.note }] : []
  })

  const mark = (workerId: string, status: MusterStatus) => setEdits((current) => ({ ...current, [workerId]: status }))
  const markAllPresent = () => setEdits(Object.fromEntries(rows.filter((row) => row.isActive).map((row) => [row.workerId, 'Present' as MusterStatus])))

  const save = () => {
    actions.saveMuster(site, date, marks)
      .then((result) => {
        setEdits({})
        toast.success(savedMessage('Muster roll saved', result))
      })
      .catch(() => {
        // interceptor surfaces the error toast (day locked, worker removed, …)
      })
  }

  return (
    <div className="space-y-3">
      {locked ? (
        <div className="flex items-start gap-2 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700">
          <Lock className="mt-0.5 h-4 w-4 shrink-0" />
          <span>This day is locked. Ask your project manager if a mark needs correcting.</span>
        </div>
      ) : null}
      {pending.length > 0 ? (
        <div role="status" className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          <CloudOff className="mt-0.5 h-4 w-4 shrink-0" />
          <span>Saved on this phone. It will sync when you&apos;re back online.</span>
        </div>
      ) : null}
      {failed.map((entry) => (
        <div key={entry.id} role="alert" className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div className="min-w-0 flex-1">
            <p>This day&apos;s marks didn&apos;t sync{entry.error ? `: ${entry.error}` : '.'}</p>
            <button type="button" onClick={() => void retryOutboxEntry(entry.id, () => refreshAfterSync(queryClient))} className="mt-1 inline-flex h-11 items-center font-medium underline">
              Try again
            </button>
          </div>
        </div>
      ))}

      <div className="grid grid-cols-4 gap-1.5 text-center" aria-label="Day totals">
        <Total label="Present" value={counts.Present} />
        <Total label="Half day" value={counts.HalfDay} />
        <Total label="Absent" value={counts.Absent} />
        <Total label="Man-days" value={counts.manDays} />
      </div>
      {counts.unmarked > 0 ? <p className="text-xs text-slate-500">{counts.unmarked} not marked yet.</p> : null}

      {rows.length === 0 ? (
        <EmptyState icon={HardHat} title="No workers on the roster" description="Add the workers on this site to start the muster roll." />
      ) : (
        <>
          {!locked ? (
            <button type="button" onClick={markAllPresent} className="inline-flex h-11 w-full items-center justify-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 text-sm font-medium text-emerald-800 hover:bg-emerald-100">
              <CheckCheck className="h-4 w-4" /> Mark all present
            </button>
          ) : null}
          <ul className="space-y-2">
            {rows.map((row) => {
              const status = statusOf(row)
              const suggested = row.isSuggestion && !edits[row.workerId]
              return (
                <li key={row.workerId} className="rounded-xl border border-slate-200 bg-white p-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-slate-900">{row.fullName}</p>
                      <p className="truncate text-xs text-slate-500">{[row.trade, row.engagementType, row.isActive ? null : 'Off the roster'].filter(Boolean).join(' · ')}</p>
                    </div>
                    {suggested && status ? <span className="shrink-0 rounded-full bg-sky-50 px-2 py-0.5 text-[11px] font-medium text-sky-700">Suggested</span> : null}
                  </div>
                  <div role="group" aria-label={`Mark ${row.fullName}`} className="mt-2 grid grid-cols-3 gap-1.5">
                    {STATUS_OPTIONS.map((option) => {
                      const selected = status === option.value
                      return (
                        <button
                          key={option.value}
                          type="button"
                          disabled={locked}
                          aria-pressed={selected}
                          onClick={() => mark(row.workerId, option.value)}
                          className={cn(
                            'h-12 rounded-lg border px-1 text-sm font-medium transition disabled:opacity-60',
                            selected ? option.active : 'border-slate-200 bg-white text-slate-700',
                            selected && suggested && 'opacity-70',
                          )}
                        >
                          {option.label}
                        </button>
                      )
                    })}
                  </div>
                </li>
              )
            })}
          </ul>
        </>
      )}

      {!locked ? <TechAddWorker projectId={site.projectId} /> : null}

      {!locked && rows.length > 0 ? (
        <div className="fixed inset-x-0 bottom-0 z-10 border-t border-slate-200 bg-white/95 backdrop-blur">
          <div className="mx-auto max-w-xl px-4 pb-[max(env(safe-area-inset-bottom),0.75rem)] pt-3">
            <button
              type="button"
              onClick={save}
              disabled={actions.busy || marks.length === 0 || !dirty}
              className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-semibold text-white disabled:opacity-50"
            >
              {actions.busy ? 'Saving…' : dirty ? `Save ${formatDate(date, 'EEE d MMM')} (${marks.length} marked)` : 'Saved'}
            </button>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function Total({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg bg-white p-2 ring-1 ring-slate-200">
      <p className="text-base font-semibold text-slate-900">{value}</p>
      <p className="text-[11px] text-slate-500">{label}</p>
    </div>
  )
}
