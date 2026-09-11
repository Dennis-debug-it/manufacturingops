import { useState } from 'react'
import { Plus } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { FormDrawer } from '../shared/components/FormDrawer'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { formatDate, formatDateTime } from '../shared/utils/dates'
import { api } from '../shared/api/apiClient'
import { endpoints } from '../shared/api/endpoints'
import { useLicensePlans, useLicensingUsage, useSubscriptions } from './api'
import { Field, licenseStatusBadge, trialStatusBadge } from './common'
import type { PlatformLicensePlan, PlatformSubscription } from './types'
import type { LicenseUsageSnapshot } from './api'

const emptyPlan = {
  id: null as string | null,
  name: '',
  code: '',
  monthlyPrice: 0,
  annualPrice: 0,
  maxUsers: null as number | null,
  maxBranches: null as number | null,
  maxAssets: null as number | null,
  maxWorkOrders: null as number | null,
  modulesIncluded: '',
  isActive: true,
}

export function LicensingPage() {
  const usage = useLicensingUsage()
  const plans = useLicensePlans()
  const subscriptions = useSubscriptions()
  const [showPlan, setShowPlan] = useState(false)
  const [editingPlan, setEditingPlan] = useState<typeof emptyPlan>(emptyPlan)
  const [actionTarget, setActionTarget] = useState<PlatformSubscription | null>(null)
  const [action, setAction] = useState<'activate' | 'suspend' | 'cancel'>('activate')

  const rows = subscriptions.data ?? []
  const usageRows: LicenseUsageSnapshot[] = usage.data ?? []
  const plansRows = plans.data ?? []

  const subscriptionColumns: DataTableColumn<PlatformSubscription>[] = [
    { key: 'tenant', header: 'Tenant', cell: (row) => row.tenantName ?? row.tenantId },
    { key: 'plan', header: 'Plan', cell: (row) => row.planName ?? row.planId },
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

  const planColumns: DataTableColumn<PlatformLicensePlan>[] = [
    { key: 'name', header: 'Plan', cell: (row) => row.name },
    { key: 'code', header: 'Code', cell: (row) => <span className="font-mono text-xs">{row.code}</span> },
    { key: 'monthly', header: 'Monthly', cell: (row) => row.monthlyPrice.toLocaleString() },
    { key: 'annual', header: 'Annual', cell: (row) => row.annualPrice.toLocaleString() },
    { key: 'modules', header: 'Modules', cell: (row) => row.modulesIncluded.join(', ') || '—' },
    { key: 'active', header: 'Status', cell: (row) => <span className="inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700">{row.isActive ? 'Active' : 'Inactive'}</span> },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setEditingPlan({ ...row, modulesIncluded: row.modulesIncluded.join(', ') })}>
          Edit
        </button>
      ),
    },
  ]

  const savePlan = async () => {
    if (!editingPlan.name.trim()) {
      toast.error('Plan name is required.')
      return
    }
    try {
      const payload = {
        name: editingPlan.name.trim(),
        code: editingPlan.code.trim() || null,
        monthlyPrice: editingPlan.monthlyPrice,
        annualPrice: editingPlan.annualPrice,
        maxUsers: editingPlan.maxUsers,
        maxBranches: editingPlan.maxBranches,
        maxAssets: editingPlan.maxAssets,
        maxWorkOrders: editingPlan.maxWorkOrders,
        modulesIncluded: editingPlan.modulesIncluded.split(',').map((item) => item.trim()).filter(Boolean),
        isActive: editingPlan.isActive,
      }
      if (editingPlan.id) {
        await api.put(endpoints.platformLicensing.plans + `/${editingPlan.id}`, payload)
      } else {
        await api.post(endpoints.platformLicensing.plans, payload)
      }
      toast.success('License plan saved')
      setShowPlan(false)
      setEditingPlan(emptyPlan)
    } catch {
      // handled by interceptor
    }
  }

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
        primaryAction={<button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => { setShowPlan(true); setEditingPlan(emptyPlan) }}><Plus className="h-4 w-4" />New plan</button>}
      />

      <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Kpi label="Usage snapshots" value={usageRows.length} />
        <Kpi label="Plans" value={plansRows.length} />
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
                { key: 'maxUsers', header: 'Max users', cell: (row) => row.maxUsers ?? 'Unlimited' },
                { key: 'maxBranches', header: 'Max branches', cell: (row) => row.maxBranches ?? 'Unlimited' },
                { key: 'maxAssets', header: 'Max assets', cell: (row) => row.maxAssets ?? 'Unlimited' },
                { key: 'monthlyWorkOrders', header: 'Work orders/mo', cell: (row) => row.monthlyWorkOrders ?? 'Unlimited' },
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

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="License plans" description="Plan pricing and entitlements." />
            <DataTable<PlatformLicensePlan> columns={planColumns} data={plansRows} emptyTitle="No plans" emptyDescription="Create a license plan to start onboarding tenants." />
          </section>
        </div>
      ) : null}

      <FormDrawer
        open={showPlan || editingPlan.id !== null || editingPlan.name !== ''}
        title={editingPlan.id ? `Edit ${editingPlan.name}` : 'New license plan'}
        onClose={() => { setShowPlan(false); setEditingPlan(emptyPlan) }}
        footer={<div className="flex justify-end gap-2"><button type="button" className="rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => { setShowPlan(false); setEditingPlan(emptyPlan) }}>Cancel</button><button type="button" className="rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-medium text-white hover:bg-slate-800" onClick={() => void savePlan()}>Save</button></div>}
      >
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Field label="Name"><input value={editingPlan.name} onChange={(event) => setEditingPlan((current) => ({ ...current, name: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Code"><input value={editingPlan.code} onChange={(event) => setEditingPlan((current) => ({ ...current, code: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Monthly price"><input type="number" value={editingPlan.monthlyPrice} onChange={(event) => setEditingPlan((current) => ({ ...current, monthlyPrice: Number(event.target.value) || 0 }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Annual price"><input type="number" value={editingPlan.annualPrice} onChange={(event) => setEditingPlan((current) => ({ ...current, annualPrice: Number(event.target.value) || 0 }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Max branches"><input type="number" value={editingPlan.maxBranches ?? ''} onChange={(event) => setEditingPlan((current) => ({ ...current, maxBranches: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Max users"><input type="number" value={editingPlan.maxUsers ?? ''} onChange={(event) => setEditingPlan((current) => ({ ...current, maxUsers: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Max assets"><input type="number" value={editingPlan.maxAssets ?? ''} onChange={(event) => setEditingPlan((current) => ({ ...current, maxAssets: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Max work orders"><input type="number" value={editingPlan.maxWorkOrders ?? ''} onChange={(event) => setEditingPlan((current) => ({ ...current, maxWorkOrders: event.target.value ? Number(event.target.value) : null }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Modules included"><input value={editingPlan.modulesIncluded} onChange={(event) => setEditingPlan((current) => ({ ...current, modulesIncluded: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Active"><input type="checkbox" checked={editingPlan.isActive} onChange={(event) => setEditingPlan((current) => ({ ...current, isActive: event.target.checked }))} className="h-4 w-4 rounded border-slate-300" /></Field>
        </div>
      </FormDrawer>

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
