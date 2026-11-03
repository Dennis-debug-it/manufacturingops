import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useWorkOrderChildren } from '../api'
import type { WorkOrder } from '../types'
import { NewWorkOrderForm } from './NewWorkOrderForm'

const panelClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm'
const headingClass = 'text-xs font-semibold uppercase tracking-[0.18em] text-slate-500'

export function SubtasksPanel({ workOrder }: { workOrder: WorkOrder }) {
  const canCreate = usePermission('work-orders.create')
  const { data: children } = useWorkOrderChildren(workOrder.id)
  const [showAddForm, setShowAddForm] = useState(false)
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

  if (subtasks.length === 0 && !canCreate) {
    return null
  }

  return (
    <div className={panelClass}>
      <div className="flex items-center justify-between gap-2">
        <h3 className={headingClass}>Sub-tasks</h3>
        {canCreate ? (
          <button type="button" onClick={() => setShowAddForm(true)} className="inline-flex items-center gap-1 text-xs font-medium text-slate-700 hover:text-slate-900">
            <Plus className="h-3.5 w-3.5" />Add
          </button>
        ) : null}
      </div>
      {subtasks.length === 0 ? (
        <p className="mt-2 text-sm text-slate-500">None. The parent completes automatically once every sub-task is done.</p>
      ) : (
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
      )}
      <NewWorkOrderForm open={showAddForm} onClose={() => setShowAddForm(false)} parentWorkOrderId={workOrder.id} />
    </div>
  )
}
