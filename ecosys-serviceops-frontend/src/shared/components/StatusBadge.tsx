import { Badge, type BadgeTone } from './Badge'
import { formatStatus } from '../utils/format'

export interface StatusBadgeProps {
  status: string
}

function resolveVariant(status: string): BadgeTone {
  const normalized = status.toLowerCase()
  if (normalized === 'out of support') return 'danger'
  if (normalized === 'under contract') return 'success'
  if (normalized === 'retired') return 'neutral'
  if (normalized === 'negative') return 'danger'
  if (normalized === 'low stock') return 'warning'
  if (normalized === 'reconciled') return 'success'
  if (normalized === 'contractlapsed' || normalized === 'contract lapsed') return 'warning'
  if (normalized.includes('warranty')) {
    if (normalized.includes('expired')) return 'danger'
    if (normalized.includes('expiring')) return 'warning'
    if (normalized.startsWith('under')) return 'success'
    return 'neutral'
  }
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
  return <Badge tone={resolveVariant(status)}>{formatStatus(status)}</Badge>
}
