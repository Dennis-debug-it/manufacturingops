import { useState } from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router-dom'
import { KeyRound, Send, UserCog, UserPlus, Users } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import { openImpersonationTab } from '../../shared/api/sessionStore'
import {
  useActivateTenant,
  useRenewTenantLicense,
  useExtendTenantLicense,
  useReactivateTenantLicense,
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
  useResetTenantUserPassword,
  useDeactivateTenant,
  useUpdateTenantSeats,
} from '../api'
import { SectionTitle, licenseStatusBadge, tenantStatusBadge, trialStatusBadge } from '../common'
import { NewWorkspaceUserDrawer } from './NewWorkspaceUserDrawer'
import { LicenseTermDialog } from './LicenseTermDialog'
import { ClientTemplatesPanel } from './ClientTemplatesPanel'
import { TenantWhatsAppPanel } from './TenantWhatsAppPanel'
import { parseLicensedUsers, summarizeSeats } from './licensedUsers'
import type { PlatformTenantAuditLog } from '../types'
import { StatusBadge } from '../../shared/components/StatusBadge'
import type { PlatformTenantNumbering, PlatformTenantUser } from '../api'

const moduleLabels: Record<string, string> = {
  operations: 'Operations',
  finance: 'Finance',
}

const primaryButton = 'inline-flex h-11 items-center gap-1.5 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-50'
const secondaryButton = 'inline-flex h-11 items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50'
const warningButton = 'inline-flex h-11 items-center rounded-lg border border-amber-200 bg-amber-50 px-4 text-sm font-medium text-amber-800 hover:bg-amber-100'
const dangerButton = 'inline-flex h-11 items-center rounded-lg border border-rose-200 bg-rose-50 px-4 text-sm font-medium text-rose-700 hover:bg-rose-100'
const panelClass = 'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm'
const inputClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/** Seats are shown as "used / limit"; a null limit means the tenant predates required seat limits. */
function formatSeats(used: number, limit: number | null | undefined) {
  return limit != null ? `${used} / ${limit}` : `${used} / —`
}

