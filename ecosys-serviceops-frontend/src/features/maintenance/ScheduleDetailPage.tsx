import { useMemo } from 'react'
import toast from 'react-hot-toast'
import { Link, useParams } from 'react-router-dom'
import { isPast } from 'date-fns'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { DetailLayout } from '../../shared/components/DetailLayout'
import { DataTable, type DataTableColumn } from '../../shared/components/DataTable'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { SummarySection } from '../../shared/pages/ModulePage'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { useWorkOrders } from '../work-orders/api'
import { useGenerateWorkOrderNow, usePmPlans } from './api'
import { ContractLapsePanel } from './components/ContractLapsePanel'
import { PM_CONTRACT_LAPSED } from './types'

type HistoryOutcome = 'Completed On Time' | 'Completed Late' | 'Missed' | 'In Progress'

export function ScheduleDetailPage() {
  const { id } = useParams<{ id: string }>()
  const canTrigger = usePermission('maintenance.trigger')
  const { data: plans, isLoading, isError } = usePmPlans()
  const { data: workOrders } = useWorkOrders()
  const generateNow = useGenerateWorkOrderNow(id ?? '')

  const plan = useMemo(() => (plans ?? []).find((item) => item.id === id), [plans, id])

  const history = useMemo(() => {
    return (workOrders ?? [])
      .filter((wo) => wo.preventiveMaintenancePlanId === id)
      .map((wo) => {
        let outcome: HistoryOutcome = 'In Progress'
        if (wo.status === 'Completed' && wo.completedAt) {
          outcome = wo.dueDate && new Date(wo.completedAt) > new Date(wo.dueDate) ? 'Completed Late' : 'Completed On Time'
        } else if (wo.dueDate && isPast(new Date(wo.dueDate)) && !['Completed', 'Closed'].includes(wo.status)) {
          outcome = 'Missed'
        }
        return { ...wo, outcome }
      })
  }, [workOrders, id])

  const triggerGenerate = async () => {
    try {
      const wo = await generateNow.mutateAsync({})
      toast.success(`Work order ${wo.workOrderNumber} generated`)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading schedule" />
  }

  if (isError || !plan) {
    return <EmptyState title="Schedule unavailable" description="This PM schedule could not be loaded." />
  }

  const header = (
    <div className="space-y-4">
      <PageHeader
        title={plan.assetName ?? 'PM Schedule'}
        subtitle={plan.clientName ?? undefined}
        breadcrumbs={[{ label: 'PM Schedules', to: '/maintenance/schedules' }, { label: plan.assetName ?? 'Schedule' }]}
        secondaryActions={<StatusBadge status={plan.status === PM_CONTRACT_LAPSED ? 'Contract Lapsed' : plan.status} />}
        primaryAction={
          // While on hold the panel below offers a chargeable visit instead.
          canTrigger && plan.status !== PM_CONTRACT_LAPSED ? (
            <button type="button" onClick={triggerGenerate} disabled={generateNow.isPending} className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
              {generateNow.isPending ? 'Generating…' : 'Generate Work Order Now'}
            </button>
          ) : undefined
        }
      />
      <ContractLapsePanel plan={plan} />
    </div>
  )

  const historyColumns: DataTableColumn<(typeof history)[number]>[] = [
    { key: 'workOrderNumber', header: 'WO No', cell: (row) => <Link className="font-mono text-slate-900 underline decoration-slate-300 underline-offset-4" to={`/work-orders/${row.id}`}>{row.workOrderNumber}</Link> },
    { key: 'dueDate', header: 'Due Date', cell: (row) => (row.dueDate ? formatDate(row.dueDate) : '—') },
    { key: 'completedAt', header: 'Completed', cell: (row) => (row.completedAt ? formatDate(row.completedAt) : '—') },
    { key: 'outcome', header: 'Outcome', cell: (row) => <StatusBadge status={row.outcome} /> },
  ]

  return (
    <DetailLayout
      header={header}
      tabs={[
        {
          id: 'overview',
          label: 'Overview',
          content: (
            <SummarySection title="Schedule Overview" description="Core PM plan fields.">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {[
                  ['Client', plan.clientName ?? '—'],
                  ['Asset', plan.assetName ?? '—'],
                  ['Branch', plan.branchName ?? '—'],
                  ['Frequency', plan.frequency],
                  ['Interval (months)', plan.serviceIntervalMonths != null ? String(plan.serviceIntervalMonths) : '—'],
                  ['Auto-Schedule', plan.autoSchedule ? 'Yes' : 'No'],
                  ['Last PM Date', plan.lastPmDate ? formatDate(plan.lastPmDate) : '—'],
                  ['Next PM Date', plan.nextPmDate ? formatDate(plan.nextPmDate) : '—'],
                  ['Created', formatDate(plan.createdAt)],
                ].map(([label, value]) => (
                  <div key={label} className="rounded-xl border border-slate-200 px-4 py-3">
                    <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
                    <div className="mt-2 text-sm text-slate-900">{value}</div>
                  </div>
                ))}
              </div>
            </SummarySection>
          ),
        },
        {
          id: 'trigger',
          label: 'Trigger Config',
          content: (
            <SummarySection title="Trigger Configuration" description="How and when this schedule generates work orders.">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div className="rounded-xl border border-slate-200 px-4 py-3">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Frequency</div>
                  <div className="mt-2 text-sm text-slate-900">{plan.frequency} — every {plan.serviceIntervalMonths ?? '—'} month(s)</div>
                </div>
                <div className="rounded-xl border border-slate-200 px-4 py-3">
                  <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Auto-Schedule</div>
                  <div className="mt-2 text-sm text-slate-900">{plan.autoSchedule ? 'Work orders auto-generate on schedule' : 'Manual trigger only'}</div>
                </div>
              </div>
            </SummarySection>
          ),
        },
        {
          id: 'assignment',
          label: 'Assignment Rule',
          content: <EmptyState title="No stored assignment rule" description="Technician/assignment-group assignment is not a field on the PM plan — each generated work order is assigned individually via the normal Work Order Assignment flow." />,
        },
        {
          id: 'wo-settings',
          label: 'WO Settings',
          content: (
            <SummarySection title="Work Order Settings" description="Checklist template applied to generated work orders.">
              <div className="rounded-xl border border-slate-200 px-4 py-3">
                <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Checklist Template</div>
                <div className="mt-2 text-sm text-slate-900">{plan.pmTemplateName ?? 'No template attached'}</div>
              </div>
            </SummarySection>
          ),
        },
        {
          id: 'history',
          label: 'History',
          content: (
            <DataTable columns={historyColumns} data={history} emptyTitle="No generated work orders" emptyDescription="Work orders generated from this schedule will appear here." />
          ),
        },
      ]}
    />
  )
}
