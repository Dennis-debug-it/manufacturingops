import { Link } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { usePortalQuotations, type PortalQuotationListItem } from '../api'

export function PortalQuotesPage() {
  const { data, isLoading, isError } = usePortalQuotations()

  const columns: DataTableColumn<PortalQuotationListItem>[] = [
    { key: 'quoteNumber', header: 'Quote #', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/portal/quotes/${row.id}`}>{row.quoteNumber}</Link> },
    { key: 'quoteDate', header: 'Date', cell: (row) => formatDate(row.quoteDate) },
    { key: 'validUntil', header: 'Valid Until', cell: (row) => formatDate(row.validUntil) },
    { key: 'totalAmount', header: 'Total', cell: (row) => row.totalAmount.toLocaleString(undefined, { style: 'currency', currency: row.currency }) },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader title="Quotes" subtitle="Review and respond to quotations." />
      <DataTable
        columns={columns}
        data={data ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Quotes unavailable' : 'No quotes yet'}
        emptyDescription={isError ? 'Quotes could not be loaded.' : 'Quotations sent to you will appear here.'}
      />
    </div>
  )
}
