import {
  AlertTriangle,
  BriefcaseMedical,
  CheckCircle2,
  Clock,
  MapPin,
  RefreshCw,
  Search,
  Truck,
  User,
  UserCheck,
  UserX,
  X,
  Zap,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useServiceOpsHub } from '../../hooks/useServiceOpsHub'
import { Link } from 'react-router-dom'
import { Avatar } from '../../components/ui/Avatar'
import { Badge } from '../../components/ui/Badge'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../components/ui/ToastProvider'
import { PageTabs } from '../../components/ui/Workspace'
import { useShellContext } from '../../components/layout/AppShell'
import { settingsService } from '../../services/settingsService'
import { technicianService } from '../../services/technicianService'
import { workOrderService } from '../../services/workOrderService'
import type { AssignmentGroupRecord, TechnicianRecord, WorkOrder } from '../../types/api'
import { DispatchMapView } from './DispatchMapView'

// ─────────────────────────────────────────────────────────────────────────────
// Types
// ─────────────────────────────────────────────────────────────────────────────

type TechStatus = 'Available' | 'On Job' | 'Travelling' | 'Off Shift' | 'Uncontactable'
type StatusFilter = 'All' | TechStatus
type SortBy = 'priority' | 'age' | 'due'
type TimelineRange = 'today' | 'tomorrow' | 'week'
type DispatchWorkspaceView = 'queue' | 'technicians' | 'map' | 'timeline'

interface DerivedTech {
  record: TechnicianRecord
  status: TechStatus
  currentJob: WorkOrder | null
  todayJobs: WorkOrder[]
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

function deriveTechStatus(record: TechnicianRecord): TechStatus {
  if (!record.isActive) return 'Off Shift'
  if (record.activeWorkOrderId) return 'On Job'
  if (record.isTrackingActive) return 'Travelling'
  return 'Available'
}

function isUnassignedWO(wo: WorkOrder): boolean {
  if (wo.assignmentType === 'Unassigned') return true
  if (wo.isUnassigned === true) return true
  const closedStatuses = ['completed', 'closed', 'cancelled']
  if (closedStatuses.includes(wo.status?.toLowerCase() ?? '')) return false
  return !wo.assignedTechnicianId && !wo.leadTechnicianId && (!wo.technicianAssignments || wo.technicianAssignments.length === 0)
}

function timeAgo(iso: string): string {
  const ms = Date.now() - new Date(iso).getTime()
  const mins = Math.floor(ms / 60000)
  if (mins < 60) return `${mins}m ago`
  const hrs = Math.floor(mins / 60)
  if (hrs < 24) return `${hrs}h ago`
  return `${Math.floor(hrs / 24)}d ago`
}

function isToday(iso: string): boolean {
  const d = new Date(iso)
  const t = new Date()
  return d.getFullYear() === t.getFullYear() && d.getMonth() === t.getMonth() && d.getDate() === t.getDate()
}

function isTomorrow(iso: string): boolean {
  const d = new Date(iso)
  const t = new Date()
  t.setDate(t.getDate() + 1)
  return d.getFullYear() === t.getFullYear() && d.getMonth() === t.getMonth() && d.getDate() === t.getDate()
}

function priorityOrder(priority: string): number {
  const p = priority?.toLowerCase() ?? ''
  if (p === 'critical') return 0
  if (p === 'high') return 1
  if (p === 'medium') return 2
  if (p === 'low') return 3
  return 4
}

interface PriorityMeta { label: string; dot: string; text: string; bg: string }

function priorityMeta(priority: string): PriorityMeta {
  const p = priority?.toLowerCase() ?? ''
  if (p === 'critical') return { label: 'Critical', dot: 'bg-rose-500', text: 'text-rose-700 dark:text-rose-400', bg: 'bg-rose-50 dark:bg-rose-500/10' }
  if (p === 'high') return { label: 'High', dot: 'bg-amber-500', text: 'text-amber-700 dark:text-amber-400', bg: 'bg-amber-50 dark:bg-amber-500/10' }
  if (p === 'medium') return { label: 'Medium', dot: 'bg-blue-500', text: 'text-blue-700 dark:text-blue-400', bg: 'bg-blue-50 dark:bg-blue-500/10' }
  return { label: 'Low', dot: 'bg-slate-400', text: 'text-slate-600 dark:text-slate-400', bg: 'bg-slate-100 dark:bg-slate-500/10' }
}

interface StatusMeta { bg: string; text: string; ring: string; icon: React.ElementType }

function techStatusMeta(status: TechStatus): StatusMeta {
  switch (status) {
    case 'Available': return { bg: 'bg-emerald-500/12', text: 'text-emerald-700 dark:text-emerald-400', ring: 'ring-emerald-500', icon: UserCheck }
    case 'On Job': return { bg: 'bg-amber-500/12', text: 'text-amber-700 dark:text-amber-400', ring: 'ring-amber-500', icon: BriefcaseMedical }
    case 'Travelling': return { bg: 'bg-sky-500/12', text: 'text-sky-700 dark:text-sky-400', ring: 'ring-sky-500', icon: Truck }
    case 'Off Shift': return { bg: 'bg-slate-200/60 dark:bg-slate-700/30', text: 'text-slate-500 dark:text-slate-400', ring: 'ring-slate-400', icon: UserX }
    case 'Uncontactable': return { bg: 'bg-rose-500/10', text: 'text-rose-700 dark:text-rose-400', ring: 'ring-rose-500', icon: AlertTriangle }
  }
}

function timelineHourOffset(iso: string): number {
  const d = new Date(iso)
  return d.getHours() + d.getMinutes() / 60
}

// ─────────────────────────────────────────────────────────────────────────────
// StatusStrip
// ─────────────────────────────────────────────────────────────────────────────

const STATUS_PILLS: { label: string; value: StatusFilter; icon: React.ElementType }[] = [
  { label: 'Available', value: 'Available', icon: UserCheck },
  { label: 'On Job', value: 'On Job', icon: BriefcaseMedical },
  { label: 'Travelling', value: 'Travelling', icon: Truck },
  { label: 'Off Shift', value: 'Off Shift', icon: UserX },
  { label: 'Uncontactable', value: 'Uncontactable', icon: AlertTriangle },
]

function StatusStrip({
  techs,
  filter,
  onFilter,
  lastRefresh,
  isRefreshing,
  onRefresh,
}: {
  techs: DerivedTech[]
  filter: StatusFilter
  onFilter: (f: StatusFilter) => void
  lastRefresh: Date | null
  isRefreshing: boolean
  onRefresh: () => void
}) {
  const counts = useMemo(() => {
    const map: Record<string, number> = {}
    for (const t of techs) {
      map[t.status] = (map[t.status] ?? 0) + 1
    }
    return map
  }, [techs])

  const activeCount = (counts['Available'] ?? 0) + (counts['On Job'] ?? 0) + (counts['Travelling'] ?? 0)

  return (
    <section className="surface-card rounded-[24px] px-4 py-3">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        {/* Pills */}
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => onFilter('All')}
            className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-all ${
              filter === 'All'
                ? 'bg-[var(--app-primary)] text-white shadow-sm'
                : 'bg-[var(--app-primary-soft)] text-[var(--app-primary-strong)] hover:bg-[var(--app-primary)]/15'
            }`}
          >
            <Zap className="size-3" />
            All Active
            <span className="ml-0.5 rounded-full bg-white/20 px-1.5 py-0.5 text-[10px] font-bold leading-none">
              {activeCount}
            </span>
          </button>

          {STATUS_PILLS.map(({ label, value, icon: Icon }) => {
            const count = counts[value] ?? 0
            const meta = techStatusMeta(value as TechStatus)
            const isActive = filter === value
            return (
              <button
                key={value}
                type="button"
                onClick={() => onFilter(isActive ? 'All' : value)}
                className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-all ${
                  isActive
                    ? `ring-2 ${meta.ring} ${meta.bg} ${meta.text}`
                    : `${meta.bg} ${meta.text} hover:ring-1 ${meta.ring}`
                }`}
              >
                <Icon className="size-3" />
                {label}
                <span className="ml-0.5 rounded-full bg-black/10 dark:bg-white/15 px-1.5 py-0.5 text-[10px] font-bold leading-none">
                  {count}
                </span>
              </button>
            )
          })}
        </div>

        {/* Refresh indicator */}
        <div className="flex items-center gap-2 text-[11px] text-muted">
          {lastRefresh ? (
            <span>Updated {timeAgo(lastRefresh.toISOString())}</span>
          ) : null}
          <button
            type="button"
            className="icon-button h-7 w-7"
            onClick={onRefresh}
            title="Refresh now"
            disabled={isRefreshing}
          >
            <RefreshCw className={`size-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
          </button>
          <span className="text-[10px] text-muted/60">Auto-refresh 15s</span>
        </div>
      </div>
    </section>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// PriorityBadge
// ─────────────────────────────────────────────────────────────────────────────

function PriorityBadge({ priority }: { priority: string }) {
  const meta = priorityMeta(priority)
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider ${meta.bg} ${meta.text}`}>
      <span className={`inline-block size-1.5 rounded-full ${meta.dot}`} />
      {meta.label}
    </span>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// TechStatusBadge
