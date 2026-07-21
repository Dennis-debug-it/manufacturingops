import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { useWorkOrderChildren } from '../api'
import { NewWorkOrderForm } from '../components/NewWorkOrderForm'
import type { WorkOrder } from '../types'

export function SubtasksTab({ workOrder }: { workOrder: WorkOrder }) {
  const canCreate = usePermission('work-orders.create')
  const { data: children, isLoading } = useWorkOrderChildren(workOrder.id)
  const [showAddForm, setShowAddForm] = useState(false)

  if (workOrder.parentWorkOrderId) {
    return (
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <p className="text-sm text-slate-600">
          This work order is a sub-task of{' '}
          <Link to={`/work-orders/${workOrder.parentWorkOrderId}`} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
            another work order
          </Link>
          .
        </p>
      </div>
    )
  }

  if (isLoading) {
    return <Spinner label="Loading sub-tasks" />
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-slate-600">Break this job into smaller trackable sub-tasks. The parent work order auto-completes once every sub-task is done.</p>
        {canCreate ? (
          <button
            type="button"
            onClick={() => setShowAddForm(true)}
            className="inline-flex h-10 shrink-0 items-center gap-2 rounded-lg bg-slate-900 px-3 text-sm font-medium text-white transition hover:bg-slate-800"
          >
            <Plus className="h-4 w-4" />Add Sub-task
          </button>
        ) : null}
      </div>

      {(children ?? []).length === 0 ? (
        <EmptyState title="No sub-tasks yet" description="Add a sub-task to break this job into smaller pieces of work." />
      ) : (
        <div className="space-y-2">
          {(children ?? []).map((child) => (
            <Link
              key={child.id}
              to={`/work-orders/${child.id}`}
              className="flex items-center justify-between rounded-xl border border-slate-200 bg-white p-3 transition hover:border-slate-900 hover:shadow-sm"
            >
              <div>
                <p className="font-mono text-sm text-slate-900">{child.workOrderNumber}</p>
                <p className="text-sm text-slate-700">{child.title}</p>
                {child.dueDate ? <p className="mt-1 text-xs text-slate-500">Due {formatDate(child.dueDate)}</p> : null}
              </div>
              <StatusBadge status={child.status} />
            </Link>
          ))}
        </div>
      )}

      <NewWorkOrderForm open={showAddForm} onClose={() => setShowAddForm(false)} parentWorkOrderId={workOrder.id} />
    </div>
  )
}
