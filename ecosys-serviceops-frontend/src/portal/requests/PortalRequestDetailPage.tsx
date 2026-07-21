import { useParams } from 'react-router-dom'
import { PageHeader } from '../../shared/components/PageHeader'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { formatDateTime } from '../../shared/utils/dates'
import { usePortalWorkOrder, usePortalWorkOrderNotes } from '../api'

export function PortalRequestDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, isError } = usePortalWorkOrder(id)
  const { data: notes } = usePortalWorkOrderNotes(id)

  if (isLoading) {
    return <Spinner label="Loading request" />
  }

  if (isError || !data) {
    return <EmptyState title="Request unavailable" description="This request could not be loaded." />
  }

  const rows: Array<[string, string]> = [
    ['Site', data.siteName ?? '—'],
    ['Asset', data.assetName ?? '—'],
    ['Work Type', data.workType],
    ['Due', data.dueDate ? formatDateTime(data.dueDate) : '—'],
    ['Scheduled Start', data.scheduledStartAt ? formatDateTime(data.scheduledStartAt) : '—'],
    ['Completed', data.completedAt ? formatDateTime(data.completedAt) : '—'],
    ['Submitted', formatDateTime(data.createdAt)],
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title={data.workOrderNumber}
        subtitle={data.title}
        breadcrumbs={[{ label: 'Requests', to: '/portal/requests' }, { label: data.workOrderNumber }]}
        secondaryActions={<div className="flex gap-2"><StatusBadge status={data.priority} /><StatusBadge status={data.status} /></div>}
      />

      {data.description ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Description</h2>
          <p className="mt-2 whitespace-pre-line text-sm text-slate-700">{data.description}</p>
        </div>
      ) : null}

      <div className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:grid-cols-2">
        {rows.map(([label, value]) => (
          <div key={label}>
            <div className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</div>
            <div className="mt-1 text-sm text-slate-900">{value}</div>
          </div>
        ))}
      </div>

      {data.workDoneNotes ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Technician Notes</h2>
          <p className="mt-2 whitespace-pre-line text-sm text-slate-700">{data.workDoneNotes}</p>
        </div>
      ) : null}

      {notes && notes.length > 0 ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
          <h2 className="text-sm font-semibold text-slate-900">Updates</h2>
          <ul className="mt-3 space-y-3">
            {notes.map((note) => (
              <li key={note.id} className="rounded-xl border border-slate-200 p-3">
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-500">
                  <span>{note.createdByUserName ?? 'Service team'}</span>
                  <span>{formatDateTime(note.createdAt)}</span>
                </div>
                <p className="mt-2 whitespace-pre-line text-sm text-slate-700">{note.content}</p>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  )
}
