import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Download, FileText, Send, Eye, Archive } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { StatusBadge } from '../../../shared/components/StatusBadge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import {
  downloadReportPdf,
  downloadReportsZip,
  useApproveQueuedReport,
  useGenerateQueuedReport,
  useGenerateReportBatch,
  useReportQueue,
  useSendQueuedReport,
  useSendReportBatch,
  type ReportQueueRow,
  type ReportStatus,
} from '../reportQueueApi'

const statusFilters: Array<{ key: ReportStatus | 'All'; label: string }> = [
  { key: 'All', label: 'All' },
  { key: 'AwaitingGeneration', label: 'Awaiting Generation' },
  { key: 'Draft', label: 'Draft' },
  { key: 'UnderReview', label: 'Under Review' },
  { key: 'Approved', label: 'Approved' },
  { key: 'Sent', label: 'Sent' },
  { key: 'Acknowledged', label: 'Acknowledged' },
]

const statusLabels: Record<ReportStatus, string> = {
  AwaitingGeneration: 'Awaiting Generation',
  Draft: 'Draft',
  UnderReview: 'Under Review',
  Approved: 'Approved',
  Sent: 'Sent',
  Acknowledged: 'Acknowledged',
}

export function ReportQueueTab() {
  const canManage = usePermission('work-orders.report.generate')
  const [status, setStatus] = useState<ReportStatus | 'All'>('All')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<Set<string>>(new Set())

  const { data, isLoading } = useReportQueue(status, page)
  const generate = useGenerateQueuedReport()
  const approve = useApproveQueuedReport()
  const send = useSendQueuedReport()
  const generateBatch = useGenerateReportBatch()
  const sendBatch = useSendReportBatch()
  const [zipping, setZipping] = useState(false)

  const rows = useMemo(() => data?.items ?? [], [data])
  const counts = data?.counts

  const toggleRow = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const allVisibleSelected = rows.length > 0 && rows.every((r) => selected.has(r.workOrderId))
  const toggleAll = () =>
    setSelected((prev) => {
      if (allVisibleSelected) {
        const next = new Set(prev)
        rows.forEach((r) => next.delete(r.workOrderId))
        return next
      }
      return new Set([...prev, ...rows.map((r) => r.workOrderId)])
    })

  const selectedIds = [...selected]
  const selectedRows = rows.filter((r) => selected.has(r.workOrderId))

  const runGenerate = async (id: string) => { await generate.mutateAsync(id); toast.success('Report generated') }
  const runApprove = async (id: string) => { await approve.mutateAsync(id); toast.success('Report approved') }
  const runSend = async (id: string) => { await send.mutateAsync(id); toast.success('Report sent') }
  const runDownload = async (row: ReportQueueRow) => {
    try { await downloadReportPdf(row.workOrderId, row.woNo) } catch { toast.error('Could not download the PDF') }
  }

  const batchGenerate = async () => {
    const eligible = selectedRows.filter((r) => r.reportStatus === 'AwaitingGeneration').map((r) => r.workOrderId)
    if (eligible.length === 0) return toast.error('Select work orders awaiting generation')
    const result = await generateBatch.mutateAsync(eligible)
    toast.success(`Generated ${result.processed} report(s)`)
    setSelected(new Set())
  }

  const batchSend = async () => {
    const eligible = selectedRows.filter((r) => r.reportStatus === 'Approved').map((r) => r.workOrderId)
    if (eligible.length === 0) return toast.error('Select approved reports to send')
    const result = await sendBatch.mutateAsync(eligible)
    toast.success(`Sent ${result.processed} report(s)`)
    setSelected(new Set())
  }

  const batchZip = async () => {
    if (selectedIds.length === 0) return toast.error('Select work orders to download')
    setZipping(true)
    try { await downloadReportsZip(selectedIds) } catch { toast.error('Could not build the ZIP') } finally { setZipping(false) }
  }

  const statCards = [
    { label: 'Awaiting Generation', value: counts?.awaitingGeneration ?? 0, filter: 'AwaitingGeneration' as const, tone: 'text-slate-900' },
    { label: 'Under Review', value: counts?.underReview ?? 0, filter: 'UnderReview' as const, tone: 'text-amber-600' },
    { label: 'Approved Not Sent', value: counts?.approvedNotSent ?? 0, filter: 'Approved' as const, tone: 'text-blue-600' },
    { label: 'Sent Not Acknowledged', value: counts?.sentNotAcknowledged ?? 0, filter: 'Sent' as const, tone: 'text-emerald-600' },
  ]

  const totalPages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1

  return (
    <div className="space-y-4">
      {/* Stat strip */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {statCards.map((card) => (
          <button
            key={card.label}
            type="button"
            onClick={() => { setStatus(card.filter); setPage(1) }}
            className={cn(
              'rounded-2xl border bg-white p-4 text-left shadow-sm transition hover:border-slate-300',
              status === card.filter ? 'border-slate-900' : 'border-slate-200',
            )}
          >
            <div className="text-xs font-semibold uppercase tracking-[0.14em] text-slate-500">{card.label}</div>
            <div className={cn('mt-2 text-2xl font-bold', card.tone)}>{card.value}</div>
          </button>
        ))}
      </div>

      {/* Filters + batch actions */}
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-1.5">
          {statusFilters.map((f) => (
            <button
              key={f.key}
              type="button"
              onClick={() => { setStatus(f.key); setPage(1) }}
              className={cn(
                'rounded-full border px-3 py-1.5 text-xs font-medium transition',
                status === f.key ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
              )}
            >
              {f.label}
            </button>
          ))}
        </div>
        {canManage ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs text-slate-500">{selected.size} selected</span>
            <button type="button" disabled={selected.size === 0 || generateBatch.isPending} onClick={() => void batchGenerate()} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              <FileText className="h-3.5 w-3.5" />Generate Selected
            </button>
            <button type="button" disabled={selected.size === 0 || sendBatch.isPending} onClick={() => void batchSend()} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">
              <Send className="h-3.5 w-3.5" />Send Selected
            </button>
            <button type="button" disabled={selected.size === 0 || zipping} onClick={() => void batchZip()} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-slate-900 px-3 text-xs font-medium text-white hover:bg-slate-800 disabled:opacity-50">
              <Archive className="h-3.5 w-3.5" />{zipping ? 'Zipping…' : 'Download ZIP'}
            </button>
          </div>
        ) : null}
      </div>

      {/* Table */}
      {isLoading ? (
        <Spinner label="Loading report queue" />
      ) : rows.length === 0 ? (
        <EmptyState title="No reports in the queue" description="Completed work orders awaiting reports will appear here." />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-3 py-3">
                  <input type="checkbox" checked={allVisibleSelected} onChange={toggleAll} className="h-4 w-4 rounded border-slate-300" aria-label="Select all" />
                </th>
                {['SR No', 'WO No', 'Client', 'Site', 'Asset', 'Technician', 'Completed', 'Report Status', 'Days', 'Actions'].map((h) => (
                  <th key={h} className="whitespace-nowrap px-3 py-3 font-medium text-slate-600">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((row) => (
                <tr key={row.workOrderId} className={cn(selected.has(row.workOrderId) && 'bg-slate-50')}>
                  <td className="px-3 py-3">
                    <input type="checkbox" checked={selected.has(row.workOrderId)} onChange={() => toggleRow(row.workOrderId)} className="h-4 w-4 rounded border-slate-300" aria-label={`Select ${row.woNo}`} />
                  </td>
                  <td className="whitespace-nowrap px-3 py-3 font-mono text-xs text-slate-700">{row.srNo ?? '—'}</td>
                  <td className="whitespace-nowrap px-3 py-3"><Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.workOrderId}`}>{row.woNo}</Link></td>
                  <td className="px-3 py-3 text-slate-700">{row.client}</td>
                  <td className="px-3 py-3 text-slate-700">{row.site}</td>
                  <td className="px-3 py-3 text-slate-700">{row.asset}</td>
                  <td className="px-3 py-3 text-slate-700">{row.technician}</td>
                  <td className="whitespace-nowrap px-3 py-3 text-slate-700">{row.completedAt ? formatDate(row.completedAt) : '—'}</td>
                  <td className="px-3 py-3"><StatusBadge status={statusLabels[row.reportStatus]} /></td>
                  <td className="px-3 py-3">
                    <span className={cn('font-medium', (row.daysSinceCompletion ?? 0) > 7 ? 'text-rose-600' : 'text-slate-600')}>{row.daysSinceCompletion ?? '—'}</span>
                  </td>
                  <td className="px-3 py-3">
                    <div className="flex items-center gap-1">
                      {canManage && row.reportStatus === 'AwaitingGeneration' ? <ActionButton icon={FileText} label="Generate" onClick={() => void runGenerate(row.workOrderId)} /> : null}
                      {canManage && (row.reportStatus === 'Draft' || row.reportStatus === 'UnderReview') ? <ActionButton label="Approve" onClick={() => void runApprove(row.workOrderId)} /> : null}
                      {canManage && row.reportStatus === 'Approved' ? <ActionButton icon={Send} label="Send" onClick={() => void runSend(row.workOrderId)} /> : null}
                      <Link to={`/work-orders/${row.workOrderId}?tab=report`} className="inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 text-xs font-medium text-slate-600 hover:bg-slate-50">
                        <Eye className="h-3.5 w-3.5" />View
                      </Link>
                      {row.reportStatus !== 'AwaitingGeneration' ? <ActionButton icon={Download} label="PDF" onClick={() => void runDownload(row)} /> : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {totalPages > 1 ? (
        <div className="flex items-center justify-between text-sm text-slate-600">
          <span>Page {data?.page ?? 1} of {totalPages} · {data?.total ?? 0} total</span>
          <div className="flex gap-2">
            <button type="button" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))} className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">Previous</button>
            <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)} className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-50">Next</button>
          </div>
        </div>
      ) : null}
    </div>
  )
}

function ActionButton({ icon: Icon, label, onClick }: { icon?: typeof Send; label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="inline-flex h-8 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 text-xs font-medium text-slate-600 hover:bg-slate-50">
      {Icon ? <Icon className="h-3.5 w-3.5" /> : null}{label}
    </button>
  )
}
