import { useState } from 'react'
import toast from 'react-hot-toast'
import { Link } from 'react-router-dom'
import { SummarySection } from '../../../shared/pages/ModulePage'
import { formatDate } from '../../../shared/utils/dates'
import { useSequences, useStartSequence, useStopSequence } from '../api'
import type { SalesOpportunityDetail } from '../types'

/** The deal's follow-up sequence: what is running and what comes next, or a picker to start one. */
export function DealSequenceCard({ opportunity, canManage }: { opportunity: SalesOpportunityDetail; canManage: boolean }) {
  const { data: sequences } = useSequences()
  const start = useStartSequence(opportunity.id)
  const stop = useStopSequence(opportunity.id)
  const [picked, setPicked] = useState('')
  const current = opportunity.sequence
  const running = current?.status === 'Active'
  const closed = opportunity.stage === 'Won' || opportunity.stage === 'Lost'
  const available = (sequences ?? []).filter((x) => x.isActive)
  if (!current && (closed || available.length === 0)) return null

  return (
    <SummarySection title="Follow-up sequence" description="Automatic tasks and emails until the client answers.">
      {current ? (
        <div className="text-sm text-slate-700">
          <div className="font-medium text-slate-900">{current.name}</div>
          {running ? (
            <div>
              Step {current.nextStep + 1} of {current.stepCount}
              {current.nextDueAt ? ` due ${formatDate(current.nextDueAt)}` : ''}
            </div>
          ) : (
            <div className="text-slate-500">
              {current.status === 'Completed' ? 'Finished' : 'Stopped'}
              {current.finishedAt ? ` ${formatDate(current.finishedAt)}` : ''}
              {current.stoppedReason ? `: ${current.stoppedReason}` : ''}
            </div>
          )}
        </div>
      ) : null}
      {canManage && running ? (
        <button
          type="button"
          disabled={stop.isPending}
          onClick={() => stop.mutateAsync().then(() => toast.success('Sequence stopped')).catch(() => undefined)}
          className="mt-2 inline-flex h-9 items-center rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50"
        >
          Stop sequence
        </button>
      ) : null}
      {canManage && !running && !closed && available.length > 0 ? (
        <div className="mt-2 flex gap-2">
          <select aria-label="Sequence" value={picked} onChange={(event) => setPicked(event.target.value)} className="h-10 min-w-0 flex-1 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]">
            <option value="">Choose a sequence</option>
            {available.map((x) => <option key={x.id} value={x.id}>{x.name} ({x.steps.length} steps)</option>)}
          </select>
          <button
            type="button"
            disabled={!picked || start.isPending}
            onClick={() => start.mutateAsync(picked).then(() => { setPicked(''); toast.success('Sequence started') }).catch(() => undefined)}
            className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white disabled:opacity-60"
          >
            Start
          </button>
        </div>
      ) : null}
      {canManage && available.length === 0 && !running ? (
        <p className="mt-2 text-xs text-slate-500">Set up sequences in <Link to="/settings/sales" className="underline">Sales settings</Link>.</p>
      ) : null}
    </SummarySection>
  )
}
