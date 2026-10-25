import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Plus, Search } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatCard } from '../../shared/components/StatCard'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { formatCurrency } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { useSalesCurrency, useTenders } from './api'
import { CrmNav } from './components/CrmNav'
import { OwnerSelect } from './components/OwnerSelect'
import { TenderFormDrawer } from './components/TenderFormDrawer'
import {
  TENDER_STATUSES,
  daysUntilDeadline,
  tenderStatusLabels,
  type SalesTenderListItem,
} from './types'

const selectClass = 'h-10 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

/** "Due in 3 days", "Due today", "4 days overdue" — coloured by urgency while the bid is still ours to send. */
export function DeadlineBadge({ deadline, status }: { deadline: string; status: SalesTenderListItem['status'] }) {
  const awaitingUs = status === 'Received' || status === 'Reviewing' || status === 'Preparing'
  if (!awaitingUs) return <span className="text-sm text-slate-500">{formatDate(deadline)}</span>
  const days = daysUntilDeadline(deadline)
  const label = days < 0 ? `${-days} day${days === -1 ? '' : 's'} overdue` : days === 0 ? 'Due today' : `Due in ${days} day${days === 1 ? '' : 's'}`
  return (
    <span className="text-sm">
      <span className="whitespace-nowrap text-slate-700">{formatDate(deadline)}</span>
      <span className={cn('ml-2 inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium', days < 0 ? 'bg-rose-100 text-rose-700' : days <= 3 ? 'bg-amber-100 text-amber-800' : 'bg-slate-100 text-slate-600')}>
        {label}
      </span>
    </span>
  )
}

export function TendersPage() {
  const canManage = usePermission('crm.manage')
  const [search, setSearch] = useState('')
  const [type, setType] = useState('')
  const [status, setStatus] = useState('Open')
  const [owner, setOwner] = useState('')
  const [showForm, setShowForm] = useState(false)
  const debouncedSearch = useDebounce(search, 300)
  const { data, isLoading, isError } = useTenders({ search: debouncedSearch, type: type || undefined, status: status || undefined, ownerUserId: owner || undefined })
  // Unfiltered open set for the headline numbers, so they don't move with the table filters.
  const { data: openTenders } = useTenders({ status: 'Open' })
  const { data: currency } = useSalesCurrency()
  const reporting = currency?.reportingCurrency
  const rows = useMemo(() => data ?? [], [data])

  const stats = useMemo(() => {
    const open = openTenders ?? []
    const toSend = open.filter((x) => x.status !== 'Submitted')
    return {
      toSend: toSend.length,
      dueThisWeek: toSend.filter((x) => { const d = daysUntilDeadline(x.submissionDeadline); return d >= 0 && d <= 7 }).length,
      overdue: toSend.filter((x) => daysUntilDeadline(x.submissionDeadline) < 0).length,
      awaiting: open.filter((x) => x.status === 'Submitted').length,
      awaitingValue: open.filter((x) => x.status === 'Submitted').reduce((sum, x) => sum + (x.convertedValue ?? 0), 0),
    }
  }, [openTenders])

  const columns: DataTableColumn<SalesTenderListItem>[] = [
    {
      key: 'title',
      header: 'Request',
      cell: (row) => (
        <Link to={`/crm/tenders/${row.id}`} className="block min-w-0">
          <span className="mr-2 rounded bg-slate-100 px-1.5 py-0.5 text-xs font-semibold text-slate-700">{row.type}</span>
          <span className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{row.title}</span>
          {row.reference ? <span className="block text-xs text-slate-500">{row.reference}</span> : null}
        </Link>
      ),
    },
    { key: 'issuer', header: 'Issued by', cell: (row) => row.clientName ?? row.issuerName ?? '—' },
    { key: 'submissionDeadline', header: 'Deadline', cell: (row) => <DeadlineBadge deadline={row.submissionDeadline} status={row.status} /> },
    {
      key: 'value',
      header: 'Value',
      cell: (row) => {
        const amount = row.submittedValue ?? row.estimatedValue
        return (
          <span>
            {formatCurrency(amount, row.currency)}
            {reporting && row.currency !== reporting ? (
              <span className={cn('block text-xs', row.convertedValue === null ? 'text-amber-700' : 'text-slate-500')}>
                {row.convertedValue === null ? 'No rate yet' : `≈ ${formatCurrency(row.convertedValue, reporting)}`}
              </span>
            ) : null}
          </span>
        )
      },
    },
    { key: 'status', header: 'Status', cell: (row) => <span className="whitespace-nowrap"><StatusBadge status={tenderStatusLabels[row.status]} /></span> },
    { key: 'ownerName', header: 'Owner', cell: (row) => row.ownerName ?? '—' },
  ]

  return (
    <div className="space-y-4">
      <PageHeader
        title="RFPs & RFQs"
        subtitle="Requests for proposals and quotations: deadlines, bid decisions and outcomes."
        primaryAction={
          canManage ? (
            <button type="button" onClick={() => setShowForm(true)} className="inline-flex h-11 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)]">
              <Plus className="h-4 w-4" />Log RFP / RFQ
            </button>
          ) : undefined
        }
      />

      <CrmNav />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard label="Bids to send" value={stats.toSend} />
        <StatCard label="Due in the next 7 days" value={stats.dueThisWeek} tone={stats.dueThisWeek > 0 ? 'warning' : 'default'} />
        <StatCard label="Past deadline, not sent" value={stats.overdue} tone={stats.overdue > 0 ? 'danger' : 'default'} />
        <StatCard label="Submitted, awaiting award" value={`${stats.awaiting} · ${formatCurrency(stats.awaitingValue, reporting)}`} />
      </div>

      <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-4 shadow-sm backdrop-blur-[var(--glass-blur-md)] lg:flex-row lg:items-center">
        <label className="flex flex-1 items-center gap-2 text-sm text-slate-600">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search title, reference or issuer" className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400" />
        </label>
        <div className="flex flex-wrap gap-2">
          <select aria-label="Filter by type" value={type} onChange={(event) => setType(event.target.value)} className={selectClass}>
            <option value="">RFPs and RFQs</option>
            <option value="RFQ">RFQs only</option>
            <option value="RFP">RFPs only</option>
          </select>
          <select aria-label="Filter by status" value={status} onChange={(event) => setStatus(event.target.value)} className={selectClass}>
            <option value="Open">Open</option>
            <option value="">All statuses</option>
            {TENDER_STATUSES.map((value) => (
              <option key={value} value={value}>{tenderStatusLabels[value]}</option>
            ))}
          </select>
          <OwnerSelect aria-label="Filter by owner" value={owner} onChange={setOwner} emptyLabel="All owners" className={selectClass} />
        </div>
      </div>

      <DataTable
        columns={columns}
        data={rows}
        loading={isLoading}
        emptyTitle={isError ? 'RFPs & RFQs unavailable' : 'Nothing here'}
        emptyDescription={isError ? 'The list could not be loaded.' : 'Log an RFP or RFQ when one comes in to track its deadline and outcome.'}
      />

      <TenderFormDrawer open={showForm} onClose={() => setShowForm(false)} />
    </div>
  )
}
