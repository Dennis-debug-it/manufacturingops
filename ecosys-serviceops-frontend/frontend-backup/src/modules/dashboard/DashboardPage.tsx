import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../../auth/AuthContext'
import { useShellContext } from '../../components/layout/AppShell'
import { Badge } from '../../components/ui/Badge'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { MetricCard } from '../../components/ui/Workspace'
import { WorkspaceHeader } from '../../components/workspace/WorkspaceHeader'
import { ResizableWidgetGrid, type ResizableWidgetItem } from '../../components/widgets/ResizableWidgetGrid'
import { useAsyncData } from '../../hooks/useAsyncData'
import { usePersistentState } from '../../hooks/usePersistentState'
import { contractService } from '../../services/contractService'
import { dashboardService } from '../../services/dashboardService'
import { materialIssueService } from '../../services/materialIssueService'
import { materialService } from '../../services/materialService'
import { technicianJobsService } from '../../services/technicianJobsService'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import { workOrderService } from '../../services/workOrderService'
import { emptyDashboardSummary } from '../../utils/apiDefaults'
import { formatDateOnly } from '../../utils/date'
import {
  getDashboardAccessProfile,
  getDashboardViewMeta,
  getDashboardViewMode,
  isDashboardViewKey,
  type DashboardViewKey,
} from '../../utils/roles'
import type {
  ContractListItem,
  DashboardSummary,
  MaterialIssueRecord,
  MaterialItem,
  MaterialRequestRecord,
  TenantBillableWorkRecord,
  TenantFinanceOverview,
  WorkOrder,
} from '../../types/api'

type DashboardData = {
  summary: DashboardSummary
  workOrders: WorkOrder[]
  technicianJobs: WorkOrder[]
  contracts: ContractListItem[]
  materials: MaterialItem[]
  materialRequests: MaterialRequestRecord[]
  materialIssues: MaterialIssueRecord[]
  financeSummary: TenantFinanceOverview | null
  billableWork: TenantBillableWorkRecord[]
}

const DASHBOARD_VIEW_LABELS: Record<DashboardViewKey, string> = {
  operations: 'Operations',
  contracts: 'Contracts',
  materials: 'Materials',
  finance: 'Finance',
  'my-jobs': 'My Jobs',
}

function formatMoney(value: number, currency?: string | null) {
  const safeCurrency = currency?.trim()
  if (!safeCurrency) {
    return value.toLocaleString()
  }

  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency: safeCurrency,
      maximumFractionDigits: 0,
    }).format(value)
  } catch {
    return `${safeCurrency} ${value.toLocaleString()}`
  }
}

function toDate(value?: string | null) {
  return value ? new Date(value) : null
}

function isDueToday(value?: string | null) {
  const date = toDate(value)
  if (!date || Number.isNaN(date.getTime())) {
    return false
  }

  const today = new Date()
  return date.toDateString() === today.toDateString()
}

function isOverdue(value?: string | null) {
  const date = toDate(value)
  if (!date || Number.isNaN(date.getTime())) {
    return false
  }

  const today = new Date()
  today.setHours(0, 0, 0, 0)
  return date.getTime() < today.getTime()
}

function compareByDateDesc(left?: string | null, right?: string | null) {
  return (toDate(right)?.getTime() ?? 0) - (toDate(left)?.getTime() ?? 0)
}

function compareByDateAsc(left?: string | null, right?: string | null) {
  return (toDate(left)?.getTime() ?? Number.POSITIVE_INFINITY) - (toDate(right)?.getTime() ?? Number.POSITIVE_INFINITY)
}

function materialIssueDate(issue: MaterialIssueRecord) {
  return issue.issuedAt || issue.approvedAt || issue.createdAt
}

function widgetUnavailable(title: string, description: string, note?: string) {
  return (
    <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-5 text-sm text-muted">
      <p className="font-semibold text-app">{title}</p>
      <p className="mt-1 text-sm text-muted">{description}</p>
      {note ? <p className="mt-2 text-xs text-muted">{note}</p> : null}
    </div>
  )
}

function emptyListState(title: string, description: string) {
  return (
    <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-5 text-sm text-muted">
      <p className="font-semibold text-app">{title}</p>
      <p className="mt-1 text-sm text-muted">{description}</p>
    </div>
  )
}

