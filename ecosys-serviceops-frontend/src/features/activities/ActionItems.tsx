import { useState } from 'react'
import { Link } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Pencil } from 'lucide-react'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { cn } from '../../shared/utils/cn'
import { formatDate } from '../../shared/utils/dates'
import { dateInputValue, dateOrNull, fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textOrNull } from '../projects/components/formStyles'
import { useActivityPeople, useSaveActionItem } from './api'
import type { ActionItem, ActionItemStatus, LinkedEntityType, SaveActionItemPayload } from './types'

export interface ActionItemDefaults {
  activityId?: string
  entityType?: LinkedEntityType
  entityId?: string
}

const payloadOf = (item: ActionItem, changes: Partial<SaveActionItemPayload> = {}): SaveActionItemPayload => ({
  title: item.title,
  assigneeUserId: item.assigneeUserId,
  assigneeExternalName: item.assigneeExternalName,
  dueDate: item.dueDate,
  status: item.status,
  activityId: null,
  entityType: null,
  entityId: null,
  ...changes,
})

const isOverdue = (item: ActionItem) => item.status === 'Open' && item.dueDate !== null && item.dueDate < new Date().toISOString().slice(0, 10)

/** A compact list of action items with a done tick, for record pages, a meeting and "My action items". */
export function ActionItemList({ items, onEdit, showSource = true, emptyText = 'No action items.' }: {
  items: ActionItem[]
  onEdit?: (item: ActionItem) => void
  showSource?: boolean
  emptyText?: string
}) {
  const save = useSaveActionItem()
  if (items.length === 0) return <p className="text-sm text-slate-500">{emptyText}</p>

  const toggle = (item: ActionItem) =>
    save.mutate({ id: item.id, ...payloadOf(item, { status: item.status === 'Done' ? 'Open' : 'Done' }) })

  return (
    <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
      {items.map((item) => (
        <li key={item.id} className="flex min-w-0 items-start gap-3 px-3 py-2.5">
          <input
            type="checkbox"
            checked={item.status === 'Done'}
            disabled={!item.canEdit || item.status === 'Cancelled' || save.isPending}
            onChange={() => toggle(item)}
            aria-label={`Mark ${item.title} done`}
            className="mt-1 h-4 w-4 shrink-0 rounded border-slate-300"
          />
          <div className="min-w-0 flex-1">
            <div className={cn('break-words text-sm', item.status === 'Open' ? 'font-medium text-slate-900' : 'text-slate-400 line-through')}>{item.title}</div>
            <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-slate-500">
              <span>{item.assigneeName ?? item.assigneeExternalName ?? 'Unassigned'}</span>
              {item.dueDate ? <span className={isOverdue(item) ? 'font-medium text-rose-600' : ''}>Due {formatDate(item.dueDate)}</span> : null}
              {showSource && item.activityId ? <Link to={`/activities/${item.activityId}`} className="underline decoration-slate-300 underline-offset-2">{item.activityTitle ?? 'Meeting'}</Link> : null}
              {showSource && item.linkedRecord?.url ? <Link to={item.linkedRecord.url} className="truncate underline decoration-slate-300 underline-offset-2">{item.linkedRecord.label}</Link> : null}
            </div>
          </div>
          {item.status === 'Cancelled' ? <StatusBadge status="Cancelled" /> : null}
          {onEdit && item.canEdit ? (
            <button type="button" aria-label={`Edit ${item.title}`} className={smallButtonClass} onClick={() => onEdit(item)}><Pencil className="h-3.5 w-3.5" /></button>
          ) : null}
        </li>
      ))}
    </ul>
  )
}

interface Draft {
  title: string
  owner: string
  externalName: string
  dueDate: string
  status: ActionItemStatus
}

/** Add or edit an action item. The assignee is told in their bell and by email. */
export function ActionItemDrawer({ item, defaults, onClose }: { item?: ActionItem; defaults?: ActionItemDefaults; onClose: () => void }) {
  const { data: people } = useActivityPeople(null)
  const save = useSaveActionItem()
  const [draft, setDraft] = useState<Draft>(() => ({
    title: item?.title ?? '',
    owner: item?.assigneeUserId ?? (item?.assigneeExternalName ? 'external' : ''),
    externalName: item?.assigneeExternalName ?? '',
    dueDate: dateInputValue(item?.dueDate),
    status: item?.status ?? 'Open',
  }))

  const submit = async () => {
    if (!draft.title.trim()) return
    const external = draft.owner === 'external'
    try {
      await save.mutateAsync({
        ...(item ? { id: item.id } : {}),
        title: draft.title.trim(),
        assigneeUserId: draft.owner && !external ? draft.owner : null,
        assigneeExternalName: external ? textOrNull(draft.externalName) : null,
        dueDate: dateOrNull(draft.dueDate),
        status: item ? draft.status : null,
        activityId: item ? null : defaults?.activityId ?? null,
        entityType: item ? null : defaults?.entityType ?? null,
        entityId: item ? null : defaults?.entityId ?? null,
      })
      toast.success(item ? 'Action item saved' : 'Action item added')
      onClose()
    } catch {
      // toast from the API client
    }
  }

  return (
    <FormDrawer
      open
      title={item ? 'Edit action item' : 'Add action item'}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButtonClass} onClick={onClose}>Cancel</button>
          <button type="button" className={primaryButtonClass} disabled={!draft.title.trim() || save.isPending} onClick={() => void submit()}>Save</button>
        </div>
      }
    >
      <div className="space-y-4">
        <div>
          <label className={labelClass} htmlFor="action-title">What needs doing</label>
          <input id="action-title" value={draft.title} maxLength={300} onChange={(e) => setDraft({ ...draft, title: e.target.value })} className={fieldClass} />
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelClass} htmlFor="action-owner">Owner</label>
            <select id="action-owner" value={draft.owner} onChange={(e) => setDraft({ ...draft, owner: e.target.value })} className={fieldClass}>
              <option value="">Unassigned</option>
              {(people?.users ?? []).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              <option value="external">Someone without a login…</option>
            </select>
          </div>
          {draft.owner === 'external' ? (
            <div>
              <label className={labelClass} htmlFor="action-external">Their name</label>
              <input id="action-external" value={draft.externalName} maxLength={200} placeholder="e.g. Consultant" onChange={(e) => setDraft({ ...draft, externalName: e.target.value })} className={fieldClass} />
            </div>
          ) : null}
          <div>
            <label className={labelClass} htmlFor="action-due">Due date</label>
            <input id="action-due" type="date" value={draft.dueDate} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} className={fieldClass} />
          </div>
          {item ? (
            <div>
              <label className={labelClass} htmlFor="action-status">Status</label>
              <select id="action-status" value={draft.status} onChange={(e) => setDraft({ ...draft, status: e.target.value as ActionItemStatus })} className={fieldClass}>
                {(['Open', 'Done', 'Cancelled'] as const).map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
            </div>
          ) : null}
        </div>
      </div>
    </FormDrawer>
  )
}
