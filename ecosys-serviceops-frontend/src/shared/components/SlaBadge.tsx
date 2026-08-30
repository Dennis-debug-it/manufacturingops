import { Badge } from './Badge'
import { differenceInMinutes, formatDuration } from '../utils/dates'

export interface SlaBadgeProps {
  deadline: string | null | undefined
  breached: boolean
  paused?: boolean
}

export function SlaBadge({ deadline, breached, paused }: SlaBadgeProps) {
  if (paused) {
    return <Badge tone="neutral">Paused</Badge>
  }

  if (!deadline) {
    return <Badge tone="neutral">No SLA</Badge>
  }

  if (breached) {
    return <Badge tone="danger">Breached</Badge>
  }

  const minutesRemaining = differenceInMinutes(deadline, new Date())
  const tone = minutesRemaining <= 120 ? 'warning' : 'success'
  const label = minutesRemaining <= 0 ? 'Overdue' : `${formatDuration(minutesRemaining)} left`

  return <Badge tone={tone}>{label}</Badge>
}
