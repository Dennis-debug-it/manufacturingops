import type { WorkOrder, WorkOrderTechnicianAssignment } from '../work-orders/types'

export const FINAL_STATUSES = ['Completed', 'Closed', 'Cancelled']
export const HOLD_STATUSES = ['On Hold', 'Waiting on Parts', 'Waiting on Client']
const STARTABLE_STATUSES = ['New', 'Open', 'Assigned', 'Scheduled', 'Reopened']

/** Where the job stands for this technician, i.e. what they should do next. */
export type JobPhase = 'accept' | 'arrive' | 'en-route' | 'start' | 'work' | 'on-hold' | 'done' | 'declined'

export function myAssignmentOf(job: WorkOrder, technicianId: string | undefined): WorkOrderTechnicianAssignment | undefined {
  return technicianId ? job.technicianAssignments.find((assignment) => assignment.technicianId === technicianId) : undefined
}

export function jobPhase(job: WorkOrder, mine: WorkOrderTechnicianAssignment | undefined): JobPhase {
  if (FINAL_STATUSES.includes(job.status)) return 'done'
  if (HOLD_STATUSES.includes(job.status)) return 'on-hold'
  if (mine?.status === 'Declined') return 'declined'
  if (mine?.status === 'Pending') return 'accept'
  if (mine && !mine.arrivalAt) return mine.status === 'InTransit' ? 'en-route' : 'arrive'
  if (STARTABLE_STATUSES.includes(job.status)) return 'start'
  return 'work'
}

export const PHASE_LABEL: Record<JobPhase, string> = {
  accept: 'New — accept to confirm',
  arrive: 'Accepted — head to site',
  'en-route': 'On the way to site',
  start: 'On site — ready to start',
  work: 'In progress',
  'on-hold': 'On hold',
  done: 'Done',
  declined: 'Declined',
}

export const PHASE_TONE: Record<JobPhase, string> = {
  accept: 'bg-sky-50 text-sky-700 ring-sky-200',
  arrive: 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  'en-route': 'bg-indigo-50 text-indigo-700 ring-indigo-200',
  start: 'bg-amber-50 text-amber-800 ring-amber-200',
  work: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  'on-hold': 'bg-orange-50 text-orange-700 ring-orange-200',
  done: 'bg-slate-100 text-slate-600 ring-slate-200',
  declined: 'bg-slate-100 text-slate-500 ring-slate-200',
}
