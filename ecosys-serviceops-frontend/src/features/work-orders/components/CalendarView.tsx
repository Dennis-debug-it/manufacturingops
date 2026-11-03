import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  addDays,
  addMonths,
  addWeeks,
  eachDayOfInterval,
  endOfMonth,
  endOfWeek,
  format,
  isSameMonth,
  isToday,
  startOfMonth,
  startOfWeek,
} from 'date-fns'
import { ChevronLeft, ChevronRight, X } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { EmptyState } from '../../../shared/components/EmptyState'
import type { WorkOrderRow } from '../listModel'

type CalendarMode = 'month' | 'week' | 'day'

const priorityBarClass: Record<string, string> = {
  Critical: 'bg-rose-500 text-white',
  High: 'bg-amber-500 text-white',
  Medium: 'bg-sky-500 text-white',
  Low: 'bg-slate-400 text-white',
}

function rowDate(row: WorkOrderRow): Date | null {
  const raw = row.scheduledStartAt ?? (row.dueDate !== '—' ? row.dueDate : row.createdAt)
  if (!raw) return null
  const date = new Date(raw)
  return Number.isNaN(date.getTime()) ? null : date
}

export interface CalendarViewProps {
  rows: WorkOrderRow[]
}

export function CalendarView({ rows }: CalendarViewProps) {
  const [mode, setMode] = useState<CalendarMode>('month')
  const [cursor, setCursor] = useState(() => new Date())
  const [selectedDay, setSelectedDay] = useState<Date | null>(null)

  const rowsByDay = useMemo(() => {
    const map = new Map<string, WorkOrderRow[]>()
    for (const row of rows) {
      const date = rowDate(row)
      if (!date) continue
      const key = format(date, 'yyyy-MM-dd')
      const bucket = map.get(key)
      if (bucket) {
        bucket.push(row)
      } else {
        map.set(key, [row])
      }
    }
    return map
  }, [rows])

  const step = (direction: -1 | 1) => {
    setCursor((current) => {
      if (mode === 'month') return addMonths(current, direction)
      if (mode === 'week') return addWeeks(current, direction)
      return addDays(current, direction)
    })
  }

  const days = useMemo(() => {
    if (mode === 'day') {
      return [cursor]
    }
    if (mode === 'week') {
      return eachDayOfInterval({ start: startOfWeek(cursor), end: endOfWeek(cursor) })
    }
    return eachDayOfInterval({ start: startOfWeek(startOfMonth(cursor)), end: endOfWeek(endOfMonth(cursor)) })
  }, [cursor, mode])

  const headerLabel = mode === 'day' ? format(cursor, 'EEEE, d MMM yyyy') : mode === 'week' ? `Week of ${format(startOfWeek(cursor), 'd MMM yyyy')}` : format(cursor, 'MMMM yyyy')

  const selectedDayRows = selectedDay ? rowsByDay.get(format(selectedDay, 'yyyy-MM-dd')) ?? [] : []

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-1 py-1">
          <button type="button" onClick={() => step(-1)} className="rounded-md p-1.5 hover:bg-slate-100" aria-label="Previous">
            <ChevronLeft className="h-4 w-4" />
          </button>
          <span className="min-w-[11rem] px-1 text-center text-sm font-medium text-slate-900">{headerLabel}</span>
          <button type="button" onClick={() => step(1)} className="rounded-md p-1.5 hover:bg-slate-100" aria-label="Next">
            <ChevronRight className="h-4 w-4" />
          </button>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setCursor(new Date())}
            className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50"
          >
            Today
          </button>
          <div className="flex items-center gap-1 rounded-lg border border-slate-200 bg-white px-1 py-1">
            {(['month', 'week', 'day'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setMode(option)}
                className={cn(
                  'rounded-md px-2.5 py-1 text-xs font-medium capitalize transition',
                  mode === option ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100',
                )}
              >
                {option}
              </button>
            ))}
          </div>
        </div>
      </div>

      {rows.length === 0 ? (
        <EmptyState title="No work orders" description="Once work orders match the current filters, they'll appear on the calendar." />
      ) : (
        <div
          className={cn(
            'grid gap-px overflow-hidden rounded-2xl border border-slate-200 bg-slate-200 shadow-sm',
            mode === 'day' ? 'grid-cols-1' : 'grid-cols-7',
          )}
        >
          {mode !== 'day'
            ? ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((label) => (
                <div key={label} className="bg-slate-50 px-2 py-1.5 text-center text-xs font-semibold uppercase tracking-wide text-slate-500">
                  {label}
                </div>
              ))
            : null}

          {days.map((day) => {
            const key = format(day, 'yyyy-MM-dd')
            const dayRows = rowsByDay.get(key) ?? []
            const inCurrentMonth = mode !== 'month' || isSameMonth(day, cursor)

            return (
              <button
                key={key}
                type="button"
                onClick={() => setSelectedDay(day)}
                className={cn(
                  'flex min-h-[7rem] flex-col items-stretch gap-1 bg-white p-2 text-left transition hover:bg-slate-50',
                  !inCurrentMonth && 'bg-slate-50/50 text-slate-400',
                  mode === 'day' && 'min-h-[16rem]',
                )}
              >
                <span
                  className={cn(
                    'inline-flex h-6 w-6 items-center justify-center rounded-full text-xs font-semibold',
                    isToday(day) ? 'bg-[var(--app-primary)] text-white' : 'text-slate-700',
                  )}
                >
                  {format(day, 'd')}
                </span>
                <div className="flex-1 space-y-1 overflow-hidden">
                  {dayRows.slice(0, mode === 'day' ? dayRows.length : 3).map((row) => (
                    <div key={row.id} className={cn('truncate rounded px-1.5 py-0.5 text-[11px] font-medium', priorityBarClass[row.priority] ?? 'bg-slate-400 text-white')}>
                      {row.woNo} · {row.client}
                    </div>
                  ))}
                  {mode !== 'day' && dayRows.length > 3 ? (
                    <div className="text-[11px] font-medium text-slate-500">+{dayRows.length - 3} more</div>
                  ) : null}
                </div>
              </button>
            )
          })}
        </div>
      )}

      {selectedDay ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6">
          <button type="button" aria-label="Close" className="absolute inset-0 bg-[var(--app-primary)]/40" onClick={() => setSelectedDay(null)} />
          <div className="relative flex max-h-[80vh] w-full max-w-lg flex-col rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-6 shadow-xl">
            <div className="flex items-center justify-between gap-4">
              <h3 className="text-lg font-semibold text-slate-900">{format(selectedDay, 'EEEE, d MMM yyyy')}</h3>
              <button type="button" onClick={() => setSelectedDay(null)} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
                <X className="h-4 w-4" />
              </button>
            </div>
            <div className="mt-4 flex-1 space-y-2 overflow-y-auto">
              {selectedDayRows.length === 0 ? (
                <p className="text-sm text-slate-500">No work orders scheduled for this day.</p>
              ) : (
                selectedDayRows.map((row) => (
                  <Link
                    key={row.id}
                    to={`/work-orders/${row.id}`}
                    className="flex items-center justify-between gap-3 rounded-xl border border-slate-200 p-3 text-sm transition hover:border-slate-300 hover:bg-slate-50"
                  >
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <span className={cn('h-2 w-2 shrink-0 rounded-full', priorityBarClass[row.priority]?.split(' ')[0] ?? 'bg-slate-400')} />
                        <span className="font-medium text-slate-900">{row.woNo}</span>
                      </div>
                      <div className="mt-0.5 truncate text-xs text-slate-500">{row.client} · {row.site}</div>
                    </div>
                    <StatusBadge status={row.status} />
                  </Link>
                ))
              )}
            </div>
          </div>
        </div>
      ) : null}
    </div>
  )
}
