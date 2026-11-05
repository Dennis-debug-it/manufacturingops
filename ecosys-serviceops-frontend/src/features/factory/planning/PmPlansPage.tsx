import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, Gauge, Plus, Route as RouteIcon, Search } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useMeters } from '../api'
import { CriticalityBadge, Field, LocationSelect, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { PRIORITIES } from '../jobs/types'
import { AssetListPicker, AssetPicker, type PickedAsset } from './AssetPicker'
import { useChecklistTemplates, usePmAction, usePmCompliance, usePmForecast, usePmPlan, usePmPlans, useSavePmPlan, type PmPlanDetail, type PmPlanSummary, type SavePmPlan } from './api'

const TABS = [{ id: 'plans', label: 'Plans' }, { id: 'forecast', label: 'Workload' }, { id: 'compliance', label: 'Compliance' }] as const
type Tab = (typeof TABS)[number]['id']

/** Planned maintenance: PM plans (by date or by meter), the weekly workload and PM compliance. */
export function PmPlansPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'plans') as Tab
  const canPlan = usePermission('work-orders.assign')
  const [orgNodeId, setOrgNodeId] = useState('')
  const [editing, setEditing] = useState<PmPlanDetail | 'new' | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)

  return (
    <div className="space-y-4">
      <PageHeader title="Planned maintenance" subtitle="PM plans by date or by run hours and production, the weekly workload, and PM done on time."
        primaryAction={canPlan ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />New PM plan</button> : undefined} />
      <div className="flex flex-wrap items-center gap-2">
        <div role="tablist" className="inline-flex gap-1 rounded-lg border border-slate-200 bg-white p-1">
          {TABS.map((item) => (
            <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'plans' ? {} : { tab: item.id }, { replace: true })}
              className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
          ))}
        </div>
        <div className="w-full sm:w-64"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All locations" includeGroups /></div>
      </div>

      {tab === 'plans' ? <PlansTab orgNodeId={orgNodeId} onOpen={setOpenId} /> : tab === 'forecast' ? <ForecastTab orgNodeId={orgNodeId} /> : <ComplianceTab orgNodeId={orgNodeId} onOpen={setOpenId} />}

      <PlanDetailDrawer id={openId} canPlan={canPlan} onClose={() => setOpenId(null)} onEdit={(plan) => { setOpenId(null); setEditing(plan) }} />
      <PlanFormDrawer plan={editing} onClose={() => setEditing(null)} onSaved={(id) => { setEditing(null); setOpenId(id) }} />
    </div>
  )
}

function dueText(plan: PmPlanSummary) {
  if (plan.triggerType === 'Meter') {
    return plan.nextMeterDue == null ? '—' : `at ${formatNumber(plan.nextMeterDue)} ${plan.meterUnit ?? ''}`
  }
  return plan.nextDueDate ? formatDate(plan.nextDueDate) : '—'
}

function isLate(plan: PmPlanSummary) {
  if (plan.triggerType === 'Meter') return plan.nextMeterDue != null && plan.meterValue != null && plan.meterValue >= plan.nextMeterDue
  return plan.nextDueDate != null && new Date(plan.nextDueDate) < new Date(new Date().toDateString())
}

