import { useMemo } from 'react'
import { Lock } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { AsyncPanel } from '../../shared/components/AsyncPanel'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { Badge } from '../../shared/components/Badge'
import { formatDate } from '../../shared/utils/dates'
import { usePermission } from '../../shared/hooks/usePermission'
import { TeamTabNav } from './components/TeamTabNav'
import { useTeamUsers } from './api'
import { capabilityLabel, useAccessCapabilities, useAccessScopes, usePermissionGroups, type AccessScopeRow } from './accessApi'

export function AccessScopesPage() {
  // `GET /api/access/scopes` calls EnsureAdminOrPermission(CanManageSettings) — without it the
  // request is a guaranteed 403, so don't fire it.
  const canManage = usePermission('settings.manage')
  const scopesQuery = useAccessScopes(canManage)
  const usersQuery = useTeamUsers()
  const groupsQuery = usePermissionGroups(canManage)
  const capabilitiesQuery = useAccessCapabilities(canManage)

  const userNames = useMemo(
    () => new Map((usersQuery.data ?? []).map((user) => [user.id, user.fullName])),
    [usersQuery.data],
  )
  const groupNames = useMemo(
    () => new Map((groupsQuery.data ?? []).map((group) => [group.id, group.name])),
    [groupsQuery.data],
  )

  const columns: DataTableColumn<AccessScopeRow>[] = [
    {
      key: 'subject',
      header: 'Applies to',
      cell: (row) => {
        if (row.userId) {
          return (
            <div>
              <p className="font-medium text-slate-900">{userNames.get(row.userId) ?? 'Unknown user'}</p>
              <p className="mt-0.5 font-mono text-[11px] text-slate-400">{row.userId}</p>
            </div>
          )
        }
        if (row.permissionGroupId) {
          return (
            <div>
              <p className="font-medium text-slate-900">{groupNames.get(row.permissionGroupId) ?? 'Unknown group'}</p>
              <p className="mt-0.5 text-xs text-slate-500">Permission group</p>
            </div>
          )
        }
        return <span className="text-slate-700">Whole workspace</span>
      },
    },
    {
      key: 'capability',
      header: 'Capability',
      cell: (row) =>
        row.capability === '*' ? (
          <Badge tone="info">All capabilities</Badge>
        ) : (
          <div>
            <p className="text-slate-900">{capabilityLabel(row.capability, capabilitiesQuery.data)}</p>
            <p className="mt-0.5 font-mono text-[11px] text-slate-400">{row.capability}</p>
          </div>
        ),
    },
    { key: 'scopeType', header: 'Scope', cell: (row) => <span className="text-slate-700">{row.scopeType}</span> },
    {
      key: 'scopeEntityId',
      header: 'Scoped to',
      cell: (row) => (row.scopeEntityId ? <span className="font-mono text-xs">{row.scopeEntityId}</span> : <span className="text-slate-400">—</span>),
    },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Access Scopes"
        subtitle="Where each granted capability applies — the whole organisation, a branch or region, a client or site, or only the user's own work."
      />
      <TeamTabNav />

      {canManage ? (
        <AsyncPanel
          query={scopesQuery}
          loadingLabel="Loading access scopes"
          errorTitle="Access scopes unavailable"
          errorDescription="The access scopes could not be loaded."
        >
          {(scopes) => (
            <DataTable
              columns={columns}
              data={scopes}
              emptyTitle="No access scopes configured"
              emptyDescription="Nobody in this workspace has a narrowed scope, so every granted capability applies across the whole organisation. Scopes are assigned per user and there is no scope editor in the app yet."
            />
          )}
        </AsyncPanel>
      ) : (
        <EmptyState
          icon={Lock}
          title="You cannot view access scopes"
          description="Viewing access scopes requires the Manage settings capability. Ask an administrator if you need access."
        />
      )}
    </div>
  )
}
