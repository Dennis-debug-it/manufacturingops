import { useMemo, useState } from 'react'
import { Download, Search } from 'lucide-react'
import { PageHeader } from '../shared/components/PageHeader'
import { AsyncPanel } from '../shared/components/AsyncPanel'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { useDebounce } from '../shared/hooks/useDebounce'
import { downloadCsv } from '../shared/utils/csv'
import { formatDateTime } from '../shared/utils/dates'
import { usePlatformAuditLogs } from './api'
import type { PlatformAuditLog } from './types'

export function AuditPage() {
  const auditQuery = usePlatformAuditLogs()
  const { data } = auditQuery
  const [search, setSearch] = useState('')
  const [tenantFilter, setTenantFilter] = useState('All')
  const [severityFilter, setSeverityFilter] = useState('All')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const debouncedSearch = useDebounce(search, 250)

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((row) => {
      const matchesSearch =
        !query
        || row.action.toLowerCase().includes(query)
        || row.actorName.toLowerCase().includes(query)
        || row.entityType.toLowerCase().includes(query)
        || (row.description ?? '').toLowerCase().includes(query)
      const matchesTenant = tenantFilter === 'All' || (row.tenantName ?? 'Platform') === tenantFilter
      const matchesSeverity = severityFilter === 'All' || row.severity === severityFilter
      const when = row.createdAt.slice(0, 10)
      const matchesFrom = !from || when >= from
      const matchesTo = !to || when <= to
      return matchesSearch && matchesTenant && matchesSeverity && matchesFrom && matchesTo
    })
  }, [data, debouncedSearch, from, severityFilter, tenantFilter, to])

  const columns: DataTableColumn<PlatformAuditLog>[] = [
    { key: 'createdAt', header: 'Date/time', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actorName', header: 'Actor', cell: (row) => row.actorName },
    { key: 'tenantName', header: 'Tenant', cell: (row) => row.tenantName ?? 'Platform' },
    { key: 'action', header: 'Action', cell: (row) => row.action },
    { key: 'entityType', header: 'Entity', cell: (row) => `${row.entityType} · ${row.entityId}` },
    { key: 'details', header: 'Details', cell: (row) => row.description ?? '—' },
    { key: 'ipAddress', header: 'IP', cell: (row) => row.ipAddress ?? '—' },
    { key: 'severity', header: 'Severity', cell: (row) => row.severity },
  ]

  const tenantOptions = ['All', ...Array.from(new Set((data ?? []).map((row) => row.tenantName ?? 'Platform')))]

  return (
    <div className="space-y-5">
      <PageHeader
        title="Audit Logs"
        subtitle="Cross-tenant administrative activity with filters and export."
        primaryAction={
          rows.length > 0 ? (
            <button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => downloadCsv('platform-audit-logs.csv', rows.map((row) => ({ When: formatDateTime(row.createdAt), Actor: row.actorName, Tenant: row.tenantName ?? 'Platform', Action: row.action, Entity: `${row.entityType} ${row.entityId}`, Severity: row.severity })))}>
              <Download className="h-4 w-4" />
              Export CSV
            </button>
          ) : undefined
        }
      />

      <AsyncPanel query={auditQuery} loadingLabel="Loading audit logs" errorTitle="Audit unavailable" errorDescription="Audit records could not be loaded.">
        {() => (
          <>
            <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(220px,280px))]">
              <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2">
                <Search className="h-4 w-4 text-slate-400" />
                <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search actor, action, entity, or details" className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400" />
              </label>
              <select value={tenantFilter} onChange={(event) => setTenantFilter(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
                {tenantOptions.map((tenant) => <option key={tenant} value={tenant}>{tenant === 'All' ? 'All tenants' : tenant}</option>)}
              </select>
              <select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
                {['All', 'Info', 'Warning', 'Error'].map((severity) => <option key={severity} value={severity}>{severity === 'All' ? 'All severities' : severity}</option>)}
              </select>
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none" />
                <input type="date" value={to} onChange={(event) => setTo(event.target.value)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none" />
              </div>
            </div>

            <div className="mt-4">
              {rows.length === 0 ? <EmptyState title="No audit logs found" description="Adjust the filters or widen the date range." /> : <DataTable columns={columns} data={rows} emptyTitle="No audit logs found" emptyDescription="Adjust the filters or widen the date range." />}
            </div>
          </>
        )}
      </AsyncPanel>
    </div>
  )
}

