import { useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { usePortalInvoice } from '../api'

function money(amount: number, currency: string) {
  return amount.toLocaleString(undefined, { style: 'currency', currency })
}

export function PortalInvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = usePortalInvoice(id)

  if (isLoading) {
    return <Spinner label="Loading invoice" />
  }

  if (isError || !data) {
    return <EmptyState title="Invoice unavailable" description="This invoice could not be loaded." />
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={data.invoiceNumber}
        subtitle={`Due ${formatDate(data.dueDate)}`}
        breadcrumbs={[{ label: 'Invoices', to: '/portal/invoices' }, { label: data.invoiceNumber }]}
        secondaryActions={<StatusBadge status={data.status} />}
      />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm"><div className="text-xs uppercase text-slate-500">Total</div><div className="mt-1 text-lg font-semibold text-slate-900">{money(data.totalAmount, data.currency)}</div></div>
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm"><div className="text-xs uppercase text-slate-500">Paid</div><div className="mt-1 text-lg font-semibold text-emerald-600">{money(data.amountPaid, data.currency)}</div></div>
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm"><div className="text-xs uppercase text-slate-500">Balance</div><div className="mt-1 text-lg font-semibold text-rose-600">{money(data.balance, data.currency)}</div></div>
      </div>

      <div className="overflow-x-auto rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] shadow-sm">
        <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-4 py-3 font-medium text-slate-600">Description</th>
              <th className="px-4 py-3 font-medium text-slate-600">Qty</th>
              <th className="px-4 py-3 font-medium text-slate-600">Rate</th>
              <th className="px-4 py-3 font-medium text-slate-600">Total</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {data.lines.map((line, index) => (
              <tr key={index}>
                <td className="px-4 py-3 text-slate-700">{line.description}</td>
                <td className="px-4 py-3 text-slate-700">{line.quantity} {line.unit}</td>
                <td className="px-4 py-3 text-slate-700">{money(line.unitRate, data.currency)}</td>
                <td className="px-4 py-3 font-medium text-slate-900">{money(line.grandTotal, data.currency)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {data.payments.length > 0 ? (
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Payment History</h2>
          <div className="mt-3 space-y-2">
            {data.payments.map((payment) => (
              <div key={payment.id} className="flex items-center justify-between text-sm">
                <span className="text-slate-600">{formatDate(payment.paymentDate)} — {payment.paymentMethod}{payment.referenceNumber ? ` (${payment.referenceNumber})` : ''}</span>
                <span className="font-medium text-slate-900">{money(payment.amount, data.currency)}</span>
              </div>
            ))}
          </div>
        </div>
      ) : null}

      {data.notes ? (
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Notes</h2>
          <p className="mt-2 whitespace-pre-line text-sm text-slate-700">{data.notes}</p>
        </div>
      ) : null}
    </div>
  )
}
