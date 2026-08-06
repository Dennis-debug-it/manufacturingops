import { useEffect, useState } from 'react'
import { Plus, Search, ShieldCheck } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../shared/components/PageHeader'
import { Spinner } from '../shared/components/Spinner'
import { EmptyState } from '../shared/components/EmptyState'
import { DataTable, type DataTableColumn } from '../shared/components/DataTable'
import { FormDrawer } from '../shared/components/FormDrawer'
import { ConfirmDialog } from '../shared/components/ConfirmDialog'
import { useDebounce } from '../shared/hooks/useDebounce'
import { formatDateTime } from '../shared/utils/dates'
import {
  useCreatePlatformUser,
  usePlatformUsersPaged,
  useResendPlatformUserCredentials,
  useResetPlatformUserPassword,
  useUpdatePlatformUserStatus,
} from './api'
import { Field, SectionTitle } from './common'
import type { PlatformUser } from './types'

const roles = ['All', 'PlatformOwner', 'PlatformAdmin', 'SupportAdmin', 'FinanceAdmin', 'ReadOnlyAuditor'] as const
const statuses = ['All', 'Active', 'Inactive'] as const

type UserRole = (typeof roles)[number]
type UserStatus = (typeof statuses)[number]

const emptyForm = {
  fullName: '',
  email: '',
  phone: '',
  role: 'PlatformAdmin',
  status: 'Active',
}

