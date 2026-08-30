import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertTriangle, Funnel, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SlaBadge } from '../../shared/components/SlaBadge'
import { useAuth } from '../../shared/hooks/useAuth'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatRelative } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import toast from 'react-hot-toast'
import { useAssignWorkOrderById, useCompleteWorkOrderById, useStartWorkOrderById, useWorkOrders, useWorkOrdersPaged } from './api'
import { NewWorkOrderForm } from './components/NewWorkOrderForm'
import { ReportQueueTab } from './components/ReportQueueTab'
import { ViewSwitcher } from './components/ViewSwitcher'
import { FilterPanel } from './components/FilterPanel'
import { KanbanView } from './components/KanbanView'
import { CalendarView } from './components/CalendarView'
import { MapView } from './components/MapView'
import { BulkActionBar } from './components/BulkActionBar'
import { QuickActionsMenu } from './components/QuickActionsMenu'
import { AssignTechnicianDialog } from './components/AssignTechnicianDialog'
import { CompleteWorkOrderDialog } from './components/CompleteWorkOrderDialog'
import { useWorkOrderListState } from './hooks/useWorkOrderListState'
import { normalizeWorkOrders, toWorkOrderListFilters, type WorkOrderRow } from './listModel'

export type { WorkOrderRow }

export function WorkOrdersPage() {
  const { user } = useAuth()
  const canCreate = usePermission('work-orders.create')
  const [tab, setTab] = useState<'all' | 'queue'>('all')
  const [showNewForm, setShowNewForm] = useState(false)
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [assignTarget, setAssignTarget] = useState<WorkOrderRow | null>(null)
  const [completeTarget, setCompleteTarget] = useState<WorkOrderRow | null>(null)

  const { state, update, setView, clearFilters, activeFilterCount } = useWorkOrderListState()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)

  // The search box drives a server-side `search` param, so it is debounced rather than typed
  // straight into a query key.
  const debouncedSearch = useDebounce(state.search, 300)
  const filters = useMemo(
    () => toWorkOrderListFilters({ ...state, search: debouncedSearch }),
    [state, debouncedSearch],
  )
  const filterSignature = JSON.stringify(filters)
  useEffect(() => setPage(1), [filterSignature])

  // EC-P2-07: the table pages at the database; the board/calendar/map views operate over the whole
  // filtered set (grouping, month layout, marker clustering) and so keep the unpaged hook. Only the
  // view that is actually on screen fetches.
  const isTableView = state.view === 'table'
  const pagedQuery = useWorkOrdersPaged(filters, page, pageSize, { enabled: tab === 'all' && isTableView })
  const listQuery = useWorkOrders(filters, { enabled: tab === 'all' && !isTableView })
  const { data, isLoading, isError } = isTableView ? pagedQuery : listQuery

  const assignMutation = useAssignWorkOrderById()
  const startMutation = useStartWorkOrderById()
  const completeMutation = useCompleteWorkOrderById()

  const rows = useMemo(() => {
    const normalized = normalizeWorkOrders(data)
    if (user?.role === 'technician') {
      return normalized.filter((row) => row.assignedTo === user.fullName || row.assignedTo === '—')
    }
    return normalized
  }, [data, user])

  const selectedRows = useMemo(() => rows.filter((row) => selectedIds.includes(row.id)), [rows, selectedIds])

  const columns: DataTableColumn<WorkOrderRow>[] = [
    {
      key: 'woNo',
      header: 'WO No',
      cell: (row) => (
        <span className="inline-flex items-center gap-2">
          <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.id}`}>{row.woNo}</Link>
          {row.childWorkOrderCount > 0 ? (
            <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600" title={`${row.childWorkOrderCount} sub-task(s)`}>
              {row.childWorkOrderCount} sub-task{row.childWorkOrderCount > 1 ? 's' : ''}
            </span>
          ) : null}
          {row.isRepeatVisit ? (
            <span
              className="inline-flex items-center rounded-full bg-amber-50 p-1 text-amber-600 ring-1 ring-inset ring-amber-200"
              title={`Repeat visit — ${row.repeatVisitCount} jobs on this asset in the last 30 days`}
            >
              <AlertTriangle className="h-3 w-3" />
            </span>
          ) : null}
        </span>
      ),
    },
    { key: 'client', header: 'Client', cell: (row) => row.client },
    { key: 'site', header: 'Site', cell: (row) => row.site },
    { key: 'asset', header: 'Asset', cell: (row) => row.asset },
    { key: 'type', header: 'Type', cell: (row) => row.type },
    { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
    {
      key: 'status',
      header: 'Status',
      cell: (row) => (
        <span title={row.holdReason ? `${row.holdReason}${row.holdNote ? ` — ${row.holdNote}` : ''}` : undefined}>
          <StatusBadge status={row.status} />
        </span>
      ),
    },
    {
      key: 'sla',
      header: 'SLA',
      cell: (row) => (
        <SlaBadge
          deadline={row.slaResolutionDeadline}
          breached={row.slaResponseBreached || row.slaResolutionBreached}
        />
      ),
    },
    { key: 'assignedTo', header: 'Assigned To', cell: (row) => row.assignedTo },
    { key: 'dueDate', header: 'Due Date', cell: (row) => (row.dueDate && row.dueDate !== '—' ? formatDate(row.dueDate) : '—') },
    { key: 'lastUpdate', header: 'Last Update', cell: (row) => (row.lastUpdate && row.lastUpdate !== '—' ? formatRelative(row.lastUpdate) : '—') },
    {
      key: 'actions',
      header: '',
      className: 'w-10',
      cell: (row) => (
        <QuickActionsMenu
          row={row}
          onAssign={setAssignTarget}
          onStart={(target) =>
            startMutation.mutate(target.id, {
              onSuccess: () => toast.success(`${target.woNo} started`),
            })
          }
          onComplete={setCompleteTarget}
        />
      ),
    },
  ]

  // Switching view now switches which query runs, so the loading and error states render inside the
  // results slot instead of replacing the page — the tabs, search box and filters stay put.
  const resultsError = (
    <EmptyState
      title="Work orders unavailable"
      description="The work order list could not be loaded."
      actionLabel="Reload"
      onAction={() => window.location.reload()}
    />
  )

  return (
    <div className={cn('space-y-6', user?.role === 'technician' ? 'pb-20' : '')}>
      <PageHeader
        title="Work Orders"
        subtitle="Operational backlog, execution, and service reporting."
        primaryAction={
          canCreate ? (
            <button
              type="button"
              onClick={() => setShowNewForm(true)}
              className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]"
            >
              <Plus className="h-4 w-4" />New Work Order
            </button>
          ) : undefined
        }
      />

      <NewWorkOrderForm open={showNewForm} onClose={() => setShowNewForm(false)} />

      <div className="flex flex-wrap gap-2">
        {[
          ['all', 'All Work Orders'],
          ['queue', 'Report Queue'],
        ].map(([key, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key as 'all' | 'queue')}
            className={cn(
              'inline-flex h-11 items-center gap-2 rounded-full border px-4 text-sm font-medium transition',
              tab === key ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'all' ? (
        <div className="flex flex-wrap items-center gap-3">
          <div className="min-w-[16rem] flex-1 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <Funnel className="h-4 w-4 text-slate-400" />
              <input
                value={state.search}
                onChange={(event) => update({ search: event.target.value })}
                placeholder="Search by WO number, title, client, asset, or technician"
                className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
              />
            </label>
          </div>
          <ViewSwitcher value={state.view} onChange={setView} />
        </div>
      ) : null}

      {tab === 'all' ? (
        <FilterPanel state={state} onChange={update} onClear={clearFilters} activeFilterCount={activeFilterCount} />
      ) : null}

      {tab === 'all' ? (
        isError ? (
          resultsError
        ) : !isTableView && isLoading ? (
          <Spinner label="Loading work orders" />
        ) : state.view === 'table' ? (
          <>
            <DataTable
              columns={columns}
              data={rows}
              loading={isLoading}
              emptyTitle={activeFilterCount > 0 || state.search ? 'No matching work orders' : 'No work orders yet'}
              emptyDescription={
                activeFilterCount > 0 || state.search
                  ? 'No work orders match the current search and filters. Clear them to see the full backlog.'
                  : 'Raise the first work order to start tracking jobs, technicians, and service reports.'
              }
              emptyActionLabel={activeFilterCount > 0 || state.search ? 'Clear filters' : canCreate ? 'New Work Order' : undefined}
              onEmptyAction={
                activeFilterCount > 0 || state.search
                  ? () => {
                      clearFilters()
                      update({ search: '' })
                    }
                  : canCreate
                    ? () => setShowNewForm(true)
                    : undefined
              }
              pagination={{
                page,
                pageSize,
                total: pagedQuery.data?.totalCount ?? 0,
                onPageChange: setPage,
                onPageSizeChange: (size) => {
                  setPageSize(size)
                  setPage(1)
                },
              }}
              batchSelect
              onSelectionChange={setSelectedIds}
            />
            <BulkActionBar selectedRows={selectedRows} onClearSelection={() => setSelectedIds([])} />
          </>
        ) : state.view === 'kanban' ? (
          <KanbanView rows={rows} />
        ) : state.view === 'calendar' ? (
          <CalendarView rows={rows} />
        ) : (
          <MapView rows={rows} />
        )
      ) : (
        <ReportQueueTab />
      )}

      <AssignTechnicianDialog
        open={Boolean(assignTarget)}
        title={assignTarget ? `Assign ${assignTarget.woNo}` : 'Assign technician'}
        submitting={assignMutation.isPending}
        onClose={() => setAssignTarget(null)}
        onSubmit={(technicianId, notes) => {
          if (!assignTarget) return
          assignMutation.mutate(
            { id: assignTarget.id, technicianId, notes },
            {
              onSuccess: () => {
                toast.success(`${assignTarget.woNo} assigned`)
                setAssignTarget(null)
              },
            },
          )
        }}
      />

      <CompleteWorkOrderDialog
        open={Boolean(completeTarget)}
        woNumber={completeTarget?.woNo}
        submitting={completeMutation.isPending}
        arrivalRecorded={completeTarget?.hasRecordedArrival ?? false}
        canAdminOverride={user?.role === 'admin'}
        onClose={() => setCompleteTarget(null)}
        onSubmit={({ workDoneNotes, adminOverride, overrideReason }) => {
          if (!completeTarget) return
          completeMutation.mutate(
            { id: completeTarget.id, workDoneNotes, adminOverride, overrideReason },
            {
              onSuccess: () => {
                toast.success(`${completeTarget.woNo} completed`)
                setCompleteTarget(null)
              },
            },
          )
        }}
      />
    </div>
  )
}
