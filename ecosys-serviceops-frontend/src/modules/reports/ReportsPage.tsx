import {
  Activity,
  AlertTriangle,
  BarChart3,
  Filter,
  CheckCircle2,
  ClipboardList,
  Clock,
  Download,
  Settings2,
  ShieldCheck,
  Users,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { useAuth } from '../../auth/AuthContext'
import { KipButton } from '../../components/kip/KipButton'
import { KipPanel } from '../../components/kip/KipPanel'
import { Badge } from '../../components/ui/Badge'
import type { DataTableColumn } from '../../components/ui/DataTable'
import { DataTable } from '../../components/ui/DataTable'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { StatCard } from '../../components/ui/StatCard'
import { useAsyncData } from '../../hooks/useAsyncData'
import { assetCategoryService } from '../../services/assetCategoryService'
import { clientService } from '../../services/clientService'
import type {
  AssetReliabilityReport,
  PmComplianceReport,
  ReportFilters,
  TechnicianProductivityReport,
  WorkOrderPerformanceReport,
} from '../../services/reportService'
import { reportService } from '../../services/reportService'
import { useToast } from '../../components/ui/ToastProvider'
import { siteService } from '../../services/siteService'
import type { ClientRecord, SiteRecord } from '../../types/api'
import type { AssetCategoryRecord } from '../../services/assetCategoryService'
import { formatDateOnly } from '../../utils/date'

type ReportTab = 'wo-performance' | 'technician' | 'asset-reliability' | 'pm-compliance'

const TABS: Array<{ id: ReportTab; label: string; icon: typeof BarChart3 }> = [
  { id: 'wo-performance', label: 'Work Order Performance', icon: BarChart3 },
  { id: 'technician', label: 'Technician Productivity', icon: Users },
  { id: 'asset-reliability', label: 'Asset Reliability', icon: Activity },
  { id: 'pm-compliance', label: 'PM Compliance', icon: ShieldCheck },
]

function pct(n: number) {
  return `${n.toFixed(1)}%`
}

function hrs(n: number) {
  return `${n.toFixed(1)}h`
}

function today() {
  return new Date().toISOString().slice(0, 10)
}

function thirtyDaysAgo() {
  const d = new Date()
  d.setDate(d.getDate() - 30)
  return d.toISOString().slice(0, 10)
}

function ReportsFilterBar({
  activeTab,
  filters,
  onChange,
  clients,
  sites,
  categories,
}: {
  activeTab: ReportTab
  filters: ReportFilters
  onChange: (f: ReportFilters) => void
  clients: ClientRecord[]
  sites: SiteRecord[]
  categories: AssetCategoryRecord[]
}) {
  const showSite = activeTab === 'asset-reliability' || activeTab === 'pm-compliance'
  const showCategory = activeTab === 'asset-reliability' || activeTab === 'pm-compliance'
  const showStatus = activeTab === 'wo-performance'
  const showPriority = activeTab === 'wo-performance'

  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
      <div className="flex items-center gap-2">
        <Settings2 className="h-4 w-4 text-muted" />
        <span className="text-xs font-semibold text-muted uppercase tracking-[0.18em]">Filter</span>
      </div>
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-6">
        <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
          From
          <input
            type="date"
            value={filters.dateFrom ?? ''}
            onChange={(e) => onChange({ ...filters, dateFrom: e.target.value || undefined })}
            className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
          To
          <input
            type="date"
            value={filters.dateTo ?? ''}
            onChange={(e) => onChange({ ...filters, dateTo: e.target.value || undefined })}
            className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
          Client
          <select
            value={filters.clientId ?? ''}
            onChange={(e) => onChange({ ...filters, clientId: e.target.value || undefined, siteId: undefined })}
            className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
          >
            <option value="">All clients</option>
            {clients.map((client) => (
              <option key={client.id} value={client.id}>
                {client.clientName}
              </option>
            ))}
          </select>
        </label>
        {showSite && (
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
            Site
            <select
              value={filters.siteId ?? ''}
              onChange={(e) => onChange({ ...filters, siteId: e.target.value || undefined })}
              className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
            >
              <option value="">All sites</option>
              {sites.map((site) => (
                <option key={site.id} value={site.id}>
                  {site.siteName}
                </option>
              ))}
            </select>
          </label>
        )}
        {showCategory && (
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
            Category
            <select
              value={filters.categoryId ?? ''}
              onChange={(e) => onChange({ ...filters, categoryId: e.target.value || undefined })}
              className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
            >
              <option value="">All categories</option>
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>
        )}
        {showStatus && (
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
            Status
            <select
              value={filters.status ?? ''}
              onChange={(e) => onChange({ ...filters, status: e.target.value || undefined })}
              className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
            >
              <option value="">All statuses</option>
              <option value="Open">Open</option>
              <option value="In Progress">In Progress</option>
              <option value="Completed">Completed</option>
              <option value="Cancelled">Cancelled</option>
            </select>
          </label>
        )}
        {showPriority && (
          <label className="flex flex-col gap-1 text-xs font-semibold uppercase tracking-[0.14em] text-muted">
            Priority
            <select
              value={filters.priority ?? ''}
              onChange={(e) => onChange({ ...filters, priority: e.target.value || undefined })}
              className="input-field h-10 min-w-0 text-sm font-normal normal-case tracking-normal"
            >
              <option value="">All priorities</option>
              <option value="Critical">Critical</option>
              <option value="High">High</option>
              <option value="Medium">Medium</option>
              <option value="Low">Low</option>
            </select>
          </label>
        )}
      </div>
      <div className="flex items-center justify-end">
        <button
          type="button"
          className="button-secondary"
          onClick={() =>
            onChange({
              dateFrom: thirtyDaysAgo(),
              dateTo: today(),
            })
          }
        >
          <Filter className="h-4 w-4" />
          Reset filters
        </button>
      </div>
    </div>
  )
}

