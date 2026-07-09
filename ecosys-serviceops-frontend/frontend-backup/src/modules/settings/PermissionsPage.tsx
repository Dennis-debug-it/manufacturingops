import { useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAuth } from '../../auth/AuthContext'
import { useAsyncData } from '../../hooks/useAsyncData'
import { userService } from '../../services/userService'
import { useAppStore } from '../../store/appStore'
import type { ApiPermissions, UserRecord } from '../../types/api'

const permissionFields: Array<{ key: keyof ApiPermissions; label: string }> = [
  { key: 'canViewWorkOrders', label: 'View work orders' },
  { key: 'canCreateWorkOrders', label: 'Create work orders' },
  { key: 'canAssignWorkOrders', label: 'Assign work orders' },
  { key: 'canCompleteWorkOrders', label: 'Complete work orders' },
  { key: 'canViewKnowledge', label: 'View knowledge' },
  { key: 'canManageKnowledge', label: 'Manage knowledge' },
  { key: 'canViewMaterials', label: 'View materials' },
  { key: 'canRequestMaterials', label: 'Request materials' },
  { key: 'canManageMaterials', label: 'Manage materials' },
  { key: 'canApproveMaterials', label: 'Approve materials' },
  { key: 'canIssueMaterials', label: 'Issue materials' },
  { key: 'canManageAssets', label: 'Manage assets' },
  { key: 'canManageSettings', label: 'Manage settings' },
  { key: 'canViewReports', label: 'View reports' },
  { key: 'canViewFinance', label: 'View finance' },
  { key: 'canManageFinance', label: 'Manage finance' },
  { key: 'canViewFinanceRates', label: 'View rates' },
  { key: 'canManageFinanceRates', label: 'Manage rates' },
  { key: 'canViewFinanceApprovals', label: 'View approvals' },
  { key: 'canManageFinanceApprovals', label: 'Manage approvals' },
  { key: 'canViewBillableWork', label: 'View billable work' },
  { key: 'canManageBillableWork', label: 'Manage billable work' },
  { key: 'canOverrideMaterialStock', label: 'Override stock limits' },
]

export function SettingsPermissionsPage() {
  const { pushToast } = useToast()
  const { session } = useAuth()
  const [selectedUserId, setSelectedUserId] = useState('')
  const tenantData = useAppStore((database) => (session?.tenantId ? database.tenantData[session.tenantId] : undefined))
  const { data, loading, error, reload, setData } = useAsyncData<UserRecord[]>(
    (signal) => userService.list(signal),
    [],
    [],
  )

  const selectedUser = data.find((user) => user.id === selectedUserId) ?? data[0] ?? null

  async function togglePermission(key: keyof ApiPermissions, value: boolean) {
    if (!selectedUser) return

    const nextPermissions = {
      ...selectedUser.permissions,
      [key]: value,
    }

    setData((current) =>
      current.map((user) =>
        user.id === selectedUser.id
          ? {
              ...user,
              permissions: nextPermissions,
            }
          : user,
      ),
    )

    try {
      await userService.updatePermissions(selectedUser.id, nextPermissions)
      pushToast({ title: 'Permissions updated', description: `Saved permission changes for ${selectedUser.fullName}.`, tone: 'success' })
      await reload()
    } catch (error) {
      pushToast({ title: 'Update failed', description: error instanceof Error ? error.message : 'Unable to update permissions.', tone: 'danger' })
      await reload()
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Roles & permissions"
        description="Review workspace role definitions and fine-tune per-user permissions with optimistic updates and safe rollback on failure."
      />

      {tenantData ? (
        <div className="grid gap-4 xl:grid-cols-2">
          <section className="section-panel">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-xl font-semibold text-app">Role templates</h2>
              <Badge tone="info">{tenantData.settings.rolePermissions.length}</Badge>
            </div>
            <div className="mt-4 divide-y divide-[var(--app-divider)]">
              {tenantData.settings.rolePermissions.map((role) => (
                <article key={role.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-center justify-between gap-3">
                    <p className="text-sm font-semibold text-app">{role.role}</p>
                    <Badge tone={role.permissions.includes('*') ? 'success' : 'neutral'}>
                      {role.permissions.includes('*') ? 'Full access' : `${role.permissions.length} grants`}
                    </Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">{role.permissions.includes('*') ? 'Wildcard permission set for workspace administrators.' : role.permissions.join(', ')}</p>
                </article>
              ))}
            </div>
          </section>

          <section className="section-panel">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-xl font-semibold text-app">Permission groups</h2>
              <Badge tone="info">{tenantData.settings.permissionGroups.length}</Badge>
            </div>
            <div className="mt-4 divide-y divide-[var(--app-divider)]">
              {tenantData.settings.permissionGroups.map((group) => (
                <article key={group.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-center justify-between gap-3">
                    <p className="text-sm font-semibold text-app">{group.name}</p>
                    <Badge tone="neutral">{group.permissions.length} permissions</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">{group.permissions.join(', ')}</p>
                </article>
              ))}
            </div>
          </section>
        </div>
      ) : null}

      {loading ? <LoadingState label="Loading permission sets" /> : null}
      {!loading && error ? <ErrorState title="Unable to load permissions" description={error} /> : null}
      {!loading && !error && data.length === 0 ? (
        <EmptyState title="No users found" description="User records are required before permissions can be adjusted." actionLabel="Refresh" onAction={() => void reload()} />
      ) : null}

      {!loading && !error && data.length > 0 ? (
        <div className="space-y-4">
          <section className="section-panel space-y-3">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-xl font-semibold text-app">Users</h2>
              <Badge tone="info">{data.length}</Badge>
            </div>
            {data.map((user) => (
              <button
                key={user.id}
                type="button"
                className={`w-full rounded-2xl px-4 py-3 text-left transition ${selectedUser?.id === user.id ? 'bg-cyan-400/10' : 'panel-subtle hover-surface'}`}
                onClick={() => setSelectedUserId(user.id)}
              >
                <div className="flex items-center justify-between gap-3">
                  <div>
                    <p className="font-semibold text-app">{user.fullName}</p>
                    <p className="mt-1 text-xs text-muted">{user.role}</p>
                  </div>
                  <Badge tone={user.isActive ? 'success' : 'danger'}>{user.isActive ? 'Active' : 'Inactive'}</Badge>
                </div>
              </button>
            ))}
          </section>

          <section className="section-panel">
            {selectedUser ? (
              <>
                <h2 className="text-xl font-semibold text-app">{selectedUser.fullName}</h2>
                <p className="mt-2 text-sm text-muted">Role: {selectedUser.role} | Branch access: {selectedUser.hasAllBranchAccess ? 'All branches' : `${selectedUser.branchIds.length} assigned`}</p>
                <div className="mt-6 grid gap-3 md:grid-cols-2">
                  {permissionFields.map((permission) => (
                    <label key={permission.key} className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3">
                      <span className="text-sm text-app">{permission.label}</span>
                      <input
                        type="checkbox"
                        checked={selectedUser.permissions?.[permission.key] ?? false}
                        onChange={(event) => void togglePermission(permission.key, event.target.checked)}
                      />
                    </label>
                  ))}
                </div>
              </>
            ) : null}
          </section>
        </div>
      ) : null}
    </div>
  )
}
