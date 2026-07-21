import toast from 'react-hot-toast'
import { useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDateTime } from '../../shared/utils/dates'
import { useResendCredentials, useResetUserPassword, useSetUserStatus, useTeamUser } from './api'
import { capabilityGroups } from './rolePermissionsApi'

export function UserDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canManage = usePermission('team.manage')
  const { data: user, isLoading, isError } = useTeamUser(id)
  const setStatus = useSetUserStatus(id ?? '')
  const resendCredentials = useResendCredentials(id ?? '')
  const resetPassword = useResetUserPassword(id ?? '')

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

      <SummarySection title="Access & Permissions" description="This user’s current direct capabilities. Explicit denies take precedence over inherited group access.">
        <div className="mb-4 grid gap-3 sm:grid-cols-3">
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3"><div className="text-xs font-semibold uppercase tracking-wider text-slate-500">Permission groups</div><div className="mt-1 text-sm font-medium text-slate-900">No groups assigned</div></div>
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3"><div className="text-xs font-semibold uppercase tracking-wider text-slate-500">Access scope</div><div className="mt-1 text-sm font-medium text-slate-900">{user.hasAllBranchAccess ? 'Entire organization' : (user.defaultBranchName ?? 'Restricted')}</div></div>
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3"><div className="text-xs font-semibold uppercase tracking-wider text-slate-500">Approval limits</div><div className="mt-1 text-sm font-medium text-slate-900">No monetary limits configured</div></div>
        </div>
        <div className="space-y-4">
          {capabilityGroups.map((group) => <section key={group.group} className="overflow-hidden rounded-xl border border-slate-200"><h3 className="bg-slate-50 px-4 py-3 text-sm font-semibold text-slate-900">{group.group}</h3><ul className="divide-y divide-slate-100">{group.items.map((item) => { const allowed = user.permissions[item.key]; return <li key={item.key} className="flex items-center justify-between gap-4 px-4 py-3"><span className="text-sm text-slate-700">{item.label}</span><span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${allowed ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-600'}`}>{allowed ? 'Allowed · Direct' : 'Not granted'}</span></li> })}</ul></section>)}
        </div>
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
