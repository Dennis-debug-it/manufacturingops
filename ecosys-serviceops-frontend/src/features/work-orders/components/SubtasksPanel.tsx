import { Link } from 'react-router-dom'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { useWorkOrderChildren } from '../api'
import type { WorkOrder } from '../types'

const panelClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm'
const headingClass = 'text-xs font-semibold uppercase tracking-[0.18em] text-slate-500'

export function SubtasksPanel({ workOrder }: { workOrder: WorkOrder }) {
  const { data: children } = useWorkOrderChildren(workOrder.id)
  const subtasks = children ?? []

  if (workOrder.parentWorkOrderId) {
    return (
      <div className={panelClass}>
        <h3 className={headingClass}>Sub-task of</h3>
        <Link to={`/work-orders/${workOrder.parentWorkOrderId}`} className="mt-2 inline-block text-sm font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
          Open parent work order
        </Link>
      </div>
    )
  }

  if (subtasks.length === 0) {
    return null
  }

  return (
    <div className={panelClass}>
      <h3 className={headingClass}>Sub-tasks</h3>
      <ul className="mt-2 space-y-1.5">
          {subtasks.map((child) => (
            <li key={child.id}>
              <Link to={`/work-orders/${child.id}`} className="flex items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-slate-50">
                <span className="min-w-0 truncate"><span className="font-mono text-xs text-slate-500">{child.workOrderNumber}</span> {child.title}</span>
                <StatusBadge status={child.status} />
              </Link>
            </li>
          ))}
      </ul>
    </div>
  )
}
