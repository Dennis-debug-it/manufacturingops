import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { formatDate, formatDateTime } from '../shared/utils/dates'
import { api } from '../shared/api/apiClient'
import { endpoints } from '../shared/api/endpoints'
import { useLicensePlans, useLicensingUsage, useSubscriptions } from './api'
import { licenseStatusBadge, trialStatusBadge } from './common'
import type { PlatformSubscription } from './types'
import type { LicenseUsageSnapshot } from './api'

export function LicensingPage() {
  const usage = useLicensingUsage()
  const plans = useLicensePlans()
  const subscriptions = useSubscriptions()
  const [actionTarget, setActionTarget] = useState<PlatformSubscription | null>(null)
  const [action, setAction] = useState<'activate' | 'suspend' | 'cancel'>('activate')

  const rows = subscriptions.data ?? []
  const usageRows: LicenseUsageSnapshot[] = usage.data ?? []
  const plansRows = plans.data ?? []

  const subscriptionColumns: DataTableColumn<PlatformSubscription>[] = [
    { key: 'tenant', header: 'Tenant', cell: (row) => row.tenantName ?? row.tenantId },
    { key: 'plan', header: 'Plan', cell: (row) => row.planName ?? row.planId },
    { key: 'seats', header: 'Office / field seats', cell: (row) => `${row.officeSeats} / ${row.fieldSeats}` },
    { key: 'whatsApp', header: 'WhatsApp', cell: (row) => (row.whatsAppAddOn === 'whatsapp_business' ? 'Business' : row.whatsAppAddOn === 'whatsapp_notifications' ? 'Notifications' : '—') },
    { key: 'status', header: 'Status', cell: (row) => licenseStatusBadge(row.status) },
    { key: 'billingCycle', header: 'Billing', cell: (row) => row.billingCycle },
    { key: 'startsAt', header: 'Start', cell: (row) => formatDateTime(row.startsAt) },
    { key: 'endsAt', header: 'End', cell: (row) => (row.endsAt ? formatDateTime(row.endsAt) : '—') },
    { key: 'trialEndsAt', header: 'Trial end', cell: (row) => (row.trialEndsAt ? formatDateTime(row.trialEndsAt) : '—') },
    { key: 'nextBillingDate', header: 'Next billing', cell: (row) => (row.nextBillingDate ? formatDateTime(row.nextBillingDate) : '—') },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <div className="flex flex-wrap gap-2">
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => { setActionTarget(row); setAction('activate') }}>Activate</button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => { setActionTarget(row); setAction('suspend') }}>Suspend</button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => { setActionTarget(row); setAction('cancel') }}>Cancel</button>
        </div>
      ),
    },
  ]

  const runAction = async () => {
    if (!actionTarget) return
    try {
      await api.post(
        action === 'activate'
          ? endpoints.platformLicensing.subscriptions + `/${actionTarget.id}/activate`
          : action === 'suspend'
            ? endpoints.platformLicensing.subscriptions + `/${actionTarget.id}/suspend`
            : endpoints.platformLicensing.subscriptions + `/${actionTarget.id}/cancel`,
      )
      toast.success('Subscription updated')
      setActionTarget(null)
    } catch {
      // handled by interceptor
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Licensing"
        subtitle="Trial, paid, suspended, and deactivated tenant license lifecycle."
        primaryAction={<Link to="/platform/settings/plans" className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800">Editions & pricing</Link>}
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Kpi label="Usage snapshots" value={usageRows.length} />
        <Kpi label="Editions" value={plansRows.length} />
        <Kpi label="Subscriptions" value={rows.length} />
        <Kpi label="Trial expiring soon" value={rows.filter((row) => row.trialEndsAt && row.status !== 'Cancelled').length} />
      </section>

      {usage.isLoading || plans.isLoading || subscriptions.isLoading ? <Spinner label="Loading licensing data" /> : null}
      {usage.isError || plans.isError || subscriptions.isError ? <EmptyState title="Licensing unavailable" description="Licensing data could not be loaded." /> : null}

      {!usage.isLoading && !plans.isLoading && !subscriptions.isLoading && !(usage.isError || plans.isError || subscriptions.isError) ? (
        <div className="space-y-5">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Tenant usage" description="License and trial posture by tenant." />
            {usageRows.length === 0 ? <EmptyState title="No usage snapshots" description="Tenant usage snapshots will appear once licenses are provisioned." /> : (
              <DataTable<LicenseUsageSnapshot> columns={[
                { key: 'tenant', header: 'Tenant', cell: (row) => row.tenantName },
                { key: 'plan', header: 'Plan', cell: (row) => row.planName ?? '—' },
                { key: 'modules', header: 'Modules', cell: (row) => row.modules?.join(', ') || '—' },
                { key: 'status', header: 'License', cell: (row) => licenseStatusBadge(row.licenseStatus) },
                { key: 'trial', header: 'Trial', cell: (row) => trialStatusBadge(row.status) },
                { key: 'maxBranches', header: 'Max branches', cell: (row) => row.maxBranches ?? 'Unlimited' },
                { key: 'trialEndsAt', header: 'Trial end', cell: (row) => (row.trialEndsAt ? formatDate(row.trialEndsAt) : '—') },
                { key: 'expiresAt', header: 'Subscription end', cell: (row) => (row.expiresAt ? formatDate(row.expiresAt) : '—') },
                { key: 'daysRemaining', header: 'Days remaining', cell: (row) => row.daysRemaining ?? '—' },
              ]} data={usageRows} />
            )}
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Subscriptions" description="Lifecycle control for tenant subscriptions." />
            <DataTable<PlatformSubscription> columns={subscriptionColumns} data={rows} emptyTitle="No subscriptions" emptyDescription="Subscription records will appear here." />
          </section>

        </div>
      ) : null}

      <ConfirmDialog
        open={actionTarget !== null}
        title={`${action === 'activate' ? 'Activate' : action === 'suspend' ? 'Suspend' : 'Cancel'} subscription`}
        message={actionTarget ? `${action === 'activate' ? 'Activate' : action === 'suspend' ? 'Suspend' : 'Cancel'} the subscription for ${actionTarget.tenantName ?? actionTarget.tenantId}?` : ''}
        confirmLabel="Apply"
        onCancel={() => setActionTarget(null)}
        onConfirm={() => void runAction()}
      />
    </div>
  )
}

function Kpi({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function SectionTitle({ title, description }: { title: string; description: string }) {
  return (
    <div>
      <p className="text-base font-semibold text-slate-900">{title}</p>
      <p className="mt-1 text-sm text-slate-600">{description}</p>
    </div>
  )
}
