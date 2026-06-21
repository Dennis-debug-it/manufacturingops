import { AlertTriangle, BookOpen, Boxes, Building2, CheckCircle2, ClipboardList, FileText, Package, Wallet } from 'lucide-react'
import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useShellContext } from '../../components/layout/AppShell'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { StatCard } from '../../components/ui/StatCard'
import { useAsyncData } from '../../hooks/useAsyncData'
import { dashboardService } from '../../services/dashboardService'
import { materialService } from '../../services/materialService'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import { workOrderService } from '../../services/workOrderService'
import { emptyDashboardSummary } from '../../utils/apiDefaults'
import { formatDateOnly } from '../../utils/date'

type DashboardView = {
  summary: {
    openWorkOrders: number
    closedWorkOrders: number
    overdueWorkOrders: number
    assets: number
    clients: number
    materialsLowStock: number
    unassignedWorkOrders: number
    assignedToGroup: number
    assignedToTechnicians: number
    awaitingAcceptance: number
    techniciansOnSite: number
    contractsExpiringIn90Days: number
    contractsExpiringIn30Days: number
    criticalRenewalRiskContracts: number
    expiredContracts: number
    renewalInProgressContracts: number
    workOrdersByGroup: Array<{ groupName: string; count: number }>
    technicianWorkload: Array<{ technicianName: string; activeWorkOrders: number; pendingResponses: number; onSiteCount: number }>
  }
  recentWorkOrders: Array<{
    id: string
    workOrderNumber: string
    title: string
    clientName?: string | null
    status: string
    dueDate?: string | null
  }>
  lowStockItems: Array<{
    id: string
    itemName: string
    quantityOnHand: number
    reorderLevel: number
    branchName?: string | null
  }>
  summaryUnavailable: boolean
}

