import { ArrowLeft, PackagePlus, Save, Signature } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { DataTableWrapper } from '../../components/ui/DataTableWrapper'
import { DetailRows, type DescriptionListItem } from '../../components/ui/DescriptionList'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageTabs, SectionPanel, StatusBadge } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { materialService } from '../../services/materialService'
import { settingsService } from '../../services/settingsService'
import { technicianService } from '../../services/technicianService'
import { workOrderService } from '../../services/workOrderService'
import type {
  AssignmentGroupRecord,
  MaterialItem,
  TechnicianRecord,
  WorkOrder,
  WorkOrderEventRecord,
  WorkOrderExecutionBundle,
} from '../../types/api'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { isTechnicianOnlyWorkspaceUser } from '../../utils/roles'
import {
  EvidenceSection,
  HoldReasonModal,
  HoldReasonSummary,
  MoreActionsMenu,
  PriorityBadge,
  ServiceReportSection,
  WorkflowStatusBadge,
  canTransitionTo,
  formatWorkTypeLabel,
  hasServiceReport,
  resolveWorkflowStatus,
} from './workOrderWorkflowUi'
import { WorkOrderSignaturePad } from './WorkOrderSignaturePad'
import { renderBillingModelBadge, renderContractStatusBadge, renderCoverageStatusBadge, renderRenewalRiskBadge } from '../contracts/contractUi'

type WorkOrderDetailPayload = {
  workOrder: WorkOrder | null
  execution: WorkOrderExecutionBundle | null
  materials: MaterialItem[]
  events: WorkOrderEventRecord[]
  relatedAssetWorkOrders: WorkOrder[]
  assignmentGroups: AssignmentGroupRecord[]
  technicians: TechnicianRecord[]
}

type PrimaryAction =
  | { kind: 'assign-schedule'; label: string; tone?: 'primary' | 'secondary' }
  | { kind: 'start' | 'hold' | 'resume' | 'complete'; label: string; tone?: 'primary' | 'secondary' }
  | { kind: 'view-report'; label: string; tone?: 'primary' | 'secondary' }

type WorkOrderTab =
  | 'summary'
  | 'timeline'
  | 'assignment'
  | 'work-execution'
  | 'evidence'
  | 'materials'
  | 'signatures'
  | 'report'

const workOrderTabs: Array<{ id: WorkOrderTab; label: string }> = [
  { id: 'summary', label: 'Summary' },
  { id: 'timeline', label: 'Timeline' },
  { id: 'assignment', label: 'Assignment' },
  { id: 'work-execution', label: 'Work Execution' },
  { id: 'evidence', label: 'Evidence' },
  { id: 'materials', label: 'Materials' },
  { id: 'signatures', label: 'Signatures' },
  { id: 'report', label: 'Report' },
]

const workOrderTabAnchors: Record<WorkOrderTab, string> = {
  summary: 'summary',
  timeline: 'timeline',
  assignment: 'assignment',
  'work-execution': 'work-execution',
  evidence: 'evidence',
  materials: 'materials',
  signatures: 'signatures',
  report: 'report',
}

const emptyPayload: WorkOrderDetailPayload = {
  workOrder: null,
  execution: null,
  materials: [],
  events: [],
  relatedAssetWorkOrders: [],
  assignmentGroups: [],
  technicians: [],
}

