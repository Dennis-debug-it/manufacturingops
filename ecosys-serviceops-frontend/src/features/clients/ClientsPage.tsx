import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useClients } from './api'
import { TriTabNav } from './components/TriTabNav'
import { ClientFormDrawer } from './components/ClientFormDrawer'
import type { Client } from './types'

export function ClientsPage() {
  const canCreate = usePermission('clients.edit')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const { data, isLoading, isError } = useClients({ search: debouncedSearch, status: 'all' })

  const rows = useMemo(() => data ?? [], [data])

  const columns: DataTableColumn<Client>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/clients/${row.id}`}>{row.clientName}</Link> },
    { key: 'clientType', header: 'Type', cell: (row) => row.clientType ?? '—' },
    { key: 'contactPerson', header: 'Contact', cell: (row) => row.contactPerson ?? '—' },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? row.contactPhone ?? '—' },
    { key: 'siteCount', header: 'Sites', cell: (row) => row.siteCount },
    { key: 'assetCount', header: 'Assets', cell: (row) => row.assetCount },
    { key: 'openWorkOrderCount', header: 'Open WOs', cell: (row) => row.openWorkOrderCount },
    { key: 'activeStatus', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? row.activeStatus : 'Inactive'} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Clients"
        subtitle="Client registry, filters, and hierarchy entry point."
        primaryAction={
          canCreate ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />New Client
            </button>
          ) : undefined
        }
      />

      <TriTabNav />

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search clients by name, contact, or location" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Clients unavailable' : 'No clients yet'}
        emptyDescription={isError ? 'The client list could not be loaded.' : 'Create your first client to get started.'}
        virtualise={rows.length > 100}
      />

      <ClientFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
