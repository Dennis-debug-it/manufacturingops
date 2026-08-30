import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Info, Lock, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { api } from '../../shared/api/apiClient'
import { endpoints } from '../../shared/api/endpoints'
import type { PagedResult } from '../../shared/types/pagination'
import { formatDateTime } from '../../shared/utils/dates'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { cn } from '../../shared/utils/cn'
import { TeamTabNav } from './components/TeamTabNav'
import { useTeamUsers } from './api'

interface AuditLogEntry {
  id: string
  userId: string | null
  action: string
  entityName: string
  entityId: string
  details: string | null
  createdAt: string
}

/**
 * There is no dedicated permission-change history endpoint. What exists is the tenant audit log
 * (`GET /api/audit-logs`), and the access-control writes land in it under three entity names:
 *
 *  - `AccessControl`  — AccessControlController: permission_group.created/updated/deleted and
 *                       user_access.groups_changed / overrides_changed / scopes_changed /
 *                       thresholds_changed.
 *  - `RolePermission` — RolePermissionsController: settings.role_permissions.updated.
 *  - `User`           — UsersController: account created / updated / activated / deactivated /
 *                       deleted, password resets and credential sends.
 *
 * Each scope is its own server-filtered, server-paged query rather than one query filtered in the
 * browser: the endpoint returns a page at a time, so filtering client-side would silently turn a
 * tenant-wide history into a page-local one.
 */
const scopes = [
  {
    id: 'access',
    label: 'Groups, scopes & limits',
    entityName: 'AccessControl',
    empty: 'No permission group, scope, or approval-limit change has been recorded for this workspace yet.',
  },
  {
    id: 'roles',
    label: 'Role matrix',
    entityName: 'RolePermission',
    empty: 'The role permission matrix has not been changed since audit logging began.',
  },
  {
    id: 'users',
    label: 'User accounts',
    entityName: 'User',
    empty: 'No user account has been created, changed, or deactivated yet.',
  },
] as const

type ScopeId = (typeof scopes)[number]['id']

export function AccessAuditHistoryPage() {
  // `GET /api/audit-logs` calls EnsureAdminOrPermission(CanViewReports).
  const canView = usePermission('reports.view')
  const [scopeId, setScopeId] = useState<ScopeId>('access')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const debouncedSearch = useDebounce(search, 300)

  const scope = scopes.find((item) => item.id === scopeId) ?? scopes[0]

  const { data, isLoading, isError } = useQuery({
    queryKey: ['audit-logs', 'access-history', scope.entityName, debouncedSearch, page, pageSize],
    queryFn: () =>
      api.get<PagedResult<AuditLogEntry>>(endpoints.auditLogs.list, {
        entityName: scope.entityName,
        search: debouncedSearch.trim() || undefined,
        page,
        pageSize,
      }),
    enabled: canView,
    staleTime: 30_000,
  })

  const usersQuery = useTeamUsers()
  const userNames = useMemo(
    () => new Map((usersQuery.data ?? []).map((user) => [user.id, user.fullName])),
    [usersQuery.data],
  )

  const columns: DataTableColumn<AuditLogEntry>[] = [
    { key: 'createdAt', header: 'When', cell: (row) => formatDateTime(row.createdAt) },
    {
      key: 'actor',
      header: 'Changed by',
      cell: (row) => {
        if (!row.userId) return <span className="text-slate-400">System</span>
        const name = userNames.get(row.userId)
        return name ? (
          <span className="text-slate-700">{name}</span>
        ) : (
          // The audit row stores only a user id. If that user has since been removed from the
          // team list there is no name to resolve, and showing the raw id is more honest than
          // pretending the actor is unknown.
          <span className="font-mono text-xs text-slate-500">{row.userId}</span>
        )
      },
    },
    { key: 'action', header: 'Action', cell: (row) => <span className="font-medium text-slate-900">{row.action}</span> },
    { key: 'details', header: 'What changed', cell: (row) => row.details ?? <span className="text-slate-400">—</span> },
    { key: 'entityId', header: 'Record', cell: (row) => <span className="font-mono text-[11px] text-slate-400">{row.entityId}</span> },
  ]

  if (!canView) {
    return (
      <div className="space-y-6">
        <PageHeader title="Audit History" subtitle="Who changed access, when, and what they changed." />
        <TeamTabNav />
        <EmptyState
          icon={Lock}
          title="You cannot view audit history"
          description="Audit history comes from the tenant audit log, which requires the View reports capability. Ask an administrator if you need access."
        />
      </div>
    )
  }

  const rows = data?.items ?? []

  return (
    <div className="space-y-6">
      <PageHeader title="Audit History" subtitle="Who changed access, when, and what they changed." />
      <TeamTabNav />

      <div className="flex items-start gap-3 rounded-2xl border border-sky-200 bg-sky-50 p-4 text-sm text-sky-950">
        <Info className="mt-0.5 h-5 w-5 shrink-0" />
        <div className="space-y-1">
          <p>
            This is the access-related slice of the tenant audit log. Each entry records{' '}
            <strong>who</strong> made the change, <strong>when</strong>, the action, the record it
            affected, and a one-line summary.
          </p>
          <p className="text-sky-900">
            It does <strong>not</strong> record previous and new values — the audit log stores a
            summary, not a before/after diff — so an entry tells you that a group&apos;s capabilities
            changed, not which capabilities were added or removed.
          </p>
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        {scopes.map((item) => (
          <button
            key={item.id}
            type="button"
            onClick={() => {
              setScopeId(item.id)
              setPage(1)
            }}
            className={cn(
              'inline-flex h-10 items-center rounded-full border px-4 text-sm font-medium transition',
              item.id === scopeId
                ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white'
                : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {item.label}
          </button>
        ))}
      </div>

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 shrink-0 text-slate-400" />
          <input
            value={search}
            onChange={(event) => {
              setSearch(event.target.value)
              setPage(1)
            }}
            placeholder="Search by action, record, or summary"
            className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
          />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Audit history unavailable' : debouncedSearch.trim() ? 'No matching entries' : 'Nothing recorded yet'}
        emptyDescription={
          isError
            ? 'The audit log could not be loaded.'
            : debouncedSearch.trim()
              ? `No ${scope.label.toLowerCase()} entry matches “${debouncedSearch.trim()}”.`
              : scope.empty
        }
        pagination={{
          page,
          pageSize,
          total: data?.totalCount ?? 0,
          onPageChange: setPage,
          onPageSizeChange: (size) => {
            setPageSize(size)
            setPage(1)
          },
        }}
      />
    </div>
  )
}
