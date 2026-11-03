import { describe, expect, it } from 'vitest'
import type { WorkOrder, WorkOrderTechnicianAssignment } from '../work-orders/types'
import { jobPhase, myAssignmentOf } from './jobState'

const assignment = (overrides: Partial<WorkOrderTechnicianAssignment> = {}): WorkOrderTechnicianAssignment => ({
  id: 'a1', technicianId: 't1', technicianName: 'Ann', isLead: false, status: 'Pending',
  assignedAt: '2026-10-08T08:00:00Z', acceptedAt: null, arrivalAt: null, departureAt: null, notes: null, ...overrides,
})
const job = (status: string, assignments: WorkOrderTechnicianAssignment[]) => ({ status, technicianAssignments: assignments }) as unknown as WorkOrder

describe('jobPhase', () => {
  it('walks a non-lead technician through accept → arrive → start → work', () => {
    expect(jobPhase(job('Assigned', []), assignment())).toBe('accept')
    expect(jobPhase(job('Assigned', []), assignment({ status: 'Accepted' }))).toBe('arrive')
    expect(jobPhase(job('Assigned', []), assignment({ status: 'InTransit' }))).toBe('en-route')
    expect(jobPhase(job('Assigned', []), assignment({ status: 'Arrived', arrivalAt: '2026-10-08T09:00:00Z' }))).toBe('start')
    expect(jobPhase(job('In Progress', []), assignment({ status: 'Arrived', arrivalAt: '2026-10-08T09:00:00Z' }))).toBe('work')
  })

  it('reports holds, declines and finished jobs', () => {
    expect(jobPhase(job('Waiting on Parts', []), assignment({ status: 'Arrived', arrivalAt: 'x' }))).toBe('on-hold')
    expect(jobPhase(job('Assigned', []), assignment({ status: 'Declined' }))).toBe('declined')
    expect(jobPhase(job('Completed', []), assignment())).toBe('done')
  })
})

describe('myAssignmentOf', () => {
  it('finds the signed-in technician among the crew', () => {
    const crew = [assignment({ id: 'a1', technicianId: 't1', isLead: true }), assignment({ id: 'a2', technicianId: 't2' })]
    expect(myAssignmentOf(job('Assigned', crew), 't2')?.id).toBe('a2')
    expect(myAssignmentOf(job('Assigned', crew), undefined)).toBeUndefined()
  })
})
