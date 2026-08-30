import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Search, SlidersHorizontal } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { LastUpdated } from '../../shared/components/LastUpdated'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { Field } from '../common'
import {
  useCreateTenant,
  useDeactivateTenant,
  useExtendTenantTrial,
  useLead,
  useSuspendTenant,
  useTenantsPaged,
} from '../api'
import { licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from '../common'
import type { PlatformTenantListItem } from '../types'

const statusOptions = ['All', 'Active', 'Trial', 'Suspended', 'Inactive'] as const
const trialOptions = ['All', 'TrialActive', 'TrialExpiringSoon', 'TrialExpired', 'TrialExtended', 'PaidActive'] as const

type StatusFilter = (typeof statusOptions)[number]
type TrialFilter = (typeof trialOptions)[number]

type CreateTenantForm = {
  companyName: string
  slug: string
  companyEmail: string
  companyPhone: string
  industry: string
  contactName: string
  contactEmail: string
  contactPhone: string
  country: string
  county: string
  city: string
  address: string
  taxPin: string
  planName: string
  maxUsers: number | null
  maxBranches: number | null
  trialLengthDays: number
  createDefaultAdmin: boolean
  usePrimaryContactAsWorkspaceAdmin: boolean
  adminFullName: string
  adminEmail: string
  adminPhone: string
}

const emptyForm: CreateTenantForm = {
  companyName: '',
  slug: '',
  companyEmail: '',
  companyPhone: '',
  industry: '',
  contactName: '',
  contactEmail: '',
  contactPhone: '',
  country: '',
  county: '',
  city: '',
  address: '',
  taxPin: '',
  planName: '',
  maxUsers: 25,
  maxBranches: 3,
  trialLengthDays: 14,
  createDefaultAdmin: true,
  usePrimaryContactAsWorkspaceAdmin: true,
  adminFullName: '',
  adminEmail: '',
  adminPhone: '',
}

export function TenantsPage() {
  const canCreate = usePermission('platform.tenants.create')
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>(() => {
    const seed = searchParams.get('status')
    return (statusOptions as readonly string[]).includes(seed ?? '') ? (seed as StatusFilter) : 'All'
  })
  const [trialFilter, setTrialFilter] = useState<TrialFilter>(() => {
    const seed = searchParams.get('trial')
    return (trialOptions as readonly string[]).includes(seed ?? '') ? (seed as TrialFilter) : 'All'
  })
  const [sortBy, setSortBy] = useState<'name' | 'createdAt' | 'trialEndsAt' | 'status'>('name')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const debouncedSearch = useDebounce(search, 250)
  useEffect(() => setPage(1), [debouncedSearch, statusFilter, trialFilter, sortBy, sortDirection])
  const { data, isLoading, isError, dataUpdatedAt } = useTenantsPaged(
    {
      search: debouncedSearch || undefined,
      status: statusFilter === 'All' ? undefined : statusFilter,
      trial: trialFilter === 'All' ? undefined : trialFilter,
      sortBy,
      sortDir: sortDirection,
    },
    page,
    pageSize,
    { refetchInterval: 30_000 },
  )
  const currentLeadId = searchParams.get('leadId')
  const lead = useLead(currentLeadId ?? undefined)

  const [showCreate, setShowCreate] = useState(false)
  const [createForm, setCreateForm] = useState(emptyForm)
  const [prefilledLeadId, setPrefilledLeadId] = useState<string | null>(null)
  const [showSuspend, setShowSuspend] = useState<PlatformTenantListItem | null>(null)
  const [showDeactivate, setShowDeactivate] = useState<PlatformTenantListItem | null>(null)
  const [trialTarget, setTrialTarget] = useState<PlatformTenantListItem | null>(null)

  const createTenant = useCreateTenant()
  const suspendTenant = useSuspendTenant(showSuspend?.tenantId ?? '')
  const deactivateTenant = useDeactivateTenant(showDeactivate?.tenantId ?? '')
  const extendTenantTrial = useExtendTenantTrial(trialTarget?.tenantId ?? '')

  // Search / filter / sort / paging all run server-side now (see useTenantsPaged).
  const rows = data?.items ?? []

  const columns: DataTableColumn<PlatformTenantListItem>[] = [
    {
      key: 'name',
      header: 'Tenant',
      cell: (row) => (
        <div>
          <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/platform/tenants/${row.tenantId}`}>
            {row.name}
          </Link>
          <div className="mt-1 text-xs text-slate-500">{row.slug}</div>
        </div>
      ),
    },
    { key: 'contact', header: 'Primary contact', cell: (row) => <div>{row.contactName ?? 'Not set'}<div className="text-xs text-slate-500">{row.contactEmail ?? 'No email'}</div></div> },
    { key: 'plan', header: 'Plan', cell: (row) => row.planName ?? 'No plan' },
    { key: 'status', header: 'Status', cell: (row) => tenantStatusBadge(row.status) },
    { key: 'license', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
    { key: 'trial', header: 'Trial', cell: (row) => trialStatusBadge(row.trialStatus) },
    { key: 'days', header: 'Days remaining', cell: (row) => row.trialDaysRemaining ?? '—' },
    { key: 'users', header: 'Users', cell: (row) => row.userCount },
    { key: 'branches', header: 'Sites', cell: (row) => row.branchCount },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
    {
      key: 'actions',
      header: 'Account Management',
      cell: (row) => (
        <div className="flex flex-wrap gap-2">
          {row.status !== 'Active' ? (
            <button type="button" className="inline-flex h-9 items-center rounded-lg bg-[var(--app-primary)] px-3 text-xs font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => void api.post(endpoints.platform.tenantActivate(row.tenantId)).then(() => toast.success('Tenant activated'))}>
              Activate
            </button>
          ) : null}
          {row.status === 'Active' ? (
            <button type="button" className="inline-flex h-9 items-center rounded-lg border border-amber-200 bg-amber-50 px-3 text-xs font-medium text-amber-800 hover:bg-amber-100" onClick={() => setShowSuspend(row)}>
              Suspend
            </button>
          ) : null}
          {!row.trialExtensionUsed ? (
            <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setTrialTarget(row)}>
              Extend trial
            </button>
          ) : null}
          {row.status !== 'Inactive' ? (
            <button type="button" className="inline-flex h-9 items-center rounded-lg border border-rose-200 bg-rose-50 px-3 text-xs font-medium text-rose-700 hover:bg-rose-100" onClick={() => setShowDeactivate(row)}>
              Deactivate
            </button>
          ) : null}
        </div>
      ),
    },
  ]

  const closeCreate = () => {
    setShowCreate(false)
    setCreateForm(emptyForm)
    setPrefilledLeadId(null)
  }

  const submitCreate = async () => {
    if (!createForm.companyName.trim() || !createForm.slug.trim()) {
      toast.error('Company name and workspace slug are required.')
      return
    }

    if (createForm.usePrimaryContactAsWorkspaceAdmin) {
      if (!createForm.contactName.trim() || !createForm.contactEmail.trim()) {
        toast.error('Primary contact name and email are required for the first admin.')
        return
      }
    } else if (!createForm.adminFullName.trim() || !createForm.adminEmail.trim()) {
      toast.error('First admin full name and email are required.')
      return
    }

    try {
      const payload = {
        companyName: createForm.companyName.trim(),
        slug: createForm.slug.trim(),
        ...(createForm.companyEmail.trim() ? { companyEmail: createForm.companyEmail.trim() } : {}),
        ...(createForm.companyPhone.trim() ? { companyPhone: createForm.companyPhone.trim() } : {}),
        ...(createForm.industry.trim() ? { industry: createForm.industry.trim() } : {}),
        ...(createForm.contactName.trim() ? { contactName: createForm.contactName.trim() } : {}),
        ...(createForm.contactEmail.trim() ? { contactEmail: createForm.contactEmail.trim() } : {}),
        ...(createForm.contactPhone.trim() ? { contactPhone: createForm.contactPhone.trim() } : {}),
        ...(createForm.contactName.trim() ? { primaryContactName: createForm.contactName.trim() } : {}),
        ...(createForm.contactEmail.trim() ? { primaryContactEmail: createForm.contactEmail.trim() } : {}),
        ...(createForm.contactPhone.trim() ? { primaryContactPhone: createForm.contactPhone.trim() } : {}),
        ...(createForm.country.trim() ? { country: createForm.country.trim() } : {}),
        ...(createForm.county.trim() ? { county: createForm.county.trim() } : {}),
        ...(createForm.city.trim() ? { city: createForm.city.trim() } : {}),
        ...(createForm.address.trim() ? { address: createForm.address.trim() } : {}),
        ...(createForm.taxPin.trim() ? { taxPin: createForm.taxPin.trim() } : {}),
        ...(createForm.planName.trim() ? { planName: createForm.planName.trim() } : {}),
        maxUsers: createForm.maxUsers,
        maxBranches: createForm.maxBranches,
        trialEndsAt: null,
        subscriptionEndsAt: null,
        startsAt: null,
        expiresAt: null,
        status: 'Active',
        licenseStatus: 'Trial',
        createDefaultAdmin: createForm.createDefaultAdmin,
        usePrimaryContactAsWorkspaceAdmin: createForm.usePrimaryContactAsWorkspaceAdmin,
        adminFullName: createForm.usePrimaryContactAsWorkspaceAdmin ? createForm.contactName.trim() : createForm.adminFullName.trim(),
        adminEmail: createForm.usePrimaryContactAsWorkspaceAdmin ? createForm.contactEmail.trim() : createForm.adminEmail.trim(),
        ...(createForm.usePrimaryContactAsWorkspaceAdmin
          ? (createForm.contactPhone.trim() ? { adminPhone: createForm.contactPhone.trim() } : {})
          : (createForm.adminPhone.trim() ? { adminPhone: createForm.adminPhone.trim() } : {})),
      } as const
      const created = await createTenant.mutateAsync(payload)
      toast.success(`Tenant ${created.name} created. The first admin was created inside the workspace.`)
      closeCreate()
      navigate(`/platform/tenants/${created.tenantId}`)
    } catch {
      // toast handled by interceptor
    }
  }

  useEffect(() => {
    if (!showCreate || !lead.data || prefilledLeadId === lead.data.id) {
      return
    }

    setCreateForm((current) => ({
      ...current,
      companyName: lead.data.companyName,
      slug: current.slug || lead.data.companyName.trim().toLowerCase().replace(/[^a-z0-9-]/g, '-'),
      industry: lead.data.industry ?? current.industry,
      contactName: lead.data.contactPersonName ?? current.contactName,
      contactEmail: lead.data.email ?? current.contactEmail,
      contactPhone: lead.data.phone ?? current.contactPhone,
      country: lead.data.country ?? current.country,
      adminFullName: lead.data.contactPersonName ?? current.adminFullName,
      adminEmail: lead.data.email ?? current.adminEmail,
      adminPhone: lead.data.phone ?? current.adminPhone,
    }))
    setPrefilledLeadId(lead.data.id)
  }, [lead.data, prefilledLeadId, showCreate])

  return (
    <div className="space-y-5" data-testid="tenants-page">
      <PageHeader
        title="Tenants"
        subtitle="Workspace registry, lifecycle control, and account management."
        secondaryActions={<LastUpdated at={dataUpdatedAt} />}
        primaryAction={
          canCreate ? (
            <button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => setShowCreate(true)}>
              <Plus className="h-4 w-4" />
              Create tenant
            </button>
          ) : undefined
        }
      />

      {currentLeadId ? (
        <div className="rounded-2xl border border-sky-200 bg-sky-50 px-4 py-3 text-sm text-sky-900">
          This workspace can be created from lead <Link className="font-medium underline" to={`/platform/leads?lead=${currentLeadId}`}>{currentLeadId}</Link>.
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]">
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2">
          <Search className="h-4 w-4 text-slate-400" />
          <input
            data-testid="tenants-search-input"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search tenants, slug, or contact"
            className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400"
          />
        </label>

        <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as StatusFilter)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
          {statusOptions.map((status) => <option key={status} value={status}>{status === 'All' ? 'All statuses' : status}</option>)}
        </select>

        <select value={trialFilter} onChange={(event) => setTrialFilter(event.target.value as TrialFilter)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
          {trialOptions.map((status) => <option key={status} value={status}>{status === 'All' ? 'All trial states' : status}</option>)}
        </select>

        <div className="flex items-center gap-2">
          <SlidersHorizontal className="h-4 w-4 text-slate-400" />
          <select value={sortBy} onChange={(event) => setSortBy(event.target.value as typeof sortBy)} className="h-11 flex-1 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
            <option value="name">Sort by name</option>
            <option value="createdAt">Sort by created date</option>
            <option value="trialEndsAt">Sort by trial end</option>
            <option value="status">Sort by status</option>
          </select>
          <button type="button" className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm text-slate-700" onClick={() => setSortDirection((current) => (current === 'asc' ? 'desc' : 'asc'))}>
            {sortDirection === 'asc' ? 'Asc' : 'Desc'}
          </button>
        </div>
      </div>

      {isLoading ? <Spinner label="Loading tenants" /> : null}
      {!isLoading && isError ? <EmptyState title="Tenants unavailable" description="Tenant records could not be loaded." /> : null}

      {!isLoading && !isError ? (
        rows.length === 0 ? (
          <EmptyState title="No tenants found" description="Adjust filters or create a new tenant workspace." />
        ) : (
          <DataTable<PlatformTenantListItem>
            columns={columns}
            data={rows}
            loading={isLoading}
            emptyTitle={isError ? 'Tenants unavailable' : 'No tenants found'}
            emptyDescription={isError ? 'The tenant list could not be loaded.' : 'Adjust filters or create a new tenant workspace.'}
            pagination={{
              page,
              pageSize,
              total: data?.totalCount ?? 0,
              onPageChange: setPage,
              onPageSizeChange: (size) => { setPageSize(size); setPage(1) },
            }}
          />
        )
      ) : null}

      <FormDrawer
        open={showCreate}
        title="Create tenant"
        onClose={closeCreate}
        footer={
          <div className="flex items-center justify-end gap-2">
            <button type="button" className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={closeCreate}>
              Cancel
            </button>
            <button type="button" className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:opacity-60" onClick={() => void submitCreate()} disabled={createTenant.isPending}>
              {createTenant.isPending ? 'Creating...' : 'Create tenant'}
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Field label="Company / tenant name">
              <input value={createForm.companyName} onChange={(event) => setCreateForm((current) => ({ ...current, companyName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Workspace slug">
              <input value={createForm.slug} onChange={(event) => setCreateForm((current) => ({ ...current, slug: event.target.value.trim().toLowerCase().replace(/[^a-z0-9-]/g, '-') }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 font-mono text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Industry">
              <input value={createForm.industry} onChange={(event) => setCreateForm((current) => ({ ...current, industry: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Plan">
              <input value={createForm.planName} onChange={(event) => setCreateForm((current) => ({ ...current, planName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" placeholder="Trial, Starter, Pro..." />
            </Field>
            <Field label="Contact name">
              <input value={createForm.contactName} onChange={(event) => setCreateForm((current) => ({ ...current, contactName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Contact email">
              <input type="email" value={createForm.contactEmail} onChange={(event) => setCreateForm((current) => ({ ...current, contactEmail: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Contact phone">
              <input value={createForm.contactPhone} onChange={(event) => setCreateForm((current) => ({ ...current, contactPhone: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Country">
              <input value={createForm.country} onChange={(event) => setCreateForm((current) => ({ ...current, country: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="County">
              <input value={createForm.county} onChange={(event) => setCreateForm((current) => ({ ...current, county: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="City">
              <input value={createForm.city} onChange={(event) => setCreateForm((current) => ({ ...current, city: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Max users">
              <input type="number" value={createForm.maxUsers ?? ''} onChange={(event) => setCreateForm((current) => ({ ...current, maxUsers: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
            <Field label="Max branches / sites">
              <input type="number" value={createForm.maxBranches ?? ''} onChange={(event) => setCreateForm((current) => ({ ...current, maxBranches: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
            </Field>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
            <label className="flex items-center justify-between gap-3 text-sm text-slate-700">
              <span>Use primary contact as first workspace admin</span>
              <input type="checkbox" checked={createForm.usePrimaryContactAsWorkspaceAdmin} onChange={(event) => setCreateForm((current) => ({ ...current, usePrimaryContactAsWorkspaceAdmin: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
            </label>
            <label className="mt-3 flex items-center justify-between gap-3 text-sm text-slate-700">
              <span>Create default admin account</span>
              <input type="checkbox" checked={createForm.createDefaultAdmin} onChange={(event) => setCreateForm((current) => ({ ...current, createDefaultAdmin: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" />
            </label>
            {!createForm.usePrimaryContactAsWorkspaceAdmin ? (
              <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
                <Field label="First admin full name">
                  <input value={createForm.adminFullName} onChange={(event) => setCreateForm((current) => ({ ...current, adminFullName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
                </Field>
                <Field label="First admin email">
                  <input type="email" value={createForm.adminEmail} onChange={(event) => setCreateForm((current) => ({ ...current, adminEmail: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
                </Field>
                <Field label="First admin phone">
                  <input value={createForm.adminPhone} onChange={(event) => setCreateForm((current) => ({ ...current, adminPhone: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
                </Field>
              </div>
            ) : null}
          </div>
        </div>
      </FormDrawer>

      <ConfirmDialog
        open={showSuspend !== null}
        title="Suspend tenant"
        message={showSuspend ? `Suspend ${showSuspend.name}? The tenant will be blocked until reactivated.` : 'Suspend tenant?'}
        confirmLabel="Suspend"
        danger
        onCancel={() => setShowSuspend(null)}
        onConfirm={() => {
          if (!showSuspend) return
          void suspendTenant.mutateAsync('Suspended by platform owner').then(() => {
            toast.success('Tenant suspended')
            setShowSuspend(null)
          })
        }}
      />

      <ConfirmDialog
        open={showDeactivate !== null}
        title="Deactivate tenant"
        message={showDeactivate ? `Deactivate ${showDeactivate.name}? This moves the workspace offline.` : 'Deactivate tenant?'}
        confirmLabel="Deactivate"
        danger
        {...(showDeactivate ? { confirmText: showDeactivate.slug } : {})}
        onCancel={() => setShowDeactivate(null)}
        onConfirm={() => {
          if (!showDeactivate) return
          void deactivateTenant.mutateAsync('Deactivated by platform owner').then(() => {
            toast.success('Tenant deactivated')
            setShowDeactivate(null)
          })
        }}
      />

      <ConfirmDialog
        open={trialTarget !== null}
        title="Extend trial"
        message={trialTarget ? `Extend ${trialTarget.name} by 14 days? This can only be done once.` : 'Extend trial?'}
        confirmLabel="Extend"
        onCancel={() => setTrialTarget(null)}
        onConfirm={() => {
          if (!trialTarget) return
          void extendTenantTrial.mutateAsync(undefined).then(() => {
            toast.success('Trial extended')
            setTrialTarget(null)
          })
        }}
      />
    </div>
  )
}
