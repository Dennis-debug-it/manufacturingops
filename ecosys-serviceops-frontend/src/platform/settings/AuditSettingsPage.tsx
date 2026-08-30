import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { formatDateTime } from '../../shared/utils/dates'
import { downloadCsv } from '../../shared/utils/csv'
import { api } from '../../shared/api/apiClient'

interface PlatformAuditLogEntry {
  id: string
  tenantId: string | null
  actorUserId: string | null
  actorName: string
  action: string
  entityType: string
  entityId: string
  description: string | null
  ipAddress: string | null
  userAgent: string | null
  severity: string
  createdAt: string
  tenantName: string | null
}

export function AuditSettingsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['platform', 'audit-logs'],
    queryFn: () => api.get<PlatformAuditLogEntry[]>('/api/platform/audit-logs'),
    staleTime: 30_000,
  })
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((row) =>
      [row.action, row.entityType, row.actorName, row.tenantName ?? '', row.description ?? ''].some((value) => value.toLowerCase().includes(query)),
    )
  }, [data, debouncedSearch])

  const columns: DataTableColumn<PlatformAuditLogEntry>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actorName', header: 'Actor', cell: (row) => row.actorName },
    { key: 'action', header: 'Action', cell: (row) => <span className="font-medium text-slate-900">{row.action}</span> },
    { key: 'tenantName', header: 'Tenant', cell: (row) => row.tenantName ?? 'Platform' },
    { key: 'entityType', header: 'Entity', cell: (row) => `${row.entityType} · ${row.entityId}` },
    { key: 'severity', header: 'Severity', cell: (row) => <StatusBadge status={row.severity} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Platform Audit Log"
        subtitle="Cross-tenant record of administrative and system actions, most recent 500 entries."
        primaryAction={
          rows.length > 0 ? (
            <button
              type="button"
              onClick={() => downloadCsv('platform-audit-logs.csv', rows.map((row) => ({ When: formatDateTime(row.createdAt), Actor: row.actorName, Action: row.action, Tenant: row.tenantName ?? 'Platform', Entity: `${row.entityType} ${row.entityId}`, Severity: row.severity })))}
              className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50"
            >
              Export CSV
            </button>
          ) : undefined
        }
      />
      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by actor, action, tenant, or entity" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>
      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Audit logs unavailable' : 'No audit log entries'}
        emptyDescription={isError ? 'Platform audit logs could not be loaded.' : 'Administrative actions across the platform will appear here.'}
      />
    </div>
  )
}
