import { useMemo, useState, type ReactNode } from 'react'
import { Plus, Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../shared/components/PageHeader'
import { AsyncPanel } from '../shared/components/AsyncPanel'
import { LastUpdated } from '../shared/components/LastUpdated'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { FormDrawer } from '../shared/components/FormDrawer'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { formatDate } from '../shared/utils/dates'
import { formatCurrency } from '../shared/utils/format'
import { api } from '../shared/api/apiClient'
import { endpoints } from '../shared/api/endpoints'
import { useFinanceDashboard, useFinanceExpenses, useFinanceInvoices, useFinancePayments, useFinanceQuotations } from './api'
import { Field } from './common'
import type { PlatformExpense, PlatformInvoice, PlatformPayment, PlatformQuotation } from './types'

type TabId = 'overview' | 'quotations' | 'invoices' | 'payments' | 'expenses' | 'reports' | 'tax' | 'templates'

const tabs: Array<{ id: TabId; label: string }> = [
  { id: 'overview', label: 'Overview' },
  { id: 'quotations', label: 'Quotations' },
  { id: 'invoices', label: 'Invoices' },
  { id: 'payments', label: 'Payments' },
  { id: 'expenses', label: 'Expenses' },
  { id: 'reports', label: 'Reports' },
  { id: 'tax', label: 'Tax Settings' },
  { id: 'templates', label: 'Templates' },
]

type FinanceAction = 'send' | 'mark-paid' | 'void' | 'convert' | 'approve'

export function FinancePage() {
  const dashboard = useFinanceDashboard()
  const quotations = useFinanceQuotations()
  const invoices = useFinanceInvoices()
  const payments = useFinancePayments()
  const expenses = useFinanceExpenses()
  const [tab, setTab] = useState<TabId>('overview')
  const [search, setSearch] = useState('')
  const [createKind, setCreateKind] = useState<'quotation' | 'invoice' | 'payment' | 'expense' | null>(null)
  const [actionTarget, setActionTarget] = useState<PlatformInvoice | PlatformQuotation | PlatformExpense | null>(null)
  const [action, setAction] = useState<FinanceAction | null>(null)

  const query = search.trim().toLowerCase()
  const createFormId = 'platform-finance-create-form'

  const filteredQuotations = useMemo(
    () => (quotations.data ?? []).filter((row) => !query || row.quotationNumber.toLowerCase().includes(query) || row.customerName.toLowerCase().includes(query)),
    [quotations.data, query],
  )
  const filteredInvoices = useMemo(
    () => (invoices.data ?? []).filter((row) => !query || row.invoiceNumber.toLowerCase().includes(query) || row.customerName.toLowerCase().includes(query)),
    [invoices.data, query],
  )
  const filteredPayments = useMemo(
    () => (payments.data ?? []).filter((row) => !query || row.paymentNumber.toLowerCase().includes(query) || row.method.toLowerCase().includes(query)),
    [payments.data, query],
  )
  const filteredExpenses = useMemo(
    () => (expenses.data ?? []).filter((row) => !query || row.category.toLowerCase().includes(query) || (row.vendor ?? '').toLowerCase().includes(query)),
    [expenses.data, query],
  )

  const quotationColumns: DataTableColumn<PlatformQuotation>[] = [
    { key: 'quotationNumber', header: 'Quotation', cell: (row) => row.quotationNumber },
    { key: 'customerName', header: 'Customer', cell: (row) => row.customerName },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.total, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    { key: 'validUntil', header: 'Valid until', cell: (row) => (row.validUntil ? formatDate(row.validUntil) : '—') },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <div className="flex flex-wrap gap-2">
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'send')}>
            Send
          </button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'convert')}>
            Convert to invoice
          </button>
        </div>
      ),
    },
  ]

  const invoiceColumns: DataTableColumn<PlatformInvoice>[] = [
    { key: 'invoiceNumber', header: 'Invoice', cell: (row) => row.invoiceNumber },
    { key: 'customerName', header: 'Customer', cell: (row) => row.customerName },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.total, row.currency) },
    { key: 'paid', header: 'Paid', cell: (row) => formatCurrency(row.amountPaid, row.currency) },
    { key: 'balance', header: 'Balance', cell: (row) => formatCurrency(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <div className="flex flex-wrap gap-2">
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'send')}>
            Send
          </button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'mark-paid')}>
            Mark paid
          </button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'void')}>
            Void
          </button>
        </div>
      ),
    },
  ]

  const paymentColumns: DataTableColumn<PlatformPayment>[] = [
    { key: 'paymentNumber', header: 'Payment', cell: (row) => row.paymentNumber },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount, row.currency) },
    { key: 'method', header: 'Method', cell: (row) => row.method },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    { key: 'paidAt', header: 'Date', cell: (row) => formatDate(row.paidAt) },
    { key: 'reference', header: 'Reference', cell: (row) => row.reference ?? '—' },
  ]

  const expenseColumns: DataTableColumn<PlatformExpense>[] = [
    { key: 'expenseDate', header: 'Date', cell: (row) => formatDate(row.expenseDate) },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'vendor', header: 'Vendor', cell: (row) => row.vendor ?? '—' },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => row.status },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => openAction(row, 'approve')}>
          Approve
        </button>
      ),
    },
  ]

  function openAction(row: PlatformInvoice | PlatformQuotation | PlatformExpense, nextAction: FinanceAction) {
    setActionTarget(row)
    setAction(nextAction)
  }

  const confirmAction = async () => {
    if (!actionTarget || !action) return
    try {
      if ('quotationNumber' in actionTarget) {
        if (action === 'send') await api.post(endpoints.platformFinance.quotationSend(actionTarget.id))
        if (action === 'convert') await api.post(endpoints.platformFinance.quotationConvert(actionTarget.id))
      } else if ('invoiceNumber' in actionTarget) {
        if (action === 'send') await api.post(endpoints.platformFinance.invoiceSend(actionTarget.id))
        if (action === 'mark-paid') await api.post(endpoints.platformFinance.invoiceMarkPaid(actionTarget.id), {})
        if (action === 'void') await api.post(endpoints.platformFinance.invoiceVoid(actionTarget.id))
      } else if ('expenseDate' in actionTarget) {
        if (action === 'approve') await api.post(endpoints.platformFinance.expenseApprove(actionTarget.id))
      }
      toast.success('Finance action completed')
      setActionTarget(null)
      setAction(null)
    } catch {
      // handled by interceptor
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Finance"
        subtitle="Subscriptions, invoices, quotations, payments, expenses, and reporting."
        secondaryActions={<LastUpdated at={dashboard.dataUpdatedAt} />}
        primaryAction={(
          <button
            type="button"
            className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]"
            onClick={() => setCreateKind('invoice')}
          >
            <Plus className="h-4 w-4" />
            Create
          </button>
        )}
      />

      <div className="flex flex-wrap gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-2 shadow-sm">
        {tabs.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => setTab(item.id)}
            className={`rounded-lg px-3 py-2 text-sm font-medium ${tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'}`}
          >
            {item.label}
          </button>
        ))}
      </div>

      <div className="flex items-center gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <Search className="h-4 w-4 text-slate-400" />
        <input
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Search records"
          className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400"
        />
      </div>

      {tab === 'overview' ? (
        <AsyncPanel query={dashboard} loadingLabel="Loading finance overview" errorTitle="Overview unavailable" errorDescription="Finance overview could not be loaded.">
          {() => (
            <div className="space-y-5">
              <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                <Kpi label="Revenue" value={formatCurrency(dashboard.data?.totalRevenue ?? 0)} />
                <Kpi label="Outstanding" value={formatCurrency(dashboard.data?.outstandingInvoices ?? 0)} />
                <Kpi label="Overdue" value={formatCurrency(dashboard.data?.overdueInvoices ?? 0)} />
                <Kpi label="Net position" value={formatCurrency(dashboard.data?.profitEstimate ?? 0)} />
              </section>

              <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
                <Panel title="Recent invoices">
                  <DataTable<PlatformInvoice>
                    columns={invoiceColumns.slice(0, 4)}
                    data={(dashboard.data?.recentInvoices ?? []).slice(0, 5)}
                    emptyTitle="No invoices"
                    emptyDescription="Invoices will appear here."
                  />
                </Panel>
                <Panel title="Recent payments">
                  <DataTable<PlatformPayment>
                    columns={paymentColumns}
                    data={(dashboard.data?.recentPayments ?? []).slice(0, 5)}
                    emptyTitle="No payments"
                    emptyDescription="Payments will appear here."
                  />
                </Panel>
              </section>
            </div>
          )}
        </AsyncPanel>
      ) : null}

      {tab === 'quotations' ? (
        <Panel title="Quotations">
          <AsyncPanel query={quotations} loadingLabel="Loading quotations" errorTitle="Quotations unavailable" errorDescription="Quotations could not be loaded.">
            {() => <DataTable<PlatformQuotation> columns={quotationColumns} data={filteredQuotations} emptyTitle="No quotations" emptyDescription="Create a quotation to start the billing flow." />}
          </AsyncPanel>
        </Panel>
      ) : null}
      {tab === 'invoices' ? (
        <Panel title="Invoices">
          <AsyncPanel query={invoices} loadingLabel="Loading invoices" errorTitle="Invoices unavailable" errorDescription="Invoices could not be loaded.">
            {() => <DataTable<PlatformInvoice> columns={invoiceColumns} data={filteredInvoices} emptyTitle="No invoices" emptyDescription="Create an invoice to start billing." />}
          </AsyncPanel>
        </Panel>
      ) : null}
      {tab === 'payments' ? (
        <Panel title="Payments">
          <AsyncPanel query={payments} loadingLabel="Loading payments" errorTitle="Payments unavailable" errorDescription="Payments could not be loaded.">
            {() => <DataTable<PlatformPayment> columns={paymentColumns} data={filteredPayments} emptyTitle="No payments" emptyDescription="Record payments as they are received." />}
          </AsyncPanel>
        </Panel>
      ) : null}
      {tab === 'expenses' ? (
        <Panel title="Expenses">
          <AsyncPanel query={expenses} loadingLabel="Loading expenses" errorTitle="Expenses unavailable" errorDescription="Expenses could not be loaded.">
            {() => <DataTable<PlatformExpense> columns={expenseColumns} data={filteredExpenses} emptyTitle="No expenses" emptyDescription="Record platform expenses as they are incurred." />}
          </AsyncPanel>
        </Panel>
      ) : null}
      {tab === 'reports' ? (
            <Panel title="Reports">
              <EmptyState
                title="Reports summary"
                description="Platform finance reports are exposed through the backend, but the command centre keeps this tab compact. Use the finance overview and reports route for platform-wide reporting."
              />
            </Panel>
          ) : null}
          {tab === 'tax' ? (
            <Panel title="Tax settings">
              <EmptyState
                title="Tax settings"
                description="Tax defaults are controlled from platform settings. Open platform settings to edit tax and numbering defaults."
                actionLabel="Open settings"
                onAction={() => window.location.assign('/platform/settings')}
              />
            </Panel>
          ) : null}
          {tab === 'templates' ? (
            <Panel title="Document templates">
              <EmptyState
                title="Document templates"
                description="Platform document templates are managed in platform settings."
              />
            </Panel>
          ) : null}

      <FormDrawer
        open={createKind !== null}
        title={`Create ${createKind ?? 'record'}`}
        onClose={() => setCreateKind(null)}
        footer={(
          <div className="flex justify-end gap-2">
            <button
              type="button"
              className="rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50"
              onClick={() => setCreateKind(null)}
            >
              Cancel
            </button>
            <button
              type="submit"
              form={createFormId}
              className="rounded-lg bg-[var(--app-primary)] px-4 py-2.5 text-sm font-medium text-white hover:bg-[var(--app-secondary)]"
            >
              Save
            </button>
          </div>
        )}
      >
        <CreateFinanceForm kind={createKind} formId={createFormId} onSaved={() => setCreateKind(null)} />
      </FormDrawer>

      <ConfirmDialog
        open={actionTarget !== null && action !== null}
        title="Confirm finance action"
        message="This will call the live platform finance API."
        confirmLabel="Confirm"
        onCancel={() => {
          setActionTarget(null)
          setAction(null)
        }}
        onConfirm={() => void confirmAction()}
      />
    </div>
  )
}