export function WorkOrderDetailPage() {
  const { id = '' } = useParams()
  const { session } = useAuth()
  const { pushToast } = useToast()
  const hydratedWorkOrderIdRef = useRef<string | null>(null)
  const [activeTab, setActiveTab] = useState<WorkOrderTab>('summary')
  const [assignmentEditorOpen, setAssignmentEditorOpen] = useState(false)
  const [busy, setBusy] = useState<string | null>(null)
  const [selectedFiles, setSelectedFiles] = useState<File[]>([])
  const [evidenceCaptions, setEvidenceCaptions] = useState<string[]>([])
  const [materialItemId, setMaterialItemId] = useState('')
  const [materialQty, setMaterialQty] = useState(1)
  const [materialNotes, setMaterialNotes] = useState('')
  const [findings, setFindings] = useState('')
  const [workDone, setWorkDone] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [technicianSignerName, setTechnicianSignerName] = useState('')
  const [technicianSignerRole, setTechnicianSignerRole] = useState('Technician')
  const [technicianSignature, setTechnicianSignature] = useState('')
  const [clientSignerName, setClientSignerName] = useState('')
  const [clientSignerRole, setClientSignerRole] = useState('Client Representative')
  const [clientSignature, setClientSignature] = useState('')
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdReason, setHoldReason] = useState('')
  const [holdNote, setHoldNote] = useState('')
  const [progressComment, setProgressComment] = useState('')
  const [selectedTechnicianIds, setSelectedTechnicianIds] = useState<string[]>([])
  const canManageAssignments = Boolean(
    session
    && !isTechnicianOnlyWorkspaceUser(session.role, session.title, session.permissions)
    && (session.permissions?.canAssignWorkOrders || session.role === 'tenantadmin' || session.role === 'admin'),
  )
  const canViewPricing = Boolean(
    session
    && !isTechnicianOnlyWorkspaceUser(session.role, session.title, session.permissions)
    && (
      session.permissions?.canViewFinance
      || session.permissions?.canManageFinance
      || session.permissions?.canViewFinanceRates
      || session.permissions?.canManageFinanceRates
      || session.permissions?.canViewBillableWork
      || session.permissions?.canManageBillableWork
      || session.role === 'tenantadmin'
      || session.role === 'admin'
    ),
  )

  const { data, loading, error, reload } = useAsyncData<WorkOrderDetailPayload>(
    async (signal) => {
      const workOrder = await workOrderService.get(id, signal)
      const [execution, materials, events, relatedAssetWorkOrders, assignmentGroups, technicians] = await Promise.all([
        workOrderService.getExecution(id, signal),
        materialService.list({ branchId: workOrder.branchId, signal }),
        workOrderService.getEvents(id, signal),
        workOrder.assetId
          ? workOrderService.list(workOrder.branchId, signal).then((items) => items.filter((item) => item.assetId === workOrder.assetId && item.id !== workOrder.id).slice(0, 6))
          : Promise.resolve([] as WorkOrder[]),
        canManageAssignments
          ? settingsService.listAssignmentGroups(signal).catch(() => [] as AssignmentGroupRecord[])
          : Promise.resolve([] as AssignmentGroupRecord[]),
        technicianService.list(workOrder.branchId, signal).catch(() => [] as TechnicianRecord[]),
      ])
      return { workOrder, execution, materials, events, relatedAssetWorkOrders, assignmentGroups, technicians }
    },
    emptyPayload,
    [canManageAssignments, id],
  )

  useEffect(() => {
    const workOrder = data.workOrder
    if (!workOrder) return
    if (hydratedWorkOrderIdRef.current === workOrder.id) return

    hydratedWorkOrderIdRef.current = workOrder.id
    setActiveTab('summary')
    setAssignmentEditorOpen(false)
    setBusy(null)
    setSelectedFiles([])
    setMaterialItemId('')
    setMaterialQty(1)
    setMaterialNotes('')
    setHoldOpen(false)
    setHoldReason('')
    setHoldNote('')
    setProgressComment('')
    setFindings(data.execution?.notes?.findings || '')
    setWorkDone(data.execution?.notes?.workDone || workOrder.workDoneNotes || '')
    setScheduledAt(toDateTimeLocalValue(workOrder.dueDate))
    const technicianExisting = data.execution?.signatures.find((item) => item.signatureType.toLowerCase() === 'technician')
    const clientExisting = data.execution?.signatures.find((item) => item.signatureType.toLowerCase() === 'client')
    setTechnicianSignerName(technicianExisting?.signerName || workOrder.leadTechnicianName || workOrder.assignedTechnicianName || '')
    setTechnicianSignerRole(technicianExisting?.signerRole || 'Technician')
    setTechnicianSignature(technicianExisting?.signatureDataUrl || '')
    setClientSignerName(clientExisting?.signerName || '')
    setClientSignerRole(clientExisting?.signerRole || 'Client Representative')
    setClientSignature(clientExisting?.signatureDataUrl || '')
    setSelectedTechnicianIds(getAssignedTechnicianIds(workOrder))
  }, [data.execution, data.workOrder])

  async function handlePrimaryAction(kind: 'start' | 'hold' | 'resume' | 'complete') {
    const workOrder = data.workOrder
    if (!workOrder) return

    if (kind === 'hold') {
      setHoldOpen(true)
      return
    }

    if (kind === 'start' && (!workOrder.assignmentGroupId || !hasAssignedTechnicians(workOrder))) {
      pushToast({
        title: 'Assignment required',
        description: 'Assign a group and at least one technician before starting work.',
        tone: 'warning',
      })
      setActiveTab('assignment')
      setAssignmentEditorOpen(true)
      return
    }

    setBusy(kind)
    try {
      if (kind === 'start') {
        await workOrderService.start(workOrder.id)
      } else if (kind === 'resume') {
        await workOrderService.resume(workOrder.id)
      } else if (kind === 'complete') {
        await workOrderService.complete(workOrder.id, {
          workDoneNotes: workDone.trim() || workOrder.workDoneNotes || 'Completed from work order detail.',
          reportSummary: workDone.trim() || workOrder.workDoneNotes || 'Completed from work order detail.',
          technicianId: workOrder.leadTechnicianId || workOrder.assignedTechnicianId || null,
        })
      }

      pushToast({
        title: 'Work order updated',
        description: 'The workflow moved forward successfully.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Action failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to update this work order.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function submitHold() {
    const workOrder = data.workOrder
    if (!workOrder || !holdReason) return

    setBusy('hold')
    try {
      await workOrderService.pause(workOrder.id, {
        holdReason,
        note: holdNote.trim() || null,
      })
      pushToast({
        title: 'Work order placed on hold',
        description: 'The hold reason was saved successfully.',
        tone: 'success',
      })
      setHoldOpen(false)
      setHoldReason('')
      setHoldNote('')
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Hold failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to place this work order on hold.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function assignGroup(nextGroupId: string) {
    const workOrder = data.workOrder
    if (!workOrder) return

    setBusy('assign-group')
    try {
      await workOrderService.assignGroup(workOrder.id, {
        assignmentGroupId: nextGroupId || null,
        notes: null,
      })
      setSelectedTechnicianIds([])
      pushToast({
        title: 'Assignment group updated',
        description: nextGroupId ? 'The work order was routed to the selected assignment group.' : 'The assignment group was cleared.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Assignment failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to update the assignment group.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function assignTechnician() {
    const workOrder = data.workOrder
    if (!workOrder || !workOrder.assignmentGroupId || selectedTechnicianIds.length === 0) return

    const leadTechnicianId = resolveLeadTechnicianId(workOrder, selectedTechnicianIds)
    setBusy('assign-technician')
    try {
      await workOrderService.assignTechnicians(workOrder.id, {
        technicianIds: selectedTechnicianIds,
        leadTechnicianId,
        notes: null,
      })
      pushToast({
        title: 'Assignment saved',
        description: selectedTechnicianIds.length === 1
          ? '1 technician is now assigned to this work order.'
          : `${selectedTechnicianIds.length} technicians are now assigned to this work order.`,
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Assignment failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to assign the selected technicians.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function saveSchedule() {
    const workOrder = data.workOrder
    if (!workOrder) return

    setBusy('save-schedule')
    try {
      await workOrderService.update(workOrder.id, buildWorkOrderUpdateInput(workOrder, {
        dueDate: parseDateTimeLocalValue(scheduledAt),
        status: scheduledAt && canTransitionTo(workOrder, 'Scheduled') ? 'Scheduled' : undefined,
      }))
      pushToast({
        title: 'Schedule saved',
        description: 'The scheduled date / due date was updated.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Schedule failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to update the schedule.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function saveNotes() {
    const workOrder = data.workOrder
    if (!workOrder) return

    setBusy('save-notes')
    try {
      await workOrderService.saveExecutionNotes(workOrder.id, { findings, workDone })
      pushToast({
        title: 'Completion summary saved',
        description: 'Findings and work done were updated.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Save failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to save the completion summary.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function saveProgressUpdate() {
    const workOrder = data.workOrder
    if (!workOrder || !progressComment.trim()) return
    if (workOrder.status === 'Completed' || workOrder.status === 'Closed') {
      pushToast({
        title: 'Progress updates locked',
        description: 'Progress updates become read-only after the work order is completed or closed.',
        tone: 'warning',
      })
      return
    }

    setBusy('save-progress')
    try {
      await workOrderService.saveExecutionNotes(workOrder.id, { progressComment: progressComment.trim() })
      pushToast({
        title: 'Progress update saved',
        description: 'The work order stayed active and the update was added to its history.',
        tone: 'success',
      })
      setProgressComment('')
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Save failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to save the progress update.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function uploadEvidence() {
    const workOrder = data.workOrder
    if (!workOrder || selectedFiles.length === 0) return

    setBusy('upload-evidence')
    try {
      for (const [index, file] of selectedFiles.entries()) {
        await workOrderService.uploadPhoto(workOrder.id, {
          file,
          caption: evidenceCaptions[index]?.trim() || file.name,
          category: 'During',
          includeInReport: true,
        })
      }
      pushToast({
        title: 'Evidence uploaded',
        description: selectedFiles.length === 1 ? 'The file was attached to the work order.' : `${selectedFiles.length} files were attached to the work order.`,
        tone: 'success',
      })
      setSelectedFiles([])
      setEvidenceCaptions([])
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Upload failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to upload evidence.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function addMaterial() {
    const workOrder = data.workOrder
    if (!workOrder || !materialItemId || materialQty <= 0) return

    setBusy('add-material')
    try {
      await workOrderService.addMaterialUsage(workOrder.id, {
        materialItemId,
        quantityUsed: materialQty,
        chargeable: true,
        notes: materialNotes || null,
        assetId: workOrder.assetId || null,
      })
      pushToast({
        title: 'Material added',
        description: 'Material usage was recorded.',
        tone: 'success',
      })
      setMaterialItemId('')
      setMaterialQty(1)
      setMaterialNotes('')
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Material failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to record material usage.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function saveSignature(signatureType: 'Technician' | 'Client') {
    const workOrder = data.workOrder
    if (!workOrder) return

    const isTech = signatureType === 'Technician'
    const signerName = isTech ? technicianSignerName : clientSignerName
    const signerRole = isTech ? technicianSignerRole : clientSignerRole
    const signatureDataUrl = isTech ? technicianSignature : clientSignature

    if (!signerName.trim() || !signatureDataUrl) {
      pushToast({
        title: 'Signature required',
        description: 'Name and signature are required before saving.',
        tone: 'warning',
      })
      return
    }

    setBusy(`signature-${signatureType.toLowerCase()}`)
    try {
      await workOrderService.captureSignature(workOrder.id, {
        signatureType,
        signerName: signerName.trim(),
        signerRole: signerRole.trim(),
        signatureDataUrl,
      })
      pushToast({
        title: `${signatureType} signature saved`,
        description: 'The sign-off was attached to the work order.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Signature failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to save the signature.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function generateReport() {
    const workOrder = data.workOrder
    if (!workOrder) return

    setBusy('generate-report')
    try {
      await workOrderService.generateServiceReport(workOrder.id)
      pushToast({
        title: 'Service report generated',
        description: 'The latest report preview is ready.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Generate failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to generate the service report.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function downloadReport() {
    const workOrder = data.workOrder
    if (!workOrder) return

    setBusy('download-report')
    try {
      await workOrderService.downloadServiceReportPdf(workOrder.id)
      pushToast({
        title: 'PDF downloaded',
        description: 'The service report PDF is downloading.',
        tone: 'success',
      })
    } catch (nextError) {
      pushToast({
        title: 'Download failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to download the PDF.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function markClientAccepted() {
    const workOrder = data.workOrder
    if (!workOrder || !canTransitionTo(workOrder, 'Client Accepted')) return

    const signerName = clientSignerName.trim()
      || data.execution?.signatures.find((item) => item.signatureType.toLowerCase() === 'client')?.signerName?.trim()
      || workOrder.clientContactPerson?.trim()

    if (!signerName) {
      pushToast({
        title: 'Client sign-off required',
        description: 'Capture the client signer name before marking the work order as client accepted.',
        tone: 'warning',
      })
      return
    }

    setBusy('client-accept')
    try {
      await workOrderService.acknowledge(workOrder.id, {
        acknowledgedByName: signerName,
        comments: clientSignature ? 'Client sign-off captured from work order detail.' : null,
      })
      pushToast({
        title: 'Client acceptance saved',
        description: 'The work order was moved to Client Accepted.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Client acceptance failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to mark the work order as client accepted.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function closeWorkOrder() {
    const workOrder = data.workOrder
    if (!workOrder || !canTransitionTo(workOrder, 'Closed')) return

    setBusy('close')
    try {
      await workOrderService.close(workOrder.id)
      pushToast({
        title: 'Work order closed',
        description: 'The record is now locked for close-out.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Close failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to close the work order.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  async function reopenWorkOrder() {
    const workOrder = data.workOrder
    if (!workOrder || !canTransitionTo(workOrder, 'Reopened')) return

    setBusy('reopen')
    try {
      await workOrderService.reopen(workOrder)
      pushToast({
        title: 'Work order reopened',
        description: 'The work order has been returned to active dispatch flow.',
        tone: 'success',
      })
      await reload()
    } catch (nextError) {
      pushToast({
        title: 'Reopen failed',
        description: nextError instanceof Error ? nextError.message : 'Unable to reopen the work order.',
        tone: 'danger',
      })
    } finally {
      setBusy(null)
    }
  }

  function focusAssignment() {
    setActiveTab('assignment')
    setAssignmentEditorOpen(true)
  }

  function focusCompletion() {
    setActiveTab('report')
  }

  if (loading) {
    return <LoadingState label="Loading work order details" />
  }

  if (error || !data.workOrder) {
    return <ErrorState title="Unable to load work order" description={error || 'The selected work order could not be found.'} />
  }

  const workOrder = data.workOrder
  const canAddProgressUpdates = workOrder.status !== 'Completed' && workOrder.status !== 'Closed'
  const workflowStatus = resolveWorkflowStatus(workOrder)
  const technicianNames = getAssignedTechnicianNames(workOrder)
  const reportAvailable = hasServiceReport(data.execution)
  const reportStatus = workOrder.status?.toLowerCase().includes('closed')
    ? 'Closed'
    : reportAvailable
      ? 'Generated'
      : 'Pending'
  const reportTone = reportStatus === 'Closed' ? 'success' : reportStatus === 'Generated' ? 'info' : 'neutral'
  const selectedAssignmentGroup = data.assignmentGroups.find((group) => group.id === workOrder.assignmentGroupId) ?? null
  const availableGroupMembers = (selectedAssignmentGroup?.members ?? []).filter((member) => member.isActive)
  const selectedLeadTechnicianId = resolveLeadTechnicianId(workOrder, selectedTechnicianIds)
  const primaryPanel = resolvePrimaryActionPanel(workOrder, canManageAssignments)
  const completionCounts = {
    photos: data.execution?.photos.length ?? 0,
    materials: data.execution?.materialUsages.length ?? 0,
    signatures: data.execution?.signatures.length ?? 0,
  }
  const checklistSections = Object.values(
    (workOrder.checklistItems ?? []).reduce<Record<string, { key: string; title: string; items: NonNullable<WorkOrder['checklistItems']> }>>((acc, item) => {
      const title = item.sectionName?.trim() || 'General'
      const key = `${item.sectionOrder ?? 0}-${title}`
      if (!acc[key]) {
        acc[key] = { key, title, items: [] }
      }
      acc[key].items.push(item)
      return acc
    }, {}),
  )
  const summaryItems: DescriptionListItem[] = [
    { label: 'Work order type', value: formatWorkTypeLabel(workOrder.workType) },
    { label: 'SLA state', value: <StatusBadge tone={reportTone}>{friendlyValue(workOrder.slaStatus, 'Pending')}</StatusBadge> },
    { label: 'System / service', value: friendlyValue(workOrder.systemName, 'Not set') },
    { label: 'Assignment group', value: friendlyValue(workOrder.assignmentGroupName, 'Not assigned') },
    { label: 'Assigned technicians', value: technicianNames },
    { label: 'Assignment status', value: friendlyValue(workOrder.assignmentStatus, 'Unassigned') },
    { label: 'Created', value: formatDateTime(workOrder.createdAt) },
    { label: 'Started', value: workOrder.workStartedAt ? formatDateTime(workOrder.workStartedAt) : 'Not started' },
  ]
  const contractItems: DescriptionListItem[] = [
    { label: 'Contract number', value: friendlyValue(workOrder.contractNumber, 'No contract applied') },
    { label: 'Contract status', value: workOrder.contractStatus ? renderContractStatusBadge(workOrder.contractStatus) : 'Not covered' },
    { label: 'Coverage status', value: workOrder.coverageStatus ? renderCoverageStatusBadge(workOrder.coverageStatus) : 'Not covered' },
    { label: 'Billing model', value: workOrder.billingModel ? renderBillingModelBadge(workOrder.billingModel) : 'Default tenant billing' },
    { label: 'Rate card', value: friendlyValue(workOrder.rateCardName, 'Tenant default / fallback') },
    { label: 'Response SLA', value: workOrder.slaResponseDeadline ? formatDateTime(workOrder.slaResponseDeadline) : 'Not configured' },
    { label: 'Resolution SLA', value: workOrder.slaResolutionDeadline ? formatDateTime(workOrder.slaResolutionDeadline) : 'Not configured' },
    { label: 'Contract end date', value: workOrder.contractEndDate ? formatDateOnly(workOrder.contractEndDate) : '-' },
    { label: 'Renewal risk', value: workOrder.contractRenewalRisk ? renderRenewalRiskBadge(workOrder.contractRenewalRisk) : '-' },
    { label: 'Pricing warnings', value: friendlyValue(workOrder.pricingNotes, 'No pricing warning recorded') },
    { label: 'Estimated charge', value: canViewPricing ? (workOrder.estimatedCharge != null ? workOrder.estimatedCharge.toLocaleString() : '-') : 'Restricted' },
    { label: 'Final charge', value: canViewPricing ? (workOrder.finalCharge != null ? workOrder.finalCharge.toLocaleString() : '-') : 'Restricted' },
  ]
  const lifecycleItems: DescriptionListItem[] = [
    { label: 'Created', value: formatDateTime(workOrder.createdAt) },
    { label: 'Started', value: workOrder.workStartedAt ? formatDateTime(workOrder.workStartedAt) : 'Not started' },
    { label: 'Completed', value: workOrder.completedAt ? formatDateTime(workOrder.completedAt) : 'Not completed' },
    { label: 'Client accepted', value: workOrder.acknowledgementDate ? formatDateTime(workOrder.acknowledgementDate) : 'Pending' },
  ]
  const assetItems: DescriptionListItem[] = [
    { label: 'Asset name', value: friendlyValue(workOrder.assetName, 'Not linked') },
    { label: 'Category', value: friendlyValue(workOrder.assetCategoryName, 'Not set') },
    { label: 'Tag / serial', value: friendlyValue(workOrder.assetSerialNumber || workOrder.assetCode, 'Not set') },
    { label: 'Location', value: friendlyValue(workOrder.assetLocation, 'Not set') },
    { label: 'Criticality', value: friendlyValue(workOrder.assetCriticality, 'Not configured') },
    { label: 'Warranty', value: friendlyValue(workOrder.assetWarrantyStatus, 'Not configured') },
    { label: 'Last service', value: workOrder.assetLastServiceDate ? formatDateTime(workOrder.assetLastServiceDate) : 'Not recorded' },
    { label: 'Next PM', value: workOrder.assetNextServiceDate ? formatDateTime(workOrder.assetNextServiceDate) : 'Not scheduled' },
  ]
  const siteItems: DescriptionListItem[] = [
    { label: 'Client', value: friendlyValue(workOrder.clientName, 'Not linked') },
    { label: 'Client contact', value: friendlyValue(workOrder.clientContactPerson, 'Not set') },
    { label: 'Client phone', value: friendlyValue(workOrder.clientContactPhone, 'Not set') },
    { label: 'Contract / SLA', value: friendlyValue(workOrder.clientSlaPlan || workOrder.slaStatus, 'Not configured') },
    { label: 'Site', value: friendlyValue(workOrder.siteName, 'Not linked') },
    { label: 'Site contact', value: friendlyValue(workOrder.siteContactPerson, 'Not set') },
    { label: 'Site phone', value: friendlyValue(workOrder.siteContactPhone, 'Not set') },
    { label: 'Operating hours', value: friendlyValue(workOrder.siteOperatingHours, 'Not recorded') },
    { label: 'Site location', value: friendlyValue(workOrder.siteLocation, 'Not recorded') },
    { label: 'Access notes', value: friendlyValue(workOrder.siteAccessNotes, 'No access notes') },
  ]
  const assignmentItems: DescriptionListItem[] = [
    { label: 'Assignment group', value: friendlyValue(workOrder.assignmentGroupName, 'Not assigned') },
    { label: 'Assigned technician(s)', value: technicianNames },
    { label: 'Scheduled date / time', value: workOrder.dueDate ? formatDateTime(workOrder.dueDate) : 'Not scheduled' },
    { label: 'Assignment status', value: friendlyValue(workOrder.assignmentStatus, 'Unassigned') },
  ]
  const executionSummaryItems: DescriptionListItem[] = [
    { label: 'Evidence files', value: String(completionCounts.photos) },
    { label: 'Materials recorded', value: String(completionCounts.materials) },
    { label: 'Signatures captured', value: String(completionCounts.signatures) },
  ]
  const reportMetaItems: DescriptionListItem[] = [
    { label: 'Report status', value: <StatusBadge tone={reportTone}>{reportStatus}</StatusBadge> },
    { label: 'Report title', value: data.execution?.reportPreview?.title || 'Service report pending' },
    { label: 'Generated', value: data.execution?.reportPreview?.generatedAtLabel || 'Not generated' },
  ]
  const lifecycleActions = resolveDetailLifecycleActions(workOrder)
  const secondaryWorkflowActions = primaryPanel.actions.slice(1)
  const workflowMenuActions = secondaryWorkflowActions.map((action) => ({
    label: action.label,
    onClick: () => {
      if (action.kind === 'assign-schedule') {
        focusAssignment()
        return
      }
      if (action.kind === 'view-report') {
        focusCompletion()
        return
      }
      void handlePrimaryAction(action.kind)
    },
  }))

  return (
    <div className="space-y-3 pb-20 lg:pb-8">
      <div className="space-y-3" data-testid="work-order-summary-card">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-2 min-w-0">
            <div className="flex flex-wrap items-center gap-2" data-testid="workflow-actions">
              <Link to="/work-orders" className="button-secondary h-8 px-3 py-1.5 text-xs">
                <ArrowLeft className="size-4" />
                Back to Work Orders
              </Link>
              <span className="text-sm font-semibold tracking-[-0.02em] text-app">{workOrder.workOrderNumber}</span>
              <WorkflowStatusBadge workOrder={workOrder} />
              <PriorityBadge priority={workOrder.priority} />
            </div>

            <div className="min-w-0">
              <h1 className="truncate text-[1.05rem] font-semibold tracking-[-0.03em] text-app sm:text-[1.15rem]">
                {friendlyValue(workOrder.title, 'Untitled work order')}
              </h1>
            </div>

            <p className="text-xs leading-5 text-muted">
              <span className="font-medium text-app">{workflowStatus}:</span> {primaryPanel.headline}
              {friendlyValue(workOrder.assignmentNotes, '') ? ` ${friendlyValue(workOrder.assignmentNotes, '')}` : ''}
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {lifecycleActions.map((action) => (
              <PrimaryActionButton
                key={action.kind}
                action={action}
                busy={busy}
                className="h-8 px-3 py-1.5 text-xs"
                onWorkflowAction={(kind) => void handlePrimaryAction(kind)}
                onAssignSchedule={focusAssignment}
                onViewReport={focusCompletion}
              />
            ))}
            {workflowMenuActions.length > 0 ? <MoreActionsMenu actions={workflowMenuActions} /> : null}
          </div>
        </div>

        <div
          className="flex flex-wrap gap-x-4 gap-y-2 rounded-[14px] border border-[var(--app-divider)] bg-[var(--app-subtle)] px-3 py-2 text-xs text-muted"
          data-testid="work-order-metadata-strip"
        >
          <p><span className="font-medium text-app">Client:</span> {friendlyValue(workOrder.clientName, 'Not selected')}</p>
          <p><span className="font-medium text-app">Site:</span> {friendlyValue(workOrder.siteName, 'Not selected')}</p>
          <p><span className="font-medium text-app">Asset:</span> {friendlyValue(workOrder.assetName, 'Not selected')}</p>
          <p><span className="font-medium text-app">Status:</span> <WorkflowStatusBadge workOrder={workOrder} /></p>
          <p><span className="font-medium text-app">Assigned:</span> {friendlyValue(workOrder.assignmentGroupName, 'No group')} / {technicianNames}</p>
          <p><span className="font-medium text-app">Due:</span> {workOrder.dueDate ? formatDateTime(workOrder.dueDate) : 'Not scheduled'}</p>
        </div>
      </div>

      <div className="space-y-2">
        <div className="overflow-x-auto pb-0.5" data-testid="work-order-tabs">
          <PageTabs tabs={workOrderTabs} activeTab={activeTab} onChange={setActiveTab} />
        </div>

        {workflowStatus === 'On Hold' ? <HoldReasonSummary workOrder={workOrder} /> : null}
      </div>

      <div className="space-y-4">
        <div className="min-w-0 space-y-3">
          {activeTab === 'summary' ? (
            <div id={workOrderTabAnchors.summary} className="space-y-3" data-testid="job-details-section">
              <SectionPanel title="Overview" description="Core work order details, operational state, and delivery context." className="p-3.5">
                <DetailRows items={summaryItems} columns={2} />
              </SectionPanel>

              <SectionPanel title="Contract & SLA" description="Contract coverage, billing behavior, and SLA snapshot retained on this work order." className="p-3.5">
                <DetailRows items={contractItems} columns={2} />
              </SectionPanel>

              <SectionPanel title="Lifecycle" description="Key timestamps for dispatch, execution, and customer close-out." className="p-3.5">
                <DetailRows items={lifecycleItems} columns={2} />
              </SectionPanel>

              <SectionPanel title="Problem / Scope" description="Original request and service notes carried with the work order." className="p-3.5">
                <p className="text-sm leading-7 text-secondary">{friendlyValue(workOrder.description, 'No detailed problem statement has been recorded yet.')}</p>
              </SectionPanel>
            </div>
          ) : null}

          {activeTab === 'summary' ? (
            <div className="grid gap-4 xl:grid-cols-2" data-testid="asset-site-tab">
              <SectionPanel title="Asset record" description="Core asset attributes and maintenance context linked to this job." className="p-3.5">
                <DetailRows items={assetItems} columns={2} />
              </SectionPanel>

              <SectionPanel title="Client and site" description="Customer, site access, and service context for field execution." className="p-3.5">
                <DetailRows items={siteItems} columns={2} />
              </SectionPanel>

              <SectionPanel title="Related work orders" description="Recent jobs already recorded against the same asset." className="p-3.5">
                {data.relatedAssetWorkOrders.length > 0 ? (
                  <DataTableWrapper>
                    <table className="w-full min-w-[620px]">
                      <thead className="table-head">
                        <tr>
                          {['Work Order', 'Title', 'Status', 'Date'].map((header) => (
                            <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {data.relatedAssetWorkOrders.map((item) => (
                          <tr key={item.id} className="table-row border-t border-[var(--app-divider)]">
                            <td className="px-4 py-3 text-sm font-semibold text-app">
                              <Link to={`/work-orders/${item.id}`} className="hover:underline">{item.workOrderNumber}</Link>
                            </td>
                            <td className="px-4 py-3 text-sm text-secondary">{item.title}</td>
                            <td className="px-4 py-3"><WorkflowStatusBadge workOrder={item} /></td>
                            <td className="px-4 py-3 text-sm text-muted">{item.completedAt ? `Completed ${formatDateTime(item.completedAt)}` : `Created ${formatDateTime(item.createdAt)}`}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </DataTableWrapper>
                ) : (
                  <EmptyState title="No related work orders" description="No other work orders for this asset were found in the current branch scope." />
                )}
              </SectionPanel>

              <SectionPanel title="Known issues and field context" description="Notes carried forward for dispatch and on-site execution." className="p-3.5">
                <p className="text-sm leading-7 text-secondary">
                  {friendlyValue(workOrder.jobCardNotes || workOrder.description, 'No known issues or technician notes are recorded yet.')}
                </p>
              </SectionPanel>
            </div>
          ) : null}

          {activeTab === 'assignment' ? (
            <div className="space-y-3" data-testid="assignment-card">
              <div className="grid gap-4 xl:grid-cols-2">
                <SectionPanel title="Assignment summary" description="Current dispatch ownership, schedule, and technician coverage." className="p-3.5">
                  <DetailRows items={assignmentItems} columns={2} />
                </SectionPanel>

                <SectionPanel title="Dispatch notes" description="Use this area for routing and visit planning without crowding execution details." className="p-3.5">
                  <p className="text-sm leading-7 text-secondary">
                    Keep scheduling, routing, and technician allocation here so execution screens stay focused on field delivery.
                  </p>
                </SectionPanel>
              </div>

              {canManageAssignments ? (
                <SectionPanel
                  title="Edit assignment"
                  description="Update the assignment group, confirm service time, and select the active technician team."
                  id="assignment-section"
                  data-testid="assignment-section"
                  className="p-3.5"
                  action={(
                    <button type="button" className="button-secondary" onClick={() => setAssignmentEditorOpen((current) => !current)}>
                      {assignmentEditorOpen ? 'Done' : 'Edit assignment'}
                    </button>
                  )}
                >
                  {assignmentEditorOpen ? (
                    <div className="space-y-5" data-testid="assignment-section">
                      <label className="space-y-2" data-testid="assignment-group-control">
                        <span className="text-sm font-medium text-app">Assignment group</span>
                        <select
                          value={workOrder.assignmentGroupId || ''}
                          onChange={(event) => void assignGroup(event.target.value)}
                          className="field-input"
                          disabled={busy === 'assign-group'}
                        >
                          <option value="">Not assigned</option>
                          {data.assignmentGroups.filter((group) => group.isActive).map((group) => (
                            <option key={group.id} value={group.id}>{group.name}</option>
                          ))}
                        </select>
                      </label>

                      <div className="space-y-2">
                        <span className="text-sm font-medium text-app">Scheduled date / time</span>
                        <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_auto]">
                          <input
                            type="datetime-local"
                            value={scheduledAt}
                            onChange={(event) => setScheduledAt(event.target.value)}
                            className="field-input"
                          />
                          <button type="button" className="button-secondary" onClick={() => void saveSchedule()} disabled={busy === 'save-schedule'}>
                            {busy === 'save-schedule' ? 'Saving...' : 'Save schedule'}
                          </button>
                        </div>
                        <p className="text-xs text-muted">The existing due date field is being used as the confirmed service visit time for this phase.</p>
                      </div>

                      <div className="space-y-3" data-testid="assignment-technician-control">
                        <span className="text-sm font-medium text-app">Assigned technician(s)</span>
                        <div className="rounded-[16px] bg-[var(--app-surface)]" data-testid="assignment-technician-list">
                          {workOrder.assignmentGroupId && availableGroupMembers.length > 0 ? (
                            <div className="divide-y divide-[var(--app-divider)]">
                              {availableGroupMembers.map((member) => {
                                const technicianId = member.technicianId || ''
                                const checked = selectedTechnicianIds.includes(technicianId)
                                const label = member.memberName || member.email || 'Technician'

                                return (
                                  <label key={member.id} className="flex items-center gap-3 px-4 py-3 text-sm text-app">
                                    <input
                                      type="checkbox"
                                      checked={checked}
                                      disabled={busy === 'assign-technician'}
                                      onChange={() => {
                                        if (!technicianId) return
                                        setSelectedTechnicianIds((current) => toggleTechnicianSelection(current, technicianId))
                                      }}
                                    />
                                    <span className="flex-1">{label}</span>
                                    {selectedLeadTechnicianId === technicianId ? <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Lead</span> : null}
                                  </label>
                                )
                              })}
                            </div>
                          ) : (
                            <p className="px-4 py-4 text-sm text-muted">
                              {!workOrder.assignmentGroupId
                                ? 'Assign a group before selecting technicians.'
                                : 'No active technicians are configured in this assignment group yet.'}
                            </p>
                          )}
                        </div>

                        {selectedTechnicianIds.length > 0 ? (
                          <div className="rounded-[16px] bg-[var(--app-surface)] px-4 py-3" data-testid="assignment-selected-technicians">
                            <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-muted">Selected team</p>
                            <div className="mt-3 space-y-2">
                              {availableGroupMembers
                                .filter((member) => member.technicianId && selectedTechnicianIds.includes(member.technicianId))
                                .map((member) => (
                                  <div key={member.id} className="flex items-center justify-between gap-3 text-sm text-app">
                                    <span>{member.memberName || member.email || 'Technician'}</span>
                                    {selectedLeadTechnicianId === member.technicianId ? <span className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">Lead technician</span> : null}
                                  </div>
                                ))}
                            </div>
                          </div>
                        ) : null}

                        <button
                          type="button"
                          data-testid="assignment-save-button"
                          className="button-primary w-full justify-center"
                          onClick={() => void assignTechnician()}
                          disabled={!workOrder.assignmentGroupId || selectedTechnicianIds.length === 0 || busy === 'assign-technician'}
                        >
                          {busy === 'assign-technician' ? 'Saving assignment...' : 'Save assignment'}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <p className="text-sm text-muted">Open the assignment editor when you need to change routing, schedule, or technician coverage.</p>
                  )}
                </SectionPanel>
              ) : null}
            </div>
          ) : null}

          {activeTab === 'work-execution' ? (
            <div id={workOrderTabAnchors['work-execution']} className="space-y-3" data-testid="execution-tab">
              <div className="flex flex-wrap gap-2">
                {executionSummaryItems.map((item) => (
                  <div key={item.label} className="inline-flex items-center gap-2 rounded-full border border-[var(--app-divider)] bg-[var(--app-subtle)] px-3 py-1.5 text-xs text-app">
                    <span className="font-medium text-muted">{item.label}</span>
                    <span className="font-semibold">{item.value}</span>
                  </div>
                ))}
              </div>

              <SectionPanel
                title="Execution notes"
                description="Capture what was found and what was done while keeping the field record structured."
                className="p-3.5"
                action={(
                  <button type="button" className="button-primary h-9 px-3 py-2 text-xs" onClick={() => void saveNotes()} disabled={busy === 'save-notes'}>
                    <Save className="size-4" />
                    {busy === 'save-notes' ? 'Saving...' : 'Save execution'}
                  </button>
                )}
              >
                <div className="grid gap-3 xl:grid-cols-2">
                  <label className="space-y-2">
                    <span className="text-sm font-medium text-app">Diagnosis / findings</span>
                    <textarea
                      value={findings}
                      onChange={(event) => setFindings(event.target.value)}
                      className="field-input min-h-[132px]"
                      placeholder="Describe what the technician found on site"
                    />
                  </label>

                  <label className="space-y-2">
                    <span className="text-sm font-medium text-app">Work performed</span>
                    <textarea
                      value={workDone}
                      onChange={(event) => setWorkDone(event.target.value)}
                      className="field-input min-h-[132px]"
                      placeholder="Describe the corrective or planned work completed"
                    />
                  </label>
                </div>
              </SectionPanel>

              <SectionPanel title="Progress updates" description="Capture multi-day field progress without changing the lifecycle state." className="p-3.5">
                <label className="block space-y-2">
                  <span className="text-sm font-medium text-app">Progress comment</span>
                  <textarea
                    value={progressComment}
                    onChange={(event) => setProgressComment(event.target.value)}
                    className="field-input min-h-[118px]"
                    placeholder="Day 1: inspected pump, isolated failure, awaiting seal kit."
                    disabled={!canAddProgressUpdates}
                  />
                </label>

                {!canAddProgressUpdates ? (
                  <p className="mt-3 text-sm text-muted">Progress updates are read-only after the work order is completed or closed.</p>
                ) : null}

                <div className="mt-3 flex justify-end">
                  <button
                    type="button"
                    className="button-primary h-9 px-3 py-2 text-xs"
                    onClick={() => void saveProgressUpdate()}
                    disabled={!canAddProgressUpdates || !progressComment.trim() || busy === 'save-progress'}
                  >
                    {busy === 'save-progress' ? 'Saving...' : 'Save update'}
                  </button>
                </div>

                <div className="mt-3 space-y-2.5">
                  {(data.execution?.progressUpdates ?? []).map((item) => (
                    <article key={item.id} className="rounded-[16px] bg-[var(--app-subtle)] px-3 py-2.5">
                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{item.actorUserName || 'Team update'}</p>
                        <span className="text-xs text-muted">{formatDateTime(item.occurredAt)}</span>
                      </div>
                      <p className="mt-1.5 text-sm text-app">{item.message}</p>
                    </article>
                  ))}
                  {(data.execution?.progressUpdates?.length ?? 0) === 0 ? (
                    <div className="rounded-[16px] border border-dashed border-[var(--app-border)] px-3 py-4 text-center text-sm text-muted">
                      No progress updates have been saved yet.
                    </div>
                  ) : null}
                </div>
              </SectionPanel>
            </div>
          ) : null}

          {activeTab === 'work-execution' ? (
            <div className="space-y-3" data-testid="checklist-tab">
              {checklistSections.length > 0 ? (
              <div className="space-y-3">
                  {checklistSections.map((section) => (
                    <SectionPanel key={section.key} title={section.title} description="Checklist responses saved with this work order." className="p-3.5">
                      <div className="divide-y divide-[var(--app-divider)]">
                        {section.items
                          .sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0))
                          .map((item) => (
                            <article key={item.id} className="py-4 first:pt-0 last:pb-0">
                              <div className="flex flex-wrap items-start justify-between gap-3">
                                <div>
                                  <p className="text-sm font-medium text-app">{item.questionText}</p>
                                  <p className="mt-1 text-xs text-muted">
                                    {item.isRequired ? 'Required' : 'Optional'} â€¢ {item.inputType}
                                    {item.evidenceRequired ? ' â€¢ Evidence required' : ''}
                                  </p>
                                </div>
                                <StatusBadge tone={item.isCompleted ? 'success' : 'default'}>{item.isCompleted ? 'Completed' : 'Pending'}</StatusBadge>
                              </div>
                              <DetailRows
                                className="mt-4"
                                columns={2}
                                items={[
                                  { label: 'Response', value: friendlyValue(item.responseValue, 'No response yet') },
                                  { label: 'Remarks', value: friendlyValue(item.remarks, 'No remarks') },
                                ]}
                              />
                            </article>
                          ))}
                      </div>
                    </SectionPanel>
                  ))}
                </div>
              ) : (
                <EmptyState title="No checklist attached" description="No checklist has been attached to this work order yet." />
              )}
            </div>
          ) : null}

          {activeTab === 'materials' ? (
            <div id={workOrderTabAnchors.materials} className="space-y-3" data-testid="materials-tab">
              <SectionPanel title="Record material usage" description="Capture materials consumed against this work order." className="p-3.5">
                <div>
                  <div className="flex items-center gap-2">
                    <PackagePlus className="size-4 text-[var(--app-primary-strong)]" />
                    <p className="text-sm font-semibold text-app">Add material</p>
                  </div>

                  <div className="mt-4 grid gap-3 xl:grid-cols-[minmax(0,1.2fr)_120px_minmax(0,1fr)_auto]">
                    <select value={materialItemId} onChange={(event) => setMaterialItemId(event.target.value)} className="field-input">
                      <option value="">Select material</option>
                      {data.materials.map((item) => (
                        <option key={item.id} value={item.id}>{item.itemName} ({item.quantityOnHand} {item.unitOfMeasure})</option>
                      ))}
                    </select>
                    <input type="number" min={1} value={materialQty} onChange={(event) => setMaterialQty(Number(event.target.value))} className="field-input" />
                    <input value={materialNotes} onChange={(event) => setMaterialNotes(event.target.value)} className="field-input" placeholder="Notes" />
                    <button type="button" className="button-primary" onClick={() => void addMaterial()} disabled={busy === 'add-material'}>
                      {busy === 'add-material' ? 'Adding...' : 'Add material'}
                    </button>
                  </div>
                </div>
              </SectionPanel>

              <SectionPanel title="Recorded materials" description="Inventory usage already posted to the work order history." className="p-3.5">
                <DataTableWrapper className="mt-1">
                  <table className="w-full min-w-[680px]">
                      <thead className="table-head">
                        <tr>
                          {['Material', 'Quantity', 'Unit', 'Source / store', 'Notes'].map((header) => (
                            <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                          ))}
                        </tr>
                      </thead>
                      <tbody>
                        {(data.execution?.materialUsages ?? []).map((item) => (
                          <tr key={item.id} className="table-row border-t border-[var(--app-divider)]">
                            <td className="px-4 py-3 text-sm text-app">{friendlyValue(item.materialName, 'Not recorded')}</td>
                            <td className="px-4 py-3 text-sm text-app">{item.quantityUsed}</td>
                            <td className="px-4 py-3 text-sm text-app">{friendlyValue(item.unitOfMeasure, 'Not recorded')}</td>
                            <td className="px-4 py-3 text-sm text-app">{friendlyValue(item.assetName, 'Main store')}</td>
                            <td className="px-4 py-3 text-sm text-muted">{friendlyValue(item.notes, 'No notes')}</td>
                          </tr>
                        ))}
                        {(data.execution?.materialUsages.length ?? 0) === 0 ? (
                          <tr>
                            <td colSpan={5} className="px-4 py-8 text-center text-sm text-muted">No materials used have been recorded yet.</td>
                          </tr>
                        ) : null}
                      </tbody>
                  </table>
                </DataTableWrapper>
              </SectionPanel>
            </div>
          ) : null}

          {activeTab === 'evidence' ? (
            <div id={workOrderTabAnchors.evidence} className="space-y-3" data-testid="evidence-tab">
              <EvidenceSection
                photos={data.execution?.photos ?? []}
                selectedFiles={selectedFiles}
                captions={evidenceCaptions}
                onCaptionsChange={setEvidenceCaptions}
                onSelectFiles={(files) => {
                  setSelectedFiles(files)
                  setEvidenceCaptions((current) => files.map((_, index) => current[index] ?? ''))
                }}
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
              <div className="mt-4 flex justify-end">
                <button type="button" className="button-primary" onClick={() => void uploadEvidence()} disabled={selectedFiles.length === 0 || busy === 'upload-evidence'}>
                  {busy === 'upload-evidence' ? 'Uploading...' : 'Upload evidence'}
                </button>
              </div>
            </div>
          ) : null}

          {activeTab === 'signatures' ? (
            <div id={workOrderTabAnchors.signatures} className="space-y-3" data-testid="client-signoff-tab">
              <SectionPanel title="Close-out summary" description="Evidence, materials, and signatures captured before acceptance." className="p-3.5">
                <DetailRows items={executionSummaryItems} columns={3} />
              </SectionPanel>

              <SectionPanel title="Signatures" description="Capture technician and client acknowledgement without leaving the work order." className="p-3.5">
                <div className="flex items-center gap-2">
                  <Signature className="size-4 text-[var(--app-primary-strong)]" />
                  <p className="text-sm font-semibold text-app">Sign-off records</p>
                </div>

                <div className="mt-4 grid gap-5 xl:grid-cols-2">
                  <SignatureCaptureCard
                    title="Technician signature"
                    name={technicianSignerName}
                    setName={setTechnicianSignerName}
                    role={technicianSignerRole}
                    setRole={setTechnicianSignerRole}
                    signature={technicianSignature}
                    setSignature={setTechnicianSignature}
                    timestamp={data.execution?.signatures.find((item) => item.signatureType.toLowerCase() === 'technician')?.capturedAt}
                    onSave={() => void saveSignature('Technician')}
                    busy={busy === 'signature-technician'}
                  />

                  <SignatureCaptureCard
                    title="Client sign-off"
                    name={clientSignerName}
                    setName={setClientSignerName}
                    role={clientSignerRole}
                    setRole={setClientSignerRole}
                    signature={clientSignature}
                    setSignature={setClientSignature}
                    timestamp={data.execution?.signatures.find((item) => item.signatureType.toLowerCase() === 'client')?.capturedAt}
                    onSave={() => void saveSignature('Client')}
                    busy={busy === 'signature-client'}
                  />
                </div>
              </SectionPanel>

              <SectionPanel title="Close-out actions" description="Only actions valid for the current lifecycle state are shown here." className="p-3.5">
                <div className="mt-4 flex flex-wrap gap-3">
                  {canTransitionTo(workOrder, 'Client Accepted') ? (
                    <button type="button" className="button-primary" onClick={() => void markClientAccepted()} disabled={busy === 'client-accept'}>
                      {busy === 'client-accept' ? 'Saving...' : 'Mark Client Accepted'}
                    </button>
                  ) : null}
                  {canTransitionTo(workOrder, 'Closed') ? (
                    <button type="button" className="button-secondary" onClick={() => void closeWorkOrder()} disabled={busy === 'close'}>
                      {busy === 'close' ? 'Closing...' : 'Close Work Order'}
                    </button>
                  ) : null}
                  {canTransitionTo(workOrder, 'Reopened') ? (
                    <button type="button" className="button-secondary" onClick={() => void reopenWorkOrder()} disabled={busy === 'reopen'}>
                      {busy === 'reopen' ? 'Reopening...' : 'Reopen Work Order'}
                    </button>
                  ) : null}
                  {!canTransitionTo(workOrder, 'Client Accepted') && !canTransitionTo(workOrder, 'Closed') && !canTransitionTo(workOrder, 'Reopened') ? (
                    <p className="text-sm text-muted">No additional close-out action is available for the current status.</p>
                  ) : null}
                </div>
              </SectionPanel>
            </div>
          ) : null}

          {activeTab === 'report' ? (
            <div id={workOrderTabAnchors.report} className="space-y-3" data-testid="report-tab">
              <ServiceReportSection
                workOrder={workOrder}
                execution={data.execution}
                onGenerate={() => void generateReport()}
                onDownload={() => void downloadReport()}
              />

              <SectionPanel title="Report metadata" description="Current report generation state and delivery snapshot." className="p-3.5">
                <DetailRows items={reportMetaItems} columns={3} />
              </SectionPanel>

              <div className="grid gap-4 xl:grid-cols-2">
                <SectionPanel title="Problem reported" description="Original problem statement captured on intake." className="p-3.5">
                  <p className="text-sm leading-6 text-secondary">{friendlyValue(workOrder.description, 'No problem statement recorded.')}</p>
                </SectionPanel>

                <SectionPanel title="Work performed" description="Completion summary that will feed the final service report." className="p-3.5">
                  <p className="text-sm leading-6 text-secondary">{friendlyValue(workDone || workOrder.workDoneNotes, 'No work summary recorded yet.')}</p>
                </SectionPanel>
              </div>
            </div>
          ) : null}

          {activeTab === 'timeline' ? (
            <SectionPanel title="Work order history" description="Operational audit trail for lifecycle changes and execution updates." data-testid="history-tab" className="p-3.5">
              {data.events.length > 0 ? (
                <DataTableWrapper>
                  <table className="w-full min-w-[860px]">
                    <thead className="table-head">
                      <tr>
                        {['When', 'Actor', 'Event', 'Transition', 'Reason / note'].map((header) => (
                          <th key={header} className="px-4 py-3 text-left text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{header}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {data.events.map((item) => (
                        <tr key={item.id} className="table-row border-t border-[var(--app-divider)]">
                          <td className="px-4 py-3 text-sm text-muted">{formatDateTime(item.occurredAt)}</td>
                          <td className="px-4 py-3 text-sm font-medium text-app">{item.actorUserName || 'System event'}</td>
                          <td className="px-4 py-3 text-sm text-secondary">{item.message}</td>
                          <td className="px-4 py-3 text-sm text-app">
                            {item.oldStatus || item.newStatus ? `${item.oldStatus || 'N/A'} -> ${item.newStatus || 'N/A'}` : '-'}
                          </td>
                          <td className="px-4 py-3 text-sm text-muted">{item.reason || item.note || '-'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </DataTableWrapper>
              ) : (
                <EmptyState title="No history yet" description="No work order history has been recorded yet." />
              )}
            </SectionPanel>
          ) : null}
          </div>
        </div>
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

function resolveDetailLifecycleActions(workOrder: WorkOrder): PrimaryAction[] {
  const workflowStatus = resolveWorkflowStatus(workOrder)
  const canStart = canTransitionTo(workOrder, 'In Progress')

  switch (workflowStatus) {
    case 'New':
    case 'Open':
    case 'Assigned':
    case 'Scheduled':
    case 'Reopened':
      return canStart ? [{ kind: 'start', label: 'Start Work' }] : []
    case 'In Progress':
      return [
        { kind: 'complete', label: 'Complete' },
        { kind: 'hold', label: 'Put On Hold', tone: 'secondary' },
      ]
    case 'On Hold':
      return [{ kind: 'resume', label: 'Resume' }]
    default:
      return []
  }
}

function PrimaryActionButton({
  action,
  busy,
  onWorkflowAction,
  onAssignSchedule,
  onViewReport,
  className = '',
}: {
  action: PrimaryAction
  busy: string | null
  onWorkflowAction: (kind: 'start' | 'hold' | 'resume' | 'complete') => void
  onAssignSchedule: () => void
  onViewReport: () => void
  className?: string
}) {
  const isWorkflowAction = action.kind === 'start' || action.kind === 'hold' || action.kind === 'resume' || action.kind === 'complete'
  const isBusy = isWorkflowAction ? busy === action.kind : false

  return (
    <button
      type="button"
      className={`${action.tone === 'secondary' ? 'button-secondary' : 'button-primary'} ${className}`.trim()}
      disabled={isBusy}
      onClick={() => {
        if (action.kind === 'assign-schedule') {
          onAssignSchedule()
          return
        }
        if (action.kind === 'view-report') {
          onViewReport()
          return
        }
        onWorkflowAction(action.kind)
      }}
    >
      {isBusy && isWorkflowAction ? actionBusyLabel(action.kind) : action.label}
    </button>
  )
}

function SignatureCaptureCard({
  title,
  name,
  setName,
  role,
  setRole,
  signature,
  setSignature,
  timestamp,
  onSave,
  busy,
}: {
  title: string
  name: string
  setName: (value: string) => void
  role: string
  setRole: (value: string) => void
  signature: string
  setSignature: (value: string) => void
  timestamp?: string | null
  onSave: () => void
  busy: boolean
}) {
  return (
    <article className="section-panel">
      <p className="text-sm font-semibold text-app">{title}</p>

      <div className="mt-4">
        <WorkOrderSignaturePad value={signature} onChange={setSignature} height={150} />
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <label className="space-y-2">
          <span className="text-sm font-medium text-app">Name</span>
          <input value={name} onChange={(event) => setName(event.target.value)} className="field-input" placeholder="Signer name" />
        </label>

        <label className="space-y-2">
          <span className="text-sm font-medium text-app">Role</span>
          <input value={role} onChange={(event) => setRole(event.target.value)} className="field-input" placeholder="Signer role" />
        </label>
      </div>

      <div className="mt-4 rounded-[14px] bg-[var(--app-subtle)] px-4 py-3">
        <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Timestamp</p>
        <p className="mt-2 text-sm text-app">{timestamp ? formatDateTime(timestamp) : 'Not yet saved'}</p>
      </div>

      <div className="mt-4 flex justify-end gap-3">
        <button type="button" className="button-secondary" onClick={() => setSignature('')}>Clear</button>
        <button type="button" className="button-primary" onClick={onSave} disabled={busy}>
          {busy ? 'Saving...' : 'Save signature'}
        </button>
      </div>
    </article>
  )
}

function resolvePrimaryActionPanel(workOrder: WorkOrder, canManageAssignments: boolean) {
  const workflowStatus = resolveWorkflowStatus(workOrder)
  const assigned = Boolean(workOrder.assignmentGroupId && hasAssignedTechnicians(workOrder))
  const canStart = canTransitionTo(workOrder, 'In Progress')

  switch (workflowStatus) {
    case 'New':
    case 'Open':
    case 'Reopened':
      if (assigned && canStart) {
        return {
          headline: 'Work is assigned and ready to start.',
          description: 'The job already has a group and technician. Start work when the team is ready to begin.',
          actions: [{ kind: 'start', label: 'Start Work' }] as PrimaryAction[],
        }
      }
      return {
        headline: canManageAssignments ? 'Assign the work order and confirm the service time.' : 'Awaiting assignment from dispatch.',
        description: canManageAssignments
          ? 'Route the job to the right group, add technicians, and confirm when it should happen.'
          : 'This work order is still waiting for dispatch to assign it.',
        actions: canManageAssignments ? [{ kind: 'assign-schedule', label: 'Assign / Schedule' }] as PrimaryAction[] : [] as PrimaryAction[],
      }
    case 'Assigned':
    case 'Scheduled':
      return {
        headline: 'Work is assigned and ready to start.',
        description: 'Start work when the technician is on site and ready to begin.',
        actions: canStart ? [{ kind: 'start', label: 'Start Work' }] as PrimaryAction[] : [] as PrimaryAction[],
      }
    case 'In Progress':
      return {
        headline: 'Work is active.',
        description: 'Either complete the job or place it on hold with a reason.',
        actions: [
          { kind: 'complete', label: 'Complete' },
          { kind: 'hold', label: 'Put On Hold', tone: 'secondary' },
        ] as PrimaryAction[],
      }
    case 'On Hold':
      return {
        headline: 'The job is on hold.',
        description: 'Resume work once the blocker has been cleared.',
        actions: [{ kind: 'resume', label: 'Resume Work' }] as PrimaryAction[],
      }
    case 'Completed':
      return {
        headline: 'Work is complete.',
        description: 'Review the close-out details and service report.',
        actions: [{ kind: 'view-report', label: 'View Service Report' }] as PrimaryAction[],
      }
    case 'Client Accepted':
      return {
        headline: 'Client sign-off is recorded.',
        description: 'Review the report and close the work order when the final admin checks are complete.',
        actions: [{ kind: 'view-report', label: 'View Service Report', tone: 'secondary' }] as PrimaryAction[],
      }
    case 'Closed':
      return {
        headline: 'Workflow complete.',
        description: 'This work order is closed and no further workflow action is available here.',
        actions: [] as PrimaryAction[],
      }
    case 'Cancelled':
    case 'Rejected':
      return {
        headline: 'Follow the next valid lifecycle step.',
        description: 'Use the work order tabs to review context and complete the next approved action for this status.',
        actions: [] as PrimaryAction[],
      }
  }
}

function actionBusyLabel(kind: 'start' | 'hold' | 'resume' | 'complete') {
  switch (kind) {
    case 'start':
      return 'Starting...'
    case 'hold':
      return 'Putting On Hold...'
    case 'resume':
      return 'Resuming...'
    case 'complete':
      return 'Completing...'
  }
}

function friendlyValue(value: string | null | undefined, fallback: string) {
  return value && value.trim() ? value : fallback
}

function getAssignedTechnicianNames(workOrder: WorkOrder | null) {
  if (!workOrder) return 'Not assigned'

  const names = new Set<string>()
  for (const assignment of workOrder.technicianAssignments ?? []) {
    if (assignment.technicianName?.trim()) {
      names.add(assignment.technicianName.trim())
    }
  }

  if (workOrder.leadTechnicianName?.trim()) {
    names.add(workOrder.leadTechnicianName.trim())
  }
  if (workOrder.assignedTechnicianName?.trim()) {
    names.add(workOrder.assignedTechnicianName.trim())
  }

  return names.size > 0 ? Array.from(names).join(', ') : 'Not assigned'
}

function getAssignedTechnicianIds(workOrder: WorkOrder | null) {
  if (!workOrder) return []

  const ids = new Set<string>()
  for (const assignment of workOrder.technicianAssignments ?? []) {
    if (assignment.technicianId) {
      ids.add(assignment.technicianId)
    }
  }
  for (const technicianId of workOrder.assignedTechnicianIds ?? []) {
    if (technicianId) {
      ids.add(technicianId)
    }
  }
  if (workOrder.leadTechnicianId) {
    ids.add(workOrder.leadTechnicianId)
  }
  if (workOrder.assignedTechnicianId) {
    ids.add(workOrder.assignedTechnicianId)
  }

  return Array.from(ids)
}

function hasAssignedTechnicians(workOrder: WorkOrder | null) {
  return getAssignedTechnicianIds(workOrder).length > 0
}

function resolveLeadTechnicianId(workOrder: WorkOrder | null, technicianIds: string[]) {
  if (technicianIds.length === 0) return null
  if (workOrder?.leadTechnicianId && technicianIds.includes(workOrder.leadTechnicianId)) {
    return workOrder.leadTechnicianId
  }
  return technicianIds[0]
}

function toggleTechnicianSelection(current: string[], technicianId: string) {
  if (current.includes(technicianId)) {
    return current.filter((item) => item !== technicianId)
  }
  return [...current, technicianId]
}

function toDateTimeLocalValue(value?: string | null) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  const hours = `${date.getHours()}`.padStart(2, '0')
  const minutes = `${date.getMinutes()}`.padStart(2, '0')
  return `${year}-${month}-${day}T${hours}:${minutes}`
}

function parseDateTimeLocalValue(value: string) {
  if (!value.trim()) return null
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? null : date.toISOString()
}

function buildWorkOrderUpdateInput(workOrder: WorkOrder, overrides?: Partial<{ dueDate: string | null; status: string | null }>) {
  return {
    clientId: workOrder.clientId,
    branchId: workOrder.branchId,
    siteId: workOrder.siteId,
    systemId: workOrder.systemId,
    assetId: workOrder.assetId,
    assignmentGroupId: workOrder.assignmentGroupId,
    assignmentType: workOrder.assignmentType,
    assignedTechnicianId: workOrder.assignedTechnicianId,
    assignedTechnicianIds: workOrder.assignedTechnicianIds,
    leadTechnicianId: workOrder.leadTechnicianId,
    assignmentNotes: workOrder.assignmentNotes,
    title: workOrder.title,
    description: workOrder.description,
    priority: workOrder.priority,
    status: overrides?.status ?? null,
    dueDate: overrides?.dueDate ?? workOrder.dueDate ?? null,
    workType: workOrder.workType,
    isPreventiveMaintenance: workOrder.isPreventiveMaintenance,
    pmTemplateId: workOrder.pmTemplateId,
  }
}








