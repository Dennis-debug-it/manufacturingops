import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { formatDateTime } from '../../shared/utils/dates'
import { usePermission } from '../../shared/hooks/usePermission'

interface DeliveryLog {
  id: string
  eventKey: string
  templateKey: string
  recipientEmail: string
  subject: string
  status: string
  attemptCount: number | null
  lastAttemptAt: string | null
  nextAttemptAt: string | null
  errorCategory: string | null
  errorMessage: string | null
  createdAt: string
  sentAt: string | null
  documentType: string | null
  documentId: string | null
  triggeredAutomatically: boolean
}

export function EmailDeliveryLogsPage() {
  const canManage = usePermission('settings.manage')
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('')
  const [documentType, setDocumentType] = useState('')
  const [search, setSearch] = useState('')
  const [dateFrom, setDateFrom] = useState('')
  const [dateTo, setDateTo] = useState('')
  const query = useQuery({
    queryKey: ['settings', 'email-delivery-logs', status, documentType, dateFrom, dateTo],
    queryFn: () => api.get<DeliveryLog[]>(`${endpoints.tenantEmailDeliveryLogs.list}?${new URLSearchParams({ ...(status ? { status } : {}), ...(documentType ? { documentType } : {}), ...(search.trim() ? { recipientEmail: search.trim() } : {}), ...(dateFrom ? { dateFrom } : {}), ...(dateTo ? { dateTo: `${dateTo}T23:59:59` } : {}) }).toString()}`),
    staleTime: 15_000,
  })
  const resend = useMutation({
    mutationFn: (id: string) => api.post<DeliveryLog>(endpoints.tenantEmailDeliveryLogs.resend(id)),
    onSuccess: () => {
      toast.success('Delivery retry queued')
      void queryClient.invalidateQueries({ queryKey: ['settings', 'email-delivery-logs'] })
    },
  })
  const rows = useMemo(() => {
    const value = search.trim().toLowerCase()
    return (query.data ?? []).filter((row) => !value || [row.recipientEmail, row.subject, row.eventKey, row.templateKey, row.errorMessage ?? ''].some((item) => item.toLowerCase().includes(value)))
  }, [query.data, search])
  const columns: DataTableColumn<DeliveryLog>[] = [
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'type', header: 'Type', cell: (row) => row.documentType ?? row.templateKey },
    { key: 'recipient', header: 'Recipient', cell: (row) => row.recipientEmail },
    { key: 'status', header: 'Status', cell: (row) => <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700">{row.status}</span> },
    { key: 'attempts', header: 'Attempts', cell: (row) => row.attemptCount ?? 0 },
    { key: 'sentAt', header: 'Sent', cell: (row) => row.sentAt ? formatDateTime(row.sentAt) : '—' },
    { key: 'failure', header: 'Failure', cell: (row) => row.errorMessage ?? row.errorCategory ?? '—' },
    { key: 'actions', header: 'Actions', cell: (row) => canManage && (row.status === 'Failed' || row.status === 'Cancelled') ? <button type="button" onClick={() => resend.mutate(row.id)} disabled={resend.isPending} className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 hover:bg-slate-50">Resend</button> : null },
  ]
  return (
    <div className="space-y-5">
      <PageHeader title="Email Delivery Logs" subtitle="Track queued, sent, failed, and retried workspace email deliveries." />
      <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm md:grid-cols-[minmax(0,1fr)_180px_180px_160px_160px]">
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2"><Search className="h-4 w-4 text-slate-400" /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search recipient, type, or failure" className="w-full border-0 bg-transparent p-0 text-sm outline-none" /></label>
        <select value={status} onChange={(event) => setStatus(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none"><option value="">All statuses</option><option>Pending</option><option>Sending</option><option>Sent</option><option>Retrying</option><option>Failed</option><option>Cancelled</option></select>
        <select value={documentType} onChange={(event) => setDocumentType(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none"><option value="">All types</option><option>Invoice</option><option>Receipt</option><option>Statement</option><option>User</option></select>
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2 text-xs text-slate-500">From<input aria-label="From date" type="date" value={dateFrom} onChange={(event) => setDateFrom(event.target.value)} className="min-w-0 bg-transparent text-sm text-slate-700 outline-none" /></label>
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2 text-xs text-slate-500">To<input aria-label="To date" type="date" value={dateTo} onChange={(event) => setDateTo(event.target.value)} className="min-w-0 bg-transparent text-sm text-slate-700 outline-none" /></label>
      </div>
      <DataTable columns={columns} data={rows} loading={query.isLoading} emptyTitle={query.isError ? 'Delivery logs unavailable' : 'No delivery logs'} emptyDescription={query.isError ? 'Email delivery history could not be loaded.' : 'Financial and notification deliveries will appear here.'} />
    </div>
  )
}
