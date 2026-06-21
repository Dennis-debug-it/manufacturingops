import { CheckSquare, ClipboardList, FileText, Image, ListChecks, LogIn, LogOut, PackagePlus, Save, Upload } from 'lucide-react'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { NavLink, useLocation, useParams } from 'react-router-dom'
import { DetailRows, type DescriptionListItem } from '../../components/ui/DescriptionList'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { SectionPanel, StatusBadge } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { materialService } from '../../services/materialService'
import { technicianJobsService } from '../../services/technicianJobsService'
import { workOrderService } from '../../services/workOrderService'
import type { MaterialItem, WorkOrder, WorkOrderExecutionBundle } from '../../types/api'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { EvidenceSection, HoldReasonModal, HoldReasonSummary, MobilePrimaryActionBar, MoreActionsMenu, WorkTypeBadge, WorkflowStatusBadge, resolveWorkflowActions } from '../work-orders/workOrderWorkflowUi'
import { WorkOrderSignaturePad } from '../work-orders/WorkOrderSignaturePad'
import { renderContractStatusBadge, renderCoverageStatusBadge } from '../contracts/contractUi'

type TechnicianJobPayload = {
  workOrder: WorkOrder | null
  execution: WorkOrderExecutionBundle | null
  materials: MaterialItem[]
}

const emptyPayload: TechnicianJobPayload = {
  workOrder: null,
  execution: null,
  materials: [],
}

