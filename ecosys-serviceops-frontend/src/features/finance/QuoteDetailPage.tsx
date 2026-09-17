import { useState } from 'react'
import toast from 'react-hot-toast'
import { useNavigate, useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'
import {
  useAcceptQuotation,
  useBuildProformaInvoice,
  useConvertQuotationToInvoice,
  useConvertQuotationToWorkOrder,
  useQuotation,
  useRejectQuotation,
  useSendQuotation,
} from './api'
import type { QuotationLine } from './types'

export function QuoteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const canManage = usePermission('finance.manage')
  const { data: quote, isLoading, isError } = useQuotation(id)
  const sendQuote = useSendQuotation(id ?? '')
  const accept = useAcceptQuotation(id ?? '')
  const reject = useRejectQuotation(id ?? '')
  const buildProforma = useBuildProformaInvoice()
  const convertToInvoice = useConvertQuotationToInvoice(id ?? '')
  const convertToWorkOrder = useConvertQuotationToWorkOrder(id ?? '')
  const [downloading, setDownloading] = useState(false)

  const run = async (label: string, mutateAsync: () => Promise<unknown>) => {
    try {
      await mutateAsync()
      toast.success(label)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const downloadPdf = async () => {
    if (!id) return
    setDownloading(true)
    try {
      const response = await apiClient.get(endpoints.quotations.pdf(id), { responseType: 'blob', suppressGlobalErrorToast: true })
      const url = URL.createObjectURL(response.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `${quote?.quoteNumber ?? 'quotation'}.pdf`
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      toast.error('Could not download the PDF')
    } finally {
      setDownloading(false)
    }
  }

  if (isLoading) {
    return <Spinner label="Loading quote" />
  }

  if (isError || !quote || !id) {
    return <EmptyState title="Quote unavailable" description="This quote could not be loaded." />
  }

  const lineColumns: DataTableColumn<QuotationLine>[] = [
    { key: 'description', header: 'Description', cell: (row) => row.description },
    { key: 'quantity', header: 'Qty', cell: (row) => `${row.quantity} ${row.unit}` },
    { key: 'unitRate', header: 'Rate', cell: (row) => formatCurrency(row.unitRate, quote.currency) },
    { key: 'grandTotal', header: 'Total', cell: (row) => formatCurrency(row.grandTotal, quote.currency) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={quote.quoteNumber}
        subtitle={quote.subject ?? quote.clientName ?? undefined}
        breadcrumbs={[{ label: 'Quotes', to: '/finance/quotes' }, { label: quote.quoteNumber }]}
        secondaryActions={<StatusBadge status={quote.status} />}
        primaryAction={
          <div className="flex flex-wrap gap-2">
            <button type="button" onClick={() => void downloadPdf()} disabled={downloading} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:opacity-60">
              {downloading ? 'Preparing…' : 'Download PDF'}
            </button>
            {canManage ? (
            <>
              {quote.status === 'Draft' ? (
                <button type="button" onClick={() => run('Quote sent', () => sendQuote.mutateAsync(undefined))} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Send</button>
              ) : null}
              {quote.status === 'Sent' ? (
                <>
                  <button type="button" onClick={() => run('Quote accepted', () => accept.mutateAsync(undefined))} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">Accept</button>
                  <button type="button" onClick={() => run('Quote rejected', () => reject.mutateAsync(undefined))} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 transition hover:bg-rose-50">Decline</button>
                </>
              ) : null}
              {quote.status === 'Accepted' && !quote.convertedToInvoiceId ? (
                <button
                  type="button"
                  onClick={async () => {
                    try {
                      const invoice = await buildProforma.mutateAsync({
                        quotationIds: [quote.id],
                        invoiceDate: new Date().toISOString(),
                        dueDate: new Date(Date.now() + 1000 * 60 * 60 * 24 * 30).toISOString(),
                      })
                      toast.success('Proforma invoice created')
                      navigate(`/finance/invoices/${invoice.id}`)
                    } catch {
                      // interceptor surfaces the error toast
                    }
                  }}
                  className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
                >
                  Build Proforma
                </button>
              ) : null}
              {quote.status === 'Accepted' && !quote.convertedToInvoiceId ? (
                <button
                  type="button"
                  onClick={async () => {
                    try {
                      const invoice = await convertToInvoice.mutateAsync()
                      toast.success('Converted to invoice')
                      navigate(`/finance/invoices/${invoice.id}`)
                    } catch {
                      // interceptor surfaces the error toast
                    }
                  }}
                  className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                >
                  Convert to Invoice
                </button>
              ) : null}
              {quote.status === 'Accepted' && !quote.convertedToWorkOrderId ? (
                <button
                  type="button"
                  onClick={async () => {
                    try {
                      const wo = await convertToWorkOrder.mutateAsync()
                      toast.success('Converted to work order')
                      navigate(`/work-orders/${wo.workOrderId}`)
                    } catch {
                      // interceptor surfaces the error toast
                    }
                  }}
                  className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
                >
                  Convert to Work Order
                </button>
              ) : null}
            </>
            ) : null}
          </div>
        }
      />

      <FinanceWorkspaceNav />

      <SummarySection title="Quote Overview" description="Client, validity, and totals.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            ['Client', quote.clientName ?? '-'],
            ['Quote Date', formatDate(quote.quoteDate)],
            ['Valid Until', formatDate(quote.validUntil)],
            ['Subtotal', formatCurrency(quote.subtotal, quote.currency)],
            ['Tax', formatCurrency(quote.taxAmount, quote.currency)],
            ['Total', formatCurrency(quote.totalAmount, quote.currency)],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 text-sm text-slate-900">{value}</div>
            </div>
          ))}
        </div>
      </SummarySection>

      <SummarySection title="Line Items" description="Quoted items.">
        <DataTable columns={lineColumns} data={quote.lines} emptyTitle="No lines" emptyDescription="No line items on this quote." />
      </SummarySection>
    </div>
  )
}
