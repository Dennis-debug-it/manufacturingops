import { Badge, type BadgeTone } from '../../../shared/components/Badge'

const TONES: Record<string, BadgeTone> = {
  Requested: 'warning',
  Approved: 'info',
  Planned: 'info',
  Assigned: 'info',
  'In Progress': 'info',
  'On Hold': 'warning',
  'Waiting on Parts': 'warning',
  Completed: 'success',
  Verified: 'success',
  Closed: 'neutral',
  Cancelled: 'neutral',
  Rejected: 'neutral',
  Reopened: 'warning',
}

export function JobStatusBadge({ status, holdReason }: { status: string; holdReason?: string | null | undefined }) {
  return (
    <span className="inline-flex flex-col items-start gap-0.5">
      <Badge tone={TONES[status] ?? 'neutral'} size="sm">{status}</Badge>
      {holdReason && (status === 'On Hold' || status === 'Waiting on Parts') ? <span className="text-xs text-slate-500">{holdReason}</span> : null}
    </span>
  )
}