export function TechJobPage() {
  const { id = '' } = useParams()
  const location = useLocation()
  const { pushToast } = useToast()
  const hydratedWorkOrderIdRef = useRef<string | null>(null)
  const checklistHydratedWorkOrderIdRef = useRef<string | null>(null)
  const [workDone, setWorkDone] = useState('')
  const [progressComment, setProgressComment] = useState('')
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])
  const [evidenceCaptions, setEvidenceCaptions] = useState<string[]>([])
  const [materialItemId, setMaterialItemId] = useState('')
  const [materialQty, setMaterialQty] = useState(1)
  const [clientName, setClientName] = useState('')
  const [clientRole, setClientRole] = useState('Client Representative')
  const [clientSignature, setClientSignature] = useState('')
  const [checklistDraft, setChecklistDraft] = useState<Record<string, { responseValue: string; remarks: string; file: File | null; caption: string }>>({})
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdReason, setHoldReason] = useState('')
  const [holdNote, setHoldNote] = useState('')
  const [busy, setBusy] = useState<string | null>(null)

  const { data, loading, error, reload } = useAsyncData<TechnicianJobPayload>(
    async (signal) => {
      const workOrder = await technicianJobsService.get(id, signal)
      const [execution, materials] = await Promise.all([
        workOrderService.getExecution(id, signal),
        materialService.list({ branchId: workOrder.branchId, signal }),
      ])
      return { workOrder, execution, materials }
    },
    emptyPayload,
    [id],
  )

  useEffect(() => {
    const workOrder = data.workOrder
    if (!workOrder) return
    if (hydratedWorkOrderIdRef.current === workOrder.id) return

    hydratedWorkOrderIdRef.current = workOrder.id
    setWorkDone(data.execution?.notes?.workDone || workOrder.workDoneNotes || '')
    setProgressComment('')
    setSelectedFiles([])
    setMaterialItemId('')
    setMaterialQty(1)
    setHoldOpen(false)
    setHoldReason('')
    setHoldNote('')
    const clientExisting = data.execution?.signatures?.find((item) => item.signatureType.toLowerCase() === 'client')
    setClientName(clientExisting?.signerName || '')
    setClientRole(clientExisting?.signerRole || 'Client Representative')
    setClientSignature(clientExisting?.signatureDataUrl || '')
  }, [data.execution, data.workOrder])

  useEffect(() => {
    const workOrder = data.workOrder
    if (!workOrder) return

    const checklistItems = workOrder.checklistItems ?? []
    if (checklistHydratedWorkOrderIdRef.current !== workOrder.id) {
      checklistHydratedWorkOrderIdRef.current = workOrder.id
      setChecklistDraft(buildChecklistDraft(checklistItems))
      return
    }

    setChecklistDraft((current) => mergeChecklistDraft(current, checklistItems))
  }, [data.workOrder])

  if (loading) {
    return <LoadingState label="Loading technician job" />
  }

  if (error || !data.workOrder) {
    return <ErrorState title="Unable to load technician job" description={error || 'The selected job is no longer available.'} />
  }

  const workOrder = data.workOrder
  const canAddProgressUpdates = workOrder.status !== 'Completed' && workOrder.status !== 'Closed'
  const activeSection = resolveSection(location.pathname)
  const checklistItems = workOrder.checklistItems ?? []
  const workflowActions = resolveWorkflowActions(workOrder)
  const primaryWorkflowAction = workflowActions[0] ?? null
  const overflowWorkflowActions = workflowActions.slice(1)
  const workflowMenuActions = overflowWorkflowActions.map((action) => ({
    label: action.label,
    onClick: () => void handleWorkflowAction(action.kind),
  }))
  const summaryItems: DescriptionListItem[] = [
    { label: 'Client', value: workOrder.clientName || '-' },
    { label: 'Site', value: workOrder.siteName || '-' },
    { label: 'Asset', value: workOrder.assetName || '-' },
    { label: 'Due date', value: workOrder.dueDate ? formatDateOnly(workOrder.dueDate) : '-' },
    { label: 'Coverage', value: workOrder.coverageStatus ? renderCoverageStatusBadge(workOrder.coverageStatus) : 'Not covered' },
    { label: 'SLA due', value: workOrder.slaResolutionDeadline ? formatDateTime(workOrder.slaResolutionDeadline) : 'Not configured' },
  ]
  const visitItems: DescriptionListItem[] = [
    { label: 'Arrival', value: workOrder.arrivalAt ? formatDateOnly(workOrder.arrivalAt) : 'Not recorded' },
    { label: 'Departure', value: workOrder.departureAt ? formatDateOnly(workOrder.departureAt) : 'Not recorded' },
    { label: 'Workflow', value: <WorkflowStatusBadge workOrder={workOrder} /> },
  ]

  async function handleWorkflowAction(kind: string) {
    if (kind === 'hold') {
      setHoldReason('')
      setHoldNote('')
      setHoldOpen(true)
      return
    }

    setBusy(kind)
    try {
      if (kind === 'start') {
        await workOrderService.start(workOrder.id)
      } else if (kind === 'resume') {
        await workOrderService.resume(workOrder.id)
      } else if (kind === 'complete') {
        const blockingItem = validateChecklistBeforeCompletion(checklistItems, data.execution?.signatures ?? [])
        if (blockingItem) {
          pushToast({ title: 'Checklist incomplete', description: blockingItem, tone: 'warning' })
          return
        }
        await workOrderService.complete(workOrder.id, {
          workDoneNotes: workDone.trim() || workOrder.workDoneNotes || 'Completed from technician workflow.',
          reportSummary: workDone.trim() || workOrder.workDoneNotes || 'Completed from technician workflow.',
          technicianId: workOrder.leadTechnicianId || workOrder.assignedTechnicianId || null,
        })
      }
      pushToast({ title: 'Job updated', description: 'The active job moved forward.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Action failed', description: nextError instanceof Error ? nextError.message : 'Unable to update the job.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function submitHold() {
    if (!holdReason) return
    setBusy('hold')
    try {
      await workOrderService.pause(workOrder.id, {
        holdReason,
        note: holdNote.trim() || null,
      })
      pushToast({ title: 'Job placed on hold', description: 'The hold reason was saved successfully.', tone: 'success' })
      setHoldOpen(false)
      setHoldReason('')
      setHoldNote('')
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Hold failed', description: nextError instanceof Error ? nextError.message : 'Unable to place the job on hold.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  // Silently request the device location. Resolves with coords or null if denied/unavailable.
  function getCoords(): Promise<{ latitude: number; longitude: number } | null> {
    return new Promise((resolve) => {
      if (!navigator.geolocation) {
        resolve(null)
        return
      }
      navigator.geolocation.getCurrentPosition(
        (pos) => resolve({ latitude: pos.coords.latitude, longitude: pos.coords.longitude }),
        () => resolve(null),
        { timeout: 6000, maximumAge: 30000 },
      )
    })
  }

  async function handleArrive() {
    setBusy('arrive')
    try {
      const coords = await getCoords()
      await workOrderService.recordArrival(workOrder.id, {
        technicianId: workOrder.leadTechnicianId || workOrder.assignedTechnicianId || '',
        latitude: coords?.latitude ?? null,
        longitude: coords?.longitude ?? null,
      })
      pushToast({ title: 'Arrival recorded', description: coords ? 'Location captured.' : 'Location unavailable — arrival time saved.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Arrival failed', description: nextError instanceof Error ? nextError.message : 'Unable to record arrival.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function handleDepart() {
    setBusy('depart')
    try {
      const coords = await getCoords()
      await workOrderService.recordDeparture(workOrder.id, {
        technicianId: workOrder.leadTechnicianId || workOrder.assignedTechnicianId || '',
        latitude: coords?.latitude ?? null,
        longitude: coords?.longitude ?? null,
      })
      pushToast({ title: 'Departure recorded', description: coords ? 'Location captured.' : 'Location unavailable — departure time saved.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Departure failed', description: nextError instanceof Error ? nextError.message : 'Unable to record departure.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function saveUpdate() {
    if (workOrder.status === 'Completed' || workOrder.status === 'Closed') {
      pushToast({ title: 'Progress updates locked', description: 'Progress updates become read-only after the job is completed or closed.', tone: 'warning' })
      return
    }

    if (!progressComment.trim()) {
      pushToast({ title: 'Progress update required', description: 'Enter a progress update before saving.', tone: 'warning' })
      return
    }
    setBusy('save-update')
    try {
      await workOrderService.saveExecutionNotes(workOrder.id, { progressComment: progressComment.trim() })
      pushToast({ title: 'Update saved', description: 'Your work update was captured.', tone: 'success' })
      setProgressComment('')
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the update.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function saveChecklistItem(itemId: string) {
    const item = checklistItems.find((entry) => entry.id === itemId)
    if (!item) return

    const draft = checklistDraft[itemId] ?? { responseValue: '', remarks: '', file: null, caption: '' }
    setBusy(`checklist-${itemId}`)
    try {
      if (draft.file) {
        // Use the user-entered caption if provided; fall back to the question text
        await workOrderService.uploadChecklistEvidence(workOrder.id, itemId, {
          file: draft.file,
          caption: draft.caption.trim() || item.questionText,
        })
      }

      await workOrderService.updateChecklistItem(workOrder.id, itemId, {
        responseValue: draft.responseValue || null,
        remarks: draft.remarks || null,
        isCompleted: true,
      })
      pushToast({ title: 'Checklist updated', description: 'The response was saved.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Checklist save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the checklist response.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function uploadEvidence() {
    if (selectedFiles.length === 0) return
    setBusy('upload-evidence')
    try {
      for (const [index, file] of selectedFiles.entries()) {
        // Use the user-entered caption if provided, otherwise fall back to the filename
        const caption = evidenceCaptions[index]?.trim() || file.name
        await workOrderService.uploadPhoto(workOrder.id, {
          file,
          caption,
          category: 'During',
          includeInReport: true,
        })
      }
      pushToast({
        title: 'Evidence uploaded',
        description: selectedFiles.length === 1 ? 'The file was added to the job.' : `${selectedFiles.length} files were added to the job.`,
        tone: 'success',
      })
      setSelectedFiles([])
      setEvidenceCaptions([])
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Upload failed', description: nextError instanceof Error ? nextError.message : 'Unable to upload evidence.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function addMaterial() {
    if (!materialItemId) return
    setBusy('add-material')
    try {
      await workOrderService.addMaterialUsage(workOrder.id, {
        materialItemId,
        quantityUsed: materialQty,
        chargeable: true,
        assetId: workOrder.assetId || null,
      })
      pushToast({ title: 'Material added', description: 'Material usage was recorded.', tone: 'success' })
      setMaterialItemId('')
      setMaterialQty(1)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Material failed', description: nextError instanceof Error ? nextError.message : 'Unable to add the material.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  async function saveClientSignature() {
    if (!clientName.trim() || !clientSignature) {
      pushToast({ title: 'Signature required', description: 'Capture the signature and signer name before saving.', tone: 'warning' })
      return
    }

    setBusy('save-signature')
    try {
      await workOrderService.captureSignature(workOrder.id, {
        signatureType: 'Client',
        signerName: clientName.trim(),
        signerRole: clientRole.trim(),
        signatureDataUrl: clientSignature,
      })
      pushToast({ title: 'Client sign-off saved', description: 'The client signature was attached to the job.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the client signature.', tone: 'danger' })
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="space-y-4 pb-24" data-testid="tech-job-page">
      <PageHeader
        eyebrow="Technician Job"
        title={workOrder.workOrderNumber}
        subtitle={(
          <div className="space-y-1">
            <p className="text-base font-medium text-secondary">{workOrder.title}</p>
            <p className="text-sm text-muted">{workOrder.description || 'Keep updates concise and operational while working in the field.'}</p>
          </div>
        )}
        meta={[
          { label: 'Client', value: workOrder.clientName || '-' },
          { label: 'Site', value: workOrder.siteName || '-' },
          { label: 'Asset', value: workOrder.assetName || '-' },
          { label: 'Due', value: workOrder.dueDate ? formatDateOnly(workOrder.dueDate) : '-' },
        ]}
        statusBadges={(
          <>
            <WorkflowStatusBadge workOrder={workOrder} />
            <WorkTypeBadge workType={workOrder.workType} />
          </>
        )}
        primaryAction={(
          <div className="hidden flex-wrap gap-2 lg:flex">
            {primaryWorkflowAction ? (
              <button
                type="button"
                className={primaryWorkflowAction.tone === 'secondary' ? 'button-secondary' : 'button-primary'}
                onClick={() => void handleWorkflowAction(primaryWorkflowAction.kind)}
              >
                {primaryWorkflowAction.label}
              </button>
            ) : null}
          </div>
        )}
        secondaryActions={(
          overflowWorkflowActions.length > 0
            ? <MoreActionsMenu actions={workflowMenuActions} />
            : null
        )}
      />

      <SectionPanel title="Field visit" description="Current site activity and dispatch context for this technician.">
        <DetailRows items={visitItems} columns={3} />

        {/* Arrive / Depart — GPS captured silently in the background */}
        {(workOrder.status === 'In Progress' || workOrder.status === 'Assigned') ? (
          <div className="mt-4 flex flex-wrap items-center gap-3">
            {!workOrder.arrivalAt ? (
              <button
                type="button"
                className="button-primary flex items-center gap-2"
                onClick={() => void handleArrive()}
                disabled={busy === 'arrive'}
              >
                <LogIn className="size-4" />
                {busy === 'arrive' ? 'Recording…' : 'Arrive on site'}
              </button>
            ) : !workOrder.departureAt ? (
              <>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-green-500/10 px-3 py-1.5 text-xs font-semibold text-green-700 dark:text-green-300">
                  <span className="size-1.5 rounded-full bg-green-500" />
                  On site
                </span>
                <button
                  type="button"
                  className="button-secondary flex items-center gap-2"
                  onClick={() => void handleDepart()}
                  disabled={busy === 'depart'}
                >
                  <LogOut className="size-4" />
                  {busy === 'depart' ? 'Recording…' : 'Depart site'}
                </button>
              </>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--app-badge-neutral-bg)] px-3 py-1.5 text-xs font-semibold text-[var(--app-badge-neutral-text)]">
                Departed site
              </span>
            )}
          </div>
        ) : null}

        <div className="mt-4">
          <HoldReasonSummary workOrder={workOrder} />
        </div>
      </SectionPanel>

      <div className="grid gap-5 xl:grid-cols-[1.1fr_0.9fr]">
        <SectionPanel title="Job summary" description="Core customer, site, and asset context for the field visit.">
          <DetailRows items={summaryItems} columns={2} />
        </SectionPanel>

        <SectionPanel title="Contract coverage" description="Only operational coverage guidance is shown here for the field team.">
          <DetailRows
            items={[
              { label: 'Contract', value: workOrder.contractNumber || 'Not linked' },
              { label: 'Contract status', value: workOrder.contractStatus ? renderContractStatusBadge(workOrder.contractStatus) : 'Not covered' },
              { label: 'Coverage', value: workOrder.coverageStatus ? renderCoverageStatusBadge(workOrder.coverageStatus) : 'Not covered' },
              { label: 'SLA due', value: workOrder.slaResolutionDeadline ? formatDateTime(workOrder.slaResolutionDeadline) : 'Not configured' },
              { label: 'Service instructions', value: workOrder.pricingNotes || workOrder.assignmentNotes || 'No additional contract instruction recorded.' },
              { label: 'Approval warning', value: workOrder.contractStatus === 'Suspended' || workOrder.contractStatus === 'Expired' ? 'Dispatch approval may be required before extra work.' : 'No approval warning' },
            ]}
            columns={2}
          />
        </SectionPanel>

        <SectionPanel title="Checklist preview" description="Quick look at attached tasks before opening the full checklist tab.">
          <div className="divide-y divide-[var(--app-divider)]">
            {checklistItems.length > 0 ? checklistItems.map((item) => (
              <div key={item.id} className="flex items-start justify-between gap-3 py-3 first:pt-0 last:pb-0">
                <div>
                  <p className="text-sm font-semibold text-app">{item.questionText}</p>
                  <p className="mt-1 text-sm text-muted">{item.remarks || 'Awaiting technician input'}</p>
                </div>
                <StatusBadge tone={item.isCompleted ? 'success' : 'default'}>{item.isCompleted ? 'Done' : 'Open'}</StatusBadge>
              </div>
            )) : (
              <p className="py-4 text-sm text-muted">No checklist items were attached to this job.</p>
            )}
          </div>
        </SectionPanel>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1">
        <TechNavLink to={`/tech/jobs/${id}`} active={activeSection === 'overview'} icon={<ClipboardList className="size-4" />} label="Overview" />
        <TechNavLink to={`/tech/jobs/${id}/checklist`} active={activeSection === 'checklist'} icon={<ListChecks className="size-4" />} label="Checklist" />
        <TechNavLink to={`/tech/jobs/${id}/photos`} active={activeSection === 'photos'} icon={<Image className="size-4" />} label="Evidence" />
        <TechNavLink to={`/tech/jobs/${id}/complete`} active={activeSection === 'complete'} icon={<CheckSquare className="size-4" />} label="Closure" />
      </div>

      {(activeSection === 'overview' || activeSection === 'checklist') ? (
        <SectionPanel title="Progress Updates" description="Save multi-day field progress without completing or putting the job on hold.">
          <div className="flex items-center justify-between gap-3">
            <button type="button" className="button-primary" onClick={() => void saveUpdate()} disabled={!canAddProgressUpdates || busy === 'save-update'}>
              <Save className="size-4" />
              {busy === 'save-update' ? 'Saving...' : 'Save update'}
            </button>
          </div>
          <textarea
            value={progressComment}
            onChange={(event) => setProgressComment(event.target.value)}
            className="field-input mt-4 min-h-[180px]"
            placeholder="Day 1: Inspected generator, found faulty AVR, awaiting replacement."
            disabled={!canAddProgressUpdates}
          />
          {!canAddProgressUpdates ? <p className="mt-3 text-sm text-muted">Progress updates are read-only after the job is completed or closed.</p> : null}
          <div className="mt-4 space-y-3">
            {(data.execution?.progressUpdates ?? []).map((item) => (
              <article key={item.id} className="rounded-[18px] border border-app bg-[var(--app-subtle)] px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{item.actorUserName || 'Team update'}</p>
                  <span className="text-xs text-muted">{formatDateTime(item.occurredAt)}</span>
                </div>
                <p className="mt-2 text-sm text-app">{item.message}</p>
              </article>
            ))}
            {(data.execution?.progressUpdates?.length ?? 0) === 0 ? <div className="rounded-[18px] border border-dashed border-app px-4 py-6 text-center text-sm text-muted">No progress updates have been saved yet.</div> : null}
          </div>
        </SectionPanel>
      ) : null}

      {activeSection === 'checklist' ? (
        <SectionPanel title="Template checklist" description="Responses are saved against the work order snapshot, so later template edits will not alter this job.">
          <div className="mt-4 space-y-4">
            {groupChecklistItems(checklistItems).map((section) => (
              <div key={section.name} className="space-y-3">
                <p className="text-sm font-semibold uppercase tracking-[0.16em] text-muted">{section.name}</p>
                {section.items.map((item) => (
                  <div key={item.id} className="rounded-[20px] border border-app bg-[var(--app-subtle)] p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div>
                        <p className="font-semibold text-app">{item.questionText}{item.isRequired ? ' *' : ''}</p>
                        {item.helperText ? <p className="mt-1 text-sm text-muted">{item.helperText}</p> : null}
                      </div>
                      <span className={`inline-flex rounded-full px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.12em] ${item.isCompleted ? 'badge-success' : 'badge-neutral'}`}>
                        {item.isCompleted ? 'Saved' : 'Open'}
                      </span>
                    </div>
                    <div className="mt-4 grid gap-3 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_auto]">
                      <ChecklistResponseInput
                        item={item}
                        label={`Response for ${item.questionText}`}
                        value={checklistDraft[item.id]?.responseValue ?? ''}
                        onChange={(value) => setChecklistDraft((current) => ({ ...current, [item.id]: { responseValue: value, remarks: current[item.id]?.remarks ?? '', file: current[item.id]?.file ?? null, caption: current[item.id]?.caption ?? '' } }))}
                      />
                      {item.technicianRemarksAllowed === false ? (
                        <div className="field-input flex items-center text-sm text-muted">Remarks disabled</div>
                      ) : (
                        <input
                          value={checklistDraft[item.id]?.remarks ?? ''}
                          onChange={(event) => setChecklistDraft((current) => ({ ...current, [item.id]: { responseValue: current[item.id]?.responseValue ?? '', remarks: event.target.value, file: current[item.id]?.file ?? null, caption: current[item.id]?.caption ?? '' } }))}
                          className="field-input"
                          aria-label={`Remarks for ${item.questionText}`}
                          placeholder="Technician remarks"
                        />
                      )}
                      <button
                        type="button"
                        className="button-primary"
                        aria-label={`Save ${item.questionText}`}
                        onClick={() => void saveChecklistItem(item.id)}
                        disabled={busy === `checklist-${item.id}`}
                      >
                        {busy === `checklist-${item.id}` ? 'Saving...' : 'Save'}
                      </button>
                    </div>
                    <div className="mt-3 space-y-2">
                      <div className="flex flex-wrap items-center gap-2">
                        {/* Browse library */}
                        <label className="button-secondary flex cursor-pointer items-center gap-2 text-sm">
                          <span>Browse</span>
                          <input
                            type="file"
                            accept="image/*"
                            className="hidden"
                            aria-label={`Upload evidence for ${item.questionText}`}
                            onChange={(event) => setChecklistDraft((current) => ({ ...current, [item.id]: { responseValue: current[item.id]?.responseValue ?? '', remarks: current[item.id]?.remarks ?? '', file: event.target.files?.[0] ?? null, caption: current[item.id]?.caption ?? '' } }))}
                          />
                        </label>
                        {/* Take photo with rear camera — capture="environment" for iOS Safari & Android Chrome */}
                        <label className="button-secondary flex cursor-pointer items-center gap-2 text-sm">
                          <span>📷 Take photo</span>
                          <input
                            type="file"
                            accept="image/*"
                            capture="environment"
                            className="hidden"
                            aria-label={`Take evidence photo for ${item.questionText}`}
                            onChange={(event) => setChecklistDraft((current) => ({ ...current, [item.id]: { responseValue: current[item.id]?.responseValue ?? '', remarks: current[item.id]?.remarks ?? '', file: event.target.files?.[0] ?? null, caption: current[item.id]?.caption ?? '' } }))}
                          />
                        </label>
                        <span className={`inline-flex items-center rounded-full px-3 py-2 text-xs font-semibold ${item.evidenceRequired && !item.evidenceAttachmentId ? 'badge-warning' : 'badge-neutral'}`}>
                          {item.evidenceRequired ? (item.evidenceAttachmentId ? 'Evidence attached' : 'Evidence required') : 'Evidence optional'}
                        </span>
                      </div>
                      {/* Thumbnail preview + caption input for staged evidence photo */}
                      {checklistDraft[item.id]?.file ? (
                        <div className="space-y-2 rounded-2xl border border-app bg-[var(--app-subtle)] px-3 py-3">
                          <div className="flex items-center gap-3">
                            {checklistDraft[item.id].file!.type.startsWith('image/') ? (
                              <img
                                src={URL.createObjectURL(checklistDraft[item.id].file!)}
                                alt="Evidence preview"
                                className="h-12 w-12 shrink-0 rounded-xl object-cover"
                              />
                            ) : null}
                            <p className="min-w-0 flex-1 truncate text-xs text-app">{checklistDraft[item.id].file!.name}</p>
                            <button
                              type="button"
                              aria-label="Remove evidence photo"
                              onClick={() => setChecklistDraft((current) => ({ ...current, [item.id]: { ...current[item.id], file: null, caption: '' } } as Record<string, { responseValue: string; remarks: string; file: File | null; caption: string }>))}
                              className="text-muted hover:text-app"
                            >
                              ✕
                            </button>
                          </div>
                          <input
                            type="text"
                            value={checklistDraft[item.id]?.caption ?? ''}
                            onChange={(event) => setChecklistDraft((current) => ({ ...current, [item.id]: { ...current[item.id], caption: event.target.value } }))}
                            placeholder={`Caption (default: "${item.questionText}")`}
                            className="field-input text-sm"
                            aria-label="Photo caption"
                          />
                        </div>
                      ) : null}
                    </div>
                    {(item.inputType === 'number' || item.inputType === 'reading') && (item.minValue != null || item.maxValue != null) ? (
                      <p className="mt-2 text-xs text-muted">Allowed range: {item.minValue ?? '-'} to {item.maxValue ?? '-'} {item.unit ?? ''}</p>
                    ) : null}
                  </div>
                ))}
              </div>
            ))}
          </div>
        </SectionPanel>
      ) : null}

      {activeSection === 'photos' ? (
        <div className="space-y-4">
          <EvidenceSection
            photos={data.execution?.photos ?? []}
            selectedFiles={selectedFiles}
            onSelectFiles={setSelectedFiles}
            captions={evidenceCaptions}
            onCaptionsChange={setEvidenceCaptions}
            onUpdatePhoto={async (photoId, caption, category, includeInReport) => {
              try {
                await workOrderService.updatePhoto(id, photoId, { caption, category, includeInReport })
                pushToast({ title: 'Photo updated', description: 'Caption and settings saved.', tone: 'success' })
                await reload()
              } catch (error) {
                pushToast({ title: 'Update failed', description: error instanceof Error ? error.message : 'Unable to save photo details.', tone: 'danger' })
              }
            }}
          />
          <div className="flex justify-end">
            <button type="button" className="button-primary" onClick={() => void uploadEvidence()} disabled={selectedFiles.length === 0 || busy === 'upload-evidence'}>
              <Upload className="size-4" />
              {busy === 'upload-evidence' ? 'Uploading...' : 'Upload evidence'}
            </button>
          </div>
        </div>
      ) : null}

      {activeSection === 'complete' ? (
        <div className="space-y-5">
          <section className="surface-card rounded-[24px] p-5">
            <div className="flex items-center gap-2">
              <PackagePlus className="size-4 text-[var(--app-primary-strong)]" />
              <p className="text-lg font-semibold text-app">Add material used</p>
            </div>
            <div className="mt-4 grid gap-3 xl:grid-cols-[minmax(0,1.2fr)_100px_auto]">
              <select value={materialItemId} onChange={(event) => setMaterialItemId(event.target.value)} className="field-input" aria-label="Material used">
                <option value="">Select material</option>
                {data.materials.map((item) => (
                  <option key={item.id} value={item.id}>{item.itemName} ({item.quantityOnHand} {item.unitOfMeasure})</option>
                ))}
              </select>
              <input type="number" min={1} value={materialQty} onChange={(event) => setMaterialQty(Number(event.target.value))} className="field-input" aria-label="Material quantity" />
              <button type="button" className="button-primary" onClick={() => void addMaterial()} disabled={busy === 'add-material'}>
                {busy === 'add-material' ? 'Adding...' : 'Add material'}
              </button>
            </div>
          </section>

          <section className="surface-card rounded-[24px] p-5">
            <div className="flex items-center gap-2">
              <FileText className="size-4 text-[var(--app-primary-strong)]" />
              <p className="text-lg font-semibold text-app">Capture client signature</p>
            </div>
            <div className="mt-4">
              <WorkOrderSignaturePad value={clientSignature} onChange={setClientSignature} height={150} />
            </div>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <input value={clientName} onChange={(event) => setClientName(event.target.value)} className="field-input" aria-label="Client name" placeholder="Client name" />
              <input value={clientRole} onChange={(event) => setClientRole(event.target.value)} className="field-input" aria-label="Client role" placeholder="Role" />
            </div>
            <div className="mt-4 flex justify-end">
              <button type="button" className="button-secondary" onClick={() => void saveClientSignature()} disabled={busy === 'save-signature'}>
                {busy === 'save-signature' ? 'Saving...' : 'Save signature'}
              </button>
            </div>
          </section>
        </div>
      ) : null}

      <MobilePrimaryActionBar
        actions={primaryWorkflowAction ? [primaryWorkflowAction] : []}
        onAction={(action) => void handleWorkflowAction(action.kind)}
        menuActions={workflowMenuActions}
      />
      <HoldReasonModal
        open={holdOpen}
        reason={holdReason}
        note={holdNote}
        busy={busy === 'hold'}
        onReasonChange={setHoldReason}
        onNoteChange={setHoldNote}
        onClose={() => {
          setHoldOpen(false)
          setHoldReason('')
          setHoldNote('')
        }}
        onConfirm={() => void submitHold()}
      />
    </div>
  )
}

function TechNavLink({
  to,
  active,
  icon,
  label,
}: {
  to: string
  active: boolean
  icon: ReactNode
  label: string
}) {
  return (
    <NavLink
      to={to}
      className={`flex min-h-[44px] shrink-0 items-center gap-2 rounded-full border px-4 py-2 text-sm font-semibold transition ${
        active
          ? 'border-[rgba(0,77,64,0.18)] bg-[var(--app-primary)] text-white'
          : 'border-app bg-white text-muted hover:bg-[var(--app-subtle-hover)] hover:text-app'
      }`}
    >
      {icon}
      {label}
    </NavLink>
  )
}

function resolveSection(pathname: string) {
  if (pathname.endsWith('/checklist')) return 'checklist'
  if (pathname.endsWith('/photos')) return 'photos'
  if (pathname.endsWith('/complete')) return 'complete'
  return 'overview'
}

function groupChecklistItems(items: NonNullable<WorkOrder['checklistItems']>) {
  return [...items]
    .sort((left, right) => (left.sectionOrder ?? 0) - (right.sectionOrder ?? 0) || left.sortOrder - right.sortOrder)
    .reduce<Array<{ name: string; items: NonNullable<WorkOrder['checklistItems']> }>>((sections, item) => {
      const name = item.sectionName || 'General'
      const existing = sections.find((section) => section.name === name)
      if (existing) {
        existing.items.push(item)
      } else {
        sections.push({ name, items: [item] })
      }
      return sections
    }, [])
}

function validateChecklistBeforeCompletion(
  items: NonNullable<WorkOrder['checklistItems']>,
  signatures: WorkOrderExecutionBundle['signatures'],
) {
  const requiredIncomplete = items.find((item) => item.isRequired && !isChecklistItemSatisfied(item, signatures))
  if (requiredIncomplete) {
    return `Complete required checklist item: ${requiredIncomplete.questionText}`
  }

  const missingEvidence = items.find((item) => item.evidenceRequired && !item.evidenceAttachmentId)
  if (missingEvidence) {
    return `Attach required evidence for: ${missingEvidence.questionText}`
  }

  return null
}

function isChecklistItemSatisfied(
  item: NonNullable<WorkOrder['checklistItems']>[number],
  signatures: WorkOrderExecutionBundle['signatures'],
) {
  if (!item.isCompleted) {
    return false
  }

  if (item.inputType === 'photo') {
    return Boolean(item.evidenceAttachmentId)
  }

  if (item.inputType === 'signature') {
    const needsClientSignature = item.questionText.toLowerCase().includes('client')
    const needsTechnicianSignature = item.questionText.toLowerCase().includes('technician')
    if (needsClientSignature) {
      return signatures.some((entry) => entry.signatureType.toLowerCase() === 'client')
    }
    if (needsTechnicianSignature) {
      return signatures.some((entry) => entry.signatureType.toLowerCase() === 'technician')
    }
    return signatures.length > 0
  }

  return Boolean(item.responseValue?.trim())
}

function ChecklistResponseInput({
  item,
  label,
  value,
  onChange,
}: {
  item: NonNullable<WorkOrder['checklistItems']>[number]
  label: string
  value: string
  onChange: (value: string) => void
}) {
  if (item.inputType === 'dropdown' || item.inputType === 'passfail' || item.inputType === 'condition' || item.inputType === 'yesno' || item.inputType === 'boolean') {
    const options = item.options?.length
      ? item.options
      : item.inputType === 'passfail'
        ? ['Pass', 'Fail']
        : item.inputType === 'condition'
          ? ['Good', 'Fair', 'Poor', 'Critical']
          : ['Yes', 'No']

    return (
      <select value={value} onChange={(event) => onChange(event.target.value)} className="field-input" aria-label={label}>
        <option value="">Select response</option>
        {options.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
    )
  }

  if (item.inputType === 'checkbox') {
    return (
      <select value={value} onChange={(event) => onChange(event.target.value)} className="field-input" aria-label={label}>
        <option value="">Select response</option>
        <option value="Yes">Yes</option>
        <option value="No">No</option>
      </select>
    )
  }

  if (item.inputType === 'photo') {
    return <div className="field-input flex items-center text-sm text-muted">Upload the checklist photo below, then save.</div>
  }

  if (item.inputType === 'signature') {
    return <div className="field-input flex items-center text-sm text-muted">Capture the related signature in the Closure tab, then save.</div>
  }

  if (item.inputType === 'multilinetext') {
    return (
      <textarea
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="field-input min-h-[110px]"
        aria-label={label}
        placeholder="Response"
      />
    )
  }

  return (
    <input
      type={item.inputType === 'number' || item.inputType === 'reading' ? 'number' : item.inputType === 'date' ? 'date' : 'text'}
      min={item.minValue ?? undefined}
      max={item.maxValue ?? undefined}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className="field-input"
      aria-label={label}
      placeholder={item.unit ? `Value (${item.unit})` : 'Response'}
    />
  )
}

// caption field added so per-photo captions survive checklist save/reload cycles
function buildChecklistDraft(items: NonNullable<WorkOrder['checklistItems']>) {
  return items.reduce<Record<string, { responseValue: string; remarks: string; file: File | null; caption: string }>>((draft, item) => {
    draft[item.id] = {
      responseValue: item.responseValue ?? item.defaultValue ?? '',
      remarks: item.remarks ?? '',
      file: null,
      caption: '',
    }
    return draft
  }, {})
}

function mergeChecklistDraft(
  current: Record<string, { responseValue: string; remarks: string; file: File | null; caption: string }>,
  items: NonNullable<WorkOrder['checklistItems']>,
) {
  const next = { ...current }
  for (const item of items) {
    next[item.id] = {
      responseValue: next[item.id]?.responseValue ?? item.responseValue ?? item.defaultValue ?? '',
      remarks: next[item.id]?.remarks ?? item.remarks ?? '',
      file: next[item.id]?.file ?? null,
      caption: next[item.id]?.caption ?? '',
    }
  }
  return next
}
