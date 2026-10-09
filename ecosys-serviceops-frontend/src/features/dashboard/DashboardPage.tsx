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
  LineChart,
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
import { Surface } from '../../shared/components/Surface'
import { Crystal } from '../../shared/components/Crystal'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { useAuth } from '../../shared/hooks/useAuth'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatNumber } from '../../shared/utils/format'
import { formatRelative } from '../../shared/utils/dates'
import { useWhatsAppConversations } from '../whatsapp/api'
import { WHATSAPP_ENABLED } from '../../shared/config/features'
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
  // The executive view reads the reports and revenue APIs; offer it only to people they serve.
  const canViewReports = usePermission('reports.view')
  const canViewFinance = usePermission('finance.view')
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
    <div className="space-y-4">
      <PageHeader
        title="Dashboard"
        subtitle="Operational command centre for daily field service work."
        secondaryActions={
          <div className="flex flex-wrap items-center gap-2">
            {branches.length > 1 ? (
              <select
                value={branchId}
                onChange={(event) => setBranchId(event.target.value)}
                className="h-11 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
              >
                <option value="">All Branches</option>
                {branches.map((branch) => (
                  <option key={branch.id} value={branch.id}>{branch.name}</option>
                ))}
              </select>
            ) : null}
            {/* The only entry point to the executive view — its route had no inbound link at all. */}
            {canViewReports && canViewFinance ? (
              <Link
                to="/dashboard/executive"
                className="inline-flex h-11 items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
              >
                <LineChart className="h-4 w-4" />
                Executive View
              </Link>
            ) : null}
          </div>
        }
      />

      <div className="relative grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div aria-hidden className="pointer-events-none absolute -inset-x-4 -inset-y-6 -z-10 overflow-hidden">
          <Crystal variant="dashboard" className="absolute inset-0 opacity-70" />
        </div>
        {statCards.map((card) => {
          const Icon = card.icon
          const value = data[card.key]
          return (
            <Link key={card.key} to={card.to} className="block transition hover:-translate-y-0.5">
              <Surface variant="glass" padding="sm" crystal="kpi">
                <div className="flex items-center justify-between">
                  <div className="text-sm font-medium text-slate-600">{card.label}</div>
                  <Icon className={`h-4 w-4 ${toneClasses[card.tone ?? 'default']}`} />
                </div>
                <div className={`mt-3 text-2xl font-semibold ${toneClasses[card.tone ?? 'default']}`}>{formatNumber(Number(value ?? 0))}</div>
              </Surface>
            </Link>
          )
        })}
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
        <Surface as="section" variant="glass" padding="lg" className="space-y-4">
          <SectionHeader title="Work Orders by Group" description="Open work order load per assignment group." />
          <DataTable
            columns={groupColumns}
            data={data.workOrdersByGroup.map((row) => ({ id: row.groupName, ...row }))}
            emptyTitle="No group assignments"
            emptyDescription="Work orders assigned to groups will appear here."
          />
        </Surface>

        <Surface as="section" variant="glass" padding="lg" className="space-y-4">
          <SectionHeader title="Technician Utilisation" description="Active load and on-site status per technician." />
          <DataTable
            columns={workloadColumns}
            data={data.technicianWorkload.map((row) => ({ id: row.technicianName, ...row }))}
            emptyTitle="No technician activity"
            emptyDescription="Technician workload will appear here once work orders are assigned."
          />
        </Surface>
      </div>

      <Surface as="section" variant="glass" padding="lg" className="space-y-4">
        <SectionHeader title="Contract Health" description="Renewal risk and expiry pipeline across all active contracts." />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
          <div className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring in 90 Days</div>
            <div className="mt-2 text-xl font-semibold text-slate-900">{formatNumber(data.contractsExpiringIn90Days)}</div>
          </div>
          <div className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expiring in 30 Days</div>
            <div className="mt-2 text-xl font-semibold text-amber-600">{formatNumber(data.contractsExpiringIn30Days)}</div>
          </div>
          <div className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Critical Renewal Risk</div>
            <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(data.criticalRenewalRiskContracts)}</div>
          </div>
          <div className="rounded-xl border border-[var(--app-border)] px-4 py-3">
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Expired</div>
            <div className="mt-2 text-xl font-semibold text-rose-600">{formatNumber(data.expiredContracts)}</div>
          </div>
        </div>
      </Surface>

      {WHATSAPP_ENABLED ? <WhatsAppAlertsWidget /> : null}

      <Surface variant="solid" padding="sm">
        <div className="flex flex-wrap gap-4 text-sm text-slate-600">
          <span className="flex items-center gap-1.5"><CheckCircle2 className="h-4 w-4 text-emerald-600" />Closed: {formatNumber(data.closedWorkOrders)}</span>
          <span>Assigned to Group: {formatNumber(data.assignedToGroup)}</span>
          <span>Assigned to Technicians: {formatNumber(data.assignedToTechnicians)}</span>
          <span>Awaiting Acceptance: {formatNumber(data.awaitingAcceptance)}</span>
          <span>Renewal In Progress: {formatNumber(data.renewalInProgressContracts)}</span>
        </div>
      </Surface>
    </div>
  )
}

function WhatsAppAlertsWidget() {
  const { data: conversations, isLoading } = useWhatsAppConversations()
  const recent = (conversations ?? []).slice(0, 4)
  const totalUnread = (conversations ?? []).reduce((sum, c) => sum + (c.unreadCount ?? 0), 0)

  return (
    <Surface as="section" variant="glass" padding="lg">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <MessageCircle className="h-5 w-5 text-emerald-600" />
          <div className="text-sm font-medium text-slate-700">WhatsApp Alerts</div>
          {totalUnread > 0 ? <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700">{totalUnread} unread</span> : null}
        </div>
        <Link to="/whatsapp" className="text-xs font-medium text-slate-500 underline underline-offset-2 hover:text-slate-900">Open inbox</Link>
      </div>
      {isLoading ? (
        <p className="mt-3 text-sm text-slate-400">Loading conversations…</p>
      ) : recent.length === 0 ? (
        <p className="mt-3 text-sm text-slate-500">No WhatsApp conversations yet. Start one from the inbox.</p>
      ) : (
        <ul className="mt-3 divide-y divide-slate-100">
          {recent.map((c) => (
            <li key={c.id} className="flex items-center justify-between gap-3 py-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-sm font-medium text-slate-800">
                  <span className="truncate">{c.contactName}</span>
                  {c.unreadCount > 0 ? <span className="rounded-full bg-emerald-500 px-1.5 text-[10px] font-semibold text-white">{c.unreadCount}</span> : null}
                </div>
                <div className="truncate text-xs text-slate-500">{c.lastMessagePreview}</div>
              </div>
              <span className="shrink-0 text-xs text-slate-400">{formatRelative(c.lastMessageAt)}</span>
            </li>
          ))}
        </ul>
      )}
    </Surface>
  )
}