export function DashboardPage() {
  const { permissions } = useAuth()
  const { selectedBranchId } = useShellContext()
  const canViewFinance = Boolean(
    permissions?.canViewFinance
    || permissions?.canManageFinance
    || permissions?.canViewFinanceRates
    || permissions?.canManageFinanceRates
    || permissions?.canViewFinanceApprovals
    || permissions?.canManageFinanceApprovals
    || permissions?.canViewBillableWork
    || permissions?.canManageBillableWork,
  )
  const { data, loading, hasLoaded, isRefreshing, error, reload } = useAsyncData<DashboardView>(
    async (signal) => {
      const [summary, workOrders, lowStockItems] = await Promise.all([
        // No silent catch â€” let errors propagate so the ErrorState renders instead of zeros.
        dashboardService.getSummary(selectedBranchId, signal),
        workOrderService.list(selectedBranchId, signal),
        materialService.list({ branchId: selectedBranchId, lowStockOnly: true, signal }),
      ])

      return {
        summary,
        recentWorkOrders: workOrders.slice(0, 6).map((item) => ({
          id: item.id,
          workOrderNumber: item.workOrderNumber,
          title: item.title,
          clientName: item.clientName,
          status: item.status,
          dueDate: item.dueDate,
        })),
        lowStockItems: lowStockItems.slice(0, 6).map((item) => ({
          id: item.id,
          itemName: item.itemName,
          quantityOnHand: item.quantityOnHand,
          reorderLevel: item.reorderLevel,
          branchName: item.branchName,
        })),
        summaryUnavailable: false,
      }
    },
    { summary: emptyDashboardSummary, recentWorkOrders: [], lowStockItems: [], summaryUnavailable: false },
    [selectedBranchId],
  )
  const financeSummary = useAsyncData(
    (signal) => canViewFinance ? tenantFinanceService.getSummary(signal) : Promise.resolve(null),
    null,
    [canViewFinance],
  )

  const emptyDashboard = useMemo(
    () =>
      data.summary.openWorkOrders === 0 &&
      data.summary.closedWorkOrders === 0 &&
      data.summary.materialsLowStock === 0 &&
      data.summary.assets === 0 &&
      data.summary.clients === 0,
    [data.summary],
  )

  return (
    <div className="min-w-0 space-y-4">
      <PageHeader
        eyebrow="Dashboard"
        title="Operational overview"
        description="Track work orders, assets, clients, and stock activity across your workspace."
        actions={
          <div className="flex flex-wrap items-center gap-3">
            {isRefreshing ? <Badge tone="neutral">Refreshing</Badge> : null}
            <Link to="/work-orders" className="button-primary">
              New work order
            </Link>
          </div>
        }
      />

      {!hasLoaded && loading ? <LoadingState label="Loading dashboard summary" /> : null}
      {hasLoaded && error ? <ErrorState title="Dashboard unavailable" description={error} /> : null}

      {hasLoaded && !error ? (
        <>
          {data.summaryUnavailable ? (
            <div className="rounded-2xl border border-amber-400/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-100">
              Dashboard summary is temporarily unavailable.
            </div>
          ) : null}
          <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
            <StatCard title="Open Work Orders" value={String(data.summary.openWorkOrders || 0)} detail="Jobs currently in progress, assigned, or pending materials." icon={ClipboardList} />
            <StatCard title="Closed Work Orders" value={String(data.summary.closedWorkOrders || 0)} detail="Completed, acknowledged, or closed work orders." icon={CheckCircle2} accent="emerald" />
            <StatCard title="Overdue" value={String(data.summary.overdueWorkOrders || 0)} detail="Open work orders that are past due." icon={AlertTriangle} accent="amber" />
            <StatCard title="Assets" value={String(data.summary.assets || 0)} detail="Active assets in the current branch scope." icon={Boxes} />
            <StatCard title="Clients" value={String(data.summary.clients || 0)} detail="Active client records in your workspace." icon={Building2} />
            <StatCard title="Low Stock" value={String(data.summary.materialsLowStock || 0)} detail="Materials at or below reorder threshold." icon={Package} accent="amber" />
          </section>

          <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-5">
            <StatCard title="Unassigned" value={String(data.summary.unassignedWorkOrders || 0)} detail="Work orders still waiting for dispatch." icon={AlertTriangle} accent="amber" />
            <StatCard title="Assigned to Group" value={String(data.summary.assignedToGroup || 0)} detail="Queued with a group but not yet dispatched to a technician." icon={Building2} />
            <StatCard title="Assigned to Technicians" value={String(data.summary.assignedToTechnicians || 0)} detail="Work orders currently routed to one or more technicians." icon={ClipboardList} />
            <StatCard title="Awaiting Acceptance" value={String(data.summary.awaitingAcceptance || 0)} detail="Technician-dispatched jobs still waiting for acknowledgement." icon={CheckCircle2} />
            <StatCard title="Technicians On Site" value={String(data.summary.techniciansOnSite || 0)} detail="Active technician arrivals that have not yet been closed out." icon={Boxes} />
          </section>

          <section className={`grid gap-4 ${canViewFinance ? 'lg:grid-cols-2' : 'lg:grid-cols-1'}`}>
            <Link to="/contracts" aria-label="Contracts" className="surface-card block transition hover:-translate-y-0.5">
              <div className="flex items-start gap-4">
                <div className="icon-accent rounded-2xl p-3">
                  <FileText className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <p className="text-lg font-semibold text-app">Contract risk</p>
                  <p className="mt-2 text-sm text-muted">
                    {data.summary.contractsExpiringIn30Days} expiring in 30 days, {data.summary.criticalRenewalRiskContracts} critical risk, {data.summary.expiredContracts} expired.
                  </p>
                </div>
              </div>
            </Link>
            <Link to="/knowledge" aria-label="Knowledge Centre" className="surface-card block transition hover:-translate-y-0.5">
              <div className="flex items-start gap-4">
                <div className="icon-accent rounded-2xl p-3">
                  <BookOpen className="h-5 w-5" />
                </div>
                <div className="min-w-0">
                  <p className="text-lg font-semibold text-app">Knowledge Centre</p>
                  <p className="mt-2 text-sm text-muted">Open SOPs, troubleshooting guides, and publish lessons learned from completed work orders.</p>
                </div>
              </div>
            </Link>
            {canViewFinance ? (
              <Link to="/finance" aria-label="Finance" className="surface-card block transition hover:-translate-y-0.5">
                <div className="flex items-start gap-4">
                  <div className="icon-accent rounded-2xl p-3">
                    <Wallet className="h-5 w-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-lg font-semibold text-app">Finance</p>
                    <p className="mt-2 text-sm text-muted">
                      {financeSummary.loading ? 'Loading finance summary...' : financeSummary.data
                        ? `${financeSummary.data.billableWorkItemsCount} billable items, ${financeSummary.data.pendingRateApprovalsCount} pending approvals, ${financeSummary.data.activePreferentialRatesCount} active rates.`
                        : 'Review preferential client rates, approval thresholds, and billable work in one place.'}
                    </p>
                  </div>
                </div>
              </Link>
            ) : null}
          </section>

          {emptyDashboard ? (
            <div className="surface-card">
              <div className="flex items-start gap-3">
                <div className="icon-accent rounded-2xl p-3">
                  <AlertTriangle className="h-5 w-5" />
                </div>
                <div>
                  <p className="text-lg font-semibold text-app">Your workspace is ready but still empty</p>
                  <p className="mt-2 text-sm text-muted">
                    No operational records have been returned yet. Create a client, asset, material item, or work order to populate the dashboard.
                  </p>
                </div>
              </div>
            </div>
          ) : null}

          <div className="grid min-w-0 gap-4 xl:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)]">
            <section className="surface-card min-w-0">
              <div className="mb-4 flex items-center justify-between gap-3">
                <div>
                  <p className="text-lg font-semibold text-app">Recent work orders</p>
                  <p className="text-sm text-muted">Latest items from the active branch scope.</p>
                </div>
                <Link to="/work-orders" className="button-secondary">
                  View all
                </Link>
              </div>
              <DataTable
                rows={data.recentWorkOrders}
                rowKey={(row) => row.id}
                pageSize={6}
                emptyTitle="No records yet"
                emptyDescription="Create a work order to start operational tracking."
                mobileCard={(row) => (
                  <div className="space-y-3 rounded-[24px] border border-app bg-subtle p-4">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0">
                        <Link to={`/work-orders/${row.id}`} className="font-semibold text-accent-strong hover:underline">
                          {row.workOrderNumber}
                        </Link>
                        <p className="mt-1 text-sm text-app">{row.title}</p>
                      </div>
                      <Badge tone={row.status === 'Completed' ? 'success' : row.status === 'Cancelled' ? 'danger' : 'info'}>{row.status}</Badge>
                    </div>
                    <div className="grid gap-3 sm:grid-cols-2">
                      <Detail label="Client" value={row.clientName || 'Unassigned client'} />
                      <Detail label="Due" value={formatDateOnly(row.dueDate || undefined)} />
                    </div>
                  </div>
                )}
                columns={[
                  {
                    key: 'number',
                    header: 'Work Order',
                    cell: (row) => (
                      <div>
                        <Link to={`/work-orders/${row.id}`} className="font-semibold text-accent-strong hover:underline">
                          {row.workOrderNumber}
                        </Link>
                        <p className="mt-1 text-xs text-muted">{row.title}</p>
                      </div>
                    ),
                  },
                  { key: 'client', header: 'Client', cell: (row) => <span>{row.clientName || 'Unassigned client'}</span> },
                  { key: 'due', header: 'Due', cell: (row) => <span>{formatDateOnly(row.dueDate || undefined)}</span> },
                  {
                    key: 'status',
                    header: 'Status',
                    cell: (row) => <Badge tone={row.status === 'Completed' ? 'success' : row.status === 'Cancelled' ? 'danger' : 'info'}>{row.status}</Badge>,
                  },
                ]}
              />
            </section>

            <section className="surface-card min-w-0">
              <div className="mb-4 flex items-center justify-between gap-3">
                <div>
                  <p className="text-lg font-semibold text-app">Low stock watchlist</p>
                  <p className="text-sm text-muted">Materials that need replenishment soon.</p>
                </div>
                <Link to="/materials" className="button-secondary">
                  Open materials
                </Link>
              </div>
              <div className="space-y-3">
                {data.lowStockItems.length === 0 ? (
                  <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">No records yet. Low-stock items will appear here once materials are added.</div>
                ) : (
                  data.lowStockItems.map((item) => (
                    <div key={item.id} className="panel-subtle rounded-2xl p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="font-semibold text-app">{item.itemName}</p>
                          <p className="mt-1 text-xs text-muted">{item.branchName || 'Global stock view'}</p>
                        </div>
                        <Badge tone="warning">Low stock</Badge>
                      </div>
                      <p className="mt-3 text-sm text-muted">
                        On hand: <span className="text-app">{item.quantityOnHand}</span> | Reorder level: <span className="text-app">{item.reorderLevel}</span>
                      </p>
                    </div>
                  ))
                )}
              </div>
            </section>

          <section className="surface-card min-w-0 xl:col-span-2">
            <div className="mb-4 flex items-center justify-between gap-3">
              <div>
                <p className="text-lg font-semibold text-app">Summary snapshot</p>
                <p className="text-sm text-muted">A quick view of current activity across your workspace.</p>
                </div>
              </div>
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Open Work</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.openWorkOrders}</p>
                  <p className="mt-2 text-sm text-muted">Current branch-scoped active jobs.</p>
                </div>
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Closed Work</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.closedWorkOrders}</p>
                  <p className="mt-2 text-sm text-muted">Completed operational history.</p>
                </div>
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Low Stock</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.materialsLowStock}</p>
                  <p className="mt-2 text-sm text-muted">Inventory items that need attention.</p>
                </div>
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Expiring in 90 Days</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.contractsExpiringIn90Days}</p>
                  <p className="mt-2 text-sm text-muted">Contracts that need renewal review soon.</p>
                </div>
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Critical Renewal Risk</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.criticalRenewalRiskContracts}</p>
                  <p className="mt-2 text-sm text-muted">Urgent contracts requiring action.</p>
                </div>
                <div className="panel-subtle rounded-2xl p-4">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Renewal In Progress</p>
                  <p className="mt-3 text-3xl font-semibold text-app">{data.summary.renewalInProgressContracts}</p>
                  <p className="mt-2 text-sm text-muted">Contracts already in active renewal motion.</p>
                </div>
              </div>
            </section>

            <section className="surface-card min-w-0">
              <div className="mb-4">
                <p className="text-lg font-semibold text-app">Work Orders by Group</p>
                <p className="text-sm text-muted">Current workload grouped by assignment queue.</p>
              </div>
              <div className="space-y-3">
                {data.summary.workOrdersByGroup.length === 0 ? (
                  <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">No group-assigned work orders yet.</div>
                ) : (
                  data.summary.workOrdersByGroup.map((item) => (
                    <div key={item.groupName} className="panel-subtle rounded-2xl p-4">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-semibold text-app">{item.groupName}</p>
                        <Badge tone="info">{item.count}</Badge>
                      </div>
                    </div>
                  ))
                )}
              </div>
            </section>

            <section className="surface-card min-w-0">
              <div className="mb-4">
                <p className="text-lg font-semibold text-app">Technician Workload</p>
                <p className="text-sm text-muted">Active assignment load and pending responses per technician.</p>
              </div>
              <div className="space-y-3">
                {data.summary.technicianWorkload.length === 0 ? (
                  <div className="panel-subtle rounded-2xl p-4 text-sm text-muted">No technician assignments yet.</div>
                ) : (
                  data.summary.technicianWorkload.map((item) => (
                    <div key={item.technicianName} className="panel-subtle rounded-2xl p-4">
                      <div className="flex items-center justify-between gap-3">
                        <p className="font-semibold text-app">{item.technicianName}</p>
                        <Badge tone="neutral">{item.activeWorkOrders} active</Badge>
                      </div>
                      <p className="mt-2 text-sm text-muted">Pending responses: {item.pendingResponses} | On site: {item.onSiteCount}</p>
                    </div>
                  ))
                )}
              </div>
            </section>
          </div>
        </>
      ) : null}

      {hasLoaded && !error ? (
        <div className="flex justify-end">
          <button type="button" className="button-secondary" onClick={() => void reload()} disabled={loading}>
            {loading ? 'Refreshing dashboard...' : 'Refresh dashboard'}
          </button>
        </div>
      ) : null}
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-app bg-app/20 px-3 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">{label}</p>
      <p className="mt-1 text-sm text-app">{value}</p>
    </div>
  )
}
