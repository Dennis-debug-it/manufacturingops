import { cn } from '../../../shared/utils/cn'

const priorityClasses: Record<string, string> = {
  Critical: 'bg-rose-50 text-rose-700 ring-rose-200',
  High: 'bg-orange-50 text-orange-700 ring-orange-200',
  Medium: 'bg-amber-50 text-amber-700 ring-amber-200',
  Low: 'bg-slate-100 text-slate-600 ring-slate-200',
}

const priorityDotClasses: Record<string, string> = {
  Critical: 'bg-rose-500',
  High: 'bg-orange-500',
  Medium: 'bg-amber-500',
  Low: 'bg-slate-400',
}

export function priorityDotClass(priority: string) {
  return priorityDotClasses[priority] ?? 'bg-slate-400'
}

export interface PriorityBadgeProps {
  priority: string
  className?: string
}

export function PriorityBadge({ priority, className }: PriorityBadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ring-1 ring-inset',
        priorityClasses[priority] ?? priorityClasses.Low,
        className,
      )}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full', priorityDotClass(priority))} />
      {priority}
    </span>
  )
}
