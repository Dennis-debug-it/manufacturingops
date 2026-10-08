import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { AlertTriangle, CalendarClock, MoreVertical, PauseCircle } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SlaBadge } from '../../shared/components/SlaBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { useEntitlements } from '../../shared/hooks/useEntitlements'
import { cn } from '../../shared/utils/cn'
import { formatDate, formatDateTime, isPast } from '../../shared/utils/dates'
import {
  useCancelWorkOrder,
  useCloseWorkOrder,
  useCompleteWorkOrder,
  useDuplicateWorkOrder,
  useExecutionBundle,
  useHoldWorkOrder,
  usePatchWorkOrderWorkflowStatus,
  useResumeFromHold,
  useStartWorkOrder,
  useWorkOrder,
  useWorkOrderSla,
} from './api'
import { useWorkflowStatuses } from '../settings/workflowsApi'
import { CompleteWorkOrderDialog } from './components/CompleteWorkOrderDialog'
import { DocumentsPanel } from './components/DocumentsPanel'
import { ReassignDrawer } from './components/ReassignDrawer'
import { RelatedWorkOrdersPanel } from './components/RelatedWorkOrdersPanel'
import { SubtasksPanel } from './components/SubtasksPanel'
import { TechnicianLocationPanel } from './components/TechnicianLocationPanel'
import { hasRecordedArrival } from './dispatchGuards'
import { ActivityTab } from './tabs/ActivityTab'
import { OverviewTab } from './tabs/OverviewTab'
import { ReportTab } from './tabs/ReportTab'
import { WorkTab } from './tabs/WorkTab'

const holdTypeOptions = [
  { value: 'OnHold', label: 'On Hold (general)' },
  { value: 'WaitingOnParts', label: 'Waiting on Parts' },
  { value: 'WaitingOnClient', label: 'Waiting on Client' },
] as const

const HOLD_STATUSES = ['On Hold', 'Waiting on Parts', 'Waiting on Client']
const FINAL_STATUSES = ['Completed', 'Closed', 'Cancelled']
const STARTABLE_STATUSES = ['Assigned', 'Scheduled', 'New', 'Open']

const chipClass = 'inline-flex items-center gap-1 rounded-full border border-slate-200 bg-white px-2.5 py-0.5 text-xs font-medium text-slate-700'
const secondaryButtonClass = 'inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50'

type MenuItem = { label: string; onClick: () => void; danger?: boolean }

