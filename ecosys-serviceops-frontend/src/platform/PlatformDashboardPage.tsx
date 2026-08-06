import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Activity, ArrowRight, Building2, Clock3, DollarSign, FileWarning, ShieldAlert, Users } from 'lucide-react'
import { api } from '../shared/api/apiClient'
import { PageHeader } from '../shared/components/PageHeader'
import { AsyncPanel } from '../shared/components/AsyncPanel'
import { LastUpdated } from '../shared/components/LastUpdated'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { StatusBadge } from '../shared/components/StatusBadge'
import { formatCurrency } from '../shared/utils/format'
import { formatDate, formatDateTime } from '../shared/utils/dates'
import {
  useLeads,
  usePlatformDashboardSummary,
  usePlatformSummary,
  usePlatformAuditLogs,
  useTenants,
} from './api'
import { licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from './common'
import type { PlatformLeadListItem, PlatformTenantListItem } from './types'

// Independent, self-refreshing health pill — deliberately NOT part of the dashboard's combined
// loading/error gate, so system-health stays visible even if other panels are slow or failing.
function HealthChip() {
  const { data, isError } = useQuery({
    queryKey: ['platform', 'health', 'chip'],
    queryFn: () => api.get<{ services: Array<{ status: string }> }>('/api/platform/health'),
    refetchInterval: 30_000,
    staleTime: 20_000,
  })

  const base = 'inline-flex items-center gap-2 rounded-full border px-3 py-1 text-xs font-medium transition'
  if (isError) {
    return <Link to="/platform/health" className={`${base} border-rose-200 bg-rose-50 text-rose-700`}><Activity className="h-3.5 w-3.5" />Health check unavailable</Link>
  }
  if (!data) {
    return <span className={`${base} border-slate-200 bg-slate-50 text-slate-500`}><Activity className="h-3.5 w-3.5" />Checking system health…</span>
  }

  const down = data.services.filter((s) => s.status === 'Down').length
  const degraded = data.services.filter((s) => s.status === 'Degraded').length
  const tone = down > 0 ? 'border-rose-200 bg-rose-50 text-rose-700' : degraded > 0 ? 'border-amber-200 bg-amber-50 text-amber-800' : 'border-emerald-200 bg-emerald-50 text-emerald-700'
  const label = down > 0 ? `${down} service${down > 1 ? 's' : ''} down` : degraded > 0 ? `${degraded} degraded` : 'All systems healthy'
  return (
    <Link to="/platform/health" className={`${base} ${tone}`}>
      <span className="h-2 w-2 rounded-full bg-current" />
      {label}
      <span className="opacity-60">· view</span>
    </Link>
  )
}

function Kpi({ label, value, icon, tone = 'default' }: { label: string; value: string | number; icon: ReactNode; tone?: 'default' | 'good' | 'warning' | 'danger' }) {
  const valueClass =
    tone === 'good' ? 'text-emerald-700'
      : tone === 'warning' ? 'text-amber-700'
        : tone === 'danger' ? 'text-rose-700'
          : 'text-slate-900'

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</p>
        <div className="text-slate-400">{icon}</div>
      </div>
      <div className={`mt-3 text-2xl font-semibold tracking-tight ${valueClass}`}>{value}</div>
    </div>
  )
}

