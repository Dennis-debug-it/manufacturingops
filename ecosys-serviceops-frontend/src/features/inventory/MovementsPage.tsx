import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ArrowLeftRight } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { formatDateTime } from '../../shared/utils/dates'
import {
  useApproveStockTransfer,
  useBranches,
  useCancelStockTransfer,
  useCompleteStockTransfer,
  useCreateStockTransfer,
  useMaterials,
  useStockLedger,
  useStockTransfers,
} from './api'
import type { StockLedgerEntry, StockTransfer } from './types'

const movementTypes = ['Receipt', 'Issue', 'Return', 'Adjustment', 'TransferIn', 'TransferOut', 'VanLoad', 'VanReturn', 'VanConsume']
const typeLabels: Record<string, string> = {
  TransferIn: 'Transfer In',
  TransferOut: 'Transfer Out',
}
const inputClass = 'h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]'

export function MovementsPage() {
  const [view, setView] = useState<'ledger' | 'transfers'>('ledger')
  return (
    <div className="space-y-4">
      <PageHeader
        title="Stock Movements"
        subtitle="Every receipt, issue, return, adjustment, transfer and van move, and the branch-to-branch transfer queue."
        secondaryActions={
          <div className="flex items-center gap-2">
            {(['ledger', 'transfers'] as const).map((option) => (
              <button
                key={option}
                type="button"
                onClick={() => setView(option)}
                className={cn('inline-flex h-10 items-center rounded-full border px-3 text-sm font-medium transition', view === option ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}
              >
                {option === 'ledger' ? 'All Movements' : 'Transfers'}
              </button>
            ))}
          </div>
        }
      />
      {view === 'ledger' ? <LedgerView /> : <TransfersView />}
    </div>
  )
}

function formatChange(row: StockLedgerEntry) {
  if (row.quantityChange == null) return `±${row.quantity}`
  return row.quantityChange > 0 ? `+${row.quantityChange}` : `${row.quantityChange}`
}

function LedgerView() {
  const [search, setSearch] = useState('')
  const [type, setType] = useState('')
  const [branchId, setBranchId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const debouncedSearch = useDebounce(search, 300)
  const { data: branches } = useBranches()
  const { data, isLoading, isError, refetch } = useStockLedger({ search: debouncedSearch.trim(), type, branchId, from, to, page, pageSize })

  const resetPage = <T,>(setter: (value: T) => void) => (value: T) => {
    setter(value)
    setPage(1)
  }

  const columns: DataTableColumn<StockLedgerEntry>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => <span className="whitespace-nowrap text-slate-600">{formatDateTime(row.createdAt)}</span> },
    {
      key: 'item',
      header: 'Part',
      cell: (row) => (
        <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialId}`}>
          {row.itemName ?? 'Part'}
          {row.itemCode ? <span className="ml-1 font-mono text-xs text-slate-500">{row.itemCode}</span> : null}
        </Link>
      ),
    },
    { key: 'movementType', header: 'Type', cell: (row) => <StatusBadge status={typeLabels[row.movementType] ?? row.movementType} /> },
    {
      key: 'change',
      header: 'Change',
      cell: (row) => (
        <span className={cn('font-semibold tabular-nums', row.quantityChange == null ? 'text-slate-600' : row.quantityChange > 0 ? 'text-emerald-700' : 'text-red-600')}>
          {formatChange(row)} <span className="font-normal text-slate-500">{row.unitOfMeasure ?? ''}</span>
        </span>
      ),
    },
    { key: 'balanceAfter', header: 'Balance After', cell: (row) => <span className="tabular-nums">{row.balanceAfter}</span> },
    { key: 'location', header: 'Location', cell: (row) => row.branchName ?? '—' },
    {
      key: 'reference',
      header: 'Reference',
      cell: (row) =>
        row.workOrderId ? (
          <Link className="font-mono text-xs underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.workOrderId}`}>{row.workOrderNumber ?? row.referenceNumber}</Link>
        ) : (
          <span className="font-mono text-xs text-slate-600">{row.referenceNumber ?? '—'}</span>
        ),
    },
    { key: 'reason', header: 'Reason', cell: (row) => <span className="text-slate-600">{row.reason ?? '—'}</span> },
    { key: 'batchOrSerial', header: 'Batch / Serial', cell: (row) => (row.batchOrSerial ? <span className="font-mono text-xs">{row.batchOrSerial}</span> : '—') },
    { key: 'createdByName', header: 'By', cell: (row) => row.createdByName ?? '—' },
  ]

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-3 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
        <input aria-label="Search movements" placeholder="Part, code, reference, reason or serial…" value={search} onChange={(event) => resetPage(setSearch)(event.target.value)} className={cn(inputClass, 'min-w-[14rem] flex-1')} />
        <select aria-label="Movement type" value={type} onChange={(event) => resetPage(setType)(event.target.value)} className={inputClass}>
          <option value="">All types</option>
          {movementTypes.map((value) => <option key={value} value={value}>{typeLabels[value] ?? value}</option>)}
        </select>
        {(branches ?? []).length > 0 ? (
          <select aria-label="Branch" value={branchId} onChange={(event) => resetPage(setBranchId)(event.target.value)} className={inputClass}>
            <option value="">All branches</option>
            {(branches ?? []).map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select>
        ) : null}
        <label className="flex flex-col gap-1 text-xs font-medium text-slate-500">
          From
          <input type="date" value={from} onChange={(event) => resetPage(setFrom)(event.target.value)} className={inputClass} />
        </label>
        <label className="flex flex-col gap-1 text-xs font-medium text-slate-500">
          To
          <input type="date" value={to} onChange={(event) => resetPage(setTo)(event.target.value)} className={inputClass} />
        </label>
      </div>
      <DataTable
        columns={columns}
        data={data?.items ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Movements unavailable' : 'No movements'}
        emptyDescription={isError ? 'The stock ledger could not be loaded — this is not a confirmation that nothing moved.' : 'Nothing matches these filters.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size)
            setPage(1)
          },
        }}
      />
    </div>
  )
}

