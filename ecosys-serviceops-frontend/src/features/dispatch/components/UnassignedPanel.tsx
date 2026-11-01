import { useEffect, useMemo, useState } from 'react'
import { useDroppable } from '@dnd-kit/core'
import { Search } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import type { BoardWorkOrder } from '../boardApi'
import { WorkOrderCard } from './WorkOrderCard'

export interface UnassignedPanelProps {
  workOrders: BoardWorkOrder[]
  isDraggingAssigned: boolean
}

// Every open, unassigned work order comes back at once. Rendering them all as cards made the panel
// (and the page) run on for screens; show a page at a time and let the dispatcher search the rest.
const PAGE_SIZE = 20

export function UnassignedPanel({ workOrders, isDraggingAssigned }: UnassignedPanelProps) {
  const { setNodeRef, isOver } = useDroppable({ id: 'unassigned-panel' })
  const [search, setSearch] = useState('')
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE)

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    if (!term) return workOrders
    return workOrders.filter((wo) =>
      [wo.woNumber, wo.clientName, wo.siteAddress, wo.description ?? ''].some((value) => value.toLowerCase().includes(term)),
    )
  }, [search, workOrders])

  useEffect(() => {
    setVisibleCount(PAGE_SIZE)
  }, [search])

  const visible = filtered.slice(0, visibleCount)
  const remaining = filtered.length - visible.length

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'flex h-full w-full flex-col rounded-2xl border bg-white p-3 shadow-sm transition',
        isDraggingAssigned && isOver ? 'border-rose-300 bg-rose-50/60' : 'border-slate-200',
      )}
    >
      <div className="mb-2 flex items-center justify-between px-1">
        <h2 className="text-sm font-semibold text-slate-900">Unassigned</h2>
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-medium text-slate-600">{workOrders.length}</span>
      </div>
      {workOrders.length > PAGE_SIZE ? (
        <label className="mb-2 flex items-center gap-2 rounded-lg border border-slate-200 px-2">
          <Search className="h-3.5 w-3.5 shrink-0 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search WO, client or site"
            aria-label="Search unassigned work orders"
            className="h-8 w-full border-0 bg-transparent p-0 text-xs outline-none placeholder:text-slate-400"
          />
        </label>
      ) : null}
      {isDraggingAssigned ? (
        <p className="mb-2 rounded-lg bg-slate-50 px-2 py-1.5 text-[11px] text-slate-500">Drop here to unassign</p>
      ) : null}
      <div className="min-h-0 flex-1 space-y-2 overflow-y-auto pr-1">
        {filtered.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-200 p-4 text-center text-xs text-slate-400">
            {workOrders.length === 0 ? 'No unassigned work orders' : 'No unassigned work orders match your search'}
          </div>
        ) : (
          visible.map((workOrder) => (
            <WorkOrderCard
              key={workOrder.id}
              workOrder={workOrder}
              draggableId={`unassigned:${workOrder.id}`}
              sourceTechnicianId={null}
              variant="panel"
            />
          ))
        )}
        {remaining > 0 ? (
          <button
            type="button"
            onClick={() => setVisibleCount((count) => count + PAGE_SIZE)}
            className="w-full rounded-lg border border-slate-200 bg-white py-2 text-xs font-medium text-slate-700 hover:bg-slate-50"
          >
            Show {Math.min(PAGE_SIZE, remaining)} more ({remaining} not shown)
          </button>
        ) : null}
      </div>
    </div>
  )
}
