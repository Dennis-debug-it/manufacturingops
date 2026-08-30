import { useEffect, useMemo, useState } from 'react'
import { ShieldCheck } from 'lucide-react'
import toast from 'react-hot-toast'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { cn } from '../../shared/utils/cn'
import { TeamTabNav } from './components/TeamTabNav'
import { PermissionMatrix } from './components/PermissionMatrix'
import {
  FULL_CAPABILITIES,
  roleLabel,
  useRolePermissions,
  useSaveRolePermissions,
  type RoleCapabilities,
  type RolePermissionRow,
} from './rolePermissionsApi'

export function RolesPage() {
  const { data, isLoading, isError } = useRolePermissions()
  const save = useSaveRolePermissions()
  const [draft, setDraft] = useState<RolePermissionRow[]>([])
  const [selectedRole, setSelectedRole] = useState<string>('')

  useEffect(() => {
    if (!data) return
    setDraft(data.roles.map((row) => ({ role: row.role, permissions: { ...row.permissions } })))
    setSelectedRole((prev) => prev || data.roles[0]?.role || data.implicitAllAccessRole || '')
  }, [data])

  const dirty = useMemo(() => (data ? JSON.stringify(data.roles) !== JSON.stringify(draft) : false), [data, draft])

  if (isLoading) return <Spinner label="Loading roles" />
  if (isError || !data) return <EmptyState title="Roles unavailable" description="The role permission matrix could not be loaded." />

  const isAdmin = Boolean(data.implicitAllAccessRole) && selectedRole === data.implicitAllAccessRole
  const current = draft.find((row) => row.role === selectedRole)
  const roleTabs = [data.implicitAllAccessRole, ...draft.map((row) => row.role)].filter(Boolean) as string[]

  const setCurrent = (permissions: RoleCapabilities) =>
    setDraft((prev) => prev.map((row) => (row.role === selectedRole ? { ...row, permissions } : row)))

  const onSave = async () => {
    try {
      await save.mutateAsync(draft)
      toast.success('Role permissions saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Roles & Permissions" subtitle="Check or uncheck each capability to define what a role can do." />
      <TeamTabNav />

      <div className="flex items-start gap-3 rounded-2xl border border-blue-200 bg-blue-50 p-4 text-sm text-blue-950">
        <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0" />
        <p>These are the default capabilities for each role. A user inherits their role&apos;s boxes and can be given per-user overrides on their profile. <strong>Admin</strong> always has full access.</p>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {roleTabs.map((role) => (
          <button
            key={role}
            type="button"
            onClick={() => setSelectedRole(role)}
            className={cn(
              'inline-flex h-10 items-center rounded-full border px-4 text-sm font-medium transition',
              role === selectedRole ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {roleLabel(role)}
          </button>
        ))}
      </div>

      {isAdmin ? (
        <>
          <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm text-slate-600">
            The <strong>Admin</strong> role implicitly has every capability and cannot be edited.
          </div>
          <PermissionMatrix value={FULL_CAPABILITIES} onChange={() => undefined} disabled />
        </>
      ) : current ? (
        <>
          <PermissionMatrix value={current.permissions} onChange={setCurrent} />
          <div className="flex items-center justify-end gap-3">
            {dirty ? <span className="text-xs font-medium text-amber-600">Unsaved changes</span> : null}
            <button
              type="button"
              onClick={onSave}
              disabled={!dirty || save.isPending}
              className="inline-flex h-11 items-center rounded-lg bg-[var(--app-primary)] px-5 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-50"
            >
              {save.isPending ? 'Saving…' : 'Save changes'}
            </button>
          </div>
        </>
      ) : null}
    </div>
  )
}