type TransferAction = { transfer: StockTransfer; type: 'approve' | 'complete' | 'cancel' } | null

function TransferActions({ transfer, onAction }: { transfer: StockTransfer; onAction: (action: TransferAction) => void }) {
  const canIssue = usePermission('inventory.requests.issue')
  const button = 'rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50'
  return (
    <div className="flex justify-end gap-2">
      {canIssue && transfer.status === 'Submitted' ? <button type="button" className={button} onClick={() => onAction({ transfer, type: 'approve' })}>Approve</button> : null}
      {canIssue && transfer.status === 'Approved' ? <button type="button" className={button} onClick={() => onAction({ transfer, type: 'complete' })}>Complete</button> : null}
      {transfer.status === 'Submitted' || transfer.status === 'Approved' ? (
        <button type="button" className="rounded-lg border border-red-200 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50" onClick={() => onAction({ transfer, type: 'cancel' })}>Cancel</button>
      ) : null}
    </div>
  )
}

function TransferActionDialog({ action, onClose }: { action: NonNullable<TransferAction>; onClose: () => void }) {
  const approve = useApproveStockTransfer(action.transfer.id)
  const complete = useCompleteStockTransfer(action.transfer.id)
  const cancel = useCancelStockTransfer(action.transfer.id)
  const { transfer, type } = action
  const verb = type === 'approve' ? 'Approve' : type === 'complete' ? 'Complete' : 'Cancel'
  const message =
    type === 'complete'
      ? `This moves ${transfer.quantity} × ${transfer.materialName ?? 'part'} out of ${transfer.fromBranchName ?? 'the source branch'} and into ${transfer.toBranchName ?? 'the destination branch'}.`
      : `This will ${type} transfer ${transfer.referenceNumber ?? ''}.`

  const run = async () => {
    try {
      if (type === 'approve') await approve.mutateAsync()
      if (type === 'complete') await complete.mutateAsync()
      if (type === 'cancel') await cancel.mutateAsync()
      toast.success(`Transfer ${type === 'approve' ? 'approved' : type === 'complete' ? 'completed' : 'cancelled'}`)
    } catch {
      // interceptor surfaces the error toast
    } finally {
      onClose()
    }
  }

  return <ConfirmDialog open title={`${verb} Transfer`} message={message} onCancel={onClose} onConfirm={run} />
}

