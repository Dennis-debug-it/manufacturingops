import type {
  WorkOrder,
  WorkOrderChecklistItem,
  WorkOrderExecutionBundle,
  WorkOrderPhoto,
} from '../../work-orders/types'
import type { MyAttendanceStatus } from '../../team/timeAttendance/api'
import type { OutboxEntry } from './outbox'

/**
 * What the technician sees while actions are waiting to sync: the last server copy with their
 * queued actions applied on top, so the job moves on (accepted → on site → in progress → done) the
 * moment they tap, signal or not. Pure functions; the server copy replaces this once synced.
 */

const HOLD_STATUS: Record<string, string> = {
  OnHold: 'On Hold',
  WaitingOnParts: 'Waiting on Parts',
  WaitingOnClient: 'Waiting on Client',
}

type Body = Record<string, unknown>
const bodyOf = (entry: OutboxEntry) => (entry.body ?? {}) as Body
const forJob = (entries: OutboxEntry[], jobId: string) => entries.filter((entry) => entry.workOrderId === jobId)

export function applyToJob(job: WorkOrder, entries: OutboxEntry[]): WorkOrder {
  let next = job
  for (const entry of forJob(entries, job.id)) {
    const body = bodyOf(entry)
    const at = entry.occurredAt
    const updateAssignment = (patch: (assignment: WorkOrder['technicianAssignments'][number]) => Partial<WorkOrder['technicianAssignments'][number]>) => {
      next = {
        ...next,
        technicianAssignments: next.technicianAssignments.map((assignment) =>
          assignment.technicianId === body.technicianId ? { ...assignment, ...patch(assignment) } : assignment),
      }
    }
    switch (entry.kind) {
      case 'respond':
        updateAssignment(() => body.response === 'Accepted' ? { status: 'Accepted', acceptedAt: at } : { status: 'Declined' })
        break
      case 'arrive':
        updateAssignment((assignment) => ({ status: 'Arrived', arrivalAt: assignment.arrivalAt ?? at, lastArrivalAt: at }))
        next = { ...next, arrivalAt: next.arrivalAt ?? at }
        break
      case 'depart':
        updateAssignment(() => ({ departureAt: at }))
        next = { ...next, departureAt: at }
        break
      case 'start':
        next = { ...next, status: 'In Progress', workStartedAt: next.workStartedAt ?? at }
        break
      case 'hold':
        next = { ...next, status: HOLD_STATUS[String(body.holdType)] ?? 'On Hold', holdReason: (body.reason as string | null) ?? null }
        break
      case 'resume':
        next = { ...next, status: 'In Progress', holdReason: null }
        break
      case 'complete':
        next = { ...next, status: 'Completed', completedAt: at, workDoneNotes: (body.workDoneNotes as string) ?? next.workDoneNotes }
        break
      case 'checklist':
        next = { ...next, checklistItems: next.checklistItems.map((item) => item.id === entry.meta?.itemId ? applyChecklistAnswer(item, body, at) : item) }
        break
      default:
        break
    }
  }
  return next
}

function applyChecklistAnswer(item: WorkOrderChecklistItem, body: Body, at: string): WorkOrderChecklistItem {
  return {
    ...item,
    responseValue: body.responseValue === undefined ? item.responseValue : (body.responseValue as string | null),
    remarks: body.remarks === undefined ? item.remarks : (body.remarks as string | null),
    isCompleted: typeof body.isCompleted === 'boolean' ? body.isCompleted : item.isCompleted,
    completedAt: body.isCompleted ? at : item.completedAt,
  }
}

/** A queued photo is shown from the phone until it's uploaded. */
export interface PendingPhoto extends WorkOrderPhoto {
  pending: true
  localBlob: Blob
}

export function applyToBundle(bundle: WorkOrderExecutionBundle, jobId: string, entries: OutboxEntry[], userName: string | null): WorkOrderExecutionBundle {
  let next = bundle
  for (const entry of forJob(entries, jobId)) {
    const body = bodyOf(entry)
    const at = entry.occurredAt
    switch (entry.kind) {
      case 'notes':
        next = {
          ...next,
          notes: {
            findings: body.findings === undefined || body.findings === null ? next.notes.findings : (body.findings as string),
            workDone: body.workDone === undefined || body.workDone === null ? next.notes.workDone : (body.workDone as string),
          },
          progressUpdates: body.progressComment
            ? [{ id: entry.id, message: String(body.progressComment), actorUserId: null, actorUserName: userName, occurredAt: at }, ...next.progressUpdates]
            : next.progressUpdates,
        }
        break
      case 'complete':
        next = { ...next, notes: { ...next.notes, workDone: (body.workDoneNotes as string) ?? next.notes.workDone } }
        break
      case 'photo':
        if (entry.file) {
          const photo: PendingPhoto = {
            id: entry.id, attachmentId: '', fileName: entry.file.name, publicUrl: null,
            caption: entry.file.fields.caption ?? entry.file.name, category: entry.file.fields.category ?? 'During',
            includeInReport: true, uploadedByUserId: entry.userId, uploadedByName: userName, uploadedAt: at,
            pending: true, localBlob: entry.file.blob,
          }
          next = { ...next, photos: [...next.photos, photo] }
        }
        break
      case 'signature':
        next = {
          ...next,
          signatures: [
            ...next.signatures.filter((signature) => signature.signatureType !== body.signatureType),
            {
              id: entry.id, signatureType: String(body.signatureType), signerName: String(body.signerName),
              signerRole: (body.signerRole as string | null) ?? null, signatureDataUrl: String(body.signatureDataUrl),
              comment: (body.comment as string | null) ?? null, capturedByUserId: entry.userId, capturedAt: at,
            },
          ],
        }
        break
      case 'material':
        next = {
          ...next,
          materialUsages: [...next.materialUsages, {
            id: entry.id, materialItemId: String(body.materialItemId), materialName: (entry.meta?.materialName as string) ?? null,
            unitOfMeasure: (entry.meta?.unitOfMeasure as string) ?? null, assetId: (body.assetId as string | null) ?? null, assetName: null,
            quantityUsed: Number(body.quantityUsed), unitCost: null, chargeable: true, notes: null,
            usedByUserId: entry.userId, usedByName: userName, usedAt: at,
          }],
        }
        break
      default:
        break
    }
  }
  return next
}

export function applyToAttendance(status: MyAttendanceStatus, entries: OutboxEntry[]): MyAttendanceStatus {
  let next = status
  for (const entry of entries) {
    if (entry.kind === 'clock-in') next = { ...next, status: 'ClockedIn', clockInAt: entry.occurredAt }
    else if (entry.kind === 'clock-out') next = { ...next, status: 'NotClockedIn', clockInAt: null }
    else if (entry.kind === 'clock-status') next = { ...next, status: String(bodyOf(entry).status) as MyAttendanceStatus['status'] }
  }
  return next
}

export function isPendingPhoto(photo: WorkOrderPhoto): photo is PendingPhoto {
  return (photo as Partial<PendingPhoto>).pending === true
}
