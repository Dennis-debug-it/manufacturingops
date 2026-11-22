import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ChevronDown, FileDown, Pencil, Plus, Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { useQueryClient } from '@tanstack/react-query'
import { PageHeader } from '../shared/components/PageHeader'
import { AsyncPanel } from '../shared/components/AsyncPanel'
import { LastUpdated } from '../shared/components/LastUpdated'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { FormDrawer } from '../shared/components/FormDrawer'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { StatusBadge } from '../shared/components/StatusBadge'
import { formatDate } from '../shared/utils/dates'
import { formatCurrency } from '../shared/utils/format'
import { cn } from '../shared/utils/cn'
import { api } from '../shared/api/apiClient'
import { endpoints } from '../shared/api/endpoints'
import { useFinanceDashboard, useFinanceExpenses, useFinanceInvoices, useFinancePayments, useFinanceQuotations } from './api'
import type { PlatformExpense, PlatformInvoice, PlatformPayment, PlatformQuotation } from './types'
import { DocumentForm, ExpenseForm, KIND_LABEL, PaymentForm, TaxForm, useFinanceTaxes, type CreateKind, type PlatformTax } from './finance/forms'
import { downloadFinancePdf, useDocumentTemplates } from './documents/api'

type TabId = 'overview' | 'quotations' | 'invoices' | 'payments' | 'expenses' | 'tax' | 'templates'

const tabs: Array<{ id: TabId; label: string; create?: CreateKind }> = [
  { id: 'overview', label: 'Overview' },
  { id: 'quotations', label: 'Quotations', create: 'quotation' },
  { id: 'invoices', label: 'Invoices', create: 'invoice' },
  { id: 'payments', label: 'Payments', create: 'payment' },
  { id: 'expenses', label: 'Expenses', create: 'expense' },
  { id: 'tax', label: 'Tax' },
  { id: 'templates', label: 'Document templates' },
]

type FinanceAction = 'send-quotation' | 'accept' | 'convert' | 'send-invoice' | 'mark-paid' | 'void' | 'approve'

/** What the open drawer is: a new record of a kind, or an existing one to change. */
type Editing =
  | { kind: 'quotation'; record: PlatformQuotation | null }
  | { kind: 'invoice'; record: PlatformInvoice | null }
  | { kind: 'payment'; invoiceId: string | null }
  | { kind: 'expense'; record: PlatformExpense | null }
  | { kind: 'tax'; record: PlatformTax | null }

interface PendingAction { action: FinanceAction; target: PlatformInvoice | PlatformQuotation | PlatformExpense }

const actionButton = 'inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2.5 text-xs font-medium text-slate-700 hover:bg-slate-50'

