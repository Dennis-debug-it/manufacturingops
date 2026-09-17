import { useMemo } from 'react'
import { AlertTriangle, CheckCircle2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { ErrorState } from '../../shared/components/ErrorState'
import { SectionHeader } from '../../shared/components/SectionHeader'
import { StatCard } from '../../shared/components/StatCard'
import { cn } from '../../shared/utils/cn'
import { usePmPlans, usePmPlansDue } from './api'
import { MaintenanceTabNav } from './components/MaintenanceTabNav'

export function EnginePage() {
  const plansQuery = usePmPlans()
  const dueQuery = usePmPlansDue()
  const { data: plans, isLoading: plansLoading } = plansQuery
  const { data: due, isLoading: dueLoading } = dueQuery

  const activePlans = useMemo(() => (plans ?? []).filter((plan) => plan.status === 'Active'), [plans])
  const overdueCount = due?.length ?? 0
  const healthy = activePlans.length === 0 || overdueCount / activePlans.length < 0.15

  if (plansLoading || dueLoading) {
    return <Spinner label="Loading engine status" />
  }

  // Every number and the health banner below are ratios of these two lists. Falling back to empty
  // arrays would render a confident "PM schedules are on track" out of a failed request.
  if (plansQuery.isError || dueQuery.isError) {
    return (
      <ErrorState
        title="Engine status unavailable"
        description="We couldn’t load PM plans or the due list, so no health signal is shown — an all-clear here would be indistinguishable from a failed request."
        onRetry={() => {
          void plansQuery.refetch()
          void dueQuery.refetch()
        }}
      />
    )
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Engine Status" subtitle="Maintenance generation engine health and rules." />
      <MaintenanceTabNav />

      <div className={cn('flex items-center gap-4 rounded-2xl border p-5 shadow-sm', healthy ? 'border-emerald-200 bg-emerald-50' : 'border-amber-200 bg-amber-50')}>
        {healthy ? <CheckCircle2 className="h-8 w-8 shrink-0 text-emerald-600" /> : <AlertTriangle className="h-8 w-8 shrink-0 text-amber-600" />}
        <div>
          <div className={cn('text-sm font-semibold', healthy ? 'text-emerald-900' : 'text-amber-900')}>
            {healthy ? 'PM schedules are on track' : 'A significant share of PM plans are overdue'}
          </div>
          <p className={cn('mt-1 text-sm', healthy ? 'text-emerald-700' : 'text-amber-700')}>
            {overdueCount} of {activePlans.length} active plan(s) are due or overdue right now. This is a derived signal from{' '}
            <code className="rounded bg-white/60 px-1 py-0.5 text-xs">GET /api/preventive-maintenance/due</code> vs. total active
            plans — the backend has no dedicated engine-health or activity-log endpoint, so this banner is an approximation, not
            an authoritative system status.
          </p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <StatCard label="Active Plans" value={activePlans.length} />
        <StatCard label="Due / Overdue Now" value={overdueCount} tone={overdueCount > 0 ? 'warning' : 'success'} />
        <StatCard label="Auto-Schedule Enabled" value={activePlans.filter((plan) => plan.autoSchedule).length} />
        <StatCard label="Manual Trigger Only" value={activePlans.filter((plan) => !plan.autoSchedule).length} />
      </div>

      <section className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
        <SectionHeader title="Generation Rules" description="How the current PM plans are configured to fire." />
        <p className="text-sm leading-6 text-slate-600">
          Each PM plan generates a work order via <code className="rounded bg-slate-100 px-1 py-0.5 text-xs">POST /api/preventive-maintenance/{'{id}'}/generate-workorder</code>,
          which updates the plan's Last PM Date and computes the next due date. There is no scheduled background job endpoint
          exposed to the frontend — triggering happens manually from the Calendar or Schedule Detail pages until a backend job
          confirms otherwise.
        </p>
      </section>

      <section className="rounded-2xl border border-dashed border-slate-300 bg-white p-5">
        <SectionHeader title="Activity Log" description="Recent engine-triggered work order generation events." />
        <EmptyState title="No activity log endpoint" description="The backend does not expose a maintenance-engine activity log. Once available, generation events will be listed here." />
      </section>
    </div>
  )
}
