import { Download, Plus, Search, X } from 'lucide-react'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { useShellContext } from '../../components/layout/AppShell'
import { BulkImportModal } from '../../components/ui/BulkImportModal'
import { Badge } from '../../components/ui/Badge'
import { AttachmentPanel } from '../../components/ui/AttachmentPanel'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { MetricCard, MetricGrid, PageScaffold, SearchToolbar, SectionCard, StickyActionFooter } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { ApiError } from '../../lib/api'
import { assetCategoryService, type AssetCategoryRecord } from '../../services/assetCategoryService'
import { assetService } from '../../services/assetService'
import { attachmentService, ATTACHMENT_ENTITY_TYPES } from '../../services/attachmentService'
import { clientService } from '../../services/clientService'
import { importService } from '../../services/importService'
import { siteService } from '../../services/siteService'
import { systemService } from '../../services/systemService'
import type {
  AssetCustomFieldValueRecord,
  AssetRecord,
  AttachmentRecord,
  ClientRecord,
  SiteRecord,
  SystemRecord,
  UpsertAssetCustomFieldValueInput,
  UpsertAssetInput,
} from '../../types/api'
import { formatDateOnly } from '../../utils/date'

type AssetsPayload = {
  assets: AssetRecord[]
  clients: ClientRecord[]
  categories: AssetCategoryRecord[]
}

type AssetFieldKey =
  | 'clientId'
  | 'assetName'
  | 'assetCode'
  | 'assetType'
  | 'ownershipType'
  | 'installationDate'
  | 'pmFrequencyValue'
  | 'pmFrequencyUnit'
  | 'warrantyExpiryDate'
  | 'lastPmDate'
  | 'nextPmDate'
  | 'status'
  | 'assetCategoryId'

type AssetFieldErrors = Partial<Record<AssetFieldKey, string>>

const emptyPayload: AssetsPayload = { assets: [], clients: [], categories: [] }

const emptyForm = (selectedBranchId: string): UpsertAssetInput => ({
  clientId: '',
  branchId: selectedBranchId === 'all' ? null : selectedBranchId,
  siteId: null,
  systemId: null,
  assetCategoryId: null,
  assetName: '',
  assetCode: '',
  assetType: '',
  ownershipType: '',
  location: '',
  serialNumber: '',
  manufacturer: '',
  model: '',
  installationDate: '',
  warrantyExpiryDate: '',
  recommendedPmFrequency: '',
  pmFrequencyValue: null,
  pmFrequencyUnit: 'months',
  autoSchedulePm: true,
  lastPmDate: '',
  nextPmDate: '',
  notes: '',
  status: 'Active',
  customFieldValues: [],
})

function toCustomFieldInputs(values: AssetCustomFieldValueRecord[] | undefined): UpsertAssetCustomFieldValueInput[] {
  return (values ?? []).map((item) => ({
    fieldDefinitionId: item.fieldDefinitionId,
    value: item.value,
  }))
}

