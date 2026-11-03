import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import {
  DndContext,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import { cn } from '../../../shared/utils/cn'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAuth } from '../../../shared/hooks/useAuth'
import { COMPLETE_BLOCKED_REASON } from '../dispatchGuards'
import { KANBAN_STATUSES, type WorkOrderRow } from '../listModel'
import {
  useAssignWorkOrderById,
  useCompleteWorkOrderById,
  useStartWorkOrderById,
  usePatchWorkOrderStatus,
  useCloseWorkOrderById,
} from '../api'
import { KanbanCard } from './KanbanCard'
import { AssignTechnicianDialog } from './AssignTechnicianDialog'
import { CompleteWorkOrderDialog } from './CompleteWorkOrderDialog'

const DIRECT_PATCH_STATUSES = new Set(['New', 'Assigned', 'In Progress', 'On Hold'])

function KanbanColumn({ status, rows, children }: { status: string; rows: WorkOrderRow[]; children: React.ReactNode }) {
  const { setNodeRef, isOver } = useDroppable({ id: `status:${status}` })
  const totalHours = rows.reduce((sum, row) => sum + row.estimatedHours, 0)

  return (
    <div
      ref={setNodeRef}
      className={cn(
        'flex w-72 shrink-0 flex-col rounded-2xl border bg-slate-50/60 transition',
        isOver ? 'border-[var(--app-primary)] bg-slate-100' : 'border-slate-200',
      )}
    >
      <div className="flex items-center justify-between gap-2 border-b border-slate-200 px-3 py-2.5">
        <h3 className="text-sm font-semibold text-slate-900">{status}</h3>
        <div className="flex items-center gap-1.5 text-[11px] font-medium text-slate-500">
          <span className="rounded-full bg-white px-2 py-0.5 ring-1 ring-inset ring-slate-200">{rows.length}</span>
          <span>{totalHours.toFixed(1)}h</span>
        </div>
      </div>
      <div className="flex-1 space-y-2 overflow-y-auto p-2" style={{ maxHeight: 'calc(100vh - 360px)', minHeight: 160 }}>
        {rows.length === 0 ? (
          <div className="rounded-xl border border-dashed border-slate-200 p-4 text-center text-xs text-slate-400">No work orders</div>
        ) : (
          children
        )}
      </div>
    </div>
  )
}

export interface KanbanViewProps {
  rows: WorkOrderRow[]
}

export function KanbanView({ rows }: KanbanViewProps) {
  const { user } = useAuth()
  const isTenantAdmin = user?.role === 'admin'
  const queryClient = useQueryClient()
  const patchStatus = usePatchWorkOrderStatus()
  const assignMutation = useAssignWorkOrderById()
  const startMutation = useStartWorkOrderById()
  const completeMutation = useCompleteWorkOrderById()
  const closeMutation = useCloseWorkOrderById()

  const [assignTarget, setAssignTarget] = useState<WorkOrderRow | null>(null)
  const [completeTarget, setCompleteTarget] = useState<WorkOrderRow | null>(null)

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }))

  const columns = KANBAN_STATUSES.map((status) => ({
    status,
    rows: rows.filter((row) => row.status === status),
  }))

  const handleDragEnd = (event: DragEndEvent) => {
    const overId = event.over?.id
    if (typeof overId !== 'string' || !overId.startsWith('status:')) {
      return
    }
    const targetStatus = overId.slice('status:'.length)
    const row = event.active.data.current?.row as WorkOrderRow | undefined
    if (!row || row.status === targetStatus) {
      return
    }

    if (targetStatus === 'Completed') {
      // Dropping into Completed is the same call as the Complete action, so it answers to the same
      // arrival guard: without an arrival only a tenant admin can proceed, via the override in the
      // dialog.
      if (!row.hasRecordedArrival && !isTenantAdmin) {
        toast.error(COMPLETE_BLOCKED_REASON)
        return
      }
      setCompleteTarget(row)
      return
    }

    // Note: dropping into "In Progress" goes through PATCH /status -> ChangeStatusAsync, which is a
    // plain state-machine move and does not run the accepted-technician guard that POST /start does.
    // It is deliberately not gated here.

    if (targetStatus === 'Closed') {
      closeMutation.mutate(row.id, {
        onSuccess: () => toast.success(`${row.woNo} closed`),
        onError: () => void queryClient.invalidateQueries({ queryKey: ['work-orders'] }),
      })
      return
    }

    if (DIRECT_PATCH_STATUSES.has(targetStatus)) {
      patchStatus.mutate(
        { id: row.id, status: targetStatus },
        {
          onSuccess: () => toast.success(`${row.woNo} moved to ${targetStatus}`),
          onError: () => void queryClient.invalidateQueries({ queryKey: ['work-orders'] }),
        },
      )
    }
  }

  if (rows.length === 0) {
    return <EmptyState title="No work orders" description="Once work orders match the current filters, they'll appear on the board." />
  }

  return (
    <>
      <DndContext sensors={sensors} onDragEnd={handleDragEnd}>
        <div className="flex gap-3 overflow-x-auto pb-2">
          {columns.map(({ status, rows: columnRows }) => (
            <KanbanColumn key={status} status={status} rows={columnRows}>
              {columnRows.map((row) => (
                <KanbanCard
                  key={row.id}
                  row={row}
                  onAssign={setAssignTarget}
                  onStart={(target) =>
                    startMutation.mutate(target.id, {
                      onSuccess: () => toast.success(`${target.woNo} started`),
                    })
                  }
                  onComplete={setCompleteTarget}
                />
              ))}
            </KanbanColumn>
          ))}
        </div>
      </DndContext>

      <AssignTechnicianDialog
        open={Boolean(assignTarget)}
        title={assignTarget ? `Assign ${assignTarget.woNo}` : 'Assign technician'}
        submitting={assignMutation.isPending}
        onClose={() => setAssignTarget(null)}
        onSubmit={(technicianId, notes) => {
          if (!assignTarget) return
          assignMutation.mutate(
            { id: assignTarget.id, technicianId, notes },
            {
              onSuccess: () => {
                toast.success(`${assignTarget.woNo} assigned`)
                setAssignTarget(null)
              },
            },
          )
        }}
      />

      <CompleteWorkOrderDialog
        open={Boolean(completeTarget)}
        woNumber={completeTarget?.woNo}
        submitting={completeMutation.isPending}
        arrivalRecorded={completeTarget?.hasRecordedArrival ?? false}
        canAdminOverride={isTenantAdmin}
        onClose={() => setCompleteTarget(null)}
        onSubmit={({ workDoneNotes, adminOverride, overrideReason }) => {
          if (!completeTarget) return
          completeMutation.mutate(
            { id: completeTarget.id, workDoneNotes, adminOverride, overrideReason },
            {
              onSuccess: () => {
                toast.success(`${completeTarget.woNo} completed`)
                setCompleteTarget(null)
              },
            },
          )
        }}
      />
    </>
  )
}
