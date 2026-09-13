import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useParams } from 'react-router-dom'
import { Copy } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useResendCredentials, useResetUserPassword, useSetUserStatus, useTeamUser, useUpdateTeamUser } from './api'
import { PermissionMatrix } from './components/PermissionMatrix'
import { UserAccessPanel } from './components/UserAccessPanel'
import type { RoleCapabilities } from './rolePermissionsApi'

export function UserDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canManage = usePermission('team.manage')
  const { data: user, isLoading, isError } = useTeamUser(id)
  const setStatus = useSetUserStatus(id ?? '')
  const resendCredentials = useResendCredentials(id ?? '')
  const resetPassword = useResetUserPassword(id ?? '')
  const updateUser = useUpdateTeamUser(id ?? '')

  const [caps, setCaps] = useState<RoleCapabilities | null>(null)
  const [revealedPassword, setRevealedPassword] = useState<string | null>(null)
  useEffect(() => {
    if (user) setCaps({ ...user.permissions })
  }, [user])

  const isAdminUser = Boolean(user) && user!.role.toLowerCase() === 'admin'
  const permsDirty = Boolean(user && caps) && JSON.stringify(caps) !== JSON.stringify(user!.permissions)

  const savePermissions = async () => {
    if (!user || !caps) return
    try {
      await updateUser.mutateAsync({
        fullName: user.fullName,
        email: user.hasEmail ? user.email : null,
        username: user.username,
        phoneNumber: user.phoneNumber,
        role: user.role,
        jobTitle: user.jobTitle,
        department: user.department,
        isActive: user.isActive,
        permissions: caps,
        branchIds: user.branchIds,
        defaultBranchId: user.defaultBranchId,
        hasAllBranchAccess: user.hasAllBranchAccess,
        assignmentGroupIds: user.assignmentGroups.map((group) => group.id),
      })
      toast.success('Permissions updated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const toggleStatus = async () => {
    if (!user) return
    try {
      await setStatus.mutateAsync(!user.isActive)
      toast.success(user.isActive ? 'User deactivated' : 'User activated')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const resend = async () => {
    try {
      const result = await resendCredentials.mutateAsync(undefined)
      if (result.success) toast.success('Invitation sent')
      else toast.error(result.message ?? 'Invitation delivery failed. Please try again.')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const reset = async () => {
    try {
      if (user && !user.hasEmail) {
        // Nothing to email — generate a new temporary password and reveal it once instead.
        const result = await resetPassword.mutateAsync({ generateAndReveal: true })
        if (result.temporaryPassword) setRevealedPassword(result.temporaryPassword)
        else toast.error(result.message ?? 'Could not reset the password. Please try again.')
        return
      }

      await resetPassword.mutateAsync(undefined)
      toast.success('Password reset — new credentials sent')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const copyRevealedPassword = async () => {
    if (!revealedPassword) return
    try {
      await navigator.clipboard.writeText(revealedPassword)
      toast.success('Password copied')
    } catch {
      toast.error('Could not copy — select and copy the password manually')
    }
  }

  if (isLoading) {
    return <Spinner label="Loading user" />
  }

  if (isError || !user) {
    return <EmptyState title="User unavailable" description="This user could not be loaded." />
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title={user.fullName}
        subtitle={user.jobTitle ?? user.department ?? 'User'}
        breadcrumbs={[{ label: 'Users & Access', to: '/team/users' }, { label: user.fullName }]}
        secondaryActions={<StatusBadge status={user.isActive ? (user.invitationStatus ?? (user.mustChangePassword ? 'Invited' : 'Active')) : 'Inactive'} />}
        primaryAction={
          canManage ? (
            <div className="flex flex-wrap gap-2">
              {user.hasEmail && (user.mustChangePassword || user.invitationStatus === 'Invited' || user.invitationStatus === 'Expired') ? <button type="button" onClick={resend} disabled={resendCredentials.isPending} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">{resendCredentials.isPending ? 'Sending…' : 'Resend Invite'}</button> : null}
              <button type="button" onClick={reset} disabled={resetPassword.isPending} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">{resetPassword.isPending ? 'Working…' : user.hasEmail ? 'Reset Password' : 'Generate Password'}</button>
              <button type="button" onClick={toggleStatus} className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
                {user.isActive ? 'Deactivate' : 'Activate'}
              </button>
            </div>
          ) : undefined
        }
      />

      {revealedPassword ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
          <div className="text-sm text-amber-900">
            New temporary password: <span className="font-mono font-semibold">{revealedPassword}</span>
            <span className="ml-2 text-xs text-amber-700">Share it with the user directly — it will not be shown again.</span>
          </div>
          <div className="flex items-center gap-2">
            <button type="button" onClick={copyRevealedPassword} className="inline-flex items-center gap-1 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-xs font-medium text-amber-800 transition hover:bg-amber-100">
              <Copy className="h-3.5 w-3.5" /> Copy
            </button>
            <button type="button" onClick={() => setRevealedPassword(null)} className="text-xs font-medium text-amber-700 hover:text-amber-900">Dismiss</button>
          </div>
        </div>
      ) : null}

      <SummarySection title="Profile" description="Contact and organization details.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            ['Email', user.hasEmail ? user.email : 'No email address'],
            ['Username', user.username ?? 'â€”'],
            ['Phone', user.phoneNumber ?? 'â€”'],
            ['Department', user.department ?? 'â€”'],
            ['Branch Access', user.hasAllBranchAccess ? 'All Branches' : (user.defaultBranchName ?? 'â€”')],
            ['Must Change Password', user.mustChangePassword ? 'Yes' : 'No'],
            ['Last Invitation Sent', user.lastCredentialSentAt ? formatDateTime(user.lastCredentialSentAt) : 'Never'],
            ['Created', formatDateTime(user.createdAt)],
          ].map(([label, value]) => (
            <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
              <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
              <div className="mt-2 text-sm text-slate-900">{value}</div>
            </div>
          ))}
        </div>
      </SummarySection>

      <SummarySection title="Access & Permissions" description="Check or uncheck each capability to control what this user can do.">
        {isAdminUser ? (
          <div className="mb-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            This user is an <strong>Admin</strong> and implicitly has every capability. Change their role to grant a limited set.
          </div>
        ) : null}
        {caps ? (
          <PermissionMatrix value={isAdminUser ? user.permissions : caps} onChange={setCaps} disabled={!canManage || isAdminUser} />
        ) : null}
        {canManage && !isAdminUser ? (
          <div className="mt-4 flex items-center justify-end gap-3">
            {permsDirty ? <span className="text-xs font-medium text-amber-600">Unsaved changes</span> : null}
            <button
              type="button"
              onClick={savePermissions}
              disabled={!permsDirty || updateUser.isPending}
              className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50"
            >
              {updateUser.isPending ? 'Savingâ€¦' : 'Save permissions'}
            </button>
          </div>
        ) : null}
      </SummarySection>

      {id ? <UserAccessPanel userId={id} isAdminUser={isAdminUser} /> : null}

      {user.assignmentGroups.length > 0 ? (
        <SummarySection title="Assignment Groups" description="Groups this user belongs to.">
          <ul className="space-y-2 text-sm text-slate-700">
            {user.assignmentGroups.map((group) => (
              <li key={group.id} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2">
                <span>{group.name}</span>
                {group.isLead ? <StatusBadge status="Lead" /> : null}
              </li>
            ))}
          </ul>
        </SummarySection>
      ) : null}
    </div>
  )
}
