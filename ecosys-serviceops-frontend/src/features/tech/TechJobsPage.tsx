import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { ChevronRight, MapPin, Star } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { useAuth } from '../../shared/hooks/useAuth'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { useMyTechnician, useTechJobs } from './api'
import { jobPhase, myAssignmentOf, PHASE_LABEL, PHASE_TONE } from './jobState'

const PRIORITY_RANK: Record<string, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 }

export function TechJobsPage() {
  const { user } = useAuth()
  const { data: jobs, isLoading, isError } = useTechJobs()
  const { data: me } = useMyTechnician()
  const [view, setView] = useState<'active' | 'done'>('active')

  const rows = useMemo(() => {
    const withPhase = (jobs ?? []).map((job) => {
      const mine = myAssignmentOf(job, me?.technicianId)
      return { job, mine, phase: jobPhase(job, mine) }
    })
    const active = withPhase
      .filter((row) => row.phase !== 'done' && row.phase !== 'declined')
      // Jobs waiting on me first, then by priority, then by due date.
      .sort((a, b) =>
        Number(b.phase === 'accept') - Number(a.phase === 'accept')
        || (PRIORITY_RANK[a.job.priority] ?? 9) - (PRIORITY_RANK[b.job.priority] ?? 9)
        || (a.job.dueDate ?? '9').localeCompare(b.job.dueDate ?? '9'))
    const done = withPhase.filter((row) => row.phase === 'done' || row.phase === 'declined')
    return { active, done }
  }, [jobs, me])

  const visible = view === 'active' ? rows.active : rows.done

  return (
    <div className="space-y-4" data-testid="tech-jobs-page">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">My Jobs</h1>
        <p className="text-sm text-slate-500">Hi {user?.fullName?.split(' ')[0] ?? 'there'} — {rows.active.length} active job{rows.active.length === 1 ? '' : 's'}.</p>
      </div>

      <div className="grid grid-cols-2 gap-1 rounded-lg border border-slate-200 bg-white p-1" role="tablist">
        {(['active', 'done'] as const).map((id) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={view === id}
            onClick={() => setView(id)}
            className={cn('h-9 rounded-md text-sm font-medium', view === id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}
          >
            {id === 'active' ? `Active (${rows.active.length})` : `Done (${rows.done.length})`}
          </button>
        ))}
      </div>

      {isLoading ? (
        <Spinner label="Loading your jobs" />
      ) : isError ? (
        <p className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">Your jobs could not be loaded. Check your connection and pull to refresh.</p>
      ) : visible.length === 0 ? (
        <p className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-center text-sm text-slate-500">
          {view === 'active' ? 'No active jobs. New assignments will appear here.' : 'No finished jobs yet.'}
        </p>
      ) : (
        <ul className="space-y-2">
          {visible.map(({ job, mine, phase }) => (
            <li key={job.id}>
              <Link
                to={`/tech/jobs/${job.id}`}
                data-testid={`tech-job-card-${job.id}`}
                className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm transition active:bg-slate-50"
              >
                <div className="min-w-0 flex-1 space-y-1">
                  <div className="flex items-center gap-2 text-xs text-slate-500">
                    <span className="font-mono">{job.workOrderNumber}</span>
                    <span className={cn('rounded-full px-1.5 py-0.5 font-medium', job.priority === 'Critical' || job.priority === 'High' ? 'bg-rose-50 text-rose-700' : 'bg-slate-100 text-slate-600')}>{job.priority}</span>
                    {mine?.isLead ? <span className="inline-flex items-center gap-0.5 text-amber-700"><Star className="h-3 w-3" />Lead</span> : null}
                  </div>
                  <p className="truncate text-sm font-semibold text-slate-900">{job.title}</p>
                  <p className="flex items-center gap-1 truncate text-xs text-slate-500">
                    <MapPin className="h-3 w-3 shrink-0" />{[job.clientName, job.siteName].filter(Boolean).join(' · ') || 'No site'}
                  </p>
                  <div className="flex flex-wrap items-center gap-2 pt-0.5">
                    <span className={cn('rounded-full px-2 py-0.5 text-[11px] font-medium ring-1 ring-inset', PHASE_TONE[phase])}>{PHASE_LABEL[phase]}</span>
                    {job.dueDate ? <span className="text-[11px] text-slate-500">Due {formatDate(job.dueDate)}</span> : null}
                  </div>
                </div>
                <ChevronRight className="h-4 w-4 shrink-0 text-slate-400" />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
