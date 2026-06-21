import { MapPin, Paperclip, Search, X } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { AttachmentPanel } from '../../components/ui/AttachmentPanel'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, SearchToolbar, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { attachmentService, ATTACHMENT_ENTITY_TYPES } from '../../services/attachmentService'
import { clientService } from '../../services/clientService'
import { siteService } from '../../services/siteService'
import type { AttachmentRecord, ClientRecord, SiteRecord, UpsertSiteInput } from '../../types/api'

const emptySiteForm: UpsertSiteInput = {
  siteName: '',
  siteType: 'Branch',
  streetAddress: '',
  areaEstate: '',
  townCity: '',
  county: '',
  country: '',
  region: '',
  contactPerson: '',
  contactPhone: '',
  contactEmail: '',
  alternateContact: '',
  operatingHours: '',
  accessNotes: '',
  specialInstructions: '',
}

type SiteWithClient = SiteRecord & { clientName?: string }

export function SitesPage() {
  const { pushToast } = useToast()
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'Active' | 'Inactive' | 'all'>('Active')
  const [clientFilter, setClientFilter] = useState('')
  const [regionFilter, setRegionFilter] = useState('')
  const [editorOpen, setEditorOpen] = useState(false)
  const [editing, setEditing] = useState<SiteRecord | null>(null)
  const [editorClientId, setEditorClientId] = useState('')
  const [form, setForm] = useState<UpsertSiteInput>(emptySiteForm)
  const [saving, setSaving] = useState(false)
  const [docsSite, setDocsSite] = useState<SiteRecord | null>(null)
  const [siteAttachments, setSiteAttachments] = useState<AttachmentRecord[]>([])

  useEffect(() => {
    const handle = window.setTimeout(() => setDebouncedSearch(searchInput.trim()), 300)
    return () => window.clearTimeout(handle)
  }, [searchInput])

  useEffect(() => {
    if (!docsSite) { setSiteAttachments([]); return }
    let active = true
    attachmentService.list(ATTACHMENT_ENTITY_TYPES.Site, docsSite.id)
      .then((result) => { if (active) setSiteAttachments(result) })
      .catch(() => { if (active) setSiteAttachments([]) })
    return () => { active = false }
  }, [docsSite])

  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ signal, status: 'active' }),
    [],
    [],
  )

  const { data: allSites, loading, error, reload } = useAsyncData<SiteRecord[]>(
    async (signal) => {
      if (clientFilter) {
        return siteService.list(clientFilter, { signal, status: statusFilter === 'all' ? undefined : statusFilter })
      }
      return siteService.search({ q: debouncedSearch || undefined, region: regionFilter || undefined, signal })
    },
    [],
    [clientFilter, statusFilter, debouncedSearch, regionFilter],
  )

  const clientMap = useMemo(() => new Map(clients.map((c) => [c.id, c.clientName])), [clients])

  const sitesWithClients: SiteWithClient[] = useMemo(() =>
    allSites.map((s) => ({ ...s, clientName: clientMap.get(s.clientId) })), [allSites, clientMap])

  const filtered = useMemo(() => {
    const q = debouncedSearch.toLowerCase()
    return sitesWithClients.filter((s) => {
      if (!clientFilter && q && !s.siteName.toLowerCase().includes(q) && !s.siteCode.toLowerCase().includes(q) && !(s.clientName ?? '').toLowerCase().includes(q)) return false
      return true
    })
  }, [sitesWithClients, debouncedSearch, clientFilter])

  const activeSitesCount = filtered.filter((s) => s.status === 'Active').length
  const regions = useMemo(() => [...new Set(allSites.map((s) => s.region).filter(Boolean))].sort() as string[], [allSites])

  const hasFilters = Boolean(debouncedSearch) || statusFilter !== 'Active' || clientFilter || regionFilter

  function openEditor(client: ClientRecord, site?: SiteRecord) {
    setEditorClientId(client.id)
    setEditing(site ?? null)
    setForm(site ? {
      siteName: site.siteName,
      siteType: site.siteType,
      streetAddress: site.streetAddress ?? '',
      areaEstate: site.areaEstate ?? '',
      townCity: site.townCity ?? '',
      county: site.county ?? '',
      country: site.country ?? '',
      region: site.region ?? '',
      contactPerson: site.contactPerson ?? '',
      contactPhone: site.contactPhone ?? '',
      contactEmail: site.contactEmail ?? '',
      alternateContact: site.alternateContact ?? '',
      operatingHours: site.operatingHours ?? '',
      accessNotes: site.accessNotes ?? '',
      specialInstructions: site.specialInstructions ?? '',
    } : emptySiteForm)
    setEditorOpen(true)
  }

  async function saveSite() {
    if (!editorClientId || saving) return
    setSaving(true)
    try {
      if (editing) {
        await siteService.update(editorClientId, editing.id, form)
        pushToast({ title: 'Site updated', description: 'Changes saved successfully.', tone: 'success' })
      } else {
        await siteService.create(editorClientId, form)
        pushToast({ title: 'Site created', description: 'New site is ready.', tone: 'success' })
      }
      setEditorOpen(false)
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save site.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function deactivateSite(site: SiteWithClient) {
    if (!site.clientId) return
    if (!window.confirm(`Deactivate site "${site.siteName}"?`)) return
    try {
      await siteService.deactivate(site.clientId, site.id)
      pushToast({ title: 'Site deactivated', description: 'Site is now inactive.', tone: 'success' })
      await reload()
    } catch (err) {
      pushToast({ title: 'Failed', description: err instanceof Error ? err.message : 'Could not deactivate site.', tone: 'danger' })
    }
  }

  return (
    <PageScaffold
      eyebrow="Clients"
      title="Sites register"
      description="View and manage physical locations across all clients. Filter by client, region, or status."
      actions={null}
    >
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Sites in view" value={filtered.length} meta={hasFilters ? 'Filtered result' : 'All active sites'} emphasis="accent" />
        <MetricCard label="Active" value={activeSitesCount} meta="Operational sites" />
        <MetricCard label="Inactive" value={filtered.length - activeSitesCount} meta="Closed or deactivated" />
      </MetricGrid>

      <SectionCard title="Sites list" description="Sites are managed under their parent client. Use the Edit button to update details.">
        <div className="space-y-4">
          <SearchToolbar
            searchSlot={(
              <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
                <Search className="h-4 w-4 text-muted" />
                <input
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                  placeholder="Search by site name, code, or client"
                />
              </label>
            )}
            filters={(
              <>
                <select value={clientFilter} onChange={(e) => setClientFilter(e.target.value)} className="field-input">
                  <option value="">All clients</option>
                  {clients.map((c) => <option key={c.id} value={c.id}>{c.clientName}</option>)}
                </select>
                <select value={regionFilter} onChange={(e) => setRegionFilter(e.target.value)} className="field-input">
                  <option value="">All regions</option>
                  {regions.map((r) => <option key={r} value={r}>{r}</option>)}
                </select>
                <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} className="field-input">
                  <option value="Active">Active</option>
                  <option value="Inactive">Inactive</option>
                  <option value="all">All statuses</option>
                </select>
              </>
            )}
            actions={(
              <button
                type="button"
                className="button-secondary"
                onClick={() => { setSearchInput(''); setDebouncedSearch(''); setStatusFilter('Active'); setClientFilter(''); setRegionFilter('') }}
                disabled={!hasFilters}
              >
                <X className="h-4 w-4" />
                Clear
              </button>
            )}
          />

          {loading ? <LoadingState label="Loading sites" /> : null}
          {!loading && error ? <ErrorState title="Unable to load sites" description={error} /> : null}
          {!loading && !error ? (
            <DataTable
              rows={filtered}
              rowKey={(row) => row.id}
              pageSize={15}
              emptyTitle="No sites found"
              emptyDescription={hasFilters ? 'Try clearing filters or search.' : 'Sites are created under clients. Go to Clients and use the Sites button.'}
              mobileCard={(row) => (
                <div className="space-y-3 rounded-[24px] border border-app bg-subtle p-4">
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-semibold text-app">{row.siteName}</p>
                      <p className="mt-1 text-xs text-muted">{row.siteCode} · {row.siteType}</p>
                      <p className="mt-1 text-xs text-muted">{row.clientName || '—'}</p>
                    </div>
                    <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge>
                  </div>
                  {(row.townCity || row.county) && <p className="text-sm text-muted">{[row.townCity, row.county].filter(Boolean).join(', ')}</p>}
                  <div className="flex gap-2">
                    {clients.find((c) => c.id === row.clientId) && (
                      <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => openEditor(clients.find((c) => c.id === row.clientId)!, row)}>
                        Edit
                      </button>
                    )}
                    {row.status === 'Active' && (
                      <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => void deactivateSite(row)}>
                        Deactivate
                      </button>
                    )}
                  </div>
                </div>
              )}
              columns={[
                {
                  key: 'site',
                  header: 'Site',
                  cell: (row) => (
                    <div>
                      <div className="flex items-center gap-2">
                        <MapPin className="h-3.5 w-3.5 text-muted" />
                        <p className="font-semibold text-app">{row.siteName}</p>
                      </div>
                      <p className="mt-1 text-xs text-muted">{row.siteCode} · {row.siteType}</p>
                    </div>
                  ),
                },
                { key: 'client', header: 'Client', cell: (row) => <span>{row.clientName || '—'}</span> },
                { key: 'location', header: 'Location', cell: (row) => <span>{[row.townCity, row.county, row.country].filter(Boolean).join(', ') || '—'}</span> },
                { key: 'region', header: 'Region', cell: (row) => <span>{row.region || '—'}</span> },
                { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
                {
                  key: 'actions',
                  header: 'Actions',
                  cell: (row) => (
                    <div className="flex gap-2">
                      {clients.find((c) => c.id === row.clientId) && (
                        <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(clients.find((c) => c.id === row.clientId)!, row)}>
                          Edit
                        </button>
                      )}
                      <button type="button" className="button-secondary px-3 py-2" title="Documents" onClick={() => setDocsSite(row)} data-testid={`site-docs-btn-${row.id}`}>
                        <Paperclip className="size-4" />
                      </button>
                      {row.status === 'Active' && (
                        <button type="button" className="button-secondary px-3 py-2" onClick={() => void deactivateSite(row)}>
                          Deactivate
                        </button>
                      )}
                    </div>
                  ),
                },
              ]}
            />
          ) : null}
        </div>
      </SectionCard>

      <Drawer
        open={editorOpen}
        title={editing ? 'Edit site' : 'Add site'}
        description={`Managing site for ${clients.find((c) => c.id === editorClientId)?.clientName ?? 'client'}.`}
        onClose={() => setEditorOpen(false)}
      >
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="Site name"><input value={form.siteName} onChange={(e) => setForm((f) => ({ ...f, siteName: e.target.value }))} className="field-input" /></Field>
          <Field label="Site type">
            <select value={form.siteType ?? ''} onChange={(e) => setForm((f) => ({ ...f, siteType: e.target.value }))} className="field-input">
              <option value="Branch">Branch</option>
              <option value="HQ">HQ</option>
              <option value="Data Centre">Data Centre</option>
              <option value="Warehouse">Warehouse</option>
              <option value="Factory">Factory</option>
              <option value="Other">Other</option>
            </select>
          </Field>
          <Field label="Street address"><input value={form.streetAddress ?? ''} onChange={(e) => setForm((f) => ({ ...f, streetAddress: e.target.value }))} className="field-input" /></Field>
          <Field label="Area / Estate"><input value={form.areaEstate ?? ''} onChange={(e) => setForm((f) => ({ ...f, areaEstate: e.target.value }))} className="field-input" /></Field>
          <Field label="Town / City"><input value={form.townCity ?? ''} onChange={(e) => setForm((f) => ({ ...f, townCity: e.target.value }))} className="field-input" /></Field>
          <Field label="County"><input value={form.county ?? ''} onChange={(e) => setForm((f) => ({ ...f, county: e.target.value }))} className="field-input" /></Field>
          <Field label="Country"><input value={form.country ?? ''} onChange={(e) => setForm((f) => ({ ...f, country: e.target.value }))} className="field-input" /></Field>
          <Field label="Region"><input value={form.region ?? ''} onChange={(e) => setForm((f) => ({ ...f, region: e.target.value }))} className="field-input" /></Field>
          <Field label="Contact person"><input value={form.contactPerson ?? ''} onChange={(e) => setForm((f) => ({ ...f, contactPerson: e.target.value }))} className="field-input" /></Field>
          <Field label="Contact phone"><input value={form.contactPhone ?? ''} onChange={(e) => setForm((f) => ({ ...f, contactPhone: e.target.value }))} className="field-input" /></Field>
          <Field label="Contact email"><input value={form.contactEmail ?? ''} onChange={(e) => setForm((f) => ({ ...f, contactEmail: e.target.value }))} className="field-input" /></Field>
          <Field label="Operating hours"><input value={form.operatingHours ?? ''} onChange={(e) => setForm((f) => ({ ...f, operatingHours: e.target.value }))} className="field-input" /></Field>
        </div>
        <Field label="Access notes"><textarea value={form.accessNotes ?? ''} onChange={(e) => setForm((f) => ({ ...f, accessNotes: e.target.value }))} className="field-input mt-3 min-h-[80px]" /></Field>
        <Field label="Special instructions"><textarea value={form.specialInstructions ?? ''} onChange={(e) => setForm((f) => ({ ...f, specialInstructions: e.target.value }))} className="field-input mt-3 min-h-[60px]" /></Field>
        <StickyActionFooter className="mt-4">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)} disabled={saving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveSite()} disabled={saving}>{saving ? 'Saving...' : 'Save site'}</button>
        </StickyActionFooter>
      </Drawer>

      <Modal
        open={Boolean(docsSite)}
        title={`Documents — ${docsSite?.siteName ?? ''}`}
        description="Upload and manage files attached to this site."
        onClose={() => setDocsSite(null)}
        maxWidth="max-w-2xl"
      >
        {docsSite ? (
          <AttachmentPanel
            entityType={ATTACHMENT_ENTITY_TYPES.Site}
            entityId={docsSite.id}
            attachments={siteAttachments}
            onUploaded={(a) => setSiteAttachments((prev) => [...prev, a])}
            onDeleted={(id) => setSiteAttachments((prev) => prev.filter((a) => a.id !== id))}
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
