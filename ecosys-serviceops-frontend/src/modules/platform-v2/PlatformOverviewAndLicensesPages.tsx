import { useMemo, useState } from 'react'
import { BarChart3, Building2, FileText, RefreshCw, Wallet } from 'lucide-react'
import { StatCard } from '../../components/ui/StatCard'
import { useAsyncData } from '../../hooks/useAsyncData'
import { platformService, toServiceError } from '../../services/platformService'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { InfoAlert } from '../../components/ui/InfoAlert'
import { LoadingState } from '../../components/ui/LoadingState'
import { useToast } from '../../components/ui/ToastProvider'
import { PageScaffold, SectionCard } from '../../components/ui/Workspace'
import { licenseStatusBadge, tenantStatusBadge } from './PlatformCommon'
import { formatDateOnly } from '../../utils/date'
import type { Tenant } from '../../types/platform'

type OverviewPayload = {
  tenants: Tenant[]
  dashboard: Record<string, unknown>
  summary: Record<string, unknown>
  finance: Record<string, unknown>
}

function asNumber(value: unknown) {
  return typeof value === 'number' ? value : Number(value || 0)
}

function asString(value: unknown, fallback = '-') {
  return typeof value === 'string' && value ? value : fallback
}

function safeDate(value: unknown) {
  return typeof value === 'string' && value ? formatDateOnly(value) : 'Not set'
}

