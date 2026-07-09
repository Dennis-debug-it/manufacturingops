import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import {
  AlertTriangle, Building2, Download, Edit2, FileText, Layers, Mail, MapPin, Plus,
  Search, Wrench, X, ExternalLink,
} from 'lucide-react'
import { EmailStatementDrawer } from '../tenant-finance/EmailStatementDrawer'
import { statementService } from '../../services/statementService'
import { AttachmentPanel } from '../../components/ui/AttachmentPanel'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import {
  MetricCard, MetricGrid, PageScaffold, SectionCard, StickyActionFooter,
} from '../../components/ui/Workspace'
import { useShellContext } from '../../components/layout/AppShell'
import { statusTone, type WorkflowStatus } from '../work-orders/workOrderWorkflowUi'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetCategoryService, type AssetCategoryRecord } from '../../services/assetCategoryService'
import { assetService } from '../../services/assetService'
import { attachmentService, ATTACHMENT_ENTITY_TYPES } from '../../services/attachmentService'
import { clientService } from '../../services/clientService'
import { creditTermService } from '../../services/creditTermService'
import { siteService } from '../../services/siteService'
import { slaService } from '../../services/slaService'
import { systemService } from '../../services/systemService'
import { workOrderService } from '../../services/workOrderService'
import { contractService } from '../../services/contractService'
import type {
  AssetRecord,
  AttachmentRecord,
  ClientCreditSummaryRecord,
  ContractListItem,
  ClientRecord,
  ClientSummaryRecord,
  SiteDetailRecord,
  SiteRecord,
  SlaDefinitionRecord,
  SystemRecord,
  UpsertAssetInput,
  UpsertClientInput,
  UpsertSiteInput,
  WorkOrder,
} from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { renderBillingModelBadge, renderContractStatusBadge, renderRenewalRiskBadge } from '../contracts/contractUi'

// ─────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────
type Tab = 'overview' | 'contracts' | 'sites-assets' | 'work-orders' | 'documents'

type ClientWorkspaceProps = {
  clientId: string
  embedded?: boolean
  onBackHref?: string | null
  initialTab?: Tab
  initialSiteId?: string | null
}

const TABS: { id: Tab; label: string; icon: React.ElementType }[] = [
  { id: 'overview', label: 'Overview', icon: Building2 },
  { id: 'contracts', label: 'Contracts', icon: FileText },
  { id: 'sites-assets', label: 'Sites & Assets', icon: Layers },
  { id: 'work-orders', label: 'Work Orders', icon: Wrench },
  { id: 'documents', label: 'Documents', icon: FileText },
]

// ─────────────────────────────────────────────
// Form defaults
// ─────────────────────────────────────────────
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

function emptyAssetForm(clientId: string, siteId?: string | null): UpsertAssetInput {
  return {
    clientId,
    branchId: null,
    siteId: siteId ?? null,
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
  }
}

// ─────────────────────────────────────────────
// Main page
// ─────────────────────────────────────────────
function renderClientStatusBadge(client: { activeStatus?: string | null; isActive?: boolean | null }) {
  const normalizedStatus = (client.activeStatus || (client.isActive ? 'active' : 'inactive')).toLowerCase()
  if (normalizedStatus === 'expired') return <Badge tone="danger">Expired</Badge>
  if (normalizedStatus === 'expiring') return <Badge tone="warning">Expiring Soon</Badge>
  if (normalizedStatus === 'inactive' || normalizedStatus === 'suspended') return <Badge tone="neutral">{normalizedStatus === 'suspended' ? 'Suspended' : 'Inactive'}</Badge>
  return <Badge tone="success">Active</Badge>
}

export function ClientDetailPage() {
  const { clientId = '' } = useParams<{ clientId: string }>()
  return <ClientWorkspace clientId={clientId} />
}

