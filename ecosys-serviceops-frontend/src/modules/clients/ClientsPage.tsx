import { Plus, Search, Upload, X } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { AttachmentPanel } from '../../components/ui/AttachmentPanel'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, SearchToolbar, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { attachmentService, ATTACHMENT_ENTITY_TYPES } from '../../services/attachmentService'
import { clientService } from '../../services/clientService'
import { slaService } from '../../services/slaService'
import type { AttachmentRecord, ClientRecord, SlaDefinitionRecord, UpsertClientInput } from '../../types/api'
import { ClientBulkImportModal } from './BulkImportModal'
import { ClientWorkspace } from './ClientDetailPage'

type ClientStatusFilter = 'active' | 'inactive' | 'all'
type ClientWorkflowForm = UpsertClientInput
type WorkspaceTab = 'overview' | 'sites-assets' | 'work-orders' | 'documents'

const WORKSPACE_TABS = new Set<WorkspaceTab>(['overview', 'sites-assets', 'work-orders', 'documents'])

export function ClientsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
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
  const [docsClient, setDocsClient] = useState<ClientRecord | null>(null)
  const [clientAttachments, setClientAttachments] = useState<AttachmentRecord[]>([])

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
  const selectedClientId = searchParams.get('clientId') ?? ''
  const selectedClient = data.find((client) => client.id === selectedClientId) ?? null
  const selectedTab = resolveWorkspaceTab(searchParams.get('tab'))
  const selectedSiteId = searchParams.get('siteId')

  useEffect(() => {
    if (!docsClient) {
      setClientAttachments([])
      return
    }
    let active = true
    attachmentService.list(ATTACHMENT_ENTITY_TYPES.Client, docsClient.id)
      .then((result) => { if (active) setClientAttachments(result) })
      .catch(() => { if (active) setClientAttachments([]) })
    return () => { active = false }
  }, [docsClient])

  function updateWorkspace(clientId: string, options?: { tab?: WorkspaceTab; siteId?: string | null }) {
    const nextParams = new URLSearchParams(searchParams)
    nextParams.set('clientId', clientId)
    if (options?.tab && options.tab !== 'overview') nextParams.set('tab', options.tab)
    else nextParams.delete('tab')
    if (options?.siteId) nextParams.set('siteId', options.siteId)
    else nextParams.delete('siteId')
    setSearchParams(nextParams, { replace: false })
  }

  function clearWorkspaceSelection() {
    const nextParams = new URLSearchParams(searchParams)
    nextParams.delete('clientId')
    nextParams.delete('tab')
    nextParams.delete('siteId')
    setSearchParams(nextParams, { replace: false })
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
      title="Clients, sites, and assets"
      description="Operate the full client hierarchy from one workspace instead of separate pages."
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
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Active clients" value={activeClientsCount} meta="Ready for service operations" emphasis="accent" />
        <MetricCard label="Expiring soon" value={expiringClientsCount} meta="Within the next 30 days" emphasis="warning" />
        <MetricCard label="Inactive clients" value={inactiveClientsCount} meta={hasFilters ? 'Filtered register view' : 'Retained for historical records'} />
      </MetricGrid>

      <div className="grid gap-5 xl:grid-cols-[minmax(0,1.08fr)_minmax(0,1.62fr)]">
        <SectionCard title="Client register" description="Select a client to open its overview, sites, assets, work orders, and documents in the same screen.">
          <div className="space-y-4">
            <SearchToolbar
              searchSlot={(
                <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
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
                emptyTitle="No clients found"
                emptyDescription={hasFilters ? 'Try clearing search or changing the status filter.' : 'Create the first client record to link work orders and assets.'}
                mobileCard={(row) => (
                  <div
                    className={`space-y-3 rounded-[24px] border p-4 transition-colors ${
                      row.id === selectedClientId
                        ? 'border-accent bg-accent/5'
                        : 'border-app bg-subtle'
                    }`}
                  >
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0">
                        <button
                          type="button"
                          className={`text-left font-semibold ${row.isActive ? 'text-app' : 'text-muted'}`}
                          onClick={() => updateWorkspace(row.id)}
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
                    <RowActionSelector
                      options={[
                        { value: 'workspace', label: 'Open workspace' },
                        { value: 'sites-assets', label: 'Sites & assets' },
                        { value: 'work-orders', label: 'Work orders' },
                        { value: 'edit', label: 'Edit' },
                        { value: 'documents', label: 'Documents' },
                        { value: row.isActive ? 'deactivate' : 'activate', label: row.isActive ? 'Deactivate' : 'Activate', disabled: actionClientId === row.id },
                      ]}
                      onApply={async (action) => {
                        if (action === 'workspace') updateWorkspace(row.id)
                        if (action === 'sites-assets') updateWorkspace(row.id, { tab: 'sites-assets' })
                        if (action === 'work-orders') updateWorkspace(row.id, { tab: 'work-orders' })
                        if (action === 'edit') openEditor(row)
                        if (action === 'documents') setDocsClient(row)
                        if (action === 'activate' || action === 'deactivate') await toggleClientStatus(row)
                      }}
                      buttonLabel="Apply"
                    />
                  </div>
                )}
                columns={[
                  {
                    key: 'name',
                    header: 'Client',
                    cell: (row) => (
                      <div className={row.isActive ? '' : 'opacity-70'}>
                        <button
                          type="button"
                          onClick={() => updateWorkspace(row.id)}
                          className={`text-left font-semibold hover:underline ${
                            row.id === selectedClientId
                              ? 'text-accent-strong'
                              : row.isActive
                                ? 'text-app'
                                : 'text-muted'
                          }`}
                        >
                          {row.clientName}
                        </button>
                        <p className="mt-1 text-xs text-muted">{row.clientType || 'Client'}</p>
                      </div>
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
                    className: 'min-w-[270px]',
                    cell: (row) => (
                      <RowActionSelector
                        options={[
                          { value: 'workspace', label: 'Open workspace' },
                          { value: 'sites-assets', label: 'Sites & assets' },
                          { value: 'work-orders', label: 'Work orders' },
                          { value: 'edit', label: 'Edit' },
                          { value: 'documents', label: 'Documents' },
                          { value: row.isActive ? 'deactivate' : 'activate', label: row.isActive ? 'Deactivate' : 'Activate', disabled: actionClientId === row.id },
                        ]}
                        onApply={async (action) => {
                          if (action === 'workspace') updateWorkspace(row.id)
                          if (action === 'sites-assets') updateWorkspace(row.id, { tab: 'sites-assets' })
                          if (action === 'work-orders') updateWorkspace(row.id, { tab: 'work-orders' })
                          if (action === 'edit') openEditor(row)
                          if (action === 'documents') setDocsClient(row)
                          if (action === 'activate' || action === 'deactivate') await toggleClientStatus(row)
                        }}
                        buttonLabel="Apply"
                      />
                    ),
                  },
                ]}
              />
            ) : null}
          </div>
        </SectionCard>

        <SectionCard
          title={selectedClient ? selectedClient.clientName : 'Unified workspace'}
          description={selectedClient
            ? 'Overview, sites, assets, work orders, and documents now stay together in this single client panel.'
            : 'Choose a client from the register to open the merged workspace.'}
          action={selectedClientId ? (
            <button type="button" className="button-secondary" onClick={clearWorkspaceSelection}>
              Clear selection
            </button>
          ) : null}
        >
          {selectedClientId ? (
            <ClientWorkspace
              clientId={selectedClientId}
              embedded
              onBackHref={null}
              initialTab={selectedTab}
              initialSiteId={selectedSiteId}
            />
          ) : (
            <div className="flex min-h-[480px] items-center justify-center rounded-[28px] border border-dashed border-app bg-subtle/70 p-8 text-center">
              <div className="max-w-md space-y-3">
                <p className="text-lg font-semibold text-app">Everything now lives in one client workspace.</p>
                <p className="text-sm text-muted">
                  Select a client to manage its overview, sites, assets, documents, and work orders without jumping to separate pages.
                </p>
              </div>
            </div>
          )}
        </SectionCard>
      </div>

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

      <Modal
        open={Boolean(docsClient)}
        title={`Documents - ${docsClient?.clientName ?? ''}`}
        description="Upload and manage files attached to this client."
        onClose={() => setDocsClient(null)}
        maxWidth="max-w-2xl"
      >
        {docsClient ? (
          <AttachmentPanel
            entityType={ATTACHMENT_ENTITY_TYPES.Client}
            entityId={docsClient.id}
            attachments={clientAttachments}
            onUploaded={(attachment) => setClientAttachments((prev) => [...prev, attachment])}
            onDeleted={(id) => setClientAttachments((prev) => prev.filter((attachment) => attachment.id !== id))}
          />
        ) : null}
      </Modal>
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
    <div className="rounded-2xl border border-app bg-app/20 px-3 py-3">
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

function resolveWorkspaceTab(tab: string | null): WorkspaceTab {
  if (tab && WORKSPACE_TABS.has(tab as WorkspaceTab)) {
    return tab as WorkspaceTab
  }
  return 'overview'
}
