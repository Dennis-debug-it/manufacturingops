import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { CalendarClock, ChevronRight, Settings2, Wrench } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { formatNumber, inputClass, secondaryButton } from '../components/ui'
import { useFactoryLinks } from '../links'
import { DAYS, WINDOW_STATUS_LABELS, useFactoryWindowSettings, useInstallRoutines, useSaveFactoryWindow, useWindows, type WindowStatus, type WindowSummary } from './api'

export const WINDOW_TONES: Record<WindowStatus, BadgeTone> = { Planned: 'info', Packed: 'info', InProgress: 'warning', HandedBack: 'success', Skipped: 'neutral' }

/** The weekly maintenance windows of every factory: what is coming, and how the last ones went. */
export function WindowsPage() {
  const links = useFactoryLinks()
  const canPlan = usePermission('work-orders.assign')
  const windows = useWindows()
  const [settingsOpen, setSettingsOpen] = useState(false)
  const today = new Date(new Date().toDateString())
  const rows = windows.data ?? []
  const upcoming = rows.filter((x) => new Date(x.windowDate) >= today || x.status === 'InProgress')
  const past = rows.filter((x) => !upcoming.includes(x)).reverse()

  return (
    <div className="space-y-5">
      <PageHeader title="Weekly window" subtitle="Line-stop work done on the weekly no-leaf day: planned, packed, worked and handed back to production."
        secondaryActions={canPlan ? <button type="button" className={secondaryButton} onClick={() => setSettingsOpen(true)}><Settings2 className="h-4 w-4" />Window days and routines</button> : undefined} />

      {windows.isLoading ? <Spinner /> : rows.length === 0 ? (
        <EmptyState icon={CalendarClock} title="No windows" description="Turn the weekly window on for a factory to start planning." />
      ) : (
        <>
          <section>
            <h2 className="mb-2 text-sm font-semibold text-slate-800">Coming up</h2>
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {upcoming.map((window) => <WindowCard key={window.id} window={window} to={links.window(window.id)} />)}
            </div>
          </section>
          {past.length > 0 ? (
            <section>
              <h2 className="mb-2 text-sm font-semibold text-slate-800">Recent</h2>
              <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
                <table className="w-full text-sm">
                  <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Date</th><th className="px-4 py-2">Factory</th><th className="px-4 py-2">Status</th><th className="px-4 py-2 text-right">Jobs done</th></tr></thead>
                  <tbody className="divide-y divide-slate-100">
                    {past.map((window) => (
                      <tr key={window.id}>
                        <td className="px-4 py-2"><Link className="underline" to={links.window(window.id)}>{formatDate(window.windowDate, 'EEE dd MMM')}</Link></td>
                        <td className="px-4 py-2">{window.factoryName}</td>
                        <td className="px-4 py-2"><Badge size="sm" tone={WINDOW_TONES[window.status]}>{WINDOW_STATUS_LABELS[window.status]}</Badge>{window.skipReason ? <span className="ml-2 text-xs text-slate-500">{window.skipReason}</span> : null}</td>
                        <td className="px-4 py-2 text-right tabular-nums">{window.status === 'Skipped' ? '—' : `${window.jobsDone}/${window.jobs}`}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          ) : null}
        </>
      )}

      <WindowSettingsDrawer open={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </div>
  )
}

export function CapacityBar({ planned, capacity }: { planned: number; capacity: number }) {
  const pct = capacity > 0 ? Math.min(100, (planned / capacity) * 100) : planned > 0 ? 100 : 0
  const over = capacity > 0 && planned > capacity
  return (
    <div>
      <div className="flex justify-between text-xs text-slate-600">
        <span>{formatNumber(planned)} h planned</span>
        <span className={cn(over && 'font-semibold text-rose-700')}>{over ? `${formatNumber(planned - capacity)} h over ` : ''}{formatNumber(capacity)} h available</span>
      </div>
      <div className="mt-1 h-2 rounded-full bg-slate-100" role="img" aria-label={`${planned} of ${capacity} hours`}>
        <div className={cn('h-2 rounded-full', over ? 'bg-rose-600' : 'bg-[var(--app-primary)]')} style={{ width: `${pct}%` }} />
      </div>
    </div>
  )
}

function WindowCard({ window, to }: { window: WindowSummary; to: string }) {
  return (
    <Link to={to} className="block rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm hover:border-slate-300">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{window.factoryName}</p>
          <p className="text-lg font-semibold text-slate-900">{formatDate(window.windowDate, 'EEEE dd MMM')}</p>
          <p className="text-xs text-slate-500">{formatTime(window.startsAt)}–{formatTime(window.endsAt)}{window.movedFromDate ? ` · moved from ${formatDate(window.movedFromDate, 'EEE dd MMM')}` : ''}</p>
        </div>
        <Badge size="sm" tone={WINDOW_TONES[window.status]}>{WINDOW_STATUS_LABELS[window.status]}</Badge>
      </div>
      {window.status === 'Skipped' ? <p className="mt-3 text-sm text-slate-600">Skipped: {window.skipReason}</p> : (
        <div className="mt-3 space-y-2">
          <p className="flex items-center gap-1.5 text-sm text-slate-700"><Wrench className="h-4 w-4 text-slate-400" />{window.jobs} job{window.jobs === 1 ? '' : 's'}{window.jobsDone ? ` · ${window.jobsDone} done` : ''}<ChevronRight className="ml-auto h-4 w-4 text-slate-300" /></p>
          <CapacityBar planned={window.plannedHours} capacity={window.capacityHours} />
        </div>
      )}
    </Link>
  )
}

function WindowSettingsDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const factories = useFactoryWindowSettings()
  const save = useSaveFactoryWindow()
  const install = useInstallRoutines()

  const update = async (factoryId: string, enabled: boolean, day: number) => {
    try {
      await save.mutateAsync({ factoryId, enabled, day })
      toast.success('Window saved')
    } catch {
      // The API client shows the error.
    }
  }

  const installRoutines = async (factoryId: string) => {
    try {
      const result = await install.mutateAsync(factoryId)
      toast.success(result.plansAdded ? `${result.plansAdded} weekly routine${result.plansAdded === 1 ? '' : 's'} added` : 'The routines are already set up')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={open} title="Window days and routines" onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-slate-600">Each factory's weekly window day. Weekly routines (lubrication, belts and chains, CTC segments, dryer cleaning, fans, panels, boiler) are added as line-stop PM plans on the factory's matching assets, so they are raised into every window.</p>
        {(factories.data ?? []).map((factory) => (
          <div key={factory.factoryId} className="space-y-3 rounded-lg border border-slate-200 p-3">
            <div className="flex items-center justify-between gap-2">
              <p className="font-medium text-slate-900">{factory.factoryName}</p>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={factory.enabled} onChange={(event) => void update(factory.factoryId, event.target.checked, factory.day)} />Weekly window</label>
            </div>
            {factory.enabled ? (
              <div className="flex flex-wrap items-center gap-2">
                <select aria-label={`${factory.factoryName} window day`} className={cn(inputClass, 'w-40')} value={factory.day} onChange={(event) => void update(factory.factoryId, true, Number(event.target.value))}>
                  {DAYS.map((day, index) => <option key={day} value={index}>{day}</option>)}
                </select>
                <button type="button" className={secondaryButton} disabled={install.isPending} onClick={() => void installRoutines(factory.factoryId)}>Set up weekly routines</button>
              </div>
            ) : null}
          </div>
        ))}
      </div>
    </FormDrawer>
  )
}
