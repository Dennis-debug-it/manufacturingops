import toast from 'react-hot-toast'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useResendCredentials, useTeamUsers } from './api'
import { NewUserFormDrawer } from './components/NewUserFormDrawer'
import type { TeamUser } from './types'
import { TeamTabNav } from './components/TeamTabNav'

function ResendInviteButton({ userId }: { userId: string }) {
  const resend = useResendCredentials(userId)
  return <button type="button" disabled={resend.isPending} onClick={() => {
    void resend.mutateAsync(undefined).then((result) => {
      if (result.success) toast.success('Invitation sent')
      else toast.error(result.message ?? 'Invitation delivery failed. Please try again.')
    }).catch(() => { /* Interceptor displays the error. */ })
  }} className="inline-flex min-h-9 items-center rounded-lg border border-slate-200 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">{resend.isPending ? 'Sending�' : 'Resend Invite'}</button>
}

export function UsersPage() {
  const canManage = usePermission('team.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const { data, isLoading, isError } = useTeamUsers()

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((user) => [user.fullName, user.email, user.jobTitle].some((value) => (value ?? '').toLowerCase().includes(query)))
  }, [data, debouncedSearch])

  const columns: DataTableColumn<TeamUser>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/team/users/${row.id}`}>{row.fullName}</Link> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'department', header: 'Department', cell: (row) => row.department ?? '—' },
    { key: 'jobTitle', header: 'Job Title', cell: (row) => row.jobTitle ?? '—' },
    { key: 'defaultBranchName', header: 'Branch', cell: (row) => row.defaultBranchName ?? (row.hasAllBranchAccess ? 'All Branches' : '—') },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.isActive ? (row.invitationStatus ?? (row.mustChangePassword ? 'Invited' : 'Active')) : 'Inactive'} /> },
    ...(canManage ? [{ key: 'invitation', header: 'Invitation', cell: (row: TeamUser) => row.mustChangePassword || row.invitationStatus === 'Invited' || row.invitationStatus === 'Expired' ? <ResendInviteButton userId={row.id} /> : null }] : []),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Users & Access"
        subtitle="Manage people, their capabilities, and where access applies."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800">
              <Plus className="h-4 w-4" />Invite User
            </button>
          ) : undefined
        }
      />
      <TeamTabNav />

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search users by name, email, or job title" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Users unavailable' : 'No users yet'} emptyDescription={isError ? 'Users could not be loaded.' : 'Invite your first user to get started.'} />

      <NewUserFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
