import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Search, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ErrorState } from '../../shared/components/ErrorState'
import { ProjectPicker } from '../../shared/components/ProjectPicker'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useWorkOrders } from '../work-orders/api'
import { useBranches, useCreateMaterialIssue, useMaterials } from './api'
import {
  requestReference,
  sourceLabels,
  useApproveRequest,
  useCancelRequest,
  useCloseRestockRequest,
  useInventoryRequests,
  useIssueRequest,
  useRejectRequest,
  useReturnRequest,
  type InventoryRequest,
  type InventoryRequestSource,
  type InventoryRequestStage,
} from './requestsApi'
import { matchesRequestStage, requestStageFilters, requestStageFromParam, type RequestStageFilter } from './listFilters'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const qtyClass = 'h-9 w-24 rounded-lg border border-slate-300 px-2 text-sm tabular-nums outline-none focus:border-[var(--app-primary)]'
const primaryButton = 'inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60'
const secondaryButton = 'inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60'
const dangerButton = 'inline-flex h-11 items-center justify-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-700 transition hover:bg-rose-50 disabled:opacity-60'

const stageTone: Record<InventoryRequestStage, string> = {
  Requested: 'Pending',
  Approved: 'Approved',
  Issued: 'Issued',
  Completed: 'Completed',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
}

/**
 * The stores' one queue. Job parts requests, issue slips and the retired restock requests appear
 * together with one set of stages; each row opens a panel with the form for its next step.
 */
