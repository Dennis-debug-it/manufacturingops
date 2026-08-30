import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import {
  addMonths,
  addWeeks,
  differenceInCalendarDays,
  eachDayOfInterval,
  endOfMonth,
  endOfWeek,
  format,
  isSameDay,
  isSameMonth,
  startOfMonth,
  startOfWeek,
  subMonths,
  subWeeks,
} from 'date-fns'
import { ChevronLeft, ChevronRight, X } from 'lucide-react'
import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatCard } from '../../shared/components/StatCard'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate, toWorkspaceDate } from '../../shared/utils/dates'
import { useGenerateWorkOrderNow, usePmPlans } from './api'
import type { PmPlan } from './types'
import { MaintenanceTabNav } from './components/MaintenanceTabNav'

type Tone = 'grey' | 'green' | 'blue' | 'amber' | 'red'

const toneDot: Record<Tone, string> = {
  grey: 'bg-slate-400',
  green: 'bg-emerald-500',
  blue: 'bg-sky-500',
  amber: 'bg-amber-500',
  red: 'bg-rose-500',
}
const toneChip: Record<Tone, string> = {
  grey: 'bg-slate-100 text-slate-600',
  green: 'bg-emerald-50 text-emerald-700',
  blue: 'bg-sky-50 text-sky-700',
  amber: 'bg-amber-50 text-amber-700',
  red: 'bg-rose-50 text-rose-700',
}

function planTone(plan: PmPlan): Tone {
  if (plan.status !== 'Active') return 'grey'
  if (!plan.nextPmDate) return 'grey'
  const days = differenceInCalendarDays(new Date(plan.nextPmDate), new Date())
  if (days < 0) return 'red'
  if (days <= 7) return 'amber'
  if (days <= 14) return 'blue'
  return 'green'
}

