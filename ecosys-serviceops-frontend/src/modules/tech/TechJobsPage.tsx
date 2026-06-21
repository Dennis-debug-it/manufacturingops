import { ArrowRight, Clock3, MapPin, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useAsyncData } from '../../hooks/useAsyncData'
import { technicianJobsService } from '../../services/technicianJobsService'
import type { WorkOrder } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { priorityTone, statusTone } from '../../utils/format'

export function TechJobsPage() {
  const { data, loading, error } = useAsyncData<WorkOrder[]>(
    async (signal) => technicianJobsService.list(signal),
    [],
    [],
  )

  if (loading) {
    return <LoadingState label="Loading assigned jobs" />
  }

  if (error) {
    return <ErrorState title="Unable to load technician jobs" description={error} />
  }

  if (data.length === 0) {
    return (
      <EmptyState
        title="No assigned jobs"
        description="Your assigned work orders will appear here as soon as dispatch sends them to you."
      />
    )
  }

  return (
    <div className="space-y-5" data-testid="tech-jobs-page">
      <section className="surface-card">
        <div className="space-y-2">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">Field queue</p>
          <h2 className="text-2xl font-semibold text-app">Your mobile workboard</h2>
          <p className="text-sm text-muted">Open a job, capture evidence, and complete the visit without returning to the full desktop shell.</p>
        </div>
      </section>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {data.map((job) => (
          <TechJobCard key={job.id} job={job} />
        ))}
      </div>
    </div>
  )
}

function TechJobCard({ job }: { job: WorkOrder }) {
  return (
    <Link
      to={`/tech/jobs/${job.id}`}
      className="surface-card block transition hover:-translate-y-0.5 hover:shadow-lg"
      data-testid={`tech-job-card-${job.id}`}
    >
      <div className="space-y-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{job.workOrderNumber}</p>
            <h3 className="mt-2 text-lg font-semibold text-app">{job.title}</h3>
          </div>
          <Badge tone={statusTone(job.status as never)}>{job.status}</Badge>
        </div>

        <div className="grid gap-3">
          <MetaRow icon={<ShieldCheck className="size-4" />} label={job.priority}>
            <Badge tone={priorityTone(job.priority as never)}>{job.priority}</Badge>
          </MetaRow>
          <MetaRow icon={<MapPin className="size-4" />} label="Location">
            <span>{job.siteName || job.assetName || job.clientName || 'Field visit'}</span>
          </MetaRow>
          <MetaRow icon={<Clock3 className="size-4" />} label="Due">
            <span>{job.dueDate ? formatDateOnly(job.dueDate) : 'No due date'}</span>
          </MetaRow>
        </div>

        <div className="flex items-center justify-between rounded-2xl border border-app px-4 py-3 text-sm font-medium text-app">
          <span>{job.clientName || 'Client'}</span>
          <span className="inline-flex items-center gap-2 text-brand-primary">
            Open job
            <ArrowRight className="size-4" />
          </span>
        </div>
      </div>
    </Link>
  )
}

function MetaRow({
  icon,
  label,
  children,
}: {
  icon: ReactNode
  label: string
  children: ReactNode
}) {
  return (
    <div className="panel-subtle flex items-center justify-between gap-3 rounded-2xl px-4 py-3 text-sm text-app">
      <div className="inline-flex items-center gap-2 text-muted">
        {icon}
        <span>{label}</span>
      </div>
      {children}
    </div>
  )
}
