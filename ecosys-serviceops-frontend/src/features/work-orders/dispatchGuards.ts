/**
 * Client-side mirror of the server guard that decides whether Start is legal.
 *
 * It lives in `WorkOrderAssignmentWorkflowService`:
 * - `HasAcceptedTechnicianAsync` — a `work_order_technician_assignments` row whose `Status` is one
 *   of Accepted/InTransit/Arrived/InProgress/Completed. `WorkOrdersController.Start` rejects the
 *   transition with "cannot be moved to In Progress before at least one technician accepts" when
 *   no such row exists.
 *
 * Mirroring it here lets the UI disable the action and say *why*, instead of firing a request
 * that is guaranteed to come back 400.
 */

/** Assignment statuses the server counts as "this technician accepted the job". */
const ACCEPTED_ASSIGNMENT_STATUSES = new Set(['Accepted', 'InTransit', 'Arrived', 'InProgress', 'Completed'])

/**
 * `WorkOrderAssignmentView.Resolve` synthesises a technician-assignment entry for work orders that
 * only ever got the legacy `work_orders.AssignedTechnicianId`, and marks it with an empty Guid
 * because no persisted row backs it. The server-side guards query the real table, so a stand-in
 * must never be read as an acceptance — and no dispatch action can target it either
 * (the workflow service throws "Technician is not currently assigned to this work order.").
 */
const LEGACY_STAND_IN_ASSIGNMENT_ID = '00000000-0000-0000-0000-000000000000'

/** Minimal shape the guards need; `WorkOrderTechnicianAssignment` satisfies it structurally. */
export interface AssignmentGuardState {
  id: string
  status: string
}

export function isPersistedAssignment(assignment: AssignmentGuardState): boolean {
  return assignment.id !== LEGACY_STAND_IN_ASSIGNMENT_ID
}

export function hasAcceptedTechnician(assignments: readonly AssignmentGuardState[]): boolean {
  return assignments.some((assignment) => isPersistedAssignment(assignment) && ACCEPTED_ASSIGNMENT_STATUSES.has(assignment.status))
}

/** True once the technician has accepted the job. */
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
  }))
}

export const START_BLOCKED_REASON = 'Waiting for a technician to accept this job.'
