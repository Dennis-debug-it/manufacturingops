import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Search, Upload, Pencil, Power } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useClients, useToggleClientActive } from './api'
import { TriTabNav } from './components/TriTabNav'
import { ClientFormDrawer } from './components/ClientFormDrawer'
import { ClientBulkImportDrawer } from './components/ClientBulkImportDrawer'
import type { Client } from './types'

export function ClientsPage() {
  const canManage = usePermission('clients.edit')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const [editClient, setEditClient] = useState<Client | null>(null)
  const [toggleClient, setToggleClient] = useState<Client | null>(null)
  const { data, isLoading, isError } = useClients({ search: debouncedSearch, status: 'all' })
  const toggleActive = useToggleClientActive()

  const rows = useMemo(() => data ?? [], [data])

  const confirmToggle = async () => {
    if (!toggleClient) {
      return
    }
    const target = toggleClient
    setToggleClient(null)
    try {
      await toggleActive.mutateAsync({ id: target.id, active: !target.isActive })
      toast.success(target.isActive ? 'Client deactivated' : 'Client activated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const columns: DataTableColumn<Client>[] = [
    { key: 'clientName', header: 'Client', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/clients/${row.id}`}>{row.clientName}</Link> },
    { key: 'clientType', header: 'Type', cell: (row) => row.clientType ?? '—' },
    { key: 'contactPerson', header: 'Contact', cell: (row) => row.contactPerson ?? '—' },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? row.contactPhone ?? '—' },
    { key: 'siteCount', header: 'Sites', cell: (row) => row.siteCount },
    { key: 'assetCount', header: 'Assets', cell: (row) => row.assetCount },
    { key: 'openWorkOrderCount', header: 'Open WOs', cell: (row) => row.openWorkOrderCount },
    { key: 'activeStatus', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? row.activeStatus : 'Inactive'} /> },
    ...(canManage ? [{
      key: 'actions',
      header: '',
      cell: (row: Client) => (
        <div className="flex items-center justify-end gap-2">
          <button type="button" onClick={() => setEditClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
            <Pencil className="h-3.5 w-3.5" />Edit
          </button>
          <button type="button" onClick={() => setToggleClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
            <Power className="h-3.5 w-3.5" />{row.isActive ? 'Deactivate' : 'Activate'}
          </button>
        </div>
      ),
    } as DataTableColumn<Client>] : []),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Clients"
        subtitle="Client registry, filters, and hierarchy entry point."
        primaryAction={
          canManage ? (
            <div className="flex items-center gap-2">
              <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Upload className="h-4 w-4" />Import
              </button>
              <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
                <Plus className="h-4 w-4" />New Client
              </button>
            </div>
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
        emptyDescription={isError ? 'The client list could not be loaded.' : 'Create your first client or bulk-import from CSV to get started.'}
        virtualise={rows.length > 100}
      />

      <ClientFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <ClientFormDrawer open={editClient !== null} client={editClient} onClose={() => setEditClient(null)} />
      <ClientBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} />

      <ConfirmDialog
        open={toggleClient !== null}
        title={toggleClient?.isActive ? 'Deactivate client' : 'Activate client'}
        message={toggleClient?.isActive
          ? `Deactivate "${toggleClient?.clientName}"? They will be hidden from active lists but their history is retained.`
          : `Reactivate "${toggleClient?.clientName}"?`}
        confirmLabel={toggleClient?.isActive ? 'Deactivate' : 'Activate'}
        danger={toggleClient?.isActive ?? false}
        onCancel={() => setToggleClient(null)}
        onConfirm={() => void confirmToggle()}
      />
    </div>
  )
}