export function CalendarPage() {
  const [view, setView] = useState<'month' | 'week'>('month')
  const [cursor, setCursor] = useState(new Date())
  const [selected, setSelected] = useState<PmPlan | null>(null)
  const canTrigger = usePermission('maintenance.trigger')
  const { data, isLoading, isError, refetch } = usePmPlans()
  const generateNow = useGenerateWorkOrderNow(selected?.id ?? '')

  const plans = useMemo(() => data ?? [], [data])

  const days = useMemo(() => {
    if (view === 'week') {
      return eachDayOfInterval({ start: startOfWeek(cursor, { weekStartsOn: 1 }), end: endOfWeek(cursor, { weekStartsOn: 1 }) })
    }
    return eachDayOfInterval({ start: startOfWeek(startOfMonth(cursor), { weekStartsOn: 1 }), end: endOfWeek(endOfMonth(cursor), { weekStartsOn: 1 }) })
  }, [view, cursor])

  const plansByDay = useMemo(() => {
    const map = new Map<string, PmPlan[]>()
    for (const plan of plans) {
      if (!plan.nextPmDate) continue
      const key = format(toWorkspaceDate(plan.nextPmDate), 'yyyy-MM-dd')
      const bucket = map.get(key) ?? []
      bucket.push(plan)
      map.set(key, bucket)
    }
    return map
  }, [plans])

  const stats = useMemo(() => {
    const counts: Record<Tone, number> = { grey: 0, green: 0, blue: 0, amber: 0, red: 0 }
    for (const plan of plans) counts[planTone(plan)] += 1
    return counts
  }, [plans])

  const navigate = (direction: -1 | 1) => {
    setCursor((current) => (view === 'week' ? (direction === 1 ? addWeeks(current, 1) : subWeeks(current, 1)) : direction === 1 ? addMonths(current, 1) : subMonths(current, 1)))
  }

  const triggerGenerate = async () => {
    try {
      const wo = await generateNow.mutateAsync(undefined)
      toast.success(`Work order ${wo.workOrderNumber} generated`)
      setSelected(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading PM calendar" />
  }

  if (isError) {
    return (
      <ErrorState
        title="PM calendar unavailable"
        description="We couldn’t load preventive maintenance plans, so an empty calendar would be misleading."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="PM Calendar"
        subtitle="Preventive maintenance schedule across all active plans."
        secondaryActions={
          <div className="flex items-center gap-2">
            {(['month', 'week'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setView(option)}
                className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium capitalize transition', view === option ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
              >
                {option}
              </button>
            ))}
          </div>
        }
      />
      <MaintenanceTabNav />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        <StatCard label="Overdue" value={stats.red} tone="danger" />
        <StatCard label="Due This Week" value={stats.amber} tone="warning" />
        <StatCard label="Due Soon" value={stats.blue} />
        <StatCard label="On Track" value={stats.green} tone="success" />
        <StatCard label="Inactive" value={stats.grey} />
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <div className="mb-4 flex items-center justify-between">
          <button type="button" onClick={() => navigate(-1)} className="rounded-lg p-2 hover:bg-slate-100"><ChevronLeft className="h-4 w-4" /></button>
          <div className="text-sm font-semibold text-slate-900">{view === 'week' ? `Week of ${formatDate(days[0] ?? cursor)}` : format(cursor, 'MMMM yyyy')}</div>
          <button type="button" onClick={() => navigate(1)} className="rounded-lg p-2 hover:bg-slate-100"><ChevronRight className="h-4 w-4" /></button>
        </div>
        <div className="grid grid-cols-7 gap-1 text-center text-xs font-semibold uppercase tracking-wide text-slate-500">
          {['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'].map((day) => <div key={day} className="py-2">{day}</div>)}
        </div>
        <div className={cn('grid grid-cols-7 gap-1', view === 'week' ? '' : '')}>
          {days.map((day) => {
            const key = format(day, 'yyyy-MM-dd')
            const dayPlans = plansByDay.get(key) ?? []
            const inMonth = view === 'week' || isSameMonth(day, cursor)
            const today = isSameDay(day, new Date())
            return (
              <div key={key} className={cn('min-h-[6rem] rounded-lg border p-1.5', inMonth ? 'border-slate-200 bg-white' : 'border-slate-100 bg-slate-50', today && 'ring-2 ring-[var(--app-primary)]')}>
                <div className={cn('text-xs font-medium', inMonth ? 'text-slate-700' : 'text-slate-400')}>{format(day, 'd')}</div>
                <div className="mt-1 space-y-1">
                  {dayPlans.slice(0, 3).map((plan) => (
                    <button
                      key={plan.id}
                      type="button"
                      onClick={() => setSelected(plan)}
                      className={cn('flex w-full items-center gap-1 truncate rounded px-1.5 py-0.5 text-left text-[11px] font-medium', toneChip[planTone(plan)])}
                    >
                      <span className={cn('h-1.5 w-1.5 shrink-0 rounded-full', toneDot[planTone(plan)])} />
                      <span className="truncate">{plan.assetName ?? 'Asset'}</span>
                    </button>
                  ))}
                  {dayPlans.length > 3 ? <div className="text-[11px] text-slate-400">+{dayPlans.length - 3} more</div> : null}
                </div>
              </div>
            )
          })}
        </div>
      </div>

      {selected ? (
        <div className="fixed inset-0 z-50 flex justify-end bg-[var(--app-primary)]/40" onClick={() => setSelected(null)}>
          <div className="h-full w-full max-w-md overflow-y-auto bg-white p-6 shadow-2xl" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-start justify-between">
              <h3 className="text-lg font-semibold text-slate-900">{selected.assetName ?? 'PM Plan'}</h3>
              <button type="button" onClick={() => setSelected(null)} className="rounded-lg p-2 hover:bg-slate-100"><X className="h-4 w-4" /></button>
            </div>
            <div className="mt-4 space-y-3 text-sm">
              <div className="flex justify-between"><span className="text-slate-500">Client</span><span className="font-medium text-slate-900">{selected.clientName ?? '—'}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Template</span><span className="font-medium text-slate-900">{selected.pmTemplateName ?? '—'}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Frequency</span><span className="font-medium text-slate-900">{selected.frequency}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Next Due</span><span className="font-medium text-slate-900">{selected.nextPmDate ? formatDate(selected.nextPmDate) : '—'}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Last Done</span><span className="font-medium text-slate-900">{selected.lastPmDate ? formatDate(selected.lastPmDate) : '—'}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Status</span><span className="font-medium text-slate-900">{selected.status}</span></div>
            </div>
            <div className="mt-6 flex flex-col gap-2">
              {canTrigger ? (
                <button type="button" onClick={triggerGenerate} disabled={generateNow.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
                  {generateNow.isPending ? 'Generating…' : 'Generate Work Order Now'}
                </button>
              ) : null}
              <Link to={`/maintenance/schedules/${selected.id}`} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                View Full Schedule
              </Link>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
