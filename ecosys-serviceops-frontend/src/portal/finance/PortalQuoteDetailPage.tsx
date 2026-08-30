import { useState } from 'react'
import { useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { formatDate } from '../../shared/utils/dates'
import { useAcceptPortalQuotation, usePortalQuotation, useRejectPortalQuotation } from '../api'

function money(amount: number, currency: string) {
  return amount.toLocaleString(undefined, { style: 'currency', currency })
}

export function PortalQuoteDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = usePortalQuotation(id)
  const accept = useAcceptPortalQuotation(id ?? '')
  const reject = useRejectPortalQuotation(id ?? '')
  const [showReject, setShowReject] = useState(false)

  if (isLoading) {
    return <Spinner label="Loading quote" />
  }

  if (isError || !data) {
    return <EmptyState title="Quote unavailable" description="This quotation could not be loaded." />
  }

  const canRespond = data.status === 'Sent'

  return (
    <div className="space-y-6">
      <PageHeader
        title={data.quoteNumber}
        subtitle={data.subject ?? `Valid until ${formatDate(data.validUntil)}`}
        breadcrumbs={[{ label: 'Quotes', to: '/portal/quotes' }, { label: data.quoteNumber }]}
        secondaryActions={<StatusBadge status={data.status} />}
        primaryAction={
          canRespond ? (
            <div className="flex gap-2">
              <button type="button" onClick={() => setShowReject(true)} className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-600 hover:bg-rose-50">Decline</button>
              <button
                type="button"
                onClick={async () => {
                  try {
                    await accept.mutateAsync()
                    toast.success('Quote accepted')
                  } catch {
                    // interceptor surfaces the error toast
                  }
                }}
                disabled={accept.isPending}
                className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60"
              >
                {accept.isPending ? 'Accepting…' : 'Accept Quote'}
              </button>
            </div>
          ) : undefined
        }
      />

      {data.rejectionReason ? (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">Declined: {data.rejectionReason}</div>
      ) : null}

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
          <tfoot className="bg-slate-50">
            <tr>
              <td colSpan={3} className="px-4 py-3 text-right font-medium text-slate-600">Total</td>
              <td className="px-4 py-3 font-semibold text-slate-900">{money(data.totalAmount, data.currency)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      <ConfirmDialog
        open={showReject}
        title="Decline Quote"
        message="Are you sure you want to decline this quotation? You can add a reason for our team."
        confirmLabel="Decline Quote"
        danger
        onCancel={() => setShowReject(false)}
        onConfirm={async () => {
          try {
            await reject.mutateAsync(undefined)
            toast.success('Quote declined')
            setShowReject(false)
          } catch {
            setShowReject(false)
          }
        }}
      />
    </div>
  )
}
