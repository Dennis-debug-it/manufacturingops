import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Download } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { apiClient } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { useAllPayments, type PaymentListItem } from './api'
import { FinanceWorkspaceNav } from './FinanceWorkspaceNav'

export function ReceiptsPage() {
  const { data, isLoading, isError } = useAllPayments()
  const [downloadingId, setDownloadingId] = useState<string | null>(null)

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
    { key: 'invoiceId', header: 'Invoice', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/finance/invoices/${row.invoiceId}`}>{row.invoiceNumber ?? row.invoiceId}</Link> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'amount', header: 'Amount', cell: (row) => formatCurrency(row.amount) },
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
      <PageHeader title="Receipts" subtitle="Payments recorded across all invoices. Record a new receipt from the relevant Invoice Detail page." />
      <FinanceWorkspaceNav />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Receipts unavailable' : 'No receipts'}
        emptyDescription={isError ? 'Payments could not be loaded.' : 'Payments recorded against invoices will appear here.'}
      />
    </div>
  )
}
