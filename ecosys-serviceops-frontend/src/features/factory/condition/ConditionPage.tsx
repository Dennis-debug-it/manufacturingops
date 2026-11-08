import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Activity, AlertTriangle, BellOff, CheckCircle2, Gauge } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { StatCard } from '../../../shared/components/StatCard'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, LocationSelect, formatNumber, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { ReadingsChart } from '../components/ReadingsChart'
import { useFactoryLinks } from '../links'
import type { ConditionLevel } from '../types'
import {
  describeTrend, useAcknowledgeEvent, useConditionEvents, useConditionOverview, useMeterCondition,
  type ConditionEvent, type ConditionMeterRow,
} from './api'

const LEVEL_TONES: Record<ConditionLevel, BadgeTone> = { Normal: 'success', Warning: 'warning', Alarm: 'danger' }
const RANGES = [{ days: 7, label: '7 days' }, { days: 30, label: '30 days' }, { days: 90, label: '90 days' }] as const

function limitsText(min: number | null, max: number | null, unit: string) {
  if (min === null && max === null) return '—'
  if (min === null) return `≤ ${formatNumber(max, unit)}`
  if (max === null) return `≥ ${formatNumber(min, unit)}`
  return `${formatNumber(min)} to ${formatNumber(max, unit)}`
}

/** Condition monitoring: every meter with limits, worst first, with its excursions and trend. */
export function ConditionPage() {
  const [params, setParams] = useSearchParams()
  const [orgNodeId, setOrgNodeId] = useState('')
  const tab = params.get('tab') === 'events' ? 'events' : 'now'
  const meterId = params.get('meter')
  const overview = useConditionOverview(orgNodeId)
  const setParam = (key: string, value: string | null) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Condition monitoring"
        subtitle="Gauges with limits: what is drifting, what is in alarm, and where each trend is heading."
        secondaryActions={<div className="w-64"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>}
      />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <StatCard label="Monitored meters" value={overview.data?.monitored ?? '—'} icon={<Gauge className="h-4 w-4 text-slate-400" />} />
        <StatCard label="In alarm" value={overview.data?.alarms ?? '—'} tone={overview.data?.alarms ? 'danger' : 'default'} icon={<AlertTriangle className="h-4 w-4 text-rose-500" />} />
        <StatCard label="Warning" value={overview.data?.warnings ?? '—'} tone={overview.data?.warnings ? 'warning' : 'default'} icon={<Activity className="h-4 w-4 text-amber-500" />} />
        <StatCard label="No reading for a week" value={overview.data?.quiet ?? '—'} icon={<BellOff className="h-4 w-4 text-slate-400" />} />
      </div>

      <div className="flex gap-2 border-b border-slate-200" role="tablist">
        {([['now', 'Now'], ['events', 'Excursions']] as const).map(([id, label]) => (
          <button key={id} type="button" role="tab" aria-selected={tab === id}
            className={cn('-mb-px border-b-2 px-3 py-2 text-sm font-medium', tab === id ? 'border-[var(--app-primary)] text-slate-900' : 'border-transparent text-slate-500 hover:text-slate-700')}
            onClick={() => setParam('tab', id === 'now' ? null : id)}>{label}</button>
        ))}
      </div>

      {tab === 'now' ? (
        overview.isLoading ? <Spinner label="Loading meters" /> : (overview.data?.meters ?? []).length === 0 ? (
          <EmptyState title="No meters with limits yet" description="Give a meter alarm limits (and a warning band) on its asset's Meters tab, and it appears here." />
        ) : (
          <MeterTable rows={overview.data!.meters} onOpen={(id) => setParam('meter', id)} />
        )
      ) : <EventsTab orgNodeId={orgNodeId} onOpen={(id) => setParam('meter', id)} />}

      {meterId ? <MeterConditionDrawer meterId={meterId} onClose={() => setParam('meter', null)} /> : null}
    </div>
  )
}

