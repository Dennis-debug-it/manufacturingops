import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, X } from 'lucide-react'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { usePermission } from '../../shared/hooks/usePermission'
import { useAuth } from '../../shared/hooks/useAuth'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull, todayInput } from '../projects/components/formStyles'
import { useActivityPeople, useSaveActivity } from './api'
import { fromActivityTimes, toActivityTimes, type ActivityTimesDraft } from './logic'
import { activityTypeLabels, type ActivityDetail, type ActivityType, type AttendeePayload, type LinkedEntityType } from './types'

export interface ActivityDrawerDefaults {
  entityType?: LinkedEntityType
  entityId?: string
  /** The linked record's client, so its portal contacts can be invited. */
  clientId?: string
  date?: string
  title?: string
}

interface External {
  name: string
  email: string
}

interface Draft extends ActivityTimesDraft {
  type: ActivityType
  title: string
  location: string
  agenda: string
  reminder: string
  organizerUserId: string
  userIds: string[]
  consultantIds: string[]
  contactIds: string[]
  externals: External[]
}

const reminderOptions: Array<{ value: string; label: string }> = [
  { value: '', label: 'No reminder' },
  { value: '10', label: '10 minutes before' },
  { value: '30', label: '30 minutes before' },
  { value: '60', label: '1 hour before' },
  { value: '1440', label: '1 day before' },
]

function draftFrom(activity: ActivityDetail | undefined, defaults: ActivityDrawerDefaults | undefined, userId: string): Draft {
  if (activity) {
    const { summary } = activity
    return {
      ...fromActivityTimes(summary.startsAt, summary.endsAt, summary.allDay),
      type: summary.type,
      title: summary.title,
      location: summary.location ?? '',
      agenda: activity.agenda ?? '',
      reminder: activity.reminderMinutesBefore?.toString() ?? '',
      organizerUserId: summary.organizerUserId,
      userIds: activity.attendees.filter((x) => x.userId).map((x) => x.userId!),
      consultantIds: activity.attendees.filter((x) => x.consultantId).map((x) => x.consultantId!),
      contactIds: activity.attendees.filter((x) => x.clientContactId).map((x) => x.clientContactId!),
      externals: activity.attendees.filter((x) => x.kind === 'External').map((x) => ({ name: x.name === x.email ? '' : x.name, email: x.email ?? '' })),
    }
  }
  const date = defaults?.date ?? todayInput()
  return {
    date, endDate: date, startTime: '09:00', endTime: '10:00', allDay: false,
    type: 'Meeting', title: defaults?.title ?? '', location: '', agenda: '', reminder: '30', organizerUserId: userId,
    userIds: [], consultantIds: [], contactIds: [], externals: [],
  }
}

