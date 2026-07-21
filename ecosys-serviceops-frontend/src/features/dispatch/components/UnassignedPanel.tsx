import { useDroppable } from '@dnd-kit/core'
import { cn } from '../../../shared/utils/cn'
import type { BoardWorkOrder } from '../boardApi'
import { WorkOrderCard } from './WorkOrderCard'

export interface UnassignedPanelProps {
  workOrders: BoardWorkOrder[]
  isDraggingAssigned: boolean
}

export function UnassignedPanel({ workOrders, isDraggingAssigned }: UnassignedPanelProps) {
  const { setNodeRef, isOver } = useDroppable({ id: 'unassigned-panel' })

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
      {isDraggingAssigned ? (
        <p className="mb-2 rounded-lg bg-slate-50 px-2 py-1.5 text-[11px] text-slate-500">Drop here to unassign</p>
      ) : null}
      <div className="flex-1 space-y-2 overflow-y-auto pr-1">
        {workOrders.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-200 p-4 text-center text-xs text-slate-400">
            No unassigned work orders
          </div>
        ) : (
          workOrders.map((workOrder) => (
            <WorkOrderCard
              key={workOrder.id}
              workOrder={workOrder}
              draggableId={`unassigned:${workOrder.id}`}
              sourceTechnicianId={null}
              variant="panel"
            />
          ))
        )}
      </div>
    </div>
  )
}