export function AssetsPage() {
  const { selectedBranchId } = useShellContext()
  const { pushToast } = useToast()
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'active' | 'inactive' | 'all'>('active')
  const [clientFilterId, setClientFilterId] = useState('')
  const [siteFilterId, setSiteFilterId] = useState('')
  const [systemFilterId, setSystemFilterId] = useState('')
  const [categoryFilterId, setCategoryFilterId] = useState('')
  const [filterSites, setFilterSites] = useState<SiteRecord[]>([])
  const [filterSystems, setFilterSystems] = useState<SystemRecord[]>([])
  const [editorOpen, setEditorOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [allowCreateSystems, setAllowCreateSystems] = useState(false)
  const [editing, setEditing] = useState<AssetRecord | null>(null)
  const [form, setForm] = useState<UpsertAssetInput>(emptyForm(selectedBranchId))
  const [saveError, setSaveError] = useState('')
  const [fieldErrors, setFieldErrors] = useState<AssetFieldErrors>({})
  const [saving, setSaving] = useState(false)
  const [sites, setSites] = useState<SiteRecord[]>([])
  const [systems, setSystems] = useState<SystemRecord[]>([])
  const [docsAsset, setDocsAsset] = useState<AssetRecord | null>(null)
  const [assetAttachments, setAssetAttachments] = useState<AttachmentRecord[]>([])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      setDebouncedSearch(searchInput.trim())
    }, 300)
    return () => window.clearTimeout(handle)
  }, [searchInput])

  const { data, loading, error, reload } = useAsyncData<AssetsPayload>(
    async (signal) => {
      const [assets, clients, categories] = await Promise.all([
        assetService.list(selectedBranchId, signal, {
          search: debouncedSearch || undefined,
          status: statusFilter,
          clientId: clientFilterId || undefined,
          siteId: siteFilterId || undefined,
          systemId: systemFilterId || undefined,
          categoryId: categoryFilterId || undefined,
        }),
        clientService.list({ signal, status: 'active' }),
        assetCategoryService.list(signal),
      ])
      return { assets, clients, categories }
    },
    emptyPayload,
    [debouncedSearch, selectedBranchId, statusFilter, clientFilterId, siteFilterId, systemFilterId, categoryFilterId],
  )

  useEffect(() => {
    if (!clientFilterId) {
      setFilterSites([])
      setSiteFilterId('')
      setFilterSystems([])
      setSystemFilterId('')
      return
    }

    let cancelled = false
    siteService.list(clientFilterId, { status: 'Active' })
      .then((result) => {
        if (!cancelled) setFilterSites(result)
      })
      .catch(() => {
        if (!cancelled) setFilterSites([])
      })
    return () => { cancelled = true }
  }, [clientFilterId])

  useEffect(() => {
    if (!clientFilterId || !siteFilterId) {
      setFilterSystems([])
      setSystemFilterId('')
      return
    }

    let cancelled = false
    systemService.list(clientFilterId, siteFilterId)
      .then((result) => {
        if (!cancelled) setFilterSystems(result)
      })
      .catch(() => {
        if (!cancelled) setFilterSystems([])
      })
    return () => { cancelled = true }
  }, [clientFilterId, siteFilterId])

  const hasFilters = useMemo(
    () => Boolean(debouncedSearch) || statusFilter !== 'active' || Boolean(clientFilterId) || Boolean(siteFilterId) || Boolean(systemFilterId) || Boolean(categoryFilterId),
    [debouncedSearch, statusFilter, clientFilterId, siteFilterId, systemFilterId, categoryFilterId],
  )
  const activeAssetsCount = data.assets.filter((asset) => asset.status === 'Active').length
  const nextPmCount = data.assets.filter((asset) => Boolean(asset.nextPmDate)).length

  const selectedCategory = useMemo(
    () => data.categories.find((item) => item.id === form.assetCategoryId) ?? null,
    [data.categories, form.assetCategoryId],
  )

  useEffect(() => {
    if (!docsAsset) {
      setAssetAttachments([])
      return
    }
    let active = true
    attachmentService.list(ATTACHMENT_ENTITY_TYPES.Asset, docsAsset.id)
      .then((result) => { if (active) setAssetAttachments(result) })
      .catch(() => { if (active) setAssetAttachments([]) })
    return () => { active = false }
  }, [docsAsset])

  useEffect(() => {
    if (!form.clientId) {
      setSites([])
      setSystems([])
      return
    }
    let cancelled = false
    siteService.list(form.clientId, { status: 'Active' }).then((result) => {
      if (!cancelled) setSites(result)
    }).catch(() => { if (!cancelled) setSites([]) })
    return () => { cancelled = true }
  }, [form.clientId])

  useEffect(() => {
    if (!form.clientId || !form.siteId) {
      setSystems([])
      return
    }

    let cancelled = false
    systemService.list(form.clientId, form.siteId)
      .then((result) => {
        if (!cancelled) setSystems(result)
      })
      .catch(() => { if (!cancelled) setSystems([]) })
    return () => { cancelled = true }
  }, [form.clientId, form.siteId])

  function syncCustomFields(categoryId: string | null | undefined, existingValues?: UpsertAssetCustomFieldValueInput[]) {
    const category = data.categories.find((item) => item.id === categoryId)
    if (!category) return []

    return category.fields.map((field) => ({
      fieldDefinitionId: field.id,
      value: existingValues?.find((item) => item.fieldDefinitionId === field.id)?.value ?? '',
    }))
  }

  function openEditor(asset?: AssetRecord) {
    setEditing(asset ?? null)
    setSaveError('')
    setFieldErrors({})
    setSites([])

    const assetCategoryId = asset?.assetCategoryId ?? null
    const customFieldValues = asset
      ? syncCustomFields(assetCategoryId, toCustomFieldInputs(asset.customFieldValues))
      : []

    setForm({
      clientId: asset?.clientId ?? '',
      branchId: asset?.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
      siteId: asset?.siteId ?? null,
      systemId: asset?.systemId ?? null,
      assetCategoryId,
      assetName: asset?.assetName ?? '',
      assetCode: asset?.assetCode ?? '',
      assetType: asset?.assetType ?? '',
      ownershipType: asset?.ownershipType ?? '',
      location: asset?.location ?? '',
      serialNumber: asset?.serialNumber ?? '',
      manufacturer: asset?.manufacturer ?? '',
      model: asset?.model ?? '',
      installationDate: asset?.installationDate?.slice(0, 10) ?? '',
      warrantyExpiryDate: asset?.warrantyExpiryDate?.slice(0, 10) ?? '',
      recommendedPmFrequency: asset?.recommendedPmFrequency ?? '',
      pmFrequencyValue: asset?.pmFrequencyValue ?? null,
      pmFrequencyUnit: asset?.pmFrequencyUnit ?? 'months',
      autoSchedulePm: asset?.autoSchedulePm ?? true,
      lastPmDate: asset?.lastPmDate?.slice(0, 10) ?? '',
      nextPmDate: asset?.nextPmDate?.slice(0, 10) ?? '',
      notes: asset?.notes ?? '',
      status: asset?.status ?? 'Active',
      customFieldValues,
    })
    setEditorOpen(true)
  }

  function updateCustomFieldValue(fieldDefinitionId: string, value: string) {
    setForm((current) => ({
      ...current,
      customFieldValues: (current.customFieldValues ?? []).map((item) =>
        item.fieldDefinitionId === fieldDefinitionId ? { ...item, value } : item,
      ),
    }))
  }

  function validateFieldOnBlur(field: AssetFieldKey, value: string) {
    if (field === 'clientId' && !value.trim()) {
      setFieldErrors((current) => ({ ...current, clientId: 'Client is required.' }))
    } else if (field === 'assetName' && !value.trim()) {
      setFieldErrors((current) => ({ ...current, assetName: 'Asset name is required.' }))
    } else {
      setFieldErrors((current) => { const next = { ...current }; delete next[field]; return next })
    }
  }

  async function saveAsset() {
    if (saving) return

    setSaving(true)
    setSaveError('')
    setFieldErrors({})

    const nextFieldErrors: AssetFieldErrors = {}
    if (!form.clientId.trim()) {
      nextFieldErrors.clientId = 'Client is required.'
    }
    if (!form.assetName.trim()) {
      nextFieldErrors.assetName = 'Asset name is required.'
    }
    if (!form.siteId) {
      nextFieldErrors.clientId = nextFieldErrors.clientId || 'Client and site are required.'
    }

    if (Object.keys(nextFieldErrors).length > 0) {
      setFieldErrors(nextFieldErrors)
      setSaveError('Please fix the highlighted asset fields and try again.')
      setSaving(false)
      return
    }

    try {
      if (editing) {
        await assetService.update(editing.id, form)
        pushToast({ title: 'Asset updated', description: 'The asset changes were saved.', tone: 'success' })
      } else {
        await assetService.create(form)
        pushToast({ title: 'Asset created', description: 'The new asset is now available in the register.', tone: 'success' })
      }
      setForm(emptyForm(selectedBranchId))
      setEditing(null)
      setEditorOpen(false)
      setFieldErrors({})
      await reload()
    } catch (error) {
      const { formError, fieldErrors: backendFieldErrors } = extractAssetErrors(error)
      setFieldErrors(backendFieldErrors)
      setSaveError(formError)
    } finally {
      setSaving(false)
    }
  }

  async function deactivateAsset(asset: AssetRecord) {
    if (asset.status === 'Inactive') return
    if (!window.confirm(`Disable asset "${asset.assetName}"?`)) return

    try {
      await assetService.deactivate(asset.id)
      pushToast({ title: 'Asset disabled', description: 'The asset is now inactive and will not be scheduled for PM.', tone: 'success' })
      await reload()
    } catch (error) {
      pushToast({ title: 'Disable failed', description: error instanceof Error ? error.message : 'Unable to disable this asset.', tone: 'danger' })
    }
  }

  return (
    <PageScaffold
      eyebrow="Assets"
      title="Asset register"
      description="Manage client assets, categories, service details, and preventive maintenance dates."
      actions={(
        <>
          <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => assetService.downloadImportTemplate()}>
            <Download className="h-4 w-4" />
            Import template
          </button>
          <button type="button" className="button-secondary w-full sm:w-auto" onClick={() => setImportOpen(true)}>
            Bulk import
          </button>
          <button type="button" className="button-primary w-full sm:w-auto" onClick={() => openEditor()}>
            <Plus className="h-4 w-4" />
            Add asset
          </button>
        </>
      )}
    >
      <MetricGrid className="xl:grid-cols-3">
        <MetricCard label="Assets in view" value={data.assets.length} meta={hasFilters ? 'Filtered asset register' : 'Current branch scope'} emphasis="accent" />
        <MetricCard label="Active assets" value={activeAssetsCount} meta={`${data.assets.length - activeAssetsCount} inactive`} />
        <MetricCard label="PM scheduled" value={nextPmCount} meta="Assets with next PM date" />
      </MetricGrid>

      <SectionCard title="Asset list" description="Keep serviceable equipment aligned to the correct client, site, category, and PM schedule.">
        <div className="space-y-4">
          <SearchToolbar
            searchSlot={(
              <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-3">
                <Search className="h-4 w-4 text-muted" />
                <input
                  data-testid="asset-search-input"
                  value={searchInput}
                  onChange={(event) => setSearchInput(event.target.value)}
                  className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                  placeholder="Search by asset, tag, serial, make, model, client, or location"
                />
              </label>
            )}
            filters={(
              <>
                <select data-testid="asset-client-filter" value={clientFilterId} onChange={(event) => setClientFilterId(event.target.value)} className="field-input">
                  <option value="">All clients</option>
                  {data.clients.map((client) => (
                    <option key={client.id} value={client.id}>{client.clientName}</option>
                  ))}
                </select>
                <select data-testid="asset-site-filter" value={siteFilterId} onChange={(event) => setSiteFilterId(event.target.value)} className="field-input" disabled={!clientFilterId}>
                  <option value="">{clientFilterId ? 'All sites' : 'Select client first'}</option>
                  {filterSites.map((site) => (
                    <option key={site.id} value={site.id}>{site.siteName}</option>
                  ))}
                </select>
                <select data-testid="asset-system-filter" value={systemFilterId} onChange={(event) => setSystemFilterId(event.target.value)} className="field-input" disabled={!siteFilterId}>
                  <option value="">{siteFilterId ? 'All systems' : 'Select site first'}</option>
                  {filterSystems.map((system) => (
                    <option key={system.id} value={system.id}>{system.systemName}</option>
                  ))}
                </select>
                <select data-testid="asset-category-filter" value={categoryFilterId} onChange={(event) => setCategoryFilterId(event.target.value)} className="field-input">
                  <option value="">All categories</option>
                  {data.categories.map((category) => (
                    <option key={category.id} value={category.id}>{category.name}</option>
                  ))}
                </select>
                <select data-testid="asset-status-filter" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="field-input">
                  <option value="active">Active</option>
                  <option value="inactive">Inactive</option>
                  <option value="all">All</option>
                </select>
              </>
            )}
            actions={(
              <button
                type="button"
                className="button-secondary"
                onClick={() => {
                  setSearchInput('')
                  setDebouncedSearch('')
                  setStatusFilter('active')
                  setClientFilterId('')
                  setSiteFilterId('')
                  setSystemFilterId('')
                  setCategoryFilterId('')
                }}
                disabled={!hasFilters}
              >
                <X className="h-4 w-4" />
                Clear
              </button>
            )}
          />

          {loading ? <LoadingState label="Loading assets" /> : null}
          {!loading && error ? <ErrorState title="Unable to load assets" description={error} /> : null}
          {!loading && !error ? (
            <DataTable
              rows={data.assets}
              rowKey={(row) => row.id}
              pageSize={10}
              emptyTitle="No assets found"
              emptyDescription={hasFilters ? 'Try clearing search or changing filters.' : 'Create an asset or import your asset list to get started.'}
              mobileCard={(row) => (
                <div className="space-y-3 rounded-[24px] border border-app bg-subtle p-4">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{row.assetName}</p>
                      <p className="mt-1 text-xs text-muted">{row.assetCode}</p>
                    </div>
                    <Badge tone={row.status === 'Active' ? 'success' : 'warning'}>{row.status}</Badge>
                  </div>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <Detail label="Client" value={row.clientName || 'Not linked'} />
                    <Detail label="Site" value={row.siteName || 'Not linked'} />
                    <Detail label="System" value={row.systemName || 'Not linked'} />
                    <Detail label="Category" value={row.assetCategoryName || 'Not set'} />
                    <Detail label="Next PM" value={formatPmPreview(row.lastPmDate, row.pmFrequencyValue, row.pmFrequencyUnit, row.nextPmDate)} />
                  </div>
                  <RowActionSelector
                    options={[
                      { value: 'edit', label: 'Edit' },
                      { value: 'documents', label: 'Documents' },
                      ...(row.status === 'Active' ? [{ value: 'deactivate', label: 'Disable' }] : []),
                    ]}
                    selectTestId={`asset-action-select-${row.id}`}
                    applyTestId={`asset-action-apply-${row.id}`}
                    onApply={(action) => {
                      if (action === 'edit') openEditor(row)
                      if (action === 'documents') setDocsAsset(row)
                      if (action === 'deactivate') void deactivateAsset(row)
                    }}
                    buttonLabel="Apply"
                  />
                </div>
              )}
              columns={[
                {
                  key: 'asset',
                  header: 'Asset',
                  cell: (row) => (
                    <div className={row.status === 'Inactive' ? 'opacity-70' : ''}>
                      <p className="font-semibold text-app">{row.assetName}</p>
                      <p className="mt-1 text-xs text-muted">{row.assetCode}</p>
                    </div>
                  ),
                },
                { key: 'client', header: 'Client', cell: (row) => <span>{row.clientName || 'Not linked'}</span> },
                { key: 'site', header: 'Site', cell: (row) => <span>{row.siteName || '-'}</span> },
                { key: 'system', header: 'System', cell: (row) => <span>{row.systemName || '-'}</span> },
                { key: 'category', header: 'Category', cell: (row) => <span>{row.assetCategoryName || '-'}</span> },
                { key: 'type', header: 'Type', cell: (row) => <span>{row.assetType || 'Not set'}</span> },
                { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'warning'}>{row.status}</Badge> },
                { key: 'pm', header: 'Next PM', cell: (row) => <span>{formatPmPreview(row.lastPmDate, row.pmFrequencyValue, row.pmFrequencyUnit, row.nextPmDate)}</span> },
                {
                  key: 'actions',
                  header: 'Actions',
                  className: 'min-w-[220px]',
                  cell: (row) => (
                    <RowActionSelector
                      options={[
                        { value: 'edit', label: 'Edit' },
                        { value: 'documents', label: 'Documents' },
                        ...(row.status === 'Active' ? [{ value: 'deactivate', label: 'Disable' }] : []),
                      ]}
                      selectTestId={`asset-action-select-${row.id}`}
                      applyTestId={`asset-action-apply-${row.id}`}
                      onApply={(action) => {
                        if (action === 'edit') openEditor(row)
                        if (action === 'documents') setDocsAsset(row)
                        if (action === 'deactivate') void deactivateAsset(row)
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

      <Modal open={editorOpen} title={editing ? 'Edit asset' : 'Add asset'} description="Capture the asset details needed for service, category-specific data, and maintenance." onClose={() => setEditorOpen(false)} maxWidth="max-w-5xl">
        <div className="mb-4 rounded-2xl border border-app bg-app/20 px-4 py-3 text-sm text-muted">
          Hierarchy: <span className="font-medium text-app">Client {'->'} Site {'->'} System {'->'} Asset / Component</span>
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Client">
            <select
              value={form.clientId}
              onChange={(event) => setForm((current) => ({ ...current, clientId: event.target.value, siteId: null, systemId: null }))}
              onBlur={(event) => validateFieldOnBlur('clientId', event.target.value)}
              className="field-input"
            >
              <option value="">Select client</option>
              {data.clients.map((client) => (
                <option key={client.id} value={client.id}>{client.clientName}</option>
              ))}
            </select>
            {fieldErrors.clientId ? <FieldError message={fieldErrors.clientId} /> : null}
          </Field>
          <Field label="Site">
            <select
              value={form.siteId ?? ''}
              onChange={(event) => setForm((current) => ({ ...current, siteId: event.target.value || null, systemId: null }))}
              className="field-input"
              disabled={!form.clientId}
            >
              <option value="">{form.clientId ? 'No site selected' : 'Select a client first'}</option>
              {sites.map((site) => (
                <option key={site.id} value={site.id}>{site.siteName}</option>
              ))}
            </select>
          </Field>
          <Field label="System (optional)">
            <select
              value={form.systemId ?? ''}
              onChange={(event) => setForm((current) => ({ ...current, systemId: event.target.value || null }))}
              className="field-input"
              disabled={!form.siteId}
            >
              <option value="">{form.siteId ? 'No system selected' : 'Select a site first'}</option>
              {systems.map((system) => (
                <option key={system.id} value={system.id}>{system.systemName}</option>
              ))}
            </select>
          </Field>
          <Field label="Asset category">
            <select
              data-testid="asset-category-select"
              value={form.assetCategoryId ?? ''}
              onChange={(event) => {
                const nextCategoryId = event.target.value || null
                setForm((current) => ({
                  ...current,
                  assetCategoryId: nextCategoryId,
                  customFieldValues: syncCustomFields(nextCategoryId, current.customFieldValues),
                }))
              }}
              className="field-input"
            >
              <option value="">No category selected</option>
              {data.categories.map((category) => (
                <option key={category.id} value={category.id}>{category.name}</option>
              ))}
            </select>
            {fieldErrors.assetCategoryId ? <FieldError message={fieldErrors.assetCategoryId} /> : null}
          </Field>
          <Field label="Asset name">
            <input value={form.assetName} onChange={(event) => setForm((current) => ({ ...current, assetName: event.target.value }))} onBlur={(event) => validateFieldOnBlur('assetName', event.target.value)} className="field-input" />
            {fieldErrors.assetName ? <FieldError message={fieldErrors.assetName} /> : null}
          </Field>
          <Field label="Asset code">
            <input value={form.assetCode || ''} onChange={(event) => setForm((current) => ({ ...current, assetCode: event.target.value }))} className="field-input" placeholder="Leave blank to auto-generate" />
            {fieldErrors.assetCode ? <FieldError message={fieldErrors.assetCode} /> : null}
          </Field>
          <Field label="Asset type">
            <input value={form.assetType || ''} onChange={(event) => setForm((current) => ({ ...current, assetType: event.target.value }))} className="field-input" />
            {fieldErrors.assetType ? <FieldError message={fieldErrors.assetType} /> : null}
          </Field>
          <Field label="Ownership type">
            <input value={form.ownershipType || ''} onChange={(event) => setForm((current) => ({ ...current, ownershipType: event.target.value }))} className="field-input" placeholder="Owned, leased, client-owned..." />
            {fieldErrors.ownershipType ? <FieldError message={fieldErrors.ownershipType} /> : null}
          </Field>
          <Field label="Location"><input value={form.location || ''} onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))} className="field-input" /></Field>
          <Field label="Serial number"><input value={form.serialNumber || ''} onChange={(event) => setForm((current) => ({ ...current, serialNumber: event.target.value }))} className="field-input" /></Field>
          <Field label="Manufacturer"><input value={form.manufacturer || ''} onChange={(event) => setForm((current) => ({ ...current, manufacturer: event.target.value }))} className="field-input" /></Field>
          <Field label="Model"><input value={form.model || ''} onChange={(event) => setForm((current) => ({ ...current, model: event.target.value }))} className="field-input" /></Field>
          <Field label="Installation date">
            <input type="date" value={form.installationDate || ''} onChange={(event) => setForm((current) => ({ ...current, installationDate: event.target.value }))} className="field-input" />
            {fieldErrors.installationDate ? <FieldError message={fieldErrors.installationDate} /> : null}
          </Field>
          <Field label="Warranty expiry">
            <input type="date" value={form.warrantyExpiryDate || ''} onChange={(event) => setForm((current) => ({ ...current, warrantyExpiryDate: event.target.value }))} className="field-input" />
            {fieldErrors.warrantyExpiryDate ? <FieldError message={fieldErrors.warrantyExpiryDate} /> : null}
          </Field>
          <Field label="Last PM Service Date">
            <input type="date" value={form.lastPmDate || ''} onChange={(event) => setForm((current) => ({ ...current, lastPmDate: event.target.value }))} className="field-input" />
            {fieldErrors.lastPmDate ? <FieldError message={fieldErrors.lastPmDate} /> : null}
          </Field>
          <Field label="PM Frequency">
            <input type="number" min={1} value={form.pmFrequencyValue ?? ''} onChange={(event) => setForm((current) => ({ ...current, pmFrequencyValue: event.target.value ? Number(event.target.value) : null }))} className="field-input" />
            {fieldErrors.pmFrequencyValue ? <FieldError message={fieldErrors.pmFrequencyValue} /> : null}
          </Field>
          <Field label="PM Frequency Unit">
            <select value={form.pmFrequencyUnit || 'months'} onChange={(event) => setForm((current) => ({ ...current, pmFrequencyUnit: event.target.value }))} className="field-input">
              <option value="days">days</option>
              <option value="weeks">weeks</option>
              <option value="months">months</option>
              <option value="quarters">quarters</option>
              <option value="years">years</option>
            </select>
            {fieldErrors.pmFrequencyUnit ? <FieldError message={fieldErrors.pmFrequencyUnit} /> : null}
          </Field>
          <Field label="Recommended PM frequency"><input value={form.recommendedPmFrequency || ''} onChange={(event) => setForm((current) => ({ ...current, recommendedPmFrequency: event.target.value }))} className="field-input" placeholder="Monthly, Quarterly..." /></Field>
          <Field label="Next PM Due Date">
            <input type="date" value={form.nextPmDate || ''} onChange={(event) => setForm((current) => ({ ...current, nextPmDate: event.target.value }))} className="field-input" />
            {fieldErrors.nextPmDate ? <FieldError message={fieldErrors.nextPmDate} /> : null}
          </Field>
        </div>

        {selectedCategory ? (
          <div className="mt-5 space-y-3 rounded-[24px] border border-app bg-subtle p-4" data-testid="asset-custom-fields-panel">
            <div>
              <p className="text-sm font-semibold text-app">Category-specific fields</p>
              <p className="mt-1 text-sm text-muted">Capture the structured data defined for {selectedCategory.name}.</p>
            </div>
            {selectedCategory.fields.length === 0 ? (
              <p className="text-sm text-muted">This category has no custom fields yet.</p>
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                {selectedCategory.fields.map((field) => {
                  const currentValue = form.customFieldValues?.find((item) => item.fieldDefinitionId === field.id)?.value ?? ''
                  return (
                    <Field key={field.id} label={`${field.fieldLabel}${field.isRequired ? ' *' : ''}`}>
                      {renderCustomFieldInput(field, currentValue, (value) => updateCustomFieldValue(field.id, value))}
                    </Field>
                  )
                })}
              </div>
            )}
          </div>
        ) : null}

        <label className="panel-subtle mt-4 flex items-center justify-between rounded-2xl px-4 py-3">
          <span className="text-sm text-app">Auto Schedule PM</span>
          <input type="checkbox" checked={form.autoSchedulePm} onChange={(event) => setForm((current) => ({ ...current, autoSchedulePm: event.target.checked }))} />
        </label>
        <div className="mt-4 rounded-2xl border border-app bg-app/20 px-4 py-3 text-sm text-muted">
          Next PM preview: <span className="font-medium text-app">{formatPmPreview(form.lastPmDate, form.pmFrequencyValue, form.pmFrequencyUnit, form.nextPmDate)}</span>
        </div>
        <Field label="Notes"><textarea value={form.notes || ''} onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))} className="field-input mt-4 min-h-[120px]" /></Field>
        {saveError ? <div className="mt-4 rounded-2xl border border-rose-400/30 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">{saveError}</div> : null}
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setEditorOpen(false)} disabled={saving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveAsset()} disabled={saving}>{saving ? 'Saving asset...' : 'Save asset'}</button>
        </StickyActionFooter>
      </Modal>

      <BulkImportModal
        open={importOpen}
        title="Bulk import assets"
        description="Upload a CSV file to preview and import the client, site, system, asset, and PM hierarchy."
        onClose={() => setImportOpen(false)}
        onPreview={(file) => importService.previewAssets(file)}
        onCommit={(file) => importService.commitAssets(file, { allowCreateSystems })}
        onCommitted={async (summary) => {
          pushToast({
            title: 'Asset import complete',
            description: `Imported ${summary.successfulRows} of ${summary.totalRows} rows.`,
            tone: summary.failedRows > 0 ? 'warning' : 'success',
          })
          await reload()
        }}
        instructions={[
          'Required: ClientName or ClientId, SiteName or SiteId, AssetName, and AssetCode.',
          'Optional system linkage: provide SystemName or SystemId. SystemName resolves under the selected site.',
          'PM fields supported: LastPmServiceDate, PmFrequency, PmFrequencyUnit, NextPmDueDate, and AutoSchedulePm.',
          'BranchName and BranchId are included for branch-scoped imports where available.',
        ]}
        optionsSlot={(
          <label className="flex items-center justify-between gap-3 text-sm text-app">
            <span>AllowCreateSystems</span>
            <input type="checkbox" checked={allowCreateSystems} onChange={(event) => setAllowCreateSystems(event.target.checked)} />
          </label>
        )}
      />
      <Modal
        open={Boolean(docsAsset)}
        title={`Documents - ${docsAsset?.assetName ?? ''}`}
        description="Upload and manage files attached to this asset."
        onClose={() => setDocsAsset(null)}
        maxWidth="max-w-2xl"
      >
        {docsAsset ? (
          <AttachmentPanel
            entityType={ATTACHMENT_ENTITY_TYPES.Asset}
            entityId={docsAsset.id}
            attachments={assetAttachments}
            onUploaded={(a) => setAssetAttachments((prev) => [...prev, a])}
            onDeleted={(id) => setAssetAttachments((prev) => prev.filter((a) => a.id !== id))}
          />
        ) : null}
      </Modal>
    </PageScaffold>
  )
}

