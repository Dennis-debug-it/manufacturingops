import { AlertTriangle, ArrowUpRight, Camera, Check, Download, FileText, GripVertical, ImagePlus, PauseCircle, Play, Plus, Signature, UploadCloud, Wrench, X } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Modal } from '../../components/ui/Modal'
import { WorkOrderSignaturePad } from './WorkOrderSignaturePad'
import type { MaterialItem, WorkOrder, WorkOrderExecutionBundle, WorkOrderMaterialUsageRecord, WorkOrderPhotoRecord, WorkOrderSignatureRecord } from '../../types/api'

export type WorkflowStatus =
  | 'New'
  | 'Open'
  | 'Assigned'
  | 'Scheduled'
  | 'In Progress'
  | 'On Hold'
  | 'Completed'
  | 'Client Accepted'
  | 'Closed'
  | 'Cancelled'
  | 'Rejected'
  | 'Reopened'

export type WorkflowActionKind =
  | 'assign-schedule'
  | 'start'
  | 'hold'
  | 'resume'
  | 'complete'
  | 'view-report'

export type WorkflowActionMeta = {
  kind: WorkflowActionKind
  label: string
  tone?: 'primary' | 'secondary'
}

export const holdReasonOptions = [
  { value: 'Awaiting Materials', label: 'Awaiting Materials' },
  { value: 'Awaiting Client', label: 'Awaiting Client' },
  { value: 'Awaiting Site Access', label: 'Awaiting Access' },
  { value: 'Awaiting Approval', label: 'Awaiting Approval' },
  { value: 'Other', label: 'Other' },
] as const

export type MenuAction = {
  label: string
  tone?: 'default' | 'danger'
  disabled?: boolean
  onClick?: () => void
}

export const workflowStages = ['New', 'Open', 'Assigned', 'Scheduled', 'In Progress', 'Completed', 'Client Accepted', 'Closed'] as const

export function isClosedStatus(status: string | null | undefined) {
  const value = `${status ?? ''}`.trim().toLowerCase()
  return value === 'closed' || value === 'cancelled' || value === 'rejected'
}

export function isPausedStatus(status: string | null | undefined) {
  const value = `${status ?? ''}`.trim().toLowerCase()
  return value === 'on hold'
    || value === 'paused'
    || value === 'awaiting client'
    || value === 'awaiting materials'
    || value === 'awaiting vendor'
    || value === 'awaiting approval'
    || value === 'awaiting site access'
    || value === 'awaiting spares'
    || value === 'awaiting parts'
    || value === 'pending client'
    || value === 'pending materials'
    || value === 'waiting for access'
    || value === 'waiting for spares'
}

export function isAwaitingClientStatus(status: string | null | undefined) {
  return `${status ?? ''}`.trim().toLowerCase().includes('awaiting')
}

export function isReadyToCloseStatus(status: string | null | undefined) {
  const value = `${status ?? ''}`.trim().toLowerCase()
  return value === 'completed' || value === 'ready to close'
}

export function isInProgressStatus(status: string | null | undefined) {
  const value = `${status ?? ''}`.trim().toLowerCase()
  return value === 'in progress'
}

export function isAssignedStatus(status: string | null | undefined) {
  return `${status ?? ''}`.trim().toLowerCase() === 'assigned'
}

const fallbackTransitionsByStatus: Record<WorkflowStatus, string[]> = {
  New: ['Open', 'Rejected', 'Cancelled'],
  Open: ['Assigned', 'Scheduled', 'Cancelled'],
  Assigned: ['Scheduled', 'In Progress', 'On Hold', 'Cancelled'],
  Scheduled: ['In Progress', 'On Hold', 'Cancelled'],
  'In Progress': ['On Hold', 'Completed'],
  'On Hold': ['In Progress', 'Cancelled'],
  Completed: ['Client Accepted', 'Reopened', 'Closed'],
  'Client Accepted': ['Closed', 'Reopened'],
  Closed: [],
  Cancelled: [],
  Rejected: [],
  Reopened: ['Assigned', 'In Progress'],
}

export function canTransitionTo(workOrder: Pick<WorkOrder, 'availableTransitions' | 'status' | 'assignmentGroupId' | 'assignedTechnicianId' | 'assignedTechnicianIds' | 'technicianAssignments'>, targetStatus: string) {
  const normalizedTarget = targetStatus.trim().toLowerCase()
  const availableTransitions = workOrder.availableTransitions ?? []

  if (availableTransitions.length > 0) {
    return availableTransitions.some(
      (candidate) => candidate.trim().toLowerCase() === normalizedTarget,
    )
  }

  const status = resolveWorkflowStatus(workOrder)
  if (status === 'Open' && normalizedTarget === 'in progress') {
    const hasAssignment = Boolean(
      workOrder.assignmentGroupId
      || workOrder.assignedTechnicianId
      || (workOrder.assignedTechnicianIds?.length ?? 0) > 0
      || (workOrder.technicianAssignments?.length ?? 0) > 0,
    )
    return hasAssignment
  }

  return fallbackTransitionsByStatus[status].some(
    (candidate) => candidate.trim().toLowerCase() === normalizedTarget,
  )
}

