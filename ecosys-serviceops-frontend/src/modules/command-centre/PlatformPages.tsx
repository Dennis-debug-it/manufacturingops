import { BarChart3, Building2, CreditCard, FileText, Receipt, Settings2, ShieldCheck, Users, Wallet } from 'lucide-react'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { StatCard } from '../../components/ui/StatCard'
import { useAsyncData } from '../../hooks/useAsyncData'
import { licenseService } from '../../services/licenseService'
import { platformReportsService } from '../../services/platformReportsService'
import { platformTenantService } from '../../services/platformTenantService'
import type { PlatformReportsSummary, PlatformTenant, TenantLicenseSnapshot } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { CommandCentrePage } from './CommandCentrePage'

export const PlatformTenantsPage = CommandCentrePage

type OverviewPayload = {
  tenants: PlatformTenant[]
  licenses: TenantLicenseSnapshot[]
}

type LicensePayload = {
  tenants: PlatformTenant[]
  licenses: TenantLicenseSnapshot[]
}

export function PlatformOverviewPage() {
  const { data, loading, error } = useAsyncData<OverviewPayload>(
    async (signal) => {
      const [tenants, licenses] = await Promise.all([
        platformTenantService.getPlatformTenants(signal),
        licenseService.getTenantLicenses(signal),
      ])

      return { tenants, licenses }
    },
    { tenants: [], licenses: [] },
    [],
  )

  const activeTenants = data.tenants.filter((tenant) => tenant.status === 'Active').length
  const trialTenants = data.tenants.filter((tenant) => tenant.status === 'Trial').length
  const suspendedTenants = data.tenants.filter((tenant) => tenant.status === 'Suspended').length
  const expiringLicenses = data.licenses
    .filter((license) => license.expiresAt)
    .sort((left, right) => new Date(left.expiresAt || '').getTime() - new Date(right.expiresAt || '').getTime())
    .slice(0, 5)

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Platform"
        title="Overview"
        description="Monitor the Ecosys platform business, tenant growth, and subscription health."
      />

      {loading ? <LoadingState label="Loading platform overview" /> : null}
      {!loading && error ? <ErrorState title="Unable to load overview" description={error} /> : null}

      {!loading && !error ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
            <StatCard title="Total Tenants" value={String(data.tenants.length)} detail="All platform-managed workspaces." icon={Building2} />
            <StatCard title="Active Tenants" value={String(activeTenants)} detail="Currently available to sign in." icon={ShieldCheck} accent="emerald" />
            <StatCard title="Trial Tenants" value={String(trialTenants)} detail="Tenants currently on trial access." icon={FileText} accent="amber" />
            <StatCard title="Suspended Tenants" value={String(suspendedTenants)} detail="Tenants paused by platform admin." icon={CreditCard} accent="rose" />
            <StatCard title="Expiring Licenses" value={String(expiringLicenses.length)} detail="Nearest license renewals in view." icon={BarChart3} />
          </section>

          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            <StatCard title="Monthly Recurring Revenue" value="0" detail="No platform billing records captured yet." icon={Wallet} />
            <StatCard title="Annual Recurring Revenue" value="0" detail="Ready for subscription billing totals." icon={Wallet} />
            <StatCard title="Net Revenue / Profit" value="0" detail="Calculated from payments less expenses." icon={Receipt} />
          </section>

          <section className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
            <section className="surface-card space-y-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="text-lg font-semibold text-app">Recently onboarded tenants</p>
                  <p className="mt-1 text-sm text-muted">Newest platform accounts and current subscription standing.</p>
                </div>
                <Badge tone="info">{Math.min(data.tenants.length, 6)}</Badge>
              </div>
              <DataTable
                rows={[...data.tenants].sort((left, right) => new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()).slice(0, 6)}
                rowKey={(row) => row.tenantId}
                pageSize={6}
                emptyTitle="No tenants yet"
                emptyDescription="New platform tenants will appear here after onboarding."
                columns={[
                  { key: 'name', header: 'Tenant', cell: (row) => <span className="font-semibold text-app">{row.name}</span> },
                  { key: 'plan', header: 'Plan', cell: (row) => <span>{row.planName || 'Not set'}</span> },
                  { key: 'status', header: 'Status', cell: (row) => <Badge tone={tenantTone(row.status)}>{row.status}</Badge> },
                  { key: 'createdAt', header: 'Created', cell: (row) => <span>{formatDateOnly(row.createdAt)}</span> },
                ]}
              />
            </section>

            <section className="surface-card space-y-4">
              <div>
                <p className="text-lg font-semibold text-app">Expiring licenses</p>
                <p className="mt-1 text-sm text-muted">Use this list to review upcoming renewals.</p>
              </div>
              {expiringLicenses.length === 0 ? (
                <EmptyState
                  title="No expiring licenses"
                  description="License renewals will appear here when expiry dates are available."
                />
              ) : (
                <div className="space-y-3">
                  {expiringLicenses.map((license) => (
                    <article key={license.tenantId} className="panel-subtle rounded-2xl p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="font-semibold text-app">{license.planName}</p>
                          <p className="mt-1 text-xs text-muted">{license.planCode}</p>
                        </div>
                        <Badge tone={licenseTone(license.status)}>{license.status}</Badge>
                      </div>
                      <p className="mt-3 text-sm text-muted">Expires {formatDateOnly(license.expiresAt || undefined)}</p>
                    </article>
                  ))}
                </div>
              )}
            </section>
          </section>
        </>
      ) : null}
    </div>
  )
}