function renderCustomFieldInput(
  field: AssetCategoryRecord['fields'][number],
  value: string,
  onChange: (value: string) => void,
) {
  if (field.fieldType === 'Dropdown') {
    const options = (field.dropdownOptions ?? '')
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean)

    return (
      <select value={value} onChange={(event) => onChange(event.target.value)} className="field-input">
        <option value="">Select option</option>
        {options.map((option) => (
          <option key={option} value={option}>{option}</option>
        ))}
      </select>
    )
  }

  if (field.fieldType === 'Boolean') {
    return (
      <select value={value} onChange={(event) => onChange(event.target.value)} className="field-input">
        <option value="">Select</option>
        <option value="true">Yes</option>
        <option value="false">No</option>
      </select>
    )
  }

  if (field.fieldType === 'Date') {
    return <input type="date" value={value} onChange={(event) => onChange(event.target.value)} className="field-input" />
  }

  if (field.fieldType === 'TextArea' || field.fieldType === 'MultilineText') {
    return <textarea value={value} onChange={(event) => onChange(event.target.value)} className="field-input min-h-[110px]" />
  }

  return (
    <div className="space-y-1">
      <input
        type={field.fieldType === 'Number' ? 'number' : 'text'}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        className="field-input"
      />
      {field.unit ? <p className="text-xs text-muted">Unit: {field.unit}</p> : null}
    </div>
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

function FieldError({ message }: { message: string }) {
  return <p className="text-sm text-rose-200">{message}</p>
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-app bg-app/20 px-3 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 break-words text-sm text-app">{value}</p>
    </div>
  )
}

