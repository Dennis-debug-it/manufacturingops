import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { CheckCircle2, ClipboardList, Plus, Repeat } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { ErrorState } from '../../../shared/components/ErrorState'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useOrgNodes } from '../api'
import { CriticalityBadge, Field, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { PRIORITIES } from '../jobs/types'
import { AssetPicker, type PickedAsset } from './AssetPicker'
import { OVERHAUL_SOURCE_LABELS, useOverhaulAction, useOverhaulItems, useOverhaulPlan, useOverhaulPlans, type OverhaulItem, type OverhaulPlan } from './api'

const money = (value: number | null | undefined) => (value == null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: 0 }))

function useFactories() {
  const nodes = useOrgNodes()
  return useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory' && x.isActive), [nodes.data])
}

/** The overhaul list built through the season, and each factory's overhauls. */
export function OverhaulPage() {
  const links = useFactoryLinks()
  const navigate = useNavigate()
  const canPlan = usePermission('work-orders.assign')
  const factories = useFactories()
  const [factoryId, setFactoryId] = useState('')
  const items = useOverhaulItems({ factoryId: factoryId || undefined })
  const plans = useOverhaulPlans(factoryId || undefined)
  const [editing, setEditing] = useState<OverhaulItem | 'new' | null>(null)
  const [newPlan, setNewPlan] = useState(false)
  const listed = (items.data ?? []).filter((x) => x.status === 'Listed')

  return (
    <div className="space-y-5">
      <PageHeader title="Overhaul" subtitle="Work saved up for the low-crop shutdown: deferred jobs, window carryovers and findings, then each overhaul's scope, budget and progress."
        primaryAction={canPlan ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add to the list</button> : undefined}
        secondaryActions={canPlan ? <button type="button" className={secondaryButton} onClick={() => setNewPlan(true)}>New overhaul</button> : undefined} />

      {factories.length > 1 ? (
        <select aria-label="Factory" className={cn(inputClass, 'w-full sm:w-64')} value={factoryId} onChange={(event) => setFactoryId(event.target.value)}>
          <option value="">All factories</option>
          {factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
        </select>
      ) : null}

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-800">Overhauls</h2>
        {plans.isLoading ? <Spinner /> : (plans.data ?? []).length === 0 ? <p className="text-sm text-slate-500">No overhaul planned yet.</p> : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {(plans.data ?? []).map((plan) => <PlanCard key={plan.id} plan={plan} to={links.overhaul(plan.id)} />)}
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-800">On the list · {listed.length} not yet in an overhaul</h2>
        {items.isLoading ? <Spinner /> : (items.data ?? []).length === 0 ? (
          <EmptyState icon={ClipboardList} title="The list is empty" description="Jobs deferred to the overhaul, window carryovers and findings collect here." />
        ) : <ItemsTable items={items.data ?? []} onOpen={canPlan ? setEditing : undefined} />}
      </section>

      <ItemDrawer item={editing} factories={factories} onClose={() => setEditing(null)} />
      <PlanDrawer open={newPlan} plan={null} factories={factories} onClose={() => setNewPlan(false)} onSaved={(id) => { setNewPlan(false); navigate(links.overhaul(id)) }} />
    </div>
  )
}

function PlanCard({ plan, to }: { plan: OverhaulPlan; to: string }) {
  const over = plan.budget != null && plan.estimatedCost > plan.budget
  return (
    <Link to={to} className="block rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm hover:border-slate-300">
      <div className="flex items-start justify-between gap-2">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{plan.factoryName}</p>
          <p className="font-semibold text-slate-900">{plan.name}</p>
          <p className="text-xs text-slate-500">{formatDate(plan.plannedStart)} – {formatDate(plan.plannedEnd)}</p>
        </div>
        <Badge size="sm" tone={plan.status === 'Completed' ? 'success' : plan.status === 'InProgress' ? 'warning' : 'info'}>{plan.status === 'InProgress' ? 'In progress' : plan.status}</Badge>
      </div>
      <p className="mt-3 text-sm text-slate-700">{plan.items} items · {plan.itemsDone} done · {plan.progressPercent}%</p>
      <div className="mt-1 h-2 rounded-full bg-slate-100"><div className="h-2 rounded-full bg-[var(--app-primary)]" style={{ width: `${plan.progressPercent}%` }} /></div>
      <p className={cn('mt-2 text-xs', over ? 'font-medium text-rose-700' : 'text-slate-500')}>Estimated {money(plan.estimatedCost)}{plan.budget != null ? ` of ${money(plan.budget)} budget` : ''}</p>
    </Link>
  )
}

function ItemsTable({ items, onOpen, recommission }: { items: OverhaulItem[]; onOpen?: ((item: OverhaulItem) => void) | undefined; recommission?: ((item: OverhaulItem) => void) | undefined }) {
  const links = useFactoryLinks()
  return (
    <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
      <table className="w-full min-w-[760px] text-sm">
        <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500">
          <tr><th className="px-4 py-2">Work</th><th className="px-4 py-2">Asset</th><th className="px-4 py-2">From</th><th className="px-4 py-2 text-right">Hours</th><th className="px-4 py-2 text-right">Cost</th><th className="px-4 py-2">Status</th></tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {items.map((item) => (
            <tr key={item.id} className={cn(onOpen && 'cursor-pointer hover:bg-slate-50')} onClick={() => onOpen?.(item)}>
              <td className="px-4 py-2">
                <span className="font-medium text-slate-900">{item.title}</span>
                <span className="block text-xs text-slate-500">{item.priority}{item.contractorName ? ` · ${item.contractorName}` : ''}{item.overhaulPlanName ? ` · ${item.overhaulPlanName}` : ''}</span>
              </td>
              <td className="px-4 py-2">{item.assetCode ? <><span className="font-mono text-xs">{item.assetCode}</span> <CriticalityBadge value={item.assetCriticality as 'A' | 'B' | 'C' | null} /><span className="block text-xs text-slate-500">{item.assetName}</span></> : <span className="text-slate-500">{item.factoryName}</span>}</td>
              <td className="px-4 py-2">
                <span className="inline-flex items-center gap-1 text-xs">{item.source === 'Carryover' ? <Repeat className="h-3 w-3" /> : null}{OVERHAUL_SOURCE_LABELS[item.source]}{item.carryoverCount ? ` ${item.carryoverCount}×` : ''}</span>
                {item.workOrderId ? <Link to={links.job(item.workOrderId)} onClick={(event) => event.stopPropagation()} className="block font-mono text-xs underline">{item.workOrderNumber}</Link> : null}
              </td>
              <td className="px-4 py-2 text-right tabular-nums">{item.estimatedHours != null ? formatNumber(item.estimatedHours) : '—'}</td>
              <td className="px-4 py-2 text-right tabular-nums">{money(item.actualCost ?? item.estimatedCost)}{item.actualCost != null ? <span className="block text-xs text-slate-500">actual</span> : null}</td>
              <td className="px-4 py-2">
                <Badge size="sm" tone={item.status === 'Done' ? 'success' : item.status === 'InProgress' ? 'warning' : item.status === 'Dropped' ? 'neutral' : 'info'}>{item.status === 'InProgress' ? `${item.progressPercent}%` : item.status}</Badge>
                {item.status === 'Done' && item.needsRecommissioning ? (
                  item.recommissionedAt ? <span className="mt-0.5 flex items-center gap-1 text-xs text-emerald-700"><CheckCircle2 className="h-3 w-3" />Recommissioned</span>
                    : recommission ? <button type="button" className="mt-0.5 block text-xs font-medium text-[var(--app-primary)] underline" onClick={(event) => { event.stopPropagation(); recommission(item) }}>Recommission</button>
                      : <span className="mt-0.5 block text-xs text-amber-700">To recommission</span>
                ) : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function ItemDrawer({ item, factories, onClose, plans }: { item: OverhaulItem | 'new' | null; factories: { id: string; name: string }[]; onClose: () => void; plans?: OverhaulPlan[] | undefined }) {
  const action = useOverhaulAction()
  const existing = item && item !== 'new' ? item : null
  const allPlans = useOverhaulPlans(existing?.factoryId)
  const choices = (plans ?? allPlans.data ?? []).filter((x) => x.status !== 'Completed' && (!existing || x.factoryId === existing.factoryId))
  const [asset, setAsset] = useState<PickedAsset | null>(null)
  const [form, setForm] = useState({ factoryId: '', title: '', description: '', priority: 'Medium', hours: '', cost: '', actual: '', spares: '', contractor: '', progress: '0', recommission: true, planId: '' })
  const [dropReason, setDropReason] = useState('')

  useEffect(() => {
    if (!item) return
    setAsset(existing?.assetId ? { id: existing.assetId, code: existing.assetCode ?? '', name: existing.assetName ?? '', categoryId: null } : null)
    setDropReason('')
    setForm(existing ? {
      factoryId: existing.factoryId, title: existing.title, description: existing.description ?? '', priority: existing.priority, hours: existing.estimatedHours?.toString() ?? '',
      cost: existing.estimatedCost?.toString() ?? '', actual: existing.actualCost?.toString() ?? '', spares: existing.sparesNote ?? '', contractor: existing.contractorName ?? '',
      progress: String(existing.progressPercent), recommission: existing.needsRecommissioning, planId: existing.overhaulPlanId ?? '',
    } : { factoryId: factories.length === 1 ? factories[0]!.id : '', title: '', description: '', priority: 'Medium', hours: '', cost: '', actual: '', spares: '', contractor: '', progress: '0', recommission: true, planId: '' })
  }, [item, existing, factories])

  const num = (value: string) => (value.trim() ? Number(value) : null)
  const submit = async () => {
    try {
      if (existing) {
        await action.mutateAsync({ method: 'put', path: `/items/${existing.id}`, body: {
          title: form.title, description: form.description || null, priority: form.priority, estimatedHours: num(form.hours), estimatedCost: num(form.cost), actualCost: num(form.actual),
          sparesNote: form.spares || null, contractorName: form.contractor || null, progressPercent: Number(form.progress), needsRecommissioning: form.recommission, overhaulPlanId: form.planId || null,
        } })
      } else {
        await action.mutateAsync({ path: '/items', body: { factoryId: asset ? null : form.factoryId || null, assetId: asset?.id ?? null, title: form.title, description: form.description || null, source: 'Finding', priority: form.priority, estimatedHours: num(form.hours), estimatedCost: num(form.cost) } })
      }

      toast.success('Saved')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  const setStatus = async (status: 'Done' | 'Dropped' | 'Listed') => {
    if (!existing) return
    try {
      await action.mutateAsync({ path: `/items/${existing.id}/status`, body: { status, reason: dropReason || null } })
      toast.success(status === 'Dropped' ? 'Taken off the list' : 'Saved')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <FormDrawer open={item !== null} title={existing ? existing.title : 'Add to the overhaul list'} onClose={onClose} footer={
      <div className="flex flex-wrap justify-end gap-2">
        {existing && existing.status !== 'Done' && existing.status !== 'Dropped' ? <button type="button" className={secondaryButton} disabled={!dropReason.trim()} onClick={() => void setStatus('Dropped')}>Drop</button> : null}
        {existing && existing.status === 'InProgress' ? <button type="button" className={secondaryButton} onClick={() => void setStatus('Done')}>Mark done</button> : null}
        <button type="button" className={primaryButton} disabled={action.isPending || !form.title.trim() || (!existing && !asset && !form.factoryId)} onClick={() => void submit()}>Save</button>
      </div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        {!existing ? (
          <>
            <Field label="Asset" className="sm:col-span-2"><AssetPicker value={asset} onChange={setAsset} /></Field>
            {!asset ? (
              <Field label="Or the factory" className="sm:col-span-2">
                <select className={inputClass} value={form.factoryId} onChange={(event) => setForm({ ...form, factoryId: event.target.value })}>
                  <option value="">Choose</option>
                  {factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
              </Field>
            ) : null}
          </>
        ) : existing.workOrderNumber ? <p className="text-sm text-slate-600 sm:col-span-2">From job {existing.workOrderNumber} ({OVERHAUL_SOURCE_LABELS[existing.source].toLowerCase()}).</p> : null}
        <Field label="What needs doing" required className="sm:col-span-2"><input className={inputClass} value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} /></Field>
        <Field label="Details" className="sm:col-span-2"><textarea className={textareaClass} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field>
        <Field label="Priority"><select className={inputClass} value={form.priority} onChange={(event) => setForm({ ...form, priority: event.target.value })}>{PRIORITIES.map((x) => <option key={x}>{x}</option>)}</select></Field>
        <Field label="Estimated hours"><input type="number" min={0} className={inputClass} value={form.hours} onChange={(event) => setForm({ ...form, hours: event.target.value })} /></Field>
        <Field label="Estimated cost"><input type="number" min={0} className={inputClass} value={form.cost} onChange={(event) => setForm({ ...form, cost: event.target.value })} /></Field>
        {existing ? (
          <>
            <Field label="Actual cost"><input type="number" min={0} className={inputClass} value={form.actual} onChange={(event) => setForm({ ...form, actual: event.target.value })} /></Field>
            <Field label="Spares" className="sm:col-span-2"><textarea className={textareaClass} value={form.spares} onChange={(event) => setForm({ ...form, spares: event.target.value })} placeholder="What to order, and lead times" /></Field>
            <Field label="Contractor"><input className={inputClass} value={form.contractor} onChange={(event) => setForm({ ...form, contractor: event.target.value })} /></Field>
            <Field label="Progress %"><input type="number" min={0} max={100} className={inputClass} value={form.progress} onChange={(event) => setForm({ ...form, progress: event.target.value })} /></Field>
            <Field label="Overhaul" className="sm:col-span-2" hint="Putting it in an overhaul's scope.">
              <select className={inputClass} value={form.planId} onChange={(event) => setForm({ ...form, planId: event.target.value })} disabled={existing.status === 'InProgress' || existing.status === 'Done'}>
                <option value="">Not yet (on the list)</option>
                {choices.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
              </select>
            </Field>
            <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={form.recommission} onChange={(event) => setForm({ ...form, recommission: event.target.checked })} />Needs recommissioning before production</label>
            {existing.status !== 'Done' && existing.status !== 'Dropped' ? <Field label="Reason to drop it" className="sm:col-span-2"><input className={inputClass} value={dropReason} onChange={(event) => setDropReason(event.target.value)} /></Field> : null}
          </>
        ) : null}
      </div>
    </FormDrawer>
  )
}

function PlanDrawer({ open, plan, factories, onClose, onSaved }: { open: boolean; plan: OverhaulPlan | null; factories: { id: string; name: string }[]; onClose: () => void; onSaved: (id: string) => void }) {
  const action = useOverhaulAction()
  const [form, setForm] = useState({ factoryId: '', name: '', start: '', end: '', budget: '', notes: '' })
  useEffect(() => {
    if (!open) return
    setForm(plan
      ? { factoryId: plan.factoryId, name: plan.name, start: plan.plannedStart.slice(0, 10), end: plan.plannedEnd.slice(0, 10), budget: plan.budget?.toString() ?? '', notes: plan.notes ?? '' }
      : { factoryId: factories.length === 1 ? factories[0]!.id : '', name: `${new Date().getFullYear() + 1} low-crop overhaul`, start: '', end: '', budget: '', notes: '' })
  }, [open, plan, factories])
  const submit = async () => {
    try {
      const body = { factoryId: form.factoryId, name: form.name, plannedStart: form.start, plannedEnd: form.end, budget: form.budget ? Number(form.budget) : null, notes: form.notes || null }
      const result = await action.mutateAsync(plan ? { method: 'put', path: `/plans/${plan.id}`, body } : { path: '/plans', body }) as OverhaulPlan
      toast.success('Overhaul saved')
      onSaved(result.id)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title={plan ? 'Edit overhaul' : 'New overhaul'} onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={action.isPending || !form.factoryId || !form.name.trim() || !form.start || !form.end} onClick={() => void submit()}>Save</button></div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        {!plan ? (
          <Field label="Factory" required className="sm:col-span-2">
            <select className={inputClass} value={form.factoryId} onChange={(event) => setForm({ ...form, factoryId: event.target.value })}>
              <option value="">Choose</option>
              {factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
            </select>
          </Field>
        ) : null}
        <Field label="Name" required className="sm:col-span-2"><input className={inputClass} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} /></Field>
        <Field label="Shutdown starts" required><input type="date" className={inputClass} value={form.start} onChange={(event) => setForm({ ...form, start: event.target.value })} /></Field>
        <Field label="Back in production" required><input type="date" className={inputClass} value={form.end} onChange={(event) => setForm({ ...form, end: event.target.value })} /></Field>
        <Field label="Budget"><input type="number" min={0} className={inputClass} value={form.budget} onChange={(event) => setForm({ ...form, budget: event.target.value })} /></Field>
        <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}

/** One overhaul: its scope against budget, progress through the shutdown, and recommissioning. */
export function OverhaulPlanPage() {
  const { id = '' } = useParams()
  const links = useFactoryLinks()
  const canPlan = usePermission('work-orders.assign')
  const canRun = usePermission('work-orders.complete') || canPlan
  const factories = useFactories()
  const { data, isLoading, isError } = useOverhaulPlan(id)
  const listed = useOverhaulItems({ factoryId: data?.plan.factoryId })
  const action = useOverhaulAction()
  const [editing, setEditing] = useState<OverhaulItem | null>(null)
  const [editPlan, setEditPlan] = useState(false)
  const [recommissioning, setRecommissioning] = useState<OverhaulItem | null>(null)
  const [notes, setNotes] = useState('')

  if (isLoading) return <Spinner />
  if (isError || !data) return <ErrorState title="Overhaul not found" description="It may be in a factory you don't have access to." />
  const { plan, items } = data
  const available = (listed.data ?? []).filter((x) => x.status === 'Listed')
  const over = plan.budget != null && plan.estimatedCost > plan.budget

  const run = async (path: string, done: string, body?: unknown) => {
    try {
      await action.mutateAsync({ path, body })
      toast.success(done)
    } catch {
      // The API client shows the error.
    }
  }

  const scope = (item: OverhaulItem) => void action.mutateAsync({ method: 'put', path: `/items/${item.id}`, body: {
    title: item.title, description: item.description, priority: item.priority, estimatedHours: item.estimatedHours, estimatedCost: item.estimatedCost, actualCost: item.actualCost,
    sparesNote: item.sparesNote, contractorName: item.contractorName, progressPercent: item.progressPercent, needsRecommissioning: item.needsRecommissioning, overhaulPlanId: plan.id,
  } }).then(() => toast.success('Added to the scope')).catch(() => undefined)

  return (
    <div className="space-y-4">
      <PageHeader title={plan.name} subtitle={`${plan.factoryName} · ${formatDate(plan.plannedStart)} – ${formatDate(plan.plannedEnd)}`}
        breadcrumbs={[{ label: 'Overhaul', to: links.overhauls() }, { label: plan.name }]}
        titleAdornment={<Badge tone={plan.status === 'Completed' ? 'success' : plan.status === 'InProgress' ? 'warning' : 'info'}>{plan.status === 'InProgress' ? 'In progress' : plan.status}</Badge>}
        primaryAction={canPlan && plan.status === 'Planning' ? <button type="button" className={primaryButton} onClick={() => { if (globalThis.confirm('Start the overhaul? A job is raised for every item in scope.')) void run(`/plans/${plan.id}/start`, 'Overhaul started') }}>Start the overhaul</button>
          : canPlan && plan.status === 'InProgress' ? <button type="button" className={primaryButton} onClick={() => void run(`/plans/${plan.id}/complete`, 'Overhaul closed')}>Close the overhaul</button> : undefined}
        secondaryActions={canPlan && plan.status !== 'Completed' ? <button type="button" className={secondaryButton} onClick={() => setEditPlan(true)}>Edit</button> : undefined} />

      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Tile label="Progress" value={`${plan.progressPercent}%`} hint={`${plan.itemsDone} of ${plan.items} items done`} />
        <Tile label="Estimated cost" value={money(plan.estimatedCost)} hint={plan.budget != null ? `Budget ${money(plan.budget)}` : 'No budget set'} warn={over} />
        <Tile label="Actual cost" value={money(plan.actualCost)} hint="Entered on items" />
        <Tile label="To recommission" value={String(plan.awaitingRecommissioning)} hint="Done, not yet signed off" warn={plan.awaitingRecommissioning > 0} />
      </div>
      {plan.actualStart ? <p className="text-sm text-slate-600">Started {formatDateTime(plan.actualStart)}{plan.actualEnd ? ` · closed ${formatDateTime(plan.actualEnd)}` : ''}.</p> : null}

      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-800">Scope · {items.length}</h2>
        {items.length === 0 ? <p className="text-sm text-slate-500">Nothing in scope yet. Add items from the list below.</p> : (
          <ItemsTable items={items} onOpen={canPlan ? setEditing : undefined} recommission={canRun ? (item) => { setNotes(''); setRecommissioning(item) } : undefined} />
        )}
      </section>

      {plan.status !== 'Completed' && canPlan && available.length > 0 ? (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-slate-800">On the list, not yet in an overhaul</h2>
          <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
            {available.map((item) => (
              <li key={item.id} className="flex items-center gap-3 px-4 py-2 text-sm">
                <span className="min-w-0 flex-1"><span className="font-medium">{item.title}</span><span className="block text-xs text-slate-500">{item.assetCode ? `${item.assetCode} · ` : ''}{OVERHAUL_SOURCE_LABELS[item.source]}{item.estimatedCost != null ? ` · ${money(item.estimatedCost)}` : ''}</span></span>
                <button type="button" className={secondaryButton} onClick={() => scope(item)}>Add to scope</button>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <ItemDrawer item={editing} factories={factories} plans={[plan]} onClose={() => setEditing(null)} />
      <PlanDrawer open={editPlan} plan={plan} factories={factories} onClose={() => setEditPlan(false)} onSaved={() => setEditPlan(false)} />
      <FormDrawer open={recommissioning !== null} title={`Recommission · ${recommissioning?.title ?? ''}`} onClose={() => setRecommissioning(null)} footer={
        <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={() => setRecommissioning(null)}>Back</button>
          <button type="button" className={primaryButton} disabled={action.isPending} onClick={() => recommissioning && void run(`/items/${recommissioning.id}/recommission`, 'Recommissioned', { notes: notes || null }).then(() => setRecommissioning(null))}>Sign off</button></div>
      }>
        <Field label="What was checked" hint="Trial run, settings, guards, readings."><textarea className={textareaClass} value={notes} onChange={(event) => setNotes(event.target.value)} /></Field>
      </FormDrawer>
    </div>
  )
}

function Tile({ label, value, hint, warn }: { label: string; value: string; hint: string; warn?: boolean | undefined }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-sm text-slate-600">{label}</p>
      <p className={cn('mt-1 text-2xl font-semibold tabular-nums', warn ? 'text-rose-700' : 'text-slate-900')}>{value}</p>
      <p className="text-xs text-slate-500">{hint}</p>
    </div>
  )
}

/** On a job: put it off until the overhaul (it leaves the weekly window queue). */
export function DeferToOverhaulDrawer({ open, workOrderId, workOrderNumber, onClose }: { open: boolean; workOrderId: string; workOrderNumber: string; onClose: () => void }) {
  const action = useOverhaulAction()
  const [reason, setReason] = useState('')
  useEffect(() => { if (open) setReason('') }, [open])
  return (
    <FormDrawer open={open} title={`Defer ${workOrderNumber} to the overhaul`} onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Back</button>
        <button type="button" className={primaryButton} disabled={!reason.trim() || action.isPending}
          onClick={() => void action.mutateAsync({ path: '/items/from-job', body: { workOrderId, reason } }).then(() => { toast.success('Added to the overhaul list'); onClose() }).catch(() => undefined)}>Defer</button></div>
    }>
      <div className="space-y-3">
        <p className="text-sm text-slate-600">The job stays open but waits for the low-crop shutdown instead of the weekly window.</p>
        <Field label="Why it waits" required><textarea className={textareaClass} value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Needs a three-day stop" /></Field>
      </div>
    </FormDrawer>
  )
}
