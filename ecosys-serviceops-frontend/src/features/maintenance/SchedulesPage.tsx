import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { usePmPlans } from './api'
import { NewScheduleWizard } from './components/NewScheduleWizard'
import { PM_CONTRACT_LAPSED, type PmPlan } from './types'
import { MaintenanceTabNav } from './components/MaintenanceTabNav'
import { ClientFilterSelect, useMaintenanceClientFilter } from './components/ClientFilter'

function coverageBadge(plan: PmPlan) {
  if (plan.status === PM_CONTRACT_LAPSED) return 'Contract Lapsed'
  if (plan.status !== 'Active') return 'Inactive'
  if (!plan.nextPmDate) return 'Unscheduled'
  const days = Math.floor((new Date(plan.nextPmDate).getTime() - Date.now()) / 86_400_000)
  if (days < 0) return 'Overdue'
  if (days <= 7) return 'Due Soon'
  return 'On Track'
}

export function SchedulesPage() {
  const canManage = usePermission('maintenance.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showWizard, setShowWizard] = useState(false)
  const { clientId, setClientId } = useMaintenanceClientFilter()
  const { data, isLoading, isError } = usePmPlans()

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data ?? []).filter((plan) =>
      (!clientId || plan.clientId === clientId) &&
      [plan.assetName, plan.clientName, plan.pmTemplateName].some((value) => (value ?? '').toLowerCase().includes(query)),
    )
  }, [data, debouncedSearch, clientId])

  const columns: DataTableColumn<PmPlan>[] = [
    { key: 'assetName', header: 'Asset', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/maintenance/schedules/${row.id}`}>{row.assetName ?? '—'}</Link> },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'pmTemplateName', header: 'Template', cell: (row) => row.pmTemplateName ?? '—' },
    { key: 'frequency', header: 'Frequency', cell: (row) => row.frequency },
    { key: 'nextPmDate', header: 'Next Due', cell: (row) => (row.nextPmDate ? formatDate(row.nextPmDate) : '—') },
    { key: 'coverage', header: 'Coverage', cell: (row) => <StatusBadge status={coverageBadge(row)} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="PM Schedules"
        subtitle="Create, edit, and manage preventive maintenance schedules."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowWizard(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Schedule
            </button>
          ) : undefined
        }
      />
      <MaintenanceTabNav />

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by asset, client, or template" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        <ClientFilterSelect value={clientId} onChange={setClientId} />
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'Schedules unavailable' : clientId || debouncedSearch ? 'No matching schedules' : 'No PM schedules yet'}
        emptyDescription={isError ? 'PM plans could not be loaded.' : clientId || debouncedSearch ? 'No PM schedules match the current filters.' : 'Create your first preventive maintenance schedule to get started.'}
      />

      <NewScheduleWizard open={showWizard} onClose={() => setShowWizard(false)} />
    </div>
  )
}
