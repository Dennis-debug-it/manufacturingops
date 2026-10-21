import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search, X } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { useUrlFilters } from '../../shared/hooks/useUrlFilters'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useInvoices } from './api'
import { invoiceAgeLabels, invoiceListFilterSpec, invoiceListParams, invoiceStatusOptions } from './invoiceListFilters'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { NewInvoiceFormDrawer } from './components/NewInvoiceFormDrawer'
import type { InvoiceListItem } from './types'

const filterClass = 'h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]'

const documentFilters = [
  { value: 'all', label: 'All documents' },
  { value: 'ProformaInvoice', label: 'Proformas' },
  { value: 'Invoice', label: 'Invoices' },
] as const

type FinanceDocumentFilter = (typeof documentFilters)[number]['value']

interface InvoicesPageProps {
  defaultDocumentType?: FinanceDocumentFilter
  title?: string
  subtitle?: string
}

export function InvoicesPage({
  defaultDocumentType = 'Invoice',
  title = 'Invoices',
  subtitle = 'Invoices list and payment states.',
}: InvoicesPageProps) {
  const canManage = usePermission('finance.manage')
  const [search, setSearch] = useState('')
  const [documentType, setDocumentType] = useState<FinanceDocumentFilter>(defaultDocumentType)
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  // Status, age and currency live in the URL, so dashboard figures open the list already filtered.
  const { filters, update } = useUrlFilters(invoiceListFilterSpec)
  useEffect(() => {
    setDocumentType(defaultDocumentType)
  }, [defaultDocumentType])
  const invoiceFilters = {
    search: debouncedSearch,
    ...(documentType === 'all' ? {} : { documentType }),
    ...invoiceListParams(filters),
  }
  const { data, isLoading, isError } = useInvoices(invoiceFilters)

  const rows = useMemo(() => data ?? [], [data])
  const filtered = Boolean(filters.status || filters.age || filters.currency || debouncedSearch)

  const columns: DataTableColumn<InvoiceListItem>[] = [
    {
      key: 'invoiceNumber',
      header: 'Invoice',
      cell: (row) => (
        <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/invoices/${row.id}`}>
          {row.invoiceNumber}
        </Link>
      ),
    },
    { key: 'documentType', header: 'Type', cell: (row) => (row.documentType === 'ProformaInvoice' ? 'Proforma' : 'Invoice') },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '-' },
    { key: 'invoiceDate', header: 'Date', cell: (row) => formatDate(row.invoiceDate) },
    { key: 'dueDate', header: 'Due', cell: (row) => formatDate(row.dueDate) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'balance', header: 'Balance', cell: (row) => formatCurrency(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={title}
        subtitle={subtitle}
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />
              New Invoice
            </button>
          ) : undefined
        }
      />

      <FinanceWorkspaceNav />

      <div className="flex flex-wrap gap-2">
        {documentFilters.map((filter) => {
          const isActive = documentType === filter.value
          return (
            <button
              key={filter.value}
              type="button"
              onClick={() => setDocumentType(filter.value)}
              className={isActive
                ? 'inline-flex h-10 items-center rounded-full bg-[var(--app-primary)] px-4 text-sm font-medium text-white'
                : 'inline-flex h-10 items-center rounded-full border border-slate-200 bg-white px-4 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:text-slate-900'}
            >
              {filter.label}
            </button>
          )
        })}
      </div>

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={documentType === 'ProformaInvoice' ? 'Search proformas' : 'Search invoices'}
            className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
          />
        </label>
        <select aria-label="Status" value={filters.status} onChange={(event) => update({ status: event.target.value })} className={filterClass}>
          <option value="">Any status</option>
          {invoiceStatusOptions.map((status) => (
            <option key={status} value={status}>{status === 'PartiallyPaid' ? 'Partially paid' : status}</option>
          ))}
        </select>
        <select aria-label="Age" value={filters.age} onChange={(event) => update({ age: event.target.value })} className={filterClass}>
          <option value="">Any age</option>
          {Object.entries(invoiceAgeLabels).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>
        {filters.currency ? (
          <button type="button" onClick={() => update({ currency: '' })} className="inline-flex h-11 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 hover:bg-slate-50" aria-label={`Clear currency ${filters.currency}`}>
            {filters.currency.toUpperCase()} only<X className="h-3.5 w-3.5" />
          </button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={
          isError
            ? 'Documents unavailable'
            : filtered
              ? 'No matching documents'
              : documentType === 'ProformaInvoice'
              ? 'No proformas yet'
              : documentType === 'Invoice'
                ? 'No invoices yet'
                : 'No documents yet'
        }
        emptyDescription={
          isError
            ? 'Documents could not be loaded.'
            : filtered
              ? 'Change or clear the filters to see more.'
              : documentType === 'ProformaInvoice'
              ? 'Build a proforma invoice from accepted quotations to get started.'
              : documentType === 'Invoice'
                ? 'Create your first invoice to get started.'
                : 'Create an invoice or build a proforma from quotations to get started.'
        }
      />

      <NewInvoiceFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
