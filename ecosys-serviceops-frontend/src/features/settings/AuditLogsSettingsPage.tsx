import { useEffect, useMemo, useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { formatDateTime } from '../../shared/utils/dates'
import { api, apiClient } from '../../shared/api/apiClient'
import type { PagedResult } from '../../shared/types/pagination'

interface AuditLogEntry {
  id: string
  userId: string | null
  action: string
  entityName: string
  entityId: string
  details: string | null
  createdAt: string
}

const AUDIT_LOGS_URL = '/api/audit-logs'

export function AuditLogsSettingsPage() {
  const [search, setSearch] = useState('')
  const [action, setAction] = useState('')
  const [entityName, setEntityName] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [exporting, setExporting] = useState(false)

  const debouncedSearch = useDebounce(search, 300)
  const debouncedAction = useDebounce(action, 300)
  const debouncedEntity = useDebounce(entityName, 300)

  // The filters are applied server-side now, so every filter change invalidates the current page
  // number: page 3 of the old result set is not page 3 of the new one.
  useEffect(() => {
    setPage(1)
  }, [debouncedSearch, debouncedAction, debouncedEntity, from, to])

  const filters = useMemo(
    () => ({
      search: debouncedSearch.trim() || undefined,
      action: debouncedAction.trim() || undefined,
      entityName: debouncedEntity.trim() || undefined,
      from: from || undefined,
      to: to || undefined,
    }),
    [debouncedSearch, debouncedAction, debouncedEntity, from, to],
  )

  const { data, isLoading, isError } = useQuery({
    queryKey: ['settings', 'audit-logs', filters, page, pageSize],
    queryFn: () => api.get<PagedResult<AuditLogEntry>>(AUDIT_LOGS_URL, { ...filters, page, pageSize }),
    staleTime: 30_000,
    placeholderData: keepPreviousData,
  })

  const rows = useMemo(() => data?.items ?? [], [data])
  const total = data?.totalCount ?? 0

  /**
   * The export runs on the server over the whole filtered set. Exporting the loaded page instead
   * would have quietly redefined "Export CSV" the moment the list started paginating — a 50-row
   * file that looks like a complete audit trail. The API refuses (400) rather than truncating when
   * the filtered set is too large to export, and the interceptor surfaces that message.
   */
  const exportCsv = async () => {
    setExporting(true)
    try {
      const response = await apiClient.get<string>(`${AUDIT_LOGS_URL}/export`, { params: filters })
      const blob = new Blob([response.data], { type: 'text/csv' })
      const objectUrl = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = objectUrl
      link.download = 'audit-logs.csv'
      link.click()
      URL.revokeObjectURL(objectUrl)
    } catch {
      // The axios interceptor already surfaced the error toast.
    } finally {
      setExporting(false)
    }
  }

  const columns: DataTableColumn<AuditLogEntry>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'action', header: 'Action', cell: (row) => <span className="font-medium text-slate-900">{row.action}</span> },
    { key: 'entityName', header: 'Entity', cell: (row) => row.entityName },
    { key: 'entityId', header: 'Entity ID', cell: (row) => <span className="font-mono text-xs">{row.entityId}</span> },
    { key: 'details', header: 'Details', cell: (row) => row.details ?? '—' },
  ]

  const inputClass =
    'h-11 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-900 outline-none placeholder:text-slate-400 focus:border-[var(--app-primary)]'

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Logs"
        subtitle="Tenant-wide record of settings, security, and administrative changes."
        primaryAction={
          total > 0 ? (
            <button
              type="button"
              onClick={exportCsv}
              disabled={exporting}
              title="Exports every entry matching the current filters, not just the page shown."
              className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {exporting ? 'Exporting…' : 'Export CSV (all filtered)'}
            </button>
          ) : undefined
        }
      />
      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search by action, entity, or details"
            className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
          />
        </label>
        <div className="mt-3 grid gap-3 border-t border-slate-100 pt-3 sm:grid-cols-2 lg:grid-cols-4">
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">Action</span>
            <input value={action} onChange={(event) => setAction(event.target.value)} placeholder="e.g. Login" className={inputClass} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">Entity</span>
            <input value={entityName} onChange={(event) => setEntityName(event.target.value)} placeholder="e.g. WorkOrder" className={inputClass} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">From</span>
            <input type="date" value={from} max={to || undefined} onChange={(event) => setFrom(event.target.value)} className={inputClass} />
          </label>
          <label className="space-y-1">
            <span className="text-xs font-medium text-slate-500">To</span>
            <input type="date" value={to} min={from || undefined} onChange={(event) => setTo(event.target.value)} className={inputClass} />
          </label>
        </div>
      </div>
      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Audit logs unavailable' : 'No audit log entries'}
        emptyDescription={isError ? 'Audit logs could not be loaded.' : 'Administrative actions on this tenant will appear here.'}
        pagination={{
          page,
          pageSize,
          total,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size)
            setPage(1)
          },
        }}
      />
    </div>
  )
}
