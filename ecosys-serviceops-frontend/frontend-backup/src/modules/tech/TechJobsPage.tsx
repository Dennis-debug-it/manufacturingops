import { ArrowRight } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { useAsyncData } from '../../hooks/useAsyncData'
import { technicianJobsService } from '../../services/technicianJobsService'
import type { WorkOrder } from '../../types/api'
import { formatDateOnly } from '../../utils/date'
import { priorityTone, statusTone } from '../../utils/format'

export function TechJobsPage() {
  const navigate = useNavigate()
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

      <DataTable
        rows={data}
        rowKey={(row) => row.id}
        pageSize={10}
        variant="open"
        minTableWidth="min-w-[980px] w-full"
        onRowClick={(job) => navigate(`/tech/jobs/${job.id}`)}
        emptyTitle="No assigned jobs"
        emptyDescription="Your assigned work orders will appear here as soon as dispatch sends them to you."
        columns={[
          {
            key: 'job',
            header: 'Job',
            cell: (job) => (
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{job.workOrderNumber}</p>
                <p className="mt-1 font-semibold text-app">{job.title}</p>
              </div>
            ),
          },
          {
            key: 'location',
            header: 'Location',
            cell: (job) => job.siteName || job.assetName || job.clientName || 'Field visit',
          },
          {
            key: 'priority',
            header: 'Priority',
            cell: (job) => <Badge tone={priorityTone(job.priority as never)}>{job.priority}</Badge>,
          },
          {
            key: 'status',
            header: 'Status',
            cell: (job) => <Badge tone={statusTone(job.status as never)}>{job.status}</Badge>,
          },
          {
            key: 'due',
            header: 'Due',
            cell: (job) => (job.dueDate ? formatDateOnly(job.dueDate) : 'No due date'),
          },
          {
            key: 'open',
            header: 'Open',
            className: 'min-w-[120px]',
            cell: (job) => (
              <Link
                to={`/tech/jobs/${job.id}`}
                className="inline-flex items-center gap-2 rounded-full border border-[var(--app-border)] px-3 py-1.5 text-xs font-medium text-app transition hover:bg-[var(--app-subtle)]"
              >
                Open job
                <ArrowRight className="size-3.5" />
              </Link>
            ),
          },
        ]}
      />
    </div>
  )
}
