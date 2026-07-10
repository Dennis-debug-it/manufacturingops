import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { Funnel, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { useDebounce } from '../../shared/hooks/useDebounce'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate, formatRelative } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { useWorkOrders } from './api'
import { NewWorkOrderForm } from './components/NewWorkOrderForm'
import { ReportQueueTab } from './components/ReportQueueTab'

export interface WorkOrderRow {
  id: string
  woNo: string
  client: string
  site: string
  asset: string
  type: string
  priority: string
  status: string
  assignedTo: string
  dueDate: string
  lastUpdate: string
}

const asText = (value: unknown) => (typeof value === 'string' ? value : value == null ? '—' : String(value))

const pick = (row: Record<string, unknown>, keys: string[]) => {
  for (const key of keys) {
    const value = row[key]
    if (typeof value === 'string' && value.trim()) {
      return value
    }
  }
  return '—'
}

const normalizeWorkOrders = (payload: unknown): WorkOrderRow[] => {
  const records = Array.isArray(payload)
    ? payload
    : payload && typeof payload === 'object'
      ? (payload as { items?: unknown[]; data?: unknown[] }).items ?? (payload as { data?: unknown[] }).data ?? []
      : []

  return records
    .filter((record): record is Record<string, unknown> => Boolean(record && typeof record === 'object'))
    .map((row, index) => ({
      id: asText(row.id ?? row.workOrderId ?? `${index}`),
      woNo: pick(row, ['workOrderNumber', 'woNo', 'woNumber', 'workOrderNo', 'number', 'code']),
      client: pick(row, ['clientName', 'client', 'customerName']),
      site: pick(row, ['siteName', 'site']),
      asset: pick(row, ['assetName', 'asset']),
      type: pick(row, ['workType', 'type']),
      priority: pick(row, ['priority']),
      status: pick(row, ['status', 'state']),
      assignedTo: pick(row, ['assignmentSummary', 'assignedTechnicianName', 'assignedTo', 'technicianName', 'assignee']),
      dueDate: pick(row, ['dueDate', 'dueAt']),
      lastUpdate: pick(row, ['updatedAt', 'lastUpdate', 'lastUpdatedAt']),
    }))
}

export function WorkOrdersPage() {
  const { user } = useAuth()
  const canCreate = usePermission('work-orders.create')
  const [search, setSearch] = useState('')
  const debouncedSearch = useDebounce(search, 300)
  const [tab, setTab] = useState<'all' | 'queue'>('all')
  const [showNewForm, setShowNewForm] = useState(false)

  const { data, isLoading, isError } = useWorkOrders()

  const rows = useMemo(() => {
    const normalized = normalizeWorkOrders(data)
    const query = debouncedSearch.trim().toLowerCase()
    const filtered = normalized.filter((row) =>
      [row.woNo, row.client, row.site, row.asset, row.type, row.assignedTo, row.status]
        .some((value) => value.toLowerCase().includes(query)),
    )
    if (user?.role === 'technician') {
      return filtered.filter((row) => row.assignedTo === user.fullName || row.assignedTo === '—')
    }
    return filtered
  }, [data, debouncedSearch, user])

  const columns: DataTableColumn<WorkOrderRow>[] = [
    { key: 'woNo', header: 'WO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.id}`}>{row.woNo}</Link> },
    { key: 'client', header: 'Client', cell: (row) => row.client },
    { key: 'site', header: 'Site', cell: (row) => row.site },
    { key: 'asset', header: 'Asset', cell: (row) => row.asset },
    { key: 'type', header: 'Type', cell: (row) => row.type },
    { key: 'priority', header: 'Priority', cell: (row) => <StatusBadge status={row.priority} /> },
    { key: 'status', header: 'Status', cell: (row) => <StatusBadge status={row.status} /> },
    { key: 'assignedTo', header: 'Assigned To', cell: (row) => row.assignedTo },
    { key: 'dueDate', header: 'Due Date', cell: (row) => (row.dueDate && row.dueDate !== '—' ? formatDate(row.dueDate) : '—') },
    { key: 'lastUpdate', header: 'Last Update', cell: (row) => (row.lastUpdate && row.lastUpdate !== '—' ? formatRelative(row.lastUpdate) : '—') },
  ]

  if (isLoading) {
    return <Spinner label="Loading work orders" />
  }

  if (isError) {
    return (
      <EmptyState
        title="Work orders unavailable"
        description="The work order list could not be loaded."
        actionLabel="Reload"
        onAction={() => window.location.reload()}
      />
    )
  }

  return (
    <div className={cn('space-y-6', user?.role === 'technician' ? 'pb-20' : '')}>
      <PageHeader
        title="Work Orders"
        subtitle="Operational backlog, execution, and service reporting."
        primaryAction={
          canCreate ? (
            <button
              type="button"
              onClick={() => setShowNewForm(true)}
              className="inline-flex h-11 items-center gap-2 rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800"
            >
              <Plus className="h-4 w-4" />New Work Order
            </button>
          ) : undefined
        }
      />

      <NewWorkOrderForm open={showNewForm} onClose={() => setShowNewForm(false)} />

      <div className="flex flex-wrap gap-2">
        {[
          ['all', 'All Work Orders'],
          ['queue', 'Report Queue'],
        ].map(([key, label]) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key as 'all' | 'queue')}
            className={cn(
              'inline-flex h-11 items-center gap-2 rounded-full border px-4 text-sm font-medium transition',
              tab === key ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {label}
          </button>
        ))}
      </div>

      {tab === 'all' ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <Funnel className="h-4 w-4 text-slate-400" />
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Search by WO number, client, site, or asset"
              className="h-11 w-full border-0 bg-transparent p-0 outline-none placeholder:text-slate-400"
            />
          </label>
        </div>
      ) : null}

      {tab === 'all' ? (
        <DataTable
          columns={columns}
          data={rows}
          loading={isLoading}
          emptyTitle="No work orders"
          emptyDescription="Once the work order API is connected, records will show here."
          virtualise={rows.length > 100}
        />
      ) : (
        <ReportQueueTab />
      )}
    </div>
  )
}
