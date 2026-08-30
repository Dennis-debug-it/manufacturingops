import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Plus, Search, Trash2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { getActiveCurrency } from '../../shared/utils/currency'
import { useClients } from '../clients/api'
import { useDefaultTaxRate } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { useCreateCreditNote, useCreditNotes } from './creditNotesApi'
import type { CreditNoteListItem } from './types'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'
const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'
const reasons = ['Return', 'Overcharge', 'Goodwill', 'Cancellation', 'Other'] as const

/** `taxRate: null` means "the user hasn't set one" — the tenant's configured rate is shown instead. */
interface DraftLine { description: string; quantity: string; unitRate: string; taxRate: string | null }

const emptyLine = (): DraftLine => ({ description: '', quantity: '1', unitRate: '0', taxRate: null })

export function CreditNotesPage() {
  const canManage = usePermission('finance.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const { data, isLoading, isError } = useCreditNotes({ search: debouncedSearch })
  const rows = useMemo(() => data ?? [], [data])

  const columns: DataTableColumn<CreditNoteListItem>[] = [
    {
      key: 'creditNoteNumber',
      header: 'Credit Note',
      cell: (row) => (
        <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/credit-notes/${row.id}`}>
          {row.creditNoteNumber}
        </Link>
      ),
    },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '-' },
    { key: 'invoiceNumber', header: 'Against', cell: (row) => row.invoiceNumber ?? '—' },
    { key: 'reason', header: 'Reason', cell: (row) => row.reason },
    { key: 'issueDate', header: 'Issued', cell: (row) => formatDate(row.issueDate) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'balance', header: 'Unapplied', cell: (row) => formatCurrency(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Credit Notes"
        subtitle="Reverse or reduce an invoice, or issue standalone customer credit that can be applied to open invoices."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />
              New Credit Note
            </button>
          ) : undefined
        }
      />

      <FinanceWorkspaceNav />

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search credit notes" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Credit notes unavailable' : 'No credit notes yet'}
        emptyDescription={isError ? 'Credit notes could not be loaded.' : 'Issue a credit note to reverse a charge or grant customer credit.'}
      />

      <NewCreditNoteDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}

function NewCreditNoteDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate()
  const { data: clients } = useClients()
  const createCreditNote = useCreateCreditNote()
  // The rate the tenant configured on its default rate card. Null when it has configured none.
  const { taxRate: tenantTaxRate } = useDefaultTaxRate()

  const [clientId, setClientId] = useState('')
  const [reason, setReason] = useState<(typeof reasons)[number]>('Return')
  const [issueDate, setIssueDate] = useState(new Date().toISOString().slice(0, 10))
  const [currency, setCurrency] = useState(getActiveCurrency)
  const [notes, setNotes] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([emptyLine()])

  /** What the Tax % input shows: the user's own entry, else the tenant's configured rate. */
  const displayedTaxRate = (line: DraftLine) => line.taxRate ?? (tenantTaxRate != null ? String(tenantTaxRate) : '')

  const close = () => {
    setClientId('')
    setReason('Return')
    setCurrency(getActiveCurrency())
    setNotes('')
    setLines([emptyLine()])
    onClose()
  }

  const updateLine = (index: number, field: keyof DraftLine, value: string) =>
    setLines((current) => current.map((line, i) => (i === index ? { ...line, [field]: value } : line)))

  const submit = async () => {
    if (!clientId) {
      toast.error('Select a client')
      return
    }
    const cleaned = lines.filter((line) => line.description.trim())
    if (cleaned.length === 0) {
      toast.error('Add at least one line')
      return
    }
    try {
      const created = await createCreditNote.mutateAsync({
        clientId,
        reason,
        issueDate: new Date(issueDate).toISOString(),
        currency: currency.trim().toUpperCase() || getActiveCurrency(),
        notes: notes.trim() || null,
        lines: cleaned.map((line) => ({
          description: line.description.trim(),
          itemType: 'Service',
          quantity: Number(line.quantity) || 1,
          unit: 'unit',
          unitRate: Number(line.unitRate) || 0,
          taxRate: Number(displayedTaxRate(line)) || 0,
        })),
      })
      toast.success(`Credit note ${created.creditNoteNumber} created`)
      close()
      navigate(`/finance/credit-notes/${created.id}`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="New Credit Note"
      onClose={close}
      footer={
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={close} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Cancel</button>
          <button type="button" onClick={submit} disabled={createCreditNote.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
            {createCreditNote.isPending ? 'Creating…' : 'Create Draft'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="cn-client">Client</label>
          <select id="cn-client" value={clientId} onChange={(event) => setClientId(event.target.value)} className={fieldClass}>
            <option value="">Select a client</option>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="cn-reason">Reason</label>
            <select id="cn-reason" value={reason} onChange={(event) => setReason(event.target.value as (typeof reasons)[number])} className={fieldClass}>
              {reasons.map((r) => (<option key={r} value={r}>{r}</option>))}
            </select>
          </div>
          <div>
            <label className={labelClass} htmlFor="cn-date">Issue Date</label>
            <input id="cn-date" type="date" value={issueDate} onChange={(event) => setIssueDate(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <div>
          <label className={labelClass} htmlFor="cn-currency">Currency</label>
          <input id="cn-currency" value={currency} maxLength={3} onChange={(event) => setCurrency(event.target.value.toUpperCase())} className={fieldClass} />
        </div>
        <div>
          <label className={labelClass}>Line Items</label>
          <div className="space-y-3">
            {lines.map((line, index) => {
              const subtotal = (Number(line.quantity) || 0) * (Number(line.unitRate) || 0)
              return (
                <div key={index} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <input value={line.description} onChange={(event) => updateLine(index, 'description', event.target.value)} aria-label="Description" placeholder="Description" className={fieldClass} />
                  <div className="mt-2 flex items-end gap-3">
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Qty</span>
                      <input type="number" min="0" value={line.quantity} onChange={(event) => updateLine(index, 'quantity', event.target.value)} aria-label="Quantity" className="h-10 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Rate</span>
                      <input type="number" min="0" value={line.unitRate} onChange={(event) => updateLine(index, 'unitRate', event.target.value)} aria-label="Unit rate" className="h-10 w-28 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <label className="flex flex-col gap-1">
                      <span className="text-xs font-medium text-slate-500">Tax %</span>
                      <input type="number" min="0" value={displayedTaxRate(line)} onChange={(event) => updateLine(index, 'taxRate', event.target.value)} aria-label="Tax rate" className="h-10 w-20 rounded-lg border border-slate-300 px-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                    </label>
                    <div className="flex flex-1 flex-col gap-1 text-right">
                      <span className="text-xs font-medium text-slate-500">Subtotal</span>
                      <span className="flex h-10 items-center justify-end px-1 text-sm font-semibold tabular-nums text-slate-900">{formatCurrency(subtotal)}</span>
                    </div>
                    <button type="button" disabled={lines.length === 1} onClick={() => setLines((current) => current.filter((_, i) => i !== index))} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-400 hover:bg-slate-50 disabled:opacity-30" aria-label="Remove line">
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </div>
              )
            })}
          </div>
          <button type="button" onClick={() => setLines((current) => [...current, emptyLine()])} className="mt-3 text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900">
            + Add line
          </button>
        </div>
        <div>
          <label className={labelClass} htmlFor="cn-notes">Notes</label>
          <textarea id="cn-notes" value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]" />
        </div>
      </div>
    </FormDrawer>
  )
}
