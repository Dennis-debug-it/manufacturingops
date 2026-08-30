import toast from 'react-hot-toast'
import { CheckCircle2, LogOut, MapPin, Navigation, XCircle } from 'lucide-react'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import {
  useMarkTechnicianInTransit,
  useRecordTechnicianArrival,
  useRecordTechnicianDeparture,
  useRecordTechnicianResponse,
} from '../api'
import { hasAcceptedAssignment, isPersistedAssignment } from '../dispatchGuards'
import type { WorkOrder, WorkOrderTechnicianAssignment } from '../types'

/**
 * The dispatch lifecycle for each technician on the job: accept/decline, en route, arrival and
 * departure. These are the actions the Start and Complete guards read — nothing else in the app
 * records a technician acceptance or an assignment-level arrival, so without this panel a work
 * order can never leave Assigned.
 *
 * All four endpoints only require `EnsureTenantOperationalAccess` server-side, so a dispatcher can
 * record a response phoned in by a technician, and a technician can record their own.
 */
export function CrewDispatchPanel({ workOrder }: { workOrder: WorkOrder }) {
  const canExecute = usePermission('work-orders.execute')
  const respond = useRecordTechnicianResponse()
  const markInTransit = useMarkTechnicianInTransit()
  const recordArrival = useRecordTechnicianArrival()
  const recordDeparture = useRecordTechnicianDeparture()

  const run = async (label: string, action: () => Promise<unknown>) => {
    try {
      await action()
      toast.success(label)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const assignments = workOrder.technicianAssignments

  if (assignments.length === 0) {
    return (
      <div className="rounded-xl border border-[var(--app-border)] px-4 py-3 text-sm text-slate-600">
        No technician is assigned yet. Assign one before the job can be accepted and started.
      </div>
    )
  }

  return (
    <ul className="space-y-3">
      {assignments.map((assignment) => {
        const persisted = isPersistedAssignment(assignment)
        const accepted = hasAcceptedAssignment(assignment)
        const busyTechnicianId =
          (respond.isPending ? respond.variables?.technicianId : undefined) ??
          (markInTransit.isPending ? markInTransit.variables?.technicianId : undefined) ??
          (recordArrival.isPending ? recordArrival.variables?.technicianId : undefined) ??
          (recordDeparture.isPending ? recordDeparture.variables?.technicianId : undefined)
        const busy = busyTechnicianId === assignment.technicianId
        const technicianName = assignment.technicianName ?? 'Technician'

        return (
          <li key={assignment.technicianId} className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-medium text-slate-900">
                {technicianName}
                {assignment.isLead ? <span className="ml-1 text-xs font-normal text-slate-500">(Lead)</span> : null}
              </span>
              <StatusBadge status={assignment.status} />
            </div>

            <AssignmentMilestones assignment={assignment} />

            {!persisted ? (
              <p className="mt-2 text-xs text-amber-700">
                This assignment predates the dispatch workflow and has no assignment record, so responses and site
                milestones cannot be recorded against it. Reassign the technician to create one.
              </p>
            ) : canExecute ? (
              <div className="mt-3 flex flex-wrap gap-2">
                {!accepted ? (
                  <>
                    <DispatchButton
                      icon={CheckCircle2}
                      label="Accept job"
                      tone="primary"
                      disabled={busy}
                      onClick={() =>
                        void run(`${technicianName} accepted the job`, () =>
                          respond.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId, response: 'Accepted' }),
                        )
                      }
                    />
                    <DispatchButton
                      icon={XCircle}
                      label="Decline"
                      tone="danger"
                      disabled={busy}
                      onClick={() =>
                        void run(`${technicianName} declined the job`, () =>
                          respond.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId, response: 'Declined' }),
                        )
                      }
                    />
                  </>
                ) : null}
                {/*
                  `RecordInTransitAsync` and `RecordArrivalAsync` only require the assignment row to
                  exist — neither demands a prior acceptance, and arrival sets Status to "Arrived",
                  which itself satisfies the Start guard. So the site milestones are offered
                  independently of acceptance rather than hidden behind it.
                */}
                {!assignment.arrivalAt ? (
                  <>
                    {assignment.status !== 'InTransit' ? (
                      <DispatchButton
                        icon={Navigation}
                        label="En route"
                        tone="secondary"
                        disabled={busy}
                        onClick={() =>
                          void run(`${technicianName} marked en route`, () =>
                            markInTransit.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId }),
                          )
                        }
                      />
                    ) : null}
                    <DispatchButton
                      icon={MapPin}
                      label="Record arrival"
                      tone="primary"
                      disabled={busy}
                      onClick={() =>
                        void run(`${technicianName} arrived on site`, () =>
                          recordArrival.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId }),
                        )
                      }
                    />
                  </>
                ) : null}
                {assignment.arrivalAt && !assignment.departureAt ? (
                  <DispatchButton
                    icon={LogOut}
                    label="Record departure"
                    tone="secondary"
                    disabled={busy}
                    onClick={() =>
                      void run(`${technicianName} departed site`, () =>
                        recordDeparture.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId }),
                      )
                    }
                  />
                ) : null}
              </div>
            ) : null}
          </li>
        )
      })}
    </ul>
  )
}

function AssignmentMilestones({ assignment }: { assignment: WorkOrderTechnicianAssignment }) {
  const milestones: Array<{ label: string; value: string | null }> = [
    { label: 'Accepted', value: assignment.acceptedAt },
    { label: 'Arrived', value: assignment.arrivalAt },
    { label: 'Departed', value: assignment.departureAt },
  ]

  return (
    <dl className="mt-2 flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-500">
      {milestones.map((milestone) => (
        <div key={milestone.label} className="flex gap-1.5">
          <dt className="font-medium text-slate-600">{milestone.label}:</dt>
          <dd>{milestone.value ? formatDateTime(milestone.value) : 'Not recorded'}</dd>
        </div>
      ))}
    </dl>
  )
}

const toneClass = {
  primary: 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white hover:bg-[var(--app-secondary)]',
  secondary: 'border-[var(--app-border)] bg-white text-slate-700 hover:bg-[var(--app-primary-soft)]',
  danger: 'border-rose-200 bg-white text-rose-600 hover:bg-rose-50',
} as const

function DispatchButton({
  icon: Icon,
  label,
  tone,
  disabled,
  onClick,
}: {
  icon: typeof CheckCircle2
  label: string
  tone: keyof typeof toneClass
  disabled: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      className={`inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-xs font-medium transition disabled:cursor-not-allowed disabled:opacity-60 ${toneClass[tone]}`}
    >
      <Icon className="h-3.5 w-3.5" />
      {label}
    </button>
  )
}
