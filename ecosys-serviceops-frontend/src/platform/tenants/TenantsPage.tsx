import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Plus, Search, SlidersHorizontal } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { Field } from '../common'
import { useCreateTenant, useLead, useTenants } from '../api'
import { licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from '../common'
import type { PlatformTenantListItem } from '../types'
import { buildCreateTenantPayload, DEFAULT_LICENSED_USERS, type CreateTenantForm } from './licensedUsers'

const statusOptions = ['All', 'Active', 'Trial', 'Suspended', 'Inactive'] as const
const trialOptions = ['All', 'TrialActive', 'TrialExpiringSoon', 'TrialExpired', 'TrialExtended', 'PaidActive'] as const

type StatusFilter = (typeof statusOptions)[number]
type TrialFilter = (typeof trialOptions)[number]

const countryTimeZones: Record<string, string> = {
  kenya: 'Africa/Nairobi', uganda: 'Africa/Kampala', rwanda: 'Africa/Kigali',
  tanzania: 'Africa/Dar_es_Salaam', nigeria: 'Africa/Lagos',
  'south africa': 'Africa/Johannesburg', 'united kingdom': 'Europe/London',
}

// A suggestion only: the server normalizes, validates and checks uniqueness.
function suggestSlug(companyName: string) {
  return companyName.toLowerCase().replace(/\b(ltd|limited|inc|llc)\.?$/g, '').trim()
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '')
}

const emptyForm: CreateTenantForm = {
  companyName: '', slug: '', planCode: 'suite', adminFullName: '', adminEmail: '',
  country: 'Kenya', timeZoneId: 'Africa/Nairobi', maxUsers: String(DEFAULT_LICENSED_USERS),
}

