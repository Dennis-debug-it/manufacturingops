import { describe, expect, it } from 'vitest'
import type { WorkOrder, WorkOrderExecutionBundle } from '../../work-orders/types'
import type { MyAttendanceStatus } from '../../team/timeAttendance/api'
import type { MusterDaySheet } from '../../projects/types'
import type { OutboxEntry } from './outbox'
import { applyToAttendance, applyToBundle, applyToJob, applyToMusterSheet, isPendingPhoto } from './overlay'

const job = {
  id: 'wo-1',
  workOrderNumber: 'WO-1',
  status: 'Assigned',
  arrivalAt: null,
  departureAt: null,
  workStartedAt: null,
  completedAt: null,
  workDoneNotes: null,
  holdReason: null,
  technicianAssignments: [{ id: 'a1', technicianId: 'tech-1', technicianName: 'Moses', isLead: true, status: 'Pending', assignedAt: '2026-10-08T07:00:00Z', acceptedAt: null, arrivalAt: null, departureAt: null, notes: null }],
  checklistItems: [{ id: 'c1', questionText: 'Follow-up required', responseValue: null, remarks: null, isCompleted: false, completedAt: null }],
} as unknown as WorkOrder

const bundle: WorkOrderExecutionBundle = {
  notes: { findings: null, workDone: null },
  photos: [],
  materialUsages: [],
  signatures: [],
  progressUpdates: [],
  reportPreview: {} as WorkOrderExecutionBundle['reportPreview'],
}

let seq = 0
function entry(kind: OutboxEntry['kind'], extra: Partial<OutboxEntry> = {}): OutboxEntry {
  seq++
  return {
    id: `e${seq}`, userId: 'u1', kind, label: kind, workOrderId: 'wo-1', method: 'POST', url: '/x',
    occurredAt: `2026-10-08T08:0${seq % 10}:00Z`, createdAt: `2026-10-08T08:0${seq % 10}:00Z`, status: 'pending', attempts: 0, ...extra,
  }
}

describe('applyToJob', () => {
  it('moves the job through accept, arrive, start and complete as actions are queued', () => {
    const queued = [
      entry('respond', { body: { technicianId: 'tech-1', response: 'Accepted' } }),
      entry('arrive', { body: { technicianId: 'tech-1' } }),
      entry('start'),
      entry('complete', { body: { workDoneNotes: 'Replaced AVR' } }),
    ]
    const view = applyToJob(job, queued)
    const mine = view.technicianAssignments[0]!
    expect(mine.status).toBe('Arrived')
    expect(mine.acceptedAt).toBe(queued[0]!.occurredAt)
    expect(mine.arrivalAt).toBe(queued[1]!.occurredAt)
    expect(view.workStartedAt).toBe(queued[2]!.occurredAt)
    expect(view.status).toBe('Completed')
    expect(view.workDoneNotes).toBe('Replaced AVR')
    // The server copy itself is never changed.
    expect(job.status).toBe('Assigned')
  })

  it('applies hold and checklist answers, and ignores other jobs', () => {
    const view = applyToJob(job, [
      entry('hold', { body: { holdType: 'WaitingOnParts', reason: 'No AVR in van' } }),
      entry('checklist', { body: { responseValue: 'Yes', isCompleted: true }, meta: { itemId: 'c1' } }),
      entry('start', { workOrderId: 'other-job' }),
    ])
    expect(view.status).toBe('Waiting on Parts')
    expect(view.holdReason).toBe('No AVR in van')
    expect(view.checklistItems[0]).toMatchObject({ responseValue: 'Yes', isCompleted: true })
  })
})

describe('applyToBundle', () => {
  it('adds queued notes, photos, signatures and parts', () => {
    const blob = new Blob(['x'], { type: 'image/jpeg' })
    const view = applyToBundle(bundle, 'wo-1', [
      entry('notes', { body: { progressComment: 'Waiting for parts' } }),
      entry('photo', { file: { blob, name: 'avr.jpg', fields: { caption: 'AVR', category: 'Before' } } }),
      entry('signature', { body: { signatureType: 'Client', signerName: 'Alice', signatureDataUrl: 'data:image/png;base64,AA' } }),
      entry('material', { body: { materialItemId: 'm1', quantityUsed: 2 }, meta: { materialName: 'Fuse 10A', unitOfMeasure: 'pcs' } }),
    ], 'Moses')
    expect(view.progressUpdates[0]).toMatchObject({ message: 'Waiting for parts', actorUserName: 'Moses' })
    expect(view.photos).toHaveLength(1)
    expect(isPendingPhoto(view.photos[0]!)).toBe(true)
    expect(view.photos[0]).toMatchObject({ caption: 'AVR', category: 'Before' })
    expect(view.signatures[0]).toMatchObject({ signatureType: 'Client', signerName: 'Alice' })
    expect(view.materialUsages[0]).toMatchObject({ materialName: 'Fuse 10A', quantityUsed: 2, unitOfMeasure: 'pcs' })
  })
})

