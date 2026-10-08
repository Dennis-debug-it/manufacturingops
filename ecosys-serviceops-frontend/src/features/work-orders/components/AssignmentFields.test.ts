import { describe, expect, it } from 'vitest'
import { effectiveLead, emptyAssignment, toCreateAssignmentPayload } from './AssignmentFields'

describe('toCreateAssignmentPayload', () => {
  it('is unassigned when nothing is picked', () => {
    expect(toCreateAssignmentPayload(emptyAssignment)).toEqual({ assignmentType: 'Unassigned' })
  })

  it('sends a single technician as an individual assignment', () => {
    expect(toCreateAssignmentPayload({ groupId: '', technicianIds: ['t1'], leadTechnicianId: '' })).toEqual({
      assignmentType: 'IndividualTechnician',
      assignedTechnicianId: 't1',
      leadTechnicianId: null,
    })
  })

  it('sends several technicians with the chosen lead', () => {
    expect(toCreateAssignmentPayload({ groupId: '', technicianIds: ['t1', 't2'], leadTechnicianId: 't2' })).toEqual({
      assignmentType: 'MultipleTechnicians',
      assignedTechnicianIds: ['t1', 't2'],
      leadTechnicianId: 't2',
    })
  })

  it('defaults the lead to the first technician picked', () => {
    expect(toCreateAssignmentPayload({ groupId: '', technicianIds: ['t1', 't2'], leadTechnicianId: '' }).leadTechnicianId).toBe('t1')
  })

  it('sends a team with its picked technicians and lead', () => {
    expect(toCreateAssignmentPayload({ groupId: 'g1', technicianIds: ['t1', 't2'], leadTechnicianId: 't2' })).toEqual({
      assignmentType: 'AssignmentGroup',
      assignmentGroupId: 'g1',
      assignedTechnicianIds: ['t1', 't2'],
      leadTechnicianId: 't2',
    })
  })

  it('sends a team alone to the team queue', () => {
    expect(toCreateAssignmentPayload({ groupId: 'g1', technicianIds: [], leadTechnicianId: '' })).toEqual({
      assignmentType: 'AssignmentGroup',
      assignmentGroupId: 'g1',
      assignedTechnicianIds: null,
      leadTechnicianId: null,
    })
  })
})

describe('effectiveLead', () => {
  it('ignores a lead who is no longer picked', () => {
    expect(effectiveLead({ groupId: '', technicianIds: ['t1'], leadTechnicianId: 't9' })).toBe('t1')
  })
})
