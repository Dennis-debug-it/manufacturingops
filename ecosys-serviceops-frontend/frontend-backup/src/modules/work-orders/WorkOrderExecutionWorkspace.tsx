import { Camera, CheckCircle2, CirclePause, FileDown, FileSearch, MapPinned, PenSquare, Play, Route, Save, SquareX, Upload, UserCheck, X } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import type { MaterialItem, WorkOrder, WorkOrderExecutionBundle } from '../../types/api'
import { workOrderService } from '../../services/workOrderService'
import { formatDateTime } from '../../utils/date'
import { Badge } from '../../components/ui/Badge'
import { HoldReasonModal } from './workOrderWorkflowUi'
import { WorkOrderSignaturePad } from './WorkOrderSignaturePad'

type ToastInput = {
  title: string
  description?: string
  tone: 'success' | 'danger' | 'warning' | 'info'
}

type Props = {
  workOrder: WorkOrder
  execution: WorkOrderExecutionBundle | null
  materials: MaterialItem[]
  onReload: () => Promise<void>
  pushToast: (toast: ToastInput) => void
}

const photoCategories = ['Before', 'During', 'After', 'Defect', 'Completion', 'Spare Part', 'Client Sign-off', 'Other'] as const

export function WorkOrderExecutionWorkspace({ workOrder, execution, materials, onReload, pushToast }: Props) {
  const primaryTechnicianId = useMemo(
    () => workOrder.leadTechnicianId || workOrder.assignedTechnicianId || workOrder.technicianAssignments?.[0]?.technicianId || '',
    [workOrder],
  )
  const hydratedWorkOrderIdRef = useRef<string | null>(null)
  const hydratedSignatureDraftKeyRef = useRef<string | null>(null)
  const [selectedTechnicianId, setSelectedTechnicianId] = useState(primaryTechnicianId)
  const [findings, setFindings] = useState(execution?.notes?.findings || workOrder.jobCardNotes || '')
  const [workDone, setWorkDone] = useState(execution?.notes?.workDone || workOrder.workDoneNotes || '')
  const [materialItemId, setMaterialItemId] = useState('')
  const [materialQty, setMaterialQty] = useState(1)
  const [materialUnitCost, setMaterialUnitCost] = useState('')
  const [materialChargeable, setMaterialChargeable] = useState(true)
  const [materialNotes, setMaterialNotes] = useState('')
  const [photoFiles, setPhotoFiles] = useState<File[]>([])
  const [photoCaption, setPhotoCaption] = useState('')
  const [photoCategory, setPhotoCategory] = useState<(typeof photoCategories)[number]>('During')
  const [includeInReport, setIncludeInReport] = useState(true)
  const [signatureType, setSignatureType] = useState<'Technician' | 'Client'>('Technician')
  const [signatureName, setSignatureName] = useState('')
  const [signatureRole, setSignatureRole] = useState('')
  const [signatureComment, setSignatureComment] = useState('')
  const [signatureDataUrl, setSignatureDataUrl] = useState('')
  const [busyAction, setBusyAction] = useState<string | null>(null)
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdReason, setHoldReason] = useState('')
  const [holdNote, setHoldNote] = useState('')
  const [progressComment, setProgressComment] = useState('')

  useEffect(() => {
    const availableTechnicianIds = (workOrder.technicianAssignments ?? [])
      .map((assignment) => assignment.technicianId)
      .filter((technicianId): technicianId is string => Boolean(technicianId))

    if (!selectedTechnicianId || (availableTechnicianIds.length > 0 && !availableTechnicianIds.includes(selectedTechnicianId))) {
      setSelectedTechnicianId(primaryTechnicianId)
    }
  }, [primaryTechnicianId, selectedTechnicianId, workOrder.technicianAssignments])

  useEffect(() => {
    if (hydratedWorkOrderIdRef.current === workOrder.id) return

    hydratedWorkOrderIdRef.current = workOrder.id
    hydratedSignatureDraftKeyRef.current = null
    setSelectedTechnicianId(primaryTechnicianId)
    setFindings(execution?.notes?.findings || workOrder.jobCardNotes || '')
    setWorkDone(execution?.notes?.workDone || workOrder.workDoneNotes || '')
    setMaterialItemId('')
    setMaterialQty(1)
    setMaterialUnitCost('')
    setMaterialChargeable(true)
    setMaterialNotes('')
    setPhotoFiles([])
    setPhotoCaption('')
    setPhotoCategory('During')
    setIncludeInReport(true)
    setSignatureType('Technician')
    setHoldOpen(false)
    setHoldReason('')
    setHoldNote('')
    setProgressComment('')
  }, [execution?.notes?.findings, execution?.notes?.workDone, primaryTechnicianId, workOrder])

  useEffect(() => {
    const draftKey = `${workOrder.id}:${signatureType}`
    if (hydratedSignatureDraftKeyRef.current === draftKey) return

    hydratedSignatureDraftKeyRef.current = draftKey
    const existing = execution?.signatures.find((item) => item.signatureType.toLowerCase() === signatureType.toLowerCase())
    setSignatureName(existing?.signerName || (signatureType === 'Technician' ? workOrder.leadTechnicianName || workOrder.assignedTechnicianName || '' : ''))
    setSignatureRole(existing?.signerRole || (signatureType === 'Technician' ? 'Technician' : 'Client Representative'))
    setSignatureComment(existing?.comment || '')
    setSignatureDataUrl(existing?.signatureDataUrl || '')
  }, [execution?.signatures, signatureType, workOrder.assignedTechnicianName, workOrder.id, workOrder.leadTechnicianName])

  async function withAction<T>(actionKey: string, operation: () => Promise<T>, successTitle: string, successDescription: string) {
    setBusyAction(actionKey)
    try {
      await operation()
      pushToast({ title: successTitle, description: successDescription, tone: 'success' })
      await onReload()
    } catch (error) {
      pushToast({ title: `${successTitle} failed`, description: error instanceof Error ? error.message : 'The action could not be completed.', tone: 'danger' })
    } finally {
      setBusyAction(null)
    }
  }

  async function getCurrentPosition() {
    if (!('geolocation' in navigator)) return { latitude: null, longitude: null }
    return new Promise<{ latitude: number | null; longitude: number | null }>((resolve) => {
      navigator.geolocation.getCurrentPosition(
        (position) => resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude }),
        () => resolve({ latitude: null, longitude: null }),
        { enableHighAccuracy: true, timeout: 5000, maximumAge: 60000 },
      )
    })
  }

  async function saveNotes() {
    await withAction(
      'save-notes',
      () => workOrderService.saveExecutionNotes(workOrder.id, { findings, workDone }),
      'Execution notes saved',
      'Findings and work done were updated.',
    )
  }

  async function saveProgressUpdate() {
    if (!progressComment.trim()) {
      pushToast({ title: 'Progress update required', description: 'Enter a progress update before saving.', tone: 'warning' })
      return
    }

    await withAction(
      'save-progress-update',
      () => workOrderService.saveExecutionNotes(workOrder.id, { progressComment: progressComment.trim() }),
      'Progress update saved',
      'The work order stayed active and the update was added to its history.',
    )

    setProgressComment('')
  }

  async function acceptJob() {
    if (!selectedTechnicianId) {
      pushToast({ title: 'Technician required', description: 'Select a technician first.', tone: 'warning' })
      return
    }

    await withAction(
      'accept-job',
      () => workOrderService.technicianResponse(workOrder.id, { technicianId: selectedTechnicianId, response: 'Accepted' }),
      'Job accepted',
      'The technician has accepted this job.',
    )
  }

  async function markInTransit() {
    if (!selectedTechnicianId) {
      pushToast({ title: 'Technician required', description: 'Select a technician first.', tone: 'warning' })
      return
    }
    const position = await getCurrentPosition()
    await withAction(
      'mark-in-transit',
      () => workOrderService.markInTransit(workOrder.id, { technicianId: selectedTechnicianId, ...position, inTransitAt: new Date().toISOString() }),
      'In transit recorded',
      'The technician is marked in transit.',
    )
  }

  async function markArrival() {
    if (!selectedTechnicianId) {
      pushToast({ title: 'Technician required', description: 'Select a technician first.', tone: 'warning' })
      return
    }
    const position = await getCurrentPosition()
    await withAction(
      'mark-arrival',
      () => workOrderService.recordArrival(workOrder.id, { technicianId: selectedTechnicianId, ...position, arrivedAt: new Date().toISOString() }),
      'Arrival recorded',
      'The technician arrival was saved.',
    )
  }

  async function startWork() {
    await withAction(
      'start-work',
      () => workOrderService.start(workOrder.id),
      'Work started',
      'The work order is now in progress.',
    )
  }

  async function markDeparture() {
    if (!selectedTechnicianId) {
      pushToast({ title: 'Technician required', description: 'Select a technician first.', tone: 'warning' })
      return
    }
    const position = await getCurrentPosition()
    await withAction(
      'mark-departure',
      () => workOrderService.recordDeparture(workOrder.id, { technicianId: selectedTechnicianId, ...position, departedAt: new Date().toISOString() }),
      'Departure recorded',
      'The technician departure was saved.',
    )
  }

  async function addMaterialUsage() {
    if (!materialItemId || materialQty <= 0) {
      pushToast({ title: 'Material required', description: 'Select a material and quantity first.', tone: 'warning' })
      return
    }

    await withAction(
      'add-material',
      () => workOrderService.addMaterialUsage(workOrder.id, {
        materialItemId,
        assetId: workOrder.assetId || null,
        quantityUsed: materialQty,
        unitCost: materialUnitCost ? Number(materialUnitCost) : null,
        chargeable: materialChargeable,
        notes: materialNotes || null,
        usedAt: new Date().toISOString(),
      }),
      'Material usage saved',
      'Materials and spares used were recorded.',
    )

    setMaterialItemId('')
    setMaterialQty(1)
    setMaterialUnitCost('')
    setMaterialChargeable(true)
    setMaterialNotes('')
  }

  async function uploadPhoto() {
    if (photoFiles.length === 0) {
      pushToast({ title: 'Photo required', description: 'Choose one or more photos to upload first.', tone: 'warning' })
      return
    }

    await withAction(
      'upload-photo',
      async () => {
        for (const [index, file] of photoFiles.entries()) {
          const caption = photoFiles.length === 1
            ? photoCaption || file.name
            : photoCaption.trim()
              ? `${photoCaption.trim()} ${index + 1}`
              : file.name

          await workOrderService.uploadPhoto(workOrder.id, {
            file,
            caption,
            category: photoCategory,
            includeInReport,
          })
        }
      },
      'Photo uploaded',
      photoFiles.length === 1 ? 'Evidence photo was added to the work order.' : `${photoFiles.length} evidence photos were added to the work order.`,
    )

    setPhotoFiles([])
    setPhotoCaption('')
    setPhotoCategory('During')
    setIncludeInReport(true)
  }

  async function savePhoto(photoId: string, nextCaption: string, nextCategory: string, nextIncludeInReport: boolean) {
    await withAction(
      `save-photo-${photoId}`,
      () => workOrderService.updatePhoto(workOrder.id, photoId, {
        caption: nextCaption,
        category: nextCategory,
        includeInReport: nextIncludeInReport,
      }),
      'Photo updated',
      'Photo caption and report settings were saved.',
    )
  }

  async function saveSignature() {
    if (!signatureName.trim() || !signatureDataUrl) {
      pushToast({ title: 'Signature required', description: 'Capture the signature and enter the signer name first.', tone: 'warning' })
      return
    }

    const actionKey = `save-signature-${signatureType.toLowerCase()}`
    setBusyAction(actionKey)
    try {
      await workOrderService.captureSignature(workOrder.id, {
        signatureType,
        signerName: signatureName.trim(),
        signerRole: signatureRole.trim() || null,
        signatureDataUrl,
        comment: signatureComment.trim() || null,
      })
      await onReload()
      pushToast({ title: `${signatureType} signature saved`, description: 'The signature was attached to this work order.', tone: 'success' })
    } catch (error) {
      pushToast({ title: `${signatureType} signature saved failed`, description: error instanceof Error ? error.message : 'The action could not be completed.', tone: 'danger' })
    } finally {
      setBusyAction(null)
    }
  }

  async function completeWorkOrder() {
    if (!workDone.trim()) {
      pushToast({ title: 'Work done required', description: 'Record the work done before completing the job.', tone: 'warning' })
      return
    }

    const requiredIncomplete = workOrder.checklistItems?.find((item) => item.isRequired && (!item.isCompleted || !item.responseValue))
    if (requiredIncomplete) {
      pushToast({ title: 'Checklist incomplete', description: `Complete required checklist item: ${requiredIncomplete.questionText}`, tone: 'warning' })
      return
    }

    const missingEvidence = workOrder.checklistItems?.find((item) => item.evidenceRequired && !item.evidenceAttachmentId)
    if (missingEvidence) {
      pushToast({ title: 'Evidence required', description: `Attach required evidence for: ${missingEvidence.questionText}`, tone: 'warning' })
      return
    }

    await withAction(
      'complete-work-order',
      () => workOrderService.complete(workOrder.id, {
        workDoneNotes: workDone.trim(),
        technicianId: selectedTechnicianId || null,
        assignmentGroupId: workOrder.assignmentGroupId || null,
        reportSummary: workDone.trim(),
      }),
      'Work order completed',
      'The service report data has been finalized.',
    )
  }

  async function pauseWork() {
    if (!holdReason) {
      pushToast({ title: 'Hold reason required', description: 'Select a hold reason before pausing the job.', tone: 'warning' })
      return
    }

    await withAction(
      'pause-work',
      () => workOrderService.pause(workOrder.id, {
        holdReason,
        note: holdNote.trim() || null,
      }),
      'Work placed on hold',
      'The work order has been paused.',
    )

    setHoldOpen(false)
    setHoldReason('')
    setHoldNote('')
  }

  async function resumeWork() {
    await withAction(
      'resume-work',
      () => workOrderService.resume(workOrder.id),
      'Work resumed',
      'The work order is back in progress.',
    )
  }

  async function closeWorkOrder() {
    await withAction(
      'close-work-order',
      () => workOrderService.close(workOrder.id),
      'Work order closed',
      'The work order has been closed.',
    )
  }

  async function cancelWorkOrder() {
    await withAction(
      'cancel-work-order',
      () => workOrderService.cancel(workOrder.id),
      'Work order cancelled',
      'The work order has been cancelled.',
    )
  }

  async function generateReportPreview() {
    setBusyAction('generate-report')
    try {
      await workOrderService.generateServiceReport(workOrder.id)
      pushToast({ title: 'Report generated', description: 'The service report preview has been refreshed.', tone: 'success' })
      await onReload()
    } catch (error) {
      pushToast({ title: 'Generate failed', description: error instanceof Error ? error.message : 'The report could not be generated.', tone: 'danger' })
    } finally {
      setBusyAction(null)
    }
  }

  async function downloadReport() {
    setBusyAction('download-report')
    try {
      await workOrderService.downloadServiceReportPdf(workOrder.id)
      pushToast({ title: 'Report downloaded', description: 'The service report PDF is downloading.', tone: 'success' })
    } catch (error) {
      pushToast({ title: 'Download failed', description: error instanceof Error ? error.message : 'The PDF could not be downloaded.', tone: 'danger' })
    } finally {
      setBusyAction(null)
    }
  }

  return (
    <div className="grid gap-3 lg:grid-cols-[1.12fr_0.88fr]">
      <div className="space-y-3">
        <section className="section-panel space-y-2.5" data-testid="work-order-execution-panel">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="text-sm font-semibold text-app">Execution workflow</h3>
              <p className="text-xs text-muted">Run the field job, capture evidence, and finalize the service report from saved work order data.</p>
            </div>
            <Badge tone="info">{workOrder.status}</Badge>
          </div>

          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Technician</span>
            <select
              value={selectedTechnicianId}
              onChange={(event) => setSelectedTechnicianId(event.target.value)}
              className="field-input"
              data-testid="execution-technician-select"
              disabled={!workOrder.assignmentGroupId || (workOrder.technicianAssignments?.length ?? 0) === 0}
            >
              <option value="">Select technician</option>
              {workOrder.technicianAssignments?.map((assignment) => (
                <option key={assignment.technicianId} value={assignment.technicianId}>
                  {assignment.technicianName || 'Technician'}
                </option>
              ))}
            </select>
            {!workOrder.assignmentGroupId ? (
              <p className="text-sm text-muted">Assign this work order to a group before selecting a technician.</p>
            ) : (workOrder.technicianAssignments?.length ?? 0) === 0 ? (
              <p className="text-sm text-muted">Assign a technician from the assignment panel before running execution actions.</p>
            ) : null}
          </label>

          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
            <ActionButton icon={<UserCheck className="size-3.5" />} label="Accept job" testId="action-accept-job" busy={busyAction === 'accept-job'} onClick={acceptJob} />
            <ActionButton icon={<Route className="size-3.5" />} label="Mark in transit" testId="action-mark-in-transit" busy={busyAction === 'mark-in-transit'} onClick={markInTransit} />
            <ActionButton icon={<MapPinned className="size-3.5" />} label="Mark arrived" testId="action-mark-arrived" busy={busyAction === 'mark-arrival'} onClick={markArrival} />
            <ActionButton icon={<Play className="size-3.5" />} label="Start work" testId="action-start-work" busy={busyAction === 'start-work'} onClick={startWork} />
            <ActionButton icon={<CirclePause className="size-3.5" />} label="Put On Hold" testId="action-pause-work" busy={busyAction === 'pause-work'} onClick={() => setHoldOpen(true)} />
            <ActionButton icon={<Play className="size-3.5" />} label="Resume Work" testId="action-resume-work" busy={busyAction === 'resume-work'} onClick={resumeWork} />
            <ActionButton icon={<CheckCircle2 className="size-3.5" />} label="Complete work" testId="action-complete-work" busy={busyAction === 'complete-work-order'} onClick={completeWorkOrder} />
            <ActionButton icon={<MapPinned className="size-3.5" />} label="Mark departed" testId="action-mark-departed" busy={busyAction === 'mark-departure'} onClick={markDeparture} />
            <ActionButton icon={<CheckCircle2 className="size-3.5" />} label="Close work order" testId="action-close-work-order" busy={busyAction === 'close-work-order'} onClick={closeWorkOrder} />
            <ActionButton icon={<SquareX className="size-3.5" />} label="Cancel work order" testId="action-cancel-work-order" busy={busyAction === 'cancel-work-order'} onClick={cancelWorkOrder} />
          </div>

          <div className="grid gap-2 md:grid-cols-2">
            {(execution?.reportPreview.timestamps || []).map((timestamp) => (
              <div key={timestamp.label} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{timestamp.label}</p>
                <p className="mt-1.5 text-sm font-medium text-app">{timestamp.value || 'Not recorded'}</p>
              </div>
            ))}
          </div>
        </section>

        <section className="section-panel space-y-3">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h3 className="text-base font-semibold text-app">Execution notes</h3>
              <p className="text-sm text-muted">These notes feed directly into the generated service report.</p>
            </div>
            <button type="button" className="button-primary h-9 px-3 py-2 text-xs" onClick={() => void saveNotes()} disabled={busyAction === 'save-notes'} data-testid="save-execution-notes">
              <Save className="size-3.5" />
              {busyAction === 'save-notes' ? 'Saving...' : 'Save notes'}
            </button>
          </div>

          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Findings</span>
            <textarea value={findings} onChange={(event) => setFindings(event.target.value)} className="field-input min-h-[120px]" data-testid="findings-input" />
          </label>

          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Work done</span>
            <textarea value={workDone} onChange={(event) => setWorkDone(event.target.value)} className="field-input min-h-[120px]" data-testid="work-done-input" />
          </label>
        </section>

        <section className="section-panel space-y-3">
          <div className="flex items-center justify-between gap-2">
            <div>
              <h3 className="text-base font-semibold text-app">Progress update</h3>
              <p className="text-sm text-muted">Save multi-day progress without completing or holding the work order.</p>
            </div>
            <button type="button" className="button-primary h-9 px-3 py-2 text-xs" onClick={() => void saveProgressUpdate()} disabled={!progressComment.trim() || busyAction === 'save-progress-update'}>
              <Save className="size-3.5" />
              {busyAction === 'save-progress-update' ? 'Saving...' : 'Save update'}
            </button>
          </div>

          <label className="block space-y-2">
            <span className="text-sm font-medium text-app">Progress update</span>
            <textarea value={progressComment} onChange={(event) => setProgressComment(event.target.value)} className="field-input min-h-[110px]" placeholder="Day 1: Inspected generator, found faulty AVR, awaiting replacement." />
          </label>

          <div className="space-y-2.5">
            {(execution?.progressUpdates || []).map((item) => (
              <div key={item.id} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{item.actorUserName || 'Team update'}</p>
                  <p className="text-xs text-muted">{formatDateTime(item.occurredAt)}</p>
                </div>
                <p className="mt-1.5 text-sm text-app">{item.message}</p>
              </div>
            ))}
            {(execution?.progressUpdates?.length || 0) === 0 ? <div className="border-t border-[var(--app-divider)] pt-3 text-sm text-muted">No progress updates have been saved yet.</div> : null}
          </div>
        </section>

        <section className="section-panel space-y-3" data-testid="work-order-materials-used">
          <div>
            <h3 className="text-base font-semibold text-app">Materials / spares used</h3>
            <p className="text-sm text-muted">Record what was consumed on the job so the report and stock history stay aligned.</p>
          </div>

          <div className="grid gap-2 md:grid-cols-2 xl:grid-cols-4">
            <select value={materialItemId} onChange={(event) => setMaterialItemId(event.target.value)} className="field-input" data-testid="material-select">
              <option value="">Select material</option>
              {materials.map((material) => (
                <option key={material.id} value={material.id}>
                  {material.itemName} ({material.quantityOnHand} {material.unitOfMeasure})
                </option>
              ))}
            </select>
            <input type="number" min={0.01} step="0.01" value={materialQty} onChange={(event) => setMaterialQty(Number(event.target.value) || 1)} className="field-input" placeholder="Quantity used" />
            <input value={materialUnitCost} onChange={(event) => setMaterialUnitCost(event.target.value)} className="field-input" placeholder="Unit cost" />
            <label className="panel-subtle flex items-center gap-3 rounded-full px-4 py-3 text-sm text-app">
              <input type="checkbox" checked={materialChargeable} onChange={(event) => setMaterialChargeable(event.target.checked)} />
              Chargeable
            </label>
          </div>

          <textarea value={materialNotes} onChange={(event) => setMaterialNotes(event.target.value)} className="field-input min-h-[84px]" placeholder="Usage notes" />

          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={() => void addMaterialUsage()} disabled={busyAction === 'add-material'} data-testid="add-material-usage">
              {busyAction === 'add-material' ? 'Saving...' : 'Add material usage'}
            </button>
          </div>

          <div className="space-y-2.5">
            {(execution?.materialUsages || []).length === 0 ? (
              <div className="border-t border-[var(--app-divider)] pt-3 text-sm text-muted">No materials or spares have been recorded yet.</div>
            ) : (
              execution?.materialUsages.map((usage) => (
                <div key={usage.id} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <p className="font-medium text-app">{usage.materialName || 'Material'}</p>
                      <p className="text-sm text-muted">
                        {usage.quantityUsed} {usage.unitOfMeasure || 'unit'}{usage.unitCost != null ? ` @ ${usage.unitCost}` : ''} • {usage.chargeable ? 'Chargeable' : 'Non-chargeable'}
                      </p>
                    </div>
                    <p className="text-xs text-muted">{formatDateTime(usage.usedAt)}</p>
                  </div>
                  {usage.notes ? <p className="mt-3 text-sm text-muted">{usage.notes}</p> : null}
                </div>
              ))
            )}
          </div>
        </section>

        <section className="section-panel space-y-4" data-testid="work-order-photos">
          <div>
            <h3 className="text-lg font-semibold text-app">Photo evidence</h3>
            <p className="text-sm text-muted">Upload job photos, caption them, choose a report category, and decide whether each one appears in the PDF.</p>
          </div>

          <div className="grid gap-3 md:grid-cols-2">
          <label className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-[20px] border border-dashed border-[var(--app-border)] bg-[var(--app-subtle)] px-4 py-5 text-center" data-testid="photo-browse-label">
              <Upload className="size-5 text-muted" />
              <span className="text-sm font-medium text-app">Browse files</span>
              <span className="text-xs text-muted">Select from library</span>
              <input
                type="file"
                accept="image/*"
                multiple
                aria-label="Upload photo"
                className="hidden"
                data-testid="photo-file-input"
                onChange={(event) => setPhotoFiles((prev) => [...prev, ...Array.from(event.target.files ?? [])])}
              />
            </label>

            <label className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-[20px] bg-[var(--app-surface)] px-4 py-5 text-center" data-testid="photo-capture-label">
              <Camera className="size-5 text-[var(--app-primary-strong)]" />
              <span className="text-sm font-medium text-app">Take photo</span>
              <span className="text-xs text-muted">Use device camera</span>
              <input
                type="file"
                accept="image/*"
                capture="environment"
                aria-label="Take photo with camera"
                className="hidden"
                data-testid="photo-capture-input"
                onChange={(event) => setPhotoFiles((prev) => [...prev, ...Array.from(event.target.files ?? [])])}
              />
            </label>
            <input value={photoCaption} onChange={(event) => setPhotoCaption(event.target.value)} className="field-input" placeholder="Caption / footer" data-testid="photo-caption-input" />
            <select value={photoCategory} onChange={(event) => setPhotoCategory(event.target.value as (typeof photoCategories)[number])} className="field-input">
              {photoCategories.map((category) => (
                <option key={category} value={category}>{category}</option>
              ))}
            </select>
            <label className="panel-subtle flex items-center gap-3 rounded-full px-4 py-3 text-sm text-app">
              <input type="checkbox" checked={includeInReport} onChange={(event) => setIncludeInReport(event.target.checked)} />
              Include in report
            </label>
          </div>

          {photoFiles.length > 0 ? (
            <div className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
                Ready to upload ({photoFiles.length} {photoFiles.length === 1 ? 'photo' : 'photos'})
              </p>
              <div className="flex flex-wrap gap-3">
                {photoFiles.map((file, index) => (
                  <div key={`${file.name}-${index}`} className="relative">
                    <img
                      src={URL.createObjectURL(file)}
                      alt={file.name}
                      className="h-20 w-20 rounded-[14px] border border-app object-cover"
                      onLoad={(event) => {
                        const img = event.currentTarget
                        setTimeout(() => URL.revokeObjectURL(img.src), 60_000)
                      }}
                    />
                    <p className="mt-1 max-w-[80px] truncate text-[10px] text-muted">{file.name}</p>
                    <button
                      type="button"
                      aria-label={`Remove ${file.name}`}
                      onClick={() => setPhotoFiles((prev) => prev.filter((_, i) => i !== index))}
                    className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-white shadow-none"
                    >
                      <X className="size-3" />
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ) : null}

          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={() => void uploadPhoto()} disabled={photoFiles.length === 0 || busyAction === 'upload-photo'} data-testid="upload-photo-button">
              <Upload className="size-4" />
              {busyAction === 'upload-photo' ? 'Uploading...' : 'Upload photo'}
            </button>
          </div>

          <div className="grid gap-4 xl:grid-cols-2">
            {(execution?.photos || []).length === 0 ? (
              <div className="border-t border-[var(--app-divider)] pt-4 text-sm text-muted">No photos have been uploaded yet.</div>
            ) : (
              execution?.photos.map((photo) => <EditablePhotoCard key={photo.id} photo={photo} onSave={savePhoto} busy={busyAction === `save-photo-${photo.id}`} />)
            )}
          </div>
        </section>

        <section className="section-panel space-y-4" data-testid="work-order-signatures">
          <div>
            <h3 className="text-lg font-semibold text-app">Signatures</h3>
            <p className="text-sm text-muted">Capture both the technician and client sign-off before completing the work order.</p>
          </div>

          <div className="grid gap-3 md:grid-cols-3">
            <select value={signatureType} onChange={(event) => setSignatureType(event.target.value as 'Technician' | 'Client')} className="field-input" data-testid="signature-type-select">
              <option value="Technician">Technician signature</option>
              <option value="Client">Client / user signature</option>
            </select>
            <input value={signatureName} onChange={(event) => setSignatureName(event.target.value)} className="field-input" placeholder="Signer name" data-testid="signature-name-input" />
            <input value={signatureRole} onChange={(event) => setSignatureRole(event.target.value)} className="field-input" placeholder="Role / title" />
          </div>

          <textarea value={signatureComment} onChange={(event) => setSignatureComment(event.target.value)} className="field-input min-h-[96px]" placeholder="Optional comment" />

          <WorkOrderSignaturePad value={signatureDataUrl} onChange={setSignatureDataUrl} dataTestId="signature-pad" />

          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={() => void saveSignature()} disabled={busyAction === `save-signature-${signatureType.toLowerCase()}`} data-testid="save-signature-button">
              <PenSquare className="size-4" />
              {busyAction === `save-signature-${signatureType.toLowerCase()}` ? 'Saving...' : `Save ${signatureType.toLowerCase()} signature`}
            </button>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            {(execution?.signatures || []).map((signature) => (
              <div key={signature.id} className="border-t border-[var(--app-divider)] pt-4 first:border-t-0 first:pt-0">
                <div className="flex items-start justify-between gap-3">
                  <div>
                    <p className="font-medium text-app">{signature.signatureType}</p>
                    <p className="text-sm text-muted">{signature.signerName}{signature.signerRole ? ` • ${signature.signerRole}` : ''}</p>
                  </div>
                  <p className="text-xs text-muted">{formatDateTime(signature.capturedAt)}</p>
                </div>
                <img src={signature.signatureDataUrl} alt={`${signature.signatureType} signature`} className="mt-3 h-20 rounded-[12px] border border-[var(--app-divider)] bg-white object-contain" />
                {signature.comment ? <p className="mt-3 text-sm text-muted">{signature.comment}</p> : null}
              </div>
            ))}
          </div>
        </section>
      </div>

      <div className="space-y-4">
        <section className="section-panel space-y-3" data-testid="work-order-service-report">
          <div className="flex items-center justify-between gap-3">
            <div>
              <h3 className="text-base font-semibold text-app">Service report preview</h3>
              <p className="text-xs text-muted">This preview is generated from the saved work order data and matches the downloadable PDF.</p>
            </div>
            <button type="button" className="button-primary" onClick={() => void downloadReport()} disabled={busyAction === 'download-report'} data-testid="download-service-report">
              <FileDown className="size-4" />
              {busyAction === 'download-report' ? 'Preparing...' : 'Download PDF'}
            </button>
            <button type="button" className="button-secondary" onClick={() => void generateReportPreview()} disabled={busyAction === 'generate-report'} data-testid="generate-service-report">
              <FileSearch className="size-4" />
              {busyAction === 'generate-report' ? 'Generating...' : 'Generate report'}
            </button>
          </div>

          <div className="rounded-[18px] border border-[var(--app-divider)] bg-white p-5 shadow-none">
            <div className="border-b border-[var(--app-divider)] pb-4">
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-slate-500">{execution?.reportPreview.companyName || 'Service report'}</p>
              <h4 className="mt-2 text-xl font-semibold text-slate-900">{execution?.reportPreview.workOrderNumber} • {execution?.reportPreview.title}</h4>
              <p className="mt-1 text-sm text-slate-500">{execution?.reportPreview.generatedAtLabel}</p>
            </div>

            <div className="mt-5 grid gap-4 md:grid-cols-2">
              <PreviewBlock label="Client" value={execution?.reportPreview.clientName} />
              <PreviewBlock label="Site / Location" value={execution?.reportPreview.siteLabel} />
              <PreviewBlock label="Asset" value={execution?.reportPreview.assetName} />
              <PreviewBlock label="Technician / Team" value={execution?.reportPreview.technicianTeam} />
              <PreviewBlock
                label="Template"
                value={execution?.reportPreview.templateName ? `${execution.reportPreview.templateName}${execution.reportPreview.templateVersion ? ` v${execution.reportPreview.templateVersion}` : ''}` : null}
              />
            </div>

            <PreviewText label="Problem reported" value={execution?.reportPreview.reportedProblem} />
            <PreviewText label="Findings" value={execution?.reportPreview.findings} />
            <PreviewText label="Work done" value={execution?.reportPreview.workDone} />

            <div className="mt-5 space-y-3">
              <p className="text-sm font-semibold text-slate-900">Checklist responses</p>
              {(execution?.reportPreview.checklistSections || []).length === 0 ? (
                <p className="text-sm text-slate-500">No templated checklist responses recorded.</p>
              ) : (
                execution?.reportPreview.checklistSections.map((section) => (
                  <div key={section.sectionName} className="space-y-3 border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                    <p className="font-medium text-slate-900">{section.sectionName}</p>
                    {section.items.map((item, index) => (
                      <div key={`${section.sectionName}-${index}`} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                        <p className="text-sm font-medium text-slate-900">{item.question}{item.isRequired ? ' *' : ''}</p>
                        <p className="mt-1 text-sm text-slate-600">
                          {item.responseValue || 'Not answered'}{item.unit ? ` ${item.unit}` : ''}
                        </p>
                        {item.remarks ? <p className="mt-1 text-sm text-slate-500">Remarks: {item.remarks}</p> : null}
                        {item.evidenceRequired ? <p className="mt-1 text-xs text-slate-500">{item.evidenceUrl ? 'Evidence attached' : 'Evidence missing'}</p> : null}
                      </div>
                    ))}
                  </div>
                ))
              )}
            </div>

            <div className="mt-5 space-y-3">
              <p className="text-sm font-semibold text-slate-900">Materials / spares used</p>
              {(execution?.reportPreview.materials || []).length === 0 ? (
                <p className="text-sm text-slate-500">No materials recorded.</p>
              ) : (
                execution?.reportPreview.materials.map((material, index) => (
                  <div key={`${material.name}-${index}`} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                    <p className="font-medium text-slate-900">{material.name}</p>
                    <p className="text-sm text-slate-500">
                      {material.quantityUsed} {material.unitOfMeasure}{material.unitCost != null ? ` @ ${material.unitCost}` : ''} • {material.chargeable ? 'Chargeable' : 'Non-chargeable'}
                    </p>
                    {material.notes ? <p className="mt-2 text-sm text-slate-600">{material.notes}</p> : null}
                  </div>
                ))
              )}
            </div>

            <div className="mt-5 space-y-3">
              <p className="text-sm font-semibold text-slate-900">Photos grouped by category</p>
              {(execution?.reportPreview.photoGroups || []).length === 0 ? (
                <p className="text-sm text-slate-500">No report photos selected yet.</p>
              ) : (
                execution?.reportPreview.photoGroups.map((group) => (
                  <div key={group.category} className="space-y-3 border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                    <p className="font-medium text-slate-900">{group.category}</p>
                    <div className="grid gap-3">
                      {group.photos.map((photo, index) => (
                        <div key={`${group.category}-${index}`} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                          {photo.publicUrl ? <img src={photo.publicUrl} alt={photo.caption} className="h-36 w-full rounded-xl object-cover" /> : null}
                          <p className="mt-2 text-sm text-slate-700">{photo.caption}</p>
                        </div>
                      ))}
                    </div>
                  </div>
                ))
              )}
            </div>

            <div className="mt-5 space-y-3">
              <p className="text-sm font-semibold text-slate-900">Signatures</p>
              <div className="grid gap-3 md:grid-cols-2">
                {(execution?.reportPreview.signatures || []).map((signature) => (
                  <div key={signature.signatureType} className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
                    <p className="font-medium text-slate-900">{signature.signatureType}</p>
                    <p className="text-sm text-slate-500">{signature.signerName}{signature.signerRole ? ` • ${signature.signerRole}` : ''}</p>
                    <p className="mt-1 text-xs text-slate-400">{signature.capturedAtLabel}</p>
                    {signature.comment ? <p className="mt-2 text-sm text-slate-600">{signature.comment}</p> : null}
                  </div>
                ))}
              </div>
            </div>

            {execution?.reportPreview.showPoweredByEcosys ? <p className="mt-6 text-center text-xs text-slate-400">Powered by Ecosys</p> : null}
          </div>
        </section>
      </div>

      <HoldReasonModal
        open={holdOpen}
        reason={holdReason}
        note={holdNote}
        busy={busyAction === 'pause-work'}
        onReasonChange={setHoldReason}
        onNoteChange={setHoldNote}
        onClose={() => {
          setHoldOpen(false)
          setHoldReason('')
          setHoldNote('')
        }}
        onConfirm={() => void pauseWork()}
      />
    </div>
  )
}

function ActionButton({
  icon,
  label,
  testId,
  busy,
  onClick,
}: {
  icon: ReactNode
  label: string
  testId: string
  busy: boolean
  onClick: () => void
}) {
  return (
    <button type="button" className="button-secondary justify-start" onClick={onClick} disabled={busy} data-testid={testId}>
      {icon}
      {busy ? 'Working...' : label}
    </button>
  )
}

function PreviewBlock({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="border-t border-[var(--app-divider)] pt-3 first:border-t-0 first:pt-0">
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-slate-500">{label}</p>
      <p className="mt-2 text-sm font-medium text-slate-900">{value || 'Not recorded'}</p>
    </div>
  )
}

function PreviewText({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="mt-5">
      <p className="text-sm font-semibold text-slate-900">{label}</p>
      <p className="mt-2 text-sm text-slate-600">{value || 'Not recorded.'}</p>
    </div>
  )
}

function EditablePhotoCard({
  photo,
  onSave,
  busy,
}: {
  photo: WorkOrderExecutionBundle['photos'][number]
  onSave: (photoId: string, caption: string, category: string, includeInReport: boolean) => Promise<void>
  busy: boolean
}) {
  const [caption, setCaption] = useState(photo.caption)
  const [category, setCategory] = useState(photo.category)
  const [includeInReport, setIncludeInReport] = useState(photo.includeInReport)

  useEffect(() => {
    setCaption(photo.caption)
    setCategory(photo.category)
    setIncludeInReport(photo.includeInReport)
  }, [photo.caption, photo.category, photo.includeInReport])

  return (
    <div className="rounded-[18px] border border-[var(--app-divider)] bg-white p-4 shadow-none">
      {photo.publicUrl ? <img src={photo.publicUrl} alt={photo.caption} className="h-52 w-full rounded-[14px] object-cover" /> : null}
      <div className="mt-4 space-y-3">
        <input value={caption} onChange={(event) => setCaption(event.target.value)} className="field-input" />
        <select value={category} onChange={(event) => setCategory(event.target.value)} className="field-input">
          {photoCategories.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
        <label className="panel-subtle flex items-center gap-3 rounded-full px-4 py-3 text-sm text-app">
          <input type="checkbox" checked={includeInReport} onChange={(event) => setIncludeInReport(event.target.checked)} />
          Include in report
        </label>
        <div className="flex items-center justify-between gap-3">
          <p className="text-xs text-muted">{formatDateTime(photo.uploadedAt)}</p>
          <button type="button" className="button-secondary" onClick={() => void onSave(photo.id, caption, category, includeInReport)} disabled={busy}>
            {busy ? 'Saving...' : 'Save photo details'}
          </button>
        </div>
      </div>
    </div>
  )
}
