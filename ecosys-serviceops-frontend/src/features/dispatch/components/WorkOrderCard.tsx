import { useState, type CSSProperties } from 'react'
import { useDraggable } from '@dnd-kit/core'
import { ClipboardList, MapPin, User } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import { formatDuration } from '../../../shared/utils/dates'
import type { BoardWorkOrder } from '../boardApi'
import { PriorityBadge, priorityDotClass } from './PriorityBadge'

export interface WorkOrderCardProps {
  workOrder: BoardWorkOrder
  draggableId: string
  sourceTechnicianId: string | null
  variant: 'panel' | 'block'
  style?: CSSProperties
  className?: string
}

export function WorkOrderCard({ workOrder, draggableId, sourceTechnicianId, variant, style, className }: WorkOrderCardProps) {
  const [hovered, setHovered] = useState(false)
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: draggableId,
    data: { workOrder, sourceTechnicianId },
  })

  const dragStyle: CSSProperties = transform
    ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`, zIndex: 40 }
    : {}

  if (variant === 'block') {
    return (
      <div
        ref={setNodeRef}
        {...listeners}
        {...attributes}
        style={{ ...style, ...dragStyle }}
        className={cn(
          'group absolute top-1 flex h-[calc(100%-0.5rem)] cursor-grab items-center gap-1.5 overflow-hidden rounded-md border-l-4 bg-white px-2 text-left shadow-sm ring-1 ring-slate-200 transition hover:shadow-md active:cursor-grabbing',
          isDragging && 'opacity-40',
          className,
        )}
        onMouseEnter={() => setHovered(true)}
        onMouseLeave={() => setHovered(false)}
      >
        <span className={cn('h-2 w-2 shrink-0 rounded-full', priorityDotClass(workOrder.priority))} />
        <div className="min-w-0 flex-1">
          <div className="truncate text-[11px] font-semibold text-slate-900">{workOrder.woNumber}</div>
          <div className="truncate text-[10px] text-slate-500">{workOrder.clientName}</div>
        </div>
        {hovered ? <WorkOrderPopover workOrder={workOrder} /> : null}
      </div>
    )
  }

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={dragStyle}
      className={cn(
        'group relative cursor-grab rounded-xl border border-slate-200 bg-white p-3 shadow-sm transition hover:shadow-md active:cursor-grabbing',
        isDragging && 'opacity-40',
        className,
      )}
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="text-sm font-semibold text-slate-900">{workOrder.woNumber}</span>
        <PriorityBadge priority={workOrder.priority} />
      </div>
      <div className="mt-1 truncate text-xs text-slate-600">{workOrder.clientName}</div>
      <div className="mt-2 flex items-center justify-between text-[11px] text-slate-500">
        <span className="truncate">{workOrder.siteAddress}</span>
        <span className="shrink-0 font-medium text-slate-700">{formatDuration(workOrder.estimatedDurationMins)}</span>
      </div>
      {hovered ? <WorkOrderPopover workOrder={workOrder} /> : null}
    </div>
  )
}

function WorkOrderPopover({ workOrder }: { workOrder: BoardWorkOrder }) {
  return (
    <div className="pointer-events-none absolute left-0 top-full z-50 mt-2 w-64 rounded-xl border border-slate-200 bg-white p-3 text-xs shadow-xl">
      <div className="flex items-center justify-between gap-2">
        <span className="font-semibold text-slate-900">{workOrder.woNumber}</span>
        <PriorityBadge priority={workOrder.priority} />
      </div>
      <div className="mt-2 space-y-1.5 text-slate-600">
        <div className="flex items-start gap-1.5">
          <User className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
          <span>{workOrder.clientName}</span>
        </div>
        <div className="flex items-start gap-1.5">
          <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
          <span>{workOrder.siteAddress}</span>
        </div>
        <div className="flex items-start gap-1.5">
          <ClipboardList className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
          <span>{workOrder.checklistCount} checklist item{workOrder.checklistCount === 1 ? '' : 's'}</span>
        </div>
        {workOrder.description ? <p className="line-clamp-3 text-slate-500">{workOrder.description}</p> : null}
      </div>
      <div className="mt-2 flex items-center justify-between border-t border-slate-100 pt-2 text-[11px] text-slate-500">
        <span>Status: {workOrder.status}</span>
        <span>{formatDuration(workOrder.estimatedDurationMins)}</span>
      </div>
    </div>
  )
}
