import { Plus, Search, Upload, X } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, SearchToolbar, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { clientService } from '../../services/clientService'
import { slaService } from '../../services/slaService'
import type { ClientRecord, SlaDefinitionRecord, UpsertClientInput } from '../../types/api'
import { ClientBulkImportModal } from './BulkImportModal'

type ClientStatusFilter = 'active' | 'inactive' | 'all'
type ClientWorkflowForm = UpsertClientInput

export function ClientsPage() {
  const navigate = useNavigate()
  const { pushToast } = useToast()
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<ClientRecord | null>(null)
  const [form, setForm] = useState<ClientWorkflowForm>({
    clientName: '',
    clientType: '',
    email: '',
    phone: '',
    location: '',
    contactPerson: '',
    contactPhone: '',
    slaDefinitionId: '',
    activeStartDate: '',
    activeDurationValue: null,
    activeDurationUnit: 'months',
    notes: '',
  })
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<ClientStatusFilter>('active')
  const [bulkImportOpen, setBulkImportOpen] = useState(false)
  const [actionClientId, setActionClientId] = useState<string | null>(null)

  const { data: slaDefinitions } = useAsyncData<SlaDefinitionRecord[]>(
    (signal) => slaService.list(signal),
    [],
    [],
  )

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim())
    }, 300)
    return () => window.clearTimeout(handle)
  }, [searchInput])

  const { data, loading, error, reload } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, search: debouncedSearch || undefined, status: statusFilter }),
    [],
    [debouncedSearch, statusFilter],
  )

  const hasFilters = useMemo(() => Boolean(debouncedSearch) || statusFilter !== 'active', [debouncedSearch, statusFilter])
  const activeClientsCount = data.filter((client) => client.isActive).length
  const inactiveClientsCount = data.filter((client) => !client.isActive).length
  const expiringClientsCount = data.filter((client) => client.activeStatus === 'expiring').length

  function openClient(clientId: string) {
    navigate(`/clients/${clientId}`)
  }

  function openEditor(client?: ClientRecord) {
    setEditing(client ?? null)
    setForm({
      clientName: client?.clientName ?? '',
      clientType: client?.clientType ?? '',
      email: client?.email ?? '',
      phone: client?.phone ?? '',
      location: client?.location ?? '',
      contactPerson: client?.contactPerson ?? '',
      contactPhone: client?.contactPhone ?? '',
      slaDefinitionId: client?.slaDefinitionId ?? '',
      activeStartDate: client?.activeStartDate?.slice(0, 10) ?? '',
      activeDurationValue: client?.activeDurationValue ?? null,
      activeDurationUnit: client?.activeDurationUnit ?? 'months',
      notes: client?.notes ?? '',
    })
    setDrawerOpen(true)
  }

  async function saveClient() {
    try {
      if (editing) {
        await clientService.update(editing.id, form)
        pushToast({ title: 'Client updated', description: 'Client details were saved successfully.', tone: 'success' })
      } else {
        await clientService.create(form)
        pushToast({ title: 'Client created', description: 'The new client is ready to use.', tone: 'success' })
      }
      setDrawerOpen(false)
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save client.', tone: 'danger' })
    }
  }

  async function toggleClientStatus(client: ClientRecord) {
    if (!client.isActive) {
      try {
        setActionClientId(client.id)
        await clientService.activate(client.id)
        pushToast({ title: 'Client activated', description: 'Client is active again.', tone: 'success' })
        await reload()
      } catch (nextError) {
        pushToast({ title: 'Activation failed', description: nextError instanceof Error ? nextError.message : 'Unable to activate client.', tone: 'danger' })
      } finally {
        setActionClientId(null)
      }
      return
    }

    const confirmed = window.confirm('Deactivating this client will also deactivate all linked assets. Historical work orders will remain available. Continue?')
    if (!confirmed) return

    try {
      setActionClientId(client.id)
      await clientService.deactivate(client.id)
      pushToast({ title: 'Client deactivated', description: 'Linked assets were deactivated and historical records were preserved.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Deactivation failed', description: nextError instanceof Error ? nextError.message : 'Unable to deactivate client.', tone: 'danger' })
    } finally {
      setActionClientId(null)
    }
  }

  return (
    <PageScaffold
      eyebrow="Clients"
      title="Clients"
      description="Browse client records and open the detail page for sites, assets, contracts, and work orders."
      actions={(
        <div className="flex items-center gap-2">
          <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => setBulkImportOpen(true)}>
            <Upload className="h-4 w-4" />
            Import
          </button>
          <button type="button" className="button-primary w-full sm:w-auto" onClick={() => openEditor()}>
            <Plus className="h-4 w-4" />
            Add client
          </button>
        </div>
      )}
    >
      <div className="rounded-2xl border border-amber-400/40 bg-amber-500/10 px-4 py-3 text-sm font-semibold text-amber-950 dark:text-amber-200">
        DEBUG MARKER: Clients page is rendering this file.
      </div>

      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Active clients" value={activeClientsCount} meta="Ready for service operations" emphasis="accent" />
        <MetricCard label="Expiring soon" value={expiringClientsCount} meta="Within the next 30 days" emphasis="warning" />
        <MetricCard label="Inactive clients" value={inactiveClientsCount} meta={hasFilters ? 'Filtered register view' : 'Retained for historical records'} />
      </MetricGrid>

      <SectionCard title="Client register" description="Click any client to open its detail page.">
        <div className="space-y-3">
          <SearchToolbar
            searchSlot={(
              <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-2.5">
                <Search className="h-4 w-4 text-muted" />
                <input
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                  className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                  placeholder="Search by client, contact, email, phone, or location"
                />
              </label>
            )}
            filters={(
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as ClientStatusFilter)} className="field-input">
                <option value="active">Active</option>
                <option value="inactive">Inactive</option>
                <option value="all">All</option>
              </select>
            )}
            actions={(
              <button
                type="button"
                className="button-secondary"
                onClick={() => {
                  setSearchInput('')
                  setDebouncedSearch('')
                  setStatusFilter('active')
                }}
                disabled={!hasFilters}
              >
                <X className="h-4 w-4" />
                Clear
              </button>
            )}
          />

          {loading ? <LoadingState label="Loading clients" /> : null}
          {!loading && error ? <ErrorState title="Unable to load clients" description={error} /> : null}
          {!loading && !error ? (
            <DataTable
              rows={data}
              rowKey={(row) => row.id}
              pageSize={10}
              variant="open"
              onRowClick={(row) => openClient(row.id)}
              emptyTitle="No clients found"
              emptyDescription={hasFilters ? 'Try clearing search or changing the status filter.' : 'Create the first client record to link work orders and assets.'}
              mobileCard={(row) => (
                <div className="space-y-3 rounded-[24px] bg-subtle p-4 transition-colors">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <button
                        type="button"
                        className={`text-left font-semibold ${row.isActive ? 'text-app' : 'text-muted'}`}
                        onClick={() => openClient(row.id)}
                      >
                        {row.clientName}
                      </button>
                      <p className="mt-1 text-xs text-muted">{row.clientType || 'Client'}</p>
                    </div>
                    {renderClientStatusBadge(row)}
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Detail label="Contact" value={row.contactPerson || row.email || 'Not set'} />
                    <Detail label="Phone" value={row.contactPhone || row.phone || 'Not set'} />
                    <Detail label="Sites" value={String(row.siteCount)} />
                    <Detail label="Systems" value={String(row.systemCount)} />
                    <Detail label="Assets / Components" value={String(row.assetCount)} />
                    <Detail label="Open Work Orders" value={String(row.openWorkOrderCount)} />
                  </div>
                  <div onClick={(event) => event.stopPropagation()}>
                    <RowActionSelector
                      options={[
                        { value: 'open', label: 'Open detail' },
                        { value: 'edit', label: 'Edit' },
                        { value: row.isActive ? 'deactivate' : 'activate', label: row.isActive ? 'Deactivate' : 'Activate', disabled: actionClientId === row.id },
                      ]}
                      onApply={async (action) => {
                        if (action === 'open') openClient(row.id)
                        if (action === 'edit') openEditor(row)
                        if (action === 'activate' || action === 'deactivate') await toggleClientStatus(row)
                      }}
                      buttonLabel="Apply"
                    />
                  </div>
                </div>
              )}
              columns={[
                {
                  key: 'name',
                  header: 'Client',
                  cell: (row) => (
                    <button
                      type="button"
                      onClick={() => openClient(row.id)}
                      className={`text-left font-semibold hover:underline ${
                        row.isActive ? 'text-app' : 'text-muted'
                      }`}
                    >
                      {row.clientName}
                    </button>
                  ),
                },
                { key: 'status', header: 'Status', cell: (row) => renderClientStatusBadge(row) },
                { key: 'sites', header: 'Sites', cell: (row) => <span>{row.siteCount}</span> },
                { key: 'systems', header: 'Systems', cell: (row) => <span>{row.systemCount}</span> },
                { key: 'assets', header: 'Assets / Components', cell: (row) => <span>{row.assetCount}</span> },
                { key: 'openWorkOrders', header: 'Open Work Orders', cell: (row) => <span>{row.openWorkOrderCount}</span> },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[210px]',
                  cell: (row) => (
                    <div onClick={(event) => event.stopPropagation()}>
                      <RowActionSelector
                        options={[
                          { value: 'open', label: 'Open detail' },
                          { value: 'edit', label: 'Edit' },
                          { value: row.isActive ? 'deactivate' : 'activate', label: row.isActive ? 'Deactivate' : 'Activate', disabled: actionClientId === row.id },
                        ]}
                        onApply={async (action) => {
                          if (action === 'open') openClient(row.id)
                          if (action === 'edit') openEditor(row)
                          if (action === 'activate' || action === 'deactivate') await toggleClientStatus(row)
                        }}
                        buttonLabel="Apply"
                      />
                    </div>
                  ),
                },
              ]}
            />
          ) : null}
        </div>
      </SectionCard>

      <Drawer open={drawerOpen} title={editing ? 'Edit client' : 'Add client'} description="Keep client details up to date for operations and support." onClose={() => setDrawerOpen(false)}>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Client name"><input value={form.clientName} onChange={(event) => setForm((current) => ({ ...current, clientName: event.target.value }))} className="field-input" /></Field>
          <Field label="Client type"><input value={form.clientType || ''} onChange={(event) => setForm((current) => ({ ...current, clientType: event.target.value }))} className="field-input" /></Field>
          <Field label="Email"><input value={form.email || ''} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="field-input" /></Field>
          <Field label="Phone"><input value={form.phone || ''} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="field-input" /></Field>
          <Field label="Contact person"><input value={form.contactPerson || ''} onChange={(event) => setForm((current) => ({ ...current, contactPerson: event.target.value }))} className="field-input" /></Field>
          <Field label="Contact phone"><input value={form.contactPhone || ''} onChange={(event) => setForm((current) => ({ ...current, contactPhone: event.target.value }))} className="field-input" /></Field>
          <Field label="Location"><input value={form.location || ''} onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))} className="field-input" /></Field>
          <Field label="Response targets">
            <select value={form.slaDefinitionId || ''} onChange={(event) => setForm((current) => ({ ...current, slaDefinitionId: event.target.value || null }))} className="field-input">
              <option value="">No response target set</option>
              {slaDefinitions.filter((item) => item.isActive).map((definition) => (
                <option key={definition.id} value={definition.id}>{definition.planName}</option>
              ))}
            </select>
          </Field>
          <Field label="Active start date"><input type="date" value={form.activeStartDate || ''} onChange={(event) => setForm((current) => ({ ...current, activeStartDate: event.target.value }))} className="field-input" /></Field>
          <Field label="Duration value"><input type="number" min={1} value={form.activeDurationValue ?? ''} onChange={(event) => setForm((current) => ({ ...current, activeDurationValue: event.target.value ? Number(event.target.value) : null }))} className="field-input" /></Field>
          <Field label="Duration unit">
            <select value={form.activeDurationUnit || 'months'} onChange={(event) => setForm((current) => ({ ...current, activeDurationUnit: event.target.value }))} className="field-input">
              <option value="days">days</option>
              <option value="months">months</option>
              <option value="years">years</option>
            </select>
          </Field>
          <Field label="Calculated end date"><input value={calculateActiveEndDate(form.activeStartDate, form.activeDurationValue, form.activeDurationUnit)} className="field-input" readOnly /></Field>
        </div>
        <Field label="Notes"><textarea value={form.notes || ''} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} className="field-input mt-4 min-h-[120px]" /></Field>
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveClient()}>Save client</button>
        </StickyActionFooter>
      </Drawer>

      <ClientBulkImportModal
        open={bulkImportOpen}
        onClose={() => setBulkImportOpen(false)}
        onSuccess={() => { setBulkImportOpen(false); void reload() }}
      />
    </PageScaffold>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl bg-[var(--app-subtle)] px-3 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 text-sm text-app">{value}</p>
    </div>
  )
}

