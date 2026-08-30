import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { CheckCircle2, Eye, MoreHorizontal, PlayCircle, UserPlus } from 'lucide-react'
import { cn } from '../../../shared/utils/cn'
import { useAuth } from '../../../shared/hooks/useAuth'
import { usePermission } from '../../../shared/hooks/usePermission'
import { COMPLETE_BLOCKED_REASON, START_BLOCKED_REASON } from '../dispatchGuards'
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

  const { user } = useAuth()
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
  // The server guards on top of the state machine: Start needs an accepted technician assignment,
  // Complete needs a recorded arrival unless a tenant admin overrides it in the completion dialog.
  const startBlockedReason = row.hasAcceptedTechnician ? null : START_BLOCKED_REASON
  const completeBlockedReason = row.hasRecordedArrival || user?.role === 'admin' ? null : COMPLETE_BLOCKED_REASON

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
            <GuardedAction
              icon={PlayCircle}
              label="Start"
              blockedReason={startBlockedReason}
              onSelect={() => {
                setOpen(false)
                onStart(row)
              }}
            />
          ) : null}
          {canCompleteNow ? (
            <GuardedAction
              icon={CheckCircle2}
              label="Complete"
              blockedReason={completeBlockedReason}
              onSelect={() => {
                setOpen(false)
                onComplete(row)
              }}
            />
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

/**
 * A menu action that the server would reject stays visible but disabled with the reason underneath,
 * so the operator learns what is missing instead of watching the request come back 400.
 */
function GuardedAction({
  icon: Icon,
  label,
  blockedReason,
  onSelect,
}: {
  icon: typeof PlayCircle
  label: string
  blockedReason: string | null
  onSelect: () => void
}) {
  return (
    <button
      type="button"
      disabled={blockedReason !== null}
      title={blockedReason ?? undefined}
      onClick={onSelect}
      className="flex w-full items-start gap-2 px-3 py-2 text-left text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:bg-transparent disabled:text-slate-400"
    >
      <Icon className="mt-0.5 h-3.5 w-3.5 shrink-0 text-slate-400" />
      <span className="min-w-0">
        {label}
        {blockedReason ? <span className="mt-0.5 block text-[11px] leading-4 text-slate-400">{blockedReason}</span> : null}
      </span>
    </button>
  )
}
