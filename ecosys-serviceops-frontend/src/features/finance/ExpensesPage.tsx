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
import { getActiveCurrency } from '../../shared/utils/currency'
import { formatDate } from '../../shared/utils/dates'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { useCreateExpense, useDecideExpense, useExpense, useExpenses, type ExpenseListItem, type ExpenseStatus } from './expensesPayablesApi'

const statusOptions: Array<ExpenseStatus | 'All'> = ['All', 'Draft', 'Submitted', 'Approved', 'Rejected', 'Reimbursed']

export function ExpensesPage() {
  const [statusFilter, setStatusFilter] = useState<ExpenseStatus | 'All'>('All')
  const { data, isLoading, isError, refetch } = useExpenses(statusFilter)
  const createExpense = useCreateExpense()
  const [showNewExpense, setShowNewExpense] = useState(false)
  const [category, setCategory] = useState('Travel')
  const [amount, setAmount] = useState('')
  const [incurredAt, setIncurredAt] = useState('')
  const [description, setDescription] = useState('')
  const [currency, setCurrency] = useState(getActiveCurrency)

  const submit = async () => {
    await createExpense.mutateAsync({ category, amount: Number(amount), incurredAt, description: description || undefined, currency: currency.trim() || getActiveCurrency() })
    toast.success('Expense claim submitted')
    setShowNewExpense(false)
    setAmount('')
    setIncurredAt('')
    setDescription('')
    setCurrency(getActiveCurrency())
  }

  const columns: DataTableColumn<ExpenseListItem>[] = [
    { key: 'claimNumber', header: 'Claim #', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/expenses/${row.id}`}>{row.claimNumber}</Link> },
    { key: 'submittedByName', header: 'Submitted By', cell: (row) => row.submittedByName },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'incurredAt', header: 'Date', cell: (row) => formatDate(row.incurredAt) },
    { key: 'amount', header: 'Amount', cell: (row) => `${row.currency} ${row.amount.toLocaleString()}` },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Expenses"
        subtitle="Expense claims and reimbursements."
        primaryAction={
          <button type="button" onClick={() => setShowNewExpense(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
            <Plus className="h-4 w-4" />New Expense Claim
          </button>
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
            {status}
          </button>
        ))}
      </div>
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Expense claims unavailable' : 'No expense claims yet'}
        emptyDescription={isError ? 'Expense claims could not be loaded — this is not a confirmation that none exist.' : 'Submit a claim to start the approval workflow.'}
        emptyActionLabel={isError ? 'Retry' : undefined}
        onEmptyAction={isError ? () => void refetch() : undefined}
      />

      <FormDrawer open={showNewExpense} title="New Expense Claim" onClose={() => setShowNewExpense(false)}>
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Category</label>
            <select value={category} onChange={(event) => setCategory(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              <option>Travel</option>
              <option>Fuel</option>
              <option>Meals</option>
              <option>Tools & Equipment</option>
              <option>Other</option>
            </select>
          </div>
          <div className="grid grid-cols-[1fr_6rem] gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700">Amount</label>
              <input type="number" min={0} value={amount} onChange={(event) => setAmount(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label htmlFor="expense-currency" className="mb-1.5 block text-sm font-medium text-slate-700">Currency</label>
              <input id="expense-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Date Incurred</label>
            <input type="date" value={incurredAt} onChange={(event) => setIncurredAt(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Description</label>
            <textarea value={description} onChange={(event) => setDescription(event.target.value)} rows={3} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
          <button
            type="button"
            disabled={!amount || !incurredAt || createExpense.isPending}
            onClick={() => void submit()}
            className="inline-flex h-11 w-full items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            {createExpense.isPending ? 'Submitting…' : 'Submit Claim'}
          </button>
        </div>
      </FormDrawer>
    </div>
  )
}

export function ExpenseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError, refetch } = useExpense(id)
  const decide = useDecideExpense(id ?? '')

  const respond = async (approve: boolean) => {
    await decide.mutateAsync({ approve })
    toast.success(approve ? 'Expense approved' : 'Expense rejected')
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={data?.claimNumber ?? 'Expense'}
        subtitle="Expense review and status changes."
        breadcrumbs={[{ label: 'Expenses', to: '/finance/expenses' }, { label: data?.claimNumber ?? 'Detail' }]}
        secondaryActions={data ? <StatusBadge status={data.status} /> : undefined}
        primaryAction={
          data && data.status === 'Submitted' ? (
            <div className="flex gap-2">
              <button type="button" onClick={() => void respond(false)} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 hover:bg-rose-50">Reject</button>
              <button type="button" onClick={() => void respond(true)} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">Approve</button>
            </div>
          ) : undefined
        }
      />
      {isLoading ? null : isError ? (
        <ErrorState
          title="Expense unavailable"
          description="We couldn’t load this expense claim, so we can’t say whether it still exists."
          onRetry={() => void refetch()}
        />
      ) : !data ? (
        <EmptyState title="Expense not found" description="This expense claim may have been removed." />
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            ['Submitted By', data.submittedByName],
            ['Category', data.category],
            ['Amount', `${data.currency} ${data.amount.toLocaleString()}`],
            ['Date Incurred', formatDate(data.incurredAt)],
            ['Approved By', data.approvedByName ?? '—'],
            ['Approved At', data.approvedAt ? formatDate(data.approvedAt) : '—'],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl border border-slate-200 bg-white px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 text-sm text-slate-900">{value}</div>
            </div>
          ))}
          {data.description ? (
            <div className="rounded-xl border border-slate-200 bg-white px-4 py-3 sm:col-span-2 xl:col-span-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Description</div>
              <p className="mt-2 text-sm text-slate-700">{data.description}</p>
            </div>
          ) : null}
        </div>
      )}
    </div>
  )
}