export function hasServiceReport(execution?: WorkOrderExecutionBundle | null) {
  return Boolean(execution?.reportPreview?.title)
}

export function isOverdue(workOrder: Pick<WorkOrder, 'dueDate' | 'status'>) {
  if (!workOrder.dueDate || isClosedStatus(workOrder.status)) {
    return false
  }

  return new Date(workOrder.dueDate).getTime() < Date.now()
}

export function resolveWorkflowStatus(workOrder: Pick<WorkOrder, 'status'>) {
  const value = `${workOrder.status ?? ''}`.trim().toLowerCase()
  if (value === 'cancelled') return 'Cancelled'
  if (value === 'rejected') return 'Rejected'
  if (value === 'closed') return 'Closed'
  if (value === 'client accepted' || value === 'acknowledged') return 'Client Accepted'
  if (value === 'completed' || value === 'ready to close') return 'Completed'
  if (value === 'on hold' || isPausedStatus(workOrder.status)) return 'On Hold'
  if (value === 'in progress') return 'In Progress'
  if (value === 'scheduled') return 'Scheduled'
  if (value === 'assigned') return 'Assigned'
  if (value === 'reopened') return 'Reopened'
  if (value === 'open') return 'Open'
  return 'New'
}

export function resolveWorkflowStageIndex(workOrder: WorkOrder, _execution?: WorkOrderExecutionBundle | null) {
  void _execution
  const status = resolveWorkflowStatus(workOrder)
  if (status === 'Cancelled' || status === 'Rejected') return workflowStages.length - 1
  if (status === 'Closed') return 7
  if (status === 'Client Accepted') return 6
  if (status === 'Completed') return 5
  if (status === 'On Hold') return 4
  if (status === 'In Progress') return 4
  if (status === 'Scheduled') return 3
  if (status === 'Assigned' || status === 'Reopened') return 2
  if (status === 'Open') return 1
  return 0
}

export function resolveWorkflowStageLabel(workOrder: WorkOrder, execution?: WorkOrderExecutionBundle | null) {
  return workflowStages[resolveWorkflowStageIndex(workOrder, execution)]
}

export function resolveWorkflowActions(workOrder: WorkOrder): WorkflowActionMeta[] {
  const status = resolveWorkflowStatus(workOrder)
  switch (status) {
    case 'Open':
    case 'Assigned':
    case 'Scheduled':
    case 'Reopened':
      return canTransitionTo(workOrder, 'In Progress') ? [{ kind: 'start', label: 'Start Work' }] : []
    case 'In Progress': {
      const actions: WorkflowActionMeta[] = []
      if (canTransitionTo(workOrder, 'Completed')) {
        actions.push({ kind: 'complete', label: 'Complete' })
      }
      if (canTransitionTo(workOrder, 'On Hold')) {
        actions.push({ kind: 'hold', label: 'Put On Hold', tone: 'secondary' })
      }
      return actions
    }
    case 'On Hold':
      return canTransitionTo(workOrder, 'In Progress') ? [{ kind: 'resume', label: 'Resume Work' }] : []
    case 'Completed':
    case 'Client Accepted':
    case 'Closed':
    case 'Cancelled':
    case 'Rejected':
    case 'New':
    case 'Open':
      return []
  }
}

export function statusTone(status: WorkflowStatus) {
  switch (status) {
    case 'Scheduled':
    case 'Reopened':
      return 'info'
    case 'In Progress':
      return 'info'
    case 'On Hold':
      return 'warning'
    case 'Completed':
      return 'success'
    case 'Client Accepted':
      return 'success'
    case 'Cancelled':
    case 'Rejected':
      return 'danger'
    case 'Closed':
      return 'neutral'
    default:
      return 'default'
  }
}

export function priorityTone(priority: string | null | undefined) {
  const normalized = `${priority ?? ''}`.toLowerCase()
  if (normalized.includes('critical') || normalized.includes('urgent')) return 'danger'
  if (normalized.includes('high')) return 'warning'
  if (normalized.includes('low')) return 'neutral'
  return 'info'
}

export function formatWorkTypeLabel(workType: string | null | undefined) {
  switch (workType) {
    case 'NewInstallation':
      return 'New Installation'
    case 'PreventiveMaintenance':
      return 'Preventive Maintenance'
    case 'CorrectiveMaintenance':
    case 'BreakdownCorrective':
      return 'Corrective Maintenance'
    case 'InspectionAssessment':
      return 'Inspection'
    default:
      return 'Other'
  }
}

export function formatHoldReasonLabel(holdReason: string | null | undefined) {
  switch (`${holdReason ?? ''}`.trim()) {
    case 'Awaiting Site Access':
    case 'Waiting for Access':
      return 'Awaiting Access'
    default:
      return `${holdReason ?? ''}`.trim() || 'Other'
  }
}