export function RequestsPage() {
  const canRequest = usePermission('materials.request')
  const [params, setParams] = useSearchParams()
  const stage = requestStageFromParam(params.get('stage'))
  const source = (params.get('type') ?? '') as InventoryRequestSource | ''
  const [search, setSearch] = useState(params.get('q') ?? '')
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showNew, setShowNew] = useState(false)
  const { data, isLoading, isError, refetch } = useInventoryRequests()

  const setParam = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  const all = useMemo(() => data ?? [], [data])
  const hasRestock = all.some((request) => request.source === 'Restock')
  const hasSeveralBranches = new Set(all.map((request) => request.branchId)).size > 1
  const counts = useMemo(() => {
    const result: Partial<Record<RequestStageFilter, number>> = {}
    for (const option of requestStageFilters) result[option.value] = all.filter((request) => (!source || request.source === source) && matchesRequestStage(request, option.value)).length
    return result
  }, [all, source])

  const rows = useMemo(() => {
    const term = search.trim().toLowerCase()
    return all.filter((request) =>
      matchesRequestStage(request, stage)
      && (!source || request.source === source)
      && (!term || [requestReference(request), request.workOrderNumber, request.requestedByName, request.branchName, ...request.lines.map((line) => line.itemName ?? '')]
        .some((value) => value?.toLowerCase().includes(term))),
    )
  }, [all, stage, source, search])

  const selected = all.find((request) => request.id === selectedId) ?? null

  const columns: DataTableColumn<InventoryRequest>[] = [
    {
      key: 'reference',
      header: 'Request',
      cell: (row) => (
        <span>
          <button type="button" onClick={() => setSelectedId(row.id)} className="whitespace-nowrap text-left font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">
            {requestReference(row)}
          </button>
          <span className="block text-xs text-slate-500">{sourceLabels[row.source]}</span>
        </span>
      ),
      sortValue: (row) => requestReference(row),
    },
    { key: 'workOrder', header: 'Work order', cell: (row) => (row.workOrderId ? <Link className="underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.workOrderId}`}>{row.workOrderNumber}</Link> : '—'), sortValue: (row) => row.workOrderNumber },
    { key: 'requestedBy', header: 'Requested by', cell: (row) => row.requestedByName ?? '—', sortValue: (row) => row.requestedByName },
    ...(hasSeveralBranches ? [{ key: 'branch', header: 'Branch', cell: (row: InventoryRequest) => row.branchName ?? '—', sortValue: (row: InventoryRequest) => row.branchName }] : []),
    { key: 'lines', header: 'Parts', cell: (row) => <span title={row.lines.map((line) => line.itemName).join(', ')}>{row.lines.length}</span>, sortValue: (row) => row.lines.length },
    { key: 'value', header: 'Value', cell: (row) => <span className="whitespace-nowrap">{formatCurrency(row.value)}</span>, sortValue: (row) => row.value },
    { key: 'stage', header: 'Stage', cell: (row) => <StatusBadge status={stageTone[row.stage] ?? row.stage} />, sortValue: (row) => row.stage },
    { key: 'createdAt', header: 'Raised', cell: (row) => <span className="whitespace-nowrap" title={formatDateTime(row.createdAt)}>{formatDate(row.createdAt)}</span>, sortValue: (row) => row.createdAt },
    {
      key: 'open',
      header: '',
      cell: (row) => (
        <button type="button" onClick={() => setSelectedId(row.id)} className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50">
          {row.stage === 'Requested' || row.stage === 'Approved' ? 'Action' : 'View'}
        </button>
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Requests"
        subtitle="Every request for parts from stores: job parts requests from technicians and stores issue slips. Requested → Approved → Issued → Completed."
        primaryAction={
          canRequest ? (
            <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New issue slip
            </button>
          ) : undefined
        }
      />

      <div className="flex flex-wrap gap-2" role="group" aria-label="Stage">
        {requestStageFilters.map((option) => (
          <button
            key={option.value}
            type="button"
            aria-pressed={stage === option.value}
            onClick={() => setParam('stage', option.value === 'open' ? '' : option.value)}
            className={cn('inline-flex h-9 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium transition', stage === option.value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
          >
            {option.label}
            <span className={cn('tabular-nums text-xs', stage === option.value ? 'text-white/70' : 'text-slate-400')}>{counts[option.value] ?? 0}</span>
          </button>
        ))}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <label className="flex h-10 min-w-[14rem] flex-1 items-center gap-2 rounded-lg border border-slate-300 bg-white px-3">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search number, work order, person or part" aria-label="Search requests" className="w-full border-0 bg-transparent p-0 text-sm outline-none" />
        </label>
        <select aria-label="Type" value={source} onChange={(event) => setParam('type', event.target.value)} className="h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">All types</option>
          <option value="PartsRequest">Job parts requests</option>
          <option value="IssueSlip">Issue slips</option>
          {hasRestock ? <option value="Restock">Restock (retired)</option> : null}
        </select>
      </div>

      {isError ? (
        <ErrorState title="Requests unavailable" description="The requests queue could not be loaded." onRetry={() => void refetch()} />
      ) : (
        <DataTable
          columns={columns}
          data={rows}
          loading={isLoading}
          emptyTitle={stage === 'open' ? 'Nothing waiting on stores' : 'No requests'}
          emptyDescription="Technicians raise parts requests from a work order's Parts tab; stores can raise an issue slip with New issue slip."
          virtualise={rows.length > 100}
        />
      )}

      {selected ? <RequestPanel key={selected.id} request={selected} onClose={() => setSelectedId(null)} /> : null}
      <NewIssueSlipDrawer open={showNew} onClose={() => setShowNew(false)} />
    </div>
  )
}

type PanelMode = 'view' | 'approve' | 'reject' | 'issue' | 'return' | 'close'

function RequestPanel({ request, onClose }: { request: InventoryRequest; onClose: () => void }) {
  const canApprove = usePermission('inventory.requests.approve')
  const canIssue = usePermission('inventory.requests.issue')
  const canRequest = usePermission('materials.request')
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const approve = useApproveRequest()
  const reject = useRejectRequest()
  const cancel = useCancelRequest()
  const issue = useIssueRequest()
  const returnStock = useReturnRequest()
  const closeRestock = useCloseRestockRequest()
  const [mode, setMode] = useState<PanelMode>('view')
  const [quantities, setQuantities] = useState<Record<string, string>>({})
  const [closeLines, setCloseLines] = useState<Record<string, { used: string; returned: string }>>({})
  const [reason, setReason] = useState('')
  const [sourceBranchId, setSourceBranchId] = useState(request.branchId ?? '')
  const busy = approve.isPending || reject.isPending || cancel.isPending || issue.isPending || returnStock.isPending || closeRestock.isPending
  const isParts = request.source === 'PartsRequest'

  const start = (next: PanelMode) => {
    const initial: Record<string, string> = {}
    for (const line of request.lines) {
      if (next === 'approve') initial[line.id] = String(line.quantityRequested)
      if (next === 'issue') initial[line.id] = String(line.quantityApproved ?? line.quantityRequested)
      if (next === 'return') initial[line.id] = '0'
    }
    setQuantities(initial)
    if (next === 'close') {
      setCloseLines(Object.fromEntries(request.lines.map((line) => [line.id, { used: String(line.quantityIssued), returned: '0' }])))
    }
    setReason('')
    setMode(next)
  }

  /** Parses the quantity inputs; null (with a toast) when one is not a number within its limit. */
  const readQuantities = (limit: (lineId: string) => number): Record<string, number> | null => {
    const result: Record<string, number> = {}
    for (const line of request.lines) {
      const value = Number(quantities[line.id] ?? '0')
      if (!Number.isFinite(value) || value < 0 || value > limit(line.id)) {
        toast.error(`${line.itemName ?? 'A line'}: enter 0 to ${limit(line.id)}`)
        return null
      }
      result[line.id] = value
    }
    return result
  }

  const done = (message: string) => {
    toast.success(message)
    onClose()
  }

  const submit = async () => {
    try {
      if (mode === 'approve') {
        const parsed = isParts ? readQuantities((id) => request.lines.find((line) => line.id === id)?.quantityRequested ?? 0) : undefined
        if (isParts && !parsed) return
        await approve.mutateAsync({ request, ...(parsed ? { quantities: parsed } : {}) })
        return done('Request approved')
      }
      if (mode === 'reject') {
        if (!reason.trim()) return void toast.error('Give the requester a reason')
        await reject.mutateAsync({ request, reason: reason.trim() })
        return done('Request rejected')
      }
      if (mode === 'issue') {
        if (isParts) {
          await issue.mutateAsync({ request, quantities: {}, sourceBranchId: sourceBranchId || null })
          return done('Parts dispatched')
        }
        const parsed = readQuantities((id) => request.lines.find((line) => line.id === id)?.quantityRequested ?? 0)
        if (!parsed) return
        if (Object.values(parsed).every((value) => value === 0)) return void toast.error('Issue at least one line, or reject the request instead')
        await issue.mutateAsync({ request, quantities: parsed })
        return done('Stock issued')
      }
      if (mode === 'return') {
        const parsed = readQuantities((id) => {
          const line = request.lines.find((item) => item.id === id)
          return line ? line.quantityIssued - line.quantityReturned : 0
        })
        if (!parsed) return
        if (Object.values(parsed).every((value) => value === 0)) return void toast.error('Enter a quantity to return')
        await returnStock.mutateAsync({ request, quantities: parsed })
        return done('Return recorded')
      }
      if (mode === 'close') {
        const lines: Record<string, { used: number; returned: number }> = {}
        for (const line of request.lines) {
          const entry = closeLines[line.id] ?? { used: '0', returned: '0' }
          const used = Number(entry.used)
          const returned = Number(entry.returned)
          if (!Number.isFinite(used) || !Number.isFinite(returned) || used < 0 || returned < 0 || used + returned > line.quantityIssued) {
            return void toast.error(`${line.itemName ?? 'A line'}: used + returned cannot exceed ${line.quantityIssued} issued`)
          }
          lines[line.id] = { used, returned }
        }
        await closeRestock.mutateAsync({ request, lines })
        return done('Request closed')
      }
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const actions: Array<{ mode: PanelMode | 'cancel'; label: string; tone: 'primary' | 'secondary' | 'danger' }> = []
  if (request.stage === 'Requested') {
    if (canApprove) actions.push({ mode: 'approve', label: 'Approve', tone: 'primary' })
    if (canApprove && request.source !== 'Restock') actions.push({ mode: 'reject', label: 'Reject', tone: 'danger' })
    if (canRequest && request.source === 'IssueSlip') actions.push({ mode: 'cancel', label: 'Cancel request', tone: 'secondary' })
  }
  if (request.stage === 'Approved' && canIssue) actions.push({ mode: 'issue', label: isParts ? 'Dispatch' : 'Issue stock', tone: 'primary' })
  if (request.stage === 'Issued' && request.source === 'IssueSlip' && canIssue) actions.push({ mode: 'return', label: 'Record return', tone: 'secondary' })
  if (request.stage === 'Issued' && request.source === 'Restock' && canApprove) actions.push({ mode: 'close', label: 'Close', tone: 'primary' })

  const title = mode === 'approve' ? 'Approve request' : mode === 'reject' ? 'Reject request' : mode === 'issue' ? (isParts ? 'Dispatch parts' : 'Issue stock') : mode === 'return' ? 'Record return' : mode === 'close' ? 'Close restock request' : requestReference(request)

  const footer = mode === 'view' ? (
    <div className="flex flex-wrap items-center justify-end gap-2">
      <button type="button" onClick={onClose} className={secondaryButton}>Close</button>
      {actions.map((action) => (
        <button
          key={action.mode}
          type="button"
          disabled={busy}
          onClick={() => {
            if (action.mode === 'cancel') {
              void cancel.mutateAsync(request).then(() => done('Request cancelled')).catch(() => undefined)
            } else {
              start(action.mode)
            }
          }}
          className={action.tone === 'primary' ? primaryButton : action.tone === 'danger' ? dangerButton : secondaryButton}
        >
          {action.label}
        </button>
      ))}
    </div>
  ) : (
    <div className="flex items-center justify-end gap-2">
      <button type="button" onClick={() => setMode('view')} disabled={busy} className={secondaryButton}>Back</button>
      <button type="button" onClick={() => void submit()} disabled={busy} className={mode === 'reject' ? dangerButton : primaryButton}>
        {busy ? 'Saving…' : mode === 'approve' ? 'Approve' : mode === 'reject' ? 'Reject' : mode === 'issue' ? (isParts ? 'Dispatch' : 'Issue') : mode === 'return' ? 'Return to stock' : 'Close request'}
      </button>
    </div>
  )

  const quantityInput = (lineId: string, max: number, label: string) => (
    <input
      type="number"
      min="0"
      max={max}
      step="any"
      inputMode="decimal"
      aria-label={label}
      value={quantities[lineId] ?? ''}
      onChange={(event) => setQuantities((current) => ({ ...current, [lineId]: event.target.value }))}
      className={qtyClass}
    />
  )

  return (
    <FormDrawer open title={title} onClose={onClose} footer={footer}>
      <div className="space-y-4">
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
          <dt className="text-slate-500">Type</dt><dd className="text-slate-900">{sourceLabels[request.source]}</dd>
          <dt className="text-slate-500">Stage</dt><dd><StatusBadge status={stageTone[request.stage] ?? request.stage} /></dd>
          {request.workOrderId ? (<><dt className="text-slate-500">Work order</dt><dd><Link className="underline decoration-slate-300 underline-offset-4" to={`/work-orders/${request.workOrderId}`}>{request.workOrderNumber}</Link></dd></>) : null}
          <dt className="text-slate-500">Requested by</dt><dd className="text-slate-900">{request.requestedByName ?? '—'}</dd>
          <dt className="text-slate-500">Branch</dt><dd className="text-slate-900">{request.branchName ?? '—'}</dd>
          <dt className="text-slate-500">Raised</dt><dd className="text-slate-900">{formatDateTime(request.createdAt)}</dd>
        </dl>
        {request.notes ? <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-700">{request.notes}</p> : null}
        {request.rejectionReason ? <p className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">Rejected: {request.rejectionReason}</p> : null}

        {mode === 'approve' && !isParts ? <p className="text-sm text-slate-600">Approves the request as asked. You set how much to hand over when you issue it.</p> : null}
        {mode === 'approve' && isParts ? <p className="text-sm text-slate-600">Approve less than asked where stock is short; 0 leaves a part out.</p> : null}
        {mode === 'issue' && !isParts ? <p className="text-sm text-slate-600">Hand over less than asked if stock is short; 0 leaves a line unissued. Issuing takes the stock from {request.branchName ?? 'stores'}{request.workOrderNumber ? ` and books it on ${request.workOrderNumber}` : ''}.</p> : null}
        {mode === 'issue' && isParts ? (
          <div className="space-y-3">
            <p className="text-sm text-slate-600">Dispatching takes the approved quantities out of stock and books them as used on {request.workOrderNumber ?? 'the work order'}. Anything unused comes back through a parts return.</p>
            {activeBranches.length > 1 ? (
              <div>
                <label className={labelClass} htmlFor="dispatch-branch">Send from</label>
                <select id="dispatch-branch" value={sourceBranchId} onChange={(event) => setSourceBranchId(event.target.value)} className={fieldClass}>
                  {request.branchId ? null : <option value="">Default branch</option>}
                  {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}{branch.id === request.branchId ? ' (job branch)' : ''}</option>)}
                </select>
              </div>
            ) : null}
          </div>
        ) : null}
        {mode === 'return' ? <p className="text-sm text-slate-600">Stock coming back to {request.branchName ?? 'stores'}. It is taken off the work order's parts used.</p> : null}
        {mode === 'close' ? <p className="text-sm text-slate-600">Record what was used on the job and what came back. Restock requests are retired; closing finishes this one.</p> : null}
        {mode === 'reject' ? (
          <div>
            <label className={labelClass} htmlFor="reject-reason">Reason (the requester sees this)</label>
            <textarea id="reject-reason" value={reason} onChange={(event) => setReason(event.target.value)} rows={3} placeholder="e.g. Out of stock until Friday — use the spare on van 2" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        ) : null}

        <div className="overflow-x-auto rounded-lg border border-slate-200">
          <table className="min-w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr>
                <th className="px-3 py-2">Part</th>
                <th className="px-3 py-2 text-right">Asked</th>
                {isParts ? <th className="px-3 py-2 text-right">Approved</th> : null}
                <th className="px-3 py-2 text-right">Issued</th>
                {!isParts ? <th className="px-3 py-2 text-right">Returned</th> : null}
                {mode === 'approve' && isParts ? <th className="px-3 py-2">Approve</th> : null}
                {mode === 'issue' && !isParts ? <th className="px-3 py-2">Issue now</th> : null}
                {mode === 'return' ? <th className="px-3 py-2">Return</th> : null}
                {mode === 'close' ? <><th className="px-3 py-2">Used</th><th className="px-3 py-2">Returned</th></> : null}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {request.lines.map((line) => (
                <tr key={line.id}>
                  <td className="px-3 py-2">
                    {line.materialItemId ? <Link className="text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${line.materialItemId}`}>{line.itemName ?? 'Part'}</Link> : <span className="text-slate-900">{line.itemName ?? 'Part'}</span>}
                    {line.itemCode ? <span className="ml-1 font-mono text-xs text-slate-500">{line.itemCode}</span> : null}
                    {!line.materialItemId ? <span className="ml-1 text-xs text-slate-500">(not a stocked part)</span> : null}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">{line.quantityRequested} {line.unitOfMeasure ?? ''}</td>
                  {isParts ? <td className="px-3 py-2 text-right tabular-nums">{line.quantityApproved ?? '—'}</td> : null}
                  <td className="px-3 py-2 text-right tabular-nums">{line.quantityIssued}</td>
                  {!isParts ? <td className="px-3 py-2 text-right tabular-nums">{line.quantityReturned}</td> : null}
                  {mode === 'approve' && isParts ? <td className="px-3 py-2">{quantityInput(line.id, line.quantityRequested, `Approve ${line.itemName ?? 'part'}`)}</td> : null}
                  {mode === 'issue' && !isParts ? <td className="px-3 py-2">{quantityInput(line.id, line.quantityRequested, `Issue ${line.itemName ?? 'part'}`)}</td> : null}
                  {mode === 'return' ? <td className="px-3 py-2">{quantityInput(line.id, line.quantityIssued - line.quantityReturned, `Return ${line.itemName ?? 'part'}`)}</td> : null}
                  {mode === 'close' ? (
                    <>
                      <td className="px-3 py-2">
                        <input type="number" min="0" step="any" aria-label={`Used ${line.itemName ?? 'part'}`} value={closeLines[line.id]?.used ?? ''} onChange={(event) => setCloseLines((current) => ({ ...current, [line.id]: { used: event.target.value, returned: current[line.id]?.returned ?? '0' } }))} className={qtyClass} />
                      </td>
                      <td className="px-3 py-2">
                        <input type="number" min="0" step="any" aria-label={`Returned ${line.itemName ?? 'part'}`} value={closeLines[line.id]?.returned ?? ''} onChange={(event) => setCloseLines((current) => ({ ...current, [line.id]: { used: current[line.id]?.used ?? '0', returned: event.target.value } }))} className={qtyClass} />
                      </td>
                    </>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {mode === 'view' && request.stage === 'Issued' && isParts ? <p className="text-xs text-slate-500">Waiting for the technician to confirm delivery in the field app.</p> : null}
        {mode === 'view' && actions.length === 0 && (request.stage === 'Requested' || request.stage === 'Approved') ? <p className="text-xs text-slate-500">You don't have permission to act on this request.</p> : null}
      </div>
    </FormDrawer>
  )
}

interface DraftLine {
  materialItemId: string
  quantity: string
}

/** A stores issue slip: parts handed out against a work order, a project, or neither. */
function NewIssueSlipDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const createIssue = useCreateMaterialIssue()
  const { data: materials } = useMaterials({ status: 'active' })
  const { data: workOrders } = useWorkOrders()
  const [workOrderId, setWorkOrderId] = useState('')
  const [projectId, setProjectId] = useState('')
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([{ materialItemId: '', quantity: '1' }])
  const workOrderClientId = (workOrders ?? []).find((wo) => wo.id === workOrderId)?.clientId ?? null

  const close = () => {
    setWorkOrderId('')
    setProjectId('')
    setNotes('')
    setLines([{ materialItemId: '', quantity: '1' }])
    onClose()
  }

  const submit = async () => {
    const payloadLines = lines
      .filter((line) => line.materialItemId && Number(line.quantity) > 0)
      .map((line) => ({ materialItemId: line.materialItemId, quantityRequested: Number(line.quantity) }))
    if (payloadLines.length === 0) return void toast.error('Add at least one part and quantity')
    try {
      const created = await createIssue.mutateAsync({
        workOrderId: workOrderId || null,
        projectId: projectId || null,
        notes: notes.trim() || null,
        lines: payloadLines,
      })
      toast.success(`Issue slip ${created.issueNumber} raised`)
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="New issue slip"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className={secondaryButton}>Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={createIssue.isPending} className={primaryButton}>
            {createIssue.isPending ? 'Saving…' : 'Raise issue slip'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <p className="text-xs text-slate-500">If you can issue stock and no approval is needed, the slip is issued straight away. Otherwise it waits in the queue for approval.</p>
        <div>
          <label className={labelClass} htmlFor="issue-wo">Work order (optional)</label>
          <select
            id="issue-wo"
            value={workOrderId}
            onChange={(event) => {
              setWorkOrderId(event.target.value)
              setProjectId('')
            }}
            className={fieldClass}
          >
            <option value="">Not linked to a work order</option>
            {(workOrders ?? []).map((wo) => <option key={wo.id} value={wo.id}>{wo.workOrderNumber} — {wo.title}</option>)}
          </select>
        </div>
        <ProjectPicker id="issue-project" value={projectId} onChange={setProjectId} clientId={workOrderClientId} className={fieldClass} labelClassName={labelClass} />
        <div>
          <span className={labelClass}>Parts</span>
          <div className="space-y-2">
            {lines.map((line, index) => (
              <div key={index} className="flex items-center gap-2">
                <select
                  value={line.materialItemId}
                  onChange={(event) => setLines((current) => current.map((l, i) => (i === index ? { ...l, materialItemId: event.target.value } : l)))}
                  className={`${fieldClass} flex-1`}
                  aria-label="Part"
                >
                  <option value="">Select a part…</option>
                  {(materials ?? []).map((material) => <option key={material.id} value={material.id}>{material.itemName} ({material.quantityOnHand} {material.unitOfMeasure} on hand)</option>)}
                </select>
                <input
                  type="number"
                  min="0"
                  step="any"
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
            + Add part
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
