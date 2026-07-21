import { useMemo, useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { ArrowLeft } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { Spinner } from '../../shared/components/Spinner'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { useInvoices } from './api'
import { useApplyCreditNote, useCreditNote, useIssueCreditNote, useVoidCreditNote } from './creditNotesApi'

export function CreditNoteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canManage = usePermission('finance.manage')
  const { data: note, isLoading, isError } = useCreditNote(id)
  const issue = useIssueCreditNote(id ?? '')
  const voidNote = useVoidCreditNote(id ?? '')
  const [showApply, setShowApply] = useState(false)

  if (isLoading) return <div className="flex justify-center py-20"><Spinner label="Loading credit note" /></div>
  if (isError || !note) return <div className="rounded-2xl border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">Credit note not found.</div>

  const canApply = note.status === 'Issued' || note.status === 'PartiallyApplied'

  return (
    <div className="space-y-6">
      <Link to="/finance/credit-notes" className="inline-flex items-center gap-1.5 text-sm font-medium text-slate-500 hover:text-slate-900">
        <ArrowLeft className="h-4 w-4" /> Credit Notes
      </Link>

      <PageHeader
        title={note.creditNoteNumber}
        subtitle={`${note.clientName ?? 'Unknown client'} · ${note.reason}${note.invoiceNumber ? ` · against ${note.invoiceNumber}` : ''}`}
        primaryAction={
          canManage ? (
            <div className="flex items-center gap-2">
              {note.status === 'Draft' && (
                <button type="button" onClick={() => issue.mutateAsync().then(() => toast.success('Credit note issued')).catch(() => {})} disabled={issue.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
                  {issue.isPending ? 'Issuing…' : 'Issue'}
                </button>
              )}
              {canApply && (
                <button type="button" onClick={() => setShowApply(true)} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
                  Apply to invoices
                </button>
              )}
              {note.status !== 'Void' && note.amountApplied === 0 && (
                <button type="button" onClick={() => voidNote.mutateAsync(undefined).then(() => toast.success('Credit note voided')).catch(() => {})} disabled={voidNote.isPending} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50 disabled:opacity-60">
                  Void
                </button>
              )}
            </div>
          ) : undefined
        }
      />

      <div className="grid gap-4 sm:grid-cols-4">
        <SummaryCard label="Status"><StatusBadge status={note.status} /></SummaryCard>
        <SummaryCard label="Total">{formatCurrency(note.totalAmount, note.currency)}</SummaryCard>
        <SummaryCard label="Applied">{formatCurrency(note.amountApplied, note.currency)}</SummaryCard>
        <SummaryCard label="Unapplied credit">{formatCurrency(note.balance, note.currency)}</SummaryCard>
      </div>

      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
            <tr>
              <th className="px-4 py-3">Description</th>
              <th className="px-4 py-3 text-right">Qty</th>
              <th className="px-4 py-3 text-right">Rate</th>
              <th className="px-4 py-3 text-right">Tax</th>
              <th className="px-4 py-3 text-right">Total</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {note.lines.map((line) => (
              <tr key={line.id}>
                <td className="px-4 py-3 text-slate-800">{line.description}</td>
                <td className="px-4 py-3 text-right tabular-nums">{line.quantity}</td>
                <td className="px-4 py-3 text-right tabular-nums">{formatCurrency(line.unitRate, note.currency)}</td>
                <td className="px-4 py-3 text-right tabular-nums">{line.taxRate}%</td>
                <td className="px-4 py-3 text-right font-medium tabular-nums">{formatCurrency(line.grandTotal, note.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {note.applications.length > 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <h3 className="mb-3 text-sm font-semibold text-slate-900">Applied to</h3>
          <ul className="divide-y divide-slate-100 text-sm">
            {note.applications.map((app, i) => (
              <li key={i} className="flex items-center justify-between py-2">
                <span className="font-mono text-slate-700">{app.invoiceNumber ?? app.invoiceId}</span>
                <span className="text-slate-500">{formatDate(app.allocatedAt)}</span>
                <span className="font-medium tabular-nums text-slate-900">{formatCurrency(app.amount, note.currency)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {note.notes && <div className="rounded-2xl border border-slate-200 bg-white p-4 text-sm text-slate-600 shadow-sm whitespace-pre-line">{note.notes}</div>}

      {canApply && (
        <ApplyCreditNoteDrawer
          open={showApply}
          onClose={() => setShowApply(false)}
          creditNoteId={note.id}
          clientId={note.clientId}
          currency={note.currency}
          available={note.balance}
        />
      )}
    </div>
  )
}

function SummaryCard({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="mt-1.5 text-lg font-semibold text-slate-900">{children}</div>
    </div>
  )
}

function ApplyCreditNoteDrawer({ open, onClose, creditNoteId, clientId, currency, available }: {
  open: boolean
  onClose: () => void
  creditNoteId: string
  clientId: string
  currency: string
  available: number
}) {
  const { data: invoices } = useInvoices({ clientId })
  const apply = useApplyCreditNote(creditNoteId)
  const [amounts, setAmounts] = useState<Record<string, string>>({})

  const openInvoices = useMemo(
    () => (invoices ?? []).filter((inv) => inv.balance > 0 && inv.status !== 'Void' && inv.currency === currency),
    [invoices, currency],
  )

  const allocated = Object.values(amounts).reduce((sum, v) => sum + (Number(v) || 0), 0)
  const remaining = available - allocated

  const submit = async () => {
    const allocations = Object.entries(amounts)
      .map(([invoiceId, v]) => ({ invoiceId, amount: Number(v) || 0 }))
      .filter((a) => a.amount > 0)
    if (allocations.length === 0) {
      toast.error('Enter at least one amount')
      return
    }
    if (allocated > available + 0.005) {
      toast.error('Allocations exceed the available credit')
      return
    }
    try {
      await apply.mutateAsync({ allocations })
      toast.success('Credit applied')
      setAmounts({})
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Apply credit to invoices"
      onClose={onClose}
      footer={
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm text-slate-500">Remaining: <span className={remaining < 0 ? 'font-semibold text-rose-600' : 'font-semibold text-slate-900'}>{formatCurrency(remaining, currency)}</span></span>
          <button type="button" onClick={submit} disabled={apply.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {apply.isPending ? 'Applying…' : 'Apply'}
          </button>
        </div>
      }
    >
      <div className="space-y-3">
        <p className="text-sm text-slate-500">Available credit: <span className="font-semibold text-slate-900">{formatCurrency(available, currency)}</span></p>
        {openInvoices.length === 0 ? (
          <p className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">No open invoices for this client in {currency}.</p>
        ) : (
          openInvoices.map((inv) => (
            <div key={inv.id} className="flex items-center gap-3 rounded-xl border border-slate-200 p-3">
              <div className="flex-1">
                <div className="font-mono text-sm text-slate-800">{inv.invoiceNumber}</div>
                <div className="text-xs text-slate-500">Balance {formatCurrency(inv.balance, inv.currency)}</div>
              </div>
              <input
                type="number"
                min="0"
                max={inv.balance}
                placeholder="0.00"
                value={amounts[inv.id] ?? ''}
                onChange={(event) => setAmounts((current) => ({ ...current, [inv.id]: event.target.value }))}
                className="h-10 w-32 rounded-lg border border-slate-300 px-2 text-right text-sm outline-none focus:border-slate-900"
                aria-label={`Amount for ${inv.invoiceNumber}`}
              />
            </div>
          ))
        )}
      </div>
    </FormDrawer>
  )
}