// ─────────────────────────────────────────────────────────────────────────────

function TechStatusBadge({ status }: { status: TechStatus }) {
  const meta = techStatusMeta(status)
  const Icon = meta.icon
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${meta.bg} ${meta.text}`}>
      <Icon className="size-3" />
      {status}
    </span>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// JobCard (Unassigned queue)
// ─────────────────────────────────────────────────────────────────────────────

function JobCard({ wo, onAssign }: { wo: WorkOrder; onAssign: (wo: WorkOrder) => void }) {
  return (
    <div className="group relative rounded-[16px] border border-app bg-[var(--app-surface)] p-3.5 transition-shadow hover:shadow-md">
      {/* Header row */}
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="font-mono text-[11px] font-bold text-[var(--app-primary-strong)]">
              {wo.workOrderNumber}
            </span>
            <PriorityBadge priority={wo.priority ?? 'Low'} />
          </div>
          <p className="mt-0.5 truncate text-sm font-semibold text-app">{wo.title}</p>
        </div>
      </div>

      {/* Client / Site */}
      <div className="mt-2 flex flex-col gap-0.5">
        {wo.clientName ? (
          <p className="flex items-center gap-1 truncate text-[11px] text-secondary">
            <User className="size-3 shrink-0" />
            {wo.clientName}
          </p>
        ) : null}
        {wo.siteName ? (
          <p className="flex items-center gap-1 truncate text-[11px] text-muted">
            <MapPin className="size-3 shrink-0" />
            {wo.siteName}
          </p>
        ) : null}
      </div>

      {/* Meta row */}
      <div className="mt-2.5 flex items-center justify-between">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="flex items-center gap-1 text-[10px] text-muted">
            <Clock className="size-3" />
            {timeAgo(wo.createdAt)}
          </span>
          {wo.assignmentGroupName ? (
            <span className="rounded-full bg-[var(--app-primary-soft)] px-2 py-0.5 text-[10px] font-semibold text-[var(--app-primary-strong)]">
              {wo.assignmentGroupName}
            </span>
          ) : null}
        </div>

        <button
          type="button"
          className="button-primary h-7 shrink-0 px-3 text-[11px]"
          onClick={() => onAssign(wo)}
        >
          Assign
        </button>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// UnassignedJobsPanel (left panel)
// ─────────────────────────────────────────────────────────────────────────────

function UnassignedJobsPanel({
  jobs,
  onAssign,
}: {
  jobs: WorkOrder[]
  onAssign: (wo: WorkOrder) => void
}) {
  const [sortBy, setSortBy] = useState<SortBy>('priority')
  const [search, setSearch] = useState('')

  const sorted = useMemo(() => {
    let list = [...jobs]
    if (search.trim()) {
      const q = search.toLowerCase()
      list = list.filter(
        (wo) =>
          wo.title.toLowerCase().includes(q)
          || wo.workOrderNumber.toLowerCase().includes(q)
          || (wo.clientName ?? '').toLowerCase().includes(q)
          || (wo.siteName ?? '').toLowerCase().includes(q),
      )
    }
    if (sortBy === 'priority') {
      list.sort((a, b) => priorityOrder(a.priority) - priorityOrder(b.priority))
    } else if (sortBy === 'age') {
      list.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime())
    } else if (sortBy === 'due') {
      list.sort((a, b) => {
        if (!a.dueDate && !b.dueDate) return 0
        if (!a.dueDate) return 1
        if (!b.dueDate) return -1
        return new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime()
      })
    }
    return list
  }, [jobs, search, sortBy])

  return (
    <div className="surface-card flex flex-col rounded-[24px] overflow-hidden" style={{ height: '100%' }}>
      {/* Panel header */}
      <div className="border-b border-app px-4 py-3 shrink-0">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <h2 className="text-sm font-bold text-app">Unassigned Jobs</h2>
            <span className="inline-flex items-center justify-center rounded-full bg-rose-500/10 px-2 py-0.5 text-[11px] font-bold text-rose-600 dark:text-rose-400">
              {jobs.length}
            </span>
          </div>
        </div>

        {/* Search */}
        <div className="relative mt-2">
          <Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted pointer-events-none" />
          <input
            type="text"
            placeholder="Search jobs…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="input-field h-8 w-full pl-7 text-xs"
          />
          {search ? (
            <button
              type="button"
              className="absolute right-2 top-1/2 -translate-y-1/2 text-muted hover:text-app"
              onClick={() => setSearch('')}
            >
              <X className="size-3" />
            </button>
          ) : null}
        </div>

        {/* Sort */}
        <div className="mt-2 flex gap-1">
          {([['priority', 'Priority'], ['age', 'Age'], ['due', 'Due Date']] as const).map(([val, label]) => (
            <button
              key={val}
              type="button"
              onClick={() => setSortBy(val)}
              className={`rounded-full px-2.5 py-1 text-[10px] font-semibold transition-colors ${
                sortBy === val
                  ? 'bg-[var(--app-primary)] text-white'
                  : 'bg-[var(--app-surface-strong)] text-muted hover:text-app'
              }`}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {/* Job list */}
      <div className="flex-1 overflow-y-auto p-3 space-y-2">
        {sorted.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <CheckCircle2 className="size-10 text-emerald-500/40 mb-3" />
            <p className="text-sm font-semibold text-app">All jobs assigned</p>
            <p className="mt-1 text-xs text-muted">No unassigned work orders found</p>
          </div>
        ) : (
          sorted.map((wo) => (
            <JobCard key={wo.id} wo={wo} onAssign={onAssign} />
          ))
        )}
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// TechSlideOver (right-side detail panel)
// ─────────────────────────────────────────────────────────────────────────────

function TechSlideOver({
  tech,
  onClose,
  onAssignJob,
}: {
  tech: DerivedTech
  onClose: () => void
  onAssignJob: (tech: DerivedTech) => void
}) {
  const { record } = tech

  return (
    <div
      className="fixed inset-0 z-50 flex items-stretch justify-end bg-black/40 backdrop-blur-[2px]"
      onClick={onClose}
    >
      <div
        className="glass-panel flex h-full w-full max-w-md flex-col overflow-hidden rounded-none sm:rounded-l-[24px]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="border-b border-app px-5 py-4 shrink-0">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-3">
              <Avatar name={record.fullName} size="lg" tone="accent" />
              <div>
                <p className="font-semibold text-app">{record.fullName}</p>
                <div className="mt-1">
                  <TechStatusBadge status={tech.status} />
                </div>
              </div>
            </div>
            <button type="button" className="icon-button h-9 w-9" onClick={onClose}>
              <X className="size-4" />
            </button>
          </div>

          {/* Contact info */}
          <div className="mt-3 flex flex-wrap gap-3 text-xs text-muted">
            {record.phone ? <span>📞 {record.phone}</span> : null}
            {record.email ? <span>✉ {record.email}</span> : null}
          </div>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5">
          {/* Current Job */}
          <div>
            <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
              Current Job
            </p>
            {tech.currentJob ? (
              <Link
                to={`/work-orders/${tech.currentJob.id}`}
                className="block rounded-[14px] border border-app bg-[var(--app-surface-strong)] p-3 hover:opacity-80 transition-opacity"
              >
                <div className="flex items-center gap-2">
                  <span className="font-mono text-[11px] font-bold text-[var(--app-primary-strong)]">
                    {tech.currentJob.workOrderNumber}
                  </span>
                  <PriorityBadge priority={tech.currentJob.priority ?? 'Low'} />
                </div>
                <p className="mt-1 text-sm font-medium text-app">{tech.currentJob.title}</p>
                {tech.currentJob.clientName ? (
                  <p className="mt-1 text-xs text-muted">{tech.currentJob.clientName}</p>
                ) : null}
              </Link>
            ) : (
              <p className="rounded-[14px] border border-app bg-[var(--app-surface-strong)] px-3 py-3 text-sm text-muted">
                No active job
              </p>
            )}
          </div>

          {/* Today's Queue */}
          {tech.todayJobs.length > 0 ? (
            <div>
              <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
                Today's Queue ({tech.todayJobs.length})
              </p>
              <div className="space-y-1.5">
                {tech.todayJobs.map((wo, idx) => (
                  <Link
                    key={wo.id}
                    to={`/work-orders/${wo.id}`}
                    className="flex items-center gap-2 rounded-[12px] border border-app p-2.5 hover:opacity-80 transition-opacity"
                  >
                    <span className="flex size-5 shrink-0 items-center justify-center rounded-full bg-[var(--app-primary-soft)] text-[10px] font-bold text-[var(--app-primary-strong)]">
                      {idx + 1}
                    </span>
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-xs font-semibold text-app">{wo.title}</p>
                      <p className="text-[10px] text-muted">{wo.workOrderNumber}</p>
                    </div>
                    <PriorityBadge priority={wo.priority ?? 'Low'} />
                  </Link>
                ))}
              </div>
            </div>
          ) : null}

          {/* Skills */}
          <div>
            <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Skills</p>
            <div className="flex flex-wrap gap-1.5">
              {record.skillCategory ? (
                <span className="rounded-full bg-[var(--app-primary-soft)] px-2.5 py-1 text-[11px] font-semibold text-[var(--app-primary-strong)]">
                  {record.skillCategory}
                </span>
              ) : null}
              {record.assignmentGroup ? (
                <span className="rounded-full bg-[var(--app-primary-soft)] px-2.5 py-1 text-[11px] font-semibold text-[var(--app-primary-strong)]">
                  {record.assignmentGroup}
                </span>
              ) : null}
              {!record.skillCategory && !record.assignmentGroup ? (
                <span className="text-xs text-muted">No skills recorded</span>
              ) : null}
            </div>
          </div>

          {/* Tracking */}
          {record.lastLocationAt ? (
            <div>
              <p className="mb-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Location</p>
              <p className="text-xs text-secondary">Last seen {timeAgo(record.lastLocationAt)}</p>
            </div>
          ) : null}
        </div>

        {/* Actions */}
        <div className="border-t border-app px-5 py-3 shrink-0 flex gap-2">
          <button
            type="button"
            className="button-primary flex-1 text-sm"
            onClick={() => onAssignJob(tech)}
          >
            Assign a Job
          </button>
        </div>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// TechnicianTable (right panel)
// ─────────────────────────────────────────────────────────────────────────────

function TechnicianTable({
  techs,
  statusFilter,
  onSelectTech,
}: {
  techs: DerivedTech[]
  statusFilter: StatusFilter
  onSelectTech: (tech: DerivedTech) => void
}) {
  const [search, setSearch] = useState('')

  const filtered = useMemo(() => {
    let list = [...techs]
    if (statusFilter !== 'All') {
      list = list.filter((t) => t.status === statusFilter)
    }
    if (search.trim()) {
      const q = search.toLowerCase()
      list = list.filter(
        (t) =>
          t.record.fullName.toLowerCase().includes(q)
          || (t.record.skillCategory ?? '').toLowerCase().includes(q)
          || (t.record.assignmentGroup ?? '').toLowerCase().includes(q),
      )
    }
    // Sort: Available → On Job → Travelling → Off Shift → Uncontactable
    const order: Record<TechStatus, number> = {
      Available: 0,
      'On Job': 1,
      Travelling: 2,
      'Off Shift': 3,
      Uncontactable: 4,
    }
    list.sort((a, b) => order[a.status] - order[b.status])
    return list
  }, [techs, statusFilter, search])

  return (
    <div className="surface-card flex flex-col rounded-[24px] overflow-hidden" style={{ height: '100%' }}>
      {/* Header */}
      <div className="border-b border-app px-4 py-3 shrink-0">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-bold text-app">Technician Status</h2>
          <span className="text-[11px] text-muted">{filtered.length} showing</span>
        </div>
        <div className="relative mt-2">
          <Search className="absolute left-2.5 top-1/2 size-3.5 -translate-y-1/2 text-muted pointer-events-none" />
          <input
            type="text"
            placeholder="Filter technicians…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="input-field h-8 w-full pl-7 text-xs"
          />
          {search ? (
            <button
              type="button"
              className="absolute right-2 top-1/2 -translate-y-1/2 text-muted hover:text-app"
              onClick={() => setSearch('')}
            >
              <X className="size-3" />
            </button>
          ) : null}
        </div>
      </div>

      {/* Table */}
      <div className="flex-1 overflow-y-auto">
        {filtered.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12 text-center">
            <User className="size-10 text-muted/30 mb-3" />
            <p className="text-sm font-semibold text-app">No technicians match</p>
            <p className="mt-1 text-xs text-muted">Try adjusting the status filter or search</p>
          </div>
        ) : (
          <table className="w-full border-collapse">
            <thead className="sticky top-0 z-10">
              <tr className="table-head">
                <th className="px-4 py-2 text-left text-[10px] font-semibold uppercase tracking-[0.18em] text-muted">
                  Technician
                </th>
                <th className="px-3 py-2 text-left text-[10px] font-semibold uppercase tracking-[0.18em] text-muted">
                  Status
                </th>
                <th className="px-3 py-2 text-left text-[10px] font-semibold uppercase tracking-[0.18em] text-muted hidden md:table-cell">
                  Current Job
                </th>
                <th className="px-3 py-2 text-left text-[10px] font-semibold uppercase tracking-[0.18em] text-muted hidden lg:table-cell">
                  Group / Skills
                </th>
                <th className="px-3 py-2 text-center text-[10px] font-semibold uppercase tracking-[0.18em] text-muted">
                  Shift
                </th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((tech) => {
                const { record } = tech
                const skills = [record.skillCategory, record.assignmentGroup].filter(Boolean)
                return (
                  <tr
                    key={record.id}
                    className="border-t border-app cursor-pointer hover:bg-[var(--app-surface-strong)] transition-colors"
                    onClick={() => onSelectTech(tech)}
                  >
                    {/* Avatar + Name */}
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2.5">
                        <Avatar name={record.fullName} size="sm" tone="accent" />
                        <div>
                          <p className="text-xs font-semibold text-app">{record.fullName}</p>
                          {record.phone ? (
                            <p className="text-[10px] text-muted">{record.phone}</p>
                          ) : null}
                        </div>
                      </div>
                    </td>

                    {/* Status */}
                    <td className="px-3 py-3">
                      <TechStatusBadge status={tech.status} />
                    </td>

                    {/* Current Job */}
                    <td className="px-3 py-3 hidden md:table-cell">
                      {tech.currentJob ? (
                        <div>
                          <p className="font-mono text-[10px] font-bold text-[var(--app-primary-strong)]">
                            {tech.currentJob.workOrderNumber}
                          </p>
                          <p className="max-w-[180px] truncate text-[11px] text-secondary">
                            {tech.currentJob.clientName ?? tech.currentJob.title}
                          </p>
                        </div>
                      ) : (
                        <span className="text-xs text-muted">—</span>
                      )}
                    </td>

                    {/* Skills */}
                    <td className="px-3 py-3 hidden lg:table-cell">
                      <div className="flex flex-wrap gap-1">
                        {skills.slice(0, 2).map((s) => (
                          <span
                            key={s}
                            className="rounded-full bg-[var(--app-primary-soft)] px-1.5 py-0.5 text-[10px] font-semibold text-[var(--app-primary-strong)]"
                          >
                            {s}
                          </span>
                        ))}
                        {skills.length > 2 ? (
                          <span className="text-[10px] text-muted">+{skills.length - 2}</span>
                        ) : null}
                        {skills.length === 0 ? <span className="text-[10px] text-muted">—</span> : null}
                      </div>
                    </td>

                    {/* Shift */}
                    <td className="px-3 py-3 text-center">
                      <span
                        className={`inline-block size-2.5 rounded-full ${
                          record.isActive ? 'bg-emerald-500' : 'bg-slate-400'
                        }`}
                        title={record.isActive ? 'On Shift' : 'Off Shift'}
                      />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// AssignModal
// ─────────────────────────────────────────────────────────────────────────────

function AssignModal({
  job,
  techs,
  groups,
  onClose,
  onAssigned,
}: {
  job: WorkOrder
  techs: DerivedTech[]
  groups: AssignmentGroupRecord[]
  onClose: () => void
  onAssigned: (wo: WorkOrder) => void
}) {
  const { pushToast } = useToast()
  const [assigning, setAssigning] = useState<string | null>(null)
  const [selectedGroupId, setSelectedGroupId] = useState(job.assignmentGroupId ?? '')

  useEffect(() => {
    setSelectedGroupId(job.assignmentGroupId ?? '')
  }, [job.assignmentGroupId, job.id])

  const selectedGroup = useMemo(
    () => groups.find((group) => group.id === selectedGroupId) ?? null,
    [groups, selectedGroupId],
  )

  // Sort: skill match first, then availability, then name
  const sorted = useMemo(() => {
    const skillHint = (selectedGroup?.name ?? job.assignmentGroupName ?? '').toLowerCase()
    return [...techs].sort((a, b) => {
      // Available/On Job before Off Shift
      const aActive = a.status !== 'Off Shift' && a.status !== 'Uncontactable' ? 0 : 1
      const bActive = b.status !== 'Off Shift' && b.status !== 'Uncontactable' ? 0 : 1
      if (aActive !== bActive) return aActive - bActive

      // Skill match
      if (skillHint) {
        const aSkill = ((a.record.skillCategory ?? '') + (a.record.assignmentGroup ?? '')).toLowerCase()
        const bSkill = ((b.record.skillCategory ?? '') + (b.record.assignmentGroup ?? '')).toLowerCase()
        const aMatch = aSkill.includes(skillHint) ? 0 : 1
        const bMatch = bSkill.includes(skillHint) ? 0 : 1
        if (aMatch !== bMatch) return aMatch - bMatch
      }

      // Available before On Job
      const statusOrder: Record<TechStatus, number> = { Available: 0, Travelling: 1, 'On Job': 2, 'Off Shift': 3, Uncontactable: 4 }
      if (statusOrder[a.status] !== statusOrder[b.status]) return statusOrder[a.status] - statusOrder[b.status]

      return a.record.fullName.localeCompare(b.record.fullName)
    })
  }, [job.assignmentGroupName, selectedGroup?.name, techs])

  async function handleAssign(tech: DerivedTech) {
    setAssigning(tech.record.id)
    try {
      const updated = await workOrderService.assign(job.id, tech.record.id, selectedGroupId || null)
      pushToast({ tone: 'success', title: 'Job assigned', description: `${job.workOrderNumber} → ${tech.record.fullName}` })
      onAssigned(updated)
      onClose()
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Assignment failed'
      pushToast({ tone: 'danger', title: 'Assignment failed', description: msg })
    } finally {
      setAssigning(null)
    }
  }

  return (
    <Modal
      open
      title={`Assign ${job.workOrderNumber}`}
      description={`${job.title}${job.clientName ? ` · ${job.clientName}` : ''}`}
      onClose={onClose}
      maxWidth="max-w-2xl"
    >
      <div className="space-y-4">
        {/* Job summary */}
        <div className="rounded-[16px] bg-[var(--app-surface-strong)] p-3 flex flex-wrap gap-3 text-xs">
          <PriorityBadge priority={job.priority ?? 'Low'} />
          {job.siteName ? (
            <span className="flex items-center gap-1 text-muted">
              <MapPin className="size-3" />
              {job.siteName}
            </span>
          ) : null}
          {job.assignmentGroupName ? (
            <span className="flex items-center gap-1 text-muted">
              Required: <strong className="text-app">{job.assignmentGroupName}</strong>
            </span>
          ) : null}
          <span className="text-muted">Unassigned {timeAgo(job.createdAt)}</span>
        </div>

        {groups.length > 0 ? (
          <label className="space-y-2">
            <span className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">Assignment group</span>
            <select value={selectedGroupId} onChange={(event) => setSelectedGroupId(event.target.value)} className="field-input">
              <option value="">Select group</option>
              {groups.filter((group) => group.isActive).map((group) => (
                <option key={group.id} value={group.id}>{group.name}</option>
              ))}
            </select>
            <p className="text-xs text-muted">Choose the assignment group first so dispatch routing and technician selection stay aligned.</p>
          </label>
        ) : (
          <div className="rounded-[14px] border border-app bg-[var(--app-surface-strong)] px-3 py-2 text-xs text-muted">
            No active assignment groups are available yet.
          </div>
        )}

        {/* Technician list */}
        <div className="space-y-1.5">
          <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted">
            Select Technician ({sorted.length})
          </p>
          {sorted.length === 0 ? (
            <div className="rounded-[14px] border border-app bg-[var(--app-surface-strong)] px-3 py-6 text-center text-sm text-muted">
              No technicians are available to assign from this dispatch view.
            </div>
          ) : null}
          {sorted.map((tech) => {
            const isUnavailable = tech.status === 'Off Shift' || tech.status === 'Uncontactable'
            const skillHint = (selectedGroup?.name ?? job.assignmentGroupName ?? '').toLowerCase()
            const techSkills = ((tech.record.skillCategory ?? '') + (tech.record.assignmentGroup ?? '')).toLowerCase()
            const skillMatch = Boolean(skillHint && techSkills.includes(skillHint))
            const isAssigning = assigning === tech.record.id

            return (
              <div
                key={tech.record.id}
                className={`flex items-center gap-3 rounded-[14px] border border-app p-3 transition-colors ${
                  isUnavailable ? 'opacity-50' : 'hover:bg-[var(--app-surface-strong)]'
                }`}
              >
                <Avatar name={tech.record.fullName} size="sm" tone={isUnavailable ? 'neutral' : 'accent'} />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-semibold text-app">{tech.record.fullName}</p>
                    {skillMatch ? (
                      <span className="rounded-full bg-emerald-500/10 px-2 py-0.5 text-[10px] font-bold text-emerald-700 dark:text-emerald-400">
                        Skills match
                      </span>
                    ) : null}
                  </div>
                  <div className="mt-0.5 flex flex-wrap gap-1.5">
                    <TechStatusBadge status={tech.status} />
                    {tech.record.skillCategory ? (
                      <span className="rounded-full bg-[var(--app-primary-soft)] px-1.5 py-0.5 text-[10px] text-[var(--app-primary-strong)]">
                        {tech.record.skillCategory}
                      </span>
                    ) : null}
                    {tech.currentJob ? (
                      <span className="text-[10px] text-muted">
                        On: {tech.currentJob.workOrderNumber}
                      </span>
                    ) : null}
                  </div>
                </div>
                <button
                  type="button"
                  className={isUnavailable ? 'button-secondary text-xs px-3 h-8' : 'button-primary text-xs px-3 h-8'}
                  disabled={isAssigning}
                  onClick={() => handleAssign(tech)}
                >
                  {isAssigning ? (
                    <RefreshCw className="size-3.5 animate-spin" />
                  ) : (
                    'Assign'
                  )}
                </button>
              </div>
            )
          })}
        </div>
      </div>
    </Modal>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// TimelineView (collapsible)
// ─────────────────────────────────────────────────────────────────────────────

const TIMELINE_START_H = 6
const TIMELINE_END_H = 22
const TIMELINE_SPAN = TIMELINE_END_H - TIMELINE_START_H

function timelinePercent(hour: number): number {
  const clamped = Math.min(Math.max(hour, TIMELINE_START_H), TIMELINE_END_H)
  return ((clamped - TIMELINE_START_H) / TIMELINE_SPAN) * 100
}

// Block kind:
//   'completed'  — workStartedAt + completedAt both set
//   'active'     — workStartedAt set, completedAt null (still in progress; end = now)
//   'scheduled'  — workStartedAt null (not yet started; derived from dueDate or createdAt)
type BlockKind = 'completed' | 'active' | 'scheduled'

interface TimelineBlock {
  wo: WorkOrder
  startH: number
  endH: number
  isConflict: boolean
  kind: BlockKind
}

function buildBlocks(jobs: WorkOrder[]): TimelineBlock[] {
  const blocks: TimelineBlock[] = []
  const nowH = timelineHourOffset(new Date().toISOString())

  for (const wo of jobs) {
    let startH: number
    let endH: number
    let kind: BlockKind

    if (wo.workStartedAt) {
      // Job has actually started — use real start time
      // Fallback chain: workStartedAt (real) → [scheduledStartAt not in API type] → createdAt
      startH = timelineHourOffset(wo.workStartedAt)

      if (wo.completedAt) {
        // Fully completed: use real completion time
        endH = timelineHourOffset(wo.completedAt)
        kind = 'completed'
      } else {
        // Still in progress: extend block to current time so it shows as live
        endH = nowH
        kind = 'active'
      }
    } else {
      // Not started yet — show as a "scheduled" placeholder block.
      // Fallback chain:
      //   scheduledStartAt (set by dispatcher) → dueDate − 30 min → createdAt
      //   scheduledEndAt   (set by dispatcher) → dueDate + 30 min → createdAt + 1 h
      if (wo.scheduledStartAt) {
        startH = timelineHourOffset(wo.scheduledStartAt)
        endH = wo.scheduledEndAt
          ? timelineHourOffset(wo.scheduledEndAt)
          : startH + 1
      } else if (wo.dueDate) {
        const dueH = timelineHourOffset(wo.dueDate)
        startH = dueH - 0.5   // 30 min before due
        endH = dueH + 0.5     // 30 min after due
      } else {
        // Last resort: anchor at createdAt with a 1-hour placeholder
        startH = timelineHourOffset(wo.createdAt)
        endH = startH + 1
      }
      kind = 'scheduled'
    }

    // Clamp to visible timeline range
    startH = Math.max(startH, TIMELINE_START_H)
    endH = Math.min(endH, TIMELINE_END_H)
    // Ensure minimum visible width (15 min)
    if (endH - startH < 0.25) endH = startH + 0.25

    blocks.push({ wo, startH, endH, isConflict: false, kind })
  }

  // Detect overlaps
  for (let i = 0; i < blocks.length; i++) {
    for (let j = i + 1; j < blocks.length; j++) {
      if (blocks[i].startH < blocks[j].endH && blocks[j].startH < blocks[i].endH) {
        blocks[i].isConflict = true
        blocks[j].isConflict = true
      }
    }
  }

  return blocks
}

// Returns Tailwind class strings for the block background based on kind + priority + conflict.
// 'scheduled' blocks use a dashed border and semi-transparent fill to signal "not started".
// 'active' blocks share the normal fill; the pulsing right edge is rendered separately in JSX.
function woBlockClasses(wo: WorkOrder, isConflict: boolean, kind: BlockKind): string {
  if (isConflict) return 'bg-rose-500/90 text-white border border-rose-400'
  const p = (wo.priority ?? '').toLowerCase()

  if (kind === 'scheduled') {
    // Lighter fill + dashed border — visually distinct "not yet started" state
    if (p === 'critical') return 'bg-rose-500/20 text-rose-700 dark:text-rose-300 border border-dashed border-rose-500'
    if (p === 'high') return 'bg-amber-500/20 text-amber-700 dark:text-amber-300 border border-dashed border-amber-500'
    return 'bg-[var(--app-primary)]/15 text-[var(--app-primary-strong)] border border-dashed border-[var(--app-primary)]'
  }

  // completed / active share the same fill colours
  if (p === 'critical') return 'bg-rose-500 text-white'
  if (p === 'high') return 'bg-amber-500 text-white'
  return 'bg-[var(--app-primary)] text-white'
}

function TechTimelineRow({ tech, range }: { tech: DerivedTech; range: TimelineRange }) {
  const jobs = useMemo(() => {
    return tech.todayJobs.filter((wo) => {
      const refDate = wo.workStartedAt ?? wo.dueDate ?? wo.createdAt
      if (range === 'today') return isToday(refDate)
      if (range === 'tomorrow') return isTomorrow(refDate)
      return true
    })
  }, [tech.todayJobs, range])

  const blocks = useMemo(() => buildBlocks(jobs), [jobs])

  return (
    <div className="flex items-center border-t border-app">
      <div className="w-32 shrink-0 px-3 py-2">
        <p className="truncate text-[11px] font-semibold text-app">{tech.record.fullName}</p>
        <TechStatusBadge status={tech.status} />
      </div>
      <div className="relative flex-1 h-10 bg-[var(--app-surface-strong)] mx-2 rounded-lg overflow-hidden">
        {blocks.map((block, i) => {
          const left = timelinePercent(block.startH)
          const width = Math.max(timelinePercent(block.endH) - left, 1)
          const title = [
            `${block.wo.workOrderNumber}: ${block.wo.title}`,
            block.kind === 'active' ? '(In progress)' : '',
            block.kind === 'scheduled' ? '(Scheduled — not yet started)' : '',
            block.isConflict ? '⚠ Scheduling conflict' : '',
          ].filter(Boolean).join(' ')
          return (
            <Link
              key={`${block.wo.id}-${i}`}
              to={`/work-orders/${block.wo.id}`}
              title={title}
              className={`absolute top-1 h-8 rounded px-1 text-[9px] font-bold flex items-center overflow-hidden hover:opacity-80 transition-opacity ${woBlockClasses(block.wo, block.isConflict, block.kind)}`}
              style={{ left: `${left}%`, width: `${width}%` }}
            >
              <span className="truncate flex-1">{block.wo.workOrderNumber}</span>
              {/* Pulsing right-edge indicator for in-progress ("active") blocks */}
              {block.kind === 'active' && !block.isConflict ? (
                <span className="absolute right-0 top-0 h-full w-1 rounded-r bg-white/50 animate-pulse" />
              ) : null}
            </Link>
          )
        })}
        {blocks.length === 0 ? (
          <div className="absolute inset-0 flex items-center justify-center">
            <span className="text-[10px] text-muted/50">No jobs</span>
          </div>
        ) : null}
      </div>
    </div>
  )
}

function TimelineView({ techs, range, onRangeChange }: {
  techs: DerivedTech[]
  range: TimelineRange
  onRangeChange: (r: TimelineRange) => void
}) {
  // Only show techs with jobs
  const activeTechs = useMemo(
    () => techs.filter((t) => t.todayJobs.length > 0 || t.currentJob),
    [techs],
  )

  const hours = useMemo(() => {
    const out: number[] = []
    for (let h = TIMELINE_START_H; h <= TIMELINE_END_H; h += 2) out.push(h)
    return out
  }, [])

  return (
    <div className="surface-card rounded-[24px] overflow-hidden">
      <div className="flex items-center justify-between border-b border-app px-4 py-3">
        <div className="flex items-center gap-3">
          <h2 className="text-sm font-bold text-app">Schedule Timeline</h2>
          <span className="text-[11px] text-muted">{activeTechs.length} technicians</span>
        </div>
        <div className="flex gap-1">
          {(['today', 'tomorrow', 'week'] as const).map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => onRangeChange(r)}
              className={`rounded-full px-2.5 py-1 text-[10px] font-semibold capitalize transition-colors ${
                range === r
                  ? 'bg-[var(--app-primary)] text-white'
                  : 'bg-[var(--app-surface-strong)] text-muted hover:text-app'
              }`}
            >
              {r}
            </button>
          ))}
        </div>
      </div>

      {/* Hour ruler */}
      <div className="flex border-b border-app">
        <div className="w-32 shrink-0" />
        <div className="relative flex-1 mx-2 h-6">
          {hours.map((h) => {
            const left = timelinePercent(h)
            return (
              <span
                key={h}
                className="absolute top-1 text-[10px] text-muted -translate-x-1/2"
                style={{ left: `${left}%` }}
              >
                {h < 12 ? `${h}am` : h === 12 ? '12pm' : `${h - 12}pm`}
              </span>
            )
          })}
        </div>
      </div>

      {/* Tech rows */}
      <div className="max-h-72 overflow-y-auto">
        {activeTechs.length === 0 ? (
          <div className="flex items-center justify-center py-10 text-sm text-muted">
            No technicians with scheduled jobs for this period
          </div>
        ) : (
          activeTechs.map((tech) => (
            <TechTimelineRow key={tech.record.id} tech={tech} range={range} />
          ))
        )}
      </div>

      {/* Legend */}
      <div className="border-t border-app px-4 py-2 flex flex-wrap gap-4 items-center">
        <div className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-6 rounded bg-[var(--app-primary)]" />
          <span className="text-[10px] text-muted">Completed</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-6 rounded bg-amber-500" />
          <span className="text-[10px] text-muted">High priority</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-6 rounded bg-[var(--app-primary)] relative overflow-hidden">
            <span className="absolute right-0 top-0 h-full w-1 bg-white/50" />
          </span>
          <span className="text-[10px] text-muted">In progress (live edge)</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-6 rounded border border-dashed border-[var(--app-primary)] bg-[var(--app-primary)]/15" />
          <span className="text-[10px] text-muted">Scheduled (not started)</span>
        </div>
        <div className="flex items-center gap-1.5">
          <span className="inline-block h-3 w-6 rounded bg-rose-500" />
          <span className="text-[10px] text-muted">Critical / Conflict</span>
        </div>
        <span className="text-[10px] text-muted ml-auto">Overlapping blocks = scheduling conflict</span>
      </div>
    </div>
  )
}

// ─────────────────────────────────────────────────────────────────────────────
// Main page
// ─────────────────────────────────────────────────────────────────────────────

export function DispatchBoardPage() {
  const { selectedBranchId } = useShellContext()
  const { pushToast } = useToast()

  const [workOrders, setWorkOrders] = useState<WorkOrder[]>([])
  const [technicians, setTechnicians] = useState<TechnicianRecord[]>([])
  const [assignmentGroups, setAssignmentGroups] = useState<AssignmentGroupRecord[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [lastRefresh, setLastRefresh] = useState<Date | null>(null)
  const [isRefreshing, setIsRefreshing] = useState(false)

  // UI state
  const [activeView, setActiveView] = useState<DispatchWorkspaceView>('queue')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [selectedTech, setSelectedTech] = useState<DerivedTech | null>(null)
  const [assigningJob, setAssigningJob] = useState<WorkOrder | null>(null)
  const [timelineRange, setTimelineRange] = useState<TimelineRange>('today')

  const abortRef = useRef<AbortController | null>(null)
  const hub = useServiceOpsHub()

  const fetchAll = useCallback(async (isBackground = false) => {
    abortRef.current?.abort()
    const ctrl = new AbortController()
    abortRef.current = ctrl

    if (!isBackground) setLoading(true)
    else setIsRefreshing(true)
    setError(null)

    try {
      const [wos, techs, groups] = await Promise.all([
        workOrderService.list(selectedBranchId, ctrl.signal),
        technicianService.list(selectedBranchId, ctrl.signal),
        settingsService.listAssignmentGroups(ctrl.signal).catch(() => [] as AssignmentGroupRecord[]),
      ])
      setWorkOrders(wos)
      setTechnicians(techs)
      setAssignmentGroups(groups)
      setLastRefresh(new Date())
    } catch (err) {
      if ((err as { name?: string }).name === 'AbortError') return
      const msg = err instanceof Error ? err.message : 'Failed to load dispatch data'
      if (!isBackground) setError(msg)
    } finally {
      if (!isBackground) setLoading(false)
      else setIsRefreshing(false)
    }
  }, [selectedBranchId])

  // Initial load
  useEffect(() => {
    void fetchAll(false)
  }, [fetchAll])

  // Poll every 15s
  useEffect(() => {
    const timer = window.setInterval(() => {
      void fetchAll(true)
    }, 15_000)
    return () => window.clearInterval(timer)
  }, [fetchAll])

  // Real-time: re-fetch immediately when a work order changes via SignalR
  useEffect(() => {
    hub.on('WorkOrderUpdated', () => { void fetchAll(true) })
    return () => hub.off('WorkOrderUpdated')
  }, [hub, fetchAll])

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      abortRef.current?.abort()
    }
  }, [])

  // Derive technician data
  const derivedTechs = useMemo<DerivedTech[]>(() => {
    const today = new Date()
    const todayStr = `${today.getFullYear()}-${today.getMonth()}-${today.getDate()}`

    return technicians.map((record) => {
      const status = deriveTechStatus(record)
      const currentJob = record.activeWorkOrderId
        ? (workOrders.find((wo) => wo.id === record.activeWorkOrderId) ?? null)
        : null

      // Today's jobs: WOs assigned to this tech where dueDate or workStartedAt is today
      const todayJobs = workOrders.filter((wo) => {
        const isAssigned =
          wo.assignedTechnicianId === record.id
          || wo.leadTechnicianId === record.id
          || (wo.technicianAssignments?.some((a) => a.technicianId === record.id) ?? false)
        if (!isAssigned) return false
        const ref = wo.workStartedAt ?? wo.dueDate ?? wo.createdAt
        const d = new Date(ref)
        return `${d.getFullYear()}-${d.getMonth()}-${d.getDate()}` === todayStr
      })

      return { record, status, currentJob, todayJobs }
    })
  }, [technicians, workOrders])

  // Unassigned jobs
  const unassignedJobs = useMemo(
    () => workOrders.filter(isUnassignedWO),
    [workOrders],
  )
  const scheduledTodayCount = useMemo(
    () => derivedTechs.filter((tech) => tech.todayJobs.length > 0).length,
    [derivedTechs],
  )
  const dispatchTabs = useMemo<Array<{ id: DispatchWorkspaceView; label: string }>>(
    () => [
      { id: 'queue', label: `Queue (${unassignedJobs.length})` },
      { id: 'technicians', label: `Technicians (${derivedTechs.length})` },
      { id: 'map', label: 'Live Map' },
      { id: 'timeline', label: `Timeline (${scheduledTodayCount})` },
    ],
    [derivedTechs.length, scheduledTodayCount, unassignedJobs.length],
  )
  const activeViewDescription = useMemo(() => {
    switch (activeView) {
      case 'queue':
        return 'Review and assign unallocated work orders without competing with technician tables or maps.'
      case 'technicians':
        return 'Monitor field status, filter technician availability, and drill straight into individual dispatch capacity.'
      case 'map':
        return 'Work from the live location view only, with tracked technicians and open unassigned pins in one surface.'
      case 'timeline':
        return 'Focus on the scheduled day plan without scrolling past queue or map content.'
    }
  }, [activeView])

  function handleAssigned(updated: WorkOrder) {
    setWorkOrders((prev) =>
      prev.map((wo) => (wo.id === updated.id ? updated : wo)),
    )
  }

  function handleAssignFromTechSlideOver(tech: DerivedTech) {
    // Pick the top unassigned job to assign (open the assign modal with no pre-selected job)
    // We'll just close the slide-over and open the first unassigned job
    setSelectedTech(null)
    if (unassignedJobs.length > 0) {
      setAssigningJob(unassignedJobs[0])
    } else {
      pushToast({ tone: 'info', title: 'No unassigned jobs', description: `All work orders are currently assigned. ${tech.record.fullName} is ready.` })
    }
  }

  if (loading) return <LoadingState label="Loading Dispatch Centre" />
  if (error) return <ErrorState title="Unable to load Dispatch Centre" description={error} />

  return (
    <div className="flex flex-col gap-4 pb-8">
      {/* Page header */}
      <section className="surface-card rounded-[28px] p-5">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.2em] text-muted">Operations</p>
            <h1 className="mt-1 text-[2rem] font-semibold tracking-[-0.04em] text-app">Dispatch Centre</h1>
            <p className="mt-2 text-sm text-muted">{activeViewDescription}</p>
            <p className="mt-1 text-sm text-secondary">
              Live Ops · {derivedTechs.length} technicians · {unassignedJobs.length} unassigned jobs
            </p>
          </div>
          <div className="flex items-center gap-2 self-start sm:self-auto">
            <button
              type="button"
              onClick={() => void fetchAll(true)}
              className="inline-flex items-center gap-1.5 rounded-full bg-[var(--app-primary-soft)] px-3 py-1.5 text-xs font-semibold text-[var(--app-primary-strong)] hover:bg-[var(--app-primary)]/15"
              disabled={isRefreshing}
            >
              <RefreshCw className={`size-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
              Refresh
            </button>
            <span className="inline-flex items-center gap-1 rounded-full bg-[var(--app-primary-soft)] px-3 py-1 text-xs font-semibold text-[var(--app-primary-strong)]">
              <span className="size-1.5 rounded-full bg-emerald-500 animate-pulse inline-block" />
              Live
            </span>
          </div>
        </div>
      </section>

      <section className="surface-card rounded-[24px] px-4 py-3">
        <PageTabs tabs={dispatchTabs} activeTab={activeView} onChange={setActiveView} />
      </section>

      {activeView === 'queue' ? (
        <div style={{ minHeight: 'calc(100vh - 21rem)' }}>
          <UnassignedJobsPanel
            jobs={unassignedJobs}
            onAssign={setAssigningJob}
          />
        </div>
      ) : null}

      {activeView === 'technicians' ? (
        <div className="space-y-4">
          <StatusStrip
            techs={derivedTechs}
            filter={statusFilter}
            onFilter={setStatusFilter}
            lastRefresh={lastRefresh}
            isRefreshing={isRefreshing}
            onRefresh={() => fetchAll(true)}
          />

          <div style={{ minHeight: 'calc(100vh - 24rem)' }}>
            <TechnicianTable
              techs={derivedTechs}
              statusFilter={statusFilter}
              onSelectTech={setSelectedTech}
            />
          </div>
        </div>
      ) : null}

      {activeView === 'map' ? (
        <DispatchMapView
          technicians={technicians}
          workOrders={workOrders}
          onTechnicianClick={(tech) => {
            const derived = derivedTechs.find((d) => d.record.id === tech.id) ?? null
            if (derived) setSelectedTech(derived)
          }}
          onWorkOrderClick={(wo) => setAssigningJob(wo)}
        />
      ) : null}

      {activeView === 'timeline' ? (
        <div className="space-y-4">
          <section className="surface-card rounded-[24px] px-4 py-3">
            <div className="flex flex-wrap items-center gap-2">
              <Clock className="size-4 text-[var(--app-primary)]" />
              <span className="text-sm font-bold text-app">Scheduled Timeline</span>
              <Badge tone="info">{scheduledTodayCount} techs scheduled today</Badge>
            </div>
          </section>

          <TimelineView
            techs={derivedTechs}
            range={timelineRange}
            onRangeChange={setTimelineRange}
          />
        </div>
      ) : null}

      {/* Tech slide-over */}
      {selectedTech ? (
        <TechSlideOver
          tech={selectedTech}
          onClose={() => setSelectedTech(null)}
          onAssignJob={handleAssignFromTechSlideOver}
        />
      ) : null}

      {assigningJob ? (
        <AssignModal
          job={assigningJob}
          techs={derivedTechs}
          groups={assignmentGroups}
          onClose={() => setAssigningJob(null)}
          onAssigned={handleAssigned}
        />
      ) : null}
    </div>
  )
}