export function PlatformDashboardPage() {
  const summary = usePlatformDashboardSummary()
  const platform = usePlatformSummary()
  const tenants = useTenants()
  const leads = useLeads()
  const auditLogs = usePlatformAuditLogs()

  // Only the two aggregate endpoints that feed the headline KPIs are load-critical; the tenant,
  // lead, and audit panels below degrade independently via <AsyncPanel> so one slow/failing list
  // no longer blanks the whole command centre.
  if (summary.isLoading || platform.isLoading) {
    return <Spinner label="Loading command centre" />
  }

  if (summary.isError || platform.isError || !summary.data || !platform.data) {
    return (
      <EmptyState
        title="Command centre unavailable"
        description="The platform overview could not be loaded."
        actionLabel="Retry"
        onAction={() => { void summary.refetch(); void platform.refetch() }}
      />
    )
  }

  const tenantRows = tenants.data ?? []
  const leadRows = leads.data ?? []
  const auditRows = (auditLogs.data ?? []).slice(0, 8)

  const attentionTenants = tenantRows.filter((tenant) =>
    tenant.status === 'Suspended'
    || tenant.trialStatus === 'TrialExpired'
    || tenant.trialStatus === 'TrialExpiringSoon'
    || tenant.licenseStatus === 'Expired'
    || tenant.licenseStatus === 'Suspended',
  ).slice(0, 8)

  const recentTenants = [...tenantRows].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 8)
  const recentLeads = [...leadRows].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 8)

  const tenantColumns: DataTableColumn<PlatformTenantListItem>[] = [
    { key: 'name', header: 'Tenant', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/platform/tenants/${row.tenantId}`}>{row.name}</Link> },
    { key: 'slug', header: 'Workspace', cell: (row) => <span className="font-mono text-xs text-slate-600">{row.slug}</span> },
    { key: 'plan', header: 'Plan', cell: (row) => row.planName ?? 'No plan' },
    { key: 'status', header: 'Status', cell: (row) => tenantStatusBadge(row.status) },
    { key: 'licenseStatus', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
    { key: 'trial', header: 'Trial', cell: (row) => trialStatusBadge(row.trialStatus) },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
  ]

  const leadColumns: DataTableColumn<PlatformLeadListItem>[] = [
    { key: 'company', header: 'Company', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/platform/leads?lead=${row.id}`}>{row.companyName}</Link> },
    { key: 'contact', header: 'Contact', cell: (row) => row.contactPersonName },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDateTime(row.createdAt) },
  ]

  return (
    <div className="space-y-5" data-testid="command-centre-dashboard">
      <PageHeader
        title="Platform Owner Overview"
        subtitle="Operational command centre for tenant lifecycle, leads, licensing, and finance."
        primaryAction={<div className="flex items-center gap-3"><LastUpdated at={summary.dataUpdatedAt} /><HealthChip /></div>}
      />

      <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Kpi label="Total tenants" value={summary.data.totalTenants} icon={<Building2 className="h-4 w-4" />} />
        <Kpi label="Active tenants" value={summary.data.activeTenants} icon={<ShieldAlert className="h-4 w-4" />} tone="good" />
        <Kpi label="Total users" value={summary.data.totalUsers} icon={<Users className="h-4 w-4" />} />
        <Kpi label="MRR" value={formatCurrency(summary.data.monthlyRecurringRevenue)} icon={<DollarSign className="h-4 w-4" />} tone="good" />
        <Kpi label="Suspended tenants" value={summary.data.suspendedTenants} icon={<AlertTriangle className="h-4 w-4" />} tone="warning" />
        <Kpi label="Expiring invoices" value={summary.data.overdueInvoices} icon={<FileWarning className="h-4 w-4" />} tone="danger" />
        <Kpi label="Active now" value={platform.data.activeUsersNow} icon={<Clock3 className="h-4 w-4" />} />
        <Kpi label="Logged in today" value={platform.data.loggedInToday} icon={<Clock3 className="h-4 w-4" />} />
      </section>

      <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
        <div className="space-y-5">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-slate-900">Tenant lifecycle</p>
                <p className="mt-1 text-sm text-slate-600">Recent tenants and current status posture.</p>
              </div>
              <Link className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/tenants">
                View tenants
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <div className="mt-4">
              <AsyncPanel query={tenants} loadingLabel="Loading tenants" errorTitle="Tenants unavailable" errorDescription="Recent tenants could not be loaded.">
                {() => (
                  <DataTable<PlatformTenantListItem>
                    columns={tenantColumns}
                    data={recentTenants}
                    emptyTitle="No tenants yet"
                    emptyDescription="Tenant workspaces will appear here after creation."
                  />
                )}
              </AsyncPanel>
            </div>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-slate-900">Lead funnel</p>
                <p className="mt-1 text-sm text-slate-600">Recent workspace requests and follow-up status.</p>
              </div>
              <Link className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/leads">
                View leads
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <div className="mt-4">
              <AsyncPanel query={leads} loadingLabel="Loading leads" errorTitle="Leads unavailable" errorDescription="Recent leads could not be loaded.">
                {() => (
                  <DataTable<PlatformLeadListItem>
                    columns={leadColumns}
                    data={recentLeads}
                    emptyTitle="No leads yet"
                    emptyDescription="Public workspace requests will appear here."
                  />
                )}
              </AsyncPanel>
            </div>
          </div>
        </div>

        <div className="space-y-5">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-slate-900">Attention queue</p>
                <p className="mt-1 text-sm text-slate-600">Tenants needing a platform owner decision.</p>
              </div>
              <Link className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/licensing">
                Review licensing
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <AsyncPanel query={tenants} loadingLabel="Loading tenants" errorTitle="Unavailable" errorDescription="The attention queue could not be loaded.">
              {() => attentionTenants.length === 0 ? (
              <div className="mt-4">
                <EmptyState title="No attention required" description="No tenants are expiring, suspended, or overdue right now." />
              </div>
            ) : (
              <div className="mt-4 space-y-2">
                {attentionTenants.map((tenant) => (
                  <Link key={tenant.tenantId} to={`/platform/tenants/${tenant.tenantId}`} className="flex items-center justify-between rounded-xl border border-slate-200 px-4 py-3 transition hover:bg-slate-50">
                    <div>
                      <p className="font-medium text-slate-900">{tenant.name}</p>
                      <p className="mt-1 text-xs text-slate-500">{tenant.slug} · {tenant.contactEmail ?? 'No contact email'}</p>
                    </div>
                    <div className="text-right">
                      <div>{tenantStatusBadge(tenant.status)}</div>
                      <div className="mt-2 text-xs text-slate-500">
                        {tenant.trialDaysRemaining != null ? `${tenant.trialDaysRemaining} days remaining` : tenant.trialStatus}
                      </div>
                    </div>
                  </Link>
                ))}
              </div>
            )}
            </AsyncPanel>
          </div>

          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <div className="flex items-center justify-between gap-3">
              <div>
                <p className="text-base font-semibold text-slate-900">Recent activity</p>
                <p className="mt-1 text-sm text-slate-600">Latest audit entries across the platform.</p>
              </div>
              <Link className="inline-flex items-center gap-2 text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/audit">
                Open audit log
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
            <AsyncPanel query={auditLogs} loadingLabel="Loading activity" errorTitle="Unavailable" errorDescription="Recent activity could not be loaded.">
              {() => auditRows.length === 0 ? (
              <div className="mt-4">
                <EmptyState title="No audit entries yet" description="Platform actions will appear here." />
              </div>
            ) : (
              <div className="mt-4 space-y-2">
                {auditRows.map((entry) => (
                  <div key={entry.id} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-medium text-slate-900">{entry.action}</p>
                        <p className="mt-1 text-xs text-slate-500">{entry.actorName} · {entry.tenantName ?? 'Platform'}</p>
                      </div>
                      <StatusBadge status={entry.severity} />
                    </div>
                    <p className="mt-2 text-sm text-slate-600">{entry.description ?? 'No detail recorded.'}</p>
                    <p className="mt-2 text-xs text-slate-500">{formatDateTime(entry.createdAt)}</p>
                  </div>
                ))}
              </div>
            )}
            </AsyncPanel>
          </div>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-5 xl:grid-cols-2">
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-base font-semibold text-slate-900">Outstanding invoices</p>
              <p className="mt-1 text-sm text-slate-600">Balances still awaiting settlement.</p>
            </div>
            <Link className="text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/finance">
              Open finance
            </Link>
          </div>
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Kpi label="Outstanding" value={formatCurrency(summary.data.outstandingBalance)} icon={<DollarSign className="h-4 w-4" />} tone="danger" />
            <Kpi label="Invoices overdue" value={summary.data.overdueInvoices} icon={<FileWarning className="h-4 w-4" />} tone="warning" />
          </div>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <div className="flex items-center justify-between gap-3">
            <div>
              <p className="text-base font-semibold text-slate-900">Platform health</p>
              <p className="mt-1 text-sm text-slate-600">Basic operational posture from platform summary.</p>
            </div>
            <Link className="text-sm font-medium text-slate-700 hover:text-slate-900" to="/platform/reports">
              View reports
            </Link>
          </div>
          <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Kpi label="Active sessions" value={platform.data.activeUsersNow} icon={<Clock3 className="h-4 w-4" />} />
            <Kpi label="Tenants with activity" value={platform.data.tenantsWithActiveUsers} icon={<Building2 className="h-4 w-4" />} />
          </div>
        </div>
      </section>
    </div>
  )
}