export function PlatformLicensesPage() {
  const { data, loading, error } = useAsyncData<LicensePayload>(
    async (signal) => {
      const [tenants, licenses] = await Promise.all([
        platformTenantService.getPlatformTenants(signal),
        licenseService.getTenantLicenses(signal),
      ])

      return { tenants, licenses }
    },
    { tenants: [], licenses: [] },
    [],
  )

  const tenantLookup = new Map(data.tenants.map((tenant) => [tenant.tenantId, tenant]))

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Platform"
        title="Licenses & Subscriptions"
        description="Review tenant plan assignments, license states, and renewal dates."
      />

      {loading ? <LoadingState label="Loading licenses" /> : null}
      {!loading && error ? <ErrorState title="Unable to load licenses" description={error} /> : null}

      {!loading && !error ? (
        <section className="surface-card space-y-4">
          <DataTable
            rows={data.licenses}
            rowKey={(row) => row.tenantId}
            pageSize={10}
            emptyTitle="No licenses found"
            emptyDescription="Tenant licenses will appear here once platform licensing is active."
            minTableWidth="min-w-[1180px] w-full"
            columns={[
              { key: 'tenant', header: 'Tenant', cell: (row) => <span className="font-semibold text-app">{tenantLookup.get(row.tenantId)?.name || row.tenantId}</span> },
              { key: 'plan', header: 'Plan', cell: (row) => <span>{row.planName}</span> },
              { key: 'status', header: 'Status', cell: (row) => <Badge tone={licenseTone(row.status)}>{row.status}</Badge> },
              { key: 'startsAt', header: 'Start Date', cell: (row) => <span>{formatDateOnly(row.startsAt)}</span> },
              { key: 'expiresAt', header: 'End Date', cell: (row) => <span>{formatDateOnly(row.expiresAt || undefined)}</span> },
              { key: 'maxUsers', header: 'Max Users', cell: (row) => <span>{row.maxUsers ?? '-'}</span> },
              { key: 'maxBranches', header: 'Max Branches', cell: (row) => <span>{row.maxBranches ?? '-'}</span> },
              { key: 'warning', header: 'Notes', cell: (row) => <span>{row.warningMessage || 'In good standing'}</span> },
            ]}
          />
        </section>
      ) : null}
    </div>
  )
}

export function PlatformQuotationsPage() {
  return <PlatformModulePage title="Quotations" description="Prepare SaaS subscription, onboarding, customization, support, and training quotations." icon={FileText} />
}

export function PlatformInvoicesPage() {
  return <PlatformModulePage title="Invoices" description="Bill tenants and platform customers using platform-owned invoice records." icon={Receipt} />
}

export function PlatformPaymentsPage() {
  return <PlatformModulePage title="Payments" description="Track collections, partial payments, and settlement history at platform level." icon={CreditCard} />
}

export function PlatformRevenuePage() {
  return <PlatformModulePage title="Revenue" description="Review invoiced revenue, received revenue, outstanding balances, and recurring billing." icon={Wallet} />
}

export function PlatformExpensesPage() {
  return <PlatformModulePage title="Expenses" description="Record and analyze Ecosys platform operating costs and vendor spend." icon={Receipt} />
}

