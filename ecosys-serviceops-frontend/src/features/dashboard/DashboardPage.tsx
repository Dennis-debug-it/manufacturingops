import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  Activity,
  AlertTriangle,
  Boxes,
  Building2,
  CheckCircle2,
  FileWarning,
  MessageCircle,
  PackageX,
  UserCheck,
  UserX,
} from 'lucide-react'
import { endpoints } from '../../shared/api/endpoints'
import { api } from '../../shared/api/apiClient'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { SectionHeader } from '../../shared/components/SectionHeader'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatNumber } from '../../shared/utils/format'
import type { DashboardGroupMetric, DashboardSummary, DashboardTechnicianWorkload } from './types'

interface StatCardDef {
  key: keyof DashboardSummary
  label: string
  to: string
  icon: typeof Activity
  tone?: 'default' | 'danger' | 'warning'
}

const statCards: StatCardDef[] = [
  { key: 'openWorkOrders', label: 'Open Work Orders', to: '/work-orders', icon: Activity },
  { key: 'overdueWorkOrders', label: 'Overdue Work Orders', to: '/work-orders?status=overdue', icon: AlertTriangle, tone: 'danger' },
  { key: 'unassignedWorkOrders', label: 'Unassigned Work Orders', to: '/work-orders?unassigned=true', icon: UserX, tone: 'warning' },
  { key: 'techniciansOnSite', label: 'Technicians On Site', to: '/team/technicians', icon: UserCheck },
  { key: 'assets', label: 'Active Assets', to: '/assets', icon: Boxes },
  { key: 'clients', label: 'Active Clients', to: '/clients', icon: Building2 },
  { key: 'materialsLowStock', label: 'Low Stock Materials', to: '/inventory/alerts', icon: PackageX, tone: 'warning' },
  { key: 'contractsExpiringIn30Days', label: 'Contracts Expiring (30d)', to: '/contracts', icon: FileWarning, tone: 'warning' },
]

const toneClasses: Record<NonNullable<StatCardDef['tone']>, string> = {
  default: 'text-slate-900',
  warning: 'text-amber-600',
  danger: 'text-rose-600',
}

export function DashboardPage() {
  const { branches } = useAuth()
  const [branchId, setBranchId] = useState('')

  const { data, isLoading, isError } = useQuery({
    queryKey: ['dashboard', 'summary', branchId],
    queryFn: async () => api.get<DashboardSummary>(endpoints.dashboard.summary, branchId ? { branchId } : undefined),
    staleTime: 30_000,
  })

  const groupColumns: DataTableColumn<DashboardGroupMetric & { id: string }>[] = [
    { key: 'groupName', header: 'Assignment Group', cell: (row) => row.groupName },
    { key: 'count', header: 'Open Work Orders', cell: (row) => formatNumber(row.count) },
  ]

  const workloadColumns: DataTableColumn<DashboardTechnicianWorkload & { id: string }>[] = [
    { key: 'technicianName', header: 'Technician', cell: (row) => row.technicianName },
    { key: 'activeWorkOrders', header: 'Active WOs', cell: (row) => formatNumber(row.activeWorkOrders) },
    { key: 'pendingResponses', header: 'Pending Responses', cell: (row) => formatNumber(row.pendingResponses) },
    { key: 'onSiteCount', header: 'On Site', cell: (row) => formatNumber(row.onSiteCount) },
  ]

  if (isLoading) {
    return <Spinner label="Loading dashboard" />
  }

  if (isError || !data) {
    return (
      <EmptyState
        title="Dashboard data unavailable"
        description="The dashboard summary could not be loaded. Try reloading the page."
        actionLabel="Reload"
        onAction={() => window.location.reload()}
      />
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        subtitle="Operational command centre for daily field service work."
        secondaryActions={
          branches.length > 1 ? (
            <select
              value={branchId}
              onChange={(event) => setBranchId(event.target.value)}
              className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-slate-900"
            >
              <option value="">All Branches</option>
              {branches.map((branch) => (
                <option key={branch.id} value={branch.id}>{branch.name}</option>
              ))}
            </select>
          ) : undefined
        }
      />

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {statCards.map((card) => {
          const Icon = card.icon
          const value = data[card.key]
          return (
            <Link
              key={card.key}
              to={card.to}
              className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md"
            >
              <div className="flex items-center justify-between">
                <div className="text-sm font-medium text-slate-600">{card.label}</div>
                <Icon className={`h-4 w-4 ${toneClasses[card.tone ?? 'default']}`} />
              </div>
              <div className={`mt-3 text-2xl font-semibold ${toneClasses[card.tone ?? 'default']}`}>{formatNumber(Number(value ?? 0))}</div>
            </Link>
          )
        })}
      </div>

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <SectionHeader title="Work Orders by Group" description="Open work order load per assignment group." />
          <DataTable
            columns={groupColumns}
            data={data.workOrdersByGroup.map((row) => ({ id: row.groupName, ...row }))}
            emptyTitle="No group assignments"
            emptyDescription="Work orders assigned to groups will appear here."
          />
        </section>

        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <SectionHeader title="Technician Utilisation" description="Active load and on-site status per technician." />
          <DataTable
            columns={workloadColumns}
            data={data.technicianWorkload.map((row) => ({ id: row.technicianName, ...row }))}
            emptyTitle="No technician activity"
            emptyDescription="Technician workload will appear here once work orders are assigned."
          />
        </section>
      </div>

      <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <SectionHeader title="Contract Health" description="Renewal risk and expiry pipeline across all active contracts." />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div className="rounded-xl border border-slate-200 px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring in 90 Days</div>
            <div className="mt-2 text-xl font-semibold text-slate-900">{formatNumber(data.contractsExpiringIn90Days)}</div>
          </div>
          <div className="rounded-xl border border-slate-200 px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring in 30 Days</div>
            <div className="mt-2 text-xl font-semibold text-amber-600">{formatNumber(data.contractsExpiringIn30Days)}</div>
          </div>
          <div className="rounded-xl border border-slate-200 px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Critical Renewal Risk</div>
            <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(data.criticalRenewalRiskContracts)}</div>
          </div>
          <div className="rounded-xl border border-slate-200 px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expired</div>
            <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(data.expiredContracts)}</div>
          </div>
        </div>
      </section>

      <section className="rounded-2xl border border-dashed border-slate-300 bg-white p-5">
        <div className="flex items-center gap-3">
          <MessageCircle className="h-5 w-5 text-slate-400" />
          <div>
            <div className="text-sm font-medium text-slate-700">WhatsApp Alerts</div>
            <p className="text-sm text-slate-500">Pending backend WhatsApp integration — this widget will populate once a WhatsApp controller is available.</p>
          </div>
        </div>
      </section>

      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap gap-4 text-sm text-slate-600">
          <span className="flex items-center gap-1.5"><CheckCircle2 className="h-4 w-4 text-emerald-600" />Closed: {formatNumber(data.closedWorkOrders)}</span>
          <span>Assigned to Group: {formatNumber(data.assignedToGroup)}</span>
          <span>Assigned to Technicians: {formatNumber(data.assignedToTechnicians)}</span>
          <span>Awaiting Acceptance: {formatNumber(data.awaitingAcceptance)}</span>
          <span>Renewal In Progress: {formatNumber(data.renewalInProgressContracts)}</span>
        </div>
      </div>
    </div>
  )
}
