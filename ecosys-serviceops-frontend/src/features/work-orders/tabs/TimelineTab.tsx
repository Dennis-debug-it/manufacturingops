import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Send } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { useAddWorkOrderComment, useWorkOrderEvents } from '../api'

export function TimelineTab({ workOrderId }: { workOrderId: string }) {
  const { data, isLoading, isError } = useWorkOrderEvents(workOrderId)
  const canComment = usePermission('work-orders.timeline.comment')
  const addComment = useAddWorkOrderComment(workOrderId)
  const [message, setMessage] = useState('')

  const events = useMemo(
    () => [...(data ?? [])].sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime()),
    [data],
  )

  const submit = async () => {
    if (!message.trim()) return
    try {
      await addComment.mutateAsync(message.trim())
      toast.success('Update added')
      setMessage('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading timeline" />
  }

  if (isError) {
    return <EmptyState title="Timeline unavailable" description="Events could not be loaded for this work order." />
  }

  return (
    <div className="space-y-6">
      {canComment ? (
        <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
          <label htmlFor="timeline-update" className="mb-2 block text-sm font-medium text-slate-700">Add Update</label>
          <div className="flex flex-col gap-3 sm:flex-row">
            <textarea
              id="timeline-update"
              value={message}
              onChange={(event) => setMessage(event.target.value)}
              rows={2}
              placeholder="Post a progress update or note…"
              className="h-auto flex-1 rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-slate-900"
            />
            <button
              type="button"
              onClick={submit}
              disabled={addComment.isPending || !message.trim()}
              className="inline-flex h-11 shrink-0 items-center justify-center gap-2 self-start rounded-lg bg-slate-900 px-4 text-sm font-medium text-white transition hover:bg-slate-800 disabled:cursor-not-allowed disabled:opacity-60"
            >
              <Send className="h-4 w-4" />
              Post
            </button>
          </div>
        </div>
      ) : null}

      {events.length === 0 ? (
        <EmptyState title="No timeline events yet" description="Status changes and updates for this work order will appear here." />
      ) : (
        <ol className="space-y-3">
          {events.map((event) => {
            const isEscalation = event.eventType === 'Escalation'
            return (
              <li
                key={event.id}
                className={`rounded-2xl border p-4 shadow-sm ${isEscalation ? 'border-amber-200 bg-amber-50' : 'border-slate-200 bg-white'}`}
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className={`flex items-center gap-1.5 text-sm font-medium ${isEscalation ? 'text-amber-800' : 'text-slate-900'}`}>
                    {isEscalation ? <AlertTriangle className="h-3.5 w-3.5" /> : null}
                    {isEscalation ? 'Escalation' : event.eventType}
                  </span>
                  <span className="text-xs text-slate-500">{formatDateTime(event.occurredAt)}</span>
                </div>
                {event.message ? <p className="mt-2 text-sm leading-6 text-slate-700">{event.message}</p> : null}
                {event.oldStatus || event.newStatus ? (
                  <p className="mt-2 text-xs text-slate-500">{event.oldStatus ?? '—'} → {event.newStatus ?? '—'}</p>
                ) : null}
                <p className="mt-2 text-xs text-slate-500">{event.actorUserName ?? 'System'}</p>
              </li>
            )
          })}
        </ol>
      )}
    </div>
  )
}