describe('applyToAttendance', () => {
  it('follows queued clock actions in order', () => {
    const status: MyAttendanceStatus = { technicianId: 't1', technicianName: 'Moses', status: 'NotClockedIn', clockInAt: null, lastLocation: null, currentWorkOrderId: null, currentWorkOrderNumber: null }
    const clockIn = entry('clock-in', { workOrderId: null })
    expect(applyToAttendance(status, [clockIn])).toMatchObject({ status: 'ClockedIn', clockInAt: clockIn.occurredAt })
    expect(applyToAttendance(status, [clockIn, entry('clock-status', { workOrderId: null, body: { status: 'OnBreak' } })]).status).toBe('OnBreak')
    expect(applyToAttendance(status, [clockIn, entry('clock-out', { workOrderId: null })]).status).toBe('NotClockedIn')
  })

  it('shows the project site a queued clock-in is against, and clears it on clock-out', () => {
    const status: MyAttendanceStatus = { technicianId: 't1', technicianName: 'Moses', status: 'NotClockedIn', clockInAt: null, lastLocation: null, currentWorkOrderId: null, currentWorkOrderNumber: null }
    const clockIn = entry('clock-in', { workOrderId: null, body: { projectId: 'p1' }, meta: { projectName: 'PRJ-0007 Westlands' } })
    expect(applyToAttendance(status, [clockIn])).toMatchObject({ status: 'ClockedIn', projectId: 'p1', projectName: 'PRJ-0007 Westlands' })
    expect(applyToAttendance(status, [clockIn, entry('clock-out', { workOrderId: null })])).toMatchObject({ projectId: null, projectName: null })
    expect(applyToAttendance(status, [entry('clock-in', { workOrderId: null, body: {} })])).toMatchObject({ projectId: null, projectName: null })
  })
})

describe('applyToMusterSheet', () => {
  const sheet: MusterDaySheet = {
    workDate: '2026-10-21',
    isSubmitted: false,
    isLocked: false,
    manDays: 0,
    rows: [
      { workerId: 'w1', fullName: 'Achieng', trade: 'Mason', engagementType: 'Casual', status: 'Present', isSuggestion: true, note: null, isActive: true },
      { workerId: 'w2', fullName: 'Kiptoo', trade: 'Helper', engagementType: 'Casual', status: null, isSuggestion: false, note: null, isActive: true },
      { workerId: 'w3', fullName: 'Wanjiru', trade: null, engagementType: 'Permanent', status: null, isSuggestion: false, note: null, isActive: true },
    ],
  }
  const muster = (projectId: string, date: string, marks: { workerId: string; status: string; note?: string | null }[]) =>
    entry('muster', { workOrderId: null, method: 'PUT', body: { marks }, meta: { projectId, date } })

  it('shows queued marks as saved and recounts the man-days, later saves winning', () => {
    const view = applyToMusterSheet(sheet, 'p1', '2026-10-21', [
      muster('p1', '2026-10-21', [{ workerId: 'w1', status: 'Present' }, { workerId: 'w2', status: 'Absent' }]),
      muster('p1', '2026-10-21', [{ workerId: 'w2', status: 'HalfDay', note: 'Left at noon' }]),
    ])
    expect(view.rows[0]).toMatchObject({ status: 'Present', isSuggestion: false })
    expect(view.rows[1]).toMatchObject({ status: 'HalfDay', isSuggestion: false, note: 'Left at noon' })
    expect(view.rows[2]).toMatchObject({ status: null })
    expect(view.manDays).toBe(1.5)
    expect(view.isSubmitted).toBe(true)
    // The server copy itself is never changed.
    expect(sheet.rows[0]!.isSuggestion).toBe(true)
  })

  it('ignores saves for another site or day', () => {
    const view = applyToMusterSheet(sheet, 'p1', '2026-10-21', [
      muster('p2', '2026-10-21', [{ workerId: 'w2', status: 'Present' }]),
      muster('p1', '2026-10-20', [{ workerId: 'w2', status: 'Present' }]),
    ])
    expect(view).toBe(sheet)
  })
})

describe('trips and the clock', () => {
  const status: MyAttendanceStatus = { technicianId: 't1', technicianName: 'Moses', status: 'NotClockedIn', clockInAt: null, lastLocation: null, currentWorkOrderId: null, currentWorkOrderNumber: null }

  it('clocks in on the way, switches to on site on arrival and releases the job on departure', () => {
    const onTheWay = entry('in-transit', { workOrderId: 'wo-1', label: 'On the way to site · WO-0042', body: { technicianId: 't1' } })
    const travelling = applyToAttendance(status, [onTheWay])
    expect(travelling).toMatchObject({ status: 'Travelling', clockInAt: onTheWay.occurredAt, currentWorkOrderId: 'wo-1', currentWorkOrderNumber: 'WO-0042' })

    const arrived = applyToAttendance(status, [onTheWay, entry('arrive', { workOrderId: 'wo-1', body: { technicianId: 't1' } })])
    expect(arrived).toMatchObject({ status: 'ClockedIn', clockInAt: onTheWay.occurredAt, currentWorkOrderId: 'wo-1' })

    const left = applyToAttendance(status, [onTheWay, entry('arrive', { workOrderId: 'wo-1', body: { technicianId: 't1' } }), entry('depart', { workOrderId: 'wo-1', body: { technicianId: 't1' } })])
    expect(left).toMatchObject({ status: 'ClockedIn', currentWorkOrderId: null })
  })

  it('shows the job as on the way until arrival', () => {
    const view = applyToJob(job, [entry('in-transit', { body: { technicianId: 'tech-1' } })])
    expect(view.technicianAssignments[0]!.status).toBe('InTransit')
  })
})