export function workTypeTone(workType: string | null | undefined) {
  switch (workType) {
    case 'NewInstallation':
      return 'info'
    case 'PreventiveMaintenance':
      return 'success'
    case 'InspectionAssessment':
      return 'warning'
    case 'CorrectiveMaintenance':
    case 'BreakdownCorrective':
      return 'default'
    default:
      return 'neutral'
  }
}

export function CompactStatCard({
  label,
  value,
  helper,
  icon,
}: {
  label: string
  value: ReactNode
  helper: string
  icon: ReactNode
}) {
  return (
    <article className="surface-card rounded-[18px] px-3.5 py-3">
      <div className="flex items-center gap-3">
        <div className="icon-accent flex h-9 w-9 shrink-0 items-center justify-center rounded-xl">{icon}</div>
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-3">
            <p className="text-[10px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
            <p className="text-lg font-semibold tracking-[-0.03em] text-app">{value}</p>
          </div>
          <p className="mt-1 truncate text-xs text-muted">{helper}</p>
        </div>
      </div>
    </article>
  )
}

export function FilterChip({
  active,
  children,
  onClick,
}: {
  active: boolean
  children: ReactNode
  onClick?: () => void
}) {
  const className = `rounded-full border px-2.5 py-1 text-[11px] font-semibold transition ${
    active
      ? 'border-[rgba(0,77,64,0.18)] bg-[rgba(221,237,73,0.22)] text-[#314100]'
      : 'border-app bg-white text-muted hover:bg-[var(--app-subtle-hover)] hover:text-app'
  }`

  if (!onClick) {
    return <span className={className}>{children}</span>
  }

  return (
    <button type="button" onClick={onClick} className={className}>
      {children}
    </button>
  )
}

export function WorkflowStatusBadge({ workOrder }: { workOrder: WorkOrder }) {
  const status = resolveWorkflowStatus(workOrder)
  return <Badge tone={statusTone(status)}>{status}</Badge>
}

export function PriorityBadge({ priority }: { priority: string | null | undefined }) {
  return <Badge tone={priorityTone(priority)}>{priority || 'Normal'}</Badge>
}

export function WorkTypeBadge({ workType }: { workType: string | null | undefined }) {
  return <Badge tone={workTypeTone(workType)}>{formatWorkTypeLabel(workType)}</Badge>
}

export function WorkflowProgressTracker({
  workOrder,
  execution,
}: {
  workOrder: WorkOrder
  execution?: WorkOrderExecutionBundle | null
}) {
  const activeIndex = resolveWorkflowStageIndex(workOrder, execution)
  const activeStageLabel = workflowStages[activeIndex]

  return (
    <section className="surface-card rounded-[24px] px-4 py-3" data-testid="work-order-lifecycle-tracker">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Lifecycle</p>
        <Badge tone="info">{activeStageLabel}</Badge>
      </div>
      <div className="mt-3 overflow-x-auto pb-1">
        <div className="flex min-w-[640px] items-start">
          {workflowStages.map((stage, index) => {
            const isDone = index < activeIndex
            const isActive = index === activeIndex
            const state = isActive ? 'active' : isDone ? 'completed' : 'upcoming'

            return (
              <div key={stage} className="flex min-w-0 flex-1 items-start">
                <div
                  className="flex min-w-[88px] flex-col items-center text-center"
                  data-testid={`workflow-stage-${stage.toLowerCase().replace(/\s+/g, '-')}`}
                  data-state={state}
                >
                  <div
                    className={`flex h-7 w-7 items-center justify-center rounded-full border text-[11px] font-semibold transition ${
                      isActive
                        ? 'border-[rgba(0,77,64,0.36)] bg-[rgba(0,77,64,0.14)] text-[var(--app-primary-strong)]'
                        : isDone
                          ? 'border-[rgba(0,77,64,0.18)] bg-[rgba(0,77,64,0.1)] text-[var(--app-primary-strong)]'
                          : 'border-app bg-white text-muted'
                    }`}
                  >
                    {isDone ? <Check className="size-3.5" /> : <span>{index + 1}</span>}
                  </div>
                  <p className={`mt-1.5 text-[11px] font-medium leading-4 ${isActive || isDone ? 'text-app' : 'text-muted'}`}>
                    {stage}
                  </p>
                </div>
                {index < workflowStages.length - 1 ? (
                  <div className="mt-3 flex-1 px-2">
                    <div className={`h-px w-full ${index < activeIndex ? 'bg-[rgba(0,77,64,0.24)]' : 'bg-[rgba(148,163,184,0.28)]'}`} />
                  </div>
                ) : null}
              </div>
            )
          })}
        </div>
      </div>
    </section>
  )
}

