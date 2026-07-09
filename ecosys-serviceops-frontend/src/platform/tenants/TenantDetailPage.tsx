import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { Trash2, UserCog } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { formatDateTime } from '../../shared/utils/dates'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import {
  useActivateTenant,
  useDeleteTenant,
  useExtendTenantTrial,
  useImpersonateTenant,
  useSuspendTenant,
  useTenantAuditLogs,
  useTenantCommunicationSettings,
  useTenantDetail,
  useUpdateTenantCommunicationSettings,
  useDeactivateTenant,
} from '../api'
import { Field, SectionTitle, licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from '../common'
import type { PlatformTenantAuditLog, TenantCommunicationSettings } from '../types'

type CommunicationForm = TenantCommunicationSettings

const communicationGroups = [
  'Admin',
  'Operations',
  'SLAEscalation',
  'Dispatch',
  'Maintenance',
  'Assets',
  'Materials',
  'SystemAlerts',
] as const

export function TenantDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: tenant, isLoading, isError } = useTenantDetail(id)
  const { data: auditLogs } = useTenantAuditLogs(id)
  const { data: communication } = useTenantCommunicationSettings(id)
  const updateCommunication = useUpdateTenantCommunicationSettings(id ?? '')
  const activate = useActivateTenant(id ?? '')
  const suspend = useSuspendTenant(id ?? '')
  const deactivate = useDeactivateTenant(id ?? '')
  const extendTrial = useExtendTenantTrial(id ?? '')
  const deleteTenant = useDeleteTenant()
  const impersonate = useImpersonateTenant(id ?? '')

  const [showDelete, setShowDelete] = useState(false)
  const [showSuspend, setShowSuspend] = useState(false)
  const [showDeactivate, setShowDeactivate] = useState(false)
  const [showExtend, setShowExtend] = useState(false)
  const [communicationForm, setCommunicationForm] = useState<CommunicationForm | null>(null)
  const [testRecipient, setTestRecipient] = useState('')

  useEffect(() => {
    if (communication) {
      setCommunicationForm(communication)
    }
  }, [communication])

  if (isLoading) {
    return <Spinner label="Loading tenant" />
  }

  if (isError || !tenant || !id) {
    return <EmptyState title="Tenant unavailable" description="This tenant could not be loaded." />
  }

  const auditColumns: DataTableColumn<PlatformTenantAuditLog>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actor', header: 'Actor', cell: (row) => row.actor },
    { key: 'action', header: 'Action', cell: (row) => row.action },
    { key: 'details', header: 'Details', cell: (row) => row.details ?? '—' },
  ]

  const saveCommunication = async () => {
    if (!communicationForm) return
    try {
      await updateCommunication.mutateAsync(communicationForm)
      toast.success('Communication settings saved')
    } catch {
      // handled by interceptor
    }
  }

  const sendTestEmail = async () => {
    if (!tenant.tenantId) return
    try {
      await api.post(`${endpoints.platform.tenant(tenant.tenantId)}/communication-settings/email-settings/test`, {
        testRecipientEmail: testRecipient.trim() || null,
      })
      toast.success('Test email queued')
    } catch {
      // handled by interceptor
    }
  }

  const verifySmtp = async () => {
    if (!tenant.tenantId) return
    try {
      await api.post(`${endpoints.platform.tenant(tenant.tenantId)}/communication-settings/email-settings/verify`)
      toast.success('SMTP verification completed')
    } catch {
      // handled by interceptor
    }
  }

  const resetCommunication = async () => {
    if (!tenant.tenantId) return
    try {
      const result = await api.post<TenantCommunicationSettings>(`${endpoints.platform.tenant(tenant.tenantId)}/communication-settings/email-settings/reset-to-defaults`)
      setCommunicationForm(result)
      toast.success('Communication settings reset to defaults')
    } catch {
      // handled by interceptor
    }
  }

  const tabs = [
    {
      id: 'overview',
      label: 'Overview',
      content: (
        <div className="space-y-5">
          <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
            <MiniStat label="Users" value={tenant.userCount} />
            <MiniStat label="Sites" value={tenant.branchCount} />
            <MiniStat label="Work orders" value={tenant.workOrderCount} />
            <MiniStat label="Active now" value={tenant.activeUsersNow} />
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Tenant profile" description="Workspace details and lifecycle summary." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              <InfoCard label="Tenant name" value={tenant.name} />
              <InfoCard label="Workspace slug" value={tenant.slug} mono />
              <InfoCard label="Status" value={tenantStatusBadge(tenant.status)} />
              <InfoCard label="Plan" value={tenant.planName ?? 'No plan'} />
              <InfoCard label="License" value={licenseStatusBadge(tenant.licenseStatus)} />
              <InfoCard label="Trial" value={trialStatusBadge(tenant.trialStatus)} />
              <InfoCard label="Primary contact" value={tenant.contactName ?? 'Not set'} />
              <InfoCard label="Contact email" value={tenant.contactEmail ?? 'Not set'} />
              <InfoCard label="Contact phone" value={tenant.contactPhone ?? 'Not set'} />
              <InfoCard label="Country" value={tenant.country ?? 'Not set'} />
              <InfoCard label="County" value={tenant.county ?? 'Not set'} />
              <InfoCard label="City" value={tenant.city ?? 'Not set'} />
              <InfoCard label="Trial ends" value={tenant.trialEndsAt ? formatDateTime(tenant.trialEndsAt) : 'Not set'} />
              <InfoCard label="Days remaining" value={tenant.trialDaysRemaining != null ? String(tenant.trialDaysRemaining) : '—'} />
              <InfoCard label="Subscription ends" value={tenant.subscriptionEndsAt ? formatDateTime(tenant.subscriptionEndsAt) : 'Not set'} />
              <InfoCard label="Created" value={formatDateTime(tenant.createdAt)} />
              <InfoCard label="Last activity" value={tenant.lastActivityAt ? formatDateTime(tenant.lastActivityAt) : 'Not set'} />
            </div>
          </section>
        </div>
      ),
    },
    {
      id: 'branding',
      label: 'Branding',
      content: (
          <UnsupportedTab
          title="Branding"
          description="Tenant branding is not currently exposed by a platform-scoped API. This should be added before we surface editable branding controls here."
          linkLabel="Open platform branding settings"
          onAction={() => navigate('/platform/settings/branding')}
        />
      ),
    },
    {
      id: 'users',
      label: 'Users',
      content: (
        <div className="space-y-5">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Workspace users" description="Cross-tenant user listing is not exposed yet. We can safely show the tenant-side account summary we do have." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
              <InfoCard label="Tenant users" value={tenant.userCount} />
              <InfoCard label="Must change password" value={tenant.initialAdminInvitationSent ? 'First admin invited' : 'Check status'} />
              <InfoCard label="Initial admin invite" value={tenant.initialAdminInvitationSent ? 'Queued or sent' : 'Not sent'} />
              <InfoCard label="Initial admin note" value={tenant.initialAdminInvitationMessage ?? '—'} />
            </div>
          </section>
          <UnsupportedTab
            title="Tenant admin list"
            description="A platform-scoped endpoint for listing another tenant's users is not available in the current backend snapshot. Use impersonation or tenant workspace access for detailed user administration."
            linkLabel="Open platform users"
            onAction={() => navigate('/platform/users')}
          />
        </div>
      ),
    },
    {
      id: 'modules',
      label: 'Modules',
      content: (
        <UnsupportedTab
          title="Module access"
          description="Cross-tenant module toggles are not exposed by the current backend. Module entitlement is governed from licensing and tenant settings."
          linkLabel="Open licensing"
          onAction={() => navigate('/platform/licensing')}
        />
      ),
    },
    {
      id: 'licensing',
      label: 'Subscription / Licensing',
      content: (
        <div className="space-y-5">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Subscription state" description="Lifecycle controls available from the platform owner workspace." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              <InfoCard label="License status" value={licenseStatusBadge(tenant.licenseStatus)} />
              <InfoCard label="Trial status" value={trialStatusBadge(tenant.trialStatus)} />
              <InfoCard label="Max users" value={tenant.maxUsers != null ? String(tenant.maxUsers) : 'Unlimited'} />
              <InfoCard label="Max branches" value={tenant.maxBranches != null ? String(tenant.maxBranches) : 'Unlimited'} />
              <InfoCard label="Trial started" value={tenant.trialStartsAt ? formatDateTime(tenant.trialStartsAt) : 'Not set'} />
              <InfoCard label="Trial extended" value={tenant.trialExtendedAt ? formatDateTime(tenant.trialExtendedAt) : 'Not used'} />
            </div>
          </section>
          <div className="flex flex-wrap gap-2">
            {tenant.status !== 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => void activate.mutateAsync(undefined)}>Activate</button> : null}
            {tenant.status === 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-amber-200 bg-amber-50 px-4 text-sm font-medium text-amber-800 hover:bg-amber-100" onClick={() => setShowSuspend(true)}>Suspend</button> : null}
            {!tenant.trialExtensionUsed ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => setShowExtend(true)}>Extend trial</button> : null}
            {tenant.status !== 'Inactive' ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-rose-50 px-4 text-sm font-medium text-rose-700 hover:bg-rose-100" onClick={() => setShowDeactivate(true)}>Deactivate</button> : null}
          </div>
        </div>
      ),
    },
    {
      id: 'communication',
      label: 'Communication',
      content: communicationForm ? (
        <div className="space-y-5">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Email settings" description="Tenant communication settings with platform defaults and explicit overrides." />
            <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
              <Field label="Use platform defaults">
                <input
                  type="checkbox"
                  checked={communicationForm.emailSettings.usePlatformDefaults}
                  onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, usePlatformDefaults: event.target.checked } } : current)}
                  className="h-4 w-4 rounded border-slate-300"
                />
              </Field>
              <Field label="Override SMTP settings">
                <input
                  type="checkbox"
                  checked={communicationForm.emailSettings.overrideSmtpSettings}
                  onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, overrideSmtpSettings: event.target.checked } } : current)}
                  className="h-4 w-4 rounded border-slate-300"
                />
              </Field>
              <Field label="SMTP host">
                <input value={communicationForm.emailSettings.smtpHost ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpHost: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="SMTP port">
                <input type="number" value={communicationForm.emailSettings.smtpPort ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpPort: event.target.value ? Number(event.target.value) : null } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="SMTP username">
                <input value={communicationForm.emailSettings.smtpUsername ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpUsername: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="SMTP password">
                <input type="password" placeholder={communicationForm.emailSettings.smtpPasswordMasked ?? 'Leave blank to keep existing'} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpPasswordMasked: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="Sender name">
                <input value={communicationForm.emailSettings.senderName ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, senderName: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="Sender email">
                <input value={communicationForm.emailSettings.senderEmail ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, senderEmail: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="Reply-to email">
                <input value={communicationForm.emailSettings.replyToEmail ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, replyToEmail: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" />
              </Field>
              <Field label="Enable tenant notifications">
                <input
                  type="checkbox"
                  checked={communicationForm.emailSettings.enableTenantEmailNotifications}
                  onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, enableTenantEmailNotifications: event.target.checked } } : current)}
                  className="h-4 w-4 rounded border-slate-300"
                />
              </Field>
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => void saveCommunication()}>
                Save communication
              </button>
              <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => void sendTestEmail()}>
                Send test email
              </button>
              <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => void verifySmtp()}>
                Verify SMTP
              </button>
              <button type="button" className="inline-flex h-11 items-center rounded-lg border border-amber-200 bg-amber-50 px-4 text-sm font-medium text-amber-800 hover:bg-amber-100" onClick={() => void resetCommunication()}>
                Reset defaults
              </button>
              <input value={testRecipient} onChange={(event) => setTestRecipient(event.target.value)} placeholder="Test recipient email" className="h-11 rounded-lg border border-slate-200 px-3 text-sm outline-none" />
            </div>
          </section>

          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Notification groups" description="Default recipient groups and switches." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2">
              {communicationGroups.map((group) => (
                <div key={group} className="rounded-xl border border-slate-200 px-4 py-3">
                  <p className="font-medium text-slate-900">{group}</p>
                  <p className="mt-1 text-sm text-slate-600">Managed by tenant notification settings.</p>
                </div>
              ))}
            </div>
          </section>
        </div>
      ) : (
        <Spinner label="Loading communication settings" />
      ),
    },
    {
      id: 'numbering',
      label: 'Numbering',
      content: <UnsupportedTab title="Numbering" description="Tenant numbering settings are backend-scoped elsewhere and are not yet available in a platform-scoped tenant detail API." linkLabel="Open numbering defaults" onAction={() => navigate('/platform/settings')} />,
    },
    {
      id: 'templates',
      label: 'Templates',
      content: <UnsupportedTab title="Templates" description="Tenant template editing is not currently exposed through the platform owner API. We can surface platform templates from settings instead." linkLabel="Open template settings" onAction={() => navigate('/platform/settings/templates')} />,
    },
    {
      id: 'finance',
      label: 'Finance',
      content: <UnsupportedTab title="Finance" description="Tenant-level invoices and billing live in the tenant workspace. The platform finance tab aggregates platform-wide records only." linkLabel="Open platform finance" onAction={() => navigate('/platform/finance')} />,
    },
    {
      id: 'audit',
      label: 'Audit Trail',
      content: (
        <DataTable<PlatformTenantAuditLog>
          columns={auditColumns}
          data={auditLogs ?? []}
          emptyTitle="No audit events"
          emptyDescription="Tenant audit entries will appear here."
        />
      ),
    },
    {
      id: 'account',
      label: 'Account Management',
      content: (
        <section className="space-y-4">
          <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <SectionTitle title="Account management" description="Lifecycle controls that are safe to expose from the platform owner workspace." />
            <div className="mt-4 flex flex-wrap gap-2">
              {tenant.status !== 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => void activate.mutateAsync(undefined)}>Activate tenant</button> : null}
              {tenant.status === 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-amber-200 bg-amber-50 px-4 text-sm font-medium text-amber-800 hover:bg-amber-100" onClick={() => setShowSuspend(true)}>Suspend tenant</button> : null}
              {tenant.status !== 'Inactive' ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-rose-200 bg-rose-50 px-4 text-sm font-medium text-rose-700 hover:bg-rose-100" onClick={() => setShowDeactivate(true)}>Deactivate tenant</button> : null}
              {!tenant.trialExtensionUsed ? <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => setShowExtend(true)}>Extend trial</button> : null}
              <button type="button" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => void impersonate.mutateAsync(undefined).then(() => toast.success('Impersonation session started.'))}>
                <UserCog className="h-4 w-4" />
                Impersonate admin
              </button>
            </div>
          </div>

          <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5">
            <p className="text-sm font-semibold text-rose-900">Delete tenant</p>
            <p className="mt-2 text-sm text-rose-800">Deletion is permanent and only shown here for platform owners. It requires explicit confirmation text.</p>
            <button type="button" className="mt-4 inline-flex h-11 items-center rounded-lg bg-rose-600 px-4 text-sm font-medium text-white hover:bg-rose-700" onClick={() => setShowDelete(true)}>
              <Trash2 className="h-4 w-4" />
              Delete tenant
            </button>
          </div>
        </section>
      ),
    },
  ] as const

  if (!id) {
    return <Navigate to="/platform/tenants" replace />
  }

  return (
    <>
      <DetailLayout
        header={
          <PageHeader
            title={tenant.name}
            subtitle={`${tenant.slug} · ${tenant.contactEmail ?? 'No contact email'}`}
            breadcrumbs={[{ label: 'Tenants', to: '/platform/tenants' }, { label: tenant.name }]}
            secondaryActions={<div className="flex items-center gap-2">{tenantStatusBadge(tenant.status)}{licenseStatusBadge(tenant.licenseStatus)}</div>}
            primaryAction={
              <Link to="/platform/tenants" className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">
                Back to tenants
              </Link>
            }
          />
        }
        tabs={tabs.map((tab) => ({ id: tab.id, label: tab.label, content: tab.content }))}
        defaultTabId="overview"
      />

      <ConfirmDialog
        open={showSuspend}
        title="Suspend tenant"
        message={`Suspend ${tenant.name}? This blocks access until reactivated.`}
        confirmLabel="Suspend"
        danger
        onCancel={() => setShowSuspend(false)}
        onConfirm={() => {
          void suspend.mutateAsync('Suspended by platform owner')
            .then(() => toast.success('Tenant suspended'))
            .finally(() => setShowSuspend(false))
        }}
      />

      <ConfirmDialog
        open={showDeactivate}
        title="Deactivate tenant"
        message={`Deactivate ${tenant.name}? The workspace will be taken offline.`}
        confirmLabel="Deactivate"
        confirmText={tenant.slug}
        danger
        onCancel={() => setShowDeactivate(false)}
        onConfirm={() => {
          void deactivate.mutateAsync('Deactivated by platform owner')
            .then(() => toast.success('Tenant deactivated'))
            .finally(() => setShowDeactivate(false))
        }}
      />

      <ConfirmDialog
        open={showExtend}
        title="Extend trial"
        message={`Extend the ${tenant.name} trial by 14 days? This can only be done once.`}
        confirmLabel="Extend"
        onCancel={() => setShowExtend(false)}
        onConfirm={() => {
          void extendTrial.mutateAsync(undefined)
            .then(() => toast.success('Trial extended'))
            .finally(() => setShowExtend(false))
        }}
      />

      <ConfirmDialog
        open={showDelete}
        title="Delete tenant"
        message={`This permanently deletes ${tenant.name}. This action cannot be undone.`}
        confirmLabel="Delete"
        confirmText={tenant.slug}
        danger
        onCancel={() => setShowDelete(false)}
        onConfirm={() => {
          void deleteTenant.mutateAsync(tenant.tenantId)
            .then(() => {
              toast.success('Tenant deleted')
              navigate('/platform/tenants')
            })
            .finally(() => setShowDelete(false))
        }}
      />
    </>
  )
}

function MiniStat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className="mt-2 text-2xl font-semibold text-slate-900">{value}</div>
    </div>
  )
}

function InfoCard({ label, value, mono = false }: { label: string; value: React.ReactNode; mono?: boolean }) {
  return (
    <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <div className={`mt-2 text-sm text-slate-900 ${mono ? 'font-mono' : ''}`}>{value}</div>
    </div>
  )
}

function UnsupportedTab({
  title,
  description,
  linkLabel,
  onAction,
}: {
  title: string
  description: string
  linkLabel: string
  onAction: () => void
}) {
  return (
    <EmptyState
      title={title}
      description={description}
      actionLabel={linkLabel}
      onAction={onAction}
    />
  )
}
