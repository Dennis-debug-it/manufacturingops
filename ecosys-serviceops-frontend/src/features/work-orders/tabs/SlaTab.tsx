import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { SlaBadge } from '../../../shared/components/SlaBadge'
import { formatDateTime, formatDuration } from '../../../shared/utils/dates'
import { differenceInMinutes } from '../../../shared/utils/dates'
import { useWorkOrderSla } from '../api'

function ClockCard({
  label,
  deadline,
  breached,
  breachedAt,
  minutesRemaining,
  paused,
}: {
  label: string
  deadline: string | null
  breached: boolean
  breachedAt: string | null
  minutesRemaining: number | null
  paused: boolean
}) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between gap-2">
        <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
        <SlaBadge deadline={deadline} breached={breached} paused={paused} />
      </div>
      {deadline ? (
        <>
          <div className="mt-3 text-sm text-slate-900">Deadline: {formatDateTime(deadline)}</div>
          {breached && breachedAt ? (
            <div className="mt-1 text-sm text-rose-600">Breached at {formatDateTime(breachedAt)}</div>
          ) : !breached && minutesRemaining != null ? (
            <div className="mt-1 text-sm text-slate-600">
              {minutesRemaining > 0 ? `${formatDuration(minutesRemaining)} remaining` : `Overdue by ${formatDuration(Math.abs(minutesRemaining))}`}
            </div>
          ) : null}
        </>
      ) : (
        <div className="mt-3 text-sm text-slate-500">No target configured for this priority.</div>
      )}
    </div>
  )
}

export function SlaTab({ workOrderId }: { workOrderId: string }) {
  const { data, isLoading, isError } = useWorkOrderSla(workOrderId)

  if (isLoading) {
    return <Spinner label="Loading SLA status" />
  }

  if (isError || !data) {
    return <EmptyState title="SLA status unavailable" description="SLA status could not be loaded for this work order." />
  }

  const responseMinutesRemaining = data.responseDeadline
    ? data.responseMinutesRemaining ?? differenceInMinutes(data.responseDeadline, new Date())
    : null
  const resolutionMinutesRemaining = data.resolutionDeadline
    ? data.resolutionMinutesRemaining ?? differenceInMinutes(data.resolutionDeadline, new Date())
    : null

  return (
    <div className="space-y-4">
      <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Applied Policy</div>
            <div className="mt-1 text-sm text-slate-900">{data.policyName ?? 'No SLA policy applied'} · {data.priority} priority</div>
          </div>
          <SlaBadge
            deadline={data.resolutionDeadline ?? data.responseDeadline}
            breached={data.responseBreached || data.resolutionBreached}
            paused={data.isPaused}
          />
        </div>
        {data.isPaused ? (
          <div className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-700">
            SLA clock paused{data.pauseReason ? `: ${data.pauseReason}` : '.'}
          </div>
        ) : null}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <ClockCard
          label="Response Clock"
          deadline={data.responseDeadline}
          breached={data.responseBreached}
          breachedAt={data.responseBreachedAt}
          minutesRemaining={responseMinutesRemaining}
          paused={data.isPaused}
        />
        <ClockCard
          label="Resolution Clock"
          deadline={data.resolutionDeadline}
          breached={data.resolutionBreached}
          breachedAt={data.resolutionBreachedAt}
          minutesRemaining={resolutionMinutesRemaining}
          paused={data.isPaused}
        />
      </div>

      {(data.responseBreachedAt || data.resolutionBreachedAt) ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">Breach History</div>
          <ul className="mt-3 space-y-2 text-sm text-slate-700">
            {data.responseBreachedAt ? <li>Response SLA breached at {formatDateTime(data.responseBreachedAt)}</li> : null}
            {data.resolutionBreachedAt ? <li>Resolution SLA breached at {formatDateTime(data.resolutionBreachedAt)}</li> : null}
          </ul>
        </div>
      ) : null}
    </div>
  )
}
