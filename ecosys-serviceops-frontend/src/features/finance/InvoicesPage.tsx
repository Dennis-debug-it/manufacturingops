import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useInvoices } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { NewInvoiceFormDrawer } from './components/NewInvoiceFormDrawer'
import type { InvoiceListItem } from './types'

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
  useEffect(() => {
    setDocumentType(defaultDocumentType)
  }, [defaultDocumentType])
  const invoiceFilters = {
    search: debouncedSearch,
    ...(documentType === 'all' ? {} : { documentType }),
  }
  const { data, isLoading, isError } = useInvoices(invoiceFilters)

  const rows = useMemo(() => data ?? [], [data])

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
    <div className="space-y-6">
      <PageHeader
        title={title}
        subtitle={subtitle}
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
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
                ? 'inline-flex h-10 items-center rounded-full bg-slate-900 px-4 text-sm font-medium text-white'
                : 'inline-flex h-10 items-center rounded-full border border-slate-200 bg-white px-4 text-sm font-medium text-slate-600 transition hover:border-slate-300 hover:text-slate-900'}
            >
              {filter.label}
            </button>
          )
        })}
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder={documentType === 'ProformaInvoice' ? 'Search proformas' : 'Search invoices'}
            className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
          />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={
          isError
            ? 'Documents unavailable'
            : documentType === 'ProformaInvoice'
              ? 'No proformas yet'
              : documentType === 'Invoice'
                ? 'No invoices yet'
                : 'No documents yet'
        }
        emptyDescription={
          isError
            ? 'Documents could not be loaded.'
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
