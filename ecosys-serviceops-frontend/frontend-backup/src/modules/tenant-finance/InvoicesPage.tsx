import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { SectionCard } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { invoiceService } from '../../services/invoiceService'
import type { TenantInvoiceRecord } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import type { BadgeTone } from '../../types/app'

const STATUS_TABS = ['All', 'Draft', 'Sent', 'Overdue', 'Paid'] as const
type StatusTab = (typeof STATUS_TABS)[number]

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

export function InvoicesPage() {
  const navigate = useNavigate()
  const { permissions } = useAuth()
  const { pushToast } = useToast()
  const canManage = Boolean(permissions?.canManageFinance || permissions?.canManageBillableWork)

  const [activeTab, setActiveTab] = useState<StatusTab>('All')
  const [search, setSearch] = useState('')

  const { data, loading, error, reload } = useAsyncData<TenantInvoiceRecord[]>(
    (signal) => invoiceService.list({ status: activeTab === 'All' ? undefined : activeTab, signal }),
    [],
    [activeTab],
  )

  const filtered = search.trim()
    ? data.filter(
        (inv) =>
          inv.invoiceNumber.toLowerCase().includes(search.toLowerCase()) ||
          (inv.clientName ?? '').toLowerCase().includes(search.toLowerCase()),
      )
    : data

  async function handleDownloadPdf(inv: TenantInvoiceRecord) {
    try {
      const result = await invoiceService.downloadPdf(inv.id)
      const url = URL.createObjectURL(result.blob)
      const a = document.createElement('a')
      a.href = url
      a.download = result.fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch {
      pushToast({ title: 'PDF failed', description: 'Could not download invoice PDF.', tone: 'danger' })
    }
  }

  async function handleSend(inv: TenantInvoiceRecord) {
    try {
      await invoiceService.send(inv.id)
      pushToast({ title: 'Invoice sent', description: `${inv.invoiceNumber} marked as Sent.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Send failed', description: err instanceof Error ? err.message : 'Unable to send invoice.', tone: 'danger' })
    }
  }

  async function handleVoid(inv: TenantInvoiceRecord) {
    if (!confirm(`Void invoice ${inv.invoiceNumber}?`)) return
    try {
      await invoiceService.void(inv.id)
      pushToast({ title: 'Invoice voided', description: `${inv.invoiceNumber} has been voided.`, tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Void failed', description: err instanceof Error ? err.message : 'Unable to void invoice.', tone: 'danger' })
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex gap-1 overflow-x-auto">
          {STATUS_TABS.map((tab) => (
            <button
              key={tab}
              type="button"
              onClick={() => setActiveTab(tab)}
              className={`rounded-full px-4 py-1.5 text-sm font-medium transition-colors ${
                activeTab === tab
                  ? 'bg-accent text-white'
                  : 'bg-surface-muted text-muted hover:text-app'
              }`}
            >
              {tab}
            </button>
          ))}
        </div>
        <div className="flex items-center gap-2">
          <input
            type="text"
            placeholder="Search invoice # or client…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="field-input w-60 py-1.5 text-sm"
          />
          {canManage ? (
            <button
              type="button"
              className="button-primary"
              onClick={() => navigate('/finance/invoices/new')}
            >
              + New Invoice
            </button>
          ) : null}
        </div>
      </div>

      <SectionCard title="Invoices" description="Tenant invoices sent to clients for billable work.">
        {loading ? <LoadingState label="Loading invoices" /> : null}
        {!loading && error ? <ErrorState title="Unable to load invoices" description={error} /> : null}
        {!loading && !error && filtered.length === 0 ? (
          <EmptyState title="No invoices found" description="Create an invoice or generate one from a work order's Billing tab." />
        ) : null}
        {!loading && !error && filtered.length > 0 ? (
          <DataTable
            rows={filtered}
            rowKey={(r) => r.id}
            minTableWidth="min-w-[1100px] w-full"
            columns={[
              {
                key: 'number',
                header: 'Invoice #',
                cell: (r) => (
                  <button
                    type="button"
                    className="font-semibold text-accent-strong hover:underline"
                    onClick={() => navigate(`/finance/invoices/${r.id}`)}
                  >
                    {r.invoiceNumber}
                  </button>
                ),
              },
              { key: 'client', header: 'Client', cell: (r) => r.clientName ?? '—' },
              { key: 'date', header: 'Date', cell: (r) => formatDateOnly(r.invoiceDate) },
              { key: 'dueDate', header: 'Due Date', cell: (r) => formatDateOnly(r.dueDate) },
              {
                key: 'amount',
                header: 'Amount',
                cell: (r) => `${r.currency} ${r.totalAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`,
              },
              {
                key: 'paid',
                header: 'Paid',
                cell: (r) => `${r.currency} ${r.amountPaid.toLocaleString(undefined, { minimumFractionDigits: 2 })}`,
              },
              {
                key: 'balance',
                header: 'Balance',
                cell: (r) => `${r.currency} ${r.balance.toLocaleString(undefined, { minimumFractionDigits: 2 })}`,
              },
              {
                key: 'status',
                header: 'Status',
                cell: (r) => <Badge tone={statusTone(r.status)}>{r.status}</Badge>,
              },
              {
                key: 'actions',
                header: 'Actions',
                className: 'min-w-[240px]',
                cell: (r) => (
                  <div className="flex flex-wrap gap-1">
                    <button
                      type="button"
                      className="button-ghost py-1 text-xs"
                      onClick={() => navigate(`/finance/invoices/${r.id}`)}
                    >
                      View
                    </button>
                    {r.status === 'Draft' && canManage ? (
                      <button
                        type="button"
                        className="button-ghost py-1 text-xs"
                        onClick={() => void handleSend(r)}
                      >
                        Send
                      </button>
                    ) : null}
                    <button
                      type="button"
                      className="button-ghost py-1 text-xs"
                      onClick={() => void handleDownloadPdf(r)}
                    >
                      PDF
                    </button>
                    {r.status !== 'Void' && canManage ? (
                      <button
                        type="button"
                        className="button-ghost py-1 text-xs text-danger"
                        onClick={() => void handleVoid(r)}
                      >
                        Void
                      </button>
                    ) : null}
                  </div>
                ),
              },
            ]}
          />
        ) : null}
      </SectionCard>
    </div>
  )
}