function ExportButton({ onClick, disabled }: { onClick: () => void; disabled?: boolean }) {
  return (
    <button type="button" className="button-secondary" onClick={onClick} disabled={disabled}>
      <Download className={`h-4 w-4 ${disabled ? 'animate-bounce' : ''}`} />
      {disabled ? 'Exporting…' : 'Export CSV'}
    </button>
  )
}

function WoPerformanceReport({ filters }: { filters: ReportFilters }) {
  const { data, loading, error, reload } = useAsyncData<WorkOrderPerformanceReport | null>(
    (signal) => reportService.getWorkOrderPerformance(filters, signal),
    null,
    [filters.dateFrom, filters.dateTo, filters.branchId, filters.clientId, filters.status, filters.priority],
  )

  const { pushToast } = useToast()
  const [exporting, setExporting] = useState(false)

  async function handleExport() {
    setExporting(true)
    try {
      await reportService.exportWorkOrderPerformance(filters)
      pushToast({ title: 'Export ready', description: 'Work order performance CSV downloaded.', tone: 'success' })
    } catch (exportError) {
      pushToast({ title: 'Export failed', description: exportError instanceof Error ? exportError.message : 'Unable to export. Please try again.', tone: 'danger' })
    } finally {
      setExporting(false)
    }
  }

  if (loading && !data) return <LoadingState />
  if (error) return <ErrorState message={error} onRetry={reload} />

  const s = data?.summary

  const byStatusCols: DataTableColumn<{ status: string; count: number }>[] = [
    { key: 'status', header: 'Status', cell: (r) => r.status },
    { key: 'count', header: 'Count', cell: (r) => r.count },
  ]

  const byDayCols: DataTableColumn<{ date: string; created: number; completed: number }>[] = [
    { key: 'date', header: 'Date', cell: (r) => formatDateOnly(r.date) },
    { key: 'created', header: 'Created', cell: (r) => r.created },
    { key: 'completed', header: 'Completed', cell: (r) => r.completed },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-end">
        <ExportButton onClick={handleExport} disabled={exporting} />
      </div>

      {s && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Total</p>
            <p className="text-2xl font-semibold text-app">{s.total}</p>
          </div>
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Completed</p>
            <p className="text-2xl font-semibold text-app">{s.completed}</p>
          </div>
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Open</p>
            <p className="text-2xl font-semibold text-app">{s.open}</p>
          </div>
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Cancelled</p>
            <p className="text-2xl font-semibold text-app">{s.cancelled}</p>
          </div>
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">On-Time Rate</p>
            <p className="text-2xl font-semibold text-app">{pct(s.onTimeRate)}</p>
          </div>
          <div className="surface-card flex flex-col gap-1 p-4">
            <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Avg. Time</p>
            <p className="text-2xl font-semibold text-app">{hrs(s.avgCompletionHours)}</p>
          </div>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="surface-card space-y-3 p-4">
          <h3 className="text-sm font-semibold text-app">By Status</h3>
          <DataTable
            columns={byStatusCols}
            rows={data?.byStatus ?? []}
            rowKey={(r) => r.status}
            pageSize={0}
            minTableWidth="min-w-[300px] w-full"
          />
        </div>

        <div className="surface-card space-y-3 p-4">
          <h3 className="text-sm font-semibold text-app">By Priority</h3>
          <DataTable
            columns={[
              { key: 'priority', header: 'Priority', cell: (r) => r.priority },
              { key: 'count', header: 'Count', cell: (r) => r.count },
            ]}
            rows={data?.byPriority ?? []}
            rowKey={(r) => r.priority}
            pageSize={0}
            minTableWidth="min-w-[300px] w-full"
          />
        </div>
      </div>

      <div className="surface-card space-y-3 p-4">
        <h3 className="text-sm font-semibold text-app">Daily Trend</h3>
        <DataTable
          columns={byDayCols}
          rows={data?.byDay ?? []}
          rowKey={(r) => r.date}
          pageSize={14}
          minTableWidth="min-w-[400px] w-full"
        />
      </div>
    </div>
  )
}

function TechnicianReport({ filters }: { filters: ReportFilters }) {
  const { data, loading, error, reload } = useAsyncData<TechnicianProductivityReport | null>(
    (signal) => reportService.getTechnicianProductivity(filters, signal),
    null,
    [filters.dateFrom, filters.dateTo, filters.branchId, filters.clientId, filters.technicianId],
  )

  const { pushToast } = useToast()
  const [exporting, setExporting] = useState(false)

  async function handleExport() {
    setExporting(true)
    try {
      await reportService.exportTechnicianProductivity(filters)
      pushToast({ title: 'Export ready', description: 'Technician productivity CSV downloaded.', tone: 'success' })
    } catch (exportError) {
      pushToast({ title: 'Export failed', description: exportError instanceof Error ? exportError.message : 'Unable to export. Please try again.', tone: 'danger' })
    } finally {
      setExporting(false)
    }
  }

  if (loading && !data) return <LoadingState />
  if (error) return <ErrorState message={error} onRetry={reload} />

  type Row = TechnicianProductivityReport['rows'][number]

  const cols: DataTableColumn<Row>[] = [
    { key: 'name', header: 'Technician', cell: (r) => r.technicianName },
    { key: 'total', header: 'Total Jobs', cell: (r) => r.totalJobs },
    { key: 'completed', header: 'Completed', cell: (r) => r.completed },
    {
      key: 'onTime',
      header: 'On-Time Rate',
      cell: (r) => (
        <span className={r.onTimeRate >= 80 ? 'text-emerald-600 font-semibold' : r.onTimeRate >= 60 ? 'text-amber-600 font-semibold' : 'text-rose-600 font-semibold'}>
          {pct(r.onTimeRate)}
        </span>
      ),
    },
    { key: 'avgTime', header: 'Avg Time On Site', cell: (r) => hrs(r.avgTimeOnSiteHours) },
    { key: 'pm', header: 'PM Jobs', cell: (r) => r.pmCount },
    { key: 'corrective', header: 'Corrective', cell: (r) => r.correctiveCount },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-end">
        <ExportButton onClick={handleExport} disabled={exporting} />
      </div>
      <DataTable
        columns={cols}
        rows={data?.rows ?? []}
        rowKey={(r) => r.technicianId}
        emptyTitle="No technician data"
        emptyDescription="No work orders were completed in this period."
        minTableWidth="min-w-[700px] w-full"
      />
    </div>
  )
}

function AssetReliabilityReport({ filters }: { filters: ReportFilters }) {
  const { data, loading, error, reload } = useAsyncData<AssetReliabilityReport | null>(
    (signal) => reportService.getAssetReliability(filters, signal),
    null,
    [filters.dateFrom, filters.dateTo, filters.clientId, filters.siteId, filters.categoryId],
  )

  const { pushToast } = useToast()
  const [exporting, setExporting] = useState(false)

  async function handleExport() {
    setExporting(true)
    try {
      await reportService.exportAssetReliability(filters)
      pushToast({ title: 'Export ready', description: 'Asset reliability CSV downloaded.', tone: 'success' })
    } catch (exportError) {
      pushToast({ title: 'Export failed', description: exportError instanceof Error ? exportError.message : 'Unable to export. Please try again.', tone: 'danger' })
    } finally {
      setExporting(false)
    }
  }

  if (loading && !data) return <LoadingState />
  if (error) return <ErrorState message={error} onRetry={reload} />

  type Row = AssetReliabilityReport['rows'][number]

  const cols: DataTableColumn<Row>[] = [
    {
      key: 'asset',
      header: 'Asset',
      cell: (r) => (
        <div>
          <p className="font-medium text-app">{r.assetName}</p>
          <p className="text-xs text-muted">{r.assetCode}</p>
        </div>
      ),
    },
    { key: 'client', header: 'Client', cell: (r) => r.clientName },
    { key: 'site', header: 'Site', cell: (r) => r.siteName || '-' },
    { key: 'corrective', header: 'Corrective WOs', cell: (r) => r.correctiveWoCount },
    {
      key: 'pmRate',
      header: 'PM Compliance',
      cell: (r) => (
        <span className={r.pmComplianceRate >= 80 ? 'text-emerald-600 font-semibold' : r.pmComplianceRate >= 60 ? 'text-amber-600 font-semibold' : 'text-rose-600 font-semibold'}>
          {pct(r.pmComplianceRate)}
        </span>
      ),
    },
    {
      key: 'warranty',
      header: 'Warranty Expiry',
      cell: (r) => (r.warrantyExpiry ? formatDateOnly(r.warrantyExpiry) : '-'),
    },
    {
      key: 'recurring',
      header: 'Recurring Fault',
      cell: (r) =>
        r.isRecurringFault ? (
          <Badge tone="danger">Yes</Badge>
        ) : (
          <Badge tone="default">No</Badge>
        ),
    },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-end">
        <ExportButton onClick={handleExport} disabled={exporting} />
      </div>
      <DataTable
        columns={cols}
        rows={data?.rows ?? []}
        rowKey={(r) => r.assetId}
        emptyTitle="No asset data"
        emptyDescription="No assets with work orders found in this period."
        minTableWidth="min-w-[800px] w-full"
      />
    </div>
  )
}

function PmComplianceReport({ filters }: { filters: ReportFilters }) {
  const { data, loading, error, reload } = useAsyncData<PmComplianceReport | null>(
    (signal) => reportService.getPmCompliance(filters, signal),
    null,
    [filters.dateFrom, filters.dateTo, filters.clientId, filters.siteId, filters.categoryId],
  )

  const { pushToast } = useToast()
  const [exporting, setExporting] = useState(false)

  async function handleExport() {
    setExporting(true)
    try {
      await reportService.exportPmCompliance(filters)
      pushToast({ title: 'Export ready', description: 'PM compliance CSV downloaded.', tone: 'success' })
    } catch (exportError) {
      pushToast({ title: 'Export failed', description: exportError instanceof Error ? exportError.message : 'Unable to export. Please try again.', tone: 'danger' })
    } finally {
      setExporting(false)
    }
  }

  if (loading && !data) return <LoadingState />
  if (error) return <ErrorState message={error} onRetry={reload} />

  const s = data?.summary

  type OverduePlan = PmComplianceReport['overduePlans'][number]

  const cols: DataTableColumn<OverduePlan>[] = [
    {
      key: 'asset',
      header: 'Asset',
      cell: (r) => (
        <div>
          <p className="font-medium text-app">{r.assetName}</p>
          <p className="text-xs text-muted">{r.assetCode}</p>
        </div>
      ),
    },
    { key: 'client', header: 'Client', cell: (r) => r.clientName },
    { key: 'nextPm', header: 'Next PM Date', cell: (r) => formatDateOnly(r.nextPmDate) },
    {
      key: 'daysOverdue',
      header: 'Days Overdue',
      cell: (r) => <span className="font-semibold text-rose-600">{r.daysOverdue}d</span>,
    },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-end">
        <ExportButton onClick={handleExport} disabled={exporting} />
      </div>

      {s && (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          <StatCard title="Active Plans" value={String(s.activePlans)} detail="Plans currently scheduled" icon={ClipboardList} accent="cyan" />
          <StatCard title="Due This Period" value={String(s.duePeriod)} detail="PM services scheduled" icon={Clock} accent="amber" />
          <StatCard title="Completed On-Time" value={String(s.completedOnTime)} detail="PM services done on schedule" icon={CheckCircle2} accent="emerald" />
          <StatCard title="Overdue" value={String(s.overdue)} detail="Past due date, no WO completed" icon={AlertTriangle} accent="rose" />
          <StatCard title="Compliance Rate" value={pct(s.complianceRate)} detail="On-time completion rate" icon={ShieldCheck} accent={s.complianceRate >= 80 ? 'emerald' : s.complianceRate >= 60 ? 'amber' : 'rose'} />
        </div>
      )}

      {(data?.overduePlans?.length ?? 0) > 0 && (
        <div className="surface-card space-y-3 p-4">
          <h3 className="text-sm font-semibold text-app">Overdue Plans</h3>
          <DataTable
            columns={cols}
            rows={data?.overduePlans ?? []}
            rowKey={(r) => r.planId}
            emptyTitle="No overdue plans"
            emptyDescription="All PM plans are on track."
            minTableWidth="min-w-[600px] w-full"
          />
        </div>
      )}
    </div>
  )
}

export function ReportsPage() {
  const { session } = useAuth()
  const [activeTab, setActiveTab] = useState<ReportTab>('wo-performance')
  const [kipOpen, setKipOpen] = useState(false)
  const [filters, setFilters] = useState<ReportFilters>({
    dateFrom: thirtyDaysAgo(),
    dateTo: today(),
  })
  const [sites, setSites] = useState<SiteRecord[]>([])
  const { data: clients } = useAsyncData<ClientRecord[]>(
    (signal) => clientService.list({ status: 'active', signal }),
    [],
    [],
  )
  const { data: categories } = useAsyncData<AssetCategoryRecord[]>(
    (signal) => assetCategoryService.list(signal),
    [],
    [],
  )

  useEffect(() => {
    const controller = new AbortController()

    if (!filters.clientId) {
      setSites([])
      return () => controller.abort()
    }

    void siteService
      .search({ clientId: filters.clientId, signal: controller.signal })
      .then((items) => setSites(items))
      .catch((error) => {
        if (!(error instanceof Error) || error.name !== 'AbortError') {
          setSites([])
        }
      })

    return () => controller.abort()
  }, [filters.clientId])

  const kipContext = {
    screen: 'reports',
    entityType: null,
    entityId: null,
    entitySummary: {
      activeTab,
      filters,
    },
    tenantId: session?.tenantId || '',
    userId: session?.userId || '',
    userRole: session?.role || 'unknown',
    timestamp: new Date().toISOString(),
  }

  return (
    <>
      <div className="space-y-6">
        <PageHeader
          eyebrow="Reports"
          title="Reports"
          description="Review operational summaries and service performance across your workspace."
        />

        <div className="surface-card p-4" data-testid="reports-filters">
          <ReportsFilterBar
            activeTab={activeTab}
            filters={filters}
            onChange={setFilters}
            clients={clients}
            sites={sites}
            categories={categories}
          />
        </div>

        <div className="surface-card overflow-hidden">
          <div className="border-b border-app overflow-x-auto">
            <nav className="flex min-w-max px-4">
              {TABS.map((tab) => {
                const Icon = tab.icon
                const isActive = activeTab === tab.id
                return (
                  <button
                    key={tab.id}
                    type="button"
                    onClick={() => setActiveTab(tab.id)}
                    data-testid={`reports-tab-${tab.id}`}
                    className={`flex items-center gap-2 border-b-2 px-4 py-3.5 text-sm font-medium transition-colors whitespace-nowrap ${
                      isActive
                        ? 'border-cyan-500 text-cyan-600 dark:text-cyan-400'
                        : 'border-transparent text-muted hover:text-app'
                    }`}
                  >
                    <Icon className="h-4 w-4" />
                    {tab.label}
                  </button>
                )
              })}
            </nav>
          </div>

          <div className="p-4 sm:p-6">
            {activeTab === 'wo-performance' && <WoPerformanceReport filters={filters} />}
            {activeTab === 'technician' && <TechnicianReport filters={filters} />}
            {activeTab === 'asset-reliability' && <AssetReliabilityReport filters={filters} />}
            {activeTab === 'pm-compliance' && <PmComplianceReport filters={filters} />}
          </div>
        </div>
      </div>
      <KipButton onClick={() => setKipOpen(true)} />
      <KipPanel open={kipOpen} onClose={() => setKipOpen(false)} title="KIP • Reports" context={kipContext} />
    </>
  )
}
