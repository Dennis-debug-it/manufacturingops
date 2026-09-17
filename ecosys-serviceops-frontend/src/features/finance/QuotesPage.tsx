import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useBuildProformaInvoice, useQuotations } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { NewQuoteFormDrawer } from './components/NewQuoteFormDrawer'
import type { QuotationListItem } from './types'

function defaultDocumentDate(offsetDays = 0) {
  return new Date(Date.now() + offsetDays * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
}

export function QuotesPage() {
  const navigate = useNavigate()
  const canManage = usePermission('finance.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const [showProformaDrawer, setShowProformaDrawer] = useState(false)
  const [selectedQuoteIds, setSelectedQuoteIds] = useState<string[]>([])
  const [invoiceDate, setInvoiceDate] = useState(() => defaultDocumentDate())
  const [dueDate, setDueDate] = useState(() => defaultDocumentDate(30))
  const [notes, setNotes] = useState('')
  const { data, isLoading, isError } = useQuotations({ search: debouncedSearch })
  const buildProforma = useBuildProformaInvoice()

  const rows = useMemo(() => data ?? [], [data])
  const selectedQuotes = useMemo(
    () => rows.filter((row) => selectedQuoteIds.includes(row.id)),
    [rows, selectedQuoteIds],
  )
  const acceptedSelectedCount = selectedQuotes.filter((row) => row.status === 'Accepted' && !row.convertedToInvoiceId).length
  const canBuildSelectedProforma =
    selectedQuotes.length > 0 &&
    acceptedSelectedCount === selectedQuotes.length &&
    new Set(selectedQuotes.map((row) => row.clientId)).size === 1 &&
    new Set(selectedQuotes.map((row) => row.currency)).size === 1

  const columns: DataTableColumn<QuotationListItem>[] = [
    { key: 'quoteNumber', header: 'Quote', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/quotes/${row.id}`}>{row.quoteNumber}</Link> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '-' },
    { key: 'quoteDate', header: 'Date', cell: (row) => formatDate(row.quoteDate) },
    { key: 'validUntil', header: 'Valid Until', cell: (row) => formatDate(row.validUntil) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Quotes"
        subtitle="Quotes list and lifecycle management."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Quote
            </button>
          ) : undefined
        }
      />

      <FinanceWorkspaceNav />

      {canManage ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] px-4 py-3 shadow-sm">
          <div>
            <div className="text-sm font-medium text-slate-900">Document workflow</div>
            <div className="text-sm text-slate-500">
              Select accepted quotations for the same client to build a proforma invoice.
            </div>
          </div>
          <button
            type="button"
            disabled={!canBuildSelectedProforma}
            onClick={() => setShowProformaDrawer(true)}
            className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-40"
          >
            Build Proforma Invoice
          </button>
        </div>
      ) : null}

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search quotes" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        batchSelect={canManage}
        onSelectionChange={setSelectedQuoteIds}
        emptyTitle={isError ? 'Quotes unavailable' : 'No quotes yet'}
        emptyDescription={isError ? 'Quotes could not be loaded.' : 'Create your first quote to get started.'}
      />

      <NewQuoteFormDrawer open={showForm} onClose={() => setShowForm(false)} />

      <FormDrawer
        open={showProformaDrawer}
        title="Build Proforma Invoice"
        onClose={() => setShowProformaDrawer(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowProformaDrawer(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button
              type="button"
              disabled={!canBuildSelectedProforma || buildProforma.isPending}
              onClick={async () => {
                try {
                  const invoice = await buildProforma.mutateAsync({
                    quotationIds: selectedQuoteIds,
                    invoiceDate: new Date(invoiceDate).toISOString(),
                    dueDate: new Date(dueDate).toISOString(),
                    notes: notes.trim() || null,
                  })
                  toast.success(`Proforma ${invoice.invoiceNumber} created`)
                  setShowProformaDrawer(false)
                  setSelectedQuoteIds([])
                  setNotes('')
                  navigate(`/finance/invoices/${invoice.id}`)
                } catch {
                  // interceptor surfaces the error toast
                }
              }}
              className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
            >
              {buildProforma.isPending ? 'Building...' : 'Create Proforma'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            {selectedQuotes.length > 0 ? `${selectedQuotes.length} quotation(s) selected` : 'No quotations selected'}
          </div>
          {!canBuildSelectedProforma && selectedQuotes.length > 0 ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
              Only accepted, not-yet-converted quotations for the same client and currency can be combined into one proforma invoice.
            </div>
          ) : null}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="proforma-date">Proforma date</label>
              <input id="proforma-date" type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="proforma-due">Due date</label>
              <input id="proforma-due" type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </div>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="proforma-notes">Notes</label>
            <textarea id="proforma-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="Optional cover note for the combined proforma invoice" />
          </div>
          <div className="rounded-xl border border-slate-200">
            <div className="border-b border-slate-200 px-4 py-3 text-sm font-medium text-slate-900">Selected quotations</div>
            <div className="divide-y divide-slate-100">
              {selectedQuotes.map((quote) => (
                <div key={quote.id} className="flex items-center justify-between gap-4 px-4 py-3 text-sm">
                  <div>
                    <div className="font-medium text-slate-900">{quote.quoteNumber}</div>
                    <div className="text-slate-500">{quote.clientName ?? 'Unknown client'}</div>
                  </div>
                  <div className="text-right">
                    <div className="text-slate-900">{formatCurrency(quote.totalAmount, quote.currency)}</div>
                    <div className="text-slate-500">{quote.status}</div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