export function TenantsPage() {
  const canCreate = usePermission('platform.tenants.create')
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { data, isLoading, isError } = useTenants()
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [trialFilter, setTrialFilter] = useState<TrialFilter>('All')
  const [sortBy, setSortBy] = useState<'name' | 'createdAt' | 'trialEndsAt' | 'status'>('name')
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('asc')
  const debouncedSearch = useDebounce(search, 250)
  const currentLeadId = searchParams.get('leadId')
  const lead = useLead(currentLeadId ?? undefined)

  const [showCreate, setShowCreate] = useState(false)
  const [createForm, setCreateForm] = useState(emptyForm)
  const [slugEdited, setSlugEdited] = useState(false)
  const [prefilledLeadId, setPrefilledLeadId] = useState<string | null>(null)

  const createTenant = useCreateTenant()

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    const filtered = (data ?? []).filter((tenant) => {
      const matchesSearch =
        !query
        || tenant.name.toLowerCase().includes(query)
        || tenant.slug.toLowerCase().includes(query)
        || (tenant.contactName ?? '').toLowerCase().includes(query)
        || (tenant.contactEmail ?? '').toLowerCase().includes(query)
      const matchesStatus = statusFilter === 'All' || tenant.status === statusFilter
      const matchesTrial = trialFilter === 'All' || tenant.trialStatus === trialFilter
      return matchesSearch && matchesStatus && matchesTrial
    })

    const direction = sortDirection === 'asc' ? 1 : -1
    return filtered.sort((a, b) => {
      const left = sortBy === 'createdAt'
        ? a.createdAt
        : sortBy === 'trialEndsAt'
          ? a.trialEndsAt ?? ''
          : sortBy === 'status'
            ? a.status
            : a.name
      const right = sortBy === 'createdAt'
        ? b.createdAt
        : sortBy === 'trialEndsAt'
          ? b.trialEndsAt ?? ''
          : sortBy === 'status'
            ? b.status
            : b.name
      return left.localeCompare(right) * direction
    })
  }, [data, debouncedSearch, sortBy, sortDirection, statusFilter, trialFilter])

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
    { key: 'admin', header: 'Primary admin', cell: (row) => <div>{row.primaryAdminEmail ?? 'No active admin'}<div className="text-xs text-slate-500">{row.contactName ?? ''}</div></div> },
    {
      key: 'plan',
      header: 'Plan',
      cell: (row) => (
        <div>
          {row.planName ?? 'No plan'}
          <div className="text-xs text-slate-500">{(row.modules ?? []).map((m) => (m === 'operations' ? 'Operations' : m === 'finance' ? 'Finance' : m)).join(' + ') || '—'}</div>
        </div>
      ),
    },
    { key: 'status', header: 'Status', cell: (row) => tenantStatusBadge(row.status) },
    { key: 'license', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
    { key: 'trial', header: 'Trial', cell: (row) => trialStatusBadge(row.trialStatus) },
    { key: 'expires', header: 'License expires', cell: (row) => (row.licenseExpiresAt ? formatDate(row.licenseExpiresAt) : '—') },
    {
      key: 'seats',
      header: 'Licensed users',
      cell: (row) => {
        const full = row.seatLimit != null && row.userCount >= row.seatLimit
        return <span className={full ? 'font-medium text-amber-700' : undefined}>{row.userCount} / {row.seatLimit ?? '—'}</span>
      },
    },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
    {
      key: 'actions',
      header: '',
      // Lifecycle actions (activate, suspend with a reason, renew, seats…) live on the tenant page only,
      // so there is one place to change access and every action carries its reason into the audit trail.
      cell: (row) => (
        <Link className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" to={`/platform/tenants/${row.tenantId}`}>
          Manage
        </Link>
      ),
    },
  ]

  const closeCreate = () => {
    setShowCreate(false)
    setCreateForm(emptyForm)
    setSlugEdited(false)
    setPrefilledLeadId(null)
  }

  const submitCreate = async () => {
    const result = buildCreateTenantPayload(createForm)
    if (!result.ok) {
      toast.error(result.error)
      return
    }

    try {
      const created = await createTenant.mutateAsync(result.payload)
      if (created.initialAdminInvitationSent === false) {
        toast.error(`Tenant ${created.name} created. Invitation delivery failed; resend it from Users & Access.`)
      } else {
        toast.success(`Tenant ${created.name} created. Administrator invitation sent.`)
      }
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
      slug: current.slug || suggestSlug(lead.data.companyName),
      country: lead.data.country ?? current.country,
      timeZoneId: countryTimeZones[(lead.data.country ?? '').toLowerCase()] ?? current.timeZoneId,
      adminFullName: lead.data.contactPersonName ?? current.adminFullName,
      adminEmail: lead.data.email ?? current.adminEmail,
    }))
    setPrefilledLeadId(lead.data.id)
  }, [lead.data, prefilledLeadId, showCreate])

  return (
    <div className="space-y-5" data-testid="tenants-page">
      <PageHeader
        title="Tenants"
        subtitle="Workspace registry, lifecycle control, and account management."
        primaryAction={
          canCreate ? (
            <button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => setShowCreate(true)}>
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

      <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm lg:grid-cols-[minmax(0,2fr)_repeat(3,minmax(0,1fr))]">
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
          <DataTable<PlatformTenantListItem> columns={columns} data={rows} emptyTitle="No tenants found" emptyDescription="Adjust filters or create a new tenant workspace." />
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
            <button type="button" className="inline-flex h-11 items-center justify-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800 disabled:opacity-60" onClick={() => void submitCreate()} disabled={createTenant.isPending}>
              {createTenant.isPending ? 'Creating...' : 'Create tenant'}
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          <p className="text-sm text-slate-600">Create a ready-to-use workspace and invite its administrator. Company details can be completed in Settings after sign-in.</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Field label="Company Name">
              <input required value={createForm.companyName} onChange={(event) => setCreateForm((current) => ({ ...current, companyName: event.target.value, slug: slugEdited ? current.slug : suggestSlug(event.target.value) }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
            </Field>
            <Field label="Tenant Slug">
              <input required value={createForm.slug} onChange={(event) => { setSlugEdited(true); setCreateForm((current) => ({ ...current, slug: event.target.value })) }} className="h-11 w-full rounded-lg border border-slate-200 px-3 font-mono text-sm outline-none focus:border-slate-900" />
            </Field>
          </div>
          <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4" aria-live="polite">
            <p className="text-xs font-semibold uppercase tracking-wider text-emerald-800">ServiceOps Email</p>
            <p className="mt-1 break-all text-sm font-medium text-emerald-950">{suggestSlug(createForm.slug) || '{tenant-slug}'}@serviceops.ecosysdigital.co.ke</p>
            <p className="mt-1 text-xs text-emerald-800">Automatically provisioned. The server confirms the final address.</p>
          </div>
          <fieldset>
            <legend className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">Plan</legend>
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              {(['operations', 'finance', 'suite'] as const).map((code) => (
                <label key={code} className={`flex cursor-pointer items-center gap-2 rounded-xl border p-3 text-sm ${createForm.planCode === code ? 'border-slate-900 bg-slate-50 font-semibold text-slate-900' : 'border-slate-200 text-slate-600'}`}>
                  <input type="radio" name="plan" value={code} checked={createForm.planCode === code} onChange={() => setCreateForm((current) => ({ ...current, planCode: code }))} />
                  <span>
                    ServiceOps {code === 'suite' ? 'Suite' : code === 'finance' ? 'Finance' : 'Operations'}
                    <span className="block text-xs font-normal text-slate-500">
                      {code === 'suite' ? 'Operations + Finance + Sales CRM' : code === 'finance' ? 'Quotes, invoices, payments' : 'Jobs, maintenance, inventory'}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          </fieldset>
          <Field label="Licensed Users">
            <input required data-testid="create-tenant-licensed-users" aria-describedby="create-tenant-licensed-users-help" type="number" min={1} step={1} inputMode="numeric" value={createForm.maxUsers} onChange={(event) => setCreateForm((current) => ({ ...current, maxUsers: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900 md:w-48" />
          </Field>
          <p id="create-tenant-licensed-users-help" className="-mt-3 text-xs text-slate-500">Maximum number of licensed user accounts allowed for this tenant. Active workspace users (including pending invitations) count against it; client-portal contacts do not. It can be changed later from the tenant's Users tab.</p>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <Field label="Primary Admin Name">
              <input required autoComplete="name" value={createForm.adminFullName} onChange={(event) => setCreateForm((current) => ({ ...current, adminFullName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
            </Field>
            <Field label="Primary Admin Email">
              <input required type="email" autoComplete="email" value={createForm.adminEmail} onChange={(event) => setCreateForm((current) => ({ ...current, adminEmail: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
            </Field>
            <Field label="Country">
              <input required list="tenant-countries" value={createForm.country} onChange={(event) => setCreateForm((current) => ({ ...current, country: event.target.value, timeZoneId: countryTimeZones[event.target.value.trim().toLowerCase()] ?? current.timeZoneId }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              <datalist id="tenant-countries">{['Kenya', 'Uganda', 'Rwanda', 'Tanzania', 'Nigeria', 'South Africa', 'United Kingdom'].map((country) => <option key={country} value={country} />)}</datalist>
            </Field>
            <Field label="Timezone">
              <input required list="tenant-timezones" value={createForm.timeZoneId} onChange={(event) => setCreateForm((current) => ({ ...current, timeZoneId: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              <datalist id="tenant-timezones">{Object.values(countryTimeZones).map((zone) => <option key={zone} value={zone} />)}</datalist>
            </Field>
          </div>
          <p className="text-xs text-slate-500">Timezone is editable before saving. Currency is set from the country where a reliable default exists.</p>
        </div>
      </FormDrawer>

    </div>
  )
}