function calculateActiveEndDate(startDate?: string | null, durationValue?: number | null, durationUnit?: string | null) {
  if (!startDate || !durationValue || durationValue <= 0) return 'Calculated after save'

  const base = new Date(`${startDate}T00:00:00.000Z`)
  if (Number.isNaN(base.getTime())) return 'Invalid start date'

  const unit = (durationUnit || 'months').toLowerCase()
  if (unit === 'days') base.setUTCDate(base.getUTCDate() + durationValue)
  else if (unit === 'years') base.setUTCFullYear(base.getUTCFullYear() + durationValue)
  else base.setUTCMonth(base.getUTCMonth() + durationValue)

  return base.toISOString().slice(0, 10)
}

function renderClientStatusBadge(client: ClientRecord) {
  const normalizedStatus = (client.activeStatus || (client.isActive ? 'active' : 'inactive')).toLowerCase()
  if (normalizedStatus === 'expired') return <Badge tone="danger">Expired</Badge>
  if (normalizedStatus === 'expiring') return <Badge tone="warning">Expiring Soon</Badge>
  if (normalizedStatus === 'inactive' || normalizedStatus === 'suspended') return <Badge tone="neutral">{normalizedStatus === 'suspended' ? 'Suspended' : 'Inactive'}</Badge>
  return <Badge tone="success">Active</Badge>
}
