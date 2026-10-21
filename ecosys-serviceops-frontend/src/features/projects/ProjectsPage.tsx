import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search, Settings2, Contact, X } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useUrlFilters } from '../../shared/hooks/useUrlFilters'
import { parseIdList } from '../../shared/utils/urlFilters'
import { formatCurrency } from '../../shared/utils/format'
import { formatDate } from '../../shared/utils/dates'
import { getActiveCurrency } from '../../shared/utils/currency'
import { useProjects, useProjectSummary, useProjectTypes } from './api'
import { ProjectFormDrawer } from './components/ProjectFormDrawer'
import { ProgressBar, StageBadge } from './components/ProjectBadges'
import { primaryButtonClass, secondaryButtonClass } from './components/formStyles'
import { filterProjects, projectListFilterSpec, stageCategoryFor, stageParamFor } from './listFilters'
import { stageCategories, stageCategoryLabels, type ProjectListItem } from './types'

const filterClass = 'h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]'

export function ProjectsPage() {
  const canManage = usePermission('projects.manage')
  const canConfigure = usePermission('projects.settings')
  const [search, setSearch] = useState('')
  // Stage, health, "mine" and the dashboard's id list live in the URL, so figures open the register filtered.
  const { filters, update } = useUrlFilters(projectListFilterSpec)
  const stageCategory = stageCategoryFor(filters.stage)
  const linkedIds = parseIdList(filters.ids).length
  const [projectTypeId, setProjectTypeId] = useState('')
  const [showForm, setShowForm] = useState(false)
  const debouncedSearch = useDebounce(search, 300)
  const { data: summary } = useProjectSummary()
  const { data: types } = useProjectTypes()
  const { data, isLoading, isError } = useProjects({
    ...(stageCategory ? { stageCategory } : {}),
    ...(projectTypeId ? { projectTypeId } : {}),
    ...(filters.mine ? { mine: true } : {}),
  })

  const rows = useMemo(() => {
    const narrowed = filterProjects(data ?? [], filters)
    const query = debouncedSearch.trim().toLowerCase()
    if (!query) return narrowed
    return narrowed.filter((project) => [project.projectNumber, project.name, project.clientName, project.siteName]
      .some((value) => (value ?? '').toLowerCase().includes(query)))
  }, [data, debouncedSearch, filters])

  const currency = rows[0]?.currency ?? getActiveCurrency()

  const columns: DataTableColumn<ProjectListItem>[] = [
    {
      key: 'projectNumber',
      header: 'Project',
      cell: (row) => (
        <div className="min-w-0">
          <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/projects/${row.id}`}>{row.name}</Link>
          <div className="font-mono text-xs text-slate-500">{row.projectNumber}</div>
        </div>
      ),
    },
    { key: 'clientName', header: 'Client', cell: (row) => (<div><div>{row.clientName}</div>{row.siteName ? <div className="text-xs text-slate-500">{row.siteName}</div> : null}</div>) },
    { key: 'projectTypeName', header: 'Type', cell: (row) => row.projectTypeName },
    { key: 'stageName', header: 'Stage', cell: (row) => <StageBadge name={row.stageName} category={row.stageCategory} /> },
    { key: 'progressPercent', header: 'Progress', cell: (row) => <ProgressBar actual={row.progressPercent} planned={row.plannedProgressPercent} behind={row.isBehindSchedule} compact /> },
    { key: 'revisedValue', header: 'Value', cell: (row) => formatCurrency(row.revisedValue, row.currency) },
    {
      key: 'plannedEndDate',
      header: 'Due',
      cell: (row) => row.plannedEndDate ? (
        <div>
          <div>{formatDate(row.plannedEndDate)}</div>
          {row.daysRemaining != null ? (
            <div className={row.daysRemaining < 0 ? 'text-xs font-semibold text-rose-600' : 'text-xs text-slate-500'}>
              {row.daysRemaining < 0 ? `${Math.abs(row.daysRemaining)}d late` : `${row.daysRemaining}d left`}
            </div>
          ) : null}
        </div>
      ) : '—',
    },
    { key: 'projectManagerName', header: 'Manager', cell: (row) => row.projectManagerName ?? '—' },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Projects"
        subtitle="Installations, fit-outs and other finite work, from award to handover."
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            <Link to="/projects/consultants" className={secondaryButtonClass}><Contact className="h-4 w-4" />Consultants</Link>
            {canConfigure ? <Link to="/projects/settings" className={secondaryButtonClass}><Settings2 className="h-4 w-4" />Settings</Link> : null}
          </div>
        }
        primaryAction={canManage ? (
          <button type="button" onClick={() => setShowForm(true)} className={primaryButtonClass}><Plus className="h-4 w-4" />New Project</button>
        ) : undefined}
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="Open projects" value={summary ? `${summary.openProjects} (${summary.activeProjects} active)` : '—'} />
        <StatCard label="Open contract value" value={summary ? formatCurrency(summary.totalRevisedValue, currency) : '—'} />
        <StatCard label="Behind schedule" value={summary?.behindSchedule ?? '—'} tone={summary && summary.behindSchedule > 0 ? 'warning' : 'default'} />
        <StatCard
          label="Pending variations"
          value={summary ? `${summary.pendingVariationsCount} · ${formatCurrency(summary.pendingVariationsValue, currency)}` : '—'}
          tone={summary && summary.pendingVariationsCount > 0 ? 'warning' : 'default'}
        />
      </div>

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by project, number, client or site" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        <select aria-label="Stage" value={stageCategory} onChange={(event) => update({ stage: stageParamFor(event.target.value) })} className={filterClass}>
          <option value="Open">Open projects</option>
          <option value="">All projects</option>
          {stageCategories.map((category) => (<option key={category} value={category}>{stageCategoryLabels[category]}</option>))}
        </select>
        <select aria-label="Type" value={projectTypeId} onChange={(event) => setProjectTypeId(event.target.value)} className={filterClass}>
          <option value="">All types</option>
          {(types ?? []).map((type) => (<option key={type.id} value={type.id}>{type.name}</option>))}
        </select>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={filters.mine === 'true'} onChange={(event) => update({ mine: event.target.checked ? 'true' : '' })} />
          Mine
        </label>
        <label className="flex items-center gap-2 whitespace-nowrap text-sm text-slate-600">
          <input type="checkbox" checked={filters.health === 'behind'} onChange={(event) => update({ health: event.target.checked ? 'behind' : '' })} />
          Behind schedule
        </label>
        {linkedIds > 0 ? (
          <button type="button" onClick={() => update({ ids: '' })} className="inline-flex h-11 items-center gap-1.5 whitespace-nowrap rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-700 hover:bg-slate-50">
            {linkedIds} from the dashboard<X className="h-3.5 w-3.5" />
          </button>
        ) : null}
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Projects unavailable' : 'No projects here'}
        emptyDescription={isError ? 'Projects could not be loaded.' : canManage ? 'Create a project for a client to start tracking it.' : 'Projects you can see will appear here.'}
      />

      <ProjectFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
