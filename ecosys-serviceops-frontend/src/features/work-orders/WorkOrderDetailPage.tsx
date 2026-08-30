import { useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, MoreVertical } from 'lucide-react'
import { cn } from '../../shared/utils/cn'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { Surface } from '../../shared/components/Surface'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { MobileActionBar } from '../../shared/components/MobileActionBar'
import { SummarySection } from '../../shared/pages/ModulePage'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import {
  useCancelWorkOrder,
  useCloseWorkOrder,
  useCompleteWorkOrder,
  useDuplicateWorkOrder,
  useHoldWorkOrder,
  usePatchWorkOrderWorkflowStatus,
  useResumeFromHold,
  useStartWorkOrder,
  useWorkOrder,
} from './api'
import {
  COMPLETE_BLOCKED_REASON,
  START_BLOCKED_REASON,
  hasAcceptedTechnician,
  hasRecordedArrival,
} from './dispatchGuards'
import { CompletionOverrideFields } from './components/CompletionOverrideFields'
import { useWorkflowStatuses } from '../settings/workflowsApi'
import { TimelineTab } from './tabs/TimelineTab'
import { ActivityTab } from './tabs/ActivityTab'
import { NotesTab } from './tabs/NotesTab'
import { DocumentsPanel } from './components/DocumentsPanel'
import { RelatedWorkOrdersPanel } from './components/RelatedWorkOrdersPanel'
import { TechnicianLocationPanel } from './components/TechnicianLocationPanel'
import { AssignmentTab } from './tabs/AssignmentTab'
import { ExecutionTab } from './tabs/ExecutionTab'
import { EvidenceTab } from './tabs/EvidenceTab'
import { PartsTab } from './tabs/PartsTab'
import { SignaturesTab } from './tabs/SignaturesTab'
import { ReportTab } from './tabs/ReportTab'
import { SlaTab } from './tabs/SlaTab'
import { SubtasksTab } from './tabs/SubtasksTab'

const holdTypeOptions = [
  { value: 'OnHold', label: 'On Hold (general)' },
  { value: 'WaitingOnParts', label: 'Waiting on Parts' },
  { value: 'WaitingOnClient', label: 'Waiting on Client' },
] as const

const summaryFields: Array<{ key: string; label: string; render?: (value: string) => string }> = [
  { key: 'workOrderNumber', label: 'WO Number' },
  { key: 'workType', label: 'Type' },
  { key: 'priority', label: 'Priority' },
  { key: 'status', label: 'Status' },
  { key: 'clientName', label: 'Client' },
  { key: 'siteName', label: 'Site' },
  { key: 'assetName', label: 'Asset' },
  { key: 'assignmentSummary', label: 'Assigned To' },
  { key: 'dueDate', label: 'Due Date', render: (value) => formatDate(value) },
  { key: 'createdAt', label: 'Created', render: (value) => formatDateTime(value) },
  { key: 'slaStatus', label: 'SLA Status' },
  { key: 'branchName', label: 'Branch' },
]

