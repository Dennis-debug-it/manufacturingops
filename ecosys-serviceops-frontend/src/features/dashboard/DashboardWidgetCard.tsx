import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ArrowDown, ArrowUp, EyeOff, Maximize2, Minimize2, RefreshCw } from 'lucide-react'
import { getErrorStatus } from '../../shared/api/apiError'
import { Surface } from '../../shared/components/Surface'
import { cn } from '../../shared/utils/cn'
import { formatCurrency, formatNumber, formatPercent } from '../../shared/utils/format'
import { useDashboardWidget } from './roleDashboardApi'
import type { DashboardFormat, DashboardLayoutItem, DashboardTone, DashboardWidgetInfo } from './roleDashboardTypes'

const toneText: Record<DashboardTone, string> = {
  warning: 'text-amber-600',
  danger: 'text-rose-600',
}

export function formatWidgetValue(value: number, format: DashboardFormat | null, currency: string | null): string {
  if (format === 'money') return formatCurrency(value, currency)
  if (format === 'percent') return formatPercent(value)
  return formatNumber(value)
}

interface EditControls {
  isFirst: boolean
  isLast: boolean
  onMove: (direction: -1 | 1) => void
  onResize: () => void
  onHide: () => void
}

interface Props {
  item: DashboardLayoutItem
  info: DashboardWidgetInfo | undefined
  branchId: string
  edit?: EditControls | undefined
}

/** One dashboard widget: its own request, its own loading and error state. */
export function DashboardWidgetCard({ item, info, branchId, edit }: Props) {
  const { data, isLoading, isError, error, refresh } = useDashboardWidget(item.key, branchId)
  const [refreshing, setRefreshing] = useState(false)
  const title = data?.title ?? info?.title ?? item.key
  const forbidden = isError && getErrorStatus(error) === 403

  const onRefresh = async () => {
    setRefreshing(true)
    try {
      await refresh()
    } catch {
      // the card keeps its last figures
    } finally {
      setRefreshing(false)
    }
  }

  return (
    <Surface
      as="section"
      variant="glass"
      padding="sm"
      className={cn('min-w-0', item.size === 'wide' ? 'md:col-span-2' : '')}
    >
      <div className="flex min-w-0 flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <h2 className="min-w-0 text-sm font-semibold text-slate-800">{title}</h2>
        {edit ? (
          <div className="flex shrink-0 items-center gap-1">
            <IconButton label={`Move ${title} up`} disabled={edit.isFirst} onClick={() => edit.onMove(-1)}><ArrowUp className="h-4 w-4" /></IconButton>
            <IconButton label={`Move ${title} down`} disabled={edit.isLast} onClick={() => edit.onMove(1)}><ArrowDown className="h-4 w-4" /></IconButton>
            <IconButton label={item.size === 'wide' ? `Make ${title} small` : `Make ${title} wide`} onClick={edit.onResize}>
              {item.size === 'wide' ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
            </IconButton>
            <IconButton label={`Hide ${title}`} onClick={edit.onHide}><EyeOff className="h-4 w-4" /></IconButton>
          </div>
        ) : (
          <IconButton label={`Refresh ${title}`} disabled={refreshing || isLoading} onClick={() => void onRefresh()}>
            <RefreshCw className={cn('h-3.5 w-3.5', refreshing ? 'animate-spin' : '')} />
          </IconButton>
        )}
      </div>

      {isLoading ? (
        <div className="space-y-2" aria-busy="true">
          <div className="h-6 w-1/2 animate-pulse rounded bg-slate-200/70" />
          <div className="h-4 w-3/4 animate-pulse rounded bg-slate-200/70" />
        </div>
      ) : isError || !data ? (
        <p className="text-sm text-slate-500">
          {forbidden ? 'You no longer have access to this widget.' : 'This widget could not be loaded.'}
          {!forbidden ? (
            <button type="button" onClick={() => void onRefresh()} className="ml-2 font-medium text-[var(--app-primary)] underline underline-offset-2">Retry</button>
          ) : null}
        </p>
      ) : (
        <>
          {data.stats.length > 0 ? (
            <div className={cn('grid gap-2', item.size === 'wide' ? 'grid-cols-2 sm:grid-cols-4' : 'grid-cols-2')}>
              {data.stats.map((stat, index) => (
                <Link
                  key={`${stat.label}-${stat.currency ?? ''}-${index}`}
                  to={stat.link}
                  className="min-w-0 rounded-lg border border-[var(--app-border)] bg-white/60 px-3 py-2 transition hover:bg-white"
                >
                  <div className="truncate text-xs text-slate-500">{stat.label}</div>
                  <div className={cn('truncate text-lg font-semibold text-slate-900', stat.tone ? toneText[stat.tone] : '')}>
                    {formatWidgetValue(stat.value, stat.format, stat.currency)}
                  </div>
                </Link>
              ))}
            </div>
          ) : null}
          {data.items.length > 0 ? (
            <ul className="divide-y divide-slate-100">
              {data.items.map((row, index) => (
                <li key={`${row.link}-${index}`}>
                  <Link to={row.link} className="flex min-w-0 items-center justify-between gap-3 py-1.5 text-sm hover:text-[var(--app-primary)]">
                    <span className="min-w-0">
                      <span className="block truncate text-slate-800">{row.label}</span>
                      {row.detail ? <span className="block truncate text-xs text-slate-500">{row.detail}</span> : null}
                    </span>
                    {row.value !== null ? (
                      <span className={cn('shrink-0 text-sm font-medium text-slate-700', row.tone ? toneText[row.tone] : '')}>
                        {formatWidgetValue(row.value, row.format, row.currency)}
                      </span>
                    ) : null}
                  </Link>
                </li>
              ))}
            </ul>
          ) : null}
          {data.note ? <p className="text-xs text-slate-500">{data.note}</p> : null}
        </>
      )}
      </div>
    </Surface>
  )
}

function IconButton({ label, disabled, onClick, children }: { label: string; disabled?: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
      className="inline-flex h-8 w-8 items-center justify-center rounded-md text-slate-500 ring-1 ring-inset ring-slate-200 transition hover:bg-slate-50 hover:text-slate-800 disabled:opacity-40"
    >
      {children}
    </button>
  )
}
