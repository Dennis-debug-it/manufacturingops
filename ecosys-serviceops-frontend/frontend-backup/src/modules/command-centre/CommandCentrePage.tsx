import { Activity, ArrowRight, Building2, PencilLine, Plus, Search, ShieldCheck, Users } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { PageHeader } from '../../components/ui/PageHeader'
import { StatCard } from '../../components/ui/StatCard'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { platformTenantService } from '../../services/platformTenantService'
import type {
  PlatformLicenseStatus,
  PlatformSummary,
  PlatformTenant,
  PlatformTenantAuditLog,
  PlatformTenantDetail,
  PlatformTenantStatus,
  UpsertPlatformTenantInput,
} from '../../types/api'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { getSlugValidationMessage, slugifyName } from '../../utils/slug'

type CommandCentrePayload = {
  summary: PlatformSummary | null
  tenants: PlatformTenant[]
}

type TenantDetailPayload = {
  detail: PlatformTenantDetail | null
  auditLogs: PlatformTenantAuditLog[]
}

type TenantEditorMode = 'create' | 'edit'

type StatusDialogState = {
  tenantId: string
  tenantName: string
  currentStatus: PlatformTenantStatus
  selectedStatus: PlatformTenantStatus
  title: string
  confirmLabel: string
}

const initialTenantForm: UpsertPlatformTenantInput = {
  name: '',
  slug: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
  country: 'Kenya',
  county: '',
  city: '',
  address: '',
  taxPin: '',
  planName: 'Professional',
  maxUsers: 25,
  maxBranches: 3,
  trialEndsAt: null,
  subscriptionEndsAt: null,
  status: 'Active',
  licenseStatus: 'Active',
}

const tenantStatuses: PlatformTenantStatus[] = ['Active', 'Trial', 'Suspended', 'Inactive']
const licenseStatuses: PlatformLicenseStatus[] = ['Active', 'Trial', 'Expired', 'Suspended']