const invitationLabels: Record<string, { label: string; className: string }> = {
  Active: { label: 'Set up', className: 'text-slate-600' },
  Invited: { label: 'Invitation sent', className: 'font-medium text-sky-700' },
  Expired: { label: 'Setup link expired', className: 'font-medium text-amber-700' },
  NotSent: { label: 'Invitation not delivered', className: 'font-medium text-rose-700' },
}

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
  const activate = useActivateTenant(id ?? '')
  const renewLicense = useRenewTenantLicense(id ?? '')
  const extendLicense = useExtendTenantLicense(id ?? '')
  const reactivateLicense = useReactivateTenantLicense(id ?? '')
  const suspend = useSuspendTenant(id ?? '')
  const deactivate = useDeactivateTenant(id ?? '')
  const extendTrial = useExtendTenantTrial(id ?? '')
  const impersonate = useImpersonateTenant(id ?? '')
  const updateSeats = useUpdateTenantSeats(id ?? '')
  const resendInvitation = useResendTenantUserInvitation(id)
  const resetPassword = useResetTenantUserPassword(id)
  const [showAddWorkspaceUser, setShowAddWorkspaceUser] = useState(false)

  const [showSuspend, setShowSuspend] = useState(false)
  const [suspendReason, setSuspendReason] = useState('')
  const [showDeactivate, setShowDeactivate] = useState(false)
  const [deactivateReason, setDeactivateReason] = useState('')
  const [showExtendTrial, setShowExtendTrial] = useState(false)
  const [showActivateDialog, setShowActivateDialog] = useState(false)
  const [showRenewDialog, setShowRenewDialog] = useState(false)
  const [showExtendLicenseDialog, setShowExtendLicenseDialog] = useState(false)
  const [extendLicenseTarget, setExtendLicenseTarget] = useState('')
  const [showSeatsDialog, setShowSeatsDialog] = useState(false)
  const [seatsInput, setSeatsInput] = useState('')
  const [showImpersonate, setShowImpersonate] = useState(false)
  const [impersonateReason, setImpersonateReason] = useState('')
  const [testRecipient, setTestRecipient] = useState('')
  const [showResetNumbering, setShowResetNumbering] = useState<{ documentType: string } | null>(null)
  const [resetNumberingReason, setResetNumberingReason] = useState('')

  if (isLoading) {
    return <Spinner label="Loading tenant" />
  }

  if (isError || !tenant || !id) {
    return <EmptyState title="Tenant unavailable" description="This tenant could not be loaded." {...(id ? { actionLabel: 'Retry', onAction: () => { void refetchTenant() } } : {})} />
  }

  const seatsUsed = tenant.seatsUsed ?? tenant.userCount
  const seatLimit = tenant.seatLimit ?? tenant.maxUsers
  const seats = summarizeSeats(seatsUsed, seatLimit)
  const seatsFull = seats.isFull
  const modules = tenant.modules ?? []
  const isTrial = tenant.status === 'Trial'
  const isSuspended = tenant.status === 'Suspended'
  const isInactive = tenant.status === 'Inactive'
  const isActive = tenant.status === 'Active'
  const licenseExpiresAt = tenant.licenseExpiresAt ?? tenant.subscriptionEndsAt

  const auditColumns: DataTableColumn<PlatformTenantAuditLog>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    { key: 'actor', header: 'Actor', cell: (row) => row.actor },
    { key: 'action', header: 'Action', cell: (row) => row.action },
    { key: 'details', header: 'Details', cell: (row) => row.details ?? '—' },
  ]

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

  const handleResetPassword = async (user: PlatformTenantUser) => {
    try {
      const result = await resetPassword.mutateAsync(user.id)
      if (result.success) {
        toast.success(result.message)
      } else {
        toast.error(result.message)
      }
    } catch {
      // handled by interceptor
    }
  }

  const startImpersonation = async () => {
    try {
      const result = await impersonate.mutateAsync(impersonateReason.trim())
      openImpersonationTab({
        token: result.token,
        user: result.user,
        tenant: result.tenant,
        branches: [],
        impersonation: {
          impersonatedByUserId: result.impersonatedByUserId,
          impersonatedByName: result.impersonatedByName,
          startedAt: result.startedAt,
        },
      })
      toast.success('Impersonation session opened in a new tab.')
      setShowImpersonate(false)
      setImpersonateReason('')
    } catch {
      // handled by interceptor
    }
  }

  const openSeatsDialog = () => {
    setSeatsInput(seatLimit != null ? String(seatLimit) : String(Math.max(seatsUsed, 1)))
    setShowSeatsDialog(true)
  }

  const seatsParse = parseLicensedUsers(seatsInput)
  const seatsValid = seatsParse.ok
  const parsedSeats = seatsParse.ok ? seatsParse.value : 0

  const tenantUserColumns: DataTableColumn<PlatformTenantUser>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <span className="font-medium text-slate-900">{row.fullName}</span> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'role', header: 'Role', cell: (row) => row.role },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} /> },
    {
      key: 'invitation',
      header: 'Account setup',
      cell: (row) => {
        const state = invitationLabels[row.invitationStatus ?? (row.mustChangePassword ? 'Invited' : 'Active')] ?? invitationLabels.Invited!
        return (
          <div>
            <span className={state.className}>{state.label}</span>
            {row.invitationStatus === 'Invited' && row.invitationExpiresAt ? (
              <div className="text-xs text-slate-500">Expires {formatDateTime(row.invitationExpiresAt)}</div>
            ) : null}
          </div>
        )
      },
    },
    {
      key: 'access',
      header: 'Last sign-in',
      cell: (row) => (row.lastLoginAt ? <span className="text-slate-600">{formatDateTime(row.lastLoginAt)}</span> : <span className="text-slate-500">Never</span>),
    },
    {
      key: 'actions',
      header: '',
      cell: (row) =>
        row.mustChangePassword ? (
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
        ) : (
          <button
            type="button"
            onClick={() => void handleResetPassword(row)}
            disabled={!row.isActive || resetPassword.isPending}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 transition hover:border-[var(--app-primary)] hover:text-[var(--app-primary)] disabled:cursor-not-allowed disabled:opacity-50"
            title={row.isActive ? 'Email this user a password reset link' : 'Reactivate the account to reset its password'}
          >
            <KeyRound className="h-3.5 w-3.5" />
            {resetPassword.isPending && resetPassword.variables === row.id ? 'Sending…' : 'Reset password'}
          </button>
        ),
    },
  ]

  const resetNumbering = async (documentType: string, reason: string) => {
    if (!tenant) return
    try {
      await api.post(`/api/platform/support/tenants/${tenant.tenantId}/reset-numbering`, { documentType, reason: reason.trim() || null })
      toast.success(`${documentType} numbering reset`)
      void refetchNumbering()
    } catch {
      // handled by interceptor
    } finally {
      setShowResetNumbering(null)
      setResetNumberingReason('')
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
        <button
          type="button"
          className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
          onClick={() => setShowResetNumbering({ documentType: row.documentType })}
        >
          Reset to 1
        </button>
      ),
    },
  ]

  const seatsPanel = (
    <section className={panelClass} data-testid="tenant-seats">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <SectionTitle
          title="Licensed users"
          description="Active workspace users, including invitees who have not signed in yet, use a seat. Deactivated users and client-portal contacts do not."
        />
        <button type="button" className={secondaryButton} onClick={openSeatsDialog}>
          <Users className="h-4 w-4" />
          Edit licensed users
        </button>
      </div>
      <dl className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3" data-testid="tenant-seats-licensed">
          <dt className="text-xs font-semibold uppercase tracking-wider text-slate-500">Licensed Users</dt>
          <dd className="mt-1 text-2xl font-semibold text-slate-900">{seats.licensed ?? '—'}</dd>
        </div>
        <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3" data-testid="tenant-seats-current">
          <dt className="text-xs font-semibold uppercase tracking-wider text-slate-500">Current Users</dt>
          <dd className="mt-1 text-2xl font-semibold text-slate-900">{seats.current}</dd>
        </div>
        <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3" data-testid="tenant-seats-remaining">
          <dt className="text-xs font-semibold uppercase tracking-wider text-slate-500">Remaining Seats</dt>
          <dd className={`mt-1 text-2xl font-semibold ${seats.isFull ? 'text-amber-700' : 'text-slate-900'}`}>{seats.remaining ?? '—'}</dd>
        </div>
      </dl>
      {seatLimit == null ? (
        <p className="mt-2 text-sm text-amber-700">This tenant has no license yet. Set licensed users to create one.</p>
      ) : seats.isOverLimit ? (
        <p className="mt-2 text-sm text-amber-700">{seats.current} active users exceed the licensed limit of {seats.licensed}. Nobody was deactivated; new users and reactivations are blocked until usage falls below the limit or it is raised.</p>
      ) : seatsFull ? (
        <p className="mt-2 text-sm text-amber-700">This tenant has reached its licensed user limit of {seats.licensed}. New users and reactivations are blocked until a seat is freed or the limit is raised.</p>
      ) : null}
    </section>
  )

  const tabs = [
    {
      id: 'overview',
      label: 'Overview',
      content: (
        <div className="space-y-5">
          <section className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-4">
            <MiniStat label="Licensed users" value={formatSeats(seatsUsed, seatLimit)} />
            <MiniStat label="Sites" value={tenant.branchCount} />
            <MiniStat label="Work orders" value={tenant.workOrderCount} />
            <MiniStat label="Active now" value={tenant.activeUsersNow} />
          </section>

          <section className={panelClass}>
            <SectionTitle title="Tenant profile" description="Workspace details and lifecycle summary." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              <InfoCard label="Status" value={tenantStatusBadge(tenant.status)} />
              <InfoCard label="License" value={licenseStatusBadge(tenant.licenseStatus)} />
              <InfoCard label="Plan" value={tenant.planName ?? 'No plan'} />
              <InfoCard label="Modules" value={modules.length ? modules.map((m) => moduleLabels[m] ?? m).join(' + ') : 'None'} />
              <InfoCard label="License expires" value={tenant.isPerpetual ? 'Never (perpetual)' : licenseExpiresAt ? formatDate(licenseExpiresAt) : 'Not set'} />
              <InfoCard label="Workspace slug" value={tenant.slug} mono />
              <InfoCard label="Primary contact" value={tenant.contactName ?? 'Not set'} />
              <InfoCard label="Contact email" value={tenant.contactEmail ?? 'Not set'} />
              <InfoCard label="Country" value={tenant.country ?? 'Not set'} />
              <InfoCard label="Created" value={formatDateTime(tenant.createdAt)} />
              <InfoCard label="Last activity" value={tenant.lastActivityAt ? formatDateTime(tenant.lastActivityAt) : 'No activity yet'} />
            </div>
          </section>
        </div>
      ),
    },
    {
      id: 'users',
      label: 'Users',
      content: (
        <div className="space-y-5">
          {seatsPanel}
          {tenant.tenantId ? <TenantWhatsAppPanel tenantId={tenant.tenantId} className={panelClass} /> : null}
          <section className={panelClass}>
            <div className="flex flex-wrap items-start justify-between gap-3">
              <SectionTitle title="Workspace users" description="Accounts inside this tenant. Resend a lapsed setup link here — it is the only recovery path for an admin who has never signed in. Ecosys staff accounts live under Platform → Users." />
              <button
                type="button"
                onClick={() => setShowAddWorkspaceUser(true)}
                disabled={seatsFull}
                title={seatsFull ? `This tenant has reached its licensed user limit of ${seats.licensed}` : undefined}
                className={primaryButton}
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
          <section className={panelClass}>
            <SectionTitle title="Licensed modules" description="What this tenant's plan entitles. The backend blocks every API of a module that is not licensed; change the plan from Subscription → Renew." />
            <div className="mt-4 flex flex-wrap gap-2">
              {['operations', 'finance'].map((module) => (
                <span
                  key={module}
                  className={`inline-flex items-center rounded-full border px-3 py-1 text-sm font-medium ${modules.includes(module) ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : 'border-slate-200 bg-slate-50 text-slate-400 line-through'}`}
                >
                  {moduleLabels[module]}
                </span>
              ))}
            </div>
            <p className="mt-3 text-sm text-slate-600">Plan: {tenant.planName ?? 'No plan'}</p>
          </section>
          <section className={panelClass}>
            <SectionTitle title="Module preferences" description="Settings this tenant has saved inside its own workspace (dashboard targets, inventory, payments, portal, integrations). These are tenant-owned and do not grant access." />
            <div className="mt-4">
              {moduleSettingsLoading ? (
                <Spinner label="Loading module preferences" />
              ) : (moduleSettings ?? []).length === 0 ? (
                <p className="text-sm text-slate-500">This tenant has not customised any module preferences.</p>
              ) : (
                <div className="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
                  {(moduleSettings ?? []).map((setting) => (
                    <InfoCard key={setting.moduleKey} label={setting.moduleKey} value={`Updated ${formatDateTime(setting.updatedAt)}`} />
                  ))}
                </div>
              )}
            </div>
          </section>
        </div>
      ),
    },
    {
      id: 'licensing',
      label: 'Subscription',
      content: (
        <div className="space-y-5">
          <section className={panelClass}>
            <SectionTitle title="Subscription and access" description="The license is the single source of truth for access. Every action below is recorded in the audit trail." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              <InfoCard label="Tenant status" value={tenantStatusBadge(tenant.status)} />
              <InfoCard label="License" value={licenseStatusBadge(tenant.licenseStatus)} />
              {isSuspended ? (
                <InfoCard label="Suspension" value={tenant.suspensionReason === 'NonPayment' ? 'Unpaid subscription (automatic)' : 'Suspended by Ecosys'} />
              ) : (
                <InfoCard label="Trial" value={trialStatusBadge(tenant.trialStatus)} />
              )}
              <InfoCard label="License expires" value={tenant.isPerpetual ? 'Never (perpetual)' : licenseExpiresAt ? formatDate(licenseExpiresAt) : 'Not set'} />
              <InfoCard label="Licensed users" value={formatSeats(seatsUsed, seatLimit)} />
              <InfoCard label="Plan" value={tenant.planName ?? 'No plan'} />
            </div>
          </section>
          <div className="flex flex-wrap gap-2" data-testid="tenant-lifecycle-actions">
            {isTrial || isInactive ? <button type="button" className={primaryButton} onClick={() => setShowActivateDialog(true)}>Activate</button> : null}
            {isSuspended ? (
              <button
                type="button"
                className={primaryButton}
                disabled={reactivateLicense.isPending}
                onClick={() => void reactivateLicense.mutateAsync(undefined).then(() => toast.success('Tenant reactivated')).catch(() => undefined)}
              >
                Reactivate
              </button>
            ) : null}
            {isActive || isSuspended ? <button type="button" className={secondaryButton} onClick={() => setShowRenewDialog(true)}>Renew</button> : null}
            {(isActive || isSuspended) && !tenant.isPerpetual ? (
              <button
                type="button"
                className={secondaryButton}
                onClick={() => {
                  setExtendLicenseTarget(licenseExpiresAt ? licenseExpiresAt.slice(0, 10) : '')
                  setShowExtendLicenseDialog(true)
                }}
              >
                Extend
              </button>
            ) : null}
            {isTrial && !tenant.trialExtensionUsed ? <button type="button" className={secondaryButton} onClick={() => setShowExtendTrial(true)}>Extend trial</button> : null}
            <button type="button" className={secondaryButton} onClick={openSeatsDialog}>Edit licensed users</button>
            {isActive || isTrial ? <button type="button" className={warningButton} onClick={() => setShowSuspend(true)}>Suspend</button> : null}
          </div>
        </div>
      ),
    },
    {
      id: 'communication',
      label: 'Communication',
      content: communication ? (
        <div className="space-y-5">
          <section className={panelClass}>
            <SectionTitle title="Email settings" description="Read-only. The tenant manages its own email in its workspace under Settings → Email (sender name, reply-to, and optionally its own mail server)." />
            <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
              <InfoCard label="Sends through" value={communication.emailSettings.usesOwnSmtp ? `Own SMTP server (${communication.emailSettings.ownSmtpHost ?? '—'})` : 'Platform mail'} />
              {communication.emailSettings.usesOwnSmtp ? <InfoCard label="From address" value={communication.emailSettings.ownFromAddress ?? '—'} /> : null}
              <InfoCard label="Sender name" value={communication.emailSettings.senderName || '—'} />
              <InfoCard label="Sender email" value={communication.emailSettings.senderEmail || '—'} />
              <InfoCard label="Reply-to email" value={communication.emailSettings.replyToEmail || '—'} />
              <InfoCard label="Tenant notifications" value={communication.emailSettings.enableTenantEmailNotifications ? 'Enabled' : 'Disabled'} />
            </div>

            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className={secondaryButton} onClick={() => void sendTestEmail()}>
                Send test email
              </button>
              <button type="button" className={secondaryButton} onClick={() => void verifySmtp()}>
                Verify SMTP
              </button>
              <input value={testRecipient} onChange={(event) => setTestRecipient(event.target.value)} placeholder="Test recipient email" className="h-11 rounded-lg border border-slate-200 px-3 text-sm outline-none" />
            </div>
          </section>

          <section className={panelClass}>
            <SectionTitle title="Finance email copies" description="Read-only. Addresses the tenant copies (CC) on invoices, receipts and statements; documents always go to the client itself. The tenant edits these under Settings → Finance Defaults." />
            {communication.recipients.length === 0 ? (
              <p className="mt-4 text-sm text-slate-500">No finance copies configured.</p>
            ) : (
              <ul className="mt-4 space-y-2 text-sm text-slate-700">
                {communication.recipients.map((recipient) => (
                  <li key={`${recipient.recipientGroup}-${recipient.email}`} className="flex justify-between rounded-lg bg-slate-50 px-3 py-2">
                    <span>{recipient.email}</span>
                    <span className="text-xs text-slate-500">{recipient.recipientGroup}</span>
                  </li>
                ))}
              </ul>
            )}
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
        <section className={panelClass}>
          <SectionTitle title="Document numbering" description="Tenant-owned numbering sequences. Reset uses the same support tool available from Platform Support." />
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
        <div className="space-y-5">
        <ClientTemplatesPanel tenantId={tenant.tenantId} className={panelClass} />
        <section className={panelClass}>
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
        </div>
      ),
    },
    {
      id: 'branding',
      label: 'Branding',
      content: brandingLoading ? (
        <Spinner label="Loading branding" />
      ) : branding ? (
        <section className={panelClass}>
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
          <p className="text-sm text-slate-600">
            Summary of the tenant's own invoicing. Individual invoices are managed inside the tenant workspace; Ecosys billing for this tenant is under{' '}
            <button type="button" className="font-medium text-[var(--app-primary)] underline" onClick={() => navigate('/platform/finance')}>Platform finance</button>.
          </p>
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
      label: 'Support & danger zone',
      content: (
        <section className="space-y-4">
          <div className={panelClass}>
            <SectionTitle title="Support access" description="Sign in as this tenant's primary admin in a new tab. The session is clearly marked, has an Exit button, and its start and end are recorded in both the tenant and platform audit logs." />
            <div className="mt-4">
              <button type="button" className={secondaryButton} disabled={!tenant || isSuspended || isInactive} onClick={() => setShowImpersonate(true)}>
                <UserCog className="h-4 w-4" />
                Impersonate admin
              </button>
              {isSuspended || isInactive ? <p className="mt-2 text-sm text-slate-500">Reactivate the tenant before starting an impersonation session.</p> : null}
            </div>
          </div>

          {!isInactive ? (
            <div className="rounded-2xl border border-rose-200 bg-rose-50 p-5">
              <p className="text-sm font-semibold text-rose-900">Deactivate tenant</p>
              <p className="mt-2 text-sm text-rose-800">Cancels the license and takes the workspace offline. No tenant data is deleted; the tenant can be restored later with Activate or Renew.</p>
              <button type="button" className={`mt-4 ${dangerButton}`} onClick={() => setShowDeactivate(true)}>
                Deactivate tenant
              </button>
            </div>
          ) : null}
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
              <Link to="/platform/tenants" className={secondaryButton}>
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

      <LicenseTermDialog
        open={showActivateDialog}
        title="Activate tenant"
        tenantName={tenant.name}
        confirmLabel="Activate Tenant"
        submitting={activate.isPending}
        onCancel={() => setShowActivateDialog(false)}
        onConfirm={(term) => {
          void activate.mutateAsync(term)
            .then(() => toast.success('Tenant activated'))
            .then(() => setShowActivateDialog(false))
            .catch(() => undefined)
        }}
      />

      <LicenseTermDialog
        open={showRenewDialog}
        title="Renew license"
        tenantName={tenant.name}
        confirmLabel="Renew"
        submitting={renewLicense.isPending}
        onCancel={() => setShowRenewDialog(false)}
        onConfirm={(term) => {
          void renewLicense.mutateAsync(term)
            .then(() => toast.success('License renewed'))
            .then(() => setShowRenewDialog(false))
            .catch(() => undefined)
        }}
      />

      <ConfirmDialog
        open={showExtendLicenseDialog}
        title="Extend license"
        message={`Push ${tenant.name}'s license expiry further out. Current expiry: ${licenseExpiresAt ? formatDate(licenseExpiresAt) : 'not set'}.${isSuspended && tenant.suspensionReason === 'NonPayment' ? ' Extending to a future date also lifts the unpaid-subscription suspension.' : ''}`}
        confirmLabel="Extend"
        confirmDisabled={!extendLicenseTarget || extendLicense.isPending}
        onCancel={() => setShowExtendLicenseDialog(false)}
        onConfirm={() => {
          if (!extendLicenseTarget) return
          void extendLicense.mutateAsync(new Date(extendLicenseTarget).toISOString())
            .then(() => toast.success('License extended'))
            .then(() => setShowExtendLicenseDialog(false))
            .catch(() => undefined)
        }}
      >
        <div className="mt-4 space-y-3">
          <div className="flex flex-wrap gap-2">
            {[{ label: '1 month', months: 1 }, { label: '3 months', months: 3 }, { label: '6 months', months: 6 }, { label: '1 year', months: 12 }].map((shortcut) => (
              <button
                key={shortcut.label}
                type="button"
                className="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-700 transition hover:bg-slate-50"
                onClick={() => {
                  const base = licenseExpiresAt && new Date(licenseExpiresAt) > new Date() ? new Date(licenseExpiresAt) : new Date()
                  base.setUTCMonth(base.getUTCMonth() + shortcut.months)
                  setExtendLicenseTarget(base.toISOString().slice(0, 10))
                }}
              >
                +{shortcut.label}
              </button>
            ))}
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">New expiry date</label>
            <input type="date" value={extendLicenseTarget} onChange={(event) => setExtendLicenseTarget(event.target.value)} className={inputClass} />
          </div>
        </div>
      </ConfirmDialog>

      <ConfirmDialog
        open={showSeatsDialog}
        title="Licensed users"
        message={`How many workspace users may ${tenant.name} have? ${seatsUsed} seat${seatsUsed === 1 ? ' is' : 's are'} in use. Lowering the limit below usage never deactivates anyone; it only blocks new users until usage falls.`}
        confirmLabel="Save"
        confirmDisabled={!seatsValid || updateSeats.isPending}
        onCancel={() => setShowSeatsDialog(false)}
        onConfirm={() => {
          if (!seatsValid) return
          void updateSeats.mutateAsync(parsedSeats)
            .then(() => toast.success('Licensed users updated'))
            .then(() => setShowSeatsDialog(false))
            .catch(() => undefined)
        }}
      >
        <div className="mt-4">
          <label htmlFor="tenant-seats-input" className="mb-1.5 block text-sm font-medium text-slate-700">Licensed users</label>
          <input id="tenant-seats-input" type="number" min={1} step={1} value={seatsInput} onChange={(event) => setSeatsInput(event.target.value)} className={inputClass} />
          {!seatsParse.ok ? (
            <p className="mt-2 text-sm text-rose-700">{seatsParse.error}</p>
          ) : parsedSeats < seatsUsed ? (
            <p className="mt-2 text-sm text-amber-700">This is below the {seatsUsed} users already active. Nobody is deactivated; new additions stay blocked until usage is under {parsedSeats}.</p>
          ) : null}
        </div>
      </ConfirmDialog>

      <ConfirmDialog
        open={showSuspend}
        title="Suspend tenant"
        message={`Suspend ${tenant.name}? All of its users lose access until the tenant is reactivated.`}
        confirmLabel="Suspend"
        confirmDisabled={!suspendReason.trim() || suspend.isPending}
        danger
        onCancel={() => { setShowSuspend(false); setSuspendReason('') }}
        onConfirm={() => {
          void suspend.mutateAsync(suspendReason.trim())
            .then(() => toast.success('Tenant suspended'))
            .catch(() => undefined)
            .finally(() => { setShowSuspend(false); setSuspendReason('') })
        }}
      >
        <ReasonField value={suspendReason} onChange={setSuspendReason} placeholder="Why is this tenant being suspended?" />
      </ConfirmDialog>

      <ConfirmDialog
        open={showDeactivate}
        title="Deactivate tenant"
        message={`Deactivate ${tenant.name}? The license is cancelled and the workspace goes offline. No data is deleted.`}
        confirmLabel="Deactivate"
        confirmText={tenant.slug}
        confirmDisabled={!deactivateReason.trim() || deactivate.isPending}
        danger
        onCancel={() => { setShowDeactivate(false); setDeactivateReason('') }}
        onConfirm={() => {
          void deactivate.mutateAsync(deactivateReason.trim())
            .then(() => toast.success('Tenant deactivated'))
            .catch(() => undefined)
            .finally(() => { setShowDeactivate(false); setDeactivateReason('') })
        }}
      >
        <ReasonField value={deactivateReason} onChange={setDeactivateReason} placeholder="Why is this tenant being deactivated?" />
      </ConfirmDialog>

      <ConfirmDialog
        open={showExtendTrial}
        title="Extend trial"
        message={`Extend the ${tenant.name} trial by 14 days? This can only be done once.`}
        confirmLabel="Extend"
        onCancel={() => setShowExtendTrial(false)}
        onConfirm={() => {
          void extendTrial.mutateAsync(undefined)
            .then(() => toast.success('Trial extended'))
            .catch(() => undefined)
            .finally(() => setShowExtendTrial(false))
        }}
      />

      <ConfirmDialog
        open={showImpersonate}
        title="Impersonate tenant admin"
        message={`Open ${tenant.name}'s workspace as its primary admin in a new tab. The reason is recorded in the audit trail.`}
        confirmLabel="Start session"
        confirmDisabled={!impersonateReason.trim() || impersonate.isPending}
        onCancel={() => { setShowImpersonate(false); setImpersonateReason('') }}
        onConfirm={() => void startImpersonation()}
      >
        <ReasonField value={impersonateReason} onChange={setImpersonateReason} placeholder="e.g. Support ticket #1234: reproduce invoice issue" />
      </ConfirmDialog>

      <ConfirmDialog
        open={showResetNumbering !== null}
        title="Reset numbering"
        message={
          showResetNumbering
            ? `Reset ${showResetNumbering.documentType} numbering to 1 for ${tenant.name}? Any numbers already issued will conflict with the next document created.`
            : ''
        }
        confirmLabel="Reset"
        {...(showResetNumbering ? { confirmText: showResetNumbering.documentType } : {})}
        confirmDisabled={!resetNumberingReason.trim()}
        danger
        onCancel={() => {
          setShowResetNumbering(null)
          setResetNumberingReason('')
        }}
        onConfirm={() => {
          if (showResetNumbering) {
            void resetNumbering(showResetNumbering.documentType, resetNumberingReason)
          }
        }}
      >
        <ReasonField value={resetNumberingReason} onChange={setResetNumberingReason} placeholder="Why is this sequence being reset?" />
      </ConfirmDialog>
    </>
  )
}

function ReasonField({ value, onChange, placeholder }: { value: string; onChange: (value: string) => void; placeholder: string }) {
  return (
    <div className="mt-4">
      <label className="mb-2 block text-sm font-medium text-slate-700">Reason (required)</label>
      <textarea
        value={value}
        onChange={(event) => onChange(event.target.value)}
        rows={2}
        placeholder={placeholder}
        className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]"
      />
    </div>
  )
}

function MiniStat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className={panelClass}>
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
