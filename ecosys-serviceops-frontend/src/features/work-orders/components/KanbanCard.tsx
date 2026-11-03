import { useDraggable } from '@dnd-kit/core'
import { Link } from 'react-router-dom'
import { cn } from '../../../shared/utils/cn'
import { SlaBadge } from '../../../shared/components/SlaBadge'
import type { WorkOrderRow } from '../listModel'
import { QuickActionsMenu } from './QuickActionsMenu'

const priorityDotClass: Record<string, string> = {
  Critical: 'bg-rose-500',
  High: 'bg-amber-500',
  Medium: 'bg-sky-500',
  Low: 'bg-slate-400',
}

function initials(name: string) {
  if (!name || name === '—') return '?'
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase())
    .join('')
}

export interface KanbanCardProps {
  row: WorkOrderRow
  onAssign: (row: WorkOrderRow) => void
  onStart: (row: WorkOrderRow) => void
  onComplete: (row: WorkOrderRow) => void
}

export function KanbanCard({ row, onAssign, onStart, onComplete }: KanbanCardProps) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({ id: row.id, data: { row } })

  const style = transform ? { transform: `translate3d(${transform.x}px, ${transform.y}px, 0)`, zIndex: 30 } : undefined

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      style={style}
      className={cn(
        'cursor-grab rounded-xl border border-slate-200 bg-white p-3 shadow-sm transition hover:shadow-md active:cursor-grabbing',
        isDragging && 'opacity-40',
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="flex min-w-0 items-center gap-1.5">
          <span className={cn('h-2 w-2 shrink-0 rounded-full', priorityDotClass[row.priority] ?? 'bg-slate-400')} title={row.priority} />
          <Link
            to={`/work-orders/${row.id}`}
            onPointerDown={(event) => event.stopPropagation()}
            className="truncate text-sm font-semibold text-slate-900 hover:underline"
          >
            {row.woNo}
          </Link>
        </div>
        <div onPointerDown={(event) => event.stopPropagation()}>
          <QuickActionsMenu row={row} onAssign={onAssign} onStart={onStart} onComplete={onComplete} />
        </div>
      </div>
      <div className="mt-1 truncate text-xs text-slate-600">{row.client}</div>
      <div className="truncate text-[11px] text-slate-400">{row.site}</div>
      <div className="mt-2.5 flex items-center justify-between gap-2">
        <span
          className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-slate-100 text-[10px] font-semibold text-slate-600"
          title={row.assignedTo === '—' ? 'Unassigned' : row.assignedTo}
        >
          {initials(row.assignedTo)}
        </span>
        <SlaBadge deadline={row.slaResolutionDeadline} breached={row.slaResponseBreached || row.slaResolutionBreached} />
      </div>
    </div>
  )
}