export function FinancePage() {
  const queryClient = useQueryClient()
  // Every action and new record changes the lists, totals and dashboard: reload them all.
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['platform', 'finance'] })
  const dashboard = useFinanceDashboard()
  const quotations = useFinanceQuotations()
  const invoices = useFinanceInvoices()
  const payments = useFinancePayments()
  const expenses = useFinanceExpenses()
  const [tab, setTab] = useState<TabId>('overview')
  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<Editing | null>(null)
  const [pending, setPending] = useState<PendingAction | null>(null)
  const formId = 'platform-finance-form'

  const query = search.trim().toLowerCase()
  const match = (...values: Array<string | null | undefined>) => !query || values.some((x) => (x ?? '').toLowerCase().includes(query))
  const filteredQuotations = useMemo(() => (quotations.data ?? []).filter((x) => match(x.quotationNumber, x.customerName, x.status)), [quotations.data, query]) // eslint-disable-line react-hooks/exhaustive-deps
  const filteredInvoices = useMemo(() => (invoices.data ?? []).filter((x) => match(x.invoiceNumber, x.customerName, x.status)), [invoices.data, query]) // eslint-disable-line react-hooks/exhaustive-deps
  const filteredPayments = useMemo(() => (payments.data ?? []).filter((x) => match(x.paymentNumber, x.method, x.reference)), [payments.data, query]) // eslint-disable-line react-hooks/exhaustive-deps
  const filteredExpenses = useMemo(() => (expenses.data ?? []).filter((x) => match(x.category, x.vendor, x.description)), [expenses.data, query]) // eslint-disable-line react-hooks/exhaustive-deps
  const invoiceNumber = (id: string | null) => (id ? invoices.data?.find((x) => x.id === id)?.invoiceNumber : undefined)

  const startCreate = (kind: CreateKind) => {
    if (kind === 'payment') setEditing({ kind, invoiceId: null })
    else setEditing({ kind, record: null } as Editing)
  }

  const pdf = (kind: 'quotations' | 'invoices' | 'payments', id: string, name: string) =>
    void downloadFinancePdf(kind, id, name).catch(() => toast.error('The PDF could not be made.'))

  const quotationColumns: DataTableColumn<PlatformQuotation>[] = [
    { key: 'quotationNumber', header: 'Quotation', cell: (row) => <span className="font-mono text-xs">{row.quotationNumber}</span> },
    { key: 'customerName', header: 'Customer', cell: (row) => row.customerName },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.total, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'validUntil', header: 'Valid until', cell: (row) => (row.validUntil ? formatDate(row.validUntil) : '—') },
    {
      key: 'actions', header: '', cell: (row) => (
        <div className="flex flex-wrap justify-end gap-1.5">
          <button type="button" className={actionButton} onClick={() => pdf('quotations', row.id, row.quotationNumber)}><FileDown className="h-3.5 w-3.5" />PDF</button>
          {row.status === 'Draft' ? <button type="button" className={actionButton} onClick={() => setEditing({ kind: 'quotation', record: row })}><Pencil className="h-3.5 w-3.5" />Edit</button> : null}
          {row.status === 'Draft' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'send-quotation', target: row })}>Mark sent</button> : null}
          {row.status === 'Sent' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'accept', target: row })}>Accepted</button> : null}
          {row.status !== 'Converted' && row.status !== 'Rejected' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'convert', target: row })}>Make invoice</button> : null}
        </div>
      ),
    },
  ]

  const invoiceColumns: DataTableColumn<PlatformInvoice>[] = [
    { key: 'invoiceNumber', header: 'Invoice', cell: (row) => <span className="font-mono text-xs">{row.invoiceNumber}</span> },
    { key: 'customerName', header: 'Customer', cell: (row) => row.customerName },
    {
      key: 'for', header: 'For', cell: (row) => (row.purpose ? (
        <span className="text-xs">
          {row.purpose === 'YearOne' ? 'Year one' : row.purpose}
          {row.coversFrom && row.coversTo ? <span className="block text-slate-500">{formatDate(row.coversFrom)} – {formatDate(new Date(new Date(row.coversTo).getTime() - 86_400_000).toISOString())}</span> : null}
        </span>
      ) : '—'),
    },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.total, row.currency) },
    { key: 'balance', header: 'Owing', cell: (row) => formatCurrency(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'dueDate', header: 'Due', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    {
      key: 'actions', header: '', cell: (row) => (
        <div className="flex flex-wrap justify-end gap-1.5">
          <button type="button" className={actionButton} onClick={() => pdf('invoices', row.id, row.invoiceNumber)}><FileDown className="h-3.5 w-3.5" />PDF</button>
          {row.status === 'Draft' ? <button type="button" className={actionButton} onClick={() => setEditing({ kind: 'invoice', record: row })}><Pencil className="h-3.5 w-3.5" />Edit</button> : null}
          {row.status === 'Draft' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'send-invoice', target: row })}>Send</button> : null}
          {row.status !== 'Paid' && row.status !== 'Void' && row.status !== 'Draft' ? <button type="button" className={actionButton} onClick={() => setEditing({ kind: 'payment', invoiceId: row.id })}>Record payment</button> : null}
          {row.status !== 'Paid' && row.status !== 'Void' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'mark-paid', target: row })}>Mark paid</button> : null}
          {row.status !== 'Void' && row.amountPaid === 0 ? <button type="button" className={cn(actionButton, 'text-rose-700')} onClick={() => setPending({ action: 'void', target: row })}>Void</button> : null}
        </div>
      ),
    },
  ]

  const paymentColumns: DataTableColumn<PlatformPayment>[] = [
    { key: 'paymentNumber', header: 'Receipt', cell: (row) => <span className="font-mono text-xs">{row.paymentNumber}</span> },
    { key: 'paidAt', header: 'Date', cell: (row) => formatDate(row.paidAt) },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount, row.currency) },
    { key: 'method', header: 'Method', cell: (row) => row.method },
    { key: 'reference', header: 'Reference', cell: (row) => row.reference ?? '—' },
    { key: 'invoice', header: 'Invoice', cell: (row) => invoiceNumber(row.invoiceId) ?? '—' },
    { key: 'actions', header: '', cell: (row) => <div className="flex justify-end"><button type="button" className={actionButton} onClick={() => pdf('payments', row.id, row.paymentNumber)}><FileDown className="h-3.5 w-3.5" />Receipt</button></div> },
  ]

  const expenseColumns: DataTableColumn<PlatformExpense>[] = [
    { key: 'expenseDate', header: 'Date', cell: (row) => formatDate(row.expenseDate) },
    { key: 'category', header: 'Category', cell: (row) => row.category },
    { key: 'description', header: 'What for', cell: (row) => <span>{row.description}{row.vendor ? <span className="block text-xs text-slate-500">{row.vendor}</span> : null}</span> },
    { key: 'total', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions', header: '', cell: (row) => (
        <div className="flex flex-wrap justify-end gap-1.5">
          {row.status !== 'Approved' ? <button type="button" className={actionButton} onClick={() => setEditing({ kind: 'expense', record: row })}><Pencil className="h-3.5 w-3.5" />Edit</button> : null}
          {row.status === 'Draft' || row.status === 'Submitted' ? <button type="button" className={actionButton} onClick={() => setPending({ action: 'approve', target: row })}>Approve</button> : null}
        </div>
      ),
    },
  ]

  const confirmAction = async () => {
    if (!pending) return
    const { action, target } = pending
    try {
      if (action === 'send-quotation') await api.post(endpoints.platformFinance.quotationSend(target.id))
      if (action === 'accept') await api.post(`${endpoints.platformFinance.quotation(target.id)}/approve`)
      if (action === 'convert') await api.post(endpoints.platformFinance.quotationConvert(target.id))
      if (action === 'send-invoice') await api.post(endpoints.platformFinance.invoiceSend(target.id))
      if (action === 'mark-paid') await api.post(endpoints.platformFinance.invoiceMarkPaid(target.id), {})
      if (action === 'void') await api.post(endpoints.platformFinance.invoiceVoid(target.id))
      if (action === 'approve') await api.post(endpoints.platformFinance.expenseApprove(target.id))
      toast.success(ACTION_DONE[action])
      if (action === 'convert') setTab('invoices')
      setPending(null)
      void refresh()
    } catch {
      // the interceptor shows the error
    }
  }

  const current = tabs.find((x) => x.id === tab)
  const drawerTitle = editing
    ? editing.kind === 'payment' ? 'Record payment'
      : editing.kind === 'tax' ? (editing.record ? `Edit ${editing.record.name}` : 'Add tax')
        : editing.record ? `Edit ${editing.kind === 'quotation' ? (editing.record as PlatformQuotation).quotationNumber : editing.kind === 'invoice' ? (editing.record as PlatformInvoice).invoiceNumber : 'expense'}`
          : KIND_LABEL[editing.kind]
    : ''
  const onSaved = () => { setEditing(null); void refresh() }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Finance"
        subtitle="Quotations, invoices, payments and expenses for the platform. Subscription invoices raised from Pricing and renewals appear here too."
        secondaryActions={<LastUpdated at={dashboard.dataUpdatedAt} />}
        primaryAction={current?.create
          ? <button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => startCreate(current.create!)}><Plus className="h-4 w-4" />{KIND_LABEL[current.create]}</button>
          : tab === 'tax' ? <button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => setEditing({ kind: 'tax', record: null })}><Plus className="h-4 w-4" />Add tax</button>
            : tab === 'templates' ? undefined
              : <CreateMenu onPick={startCreate} />}
      />

      <div className="flex flex-wrap gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-2 shadow-sm backdrop-blur-[var(--glass-blur-md)]" role="tablist">
        {tabs.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => { setTab(item.id); setSearch('') }}
            className={`rounded-lg px-3 py-2 text-sm font-medium ${tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'}`}>
            {item.label}
          </button>
        ))}
      </div>

      {current?.create ? (
        <div className="flex items-center gap-2 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-3 shadow-sm">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={`Search ${current.label.toLowerCase()}`} aria-label={`Search ${current.label.toLowerCase()}`}
            className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400" />
        </div>
      ) : null}

      {tab === 'overview' ? (
        <AsyncPanel query={dashboard} loadingLabel="Loading finance overview" errorTitle="Overview unavailable" errorDescription="Finance overview could not be loaded.">
          {() => {
            const d = dashboard.data
            const aging = d?.aging
            return (
              <div className="space-y-5">
                <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
                  <Kpi label="Received" value={formatCurrency(d?.totalRevenue ?? 0)} hint="All payments to date" />
                  <Kpi label="Owing" value={formatCurrency(d?.outstandingInvoices ?? 0)} hint="Unpaid invoice balances" />
                  <Kpi label="Overdue" value={formatCurrency(d?.overdueInvoices ?? 0)} hint="Past their due date" tone={(d?.overdueInvoices ?? 0) > 0 ? 'bad' : undefined} />
                  <Kpi label="Expenses this month" value={formatCurrency(d?.expensesThisMonth ?? 0)} hint={`Quotations won: ${d?.quotationConversionRate ?? 0}%`} />
                </section>
                {aging ? (
                  <Panel title="Money owed, by how late">
                    <div className="grid grid-cols-2 gap-3 sm:grid-cols-5">
                      {([['Not yet due', aging.current], ['1–30 days', aging.days1To30], ['31–60 days', aging.days31To60], ['61–90 days', aging.days61To90], ['Over 90 days', aging.days90Plus]] as const).map(([label, value]) => (
                        <div key={label} className="rounded-xl bg-slate-50 p-3">
                          <div className="text-xs text-slate-500">{label}</div>
                          <div className={cn('mt-1 font-semibold tabular-nums', label !== 'Not yet due' && value > 0 ? 'text-rose-700' : 'text-slate-900')}>{formatCurrency(value)}</div>
                        </div>
                      ))}
                    </div>
                  </Panel>
                ) : null}
                <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
                  <Panel title="Recent invoices">
                    <DataTable<PlatformInvoice> columns={invoiceColumns.filter((x) => ['invoiceNumber', 'customerName', 'total', 'status'].includes(x.key))} data={(d?.recentInvoices ?? []).slice(0, 5)} emptyTitle="No invoices" emptyDescription="Invoices will appear here." />
                  </Panel>
                  <Panel title="Recent payments">
                    <DataTable<PlatformPayment> columns={paymentColumns.filter((x) => ['paymentNumber', 'paidAt', 'amount', 'method'].includes(x.key))} data={(d?.recentPayments ?? []).slice(0, 5)} emptyTitle="No payments" emptyDescription="Payments will appear here." />
                  </Panel>
                </section>
              </div>
            )
          }}
        </AsyncPanel>
      ) : null}

      {tab === 'quotations' ? (
        <AsyncPanel query={quotations} loadingLabel="Loading quotations" errorTitle="Quotations unavailable" errorDescription="Quotations could not be loaded.">
          {() => <DataTable<PlatformQuotation> columns={quotationColumns} data={filteredQuotations} emptyTitle="No quotations" emptyDescription="Use New quotation for a one-off quotation. Factory subscription quotes are built in Pricing." />}
        </AsyncPanel>
      ) : null}
      {tab === 'invoices' ? (
        <AsyncPanel query={invoices} loadingLabel="Loading invoices" errorTitle="Invoices unavailable" errorDescription="Invoices could not be loaded.">
          {() => <DataTable<PlatformInvoice> columns={invoiceColumns} data={filteredInvoices} emptyTitle="No invoices" emptyDescription="Use New invoice, or make one from a quotation." />}
        </AsyncPanel>
      ) : null}
      {tab === 'payments' ? (
        <AsyncPanel query={payments} loadingLabel="Loading payments" errorTitle="Payments unavailable" errorDescription="Payments could not be loaded.">
          {() => <DataTable<PlatformPayment> columns={paymentColumns} data={filteredPayments} emptyTitle="No payments" emptyDescription="Record payments as they come in; each gets a receipt." />}
        </AsyncPanel>
      ) : null}
      {tab === 'expenses' ? (
        <AsyncPanel query={expenses} loadingLabel="Loading expenses" errorTitle="Expenses unavailable" errorDescription="Expenses could not be loaded.">
          {() => <DataTable<PlatformExpense> columns={expenseColumns} data={filteredExpenses} emptyTitle="No expenses" emptyDescription="Record what the platform spends: hosting, software, salaries." />}
        </AsyncPanel>
      ) : null}
      {tab === 'tax' ? <TaxTab onEdit={(record) => setEditing({ kind: 'tax', record })} /> : null}
      {tab === 'templates' ? <TemplatesTab /> : null}

      <FormDrawer
        open={editing !== null}
        title={drawerTitle}
        onClose={() => setEditing(null)}
        closeOnOverlayClick={false}
        footer={(
          <div className="flex justify-end gap-2">
            <button type="button" className="rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => setEditing(null)}>Cancel</button>
            <button type="submit" form={formId} className="rounded-lg bg-[var(--app-primary)] px-4 py-2.5 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
              {editing?.kind === 'payment' ? 'Record payment' : editing?.kind === 'expense' && !editing.record ? 'Record expense' : editing && 'record' in editing && editing.record ? 'Save changes' : 'Save draft'}
            </button>
          </div>
        )}
      >
        {editing?.kind === 'quotation' || editing?.kind === 'invoice' ? <DocumentForm key={`${editing.kind}-${editing.record?.id ?? 'new'}`} kind={editing.kind} formId={formId} existing={editing.record} onSaved={onSaved} /> : null}
        {editing?.kind === 'payment' ? <PaymentForm key={editing.invoiceId ?? 'none'} formId={formId} invoices={invoices.data ?? []} presetInvoiceId={editing.invoiceId} onSaved={onSaved} /> : null}
        {editing?.kind === 'expense' ? <ExpenseForm key={editing.record?.id ?? 'new'} formId={formId} existing={editing.record} onSaved={onSaved} /> : null}
        {editing?.kind === 'tax' ? <TaxForm key={editing.record?.id ?? 'new'} formId={formId} existing={editing.record} onSaved={onSaved} /> : null}
      </FormDrawer>

      <ConfirmDialog
        open={pending !== null}
        title={pending ? ACTION_TITLE[pending.action] : ''}
        message={pending ? actionMessage(pending) : ''}
        confirmLabel={pending ? ACTION_TITLE[pending.action] : 'Confirm'}
        onCancel={() => setPending(null)}
        onConfirm={() => void confirmAction()}
      />
    </div>
  )
}

