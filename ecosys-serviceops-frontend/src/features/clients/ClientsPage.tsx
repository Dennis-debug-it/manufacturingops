import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Plus, Search, Upload, Pencil, Power, Trash2, ArchiveRestore } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useClientsPaged, useToggleClientActive, useValidateClientDeletion, useDeleteClientPermanent } from './api'
import { TriTabNav } from './components/TriTabNav'
import { ClientFormDrawer } from './components/ClientFormDrawer'
import { ClientBulkImportDrawer } from './components/ClientBulkImportDrawer'
import { ClientArchiveDialog } from './components/ClientArchiveDialog'
import type { Client } from './types'

export function ClientsPage() {
  const canManage = usePermission('clients.edit')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const [editClient, setEditClient] = useState<Client | null>(null)
  const [statusFilter, setStatusFilter] = useState<'active' | 'inactive' | 'all' | 'prospects'>('active')
  const [toggleClient, setToggleClient] = useState<Client | null>(null)
  const [archiveClient, setArchiveClient] = useState<Client | null>(null)
  const [deleteClient, setDeleteClient] = useState<Client | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  // Reset to the first page whenever the search term changes.
  useEffect(() => setPage(1), [debouncedSearch])
  const { data, isLoading, isError } = useClientsPaged(statusFilter === 'prospects' ? { search: debouncedSearch, status: 'all', kind: 'prospects' } : { search: debouncedSearch, status: statusFilter }, page, pageSize)
  const toggleActive = useToggleClientActive()
  const { data: deletionCheck } = useValidateClientDeletion(deleteClient?.id ?? '')
  const deletePermanent = useDeleteClientPermanent(deleteClient?.id ?? '')

  const rows = useMemo(() => data?.items ?? [], [data])

  const confirmRestore = async () => {
    if (!toggleClient) {
      return
    }
    const target = toggleClient
    setToggleClient(null)
    try {
      await toggleActive.mutateAsync({ id: target.id, active: true })
      toast.success('Client restored')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const confirmArchive = async () => {
    if (!archiveClient) {
      return
    }
    const target = archiveClient
    setArchiveClient(null)
    try {
      await toggleActive.mutateAsync({ id: target.id, active: false })
      toast.success('Client archived')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const confirmDeletePermanent = async () => {
    if (!deleteClient) {
      return
    }
    setDeleteClient(null)
    try {
      await deletePermanent.mutateAsync()
      toast.success('Client permanently deleted')
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
    { key: 'activeStatus', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? row.activeStatus : 'Archived'} /> },
    ...(canManage ? [{
      key: 'actions',
      header: '',
      cell: (row: Client) => (
        <div className="flex items-center justify-end gap-2">
          {row.isActive ? (
            <>
              <button type="button" onClick={() => setEditClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
                <Pencil className="h-3.5 w-3.5" />Edit
              </button>
              <button type="button" onClick={() => setArchiveClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
                <Power className="h-3.5 w-3.5" />Archive
              </button>
              <button type="button" onClick={() => setDeleteClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-red-50 hover:text-red-700">
                <Trash2 className="h-3.5 w-3.5" />Delete
              </button>
            </>
          ) : (
            <>
              <button type="button" onClick={() => setToggleClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-slate-50">
                <ArchiveRestore className="h-3.5 w-3.5" />Restore
              </button>
              <button type="button" onClick={() => setDeleteClient(row)} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 transition hover:bg-red-50 hover:text-red-700">
                <Trash2 className="h-3.5 w-3.5" />Delete
              </button>
            </>
          )}
        </div>
      ),
    } as DataTableColumn<Client>] : []),
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Clients"
        subtitle="Client registry, filters, and hierarchy entry point."
        primaryAction={
          canManage ? (
            <div className="flex items-center gap-2">
              <button type="button" onClick={() => setShowImport(true)} className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
                <Upload className="h-4 w-4" />Import
              </button>
              <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                <Plus className="h-4 w-4" />New Client
              </button>
            </div>
          ) : undefined
        }
      />

      <TriTabNav />

      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => { setStatusFilter('active'); setPage(1) }}
          className={`inline-flex h-9 items-center gap-2 rounded-lg px-3 text-xs font-medium transition ${
            statusFilter === 'active'
              ? 'bg-[var(--app-primary)] text-white'
              : 'border border-[var(--app-border)] bg-white text-slate-700 hover:bg-[var(--app-primary-soft)]'
          }`}
        >
          Active
        </button>
        <button
          type="button"
          onClick={() => { setStatusFilter('inactive'); setPage(1) }}
          className={`inline-flex h-9 items-center gap-2 rounded-lg px-3 text-xs font-medium transition ${
            statusFilter === 'inactive'
              ? 'bg-[var(--app-primary)] text-white'
              : 'border border-[var(--app-border)] bg-white text-slate-700 hover:bg-[var(--app-primary-soft)]'
          }`}
        >
          Archived
        </button>
        <button
          type="button"
          onClick={() => { setStatusFilter('all'); setPage(1) }}
          className={`inline-flex h-9 items-center gap-2 rounded-lg px-3 text-xs font-medium transition ${
            statusFilter === 'all'
              ? 'bg-[var(--app-primary)] text-white'
              : 'border border-[var(--app-border)] bg-white text-slate-700 hover:bg-[var(--app-primary-soft)]'
          }`}
        >
          All
        </button>
        <button
          type="button"
          onClick={() => { setStatusFilter('prospects'); setPage(1) }}
          title="Companies sales is talking to that are not customers yet"
          className={`inline-flex h-9 items-center gap-2 rounded-lg px-3 text-xs font-medium transition ${
            statusFilter === 'prospects'
              ? 'bg-[var(--app-primary)] text-white'
              : 'border border-[var(--app-border)] bg-white text-slate-700 hover:bg-[var(--app-primary-soft)]'
          }`}
        >
          Prospects
        </button>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
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
        emptyActionLabel={!isError && canManage ? 'New Client' : undefined}
        onEmptyAction={!isError && canManage ? () => setShowForm(true) : undefined}
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size)
            setPage(1)
          },
        }}
      />

      <ClientFormDrawer open={showForm} onClose={() => setShowForm(false)} />
      <ClientFormDrawer open={editClient !== null} client={editClient} onClose={() => setEditClient(null)} />
      <ClientBulkImportDrawer open={showImport} onClose={() => setShowImport(false)} />

      <ClientArchiveDialog
        open={archiveClient !== null}
        client={archiveClient}
        onCancel={() => setArchiveClient(null)}
        onConfirm={() => void confirmArchive()}
      />

      <ConfirmDialog
        open={toggleClient !== null}
        title="Restore client"
        message={`Restore "${toggleClient?.clientName}" to active status? Linked sites and assets stay archived and must be restored individually.`}
        confirmLabel="Restore"
        danger={false}
        onCancel={() => setToggleClient(null)}
        onConfirm={() => void confirmRestore()}
      />

      <ConfirmDialog
        open={deleteClient !== null && deletionCheck !== undefined}
        title="Delete client permanently"
        message={
          deletionCheck?.canDelete
            ? `Permanently delete "${deleteClient?.clientName}"? This cannot be undone${deletionCheck.siteCount > 0 ? `, and will also delete ${deletionCheck.siteCount} linked site(s)` : ''}.`
            : deletionCheck?.blockingReason ?? 'This client cannot be deleted.'
        }
        confirmLabel="Delete permanently"
        {...(deletionCheck?.canDelete && deleteClient?.clientName ? { confirmText: deleteClient.clientName } : {})}
        danger={true}
        confirmDisabled={!deletionCheck?.canDelete || deletePermanent.isPending}
        onCancel={() => setDeleteClient(null)}
        onConfirm={() => void confirmDeletePermanent()}
      />
    </div>
  )
}
