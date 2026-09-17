import { useMemo } from 'react'
import { Lock } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { AsyncPanel } from '../../shared/components/AsyncPanel'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { formatCurrency } from '../../shared/utils/format'
import { formatDate } from '../../shared/utils/dates'
import { usePermission } from '../../shared/hooks/usePermission'
import { TeamTabNav } from './components/TeamTabNav'
import { useTeamUsers } from './api'
import { capabilityLabel, useAccessCapabilities, useApprovalLimits, usePermissionGroups, type ApprovalLimitRow } from './accessApi'

export function ApprovalLimitsPage() {
  // `GET /api/access/approval-limits` calls EnsureAdminOrPermission(CanManageSettings).
  const canManage = usePermission('settings.manage')
  const limitsQuery = useApprovalLimits(canManage)
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

  const columns: DataTableColumn<ApprovalLimitRow>[] = [
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
      cell: (row) => (
        <div>
          <p className="text-slate-900">{capabilityLabel(row.capability, capabilitiesQuery.data)}</p>
          <p className="mt-0.5 font-mono text-[11px] text-slate-400">{row.capability}</p>
        </div>
      ),
    },
    {
      key: 'maximumAmount',
      header: 'Maximum',
      className: 'text-right',
      cell: (row) => <span className="font-medium text-slate-900">{formatCurrency(row.maximumAmount, row.currency)}</span>,
    },
    { key: 'createdAt', header: 'Created', cell: (row) => formatDate(row.createdAt) },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Approval Limits"
        subtitle="Monetary ceilings on sensitive approvals. Above the limit the action is refused even when the capability is granted."
      />
      <TeamTabNav />

      {canManage ? (
        <AsyncPanel
          query={limitsQuery}
          loadingLabel="Loading approval limits"
          errorTitle="Approval limits unavailable"
          errorDescription="The approval limits could not be loaded."
        >
          {(limits) => (
            <DataTable
              columns={columns}
              data={limits}
              emptyTitle="No approval limits configured"
              emptyDescription="Nobody in this workspace has a monetary ceiling, so approvals are bounded only by the capabilities each user holds. Limits are assigned per user and there is no limit editor in the app yet."
            />
          )}
        </AsyncPanel>
      ) : (
        <EmptyState
          icon={Lock}
          title="You cannot view approval limits"
          description="Viewing approval limits requires the Manage settings capability. Ask an administrator if you need access."
        />
      )}
    </div>
  )
}
