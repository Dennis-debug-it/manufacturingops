import { useState } from 'react'
import toast from 'react-hot-toast'
import { Check, X } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { usePendingApprovals, useDecideApproval, type ApprovalRequest } from './api'

const documentTypeLabels: Record<string, string> = {
  PurchaseOrder: 'Purchase Order',
  Expense: 'Expense',
  ProjectVariation: 'Project Variation',
  Quotation: 'Quotation',
}

function ApprovalCard({ request }: { request: ApprovalRequest }) {
  const decide = useDecideApproval(request.id)
  const [note, setNote] = useState('')
  const [mode, setMode] = useState<'approve' | 'reject' | null>(null)

  const submit = async (approve: boolean) => {
    try {
      await decide.mutateAsync({ approve, note: note.trim() || null })
      toast.success(approve ? 'Approved' : 'Rejected')
      setMode(null)
      setNote('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{documentTypeLabels[request.documentType] ?? request.documentType}</span>
            <span className="font-mono text-sm text-slate-700">{request.documentNumber}</span>
          </div>
          <div className="mt-1 text-lg font-semibold text-slate-900">{formatCurrency(request.amount, request.currency)}</div>
          <div className="mt-1 text-sm text-slate-600">
            Awaiting <span className="font-medium text-slate-900">Level {request.currentLevelOrder}</span> of {request.totalLevels}
            {request.currentLevelName ? ` — ${request.currentLevelName}` : ''}
          </div>
          <div className="mt-1 text-xs text-slate-500">Submitted {formatDateTime(request.createdAt)}</div>
        </div>
        {mode === null ? (
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => setMode('approve')} className="inline-flex h-10 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-3.5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Check className="h-4 w-4" />Approve
            </button>
            <button type="button" onClick={() => setMode('reject')} className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-rose-200 bg-white px-3.5 text-sm font-medium text-rose-600 transition hover:bg-rose-50">
              <X className="h-4 w-4" />Reject
            </button>
          </div>
        ) : null}
      </div>

      {request.steps.length > 1 ? (
        <ol className="mt-4 flex flex-wrap gap-2">
          {request.steps.map((step) => (
            <li key={step.levelOrder} className="flex items-center gap-2 rounded-lg border border-slate-200 px-2.5 py-1 text-xs">
              <span className="font-medium text-slate-700">L{step.levelOrder}</span>
              <span className="text-slate-500">{step.levelName}</span>
              <StatusBadge status={step.status} />
              {step.decidedByName ? <span className="text-slate-400">· {step.decidedByName}</span> : null}
            </li>
          ))}
        </ol>
      ) : null}

      {mode !== null ? (
        <div className="mt-4 space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
          <label className="block text-sm font-medium text-slate-700" htmlFor={`note-${request.id}`}>
            {mode === 'approve' ? 'Approval note (optional)' : 'Reason for rejection (optional)'}
          </label>
          <textarea
            id={`note-${request.id}`}
            value={note}
            onChange={(event) => setNote(event.target.value)}
            rows={2}
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => { setMode(null); setNote('') }} className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">Cancel</button>
            <button
              type="button"
              disabled={decide.isPending}
              onClick={() => submit(mode === 'approve')}
              className={`inline-flex h-9 items-center rounded-lg px-4 text-sm font-medium text-white disabled:opacity-60 ${mode === 'approve' ? 'bg-[var(--app-primary)] hover:bg-[var(--app-secondary)]' : 'bg-rose-600 hover:bg-rose-700'}`}
            >
              {decide.isPending ? 'Saving…' : mode === 'approve' ? 'Confirm Approval' : 'Confirm Rejection'}
            </button>
          </div>
        </div>
      ) : null}
    </div>
  )
}

export function MyApprovalsPage() {
  const { data, isLoading, isError } = usePendingApprovals()

  return (
    <div className="space-y-4">
      <PageHeader title="My Approvals" subtitle="Documents awaiting your approval across purchasing and finance." />
      {isLoading ? (
        <Spinner label="Loading approvals" />
      ) : isError ? (
        <EmptyState title="Approvals unavailable" description="Your pending approvals could not be loaded." />
      ) : !data || data.length === 0 ? (
        <EmptyState title="Nothing awaiting you" description="When a purchase order or expense needs your approval, it will appear here." />
      ) : (
        <div className="space-y-3">
          {data.map((request) => (
            <ApprovalCard key={request.id} request={request} />
          ))}
        </div>
      )}
    </div>
  )
}
