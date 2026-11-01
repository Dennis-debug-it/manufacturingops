import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'

export interface SummaryFigure {
  label: string
  value: ReactNode
  tone?: 'default' | 'warning' | 'danger' | 'success'
  /** Makes the figure a link to the list behind it. */
  to?: string
  /** Makes the figure a filter toggle instead. */
  onClick?: () => void
  active?: boolean
}

const toneClass: Record<NonNullable<SummaryFigure['tone']>, string> = {
  default: 'text-slate-900',
  warning: 'text-amber-700',
  danger: 'text-rose-700',
  success: 'text-emerald-700',
}

/**
 * A single row of headline figures. Inventory pages used to open with a grid of large tiles before
 * the list anyone came for; this keeps the numbers in one compact band above the table.
 */
export function SummaryStrip({ figures, className }: { figures: SummaryFigure[]; className?: string }) {
  return (
    <div className={cn('grid grid-cols-2 divide-slate-200 rounded-[var(--radius-card)] border border-slate-200 bg-white sm:flex sm:divide-x', className)}>
      {figures.map((figure) => {
        const body = (
          <>
            <span className="block text-xs font-medium text-slate-500">{figure.label}</span>
            <span className={cn('block text-lg font-semibold tabular-nums', toneClass[figure.tone ?? 'default'])}>{figure.value}</span>
          </>
        )
        if (figure.onClick) {
          return (
            <button
              key={figure.label}
              type="button"
              aria-pressed={figure.active ?? false}
              onClick={figure.onClick}
              className={cn('flex-1 px-4 py-2.5 text-left transition hover:bg-slate-50', figure.active && 'bg-slate-100 ring-2 ring-inset ring-[var(--app-primary)]')}
            >
              {body}
            </button>
          )
        }
        return figure.to ? (
          <Link key={figure.label} to={figure.to} className="flex-1 px-4 py-2.5 transition hover:bg-slate-50">
            {body}
          </Link>
        ) : (
          <div key={figure.label} className="flex-1 px-4 py-2.5">
            {body}
          </div>
        )
      })}
    </div>
  )
}