function MeterTable({ rows, onOpen }: { rows: ConditionMeterRow[]; onOpen: (meterId: string) => void }) {
  const links = useFactoryLinks()
  return (
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
      <table className="w-full min-w-[860px] text-sm">
        <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
          <tr>
            <th className="px-3 py-2">Condition</th><th className="px-3 py-2">Asset</th><th className="px-3 py-2">Meter</th>
            <th className="px-3 py-2 text-right">Now</th><th className="px-3 py-2">Warning band</th><th className="px-3 py-2">Alarm limits</th>
            <th className="px-3 py-2">Last reading</th><th className="px-3 py-2">Job</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((row) => (
            <tr key={row.meterId} className="cursor-pointer hover:bg-slate-50" onClick={() => onOpen(row.meterId)}>
              <td className="px-3 py-2">
                <Badge tone={LEVEL_TONES[row.conditionState]} size="sm">{row.conditionState}</Badge>
                {row.openEventId && row.acknowledgedAt ? <span className="ml-1 text-xs text-slate-500">seen</span> : null}
              </td>
              <td className="px-3 py-2">
                <Link to={links.asset(row.assetId, 'meters')} className="font-medium text-slate-900 hover:underline" onClick={(event) => event.stopPropagation()}>{row.assetName}</Link>
                <div className="text-xs text-slate-500">{row.assetCode}{row.location ? ` · ${row.location}` : ''}</div>
              </td>
              <td className="px-3 py-2 text-slate-700">{row.meterName}</td>
              <td className="px-3 py-2 text-right font-medium tabular-nums text-slate-900">{formatNumber(row.currentValue, row.unit)}</td>
              <td className="px-3 py-2 text-slate-600">{limitsText(row.warningMin, row.warningMax, row.unit)}</td>
              <td className="px-3 py-2 text-slate-600">{limitsText(row.alarmMin, row.alarmMax, row.unit)}</td>
              <td className={cn('px-3 py-2', row.isQuiet ? 'text-amber-700' : 'text-slate-600')}>{row.lastReadingAt ? formatDateTime(row.lastReadingAt) : 'Never'}</td>
              <td className="px-3 py-2">
                {row.workOrderId ? <Link to={links.workOrder(row.workOrderId)} className="text-[var(--app-primary)] hover:underline" onClick={(event) => event.stopPropagation()}>{row.workOrderNumber}</Link>
                  : row.conditionAction === 'WorkOrder' ? <span className="text-xs text-slate-500">on alarm</span> : <span className="text-xs text-slate-400">alert only</span>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function EventsTab({ orgNodeId, onOpen }: { orgNodeId: string; onOpen: (meterId: string) => void }) {
  const [status, setStatus] = useState('open')
  const events = useConditionEvents(status, orgNodeId)
  return (
    <div className="space-y-3">
      <div className="flex gap-2">
        {([['open', 'Open'], ['cleared', 'Cleared'], ['', 'All']] as const).map(([value, label]) => (
          <button key={label} type="button" className={cn(secondaryButton, status === value && 'border-[var(--app-primary)]')} onClick={() => setStatus(value)}>{label}</button>
        ))}
      </div>
      {events.isLoading ? <Spinner label="Loading excursions" /> : (events.data ?? []).length === 0 ? (
        <EmptyState title={status === 'open' ? 'Nothing out of range' : 'No excursions'} description="An excursion starts when readings leave a meter's band and ends when they come back." />
      ) : <EventList events={events.data!} onOpen={onOpen} />}
    </div>
  )
}

function EventList({ events, onOpen }: { events: ConditionEvent[]; onOpen?: (meterId: string) => void }) {
  const links = useFactoryLinks()
  const canAcknowledge = useAnyPermission('assets.manage', 'work-orders.complete')
  const [acknowledging, setAcknowledging] = useState<ConditionEvent | null>(null)
  return (
    <>
      <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 bg-white">
        {events.map((event) => (
          <li key={event.id} className="flex flex-wrap items-start gap-3 px-4 py-3">
            <Badge tone={LEVEL_TONES[event.level]} size="sm">{event.level}</Badge>
            <div className="min-w-0 flex-1">
              <button type="button" className="text-left font-medium text-slate-900 hover:underline disabled:no-underline" disabled={!onOpen} onClick={() => onOpen?.(event.meterId)}>
                {event.assetName} · {event.meterName}
              </button>
              <div className="text-xs text-slate-500">
                {event.direction === 'High' ? 'Peaked at' : 'Fell to'} {formatNumber(event.peakValue, event.unit)} over {event.readingCount} reading{event.readingCount === 1 ? '' : 's'} ·
                {' '}from {formatDateTime(event.startedAt)}{event.clearedAt ? ` to ${formatDateTime(event.clearedAt)}` : ', still out of range'}
              </div>
              {event.acknowledgedAt ? (
                <div className="mt-1 flex items-center gap-1 text-xs text-slate-600"><CheckCircle2 className="h-3 w-3 text-emerald-600" />Seen by {event.acknowledgedByName}{event.acknowledgementNote ? `: ${event.acknowledgementNote}` : ''}</div>
              ) : null}
            </div>
            {event.workOrderId ? <Link to={links.workOrder(event.workOrderId)} className="text-sm text-[var(--app-primary)] hover:underline">{event.workOrderNumber} · {event.workOrderStatus}</Link> : null}
            {event.status === 'Open' && !event.acknowledgedAt && canAcknowledge ? (
              <button type="button" className={secondaryButton} onClick={() => setAcknowledging(event)}>Acknowledge</button>
            ) : null}
          </li>
        ))}
      </ul>
      {acknowledging ? <AcknowledgeDrawer event={acknowledging} onClose={() => setAcknowledging(null)} /> : null}
    </>
  )
}

function AcknowledgeDrawer({ event, onClose }: { event: ConditionEvent; onClose: () => void }) {
  const acknowledge = useAcknowledgeEvent()
  const [note, setNote] = useState('')
  const submit = async () => {
    try {
      await acknowledge.mutateAsync({ id: event.id, note })
      toast.success('Acknowledged')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open title={`Acknowledge: ${event.assetName} ${event.meterName}`} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={acknowledge.isPending} onClick={() => void submit()}>Acknowledge</button>
      </div>
    }>
      <p className="mb-3 text-sm text-slate-600">Says the excursion has been seen and is being dealt with. It stays open until readings are back in range.</p>
      <Field label="Note"><textarea className={textareaClass} rows={3} value={note} onChange={(e) => setNote(e.target.value)} placeholder="e.g. Fitter checking the bearing on the next stop" /></Field>
    </FormDrawer>
  )
}

function MeterConditionDrawer({ meterId, onClose }: { meterId: string; onClose: () => void }) {
  const [days, setDays] = useState(30)
  const { data, isLoading } = useMeterCondition(meterId, days)
  const links = useFactoryLinks()
  return (
    <FormDrawer open title={data ? `${data.assetName} · ${data.meterName}` : 'Meter'} onClose={onClose}>
      {isLoading || !data ? <Spinner label="Loading meter" /> : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone={LEVEL_TONES[data.conditionState]}>{data.conditionState}</Badge>
            <span className="font-semibold text-slate-900">{formatNumber(data.currentValue, data.unit)}</span>
            {data.conditionSince ? <span className="text-slate-500">since {formatDateTime(data.conditionSince)}</span> : null}
            <Link to={links.asset(data.assetId, 'meters')} className="ml-auto text-[var(--app-primary)] hover:underline">Open asset</Link>
          </div>
          <div className="flex gap-2">
            {RANGES.map((range) => (
              <button key={range.days} type="button" className={cn(secondaryButton, days === range.days && 'border-[var(--app-primary)]')} onClick={() => setDays(range.days)}>{range.label}</button>
            ))}
          </div>
          <ReadingsChart readings={data.readings} unit={data.unit} min={data.alarmMin} max={data.alarmMax} warnMin={data.warningMin} warnMax={data.warningMax} />
          {!data.isCumulative ? (
            <p className={cn('rounded-lg px-3 py-2 text-sm', data.trend.daysToLimit !== null && data.trend.daysToLimit <= 7 ? 'bg-amber-50 text-amber-800' : 'bg-slate-50 text-slate-700')}>
              {describeTrend(data.trend, data.unit)}
            </p>
          ) : null}
          <div>
            <h3 className="mb-2 text-sm font-semibold text-slate-900">Excursions</h3>
            {data.events.length === 0 ? <p className="text-sm text-slate-500">None in this period.</p> : <EventList events={data.events} />}
          </div>
        </div>
      )}
    </FormDrawer>
  )
}
