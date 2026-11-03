import { FormDrawer } from '../../../shared/components/FormDrawer'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { useWorkOrder } from '../api'
import { CrewDispatchPanel } from './CrewDispatchPanel'

/**
 * Dispatch lifecycle for one work order, opened from a list or board where only the id is known.
 *
 * The board and route endpoints return scheduling projections that carry no technician-assignment
 * state, so the drawer re-reads the work order itself (`GET /api/workorders/{id}`, whose response
 * includes `technicianAssignments`) rather than guessing at acceptance or arrival. One request per
 * opened card, and only when a card is actually opened.
 */
export interface WorkOrderDispatchDrawerProps {
  workOrderId: string | null
  onClose: () => void
}

export function WorkOrderDispatchDrawer({ workOrderId, onClose }: WorkOrderDispatchDrawerProps) {
  const { data: workOrder, isLoading, isError } = useWorkOrder(workOrderId ?? undefined)

  return (
    <FormDrawer open={workOrderId !== null} title="Dispatch" onClose={onClose}>
      {isLoading ? (
        <Spinner label="Loading dispatch state" />
      ) : isError || !workOrder ? (
        <EmptyState title="Work order unavailable" description="The dispatch state for this work order could not be loaded." />
      ) : (
        <div className="space-y-4">
          <div className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-mono text-sm font-semibold text-slate-900">{workOrder.workOrderNumber}</span>
              <StatusBadge status={workOrder.status} />
            </div>
            <p className="mt-1 text-sm text-slate-600">{workOrder.title}</p>
          </div>
          <CrewDispatchPanel workOrder={workOrder} />
        </div>
      )}
    </FormDrawer>
  )
}