export function ClientWorkspace({
  clientId,
  embedded = false,
  onBackHref = '/clients',
  initialTab = 'overview',
  initialSiteId = null,
}: ClientWorkspaceProps) {
  const navigate = useNavigate()
  const { pushToast } = useToast()
  const { selectedBranchId } = useShellContext()

  const [activeTab, setActiveTab] = useState<Tab>(initialTab)

  // ── Client summary ──
  const { data: summary, loading, error, reload: reloadSummary } = useAsyncData<ClientSummaryRecord | null>(
    (signal) =>
      clientId
        ? clientService.getSummary(clientId, signal)
        : Promise.resolve(null),
    null,
    [clientId],
  )

  // ── Credit summary ──
  const { data: creditSummary } = useAsyncData<ClientCreditSummaryRecord | null>(
    (signal) =>
      clientId && activeTab === 'overview'
        ? creditTermService.getClientCreditSummary(clientId, signal)
        : Promise.resolve(null),
    null,
    [clientId, activeTab],
  )

  // ── SLA definitions (for client editor) ──
  const { data: slaDefinitions } = useAsyncData<SlaDefinitionRecord[]>(
    (signal) => slaService.list(signal),
    [],
    [],
  )

  // ── Asset categories (for asset editor) ──
  const { data: assetCategories } = useAsyncData<AssetCategoryRecord[]>(
    (signal) => assetCategoryService.list(signal),
    [],
    [],
  )

  // ── Work orders (lazy – loaded when tab is active) ──
  const [woTabLoaded, setWoTabLoaded] = useState(false)
  const { data: allWorkOrders, loading: woLoading, error: woError } = useAsyncData<WorkOrder[]>(
    (signal) => woTabLoaded ? workOrderService.list(null, signal) : Promise.resolve([]),
    [],
    [woTabLoaded],
  )
  const clientWorkOrders = useMemo(
    () => allWorkOrders.filter((wo) => wo.clientId === clientId),
    [allWorkOrders, clientId],
  )

  const [contractTabLoaded, setContractTabLoaded] = useState(false)
  const { data: clientContracts, loading: contractsLoading, error: contractsError } = useAsyncData<ContractListItem[]>(
    (signal) => contractTabLoaded || activeTab === 'overview' ? contractService.listClientContracts(clientId, signal) : Promise.resolve([]),
    [],
    [clientId, contractTabLoaded, activeTab],
  )

  // ── Documents (lazy) ──
  const [docsLoaded, setDocsLoaded] = useState(false)
  const [clientAttachments, setClientAttachments] = useState<AttachmentRecord[]>([])
  useEffect(() => {
    if (!docsLoaded || !clientId) return
    let active = true
    attachmentService.list(ATTACHMENT_ENTITY_TYPES.Client, clientId)
      .then((result) => { if (active) setClientAttachments(result) })
      .catch(() => { if (active) setClientAttachments([]) })
    return () => { active = false }
  }, [docsLoaded, clientId])

  function handleTabChange(tab: Tab) {
    setActiveTab(tab)
    if (tab === 'work-orders') setWoTabLoaded(true)
    if (tab === 'contracts') setContractTabLoaded(true)
    if (tab === 'documents') setDocsLoaded(true)
  }

  useEffect(() => {
    setActiveTab(initialTab)
  }, [clientId, initialTab])

  // ── Client editor ──
  const [clientEditorOpen, setClientEditorOpen] = useState(false)
  const [clientForm, setClientForm] = useState<UpsertClientInput>({
    clientName: '', clientType: '', email: '', phone: '', location: '',
    contactPerson: '', contactPhone: '', slaDefinitionId: '',
    activeStartDate: '', activeDurationValue: null, activeDurationUnit: 'months', notes: '',
  })

  function openClientEditor() {
    if (!summary?.client) return
    const c = summary.client
    setClientForm({
      clientName: c.clientName ?? '',
      clientType: c.clientType ?? '',
      email: c.email ?? '',
      phone: c.phone ?? '',
      location: c.location ?? '',
      contactPerson: c.contactPerson ?? '',
      contactPhone: c.contactPhone ?? '',
      slaDefinitionId: c.slaDefinitionId ?? '',
      activeStartDate: c.activeStartDate?.slice(0, 10) ?? '',
      activeDurationValue: c.activeDurationValue ?? null,
      activeDurationUnit: c.activeDurationUnit ?? 'months',
      notes: c.notes ?? '',
    })
    setClientEditorOpen(true)
  }

  async function saveClient() {
    if (!clientId) return
    try {
      await clientService.update(clientId, clientForm)
      pushToast({ title: 'Client updated', description: 'Changes saved.', tone: 'success' })
      setClientEditorOpen(false)
      await reloadSummary()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    }
  }

  // ── Site editor ──
  const [siteEditorOpen, setSiteEditorOpen] = useState(false)
  const [editingSite, setEditingSite] = useState<SiteRecord | null>(null)
  const [siteForm, setSiteForm] = useState<UpsertSiteInput>(emptySiteForm)
  const [siteSaving, setSiteSaving] = useState(false)
  const [siteAssetsRefreshKey, setSiteAssetsRefreshKey] = useState(0)
  const [siteEditorDefaultSiteId, setSiteEditorDefaultSiteId] = useState<string | null>(null)

  function openSiteEditor(site?: SiteRecord) {
    setEditingSite(site ?? null)
    setSiteForm(site ? {
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
    setSiteEditorOpen(true)
  }

  async function saveSite() {
    if (!clientId || siteSaving) return
    setSiteSaving(true)
    try {
      let nextSelectedSiteId = siteEditorDefaultSiteId
      if (editingSite) {
        await siteService.update(clientId, editingSite.id, siteForm)
        pushToast({ title: 'Site updated', description: 'Site details saved.', tone: 'success' })
        nextSelectedSiteId = editingSite.id
      } else {
        const created = await siteService.create(clientId, siteForm)
        pushToast({ title: 'Site added', description: 'New site created.', tone: 'success' })
        setSiteEditorDefaultSiteId(created.id)
        nextSelectedSiteId = created.id
      }
      setSiteEditorOpen(false)
      await reloadSummary()
      setSiteAssetsRefreshKey((current) => current + 1)
      setSelectedSiteId((prev) => prev ?? nextSelectedSiteId)
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    } finally {
      setSiteSaving(false)
    }
  }

  async function deactivateSite(site: SiteRecord) {
    if (!clientId) return
    if (!window.confirm(`Deactivate site "${site.siteName}"?`)) return
    try {
      await siteService.deactivate(clientId, site.id)
      pushToast({ title: 'Site deactivated', description: 'Site is now inactive.', tone: 'success' })
      await reloadSummary()
      setSiteAssetsRefreshKey((current) => current + 1)
    } catch (err) {
      pushToast({ title: 'Failed', description: err instanceof Error ? err.message : 'Unable to deactivate.', tone: 'danger' })
    }
  }

  // ── Sites panel selection ──
  const [selectedSiteId, setSelectedSiteId] = useState<string | null>(initialSiteId)
  const [siteSearch, setSiteSearch] = useState('')

  // ── Asset editor ──
  const [assetEditorOpen, setAssetEditorOpen] = useState(false)
  const [editingAsset, setEditingAsset] = useState<AssetRecord | null>(null)
  const [assetForm, setAssetForm] = useState<UpsertAssetInput>(emptyAssetForm(clientId ?? ''))
  const [assetSaving, setAssetSaving] = useState(false)
  const [assetSaveError, setAssetSaveError] = useState('')
  const [assetEditorSites, setAssetEditorSites] = useState<SiteRecord[]>([])
  const [assetEditorSystems, setAssetEditorSystems] = useState<SystemRecord[]>([])

  useEffect(() => {
    if (!assetEditorOpen || !assetForm.clientId) { setAssetEditorSites([]); return }
    let cancelled = false
    siteService.list(assetForm.clientId, { status: 'Active' })
      .then((result) => { if (!cancelled) setAssetEditorSites(result) })
      .catch(() => { if (!cancelled) setAssetEditorSites([]) })
    return () => { cancelled = true }
  }, [assetEditorOpen, assetForm.clientId])

  useEffect(() => {
    if (!assetEditorOpen || !assetForm.clientId || !assetForm.siteId) {
      setAssetEditorSystems([])
      return
    }
    let cancelled = false
    systemService.list(assetForm.clientId, assetForm.siteId)
      .then((result) => { if (!cancelled) setAssetEditorSystems(result) })
      .catch(() => { if (!cancelled) setAssetEditorSystems([]) })
    return () => { cancelled = true }
  }, [assetEditorOpen, assetForm.clientId, assetForm.siteId])

  function syncAssetCustomFields(categoryId: string | null | undefined, existing?: Array<{ fieldDefinitionId: string; value: string }>) {
    const category = assetCategories.find((c) => c.id === categoryId)
    if (!category) return []
    return category.fields.map((field) => ({
      fieldDefinitionId: field.id,
      value: existing?.find((v) => v.fieldDefinitionId === field.id)?.value ?? '',
    }))
  }

  function openAssetEditor(asset?: AssetRecord, prefillSiteId?: string | null) {
    setEditingAsset(asset ?? null)
    setAssetSaveError('')
    const catId = asset?.assetCategoryId ?? null
    setAssetForm({
      clientId: asset?.clientId ?? clientId ?? '',
      branchId: asset?.branchId ?? (selectedBranchId === 'all' ? null : selectedBranchId),
      siteId: asset?.siteId ?? prefillSiteId ?? null,
      systemId: asset?.systemId ?? null,
      assetCategoryId: catId,
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
      customFieldValues: asset
        ? syncAssetCustomFields(catId, (asset.customFieldValues ?? []).map((v) => ({ fieldDefinitionId: v.fieldDefinitionId, value: v.value })))
        : [],
    })
    setAssetEditorOpen(true)
  }

  async function saveAsset(onSuccess?: () => void) {
    if (assetSaving) return
    setAssetSaving(true)
    setAssetSaveError('')
    if (!assetForm.assetName.trim()) {
      setAssetSaveError('Asset name is required.')
      setAssetSaving(false)
      return
    }
    if (!assetForm.siteId) {
      setAssetSaveError('Select the client site where this asset should be linked.')
      setAssetSaving(false)
      return
    }
    try {
      const nextSiteId = assetForm.siteId
      if (editingAsset) {
        await assetService.update(editingAsset.id, assetForm)
        pushToast({ title: 'Asset updated', description: 'Changes saved.', tone: 'success' })
      } else {
        await assetService.create(assetForm)
        pushToast({ title: 'Asset created', description: 'Asset added to register.', tone: 'success' })
      }
      setAssetEditorOpen(false)
      await reloadSummary()
      setSiteAssetsRefreshKey((current) => current + 1)
      if (nextSiteId) {
        setSelectedSiteId(nextSiteId)
      }
      onSuccess?.()
    } catch (err) {
      setAssetSaveError(err instanceof Error ? err.message : 'Unable to save asset.')
    } finally {
      setAssetSaving(false)
    }
  }

  async function deactivateAsset(assetId: string, assetName: string) {
    if (!window.confirm(`Disable asset "${assetName}"?`)) return
    try {
      await assetService.deactivate(assetId)
      pushToast({ title: 'Asset disabled', description: 'The asset is now inactive and removed from active scheduling.', tone: 'success' })
      await reloadSummary()
      setSiteAssetsRefreshKey((current) => current + 1)
    } catch (err) {
      pushToast({ title: 'Disable failed', description: err instanceof Error ? err.message : 'Unable to disable asset.', tone: 'danger' })
    }
  }

  // ── Asset slide-over ──
  const [slideOverAssetId, setSlideOverAssetId] = useState<string | null>(null)
  const { data: slideOverAsset, loading: slideOverLoading } = useAsyncData<AssetRecord | null>(
    (signal) =>
      slideOverAssetId ? assetService.get(slideOverAssetId, signal) : Promise.resolve(null),
    null,
    [slideOverAssetId],
  )

  // ── Statement ──
  const [statementDrawerOpen, setStatementDrawerOpen] = useState(false)
  const [downloadingStatement, setDownloadingStatement] = useState(false)

  async function downloadStatement() {
    if (!clientId || downloadingStatement) return
    setDownloadingStatement(true)
    try {
      const blob = await statementService.previewStatementPdf(clientId)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `statement_${client.clientName.replace(/\s+/g, '_')}_${new Date().toISOString().slice(0, 10)}.pdf`
      a.click()
      setTimeout(() => URL.revokeObjectURL(url), 10_000)
    } catch (err) {
      pushToast({ title: 'Download failed', description: err instanceof Error ? err.message : 'Unable to download.', tone: 'danger' })
    } finally {
      setDownloadingStatement(false)
    }
  }

  // ── WO tab filters ──
  const [woStatusFilter, setWoStatusFilter] = useState('')
  const [woSiteFilter, setWoSiteFilter] = useState('')

  useEffect(() => {
    setSelectedSiteId(initialSiteId)
    setSiteSearch('')
    setWoStatusFilter('')
    setWoSiteFilter('')
  }, [clientId, initialSiteId])

  const filteredWorkOrders = useMemo(() => {
    return clientWorkOrders.filter((wo) => {
      if (woStatusFilter && wo.status !== woStatusFilter) return false
      if (woSiteFilter && wo.siteId !== woSiteFilter) return false
      return true
    })
  }, [clientWorkOrders, woStatusFilter, woSiteFilter])

  // ── Loading / error states ──
  if (loading) return <LoadingState label="Loading client" />
  if (error || !summary) return <ErrorState title="Unable to load client" description={error || 'Client summary unavailable.'} />

  const client = summary.client

  const workspace = (
    <div className="space-y-5">
      {/* ── Client header card ── */}
      <ClientHeader
        client={client}
        onEdit={openClientEditor}
        onAddSite={() => { handleTabChange('sites-assets'); openSiteEditor() }}
        onNewWorkOrder={() => void navigate('/work-orders')}
      />

      {/* ── Tab bar ── */}
      <div className="panel-subtle flex gap-1 rounded-full p-1.5">
        {TABS.map((tab) => {
          const Icon = tab.icon
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => handleTabChange(tab.id)}
              className={`flex flex-1 items-center justify-center gap-2 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors sm:flex-none sm:px-5 ${
                activeTab === tab.id
                  ? 'bg-accent text-white shadow-none'
                  : 'text-muted hover:text-app'
              }`}
            >
              <Icon className="h-4 w-4 shrink-0" />
              <span className="hidden sm:inline">{tab.label}</span>
            </button>
          )
        })}
      </div>

      {/* ── Tab content ── */}
      {activeTab === 'overview' && (
        <OverviewTab
          summary={summary}
          creditSummary={creditSummary}
          contracts={clientContracts}
          onEmailStatement={() => setStatementDrawerOpen(true)}
          onDownloadStatement={() => void downloadStatement()}
          downloadingStatement={downloadingStatement}
        />
      )}

      {activeTab === 'contracts' && (
        <ContractsTab
          loading={contractsLoading}
          error={contractsError}
          contracts={clientContracts}
        />
      )}

      {activeTab === 'sites-assets' && (
        <SitesAssetsTab
          clientId={clientId ?? ''}
          summary={summary}
          refreshKey={siteAssetsRefreshKey}
          selectedSiteId={selectedSiteId}
          onSelectSite={setSelectedSiteId}
          siteSearch={siteSearch}
          onSiteSearchChange={setSiteSearch}
          onOpenSiteEditor={openSiteEditor}
          onDeactivateSite={deactivateSite}
          onOpenAssetEditor={openAssetEditor}
          onDeactivateAsset={deactivateAsset}
          onViewAsset={(assetId) => setSlideOverAssetId(assetId)}
        />
      )}

      {activeTab === 'work-orders' && (
        <WorkOrdersTab
          loading={woLoading}
          error={woError}
          workOrders={filteredWorkOrders}
          allWorkOrders={clientWorkOrders}
          sites={summary.sites}
          statusFilter={woStatusFilter}
          siteFilter={woSiteFilter}
          onStatusFilterChange={setWoStatusFilter}
          onSiteFilterChange={setWoSiteFilter}
          clientId={clientId ?? ''}
        />
      )}

      {activeTab === 'documents' && clientId && (
        <SectionCard title="Documents" description="Client contracts, SLAs, and supporting documents.">
          <AttachmentPanel
            entityType={ATTACHMENT_ENTITY_TYPES.Client}
            entityId={clientId}
            attachments={clientAttachments}
            onUploaded={(a) => setClientAttachments((prev) => [...prev, a])}
            onDeleted={(id) => setClientAttachments((prev) => prev.filter((a) => a.id !== id))}
          />
        </SectionCard>
      )}

      {/* ── Client editor drawer ── */}
      <Drawer
        open={clientEditorOpen}
        title="Edit client"
        description="Update client details, contract dates, and response targets."
        onClose={() => setClientEditorOpen(false)}
      >
        <ClientEditorForm
          form={clientForm}
          onChange={setClientForm}
          slaDefinitions={slaDefinitions}
        />
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setClientEditorOpen(false)}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveClient()}>Save client</button>
        </StickyActionFooter>
      </Drawer>

      {/* ── Site editor drawer ── */}
      <Drawer
        open={siteEditorOpen}
        title={editingSite ? 'Edit site' : 'Add site'}
        description={`${editingSite ? 'Update' : 'Create'} a site for ${client.clientName}.`}
        onClose={() => setSiteEditorOpen(false)}
      >
        <SiteEditorForm form={siteForm} onChange={setSiteForm} />
        <StickyActionFooter className="mt-4">
          <button type="button" className="button-secondary" onClick={() => setSiteEditorOpen(false)} disabled={siteSaving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveSite()} disabled={siteSaving}>
            {siteSaving ? 'Saving...' : 'Save site'}
          </button>
        </StickyActionFooter>
      </Drawer>

      {/* ── Asset editor modal ── */}
      <Modal
        open={assetEditorOpen}
        title={editingAsset ? 'Edit asset' : 'Add asset'}
        description="Capture asset details, PM schedule, and category-specific fields."
        onClose={() => setAssetEditorOpen(false)}
        maxWidth="max-w-4xl"
      >
        <AssetEditorForm
          form={assetForm}
          onChange={setAssetForm}
          sites={assetEditorSites}
          systems={assetEditorSystems}
          categories={assetCategories}
          saveError={assetSaveError}
          onSyncCustomFields={(catId) =>
            setAssetForm((f) => ({
              ...f,
              assetCategoryId: catId,
              customFieldValues: syncAssetCustomFields(catId, (f.customFieldValues ?? []).map((v) => ({ fieldDefinitionId: v.fieldDefinitionId, value: v.value ?? '' }))),
            }))
          }
        />
        <StickyActionFooter className="mt-6">
          <button type="button" className="button-secondary" onClick={() => setAssetEditorOpen(false)} disabled={assetSaving}>Cancel</button>
          <button type="button" className="button-primary" onClick={() => void saveAsset()} disabled={assetSaving}>
            {assetSaving ? 'Saving...' : 'Save asset'}
          </button>
        </StickyActionFooter>
      </Modal>

      {/* ── Asset slide-over ── */}
      {slideOverAssetId && (
        <AssetSlideOver
          asset={slideOverAsset}
          loading={slideOverLoading}
          workOrders={clientWorkOrders.filter((wo) => wo.assetId === slideOverAssetId)}
          onClose={() => setSlideOverAssetId(null)}
          onEdit={(asset) => {
            setSlideOverAssetId(null)
            openAssetEditor(asset)
          }}
        />
      )}

      {/* ── Email statement drawer ── */}
      <EmailStatementDrawer
        open={statementDrawerOpen}
        onClose={() => setStatementDrawerOpen(false)}
        clientId={clientId ?? ''}
        clientName={client.clientName}
        clientEmail={client.email}
      />
    </div>
  )

  if (embedded) {
    return workspace
  }

  return (
    <PageScaffold
      eyebrow="Clients"
      title={client.clientName}
      description={`${client.clientType || 'Client'} · ${client.location || 'No location set'}`}
      actions={
        onBackHref ? (
          <Link to={onBackHref} className="button-secondary">
            Back to clients
          </Link>
        ) : null
      }
    >
      {workspace}
    </PageScaffold>
  )
}

