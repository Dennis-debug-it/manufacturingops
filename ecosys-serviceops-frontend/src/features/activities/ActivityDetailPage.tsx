import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Ban, Download, Lock, Pencil, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { EmptyState } from '../../shared/components/EmptyState'
import { Spinner } from '../../shared/components/Spinner'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull } from '../../shared/components/formStyles'
import {
  downloadMinutesPdf,
  useActivity,
  useAddMinutesAddendum,
  useCancelActivity,
  useMarkActivityHeld,
  usePublishMinutes,
  useRespondToActivity,
  useSaveMinutes,
} from './api'
import { ActivityDrawer } from './ActivityDrawer'
import { ActionItemDrawer, ActionItemList } from './ActionItems'
import { itemTimeLabel } from './logic'
import { activityTypeLabels, type ActionItem, type ActivityDetail } from './types'

const kindLabels = { Staff: 'Staff', Consultant: 'Consultant', ClientContact: 'Client contact', External: 'Guest' } as const

export function ActivityDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { data: activity, isLoading, isError } = useActivity(id)
  if (isLoading) return <Spinner label="Loading activity" />
  if (isError || !activity) return <EmptyState title="Activity unavailable" description="It may have been removed, or you aren't on it." />
  return <ActivityDetailView key={activity.summary.id} activity={activity} />
}