export function NextStepPanel({
  workOrder,
  execution,
}: {
  workOrder: WorkOrder
  execution?: WorkOrderExecutionBundle | null
}) {
  const status = resolveWorkflowStatus(workOrder)
  const message = useMemo(() => {
    const photoCount = execution?.photos?.length ?? 0
    const signatureCount = execution?.signatures?.length ?? 0
    if (status === 'New' || status === 'Assigned') return 'Start work when the technician is ready to begin.'
    if (status === 'On Hold') return 'Resume work once the hold reason has been cleared.'
    if (status === 'Completed') return 'This work order is complete. Review the report or close out any follow-up admin work.'
    if (photoCount === 0) return 'Upload evidence before this work order can be closed.'
    if (signatureCount < 2) return 'Capture technician and client sign-off.'
    if (status === 'Closed') return 'This job is complete. Review the service report or activity log if needed.'
    return 'Generate the service report and confirm everything needed for closure is recorded.'
  }, [execution?.photos, execution?.signatures, status])

  return (
    <section className="rounded-[24px] border border-[rgba(0,77,64,0.12)] bg-[linear-gradient(180deg,rgba(228,238,234,0.82),rgba(255,255,255,0.98))] p-5 shadow-[var(--app-shadow-soft)]">
      <div className="flex items-start gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[rgba(221,237,73,0.4)] text-[#4d6200]">
          <ArrowUpRight className="size-4" />
        </div>
        <div>
          <p className="text-sm font-semibold text-app">What needs to happen next?</p>
          <p className="mt-2 text-sm leading-6 text-secondary">{message}</p>
        </div>
      </div>
    </section>
  )
}

export function MoreActionsMenu({
  actions,
  align = 'right',
}: {
  actions: MenuAction[]
  align?: 'left' | 'right'
}) {
  const [open, setOpen] = useState(false)

  return (
    <div className="relative">
      <button
        type="button"
        className="icon-button h-9 w-9"
        aria-label="Open more actions"
        onClick={() => setOpen((current) => !current)}
      >
        <GripVertical className="size-4 rotate-90" />
      </button>

      {open ? (
        <div
          className={`glass-panel absolute top-[calc(100%+0.45rem)] z-30 min-w-[180px] rounded-[18px] p-1.5 ${
            align === 'right' ? 'right-0' : 'left-0'
          }`}
        >
          <div className="space-y-1">
            {actions.map((action) => (
              <button
                key={action.label}
                type="button"
                disabled={action.disabled}
                onClick={() => {
                  if (action.disabled) return
                  action.onClick?.()
                  setOpen(false)
                }}
                className={`flex w-full items-center justify-between rounded-xl px-3 py-2 text-left text-sm transition ${
                  action.tone === 'danger'
                    ? 'text-[var(--app-danger)] hover:bg-[var(--app-badge-danger-bg)]'
                    : 'text-app hover:bg-[var(--app-subtle-hover)]'
                } ${action.disabled ? 'cursor-not-allowed opacity-50' : ''}`}
              >
                <span>{action.label}</span>
              </button>
            ))}
          </div>
        </div>
      ) : null}
    </div>
  )
}

export function CancelWorkOrderModal({
  open,
  workOrderNumber,
  reason,
  setReason,
  onClose,
  onConfirm,
  busy,
}: {
  open: boolean
  workOrderNumber: string
  reason: string
  setReason: (value: string) => void
  onClose: () => void
  onConfirm: () => void
  busy?: boolean
}) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Cancel Work Order?"
      description="Cancelling this job will stop all active work and hide its primary next actions."
      maxWidth="max-w-xl"
    >
      <div className="space-y-5">
        <div className="rounded-[24px] border border-[var(--app-badge-danger-border)] bg-[var(--app-badge-danger-bg)] px-4 py-4">
          <div className="flex items-start gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-white text-[var(--app-badge-danger-text)]">
              <AlertTriangle className="size-4" />
            </div>
            <div>
              <p className="text-sm font-semibold text-app">{workOrderNumber}</p>
              <p className="mt-1 text-sm text-secondary">This action cannot be undone from this screen. Please enter a cancellation reason before continuing.</p>
            </div>
          </div>
        </div>

        <label className="block space-y-2">
          <span className="text-sm font-medium text-app">Reason for cancellation</span>
          <textarea
            value={reason}
            onChange={(event) => setReason(event.target.value)}
            className="field-input min-h-[140px]"
            placeholder="Explain why this work order is being cancelled"
          />
        </label>

        <div className="flex flex-wrap justify-end gap-3">
          <button type="button" className="button-secondary" onClick={onClose}>
            Go Back
          </button>
          <button type="button" className="button-primary !bg-[var(--app-badge-danger-text)] !text-white" disabled={!reason.trim() || busy} onClick={onConfirm}>
            {busy ? 'Cancelling...' : 'Confirm Cancel Work Order'}
          </button>
        </div>
      </div>
    </Modal>
  )
}

const evidencePhotoCategories = ['Before', 'During', 'After', 'Defect', 'Completion', 'Spare Part', 'Client Sign-off', 'Other'] as const

type PhotoEditState = { caption: string; category: string; includeInReport: boolean; busy: boolean }

