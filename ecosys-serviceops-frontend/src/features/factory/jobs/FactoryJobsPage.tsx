import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { AlertOctagon, Plus, Repeat, Search } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../../shared/components/DataTable'
import { Badge } from '../../../shared/components/Badge'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { CriticalityBadge, LocationSelect, inputClass, primaryButton, dangerButton } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useJobs, type JobFilters } from './api'
import { NewJobDrawer } from './NewJobDrawer'
import { JobStatusBadge } from './JobStatusBadge'
import { PRIORITIES, WORK_TYPES, type JobSummary } from './types'

const VIEWS: { id: NonNullable<JobFilters['view']>; label: string }[] = [
  { id: 'open', label: 'Open' },
  { id: 'approval', label: 'To approve' },
  { id: 'verify', label: 'To verify' },
  { id: 'overdue', label: 'Overdue' },
  { id: 'closed', label: 'Done' },
  { id: 'all', label: 'All' },
]

/** Factory jobs: breakdowns first, then by priority. */
export function FactoryJobsPage() {
  const links = useFactoryLinks()
  const canCreate = usePermission('work-orders.create')
  const canAssign = usePermission('work-orders.assign')
  const canRaise = canCreate || canAssign
  const [view, setView] = useState<NonNullable<JobFilters['view']>>('open')
  const [mine, setMine] = useState(links.tech)
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 300)
  const [orgNodeId, setOrgNodeId] = useState('')
  const [workType, setWorkType] = useState('')
  const [priority, setPriority] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [drawer, setDrawer] = useState<'job' | 'breakdown' | null>(null)
  useEffect(() => setPage(1), [view, mine, debounced, orgNodeId, workType, priority])

  const { data, isLoading, isError } = useJobs({
    view, mine: mine || undefined, search: debounced || undefined, orgNodeId: orgNodeId || undefined,
    workType: workType || undefined, priority: priority || undefined,
  }, page, pageSize)

  const columns: DataTableColumn<JobSummary>[] = [
    {
      key: 'job',
      header: 'Job',
      cell: (row) => (
        <div className="min-w-48">
          <Link to={links.job(row.id)} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.title}</Link>
          <span className="mt-0.5 flex flex-wrap items-center gap-1 text-xs text-slate-500">
            <span className="font-mono">{row.workOrderNumber}</span>
            {row.isBreakdown ? <Badge tone="danger" size="sm"><AlertOctagon className="h-3 w-3" />Breakdown{row.downtimeEndedAt ? '' : ', down'}</Badge> : null}
            {row.isRework ? <Badge tone="warning" size="sm"><Repeat className="h-3 w-3" />Rework</Badge> : null}
            {row.needsLineStop ? <Badge size="sm">Line stop</Badge> : null}
          </span>
        </div>
      ),
    },
    { key: 'asset', header: 'Asset', cell: (row) => row.assetCode ? <span><span className="font-mono text-xs">{row.assetCode}</span> <CriticalityBadge value={row.assetCriticality as 'A' | 'B' | 'C' | null} /><span className="block text-xs text-slate-500">{row.orgNodeName}</span></span> : <span className="text-slate-500">{row.orgNodeName ?? '—'}</span> },
    { key: 'type', header: 'Type', cell: (row) => <span className="text-sm">{row.workType}<span className="block text-xs text-slate-500">{row.priority}</span></span> },
    { key: 'status', header: 'Status', cell: (row) => <JobStatusBadge status={row.status} holdReason={row.holdReason} /> },
    { key: 'who', header: 'Assigned', cell: (row) => row.leadTechnicianName ?? <span className="text-slate-400">—</span> },
    {
      key: 'due',
      header: 'Due',
      cell: (row) => {
        const due = row.dueDate ?? row.repairDueAt
        const late = due && !row.completedAt && new Date(due) < new Date()
        return due ? <span className={cn('whitespace-nowrap text-sm', late && 'font-medium text-rose-700')}>{formatDate(due)}</span> : '—'
      },
    },
    { key: 'raised', header: 'Raised', cell: (row) => <span className="whitespace-nowrap text-xs text-slate-500">{formatDateTime(row.createdAt)}</span> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title={links.tech ? 'Factory jobs' : 'Maintenance jobs'}
        subtitle="Breakdowns, corrective and planned work in the factory."
        primaryAction={canRaise ? <button type="button" className={cn(primaryButton, 'h-11')} onClick={() => setDrawer('job')}><Plus className="h-4 w-4" />Raise job</button> : undefined}
        secondaryActions={canRaise ? <button type="button" className={dangerButton} onClick={() => setDrawer('breakdown')}><AlertOctagon className="h-4 w-4" />Report breakdown</button> : undefined}
      />

      <div className="flex flex-wrap items-center gap-2">
        <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {VIEWS.map((item) => (
            <button key={item.id} type="button" role="tab" aria-selected={view === item.id} onClick={() => setView(item.id)}
              className={cn('h-9 rounded-md px-3 text-sm font-medium', view === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>
              {item.label}
            </button>
          ))}
        </div>
        <label className="inline-flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={mine} onChange={(event) => setMine(event.target.checked)} />Only mine</label>
      </div>

      <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-3 shadow-sm sm:grid-cols-2 lg:grid-cols-5">
        <label className="flex items-center gap-2 lg:col-span-2">
          <Search className="h-4 w-4 shrink-0 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search job number, title or asset" className="h-10 w-full border-0 bg-transparent text-sm outline-none" />
        </label>
        <LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups />
        <select aria-label="Type" className={inputClass} value={workType} onChange={(event) => setWorkType(event.target.value)}>
          <option value="">Any type</option>
          {WORK_TYPES.map((type) => <option key={type}>{type}</option>)}
        </select>
        <select aria-label="Priority" className={inputClass} value={priority} onChange={(event) => setPriority(event.target.value)}>
          <option value="">Any priority</option>
          {PRIORITIES.map((value) => <option key={value}>{value}</option>)}
        </select>
      </div>

      <DataTable
        columns={columns}
        data={data?.items ?? []}
        loading={isLoading}
        emptyTitle={isError ? 'Jobs unavailable' : 'No jobs here'}
        emptyDescription={isError ? 'The list could not be loaded.' : 'Nothing matches this view.'}
        pagination={{ page, pageSize, total: data?.totalCount ?? 0, onPageChange: setPage, onPageSizeChange: (size) => { setPageSize(size); setPage(1) } }}
      />

      <NewJobDrawer open={drawer !== null} breakdown={drawer === 'breakdown'} onClose={() => setDrawer(null)} />
    </div>
  )
}