function ActivityDetailView({ activity }: { activity: ActivityDetail }) {
  const { summary, canManage } = activity
  const id = summary.id
  const { user } = useAuth()
  const cancel = useCancelActivity()
  const respond = useRespondToActivity()
  const markHeld = useMarkActivityHeld()
  const saveMinutes = useSaveMinutes()
  const publish = usePublishMinutes()
  const addAddendum = useAddMinutesAddendum()
  const [editing, setEditing] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const [reason, setReason] = useState('')
  const [publishing, setPublishing] = useState(false)
  const [attended, setAttended] = useState<string[]>(() => activity.attendees.filter((x) => x.attended).map((x) => x.id))
  const [minutes, setMinutes] = useState(activity.minutes ?? '')
  const [addendum, setAddendum] = useState('')
  const [actionItem, setActionItem] = useState<ActionItem | 'new' | null>(null)

  const published = Boolean(summary.minutesPublishedAt)
  const started = new Date(summary.startsAt) <= new Date()
  const canRecord = canManage && summary.status !== 'Cancelled' && started && !published
  const myAttendee = activity.attendees.find((x) => x.userId !== null && x.userId === user?.userId)

  const run = async (action: () => Promise<unknown>, success: string) => {
    try {
      await action()
      toast.success(success)
    } catch {
      // toast from the API client
    }
  }

  const when = summary.allDay
    ? `${formatDate(summary.startsAt.slice(0, 10))}${summary.endsAt.slice(0, 10) !== summary.startsAt.slice(0, 10) ? ` – ${formatDate(summary.endsAt.slice(0, 10))}` : ''} · All day`
    : `${formatDate(summary.startsAt, 'EEE d MMM yyyy')} · ${itemTimeLabel(summary)}`

  return (
    <div className="space-y-5">
      <PageHeader
        title={summary.title}
        subtitle={`${activityTypeLabels[summary.type]} · ${when}`}
        breadcrumbs={[{ label: 'Calendar', to: '/calendar' }, { label: summary.title }]}
        titleAdornment={<StatusBadge status={published ? 'Minutes published' : summary.status} />}
        secondaryActions={canManage && summary.status === 'Scheduled' ? (
          <>
            <button type="button" className={secondaryButtonClass} onClick={() => setEditing(true)}><Pencil className="h-4 w-4" />Edit</button>
            <button type="button" className={secondaryButtonClass} onClick={() => setCancelling(true)}><Ban className="h-4 w-4" />Cancel</button>
          </>
        ) : undefined}
      />

      <section className="grid grid-cols-1 gap-3 rounded-2xl border border-slate-200 bg-white p-4 text-sm sm:grid-cols-2">
        <Detail label="Where" value={summary.location ?? 'Not set'} />
        <Detail label="Organiser" value={summary.organizerName ?? '—'} />
        <div>
          <div className="text-xs font-medium uppercase tracking-wide text-slate-500">About</div>
          {summary.linkedRecord?.url ? <Link to={summary.linkedRecord.url} className="break-words text-slate-900 underline decoration-slate-300 underline-offset-4">{summary.linkedRecord.label}</Link> : <div className="text-slate-900">{summary.linkedRecord?.label ?? '—'}</div>}
        </div>
        <Detail label="Reminder" value={activity.reminderMinutesBefore != null && !summary.allDay ? `${activity.reminderMinutesBefore} minutes before` : 'None'} />
        {summary.status === 'Cancelled' ? <Detail label="Cancelled" value={activity.cancellationReason ?? 'No reason given'} /> : null}
        {activity.agenda ? <div className="sm:col-span-2"><div className="text-xs font-medium uppercase tracking-wide text-slate-500">Agenda</div><p className="whitespace-pre-wrap break-words text-slate-900">{activity.agenda}</p></div> : null}
      </section>

      {myAttendee && summary.status === 'Scheduled' ? (
        <section className="flex flex-wrap items-center gap-2 rounded-2xl border border-slate-200 bg-white p-4 text-sm">
          <span className="text-slate-600">Your reply: <span className="font-medium text-slate-900">{myAttendee.response}</span></span>
          <button type="button" className={smallButtonClass} disabled={respond.isPending} onClick={() => void run(() => respond.mutateAsync({ id, response: 'Accepted' }), 'Accepted')}>Accept</button>
          <button type="button" className={smallButtonClass} disabled={respond.isPending} onClick={() => void run(() => respond.mutateAsync({ id, response: 'Declined' }), 'Declined')}>Decline</button>
        </section>
      ) : null}

      <section className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4">
        <h2 className="text-sm font-semibold text-slate-900">Attendees</h2>
        {activity.attendees.length === 0 ? <p className="text-sm text-slate-500">Only the organiser.</p> : (
          <ul className="divide-y divide-slate-100">
            {activity.attendees.map((attendee) => (
              <li key={attendee.id} className="flex min-w-0 items-center gap-3 py-2 text-sm">
                {canRecord ? (
                  <input type="checkbox" aria-label={`${attendee.name} attended`} checked={attended.includes(attendee.id)}
                    onChange={() => setAttended((list) => (list.includes(attendee.id) ? list.filter((x) => x !== attendee.id) : [...list, attendee.id]))}
                    className="h-4 w-4 shrink-0 rounded border-slate-300" />
                ) : null}
                <div className="min-w-0 flex-1">
                  <div className="truncate font-medium text-slate-900">{attendee.name}</div>
                  <div className="truncate text-xs text-slate-500">{kindLabels[attendee.kind]}{attendee.email ? ` · ${attendee.email}` : ''}</div>
                </div>
                {summary.status === 'Held' && !canRecord ? <span className="text-xs text-slate-500">{attendee.attended ? 'Attended' : 'Absent'}</span> : null}
                {attendee.response && summary.status === 'Scheduled' ? <StatusBadge status={attendee.response} /> : null}
              </li>
            ))}
          </ul>
        )}
        {canRecord ? (
          <div className="flex flex-wrap items-center gap-2 pt-1">
            <button type="button" className={smallButtonClass} disabled={markHeld.isPending} onClick={() => void run(() => markHeld.mutateAsync({ id, attendedAttendeeIds: attended }), summary.status === 'Held' ? 'Attendance saved' : 'Marked as held')}>
              {summary.status === 'Held' ? 'Save attendance' : 'Mark as held'}
            </button>
            <span className="text-xs text-slate-500">Tick who came, then write the minutes.</span>
          </div>
        ) : null}
      </section>

      {summary.status === 'Held' || published ? (
        <section className="space-y-3 rounded-2xl border border-slate-200 bg-white p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-sm font-semibold text-slate-900">Minutes</h2>
            <div className="flex flex-wrap gap-2">
              {activity.minutes ? <button type="button" className={smallButtonClass} onClick={() => void downloadMinutesPdf(id, `Minutes-${summary.startsAt.slice(0, 10)}.pdf`)}><Download className="h-3.5 w-3.5" />PDF</button> : null}
            </div>
          </div>
          {published ? (
            <>
              <p className="flex items-center gap-1 text-xs text-slate-500"><Lock className="h-3.5 w-3.5" />Published {formatDateTime(summary.minutesPublishedAt!)} and sent to staff attendees. Corrections are added below.</p>
              <p className="whitespace-pre-wrap break-words text-sm text-slate-900">{activity.minutes}</p>
              {activity.addenda.map((x) => (
                <div key={x.id} className="rounded-lg border-l-4 border-amber-300 bg-amber-50 px-3 py-2 text-sm">
                  <div className="text-xs text-amber-800">Addendum · {formatDateTime(x.createdAt)} · {x.authorName ?? 'Unknown'}</div>
                  <p className="whitespace-pre-wrap break-words text-slate-900">{x.text}</p>
                </div>
              ))}
              {canManage ? (
                <div className="space-y-2">
                  <label className={labelClass} htmlFor="minutes-addendum">Add a correction</label>
                  <textarea id="minutes-addendum" rows={2} value={addendum} maxLength={4000} onChange={(e) => setAddendum(e.target.value)} className={textAreaClass} />
                  <button type="button" className={smallButtonClass} disabled={!addendum.trim() || addAddendum.isPending}
                    onClick={() => void run(async () => { await addAddendum.mutateAsync({ id, text: addendum.trim() }); setAddendum('') }, 'Addendum added')}>Add addendum</button>
                </div>
              ) : null}
            </>
          ) : canManage ? (
            <>
              <textarea aria-label="Minutes" rows={10} value={minutes} maxLength={20000} onChange={(e) => setMinutes(e.target.value)} className={textAreaClass} placeholder="What was discussed and agreed" />
              <div className="flex flex-wrap gap-2">
                <button type="button" className={secondaryButtonClass} disabled={saveMinutes.isPending} onClick={() => void run(() => saveMinutes.mutateAsync({ id, minutes: textOrNull(minutes) }), 'Minutes saved')}>Save draft</button>
                <button type="button" className={primaryButtonClass} disabled={!minutes.trim() || publish.isPending} onClick={() => setPublishing(true)}>Publish minutes</button>
              </div>
            </>
          ) : (
            <p className="text-sm text-slate-500">The minutes haven't been published yet.</p>
          )}
        </section>
      ) : null}

      <section className="space-y-2 rounded-2xl border border-slate-200 bg-white p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-slate-900">Action items</h2>
          {canManage && summary.status !== 'Cancelled' ? <button type="button" className={smallButtonClass} onClick={() => setActionItem('new')}><Plus className="h-3.5 w-3.5" />Add action item</button> : null}
        </div>
        <ActionItemList items={activity.actionItems} onEdit={(item) => setActionItem(item)} showSource={false} />
      </section>

      {editing ? <ActivityDrawer open onClose={() => setEditing(false)} activity={activity} /> : null}
      {actionItem ? (
        <ActionItemDrawer {...(actionItem === 'new' ? { defaults: { activityId: id } } : { item: actionItem })} onClose={() => setActionItem(null)} />
      ) : null}

      <ConfirmDialog
        open={cancelling}
        title={`Cancel ${summary.title}`}
        message="Staff attendees are told it is off their calendar."
        confirmLabel="Cancel it"
        danger
        confirmDisabled={cancel.isPending}
        onCancel={() => setCancelling(false)}
        onConfirm={() => void run(async () => { await cancel.mutateAsync({ id, reason: textOrNull(reason) }); setCancelling(false) }, 'Cancelled')}
      >
        <div className="text-left">
          <label className={labelClass} htmlFor="cancel-reason">Reason (optional)</label>
          <input id="cancel-reason" value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className={fieldClass} />
        </div>
      </ConfirmDialog>

      <ConfirmDialog
        open={publishing}
        title="Publish minutes"
        message="The minutes are emailed as a PDF to the staff attendees and locked. Later corrections are added as addenda."
        confirmLabel="Publish"
        confirmDisabled={publish.isPending}
        onCancel={() => setPublishing(false)}
        onConfirm={() => void run(async () => {
          await saveMinutes.mutateAsync({ id, minutes: textOrNull(minutes) })
          await publish.mutateAsync(id)
          setPublishing(false)
        }, 'Minutes published')}
      />
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="min-w-0">
      <div className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</div>
      <div className="break-words text-slate-900">{value}</div>
    </div>
  )
}
