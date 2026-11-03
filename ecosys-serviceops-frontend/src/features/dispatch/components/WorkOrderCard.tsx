import { useState, type CSSProperties } from 'react'
import { useDraggable } from '@dnd-kit/core'
import { ClipboardList, MapPin, Radio, User } from 'lucide-react'
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
  /**
   * Opens the dispatch drawer for this work order. Supplied only for cards that sit on a
   * technician's row — an unassigned card has no technician assignment to accept or arrive against.
   */
  onOpenDispatch?: (workOrder: BoardWorkOrder) => void
}

export function WorkOrderCard({ workOrder, draggableId, sourceTechnicianId, variant, style, className, onOpenDispatch }: WorkOrderCardProps) {
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
          // perf: many of these render at once on the board — plain alpha, no backdrop-filter.
          'group absolute top-1 flex h-[calc(100%-0.5rem)] cursor-grab items-center gap-1.5 overflow-hidden rounded-md border-l-4 bg-white/85 px-2 text-left shadow-sm ring-1 ring-[var(--app-border)] transition hover:shadow-md active:cursor-grabbing',
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
        {onOpenDispatch ? <DispatchButton compact onOpen={() => onOpenDispatch(workOrder)} /> : null}
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
        // perf: many of these render at once on the board — plain alpha, no backdrop-filter.
        'group relative cursor-grab rounded-xl border border-[var(--app-border)] bg-white/85 p-3 shadow-sm transition hover:shadow-md active:cursor-grabbing',
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
      {onOpenDispatch ? (
        <div className="mt-2 flex justify-end">
          <DispatchButton compact={false} onOpen={() => onOpenDispatch(workOrder)} />
        </div>
      ) : null}
      {hovered ? <WorkOrderPopover workOrder={workOrder} /> : null}
    </div>
  )
}

/**
 * The drag listeners live on the card itself, so the button stops the pointer event before dnd-kit's
 * sensor sees it — otherwise pressing it would begin a drag instead of opening the drawer.
 */
function DispatchButton({ compact, onOpen }: { compact: boolean; onOpen: () => void }) {
  return (
    <button
      type="button"
      aria-label="Open dispatch actions"
      title="Dispatch: accept, en route, arrival, departure"
      onPointerDown={(event) => event.stopPropagation()}
      onClick={(event) => {
        event.stopPropagation()
        onOpen()
      }}
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-md border border-[var(--app-border)] bg-white text-slate-600 transition hover:bg-[var(--app-primary-soft)] hover:text-slate-900',
        compact ? 'h-5 w-5' : 'h-7 gap-1 px-2 text-[11px] font-medium',
      )}
    >
      <Radio className="h-3 w-3" />
      {compact ? null : 'Dispatch'}
    </button>
  )
}

function WorkOrderPopover({ workOrder }: { workOrder: BoardWorkOrder }) {
  return (
    <div className="pointer-events-none absolute left-0 top-full z-50 mt-2 w-64 rounded-xl border border-[var(--glass-border)] bg-[var(--glass-white-strong)] p-3 text-xs shadow-xl backdrop-blur-[var(--glass-blur-md)]">
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
