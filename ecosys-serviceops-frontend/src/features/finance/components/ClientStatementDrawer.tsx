import { useState } from 'react'
import toast from 'react-hot-toast'
import { FileText, Mail } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { formatDate } from '../../../shared/utils/dates'
import { formatCurrency } from '../../../shared/utils/format'
import { apiClient } from '../../../shared/api/apiClient'
import { endpoints } from '../../../shared/api/endpoints'
import { useClientStatement, useEmailClientStatement } from '../api'

const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-[var(--app-primary)]'

function isoDaysAgo(days: number) {
  return new Date(Date.now() - days * 24 * 60 * 60 * 1000).toISOString().slice(0, 10)
}

export interface ClientStatementDrawerProps {
  open: boolean
  clientId: string | null
  clientName: string | null
  onClose: () => void
}

export function ClientStatementDrawer({ open, clientId, clientName, onClose }: ClientStatementDrawerProps) {
  const [from, setFrom] = useState(() => isoDaysAgo(90))
  const [to, setTo] = useState(() => new Date().toISOString().slice(0, 10))
  const [overdueOnly, setOverdueOnly] = useState(false)
  const [message, setMessage] = useState('')
  const [previewing, setPreviewing] = useState(false)

  const statement = useClientStatement({ clientId: clientId ?? undefined, from, to, enabled: open })
  const emailStatement = useEmailClientStatement()

  const previewPdf = async () => {
    if (!clientId) return
    setPreviewing(true)
    try {
      const response = await apiClient.get(endpoints.finance.clientStatementPreviewPdf, {
        responseType: 'blob',
        params: { clientId, from, to, overdueOnly },
        suppressGlobalErrorToast: true,
      })
      const url = URL.createObjectURL(response.data as Blob)
      window.open(url, '_blank', 'noopener')
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch {
      toast.error('Could not generate the statement PDF')
    } finally {
      setPreviewing(false)
    }
  }

  const sendEmail = async () => {
    if (!clientId) return
    try {
      await emailStatement.mutateAsync({ clientId, from, to, overdueOnly, message: message.trim() || null })
      toast.success(`Statement emailed to ${clientName ?? 'client'}`)
      setMessage('')
      onClose()
    } catch {
      // interceptor surfaces the error toast (e.g. missing client email / SMTP not configured)
    }
  }

  return (
    <FormDrawer
      open={open}
      title={clientName ? `Statement — ${clientName}` : 'Client Statement'}
      onClose={onClose}
      footer={
        <div className="flex flex-wrap items-center justify-end gap-2">
          <button
            type="button"
            onClick={() => void previewPdf()}
            disabled={previewing}
            className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
          >
            <FileText className="h-4 w-4" />
            {previewing ? 'Preparing…' : 'Preview PDF'}
          </button>
          <button
            type="button"
            onClick={() => void sendEmail()}
            disabled={emailStatement.isPending}
            className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60"
          >
            <Mail className="h-4 w-4" />
            {emailStatement.isPending ? 'Sending…' : 'Email Statement'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="statement-from">From</label>
            <input id="statement-from" type="date" value={from} onChange={(event) => setFrom(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="statement-to">To</label>
            <input id="statement-to" type="date" value={to} onChange={(event) => setTo(event.target.value)} className={fieldClass} />
          </div>
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={overdueOnly} onChange={(event) => setOverdueOnly(event.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Overdue items only (PDF and email)
        </label>

        {statement.isLoading ? (
          <Spinner label="Loading statement" />
        ) : statement.isError || !statement.data ? (
          <div className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-center text-sm text-slate-600">
            The statement could not be loaded for this period.
          </div>
        ) : (
          <>
            <div className="grid grid-cols-3 gap-3">
              {[
                ['Invoiced', statement.data.totalInvoiced],
                ['Paid', statement.data.totalPaid],
                ['Balance', statement.data.balance],
              ].map(([label, value]) => (
                <div key={label as string} className="rounded-xl border border-slate-200 bg-slate-50 px-3 py-2.5">
                  <div className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">{label}</div>
                  <div className="mt-1 text-sm font-semibold text-slate-900">{formatCurrency(value as number)}</div>
                </div>
              ))}
            </div>

            <div className="overflow-x-auto rounded-xl border border-slate-200">
              <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
                <thead className="bg-slate-50">
                  <tr>
                    <th className="px-3 py-2 font-medium text-slate-600">Date</th>
                    <th className="px-3 py-2 font-medium text-slate-600">Reference</th>
                    <th className="px-3 py-2 font-medium text-slate-600">Debit</th>
                    <th className="px-3 py-2 font-medium text-slate-600">Credit</th>
                    <th className="px-3 py-2 font-medium text-slate-600">Type</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 bg-white">
                  {statement.data.entries.length === 0 ? (
                    <tr>
                      <td colSpan={5} className="px-3 py-6 text-center text-slate-500">No activity in this period.</td>
                    </tr>
                  ) : (
                    statement.data.entries.map((entry, index) => (
                      <tr key={`${entry.reference}-${index}`}>
                        <td className="px-3 py-2 text-slate-700">{formatDate(entry.date)}</td>
                        <td className="px-3 py-2 font-mono text-slate-900">{entry.reference}</td>
                        <td className="px-3 py-2 text-slate-700">{entry.debit > 0 ? formatCurrency(entry.debit) : '—'}</td>
                        <td className="px-3 py-2 text-emerald-700">{entry.credit > 0 ? formatCurrency(entry.credit) : '—'}</td>
                        <td className="px-3 py-2"><StatusBadge status={entry.entryType === 'invoice' ? 'Invoice' : 'Payment'} /></td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="statement-message">Email message (optional)</label>
          <textarea
            id="statement-message"
            value={message}
            onChange={(event) => setMessage(event.target.value)}
            rows={3}
            placeholder="Added above the standard statement summary in the email body"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>
      </div>
    </FormDrawer>
  )
}
