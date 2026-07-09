import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { usePortalInvoices, type PortalInvoiceListItem } from '../api'

function money(amount: number, currency: string) {
  return amount.toLocaleString(undefined, { style: 'currency', currency })
}

export function PortalInvoicesPage() {
  const { data, isLoading, isError } = usePortalInvoices()

  const columns: DataTableColumn<PortalInvoiceListItem>[] = [
    { key: 'invoiceNumber', header: 'Invoice #', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/portal/invoices/${row.id}`}>{row.invoiceNumber}</Link> },
    { key: 'invoiceDate', header: 'Date', cell: (row) => formatDate(row.invoiceDate) },
    { key: 'dueDate', header: 'Due', cell: (row) => formatDate(row.dueDate) },
    { key: 'totalAmount', header: 'Total', cell: (row) => money(row.totalAmount, row.currency) },
    { key: 'balance', header: 'Balance', cell: (row) => money(row.balance, row.currency) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Invoices" subtitle="View invoices and payment history." />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Invoices unavailable' : 'No invoices yet'}
        emptyDescription={isError ? 'Invoices could not be loaded.' : 'Invoices raised for your account will appear here.'}
      />
    </div>
  )
}
