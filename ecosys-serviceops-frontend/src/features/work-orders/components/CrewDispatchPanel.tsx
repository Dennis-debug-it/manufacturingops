import toast from 'react-hot-toast'
import { CheckCircle2, LogOut, MapPin, Navigation, XCircle } from 'lucide-react'
import { formatDurationMinutes } from '../../../shared/utils/format'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import {
  useMarkTechnicianInTransit,
  useRecordTechnicianArrival,
  useRecordTechnicianDeparture,
  useRecordTechnicianResponse,
  useWorkOrderVisits,
  type VisitState,
  type WorkOrderVisits,
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
  const { data: visits } = useWorkOrderVisits(workOrder.id)

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
    <div className="space-y-4">
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
        const visitState = resolveVisitState(assignment, visits)
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

                  Each trip is a visit: Start travel → Record arrival → Record departure, then the
                  next day's trip starts over. Arrival without travel is allowed (travel not tracked).
                */}
                {visitState === 'Idle' ? (
                  <DispatchButton
                    icon={Navigation}
                    label="Start travel"
                    tone="secondary"
                    disabled={busy}
                    onClick={() =>
                      void run(`${technicianName} started travelling to site`, () =>
                        markInTransit.mutateAsync({ id: workOrder.id, technicianId: assignment.technicianId }),
                      )
                    }
                  />
                ) : null}
                {visitState !== 'OnSite' ? (
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
                ) : (
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
                )}
              </div>
            ) : null}
          </li>
        )
      })}
    </ul>
    <VisitLog visits={visits} />
    </div>
  )
}

/**
 * The visit log is the source of truth; the assignment row is only a fallback for technicians who
 * were already travelling or on site before visits were recorded.
 */
function resolveVisitState(assignment: WorkOrderTechnicianAssignment, visits: WorkOrderVisits | undefined): VisitState {
  const summary = visits?.technicians.find((item) => item.technicianId === assignment.technicianId)
  if (summary) return summary.state
  if (assignment.arrivalAt && !assignment.departureAt) return 'OnSite'
  if (assignment.status === 'InTransit') return 'Travelling'
  return 'Idle'
}

function VisitLog({ visits }: { visits: WorkOrderVisits | undefined }) {
  if (!visits || visits.visits.length === 0) return null
  return (
    <section className="rounded-xl border border-[var(--app-border)]">
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-[var(--app-border)] px-4 py-3">
        <h3 className="text-sm font-semibold text-slate-900">Site visits</h3>
        <p className="text-xs text-slate-500">
          Travel {formatDurationMinutes(visits.totalTravelMinutes)} · On site {formatDurationMinutes(visits.totalOnSiteMinutes)}
          {visits.technicians.map((tech) => ` · ${tech.technicianName}: ${tech.daysOnJob} day${tech.daysOnJob === 1 ? '' : 's'}`).join('')}
        </p>
      </header>
      <div className="overflow-x-auto">
        <table className="min-w-full text-left text-xs">
          <thead className="bg-slate-50 text-slate-500">
            <tr>
              <th className="px-4 py-2 font-medium">Technician</th>
              <th className="px-4 py-2 font-medium">Travel started</th>
              <th className="px-4 py-2 font-medium">Arrived</th>
              <th className="px-4 py-2 font-medium">Departed</th>
              <th className="px-4 py-2 font-medium">Travel</th>
              <th className="px-4 py-2 font-medium">On site</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 text-slate-700">
            {visits.visits.map((visit) => (
              <tr key={visit.id}>
                <td className="px-4 py-2">{visit.technicianName}</td>
                <td className="px-4 py-2">{visit.travelStartedAt ? formatDateTime(visit.travelStartedAt) : '—'}</td>
                <td className="px-4 py-2">{visit.arrivedAt ? formatDateTime(visit.arrivedAt) : 'Travelling'}</td>
                <td className="px-4 py-2">{visit.departedAt ? formatDateTime(visit.departedAt) : visit.arrivedAt ? 'On site' : '—'}</td>
                <td className="px-4 py-2">{visit.arrivedAt ? formatDurationMinutes(visit.travelMinutes) : '—'}</td>
                <td className="px-4 py-2">{visit.departedAt ? formatDurationMinutes(visit.onSiteMinutes) : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function AssignmentMilestones({ assignment }: { assignment: WorkOrderTechnicianAssignment }) {
  const milestones: Array<{ label: string; value: string | null }> = [
    { label: 'Accepted', value: assignment.acceptedAt },
    { label: 'First arrival', value: assignment.arrivalAt },
    { label: 'Last departure', value: assignment.departureAt },
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
