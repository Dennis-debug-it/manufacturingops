import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { Search } from 'lucide-react'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { useClients } from './api'
import { TriTabNav } from './components/TriTabNav'
import type { Site } from './types'

export function SitesPage() {
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const { data: clients } = useClients()

  const { data, isLoading, isError } = useQuery({
    queryKey: ['sites', 'search', debouncedSearch],
    queryFn: () => api.get<Site[]>(endpoints.sites.search, { q: debouncedSearch || undefined }),
    staleTime: 30_000,
  })

  const clientNameById = useMemo(() => new Map((clients ?? []).map((client) => [client.id, client.clientName])), [clients])
  const rows = useMemo(() => data ?? [], [data])

  const columns: DataTableColumn<Site>[] = [
    { key: 'siteName', header: 'Site', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/sites/${row.id}?clientId=${row.clientId}`}>{row.siteName}</Link> },
    { key: 'siteCode', header: 'Code', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.siteCode}</span> },
    { key: 'clientName', header: 'Client', cell: (row) => clientNameById.get(row.clientId) ?? '—' },
    { key: 'siteType', header: 'Type', cell: (row) => row.siteType ?? '—' },
    { key: 'townCity', header: 'Town/City', cell: (row) => row.townCity ?? '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Sites" subtitle="Sites across all clients with search and filters." />
      <TriTabNav />

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search sites by name or code (top 25 active matches)" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Sites unavailable' : 'No sites found'}
        emptyDescription={isError ? 'The site list could not be loaded.' : 'Create a site from a client’s detail page to get started.'}
      />
    </div>
  )
}