export function EvidenceSection({
  photos,
  selectedFiles,
  onSelectFiles,
  captions,
  onCaptionsChange,
  onUpdatePhoto,
}: {
  photos: WorkOrderPhotoRecord[]
  selectedFiles?: File[]
  onSelectFiles?: (files: File[]) => void
  captions?: string[]
  onCaptionsChange?: (captions: string[]) => void
  onUpdatePhoto?: (photoId: string, caption: string, category: string, includeInReport: boolean) => Promise<void>
}) {
  const readyFiles = selectedFiles ?? []
  const readyCaptions = captions ?? []

  const [editStates, setEditStates] = useState<Record<string, PhotoEditState>>({})

  useEffect(() => {
    setEditStates(
      Object.fromEntries(
        photos.map((photo) => [
          photo.id,
          { caption: photo.caption, category: photo.category, includeInReport: photo.includeInReport, busy: false },
        ]),
      ),
    )
  }, [photos])

  const getEditState = (photo: WorkOrderPhotoRecord): PhotoEditState =>
    editStates[photo.id] ?? { caption: photo.caption, category: photo.category, includeInReport: photo.includeInReport, busy: false }

  const setEditField = (photoId: string, field: keyof PhotoEditState, value: string | boolean) => {
    setEditStates((prev) => ({ ...prev, [photoId]: { ...prev[photoId], [field]: value } }))
  }

  const handleSavePhoto = async (photo: WorkOrderPhotoRecord) => {
    if (!onUpdatePhoto) return
    const state = getEditState(photo)
    setEditField(photo.id, 'busy', true)
    try {
      await onUpdatePhoto(photo.id, state.caption, state.category, state.includeInReport)
    } finally {
      setEditField(photo.id, 'busy', false)
    }
  }

  const appendSelectedFiles = (nextFiles: File[]) => {
    if (nextFiles.length === 0) return
    const next = [...readyFiles, ...nextFiles]
    onSelectFiles?.(next)
    onCaptionsChange?.([...readyCaptions, ...nextFiles.map(() => '')])
  }

  const removeFile = (index: number) => {
    onSelectFiles?.(readyFiles.filter((_, i) => i !== index))
    onCaptionsChange?.(readyCaptions.filter((_, i) => i !== index))
  }

  const updateCaption = (index: number, value: string) => {
    const next = [...readyCaptions]
    next[index] = value
    onCaptionsChange?.(next)
  }

  return (
    <section className="surface-card rounded-[24px] p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-lg font-semibold text-app">Evidence</p>
          <p className="mt-1 text-sm text-muted">Upload job photos, documents, and proof of work without cluttering the workflow.</p>
        </div>
        <Badge tone="neutral">{photos.length} files</Badge>
      </div>

      <div className="mt-4 grid gap-3 lg:grid-cols-[minmax(0,1fr)_220px]">
        <label className="flex cursor-pointer flex-col items-center justify-center rounded-[24px] border border-dashed border-[rgba(0,77,64,0.18)] bg-[rgba(228,238,234,0.55)] px-6 py-8 text-center" data-testid="browse-evidence-control">
          <UploadCloud className="size-7 text-[var(--app-primary-strong)]" />
          <p className="mt-3 text-sm font-semibold text-app">Drag and drop files here or click to browse</p>
          <p className="mt-1 text-sm text-muted">Add photos or documents from the device library without leaving the workspace.</p>
          <input
            type="file"
            accept="image/*,application/pdf,.doc,.docx"
            multiple
            className="hidden"
            onChange={(event) => {
              appendSelectedFiles(Array.from(event.target.files ?? []))
              event.currentTarget.value = ''
            }}
          />
        </label>

        <label className="flex cursor-pointer flex-col items-center justify-center rounded-[24px] border border-[rgba(0,77,64,0.14)] bg-white px-6 py-8 text-center shadow-[var(--app-shadow-soft)]" data-testid="capture-photo-control">
          <Camera className="size-7 text-[var(--app-primary-strong)]" />
          <p className="mt-3 text-sm font-semibold text-app">Take photo</p>
          <p className="mt-1 text-sm text-muted">Open the device camera and capture evidence directly into this work order.</p>
          <input
            type="file"
            accept="image/*"
            capture="environment"
            className="hidden"
            onChange={(event) => {
              appendSelectedFiles(Array.from(event.target.files ?? []))
              event.currentTarget.value = ''
            }}
          />
        </label>
      </div>

      {readyFiles.length > 0 ? (
        <div className="mt-4 space-y-3">
          <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">
            Ready to upload ({readyFiles.length} {readyFiles.length === 1 ? 'file' : 'files'})
          </p>
          {readyFiles.map((file, index) => (
            <div
              key={`${file.name}-${index}`}
              className="flex items-start gap-3 rounded-[18px] border border-[rgba(0,77,64,0.12)] bg-[rgba(255,255,255,0.94)] p-3"
            >
              <div className="relative shrink-0">
                {file.type.startsWith('image/') ? (
                  <img
                    src={URL.createObjectURL(file)}
                    alt={file.name}
                    className="h-16 w-16 rounded-2xl border border-app object-cover"
                    onLoad={(event) => {
                      const img = event.currentTarget
                      setTimeout(() => URL.revokeObjectURL(img.src), 60_000)
                    }}
                  />
                ) : (
                  <div className="flex h-16 w-16 items-center justify-center rounded-2xl border border-app bg-[var(--app-subtle)] text-xs font-semibold text-muted">
                    {file.name.split('.').pop()?.toUpperCase() ?? 'FILE'}
                  </div>
                )}
                <button
                  type="button"
                  aria-label={`Remove ${file.name}`}
                  onClick={() => removeFile(index)}
                  className="absolute -right-1.5 -top-1.5 flex h-5 w-5 items-center justify-center rounded-full bg-red-500 text-white shadow"
                >
                  <X className="size-3" />
                </button>
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-[11px] text-muted">{file.name}</p>
                <input
                  type="text"
                  value={readyCaptions[index] ?? ''}
                  onChange={(event) => updateCaption(index, event.target.value)}
                  placeholder="Add a caption…"
                  className="field-input mt-2 text-sm"
                  aria-label={`Caption for ${file.name}`}
                />
              </div>
            </div>
          ))}
        </div>
      ) : null}

      <div className="mt-4 space-y-3">
        {photos.map((photo) => {
          const state = getEditState(photo)
          return (
            <article key={photo.id} className="rounded-[20px] border border-app bg-[var(--app-surface)] p-3">
              <div className="flex items-start gap-3">
                <div className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-2xl bg-[var(--app-subtle)]">
                  {photo.publicUrl ? <img src={photo.publicUrl} alt={photo.caption} className="h-full w-full object-cover" /> : <ImagePlus className="size-5 text-muted" />}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="truncate text-sm font-semibold text-app">{photo.fileName}</p>
                    {!onUpdatePhoto && (
                      <Badge tone={photo.includeInReport ? 'success' : 'neutral'}>{photo.includeInReport ? 'Report visible' : 'Internal only'}</Badge>
                    )}
                  </div>
                  {onUpdatePhoto ? (
                    <div className="mt-2 space-y-2">
                      <input
                        type="text"
                        value={state.caption}
                        onChange={(event) => setEditField(photo.id, 'caption', event.target.value)}
                        placeholder="Add a caption…"
                        className="field-input text-sm"
                        aria-label={`Caption for ${photo.fileName}`}
                      />
                      <select
                        value={state.category}
                        onChange={(event) => setEditField(photo.id, 'category', event.target.value)}
                        className="field-input text-sm"
                        aria-label={`Category for ${photo.fileName}`}
                      >
                        {evidencePhotoCategories.map((cat) => (
                          <option key={cat} value={cat}>{cat}</option>
                        ))}
                      </select>
                      <label className="flex items-center gap-2 text-sm text-muted">
                        <input
                          type="checkbox"
                          checked={state.includeInReport}
                          onChange={(event) => setEditField(photo.id, 'includeInReport', event.target.checked)}
                        />
                        Include in report
                      </label>
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="text-xs text-muted">
                          {photo.uploadedByName || 'Team member'} · {photo.uploadedAt}
                        </span>
                        <button
                          type="button"
                          className="button-secondary"
                          onClick={() => void handleSavePhoto(photo)}
                          disabled={state.busy}
                        >
                          <Check className="size-3.5" />
                          {state.busy ? 'Saving…' : 'Save'}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <>
                      <p className="mt-1 text-sm text-muted">{photo.caption || 'No description added.'}</p>
                      <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted">
                        <span>Uploaded by {photo.uploadedByName || 'Team member'}</span>
                        <span>{photo.uploadedAt}</span>
                        <span>{photo.category}</span>
                      </div>
                    </>
                  )}
                </div>
              </div>
            </article>
          )
        })}
        {photos.length === 0 ? (
          <div className="rounded-[20px] border border-app bg-[var(--app-subtle)] px-4 py-8 text-center text-sm text-muted">No evidence uploaded yet.</div>
        ) : null}
      </div>
    </section>
  )
}

