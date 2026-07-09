import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { MobileActionBar } from '../../shared/components/MobileActionBar'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import {
  useCancelWorkOrder,
  useCloseWorkOrder,
  useCompleteWorkOrder,
  usePauseWorkOrder,
  useResumeWorkOrder,
  useStartWorkOrder,
  useWorkOrder,
} from './api'
import { TimelineTab } from './tabs/TimelineTab'
import { AssignmentTab } from './tabs/AssignmentTab'
import { ExecutionTab } from './tabs/ExecutionTab'
import { EvidenceTab } from './tabs/EvidenceTab'
import { MaterialsTab } from './tabs/MaterialsTab'
import { SignaturesTab } from './tabs/SignaturesTab'
import { ReportTab } from './tabs/ReportTab'

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

export function WorkOrderDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data: workOrder, isLoading, isError } = useWorkOrder(id)

  const canAssign = usePermission('work-orders.assign')
  const canStart = usePermission('work-orders.start')
  const canExecute = usePermission('work-orders.execute')
  const canComplete = usePermission('work-orders.complete')
  const canClose = usePermission('work-orders.close')
  const canCancel = usePermission('work-orders.cancel')

  const startWorkOrder = useStartWorkOrder(id ?? '')
  const pauseWorkOrder = usePauseWorkOrder(id ?? '')
  const resumeWorkOrder = useResumeWorkOrder(id ?? '')
  const closeWorkOrder = useCloseWorkOrder(id ?? '')
  const cancelWorkOrder = useCancelWorkOrder(id ?? '')
  const completeWorkOrder = useCompleteWorkOrder(id ?? '')

  const [completeOpen, setCompleteOpen] = useState(false)
  const [workDoneNotes, setWorkDoneNotes] = useState('')
  const [reportSummary, setReportSummary] = useState('')
  const [confirmAction, setConfirmAction] = useState<'close' | 'cancel' | null>(null)

  const runAction = async (label: string, mutateAsync: () => Promise<unknown>) => {
    try {
      await mutateAsync()
      toast.success(label)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const submitComplete = async () => {
    if (!workDoneNotes.trim()) {
      toast.error('Work done notes are required')
      return
    }
    try {
      await completeWorkOrder.mutateAsync({ workDoneNotes: workDoneNotes.trim(), reportSummary: reportSummary.trim() || null })
      toast.success('Work order completed')
      setCompleteOpen(false)
      setWorkDoneNotes('')
      setReportSummary('')
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
  const canPause = canExecute && !['OnHold', 'Completed', 'Closed', 'Cancelled'].includes(status)
  const canResume = canExecute && status === 'OnHold'
  const canStartNow = canStart && ['Assigned', 'Scheduled', 'New', 'Open'].includes(status)
  const canCompleteNow = canComplete && !['Completed', 'Closed', 'Cancelled'].includes(status)
  const canCloseNow = canClose && status === 'Completed'
  const canCancelNow = canCancel && !['Completed', 'Closed', 'Cancelled'].includes(status)

  const header = (
    <PageHeader
      title={workOrder.workOrderNumber}
      subtitle={workOrder.title}
      breadcrumbs={[
        { label: 'Work Orders', to: '/work-orders' },
        { label: workOrder.workOrderNumber },
      ]}
      secondaryActions={
        <div className="flex flex-wrap items-center gap-2">
          <Link to="/work-orders" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Back</Link>
          <StatusBadge status={status} />
        </div>
      }
      primaryAction={
        <div className="hidden flex-wrap items-center gap-2 md:flex">
          {canStartNow ? (
            <button type="button" onClick={() => runAction('Work started', () => startWorkOrder.mutateAsync(undefined))} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Start Work</button>
          ) : null}
          {canPause ? (
            <button type="button" onClick={() => runAction('Work order on hold', () => pauseWorkOrder.mutateAsync({ holdReason: 'Other', note: 'Paused from work order detail' }))} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Put On Hold</button>
          ) : null}
          {canResume ? (
            <button type="button" onClick={() => runAction('Work resumed', () => resumeWorkOrder.mutateAsync(undefined))} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Resume</button>
          ) : null}
          {canCompleteNow ? (
            <button type="button" onClick={() => setCompleteOpen(true)} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">Complete</button>
          ) : null}
          {canCloseNow ? (
            <button type="button" onClick={() => setConfirmAction('close')} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Close</button>
          ) : null}
          {canCancelNow ? (
            <button type="button" onClick={() => setConfirmAction('cancel')} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">Cancel</button>
          ) : null}
        </div>
      }
    />
  )

  return (
    <>
      <DetailLayout
        header={header}
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
                      <div key={key} className="rounded-xl border border-slate-200 px-4 py-3">
                        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                        <div className="mt-2 text-sm text-slate-900">{value}</div>
                      </div>
                    )
                  })}
                </div>
                {workOrder.description ? (
                  <div className="mt-4 rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Description</div>
                    <p className="mt-2 text-sm leading-6 text-slate-700">{workOrder.description}</p>
                  </div>
                ) : null}
              </SummarySection>
            ),
          },
          { id: 'timeline', label: 'Timeline', content: <TimelineTab workOrderId={id} /> },
          { id: 'assignment', label: 'Assignment', content: <AssignmentTab workOrder={workOrder} /> },
          { id: 'execution', label: 'Work Execution', content: <ExecutionTab workOrderId={id} /> },
          { id: 'evidence', label: 'Evidence', content: <EvidenceTab workOrderId={id} /> },
          { id: 'materials', label: 'Materials', content: <MaterialsTab workOrderId={id} /> },
          { id: 'signatures', label: 'Signatures', content: <SignaturesTab workOrderId={id} /> },
          { id: 'report', label: 'Report', content: <ReportTab workOrder={workOrder} /> },
        ]}
      />

      {canAssign || canCompleteNow ? (
        <MobileActionBar
          actions={[
            ...(canStartNow ? [{ label: 'Start Work', onClick: () => runAction('Work started', () => startWorkOrder.mutateAsync(undefined)) }] : []),
            ...(canCompleteNow ? [{ label: 'Complete', primary: true, onClick: () => setCompleteOpen(true) }] : []),
          ]}
        />
      ) : null}

      <FormDrawer
        open={completeOpen}
        title="Complete Work Order"
        onClose={() => setCompleteOpen(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setCompleteOpen(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
            <button type="button" onClick={submitComplete} disabled={completeWorkOrder.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
              {completeWorkOrder.isPending ? 'Completing…' : 'Complete Work Order'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="complete-notes">Work Done Notes</label>
            <textarea id="complete-notes" value={workDoneNotes} onChange={(event) => setWorkDoneNotes(event.target.value)} rows={5} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="complete-summary">Report Summary (optional)</label>
            <textarea id="complete-summary" value={reportSummary} onChange={(event) => setReportSummary(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-slate-900" />
          </div>
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
    </>
  )
}
