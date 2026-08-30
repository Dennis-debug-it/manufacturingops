import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { useTeamManagementOverview } from './api'
import type { TeamManagementProfile } from './types'
import { TeamTabNav } from './components/TeamTabNav'

export function TechniciansPage() {
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const { data, isLoading, isError } = useTeamManagementOverview()

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return (data?.profiles ?? []).filter((profile) => profile.technicianId).filter((profile) => profile.fullName.toLowerCase().includes(query))
  }, [data, debouncedSearch])

  const columns: DataTableColumn<TeamManagementProfile & { id: string }>[] = [
    { key: 'fullName', header: 'Technician', cell: (row) => <Link className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/team/technicians/${row.technicianId}`}>{row.fullName}</Link> },
    { key: 'branchName', header: 'Branch', cell: (row) => row.branchName ?? '—' },
    { key: 'availabilityStatus', header: 'Availability', cell: (row) => <StatusBadge status={row.availabilityStatus} /> },
    { key: 'activeWorkOrderCount', header: 'Active WOs', cell: (row) => row.activeWorkOrderCount },
    { key: 'certifications', header: 'Certifications', cell: (row) => row.certifications.length },
    { key: 'readinessStatus', header: 'Readiness', cell: (row) => <StatusBadge status={row.readinessStatus} /> },
  ]

  return (
    <div className="space-y-6">
      <PageHeader title="Technicians" subtitle="Technician list, skills, and availability." />
      <TeamTabNav />

      <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search technicians" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
      </div>

      <DataTable
        columns={columns}
        data={rows.map((row) => ({ ...row, id: row.technicianId ?? row.userId }))}
        loading={isLoading}
        emptyTitle={isError ? 'Technicians unavailable' : 'No technicians yet'}
        emptyDescription={isError ? 'Technician data could not be loaded.' : 'Technicians linked to workforce profiles will appear here.'}
      />
    </div>
  )
}