export function CommandCentrePage() {
  const { session } = useAuth()
  const { pushToast } = useToast()
  const canView = Boolean(session?.permissions?.canViewPlatformTenants)
  const canCreate = Boolean(session?.permissions?.canCreatePlatformTenants)
  const canEdit = Boolean(session?.permissions?.canEditPlatformTenants)
  const canUpdateStatus = Boolean(session?.permissions?.canUpdatePlatformTenantStatus)

  const [searchQuery, setSearchQuery] = useState('')
  const [statusFilter, setStatusFilter] = useState<'All' | PlatformTenantStatus>('All')
  const [planFilter, setPlanFilter] = useState('All')
  const [licenseStatusFilter, setLicenseStatusFilter] = useState<'All' | PlatformLicenseStatus>('All')
  const [editorMode, setEditorMode] = useState<TenantEditorMode | null>(null)
  const [editingTenantId, setEditingTenantId] = useState<string | null>(null)
  const [tenantForm, setTenantForm] = useState<UpsertPlatformTenantInput>(initialTenantForm)
  const [slugTouched, setSlugTouched] = useState(false)
  const [savingTenant, setSavingTenant] = useState(false)
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null)
  const [loadingEditor, setLoadingEditor] = useState(false)
  const [statusDialog, setStatusDialog] = useState<StatusDialogState | null>(null)
  const [statusReason, setStatusReason] = useState('')
  const [savingStatus, setSavingStatus] = useState(false)

  const {
    data,
    loading,
    error,
    reload,
  } = useAsyncData<CommandCentrePayload>(
    async (signal) => {
      const [summary, tenants] = await Promise.all([
        platformTenantService.getPlatformSummary(signal),
        platformTenantService.getPlatformTenants(signal),
      ])
      return { summary, tenants }
    },
    { summary: null, tenants: [] },
    [],
  )

  const {
    data: detailData,
    loading: detailLoading,
    error: detailError,
    reload: reloadDetail,
  } = useAsyncData<TenantDetailPayload>(
    async (signal) => {
      if (!selectedTenantId) {
        return { detail: null, auditLogs: [] }
      }

      const [detail, auditLogs] = await Promise.all([
        platformTenantService.getPlatformTenant(selectedTenantId, signal),
        platformTenantService.getPlatformTenantAuditLogs(selectedTenantId, signal),
      ])

      return { detail, auditLogs }
    },
    { detail: null, auditLogs: [] },
    [selectedTenantId],
  )

  const planOptions = useMemo(
    () => ['All', ...Array.from(new Set(data.tenants.map((tenant) => tenant.planName).filter((value): value is string => Boolean(value))).values())],
    [data.tenants],
  )

  const filteredTenants = useMemo(() => {
    const query = searchQuery.trim().toLowerCase()

    return data.tenants.filter((tenant) => {
      const matchesQuery =
        !query ||
        tenant.name.toLowerCase().includes(query) ||
        tenant.slug.toLowerCase().includes(query) ||
        (tenant.contactEmail || '').toLowerCase().includes(query)

      const matchesStatus = statusFilter === 'All' || tenant.status === statusFilter
      const matchesPlan = planFilter === 'All' || (tenant.planName || '') === planFilter
      const matchesLicense = licenseStatusFilter === 'All' || tenant.licenseStatus === licenseStatusFilter

      return matchesQuery && matchesStatus && matchesPlan && matchesLicense
    })
  }, [data.tenants, licenseStatusFilter, planFilter, searchQuery, statusFilter])

  async function openCreateModal() {
    setTenantForm(initialTenantForm)
    setSlugTouched(false)
    setEditingTenantId(null)
    setEditorMode('create')
  }

  async function openEditModal(tenantId: string) {
    setLoadingEditor(true)
    try {
      const detail = await platformTenantService.getPlatformTenant(tenantId)
      setTenantForm({
        name: detail.name,
        slug: detail.slug,
        contactName: detail.contactName || '',
        contactEmail: detail.contactEmail || '',
        contactPhone: detail.contactPhone || '',
        country: detail.country || '',
        county: detail.county || '',
        city: detail.city || '',
        address: detail.address || '',
        taxPin: detail.taxPin || '',
        planName: detail.planName || '',
        maxUsers: detail.maxUsers ?? null,
        maxBranches: detail.maxBranches ?? null,
        trialEndsAt: detail.trialEndsAt || null,
        subscriptionEndsAt: detail.subscriptionEndsAt || null,
        status: detail.status,
        licenseStatus: detail.licenseStatus,
      })
      setSlugTouched(true)
      setEditingTenantId(tenantId)
      setEditorMode('edit')
    } catch (loadError) {
      pushToast({ title: 'Unable to open tenant', description: toErrorMessage(loadError, 'Unable to load tenant details.'), tone: 'danger' })
    } finally {
      setLoadingEditor(false)
    }
  }

  function closeEditor() {
    if (savingTenant) return
    setEditorMode(null)
    setEditingTenantId(null)
    setTenantForm(initialTenantForm)
    setSlugTouched(false)
  }

  function openDetailDrawer(tenantId: string) {
    setSelectedTenantId(tenantId)
  }

  function closeDetailDrawer() {
    setSelectedTenantId(null)
  }

  function updateTenantForm<K extends keyof UpsertPlatformTenantInput>(key: K, value: UpsertPlatformTenantInput[K]) {
    setTenantForm((current) => ({ ...current, [key]: value }))
  }

  function handleNameChange(value: string) {
    setTenantForm((current) => ({
      ...current,
      name: value,
      slug: slugTouched ? current.slug : slugifyName(value),
    }))
  }

  async function saveTenant() {
    if (!tenantForm.name.trim()) {
      pushToast({ title: 'Tenant name required', description: 'Enter a tenant name before saving.', tone: 'warning' })
      return
    }

    const normalizedSlug = slugifyName(tenantForm.slug)
    const slugError = getSlugValidationMessage(normalizedSlug)
    if (slugError) {
      pushToast({ title: 'Workspace URL name required', description: slugError, tone: 'warning' })
      return
    }

    setSavingTenant(true)
    try {
      const payload = {
        ...tenantForm,
        slug: normalizedSlug,
        name: tenantForm.name.trim(),
      }

      const saved = editorMode === 'edit' && editingTenantId
        ? await platformTenantService.updatePlatformTenant(editingTenantId, payload)
        : await platformTenantService.createPlatformTenant(payload)

      pushToast({
        title: editorMode === 'edit' ? 'Tenant updated' : 'Tenant created',
        description: `${saved.name} is now available in the Command Centre.`,
        tone: 'success',
      })

      setEditorMode(null)
      setEditingTenantId(null)
      setSelectedTenantId(saved.tenantId)
      await Promise.all([reload(), reloadDetail()])
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toErrorMessage(saveError, 'Unable to save the tenant.'), tone: 'danger' })
    } finally {
      setSavingTenant(false)
    }
  }

  function openStatusDialog(tenant: PlatformTenant | PlatformTenantDetail) {
    setStatusReason('')
    setStatusDialog({
      tenantId: tenant.tenantId,
      tenantName: tenant.name,
      currentStatus: tenant.status,
      selectedStatus: tenant.status,
      title: 'Change tenant status',
      confirmLabel: 'Save status',
    })
  }

  async function submitStatusUpdate() {
    if (!statusDialog) return

    if (statusDialog.selectedStatus !== 'Active' && !statusReason.trim()) {
      pushToast({ title: 'Reason required', description: 'Add a short reason before changing this tenant status.', tone: 'warning' })
      return
    }

    setSavingStatus(true)
    try {
      await platformTenantService.updatePlatformTenantStatus(statusDialog.tenantId, {
        status: statusDialog.selectedStatus,
        reason: statusReason.trim() || null,
      })

      pushToast({
        title: 'Tenant status updated',
        description: `${statusDialog.tenantName} is now ${statusDialog.selectedStatus.toLowerCase()}.`,
        tone: 'success',
      })

      setStatusDialog(null)
      setStatusReason('')
      await Promise.all([reload(), reloadDetail()])
    } catch (statusError) {
      pushToast({ title: 'Status update failed', description: toErrorMessage(statusError, 'Unable to update tenant status.'), tone: 'danger' })
    } finally {
      setSavingStatus(false)
    }
  }

  if (!canView) {
    return <ErrorState title="Permission denied" description="Platform tenant management is available only to Platform Owners." />
  }

  return (
    <div className="space-y-3">
      <PageHeader
        eyebrow="Platform"
        title="Command Centre"
        description="Manage tenants, access, and licensing."
      />

      {loading ? <LoadingState label="Loading platform tenants" /> : null}
      {!loading && error ? <ErrorState title="Unable to load command centre" description={error} /> : null}

      {!loading && !error && data.summary ? (
        <>
          <section className="grid gap-2.5 sm:grid-cols-2 xl:grid-cols-5">
            <StatCard title="Tenants" value={String(data.summary.totalTenants)} detail="Registered tenant workspaces." icon={Building2} />
            <StatCard title="Active Tenants" value={String(data.summary.activeTenants)} detail="Tenants currently available for access." icon={ShieldCheck} accent="emerald" />
            <StatCard title="Active Users" value={String(data.summary.activeUsersNow)} detail="Users active in the last 15 minutes." icon={Users} />
            <StatCard title="Logged In Today" value={String(data.summary.loggedInToday)} detail="Unique users seen today." icon={Activity} accent="amber" />
            <StatCard title="Tenants With Activity" value={String(data.summary.tenantsWithActiveUsers)} detail="Workspaces with recent session activity." icon={Users} />
          </section>

          <section className="space-y-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-app">Tenants</p>
                <p className="mt-1 text-sm text-muted">Manage tenant accounts and status.</p>
              </div>
              {canCreate ? (
                <button type="button" className="button-primary" onClick={() => void openCreateModal()}>
                  <Plus className="h-4 w-4" />
                  Add Tenant
                </button>
              ) : null}
            </div>

            <div className="grid gap-2.5 lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]">
              <label className="panel-subtle flex items-center gap-3 rounded-2xl px-4 py-2.5">
                <Search className="h-4 w-4 text-muted" />
                <input
                  value={searchQuery}
                  onChange={(event) => setSearchQuery(event.target.value)}
                  className="bg-transparent text-sm text-app outline-none placeholder:text-muted"
                  placeholder="Search by tenant name, slug, or contact email"
                />
              </label>

              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as typeof statusFilter)} className="field-input">
                <option value="All">All statuses</option>
                {tenantStatuses.map((status) => (
                  <option key={status} value={status}>{status}</option>
                ))}
              </select>

              <select value={planFilter} onChange={(event) => setPlanFilter(event.target.value)} className="field-input">
                {planOptions.map((plan) => (
                  <option key={plan} value={plan}>{plan === 'All' ? 'All plans' : plan}</option>
                ))}
              </select>

              <select value={licenseStatusFilter} onChange={(event) => setLicenseStatusFilter(event.target.value as typeof licenseStatusFilter)} className="field-input">
                <option value="All">All license states</option>
                {licenseStatuses.map((status) => (
                  <option key={status} value={status}>{status}</option>
                ))}
              </select>
            </div>

            {loadingEditor ? <LoadingState label="Loading tenant editor" /> : null}

            <DataTable
              rows={filteredTenants}
              rowKey={(row) => row.tenantId}
              pageSize={10}
              variant="open"
              onRowClick={(row) => openDetailDrawer(row.tenantId)}
              emptyTitle="No tenants match the current filters"
              emptyDescription="Clear the filters or add a tenant to get started."
              minTableWidth="min-w-[1250px] w-full"
              columns={[
                {
                  key: 'tenant',
                  header: 'Tenant Name',
                  cell: (row) => (
                    <button type="button" className="text-left" onClick={() => openDetailDrawer(row.tenantId)}>
                      <p className="font-semibold text-accent-strong">{row.name}</p>
                      <p className="mt-1 text-xs text-muted">Created {formatDateOnly(row.createdAt)}</p>
                    </button>
                  ),
                },
                { key: 'slug', header: 'Slug', cell: (row) => <span className="font-mono text-xs text-muted">{row.slug}</span> },
                { key: 'contact', header: 'Contact Person', cell: (row) => <span>{row.contactName || 'Not set'}</span> },
                { key: 'email', header: 'Contact Email', cell: (row) => <span>{row.contactEmail || 'Not set'}</span> },
                { key: 'plan', header: 'Plan', cell: (row) => <span>{row.planName || 'Not set'}</span> },
                { key: 'license', header: 'License Status', cell: (row) => <Badge tone={licenseTone(row.licenseStatus)}>{row.licenseStatus}</Badge> },
                { key: 'users', header: 'Users', cell: (row) => <span>{row.userCount}</span> },
                { key: 'branches', header: 'Branches', cell: (row) => <span>{row.branchCount}</span> },
                { key: 'status', header: 'Status', cell: (row) => <Badge tone={tenantTone(row.status)}>{row.status}</Badge> },
                { key: 'createdAt', header: 'Created At', cell: (row) => <span>{formatDateTime(row.createdAt)}</span> },
                {
                  key: 'actions',
                  header: 'Open',
                  className: 'min-w-[120px]',
                  cell: (row) => (
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => openDetailDrawer(row.tenantId)}>
                      <ArrowRight className="h-4 w-4" />
                      Open
                    </button>
                  ),
                },
              ]}
            />
          </section>
        </>
      ) : null}

      <Modal
        open={editorMode !== null}
        title={editorMode === 'edit' ? 'Edit tenant' : 'Add tenant'}
        description={editorMode === 'edit' ? 'Update tenant details and access.' : 'Create a new tenant.'}
        onClose={closeEditor}
        maxWidth="max-w-5xl"
      >
        <div className="space-y-3">
          <section>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-muted">Company / Tenant Details</p>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <Field label="Tenant Name"><input value={tenantForm.name} onChange={(event) => handleNameChange(event.target.value)} className="field-input" /></Field>
              <Field label="Workspace URL slug">
                <div className="space-y-2">
                  <div className="flex gap-2">
                    <input value={tenantForm.slug} onChange={(event) => { setSlugTouched(true); updateTenantForm('slug', slugifyName(event.target.value)) }} className="field-input font-mono" />
                    <button type="button" className="button-secondary px-3 py-2" onClick={() => { setSlugTouched(false); updateTenantForm('slug', slugifyName(tenantForm.name)) }}>
                      Regenerate
                    </button>
                  </div>
                  <p className="text-xs text-muted">Auto-generated from the company name. You can edit it if needed.</p>
                </div>
              </Field>
              <Field label="Contact Name"><input value={tenantForm.contactName || ''} onChange={(event) => updateTenantForm('contactName', event.target.value)} className="field-input" /></Field>
              <Field label="Contact Email"><input type="email" value={tenantForm.contactEmail || ''} onChange={(event) => updateTenantForm('contactEmail', event.target.value)} className="field-input" /></Field>
              <Field label="Contact Phone"><input value={tenantForm.contactPhone || ''} onChange={(event) => updateTenantForm('contactPhone', event.target.value)} className="field-input" /></Field>
              <Field label="Country"><input value={tenantForm.country || ''} onChange={(event) => updateTenantForm('country', event.target.value)} className="field-input" /></Field>
              <Field label="County"><input value={tenantForm.county || ''} onChange={(event) => updateTenantForm('county', event.target.value)} className="field-input" /></Field>
              <Field label="City"><input value={tenantForm.city || ''} onChange={(event) => updateTenantForm('city', event.target.value)} className="field-input" /></Field>
              <Field label="Address"><input value={tenantForm.address || ''} onChange={(event) => updateTenantForm('address', event.target.value)} className="field-input" /></Field>
              <Field label="Tax PIN"><input value={tenantForm.taxPin || ''} onChange={(event) => updateTenantForm('taxPin', event.target.value)} className="field-input" /></Field>
            </div>
          </section>

          <section>
            <p className="text-sm font-semibold uppercase tracking-[0.18em] text-muted">Subscription / License</p>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <Field label="Plan Name"><input value={tenantForm.planName || ''} onChange={(event) => updateTenantForm('planName', event.target.value)} className="field-input" placeholder="Trial, Starter, Professional, Enterprise" /></Field>
              <Field label="License Status">
                <select value={tenantForm.licenseStatus} onChange={(event) => updateTenantForm('licenseStatus', event.target.value as PlatformLicenseStatus)} className="field-input">
                  {licenseStatuses.map((status) => (
                    <option key={status} value={status}>{status}</option>
                  ))}
                </select>
              </Field>
              <Field label="Max Users"><input type="number" min={1} value={tenantForm.maxUsers ?? ''} onChange={(event) => updateTenantForm('maxUsers', event.target.value ? Number(event.target.value) : null)} className="field-input" /></Field>
              <Field label="Max Branches"><input type="number" min={1} value={tenantForm.maxBranches ?? ''} onChange={(event) => updateTenantForm('maxBranches', event.target.value ? Number(event.target.value) : null)} className="field-input" /></Field>
              <Field label="Trial Ends At"><input type="date" value={toDateInputValue(tenantForm.trialEndsAt)} onChange={(event) => updateTenantForm('trialEndsAt', event.target.value || null)} className="field-input" /></Field>
              <Field label="Subscription Ends At"><input type="date" value={toDateInputValue(tenantForm.subscriptionEndsAt)} onChange={(event) => updateTenantForm('subscriptionEndsAt', event.target.value || null)} className="field-input" /></Field>
              <Field label="Status">
                <select value={tenantForm.status} onChange={(event) => updateTenantForm('status', event.target.value as PlatformTenantStatus)} className="field-input">
                  {tenantStatuses.map((status) => (
                    <option key={status} value={status}>{status}</option>
                  ))}
                </select>
              </Field>
            </div>
          </section>

          <div className="flex flex-wrap justify-end gap-3">
            <button type="button" className="button-secondary" onClick={closeEditor} disabled={savingTenant}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void saveTenant()} disabled={savingTenant}>
              {savingTenant ? (editorMode === 'edit' ? 'Saving Changes...' : 'Creating Tenant...') : (editorMode === 'edit' ? 'Save Changes' : 'Create Tenant')}
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        open={statusDialog !== null}
        title={statusDialog?.title || 'Update tenant status'}
        description="Select a target status and add a reason when needed."
        onClose={() => { if (!savingStatus) setStatusDialog(null) }}
      >
        <div className="space-y-3">
          <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">
            Tenant: <span className="font-semibold text-app">{statusDialog?.tenantName}</span>
            <span className="ml-2">Current status: <span className="font-semibold text-app">{statusDialog?.currentStatus}</span></span>
          </div>
          <Field label="Status">
            <select
              value={statusDialog?.selectedStatus || 'Active'}
              onChange={(event) => setStatusDialog((current) => current ? { ...current, selectedStatus: event.target.value as PlatformTenantStatus } : current)}
              className="field-input"
              disabled={savingStatus}
            >
              {tenantStatuses.map((status) => (
                <option key={status} value={status}>{status}</option>
              ))}
            </select>
          </Field>
          <Field label="Reason">
            <textarea
              value={statusReason}
              onChange={(event) => setStatusReason(event.target.value)}
              className="field-input min-h-[120px]"
              placeholder={statusDialog?.selectedStatus === 'Active' ? 'Optional activation note' : 'Why is this tenant being suspended or deactivated?'}
            />
          </Field>
          <div className="flex flex-wrap justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setStatusDialog(null)} disabled={savingStatus}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void submitStatusUpdate()} disabled={savingStatus}>
              {savingStatus ? 'Applying...' : statusDialog?.confirmLabel || 'Confirm'}
            </button>
          </div>
        </div>
      </Modal>

      <Modal
        open={Boolean(selectedTenantId)}
        title={detailData.detail?.name || 'Tenant details'}
        description="Tenant profile and recent activity."
        onClose={closeDetailDrawer}
        maxWidth="max-w-5xl"
      >
        {detailLoading ? <LoadingState label="Loading tenant details" /> : null}
        {!detailLoading && detailError ? <ErrorState title="Unable to load tenant details" description={detailError} /> : null}
        {!detailLoading && !detailError && detailData.detail ? (
          <div className="space-y-5">
            <div className="flex flex-wrap gap-2">
              {canEdit ? (
                <button type="button" className="button-secondary px-3 py-2" onClick={() => void openEditModal(detailData.detail!.tenantId)}>
                  <PencilLine className="h-4 w-4" />
                  Edit Tenant
                </button>
              ) : null}
              {canUpdateStatus ? (
                <button type="button" className="button-secondary px-3 py-2" onClick={() => openStatusDialog(detailData.detail!)}>
                  Change Status
                </button>
              ) : null}
            </div>

            <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
              <StatCard title="Users" value={String(detailData.detail.userCount)} detail="Active tenant users on record." icon={Users} />
              <StatCard title="Branches" value={String(detailData.detail.branchCount)} detail="Active branches attached to this tenant." icon={Building2} accent="emerald" />
              <StatCard title="Work Orders" value={String(detailData.detail.workOrderCount)} detail="Total work orders stored for this tenant." icon={Activity} accent="amber" />
              <StatCard title="Active Users Now" value={String(detailData.detail.activeUsersNow)} detail="Users seen in the last 15 minutes." icon={ShieldCheck} accent="rose" />
            </div>

            <section className="surface-card">
              <div className="grid gap-4 md:grid-cols-2">
                <DetailItem label="Tenant Name" value={detailData.detail.name} />
                <DetailItem label="Slug" value={detailData.detail.slug} mono />
                <DetailItem label="Contact Name" value={detailData.detail.contactName} />
                <DetailItem label="Contact Email" value={detailData.detail.contactEmail} />
                <DetailItem label="Contact Phone" value={detailData.detail.contactPhone} />
                <DetailItem label="Country" value={detailData.detail.country} />
                <DetailItem label="County" value={detailData.detail.county} />
                <DetailItem label="City" value={detailData.detail.city} />
                <DetailItem label="Address" value={detailData.detail.address} />
                <DetailItem label="Tax PIN" value={detailData.detail.taxPin} />
                <DetailItem label="Plan" value={detailData.detail.planName} />
                <DetailItem label="License Status" value={<Badge tone={licenseTone(detailData.detail.licenseStatus)}>{detailData.detail.licenseStatus}</Badge>} />
                <DetailItem label="Status" value={<Badge tone={tenantTone(detailData.detail.status)}>{detailData.detail.status}</Badge>} />
                <DetailItem label="Max Users" value={detailData.detail.maxUsers != null ? String(detailData.detail.maxUsers) : null} />
                <DetailItem label="Max Branches" value={detailData.detail.maxBranches != null ? String(detailData.detail.maxBranches) : null} />
                <DetailItem label="Trial Ends" value={formatMaybeDate(detailData.detail.trialEndsAt)} />
                <DetailItem label="Subscription Ends" value={formatMaybeDate(detailData.detail.subscriptionEndsAt)} />
                <DetailItem label="Created" value={formatDateTime(detailData.detail.createdAt)} />
                <DetailItem label="Last Updated" value={formatDateTime(detailData.detail.updatedAt || undefined)} />
                <DetailItem label="Last Activity" value={formatDateTime(detailData.detail.lastActivityAt || undefined)} />
              </div>
            </section>

            <section className="surface-card">
              <div className="mb-4">
                <p className="text-lg font-semibold text-app">Audit timeline</p>
                <p className="mt-1 text-sm text-muted">Recent activity.</p>
              </div>
              {detailData.auditLogs.length === 0 ? (
                <EmptyState title="No audit events yet" description="This tenant does not have platform audit entries yet." actionLabel="Refresh" onAction={() => void reloadDetail()} />
              ) : (
                <div className="space-y-3">
                  {detailData.auditLogs.map((entry) => (
                    <article key={entry.id} className="panel-subtle rounded-2xl p-4">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-semibold text-app">{entry.action}</p>
                        <p className="text-xs text-muted">{formatDateTime(entry.createdAt)}</p>
                      </div>
                      <p className="mt-2 text-sm text-muted">{entry.details || 'No additional detail was recorded for this event.'}</p>
                      <p className="mt-2 text-xs uppercase tracking-[0.18em] text-muted">Actor: {entry.actor}</p>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </div>
        ) : null}
      </Modal>
    </div>
  )
}

function tenantTone(status: PlatformTenantStatus) {
  if (status === 'Active') return 'success'
  if (status === 'Trial') return 'info'
  if (status === 'Inactive') return 'neutral'
  return 'danger'
}

function licenseTone(status: PlatformLicenseStatus) {
  if (status === 'Active') return 'success'
  if (status === 'Trial') return 'info'
  if (status === 'Expired') return 'warning'
  return 'danger'
}

function toDateInputValue(value?: string | null) {
  return value ? value.slice(0, 10) : ''
}

function formatMaybeDate(value?: string | null) {
  return value ? formatDateOnly(value) : 'Not set'
}

function toErrorMessage(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="space-y-2">
      <span className="text-xs font-semibold uppercase tracking-[0.16em] text-muted">{label}</span>
      {children}
    </label>
  )
}

function DetailItem({ label, value, mono = false }: { label: string; value: ReactNode; mono?: boolean }) {
  return (
    <div className="panel-subtle rounded-2xl p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <div className={`mt-2 text-sm text-app ${mono ? 'font-mono' : ''}`}>{value || 'Not set'}</div>
    </div>
  )
}
