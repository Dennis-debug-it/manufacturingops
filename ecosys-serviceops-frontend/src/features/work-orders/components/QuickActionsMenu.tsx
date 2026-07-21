import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { CheckCircle2, Eye, MoreHorizontal, PlayCircle, UserPlus } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import { usePermission } from '../../../shared/hooks/usePermission'
import type { WorkOrderRow } from '../listModel'

export interface QuickActionsMenuProps {
  row: WorkOrderRow
  onAssign: (row: WorkOrderRow) => void
  onStart: (row: WorkOrderRow) => void
  onComplete: (row: WorkOrderRow) => void
  align?: 'left' | 'right'
}

export function QuickActionsMenu({ row, onAssign, onStart, onComplete, align = 'right' }: QuickActionsMenuProps) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement | null>(null)

  const canAssign = usePermission('work-orders.assign')
  const canStart = usePermission('work-orders.start')
  const canComplete = usePermission('work-orders.complete')

  useEffect(() => {
    if (!open) return
    const onClickOutside = (event: MouseEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onClickOutside)
    return () => document.removeEventListener('mousedown', onClickOutside)
  }, [open])

  const canStartNow = canStart && row.status !== 'In Progress' && row.status !== 'Completed' && row.status !== 'Closed' && !row.isUnassigned
  const canCompleteNow = canComplete && row.status === 'In Progress'

  return (
    <div className="relative inline-block" ref={ref}>
      <button
        type="button"
        onClick={(event) => {
          event.stopPropagation()
          setOpen((value) => !value)
        }}
        className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-slate-500 transition hover:bg-slate-100 hover:text-slate-900"
        aria-label="Row actions"
      >
        <MoreHorizontal className="h-4 w-4" />
      </button>

      {open ? (
        <div
          className={cn(
            'absolute z-30 mt-1 w-44 overflow-hidden rounded-lg border border-slate-200 bg-white py-1 text-sm shadow-lg',
            align === 'right' ? 'right-0' : 'left-0',
          )}
          onClick={(event) => event.stopPropagation()}
        >
          <Link to={`/work-orders/${row.id}`} className="flex items-center gap-2 px-3 py-2 text-slate-700 hover:bg-slate-50">
            <Eye className="h-3.5 w-3.5 text-slate-400" />
            View
          </Link>
          {canAssign ? (
            <button
              type="button"
              onClick={() => {
                setOpen(false)
                onAssign(row)
              }}
              className="flex w-full items-center gap-2 px-3 py-2 text-left text-slate-700 hover:bg-slate-50"
            >
              <UserPlus className="h-3.5 w-3.5 text-slate-400" />
              Assign
            </button>
          ) : null}
          {canStartNow ? (
            <button
              type="button"
              onClick={() => {
                setOpen(false)
                onStart(row)
              }}
              className="flex w-full items-center gap-2 px-3 py-2 text-left text-slate-700 hover:bg-slate-50"
            >
              <PlayCircle className="h-3.5 w-3.5 text-slate-400" />
              Start
            </button>
          ) : null}
          {canCompleteNow ? (
            <button
              type="button"
              onClick={() => {
                setOpen(false)
                onComplete(row)
              }}
              className="flex w-full items-center gap-2 px-3 py-2 text-left text-slate-700 hover:bg-slate-50"
            >
              <CheckCircle2 className="h-3.5 w-3.5 text-slate-400" />
              Complete
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