function CreateMenu({ onPick }: { onPick: (kind: CreateKind) => void }) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => {
    if (!open) return
    const close = (e: MouseEvent) => { if (!ref.current?.contains(e.target as Node)) setOpen(false) }
    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])
  return (
    <div ref={ref} className="relative">
      <button type="button" aria-haspopup="menu" aria-expanded={open} onClick={() => setOpen((x) => !x)}
        className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]">
        <Plus className="h-4 w-4" />New<ChevronDown className="h-4 w-4" />
      </button>
      {open ? (
        <div role="menu" className="absolute right-0 z-30 mt-2 w-52 overflow-hidden rounded-xl border border-slate-200 bg-white py-1 shadow-lg">
          {(Object.keys(KIND_LABEL) as CreateKind[]).map((kind) => (
            <button key={kind} type="button" role="menuitem" onClick={() => { setOpen(false); onPick(kind) }} className="block w-full px-4 py-2 text-left text-sm text-slate-700 hover:bg-slate-50">
              {KIND_LABEL[kind]}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  )
}

function TaxTab({ onEdit }: { onEdit: (tax: PlatformTax) => void }) {
  const taxes = useFinanceTaxes()
  const columns: DataTableColumn<PlatformTax>[] = [
    { key: 'name', header: 'Tax', cell: (x) => <span className="font-medium">{x.name}{x.isDefault ? <span className="ml-2 rounded-full bg-emerald-50 px-2 py-0.5 text-xs text-emerald-700">Default</span> : null}</span> },
    { key: 'rate', header: 'Rate', cell: (x) => `${x.rate}%` },
    { key: 'mode', header: 'Prices are', cell: (x) => (x.mode === 'Inclusive' ? 'Including tax' : 'Before tax') },
    { key: 'actions', header: '', cell: (x) => <div className="flex justify-end"><button type="button" className={actionButton} onClick={() => onEdit(x)}><Pencil className="h-3.5 w-3.5" />Edit</button></div> },
  ]
  return (
    <Panel title="Tax rates">
      <p className="mb-3 text-sm text-slate-600">The default rate is filled in on new quotations and invoices; you can change it on each.</p>
      <AsyncPanel query={taxes} loadingLabel="Loading taxes" errorTitle="Taxes unavailable" errorDescription="Tax rates could not be loaded.">
        {() => <DataTable<PlatformTax> columns={columns} data={taxes.data ?? []} emptyTitle="No tax rates" emptyDescription="Add VAT at 16% to have it filled in on new documents." />}
      </AsyncPanel>
    </Panel>
  )
}

function TemplatesTab() {
  const templates = useDocumentTemplates()
  const rows = templates.data ?? []
  return (
    <Panel title="Document templates">
      <p className="text-sm text-slate-600">Quotations, invoices and receipts are drawn with the default template of their type. Design them in the document designer.</p>
      <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-3">
        {(['Quotation', 'Invoice', 'Receipt'] as const).map((type) => {
          const chosen = rows.find((x) => x.type.toLowerCase() === type.toLowerCase() && x.isDefault && x.isActive)
          return (
            <Link key={type} to={chosen ? `/platform/settings/templates/${chosen.id}` : `/platform/settings/templates/new?type=${type}`}
              className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-3 hover:border-[var(--app-primary)]">
              <span aria-hidden className="h-9 w-9 shrink-0 rounded-lg" style={{ background: chosen?.accentColor ?? '#e2e8f0' }} />
              <span className="min-w-0">
                <span className="block font-medium text-slate-900">{type}s</span>
                <span className="block truncate text-xs text-slate-500">{chosen ? `${chosen.name} · ${chosen.layout}` : 'Standard look · design it'}</span>
              </span>
            </Link>
          )
        })}
      </div>
      <Link to="/platform/settings/templates" className="mt-3 inline-block text-sm font-medium text-[var(--app-primary)] hover:underline">All document templates</Link>
    </Panel>
  )
}

function Kpi({ label, value, hint, tone }: { label: string; value: string; hint?: string; tone?: 'bad' | undefined }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className={cn('mt-2 text-2xl font-semibold tabular-nums', tone === 'bad' ? 'text-rose-700' : 'text-slate-900')}>{value}</div>
      {hint ? <div className="mt-1 text-xs text-slate-500">{hint}</div> : null}
    </div>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <p className="text-base font-semibold text-slate-900">{title}</p>
      <div className="mt-3">{children}</div>
    </section>
  )
}

const ACTION_TITLE: Record<FinanceAction, string> = {
  'send-quotation': 'Mark as sent',
  accept: 'Mark accepted',
  convert: 'Make invoice',
  'send-invoice': 'Send invoice',
  'mark-paid': 'Mark paid',
  void: 'Void invoice',
  approve: 'Approve expense',
}

const ACTION_DONE: Record<FinanceAction, string> = {
  'send-quotation': 'Quotation marked as sent',
  accept: 'Quotation marked accepted',
  convert: 'Draft invoice made from the quotation',
  'send-invoice': 'Invoice sent',
  'mark-paid': 'Marked paid; receipt recorded',
  void: 'Invoice voided',
  approve: 'Expense approved',
}

/** Says what the action does, in particular what reaches the customer and what happens to their subscription. */
function actionMessage({ action, target }: PendingAction) {
  const number = 'invoiceNumber' in target ? target.invoiceNumber : 'quotationNumber' in target ? target.quotationNumber : 'this expense'
  switch (action) {
    case 'send-quotation': return `Mark quotation ${number} as sent to the customer? Download its PDF to send it.`
    case 'accept': return `Record that the customer accepted quotation ${number}?`
    case 'convert': return `Make a draft invoice from quotation ${number}? Nothing is sent until you send the invoice.`
    case 'send-invoice': return `Email invoice ${number} to ${'customerEmail' in target && target.customerEmail ? target.customerEmail : 'the customer'} and mark it sent?`
    case 'mark-paid': return `Record the outstanding balance of ${number} as paid in full? A receipt is emailed, and the workspace's subscription is extended by the period the invoice covers. To record part of it, use Record payment.`
    case 'void': return `Void ${number}? It stays on record with nothing owing.`
    case 'approve': return 'Approve this expense?'
    default: return ''
  }
}