export function MaterialsSection({
  materials,
  catalog,
  onAdd,
}: {
  materials: WorkOrderMaterialUsageRecord[]
  catalog?: MaterialItem[]
  onAdd?: () => void
}) {
  return (
    <section className="surface-card rounded-[24px] p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-lg font-semibold text-app">Materials used</p>
          <p className="mt-1 text-sm text-muted">Keep consumables compact, visible, and easy to reconcile later.</p>
        </div>
        <button type="button" className="button-primary" onClick={onAdd}>
          <Plus className="size-4" />
          Add material
        </button>
      </div>

      <div className="mt-4 overflow-x-auto overscroll-x-contain rounded-[20px] border border-app">
        <table className="w-full min-w-[640px]">
          <thead className="table-head">
            <tr>
              <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Material name</th>
              <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Quantity</th>
              <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Unit</th>
              <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Source / store</th>
              <th className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Notes</th>
            </tr>
          </thead>
          <tbody>
            {materials.map((item) => (
              <tr key={item.id} className="table-row border-t border-app">
                <td className="px-4 py-3 text-sm text-app">{item.materialName || 'Material'}</td>
                <td className="px-4 py-3 text-sm text-app">{item.quantityUsed}</td>
                <td className="px-4 py-3 text-sm text-app">{item.unitOfMeasure || '-'}</td>
                <td className="px-4 py-3 text-sm text-app">{item.assetName || catalog?.find((material) => material.id === item.materialItemId)?.itemName || 'Main store'}</td>
                <td className="px-4 py-3 text-sm text-muted">{item.notes || '-'}</td>
              </tr>
            ))}
            {materials.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-4 py-8 text-center text-sm text-muted">No material usage recorded yet.</td>
              </tr>
            ) : null}
          </tbody>
        </table>
      </div>
    </section>
  )
}

