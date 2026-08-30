import { useDroppable } from '@dnd-kit/core'
import { cn } from '../../../shared/utils/cn'
import type { BoardTechnician, BoardWorkOrder } from '../boardApi'
import { dateToSlotOffset, ROW_HEIGHT, SLOT_COUNT, SLOT_WIDTH } from '../gridUtils'
import { TechnicianAvatar, technicianStatusLabel } from './TechnicianAvatar'
import { WorkOrderCard } from './WorkOrderCard'

export interface DragPreview {
  technicianId: string
  slotIndex: number
  slotSpan: number
  valid: boolean
}

export interface TechnicianRowProps {
  technician: BoardTechnician
  day: Date
  preview: DragPreview | null
  onOpenDispatch: (workOrder: BoardWorkOrder) => void
}

export function TechnicianRow({ technician, day, preview, onOpenDispatch }: TechnicianRowProps) {
  const { setNodeRef, isOver } = useDroppable({ id: `row:${technician.id}`, data: { technicianId: technician.id } })
  const showPreview = preview && preview.technicianId === technician.id

  return (
    <div className="flex border-b border-slate-100 last:border-b-0">
      <div className="sticky left-0 z-10 flex w-56 shrink-0 items-center gap-2 border-r border-slate-200 bg-white px-3" style={{ height: ROW_HEIGHT }}>
        <TechnicianAvatar fullName={technician.fullName} status={technician.status} />
        <div className="min-w-0">
          <div className="truncate text-sm font-medium text-slate-900">{technician.fullName}</div>
          <div className="text-[11px] text-slate-500">{technicianStatusLabel[technician.status]}</div>
        </div>
      </div>
      <div
        ref={setNodeRef}
        className={cn('relative shrink-0', isOver && 'bg-slate-50')}
        style={{ width: SLOT_COUNT * SLOT_WIDTH, height: ROW_HEIGHT }}
      >
        {Array.from({ length: SLOT_COUNT }).map((_, index) => (
          <div
            key={index}
            className={cn('absolute inset-y-0 border-r border-slate-100', index % 2 === 1 && 'bg-slate-50/40')}
            style={{ left: index * SLOT_WIDTH, width: SLOT_WIDTH }}
          />
        ))}
        {technician.workOrders.map((workOrder) => {
          if (!workOrder.scheduledStart) return null
          const offset = dateToSlotOffset(day, new Date(workOrder.scheduledStart))
          const span = Math.max(workOrder.estimatedDurationMins / 30, 0.75)
          return (
            <WorkOrderCard
              key={workOrder.id}
              workOrder={workOrder}
              draggableId={`assigned:${workOrder.id}`}
              sourceTechnicianId={technician.id}
              variant="block"
              style={{ left: offset * SLOT_WIDTH + 2, width: span * SLOT_WIDTH - 4 }}
              onOpenDispatch={onOpenDispatch}
            />
          )
        })}
        {showPreview ? (
          <div
            className={cn(
              'pointer-events-none absolute top-1 h-[calc(100%-0.5rem)] rounded-md border-2',
              preview.valid ? 'border-emerald-400 bg-emerald-100/60' : 'border-rose-400 bg-rose-100/60',
            )}
            style={{ left: preview.slotIndex * SLOT_WIDTH + 2, width: preview.slotSpan * SLOT_WIDTH - 4 }}
          />
        ) : null}
      </div>
    </div>
  )
}
