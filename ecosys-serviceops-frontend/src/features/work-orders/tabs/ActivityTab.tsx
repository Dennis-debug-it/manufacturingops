import { useMemo } from 'react'
import { ArrowRightLeft, Camera, MessageSquare, PenLine, Receipt, Package, Activity as ActivityIcon } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { formatDateTime, formatRelative } from '../../../shared/utils/dates'
import { useWorkOrderEvents } from '../api'
import type { WorkOrderEvent } from '../types'

const iconForEventType = (eventType: string) => {
  const type = eventType.toLowerCase()
  if (type.includes('status') || type.includes('assign') || type.includes('transit') || type.includes('arrival') || type.includes('departure')) {
    return <ArrowRightLeft className="h-4 w-4" aria-hidden="true" />
  }
  if (type.includes('note') || type.includes('comment')) {
    return <MessageSquare className="h-4 w-4" aria-hidden="true" />
  }
  if (type.includes('photo')) {
    return <Camera className="h-4 w-4" aria-hidden="true" />
  }
  if (type.includes('part') || type.includes('material')) {
    return <Package className="h-4 w-4" aria-hidden="true" />
  }
  if (type.includes('signature')) {
    return <PenLine className="h-4 w-4" aria-hidden="true" />
  }
  if (type.includes('invoice')) {
    return <Receipt className="h-4 w-4" aria-hidden="true" />
  }
  return <ActivityIcon className="h-4 w-4" aria-hidden="true" />
}

const initials = (name: string | null) => {
  if (!name) return '?'
  const parts = name.trim().split(/\s+/)
  return parts
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function ActivityEventRow({ event }: { event: WorkOrderEvent }) {
  return (
    <li className="relative flex gap-3 pb-6 last:pb-0">
      <span className="absolute left-4 top-9 bottom-0 w-px bg-slate-200 last:hidden" aria-hidden="true" />
      <span className="relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-600 ring-4 ring-white">
        {iconForEventType(event.eventType)}
      </span>
      <div className="min-w-0 flex-1 rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex items-center gap-2">
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-slate-900 text-[10px] font-semibold text-white">
              {initials(event.actorUserName)}
            </span>
            <span className="text-sm font-medium text-slate-900">{event.actorUserName ?? 'System'}</span>
          </div>
          <span className="text-xs text-slate-500" title={formatDateTime(event.occurredAt)}>
            {formatRelative(event.occurredAt)}
          </span>
        </div>
        {event.message ? <p className="mt-2 text-sm leading-6 text-slate-700">{event.message}</p> : null}
      </div>
    </li>
  )
}

export function ActivityTab({ workOrderId }: { workOrderId: string }) {
  const { data, isLoading, isError } = useWorkOrderEvents(workOrderId)

  const events = useMemo(
    () => [...(data ?? [])].sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime()),
    [data],
  )

  if (isLoading) {
    return <Spinner label="Loading activity" />
  }

  if (isError) {
    return <EmptyState title="Activity unavailable" description="Activity could not be loaded for this work order." />
  }

  if (events.length === 0) {
    return <EmptyState title="No activity yet" description="Status changes, notes, photos, parts, and signatures will appear here as they happen." />
  }

  return (
    <ol className="max-w-2xl">
      {events.map((event) => (
        <ActivityEventRow key={event.id} event={event} />
      ))}
    </ol>
  )
}