export function SignatureSection({
  signatures,
  technicianSignature,
  setTechnicianSignature,
  clientSignature,
  setClientSignature,
}: {
  signatures: WorkOrderSignatureRecord[]
  technicianSignature: string
  setTechnicianSignature: (value: string) => void
  clientSignature: string
  setClientSignature: (value: string) => void
}) {
  const techExisting = signatures.find((item) => item.signatureType.toLowerCase() === 'technician')
  const clientExisting = signatures.find((item) => item.signatureType.toLowerCase() === 'client')

  return (
    <section className="surface-card rounded-[24px] p-4">
      <div>
        <p className="text-lg font-semibold text-app">Signatures</p>
        <p className="mt-1 text-sm text-muted">Capture both technician and client sign-off in a clear, reviewable layout.</p>
      </div>
      <div className="mt-4 grid gap-4 xl:grid-cols-2">
        <SignatureCard
          title="Technician signature"
          existing={techExisting}
          value={technicianSignature}
          onChange={setTechnicianSignature}
        />
        <SignatureCard
          title="Client signature"
          existing={clientExisting}
          value={clientSignature}
          onChange={setClientSignature}
        />
      </div>
    </section>
  )
}

function SignatureCard({
  title,
  existing,
  value,
  onChange,
}: {
  title: string
  existing?: WorkOrderSignatureRecord
  value: string
  onChange: (value: string) => void
}) {
  return (
    <article className="rounded-[22px] border border-app bg-[var(--app-surface)] p-4">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-semibold text-app">{title}</p>
        <Badge tone={existing ? 'success' : 'neutral'}>{existing ? 'Saved' : 'Pending'}</Badge>
      </div>
      <div className="mt-4">
        <WorkOrderSignaturePad value={value || existing?.signatureDataUrl || ''} onChange={onChange} height={150} />
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <div className="rounded-[18px] border border-app bg-[var(--app-subtle)] px-4 py-3">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Name</p>
          <p className="mt-2 text-sm text-app">{existing?.signerName || 'Awaiting capture'}</p>
        </div>
        <div className="rounded-[18px] border border-app bg-[var(--app-subtle)] px-4 py-3">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Timestamp</p>
          <p className="mt-2 text-sm text-app">{existing?.capturedAt || 'Not yet saved'}</p>
        </div>
      </div>
      <div className="mt-4 flex flex-wrap justify-end gap-3">
        <button type="button" className="button-secondary">
          Clear
        </button>
        <button type="button" className="button-primary">
          <Signature className="size-4" />
          Save signature
        </button>
      </div>
    </article>
  )
}

export function ServiceReportSection({
  workOrder,
  execution,
  onGenerate,
  onDownload,
}: {
  workOrder: WorkOrder
  execution?: WorkOrderExecutionBundle | null
  onGenerate?: () => void
  onDownload?: () => void
}) {
  const hasPreview = hasServiceReport(execution)
  const closed = isClosedStatus(workOrder.status)

  return (
    <section className="surface-card rounded-[18px] p-3.5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-base font-semibold text-app">Service report</p>
          <p className="mt-1 text-xs text-muted">Only the report actions valid for the current state are shown.</p>
        </div>
        <Badge tone={closed ? 'success' : hasPreview ? 'info' : 'neutral'}>
          {closed ? 'Closed' : hasPreview ? 'Draft generated' : 'Not generated'}
        </Badge>
      </div>

      <div className="mt-3 rounded-[16px] border border-app bg-[var(--app-subtle)] px-3.5 py-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold text-app">Service Report - {workOrder.workOrderNumber}</p>
            <p className="mt-1 text-xs text-muted">{execution?.reportPreview?.siteLabel || workOrder.siteName || 'Site to be confirmed'}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {!hasPreview ? (
              <button type="button" className="button-primary" onClick={onGenerate}>
                <FileText className="size-4" />
                Generate report
              </button>
            ) : (
              <button type="button" className="button-secondary" onClick={onDownload}>
                <Download className="size-4" />
                Download PDF
              </button>
            )}
          </div>
        </div>
      </div>
    </section>
  )
}