function TransfersView() {
  const { data, isLoading, isError, refetch } = useStockTransfers()
  const { data: branches } = useBranches()
  const [showNew, setShowNew] = useState(false)
  const [action, setAction] = useState<TransferAction>(null)
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)

  const columns: DataTableColumn<StockTransfer>[] = [
    { key: 'referenceNumber', header: 'Ref', cell: (row) => <span className="font-mono text-xs">{row.referenceNumber ?? '—'}</span> },
    { key: 'materialName', header: 'Part', cell: (row) => <Link className="underline decoration-slate-300 underline-offset-4" to={`/inventory/parts/${row.materialId}`}>{row.materialName ?? 'Part'}</Link> },
    { key: 'route', header: 'Route', cell: (row) => `${row.fromBranchName ?? '—'} → ${row.toBranchName ?? '—'}` },
    { key: 'quantity', header: 'Qty', cell: (row) => row.quantity },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'reason', header: 'Reason', cell: (row) => row.reason ?? '—' },
    { key: 'createdAt', header: 'Requested', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actions', header: '', cell: (row) => <TransferActions transfer={row} onAction={setAction} /> },
  ]

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-slate-600">Submitted → Approved → Completed. Stock moves only when a transfer is completed.</p>
        {activeBranches.length >= 2 ? (
          <button type="button" onClick={() => setShowNew(true)} className="inline-flex h-10 shrink-0 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <ArrowLeftRight className="h-4 w-4" />New Transfer
          </button>
        ) : null}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Transfers unavailable' : 'No transfers'}
        emptyDescription={isError ? 'Stock transfers could not be loaded.' : activeBranches.length >= 2 ? 'Move stock between branches with New Transfer.' : 'Transfers need at least two active branches.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />
      {showNew ? <NewTransferDrawer onClose={() => setShowNew(false)} /> : null}
      {action ? <TransferActionDialog action={action} onClose={() => setAction(null)} /> : null}
    </div>
  )
}

function NewTransferDrawer({ onClose }: { onClose: () => void }) {
  const { data: branches } = useBranches()
  const activeBranches = (branches ?? []).filter((branch) => branch.isActive)
  const [fromBranchId, setFromBranchId] = useState('')
  const [toBranchId, setToBranchId] = useState('')
  const [materialId, setMaterialId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [reason, setReason] = useState('')
  // Stock at the source branch, so the picker shows what can actually be sent.
  const { data: sourceStock } = useMaterials({ status: 'active', branchId: fromBranchId || undefined })
  const create = useCreateStockTransfer()
  const selected = (sourceStock ?? []).find((item) => item.id === materialId)

  const submit = async () => {
    const qty = Number(quantity)
    if (!fromBranchId || !toBranchId) return toast.error('Choose both branches')
    if (fromBranchId === toBranchId) return toast.error('Choose two different branches')
    if (!materialId) return toast.error('Choose a part')
    if (!Number.isFinite(qty) || qty <= 0) return toast.error('Enter a quantity greater than zero')
    try {
      await create.mutateAsync({ fromBranchId, toBranchId, materialId, quantity: qty, reason: reason.trim() || null })
      toast.success('Transfer submitted')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const field = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'
  return (
    <FormDrawer
      open
      title="New Transfer"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={() => void submit()} disabled={create.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {create.isPending ? 'Submitting…' : 'Submit Transfer'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="transfer-from">From branch</label>
          <select id="transfer-from" value={fromBranchId} onChange={(event) => { setFromBranchId(event.target.value); setMaterialId('') }} className={field}>
            <option value="">Select…</option>
            {activeBranches.map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="transfer-to">To branch</label>
          <select id="transfer-to" value={toBranchId} onChange={(event) => setToBranchId(event.target.value)} className={field}>
            <option value="">Select…</option>
            {activeBranches.filter((branch) => branch.id !== fromBranchId).map((branch) => <option key={branch.id} value={branch.id}>{branch.name}</option>)}
          </select>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="transfer-part">Part</label>
          <select id="transfer-part" value={materialId} disabled={!fromBranchId} onChange={(event) => setMaterialId(event.target.value)} className={cn(field, 'disabled:bg-slate-50')}>
            <option value="">{fromBranchId ? 'Select a part…' : 'Choose the source branch first'}</option>
            {(sourceStock ?? []).filter((item) => item.quantityOnHand > 0).map((item) => (
              <option key={item.id} value={item.id}>{item.itemName} ({item.itemCode}) — {item.quantityOnHand} {item.unitOfMeasure} on hand</option>
            ))}
          </select>
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="transfer-qty">Quantity</label>
          <input id="transfer-qty" type="number" min="0" step="0.01" value={quantity} onChange={(event) => setQuantity(event.target.value)} className={field} />
          {selected ? <p className="mt-1 text-xs text-slate-500">{selected.quantityOnHand} {selected.unitOfMeasure} at the source branch now.</p> : null}
        </div>
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="transfer-reason">Reason</label>
          <input id="transfer-reason" value={reason} onChange={(event) => setReason(event.target.value)} placeholder="e.g. Rebalancing for the Mombasa job" className={field} />
        </div>
      </div>
    </FormDrawer>
  )
}
