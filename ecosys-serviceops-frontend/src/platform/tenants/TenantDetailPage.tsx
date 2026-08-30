import { useEffect, useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { Send, Trash2, UserCog, UserPlus } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import {
  useActivateTenant,
  useDeleteTenant,
  useExtendTenantTrial,
  useImpersonateTenant,
  useSuspendTenant,
  useTenantAuditLogs,
  useTenantBranding,
  useTenantCommunicationSettings,
  useTenantDetail,
  useTenantDocumentDesigns,
  useTenantFinanceSummary,
  useTenantModuleSettings,
  useTenantNumbering,
  useTenantUsers,
  useResendTenantUserInvitation,
  useUpdateTenantCommunicationSettings,
  useDeactivateTenant,
} from '../api'
import { Field, SectionTitle, licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from '../common'
import { NewWorkspaceUserDrawer } from './NewWorkspaceUserDrawer'
import type { PlatformTenantAuditLog, TenantCommunicationSettings } from '../types'
import { StatusBadge } from '../../shared/components/StatusBadge'
import type { PlatformTenantNumbering, PlatformTenantUser } from '../api'

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
  const { data: tenant, isLoading, isError, refetch: refetchTenant } = useTenantDetail(id)
  const { data: auditLogs } = useTenantAuditLogs(id)
  const { data: branding, isLoading: brandingLoading } = useTenantBranding(id)
  const { data: tenantUsers, isLoading: tenantUsersLoading } = useTenantUsers(id)
  const { data: numbering, isLoading: numberingLoading, refetch: refetchNumbering } = useTenantNumbering(id)
  const { data: documentDesigns, isLoading: documentDesignsLoading } = useTenantDocumentDesigns(id)
  const { data: financeSummary, isLoading: financeSummaryLoading } = useTenantFinanceSummary(id)
  const { data: moduleSettings, isLoading: moduleSettingsLoading } = useTenantModuleSettings(id)
  const { data: communication } = useTenantCommunicationSettings(id)
  const updateCommunication = useUpdateTenantCommunicationSettings(id ?? '')
  const activate = useActivateTenant(id ?? '')
  const suspend = useSuspendTenant(id ?? '')
  const deactivate = useDeactivateTenant(id ?? '')
  const extendTrial = useExtendTenantTrial(id ?? '')
  const deleteTenant = useDeleteTenant()
  const impersonate = useImpersonateTenant(id ?? '')
  const resendInvitation = useResendTenantUserInvitation(id)
  const [showAddWorkspaceUser, setShowAddWorkspaceUser] = useState(false)

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
    return <EmptyState title="Tenant unavailable" description="This tenant could not be loaded." {...(id ? { actionLabel: 'Retry', onAction: () => { void refetchTenant() } } : {})} />
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
      await api.post(`${endpoints.platform.tenant(tenant.tenantId)}/email-settings/test`, {
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
      await api.post(`${endpoints.platform.tenant(tenant.tenantId)}/email-settings/verify`)
      toast.success('SMTP verification completed')
    } catch {
      // handled by interceptor
    }
  }

  const resetCommunication = async () => {
    if (!tenant.tenantId) return
    try {
      const result = await api.post<TenantCommunicationSettings>(`${endpoints.platform.tenant(tenant.tenantId)}/email-settings/reset-to-defaults`)
      setCommunicationForm(result)
      toast.success('Communication settings reset to defaults')
    } catch {
      // handled by interceptor
    }
  }

  const handleResendInvitation = async (user: PlatformTenantUser) => {
    try {
      const result = await resendInvitation.mutateAsync(user.id)
      if (result.success) {
        toast.success(result.message)
      } else {
        toast.error(result.message)
      }
    } catch {
      // handled by interceptor
    }
  }

  const tenantUserColumns: DataTableColumn<PlatformTenantUser>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'role', header: 'Role', cell: (row) => row.role },
    { key: 'jobTitle', header: 'Job Title', cell: (row) => row.jobTitle ?? '—' },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    {
      key: 'access',
      header: 'Access',
      // A never-signed-in account is the case this table exists to rescue: its setup link may have
      // lapsed, and the user cannot request a new one without being able to log in.
      cell: (row) => (row.lastLoginAt ? <span className="text-slate-600">Signed in {formatDateTime(row.lastLoginAt)}</span> : <span className="font-medium text-amber-700">Never signed in</span>),
    },
    {
      key: 'lastCredentialSentAt',
      header: 'Setup link sent',
      cell: (row) => (row.lastCredentialSentAt ? formatDateTime(row.lastCredentialSentAt) : '—'),
    },
    { key: 'createdAt', header: 'Joined', cell: (row) => formatDateTime(row.createdAt) },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <button
          type="button"
          onClick={() => void handleResendInvitation(row)}
          disabled={!row.isActive || resendInvitation.isPending}
          className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 transition hover:border-[var(--app-primary)] hover:text-[var(--app-primary)] disabled:cursor-not-allowed disabled:opacity-50"
          title={row.isActive ? 'Email this user a fresh account setup link' : 'Reactivate the account to resend its setup link'}
        >
          <Send className="h-3.5 w-3.5" />
          {resendInvitation.isPending && resendInvitation.variables === row.id ? 'Sending…' : 'Resend setup link'}
        </button>
      ),
    },
  ]

  const resetNumbering = async (documentType: string) => {
    if (!tenant) return
    try {
      await api.post(`/api/platform/support/tenants/${tenant.tenantId}/reset-numbering`, { documentType })
      toast.success(`${documentType} numbering reset`)
      void refetchNumbering()
    } catch {
      // handled by interceptor
    }
  }

  const numberingColumns: DataTableColumn<PlatformTenantNumbering>[] = [
    { key: 'documentType', header: 'Document Type', cell: (row) => <span className="font-medium text-slate-900">{row.documentType}</span> },
    { key: 'format', header: 'Format', cell: (row) => <span className="font-mono text-xs">{row.prefix}{row.suffix ?? ''}-{'0'.repeat(row.paddingLength)}</span> },
    { key: 'nextNumber', header: 'Next Number', cell: (row) => row.nextNumber },
    { key: 'resetFrequency', header: 'Reset Frequency', cell: (row) => row.resetFrequency },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    { key: 'lastResetAt', header: 'Last Reset', cell: (row) => (row.lastResetAt ? formatDateTime(row.lastResetAt) : 'Never') },
    {
      key: 'actions',
      header: '',
      cell: (row) => (
        <button type="button" className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => void resetNumbering(row.documentType)}>
          Reset to 1
        </button>
      ),
    },
  ]

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

          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
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
      content: brandingLoading ? (
        <Spinner label="Loading branding" />
      ) : branding ? (
        <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
          <SectionTitle title="Tenant branding" description="Read-only view. Branding is edited from within the tenant's own workspace settings." />
          <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
            <InfoCard label="Company name" value={branding.companyName} />
            <InfoCard label="Show 'Powered by Ecosys'" value={branding.showPoweredByEcosys ? 'Yes' : 'No'} />
            <InfoCard
              label="Primary color"
              value={<span className="inline-flex items-center gap-2"><span className="h-4 w-4 rounded-full border border-slate-200" style={{ backgroundColor: branding.primaryColor }} />{branding.primaryColor}</span>}
            />
            <InfoCard
              label="Secondary color"
              value={<span className="inline-flex items-center gap-2"><span className="h-4 w-4 rounded-full border border-slate-200" style={{ backgroundColor: branding.secondaryColor }} />{branding.secondaryColor}</span>}
            />
          </div>
          {branding.logoUrl ? (
            <div className="mt-4">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Logo</div>
              <img src={branding.logoUrl} alt={`${branding.companyName} logo`} className="mt-2 h-16 rounded-lg border border-slate-200 bg-slate-50 object-contain p-2" />
            </div>
          ) : null}
        </section>
      ) : (
        <EmptyState title="Branding unavailable" description="Could not load this tenant's branding settings." />
      ),
    },
    {
      id: 'users',
      label: 'Users',
      content: (
        <div className="space-y-5">
          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <SectionTitle title="Workspace users" description="Cross-tenant user listing is not exposed yet. We can safely show the tenant-side account summary we do have." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
              <InfoCard label="Tenant users" value={tenant.userCount} />
              <InfoCard label="Must change password" value={tenant.initialAdminInvitationSent ? 'First admin invited' : 'Check status'} />
              <InfoCard label="Initial admin invite" value={tenant.initialAdminInvitationSent ? 'Queued or sent' : 'Not sent'} />
              <InfoCard label="Initial admin note" value={tenant.initialAdminInvitationMessage ?? '—'} />
            </div>
          </section>
          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <SectionTitle title="Workspace user list" description="Accounts inside this tenant. Add another admin here when the workspace needs more than the one seeded at creation, or resend a lapsed setup link — the only recovery path for an admin who has never signed in. Ecosys staff accounts live under Platform → Users." />
              <button
                type="button"
                onClick={() => setShowAddWorkspaceUser(true)}
                className="inline-flex h-10 shrink-0 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
              >
                <UserPlus className="h-4 w-4" />
                Add workspace user
              </button>
            </div>
            <div className="mt-4">
              <DataTable<PlatformTenantUser>
                columns={tenantUserColumns}
                data={tenantUsers ?? []}
                loading={tenantUsersLoading}
                emptyTitle="No users yet"
                emptyDescription="This tenant has no workspace users."
              />
            </div>
          </section>
        </div>
      ),
    },
    {
      id: 'modules',
      label: 'Modules',
      content: (
        <div className="space-y-5">
          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <SectionTitle title="Configured module settings" description="Module-level configuration this tenant has saved (executive dashboard, inventory, payments, portal, integrations, etc). Full module enable/disable entitlement is governed by licensing, not a per-module toggle." />
            <div className="mt-4">
              {moduleSettingsLoading ? (
                <Spinner label="Loading module settings" />
              ) : (moduleSettings ?? []).length === 0 ? (
                <EmptyState title="No module settings configured" description="This tenant has not customized any module-level settings yet." />
              ) : (
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                  {(moduleSettings ?? []).map((setting) => (
                    <InfoCard key={setting.moduleKey} label={setting.moduleKey} value={formatDateTime(setting.updatedAt)} />
                  ))}
                </div>
              )}
            </div>
          </section>
          <UnsupportedTab
            title="Licensing & entitlement"
            description="Max users, max branches, and plan-level feature gates are managed from Subscription / Licensing."
            linkLabel="Open licensing"
            onAction={() => navigate('/platform/licensing')}
          />
        </div>
      ),
    },
    {
      id: 'licensing',
      label: 'Subscription / Licensing',
      content: (
        <div className="space-y-5">
          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
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
            {tenant.status !== 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => void activate.mutateAsync(undefined)}>Activate</button> : null}
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
          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
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
                <input value={communicationForm.emailSettings.smtpHost ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpHost: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="SMTP port">
                <input type="number" value={communicationForm.emailSettings.smtpPort ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpPort: event.target.value ? Number(event.target.value) : null } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="SMTP username">
                <input value={communicationForm.emailSettings.smtpUsername ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpUsername: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="SMTP password">
                <input type="password" placeholder={communicationForm.emailSettings.smtpPasswordMasked ?? 'Leave blank to keep existing'} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, smtpPasswordMasked: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="Sender name">
                <input value={communicationForm.emailSettings.senderName ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, senderName: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="Sender email">
                <input value={communicationForm.emailSettings.senderEmail ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, senderEmail: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
              </Field>
              <Field label="Reply-to email">
                <input value={communicationForm.emailSettings.replyToEmail ?? ''} onChange={(event) => setCommunicationForm((current) => current ? { ...current, emailSettings: { ...current.emailSettings, replyToEmail: event.target.value } } : current)} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-[var(--app-primary)]" />
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
              <button type="button" className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => void saveCommunication()}>
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

          <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
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
      content: (
        <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
          <SectionTitle title="Document numbering" description="Tenant-wide numbering sequences. Reset uses the same support tool available from Platform Support." />
          <div className="mt-4">
            <DataTable<PlatformTenantNumbering>
              columns={numberingColumns}
              data={numbering ?? []}
              loading={numberingLoading}
              emptyTitle="No numbering rules configured"
              emptyDescription="This tenant has not set up any document numbering sequences yet."
            />
          </div>
        </section>
      ),
    },
    {
      id: 'templates',
      label: 'Templates',
      content: (
        <section className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
          <SectionTitle title="Document designs" description="Which document types this tenant has customized via the Document Designer. Editing happens from within the tenant's own workspace." />
          <div className="mt-4">
            {documentDesignsLoading ? (
              <Spinner label="Loading document designs" />
            ) : (documentDesigns ?? []).length === 0 ? (
              <EmptyState title="No customized document designs" description="This tenant is using the default document design for every document type." />
            ) : (
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                {(documentDesigns ?? []).map((entry) => (
                  <InfoCard key={entry.documentType} label={entry.documentType} value="Customized" />
                ))}
              </div>
            )}
          </div>
        </section>
      ),
    },
    {
      id: 'finance',
      label: 'Finance',
      content: financeSummaryLoading ? (
        <Spinner label="Loading finance summary" />
      ) : financeSummary ? (
        <section className="space-y-5">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
            <MiniStat label="Invoices" value={financeSummary.invoiceCount} />
            <MiniStat label="Overdue" value={financeSummary.overdueCount} />
            <InfoCard label="Total invoiced" value={formatCurrency(financeSummary.totalInvoiced)} />
            <InfoCard label="Outstanding balance" value={formatCurrency(financeSummary.outstandingBalance)} />
          </div>
          <UnsupportedTab
            title="Detailed invoices"
            description="Tenant-level invoices and billing live in the tenant workspace. The platform finance tab aggregates platform-wide records only."
            linkLabel="Open platform finance"
            onAction={() => navigate('/platform/finance')}
          />
        </section>
      ) : (
        <EmptyState title="Finance summary unavailable" description="Could not load this tenant's finance summary." />
      ),
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
          <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-5 shadow-sm">
            <SectionTitle title="Account management" description="Lifecycle controls that are safe to expose from the platform owner workspace." />
            <div className="mt-4 flex flex-wrap gap-2">
              {tenant.status !== 'Active' ? <button type="button" className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)]" onClick={() => void activate.mutateAsync(undefined)}>Activate tenant</button> : null}
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

      <NewWorkspaceUserDrawer
        open={showAddWorkspaceUser}
        tenantId={id}
        tenantName={tenant.name}
        onClose={() => setShowAddWorkspaceUser(false)}
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
    <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
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
