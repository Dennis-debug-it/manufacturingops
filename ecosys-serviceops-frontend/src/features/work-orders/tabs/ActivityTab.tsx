import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, ArrowRightLeft, Eye, Lock, MessageSquare, Send, UserCog } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { cn } from '../../../shared/utils/cn'
import { formatDateTime, formatRelative } from '../../../shared/utils/dates'
import { useAddWorkOrderNote, useWorkOrderEvents, useWorkOrderNotes } from '../api'
import type { WorkOrderEvent, WorkOrderNote } from '../types'

type Filter = 'all' | 'notes' | 'status' | 'assignment'
type Kind = Exclude<Filter, 'all'> | 'other'

interface FeedItem {
  id: string
  kind: Kind
  at: string
  actor: string
  title: string
  body: string | null
  /** Notes only: who can see it. */
  visibility?: 'internal' | 'client'
  escalation?: boolean
}

const FILTERS: { id: Filter; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'notes', label: 'Notes' },
  { id: 'status', label: 'Status' },
  { id: 'assignment', label: 'Assignment' },
]

/** Typed by the server as PascalCase words, e.g. "TechnicianAssigned" → "Technician assigned". */
const humanize = (eventType: string) => {
  const words = eventType.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

function kindOf(event: WorkOrderEvent): Kind {
  const type = event.eventType.toLowerCase()
  if (type === 'comment' || type === 'progressupdate') return 'notes'
  if (type.includes('assign') || type.includes('accept') || type.includes('declin') || type.includes('technician') || type.includes('group')) return 'assignment'
  if (event.oldStatus || event.newStatus || ['statuschanged', 'created', 'scheduled', 'rescheduled', 'escalation', 'overduenotified', 'arrival', 'departure', 'intransit'].includes(type)) return 'status'
  return 'other'
}

function fromEvent(event: WorkOrderEvent): FeedItem {
  const statusChange = event.oldStatus && event.newStatus ? `${event.oldStatus} → ${event.newStatus}` : null
  return {
    id: `event-${event.id}`,
    kind: kindOf(event),
    at: event.occurredAt,
    actor: event.actorUserName ?? 'System',
    title: event.eventType === 'Comment' ? 'Update' : humanize(event.eventType),
    body: [event.message, statusChange, event.reason].filter(Boolean).join(' · ') || null,
    escalation: event.eventType === 'Escalation',
  }
}

function fromNote(note: WorkOrderNote): FeedItem {
  return {
    id: `note-${note.id}`,
    kind: 'notes',
    at: note.createdAt,
    actor: note.createdByUserName ?? 'Unknown user',
    title: 'Note',
    body: note.content,
    visibility: note.isClientVisible ? 'client' : 'internal',
  }
}

const iconFor = (item: FeedItem) => {
  if (item.escalation) return <AlertTriangle className="h-3.5 w-3.5" />
  if (item.kind === 'notes') return <MessageSquare className="h-3.5 w-3.5" />
  if (item.kind === 'assignment') return <UserCog className="h-3.5 w-3.5" />
  return <ArrowRightLeft className="h-3.5 w-3.5" />
}

export function ActivityTab({ workOrderId }: { workOrderId: string }) {
  const events = useWorkOrderEvents(workOrderId)
  const notes = useWorkOrderNotes(workOrderId)
  const addNote = useAddWorkOrderNote(workOrderId)
  const [filter, setFilter] = useState<Filter>('all')
  const [content, setContent] = useState('')
  const [isClientVisible, setIsClientVisible] = useState(false)

  const items = useMemo(() => {
    const merged = [
      // Adding a note also logs a "NoteAdded" event; the note itself is shown instead.
      ...(events.data ?? []).filter((event) => event.eventType !== 'NoteAdded').map(fromEvent),
      ...(notes.data ?? []).map(fromNote),
    ]
    return merged.sort((a, b) => new Date(b.at).getTime() - new Date(a.at).getTime())
  }, [events.data, notes.data])

  const visible = filter === 'all' ? items : items.filter((item) => item.kind === filter)

  const submit = async () => {
    if (!content.trim()) return
    try {
      await addNote.mutateAsync({ content: content.trim(), isClientVisible })
      toast.success(isClientVisible ? 'Client-visible note posted' : 'Internal note posted')
      setContent('')
      setIsClientVisible(false)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="max-w-3xl space-y-4">
      <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-3">
        <label htmlFor="activity-note" className="sr-only">Post a note</label>
        <textarea
          id="activity-note"
          value={content}
          onChange={(event) => setContent(event.target.value)}
          rows={2}
          placeholder="Post an update or note…"
          className="w-full resize-y rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]"
        />
        <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
          <div className="inline-flex rounded-lg border border-slate-200 p-0.5" role="radiogroup" aria-label="Note visibility">
            {[
              { value: false, label: 'Internal', icon: Lock },
              { value: true, label: 'Client-visible', icon: Eye },
            ].map(({ value, label, icon: Icon }) => (
              <button
                key={label}
                type="button"
                role="radio"
                aria-checked={isClientVisible === value}
                onClick={() => setIsClientVisible(value)}
                className={cn(
                  'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium transition',
                  isClientVisible === value ? (value ? 'bg-sky-600 text-white' : 'bg-[var(--app-primary)] text-white') : 'text-slate-600 hover:bg-slate-50',
                )}
              >
                <Icon className="h-3.5 w-3.5" />{label}
              </button>
            ))}
          </div>
          <button
            type="button"
            onClick={submit}
            disabled={addNote.isPending || !content.trim()}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-[var(--app-primary)] px-3 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Send className="h-4 w-4" />{addNote.isPending ? 'Posting…' : 'Post'}
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-1.5" role="tablist" aria-label="Filter activity">
        {FILTERS.map((option) => (
          <button
            key={option.id}
            type="button"
            role="tab"
            aria-selected={filter === option.id}
            onClick={() => setFilter(option.id)}
            className={cn(
              'rounded-full border px-3 py-1 text-xs font-medium transition',
              filter === option.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
            )}
          >
            {option.label}
          </button>
        ))}
      </div>

      {events.isLoading || notes.isLoading ? (
        <Spinner label="Loading activity" />
      ) : events.isError && notes.isError ? (
        <EmptyState title="Activity unavailable" description="Activity could not be loaded for this work order." />
      ) : visible.length === 0 ? (
        <p className="rounded-[var(--radius-card)] border border-dashed border-slate-200 bg-white px-4 py-6 text-center text-sm text-slate-500">
          {filter === 'all' ? 'No activity yet.' : `No ${FILTERS.find((f) => f.id === filter)!.label.toLowerCase()} activity yet.`}
        </p>
      ) : (
        <ol className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white">
          {visible.map((item) => (
            <li key={item.id} className={cn('flex gap-3 px-4 py-3', item.escalation && 'bg-amber-50')}>
              <span className={cn('mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full', item.escalation ? 'bg-amber-100 text-amber-700' : 'bg-slate-100 text-slate-500')}>
                {iconFor(item)}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
                  <span className="text-sm text-slate-900">
                    <span className="font-medium">{item.actor}</span>
                    <span className="text-slate-500"> · {item.title}</span>
                    {item.visibility ? (
                      <span className={cn('ml-2 inline-flex items-center gap-1 rounded-full px-1.5 py-0.5 text-[10px] font-medium', item.visibility === 'client' ? 'bg-sky-50 text-sky-700' : 'bg-slate-100 text-slate-600')}>
                        {item.visibility === 'client' ? <Eye className="h-3 w-3" /> : <Lock className="h-3 w-3" />}
                        {item.visibility === 'client' ? 'Client-visible' : 'Internal'}
                      </span>
                    ) : null}
                  </span>
                  <time className="text-xs text-slate-500" dateTime={item.at} title={formatDateTime(item.at)}>{formatRelative(item.at)}</time>
                </div>
                {item.body ? <p className="mt-1 whitespace-pre-line text-sm leading-6 text-slate-700">{item.body}</p> : null}
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  )
}