function extractAssetErrors(error: unknown): { formError: string; fieldErrors: AssetFieldErrors } {
  if (!(error instanceof ApiError) || !error.details || typeof error.details !== 'object') {
    return {
      formError: error instanceof Error ? error.message : 'Unable to save asset.',
      fieldErrors: {},
    }
  }

  const record = error.details as Record<string, unknown>
  const errorMap = (record.errors && typeof record.errors === 'object' ? record.errors : {}) as Record<string, unknown>
  const fieldErrors: AssetFieldErrors = {}

  for (const [key, value] of Object.entries(errorMap)) {
    const firstMessage = Array.isArray(value) ? value.find((item) => typeof item === 'string') : null
    if (typeof firstMessage !== 'string' || !firstMessage.trim()) continue

    const normalizedKey = key.toLowerCase()
    if (normalizedKey.includes('clientid')) fieldErrors.clientId = firstMessage
    else if (normalizedKey.includes('assetcategoryid')) fieldErrors.assetCategoryId = firstMessage
    else if (normalizedKey.includes('assetname')) fieldErrors.assetName = firstMessage
    else if (normalizedKey.includes('assetcode')) fieldErrors.assetCode = firstMessage
    else if (normalizedKey.includes('assettype')) fieldErrors.assetType = firstMessage
    else if (normalizedKey.includes('ownershiptype')) fieldErrors.ownershipType = firstMessage
    else if (normalizedKey.includes('installationdate')) fieldErrors.installationDate = firstMessage
    else if (normalizedKey.includes('pmfrequencyvalue')) fieldErrors.pmFrequencyValue = firstMessage
    else if (normalizedKey.includes('pmfrequencyunit')) fieldErrors.pmFrequencyUnit = firstMessage
    else if (normalizedKey.includes('warrantyexpirydate')) fieldErrors.warrantyExpiryDate = firstMessage
    else if (normalizedKey.includes('lastpmdate')) fieldErrors.lastPmDate = firstMessage
    else if (normalizedKey.includes('nextpmdate')) fieldErrors.nextPmDate = firstMessage
    else if (normalizedKey.includes('status')) fieldErrors.status = firstMessage
  }

  return {
    formError: error.message || 'Unable to save asset.',
    fieldErrors,
  }
}

function formatPmPreview(lastPmDate?: string | null, frequencyValue?: number | null, frequencyUnit?: string | null, nextPmDate?: string | null) {
  if (nextPmDate) {
    return formatDateOnly(nextPmDate)
  }

  if (!lastPmDate || !frequencyValue || !frequencyUnit) {
    return 'Not scheduled'
  }

  const base = new Date(lastPmDate)
  if (Number.isNaN(base.getTime())) {
    return 'Invalid PM inputs'
  }

  const unit = frequencyUnit.toLowerCase()
  if (unit === 'days') base.setUTCDate(base.getUTCDate() + frequencyValue)
  else if (unit === 'weeks') base.setUTCDate(base.getUTCDate() + (frequencyValue * 7))
  else if (unit === 'quarters') base.setUTCMonth(base.getUTCMonth() + (frequencyValue * 3))
  else if (unit === 'years') base.setUTCFullYear(base.getUTCFullYear() + frequencyValue)
  else base.setUTCMonth(base.getUTCMonth() + frequencyValue)

  return formatDateOnly(base.toISOString())
}
