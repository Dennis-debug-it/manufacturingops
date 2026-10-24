import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CalendarPlus, Plus } from 'lucide-react'
import { StatusBadge } from '../../shared/components/StatusBadge'
import { usePermission } from '../../shared/hooks/usePermission'
import { formatDate } from '../../shared/utils/dates'
import { smallButtonClass } from '../projects/components/formStyles'
import { useActionItems, useActivities } from './api'
import { ActivityDrawer } from './ActivityDrawer'
import { ActionItemDrawer, ActionItemList } from './ActionItems'
import { itemTimeLabel } from './logic'
import { activityTypeLabels, type ActionItem, type LinkedEntityType } from './types'

/**
 * Meetings and action items for one record: a project's Tasks tab, a client page or a contract page
 * (R3). Labelled apart from the record's own tasks.
 */
export function RecordActivitiesPanel({ entityType, entityId, clientId, readOnly = false, show = ['actions', 'meetings'] }: {
  entityType: LinkedEntityType
  entityId: string
  clientId?: string
  readOnly?: boolean
  show?: Array<'actions' | 'meetings'>
}) {
  const canView = usePermission('activities.view')
  const { data: activities } = useActivities({ entityType, entityId }, canView && show.includes('meetings'))
  const { data: items } = useActionItems({ entityType, entityId }, canView && show.includes('actions'))
  const [scheduling, setScheduling] = useState(false)
  const [editing, setEditing] = useState<ActionItem | 'new' | null>(null)
  const [showClosed, setShowClosed] = useState(false)
  if (!canView) return null

  const visibleItems = (items ?? []).filter((x) => showClosed || x.status === 'Open')
  const canAdd = !readOnly

  return (
    <div className="space-y-6">
      {show.includes('actions') ? (
        <section className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">Action items</h3>
              <p className="text-xs text-slate-500">Follow-ups from meetings and on this record.</p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-2 text-xs text-slate-600">
                <input type="checkbox" checked={showClosed} onChange={(e) => setShowClosed(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
                Show done
              </label>
              {canAdd ? <button type="button" className={smallButtonClass} onClick={() => setEditing('new')}><Plus className="h-3.5 w-3.5" />Add action item</button> : null}
            </div>
          </div>
          <ActionItemList items={visibleItems} onEdit={(item) => setEditing(item)} showSource={false} emptyText="No open action items." />
        </section>
      ) : null}

      {show.includes('meetings') ? (
        <section className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 className="text-sm font-semibold text-slate-900">Meetings & visits</h3>
              <p className="text-xs text-slate-500">Scheduled and held activities about this record.</p>
            </div>
            {canAdd ? <button type="button" className={smallButtonClass} onClick={() => setScheduling(true)}><CalendarPlus className="h-3.5 w-3.5" />Schedule</button> : null}
          </div>
          {(activities ?? []).length === 0 ? <p className="text-sm text-slate-500">Nothing scheduled.</p> : (
            <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
              {(activities ?? []).map((activity) => (
                <li key={activity.id}>
                  <Link to={`/activities/${activity.id}`} className="flex min-w-0 items-center gap-3 px-3 py-2.5 hover:bg-slate-50">
                    <div className="w-20 shrink-0 text-xs text-slate-500">
                      <div className="font-medium text-slate-900">{formatDate(activity.startsAt, 'dd MMM')}</div>
                      <div>{itemTimeLabel(activity)}</div>
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium text-slate-900">{activity.title}</div>
                      <div className="truncate text-xs text-slate-500">{activityTypeLabels[activity.type]}{activity.location ? ` · ${activity.location}` : ''}</div>
                    </div>
                    {activity.status !== 'Scheduled' ? <StatusBadge status={activity.minutesPublishedAt ? 'Minutes published' : activity.status} /> : null}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {scheduling ? (
        <ActivityDrawer open onClose={() => setScheduling(false)} defaults={{ entityType, entityId, ...(clientId ? { clientId } : {}) }} />
      ) : null}
      {editing ? (
        <ActionItemDrawer
          {...(editing === 'new' ? { defaults: { entityType, entityId } } : { item: editing })}
          onClose={() => setEditing(null)}
        />
      ) : null}
    </div>
  )
}