export function UsersPage() {
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<UserRole>('All')
  const [statusFilter, setStatusFilter] = useState<UserStatus>('All')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const debouncedSearch = useDebounce(search, 250)
  useEffect(() => setPage(1), [debouncedSearch, roleFilter, statusFilter])
  const { data, isLoading, isError } = usePlatformUsersPaged(
    {
      search: debouncedSearch || undefined,
      role: roleFilter === 'All' ? undefined : roleFilter,
      status: statusFilter === 'All' ? undefined : statusFilter,
    },
    page,
    pageSize,
  )
  const [showCreate, setShowCreate] = useState(false)
  const [form, setForm] = useState(emptyForm)
  const [activeUser, setActiveUser] = useState<PlatformUser | null>(null)
  const [resetUser, setResetUser] = useState<PlatformUser | null>(null)
  const [resendUser, setResendUser] = useState<PlatformUser | null>(null)

  const createUser = useCreatePlatformUser()
  const updateStatus = useUpdatePlatformUserStatus(activeUser?.id ?? '')
  const resetPassword = useResetPlatformUserPassword(resetUser?.id ?? '')
  const resendCredentials = useResendPlatformUserCredentials(resendUser?.id ?? '')

  // Search / role / status filtering + paging run server-side now (see usePlatformUsersPaged).
  const rows = data?.items ?? []

  const columns: DataTableColumn<PlatformUser>[] = [
    { key: 'fullName', header: 'Name', cell: (row) => <div className="font-medium text-slate-900">{row.fullName}<div className="text-xs text-slate-500">{row.mustChangePassword ? 'Must change password' : 'Password ready'}</div></div> },
    { key: 'email', header: 'Email', cell: (row) => row.email },
    { key: 'phone', header: 'Phone', cell: (row) => row.phone ?? '—' },
    { key: 'role', header: 'Role', cell: (row) => roleLabel(row.role) },
    { key: 'status', header: 'Status', cell: (row) => <span className="inline-flex rounded-full bg-slate-100 px-2.5 py-1 text-xs font-medium text-slate-700">{row.status}</span> },
    { key: 'lastLoginAt', header: 'Last login', cell: (row) => (row.lastLoginAt ? formatDateTime(row.lastLoginAt) : 'Never') },
    { key: 'lastCredentialSentAt', header: 'Credentials sent', cell: (row) => (row.lastCredentialSentAt ? formatDateTime(row.lastCredentialSentAt) : 'Never') },
    {
      key: 'actions',
      header: 'Actions',
      cell: (row) => (
        <div className="flex flex-wrap gap-2">
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setActiveUser(row)}>
            {row.status === 'Active' ? 'Deactivate' : 'Activate'}
          </button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setResendUser(row)}>
            Resend credentials
          </button>
          <button type="button" className="inline-flex h-9 items-center rounded-lg border border-slate-200 bg-white px-3 text-xs font-medium text-slate-700 hover:bg-slate-50" onClick={() => setResetUser(row)}>
            Reset password
          </button>
        </div>
      ),
    },
  ]

  const submitCreate = async () => {
    if (!form.fullName.trim() || !form.email.trim()) {
      toast.error('Full name and email are required.')
      return
    }

    try {
      await createUser.mutateAsync({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || null,
        role: form.role,
        status: form.status,
        password: null,
      })
      toast.success('Platform user created')
      setShowCreate(false)
      setForm(emptyForm)
    } catch {
      // handled by interceptor
    }
  }

  const toggleActive = async () => {
    if (!activeUser) return
    try {
      await updateStatus.mutateAsync(activeUser.status === 'Active' ? 'Inactive' : 'Active')
      toast.success(`User ${activeUser.status === 'Active' ? 'deactivated' : 'activated'}`)
      setActiveUser(null)
    } catch {
      // handled by interceptor
    }
  }

  const doReset = async () => {
    if (!resetUser) return
    try {
      await resetPassword.mutateAsync(undefined)
      toast.success('Password reset email queued')
      setResetUser(null)
    } catch {
      // handled by interceptor
    }
  }

  const doResend = async () => {
    if (!resendUser) return
    try {
      await resendCredentials.mutateAsync()
      toast.success('Credentials resent')
      setResendUser(null)
    } catch {
      // handled by interceptor
    }
  }

  return (
    <div className="space-y-5">
      <PageHeader
        title="Platform Users"
        subtitle="Manage owner, admin, support, finance, and read-only platform access."
        primaryAction={<button type="button" className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white hover:bg-slate-800" onClick={() => setShowCreate(true)}><Plus className="h-4 w-4" />Create user</button>}
      />

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionTitle title="Role guide" description="Platform users are staff-only accounts. Tenant users are managed inside each workspace." />
        <div className="mt-4 grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-5">
          {[
            ['Platform Owner', 'Full platform oversight and destructive lifecycle actions.'],
            ['Platform Admin', 'Operational administration across tenants and settings.'],
            ['Support Admin', 'Support-focused access for troubleshooting and user help.'],
            ['Finance Admin', 'Billing, invoices, payments, and reporting access.'],
            ['Read-only Auditor', 'Inspect and export platform data without editing.'],
          ].map(([title, description]) => (
            <div key={title} className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3">
              <div className="flex items-center gap-2 font-medium text-slate-900"><ShieldCheck className="h-4 w-4" />{title}</div>
              <p className="mt-2 text-sm text-slate-600">{description}</p>
            </div>
          ))}
        </div>
      </section>

      <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm lg:grid-cols-[minmax(0,2fr)_repeat(2,minmax(220px,280px))]">
        <label className="flex items-center gap-2 rounded-xl border border-slate-200 px-3 py-2">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search name, email, or phone" className="w-full border-0 bg-transparent p-0 text-sm outline-none placeholder:text-slate-400" />
        </label>
        <select value={roleFilter} onChange={(event) => setRoleFilter(event.target.value as UserRole)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
          {roles.map((role) => <option key={role} value={role}>{role === 'All' ? 'All roles' : roleLabel(role)}</option>)}
        </select>
        <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value as UserStatus)} className="h-11 rounded-xl border border-slate-200 bg-white px-3 text-sm outline-none">
          {statuses.map((status) => <option key={status} value={status}>{status === 'All' ? 'All statuses' : status}</option>)}
        </select>
      </div>

      {isLoading ? <Spinner label="Loading platform users" /> : null}
      {!isLoading && isError ? <EmptyState title="Platform users unavailable" description="Platform user records could not be loaded." /> : null}
      {!isLoading && !isError ? (
        rows.length === 0 ? (
          <EmptyState title="No platform users found" description="Adjust filters or create a new platform user." />
        ) : (
          <DataTable<PlatformUser>
            columns={columns}
            data={rows}
            emptyTitle="No platform users found"
            emptyDescription="Adjust filters or create a new platform user."
            pagination={{
              page,
              pageSize,
              total: data?.totalCount ?? 0,
              onPageChange: setPage,
              onPageSizeChange: (size) => { setPageSize(size); setPage(1) },
            }}
          />
        )
      ) : null}

      <FormDrawer
        open={showCreate}
        title="Create platform user"
        onClose={() => setShowCreate(false)}
        footer={<div className="flex justify-end gap-2"><button type="button" className="rounded-lg border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50" onClick={() => setShowCreate(false)}>Cancel</button><button type="button" className="rounded-lg bg-slate-900 px-4 py-2.5 text-sm font-medium text-white hover:bg-slate-800" onClick={() => void submitCreate()}>Create user</button></div>}
      >
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Field label="Full name"><input value={form.fullName} onChange={(event) => setForm((current) => ({ ...current, fullName: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Email"><input type="email" value={form.email} onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Phone"><input value={form.phone} onChange={(event) => setForm((current) => ({ ...current, phone: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900" /></Field>
          <Field label="Role"><select value={form.role} onChange={(event) => setForm((current) => ({ ...current, role: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900">{roles.filter((role) => role !== 'All').map((role) => <option key={role} value={role}>{roleLabel(role)}</option>)}</select></Field>
          <Field label="Status"><select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: event.target.value }))} className="h-11 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-slate-900"><option value="Active">Active</option><option value="Inactive">Inactive</option></select></Field>
        </div>
      </FormDrawer>

      <ConfirmDialog open={activeUser !== null} title={activeUser?.status === 'Active' ? 'Deactivate user' : 'Activate user'} message={activeUser ? `${activeUser.status === 'Active' ? 'Deactivate' : 'Activate'} ${activeUser.fullName}?` : ''} confirmLabel={activeUser?.status === 'Active' ? 'Deactivate' : 'Activate'} onCancel={() => setActiveUser(null)} onConfirm={() => void toggleActive()} />
      <ConfirmDialog open={resetUser !== null} title="Reset password" message={resetUser ? `Reset password for ${resetUser.fullName}? A fresh credential email will be sent.` : ''} confirmLabel="Reset" onCancel={() => setResetUser(null)} onConfirm={() => void doReset()} />
      <ConfirmDialog open={resendUser !== null} title="Resend credentials" message={resendUser ? `Resend credentials to ${resendUser.fullName}?` : ''} confirmLabel="Resend" onCancel={() => setResendUser(null)} onConfirm={() => void doResend()} />
    </div>
  )
}

function roleLabel(role: string) {
  const map: Record<string, string> = {
    PlatformOwner: 'Platform Owner',
    PlatformAdmin: 'Platform Admin',
    SupportAdmin: 'Support Admin',
    FinanceAdmin: 'Finance Admin',
    ReadOnlyAuditor: 'Read-only Auditor',
  }
  return map[role] ?? role
}
