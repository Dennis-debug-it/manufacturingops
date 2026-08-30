/**
 * Client-side mirror of the two server guards that decide whether Start and Complete are legal.
 *
 * Both live in `WorkOrderAssignmentWorkflowService`:
 * - `HasAcceptedTechnicianAsync` — a `work_order_technician_assignments` row whose `Status` is one
 *   of Accepted/InTransit/Arrived/InProgress/Completed. `WorkOrdersController.Start` rejects the
 *   transition with "cannot be moved to In Progress before at least one technician accepts" when
 *   no such row exists.
 * - `HasArrivalAsync` — a `work_order_technician_assignments` row with `ArrivalAt` set.
 *   `WorkOrdersController.Complete` rejects completion with "Completion cannot happen before
 *   arrival unless admin override is used" when no such row exists.
 *
 * Mirroring them here lets the UI disable the action and say *why*, instead of firing a request
 * that is guaranteed to come back 400.
 */

/** Assignment statuses the server counts as "this technician accepted the job". */
const ACCEPTED_ASSIGNMENT_STATUSES = new Set(['Accepted', 'InTransit', 'Arrived', 'InProgress', 'Completed'])

/**
 * `WorkOrderAssignmentView.Resolve` synthesises a technician-assignment entry for work orders that
 * only ever got the legacy `work_orders.AssignedTechnicianId`, and marks it with an empty Guid
 * because no persisted row backs it. The server-side guards query the real table, so a stand-in
 * must never be read as an acceptance or an arrival — and no dispatch action can target it either
 * (the workflow service throws "Technician is not currently assigned to this work order.").
 */
const LEGACY_STAND_IN_ASSIGNMENT_ID = '00000000-0000-0000-0000-000000000000'

/** Minimal shape the guards need; `WorkOrderTechnicianAssignment` satisfies it structurally. */
export interface AssignmentGuardState {
  id: string
  status: string
  arrivalAt: string | null
}

export function isPersistedAssignment(assignment: AssignmentGuardState): boolean {
  return assignment.id !== LEGACY_STAND_IN_ASSIGNMENT_ID
}

export function hasAcceptedTechnician(assignments: readonly AssignmentGuardState[]): boolean {
  return assignments.some((assignment) => isPersistedAssignment(assignment) && ACCEPTED_ASSIGNMENT_STATUSES.has(assignment.status))
}

export function hasRecordedArrival(assignments: readonly AssignmentGuardState[]): boolean {
  return assignments.some((assignment) => isPersistedAssignment(assignment) && Boolean(assignment.arrivalAt))
}

/** True once the technician has accepted, i.e. the on-site milestones become legal to record. */
export function hasAcceptedAssignment(assignment: AssignmentGuardState): boolean {
  return ACCEPTED_ASSIGNMENT_STATUSES.has(assignment.status)
}

const isRecord = (value: unknown): value is Record<string, unknown> => Boolean(value) && typeof value === 'object'

/** Narrows the raw `technicianAssignments` array of an untyped list row into guard input. */
export function readAssignmentGuardState(value: unknown): AssignmentGuardState[] {
  if (!Array.isArray(value)) {
    return []
  }
  return value.filter(isRecord).map((entry) => ({
    id: typeof entry.id === 'string' ? entry.id : '',
    status: typeof entry.status === 'string' ? entry.status : '',
    arrivalAt: typeof entry.arrivalAt === 'string' ? entry.arrivalAt : null,
  }))
}

export const START_BLOCKED_REASON = 'Waiting for a technician to accept this job.'
export const COMPLETE_BLOCKED_REASON = 'Arrival on site has not been recorded.'
