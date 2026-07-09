import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate } from '../../shared/utils/dates'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { usePayable, usePayables, useRecordPayablePayment, type PayableListItem, type PayableStatus } from './expensesPayablesApi'

const backendGapNote = 'No tenant-level Payables/supplier-bill controller exists in the current backend yet. This is the full intended list + detail UI wired against typed stub data — recording a payment will not persist until a real endpoint is connected.'
const statusOptions: Array<PayableStatus | 'All'> = ['All', 'Open', 'PartiallyPaid', 'Paid', 'Overdue']

export function PayablesPage() {
  const [statusFilter, setStatusFilter] = useState<PayableStatus | 'All'>('All')
  const { data, isLoading } = usePayables(statusFilter)

  const columns: DataTableColumn<PayableListItem>[] = [
    { key: 'billNumber', header: 'Bill #', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/payables/${row.id}`}>{row.billNumber}</Link> },
    { key: 'supplierName', header: 'Supplier', cell: (row) => row.supplierName },
    { key: 'dueDate', header: 'Due Date', cell: (row) => formatDate(row.dueDate) },
    { key: 'amount', header: 'Amount', cell: (row) => `${row.currency} ${row.amount.toLocaleString()}` },
    { key: 'amountPaid', header: 'Paid', cell: (row) => `${row.currency} ${row.amountPaid.toLocaleString()}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Payables" subtitle="Bills to pay and supplier invoices." />
      <FinanceWorkspaceNav />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      <div className="flex flex-wrap gap-2 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        {statusOptions.map((status) => (
          <button
            key={status}
            type="button"
            onClick={() => setStatusFilter(status)}
            className={`inline-flex h-10 items-center rounded-lg px-4 text-sm font-medium transition ${statusFilter === status ? 'bg-slate-900 text-white' : 'text-slate-600 hover:bg-slate-100'}`}
          >
            {status === 'PartiallyPaid' ? 'Partially Paid' : status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle="No payables yet"
        emptyDescription="Supplier bills and payables will appear here once a tenant-scoped Payables endpoint is connected."
      />
    </div>
  )
}

export function PayableDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading } = usePayable(id)
  const recordPayment = useRecordPayablePayment(id ?? '')
  const [showPayment, setShowPayment] = useState(false)
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState('Bank Transfer')
  const [reference, setReference] = useState('')

  const submitPayment = async () => {
    try {
      await recordPayment.mutateAsync({ amount: Number(amount), method, reference: reference || undefined })
      toast.success('Payment recorded')
      setShowPayment(false)
    } catch {
      toast.error('Payments are not yet backed by a tenant-scoped API — this is a UI stub.')
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={data?.billNumber ?? 'Payable'}
        subtitle="Supplier bill detail and payment history."
        breadcrumbs={[{ label: 'Payables', to: '/finance/payables' }, { label: data?.billNumber ?? 'Detail' }]}
        secondaryActions={data ? <StatusBadge status={data.status} /> : undefined}
        primaryAction={
          data && data.status !== 'Paid' ? (
            <button type="button" onClick={() => setShowPayment(true)} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Record Payment</button>
          ) : undefined
        }
      />
      <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">{backendGapNote}</div>
      {isLoading ? null : !data ? (
        <EmptyState title="No payable detail yet" description="Supplier bill detail and payment history will appear here once a tenant-scoped Payables endpoint is connected." />
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
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
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
            <input type="number" min={0} value={amount} onChange={(event) => setAmount(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Method</label>
            <select value={method} onChange={(event) => setMethod(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900">
              <option>Bank Transfer</option>
              <option>M-Pesa</option>
              <option>Cheque</option>
              <option>Cash</option>
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Reference (optional)</label>
            <input value={reference} onChange={(event) => setReference(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-slate-900" />
          </div>
          <button
            type="button"
            disabled={!amount || recordPayment.isPending}
            onClick={() => void submitPayment()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60"
          >
            {recordPayment.isPending ? 'Recording…' : 'Record Payment'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}
