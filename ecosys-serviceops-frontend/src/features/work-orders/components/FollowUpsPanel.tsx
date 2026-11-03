import { Link } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatDate } from '../../../shared/utils/dates'
import type { WorkOrder } from '../types'

const panelClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm'
const headingClass = 'text-xs font-semibold uppercase tracking-[0.18em] text-slate-500'

/** Return visits: the job this one follows up on, and follow-ups raised from it. */
export function FollowUpsPanel({ workOrder, onCreate }: { workOrder: WorkOrder; onCreate: (() => void) | null }) {
  const followUps = workOrder.followUps ?? []
  if (!workOrder.followUpOfWorkOrderId && followUps.length === 0 && !onCreate) {
    return null
  }

  return (
    <div className={panelClass} data-testid="follow-ups-panel">
      {workOrder.followUpOfWorkOrderId ? (
        <div className={followUps.length > 0 || onCreate ? 'mb-4' : undefined}>
          <h3 className={headingClass}>Follow-up of</h3>
          <Link to={`/work-orders/${workOrder.followUpOfWorkOrderId}`} className="mt-2 inline-block text-sm font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
            {workOrder.followUpOfWorkOrderNumber ?? 'Original work order'}
          </Link>
        </div>
      ) : null}
      {followUps.length > 0 || onCreate ? (
        <>
          <div className="flex items-center justify-between gap-2">
            <h3 className={headingClass}>Follow-ups</h3>
            {onCreate ? (
              <button type="button" onClick={onCreate} className="inline-flex items-center gap-1 text-xs font-medium text-slate-700 hover:text-slate-900">
                <Plus className="h-3.5 w-3.5" />Create
              </button>
            ) : null}
          </div>
          {followUps.length === 0 ? (
            <p className="mt-2 text-sm text-slate-500">None. Create one if another visit is needed.</p>
          ) : (
            <ul className="mt-2 space-y-2">
              {followUps.map((item) => (
                <li key={item.id} className="flex items-center justify-between gap-2">
                  <Link to={`/work-orders/${item.id}`} className="text-sm font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
                    {item.workOrderNumber}
                  </Link>
                  <span className="flex items-center gap-2 text-xs text-slate-500">
                    {formatDate(item.createdAt)}
                    <StatusBadge status={item.status} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </>
      ) : null}
    </div>
  )
}