/** Lightweight in-tab navigation used to group related sub-tabs under one top-level tab. */
function SubTabbed({ tabs }: { tabs: Array<{ id: string; label: string; content: ReactNode }> }) {
  const [active, setActive] = useState(tabs[0]?.id)
  const current = tabs.find((tab) => tab.id === active) ?? tabs[0]
  if (!current) return null
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            onClick={() => setActive(tab.id)}
            className={cn(
              'inline-flex h-9 items-center rounded-full border px-3 text-xs font-medium transition',
              tab.id === current.id
                ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white'
                : 'border-[var(--app-border)] bg-white text-slate-600 hover:bg-[var(--app-primary-soft)]',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
      <div>{current.content}</div>
    </div>
  )
}

export function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { data: workOrder, isLoading, isError } = useWorkOrder(id)
  const { data: workflowStatuses } = useWorkflowStatuses()
  const patchWorkflowStatus = usePatchWorkOrderWorkflowStatus()

  const canStart = usePermission('work-orders.start')
  const canExecute = usePermission('work-orders.execute')
  const canComplete = usePermission('work-orders.complete')
  const canClose = usePermission('work-orders.close')
  const canCancel = usePermission('work-orders.cancel')
  const canCreate = usePermission('work-orders.create')

  const startWorkOrder = useStartWorkOrder(id ?? '')
  const holdWorkOrder = useHoldWorkOrder(id ?? '')
  const resumeFromHold = useResumeFromHold(id ?? '')
  const closeWorkOrder = useCloseWorkOrder(id ?? '')
  const cancelWorkOrder = useCancelWorkOrder(id ?? '')
  const completeWorkOrder = useCompleteWorkOrder(id ?? '')
  const duplicateWorkOrder = useDuplicateWorkOrder(id ?? '')

  const [completeOpen, setCompleteOpen] = useState(false)
  const [workDoneNotes, setWorkDoneNotes] = useState('')
  const [reportSummary, setReportSummary] = useState('')
  const [overrideEnabled, setOverrideEnabled] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')
  const [confirmAction, setConfirmAction] = useState<'close' | 'cancel' | null>(null)
  const [showActionsMenu, setShowActionsMenu] = useState(false)
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdType, setHoldType] = useState<(typeof holdTypeOptions)[number]['value']>('OnHold')
  const [holdReason, setHoldReason] = useState('')

  const submitDuplicate = async () => {
    try {
      const duplicate = await duplicateWorkOrder.mutateAsync()
      toast.success(`Work order ${duplicate.workOrderNumber} created`)
      navigate(`/work-orders/${duplicate.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const submitHold = async () => {
    try {
      await holdWorkOrder.mutateAsync({ holdType, reason: holdReason.trim() || null })
      toast.success('Work order put on hold')
      setHoldOpen(false)
      setHoldReason('')
      setHoldType('OnHold')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const runAction = async (label: string, mutateAsync: () => Promise<unknown>) => {
    try {
      await mutateAsync()
      toast.success(label)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  // Mirrors of the two server guards. `HasAcceptedTechnicianAsync` gates Start and `HasArrivalAsync`
  // gates Complete; both read `work_order_technician_assignments`, which the work order response
  // carries as `technicianAssignments`.
  const assignments = workOrder?.technicianAssignments ?? []
  const technicianAccepted = hasAcceptedTechnician(assignments)
  const arrivalRecorded = hasRecordedArrival(assignments)
  // `WorkOrdersController.Complete` checks `IsAdmin`, which is the tenant `admin` role specifically
  // (HttpTenantContext.IsAdmin => HasRole(AppRoles.Admin)), so no other role may override.
  const isTenantAdmin = user?.role === 'admin'
  const overrideOffered = isTenantAdmin && !arrivalRecorded
  const overrideReady = arrivalRecorded || (overrideEnabled && overrideReason.trim().length > 0)

  const closeComplete = () => {
    setCompleteOpen(false)
    setWorkDoneNotes('')
    setReportSummary('')
    setOverrideEnabled(false)
    setOverrideReason('')
  }

  const submitComplete = async () => {
    if (!workDoneNotes.trim()) {
      toast.error('Work done notes are required')
      return
    }
    if (!overrideReady) {
      toast.error('Confirm the admin override and give a reason before completing without an arrival')
      return
    }
    try {
      await completeWorkOrder.mutateAsync({
        workDoneNotes: workDoneNotes.trim(),
        reportSummary: reportSummary.trim() || null,
        adminOverride: overrideOffered && overrideEnabled,
        overrideReason: overrideOffered && overrideEnabled ? overrideReason.trim() : null,
      })
      toast.success('Work order completed')
      closeComplete()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading work order" />
  }

  if (isError || !workOrder || !id) {
    return <EmptyState title="Work order unavailable" description="The record could not be loaded." />
  }

  const status = workOrder.status
  const holdStatuses = ['On Hold', 'Waiting on Parts', 'Waiting on Client']
  const canPause = canExecute && !holdStatuses.includes(status) && !['Completed', 'Closed', 'Cancelled'].includes(status)
  const canResume = canExecute && holdStatuses.includes(status)
  const canStartNow = canStart && ['Assigned', 'Scheduled', 'New', 'Open'].includes(status)
  // Completion is only a legal transition from In Progress (mirrors the backend state machine);
  // offering it earlier surfaces an action the server rejects.
  const canCompleteNow = canComplete && status === 'In Progress'
  // The dispatch guards on top of the state machine: Start needs an accepted technician, Complete
  // needs an arrival unless a tenant admin deliberately overrides it in the drawer below.
  const startBlockedReason = technicianAccepted ? null : START_BLOCKED_REASON
  const completeBlockedReason = arrivalRecorded || isTenantAdmin ? null : COMPLETE_BLOCKED_REASON
  const startAllowed = canStartNow && startBlockedReason === null
  const completeAllowed = canCompleteNow && completeBlockedReason === null
  const canCloseNow = canClose && status === 'Completed'
  const canCancelNow = canCancel && !['Completed', 'Closed', 'Cancelled'].includes(status)

  // The single most relevant next action for the current status earns primary (dark) emphasis;
  // everything else is a secondary button. Cancel is destructive and never primary.
  const primaryActionKey: 'start' | 'resume' | 'complete' | 'close' | null = startAllowed
    ? 'start'
    : canResume
      ? 'resume'
      : completeAllowed
        ? 'complete'
        : canCloseNow
          ? 'close'
          : null
  const primaryBtnClass = 'inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]'
  const secondaryBtnClass = 'inline-flex h-11 items-center rounded-lg border border-[var(--app-border)] bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-[var(--app-primary-soft)]'

  const currentWorkflowStatus = workOrder.workflowStatusId
    ? (workflowStatuses ?? []).find((s) => s.id === workOrder.workflowStatusId)
    : undefined
  const workflowStatusOptions = currentWorkflowStatus
    ? (workflowStatuses ?? []).filter(
        (s) => s.id === currentWorkflowStatus.id || currentWorkflowStatus.allowedNextStatusIds.includes(s.id),
      )
    : []

  const changeWorkflowStatus = async (workflowStatusId: string) => {
    if (!id || workflowStatusId === currentWorkflowStatus?.id) return
    try {
      await patchWorkflowStatus.mutateAsync({ id, workflowStatusId })
      toast.success('Status updated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const header = (
    <Surface variant="glass" padding="lg" crystal="work-orders">
    <PageHeader
      title={workOrder.workOrderNumber}
      subtitle={workOrder.title}
      breadcrumbs={[
        { label: 'Work Orders', to: '/work-orders' },
        { label: workOrder.workOrderNumber },
      ]}
      titleAdornment={
        currentWorkflowStatus && canCreate ? (
          <select
            value={currentWorkflowStatus.id}
            onChange={(event) => void changeWorkflowStatus(event.target.value)}
            disabled={patchWorkflowStatus.isPending || currentWorkflowStatus.isFinal}
            className="h-9 rounded-full border-0 px-3 text-xs font-medium text-white outline-none disabled:opacity-70"
            style={{ backgroundColor: currentWorkflowStatus.color }}
            aria-label="Work order status"
          >
            {workflowStatusOptions.map((option) => (
              <option key={option.id} value={option.id} className="bg-white text-slate-900">
                {option.name}
              </option>
            ))}
          </select>
        ) : (
          <StatusBadge status={status} />
        )
      }
      secondaryActions={
        workOrder.quoteId && workOrder.quoteNumber ? (
          <Link
            to={`/finance/quotes/${workOrder.quoteId}`}
            className="inline-flex items-center rounded-full bg-sky-50 px-3 py-1.5 text-xs font-medium text-sky-700 ring-1 ring-inset ring-sky-200 transition hover:bg-sky-100"
          >
            Created from Quote #{workOrder.quoteNumber}
          </Link>
        ) : undefined
      }
      primaryAction={
        <div className="hidden flex-wrap items-center gap-2 md:flex">
          {canStartNow ? (
            <button
              type="button"
              onClick={() => runAction('Work started', () => startWorkOrder.mutateAsync(undefined))}
              disabled={startBlockedReason !== null}
              title={startBlockedReason ?? undefined}
              className={cn(primaryActionKey === 'start' ? primaryBtnClass : secondaryBtnClass, 'disabled:cursor-not-allowed disabled:opacity-50')}
            >
              Start Work
            </button>
          ) : null}
          {canPause ? (
            <button type="button" onClick={() => setHoldOpen(true)} className={secondaryBtnClass}>Put On Hold</button>
          ) : null}
          {canResume ? (
            <button type="button" onClick={() => runAction('Work resumed', () => resumeFromHold.mutateAsync())} className={primaryActionKey === 'resume' ? primaryBtnClass : secondaryBtnClass}>Resume</button>
          ) : null}
          {canCompleteNow ? (
            <button
              type="button"
              onClick={() => setCompleteOpen(true)}
              disabled={completeBlockedReason !== null}
              title={completeBlockedReason ?? undefined}
              className={cn(primaryActionKey === 'complete' ? primaryBtnClass : secondaryBtnClass, 'disabled:cursor-not-allowed disabled:opacity-50')}
            >
              Complete
            </button>
          ) : null}
          {canCloseNow ? (
            <button type="button" onClick={() => setConfirmAction('close')} className={primaryActionKey === 'close' ? primaryBtnClass : secondaryBtnClass}>Close</button>
          ) : null}
          {canCancelNow ? (
            <button type="button" onClick={() => setConfirmAction('cancel')} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">Cancel</button>
          ) : null}
          {canCreate ? (
            <div className="relative">
              <button
                type="button"
                onClick={() => setShowActionsMenu((prev) => !prev)}
                className="inline-flex h-11 w-11 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-700 transition hover:bg-slate-50"
                aria-label="More actions"
              >
                <MoreVertical className="h-4 w-4" />
              </button>
              {showActionsMenu ? (
                <>
                  <button type="button" aria-label="Close menu" className="fixed inset-0 z-10 cursor-default" onClick={() => setShowActionsMenu(false)} />
                  <div className="absolute right-0 z-20 mt-2 w-44 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                    <button
                      type="button"
                      onClick={() => {
                        setShowActionsMenu(false)
                        void submitDuplicate()
                      }}
                      className="block w-full px-3 py-2 text-left text-sm text-slate-700 hover:bg-slate-50"
                    >
                      Duplicate
                    </button>
                  </div>
                </>
              ) : null}
            </div>
          ) : null}
        </div>
      }
    />
    </Surface>
  )

  const blockedNotice = canStartNow && startBlockedReason
    ? startBlockedReason
    : canCompleteNow && completeBlockedReason
      ? completeBlockedReason
      : null

  return (
    <>
      {blockedNotice ? (
        <div className="mb-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            {blockedNotice} Use the Crew Dispatch panel on the Assignment tab to record the technician response and site
            milestones.
          </span>
        </div>
      ) : null}
      {workOrder.isRepeatVisit ? (
        <div className="mb-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Repeat visit — this asset has had {workOrder.repeatVisitCount} jobs in the last 30 days. Consider escalating the underlying fault.
          </span>
        </div>
      ) : null}
      <DetailLayout
        header={header}
        sidebar={
          <>
            <TechnicianLocationPanel workOrder={workOrder} />
            <DocumentsPanel workOrderId={id} />
            <RelatedWorkOrdersPanel workOrderId={id} />
          </>
        }
        tabs={[
          {
            id: 'summary',
            label: 'Summary',
            content: (
              <SummarySection title="Summary" description="Core work order fields and timestamps.">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                  {summaryFields.map(({ key, label, render }) => {
                    const raw = (workOrder as unknown as Record<string, unknown>)[key]
                    const value = typeof raw === 'string' && raw ? (render ? render(raw) : raw) : '—'
                    return (
                      <div key={key} className="rounded-xl border border-[var(--app-border)] px-4 py-3">
                        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                        <div className="mt-2 text-sm text-slate-900">{value}</div>
                      </div>
                    )
                  })}
                </div>
                {workOrder.description ? (
                  <div className="mt-4 rounded-xl border border-[var(--app-border)] px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Description</div>
                    <p className="mt-2 text-sm leading-6 text-slate-700">{workOrder.description}</p>
                  </div>
                ) : null}
              </SummarySection>
            ),
          },
          {
            id: 'history',
            label: 'History',
            content: (
              <SubTabbed
                tabs={[
                  { id: 'timeline', label: 'Timeline', content: <TimelineTab workOrderId={id} /> },
                  { id: 'activity', label: 'Activity', content: <ActivityTab workOrderId={id} /> },
                  { id: 'notes', label: 'Notes', content: <NotesTab workOrderId={id} /> },
                ]}
              />
            ),
          },
          { id: 'assignment', label: 'Assignment', content: <AssignmentTab workOrder={workOrder} /> },
          { id: 'sla', label: 'SLA', content: <SlaTab workOrderId={id} /> },
          {
            id: 'execution',
            label: 'Work Execution',
            content: (
              <SubTabbed
                tabs={[
                  { id: 'execution', label: 'Execution', content: <ExecutionTab workOrderId={id} /> },
                  { id: 'evidence', label: 'Evidence', content: <EvidenceTab workOrderId={id} /> },
                  { id: 'parts', label: 'Parts', content: <PartsTab workOrderId={id} /> },
                  { id: 'subtasks', label: 'Sub-tasks', content: <SubtasksTab workOrder={workOrder} /> },
                  { id: 'signatures', label: 'Signatures', content: <SignaturesTab workOrderId={id} /> },
                  { id: 'report', label: 'Report', content: <ReportTab workOrder={workOrder} /> },
                ]}
              />
            ),
          },
        ]}
      />

      {/*
        `MobileActionBar` has no disabled state, so a guard-blocked action is omitted rather than
        rendered as a button that can only 400. The amber notice above states the reason on the same
        screen.
      */}
      {startAllowed || canResume || completeAllowed ? (
        <MobileActionBar
          actions={[
            ...(startAllowed ? [{ label: 'Start Work', primary: primaryActionKey === 'start', onClick: () => runAction('Work started', () => startWorkOrder.mutateAsync(undefined)) }] : []),
            ...(canResume ? [{ label: 'Resume', primary: primaryActionKey === 'resume', onClick: () => runAction('Work resumed', () => resumeFromHold.mutateAsync()) }] : []),
            ...(completeAllowed ? [{ label: 'Complete', primary: primaryActionKey === 'complete', onClick: () => setCompleteOpen(true) }] : []),
          ]}
        />
      ) : null}

      <FormDrawer
        open={completeOpen}
        title="Complete Work Order"
        onClose={closeComplete}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={closeComplete} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submitComplete} disabled={completeWorkOrder.isPending || !overrideReady} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60">
              {completeWorkOrder.isPending ? 'Completing…' : 'Complete Work Order'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="complete-notes">Work Done Notes</label>
            <textarea id="complete-notes" value={workDoneNotes} onChange={(event) => setWorkDoneNotes(event.target.value)} rows={5} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="complete-summary">Report Summary (optional)</label>
            <textarea id="complete-summary" value={reportSummary} onChange={(event) => setReportSummary(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          {overrideOffered ? (
            <CompletionOverrideFields
              idPrefix="complete-drawer"
              enabled={overrideEnabled}
              reason={overrideReason}
              onEnabledChange={setOverrideEnabled}
              onReasonChange={setOverrideReason}
            />
          ) : null}
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={confirmAction !== null}
        title={confirmAction === 'close' ? 'Close Work Order' : 'Cancel Work Order'}
        message={confirmAction === 'close' ? 'This closes the work order after completion. This action cannot be undone.' : 'This cancels the work order. This action cannot be undone.'}
        confirmLabel={confirmAction === 'close' ? 'Close Work Order' : 'Cancel Work Order'}
        danger={confirmAction === 'cancel'}
        onCancel={() => setConfirmAction(null)}
        onConfirm={() => {
          if (confirmAction === 'close') {
            void runAction('Work order closed', () => closeWorkOrder.mutateAsync(undefined))
          } else if (confirmAction === 'cancel') {
            void runAction('Work order cancelled', () => cancelWorkOrder.mutateAsync(undefined))
          }
          setConfirmAction(null)
        }}
      />

      <FormDrawer
        open={holdOpen}
        title="Put Work Order On Hold"
        onClose={() => setHoldOpen(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setHoldOpen(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submitHold} disabled={holdWorkOrder.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {holdWorkOrder.isPending ? 'Saving…' : 'Put On Hold'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="hold-type">Hold Type</label>
            <select id="hold-type" value={holdType} onChange={(event) => setHoldType(event.target.value as (typeof holdTypeOptions)[number]['value'])} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              {holdTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="hold-reason">Reason</label>
            <textarea id="hold-reason" value={holdReason} onChange={(event) => setHoldReason(event.target.value)} rows={4} placeholder="Add details about why this work order is on hold" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </>
  )
}
