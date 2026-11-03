import { AlertTriangle } from 'lucide-react'
import { cn } from '../utils/cn'

export interface ErrorStateProps {
  /** What failed, in the user's words — e.g. "Stock levels unavailable". */
  title: string
  /** Why they are seeing this and what it does *not* mean (i.e. "not the same as empty"). */
  description: string
  /** Usually a react-query `refetch`. Omit only when there is genuinely nothing to retry. */
  onRetry?: (() => void) | undefined
  /**
   * `panel` fills the space a table/section would have occupied. `inline` is a single-row banner
   * for the case where the surrounding layout must stay put — e.g. a KPI row whose tiles are
   * showing a placeholder instead of a fabricated number.
   */
  variant?: 'panel' | 'inline'
  className?: string
}

const retryButtonClass =
  'inline-flex items-center justify-center rounded-lg bg-[var(--app-primary)] text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]'

/**
 * The single "this request failed" affordance for the app. It exists so a failed fetch is never
 * mistaken for a genuinely empty result: `EmptyState` says "there is nothing here", this says
 * "we could not find out", and always offers a way to try again.
 */
export function ErrorState({ title, description, onRetry, variant = 'panel', className }: ErrorStateProps) {
  if (variant === 'inline') {
    return (
      <div
        role="alert"
        className={cn(
          'flex flex-wrap items-center gap-3 rounded-[var(--radius-card)] border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900',
          className,
        )}
      >
        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-600" aria-hidden="true" />
        <span className="flex-1">
          <span className="font-medium">{title}</span> <span className="text-amber-800">{description}</span>
        </span>
        {onRetry ? (
          <button type="button" onClick={onRetry} className={cn(retryButtonClass, 'h-9 px-3')}>
            Retry
          </button>
        ) : null}
      </div>
    )
  }

  return (
    <div
      role="alert"
      className={cn('rounded-2xl border border-dashed border-amber-300 bg-white p-8 text-center shadow-sm', className)}
    >
      <AlertTriangle className="mx-auto h-10 w-10 text-amber-500" aria-hidden="true" />
      <h2 className="mt-4 text-lg font-semibold text-slate-900">{title}</h2>
      <p className="mt-2 text-sm leading-6 text-slate-600">{description}</p>
      {onRetry ? (
        <button type="button" onClick={onRetry} className={cn(retryButtonClass, 'mt-6 h-11 px-4')}>
          Retry
        </button>
      ) : null}
    </div>
  )
}
