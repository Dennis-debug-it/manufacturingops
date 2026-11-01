import { useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { endpoints } from '../../../shared/api/endpoints'
import type { AttendanceStatus } from '../../team/timeAttendance/api'
import type { WorkOrder } from '../../work-orders/types'
import type { MusterDaySheet, MySite } from '../../projects/types'
import type { MusterMarkPayload } from '../../projects/siteApi'
import { performAction, type NewOutboxAction, type PerformResult } from './outbox'
import { refreshAfterSync } from './hooks'

type Job = Pick<WorkOrder, 'id' | 'workOrderNumber'>
type Position = { latitude: number | null; longitude: number | null }
type Site = Pick<MySite, 'projectId' | 'projectNumber' | 'name'>

/**
 * Every action a technician takes on a job, plus clock in/out and a site supervisor's muster roll. Each one works offline: it applies
 * on screen at once and is sent now or as soon as the signal is back (see outbox.ts). The result
 * says whether it was queued so the screen can say "saved, will sync" rather than "saved".
 */
export function useTechActions() {
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState(false)

  return useMemo(() => {
    const run = async <T,>(action: NewOutboxAction): Promise<PerformResult<T>> => {
      setBusy(true)
      try {
        const result = await performAction<T>(action, () => refreshAfterSync(queryClient))
        if (!result.queued) refreshAfterSync(queryClient)
        return result
      } finally {
        setBusy(false)
      }
    }
    const onJob = (job: Job, kind: NewOutboxAction['kind'], what: string, request: Pick<NewOutboxAction, 'method' | 'url' | 'body' | 'file' | 'meta'>) =>
      run({ kind, label: `${what} · ${job.workOrderNumber}`, workOrderId: job.id, ...request })

    return {
      busy,
      respond: (job: Job, technicianId: string, response: 'Accepted' | 'Declined') =>
        onJob(job, 'respond', response === 'Accepted' ? 'Accepted job' : 'Declined job', {
          method: 'POST', url: endpoints.workOrders.technicianResponse(job.id), body: { technicianId, response, notes: null },
        }),
      inTransit: (job: Job, technicianId: string, position: Position) =>
        onJob(job, 'in-transit', 'On the way to site', {
          // Also clocks the technician in against this job if they weren't (the API does it on their own trips).
          method: 'POST', url: endpoints.workOrders.inTransit(job.id), body: { technicianId, ...position, inTransitAt: null, notes: null },
        }),
      arrive: (job: Job, technicianId: string, position: Position) =>
        onJob(job, 'arrive', 'Arrived on site', {
          // No arrivedAt: the server dates it from when it was tapped (X-Occurred-At on a replay).
          method: 'POST', url: endpoints.workOrders.arrival(job.id), body: { technicianId, ...position, arrivedAt: null },
        }),
      depart: (job: Job, technicianId: string, position: Position) =>
        onJob(job, 'depart', 'Left site', {
          method: 'POST', url: endpoints.workOrders.departure(job.id), body: { technicianId, ...position, departedAt: null },
        }),
      start: (job: Job) => onJob(job, 'start', 'Started work', { method: 'POST', url: endpoints.workOrders.start(job.id) }),
      hold: (job: Job, holdType: string, reason: string | null) =>
        onJob(job, 'hold', 'Put on hold', { method: 'PATCH', url: endpoints.workOrders.hold(job.id), body: { holdType, reason } }),
      resume: (job: Job) => onJob(job, 'resume', 'Resumed work', { method: 'PATCH', url: endpoints.workOrders.resume(job.id) }),
      complete: (job: Job, workDoneNotes: string) =>
        onJob(job, 'complete', 'Completed job', { method: 'POST', url: endpoints.workOrders.complete(job.id), body: { workDoneNotes } }),
      saveNotes: (job: Job, notes: { findings?: string | null; workDone?: string | null; progressComment?: string | null }) =>
        onJob(job, 'notes', notes.progressComment ? 'Progress update' : 'Work notes', {
          method: 'POST', url: endpoints.workOrders.executionNotes(job.id), body: notes,
        }),
      answerChecklist: (job: Job, itemId: string, question: string, answer: { responseValue?: string | null; remarks?: string | null; isCompleted?: boolean | null }) =>
        onJob(job, 'checklist', `Checklist: ${question}`, {
          method: 'PUT', url: endpoints.workOrders.checklistItem(job.id, itemId), body: answer, meta: { itemId },
        }),
      uploadPhoto: (job: Job, file: File, details: { caption: string; category: string }) =>
        onJob(job, 'photo', `${details.category} photo`, {
          method: 'POST', url: endpoints.workOrders.photos(job.id), file: { blob: file, name: file.name, fields: details },
        }),
      captureSignature: (job: Job, signature: { signatureType: string; signerName: string; signerRole?: string | null; signatureDataUrl: string; comment?: string | null }) =>
        onJob(job, 'signature', `${signature.signatureType === 'Client' ? 'Client' : 'Your'} signature`, {
          method: 'POST', url: endpoints.workOrders.signatures(job.id), body: signature,
        }),
      /**
       * `direct` marks a part bought straight from a supplier for this job: it may take stores below
       * zero and waits for stores to reconcile it with the supplier's invoice.
       */
      logMaterial: (job: Job, usage: {
        materialItemId: string; quantityUsed: number; assetId?: string | null; materialName: string; unitOfMeasure: string | null; batchOrSerial?: string | null
        direct?: { supplierName: string | null; supplierReference: string | null; unitCost: number | null } | null
      }) =>
        onJob(job, 'material', usage.direct ? `Bought ${usage.quantityUsed} × ${usage.materialName}` : `Used ${usage.quantityUsed} × ${usage.materialName}`, {
          method: 'POST',
          url: endpoints.workOrders.materialsUsed(job.id),
          body: {
            materialItemId: usage.materialItemId, assetId: usage.assetId ?? null, quantityUsed: usage.quantityUsed,
            unitCost: usage.direct?.unitCost ?? null, chargeable: true, notes: null, usedAt: null, batchOrSerial: usage.batchOrSerial ?? null,
            ...(usage.direct ? { boughtDirect: true, supplierName: usage.direct.supplierName, supplierReference: usage.direct.supplierReference } : {}),
          },
          meta: { materialName: usage.materialName, unitOfMeasure: usage.unitOfMeasure },
        }),
      flagSalesLead: (job: Job, note: string) =>
        onJob(job, 'sales-lead', 'Sales lead flagged', { method: 'POST', url: endpoints.technicianJobs.salesLead(job.id), body: { note } }),
      /** Optionally against a project site the technician is on (shown on their clock while it syncs). */
      clockIn: (site?: Site | null) =>
        run({
          kind: 'clock-in', label: site ? `Clocked in · ${site.projectNumber}` : 'Clocked in', workOrderId: null,
          method: 'POST', url: '/api/attendance/me/clock-in', body: site ? { projectId: site.projectId } : {},
          ...(site ? { meta: { projectName: `${site.projectNumber} ${site.name}` } } : {}),
        }),
      clockOut: () => run({ kind: 'clock-out', label: 'Clocked out', workOrderId: null, method: 'POST', url: '/api/attendance/me/clock-out' }),
      setClockStatus: (status: Exclude<AttendanceStatus, 'NotClockedIn'>) =>
        run({
          kind: 'clock-status',
          label: status === 'OnBreak' ? 'Started break' : status === 'Travelling' ? 'Travelling' : 'Back to work',
          workOrderId: null, method: 'POST', url: '/api/attendance/me/status', body: { status },
        }),
      /**
       * A day's muster roll for a site: presence only, never pay. The server upserts one mark per
       * worker per day, so a replay of the same day is harmless; the date travels in the URL.
       */
      saveMuster: (site: Site, date: string, marks: MusterMarkPayload[]) =>
        run<MusterDaySheet>({
          kind: 'muster', label: `Muster roll · ${site.projectNumber} · ${date}`, workOrderId: null,
          method: 'PUT', url: `${endpoints.projectSite.muster(site.projectId)}?date=${encodeURIComponent(date)}`, body: { marks },
          meta: { projectId: site.projectId, date },
        }),
    }
  }, [busy, queryClient])
}

/** The toast after an action: plain when it reached the server, "will sync" when it was queued. */
export function savedMessage(done: string, result: { queued: boolean }) {
  return result.queued ? `${done}. Saved on this phone; it will sync when you're back online.` : done
}
