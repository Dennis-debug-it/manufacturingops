import { Link } from 'react-router-dom'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatDate } from '../../../shared/utils/dates'
import { useRelatedWorkOrders } from '../api'
import type { RelatedWorkOrder } from '../types'

function RelatedList({ items }: { items: RelatedWorkOrder[] }) {
  if (items.length === 0) {
    return <p className="mt-2 text-xs text-slate-500">None found.</p>
  }
  return (
    <ul className="mt-2 space-y-2">
      {items.map((item) => (
        <li key={item.id} className="rounded-xl border border-slate-200 p-3">
          <div className="flex flex-wrap items-center justify-between gap-1.5">
            <Link to={`/work-orders/${item.id}`} className="text-sm font-medium text-slate-900 underline decoration-slate-300 underline-offset-4 hover:decoration-slate-900">
              {item.workOrderNumber}
            </Link>
            <StatusBadge status={item.status} />
          </div>
          <p className="mt-1 truncate text-xs text-slate-600">{item.description}</p>
          <p className="mt-1 text-xs text-slate-500">
            {item.completedAt ? formatDate(item.completedAt) : 'Not completed'}
            {item.technicianName ? ` · ${item.technicianName}` : ''}
          </p>
        </li>
      ))}
    </ul>
  )
}

export function RelatedWorkOrdersPanel({ workOrderId }: { workOrderId: string }) {
  const { data, isLoading } = useRelatedWorkOrders(workOrderId)

  if (isLoading) {
    return <Spinner label="Loading related work orders" />
  }

  const sameSite = data?.sameSite ?? []
  const sameAsset = data?.sameAsset ?? []

  if (sameSite.length === 0 && sameAsset.length === 0) {
    return null
  }

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h3 className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Related Work Orders</h3>
        <Link to="/work-orders" className="text-xs font-medium text-slate-500 hover:text-slate-900">View all</Link>
      </div>

      <div className="mt-3">
        <h4 className="text-xs font-medium text-slate-700">Same Site</h4>
        <RelatedList items={sameSite} />
      </div>

      <div className="mt-4">
        <h4 className="text-xs font-medium text-slate-700">Same Asset</h4>
        <RelatedList items={sameAsset} />
      </div>
    </div>
  )
}
