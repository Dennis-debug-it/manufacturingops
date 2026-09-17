import { useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useWorkOrders } from '../work-orders/api'
import {
  useApproveMaterialIssue,
  useCancelMaterialIssue,
  useCreateMaterialIssue,
  useIssueMaterialIssue,
  useMaterialIssues,
  useMaterials,
  useRejectMaterialIssue,
  useReturnMaterialIssue,
} from './api'
import type { MaterialIssue } from './types'
import { InventoryTabNav } from './components/InventoryTabNav'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

type PendingAction = { type: 'approve' | 'reject' | 'issue' | 'cancel' } | null

function IssueActions({ issue }: { issue: MaterialIssue }) {
  const canApprove = usePermission('inventory.requests.approve')
  const canIssue = usePermission('inventory.requests.issue')
  const [pending, setPending] = useState<PendingAction>(null)
  const approve = useApproveMaterialIssue(issue.id)
  const reject = useRejectMaterialIssue(issue.id)
  const doIssue = useIssueMaterialIssue(issue.id)
  const cancel = useCancelMaterialIssue(issue.id)

  const run = async () => {
    if (!pending) return
    try {
      if (pending.type === 'approve') await approve.mutateAsync(undefined)
      if (pending.type === 'reject') await reject.mutateAsync({ reason: 'Rejected from Material Issues page' })
      if (pending.type === 'issue') await doIssue.mutateAsync(undefined)
      if (pending.type === 'cancel') await cancel.mutateAsync(undefined)
      toast.success(`Issue ${issue.issueNumber} ${pending.type === 'issue' ? 'issued — stock deducted' : `${pending.type}${pending.type === 'cancel' ? 'led' : pending.type === 'approve' ? 'd' : 'ed'}`}`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      setPending(null)
    }
  }

  const buttonClass = 'rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50'
  const status = issue.issueStatus

  return (
    <div className="flex justify-end gap-2">
      {canApprove && (status === 'draft' || status === 'pending_approval') ? (
        <>
          <button type="button" onClick={() => setPending({ type: 'approve' })} className={buttonClass}>Approve</button>
          <button type="button" onClick={() => setPending({ type: 'reject' })} className="rounded-lg border border-rose-200 bg-white px-3 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50">Reject</button>
        </>
      ) : null}
      {canIssue && status === 'approved' ? (
        <button type="button" onClick={() => setPending({ type: 'issue' })} className="rounded-lg bg-[var(--app-primary)] px-3 py-1.5 text-xs font-medium text-white hover:bg-[var(--app-secondary)]">Issue Stock</button>
      ) : null}
      {canApprove && (status === 'draft' || status === 'pending_approval' || status === 'approved') ? (
        <button type="button" onClick={() => setPending({ type: 'cancel' })} className={buttonClass}>Cancel</button>
      ) : null}
      <ConfirmDialog
        open={pending !== null}
        title={`${pending?.type === 'issue' ? 'Issue Stock' : (pending?.type ?? '').replace(/^./, (c) => c.toUpperCase())}`}
        message={
          pending?.type === 'issue'
            ? `Issue ${issue.issueNumber}? Stock on hand is deducted and a ledger movement is recorded for each line.`
            : `This will ${pending?.type} ${issue.issueNumber}.`
        }
        danger={pending?.type === 'reject' || pending?.type === 'cancel'}
        onCancel={() => setPending(null)}
        onConfirm={run}
      />
    </div>
  )
}

function ReturnDrawer({ issue, onClose }: { issue: MaterialIssue; onClose: () => void }) {
  const returnIssue = useReturnMaterialIssue(issue.id)
  const [quantities, setQuantities] = useState<Record<string, string>>(() =>
    Object.fromEntries(issue.lines.map((line) => [line.id, '0'])),
  )

  const submit = async () => {
    const lines = issue.lines
      .map((line) => ({ materialIssueLineId: line.id, quantityReturned: Number(quantities[line.id]) || 0, expectedQuantityReturned: line.quantityReturned }))
      .filter((line) => line.quantityReturned > 0)
    if (lines.length === 0) {
      toast.error('Enter a returned quantity for at least one line')
      return
    }
    try {
      await returnIssue.mutateAsync({ lines })
      toast.success('Return recorded — stock restored')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open
      title={`Return items — ${issue.issueNumber}`}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={returnIssue.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {returnIssue.isPending ? 'Saving…' : 'Record Return'}
          </button>
        </div>
      }
    >
      <div className="space-y-3">
        {issue.lines.map((line) => (
          <div key={line.id} className="rounded-xl border border-slate-200 p-3">
            <div className="text-sm font-medium text-slate-900">{line.materialItemName ?? line.itemCode ?? 'Item'}</div>
            <div className="mt-1 text-xs text-slate-500">Issued {line.quantityIssued} {line.unit} · Already returned {line.quantityReturned}</div>
            <label className="mt-2 block text-xs font-medium text-slate-600" htmlFor={`return-${line.id}`}>Quantity to return</label>
            <input
              id={`return-${line.id}`}
              type="number"
              min="0"
              max={line.quantityIssued - line.quantityReturned}
              step="0.01"
              value={quantities[line.id] ?? '0'}
              onChange={(event) => setQuantities((current) => ({ ...current, [line.id]: event.target.value }))}
              className={fieldClass}
            />
          </div>
        ))}
      </div>
    </FormDrawer>
  )
}

interface DraftLine {
  materialItemId: string
  quantity: string
}

function NewIssueDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createIssue = useCreateMaterialIssue()
  const { data: materials } = useMaterials({ status: 'all' })
  const { data: workOrders } = useWorkOrders()
  const [workOrderId, setWorkOrderId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ materialItemId: '', quantity: '1' }])

  const close = () => {
    setWorkOrderId('')
    setNotes('')
    setLines([{ materialItemId: '', quantity: '1' }])
    onClose()
  }

  const submit = async () => {
    const payloadLines = lines
      .filter((line) => line.materialItemId && Number(line.quantity) > 0)
      .map((line) => ({ materialItemId: line.materialItemId, quantityRequested: Number(line.quantity) }))
    if (payloadLines.length === 0) {
      toast.error('Add at least one line with a material and quantity')
      return
    }
    try {
      const created = await createIssue.mutateAsync({
        workOrderId: workOrderId || null,
        notes: notes.trim() || null,
        lines: payloadLines,
      })
      toast.success(`Issue ${created.issueNumber} created`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="New Material Issue"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={createIssue.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {createIssue.isPending ? 'Creating…' : 'Create Issue'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="issue-wo">Work Order (optional)</label>
          <select id="issue-wo" value={workOrderId} onChange={(event) => setWorkOrderId(event.target.value)} className={fieldClass}>
            <option value="">Not linked to a work order</option>
            {(workOrders ?? []).map((wo) => (
              <option key={wo.id} value={wo.id}>{wo.workOrderNumber} — {wo.title}</option>
            ))}
          </select>
        </div>
        <div>
          <span className={labelClass}>Lines</span>
          <div className="space-y-2">
            {lines.map((line, index) => (
              <div key={index} className="flex items-center gap-2">
                <select
                  value={line.materialItemId}
                  onChange={(event) => setLines((current) => current.map((l, i) => (i === index ? { ...l, materialItemId: event.target.value } : l)))}
                  className={`${fieldClass} flex-1`}
                  aria-label="Material"
                >
                  <option value="">Select a part…</option>
                  {(materials ?? []).map((material) => (
                    <option key={material.id} value={material.id}>{material.itemName} ({material.quantityOnHand} {material.unitOfMeasure} on hand)</option>
                  ))}
                </select>
                <input
                  type="number"
                  min="0"
                  step="0.01"
                  value={line.quantity}
                  onChange={(event) => setLines((current) => current.map((l, i) => (i === index ? { ...l, quantity: event.target.value } : l)))}
                  className={`${fieldClass} w-24`}
                  aria-label="Quantity"
                />
                {lines.length > 1 ? (
                  <button type="button" onClick={() => setLines((current) => current.filter((_, i) => i !== index))} className="inline-flex h-9 w-9 items-center justify-center rounded-lg text-rose-500 transition hover:bg-rose-50" aria-label="Remove line">
                    <Trash2 className="h-4 w-4" />
                  </button>
                ) : null}
              </div>
            ))}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, { materialItemId: '', quantity: '1' }])} className="mt-2 text-sm font-medium text-slate-700 underline decoration-slate-300 underline-offset-4">
            + Add line
          </button>
        </div>
        <div>
          <label className={labelClass} htmlFor="issue-notes">Notes</label>
          <textarea id="issue-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}

export function MaterialIssuesPage() {
  const canRequest = usePermission('materials.request')
  const canIssue = usePermission('inventory.requests.issue')
  const { data, isLoading, isError } = useMaterialIssues()
  const [showForm, setShowForm] = useState(false)
  const [returningId, setReturningId] = useState<string | null>(null)

  const returning = (data ?? []).find((issue) => issue.id === returningId) ?? null

  const columns: DataTableColumn<MaterialIssue>[] = [
    { key: 'issueNumber', header: 'Issue No', cell: (row) => <span className="font-mono text-xs text-slate-700">{row.issueNumber}</span> },
    { key: 'workOrderNumber', header: 'Work Order', cell: (row) => row.workOrderNumber ?? '—' },
    { key: 'lines', header: 'Lines', cell: (row) => row.lines.length },
    { key: 'requestedByUserName', header: 'Requested By', cell: (row) => row.requestedByUserName ?? '—' },
    { key: 'issueStatus', header: 'Status', cell: (row) => <StatusBadge status={row.issueStatus.replace('_', ' ')} /> },
    { key: 'issuedAt', header: 'Issued At', cell: (row) => (row.issuedAt ? formatDateTime(row.issuedAt) : '—') },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex items-center justify-end gap-2">
          {canIssue && row.issueStatus === 'issued' ? (
            <button type="button" onClick={() => setReturningId(row.id)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">Return</button>
          ) : null}
          <IssueActions issue={row} />
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Material Issues"
        subtitle="Issue slips: request stock, approve, issue against jobs, and record returns. Issuing deducts stock and writes the movement ledger."
        primaryAction={
          canRequest ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Issue
            </button>
          ) : undefined
        }
      />
      <InventoryTabNav />

      <DataTable columns={columns} data={data ?? []} loading={isLoading} emptyTitle={isError ? 'Issues unavailable' : 'No material issues yet'} emptyDescription={isError ? 'Material issues could not be loaded.' : 'Create an issue slip to draw stock for a job.'} />

      <NewIssueDrawer open={showForm} onClose={() => setShowForm(false)} />
      {returning ? <ReturnDrawer issue={returning} onClose={() => setReturningId(null)} /> : null}
    </div>
  )
}