export function DashboardPage() {
  const { session, permissions } = useAuth()
  const { selectedBranchId } = useShellContext()
  const location = useLocation()
  const navigate = useNavigate()
  const [layoutResetSignal, setLayoutResetSignal] = useState(0)
  const { value: storedView, setValue: setStoredView } = usePersistentState<DashboardViewKey>(
    'ecosys.dashboard.activeView.v1',
    'operations',
    { validate: isDashboardViewKey },
  )
  const queryValue = useMemo(() => new URLSearchParams(location.search).get('view'), [location.search])
  const dashboardAccess = useMemo(
    () => getDashboardAccessProfile(session?.role ?? 'user', { title: session?.title, permissions }),
    [permissions, session?.role, session?.title],
  )
  const requestedView = useMemo(() => (isDashboardViewKey(queryValue) ? queryValue : storedView), [queryValue, storedView])
  const activeView = dashboardAccess.allowedViews.includes(requestedView) ? requestedView : dashboardAccess.defaultView
  const activeViewMode = useMemo(() => getDashboardViewMode(activeView, dashboardAccess), [activeView, dashboardAccess])
  const viewMeta = useMemo(() => getDashboardViewMeta(activeView, activeViewMode), [activeView, activeViewMode])

  useEffect(() => {
    const nextSearch = `?view=${activeView}`
    if (location.search !== nextSearch) {
      navigate({ pathname: '/dashboard', search: nextSearch }, { replace: true })
    }
    if (storedView !== activeView) {
      setStoredView(activeView)
    }
  }, [activeView, location.search, navigate, setStoredView, storedView])

  const { data, loading, hasLoaded, isRefreshing, error, reload } = useAsyncData<DashboardData>(
    async (signal) => {
      if (dashboardAccess.focus === 'technician') {
        const [technicianJobs, materialRequests] = await Promise.all([
          technicianJobsService.list(signal),
          workOrderService.listMaterialRequests(selectedBranchId, signal),
        ])

        return {
          summary: emptyDashboardSummary,
          workOrders: [],
          technicianJobs,
          contracts: [],
          materials: [],
          materialRequests,
          materialIssues: [],
          financeSummary: null,
          billableWork: [],
        }
      }

      const financeSummaryPromise = dashboardAccess.canViewFinance
        ? tenantFinanceService.getSummary(signal)
        : Promise.resolve(null)
      const billableWorkPromise = dashboardAccess.canViewFinance
        ? tenantFinanceService.listBillableWork(signal)
        : Promise.resolve([])

      const [summary, workOrders, contracts, materials, materialRequests, materialIssues, financeSummary, billableWork] = await Promise.all([
        dashboardService.getSummary(selectedBranchId, signal),
        workOrderService.list(selectedBranchId, signal),
        contractService.list(undefined, signal),
        materialService.list({ branchId: selectedBranchId, signal }),
        workOrderService.listMaterialRequests(selectedBranchId, signal),
        materialIssueService.list(selectedBranchId, signal),
        financeSummaryPromise,
        billableWorkPromise,
      ])

      return {
        summary,
        workOrders,
        technicianJobs: [],
        contracts,
        materials,
        materialRequests,
        materialIssues,
        financeSummary,
        billableWork,
      }
    },
    {
      summary: emptyDashboardSummary,
      workOrders: [],
      technicianJobs: [],
      contracts: [],
      materials: [],
      materialRequests: [],
      materialIssues: [],
      financeSummary: null,
      billableWork: [],
    },
    [dashboardAccess.canViewFinance, dashboardAccess.focus, selectedBranchId],
  )

  const operations = useMemo(() => {
    const openWorkOrders = data.workOrders.filter((item) => item.status !== 'Completed' && item.status !== 'Cancelled')
    const dueToday = openWorkOrders.filter((item) => isDueToday(item.dueDate))
    const overdue = openWorkOrders.filter((item) => isOverdue(item.dueDate))
    const slaBreached = data.workOrders.filter((item) => item.slaResponseBreached || item.slaResolutionBreached)
    const recentWorkOrders = [...data.workOrders].sort((left, right) => compareByDateDesc(left.createdAt, right.createdAt)).slice(0, 5)
    const technicianWorkload = [...data.summary.technicianWorkload]
      .sort((left, right) => right.activeWorkOrders - left.activeWorkOrders || right.pendingResponses - left.pendingResponses || left.technicianName.localeCompare(right.technicianName))
      .slice(0, 5)
    const assignmentRadar = [
      { label: 'Unassigned', value: data.summary.unassignedWorkOrders, tone: 'warning' as const },
      { label: 'Assigned to group', value: data.summary.assignedToGroup, tone: 'neutral' as const },
      { label: 'Assigned to technicians', value: data.summary.assignedToTechnicians, tone: 'neutral' as const },
      { label: 'Awaiting acceptance', value: data.summary.awaitingAcceptance, tone: 'warning' as const },
    ]

    return {
      openWorkOrders,
      dueToday,
      overdue,
      slaBreached,
      recentWorkOrders,
      technicianWorkload,
      assignmentRadar,
    }
  }, [data.summary.awaitingAcceptance, data.summary.assignedToGroup, data.summary.assignedToTechnicians, data.summary.technicianWorkload, data.summary.unassignedWorkOrders, data.workOrders])

  const contracts = useMemo(() => {
    const active = data.contracts.filter((item) => item.status === 'Active')
    const expiringSoon = [...data.contracts]
      .filter((item) => item.status === 'ExpiringSoon' || (item.status === 'Active' && item.daysToExpiry <= 30 && item.daysToExpiry >= 0))
      .sort((left, right) => compareByDateAsc(left.endDate, right.endDate))
      .slice(0, 5)
    const expired = [...data.contracts]
      .filter((item) => item.status === 'Expired' || item.daysToExpiry < 0)
      .sort((left, right) => compareByDateDesc(left.endDate, right.endDate))
      .slice(0, 5)
    const renewalAttention = [...data.contracts]
      .filter((item) => item.renewalRisk === 'Critical' || item.renewalRisk === 'High' || item.daysToExpiry <= 30)
      .sort((left, right) => {
        const riskRank = (value: ContractListItem['renewalRisk']) => {
          if (value === 'Critical') return 0
          if (value === 'High') return 1
          if (value === 'Medium') return 2
          if (value === 'Low') return 3
          if (value === 'Expired') return 4
          return 5
        }
        return riskRank(left.renewalRisk) - riskRank(right.renewalRisk) || left.daysToExpiry - right.daysToExpiry
      })
      .slice(0, 5)

    return {
      active,
      expiringSoon,
      expired,
      renewalAttention,
      preferentialRates: dashboardAccess.canViewFinance ? data.financeSummary?.activePreferentialRatesCount ?? null : null,
    }
  }, [dashboardAccess.canViewFinance, data.contracts, data.financeSummary?.activePreferentialRatesCount])

  const materials = useMemo(() => {
    const lowStock = data.materials
      .filter((item) => item.isLowStock || item.quantityOnHand <= item.reorderLevel)
      .sort((left, right) => left.quantityOnHand - right.quantityOnHand || left.itemName.localeCompare(right.itemName))
      .slice(0, 5)

    const pendingRequests = [...data.materialRequests]
      .filter((item) => item.status.toLowerCase().includes('pending') || item.status.toLowerCase().includes('request'))
      .sort((left, right) => compareByDateDesc(left.createdAt, right.createdAt))
      .slice(0, 5)

    const issuedMaterials = [...data.materialIssues]
      .filter((item) => item.issueStatus === 'issued' || item.issueStatus === 'approved')
      .sort((left, right) => compareByDateDesc(materialIssueDate(left), materialIssueDate(right)))
      .slice(0, 5)

    const stockValue = data.materials.reduce((sum, item) => {
      if (typeof item.unitCost !== 'number') {
        return sum
      }
      return sum + (item.quantityOnHand * item.unitCost)
    }, 0)
    const stockValueAvailable = data.materials.some((item) => typeof item.unitCost === 'number')

    return {
      lowStock,
      pendingRequests,
      issuedMaterials,
      stockValue,
      stockValueAvailable,
    }
  }, [data.materialIssues, data.materialRequests, data.materials])

  const finance = useMemo(() => {
    const billableWork = [...data.billableWork]
      .sort((left, right) => right.totalAmount - left.totalAmount || (left.workOrderNumber || '').localeCompare(right.workOrderNumber || ''))
      .slice(0, 5)

    const contractValue = data.contracts.reduce((sum, item) => sum + (typeof item.contractValue === 'number' ? item.contractValue : 0), 0)
    const contractValueAvailable = data.contracts.some((item) => typeof item.contractValue === 'number')
    const materialExposure = materials.stockValue
    const materialExposureAvailable = materials.stockValueAvailable

    return {
      billableWork,
      contractValue,
      contractValueAvailable,
      materialExposure,
      materialExposureAvailable,
    }
  }, [data.billableWork, data.contracts, materials.stockValue, materials.stockValueAvailable])

  const dashboardWidgets = useMemo<ResizableWidgetItem[]>(() => {
    if (activeView === 'my-jobs') {
      const assignedJobs = [...data.technicianJobs]
        .sort((left, right) => compareByDateAsc(left.dueDate, right.dueDate) || compareByDateDesc(left.createdAt, right.createdAt))
        .slice(0, 5)
      const overdueJobs = data.technicianJobs.filter((item) => isOverdue(item.dueDate))
      const dueTodayJobs = data.technicianJobs.filter((item) => isDueToday(item.dueDate))
      const materialRequestsForJobs = data.materialRequests.filter((item) => data.technicianJobs.some((job) => job.id === item.workOrderId))

      return [
        {
          id: 'my-jobs-snapshot',
          title: 'My jobs snapshot',
          description: 'Assigned work and field urgency.',
          defaultLayout: { i: 'my-jobs-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Assigned jobs" value={String(data.technicianJobs.length)} meta="Jobs assigned to you right now." />
              <MetricCard label="Due today" value={String(dueTodayJobs.length)} meta="Jobs that need attention before day end." />
              <MetricCard label="Overdue" value={String(overdueJobs.length)} meta="Assigned jobs past their due date." emphasis="warning" />
              <MetricCard label="Material blockers" value={String(materialRequestsForJobs.filter((item) => item.status.toLowerCase().includes('pending')).length)} meta="Requests tied to your jobs." emphasis="warning" />
            </div>
          ),
        },
        {
          id: 'my-jobs-queue',
          title: 'Assigned jobs',
          description: 'Open your current job queue quickly.',
          actions: (
            <Link to="/tech" className="button-secondary h-8 px-3 py-1.5 text-xs">
              Open technician workspace
            </Link>
          ),
          defaultLayout: { i: 'my-jobs-queue', x: 0, y: 5, w: 7, h: 7 },
          content: assignedJobs.length === 0 ? (
            emptyListState('No assigned jobs', 'Your job queue will appear here once dispatch sends work to you.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {assignedJobs.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/tech/jobs/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.workOrderNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.title}</p>
                      <p className="mt-1 text-xs text-muted">{item.siteName || item.clientName || 'Field visit'}</p>
                    </div>
                    <Badge tone={isOverdue(item.dueDate) ? 'danger' : isDueToday(item.dueDate) ? 'warning' : 'info'}>
                      {isOverdue(item.dueDate) ? 'Overdue' : isDueToday(item.dueDate) ? 'Due today' : item.status}
                    </Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'my-jobs-blockers',
          title: 'Material blockers',
          description: 'Requests linked to your assigned jobs.',
          actions: (
            <Link to="/materials" className="button-secondary h-8 px-3 py-1.5 text-xs">
              Open materials
            </Link>
          ),
          defaultLayout: { i: 'my-jobs-blockers', x: 7, y: 5, w: 5, h: 7 },
          content: materialRequestsForJobs.length === 0 ? (
            emptyListState('No material blockers', 'Requests related to your jobs will appear here when they are raised.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {materialRequestsForJobs.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.requestNumber}</p>
                      <p className="mt-1 text-sm text-muted">
                        Work order <Link to={`/tech/jobs/${item.workOrderId}`} className="text-accent-strong hover:underline">{item.workOrderNumber || item.workOrderId}</Link>
                      </p>
                    </div>
                    <Badge tone="warning">{item.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
      ]
    }

    if (activeView === 'operations' && activeViewMode === 'limited') {
      return [
        {
          id: 'operations-limited-snapshot',
          title: 'Operations snapshot',
          description: 'Open work, SLA pressure, and blockers.',
          defaultLayout: { i: 'operations-limited-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
              <MetricCard label="Open work orders" value={String(operations.openWorkOrders.length)} meta="Currently active or pending close." />
              <MetricCard label="Overdue" value={String(operations.overdue.length)} meta="Open work orders past due." emphasis="warning" />
              <MetricCard label="Due today" value={String(operations.dueToday.length)} meta="Work due before the day ends." />
              <MetricCard label="SLA pressure" value={String(operations.slaBreached.length)} meta="Response or resolution breach." emphasis="warning" />
              <MetricCard label="Material blockers" value={String(materials.pendingRequests.length)} meta="Requests holding work back." emphasis="warning" />
            </div>
          ),
        },
        {
          id: 'operations-limited-work-orders',
          title: 'Open work orders',
          description: 'Top queue pressure and deadlines.',
          actions: (
            <Link to="/work-orders" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all work orders
            </Link>
          ),
          defaultLayout: { i: 'operations-limited-work-orders', x: 0, y: 5, w: 7, h: 7 },
          content: operations.openWorkOrders.length === 0 ? (
            emptyListState('No open work orders', 'New service work will appear here once it is created.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {operations.openWorkOrders.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/work-orders/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.workOrderNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.title}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unassigned client'}</p>
                    </div>
                    <Badge tone={isOverdue(item.dueDate) ? 'danger' : isDueToday(item.dueDate) ? 'warning' : 'info'}>
                      {isOverdue(item.dueDate) ? 'Overdue' : isDueToday(item.dueDate) ? 'Due today' : 'Open'}
                    </Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    Due <span className="text-app">{formatDateOnly(item.dueDate || undefined)}</span>
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'operations-limited-materials',
          title: 'Material blockers',
          description: 'Requests affecting active jobs.',
          actions: (
            <Link to="/materials" className="button-secondary h-8 px-3 py-1.5 text-xs">
              Open materials
            </Link>
          ),
          defaultLayout: { i: 'operations-limited-materials', x: 7, y: 5, w: 5, h: 7 },
          content: materials.pendingRequests.length === 0 ? (
            emptyListState('No pending material requests', 'Material requests will appear here once work orders raise demand.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {materials.pendingRequests.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.requestNumber}</p>
                      <p className="mt-1 text-sm text-muted">
                        Work order <Link to={`/work-orders/${item.workOrderId}`} className="text-accent-strong hover:underline">{item.workOrderNumber || item.workOrderId}</Link>
                      </p>
                    </div>
                    <Badge tone="warning">{item.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'operations-limited-recent',
          title: 'Recent work orders',
          description: 'Latest operational activity.',
          actions: (
            <Link to="/work-orders" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all
            </Link>
          ),
          defaultLayout: { i: 'operations-limited-recent', x: 0, y: 12, w: 12, h: 6 },
          content: operations.recentWorkOrders.length === 0 ? (
            emptyListState('No recent work orders', 'Work order activity will appear here once jobs start moving.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {operations.recentWorkOrders.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/work-orders/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.workOrderNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.title}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unassigned client'}</p>
                    </div>
                    <Badge tone={item.status === 'Completed' ? 'success' : item.status === 'Cancelled' ? 'danger' : 'info'}>{item.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
      ]
    }

    if (activeView === 'operations') {
      return [
        {
          id: 'operations-snapshot',
          title: 'Operations snapshot',
          description: 'Open work, due today, and service pressure.',
          defaultLayout: { i: 'operations-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
              <MetricCard label="Open work orders" value={String(operations.openWorkOrders.length)} meta="Currently active or pending close." />
              <MetricCard label="Overdue" value={String(operations.overdue.length)} meta="Open work orders past due." emphasis="warning" />
              <MetricCard label="Due today" value={String(operations.dueToday.length)} meta="Work due before the day ends." />
              <MetricCard label="SLA pressure" value={String(operations.slaBreached.length)} meta="Response or resolution breach." emphasis="warning" />
              <MetricCard label="Awaiting acceptance" value={String(data.summary.awaitingAcceptance)} meta="Waiting on technician acknowledgement." emphasis="warning" />
            </div>
          ),
        },
        {
          id: 'operations-work-orders',
          title: 'Open / overdue work orders',
          description: 'Top queue pressure and deadlines.',
          actions: (
            <Link to="/work-orders" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all work orders
            </Link>
          ),
          defaultLayout: { i: 'operations-work-orders', x: 0, y: 5, w: 7, h: 7 },
          content: operations.openWorkOrders.length === 0 ? (
            emptyListState('No open work orders', 'New service work will appear here once it is created.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {operations.openWorkOrders.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/work-orders/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.workOrderNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.title}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unassigned client'}</p>
                    </div>
                    <Badge tone={isOverdue(item.dueDate) ? 'danger' : isDueToday(item.dueDate) ? 'warning' : 'info'}>
                      {isOverdue(item.dueDate) ? 'Overdue' : isDueToday(item.dueDate) ? 'Due today' : 'Open'}
                    </Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    Due <span className="text-app">{formatDateOnly(item.dueDate || undefined)}</span>
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'operations-assignment',
          title: 'Assignment radar',
          description: 'Dispatch and acknowledgement pressure.',
          defaultLayout: { i: 'operations-assignment', x: 7, y: 5, w: 5, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2">
              {operations.assignmentRadar.map((item) => (
                <MetricCard
                  key={item.label}
                  label={item.label}
                  value={String(item.value)}
                  meta={item.label === 'Unassigned' ? 'Waiting for dispatch.' : item.label === 'Awaiting acceptance' ? 'Needs technician acknowledgement.' : 'Active workload routing.'}
                  emphasis={item.tone === 'warning' ? 'warning' : undefined}
                />
              ))}
            </div>
          ),
        },
        {
          id: 'operations-recent',
          title: 'Recent work orders',
          description: 'Latest operational activity.',
          actions: (
            <Link to="/work-orders" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all
            </Link>
          ),
          defaultLayout: { i: 'operations-recent', x: 0, y: 12, w: 7, h: 7 },
          content: operations.recentWorkOrders.length === 0 ? (
            emptyListState('No recent work orders', 'Work order activity will appear here once jobs start moving.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {operations.recentWorkOrders.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/work-orders/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.workOrderNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.title}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unassigned client'}</p>
                    </div>
                    <Badge tone={item.status === 'Completed' ? 'success' : item.status === 'Cancelled' ? 'danger' : 'info'}>{item.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'operations-workload',
          title: 'Technician workload',
          description: 'Active load and pending responses.',
          defaultLayout: { i: 'operations-workload', x: 7, y: 10, w: 5, h: 6 },
          content: operations.technicianWorkload.length === 0 ? (
            emptyListState('No technician load yet', 'Technician workload will appear once jobs are assigned.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {operations.technicianWorkload.map((item) => (
                <div key={item.technicianName} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-center justify-between gap-3">
                    <p className="font-semibold text-app">{item.technicianName}</p>
                    <Badge tone="neutral">{item.activeWorkOrders} active</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    Pending responses <span className="text-app">{item.pendingResponses}</span> | On site <span className="text-app">{item.onSiteCount}</span>
                  </p>
                </div>
              ))}
            </div>
          ),
        },
      ]
    }

    if (activeView === 'contracts' && activeViewMode === 'limited') {
      return [
        {
          id: 'contracts-limited-snapshot',
          title: 'Contracts snapshot',
          description: 'Lifecycle counts and renewal pressure.',
          defaultLayout: { i: 'contracts-limited-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Active contracts" value={String(contracts.active.length)} meta="Currently in force." />
              <MetricCard label="Expiring soon" value={String(contracts.expiringSoon.length)} meta="Due to expire or already in the near window." emphasis="warning" />
              <MetricCard label="Expired" value={String(contracts.expired.length)} meta="Already out of contract." emphasis="danger" />
              <MetricCard label="Critical / high risk" value={String(contracts.renewalAttention.filter((item) => item.renewalRisk === 'Critical' || item.renewalRisk === 'High').length)} meta="Needs follow-up." emphasis="warning" />
            </div>
          ),
        },
        {
          id: 'contracts-limited-renewal',
          title: 'Renewal attention',
          description: 'Contracts that need follow-up now.',
          actions: (
            <Link to="/contracts" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all contracts
            </Link>
          ),
          defaultLayout: { i: 'contracts-limited-renewal', x: 0, y: 5, w: 12, h: 7 },
          content: contracts.renewalAttention.length === 0 ? (
            emptyListState('No renewal alerts', 'Nothing is currently in the attention queue.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {contracts.renewalAttention.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.contractNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.name}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unknown client'}</p>
                    </div>
                    <Badge tone={item.renewalRisk === 'Critical' || item.renewalRisk === 'Expired' ? 'danger' : 'warning'}>{item.renewalRisk}</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    Expires <span className="text-app">{formatDateOnly(item.endDate)}</span> | {item.daysToExpiry} days
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'contracts-limited-commercial',
          title: 'Commercial coverage',
          description: 'Compact contract coverage and finance-linked context.',
          defaultLayout: { i: 'contracts-limited-commercial', x: 0, y: 12, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              <MetricCard label="Active contracts" value={String(contracts.active.length)} meta="Commercial coverage in force." />
              <MetricCard label="Preferential rates" value={dashboardAccess.canViewFinance && typeof contracts.preferentialRates === 'number' ? String(contracts.preferentialRates) : 'Hidden'} meta={dashboardAccess.canViewFinance ? 'Finance-linked rate coverage.' : 'Finance access not enabled.'} />
              <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-4 text-sm text-muted">
                <p className="font-semibold text-app">View scope</p>
                <p className="mt-1 text-sm text-muted">This limited contract view keeps the focus on renewal risk and coverage rather than full lifecycle editing.</p>
              </div>
            </div>
          ),
        },
      ]
    }

    if (activeView === 'contracts') {
      return [
        {
          id: 'contracts-snapshot',
          title: 'Contracts snapshot',
          description: 'Lifecycle counts and risk pressure.',
          defaultLayout: { i: 'contracts-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Active contracts" value={String(contracts.active.length)} meta="Currently in force." />
              <MetricCard label="Expiring soon" value={String(contracts.expiringSoon.length)} meta="Due to expire or already in the near window." emphasis="warning" />
              <MetricCard label="Expired" value={String(contracts.expired.length)} meta="Already out of contract." emphasis="danger" />
              <MetricCard label="Critical / high risk" value={String(contracts.renewalAttention.filter((item) => item.renewalRisk === 'Critical' || item.renewalRisk === 'High').length)} meta="Needs immediate follow-up." emphasis="warning" />
            </div>
          ),
        },
        {
          id: 'contracts-renewal',
          title: 'Renewal attention',
          description: 'Contracts that need follow-up now.',
          actions: (
            <Link to="/contracts" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all contracts
            </Link>
          ),
          defaultLayout: { i: 'contracts-renewal', x: 0, y: 5, w: 6, h: 7 },
          content: contracts.renewalAttention.length === 0 ? (
            emptyListState('No renewal alerts', 'Nothing is currently in the attention queue.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {contracts.renewalAttention.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.contractNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.name}</p>
                      <p className="mt-1 text-xs text-muted">{item.clientName || 'Unknown client'}</p>
                    </div>
                    <Badge tone={item.renewalRisk === 'Critical' || item.renewalRisk === 'Expired' ? 'danger' : 'warning'}>{item.renewalRisk}</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    Expires <span className="text-app">{formatDateOnly(item.endDate)}</span> | {item.daysToExpiry} days
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'contracts-expiring',
          title: 'Expiring soon',
          description: 'Near-term renewal window.',
          defaultLayout: { i: 'contracts-expiring', x: 6, y: 5, w: 6, h: 7 },
          content: contracts.expiringSoon.length === 0 ? (
            emptyListState('No contracts nearing expiry', 'Active contracts outside the renewal window are not shown here.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {contracts.expiringSoon.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.contractNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.name}</p>
                    </div>
                    <Badge tone={item.daysToExpiry <= 0 ? 'danger' : item.daysToExpiry <= 30 ? 'warning' : 'info'}>{item.daysToExpiry} days</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    <span className="text-app">{item.clientName || 'Unknown client'}</span> | {item.status}
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'contracts-active',
          title: 'Active contracts',
          description: 'Current commercial coverage.',
          actions: (
            <Link to="/contracts" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View all
            </Link>
          ),
          defaultLayout: { i: 'contracts-active', x: 0, y: 12, w: 12, h: 5 },
          content: contracts.active.length === 0 ? (
            emptyListState('No active contracts', 'Active contracts will appear here once they are approved and activated.')
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {contracts.active.slice(0, 6).map((item) => (
                <div key={item.id} className="rounded-[18px] bg-[var(--app-subtle)] px-4 py-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <Link to={`/contracts/${item.id}`} className="font-semibold text-accent-strong hover:underline">
                        {item.contractNumber}
                      </Link>
                      <p className="mt-1 line-clamp-2 text-sm text-app">{item.name}</p>
                    </div>
                    <Badge tone="success">Active</Badge>
                  </div>
                  <p className="mt-2 text-xs text-muted">{item.clientName || 'Unknown client'}</p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'contracts-preferential',
          title: 'Preferential rates coverage',
          description: 'Only shown when finance access is available.',
          defaultLayout: { i: 'contracts-preferential', x: 8, y: 12, w: 4, h: 4 },
          content: dashboardAccess.canViewFinance && typeof contracts.preferentialRates === 'number' ? (
            <div className="space-y-3">
              <MetricCard
                label="Active preferential rates"
                value={String(contracts.preferentialRates)}
                meta="Client-specific billing overrides available to finance."
              />
              <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-4 text-sm text-muted">
                This is the only finance-linked contract metric surfaced here to avoid crowding the view.
              </div>
            </div>
          ) : widgetUnavailable('Finance-linked data unavailable', 'Preferential rate coverage is only shown when finance access and summary data are available.')
          ,
        },
      ]
    }

    if (activeView === 'materials' && activeViewMode === 'limited') {
      return [
        {
          id: 'materials-limited-snapshot',
          title: 'Materials snapshot',
          description: 'Stock value and request pressure.',
          defaultLayout: { i: 'materials-limited-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Low stock" value={String(materials.lowStock.length)} meta="Below or at reorder threshold." emphasis="warning" />
              <MetricCard label="Pending requests" value={String(materials.pendingRequests.length)} meta="Awaiting store action." emphasis="warning" />
              <MetricCard label="Stock value" value={materials.stockValueAvailable ? formatMoney(materials.stockValue, data.financeSummary?.currency) : 'Unavailable'} meta={materials.stockValueAvailable ? 'Based on materials with unit cost.' : 'No unit cost data available.'} />
              <MetricCard label="Material exposure" value={dashboardAccess.canViewFinance ? formatMoney(materials.stockValue, data.financeSummary?.currency) : 'Hidden'} meta={dashboardAccess.canViewFinance ? 'Finance-linked cost visibility.' : 'Finance access not enabled.'} />
            </div>
          ),
        },
        {
          id: 'materials-limited-requests',
          title: 'Material requests',
          description: 'Requests blocking active work.',
          actions: (
            <Link to="/materials" className="button-secondary h-8 px-3 py-1.5 text-xs">
              Open materials
            </Link>
          ),
          defaultLayout: { i: 'materials-limited-requests', x: 0, y: 5, w: 7, h: 7 },
          content: materials.pendingRequests.length === 0 ? (
            emptyListState('No pending requests', 'Material requests will appear here once work orders raise demand.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {materials.pendingRequests.slice(0, 5).map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.requestNumber}</p>
                      <p className="mt-1 text-sm text-muted">
                        Work order <Link to={`/work-orders/${item.workOrderId}`} className="text-accent-strong hover:underline">{item.workOrderNumber || item.workOrderId}</Link>
                      </p>
                    </div>
                    <Badge tone="warning">{item.status}</Badge>
                  </div>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'materials-limited-stock',
          title: 'Stock watch',
          description: 'Compact low stock and issue visibility.',
          defaultLayout: { i: 'materials-limited-stock', x: 7, y: 5, w: 5, h: 7 },
          content: (
            <div className="space-y-3">
              <MetricCard label="Low stock items" value={String(materials.lowStock.length)} meta="Items at or below reorder level." emphasis="warning" />
              <MetricCard label="Issued materials" value={String(materials.issuedMaterials.length)} meta="Recent approved issues." />
              <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-4 text-sm text-muted">
                <p className="font-semibold text-app">View scope</p>
                <p className="mt-1 text-sm text-muted">This limited materials view keeps the focus on blockers and cost exposure instead of full inventory administration.</p>
              </div>
            </div>
          ),
        },
      ]
    }

    if (activeView === 'materials') {
      return [
        {
          id: 'materials-snapshot',
          title: 'Materials snapshot',
          description: 'Low stock, requests, and inventory value.',
          defaultLayout: { i: 'materials-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Low stock" value={String(materials.lowStock.length)} meta="Below or at reorder threshold." emphasis="warning" />
              <MetricCard label="Pending requests" value={String(materials.pendingRequests.length)} meta="Awaiting store action." emphasis="warning" />
              <MetricCard label="Issued materials" value={String(materials.issuedMaterials.length)} meta="Recent approved issues." />
              <MetricCard
                label="Stock value"
                value={materials.stockValueAvailable ? formatMoney(materials.stockValue, data.financeSummary?.currency) : 'Unavailable'}
                meta={materials.stockValueAvailable ? 'Based on materials with unit cost.' : 'No unit cost data available.'}
              />
            </div>
          ),
        },
        {
          id: 'materials-low-stock',
          title: 'Low stock watchlist',
          description: 'Immediate replenishment pressure.',
          actions: (
            <Link to="/materials" className="button-secondary h-8 px-3 py-1.5 text-xs">
              View materials
            </Link>
          ),
          defaultLayout: { i: 'materials-low-stock', x: 0, y: 5, w: 6, h: 7 },
          content: materials.lowStock.length === 0 ? (
            emptyListState('No low stock items', 'Low stock materials will appear here as inventory drops toward reorder levels.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {materials.lowStock.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.itemName}</p>
                      <p className="mt-1 text-xs text-muted">{item.branchName || 'Global stock view'}</p>
                    </div>
                    <Badge tone="warning">Low stock</Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted">
                    On hand <span className="text-app">{item.quantityOnHand}</span> | Reorder level <span className="text-app">{item.reorderLevel}</span>
                  </p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'materials-requests',
          title: 'Pending material requests',
          description: 'Waiting for store approval or issue.',
          defaultLayout: { i: 'materials-requests', x: 6, y: 5, w: 6, h: 7 },
          content: materials.pendingRequests.length === 0 ? (
            emptyListState('No pending requests', 'Material requests will appear here once work orders raise demand.')
          ) : (
            <div className="divide-y divide-[var(--app-divider)]">
              {materials.pendingRequests.map((item) => (
                <div key={item.id} className="py-3 first:pt-0 last:pb-0">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.requestNumber}</p>
                      <p className="mt-1 text-sm text-muted">
                        Work order <Link to={`/work-orders/${item.workOrderId}`} className="text-accent-strong hover:underline">{item.workOrderNumber || item.workOrderId}</Link>
                      </p>
                    </div>
                    <Badge tone="warning">{item.status}</Badge>
                  </div>
                  <p className="mt-2 text-xs text-muted">{item.lines.length} requested line{item.lines.length === 1 ? '' : 's'}</p>
                </div>
              ))}
            </div>
          ),
        },
        {
          id: 'materials-issued',
          title: 'Issued materials',
          description: 'Approved issues that have been moved out.',
          defaultLayout: { i: 'materials-issued', x: 0, y: 12, w: 12, h: 6 },
          content: materials.issuedMaterials.length === 0 ? (
            emptyListState('No issued materials', 'Issued material records will show here once store releases are processed.')
          ) : (
            <div className="grid gap-3 lg:grid-cols-2">
              {materials.issuedMaterials.map((item) => (
                <div key={item.id} className="rounded-[18px] bg-[var(--app-subtle)] px-4 py-3">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <p className="font-semibold text-app">{item.issueNumber}</p>
                      <p className="mt-1 text-sm text-muted">
                        {item.workOrderNumber ? <Link to={`/work-orders/${item.workOrderId || ''}`} className="text-accent-strong hover:underline">{item.workOrderNumber}</Link> : 'Unlinked issue'}
                      </p>
                    </div>
                    <Badge tone="success">{item.issueStatus}</Badge>
                  </div>
                  <p className="mt-2 text-xs text-muted">
                    Issued <span className="text-app">{formatDateOnly(materialIssueDate(item))}</span> | Cost <span className="text-app">{formatMoney(item.totalIssuedCost, data.financeSummary?.currency)}</span>
                  </p>
                </div>
              ))}
            </div>
          ),
        },
      ]
    }

    if (activeView === 'finance' && activeViewMode === 'limited') {
      return [
        {
          id: 'finance-limited-snapshot',
          title: 'Finance snapshot',
          description: 'Commercial exposure and billable pressure.',
          defaultLayout: { i: 'finance-limited-snapshot', x: 0, y: 0, w: 12, h: 5 },
          content: (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <MetricCard label="Billable items" value={String(data.financeSummary?.billableWorkItemsCount ?? finance.billableWork.length)} meta="Current billable register." />
              <MetricCard
                label="Estimated billable"
                value={formatMoney(data.financeSummary?.estimatedBillableAmount ?? finance.billableWork.reduce((sum, item) => sum + item.totalAmount, 0), data.financeSummary?.currency)}
                meta="Current estimated billable amount."
              />
              <MetricCard
                label="Contract value"
                value={finance.contractValueAvailable ? formatMoney(finance.contractValue, data.financeSummary?.currency) : 'Unavailable'}
                meta={finance.contractValueAvailable ? 'Total contract value currently recorded.' : 'No contract value data available.'}
              />
              <MetricCard
                label="Material exposure"
                value={finance.materialExposureAvailable ? formatMoney(finance.materialExposure, data.financeSummary?.currency) : 'Unavailable'}
                meta={finance.materialExposureAvailable ? 'Estimated stock value across current materials.' : 'No stock valuation data available.'}
              />
            </div>
          ),
        },
        {
          id: 'finance-limited-exposure',
          title: 'Commercial exposure',
          description: 'Compact billable pressure and contract context.',
          actions: (
            <Link to="/finance" className="button-secondary h-8 px-3 py-1.5 text-xs">
              Open finance
            </Link>
          ),
          defaultLayout: { i: 'finance-limited-exposure', x: 0, y: 5, w: 7, h: 7 },
          content: (
            <div className="space-y-3">
              <MetricCard label="Preferential rates" value={String(data.financeSummary?.activePreferentialRatesCount ?? 0)} meta="Client-specific billing overrides." />
              <MetricCard label="Pending approvals" value={String(data.financeSummary?.pendingRateApprovalsCount ?? 0)} meta="Awaiting rate or billable approval." emphasis="warning" />
              <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-4 text-sm text-muted">
                <p className="font-semibold text-app">View scope</p>
                <p className="mt-1 text-sm text-muted">This limited finance view keeps commercial context visible without surfacing the full invoice and billing workspace.</p>
              </div>
            </div>
          ),
        },
      ]
    }

    if (!dashboardAccess.canViewFinance) {
      return [
        {
          id: 'finance-unavailable',
          title: 'Finance access unavailable',
          description: 'Finance dashboard data is hidden because this account does not have finance access.',
          defaultLayout: { i: 'finance-unavailable', x: 0, y: 0, w: 12, h: 4 },
          content: widgetUnavailable('Finance access required', 'Finance view needs a finance-enabled account or role before it can display dashboard data.'),
        },
      ]
    }

    return [
      {
        id: 'finance-snapshot',
        title: 'Finance snapshot',
        description: 'Commercial exposure and billable pressure.',
        defaultLayout: { i: 'finance-snapshot', x: 0, y: 0, w: 12, h: 5 },
        content: (
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <MetricCard label="Billable items" value={String(data.financeSummary?.billableWorkItemsCount ?? finance.billableWork.length)} meta="Current billable register." />
            <MetricCard
              label="Estimated billable"
              value={formatMoney(data.financeSummary?.estimatedBillableAmount ?? finance.billableWork.reduce((sum, item) => sum + item.totalAmount, 0), data.financeSummary?.currency)}
              meta="Current estimated billable amount."
            />
            <MetricCard
              label="Contract value"
              value={finance.contractValueAvailable ? formatMoney(finance.contractValue, data.financeSummary?.currency) : 'Unavailable'}
              meta={finance.contractValueAvailable ? 'Total contract value currently recorded.' : 'No contract value data available.'}
            />
            <MetricCard
              label="Material exposure"
              value={finance.materialExposureAvailable ? formatMoney(finance.materialExposure, data.financeSummary?.currency) : 'Unavailable'}
              meta={finance.materialExposureAvailable ? 'Estimated stock value across current materials.' : 'No stock valuation data available.'}
            />
          </div>
        ),
      },
      {
        id: 'finance-billable',
        title: 'Billable work',
        description: 'Top billable items from the current register.',
        actions: (
          <Link to="/finance/billable-work" className="button-secondary h-8 px-3 py-1.5 text-xs">
            View billable work
          </Link>
        ),
        defaultLayout: { i: 'finance-billable', x: 0, y: 5, w: 7, h: 7 },
        content: finance.billableWork.length === 0 ? (
          emptyListState('No billable work yet', 'Billable items will appear once work orders capture chargeable work or materials.')
        ) : (
          <div className="divide-y divide-[var(--app-divider)]">
            {finance.billableWork.map((item) => (
              <div key={`${item.workOrderId}-${item.description}-${item.itemType}`} className="py-3 first:pt-0 last:pb-0">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <Link to={`/work-orders/${item.workOrderId}`} className="font-semibold text-accent-strong hover:underline">
                      {item.workOrderNumber || 'Linked work order'}
                    </Link>
                    <p className="mt-1 line-clamp-2 text-sm text-app">{item.description}</p>
                    <p className="mt-1 text-xs text-muted">{item.clientName || 'Unknown client'} | {item.itemType}</p>
                  </div>
                  <Badge tone={item.preferentialRateApplied ? 'success' : 'neutral'}>{formatMoney(item.totalAmount, item.currency)}</Badge>
                </div>
              </div>
            ))}
          </div>
        ),
      },
      {
        id: 'finance-exposure',
        title: 'Commercial exposure',
        description: 'Contract and material value summary.',
        actions: (
          <Link to="/finance" className="button-secondary h-8 px-3 py-1.5 text-xs">
            Open finance
          </Link>
        ),
        defaultLayout: { i: 'finance-exposure', x: 7, y: 5, w: 5, h: 7 },
        content: (
          <div className="space-y-3">
            <MetricCard
              label="Preferential rates"
              value={String(data.financeSummary?.activePreferentialRatesCount ?? 0)}
              meta="Active client-specific billing overrides."
            />
            <MetricCard
              label="Pending approvals"
              value={String(data.financeSummary?.pendingRateApprovalsCount ?? 0)}
              meta="Awaiting rate or billable approval."
              emphasis="warning"
            />
            <div className="rounded-[18px] border border-dashed border-[var(--app-border)] px-4 py-4 text-sm text-muted">
              <p className="font-semibold text-app">View scope</p>
              <p className="mt-1 text-sm text-muted">This view stays compact on purpose. Use the finance module pages for invoicing, approvals, and rate maintenance.</p>
            </div>
          </div>
        ),
      },
      {
        id: 'finance-risk',
        title: 'Contract value and revenue pressure',
        description: 'Useful commercial context from the current workspace.',
        defaultLayout: { i: 'finance-risk', x: 0, y: 12, w: 12, h: 5 },
        content: (
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <MetricCard label="Active contracts" value={String(contracts.active.length)} meta="Commercial coverage in force." />
            <MetricCard label="Renewal attention" value={String(contracts.renewalAttention.length)} meta="Contracts needing follow-up." emphasis="warning" />
            <MetricCard
              label="Expired contracts"
              value={String(contracts.expired.length)}
              meta="Already outside contract terms."
              emphasis="danger"
            />
            <MetricCard
              label="Materials stock value"
              value={materials.stockValueAvailable ? formatMoney(materials.stockValue, data.financeSummary?.currency) : 'Unavailable'}
              meta={materials.stockValueAvailable ? 'Inventory valuation based on unit cost.' : 'No unit cost data available.'}
            />
          </div>
        ),
      },
    ]
  }, [activeView, activeViewMode, dashboardAccess.canViewFinance, contracts, data, finance, materials, operations])

  const headerTitle = viewMeta.title
  const headerDescription = viewMeta.description

  return (
    <div className="min-w-0 space-y-3">
      <WorkspaceHeader
        crumbs={[{ label: 'Home', to: '/dashboard' }, { label: 'Dashboard' }]}
        title={headerTitle}
        description={headerDescription}
        actions={(
          <div className="flex flex-wrap items-center gap-2">
            {isRefreshing ? <Badge tone="neutral">Refreshing</Badge> : null}
            <button type="button" className="button-secondary h-9 px-3 py-2 text-xs" onClick={() => void reload()}>
              Refresh
            </button>
            <Link to="/work-orders" className="button-primary h-9 px-3 py-2 text-xs">
              New work order
            </Link>
          </div>
        )}
        onResetLayout={() => setLayoutResetSignal((current) => current + 1)}
      />

      <div className="workspace-shell-strip flex flex-col gap-2 rounded-[16px] border border-app px-3 py-2.5 md:flex-row md:items-center md:justify-between">
        <div className="min-w-0">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted">Dashboard view</p>
          <p className="mt-1 text-sm text-muted">Only the selected command view is shown.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          {dashboardAccess.allowedViews.map((view) => {
            const selected = view === activeView
            return (
              <button
                key={view}
                type="button"
                aria-pressed={selected}
                onClick={() => {
                  setStoredView(view)
                  navigate({ pathname: '/dashboard', search: `?view=${view}` }, { replace: true })
                }}
                className={`rounded-full border px-3 py-1.5 text-xs font-semibold transition ${
                  selected
                    ? 'border-[var(--app-primary)] bg-[var(--app-primary-soft)] text-app'
                    : 'border-[var(--app-divider)] bg-[var(--app-surface-strong)] text-muted hover:text-app'
                }`}
              >
                {DASHBOARD_VIEW_LABELS[view]}
              </button>
            )
          })}
        </div>
      </div>

      {!hasLoaded && loading ? <LoadingState label="Loading dashboard summary" /> : null}
      {hasLoaded && error ? <ErrorState title="Dashboard unavailable" description={error} /> : null}

      {hasLoaded && !error ? (
        <ResizableWidgetGrid
          storageKey={`ecosys.dashboard.${activeView}.layout.v1`}
          legacyStorageKeys={activeView === 'operations' ? ['ecosys.dashboard.layout.v1'] : []}
          items={dashboardWidgets}
          className="min-h-[64dvh]"
          resetSignal={layoutResetSignal}
        />
      ) : null}
    </div>
  )
}
