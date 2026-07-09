import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { invoiceService } from '../../services/invoiceService'
import type { RecordPaymentInput, TenantInvoiceDetail } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import type { BadgeTone } from '../../types/app'

function statusTone(status: string): BadgeTone {
  switch (status) {
    case 'Paid': return 'success'
    case 'Sent': return 'info'
    case 'PartiallyPaid': return 'warning'
    case 'Overdue': return 'danger'
    case 'Void': return 'neutral'
    default: return 'default'
  }
}

const PAYMENT_METHODS = ['Cash', 'MPesa', 'BankTransfer', 'Cheque', 'Card'] as const

const emptyPaymentForm: RecordPaymentInput = {
  amount: 0,
  paymentDate: new Date().toISOString().slice(0, 10),
  method: 'Cash',
  reference: '',
  notes: '',
}

export function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManage = Boolean(permissions?.canManageFinance || permissions?.canManageBillableWork)

  const [paymentDrawerOpen, setPaymentDrawerOpen] = useState(false)
  const [paymentForm, setPaymentForm] = useState<RecordPaymentInput>(emptyPaymentForm)
  const [savingPayment, setSavingPayment] = useState(false)

  const {
    data: invoice,
    loading,
    error,
    reload,
  } = useAsyncData<TenantInvoiceDetail | null>(
    (signal) => (id ? invoiceService.get(id, signal) : Promise.resolve(null)),
    null,
    [id],
  )

  if (loading) return <LoadingState label="Loading invoice" />
  if (error) return <ErrorState title="Unable to load invoice" description={error} />
  if (!invoice) return <ErrorState title="Invoice not found" description="This invoice does not exist or you do not have access." />

  async function handleSend() {
    if (!invoice) return
    try {
      await invoiceService.send(invoice.id)
      pushToast({ title: 'Invoice sent', description: `${invoice.invoiceNumber} marked as Sent.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Send failed', description: err instanceof Error ? err.message : 'Failed to send.', tone: 'danger' })
    }
  }

  async function handleVoid() {
    if (!invoice || !confirm(`Void invoice ${invoice.invoiceNumber}?`)) return
    try {
      await invoiceService.void(invoice.id)
      pushToast({ title: 'Voided', description: `${invoice.invoiceNumber} voided.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Void failed', description: err instanceof Error ? err.message : 'Failed to void.', tone: 'danger' })
    }
  }

  async function handleDownloadPdf() {
    if (!invoice) return
    try {
      const result = await invoiceService.downloadPdf(invoice.id)
      const url = URL.createObjectURL(result.blob)
      const a = document.createElement('a')
      a.href = url
      a.download = result.fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      pushToast({ title: 'PDF failed', description: 'Could not download PDF.', tone: 'danger' })
    }
  }

  async function savePayment() {
    if (!invoice) return
    setSavingPayment(true)
    try {
      const payload: RecordPaymentInput = {
        ...paymentForm,
        paymentDate: new Date(`${paymentForm.paymentDate}T00:00:00.000Z`).toISOString(),
        amount: Number(paymentForm.amount),
      }
      await invoiceService.recordPayment(invoice.id, payload)
      pushToast({ title: 'Payment recorded', description: 'Payment has been applied to this invoice.', tone: 'success' })
      setPaymentDrawerOpen(false)
      setPaymentForm(emptyPaymentForm)
      await reload()
    } catch (err) {
      pushToast({ title: 'Payment failed', description: err instanceof Error ? err.message : 'Unable to record payment.', tone: 'danger' })
    } finally {
      setSavingPayment(false)
    }
  }

  async function handleReversePayment(paymentId: string) {
    if (!confirm('Reverse this payment?')) return
    try {
      await invoiceService.reversePayment(paymentId)
      pushToast({ title: 'Payment reversed', description: 'Payment has been reversed.', tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Reversal failed', description: err instanceof Error ? err.message : 'Unable to reverse.', tone: 'danger' })
    }
  }

  async function handleDownloadReceipt(paymentId: string) {
    try {
      const result = await invoiceService.downloadReceiptPdf(paymentId)
      const url = URL.createObjectURL(result.blob)
      const a = document.createElement('a')
      a.href = url
      a.download = result.fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      pushToast({ title: 'Receipt failed', description: 'Could not download receipt.', tone: 'danger' })
    }
  }

  const linkedWorkOrderIds = [...new Set(invoice.lines.filter((l) => l.workOrderId).map((l) => l.workOrderId!))]

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-app">{invoice.invoiceNumber}</h1>
            <Badge tone={statusTone(invoice.status)}>{invoice.status}</Badge>
          </div>
          <p className="mt-1 text-sm text-muted">{invoice.clientName} · Date: {formatDateOnly(invoice.invoiceDate)} · Due: {formatDateOnly(invoice.dueDate)}</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {invoice.status === 'Draft' && canManage ? (
            <button type="button" className="button-secondary" onClick={() => navigate(`/finance/invoices/${invoice.id}/edit`)}>Edit</button>
          ) : null}
          {invoice.status === 'Draft' && canManage ? (
            <button type="button" className="button-primary" onClick={() => void handleSend()}>Send</button>
          ) : null}
          <button type="button" className="button-secondary" onClick={() => void handleDownloadPdf()}>Download PDF</button>
          {invoice.status !== 'Paid' && invoice.status !== 'Void' && canManage ? (
            <button
              type="button"
              className="button-primary"
              onClick={() => {
                setPaymentForm({ ...emptyPaymentForm, amount: invoice.balance })
                setPaymentDrawerOpen(true)
              }}
            >
              Record Payment
            </button>
          ) : null}
          {invoice.status !== 'Void' && canManage ? (
            <button type="button" className="button-secondary text-danger" onClick={() => void handleVoid()}>Void</button>
          ) : null}
        </div>
      </div>

      {/* Lines */}
      <SectionCard title="Invoice lines" description="Billable items included in this invoice.">
        {invoice.lines.length === 0 ? (
          <EmptyState title="No lines" description="This invoice has no line items." />
        ) : (
          <DataTable
            rows={invoice.lines}
            rowKey={(r) => r.id}
            minTableWidth="min-w-[800px] w-full"
            columns={[
              { key: 'desc', header: 'Description', cell: (r) => r.description },
              { key: 'type', header: 'Type', cell: (r) => r.itemType },
              { key: 'qty', header: 'Qty', cell: (r) => `${r.quantity} ${r.unit}` },
              { key: 'rate', header: 'Rate', cell: (r) => `${invoice.currency} ${r.unitRate.toLocaleString(undefined, { minimumFractionDigits: 2 })}` },
              { key: 'lineTotal', header: 'Line Total', cell: (r) => `${invoice.currency} ${r.lineTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}` },
              { key: 'tax', header: 'Tax', cell: (r) => `${r.taxRate}% = ${invoice.currency} ${r.taxAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}` },
              { key: 'grand', header: 'Grand Total', cell: (r) => <span className="font-semibold">{invoice.currency} {r.grandTotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span> },
            ]}
          />
        )}
      </SectionCard>

      {/* Summary */}
      <div className="ml-auto max-w-sm rounded-2xl border border-border bg-surface p-4 space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-muted">Subtotal</span>
          <span>{invoice.currency} {invoice.subtotal.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
        </div>
        <div className="flex justify-between text-sm">
          <span className="text-muted">Tax</span>
          <span>{invoice.currency} {invoice.taxAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
        </div>
        <div className="flex justify-between font-semibold">
          <span>Total</span>
          <span>{invoice.currency} {invoice.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
        </div>
        <div className="flex justify-between text-sm text-success">
          <span>Paid</span>
          <span>{invoice.currency} {invoice.amountPaid.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
        </div>
        <div className="flex justify-between font-semibold text-danger">
          <span>Balance Due</span>
          <span>{invoice.currency} {invoice.balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
        </div>
      </div>

      {/* Payments */}
      <SectionCard title="Payments" description="Payments recorded against this invoice.">
        {invoice.payments.length === 0 ? (
          <EmptyState title="No payments yet" description="Record the first payment against this invoice." />
        ) : (
          <DataTable
            rows={invoice.payments}
            rowKey={(r) => r.id}
            minTableWidth="min-w-[700px] w-full"
            columns={[
              { key: 'date', header: 'Date', cell: (r) => formatDateOnly(r.paymentDate) },
              { key: 'method', header: 'Method', cell: (r) => r.method },
              { key: 'ref', header: 'Reference', cell: (r) => r.reference ?? '—' },
              { key: 'amount', header: 'Amount', cell: (r) => `KES ${r.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}` },
              { key: 'status', header: 'Status', cell: (r) => <Badge tone={r.status === 'Confirmed' ? 'success' : 'neutral'}>{r.status}</Badge> },
              {
                key: 'actions',
                header: 'Actions',
                cell: (r) => (
                  <div className="flex gap-1">
                    <button type="button" className="button-ghost py-1 text-xs" onClick={() => void handleDownloadReceipt(r.id)}>Receipt</button>
                    {r.status === 'Confirmed' && canManage ? (
                      <button type="button" className="button-ghost py-1 text-xs text-danger" onClick={() => void handleReversePayment(r.id)}>Reverse</button>
                    ) : null}
                  </div>
                ),
              },
            ]}
          />
        )}
      </SectionCard>

      {/* Linked Work Orders */}
      {linkedWorkOrderIds.length > 0 ? (
        <SectionCard title="Linked work orders" description="Work orders contributing to this invoice.">
          <div className="flex flex-wrap gap-2">
            {linkedWorkOrderIds.map((woId) => (
              <Link key={woId} to={`/work-orders/${woId}`} className="rounded-lg border border-border px-3 py-1.5 text-sm text-accent-strong hover:underline">
                View work order
              </Link>
            ))}
          </div>
        </SectionCard>
      ) : null}

      {/* Notes / Terms */}
      {(invoice.notes ?? invoice.termsAndConditions) ? (
        <SectionCard title="Notes & terms">
          {invoice.notes ? <p className="text-sm text-app">{invoice.notes}</p> : null}
          {invoice.termsAndConditions ? <p className="mt-2 text-xs text-muted whitespace-pre-wrap">{invoice.termsAndConditions}</p> : null}
        </SectionCard>
      ) : null}

      {/* Record Payment Drawer */}
      <Drawer
        open={paymentDrawerOpen}
        title="Record payment"
        description="Apply a payment against this invoice."
        onClose={() => setPaymentDrawerOpen(false)}
      >
        <div className="space-y-4 p-4">
          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Amount ({invoice.currency})</span>
            <input
              type="number"
              min={0}
              step={0.01}
              value={paymentForm.amount}
              onChange={(e) => setPaymentForm((f) => ({ ...f, amount: Number(e.target.value) }))}
              className="field-input"
            />
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Payment date</span>
            <input
              type="date"
              value={paymentForm.paymentDate}
              onChange={(e) => setPaymentForm((f) => ({ ...f, paymentDate: e.target.value }))}
              className="field-input"
            />
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Method</span>
            <select
              value={paymentForm.method}
              onChange={(e) => setPaymentForm((f) => ({ ...f, method: e.target.value }))}
              className="field-input"
            >
              {PAYMENT_METHODS.map((m) => (
                <option key={m} value={m}>{m}</option>
              ))}
            </select>
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Reference (M-Pesa code, cheque #, etc.)</span>
            <input
              type="text"
              value={paymentForm.reference ?? ''}
              onChange={(e) => setPaymentForm((f) => ({ ...f, reference: e.target.value }))}
              className="field-input"
            />
          </label>
          <label className="block space-y-1">
            <span className="text-sm font-medium text-app">Notes</span>
            <textarea
              value={paymentForm.notes ?? ''}
              onChange={(e) => setPaymentForm((f) => ({ ...f, notes: e.target.value }))}
              className="field-input"
              rows={2}
            />
          </label>
        </div>
        <StickyActionFooter>
          <button type="button" className="button-secondary" onClick={() => setPaymentDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void savePayment()} disabled={savingPayment}>
            {savingPayment ? 'Saving…' : 'Record payment'}
          </button>
        </StickyActionFooter>
      </Drawer>
    </div>
  )
}
