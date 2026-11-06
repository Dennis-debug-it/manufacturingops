import { Wallet } from 'lucide-react'
import { useAnyPermission } from '../../../shared/hooks/usePermission'
import { useJobCosts } from './api'

const num = (value: number | null | undefined, digits = 0) => (value == null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: digits }))

/** On a job: what it has cost so far, and the production its downtime lost. */
export function JobCostsCard({ jobId }: { jobId: string }) {
  const canSee = useAnyPermission('reports.view', 'work-orders.assign')
  const costs = useJobCosts(jobId)
  const c = costs.data
  if (!canSee || !c || (c.total === 0 && c.downtimeHours === 0)) return null
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="mb-2 flex items-center gap-1.5 text-sm font-semibold text-slate-800"><Wallet className="h-4 w-4 text-slate-500" />Cost · {num(c.total)}</h2>
      <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
        <dt className="text-slate-500">Labour</dt><dd className="text-right tabular-nums">{num(c.labour)}</dd>
        <dt className="text-slate-500">Parts</dt><dd className="text-right tabular-nums">{num(c.parts)}</dd>
        <dt className="text-slate-500">Contractor</dt><dd className="text-right tabular-nums">{num(c.contractor)}</dd>
        {c.downtimeHours > 0 ? <><dt className="text-slate-500">Downtime</dt><dd className="text-right tabular-nums">{num(c.downtimeHours, 1)} h · {num(c.productionLost)} {c.productionUnit}{c.downtimeValue != null ? ` (${num(c.downtimeValue)})` : ''}</dd></> : null}
      </dl>
      {c.costCentre ? <p className="mt-2 text-xs text-slate-500">Charged to {c.costCentre}</p> : null}
    </section>
  )
}