export function PlatformReportsPage() {
  const { data, loading, error } = useAsyncData<PlatformReportsSummary>(
    (signal) => platformReportsService.getSummary(signal),
    {
      totalTenants: 0,
      activeTenants: 0,
      inactiveTenants: 0,
      totalUsers: 0,
      activeUsers: 0,
      monthlyRevenue: 0,
      outstandingInvoices: 0,
      totalExpenses: 0,
      netProfit: 0,
      openWorkOrders: 0,
      closedWorkOrders: 0,
      overdueWorkOrders: 0,
      slaCompliancePercent: 0,
      asAtMonthStartUtc: new Date().toISOString(),
    },
    [],
  )

  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Platform" title="Reports" description="Platform-wide KPIs and operational health metrics." />

      {loading ? <LoadingState label="Loading platform reports" /> : null}
      {!loading && error ? <ErrorState title="Unable to load platform reports. Please check the API connection." description={error} /> : null}

      {!loading && !error ? (
        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <StatCard title="Total Tenants" value={String(data.totalTenants)} detail="All managed tenants." icon={Building2} />
          <StatCard title="Active Tenants" value={String(data.activeTenants)} detail="Currently active tenants." icon={ShieldCheck} accent="emerald" />
          <StatCard title="Inactive Tenants" value={String(data.inactiveTenants)} detail="Suspended or inactive tenants." icon={ShieldCheck} accent="rose" />
          <StatCard title="Total Users" value={String(data.totalUsers)} detail="Users across tenant workspaces." icon={Users} />
          <StatCard title="Active Users" value={String(data.activeUsers)} detail="Users with active accounts." icon={Users} accent="emerald" />
          <StatCard title="Monthly Revenue" value={String(data.monthlyRevenue)} detail="Current month revenue." icon={Wallet} />
          <StatCard title="Outstanding Invoices" value={String(data.outstandingInvoices)} detail="Unpaid invoice balance." icon={Receipt} accent="amber" />
          <StatCard title="Total Expenses" value={String(data.totalExpenses)} detail="Platform operating expenses." icon={Receipt} />
          <StatCard title="Net Profit" value={String(data.netProfit)} detail="Revenue minus expenses." icon={Wallet} />
          <StatCard title="Open Work Orders" value={String(data.openWorkOrders)} detail="Open tenant work orders." icon={FileText} accent="amber" />
          <StatCard title="Closed Work Orders" value={String(data.closedWorkOrders)} detail="Closed tenant work orders." icon={FileText} accent="emerald" />
          <StatCard title="Overdue Work Orders" value={String(data.overdueWorkOrders)} detail="Past due unresolved work orders." icon={FileText} accent="rose" />
          <StatCard title="SLA Compliance" value={`${data.slaCompliancePercent}%`} detail="Completed work orders within SLA due date." icon={BarChart3} />
        </section>
      ) : null}
    </div>
  )
}

export function PlatformUsersPage() {
  return <PlatformModulePage title="Platform Users" description="Manage Platform Owner, Platform Admin, and Support Admin access for Ecosys." icon={Users} />
}

export function PlatformAuditLogsPage() {
  return <PlatformModulePage title="Audit Logs" description="Review platform-level administrative and financial activity." icon={ShieldCheck} />
}

export function PlatformSettingsPage() {
  return <PlatformModulePage title="Settings" description="Configure platform billing, numbering, notifications, and finance preferences." icon={Settings2} />
}

function PlatformModulePage({
  title,
  description,
  icon: Icon,
}: {
  title: string
  description: string
  icon: typeof FileText
}) {
  return (
    <div className="space-y-4">
      <PageHeader eyebrow="Platform" title={title} description={description} />
      <section className="surface-card space-y-4">
        <div className="flex items-center gap-3">
          <div className="rounded-2xl border border-app bg-app/40 p-3">
            <Icon className="h-5 w-5 text-accent-strong" />
          </div>
          <div>
            <p className="font-semibold text-app">{title}</p>
            <p className="mt-1 text-sm text-muted">{description}</p>
          </div>
        </div>
        <EmptyState
          title={`${title} will appear here`}
          description="This Command Centre section is reserved for platform-owned business records and remains isolated from tenant operational data."
        />
      </section>
    </div>
  )
}

function tenantTone(status: PlatformTenant['status']) {
  if (status === 'Active') return 'success'
  if (status === 'Trial') return 'info'
  if (status === 'Suspended') return 'warning'
  return 'danger'
}

function licenseTone(status: string) {
  if (status === 'Active') return 'success'
  if (status === 'Trial') return 'info'
  if (status === 'Suspended') return 'warning'
  if (status === 'Expired') return 'danger'
  return 'neutral'
}
