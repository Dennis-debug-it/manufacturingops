import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search, X } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { useUrlFilters } from '../../shared/hooks/useUrlFilters'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { useContracts } from './api'
import { ContractFormDrawer } from './components/ContractFormDrawer'
import { contractListFilterSpec, filterContracts } from './listFilters'
import type { ContractListItem } from './types'

function expiryClass(days: number) {
  if (days < 0) return 'text-rose-700 font-semibold'
  if (days < 30) return 'text-rose-600 font-semibold'
  if (days < 60) return 'text-amber-600 font-semibold'
  return 'text-slate-700'
}

export function ContractsPage() {
  const canManage = usePermission('contracts.manage')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [showForm, setShowForm] = useState(false)
  const { data, isLoading, isError } = useContracts()
  // `?expiring=` comes from the dashboard's "Contracts expiring" figures.
  const { filters, update } = useUrlFilters(contractListFilterSpec)

  const rows = useMemo(() => {
    const query = debouncedSearch.trim().toLowerCase()
    return filterContracts(data ?? [], filters.expiring)
      .filter((contract) => [contract.contractNumber, contract.clientName, contract.name].some((value) => (value ?? '').toLowerCase().includes(query)))
  }, [data, debouncedSearch, filters.expiring])

  const columns: DataTableColumn<ContractListItem>[] = [
    { key: 'contractNumber', header: 'Contract', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/contracts/${row.id}`}>{row.contractNumber}</Link> },
    { key: 'name', header: 'Name', cell: (row) => row.name },
    { key: 'clientName', header: 'Client', cell: (row) => row.clientName ?? '—' },
    { key: 'contractType', header: 'Type', cell: (row) => row.contractType },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'endDate', header: 'End Date', cell: (row) => <span className={cn(expiryClass(row.daysToExpiry))}>{formatDate(row.endDate)} ({row.daysToExpiry}d)</span> },
    { key: 'renewalRisk', header: 'Renewal Risk', cell: (row) => <StatusBadge status={row.renewalRisk} /> },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="Contracts"
        subtitle="Contract list, expiry, and renewal filters."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />New Contract
            </button>
          ) : undefined
        }
      />

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm sm:flex-row sm:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search by contract number, client, or name" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        <select aria-label="Expiring" value={filters.expiring} onChange={(event) => update({ expiring: event.target.value })} className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]">
          <option value="">Any end date</option>
          <option value="30">Ending within 30 days</option>
          <option value="60">Ending within 60 days</option>
          <option value="90">Ending within 90 days</option>
        </select>
        {filters.expiring ? (
          <button type="button" onClick={() => update({ expiring: '' })} aria-label="Clear expiry filter" className="inline-flex h-11 w-11 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-50">
            <X className="h-4 w-4" />
          </button>
        ) : null}
      </div>

      <DataTable columns={columns} data={rows} loading={isLoading} emptyTitle={isError ? 'Contracts unavailable' : 'No contracts yet'} emptyDescription={isError ? 'Contracts could not be loaded.' : 'Create your first contract to get started.'} />

      <ContractFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
