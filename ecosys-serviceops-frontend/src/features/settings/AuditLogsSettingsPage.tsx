import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { formatDateTime } from '../../shared/utils/dates'
import { downloadCsv } from '../../shared/utils/csv'
import { api } from '../../shared/api/apiClient'

interface AuditLogEntry {
  id: string
  userId: string | null
  action: string
  entityName: string
  entityId: string
  details: string | null
  createdAt: string
}

export function AuditLogsSettingsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['settings', 'audit-logs'],
    queryFn: () => api.get<AuditLogEntry[]>('/api/audit-logs'),
    staleTime: 30_000,
  })
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((row) =>
      [row.action, row.entityName, row.entityId, row.details ?? ''].some((value) => value.toLowerCase().includes(query)),
    )
  }, [data, debouncedSearch])

  const columns: DataTableColumn<AuditLogEntry>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'action', header: 'Action', cell: (row) => <span className="font-medium text-slate-900">{row.action}</span> },
    { key: 'entityName', header: 'Entity', cell: (row) => row.entityName },
    { key: 'entityId', header: 'Entity ID', cell: (row) => <span className="font-mono text-xs">{row.entityId}</span> },
    { key: 'details', header: 'Details', cell: (row) => row.details ?? '—' },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Audit Logs"
        subtitle="Tenant-wide record of settings, security, and administrative changes."
        primaryAction={
          rows.length > 0 ? (
            <button
              type="button"
              onClick={() => downloadCsv('audit-logs.csv', rows.map((row) => ({ When: formatDateTime(row.createdAt), Action: row.action, Entity: row.entityName, EntityId: row.entityId, Details: row.details ?? '' })))}
              className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Export CSV
            </button>
          ) : undefined
        }
      />
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by action, entity, or details" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>
      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Audit logs unavailable' : 'No audit log entries'}
        emptyDescription={isError ? 'Audit logs could not be loaded.' : 'Administrative actions on this tenant will appear here.'}
      />
    </div>
  )
}
