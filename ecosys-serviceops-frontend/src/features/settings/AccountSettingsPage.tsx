import { useQuery } from '@tanstack/react-query'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDate } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'

interface TenantLicenseSnapshot {
  tenantId: string
  licensePlanId: string
  planCode: string
  planName: string
  status: string
  startsAt: string
  trialEndsAt: string | null
  expiresAt: string | null
  graceEndsAt: string | null
  isReadOnly: boolean
  isSuspended: boolean
  isGracePeriod: boolean
  maxUsers: number | null
  maxBranches: number | null
  maxAssets: number | null
  monthlyWorkOrders: number | null
  emailIngestion: boolean
  monitoringIntegration: boolean
  advancedReports: boolean
  clientPortal: boolean
  warningMessage: string | null
}

function FeatureRow({ label, enabled }: { label: string; enabled: boolean }) {
  return (
    <div className="flex items-center justify-between border-b border-slate-100 py-2 text-sm last:border-0">
      <span className="text-slate-600">{label}</span>
      <StatusBadge status={enabled ? 'Included' : 'Not Included'} />
    </div>
  )
}

export function AccountSettingsPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings', 'license'],
    queryFn: () => api.get<TenantLicenseSnapshot>('/api/settings/license'),
    staleTime: 30_000,
  })

  if (isLoading) {
    return <Spinner label="Loading account details" />
  }

  if (isError || !data) {
    return (
      <ErrorState
        title="Account details unavailable"
        description="We couldn’t load this workspace’s account and licence details."
        onRetry={() => void refetch()}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Account" subtitle="Subscription status, plan limits, and license period. Read-only — contact your Ecosys account manager to make changes." />

      {data.warningMessage ? (
        <div className="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-700">{data.warningMessage}</div>
      ) : null}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Plan" value={data.planName} />
        <StatCard label="Status" value={<StatusBadge status={data.status} />} tone={data.isSuspended ? 'danger' : 'default'} />
        <StatCard label="Max Users" value={data.maxUsers ?? 'Unlimited'} />
        <StatCard label="Max Branches" value={data.maxBranches ?? 'Unlimited'} />
        <StatCard label="Max Assets" value={data.maxAssets ?? 'Unlimited'} />
        <StatCard label="Monthly Work Orders" value={data.monthlyWorkOrders ?? 'Unlimited'} />
        <StatCard label="Started" value={formatDate(data.startsAt)} />
        <StatCard label={data.trialEndsAt ? 'Trial Ends' : 'Expires'} value={(data.trialEndsAt ?? data.expiresAt) ? formatDate((data.trialEndsAt ?? data.expiresAt) as string) : 'No expiry'} />
      </div>

      {data.isSuspended || data.isGracePeriod ? (
        <div className="rounded-2xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">
          {data.isSuspended ? 'This workspace is currently suspended.' : 'This workspace is in its grace period.'}
          {data.graceEndsAt ? ` Grace period ends ${formatDate(data.graceEndsAt)}.` : ''}
        </div>
      ) : null}

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-900">Plan Features</h2>
        <div className="mt-3">
          <FeatureRow label="Email Ingestion" enabled={data.emailIngestion} />
          <FeatureRow label="Monitoring Integration" enabled={data.monitoringIntegration} />
          <FeatureRow label="Advanced Reports" enabled={data.advancedReports} />
          <FeatureRow label="Client Portal" enabled={data.clientPortal} />
        </div>
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 text-sm text-slate-600 shadow-sm">
        Plan changes, seat additions, and billing details are managed by Ecosys platform administrators. Contact support to request a change.
      </div>
    </div>
  )
}
