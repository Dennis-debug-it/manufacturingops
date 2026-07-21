import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Download, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useClients } from '../clients/api'
import { useAllPayments, useInvoices, useReceivePayment, type PaymentListItem } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'

const paymentMethods = ['Cash', 'MPesa', 'BankTransfer', 'Cheque', 'Card'] as const

export function ReceiptsPage() {
  const { data, isLoading, isError } = useAllPayments()
  const canManage = usePermission('finance.manage')
  const [downloadingId, setDownloadingId] = useState<string | null>(null)
  const [showReceive, setShowReceive] = useState(false)

  const downloadReceipt = async (payment: PaymentListItem) => {
    setDownloadingId(payment.id)
    try {
      const response = await apiClient.get(endpoints.invoices.receiptPdf(payment.id), { responseType: 'blob' })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${payment.paymentNumber}.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not download the receipt PDF')
    } finally {
      setDownloadingId(null)
    }
  }

  const columns: DataTableColumn<PaymentListItem>[] = [
    { key: 'paymentNumber', header: 'Receipt', cell: (row) => row.paymentNumber },
    {
      key: 'invoiceId',
      header: 'Invoice',
      cell: (row) =>
        row.invoiceId ? (
          <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/invoices/${row.invoiceId}`}>{row.invoiceNumber ?? row.invoiceId}</Link>
        ) : (
          <span className="text-slate-400">On account</span>
        ),
    },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount) },
    {
      key: 'unallocatedAmount',
      header: 'Unapplied',
      cell: (row) => (row.unallocatedAmount > 0 ? <span className="font-medium text-amber-600">{formatCurrency(row.unallocatedAmount)}</span> : <span className="text-slate-400">—</span>),
    },
    { key: 'method', header: 'Method', cell: (row) => row.method },
    { key: 'paymentDate', header: 'Date', cell: (row) => formatDate(row.paymentDate) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <button
          type="button"
          onClick={() => void downloadReceipt(row)}
          disabled={downloadingId === row.id}
          className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
        >
          <Download className="h-3.5 w-3.5" />
          {downloadingId === row.id ? 'Preparing…' : 'PDF'}
        </button>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Receipts"
        subtitle="Payments recorded across all invoices. Receive a payment and allocate it across a client's open invoices."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowReceive(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />
              Receive Payment
            </button>
          ) : undefined
        }
      />
      <FinanceWorkspaceNav />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Receipts unavailable' : 'No receipts'}
        emptyDescription={isError ? 'Payments could not be loaded.' : 'Payments recorded against invoices will appear here.'}
      />
      <ReceivePaymentDrawer open={showReceive} onClose={() => setShowReceive(false)} />
    </div>
  )
}

function ReceivePaymentDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { data: clients } = useClients()
  const receive = useReceivePayment()
  const [clientId, setClientId] = useState('')
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState<(typeof paymentMethods)[number]>('MPesa')
  const [paymentDate, setPaymentDate] = useState(new Date().toISOString().slice(0, 10))
  const [reference, setReference] = useState('')
  const [amounts, setAmounts] = useState<Record<string, string>>({})

  const { data: invoices } = useInvoices(clientId ? { clientId } : undefined)
  const openInvoices = useMemo(
    () => (clientId ? (invoices ?? []).filter((inv) => inv.balance > 0 && inv.status !== 'Void') : []),
    [invoices, clientId],
  )

  const total = Number(amount) || 0
  const allocated = Object.values(amounts).reduce((sum, v) => sum + (Number(v) || 0), 0)
  const leftover = total - allocated

  const close = () => {
    setClientId(''); setAmount(''); setReference(''); setAmounts({})
    onClose()
  }

  /** Fill invoice amounts oldest-first up to the entered payment amount. */
  const autoAllocate = () => {
    let remaining = total
    const next: Record<string, string> = {}
    for (const inv of [...openInvoices].sort((a, b) => a.dueDate.localeCompare(b.dueDate))) {
      if (remaining <= 0) break
      const apply = Math.min(remaining, inv.balance)
      next[inv.id] = String(apply)
      remaining -= apply
    }
    setAmounts(next)
  }

  const submit = async () => {
    if (!clientId || total <= 0) {
      toast.error('Select a client and enter an amount')
      return
    }
    if (allocated > total + 0.005) {
      toast.error('Allocations exceed the payment amount')
      return
    }
    const allocations = Object.entries(amounts)
      .map(([invoiceId, v]) => ({ invoiceId, amount: Number(v) || 0 }))
      .filter((a) => a.amount > 0)
    try {
      await receive.mutateAsync({
        clientId,
        amount: total,
        paymentDate: new Date(paymentDate).toISOString(),
        method,
        reference: reference.trim() || null,
        allocations,
      })
      toast.success(leftover > 0 ? `Payment received · ${formatCurrency(leftover)} held as credit` : 'Payment received')
      close()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const fieldClass = 'h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none transition focus:border-slate-900'
  const labelClass = 'mb-1.5 block text-sm font-medium text-slate-700'

  return (
    <FormDrawer
      open={open}
      title="Receive Payment"
      onClose={close}
      footer={
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm text-slate-500">Unapplied: <span className={leftover < 0 ? 'font-semibold text-rose-600' : 'font-semibold text-slate-900'}>{formatCurrency(leftover)}</span></span>
          <button type="button" onClick={submit} disabled={receive.isPending} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-60">
            {receive.isPending ? 'Saving…' : 'Receive Payment'}
          </button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="rp-client">Client</label>
          <select id="rp-client" value={clientId} onChange={(event) => { setClientId(event.target.value); setAmounts({}) }} className={fieldClass}>
            <option value="">Select a client</option>
            {(clients ?? []).map((client) => (<option key={client.id} value={client.id}>{client.clientName}</option>))}
          </select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="rp-amount">Amount</label>
            <input id="rp-amount" type="number" min="0" value={amount} onChange={(event) => setAmount(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="rp-method">Method</label>
            <select id="rp-method" value={method} onChange={(event) => setMethod(event.target.value as (typeof paymentMethods)[number])} className={fieldClass}>
              {paymentMethods.map((m) => (<option key={m} value={m}>{m}</option>))}
            </select>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className={labelClass} htmlFor="rp-date">Date</label>
            <input id="rp-date" type="date" value={paymentDate} onChange={(event) => setPaymentDate(event.target.value)} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="rp-ref">Reference</label>
            <input id="rp-ref" value={reference} onChange={(event) => setReference(event.target.value)} className={fieldClass} placeholder="e.g. M-Pesa code" />
          </div>
        </div>

        {clientId && (
          <div>
            <div className="mb-2 flex items-center justify-between">
              <label className={labelClass}>Apply to invoices</label>
              <button type="button" onClick={autoAllocate} disabled={total <= 0 || openInvoices.length === 0} className="text-xs font-medium text-slate-600 underline decoration-slate-300 underline-offset-4 hover:text-slate-900 disabled:opacity-40">
                Auto-allocate oldest first
              </button>
            </div>
            {openInvoices.length === 0 ? (
              <p className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-500">No open invoices — the full amount is held as customer credit.</p>
            ) : (
              <div className="space-y-2">
                {openInvoices.map((inv) => (
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
                ))}
              </div>
            )}
          </div>
        )}
      </div>
    </FormDrawer>
  )
}