/** Schedule or change a meeting, site visit, inspection or call. Staff attendees are notified; others are recorded. */
export function ActivityDrawer({ open, onClose, activity, defaults, onSaved }: {
  open: boolean
  onClose: () => void
  activity?: ActivityDetail
  defaults?: ActivityDrawerDefaults
  onSaved?: (saved: ActivityDetail) => void
}) {
  const { user } = useAuth()
  const canManageAny = usePermission('activities.manage')
  const [draft, setDraft] = useState<Draft>(() => draftFrom(activity, defaults, user?.userId ?? ''))
  const [staffFilter, setStaffFilter] = useState('')
  const linked = activity?.summary.linkedRecord
  const entityType = linked?.entityType ?? defaults?.entityType ?? null
  const entityId = linked?.entityId ?? defaults?.entityId ?? null
  const clientId = defaults?.clientId ?? (entityType === 'Client' ? entityId : null)
  const { data: people } = useActivityPeople(clientId, open)
  const save = useSaveActivity()

  const staff = useMemo(() => {
    const term = staffFilter.trim().toLowerCase()
    return (people?.users ?? []).filter((x) => x.id !== draft.organizerUserId && (!term || x.name.toLowerCase().includes(term) || draft.userIds.includes(x.id)))
  }, [people, staffFilter, draft.organizerUserId, draft.userIds])

  if (!open) return null

  const toggle = (list: 'userIds' | 'consultantIds' | 'contactIds', id: string) =>
    setDraft((d) => ({ ...d, [list]: d[list].includes(id) ? d[list].filter((x) => x !== id) : [...d[list], id] }))

  const submit = async () => {
    if (!draft.title.trim()) return
    const attendees: AttendeePayload[] = [
      ...draft.userIds.map((id) => ({ userId: id, consultantId: null, clientContactId: null, externalName: null, externalEmail: null })),
      ...draft.consultantIds.map((id) => ({ userId: null, consultantId: id, clientContactId: null, externalName: null, externalEmail: null })),
      ...draft.contactIds.map((id) => ({ userId: null, consultantId: null, clientContactId: id, externalName: null, externalEmail: null })),
      ...draft.externals.filter((x) => x.name.trim() || x.email.trim())
        .map((x) => ({ userId: null, consultantId: null, clientContactId: null, externalName: textOrNull(x.name), externalEmail: textOrNull(x.email) })),
    ]
    try {
      const saved = await save.mutateAsync({
        ...(activity ? { id: activity.summary.id } : {}),
        type: draft.type,
        title: draft.title.trim(),
        ...toActivityTimes(draft),
        allDay: draft.allDay,
        location: textOrNull(draft.location),
        organizerUserId: draft.organizerUserId || null,
        entityType,
        entityId,
        agenda: textOrNull(draft.agenda),
        reminderMinutesBefore: draft.reminder ? Number(draft.reminder) : null,
        attendees,
      })
      toast.success(activity ? 'Changes saved; staff attendees have been told' : 'Scheduled; staff attendees have been told')
      onSaved?.(saved)
      onClose()
    } catch {
      // toast from the API client
    }
  }

  return (
    <FormDrawer
      open={open}
      title={activity ? `Edit ${activityTypeLabels[draft.type].toLowerCase()}` : 'Schedule'}
      onClose={onClose}
      closeOnOverlayClick={false}
      footer={
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-xs text-slate-500">Staff get it in their bell and by email. Others are recorded only.</p>
          <div className="flex gap-2">
            <button type="button" className={secondaryButtonClass} onClick={onClose}>Cancel</button>
            <button type="button" className={primaryButtonClass} disabled={!draft.title.trim() || save.isPending} onClick={() => void submit()}>
              {activity ? 'Save' : 'Schedule'}
            </button>
          </div>
        </div>
      }
    >
      <div className="space-y-4">
        {linked ? <p className="rounded-lg bg-slate-50 px-3 py-2 text-sm text-slate-600">About <span className="font-medium text-slate-900">{linked.label}</span></p> : null}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelClass} htmlFor="activity-type">Type</label>
            <select id="activity-type" value={draft.type} onChange={(e) => setDraft({ ...draft, type: e.target.value as ActivityType })} className={fieldClass}>
              {(Object.keys(activityTypeLabels) as ActivityType[]).map((type) => <option key={type} value={type}>{activityTypeLabels[type]}</option>)}
            </select>
          </div>
          <div className="sm:col-span-2">
            <label className={labelClass} htmlFor="activity-title">Title</label>
            <input id="activity-title" value={draft.title} maxLength={200} onChange={(e) => setDraft({ ...draft, title: e.target.value })} className={fieldClass} />
          </div>
        </div>

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" checked={draft.allDay} onChange={(e) => setDraft({ ...draft, allDay: e.target.checked })} className="h-4 w-4 rounded border-slate-300" />
          All day
        </label>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          <div className="col-span-2 sm:col-span-1">
            <label className={labelClass} htmlFor="activity-date">{draft.allDay ? 'From' : 'Date'}</label>
            <input id="activity-date" type="date" value={draft.date} onChange={(e) => setDraft({ ...draft, date: e.target.value, endDate: draft.endDate < e.target.value ? e.target.value : draft.endDate })} className={fieldClass} />
          </div>
          {draft.allDay ? (
            <div className="col-span-2 sm:col-span-1">
              <label className={labelClass} htmlFor="activity-end-date">To</label>
              <input id="activity-end-date" type="date" min={draft.date} value={draft.endDate} onChange={(e) => setDraft({ ...draft, endDate: e.target.value })} className={fieldClass} />
            </div>
          ) : (
            <>
              <div>
                <label className={labelClass} htmlFor="activity-start">Starts</label>
                <input id="activity-start" type="time" value={draft.startTime} onChange={(e) => setDraft({ ...draft, startTime: e.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="activity-end">Ends</label>
                <input id="activity-end" type="time" value={draft.endTime} onChange={(e) => setDraft({ ...draft, endTime: e.target.value })} className={fieldClass} />
              </div>
            </>
          )}
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="activity-location">Place or meeting link</label>
            <input id="activity-location" value={draft.location} maxLength={500} onChange={(e) => setDraft({ ...draft, location: e.target.value })} className={fieldClass} />
          </div>
          <div>
            <label className={labelClass} htmlFor="activity-reminder">Reminder</label>
            <select id="activity-reminder" value={draft.reminder} onChange={(e) => setDraft({ ...draft, reminder: e.target.value })} className={fieldClass} disabled={draft.allDay}>
              {reminderOptions.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
            </select>
            {draft.allDay ? <p className="mt-1 text-xs text-slate-500">All-day activities have no timed reminder.</p> : null}
          </div>
        </div>
        {canManageAny ? (
          <div>
            <label className={labelClass} htmlFor="activity-organizer">Organiser</label>
            <select id="activity-organizer" value={draft.organizerUserId} onChange={(e) => setDraft({ ...draft, organizerUserId: e.target.value, userIds: draft.userIds.filter((x) => x !== e.target.value) })} className={fieldClass}>
              {(people?.users ?? []).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </div>
        ) : null}

        <fieldset className="space-y-2">
          <legend className={labelClass}>Staff attendees</legend>
          <input value={staffFilter} onChange={(e) => setStaffFilter(e.target.value)} placeholder="Find a person" aria-label="Find a person" className={fieldClass} />
          <div className="max-h-48 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
            {staff.length === 0 ? <p className="px-1 text-sm text-slate-500">No one matches.</p> : staff.map((x) => (
              <label key={x.id} className="flex min-w-0 items-center gap-2 rounded px-1 py-1 text-sm hover:bg-slate-50">
                <input type="checkbox" checked={draft.userIds.includes(x.id)} onChange={() => toggle('userIds', x.id)} className="h-4 w-4 shrink-0 rounded border-slate-300" />
                <span className="truncate text-slate-900">{x.name}</span>
                {x.detail ? <span className="truncate text-xs text-slate-500">{x.detail}</span> : null}
              </label>
            ))}
          </div>
        </fieldset>

        {(people?.consultants.length ?? 0) > 0 ? (
          <fieldset className="space-y-1">
            <legend className={labelClass}>Consultants</legend>
            <div className="max-h-36 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
              {people!.consultants.map((x) => (
                <label key={x.id} className="flex min-w-0 items-center gap-2 px-1 py-1 text-sm">
                  <input type="checkbox" checked={draft.consultantIds.includes(x.id)} onChange={() => toggle('consultantIds', x.id)} className="h-4 w-4 shrink-0 rounded border-slate-300" />
                  <span className="truncate">{x.name}</span>
                  {x.detail ? <span className="truncate text-xs text-slate-500">{x.detail}</span> : null}
                </label>
              ))}
            </div>
          </fieldset>
        ) : null}

        {(people?.clientContacts.length ?? 0) > 0 ? (
          <fieldset className="space-y-1">
            <legend className={labelClass}>Client contacts</legend>
            <div className="space-y-1 rounded-lg border border-slate-200 p-2">
              {people!.clientContacts.map((x) => (
                <label key={x.id} className="flex min-w-0 items-center gap-2 px-1 py-1 text-sm">
                  <input type="checkbox" checked={draft.contactIds.includes(x.id)} onChange={() => toggle('contactIds', x.id)} className="h-4 w-4 shrink-0 rounded border-slate-300" />
                  <span className="truncate">{x.name}</span>
                </label>
              ))}
            </div>
          </fieldset>
        ) : null}

        <fieldset className="space-y-2">
          <legend className={labelClass}>Other people</legend>
          {draft.externals.map((external, index) => (
            <div key={index} className="flex min-w-0 gap-2">
              <input value={external.name} placeholder="Name" aria-label="Name" onChange={(e) => setDraft({ ...draft, externals: draft.externals.map((x, i) => (i === index ? { ...x, name: e.target.value } : x)) })} className={`${fieldClass} min-w-0`} />
              <input value={external.email} type="email" placeholder="Email (optional)" aria-label="Email" onChange={(e) => setDraft({ ...draft, externals: draft.externals.map((x, i) => (i === index ? { ...x, email: e.target.value } : x)) })} className={`${fieldClass} min-w-0`} />
              <button type="button" aria-label="Remove" className={smallButtonClass} onClick={() => setDraft({ ...draft, externals: draft.externals.filter((_, i) => i !== index) })}><X className="h-3.5 w-3.5" /></button>
            </div>
          ))}
          <button type="button" className={smallButtonClass} onClick={() => setDraft({ ...draft, externals: [...draft.externals, { name: '', email: '' }] })}><Plus className="h-3.5 w-3.5" />Add someone</button>
          <p className="text-xs text-slate-500">Consultants, client contacts and other people are recorded on the meeting but not emailed. Invite them as you usually do.</p>
        </fieldset>

        <div>
          <label className={labelClass} htmlFor="activity-agenda">Agenda</label>
          <textarea id="activity-agenda" rows={3} value={draft.agenda} maxLength={4000} onChange={(e) => setDraft({ ...draft, agenda: e.target.value })} className={textAreaClass} />
        </div>
      </div>
    </FormDrawer>
  )
}