export function WorkOrderDetailPage() {
  const { hasModule } = useEntitlements()
  const { user } = useAuth()
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: workOrder, isLoading, isError } = useWorkOrder(id)
  const { data: sla } = useWorkOrderSla(id)
  const { data: bundle } = useExecutionBundle(id)
  const { data: workflowStatuses } = useWorkflowStatuses()
  const patchWorkflowStatus = usePatchWorkOrderWorkflowStatus()

  const canAssign = usePermission('work-orders.assign')
  const canStart = usePermission('work-orders.start')
  const canHold = usePermission('work-orders.hold')
  const canResumePermission = usePermission('work-orders.resume')
  const canComplete = usePermission('work-orders.complete')
  const canClose = usePermission('work-orders.close')
  const canCancel = usePermission('work-orders.cancel')
  const canCreate = usePermission('work-orders.create')
  const canEdit = usePermission('work-orders.edit')

  const startWorkOrder = useStartWorkOrder(id ?? '')
  const holdWorkOrder = useHoldWorkOrder(id ?? '')
  const resumeFromHold = useResumeFromHold(id ?? '')
  const closeWorkOrder = useCloseWorkOrder(id ?? '')
  const cancelWorkOrder = useCancelWorkOrder(id ?? '')
  const completeWorkOrder = useCompleteWorkOrder(id ?? '')
  const duplicateWorkOrder = useDuplicateWorkOrder(id ?? '')

  const [completeOpen, setCompleteOpen] = useState(false)
  const [reassignOpen, setReassignOpen] = useState(false)
  const [confirmAction, setConfirmAction] = useState<'close' | 'cancel' | null>(null)
  const [showActionsMenu, setShowActionsMenu] = useState(false)
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdType, setHoldType] = useState<(typeof holdTypeOptions)[number]['value']>('OnHold')
  const [holdReason, setHoldReason] = useState('')

  const runAction = async (label: string, mutateAsync: () => Promise<unknown>) => {
    try {
      await mutateAsync()
      toast.success(label)
    } catch {
      // interceptor surfaces the error toast
    }
  }

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

  if (isLoading) {
    return <Spinner label="Loading work order" />
  }

  if (isError || !workOrder || !id) {
    return <EmptyState title="Work order unavailable" description="The record could not be loaded." />
  }

  const status = workOrder.status
  const isFinal = FINAL_STATUSES.includes(status)
  const isOnHold = HOLD_STATUSES.includes(status)
  const canStartNow = canStart && STARTABLE_STATUSES.includes(status)
  const canPauseNow = canHold && !isOnHold && !isFinal
  const canResumeNow = canResumePermission && isOnHold
  const canCompleteNow = canComplete && !isFinal
  const canCloseNow = canClose && status === 'Completed'
  const canCancelNow = canCancel && !isFinal
  const arrivalRecorded = Boolean(workOrder.arrivalAt) || hasRecordedArrival(workOrder.technicianAssignments)

  const start = () => void runAction('Work started', () => startWorkOrder.mutateAsync(undefined))
  const resume = () => void runAction('Work resumed', () => resumeFromHold.mutateAsync())

  // One main action that follows the job: Resume → Start → Complete → Close.
  const mainAction: { label: string; onClick: () => void } | null = canResumeNow
    ? { label: 'Resume', onClick: resume }
    : canStartNow
      ? { label: 'Start Work', onClick: start }
      : canCompleteNow
        ? { label: 'Complete', onClick: () => setCompleteOpen(true) }
        : canCloseNow
          ? { label: 'Close', onClick: () => setConfirmAction('close') }
          : null

  const menuItems: MenuItem[] = [
    ...(canCompleteNow && mainAction?.label !== 'Complete' ? [{ label: 'Complete', onClick: () => setCompleteOpen(true) }] : []),
    ...(canPauseNow ? [{ label: 'Put On Hold', onClick: () => setHoldOpen(true) }] : []),
    ...(canResumeNow && mainAction?.label !== 'Resume' ? [{ label: 'Resume', onClick: resume }] : []),
    ...(canAssign && !isFinal ? [{ label: 'Reassign', onClick: () => setReassignOpen(true) }] : []),
    ...(canCreate ? [{ label: 'Duplicate', onClick: () => void submitDuplicate() }] : []),
    ...(canCancelNow ? [{ label: 'Cancel Work Order', onClick: () => setConfirmAction('cancel'), danger: true }] : []),
  ]

  const currentWorkflowStatus = workOrder.workflowStatusId
    ? (workflowStatuses ?? []).find((s) => s.id === workOrder.workflowStatusId)
    : undefined
  const workflowStatusOptions = currentWorkflowStatus
    ? (workflowStatuses ?? []).filter(
        (s) => s.id === currentWorkflowStatus.id || currentWorkflowStatus.allowedNextStatusIds.includes(s.id),
      )
    : []

  const changeWorkflowStatus = async (workflowStatusId: string) => {
    if (workflowStatusId === currentWorkflowStatus?.id) return
    try {
      await patchWorkflowStatus.mutateAsync({ id, workflowStatusId })
      toast.success('Status updated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const slaDeadline = sla?.resolutionDeadline ?? sla?.responseDeadline ?? workOrder.slaResolutionDeadline ?? workOrder.slaResponseDeadline
  const slaBreached = sla ? sla.responseBreached || sla.resolutionBreached : workOrder.slaResponseBreached || workOrder.slaResolutionBreached
  const dueOverdue = Boolean(workOrder.dueDate) && !isFinal && isPast(workOrder.dueDate!)

  const chips = (
    <div className="flex flex-wrap items-center gap-1.5">
      {currentWorkflowStatus && canEdit ? (
        <select
          value={currentWorkflowStatus.id}
          onChange={(event) => void changeWorkflowStatus(event.target.value)}
          disabled={patchWorkflowStatus.isPending || currentWorkflowStatus.isFinal}
          className="h-7 rounded-full border-0 px-2.5 text-xs font-medium text-white outline-none disabled:opacity-70"
          style={{ backgroundColor: currentWorkflowStatus.color }}
          aria-label="Work order status"
        >
          {workflowStatusOptions.map((option) => (
            <option key={option.id} value={option.id} className="bg-white text-slate-900">{option.name}</option>
          ))}
        </select>
      ) : (
        <StatusBadge status={status} />
      )}
      <StatusBadge status={`${workOrder.priority} priority`} />
      {slaDeadline && !isFinal ? <SlaBadge deadline={slaDeadline} breached={slaBreached} paused={sla?.isPaused ?? false} /> : null}
      {workOrder.dueDate ? (
        <span className={cn(chipClass, dueOverdue && 'border-rose-200 text-rose-700')}>
          <CalendarClock className="h-3 w-3" />Due {formatDate(workOrder.dueDate)}
        </span>
      ) : null}
      {hasModule('finance') && workOrder.quoteId && workOrder.quoteNumber ? (
        <Link to={`/finance/quotes/${workOrder.quoteId}`} className={cn(chipClass, 'text-sky-700 hover:bg-sky-50')}>
          Quote #{workOrder.quoteNumber}
        </Link>
      ) : null}
    </div>
  )

  const header = (
    <PageHeader
      title={workOrder.workOrderNumber}
      subtitle={workOrder.title}
      titleAdornment={chips}
      breadcrumbs={[
        { label: 'Work Orders', to: '/work-orders' },
        { label: workOrder.workOrderNumber },
      ]}
      primaryAction={
        <div className="flex items-center gap-2" data-testid="workflow-actions">
          {mainAction ? (
            <button type="button" onClick={mainAction.onClick} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              {mainAction.label}
            </button>
          ) : null}
          {menuItems.length > 0 ? (
            <div className="relative">
              <button
                type="button"
                onClick={() => setShowActionsMenu((prev) => !prev)}
                className={cn(secondaryButtonClass, 'w-10 justify-center px-0')}
                aria-label="More actions"
                aria-expanded={showActionsMenu}
              >
                <MoreVertical className="h-4 w-4" />
              </button>
              {showActionsMenu ? (
                <>
                  <button type="button" aria-label="Close menu" className="fixed inset-0 z-10 cursor-default" onClick={() => setShowActionsMenu(false)} />
                  <div role="menu" className="absolute right-0 z-20 mt-2 w-48 rounded-lg border border-slate-200 bg-white py-1 shadow-lg">
                    {menuItems.map((item) => (
                      <button
                        key={item.label}
                        type="button"
                        role="menuitem"
                        onClick={() => {
                          setShowActionsMenu(false)
                          item.onClick()
                        }}
                        className={cn('block w-full px-3 py-2 text-left text-sm hover:bg-slate-50', item.danger ? 'text-rose-600' : 'text-slate-700')}
                      >
                        {item.label}
                      </button>
                    ))}
                  </div>
                </>
              ) : null}
            </div>
          ) : null}
        </div>
      }
    />
  )

  const alerts = [
    isOnHold ? {
      key: 'hold',
      icon: PauseCircle,
      text: `${status}${workOrder.holdSince ? ` since ${formatDateTime(workOrder.holdSince)}` : ''}${workOrder.holdReason || workOrder.holdNote ? ` — ${workOrder.holdReason ?? workOrder.holdNote}` : ''}`,
    } : null,
    slaBreached && !isFinal ? { key: 'sla', icon: AlertTriangle, text: 'SLA breached. Prioritise or escalate this job.' } : null,
    workOrder.isRepeatVisit ? {
      key: 'repeat',
      icon: AlertTriangle,
      text: `Repeat visit — this asset has had ${workOrder.repeatVisitCount} jobs in the last 30 days. Consider escalating the underlying fault.`,
    } : null,
  ].filter((alert): alert is NonNullable<typeof alert> => alert !== null)

  return (
    <>
      {alerts.length > 0 ? (
        <div className="mb-4 space-y-1 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          {alerts.map(({ key, icon: Icon, text }) => (
            <p key={key} className="flex items-start gap-2">
              <Icon className="mt-0.5 h-4 w-4 shrink-0" />{text}
            </p>
          ))}
        </div>
      ) : null}
      <DetailLayout
        header={header}
        defaultTabId="overview"
        sidebar={
          <>
            <TechnicianLocationPanel workOrder={workOrder} />
            <DocumentsPanel workOrderId={id} />
            <SubtasksPanel workOrder={workOrder} />
            <RelatedWorkOrdersPanel workOrderId={id} />
          </>
        }
        tabs={[
          { id: 'overview', label: 'Overview', content: <OverviewTab workOrder={workOrder} onReassign={() => setReassignOpen(true)} /> },
          { id: 'work', label: 'Work', content: <WorkTab workOrder={workOrder} /> },
          { id: 'activity', label: 'Activity', content: <ActivityTab workOrderId={id} /> },
          { id: 'report', label: 'Report', content: <ReportTab workOrder={workOrder} /> },
        ]}
      />

      <CompleteWorkOrderDialog
        open={completeOpen}
        woNumber={workOrder.workOrderNumber}
        submitting={completeWorkOrder.isPending}
        arrivalRecorded={arrivalRecorded}
        canAdminOverride={user?.role === 'admin'}
        initialNotes={bundle?.notes.workDone ?? workOrder.workDoneNotes}
        onClose={() => setCompleteOpen(false)}
        onSubmit={({ workDoneNotes, adminOverride, overrideReason }) => {
          completeWorkOrder.mutate(
            { workDoneNotes, adminOverride, overrideReason },
            {
              onSuccess: () => {
                toast.success('Work order completed')
                setCompleteOpen(false)
              },
            },
          )
        }}
      />

      <ReassignDrawer workOrderId={id} open={reassignOpen} onClose={() => setReassignOpen(false)} />

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
            <button type="button" onClick={submitHold} disabled={holdWorkOrder.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {holdWorkOrder.isPending ? 'Saving…' : 'Put On Hold'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="hold-type">Hold Type</label>
            <select id="hold-type" value={holdType} onChange={(event) => setHoldType(event.target.value as (typeof holdTypeOptions)[number]['value'])} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              {holdTypeOptions.map((option) => (
                <option key={option.value} value={option.value}>{option.label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="hold-reason">Reason</label>
            <textarea id="hold-reason" value={holdReason} onChange={(event) => setHoldReason(event.target.value)} rows={4} placeholder="Add details about why this work order is on hold" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
        </div>
      </FormDrawer>
    </>
  )
}