function PlansTab({ orgNodeId, onOpen }: { orgNodeId: string; onOpen: (id: string) => void }) {
  const links = useFactoryLinks()
  const [search, setSearch] = useState('')
  const [inactive, setInactive] = useState(false)
  const debounced = useDebounce(search, 300)
  const plans = usePmPlans({ orgNodeId: orgNodeId || undefined, search: debounced || undefined, includeInactive: inactive || undefined })

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex h-10 flex-1 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3 sm:max-w-sm">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search plan or asset" className="w-full border-0 bg-transparent text-sm outline-none" />
        </label>
        <label className="inline-flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={inactive} onChange={(event) => setInactive(event.target.checked)} />Show paused</label>
      </div>
      {plans.isLoading ? <Spinner /> : (plans.data ?? []).length === 0 ? (
        <EmptyState icon={Gauge} title="No PM plans yet" description="Add a plan, or set up the weekly window routines from the Weekly window page." />
      ) : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
              <tr><th className="px-4 py-2">Plan</th><th className="px-4 py-2">Asset</th><th className="px-4 py-2">How often</th><th className="px-4 py-2">Next due</th><th className="px-4 py-2">Job</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {(plans.data ?? []).map((plan) => (
                <tr key={plan.id} className="cursor-pointer hover:bg-slate-50" onClick={() => onOpen(plan.id)}>
                  <td className="px-4 py-2">
                    <span className="font-medium text-slate-900">{plan.name}</span>
                    <span className="mt-0.5 flex flex-wrap gap-1">
                      {plan.needsLineStop ? <Badge size="sm">Line stop</Badge> : null}
                      {plan.isRoute ? <Badge size="sm"><RouteIcon className="h-3 w-3" />{plan.routeAssetCount} assets</Badge> : null}
                      {plan.deferRisk ? <Badge size="sm" tone="danger"><AlertTriangle className="h-3 w-3" />Deferred, A-class</Badge> : plan.deferReason ? <Badge size="sm" tone="warning">Deferred</Badge> : null}
                      {plan.status !== 'Active' ? <Badge size="sm">Paused</Badge> : null}
                    </span>
                  </td>
                  <td className="px-4 py-2"><span className="font-mono text-xs">{plan.assetCode}</span> <CriticalityBadge value={plan.assetCriticality as 'A' | 'B' | 'C' | null} /><span className="block text-xs text-slate-500">{plan.assetName}</span></td>
                  <td className="px-4 py-2 text-slate-600">{plan.frequency}<span className="block text-xs text-slate-400">{plan.scheduleMode === 'Fixed' ? 'Fixed' : 'Floating'}</span></td>
                  <td className={cn('whitespace-nowrap px-4 py-2', isLate(plan) && 'font-medium text-rose-700')}>
                    {dueText(plan)}
                    {plan.triggerType === 'Meter' && plan.meterValue != null ? <span className="block text-xs font-normal text-slate-500">now {formatNumber(plan.meterValue)}</span> : null}
                  </td>
                  <td className="px-4 py-2">{plan.openJobId ? <Link to={links.job(plan.openJobId)} onClick={(event) => event.stopPropagation()} className="font-mono text-xs underline">{plan.openJobNumber}</Link> : <span className="text-slate-400">—</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

/** PM hours per week against technician capacity: where the load needs levelling. */
function ForecastTab({ orgNodeId }: { orgNodeId: string }) {
  const [weeks, setWeeks] = useState(12)
  const forecast = usePmForecast(orgNodeId || undefined, weeks)
  const [open, setOpen] = useState<string | null>(null)
  const rows = forecast.data ?? []
  const max = Math.max(1, ...rows.map((x) => Math.max(x.hours, x.capacityHours)))

  if (forecast.isLoading) return <Spinner />
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-sm font-semibold text-slate-800">PM hours per week</h2>
          <p className="text-xs text-slate-500">Capacity is active technicians × planned hours a week. Meter-based plans are estimated from the last 30 days' use.</p>
        </div>
        <select aria-label="Weeks" className={cn(inputClass, 'w-32')} value={weeks} onChange={(event) => setWeeks(Number(event.target.value))}>
          {[8, 12, 26].map((x) => <option key={x} value={x}>{x} weeks</option>)}
        </select>
      </div>
      <ul className="mb-3 flex flex-wrap gap-4 text-xs text-slate-600">
        <li className="flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm bg-[var(--app-primary)]" />Running-line work</li>
        <li className="flex items-center gap-1.5"><span className="h-2.5 w-2.5 rounded-sm bg-amber-500" />Needs the line stopped</li>
        <li className="flex items-center gap-1.5"><span className="h-0.5 w-4 bg-slate-500" />Capacity</li>
      </ul>
      <ol className="space-y-1.5">
        {rows.map((week) => {
          const over = week.capacityHours > 0 && week.hours > week.capacityHours
          return (
            <li key={week.weekStart}>
              <button type="button" className="grid w-full grid-cols-[5.5rem_1fr_5.5rem] items-center gap-3 rounded-md px-1 py-1 text-left text-xs hover:bg-slate-50" onClick={() => setOpen(open === week.weekStart ? null : week.weekStart)} aria-expanded={open === week.weekStart}>
                <span className="text-slate-600">{formatDate(week.weekStart, 'dd MMM')}</span>
                <span className="relative h-4 rounded bg-slate-100" title={`${week.hours} h of ${week.capacityHours} h`}>
                  <span className="absolute inset-y-0 left-0 flex gap-0.5 overflow-hidden rounded" style={{ width: `${(week.hours / max) * 100}%` }}>
                    {week.hours - week.lineStopHours > 0 ? <span className="h-full rounded-l bg-[var(--app-primary)]" style={{ width: `${((week.hours - week.lineStopHours) / week.hours) * 100}%` }} /> : null}
                    {week.lineStopHours > 0 ? <span className="h-full flex-1 rounded-r bg-amber-500" /> : null}
                  </span>
                  {week.capacityHours > 0 ? <span className="absolute -inset-y-1 w-0.5 bg-slate-500" style={{ left: `${(week.capacityHours / max) * 100}%` }} /> : null}
                </span>
                <span className={cn('text-right tabular-nums', over ? 'font-semibold text-rose-700' : 'text-slate-700')}>{formatNumber(week.hours)} h{over ? ' · over' : ''}</span>
              </button>
              {open === week.weekStart ? (
                <ul className="mb-2 ml-[6.25rem] space-y-0.5 text-xs text-slate-600">
                  {week.items.length === 0 ? <li>Nothing due.</li> : week.items.map((item, i) => (
                    <li key={`${item.planId}-${i}`}>{formatDate(item.due, 'EEE dd')} · {item.name}{item.assetCode ? ` (${item.assetCode})` : ''} · {formatNumber(item.hours)} h{item.estimated ? ' · estimated' : ''}{item.needsLineStop ? ' · line stop' : ''}</li>
                  ))}
                </ul>
              ) : null}
            </li>
          )
        })}
      </ol>
    </section>
  )
}

function ComplianceTab({ orgNodeId, onOpen }: { orgNodeId: string; onOpen: (id: string) => void }) {
  const [days, setDays] = useState(90)
  const compliance = usePmCompliance(orgNodeId || undefined, days)
  if (compliance.isLoading || !compliance.data) return <Spinner />
  const data = compliance.data
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-4 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
        <div>
          <p className="text-sm text-slate-600">PM done on time</p>
          <p className="text-3xl font-semibold tabular-nums text-slate-900">{data.percent == null ? '—' : `${data.percent}%`}</p>
        </div>
        <dl className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
          <div><dt className="text-slate-500">Due</dt><dd className="font-medium tabular-nums">{data.due}</dd></div>
          <div><dt className="text-slate-500">On time</dt><dd className="font-medium tabular-nums">{data.onTime}</dd></div>
          <div><dt className="text-slate-500">Late</dt><dd className="font-medium tabular-nums">{data.late}</dd></div>
          <div><dt className="text-slate-500">Skipped</dt><dd className="font-medium tabular-nums">{data.skipped}</dd></div>
          <div><dt className="text-slate-500">Overdue</dt><dd className={cn('font-medium tabular-nums', data.overdue > 0 && 'text-rose-700')}>{data.overdue}</dd></div>
        </dl>
        <select aria-label="Period" className={cn(inputClass, 'ml-auto w-36')} value={days} onChange={(event) => setDays(Number(event.target.value))}>
          {[30, 90, 180, 365].map((x) => <option key={x} value={x}>Last {x} days</option>)}
        </select>
      </div>
      {data.plans.length === 0 ? <EmptyState title="No PM due in this period" description="Compliance counts PM that fell due: done on time, late, skipped or still overdue." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Plan</th><th className="px-4 py-2 text-right">Due</th><th className="px-4 py-2 text-right">On time</th><th className="px-4 py-2 text-right">Late</th><th className="px-4 py-2 text-right">Skipped</th><th className="px-4 py-2 text-right">Overdue</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {data.plans.map((row) => (
                <tr key={row.planId} className="cursor-pointer hover:bg-slate-50" onClick={() => onOpen(row.planId)}>
                  <td className="px-4 py-2">{row.name}{row.assetCode ? <span className="ml-1 font-mono text-xs text-slate-500">{row.assetCode}</span> : null}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.due}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.onTime}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.late}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{row.skipped}</td>
                  <td className={cn('px-4 py-2 text-right tabular-nums', row.overdue > 0 && 'font-medium text-rose-700')}>{row.overdue}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

const EVENT_LABELS: Record<string, string> = { Generated: 'Job raised', Completed: 'Done', Skipped: 'Skipped', Deferred: 'Deferred' }

function PlanDetailDrawer({ id, canPlan, onClose, onEdit }: { id: string | null; canPlan: boolean; onClose: () => void; onEdit: (plan: PmPlanDetail) => void }) {
  const links = useFactoryLinks()
  const { data } = usePmPlan(id)
  const action = usePmAction(id ?? '')
  const [mode, setMode] = useState<'skip' | 'defer' | null>(null)
  const [reason, setReason] = useState('')
  const [date, setDate] = useState('')
  const [meter, setMeter] = useState('')
  const [lowCrop, setLowCrop] = useState(false)
  useEffect(() => { setMode(null); setReason(''); setDate(''); setMeter(''); setLowCrop(false) }, [id])

  const run = async (name: 'skip' | 'defer' | 'active' | 'generate', body?: unknown) => {
    try {
      const result = await action.mutateAsync({ action: name, body })
      if (name === 'generate' && result && 'workOrderNumber' in result) toast.success(`${result.workOrderNumber} raised`)
      else toast.success('Saved')
      setMode(null)
      setReason('')
    } catch {
      // The API client shows the error.
    }
  }

  const plan = data?.plan
  return (
    <FormDrawer open={Boolean(id)} title={plan?.name ?? 'PM plan'} onClose={onClose} footer={plan && canPlan ? (
      <div className="flex flex-wrap justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={() => void run('active', { active: plan.status !== 'Active' })}>{plan.status === 'Active' ? 'Pause' : 'Resume'}</button>
        {!plan.openJobId ? <button type="button" className={secondaryButton} onClick={() => setMode('skip')}>Skip this one</button> : null}
        <button type="button" className={secondaryButton} onClick={() => setMode('defer')}>Defer</button>
        {!plan.openJobId ? <button type="button" className={secondaryButton} onClick={() => void run('generate')}>Raise job now</button> : null}
        <button type="button" className={primaryButton} onClick={() => data && onEdit(data)}>Edit</button>
      </div>
    ) : undefined}>
      {!data || !plan ? <Spinner /> : (
        <div className="space-y-4 text-sm">
          <dl className="grid grid-cols-[minmax(0,9rem)_1fr] gap-x-4 gap-y-2">
            <dt className="text-slate-500">Asset</dt><dd><Link className="underline" to={links.asset(plan.assetId)}>{plan.assetCode} · {plan.assetName}</Link>{data.routeAssets.length > 0 ? <span className="block text-xs text-slate-500">Route: {data.routeAssets.map((x) => x.assetCode).join(' → ')}</span> : null}</dd>
            <dt className="text-slate-500">How often</dt><dd>{plan.frequency} · {plan.scheduleMode === 'Fixed' ? 'fixed (keeps its dates)' : 'floating (counts from when it was done)'}</dd>
            <dt className="text-slate-500">Next due</dt><dd className={cn(isLate(plan) && 'font-medium text-rose-700')}>{dueText(plan)}{plan.triggerType === 'Meter' && plan.meterValue != null ? ` (now ${formatNumber(plan.meterValue)})` : ''}</dd>
            <dt className="text-slate-500">On time if</dt><dd>within ±{data.tolerancePercent}% of the interval{plan.triggerType === 'Calendar' ? `; job raised ${data.daysBeforeDue} days ahead` : ''}</dd>
            <dt className="text-slate-500">Checklist</dt><dd>{data.pmTemplateName ?? '—'}</dd>
            <dt className="text-slate-500">Job</dt><dd>{plan.priority} priority{plan.estimatedHours ? ` · ${plan.estimatedHours} h` : ''}{plan.needsLineStop ? ' · weekly window' : ''}</dd>
            {plan.basis ? <><dt className="text-slate-500">Basis</dt><dd>{plan.basis}</dd></> : null}
            <dt className="text-slate-500">Last done</dt><dd>{plan.lastDoneAt ? formatDate(plan.lastDoneAt) : '—'}</dd>
            {plan.openJobId ? <><dt className="text-slate-500">Open job</dt><dd><Link className="underline" to={links.job(plan.openJobId)}>{plan.openJobNumber}</Link></dd></> : null}
          </dl>
          {plan.deferReason ? (
            <p className={cn('rounded-lg p-3', plan.deferRisk ? 'bg-rose-50 text-rose-900' : 'bg-amber-50 text-amber-900')}>
              {plan.deferRisk ? <AlertTriangle className="mr-1 inline h-4 w-4" /> : null}
              Deferred{plan.deferredAt ? ` on ${formatDate(plan.deferredAt)}` : ''}: {plan.deferReason}{plan.deferRisk ? ' — an A-class asset put off to low crop.' : ''}
            </p>
          ) : null}

          {mode ? (
            <div className="space-y-3 rounded-lg border border-slate-200 p-3">
              <p className="font-medium text-slate-800">{mode === 'skip' ? 'Skip this PM' : 'Defer this PM'}</p>
              {mode === 'defer' ? (
                plan.triggerType === 'Meter' ? (
                  <Field label={`New due reading (${plan.meterUnit ?? ''})`}><input type="number" className={inputClass} value={meter} onChange={(event) => setMeter(event.target.value)} /></Field>
                ) : (
                  <>
                    <label className="flex items-center gap-2"><input type="checkbox" checked={lowCrop} onChange={(event) => setLowCrop(event.target.checked)} />Until the low-crop season</label>
                    {!lowCrop ? <Field label="New due date"><input type="date" className={inputClass} value={date} onChange={(event) => setDate(event.target.value)} /></Field> : null}
                  </>
                )
              ) : <p className="text-slate-600">The next one is planned as usual. A skip counts against PM compliance.</p>}
              <Field label="Reason" required><textarea className={textareaClass} value={reason} onChange={(event) => setReason(event.target.value)} /></Field>
              <div className="flex justify-end gap-2">
                <button type="button" className={secondaryButton} onClick={() => setMode(null)}>Back</button>
                <button type="button" className={primaryButton} disabled={!reason.trim() || action.isPending}
                  onClick={() => void run(mode, mode === 'skip' ? { reason } : { reason, newDueDate: date || null, newMeterDue: meter ? Number(meter) : null, untilLowCrop: lowCrop })}>
                  {mode === 'skip' ? 'Skip' : 'Defer'}
                </button>
              </div>
            </div>
          ) : null}

          <section>
            <h3 className="mb-2 font-semibold text-slate-800">History</h3>
            {data.events.length === 0 ? <p className="text-slate-500">Nothing yet.</p> : (
              <ol className="space-y-2">
                {data.events.map((event, index) => (
                  <li key={index} className="border-l-2 border-slate-200 pl-3">
                    <span className="font-medium">{EVENT_LABELS[event.kind] ?? event.kind}</span>
                    {event.kind === 'Completed' ? <Badge size="sm" tone={event.onTime ? 'success' : 'warning'} className="ml-2">{event.onTime ? 'On time' : 'Late'}</Badge> : null}
                    {event.workOrderId ? <Link className="ml-2 font-mono text-xs underline" to={links.job(event.workOrderId)}>{event.workOrderNumber}</Link> : null}
                    <span className="block text-xs text-slate-500">
                      {formatDateTime(event.occurredAt)}{event.actorName ? ` · ${event.actorName}` : ''}
                      {event.dueDate ? ` · due ${formatDate(event.dueDate)}` : event.dueMeterValue != null ? ` · due at ${formatNumber(event.dueMeterValue)}` : ''}
                    </span>
                    {event.reason ? <span className="block text-xs text-slate-600">{event.reason}</span> : null}
                  </li>
                ))}
              </ol>
            )}
          </section>
        </div>
      )}
    </FormDrawer>
  )
}

const EMPTY: SavePmPlan = {
  name: '', assetId: '', pmTemplateId: null, triggerType: 'Calendar', frequencyUnit: 'Months', frequencyInterval: 1, nextDueDate: null,
  meterId: null, meterInterval: null, nextMeterDue: null, scheduleMode: 'Floating', tolerancePercent: 10, daysBeforeDue: 3, priority: 'Medium',
  estimatedHours: null, needsLineStop: false, basis: null, routeAssetIds: null, autoSchedule: true,
}

function PlanFormDrawer({ plan, onClose, onSaved }: { plan: PmPlanDetail | 'new' | null; onClose: () => void; onSaved: (id: string) => void }) {
  const save = useSavePmPlan()
  const templates = useChecklistTemplates()
  const existing = plan && plan !== 'new' ? plan : null
  const [form, setForm] = useState<SavePmPlan>(EMPTY)
  const [asset, setAsset] = useState<PickedAsset | null>(null)
  const [route, setRoute] = useState<PickedAsset[]>([])
  const [isRoute, setIsRoute] = useState(false)
  const meters = useMeters(asset?.id ?? '')

  useEffect(() => {
    if (!plan) return
    if (existing) {
      const p = existing.plan
      setForm({
        name: p.name, assetId: p.assetId, pmTemplateId: existing.pmTemplateId, triggerType: p.triggerType, frequencyUnit: existing.frequencyUnit, frequencyInterval: existing.frequencyInterval,
        nextDueDate: p.nextDueDate?.slice(0, 10) ?? null, meterId: null, meterInterval: existing.meterInterval, nextMeterDue: p.nextMeterDue, scheduleMode: p.scheduleMode,
        tolerancePercent: existing.tolerancePercent, daysBeforeDue: existing.daysBeforeDue, priority: p.priority, estimatedHours: p.estimatedHours, needsLineStop: p.needsLineStop,
        basis: p.basis, routeAssetIds: null, autoSchedule: existing.autoSchedule,
      })
      setAsset({ id: p.assetId, code: p.assetCode, name: p.assetName, categoryId: null })
      setRoute(existing.routeAssets.map((x) => ({ id: x.assetId, code: x.assetCode, name: x.assetName, categoryId: null })))
      setIsRoute(existing.routeAssets.length > 0)
    } else {
      setForm(EMPTY)
      setAsset(null)
      setRoute([])
      setIsRoute(false)
    }
  }, [plan, existing])

  // The meter is matched by the plan's current meter unit once the asset's meters load.
  const meterOptions = useMemo(() => (meters.data ?? []).filter((m) => m.isActive && m.isCumulative), [meters.data])
  useEffect(() => {
    if (form.triggerType === 'Meter' && !form.meterId && meterOptions.length > 0) {
      const match = existing ? meterOptions.find((m) => m.unit === existing.plan.meterUnit) : meterOptions.find((m) => m.isPrimary)
      setForm((current) => ({ ...current, meterId: (match ?? meterOptions[0])!.id }))
    }
  }, [form.triggerType, form.meterId, meterOptions, existing])

  const set = <K extends keyof SavePmPlan>(key: K, value: SavePmPlan[K]) => setForm((current) => ({ ...current, [key]: value }))
  const primaryAsset = isRoute ? route[0] ?? null : asset
  const ready = form.name.trim() && primaryAsset && (form.triggerType === 'Meter' ? form.meterId && form.meterInterval : form.nextDueDate)

  const submit = async () => {
    if (!primaryAsset) return
    try {
      const result = await save.mutateAsync({ ...form, id: existing?.plan.id, assetId: primaryAsset.id, routeAssetIds: isRoute ? route.map((x) => x.id) : null })
      toast.success('PM plan saved')
      onSaved(result.plan.id)
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={plan !== null} title={existing ? `Edit · ${existing.plan.name}` : 'New PM plan'} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!ready || save.isPending} onClick={() => void submit()}>Save plan</button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2"><input className={inputClass} value={form.name} onChange={(event) => set('name', event.target.value)} placeholder="Dryer fan bearings – grease" /></Field>
        <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={isRoute} onChange={(event) => setIsRoute(event.target.checked)} />A round over several assets (one job, checklist per asset)</label>
        <Field label={isRoute ? 'Assets, in order' : 'Asset'} required className="sm:col-span-2">
          {isRoute ? <AssetListPicker value={route} onChange={setRoute} /> : <AssetPicker value={asset} onChange={(next) => { setAsset(next); set('meterId', null) }} />}
        </Field>
        <Field label="Checklist" className="sm:col-span-2" hint="The steps copied onto each job. Build them in Settings › PM templates.">
          <select className={inputClass} value={form.pmTemplateId ?? ''} onChange={(event) => set('pmTemplateId', event.target.value || null)}>
            <option value="">None</option>
            {(templates.data ?? []).filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field label="Due by">
          <select className={inputClass} value={form.triggerType} onChange={(event) => set('triggerType', event.target.value as SavePmPlan['triggerType'])}>
            <option value="Calendar">Date</option>
            <option value="Meter">Meter (run hours, kg made tea…)</option>
          </select>
        </Field>
        <Field label="Schedule" hint={form.scheduleMode === 'Fixed' ? 'Keeps its dates, however late the last one was.' : 'Counts from when it was last done.'}>
          <select className={inputClass} value={form.scheduleMode} onChange={(event) => set('scheduleMode', event.target.value)}>
            <option value="Floating">Floating</option>
            <option value="Fixed">Fixed</option>
          </select>
        </Field>
        {form.triggerType === 'Calendar' ? (
          <>
            <Field label="Every">
              <div className="flex gap-2">
                <input type="number" min={1} className={cn(inputClass, 'w-20')} value={form.frequencyInterval} onChange={(event) => set('frequencyInterval', Number(event.target.value) || 1)} />
                <select className={inputClass} value={form.frequencyUnit} onChange={(event) => set('frequencyUnit', event.target.value)}>
                  {['Days', 'Weeks', 'Months', 'Years'].map((x) => <option key={x} value={x}>{x.toLowerCase()}</option>)}
                </select>
              </div>
            </Field>
            <Field label="Next due" required><input type="date" className={inputClass} value={form.nextDueDate ?? ''} onChange={(event) => set('nextDueDate', event.target.value || null)} /></Field>
            <Field label="Raise the job" hint="Days before it is due."><input type="number" min={0} max={60} className={inputClass} value={form.daysBeforeDue} onChange={(event) => set('daysBeforeDue', Number(event.target.value))} /></Field>
          </>
        ) : (
          <>
            <Field label="Meter" required>
              <select className={inputClass} value={form.meterId ?? ''} onChange={(event) => set('meterId', event.target.value || null)} disabled={!primaryAsset}>
                {meterOptions.length === 0 ? <option value="">No counting meters on this asset</option> : null}
                {meterOptions.map((m) => <option key={m.id} value={m.id}>{m.name} ({m.unit}) · now {formatNumber(m.currentValue)}</option>)}
              </select>
            </Field>
            <Field label="Every" required><input type="number" min={1} className={inputClass} value={form.meterInterval ?? ''} onChange={(event) => set('meterInterval', event.target.value ? Number(event.target.value) : null)} placeholder="500" /></Field>
            <Field label="Next due at" hint="Blank: the current reading plus the interval."><input type="number" className={inputClass} value={form.nextMeterDue ?? ''} onChange={(event) => set('nextMeterDue', event.target.value ? Number(event.target.value) : null)} /></Field>
          </>
        )}
        <Field label="On time within" hint="± % of the interval around the due date.">
          <input type="number" min={0} max={50} className={inputClass} value={form.tolerancePercent} onChange={(event) => set('tolerancePercent', Number(event.target.value))} />
        </Field>
        <Field label="Priority">
          <select className={inputClass} value={form.priority} onChange={(event) => set('priority', event.target.value)}>{PRIORITIES.map((x) => <option key={x}>{x}</option>)}</select>
        </Field>
        <Field label="Estimated hours"><input type="number" min={0} step={0.5} className={inputClass} value={form.estimatedHours ?? ''} onChange={(event) => set('estimatedHours', event.target.value ? Number(event.target.value) : null)} /></Field>
        <Field label="Basis" hint="Why this interval.">
          <select className={inputClass} value={form.basis ?? ''} onChange={(event) => set('basis', event.target.value || null)}>
            <option value="">—</option>
            {['Manufacturer', 'Statutory', 'Experience', 'Condition', 'Weekly window routine'].map((x) => <option key={x}>{x}</option>)}
          </select>
        </Field>
        <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={form.needsLineStop} onChange={(event) => set('needsLineStop', event.target.checked)} />Needs the line stopped: goes into the weekly window</label>
        <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={form.autoSchedule} onChange={(event) => set('autoSchedule', event.target.checked)} />Raise jobs automatically</label>
      </div>
    </FormDrawer>
  )
}
