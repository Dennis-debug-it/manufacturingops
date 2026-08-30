import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useParams } from 'react-router-dom'
import { Download, Undo2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import { useConvertProformaToInvoice, useInvoice, useRecordPayment, useReversePayment, useSendInvoice, useVoidInvoice } from './api'
import { EditInvoiceDrawer } from './components/EditInvoiceDrawer'
import type { InvoiceLine, Payment, SourceQuotation } from './types'

const paymentMethods = ['Cash', 'MPesa', 'BankTransfer', 'Cheque', 'Card']

export function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canManage = usePermission('finance.manage')
  const { data: invoice, isLoading, isError } = useInvoice(id)
  const sendInvoice = useSendInvoice(id ?? '')
  const convertProforma = useConvertProformaToInvoice(id ?? '')
  const voidInvoice = useVoidInvoice(id ?? '')
  const recordPayment = useRecordPayment(id ?? '')

  const reversePayment = useReversePayment(id ?? '')

  const [showPayment, setShowPayment] = useState(false)
  const [showEdit, setShowEdit] = useState(false)
  const [amount, setAmount] = useState('')
  const [method, setMethod] = useState('MPesa')
  const [reference, setReference] = useState('')
  const [downloading, setDownloading] = useState(false)
  const [downloadingReceiptId, setDownloadingReceiptId] = useState<string | null>(null)
  const [paymentToReverse, setPaymentToReverse] = useState<Payment | null>(null)

  const isProforma = invoice?.documentType === 'ProformaInvoice'

  const send = async () => {
    try {
      await sendInvoice.mutateAsync(undefined)
      toast.success(isProforma ? 'Proforma invoice sent' : 'Invoice sent')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const convertToFinalInvoice = async () => {
    try {
      await convertProforma.mutateAsync()
      toast.success('Proforma converted to invoice')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const voidIt = async () => {
    try {
      await voidInvoice.mutateAsync(undefined)
      toast.success(isProforma ? 'Proforma invoice voided' : 'Invoice voided')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const submitPayment = async () => {
    const value = Number(amount)
    if (!Number.isFinite(value) || value <= 0) {
      toast.error('Enter a valid amount')
      return
    }
    try {
      await recordPayment.mutateAsync({ amount: value, paymentDate: new Date().toISOString(), method, reference: reference.trim() || null })
      toast.success('Payment recorded')
      setShowPayment(false)
      setAmount('')
      setReference('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const downloadReceipt = async (payment: Payment) => {
    setDownloadingReceiptId(payment.id)
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
      setDownloadingReceiptId(null)
    }
  }

  const downloadPdf = async () => {
    if (!id) return
    setDownloading(true)
    try {
      const response = await apiClient.get(endpoints.invoices.pdf(id), { responseType: 'blob' })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${invoice?.invoiceNumber ?? 'invoice'}.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not download the PDF')
    } finally {
      setDownloading(false)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading invoice" />
  }

  if (isError || !invoice || !id) {
    return <EmptyState title="Invoice unavailable" description="This invoice could not be loaded." />
  }

  const lineColumns: DataTableColumn<InvoiceLine>[] = [
    { key: 'description', header: 'Description', cell: (row) => row.description },
    { key: 'quantity', header: 'Qty', cell: (row) => `${row.quantity} ${row.unit}` },
    { key: 'unitRate', header: 'Rate', cell: (row) => formatCurrency(row.unitRate, invoice.currency) },
    { key: 'grandTotal', header: 'Total', cell: (row) => formatCurrency(row.grandTotal, invoice.currency) },
  ]

  const quotationColumns: DataTableColumn<SourceQuotation>[] = [
    {
      key: 'quoteNumber',
      header: 'Quotation',
      cell: (row) => (
        <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/quotes/${row.id}`}>
          {row.quoteNumber}
        </Link>
      ),
    },
    { key: 'subject', header: 'Subject', cell: (row) => row.subject ?? '-' },
    { key: 'quoteDate', header: 'Date', cell: (row) => formatDate(row.quoteDate) },
    { key: 'totalAmount', header: 'Total', cell: (row) => formatCurrency(row.totalAmount, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  const paymentColumns: DataTableColumn<Payment>[] = [
    { key: 'paymentNumber', header: 'Payment', cell: (row) => row.paymentNumber },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount, invoice.currency) },
    { key: 'method', header: 'Method', cell: (row) => row.method },
    { key: 'reference', header: 'Reference', cell: (row) => row.reference ?? '-' },
    { key: 'paymentDate', header: 'Date', cell: (row) => formatDate(row.paymentDate) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={() => void downloadReceipt(row)}
            disabled={downloadingReceiptId === row.id}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
          >
            <Download className="h-3.5 w-3.5" />
            Receipt
          </button>
          {canManage && row.status !== 'Reversed' ? (
            <button
              type="button"
              onClick={() => setPaymentToReverse(row)}
              className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-rose-200 bg-white px-3 text-xs font-medium text-rose-600 transition hover:bg-rose-50"
            >
              <Undo2 className="h-3.5 w-3.5" />
              Reverse
            </button>
          ) : null}
        </div>
      ),
    },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={invoice.invoiceNumber}
        subtitle={invoice.clientName ?? undefined}
        breadcrumbs={[{ label: 'Invoices', to: '/finance/invoices' }, { label: invoice.invoiceNumber }]}
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            <span className="inline-flex items-center rounded-full border border-slate-200 bg-slate-50 px-3 py-1 text-xs font-medium text-slate-600">
              {isProforma ? 'Proforma invoice' : 'Invoice'}
            </span>
            <StatusBadge status={invoice.status} />
          </div>
        }
        primaryAction={
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={downloadPdf} disabled={downloading} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              {downloading ? 'Preparing...' : 'Download PDF'}
            </button>
            {canManage && isProforma ? (
              <button type="button" onClick={convertToFinalInvoice} disabled={convertProforma.isPending} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
                {convertProforma.isPending ? 'Converting...' : 'Convert to Invoice'}
              </button>
            ) : null}
            {canManage && invoice.status === 'Draft' ? (
              <>
                <button type="button" onClick={() => setShowEdit(true)} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                  Edit
                </button>
                <button type="button" onClick={send} disabled={sendInvoice.isPending} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
                  Send
                </button>
              </>
            ) : null}
            {canManage && !isProforma && invoice.balance > 0 && invoice.status !== 'Void' ? (
              <button type="button" onClick={() => setShowPayment(true)} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                Record Payment
              </button>
            ) : null}
            {canManage && invoice.status !== 'Void' ? (
              <button type="button" onClick={voidIt} disabled={voidInvoice.isPending} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">
                Void
              </button>
            ) : null}
          </div>
        }
      />

      <FinanceWorkspaceNav />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {[
          ['Type', isProforma ? 'Proforma' : 'Invoice'],
          ['Subtotal', formatCurrency(invoice.subtotal, invoice.currency)],
          ['Total', formatCurrency(invoice.totalAmount, invoice.currency)],
          ['Balance', formatCurrency(invoice.balance, invoice.currency)],
        ].map(([label, value]) => (
          <div key={label} className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <div className="text-sm font-medium text-slate-600">{label}</div>
            <div className="mt-2 text-xl font-semibold text-slate-900">{value}</div>
          </div>
        ))}
      </div>

      {isProforma ? (
        <div className="rounded-2xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900">
          This document is still a proforma invoice. Convert it to a final invoice before recording payments.
        </div>
      ) : null}

      <SummarySection title="Line Items" description="Billed items on this document.">
        <DataTable columns={lineColumns} data={invoice.lines} emptyTitle="No lines" emptyDescription="No line items on this document." />
      </SummarySection>

      <SummarySection title="Source Quotations" description="Accepted quotations rolled into this document.">
        <DataTable
          columns={quotationColumns}
          data={invoice.sourceQuotations}
          emptyTitle="No source quotations"
          emptyDescription="This document was created directly rather than from selected quotations."
        />
      </SummarySection>

      <SummarySection title="Payment History" description="Payments recorded against this invoice.">
        <DataTable columns={paymentColumns} data={invoice.payments} emptyTitle="No payments" emptyDescription={isProforma ? 'Payments become available after this proforma is converted to a final invoice.' : 'Payments recorded for this invoice will appear here.'} />
      </SummarySection>

      <EditInvoiceDrawer open={showEdit} invoice={invoice} onClose={() => setShowEdit(false)} />

      <ConfirmDialog
        open={Boolean(paymentToReverse)}
        title="Reverse Payment"
        message={`Reverse payment ${paymentToReverse?.paymentNumber ?? ''} of ${formatCurrency(paymentToReverse?.amount ?? 0, invoice.currency)}? The invoice balance will be recalculated.`}
        confirmLabel="Reverse Payment"
        danger
        onCancel={() => setPaymentToReverse(null)}
        onConfirm={async () => {
          if (!paymentToReverse) return
          try {
            await reversePayment.mutateAsync(paymentToReverse.id)
            toast.success('Payment reversed')
          } catch {
            // interceptor surfaces the error toast
          } finally {
            setPaymentToReverse(null)
          }
        }}
      />

      <FormDrawer
        open={showPayment}
        title="Record Payment"
        onClose={() => setShowPayment(false)}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" onClick={() => setShowPayment(false)} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
              Cancel
            </button>
            <button type="button" onClick={submitPayment} disabled={recordPayment.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {recordPayment.isPending ? 'Saving...' : 'Record Payment'}
            </button>
          </div>
        }
      >
        <div className="space-y-4">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="payment-amount">Amount</label>
            <input id="payment-amount" type="number" step="0.01" value={amount} onChange={(event) => setAmount(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            <p className="mt-1 text-xs text-slate-500">Outstanding balance: {formatCurrency(invoice.balance, invoice.currency)}</p>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="payment-method">Method</label>
            <select id="payment-method" value={method} onChange={(event) => setMethod(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
              {paymentMethods.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="payment-reference">Reference</label>
            <input id="payment-reference" value={reference} onChange={(event) => setReference(event.target.value)} className="h-11 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
          </div>
        </div>
      </FormDrawer>
    </div>
  )
}