// ─────────────────────────────────────────────
// Client header card
// ─────────────────────────────────────────────
function ClientHeader({
  client,
  onEdit,
  onAddSite,
  onNewWorkOrder,
}: {
  client: ClientRecord
  onEdit: () => void
  onAddSite: () => void
  onNewWorkOrder: () => void
}) {
  return (
    <div className="surface-card">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-[14px] bg-accent/10 text-accent-strong">
            <Building2 className="h-7 w-7" />
          </div>
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <h2 className="text-xl font-bold text-app">{client.clientName}</h2>
              {renderClientStatusBadge(client)}
            </div>
            {client.clientType && <p className="mt-0.5 text-sm text-muted">{client.clientType}</p>}
            <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted">
              {client.contactPerson && <span>👤 {client.contactPerson}</span>}
              {client.email && <span>✉ {client.email}</span>}
              {(client.phone || client.contactPhone) && <span>📞 {client.contactPhone || client.phone}</span>}
              {client.location && <span className="flex items-center gap-1"><MapPin className="h-3.5 w-3.5" />{client.location}</span>}
            </div>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <button type="button" className="button-secondary" onClick={onEdit}>
            <Edit2 className="h-4 w-4" />
            Edit client
          </button>
          <button type="button" className="button-secondary" onClick={onAddSite}>
            <Plus className="h-4 w-4" />
            Add site
          </button>
          <button type="button" className="button-primary" onClick={onNewWorkOrder}>
            <Wrench className="h-4 w-4" />
            New work order
          </button>
        </div>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────
// Overview tab
// ─────────────────────────────────────────────
function OverviewTab({
  summary,
  creditSummary,
  contracts,
  onEmailStatement,
  onDownloadStatement,
  downloadingStatement,
}: {
  summary: ClientSummaryRecord
  creditSummary: ClientCreditSummaryRecord | null
  contracts: ContractListItem[]
  onEmailStatement: () => void
  onDownloadStatement: () => void
  downloadingStatement: boolean
}) {
  const client = summary.client
  const overLimit =
    creditSummary?.creditLimit != null &&
    creditSummary.currentBalance > creditSummary.creditLimit
  const riskyContracts = contracts.filter((item) => item.status === 'ExpiringSoon' || item.status === 'Expired' || item.status === 'Suspended' || item.status === 'Terminated')

  return (
    <div className="space-y-3">
      <MetricGrid className="xl:grid-cols-5">
        <MetricCard label="Sites" value={summary.siteCount} meta="Client locations" emphasis="accent" />
        <MetricCard label="Systems" value={summary.systemCount} meta="Operational groupings" />
        <MetricCard label="Assets" value={summary.assetCount} meta="Serviceable items" />
        <MetricCard label="Open Work Orders" value={summary.openWorkOrderCount} meta="Active service demand" />
        <MetricCard label="Upcoming PMs" value={summary.upcomingPmCount} meta="Due in 30 days" />
      </MetricGrid>

      {creditSummary && (
        <SectionCard
          title="Credit & billing"
          description={
            creditSummary.termName
              ? `Terms: ${creditSummary.termName}`
              : 'No credit terms assigned'
          }
        >
          {overLimit && (
            <div className="mb-4 flex items-start gap-3 rounded-[14px] bg-danger/10 p-4">
              <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0 text-danger" />
              <p className="text-sm text-danger font-medium">
                Credit limit exceeded — balance {creditSummary.currentBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })} exceeds limit of {creditSummary.creditLimit!.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </p>
            </div>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Detail
              label="Outstanding balance"
              value={`KES ${creditSummary.currentBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}`}
            />
            <Detail
              label="Credit limit"
              value={
                creditSummary.creditLimit != null
                  ? `KES ${creditSummary.creditLimit.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                  : 'No limit set'
              }
            />
            <Detail
              label="Available credit"
              value={
                creditSummary.availableCredit != null
                  ? `KES ${creditSummary.availableCredit.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                  : '—'
              }
            />
            <Detail
              label="Overdue amount"
              value={
                creditSummary.overdueAmount > 0
                  ? `KES ${creditSummary.overdueAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                  : 'None'
              }
            />
          </div>
          {creditSummary.oldestInvoiceDate && (
            <p className="mt-3 text-xs text-muted">
              Oldest unpaid invoice: {formatDateOnly(creditSummary.oldestInvoiceDate)}
            </p>
          )}
          <div className="mt-4 flex flex-wrap gap-2">
            <button
              type="button"
              className="button-primary flex items-center gap-2"
              onClick={onEmailStatement}
            >
              <Mail className="h-4 w-4" />
              Email Statement
            </button>
            <button
              type="button"
              className="button-secondary flex items-center gap-2"
              onClick={onDownloadStatement}
              disabled={downloadingStatement}
            >
              <Download className="h-4 w-4" />
              {downloadingStatement ? 'Downloading…' : 'Download Statement'}
            </button>
          </div>
        </SectionCard>
      )}

      {riskyContracts.length > 0 ? (
        <SectionCard title="Contract warning" description="Active client contracts that may affect service coverage and billing.">
          <div className="space-y-3">
            {riskyContracts.map((item) => (
              <div key={item.id} className="rounded-[14px] bg-amber-500/10 px-4 py-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.contractNumber}</Link>
                    <p className="mt-1 text-sm text-secondary">{item.name}</p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {renderContractStatusBadge(item.status)}
                    {renderRenewalRiskBadge(item.renewalRisk)}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </SectionCard>
      ) : null}

      <SectionCard title="Client profile" description="Contract, contact, and account details.">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Detail label="Client type" value={client.clientType || '-'} />
          <Detail label="Location" value={client.location || '-'} />
          <Detail label="Contact person" value={client.contactPerson || '-'} />
          <Detail label="Email" value={client.email || '-'} />
          <Detail label="Phone" value={client.phone || client.contactPhone || '-'} />
          <Detail label="SLA / Response targets" value={client.slaDefinitionName || 'Not set'} />
          <Detail label="Contract start" value={client.activeStartDate ? formatDateOnly(client.activeStartDate) : '-'} />
          <Detail label="Contract end" value={client.activeEndDate ? formatDateOnly(client.activeEndDate) : '-'} />
          <Detail label="Status" value={client.activeStatus || (client.isActive ? 'Active' : 'Inactive')} />
        </div>
        {client.notes && (
          <div className="mt-4 rounded-[14px] bg-subtle p-4">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Notes</p>
            <p className="mt-2 text-sm text-app whitespace-pre-wrap">{client.notes}</p>
          </div>
        )}
      </SectionCard>

      <SectionCard title="Sites" description="Locations registered for this client.">
        <DataTable
          rows={summary.sites}
          rowKey={(row) => row.siteId}
          pageSize={5}
          emptyTitle="No sites yet"
          emptyDescription="Add the first site from the Sites & Assets tab."
          mobileCard={(row) => (
            <div className="space-y-2 border-t border-[var(--app-divider)] pt-4 first:border-t-0 first:pt-0">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-semibold text-app">{row.siteName}</p>
                  <p className="text-xs text-muted">{row.siteType}</p>
                </div>
                <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge>
              </div>
              <div className="grid gap-2 grid-cols-3">
                <Detail label="Systems" value={String(row.systemCount)} />
                <Detail label="Assets" value={String(row.assetCount)} />
                <Detail label="Open WOs" value={String(row.openWorkOrderCount)} />
              </div>
            </div>
          )}
          columns={[
            {
              key: 'site', header: 'Site',
              cell: (row) => (
                <div>
                  <p className="font-semibold text-app">{row.siteName}</p>
                  <p className="text-xs text-muted">{row.siteCode} · {row.siteType}</p>
                </div>
              ),
            },
            { key: 'location', header: 'Location', cell: (row) => row.location || '-' },
            { key: 'contact', header: 'Contact', cell: (row) => row.contactPerson || '-' },
            { key: 'systems', header: 'Systems', cell: (row) => String(row.systemCount) },
            { key: 'assets', header: 'Assets', cell: (row) => String(row.assetCount) },
            { key: 'workOrders', header: 'Open WOs', cell: (row) => String(row.openWorkOrderCount) },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
          ]}
        />
      </SectionCard>
    </div>
  )
}

function ContractsTab({
  loading,
  error,
  contracts,
}: {
  loading: boolean
  error: string | null
  contracts: ContractListItem[]
}) {
  return (
    <SectionCard title="Contracts" description="Client-wide service coverage, renewal risk, and billing treatment.">
      {loading ? <LoadingState label="Loading contracts" /> : null}
      {!loading && error ? <ErrorState title="Unable to load contracts" description={error} /> : null}
      {!loading && !error ? (
        <DataTable
          rows={contracts}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No contracts yet"
          emptyDescription="Contracts linked to this client will appear here."
          mobileCard={(row) => (
            <div className="space-y-3 border-t border-[var(--app-divider)] pt-4 first:border-t-0 first:pt-0">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <Link to={`/contracts/${row.id}`} className="font-semibold text-accent-strong hover:underline">{row.contractNumber}</Link>
                  <p className="mt-1 text-sm text-app">{row.name}</p>
                </div>
                {renderContractStatusBadge(row.status)}
              </div>
              <div className="grid gap-2 sm:grid-cols-2">
                <Detail label="Type" value={row.contractType} />
                <Detail label="Ends" value={formatDateOnly(row.endDate)} />
                <Detail label="Risk" value={row.renewalRisk} />
                <Detail label="Billing" value={row.billingModel} />
              </div>
            </div>
          )}
          columns={[
            {
              key: 'contract',
              header: 'Contract',
              cell: (row) => (
                <div>
                  <Link to={`/contracts/${row.id}`} className="font-semibold text-accent-strong hover:underline">{row.contractNumber}</Link>
                  <p className="mt-1 text-xs text-muted">{row.name}</p>
                </div>
              ),
            },
            { key: 'type', header: 'Type', cell: (row) => row.contractType },
            { key: 'status', header: 'Status', cell: (row) => renderContractStatusBadge(row.status) },
            { key: 'end', header: 'End date', cell: (row) => formatDateOnly(row.endDate) },
            { key: 'risk', header: 'Renewal risk', cell: (row) => renderRenewalRiskBadge(row.renewalRisk) },
            { key: 'billing', header: 'Billing', cell: (row) => renderBillingModelBadge(row.billingModel) },
            { key: 'owner', header: 'Owner', cell: (row) => row.ownerName || '-' },
          ]}
        />
      ) : null}
    </SectionCard>
  )
}

// ─────────────────────────────────────────────
// Sites & Assets tab (split panel)
// ─────────────────────────────────────────────
function SitesAssetsTab({
  clientId,
  summary,
  refreshKey,
  selectedSiteId,
  onSelectSite,
  siteSearch,
  onSiteSearchChange,
  onOpenSiteEditor,
  onDeactivateSite,
  onOpenAssetEditor,
  onDeactivateAsset,
  onViewAsset,
}: {
  clientId: string
  summary: ClientSummaryRecord
  refreshKey: number
  selectedSiteId: string | null
  onSelectSite: (id: string) => void
  siteSearch: string
  onSiteSearchChange: (v: string) => void
  onOpenSiteEditor: (site?: SiteRecord) => void
  onDeactivateSite: (site: SiteRecord) => void
  onOpenAssetEditor: (asset?: AssetRecord, siteId?: string | null) => void
  onDeactivateAsset: (assetId: string, assetName: string) => void
  onViewAsset: (assetId: string) => void
}) {
  // Load full SiteRecord for editing from the sites list in summary
  const { data: allSiteRecords, reload: reloadSiteRecords } = useAsyncData<SiteRecord[]>(
    (signal) => siteService.list(clientId, { signal }),
    [],
    [clientId, refreshKey],
  )

  // Site detail for selected site
  const { data: siteDetail, loading: siteDetailLoading, error: siteDetailError, reload: reloadSiteDetail } = useAsyncData<SiteDetailRecord | null>(
    (signal) =>
      clientId && selectedSiteId
        ? siteService.get(clientId, selectedSiteId, signal)
        : Promise.resolve(null),
    null,
    [clientId, refreshKey, selectedSiteId],
  )

  const filteredSites = useMemo(() => {
    const q = siteSearch.toLowerCase()
    return summary.sites.filter((s) =>
      !q || s.siteName.toLowerCase().includes(q) || (s.location ?? '').toLowerCase().includes(q),
    )
  }, [summary.sites, siteSearch])

  function getSiteRecord(siteId: string) {
    return allSiteRecords.find((s) => s.id === siteId) ?? null
  }

  async function handleDeactivateSite(siteId: string) {
    const siteRecord = getSiteRecord(siteId)
    if (siteRecord) await onDeactivateSite(siteRecord)
    await reloadSiteRecords()
  }

  function handleEditSite(siteId: string) {
    const siteRecord = getSiteRecord(siteId)
    onOpenSiteEditor(siteRecord ?? undefined)
  }

  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
      {/* ── Left panel: sites list ── */}
      <div className="w-full space-y-3 lg:w-[40%] lg:shrink-0">
        <SectionCard
          title="Sites"
          description={`${summary.siteCount} location${summary.siteCount !== 1 ? 's' : ''}`}
        >
          <div className="mb-3 flex items-center gap-2">
            <label className="panel-subtle flex flex-1 items-center gap-2 rounded-full px-3 py-2.5">
              <Search className="h-4 w-4 shrink-0 text-muted" />
              <input
                value={siteSearch}
                onChange={(e) => onSiteSearchChange(e.target.value)}
                className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                placeholder="Search sites…"
              />
            </label>
            <button
              type="button"
              className="button-primary shrink-0 px-3 py-2"
              onClick={() => onOpenSiteEditor()}
              title="Add site"
            >
              <Plus className="h-4 w-4" />
            </button>
          </div>

          {filteredSites.length === 0 && (
            <p className="py-8 text-center text-sm text-muted">
              {siteSearch ? 'No sites match your search.' : 'No sites yet. Add the first site above.'}
            </p>
          )}

          <div className="space-y-2">
            {filteredSites.map((site) => (
              <button
                key={site.siteId}
                type="button"
                onClick={() => onSelectSite(site.siteId)}
                className={`w-full rounded-[14px] border p-4 text-left transition-colors ${
                  selectedSiteId === site.siteId
                    ? 'border-accent bg-accent/10'
                    : 'border-app bg-subtle hover:border-accent/50'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate font-semibold text-app">{site.siteName}</p>
                    <p className="text-xs text-muted">{site.siteType}</p>
                    {site.location && (
                      <p className="mt-0.5 flex items-center gap-1 text-xs text-muted">
                        <MapPin className="h-3 w-3 shrink-0" />
                        {site.location}
                      </p>
                    )}
                  </div>
                  <div className="shrink-0 text-right">
                    <Badge tone={site.status === 'Active' ? 'success' : 'neutral'}>{site.status}</Badge>
                    <div className="mt-1.5 flex gap-2 text-xs text-muted">
                      <span>{site.assetCount}A</span>
                      <span>{site.openWorkOrderCount}WO</span>
                    </div>
                  </div>
                </div>
              </button>
            ))}
          </div>
        </SectionCard>
      </div>

      {/* ── Right panel: selected site detail ── */}
      <div className="w-full lg:flex-1">
        {!selectedSiteId ? (
          <div className="flex min-h-[400px] flex-col items-center justify-center rounded-[18px] border border-[var(--app-divider)] bg-subtle text-center">
            <MapPin className="mb-3 h-10 w-10 text-muted" />
            <p className="text-sm font-medium text-app">Select a site</p>
            <p className="mt-1 text-xs text-muted">Click a site on the left to view its details and assets.</p>
          </div>
        ) : siteDetailLoading ? (
          <LoadingState label="Loading site" />
        ) : siteDetailError || !siteDetail ? (
          <ErrorState title="Unable to load site" description={siteDetailError || 'Site data unavailable.'} />
        ) : (
          <SitePanel
            clientId={clientId}
            siteDetail={siteDetail}
            onEditSite={() => handleEditSite(selectedSiteId)}
            onDeactivateSite={() => void handleDeactivateSite(selectedSiteId)}
            onAddAsset={() => onOpenAssetEditor(undefined, selectedSiteId)}
            onEditAsset={(assetId) => {
              assetService.get(assetId).then((asset) => onOpenAssetEditor(asset, selectedSiteId)).catch(() => {})
            }}
            onDeactivateAsset={onDeactivateAsset}
            onViewAsset={onViewAsset}
            onReloadSite={() => { void reloadSiteDetail(); void reloadSiteRecords() }}
          />
        )}
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────
// Site panel (right side of split view)
// ─────────────────────────────────────────────
function SitePanel({
  clientId,
  siteDetail,
  onEditSite,
  onDeactivateSite,
  onAddAsset,
  onEditAsset,
  onDeactivateAsset,
  onViewAsset,
  onReloadSite: _onReloadSite,
}: {
  clientId: string
  siteDetail: SiteDetailRecord
  onEditSite: () => void
  onDeactivateSite: () => void
  onAddAsset: () => void
  onEditAsset: (assetId: string) => void
  onDeactivateAsset: (assetId: string, assetName: string) => void
  onViewAsset: (assetId: string) => void
  onReloadSite: () => void
}) {
  const site = siteDetail.site
  const { data: siteContracts, loading: siteContractsLoading } = useAsyncData<ContractListItem[]>(
    (signal) => contractService.listSiteContracts(site.id, signal),
    [],
    [site.id],
  )
  return (
    <div className="space-y-3">
      {/* Site header */}
      <SectionCard
        title={site.siteName}
        description={`${site.siteCode} · ${site.siteType}`}
      >
        <div className="flex flex-wrap gap-2 mb-4">
          <button type="button" className="button-secondary px-3 py-2 text-xs" onClick={onEditSite}>
            <Edit2 className="h-3.5 w-3.5" />
            Edit site
          </button>
          {site.status === 'Active' && (
            <button type="button" className="button-secondary px-3 py-2 text-xs" onClick={onDeactivateSite}>
              Deactivate
            </button>
          )}
          <Link
            to={`/clients?clientId=${clientId}&tab=sites-assets&siteId=${site.id}`}
            className="button-secondary px-3 py-2 text-xs flex items-center gap-1"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Focus site
          </Link>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <Detail label="Status" value={site.status} />
          <Detail label="Location" value={[site.townCity, site.county, site.country].filter(Boolean).join(', ') || '-'} />
          <Detail label="Contact" value={site.contactPerson ? `${site.contactPerson}${site.contactPhone ? ' · ' + site.contactPhone : ''}` : '-'} />
          <Detail label="Operating hours" value={site.operatingHours || '-'} />
          <Detail label="Systems" value={String(siteDetail.systemCount)} />
          <Detail label="Open work orders" value={String(siteDetail.openWorkOrders)} />
        </div>
      </SectionCard>

      {/* Assets at this site */}
      <SectionCard
        title="Assets & components"
        description={`${siteDetail.assetCount} asset${siteDetail.assetCount !== 1 ? 's' : ''} at this site`}
      >
        <div className="mb-3 flex justify-end">
          <button type="button" className="button-primary px-3 py-2 text-sm" onClick={onAddAsset}>
            <Plus className="h-4 w-4" />
            Add asset
          </button>
        </div>
        <DataTable
          rows={siteDetail.assets}
          rowKey={(row) => row.assetId}
          pageSize={10}
          emptyTitle="No assets at this site"
          emptyDescription="Add the first asset using the button above."
          mobileCard={(row) => (
            <div className="rounded-[18px] border border-[var(--app-divider)] bg-subtle p-4 space-y-2">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-semibold text-app">{row.assetName}</p>
                  <p className="text-xs text-muted">{row.assetCode} · {row.assetType || 'Asset'}</p>
                </div>
                <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge>
              </div>
              <div className="flex gap-2 pt-1">
                <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onViewAsset(row.assetId)}>View</button>
                <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onEditAsset(row.assetId)}>Edit</button>
                {row.status === 'Active' ? (
                  <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onDeactivateAsset(row.assetId, row.assetName)}>
                    Disable
                  </button>
                ) : null}
              </div>
            </div>
          )}
          columns={[
            {
              key: 'asset', header: 'Asset',
              cell: (row) => (
                <button type="button" className="text-left" onClick={() => onViewAsset(row.assetId)}>
                  <p className="font-semibold text-accent-strong hover:underline">{row.assetName}</p>
                  <p className="text-xs text-muted">{row.assetCode}</p>
                </button>
              ),
            },
            { key: 'system', header: 'System', cell: (row) => row.systemName || '-' },
            { key: 'type', header: 'Type', cell: (row) => row.assetType || '-' },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.status === 'Active' ? 'success' : 'neutral'}>{row.status}</Badge> },
            { key: 'lastPm', header: 'Last PM', cell: (row) => formatDateOnly(row.lastPmServiceDate ?? undefined) },
            { key: 'nextPm', header: 'Next PM', cell: (row) => formatDateOnly(row.nextPmDueDate ?? undefined) },
            {
              key: 'actions', header: 'Actions',
              cell: (row) => (
                <div className="flex gap-2">
                  <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onViewAsset(row.assetId)}>View</button>
                  <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onEditAsset(row.assetId)}>Edit</button>
                  {row.status === 'Active' ? (
                    <button type="button" className="button-secondary px-3 py-1.5 text-xs" onClick={() => onDeactivateAsset(row.assetId, row.assetName)}>
                      Disable
                    </button>
                  ) : null}
                </div>
              ),
            },
          ]}
        />
      </SectionCard>

      <SectionCard title="Contracts" description="Contracts that explicitly cover this site.">
        {siteContractsLoading ? <LoadingState label="Loading site contracts" /> : null}
        {!siteContractsLoading && siteContracts.length === 0 ? <p className="text-sm text-muted">No contract is explicitly linked to this site.</p> : null}
        {!siteContractsLoading && siteContracts.length > 0 ? (
          <div className="space-y-3">
            {siteContracts.map((item) => (
              <div key={item.id} className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--app-divider)] px-4 py-3 first:border-t-0">
                <div>
                  <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.contractNumber}</Link>
                  <p className="mt-1 text-sm text-secondary">{item.name}</p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {renderContractStatusBadge(item.status)}
                  {renderRenewalRiskBadge(item.renewalRisk)}
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </SectionCard>
    </div>
  )
}

// ─────────────────────────────────────────────
// Work Orders tab
// ─────────────────────────────────────────────
function WorkOrdersTab({
  loading,
  error,
  workOrders,
  allWorkOrders,
  sites,
  statusFilter,
  siteFilter,
  onStatusFilterChange,
  onSiteFilterChange,
  clientId: _clientId,
}: {
  loading: boolean
  error: string | null
  workOrders: WorkOrder[]
  allWorkOrders: WorkOrder[]
  sites: ClientSummaryRecord['sites']
  statusFilter: string
  siteFilter: string
  onStatusFilterChange: (v: string) => void
  onSiteFilterChange: (v: string) => void
  clientId: string
}) {
  const woStatuses = useMemo(
    () => [...new Set(allWorkOrders.map((wo) => wo.status))].sort(),
    [allWorkOrders],
  )

  return (
    <SectionCard title="Work orders" description="All work orders for this client across all sites.">
      <div className="mb-4 flex flex-wrap gap-3">
        <select value={siteFilter} onChange={(e) => onSiteFilterChange(e.target.value)} className="field-input">
          <option value="">All sites</option>
          {sites.map((s) => <option key={s.siteId} value={s.siteId}>{s.siteName}</option>)}
        </select>
        <select value={statusFilter} onChange={(e) => onStatusFilterChange(e.target.value)} className="field-input">
          <option value="">All statuses</option>
          {woStatuses.map((s) => <option key={s} value={s}>{s}</option>)}
        </select>
        {(siteFilter || statusFilter) && (
          <button type="button" className="button-secondary" onClick={() => { onSiteFilterChange(''); onStatusFilterChange('') }}>
            <X className="h-4 w-4" /> Clear
          </button>
        )}
      </div>

      {loading && <LoadingState label="Loading work orders" />}
      {!loading && error && <ErrorState title="Unable to load work orders" description={error} />}
      {!loading && !error && (
        <DataTable
          rows={workOrders}
          rowKey={(row) => row.id}
          pageSize={10}
          emptyTitle="No work orders found"
          emptyDescription="No work orders match the current filters."
          mobileCard={(row) => (
            <div className="rounded-[18px] border border-[var(--app-divider)] bg-subtle p-4 space-y-2">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <Link to={`/work-orders/${row.id}`} className="font-semibold text-accent-strong hover:underline">{row.title}</Link>
                  <p className="text-xs text-muted">{row.workOrderNumber}</p>
                </div>
                <Badge tone={statusTone(row.status as WorkflowStatus)}>{row.status}</Badge>
              </div>
              <p className="text-xs text-muted">{row.siteName || '-'}</p>
            </div>
          )}
          columns={[
            {
              key: 'wo', header: 'Work order',
              cell: (row) => (
                <div>
                  <Link to={`/work-orders/${row.id}`} className="font-semibold text-accent-strong hover:underline">{row.title}</Link>
                  <p className="text-xs text-muted">{row.workOrderNumber}</p>
                </div>
              ),
            },
            { key: 'site', header: 'Site', cell: (row) => row.siteName || '-' },
            { key: 'asset', header: 'Asset', cell: (row) => row.assetName || '-' },
            { key: 'priority', header: 'Priority', cell: (row) => row.priority },
            { key: 'status', header: 'Status', cell: (row) => <Badge tone={statusTone(row.status as WorkflowStatus)}>{row.status}</Badge> },
            { key: 'due', header: 'Due', cell: (row) => formatDateOnly(row.dueDate ?? undefined) },
            { key: 'link', header: '', cell: (row) => <Link to={`/work-orders/${row.id}`} className="button-secondary px-3 py-1.5 text-xs">Open</Link> },
          ]}
        />
      )}
    </SectionCard>
  )
}

// ─────────────────────────────────────────────
// Asset slide-over panel
// ─────────────────────────────────────────────
function AssetSlideOver({
  asset,
  loading,
  workOrders,
  onClose,
  onEdit,
}: {
  asset: AssetRecord | null
  loading: boolean
  workOrders: WorkOrder[]
  onClose: () => void
  onEdit: (asset: AssetRecord) => void
}) {
  const assetId = asset?.id ?? ''
  const { data: assetContracts, loading: assetContractsLoading } = useAsyncData<ContractListItem[]>(
    (signal) => assetId ? contractService.listAssetContracts(assetId, signal) : Promise.resolve([]),
    [],
    [assetId],
  )
  return (
    <>
      {/* Backdrop */}
      <div
        className="fixed inset-0 z-40 bg-black/30 backdrop-blur-sm"
        onClick={onClose}
      />
      {/* Panel */}
      <div className="fixed inset-y-0 right-0 z-50 flex w-full max-w-lg flex-col bg-app shadow-none">
        <div className="flex items-center justify-between border-b border-[var(--app-divider)] px-5 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Asset detail</p>
            <h3 className="mt-0.5 text-lg font-semibold text-app">
              {loading ? '…' : (asset?.assetName ?? 'Asset')}
            </h3>
          </div>
          <button type="button" className="icon-button" onClick={onClose}>
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5">
          {loading && <LoadingState label="Loading asset" />}
          {!loading && !asset && <p className="text-sm text-muted text-center mt-8">Asset not found.</p>}
          {!loading && asset && (
            <div className="space-y-5">
              {/* Actions */}
              <div className="flex gap-2">
                <button type="button" className="button-secondary" onClick={() => onEdit(asset)}>
                  <Edit2 className="h-4 w-4" />
                  Edit asset
                </button>
                <Link to="/work-orders" className="button-secondary flex items-center gap-1.5">
                  <Plus className="h-4 w-4" />
                  Schedule service
                </Link>
              </div>

              {/* Status + badge */}
              <div className="flex items-center gap-3">
                <Badge tone={asset.status === 'Active' ? 'success' : 'neutral'}>{asset.status}</Badge>
                {asset.assetCategoryName && <span className="text-sm text-muted">{asset.assetCategoryName}</span>}
              </div>

              {/* Asset profile */}
              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted">Profile</p>
                <div className="grid gap-3 grid-cols-2">
                  <Detail label="Asset code" value={asset.assetCode} />
                  <Detail label="Asset type" value={asset.assetType || '-'} />
                  <Detail label="Serial number" value={asset.serialNumber || '-'} />
                  <Detail label="Manufacturer" value={asset.manufacturer || '-'} />
                  <Detail label="Model" value={asset.model || '-'} />
                  <Detail label="Ownership" value={asset.ownershipType || '-'} />
                  <Detail label="Location" value={asset.location || '-'} />
                  <Detail label="System" value={asset.systemName || '-'} />
                  <Detail label="Install date" value={formatDateOnly(asset.installationDate ?? undefined)} />
                  <Detail label="Warranty expiry" value={formatDateOnly(asset.warrantyExpiryDate ?? undefined)} />
                  <Detail label="Last PM" value={formatDateOnly(asset.lastPmDate ?? undefined)} />
                  <Detail label="Next PM" value={formatDateOnly(asset.nextPmDate ?? undefined)} />
                </div>
                {asset.notes && (
                  <div className="mt-3 rounded-[14px] bg-subtle p-3">
                    <p className="text-xs text-muted">{asset.notes}</p>
                  </div>
                )}
              </div>

              {/* Service history */}
              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted">Service history</p>
                {workOrders.length === 0 ? (
                  <p className="text-sm text-muted">No work orders recorded for this asset.</p>
                ) : (
                  <div className="space-y-2">
                    {workOrders.slice(0, 5).map((wo) => (
                      <div key={wo.id} className="flex items-start justify-between gap-3 border-t border-[var(--app-divider)] px-4 py-3 first:border-t-0">
                        <div className="min-w-0">
                          <Link to={`/work-orders/${wo.id}`} className="text-sm font-medium text-accent-strong hover:underline truncate block">{wo.title}</Link>
                          <p className="text-xs text-muted">{wo.workOrderNumber} · {formatDateOnly(wo.createdAt)}</p>
                        </div>
                        <Badge tone={statusTone(wo.status as WorkflowStatus)}>{wo.status}</Badge>
                      </div>
                    ))}
                    {workOrders.length > 5 && (
                      <p className="text-center text-xs text-muted">+{workOrders.length - 5} more — open Work Orders tab to view all</p>
                    )}
                  </div>
                )}
              </div>

              <div>
                <p className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-muted">Coverage / contracts</p>
                {assetContractsLoading ? <p className="text-sm text-muted">Loading contracts...</p> : null}
                {!assetContractsLoading && assetContracts.length === 0 ? <p className="text-sm text-muted">No contract is explicitly linked to this asset.</p> : null}
                {!assetContractsLoading && assetContracts.length > 0 ? (
                  <div className="space-y-2">
                    {assetContracts.map((item) => (
                      <div key={item.id} className="rounded-[14px] border-t border-[var(--app-divider)] px-4 py-3 first:border-t-0">
                        <div className="flex flex-wrap items-center justify-between gap-2">
                          <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">{item.contractNumber}</Link>
                          {renderContractStatusBadge(item.status)}
                        </div>
                        <p className="mt-1 text-sm text-secondary">{item.name}</p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          {renderRenewalRiskBadge(item.renewalRisk)}
                          {renderBillingModelBadge(item.billingModel)}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : null}
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  )
}

// ─────────────────────────────────────────────
// Editor form components
// ─────────────────────────────────────────────
function ClientEditorForm({
  form,
  onChange,
  slaDefinitions,
}: {
  form: UpsertClientInput
  onChange: (f: UpsertClientInput) => void
  slaDefinitions: SlaDefinitionRecord[]
}) {
  return (
    <div className="space-y-3">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Client name"><input value={form.clientName} onChange={(e) => onChange({ ...form, clientName: e.target.value })} className="field-input" /></Field>
        <Field label="Client type"><input value={form.clientType || ''} onChange={(e) => onChange({ ...form, clientType: e.target.value })} className="field-input" /></Field>
        <Field label="Email"><input value={form.email || ''} onChange={(e) => onChange({ ...form, email: e.target.value })} className="field-input" /></Field>
        <Field label="Phone"><input value={form.phone || ''} onChange={(e) => onChange({ ...form, phone: e.target.value })} className="field-input" /></Field>
        <Field label="Contact person"><input value={form.contactPerson || ''} onChange={(e) => onChange({ ...form, contactPerson: e.target.value })} className="field-input" /></Field>
        <Field label="Contact phone"><input value={form.contactPhone || ''} onChange={(e) => onChange({ ...form, contactPhone: e.target.value })} className="field-input" /></Field>
        <Field label="Location"><input value={form.location || ''} onChange={(e) => onChange({ ...form, location: e.target.value })} className="field-input" /></Field>
        <Field label="Response targets">
          <select value={form.slaDefinitionId || ''} onChange={(e) => onChange({ ...form, slaDefinitionId: e.target.value || null })} className="field-input">
            <option value="">No response target set</option>
            {slaDefinitions.filter((d) => d.isActive).map((d) => (
              <option key={d.id} value={d.id}>{d.planName}</option>
            ))}
          </select>
        </Field>
        <Field label="Active start date"><input type="date" value={form.activeStartDate || ''} onChange={(e) => onChange({ ...form, activeStartDate: e.target.value })} className="field-input" /></Field>
        <Field label="Duration">
          <div className="flex gap-2">
            <input type="number" min={1} value={form.activeDurationValue ?? ''} onChange={(e) => onChange({ ...form, activeDurationValue: e.target.value ? Number(e.target.value) : null })} className="field-input flex-1" placeholder="Value" />
            <select value={form.activeDurationUnit || 'months'} onChange={(e) => onChange({ ...form, activeDurationUnit: e.target.value })} className="field-input w-28">
              <option value="days">days</option>
              <option value="months">months</option>
              <option value="years">years</option>
            </select>
          </div>
        </Field>
      </div>
      <Field label="Notes"><textarea value={form.notes || ''} onChange={(e) => onChange({ ...form, notes: e.target.value })} className="field-input min-h-[100px]" /></Field>
    </div>
  )
}

function SiteEditorForm({
  form,
  onChange,
}: {
  form: UpsertSiteInput
  onChange: (f: UpsertSiteInput) => void
}) {
  return (
    <div className="space-y-3">
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Site name"><input value={form.siteName} onChange={(e) => onChange({ ...form, siteName: e.target.value })} className="field-input" /></Field>
        <Field label="Site type">
          <select value={form.siteType ?? ''} onChange={(e) => onChange({ ...form, siteType: e.target.value })} className="field-input">
            <option value="Branch">Branch</option>
            <option value="HQ">HQ</option>
            <option value="Data Centre">Data Centre</option>
            <option value="Depot">Depot</option>
            <option value="Office">Office</option>
            <option value="Warehouse">Warehouse</option>
            <option value="Factory">Factory</option>
            <option value="Remote">Remote</option>
            <option value="Other">Other</option>
          </select>
        </Field>
        <Field label="Street address"><input value={form.streetAddress ?? ''} onChange={(e) => onChange({ ...form, streetAddress: e.target.value })} className="field-input" /></Field>
        <Field label="Area / Estate"><input value={form.areaEstate ?? ''} onChange={(e) => onChange({ ...form, areaEstate: e.target.value })} className="field-input" /></Field>
        <Field label="Town / City"><input value={form.townCity ?? ''} onChange={(e) => onChange({ ...form, townCity: e.target.value })} className="field-input" /></Field>
        <Field label="County"><input value={form.county ?? ''} onChange={(e) => onChange({ ...form, county: e.target.value })} className="field-input" /></Field>
        <Field label="Country"><input value={form.country ?? ''} onChange={(e) => onChange({ ...form, country: e.target.value })} className="field-input" /></Field>
        <Field label="Region"><input value={form.region ?? ''} onChange={(e) => onChange({ ...form, region: e.target.value })} className="field-input" /></Field>
        <Field label="Contact person"><input value={form.contactPerson ?? ''} onChange={(e) => onChange({ ...form, contactPerson: e.target.value })} className="field-input" /></Field>
        <Field label="Contact phone"><input value={form.contactPhone ?? ''} onChange={(e) => onChange({ ...form, contactPhone: e.target.value })} className="field-input" /></Field>
        <Field label="Contact email"><input value={form.contactEmail ?? ''} onChange={(e) => onChange({ ...form, contactEmail: e.target.value })} className="field-input" /></Field>
        <Field label="Operating hours"><input value={form.operatingHours ?? ''} onChange={(e) => onChange({ ...form, operatingHours: e.target.value })} className="field-input" /></Field>
      </div>
      <Field label="Access notes"><textarea value={form.accessNotes ?? ''} onChange={(e) => onChange({ ...form, accessNotes: e.target.value })} className="field-input min-h-[70px]" /></Field>
      <Field label="Special instructions"><textarea value={form.specialInstructions ?? ''} onChange={(e) => onChange({ ...form, specialInstructions: e.target.value })} className="field-input min-h-[60px]" /></Field>
    </div>
  )
}

function AssetEditorForm({
  form,
  onChange,
  sites,
  systems,
  categories,
  saveError,
  onSyncCustomFields,
}: {
  form: UpsertAssetInput
  onChange: (f: UpsertAssetInput) => void
  sites: SiteRecord[]
  systems: SystemRecord[]
  categories: AssetCategoryRecord[]
  saveError: string
  onSyncCustomFields: (catId: string | null) => void
}) {
  const selectedCategory = categories.find((c) => c.id === form.assetCategoryId) ?? null

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Site">
          <select value={form.siteId ?? ''} onChange={(e) => onChange({ ...form, siteId: e.target.value || null, systemId: null })} className="field-input">
            <option value="">Select site</option>
            {sites.map((s) => <option key={s.id} value={s.id}>{s.siteName}</option>)}
          </select>
        </Field>
        <Field label="System (optional)">
          <select value={form.systemId ?? ''} onChange={(e) => onChange({ ...form, systemId: e.target.value || null })} className="field-input" disabled={!form.siteId}>
            <option value="">{form.siteId ? 'No system' : 'Select site first'}</option>
            {systems.map((s) => <option key={s.id} value={s.id}>{s.systemName}</option>)}
          </select>
        </Field>
        <Field label="Asset category">
          <select value={form.assetCategoryId ?? ''} onChange={(e) => onSyncCustomFields(e.target.value || null)} className="field-input">
            <option value="">No category</option>
            {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </Field>
        <Field label="Asset name *"><input value={form.assetName} onChange={(e) => onChange({ ...form, assetName: e.target.value })} className="field-input" /></Field>
        <Field label="Asset code"><input value={form.assetCode || ''} onChange={(e) => onChange({ ...form, assetCode: e.target.value })} className="field-input" placeholder="Auto-generated if blank" /></Field>
        <Field label="Asset type"><input value={form.assetType || ''} onChange={(e) => onChange({ ...form, assetType: e.target.value })} className="field-input" /></Field>
        <Field label="Serial number"><input value={form.serialNumber || ''} onChange={(e) => onChange({ ...form, serialNumber: e.target.value })} className="field-input" /></Field>
        <Field label="Manufacturer"><input value={form.manufacturer || ''} onChange={(e) => onChange({ ...form, manufacturer: e.target.value })} className="field-input" /></Field>
        <Field label="Model"><input value={form.model || ''} onChange={(e) => onChange({ ...form, model: e.target.value })} className="field-input" /></Field>
        <Field label="Ownership type"><input value={form.ownershipType || ''} onChange={(e) => onChange({ ...form, ownershipType: e.target.value })} className="field-input" placeholder="Owned, leased…" /></Field>
        <Field label="Install date"><input type="date" value={form.installationDate || ''} onChange={(e) => onChange({ ...form, installationDate: e.target.value })} className="field-input" /></Field>
        <Field label="Warranty expiry"><input type="date" value={form.warrantyExpiryDate || ''} onChange={(e) => onChange({ ...form, warrantyExpiryDate: e.target.value })} className="field-input" /></Field>
        <Field label="Last PM date"><input type="date" value={form.lastPmDate || ''} onChange={(e) => onChange({ ...form, lastPmDate: e.target.value })} className="field-input" /></Field>
        <Field label="PM frequency">
          <div className="flex gap-2">
            <input type="number" min={1} value={form.pmFrequencyValue ?? ''} onChange={(e) => onChange({ ...form, pmFrequencyValue: e.target.value ? Number(e.target.value) : null })} className="field-input flex-1" />
            <select value={form.pmFrequencyUnit || 'months'} onChange={(e) => onChange({ ...form, pmFrequencyUnit: e.target.value })} className="field-input w-28">
              <option value="days">days</option>
              <option value="weeks">weeks</option>
              <option value="months">months</option>
              <option value="quarters">quarters</option>
              <option value="years">years</option>
            </select>
          </div>
        </Field>
        <Field label="Next PM date"><input type="date" value={form.nextPmDate || ''} onChange={(e) => onChange({ ...form, nextPmDate: e.target.value })} className="field-input" /></Field>
        <Field label="Status">
          <select value={form.status || 'Active'} onChange={(e) => onChange({ ...form, status: e.target.value })} className="field-input">
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
            <option value="Under Maintenance">Under Maintenance</option>
          </select>
        </Field>
      </div>

      {selectedCategory && selectedCategory.fields.length > 0 && (
        <div className="rounded-[18px] border border-[var(--app-divider)] bg-subtle p-4 space-y-3">
          <p className="text-sm font-semibold text-app">Category-specific fields — {selectedCategory.name}</p>
          <div className="grid gap-4 md:grid-cols-2">
            {selectedCategory.fields.map((field) => {
              const currentValue = form.customFieldValues?.find((v) => v.fieldDefinitionId === field.id)?.value ?? ''
              const handleChange = (value: string) => {
                onChange({
                  ...form,
                  customFieldValues: (form.customFieldValues ?? []).map((v) =>
                    v.fieldDefinitionId === field.id ? { ...v, value } : v,
                  ),
                })
              }
              return (
                <Field key={field.id} label={`${field.fieldLabel}${field.isRequired ? ' *' : ''}`}>
                  {field.fieldType === 'Dropdown' ? (
                    <select value={currentValue} onChange={(e) => handleChange(e.target.value)} className="field-input">
                      <option value="">Select</option>
                      {(field.dropdownOptions ?? '').split(',').map((o) => o.trim()).filter(Boolean).map((o) => (
                        <option key={o} value={o}>{o}</option>
                      ))}
                    </select>
                  ) : field.fieldType === 'Boolean' ? (
                    <select value={currentValue} onChange={(e) => handleChange(e.target.value)} className="field-input">
                      <option value="">Select</option>
                      <option value="true">Yes</option>
                      <option value="false">No</option>
                    </select>
                  ) : field.fieldType === 'Date' ? (
                    <input type="date" value={currentValue} onChange={(e) => handleChange(e.target.value)} className="field-input" />
                  ) : field.fieldType === 'TextArea' ? (
                    <textarea value={currentValue} onChange={(e) => handleChange(e.target.value)} className="field-input min-h-[80px]" />
                  ) : (
                    <input type={field.fieldType === 'Number' ? 'number' : 'text'} value={currentValue} onChange={(e) => handleChange(e.target.value)} className="field-input" />
                  )}
                </Field>
              )
            })}
          </div>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Location"><input value={form.location || ''} onChange={(e) => onChange({ ...form, location: e.target.value })} className="field-input" placeholder="Room, floor, building…" /></Field>
        <Field label="Recommended PM frequency"><input value={form.recommendedPmFrequency || ''} onChange={(e) => onChange({ ...form, recommendedPmFrequency: e.target.value })} className="field-input" placeholder="e.g. Every 3 months" /></Field>
      </div>

      <label className="flex items-center gap-3 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={form.autoSchedulePm ?? true}
          onChange={(e) => onChange({ ...form, autoSchedulePm: e.target.checked })}
          className="h-4 w-4 rounded border-border"
        />
        <span className="text-sm font-medium text-app">Auto-schedule preventive maintenance</span>
      </label>

      <Field label="Notes"><textarea value={form.notes || ''} onChange={(e) => onChange({ ...form, notes: e.target.value })} className="field-input min-h-[80px]" rows={3} /></Field>

      {saveError && <p className="text-sm text-danger">{saveError}</p>}
    </div>
  )
}

// ─────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-app">{label}</span>
      {children}
    </label>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="border-t border-[var(--app-divider)] px-3 py-3 first:border-t-0">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 text-sm text-app">{value}</p>
    </div>
  )
}