export function MobilePrimaryActionBar({
  actions,
  onAction,
  menuActions,
}: {
  actions: WorkflowActionMeta[]
  onAction: (action: WorkflowActionMeta) => void
  menuActions: MenuAction[]
}) {
  if (actions.length === 0 && menuActions.length === 0) {
    return null
  }

  return (
    <div className="fixed inset-x-0 bottom-[max(0.125rem,env(safe-area-inset-bottom))] z-40 border-t border-app bg-[rgba(255,255,255,0.94)] px-3 py-2 pb-[max(0.25rem,env(safe-area-inset-bottom))] backdrop-blur lg:hidden" data-testid="tech-bottom-bar">
      <div className="mx-auto flex max-w-xl items-center gap-2">
        <div className="flex flex-1 flex-wrap gap-2" data-testid="workflow-action-bar">
          {actions.map((action) => (
            <WorkflowActionButton key={action.kind} action={action} className="flex-1" onClick={() => onAction(action)} />
          ))}
        </div>
        {menuActions.length > 0 ? <MoreActionsMenu actions={menuActions} /> : null}
      </div>
    </div>
  )
}

export function SummaryInfoCard({
  label,
  value,
}: {
  label: string
  value: ReactNode
}) {
  return (
    <div className="rounded-[18px] border border-app bg-[var(--app-subtle)] px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <p className="mt-2 text-sm font-medium text-app">{value}</p>
    </div>
  )
}

export function ContextPrimaryButton({
  workOrder,
  onAction,
}: {
  workOrder: WorkOrder
  onAction?: (meta: WorkflowActionMeta) => void
}) {
  const actions = resolveWorkflowActions(workOrder)
  const primaryAction = actions[0]
  const overflowActions = actions.slice(1)

  return (
    <div className="flex flex-wrap items-center gap-2" data-testid="workflow-actions">
      {primaryAction ? <WorkflowActionButton action={primaryAction} onClick={() => onAction?.(primaryAction)} /> : null}
      {overflowActions.length > 0 ? (
        <MoreActionsMenu
          actions={overflowActions.map((action) => ({
            label: action.label,
            onClick: () => onAction?.(action),
          }))}
        />
      ) : null}
    </div>
  )
}

function WorkflowActionButton({
  action,
  onClick,
  className = '',
}: {
  action: WorkflowActionMeta
  onClick: () => void
  className?: string
}) {
  const icon =
    action.kind === 'start' ? <Wrench className="size-4" />
      : action.kind === 'hold' ? <PauseCircle className="size-4" />
        : action.kind === 'resume' ? <Play className="size-4" />
          : action.kind === 'complete' ? <Check className="size-4" />
            : <FileText className="size-4" />

  const toneClass = action.tone === 'secondary' ? 'button-secondary' : 'button-primary'

  return (
    <button type="button" className={`${toneClass} min-h-[36px] px-3 py-2 ${className}`.trim()} onClick={onClick}>
      {icon}
      {action.label}
    </button>
  )
}

export function HoldReasonModal({
  open,
  reason,
  note,
  busy,
  onReasonChange,
  onNoteChange,
  onClose,
  onConfirm,
}: {
  open: boolean
  reason: string
  note: string
  busy?: boolean
  onReasonChange: (value: string) => void
  onNoteChange: (value: string) => void
  onClose: () => void
  onConfirm: () => void
}) {
  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Put Work Order On Hold"
      description="Choose the hold reason and add any context needed before pausing this work order."
      maxWidth="max-w-lg"
    >
      <div className="space-y-5">
        <label className="block space-y-2">
          <span className="text-sm font-medium text-app">Reason</span>
          <select value={reason} onChange={(event) => onReasonChange(event.target.value)} className="field-input">
            <option value="">Select reason</option>
            {holdReasonOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>

        <label className="block space-y-2">
          <span className="text-sm font-medium text-app">Note</span>
          <textarea
            value={note}
            onChange={(event) => onNoteChange(event.target.value)}
            className="field-input min-h-[120px]"
            placeholder="Add more details"
          />
        </label>

        <div className="flex flex-wrap justify-end gap-3">
          <button type="button" className="button-secondary" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="button-primary" disabled={!reason || busy} onClick={onConfirm}>
            {busy ? 'Putting On Hold...' : 'Put On Hold'}
          </button>
        </div>
      </div>
    </Modal>
  )
}

export function HoldReasonSummary({ workOrder }: { workOrder: Pick<WorkOrder, 'holdReason' | 'holdNote'> }) {
  if (!workOrder.holdReason) {
    return null
  }

  return (
    <div className="rounded-[14px] border border-[rgba(180,126,0,0.18)] bg-[rgba(255,248,220,0.9)] px-3 py-2.5" data-testid="hold-reason-summary">
      <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-[#7a5a00]">Hold reason</p>
      <p className="mt-1.5 text-sm font-medium text-app" data-testid="hold-reason-value">{formatHoldReasonLabel(workOrder.holdReason)}</p>
      {workOrder.holdNote ? <p className="mt-1.5 text-xs text-secondary" data-testid="hold-reason-note">{workOrder.holdNote}</p> : null}
    </div>
  )
}
