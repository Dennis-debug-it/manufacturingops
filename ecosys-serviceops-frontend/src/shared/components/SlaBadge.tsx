import { cn } from '../utils/cn'
import { differenceInMinutes, formatDuration } from '../utils/dates'

export interface SlaBadgeProps {
  deadline: string | null | undefined
  breached: boolean
  paused?: boolean
}

const variantClasses: Record<'success' | 'warning' | 'danger' | 'neutral', string> = {
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-700 ring-amber-200',
  danger: 'bg-rose-50 text-rose-700 ring-rose-200',
  neutral: 'bg-slate-100 text-slate-700 ring-slate-200',
}

export function SlaBadge({ deadline, breached, paused }: SlaBadgeProps) {
  if (paused) {
    return (
      <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', variantClasses.neutral)}>
        Paused
      </span>
    )
  }

  if (!deadline) {
    return (
      <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', variantClasses.neutral)}>
        No SLA
      </span>
    )
  }

  if (breached) {
    return (
      <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', variantClasses.danger)}>
        Breached
      </span>
    )
  }

  const minutesRemaining = differenceInMinutes(deadline, new Date())
  const variant = minutesRemaining <= 120 ? 'warning' : 'success'
  const label = minutesRemaining <= 0 ? 'Overdue' : `${formatDuration(minutesRemaining)} left`

  return (
    <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', variantClasses[variant])}>
      {label}
    </span>
  )
}
