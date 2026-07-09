import {
  Activity,
  AlertTriangle,
  Building2,
  ChevronRight,
  FileText,
  Info,
  Plus,
  RefreshCw,
  Search,
  Settings2,
  ShieldAlert,
  ShieldCheck,
  Users,
} from 'lucide-react'
import { type ReactNode, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { InfoAlert } from '../../components/ui/InfoAlert'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { RowActionSelector } from '../../components/ui/RowActionSelector'
import { useToast } from '../../components/ui/ToastProvider'
import { ConfirmationModal, PageScaffold, SectionCard, SearchToolbar } from '../../components/ui/Workspace'
import { useAsyncData } from '../../hooks/useAsyncData'
import { platformLeadService, type PlatformLeadSummary } from '../../services/platformLeadService'
import { platformService, toServiceError } from '../../services/platformService'
import { platformTenantService } from '../../services/platformTenantService'
import type { AuditLog, LicenseStatus, Tenant, TenantStatus } from '../../types/platform'
import type { PlatformSummary } from '../../types/api'
import { formatDateOnly, formatDateTime } from '../../utils/date'
import { getSlugValidationMessage, slugifyName } from '../../utils/slug'
import { Field, licenseStatusBadge, tenantStatusBadge } from './PlatformCommon'
import { TenantCommunicationSettingsPanel } from './TenantCommunicationSettingsPanel'

// ── Types ─────────────────────────────────────────────────────────────────────

type TenantRiskFlag = {
  tenantId: string
  tenantName: string
  flags: string[]
  severity: 'high' | 'medium' | 'low'
}

// ── Constants ─────────────────────────────────────────────────────────────────

const TENANT_STATUSES: TenantStatus[] = ['Active', 'Trial', 'Suspended', 'Inactive']
const LICENSE_STATUSES: LicenseStatus[] = ['Active', 'Trial', 'Expired', 'Suspended']
const TRIAL_FILTERS = ['All', 'TrialActive', 'TrialExpiringSoon', 'TrialExpired', 'TrialExtended', 'PaidActive'] as const

const DETAIL_SECTIONS = [
  { id: 'overview', label: 'Overview' },
  { id: 'subscription', label: 'Subscription' },
  { id: 'email-notifications', label: 'Email & Notifications' },
] as const
type DetailSectionId = (typeof DETAIL_SECTIONS)[number]['id']

const DEFAULT_TENANT_FORM: Tenant = {
  tenantId: '',
  name: '',
  slug: '',
  companyEmail: '',
  companyPhone: '',
  country: 'Kenya',
  industry: '',
  contactPerson: '',
  contactEmail: '',
  contactPhone: '',
  plan: '',
  licenseStatus: 'Trial',
  users: 0,
  branches: 0,
  status: 'Trial',
  createdAt: '',
  maxUsers: 10,
  maxBranches: 2,
  trialEndDate: null,
  createDefaultAdmin: true,
  usePrimaryContactAsWorkspaceAdmin: true,
  adminFullName: '',
  adminEmail: '',
  adminPhone: '',
}

const PIPELINE_STAGES: ReadonlyArray<{ label: string; statuses: readonly string[] }> = [
  { label: 'New Request', statuses: ['New'] },
  { label: 'Under Review', statuses: ['Contacted', 'Qualified'] },
  { label: 'Demo / Awaiting Info', statuses: ['Demo Scheduled'] },
  { label: 'Provisioned', statuses: ['Converted to Workspace'] },
  { label: 'Rejected', statuses: ['Not a Fit'] },
]

const ADMIN_CONTROLS = [
  { label: 'Platform Settings', description: 'Branding, numbering, email, security', to: '/platform/settings', icon: Settings2 },
  { label: 'Platform Users', description: 'Manage staff and support admin access', to: '/platform/users', icon: Users },
  { label: 'Audit Logs', description: 'Platform-wide administrative activity', to: '/platform/audit-logs', icon: ShieldCheck },
  { label: 'Reports', description: 'KPIs, tenants, operations, revenue', to: '/platform/reports', icon: Activity },
  { label: 'Licenses & Subscriptions', description: 'Plan allocation and renewal dates', to: '/platform/licenses', icon: FileText },
  { label: 'Leads & Enquiries', description: 'Review and convert workspace requests', to: '/platform/leads', icon: Building2 },
] as const

// ── Helpers ───────────────────────────────────────────────────────────────────

function formatTrialShort(tenant: Tenant): string {
  switch (tenant.trialStatus) {
    case 'TrialExpiringSoon': return tenant.trialDaysRemaining != null ? `Expiring soon · ${tenant.trialDaysRemaining}d` : 'Expiring soon'
    case 'TrialExpired': return 'Trial expired'
    case 'TrialExtended': return tenant.trialDaysRemaining != null ? `Extended · ${tenant.trialDaysRemaining}d left` : 'Extended'
    case 'PaidActive': return 'Paid active'
    case 'Suspended': return 'Suspended'
    case 'Inactive': return 'Inactive'
    default: return tenant.trialDaysRemaining != null ? `Trial · ${tenant.trialDaysRemaining}d left` : 'Trial active'
  }
}

function formatTrialFull(tenant: Tenant): string {
  switch (tenant.trialStatus) {
    case 'TrialExpiringSoon': return tenant.trialDaysRemaining != null ? `Trial expiring soon · ${tenant.trialDaysRemaining} days remaining` : 'Trial expiring soon'
    case 'TrialExpired': return 'Trial expired'
    case 'TrialExtended': return tenant.trialDaysRemaining != null ? `Trial extended once · ${tenant.trialDaysRemaining} days remaining` : 'Trial extended once'
    case 'PaidActive': return 'Paid active'
    case 'Suspended': return 'Suspended'
    case 'Inactive': return 'Inactive'
    default: return tenant.trialDaysRemaining != null ? `Trial active · ${tenant.trialDaysRemaining} days remaining` : 'Trial active'
  }
}

function formatExtension(tenant: Tenant): string {
  return tenant.trialExtensionUsed ? 'Trial extended once' : 'Extension not used'
}

function deriveRiskFlags(tenants: Tenant[]): TenantRiskFlag[] {
  const result: TenantRiskFlag[] = []
  for (const t of tenants) {
    const flags: string[] = []
    if (t.status === 'Suspended') flags.push('Account suspended')
    if (t.licenseStatus === 'Expired') flags.push('License expired')
    if (t.trialStatus === 'TrialExpired') flags.push('Trial expired — follow up needed')
    if (t.trialStatus === 'TrialExpiringSoon') flags.push(`Trial expiring soon (${t.trialDaysRemaining ?? '?'}d remaining)`)
    if ((t.status === 'Active' || t.status === 'Trial') && t.users === 0) flags.push('No users created')
    if ((t.status === 'Active' || t.status === 'Trial') && t.branches === 0) flags.push('No sites configured')
    if (flags.length === 0) continue
    const severity: TenantRiskFlag['severity'] =
      flags.some((x) => x.includes('suspended') || x.includes('expired')) ? 'high'
      : flags.some((x) => x.includes('expiring')) ? 'medium'
      : 'low'
    result.push({ tenantId: t.tenantId, tenantName: t.name, flags, severity })
  }
  const order = { high: 0, medium: 1, low: 2 }
  return result.sort((a, b) => order[a.severity] - order[b.severity])
}

// ── Sub-components ────────────────────────────────────────────────────────────

function KpiCard({ label, value, sub, accent = 'default' }: {
  label: string
  value: string | number
  sub?: string
  accent?: 'default' | 'emerald' | 'amber' | 'rose' | 'teal'
}) {
  const valueClass =
    accent === 'emerald' ? 'text-emerald-500'
    : accent === 'amber' ? 'text-amber-500'
    : accent === 'rose' ? 'text-rose-500'
    : accent === 'teal' ? 'text-accent-strong'
    : 'text-app'

  return (
    <div className="surface-card flex flex-col gap-1 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className={`font-heading text-2xl font-semibold tracking-tight ${valueClass}`}>{value}</p>
      {sub ? <p className="text-xs text-muted">{sub}</p> : null}
    </div>
  )
}

function RiskDot({ severity }: { severity: TenantRiskFlag['severity'] }) {
  const cls = severity === 'high' ? 'bg-rose-500' : severity === 'medium' ? 'bg-amber-500' : 'bg-muted'
  return <span className={`mt-1.5 inline-block h-2 w-2 shrink-0 rounded-full ${cls}`} />
}

function LogIcon({ action }: { action: string }) {
  const a = action.toLowerCase()
  if (a.includes('suspend') || a.includes('deactivat')) return <ShieldAlert className="h-3.5 w-3.5 shrink-0 text-rose-400" />
  if (a.includes('activat')) return <ShieldCheck className="h-3.5 w-3.5 shrink-0 text-emerald-400" />
  if (a.includes('creat') || a.includes('add') || a.includes('onboard')) return <Plus className="h-3.5 w-3.5 shrink-0 text-accent-strong" />
  if (a.includes('user') || a.includes('login') || a.includes('auth')) return <Users className="h-3.5 w-3.5 shrink-0 text-blue-400" />
  if (a.includes('setting') || a.includes('module') || a.includes('config')) return <Settings2 className="h-3.5 w-3.5 shrink-0 text-muted" />
  return <Activity className="h-3.5 w-3.5 shrink-0 text-muted" />
}

// ── Main Component ────────────────────────────────────────────────────────────

export function PlatformTenantsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const { pushToast } = useToast()

  // ── Data: Tenants ────────────────────────────────────────────────────────────
  const { data, loading, error, reload } = useAsyncData(
    async () => platformService.tenantsApi.list(),
    { data: [] as Tenant[], backendAvailable: true, message: '' },
    [],
  )

  // ── Data: Platform summary (KPI) ─────────────────────────────────────────────
  const { data: summary } = useAsyncData<PlatformSummary | null>(
    async (signal) => {
      try { return await platformTenantService.getPlatformSummary(signal) }
      catch { return null }
    },
    null,
    [],
  )

  // ── Data: Audit logs + Leads (right panels, non-critical) ────────────────────
  const { data: sideData } = useAsyncData<{ logs: AuditLog[]; leads: PlatformLeadSummary[] }>(
    async () => {
      const [logsResult, leadsResult] = await Promise.allSettled([
        platformService.auditLogsApi.list(),
        platformLeadService.listLeads(),
      ])
      return {
        logs: logsResult.status === 'fulfilled'
          ? [...logsResult.value.data]
              .sort((a, b) => new Date(b.dateTime).getTime() - new Date(a.dateTime).getTime())
              .slice(0, 12)
          : [],
        leads: leadsResult.status === 'fulfilled'
          ? [...leadsResult.value]
              .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
              .slice(0, 500)
          : [],
      }
    },
    { logs: [], leads: [] },
    [],
  )

  // ── Filters ───────────────────────────────────────────────────────────────────
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<'All' | TenantStatus>('All')
  const [planFilter, setPlanFilter] = useState('All')
  const [licenseFilter, setLicenseFilter] = useState<'All' | LicenseStatus>('All')
  const [trialFilter, setTrialFilter] = useState<(typeof TRIAL_FILTERS)[number]>('All')

  // ── Create / Edit ─────────────────────────────────────────────────────────────
  const [modalOpen, setModalOpen] = useState(false)
  const [mode, setMode] = useState<'create' | 'edit'>('create')
  const [form, setForm] = useState<Tenant>(DEFAULT_TENANT_FORM)
  const [slugTouched, setSlugTouched] = useState(false)
  const [saving, setSaving] = useState(false)
  const [prefillLeadId, setPrefillLeadId] = useState<string | null>(null)

  // ── Status / Extension ────────────────────────────────────────────────────────
  const [statusReason, setStatusReason] = useState('')
  const [pendingStatus, setPendingStatus] = useState<{ tenant: Tenant; nextStatus: TenantStatus } | null>(null)
  const [pendingTrialExtension, setPendingTrialExtension] = useState<Tenant | null>(null)

  // ── Detail drawer ─────────────────────────────────────────────────────────────
  const [selectedTenant, setSelectedTenant] = useState<Tenant | null>(null)
  const [activeDetailSection, setActiveDetailSection] = useState<DetailSectionId>('overview')

  // ── Derived ───────────────────────────────────────────────────────────────────
  const plans = useMemo(
    () => ['All', ...Array.from(new Set(data.data.map((t) => t.plan).filter(Boolean)))],
    [data.data],
  )

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase()
    return data.data.filter((t) => {
      const matchQuery = !q || t.name.toLowerCase().includes(q) || t.slug.toLowerCase().includes(q) || t.contactEmail.toLowerCase().includes(q)
      return matchQuery
        && (statusFilter === 'All' || t.status === statusFilter)
        && (planFilter === 'All' || t.plan === planFilter)
        && (licenseFilter === 'All' || t.licenseStatus === licenseFilter)
        && (trialFilter === 'All' || t.trialStatus === trialFilter)
    })
  }, [data.data, search, statusFilter, planFilter, licenseFilter, trialFilter])

  const riskFlags = useMemo(() => deriveRiskFlags(data.data), [data.data])

  const pipelineCounts = useMemo(() => {
    const counts: Record<string, number> = {}
    for (const lead of sideData.leads) counts[lead.status] = (counts[lead.status] ?? 0) + 1
    return counts
  }, [sideData.leads])

  // KPI values
  const kpiTotal = summary?.totalTenants ?? data.data.length
  const kpiActive = summary?.activeTenants ?? data.data.filter((t) => t.status === 'Active').length
  const kpiTrial = data.data.filter((t) => t.status === 'Trial').length
  const kpiSuspended = data.data.filter((t) => t.status === 'Suspended').length
  const kpiExpiring = data.data.filter((t) => t.trialStatus === 'TrialExpiringSoon').length
  // Prefer backend-supplied totalUsers (from PlatformSummary if available); fall back to derived sum
  const kpiUsersComputed = data.data.reduce((sum, t) => sum + (t.users ?? 0), 0)
  const kpiUsers = summary?.totalUsers ?? kpiUsersComputed
  // Open issues: prefer backend openIssues field; derive from risk flags as fallback
  const kpiOpenIssues = summary?.openIssues ?? riskFlags.length

  // ── CRUD handlers ─────────────────────────────────────────────────────────────
  function openCreate() {
    setMode('create')
    setForm(DEFAULT_TENANT_FORM)
    setPrefillLeadId(null)
    setSlugTouched(false)
    setModalOpen(true)
  }

  function openEdit(tenant: Tenant) {
    setMode('edit')
    setForm(tenant)
    setSlugTouched(true)
    setModalOpen(true)
  }

  async function saveTenant() {
    if (!form.name.trim()) {
      pushToast({ title: 'Missing fields', description: 'Tenant name is required.', tone: 'warning' })
      return
    }

    if (mode === 'create') {
      if (!form.companyEmail?.trim() || !form.companyPhone?.trim() || !form.country?.trim() || !form.contactPerson.trim() || !form.contactEmail.trim()) {
        pushToast({ title: 'Missing fields', description: 'Company email, company phone, country, primary contact name, and primary contact email are required.', tone: 'warning' })
        return
      }
      const adminName = (form.usePrimaryContactAsWorkspaceAdmin ? form.contactPerson : form.adminFullName || '').trim()
      const adminEmail = (form.usePrimaryContactAsWorkspaceAdmin ? form.contactEmail : form.adminEmail || '').trim()
      if (!adminName || !adminEmail) {
        pushToast({ title: 'Admin details required', description: 'Enter the initial workspace admin details or use the primary contact as workspace admin.', tone: 'warning' })
        return
      }
    }

    const normalizedSlug = slugifyName(form.slug)
    const slugError = getSlugValidationMessage(normalizedSlug)
    if (slugError) {
      pushToast({ title: 'Workspace URL name required', description: slugError, tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      const payload = { ...form, slug: normalizedSlug }
      if (mode === 'create') {
        const created = await platformService.tenantsApi.create(payload)
        pushToast({
          title: 'Tenant created',
          description: created.data.initialAdminInvitationSent ? 'Tenant created and workspace admin invited.' : 'Tenant created, but admin invitation could not be sent.',
          tone: created.data.initialAdminInvitationSent ? 'success' : 'warning',
        })
      } else {
        await platformService.tenantsApi.update(form.tenantId, payload)
        pushToast({ title: 'Tenant updated', description: `${form.name} saved successfully.`, tone: 'success' })
      }
      setModalOpen(false)
      setPrefillLeadId(null)
      setSlugTouched(false)
      if (selectedTenant?.tenantId === form.tenantId) setSelectedTenant((cur) => (cur ? { ...cur, ...payload } : cur))
      await reload()
    } catch (err) {
      pushToast({ title: 'Save failed', description: toServiceError(err, 'Unable to save tenant.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function changeStatus() {
    if (!pendingStatus) return
    if (pendingStatus.nextStatus !== 'Active' && !statusReason.trim()) {
      pushToast({ title: 'Reason required', description: 'Add a reason for suspension or deactivation.', tone: 'warning' })
      return
    }
    try {
      if (pendingStatus.nextStatus === 'Inactive') {
        await platformService.tenantsApi.deactivate(pendingStatus.tenant.tenantId, statusReason)
      } else if (pendingStatus.nextStatus === 'Active') {
        await platformService.tenantsApi.activate(pendingStatus.tenant.tenantId)
      } else {
        await platformService.tenantsApi.updateStatus(pendingStatus.tenant.tenantId, pendingStatus.nextStatus, statusReason)
      }
      pushToast({ title: 'Status updated', description: `${pendingStatus.tenant.name} is now ${pendingStatus.nextStatus}.`, tone: 'success' })
      setPendingStatus(null)
      setStatusReason('')
      await reload()
    } catch (err) {
      pushToast({ title: 'Update failed', description: toServiceError(err, 'Unable to update status.'), tone: 'danger' })
    }
  }

  async function extendTrial() {
    if (!pendingTrialExtension) return
    try {
      const updated = await platformService.tenantsApi.extendTrial(pendingTrialExtension.tenantId)
      pushToast({ title: 'Trial extended', description: 'Trial extended by 14 days.', tone: 'success' })
      setPendingTrialExtension(null)
      if (selectedTenant?.tenantId === updated.data.tenantId) setSelectedTenant(updated.data)
      await reload()
    } catch (err) {
      pushToast({ title: 'Extension failed', description: toServiceError(err, 'Unable to extend trial.'), tone: 'danger' })
    }
  }

  // ── Lead prefill effects ──────────────────────────────────────────────────────
  useEffect(() => {
    if (mode !== 'create' || !(form.usePrimaryContactAsWorkspaceAdmin ?? true)) return
    setForm((cur) => ({ ...cur, adminFullName: cur.contactPerson, adminEmail: cur.contactEmail, adminPhone: cur.contactPhone || '' }))
  }, [form.contactEmail, form.contactPerson, form.contactPhone, form.usePrimaryContactAsWorkspaceAdmin, mode])

  useEffect(() => {
    const leadId = searchParams.get('leadId')
    if (!leadId || modalOpen) return
    let cancelled = false
    void (async () => {
      try {
        const lead = await platformLeadService.getLead(leadId)
        if (cancelled) return
        setMode('create')
        setForm({ ...DEFAULT_TENANT_FORM, name: lead.companyName, slug: slugifyName(lead.companyName), companyEmail: lead.email, contactPerson: lead.contactPersonName, contactEmail: lead.email, adminFullName: lead.contactPersonName, adminEmail: lead.email })
        setSlugTouched(false)
        setPrefillLeadId(lead.id)
        setModalOpen(true)
        pushToast({ title: 'Lead details loaded', description: `Review ${lead.companyName} before creating the workspace.`, tone: 'success' })
      } catch (err) {
        if (cancelled) return
        pushToast({ title: 'Unable to load lead', description: err instanceof Error ? err.message : 'Could not prefill from this lead.', tone: 'danger' })
      } finally {
        if (!cancelled) { const next = new URLSearchParams(searchParams); next.delete('leadId'); setSearchParams(next, { replace: true }) }
      }
    })()
    return () => { cancelled = true }
  }, [modalOpen, pushToast, searchParams, setSearchParams])

  // ─────────────────────────────────────────────────────────────────────────────
  return (
    <div data-testid="tenants-page">
      {/* Hidden sentinel — keeps existing E2E selectors intact */}
      <div data-testid="command-centre-dashboard" className="text-[1px] leading-none text-transparent" aria-hidden="true">ready</div>

      <PageScaffold
        eyebrow="Platform Command Centre"
        title="Tenant Operations"
        description="Operational HQ — tenant lifecycle, platform health, activity, and onboarding pipeline."
      >
        {/* ── 1. KPI Strip ────────────────────────────────────────────────────── */}
        <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-7" aria-label="Platform KPIs">
          <KpiCard label="Total Tenants" value={kpiTotal} />
          <KpiCard label="Active" value={kpiActive} accent="emerald" />
          <KpiCard label="On Trial" value={kpiTrial} accent="teal" />
          <KpiCard label="Suspended" value={kpiSuspended} accent="rose" />
          <KpiCard label="Expiring Soon" value={kpiExpiring} accent="amber" sub="Trial tenants ≤7 days" />
          <KpiCard label="Total Users" value={kpiUsers} sub={summary?.totalUsers != null ? 'Platform-wide total' : 'Derived from tenant list'} />
          <KpiCard label="Attention Needed" value={kpiOpenIssues} accent={kpiOpenIssues > 0 ? 'rose' : 'default'} sub={summary?.openIssues != null ? 'Open platform issues' : 'Tenants with risk flags'} />
        </section>

        {/* ── 2. Main 2-col: Tenant Table | Health + Activity ─────────────────── */}
        <div className="space-y-4">

          {/* Left: Tenant Operations */}
          <div className="space-y-4">
            {loading ? <LoadingState label="Loading tenants" /> : null}

            {!loading && error ? (
              <SectionCard title="Unable to load tenants" description={error}>
                <InfoAlert title="Unable to load tenants" description={error} tone="danger" />
                <button type="button" className="button-secondary" onClick={() => void reload()}>
                  <RefreshCw className="h-4 w-4" /> Retry
                </button>
              </SectionCard>
            ) : null}

            {!loading && !error ? (
              <SectionCard
                title="Tenant Directory"
                description="Filter and manage tenant lifecycle, subscription, and activation state."
                action={
                  <button type="button" className="button-primary" onClick={openCreate}>
                    <Plus className="h-4 w-4" /> Add Tenant
                  </button>
                }
              >
                <SearchToolbar
                  searchSlot={
                    <label className="panel-subtle flex items-center gap-3 rounded-[14px] px-4 py-3">
                      <Search className="h-4 w-4 text-muted" />
                      <input
                        data-testid="tenants-search-input"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        className="w-full bg-transparent text-sm text-app outline-none placeholder:text-muted"
                        placeholder="Search by name, slug, or contact email"
                      />
                    </label>
                  }
                  filters={
                    <>
                      <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)} className="field-input">
                        <option value="All">All statuses</option>
                        {TENANT_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
                      </select>
                      <select value={planFilter} onChange={(e) => setPlanFilter(e.target.value)} className="field-input">
                        {plans.map((p) => <option key={p} value={p}>{p === 'All' ? 'All plans' : p}</option>)}
                      </select>
                      <select value={licenseFilter} onChange={(e) => setLicenseFilter(e.target.value as typeof licenseFilter)} className="field-input">
                        <option value="All">All license states</option>
                        {LICENSE_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
                      </select>
                      <select value={trialFilter} onChange={(e) => setTrialFilter(e.target.value as typeof trialFilter)} className="field-input">
                        {TRIAL_FILTERS.map((f) => <option key={f} value={f}>{f === 'All' ? 'All trial states' : f}</option>)}
                      </select>
                    </>
                  }
                />

                {filtered.length === 0
                  ? <EmptyState title="No tenants found" description="Adjust filters or add a new tenant." />
                  : (
                    <DataTable
                      rows={filtered}
                      rowKey={(row) => row.tenantId}
                      minTableWidth="min-w-[1280px] w-full"
                      onRowClick={(row) => { setSelectedTenant(row); setActiveDetailSection('overview') }}
                      columns={[
                        {
                          key: 'name',
                          header: 'Tenant',
                          cell: (row) => (
                            <button
                              type="button"
                              className="text-left"
                              onClick={() => { setSelectedTenant(row); setActiveDetailSection('overview') }}
                            >
                              <p className="font-semibold text-accent-strong hover:underline">{row.name}</p>
                              <p className="mt-0.5 font-mono text-[11px] text-muted">{row.slug}</p>
                            </button>
                          ),
                        },
                        {
                          key: 'contact',
                          header: 'Contact',
                          cell: (row) => (
                            <div>
                              <p className="text-sm text-app">{row.contactPerson || '—'}</p>
                              <p className="mt-0.5 text-xs text-muted">{row.contactEmail || '—'}</p>
                            </div>
                          ),
                        },
                        { key: 'plan', header: 'Plan', cell: (row) => <span>{row.plan || '—'}</span> },
                        { key: 'license', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
                        { key: 'trial', header: 'Trial', cell: (row) => <span className="text-sm text-muted">{formatTrialShort(row)}</span> },
                        { key: 'users', header: 'Users', cell: (row) => <span>{row.users}</span> },
                        { key: 'branches', header: 'Branches', cell: (row) => <span>{row.branches}</span> },
                        { key: 'workOrders', header: 'Work Orders', cell: (row) => <span>{row.workOrderCount != null ? row.workOrderCount : '—'}</span> },
                        { key: 'assets', header: 'Assets', cell: (row) => <span>{row.assetCount != null ? row.assetCount : '—'}</span> },
                        { key: 'lastActivity', header: 'Last Activity', cell: (row) => <span>{row.lastActivityAt ? formatDateOnly(row.lastActivityAt) : '—'}</span> },
                        { key: 'subscriptionEnds', header: 'Sub. Ends', cell: (row) => <span>{row.subscriptionEndsAt ? formatDateOnly(row.subscriptionEndsAt) : '—'}</span> },
                        { key: 'status', header: 'Status', cell: (row) => tenantStatusBadge(row.status) },
                        { key: 'created', header: 'Created', cell: (row) => <span>{formatDateOnly(row.createdAt)}</span> },
                        {
                          key: 'actions',
                          header: 'Actions',
                          className: 'min-w-[180px]',
                          cell: (row) => (
                            <RowActionSelector
                              options={[
                                { value: 'view', label: 'View details' },
                                { value: 'edit', label: 'Edit tenant' },
                                { value: 'extend', label: 'Extend trial', disabled: row.trialExtensionUsed || row.licenseStatus === 'Active' },
                                { value: 'activate', label: 'Activate', disabled: row.status === 'Active' },
                                { value: 'suspend', label: 'Suspend', disabled: row.status === 'Suspended' },
                                { value: 'deactivate', label: 'Deactivate', disabled: row.status === 'Inactive' },
                              ]}
                              onApply={(action) => {
                                if (action === 'view') { setSelectedTenant(row); setActiveDetailSection('overview') }
                                if (action === 'edit') openEdit(row)
                                if (action === 'extend') setPendingTrialExtension(row)
                                if (action === 'activate') setPendingStatus({ tenant: row, nextStatus: 'Active' })
                                if (action === 'suspend') setPendingStatus({ tenant: row, nextStatus: 'Suspended' })
                                if (action === 'deactivate') setPendingStatus({ tenant: row, nextStatus: 'Inactive' })
                              }}
                              buttonLabel="Actions"
                            />
                          ),
                        },
                      ]}
                    />
                  )}
              </SectionCard>
            ) : null}
          </div>

          {/* Right: Health + Activity */}
          <div className="space-y-4">

            {/* 3. Tenant Health & Risk */}
            <SectionCard title="Tenant Health" description="Accounts that may need attention.">
              {riskFlags.length === 0 ? (
                <div className="flex items-center gap-3 rounded-[14px] border border-app bg-app/20 px-4 py-3">
                  <ShieldCheck className="h-4 w-4 shrink-0 text-emerald-400" />
                  <p className="text-sm text-muted">All tenants are in good standing.</p>
                </div>
              ) : (
                <div className="max-h-[360px] space-y-2 overflow-y-auto pr-1">
                  {riskFlags.slice(0, 10).map((item) => (
                    <article key={item.tenantId} className="rounded-[14px] border border-app bg-app/20 px-4 py-3">
                      <button
                        type="button"
                        className="mb-2 flex w-full items-start justify-between gap-2 text-left"
                        onClick={() => {
                          const t = data.data.find((x) => x.tenantId === item.tenantId)
                          if (t) { setSelectedTenant(t); setActiveDetailSection('overview') }
                        }}
                      >
                        <p className="text-sm font-semibold text-app hover:text-accent-strong">{item.tenantName}</p>
                        <ChevronRight className="mt-0.5 h-3.5 w-3.5 shrink-0 text-muted" />
                      </button>
                      <ul className="space-y-1">
                        {item.flags.map((f) => (
                          <li key={f} className="flex items-start gap-2">
                            <RiskDot severity={item.severity} />
                            <span className="text-xs text-muted">{f}</span>
                          </li>
                        ))}
                      </ul>
                    </article>
                  ))}
                  {riskFlags.length > 10 && (
                    <p className="py-1 text-center text-xs text-muted">+{riskFlags.length - 10} more — use table filters</p>
                  )}
                </div>
              )}
            </SectionCard>

            {/* 4. Platform Activity Feed */}
            <SectionCard title="Platform Activity" description="Recent administrative events.">
              {sideData.logs.length === 0 ? (
                <div className="flex items-center gap-3 rounded-[14px] border border-app bg-app/20 px-4 py-3">
                  <Info className="h-4 w-4 shrink-0 text-muted" />
                  <p className="text-sm text-muted">No platform activity recorded yet.</p>
                </div>
              ) : (
                <div className="max-h-[340px] space-y-2 overflow-y-auto pr-1">
                  {sideData.logs.map((log) => (
                    <article key={log.id} className="rounded-[14px] border border-app bg-app/20 px-4 py-3">
                      <div className="flex items-start gap-2">
                        <LogIcon action={log.action} />
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-sm font-medium text-app">{log.action}</p>
                          {log.entityName ? <p className="truncate text-xs text-muted">{log.entityName}</p> : null}
                          <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-0.5">
                            {log.tenant ? <span className="text-[11px] text-muted">{log.tenant}</span> : null}
                            <span className="text-[11px] text-muted opacity-50">·</span>
                            <span className="text-[11px] text-muted">{log.actor}</span>
                            <span className="text-[11px] text-muted opacity-50">·</span>
                            <span className="text-[11px] text-muted">{formatDateTime(log.dateTime)}</span>
                          </div>
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              )}
              <div className="mt-3 border-t border-app pt-3">
                <Link to="/platform/audit-logs" className="flex items-center gap-1 text-xs text-accent-strong hover:underline">
                  View full audit log <ChevronRight className="h-3 w-3" />
                </Link>
              </div>
            </SectionCard>

          </div>
        </div>

        {/* ── 5+6. Bottom row: Onboarding Pipeline + Admin Controls ────────────── */}
        <div className="grid gap-4 xl:grid-cols-2">

          {/* Onboarding Pipeline */}
          <SectionCard
            title="Onboarding Pipeline"
            description="Workspace enquiries by review stage."
            action={
              <Link to="/platform/leads" className="button-secondary px-3 py-2 text-sm">
                Manage leads
              </Link>
            }
          >
            {sideData.leads.length === 0 ? (
              <EmptyState
                title="No enquiries yet"
                description="Workspace requests from prospects will appear here once leads are submitted."
              />
            ) : (
              <div className="grid gap-2 sm:grid-cols-2">
                {PIPELINE_STAGES.map((stage) => {
                  const count = stage.statuses.reduce((sum, s) => sum + (pipelineCounts[s] ?? 0), 0)
                  const isTerminal = stage.label === 'Rejected' || stage.label === 'Provisioned'
                  return (
                    <div
                      key={stage.label}
                      className={`flex items-center justify-between rounded-[14px] border px-4 py-3 ${isTerminal ? 'border-app bg-app/10' : 'border-app bg-app/20'}`}
                    >
                      <div>
                        <p className="text-sm font-medium text-app">{stage.label}</p>
                        <p className="text-xs text-muted">{stage.statuses.join(', ')}</p>
                      </div>
                      <span className={`font-heading text-xl font-semibold ${count > 0 && !isTerminal ? 'text-accent-strong' : 'text-muted'}`}>
                        {count}
                      </span>
                    </div>
                  )
                })}
              </div>
            )}
          </SectionCard>

          {/* Admin Control Panel */}
          <SectionCard title="Admin Controls" description="Quick access to platform configuration and management areas.">
            <div className="grid gap-2 sm:grid-cols-2">
              {ADMIN_CONTROLS.map((ctrl) => (
                <Link
                  key={ctrl.to}
                  to={ctrl.to}
                  className="flex items-start gap-3 rounded-[14px] border border-app bg-app/20 px-4 py-3 transition hover:border-accent/40 hover:bg-app/40"
                >
                  <ctrl.icon className="mt-0.5 h-4 w-4 shrink-0 text-accent-strong" />
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-app">{ctrl.label}</p>
                    <p className="mt-0.5 truncate text-xs text-muted">{ctrl.description}</p>
                  </div>
                </Link>
              ))}
            </div>
          </SectionCard>

        </div>

        {/* ── Create / Edit Modal ──────────────────────────────────────────────── */}
        <Modal
          open={modalOpen}
          title={mode === 'create' ? 'Add Tenant' : 'Edit Tenant'}
          description="Capture company profile, subscription details, and access limits."
          onClose={() => !saving && setModalOpen(false)}
          maxWidth="max-w-5xl"
        >
          {mode === 'create' && prefillLeadId ? (
            <div className="mb-4">
              <InfoAlert title="Prefilled from lead enquiry" description="Review these details carefully. Workspace creation is a manual platform-owner action and will only happen when you submit this form." tone="info" />
            </div>
          ) : null}
          <div className="grid gap-4 md:grid-cols-2">
            <Field label="Company Name">
              <input data-testid="tenant-form-company-name" value={form.name} onChange={(e) => setForm((c) => ({ ...c, name: e.target.value, slug: slugTouched ? c.slug : slugifyName(e.target.value) }))} className="field-input" />
            </Field>
            <Field label="Workspace URL slug">
              <div className="space-y-2">
                <div className="flex gap-2">
                  <input data-testid="tenant-form-slug" value={form.slug} onChange={(e) => { setSlugTouched(true); setForm((c) => ({ ...c, slug: slugifyName(e.target.value) })) }} className="field-input font-mono" />
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => { setSlugTouched(false); setForm((c) => ({ ...c, slug: slugifyName(c.name) })) }}>
                    <RefreshCw className="h-4 w-4" />
                  </button>
                </div>
                <p className="text-xs text-muted">Auto-generated from the company name. Edit if needed.</p>
              </div>
            </Field>
            <Field label="Company Email"><input data-testid="tenant-form-company-email" type="email" value={form.companyEmail || ''} onChange={(e) => setForm((c) => ({ ...c, companyEmail: e.target.value }))} className="field-input" /></Field>
            <Field label="Company Phone"><input data-testid="tenant-form-company-phone" value={form.companyPhone || ''} onChange={(e) => setForm((c) => ({ ...c, companyPhone: e.target.value }))} className="field-input" /></Field>
            <Field label="Country"><input data-testid="tenant-form-country" value={form.country || ''} onChange={(e) => setForm((c) => ({ ...c, country: e.target.value }))} className="field-input" /></Field>
            <Field label="Industry"><input value={form.industry || ''} onChange={(e) => setForm((c) => ({ ...c, industry: e.target.value }))} className="field-input" /></Field>
            <Field label="Primary Contact Name"><input data-testid="tenant-form-contact-name" value={form.contactPerson} onChange={(e) => setForm((c) => ({ ...c, contactPerson: e.target.value }))} className="field-input" /></Field>
            <Field label="Primary Contact Email"><input data-testid="tenant-form-contact-email" type="email" value={form.contactEmail} onChange={(e) => setForm((c) => ({ ...c, contactEmail: e.target.value }))} className="field-input" /></Field>
            <Field label="Primary Contact Phone"><input value={form.contactPhone || ''} onChange={(e) => setForm((c) => ({ ...c, contactPhone: e.target.value }))} className="field-input" /></Field>
            <Field label="Plan"><input value={String(form.plan)} onChange={(e) => setForm((c) => ({ ...c, plan: e.target.value }))} className="field-input" /></Field>
            <Field label="License Status">
              <select value={form.licenseStatus} onChange={(e) => setForm((c) => ({ ...c, licenseStatus: e.target.value as LicenseStatus }))} className="field-input">
                {LICENSE_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </Field>
            <Field label="Status">
              <select value={form.status} onChange={(e) => setForm((c) => ({ ...c, status: e.target.value as TenantStatus }))} className="field-input">
                {TENANT_STATUSES.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </Field>
            <Field label="Max Users"><input type="number" min={1} value={form.maxUsers ?? ''} onChange={(e) => setForm((c) => ({ ...c, maxUsers: e.target.value ? Number(e.target.value) : null }))} className="field-input" /></Field>
            <Field label="Max Branches"><input type="number" min={1} value={form.maxBranches ?? ''} onChange={(e) => setForm((c) => ({ ...c, maxBranches: e.target.value ? Number(e.target.value) : null }))} className="field-input" /></Field>
          </div>

          {mode === 'create' ? <InfoAlert title="Automatic 14-day trial" description="New tenants start with a 14-day trial automatically from the creation date." tone="info" /> : null}

          {mode === 'create' ? (
            <div className="mt-4 space-y-4">
              <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                <span className="text-sm text-app">Use primary contact as workspace admin</span>
                <input type="checkbox" checked={form.usePrimaryContactAsWorkspaceAdmin ?? true} onChange={(e) => setForm((c) => ({ ...c, usePrimaryContactAsWorkspaceAdmin: e.target.checked }))} />
              </label>
              {form.usePrimaryContactAsWorkspaceAdmin ? null : (
                <div className="grid gap-4 md:grid-cols-2">
                  <Field label="Initial Admin Full Name"><input value={form.adminFullName ?? ''} onChange={(e) => setForm((c) => ({ ...c, adminFullName: e.target.value }))} className="field-input" /></Field>
                  <Field label="Initial Admin Email"><input type="email" value={form.adminEmail ?? ''} onChange={(e) => setForm((c) => ({ ...c, adminEmail: e.target.value }))} className="field-input" /></Field>
                  <Field label="Initial Admin Phone"><input value={form.adminPhone ?? ''} onChange={(e) => setForm((c) => ({ ...c, adminPhone: e.target.value }))} className="field-input" /></Field>
                </div>
              )}
              <InfoAlert title="Workspace admin scope" description="Creating a tenant automatically creates the first workspace admin. Platform users are managed separately in the Platform Users area." tone="info" />
            </div>
          ) : null}

          <div className="mt-6 flex justify-end gap-3">
            <button type="button" className="button-secondary" onClick={() => setModalOpen(false)} disabled={saving}>Cancel</button>
            <button type="button" className="button-primary" onClick={() => void saveTenant()} disabled={saving}>
              {saving ? 'Saving...' : mode === 'create' ? 'Create Tenant' : 'Save Changes'}
            </button>
          </div>
        </Modal>

        {/* ── Status Modal ─────────────────────────────────────────────────────── */}
        <Modal
          open={Boolean(pendingStatus)}
          title="Update Tenant Status"
          description="Suspended means temporary blocked access. Deactivated means tenant is inactive."
          onClose={() => setPendingStatus(null)}
        >
          <ConfirmationModal title="Update Tenant Status" description="Suspended means temporary blocked access. Deactivated means tenant is inactive.">
            <p className="text-sm text-muted">Tenant: <span className="font-semibold text-app">{pendingStatus?.tenant.name}</span></p>
            <Field label="Reason">
              <textarea value={statusReason} onChange={(e) => setStatusReason(e.target.value)} className="field-input min-h-[100px]" placeholder="Reason for this status change" />
            </Field>
            <div className="flex justify-end gap-3">
              <button type="button" className="button-secondary" onClick={() => setPendingStatus(null)}>Cancel</button>
              <button type="button" className="button-primary" onClick={() => void changeStatus()}>Apply</button>
            </div>
          </ConfirmationModal>
        </Modal>

        {/* ── Trial Extension Modal ────────────────────────────────────────────── */}
        <Modal
          open={Boolean(pendingTrialExtension)}
          title="Extend Trial"
          description="Extend this tenant's trial by another 14 days. This can only be done once."
          onClose={() => setPendingTrialExtension(null)}
        >
          <ConfirmationModal title="Extend Trial" description="Extend this tenant's trial by another 14 days. This can only be done once.">
            <p className="text-sm text-muted">Tenant: <span className="font-semibold text-app">{pendingTrialExtension?.name}</span></p>
            <div className="flex justify-end gap-3">
              <button type="button" className="button-secondary" onClick={() => setPendingTrialExtension(null)}>Cancel</button>
              <button type="button" className="button-primary" onClick={() => void extendTrial()}>Extend by 14 days</button>
            </div>
          </ConfirmationModal>
        </Modal>

        {/* ── Tenant Detail Modal ─────────────────────────────────────────────── */}
        <Modal
          open={Boolean(selectedTenant)}
          title={selectedTenant?.name || 'Tenant'}
          description="Tenant profile, subscription status, and controls."
          onClose={() => { setSelectedTenant(null); setActiveDetailSection('overview') }}
          maxWidth="max-w-6xl"
        >
          {selectedTenant ? (
            <div data-testid="tenant-details-page" className="grid gap-4 xl:grid-cols-[200px_minmax(0,1fr)]">

              <aside data-testid="tenant-details-mini-sidebar" className="surface-card h-max space-y-1 p-3">
                {DETAIL_SECTIONS.map((section) => (
                  <button
                    key={section.id}
                    type="button"
                    data-testid={`tenant-details-section-${section.id}`}
                    className={`w-full rounded-xl px-3 py-2 text-left text-sm transition ${activeDetailSection === section.id ? 'bg-accent/20 font-semibold text-accent-strong' : 'text-muted hover:bg-app/60 hover:text-app'}`}
                    onClick={() => setActiveDetailSection(section.id)}
                  >
                    {section.label}
                  </button>
                ))}

                <div className="mt-3 space-y-1 border-t border-app pt-3">
                  <button type="button" className="w-full rounded-xl px-3 py-2 text-left text-sm text-muted transition hover:bg-app/60 hover:text-app" onClick={() => openEdit(selectedTenant)}>Edit tenant</button>
                  {selectedTenant.status !== 'Active'
                    ? <button type="button" className="w-full rounded-xl px-3 py-2 text-left text-sm text-muted transition hover:bg-app/60 hover:text-app" onClick={() => setPendingStatus({ tenant: selectedTenant, nextStatus: 'Active' })}>Activate</button>
                    : null}
                  {selectedTenant.status !== 'Suspended'
                    ? <button type="button" className="w-full rounded-xl px-3 py-2 text-left text-sm text-muted transition hover:bg-app/60 hover:text-app" onClick={() => setPendingStatus({ tenant: selectedTenant, nextStatus: 'Suspended' })}>Suspend</button>
                    : null}
                  {selectedTenant.status !== 'Inactive'
                    ? <button type="button" className="w-full rounded-xl px-3 py-2 text-left text-sm text-muted transition hover:bg-app/60 hover:text-app" onClick={() => setPendingStatus({ tenant: selectedTenant, nextStatus: 'Inactive' })}>Deactivate</button>
                    : null}
                  {!selectedTenant.trialExtensionUsed && selectedTenant.licenseStatus !== 'Active'
                    ? <button type="button" className="w-full rounded-xl px-3 py-2 text-left text-sm text-muted transition hover:bg-app/60 hover:text-app" onClick={() => setPendingTrialExtension(selectedTenant)}>Extend trial</button>
                    : null}
                </div>
              </aside>

              <div className="space-y-4">
                {/* Risk flags for this tenant */}
                {(() => {
                  const risk = riskFlags.find((r) => r.tenantId === selectedTenant.tenantId)
                  if (!risk) return null
                  return (
                    <div className="rounded-[14px] border border-amber-500/40 bg-amber-500/10 px-4 py-3">
                      <div className="mb-2 flex items-center gap-2">
                        <AlertTriangle className="h-4 w-4 shrink-0 text-amber-500" />
                        <p className="text-sm font-semibold text-app">Attention needed</p>
                      </div>
                      <ul className="space-y-1">
                        {risk.flags.map((f) => <li key={f} className="text-xs text-muted">{f}</li>)}
                      </ul>
                    </div>
                  )
                })()}

                {activeDetailSection === 'overview' ? (
                  <div className="grid gap-3 md:grid-cols-2">
                    <Detail label="Tenant Name" value={selectedTenant.name} />
                    <Detail label="Slug" value={selectedTenant.slug} />
                    <Detail label="Status" value={tenantStatusBadge(selectedTenant.status)} />
                    <Detail label="License" value={licenseStatusBadge(selectedTenant.licenseStatus)} />
                    <Detail label="Contact Person" value={selectedTenant.contactPerson || '—'} />
                    <Detail label="Contact Email" value={selectedTenant.contactEmail || '—'} />
                    <Detail label="Plan" value={String(selectedTenant.plan) || '—'} />
                    <Detail label="Trial Status" value={formatTrialFull(selectedTenant)} />
                    <Detail label="Users" value={String(selectedTenant.users)} />
                    <Detail label="Branches" value={String(selectedTenant.branches)} />
                    <Detail label="Country" value={selectedTenant.country || '—'} />
                    <Detail label="Industry" value={selectedTenant.industry || '—'} />
                    <Detail label="Company Email" value={selectedTenant.companyEmail || '—'} />
                    <Detail label="Created At" value={formatDateTime(selectedTenant.createdAt)} />
                    <Detail label="Last Activity" value={selectedTenant.lastActivityAt ? formatDateTime(selectedTenant.lastActivityAt) : '—'} />
                    <Detail label="Work Orders" value={selectedTenant.workOrderCount != null ? String(selectedTenant.workOrderCount) : '—'} />
                    <Detail label="Assets" value={selectedTenant.assetCount != null ? String(selectedTenant.assetCount) : '—'} />
                    <Detail label="Modules" value={selectedTenant.modules && selectedTenant.modules.length > 0 ? selectedTenant.modules.join(', ') : '—'} />
                  </div>
                ) : null}

                {activeDetailSection === 'subscription' ? (
                  <div className="space-y-4">
                    <div className="grid gap-3 md:grid-cols-2">
                      <Detail label="Trial Started" value={selectedTenant.trialStartDate ? formatDateTime(selectedTenant.trialStartDate) : '—'} />
                      <Detail label="Trial Ends" value={selectedTenant.trialEndDate ? formatDateTime(selectedTenant.trialEndDate) : '—'} />
                      <Detail label="Days Remaining" value={selectedTenant.trialDaysRemaining != null ? String(selectedTenant.trialDaysRemaining) : 'Expired'} />
                      <Detail label="Extension" value={formatExtension(selectedTenant)} />
                      <Detail label="Max Users" value={selectedTenant.maxUsers != null ? String(selectedTenant.maxUsers) : '—'} />
                      <Detail label="Max Branches" value={selectedTenant.maxBranches != null ? String(selectedTenant.maxBranches) : '—'} />
                      <Detail label="Subscription Starts" value={selectedTenant.subscriptionStartsAt ? formatDateOnly(selectedTenant.subscriptionStartsAt) : '—'} />
                      <Detail label="Subscription Ends" value={selectedTenant.subscriptionEndsAt ? formatDateOnly(selectedTenant.subscriptionEndsAt) : '—'} />
                    </div>
                    {!selectedTenant.trialExtensionUsed && selectedTenant.licenseStatus !== 'Active' ? (
                      <div className="flex justify-end">
                        <button type="button" className="button-primary" onClick={() => setPendingTrialExtension(selectedTenant)}>Extend trial by 14 days</button>
                      </div>
                    ) : null}
                  </div>
                ) : null}

                {activeDetailSection === 'email-notifications' ? (
                  <TenantCommunicationSettingsPanel
                    tenant={selectedTenant}
                    onToast={({ title, description, tone }) => pushToast({ title, description, tone })}
                  />
                ) : null}
              </div>
            </div>
          ) : null}
        </Modal>

      </PageScaffold>
    </div>
  )
}

// ── Small presentational helpers ──────────────────────────────────────────────

function Detail({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="panel-subtle rounded-2xl p-4">
      <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">{label}</p>
      <div className="mt-2 text-sm text-app">{value}</div>
    </div>
  )
}