export function PlatformOverviewPage() {
  const { data, loading, error, reload } = useAsyncData<OverviewPayload>(
    async () => {
      const [tenants, dashboard, summary, finance] = await Promise.all([
        platformService.tenantsApi.list(),
        platformService.reportsApi.dashboardSummary(),
        platformService.reportsApi.revenue(),
        platformService.financeApi.getSummary(),
      ])
      return { tenants: tenants.data, dashboard, summary, finance: finance.data as unknown as Record<string, unknown> }
    },
    { tenants: [], dashboard: {}, summary: {}, finance: {} },
    [],
  )

  const activeTenants = asNumber(data.dashboard.totalTenants ? data.dashboard.activeTenants : data.tenants.filter((item) => item.status === 'Active').length)
  const suspendedTenants = asNumber(data.dashboard.suspendedTenants)
  const deactivatedTenants = asNumber(data.dashboard.deactivatedTenants)
  const activeTrials = data.tenants.filter((item) => item.trialStatus === 'TrialActive' || item.trialStatus === 'TrialExtended').length
  const expiringSoonTrials = data.tenants.filter((item) => item.trialStatus === 'TrialExpiringSoon').length
  const expiredTrials = data.tenants.filter((item) => item.trialStatus === 'TrialExpired').length

  return (
    <div data-testid="command-centre-dashboard">
      <PageScaffold
        eyebrow="Platform Command Centre"
        title="System Overview"
        description="Platform-wide operational, licensing, and finance posture."
      >
        {loading ? <LoadingState label="Loading platform overview" /> : null}
        {!loading && error ? (
          <SectionCard title="Unable to load overview" description={error}>
            <InfoAlert title="Unable to load overview" description={error} tone="danger" />
            <button type="button" className="button-secondary" onClick={() => void reload()}><RefreshCw className="h-4 w-4" />Retry</button>
          </SectionCard>
        ) : null}

        {!loading && !error ? (
          <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <StatCard title="Total Tenants" value={String(asNumber(data.dashboard.totalTenants || data.tenants.length))} detail="All registered tenants." icon={Building2} />
            <StatCard title="Active Tenants" value={String(activeTenants)} detail="Tenants currently active." icon={Building2} accent="emerald" />
            <StatCard title="Suspended Tenants" value={String(suspendedTenants)} detail="Temporarily blocked tenants." icon={FileText} accent="amber" />
            <StatCard title="Deactivated Tenants" value={String(deactivatedTenants)} detail="Inactive tenants." icon={BarChart3} />
            <StatCard title="Active Trials" value={String(activeTrials)} detail="Trial tenants with time remaining." icon={Building2} />
            <StatCard title="Trials Expiring Soon" value={String(expiringSoonTrials)} detail="Three days remaining or fewer." icon={FileText} accent="amber" />
            <StatCard title="Expired Trials" value={String(expiredTrials)} detail="Trials requiring follow-up." icon={BarChart3} />
          </section>

          <section className="grid gap-4 md:grid-cols-3">
            <StatCard title="Total Revenue" value={`KES ${asNumber(data.summary.totalRevenue).toLocaleString()}`} detail="Paid collections." icon={Wallet} />
            <StatCard title="Outstanding Balance" value={`KES ${asNumber(data.summary.outstandingBalance).toLocaleString()}`} detail="Unpaid invoice balance." icon={Wallet} />
            <StatCard title="Net Position" value={`KES ${(asNumber(data.summary.totalRevenue) - asNumber(data.summary.totalExpenses)).toLocaleString()}`} detail="Revenue minus expenses." icon={Wallet} accent="emerald" />
          </section>

          <SectionCard title="Recent tenants" description="Latest tenant workspaces created across the platform.">
            {data.tenants.length === 0 ? <EmptyState title="No tenants yet" description="Create a tenant to populate this overview." /> : (
              <DataTable
                rows={[...data.tenants].sort((a, b) => b.createdAt.localeCompare(a.createdAt)).slice(0, 8)}
                rowKey={(row) => row.tenantId}
                minTableWidth="min-w-[980px] w-full"
                columns={[
                  { key: 'name', header: 'Tenant', cell: (row) => <span className="font-semibold text-app">{row.name}</span> },
                  { key: 'plan', header: 'Plan', cell: (row) => row.plan },
                  { key: 'licenseStatus', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
                  { key: 'trialStatus', header: 'Trial', cell: (row) => row.trialStatus || 'Not set' },
                  { key: 'status', header: 'Status', cell: (row) => tenantStatusBadge(row.status) },
                  { key: 'createdAt', header: 'Created At', cell: (row) => formatDateOnly(row.createdAt) },
                ]}
              />
            )}
          </SectionCard>
          </>
        ) : null}
      </PageScaffold>
    </div>
  )
}

type LicensePayload = {
  tenants: Tenant[]
  snapshots: Array<Record<string, unknown>>
  plans: Array<Record<string, unknown>>
  subscriptions: Array<Record<string, unknown>>
}

export function PlatformLicensesPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData<LicensePayload>(
    async () => {
      const [tenants, snapshots, plans, subscriptions] = await Promise.all([
        platformService.tenantsApi.list(),
        platformService.licensesApi.list(),
        platformService.licensesApi.getPlans(),
        platformService.licensesApi.getSubscriptions(),
      ])
      return {
        tenants: tenants.data,
        snapshots: snapshots.data,
        plans,
        subscriptions,
      }
    },
    { tenants: [], snapshots: [], plans: [], subscriptions: [] },
    [],
  )
  const [statusBusyId, setStatusBusyId] = useState<string | null>(null)

  const tenantMap = useMemo(() => new Map(data.tenants.map((item) => [item.tenantId, item.name])), [data.tenants])

  async function updateSubscriptionStatus(id: string, action: 'activate' | 'suspend' | 'cancel') {
    setStatusBusyId(id)
    try {
      if (action === 'activate') {
        await platformService.licensesApi.activateSubscription(id)
      } else if (action === 'suspend') {
        await platformService.licensesApi.suspendSubscription(id)
      } else {
        await platformService.licensesApi.cancelSubscription(id)
      }
      pushToast({ title: 'Subscription updated', description: `Subscription ${action}d successfully.`, tone: 'success' })
      await reload()
    } catch (updateError) {
      pushToast({ title: 'Update failed', description: toServiceError(updateError, 'Unable to update subscription.'), tone: 'danger' })
    } finally {
      setStatusBusyId(null)
    }
  }

  const snapshotRows = useMemo<Array<Record<string, unknown>>>(
    () => data.snapshots.map((row) => ({ ...row, tenantName: tenantMap.get(asString(row['tenantId'], '')) || asString(row['tenantId'], 'Unknown') })),
    [data.snapshots, tenantMap],
  )

  return (
    <PageScaffold
      eyebrow="Platform Command Centre"
      title="Licenses & Subscriptions"
      description="Plan allocation, status, limits, and renewal timing."
    >

      {loading ? <LoadingState label="Loading licensing data" /> : null}
      {!loading && error ? (
        <SectionCard title="Unable to load licensing data" description={error}>
          <InfoAlert title="Unable to load licensing data" description={error} tone="danger" />
          <button type="button" className="button-secondary" onClick={() => void reload()}><RefreshCw className="h-4 w-4" />Retry</button>
        </SectionCard>
      ) : null}

      {!loading && !error ? (
        <>
          <SectionCard title="Active Subscriptions" description="Lifecycle control for live or pending platform subscriptions.">
            {data.subscriptions.length === 0 ? <EmptyState title="No subscriptions found" description="Create subscriptions from the backend API to populate this section." /> : (
              <DataTable
                rows={data.subscriptions}
                rowKey={(row) => asString(row.id, String(Math.random()))}
                minTableWidth="min-w-[1300px] w-full"
                columns={[
                  { key: 'tenant', header: 'Tenant', cell: (row) => asString(row.tenantName, asString(row.tenantId, 'Unknown')) },
                  { key: 'plan', header: 'Plan', cell: (row) => asString(row.planName, asString(row.planId, '-')) },
                  { key: 'status', header: 'Status', cell: (row) => asString(row.status) },
                  { key: 'billing', header: 'Billing Cycle', cell: (row) => asString(row.billingCycle) },
                  { key: 'startsAt', header: 'Starts At', cell: (row) => safeDate(row.startsAt) },
                  { key: 'endsAt', header: 'Ends At', cell: (row) => safeDate(row.endsAt) },
                  { key: 'nextBillingDate', header: 'Next Billing', cell: (row) => safeDate(row.nextBillingDate) },
                  {
                    key: 'actions',
                    header: 'Actions',
                    className: 'min-w-[260px]',
                    cell: (row) => (
                      <div className="flex flex-wrap gap-2">
                        <button type="button" className="button-secondary px-3 py-2" disabled={statusBusyId === asString(row.id)} onClick={() => void updateSubscriptionStatus(asString(row.id), 'activate')}>Activate</button>
                        <button type="button" className="button-secondary px-3 py-2" disabled={statusBusyId === asString(row.id)} onClick={() => void updateSubscriptionStatus(asString(row.id), 'suspend')}>Suspend</button>
                        <button type="button" className="button-secondary px-3 py-2" disabled={statusBusyId === asString(row.id)} onClick={() => void updateSubscriptionStatus(asString(row.id), 'cancel')}>Cancel</button>
                      </div>
                    ),
                  },
                ]}
              />
            )}
          </SectionCard>

          <SectionCard title="License Plans" description="Available product plans and usage ceilings.">
            {data.plans.length === 0 ? <EmptyState title="No plans found" description="Create plans with /api/platform/licensing/plans." /> : (
              <DataTable
                rows={data.plans}
                rowKey={(row) => asString(row.id, String(Math.random()))}
                minTableWidth="min-w-[1200px] w-full"
                columns={[
                  { key: 'name', header: 'Plan', cell: (row) => asString(row.name, '-') },
                  { key: 'code', header: 'Code', cell: (row) => asString(row.code, '-') },
                  { key: 'monthly', header: 'Monthly', cell: (row) => `KES ${asNumber(row.monthlyPrice).toLocaleString()}` },
                  { key: 'annual', header: 'Annual', cell: (row) => `KES ${asNumber(row.annualPrice).toLocaleString()}` },
                  { key: 'maxUsers', header: 'Max Users', cell: (row) => asString(row.maxUsers, '-') },
                  { key: 'maxAssets', header: 'Max Assets', cell: (row) => asString(row.maxAssets, '-') },
                  { key: 'maxWorkOrders', header: 'Max Work Orders', cell: (row) => asString(row.maxWorkOrders, '-') },
                  { key: 'active', header: 'Is Active', cell: (row) => String(Boolean(row.isActive)) },
                ]}
              />
            )}
          </SectionCard>

          <SectionCard title="Tenant License Snapshots" description="Current entitlements and status by tenant.">
            {snapshotRows.length === 0 ? <EmptyState title="No snapshot records" description="Snapshots appear once tenant licenses are initialized." /> : (
              <DataTable
                rows={snapshotRows}
                rowKey={(row) => asString(row['tenantId'], String(Math.random()))}
                minTableWidth="min-w-[1250px] w-full"
                columns={[
                  { key: 'tenant', header: 'Tenant', cell: (row) => asString(row['tenantName']) },
                  { key: 'planName', header: 'Plan', cell: (row) => asString(row['planName'], asString(row['planCode'], '-')) },
                  { key: 'status', header: 'Status', cell: (row) => asString(row['status']) },
                  { key: 'startsAt', header: 'Start Date', cell: (row) => safeDate(row['startsAt']) },
                  { key: 'expiresAt', header: 'Expiry Date', cell: (row) => safeDate(row['expiresAt']) },
                  { key: 'maxUsers', header: 'Max Users', cell: (row) => asString(row['maxUsers'], '-') },
                  { key: 'maxBranches', header: 'Max Branches', cell: (row) => asString(row['maxBranches'], '-') },
                ]}
              />
            )}
          </SectionCard>
        </>
      ) : null}
    </PageScaffold>
  )
}
