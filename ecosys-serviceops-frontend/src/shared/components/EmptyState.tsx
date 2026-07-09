import type { LucideIcon } from 'lucide-react'
import { cn } from '../utils/cn'

export interface EmptyStateProps {
  icon?: LucideIcon
  title: string
  description: string
  actionLabel?: string | undefined
  onAction?: (() => void) | undefined
  className?: string
}

export function EmptyState({ icon: Icon, title, description, actionLabel, onAction, className }: EmptyStateProps) {
  return (
    <div className={cn('rounded-2xl border border-dashed border-slate-200 bg-white p-8 text-center shadow-sm', className)}>
      {Icon ? <Icon className="mx-auto h-10 w-10 text-slate-400" aria-hidden="true" /> : null}
      <h2 className="mt-4 text-lg font-semibold text-slate-900">{title}</h2>
      <p className="mt-2 text-sm leading-6 text-slate-600">{description}</p>
      {actionLabel && onAction ? (
        <button
          type="button"
          className="mt-6 inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
          onClick={onAction}
        >
          {actionLabel}
        </button>
      ) : null}
    </div>
  )
}