function CreateFinanceForm({
  kind,
  formId,
  onSaved,
}: {
  kind: 'quotation' | 'invoice' | 'payment' | 'expense' | null
  formId: string
  onSaved: () => void
}) {
  const [form, setForm] = useState({
    tenantId: '',
    customerName: '',
    customerEmail: '',
    currency: 'KES',
    total: 0,
    issueDate: new Date().toISOString().slice(0, 10),
    dueDate: '',
    notes: '',
    amount: 0,
    paidAt: new Date().toISOString().slice(0, 10),
    method: 'M-Pesa',
    category: '',
    vendor: '',
    taxAmount: 0,
  })

  const save = async () => {
    if (!kind) return
    try {
      if (kind === 'quotation') {
        await api.post(endpoints.platformFinance.quotations, {
          tenantId: form.tenantId || null,
          customerName: form.customerName,
          customerEmail: form.customerEmail || null,
          currency: form.currency,
          taxRate: 0,
          discountRate: 0,
          status: 'Draft',
          validUntil: form.dueDate || null,
          notes: form.notes || null,
          lines: [{ description: form.notes || 'Line item', quantity: 1, unitPrice: form.total, taxable: true }],
        })
      } else if (kind === 'invoice') {
        await api.post(endpoints.platformFinance.invoices, {
          tenantId: form.tenantId || null,
          quotationId: null,
          customerName: form.customerName,
          customerEmail: form.customerEmail || null,
          currency: form.currency,
          taxRate: 0,
          discountRate: 0,
          status: 'Draft',
          issueDate: new Date(form.issueDate).toISOString(),
          dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : null,
          notes: form.notes || null,
          lines: [{ description: form.notes || 'Line item', quantity: 1, unitPrice: form.total, taxable: true }],
        })
      } else if (kind === 'payment') {
        await api.post(endpoints.platformFinance.payments, {
          invoiceId: null,
          tenantId: form.tenantId || null,
          amount: form.amount,
          currency: form.currency,
          method: form.method,
          status: 'Paid',
          reference: null,
          paidAt: new Date(form.paidAt).toISOString(),
          notes: form.notes || null,
        })
      } else if (kind === 'expense') {
        await api.post(endpoints.platformFinance.expenses, {
          expenseDate: new Date(form.issueDate).toISOString(),
          category: form.category || 'General',
          vendor: form.vendor || null,
          description: form.notes || 'Expense',
          amount: form.total,
          taxAmount: form.taxAmount,
          currency: form.currency,
          paymentMethod: form.method,
          attachmentUrl: null,
          status: 'Draft',
          tenantId: form.tenantId || null,
        })
      }
      toast.success('Record saved')
      onSaved()
    } catch {
      // handled by interceptor
    }
  }

  if (!kind) return null

  return (
    <form
      id={formId}
      onSubmit={(event) => {
        event.preventDefault()
        void save()
      }}
      className="grid grid-cols-1 gap-4 md:grid-cols-2"
    >
      <Field label="Tenant ID / scope">
        <input
          value={form.tenantId}
          onChange={(event) => setForm((current) => ({ ...current, tenantId: event.target.value }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Customer / vendor">
        <input
          value={kind === 'expense' ? form.vendor : form.customerName}
          onChange={(event) => setForm((current) => (kind === 'expense' ? { ...current, vendor: event.target.value } : { ...current, customerName: event.target.value }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Email / description">
        <input
          value={kind === 'expense' ? form.notes : form.customerEmail}
          onChange={(event) => setForm((current) => (kind === 'expense' ? { ...current, notes: event.target.value } : { ...current, customerEmail: event.target.value }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Currency">
        <input
          value={form.currency}
          onChange={(event) => setForm((current) => ({ ...current, currency: event.target.value.toUpperCase() }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Date">
        <input
          type="date"
          value={form.issueDate}
          onChange={(event) => setForm((current) => ({ ...current, issueDate: event.target.value }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Due date">
        <input
          type="date"
          value={form.dueDate}
          onChange={(event) => setForm((current) => ({ ...current, dueDate: event.target.value }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      <Field label="Amount / total">
        <input
          type="number"
          value={kind === 'payment' ? form.amount : form.total}
          onChange={(event) => setForm((current) => (kind === 'payment' ? { ...current, amount: Number(event.target.value) || 0 } : { ...current, total: Number(event.target.value) || 0 }))}
          className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
      {kind === 'expense' ? (
        <Field label="Tax amount">
          <input
            type="number"
            value={form.taxAmount}
            onChange={(event) => setForm((current) => ({ ...current, taxAmount: Number(event.target.value) || 0 }))}
            className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </Field>
      ) : null}
      {kind === 'payment' ? (
        <Field label="Method">
          <input
            value={form.method}
            onChange={(event) => setForm((current) => ({ ...current, method: event.target.value }))}
            className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </Field>
      ) : null}
      {kind === 'expense' ? (
        <Field label="Category">
          <input
            value={form.category}
            onChange={(event) => setForm((current) => ({ ...current, category: event.target.value }))}
            className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </Field>
      ) : null}
      <Field label="Notes">
        <textarea
          value={form.notes}
          onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))}
          className="min-h-[100px] w-full rounded-lg border border-slate-200 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
        />
      </Field>
    </form>
  )
}

function Kpi({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
      <p className="text-base font-semibold text-slate-900">{title}</p>
      <div className="mt-4">{children}</div>
    </section>
  )
}
