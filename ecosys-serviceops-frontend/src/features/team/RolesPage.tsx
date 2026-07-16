import { Fragment, useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Loader2, ShieldCheck } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { usePermission } from '../../shared/hooks/usePermission'
import { resolveErrorMessage } from '../../shared/api/apiError'
import { TeamTabNav } from './components/TeamTabNav'
import {
  capabilityGroups,
  roleLabel,
  useRolePermissions,
  useSaveRolePermissions,
  type CapabilityKey,
  type RolePermissionRow,
} from './rolePermissionsApi'

export function RolesPage() {
  const canManage = usePermission('settings.manage')
  const { data, isLoading, isError } = useRolePermissions()
  const save = useSaveRolePermissions()

  const [rows, setRows] = useState<RolePermissionRow[]>([])

  useEffect(() => {
    if (data) {
      setRows(data.roles.map((row) => ({ ...row, permissions: { ...row.permissions } })))
    }
  }, [data])

  const dirty = useMemo(() => {
    if (!data) return false
    return JSON.stringify(data.roles) !== JSON.stringify(rows)
  }, [data, rows])

  const toggle = (role: string, key: CapabilityKey) => {
    setRows((current) =>
      current.map((row) =>
        row.role === role ? { ...row, permissions: { ...row.permissions, [key]: !row.permissions[key] } } : row,
      ),
    )
  }

  const submit = async () => {
    try {
      await save.mutateAsync(rows)
      toast.success('Role permissions saved')
    } catch (error) {
      toast.error(resolveErrorMessage(error))
    }
  }

  if (isLoading) {
    return <Spinner label="Loading role permissions" />
  }

  if (isError || !data) {
    return (
      <div className="space-y-6">
        <PageHeader title="Roles" subtitle="Choose what each role can do." />
        <TeamTabNav />
        <EmptyState title="Roles unavailable" description="Role permissions could not be loaded." />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Roles"
        subtitle="Tick a capability to grant it to a role. A user's access comes from their role."
        primaryAction={
          canManage ? (
            <button
              type="button"
              onClick={() => void submit()}
              disabled={!dirty || save.isPending}
              className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {save.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {save.isPending ? 'Saving…' : 'Save changes'}
            </button>
          ) : undefined
        }
      />
      <TeamTabNav />

      <div className="flex items-start gap-3 rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-600 shadow-sm">
        <span className="mt-0.5 rounded-lg bg-slate-100 p-1.5 text-slate-700">
          <ShieldCheck className="h-4 w-4" />
        </span>
        <p>
          <strong className="font-semibold text-slate-900">{roleLabel(data.implicitAllAccessRole)}</strong> always has every
          capability and cannot be edited. Everyone else gets exactly what is ticked below.
        </p>
      </div>

      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="min-w-full text-left text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="sticky left-0 z-10 bg-slate-50 px-5 py-3 font-medium text-slate-600">Capability</th>
              {rows.map((row) => (
                <th key={row.role} className="px-4 py-3 text-center font-medium text-slate-600">
                  {roleLabel(row.role)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {capabilityGroups.map((group) => (
              <Fragment key={group.group}>
                <tr className="bg-slate-50/60">
                  <td
                    colSpan={rows.length + 1}
                    className="px-5 py-2 text-xs font-semibold uppercase tracking-[0.14em] text-slate-500"
                  >
                    {group.group}
                  </td>
                </tr>
                {group.items.map((item) => (
                  <tr key={item.key} className="hover:bg-slate-50/60">
                    <td className="sticky left-0 z-10 bg-white px-5 py-2.5 text-slate-700">{item.label}</td>
                    {rows.map((row) => (
                      <td key={`${row.role}-${item.key}`} className="px-4 py-2.5 text-center">
                        <input
                          type="checkbox"
                          checked={row.permissions[item.key]}
                          disabled={!canManage || save.isPending}
                          onChange={() => toggle(row.role, item.key)}
                          aria-label={`${item.label} for ${roleLabel(row.role)}`}
                          className="h-4 w-4 cursor-pointer rounded border-slate-300 text-slate-900 focus:ring-slate-900 disabled:cursor-not-allowed disabled:opacity-50"
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
