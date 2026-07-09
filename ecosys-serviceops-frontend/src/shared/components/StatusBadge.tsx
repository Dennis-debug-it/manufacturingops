import { cn } from '../utils/cn'
import { formatStatus } from '../utils/format'

export interface StatusBadgeProps {
  status: string
}

const variantClasses: Record<string, string> = {
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-700 ring-amber-200',
  danger: 'bg-rose-50 text-rose-700 ring-rose-200',
  info: 'bg-sky-50 text-sky-700 ring-sky-200',
  neutral: 'bg-slate-100 text-slate-700 ring-slate-200',
}

function resolveVariant(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('complete') || normalized.includes('paid') || normalized.includes('active') || normalized.includes('approved') || normalized === 'success') {
    return 'success'
  }
  if (normalized.includes('overdue') || normalized.includes('failed') || normalized.includes('cancel') || normalized.includes('error') || normalized === 'danger') {
    return 'danger'
  }
  if (normalized.includes('pending') || normalized.includes('due') || normalized.includes('warning') || normalized.includes('hold') || normalized.includes('review')) {
    return 'warning'
  }
  if (normalized.includes('sent') || normalized.includes('open') || normalized.includes('draft')) {
    return 'info'
  }
  return 'neutral'
}

export function StatusBadge({ status }: StatusBadgeProps) {
  const variant = resolveVariant(status)
  return (
    <span className={cn('inline-flex items-center rounded-full px-2.5 py-1 text-xs font-medium ring-1 ring-inset', variantClasses[variant])}>
      {formatStatus(status)}
    </span>
  )
}
