import { useEffect, useState } from 'react'
import { Mail, Eye, AlertTriangle } from 'lucide-react'
import { Drawer } from '../../components/ui/Drawer'
import { useToast } from '../../components/ui/ToastProvider'
import { financeReportsService } from '../../services/financeReportsService'
import { statementService } from '../../services/statementService'
import type { ClientStatementResponse } from '../../types/api'

interface Props {
  open: boolean
  onClose: () => void
  clientId: string
  clientName: string
  clientEmail?: string | null
  /** If true, default the filter to overdue-only (e.g. opened from aging table) */
  defaultOverdueOnly?: boolean
}

function defaultPeriod() {
  const to = new Date()
  const from = new Date()
  from.setMonth(from.getMonth() - 6)
  return {
    from: from.toISOString().slice(0, 10),
    to: to.toISOString().slice(0, 10),
  }
}

const fmt = (v: number) =>
  `KES ${v.toLocaleString(undefined, { minimumFractionDigits: 2 })}`

export function EmailStatementDrawer({
  open,
  onClose,
  clientId,
  clientName,
  clientEmail,
  defaultOverdueOnly = false,
}: Props) {
  const { pushToast } = useToast()

  const [from, setFrom] = useState(defaultPeriod().from)
  const [to, setTo] = useState(defaultPeriod().to)
  const [overdueOnly, setOverdueOnly] = useState(defaultOverdueOnly)
  const [additionalRecipients, setAdditionalRecipients] = useState('')
  const [message, setMessage] = useState('')

  const [preview, setPreview] = useState<ClientStatementResponse | null>(null)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [previewError, setPreviewError] = useState<string | null>(null)

  const [sending, setSending] = useState(false)
  const [pdfLoading, setPdfLoading] = useState(false)

  // Reset state when drawer opens/closes
  useEffect(() => {
    if (open) {
      const period = defaultPeriod()
      setFrom(period.from)
      setTo(period.to)
      setOverdueOnly(defaultOverdueOnly)
      setAdditionalRecipients('')
      setMessage('')
      setPreview(null)
      setPreviewError(null)
    }
  }, [open, defaultOverdueOnly])

  // Auto-load preview totals when period/filter changes
  useEffect(() => {
    if (!open || !clientId) return
    let cancelled = false
    setPreviewLoading(true)
    setPreviewError(null)
    financeReportsService
      .getClientStatement({ clientId, from, to })
      .then((result) => {
        if (!cancelled) setPreview(result)
      })
      .catch((err) => {
        if (!cancelled) setPreviewError(err instanceof Error ? err.message : 'Failed to load preview.')
      })
      .finally(() => {
        if (!cancelled) setPreviewLoading(false)
      })
    return () => { cancelled = true }
  }, [open, clientId, from, to])

  async function handlePreviewPdf() {
    setPdfLoading(true)
    try {
      const blob = await statementService.previewStatementPdf(clientId, from, to, overdueOnly)
      const url = URL.createObjectURL(blob)
      window.open(url, '_blank')
      setTimeout(() => URL.revokeObjectURL(url), 10_000)
    } catch (err) {
      pushToast({
        title: 'Preview failed',
        description: err instanceof Error ? err.message : 'Unable to generate PDF.',
        tone: 'danger',
      })
    } finally {
      setPdfLoading(false)
    }
  }

  async function handleSend() {
    if (!clientEmail) {
      pushToast({ title: 'No email address', description: `${clientName} has no email address on file.`, tone: 'danger' })
      return
    }
    setSending(true)
    try {
      const extra = additionalRecipients
        .split(/[,\s]+/)
        .map((e) => e.trim())
        .filter(Boolean)

      await statementService.emailStatement({
        clientId,
        from,
        to,
        additionalRecipients: extra.length > 0 ? extra : undefined,
        message: message.trim() || undefined,
        overdueOnly,
      })

      pushToast({
        title: 'Statement sent',
        description: `Statement emailed to ${clientEmail}.`,
        tone: 'success',
      })
      onClose()
    } catch (err) {
      pushToast({
        title: 'Send failed',
        description: err instanceof Error ? err.message : 'Unable to send statement.',
        tone: 'danger',
      })
    } finally {
      setSending(false)
    }
  }

  const invoiceCount = preview
    ? preview.entries.filter((e) => e.entryType === 'invoice').length
    : 0

  return (
    <Drawer
      open={open}
      title="Email Statement of Account"
      description={clientEmail ? `Sending to: ${clientEmail}` : `${clientName} — no email address on file`}
      onClose={onClose}
    >
      <div className="space-y-5">

        {/* No email warning */}
        {!clientEmail && (
          <div className="flex items-start gap-3 rounded-2xl border border-warning/30 bg-warning/10 p-4">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-warning" />
            <p className="text-sm text-warning font-medium">
              This client has no email address. Add one in the client profile before sending.
            </p>
          </div>
        )}

        {/* Period */}
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted">Period</p>
          <div className="flex flex-wrap gap-3">
            <label className="block space-y-1">
              <span className="text-xs text-muted">From</span>
              <input
                type="date"
                value={from}
                onChange={(e) => setFrom(e.target.value)}
                className="field-input py-1 text-sm"
              />
            </label>
            <label className="block space-y-1">
              <span className="text-xs text-muted">To</span>
              <input
                type="date"
                value={to}
                onChange={(e) => setTo(e.target.value)}
                className="field-input py-1 text-sm"
              />
            </label>
          </div>
        </div>

        {/* Filter */}
        <div>
          <p className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted">Filter</p>
          <div className="space-y-2">
            <label className="flex cursor-pointer items-center gap-3">
              <input
                type="radio"
                name="statementFilter"
                checked={!overdueOnly}
                onChange={() => setOverdueOnly(false)}
                className="h-4 w-4 accent-accent"
              />
              <span className="text-sm">All outstanding invoices</span>
            </label>
            <label className="flex cursor-pointer items-center gap-3">
              <input
                type="radio"
                name="statementFilter"
                checked={overdueOnly}
                onChange={() => setOverdueOnly(true)}
                className="h-4 w-4 accent-accent"
              />
              <span className="text-sm">Overdue invoices only</span>
            </label>
          </div>
        </div>

        {/* Additional recipients */}
        <div>
          <label className="block space-y-1">
            <span className="text-xs font-semibold uppercase tracking-widest text-muted">
              Additional recipients <span className="font-normal normal-case">(optional)</span>
            </span>
            <input
              type="text"
              value={additionalRecipients}
              onChange={(e) => setAdditionalRecipients(e.target.value)}
              placeholder="email1@co.com, email2@co.com"
              className="field-input w-full text-sm"
            />
          </label>
        </div>

        {/* Personal message */}
        <div>
          <label className="block space-y-1">
            <span className="text-xs font-semibold uppercase tracking-widest text-muted">
              Personal message <span className="font-normal normal-case">(optional)</span>
            </span>
            <textarea
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              rows={3}
              placeholder={`Dear ${clientName.split(' ')[0]}, please find your statement attached…`}
              className="field-input w-full resize-none text-sm"
            />
          </label>
        </div>

        {/* Preview panel */}
        <div className="rounded-2xl border border-border bg-surface-muted p-4">
          <p className="mb-2 text-xs font-semibold uppercase tracking-widest text-muted">Preview</p>
          {previewLoading ? (
            <p className="text-sm text-muted animate-pulse">Loading…</p>
          ) : previewError ? (
            <p className="text-sm text-danger">{previewError}</p>
          ) : preview ? (
            <div className="space-y-1 text-sm">
              <p>
                <span className="text-muted">{invoiceCount} invoice{invoiceCount !== 1 ? 's' : ''}</span>
                {' · '}
                <span className="font-semibold">Total outstanding: {fmt(preview.balance)}</span>
              </p>
              {preview.balance > 0 && (
                <p className="text-xs text-danger font-medium">
                  Balance due: {fmt(preview.balance)}
                </p>
              )}
            </div>
          ) : (
            <p className="text-sm text-muted">Select a period to see a summary.</p>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex flex-wrap gap-3 pt-2">
          <button
            type="button"
            className="button-secondary flex items-center gap-2"
            onClick={() => void handlePreviewPdf()}
            disabled={pdfLoading}
          >
            <Eye className="h-4 w-4" />
            {pdfLoading ? 'Generating…' : 'Preview PDF'}
          </button>

          <button
            type="button"
            className="button-primary flex items-center gap-2"
            onClick={() => void handleSend()}
            disabled={sending || !clientEmail}
          >
            <Mail className="h-4 w-4" />
            {sending ? 'Sending…' : 'Send Statement'}
          </button>
        </div>

      </div>
    </Drawer>
  )
}
