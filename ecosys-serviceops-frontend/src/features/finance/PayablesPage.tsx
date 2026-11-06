import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { getActiveCurrency } from '../../shared/utils/currency'
import { formatDate } from '../../shared/utils/dates'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { useSuppliers } from '../inventory/purchaseOrdersApi'
import { useCreatePayable, usePayable, usePayables, useRecordPayablePayment, type PayableListItem, type PayableStatus } from './expensesPayablesApi'

const statusOptions: Array<PayableStatus | 'All'> = ['All', 'Open', 'PartiallyPaid', 'Paid', 'Overdue']

export function PayablesPage() {
  const canManage = usePermission('finance.manage')
  const [statusFilter, setStatusFilter] = useState<PayableStatus | 'All'>('All')
  const { data, isLoading, isError, refetch } = usePayables(statusFilter)
  const { data: suppliers } = useSuppliers()
  const createPayable = useCreatePayable()
  const [showNewBill, setShowNewBill] = useState(false)
  const [supplierId, setSupplierId] = useState('')
  const [amount, setAmount] = useState('')
  const [dueDate, setDueDate] = useState('')
  const [notes, setNotes] = useState('')
  const [currency, setCurrency] = useState(getActiveCurrency)

  const submit = async () => {
    if (!supplierId) return toast.error('Select a supplier')
    if (!amount || !dueDate) return toast.error('Amount and due date are required')
    await createPayable.mutateAsync({ supplierId, amount: Number(amount), dueDate, notes: notes || undefined, currency: currency.trim() || getActiveCurrency() })
    toast.success('Bill created')
    setShowNewBill(false)
    setSupplierId('')
    setAmount('')
    setDueDate('')
    setNotes('')
    setCurrency(getActiveCurrency())
  }

  const columns: DataTableColumn<PayableListItem>[] = [
    { key: 'billNumber', header: 'Bill #', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/payables/${row.id}`}>{row.billNumber}</Link> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'dueDate', header: 'Due Date', cell: (row) => formatDate(row.dueDate) },
    { key: 'amount', header: 'Amount', cell: (row) => `${row.currency} ${row.amount.toLocaleString()}` },
    { key: 'amountPaid', header: 'Paid', cell: (row) => `${row.currency} ${row.amountPaid.toLocaleString()}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Payables"
        subtitle="Bills to pay and supplier invoices."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowNewBill(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Bill
            </button>
          ) : undefined
        }
      />
      <FinanceWorkspaceNav />
      <div className="flex flex-wrap gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-3 shadow-sm">
        {statusOptions.map((status) => (
          <button
            key={status}
            type="button"
            onClick={() => setStatusFilter(status)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${statusFilter === status ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {status === 'PartiallyPaid' ? 'Partially Paid' : status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Payables unavailable' : 'No payables yet'}
        emptyDescription={isError ? 'Supplier bills could not be loaded — this is not a confirmation that you owe nothing.' : 'Record a supplier bill to track what you owe.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      <FormDrawer open={showNewBill} title="New Supplier Bill" onClose={() => setShowNewBill(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Supplier</label>
            <select value={supplierId} onChange={(event) => setSupplierId(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option value="">Select a supplier…</option>
              {(suppliers ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}
            </select>
            {suppliers && suppliers.length === 0 ? <p className="mt-1 text-xs text-amber-600">No suppliers yet — add one on the Suppliers page first.</p> : null}
          </div>
          <div className="grid grid-cols-[1fr_6rem] gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Amount</label>
              <input type="number" min={0} value={amount} onChange={(event) => setAmount(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label htmlFor="bill-currency" className="mb-1.5 block text-sm font-medium text-slate-700">Currency</label>
              <input id="bill-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Due Date</label>
            <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Notes</label>
            <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!supplierId || !amount || !dueDate || createPayable.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createPayable.isPending ? 'Creating…' : 'Create Bill'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

export function PayableDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError, refetch } = usePayable(id)
  const recordPayment = useRecordPayablePayment(id ?? '')
  const [showPayment, setShowPayment] = useState(false)
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState('Bank Transfer')
  const [reference, setReference] = useState('')

  const submitPayment = async () => {
    await recordPayment.mutateAsync({ amount: Number(amount), method, reference: reference || undefined })
    toast.success('Payment recorded')
    setShowPayment(false)
    setAmount('')
    setReference('')
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={data?.billNumber ?? 'Payable'}
        subtitle="Supplier bill detail and payment history."
        breadcrumbs={[{ label: 'Payables', to: '/finance/payables' }, { label: data?.billNumber ?? 'Detail' }]}
        secondaryActions={data ? <StatusBadge status={data.status} /> : undefined}
        primaryAction={
          data && data.status !== 'Paid' ? (
            <button type="button" onClick={() => setShowPayment(true)} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">Record Payment</button>
          ) : undefined
        }
      />
      {isLoading ? null : isError ? (
        <ErrorState
          title="Payable unavailable"
          description="We couldn’t load this supplier bill, so we can’t say whether it still exists."
          onRetry={() => void refetch()}
        />
      ) : !data ? (
        <EmptyState title="Payable not found" description="This supplier bill may have been removed." />
      ) : (
        <>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            {[
              ['Supplier', data.supplierName],
              ['Due Date', formatDate(data.dueDate)],
              ['Amount', `${data.currency} ${data.amount.toLocaleString()}`],
              ['Paid', `${data.currency} ${data.amountPaid.toLocaleString()}`],
            ].map(([label, value]) => (
              <div key={label} className="rounded-xl border border-slate-200 bg-white px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                <div className="mt-2 text-sm text-slate-900">{value}</div>
              </div>
            ))}
          </div>
          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <h2 className="text-sm font-semibold uppercase tracking-[0.18em] text-slate-500">Payment History</h2>
            {data.paymentHistory.length === 0 ? (
              <p className="mt-3 text-sm text-slate-500">No payments recorded yet.</p>
            ) : (
              <ul className="mt-4 space-y-3">
                {data.paymentHistory.map((payment, index) => (
                  <li key={index} className="flex items-center justify-between border-b border-slate-100 pb-3 text-sm last:border-0 last:pb-0">
                    <span className="text-slate-700">{payment.method} {payment.reference ? `(${payment.reference})` : ''}</span>
                    <span className="font-medium text-slate-900">{payment.amount.toLocaleString()} — {formatDate(payment.paidAt)}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </>
      )}

      <FormDrawer open={showPayment} title="Record Payment" onClose={() => setShowPayment(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Amount</label>
            <input type="number" min={0} value={amount} onChange={(event) => setAmount(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Method</label>
            <select value={method} onChange={(event) => setMethod(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option>Bank Transfer</option>
              <option>M-Pesa</option>
              <option>Cheque</option>
              <option>Cash</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Reference (optional)</label>
            <input value={reference} onChange={(event) => setReference(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!amount || recordPayment.isPending}
            onClick={() => void submitPayment()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {recordPayment.isPending ? 'Recording…' : 'Record Payment'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
