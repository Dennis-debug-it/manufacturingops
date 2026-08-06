import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useResendCredentials, useResetUserPassword, useSetUserStatus, useTeamUser, useUpdateTeamUser } from './api'
import { PermissionMatrix } from './components/PermissionMatrix'
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
        email: user.email,
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
      await resendCredentials.mutateAsync(undefined)
      toast.success('Credentials resent')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const reset = async () => {
    try {
      await resetPassword.mutateAsync(undefined)
      toast.success('Password reset — new credentials sent')
    } catch {
      // interceptor surfaces the error toast
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
        secondaryActions={<StatusBadge status={user.isActive ? 'Active' : 'Inactive'} />}
        primaryAction={
          canManage ? (
            <div className="flex flex-wrap gap-2">
              <button type="button" onClick={resend} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Resend Credentials</button>
              <button type="button" onClick={reset} className="inline-flex h-11 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">Reset Password</button>
              <button type="button" onClick={toggleStatus} className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
                {user.isActive ? 'Deactivate' : 'Activate'}
              </button>
            </div>
          ) : undefined
        }
      />

      <SummarySection title="Profile" description="Contact and organization details.">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {[
            ['Email', user.email],
            ['Phone', user.phoneNumber ?? '—'],
            ['Department', user.department ?? '—'],
            ['Branch Access', user.hasAllBranchAccess ? 'All Branches' : (user.defaultBranchName ?? '—')],
            ['Must Change Password', user.mustChangePassword ? 'Yes' : 'No'],
            ['Last Credentials Sent', user.lastCredentialSentAt ? formatDateTime(user.lastCredentialSentAt) : 'Never'],
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
              className="inline-flex h-11 items-center rounded-lg bg-slate-900 px-5 text-sm font-medium text-white transition hover:bg-slate-800 disabled:opacity-50"
            >
              {updateUser.isPending ? 'Saving…' : 'Save permissions'}
            </button>
          </div>
        ) : null}
      </SummarySection>

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
