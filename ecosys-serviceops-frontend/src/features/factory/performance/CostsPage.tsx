import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { Wallet } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Badge } from '../../../shared/components/Badge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { cn } from '../../../shared/utils/cn'
import { useOrgNodes } from '../api'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton } from '../components/ui'
import { useScopeAndPeriod } from './PerformancePage'
import { BUDGET_CATEGORIES, COST_GROUPS, useBudgets, useCostCentres, useCostSettings, useCostSummary, useFactoryAction, type CostCentre } from './api'

const TABS = [{ id: 'costs', label: 'Costs' }, { id: 'budgets', label: 'Budgets' }, { id: 'centres', label: 'Cost centres' }, { id: 'settings', label: 'Settings' }] as const
type Tab = (typeof TABS)[number]['id']
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
const num = (value: number | null | undefined, digits = 0) => (value == null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: digits }))

/** Maintenance costs by section, cost centre, asset, job or month; budgets; cost centres; and the costing settings. */
export function CostsPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'costs') as Tab
  return (
    <div className="space-y-4">
      <PageHeader title="Maintenance costs" subtitle="Labour, parts, contractors (net of VAT) and downtime, against the budget." />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'costs' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'costs' ? <CostsTab /> : tab === 'budgets' ? <BudgetsTab /> : tab === 'centres' ? <CentresTab /> : <SettingsTab />}
    </div>
  )
}

function useFactories() {
  const nodes = useOrgNodes()
  return useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory' && x.isActive), [nodes.data])
}

function CostsTab() {
  const { orgNodeId, from, to, picker } = useScopeAndPeriod()
  const [groupBy, setGroupBy] = useState('section')
  const summary = useCostSummary(orgNodeId, from, to, groupBy)
  const d = summary.data
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        {picker}
        <Field label="By" className="w-40"><select className={inputClass} value={groupBy} onChange={(e) => setGroupBy(e.target.value)}>{COST_GROUPS.map((g) => <option key={g.id} value={g.id}>{g.label}</option>)}</select></Field>
      </div>
      {summary.isLoading ? <Spinner /> : !d || d.rows.length === 0 ? <EmptyState icon={Wallet} title="No costs in this period" description="Costs come from labour logged, parts issued, contractor invoices and breakdown downtime on jobs." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr>
              <th className="px-4 py-2">{COST_GROUPS.find((g) => g.id === groupBy)?.label}</th><th className="px-4 py-2 text-right">Labour</th><th className="px-4 py-2 text-right">Parts</th><th className="px-4 py-2 text-right">Contractor</th>
              <th className="px-4 py-2 text-right">Total ({d.currency})</th><th className="px-4 py-2 text-right">Downtime h</th><th className="px-4 py-2 text-right">{d.productionUnit} lost</th><th className="px-4 py-2 text-right">Value lost</th>
            </tr></thead>
            <tbody className="divide-y divide-slate-100">
              {[...d.rows, d.totals].map((r, i) => (
                <tr key={`${r.id ?? r.name}-${i}`} className={i === d.rows.length ? 'bg-slate-50 font-semibold' : ''}>
                  <td className="px-4 py-2">{r.name}</td><td className="px-4 py-2 text-right tabular-nums">{num(r.labour)}</td><td className="px-4 py-2 text-right tabular-nums">{num(r.parts)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{num(r.contractor)}</td><td className="px-4 py-2 text-right tabular-nums">{num(r.total)}</td><td className="px-4 py-2 text-right tabular-nums">{num(r.downtimeHours, 1)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{num(r.productionLost)}</td><td className="px-4 py-2 text-right tabular-nums">{num(r.downtimeValue)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

function BudgetsTab() {
  const canManage = usePermission('settings.manage')
  const factories = useFactories()
  const centres = useCostCentres()
  const [year, setYear] = useState(new Date().getFullYear())
  const [factoryId, setFactoryId] = useState('')
  const budgets = useBudgets(year)
  const action = useFactoryAction()
  const [values, setValues] = useState<Record<string, string>>({})
  const factory = factoryId || factories[0]?.id || ''
  const rows = [{ id: null as string | null, name: 'Whole factory' }, ...(centres.data ?? []).filter((c) => c.factoryId === factory && c.isActive).map((c) => ({ id: c.id as string | null, name: `${c.code} ${c.name}` }))]
  const key = (centre: string | null, category: string) => `${centre ?? 'all'}:${category}`

  useEffect(() => {
    const next: Record<string, string> = {}
    for (const b of budgets.data ?? []) if (b.factoryId === factory) next[key(b.costCentreId, b.category)] = String(b.amount)
    setValues(next)
  }, [budgets.data, factory])

  const save = async () => {
    const lines = rows.flatMap((r) => BUDGET_CATEGORIES.map((c) => ({ costCentreId: r.id, category: c, amount: values[key(r.id, c)] ? Number(values[key(r.id, c)]) : null })))
    try {
      await action.mutateAsync({ method: 'put', path: '/api/factory/costs/budgets', body: { factoryId: factory, year, lines } })
      toast.success('Budget saved')
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Factory" className="w-56"><select className={inputClass} value={factory} onChange={(e) => setFactoryId(e.target.value)}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
        <Field label="Year" className="w-28"><input type="number" className={inputClass} value={year} onChange={(e) => setYear(Number(e.target.value))} /></Field>
        {canManage ? <button type="button" className={cn(primaryButton, 'ml-auto')} disabled={!factory || action.isPending} onClick={() => void save()}>Save budget</button> : null}
      </div>
      <p className="text-xs text-slate-500">Budget either for the whole factory or by cost centre, not both, so totals don't count twice. Dashboards pro-rate the year's budget to the period shown.</p>
      <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
        <table className="w-full min-w-[620px] text-sm">
          <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Budget for</th>{BUDGET_CATEGORIES.map((c) => <th key={c} className="px-4 py-2 text-right">{c}</th>)}<th className="px-4 py-2 text-right">Total</th></tr></thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((r) => (
              <tr key={r.id ?? 'all'}>
                <td className="px-4 py-2">{r.name}</td>
                {BUDGET_CATEGORIES.map((c) => (
                  <td key={c} className="px-2 py-1 text-right">
                    <input type="number" min={0} disabled={!canManage} aria-label={`${r.name} ${c}`} className={cn(inputClass, 'h-9 text-right')} value={values[key(r.id, c)] ?? ''} onChange={(e) => setValues({ ...values, [key(r.id, c)]: e.target.value })} />
                  </td>
                ))}
                <td className="px-4 py-2 text-right tabular-nums">{num(BUDGET_CATEGORIES.reduce((s, c) => s + (Number(values[key(r.id, c)]) || 0), 0))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function CentresTab() {
  const canManage = usePermission('settings.manage')
  const factories = useFactories()
  const centres = useCostCentres()
  const action = useFactoryAction()
  const [editing, setEditing] = useState<CostCentre | null>(null)
  const empty = { factoryId: '', code: '', name: '', orgNodeId: '', isActive: true, externalId: '' }
  const [form, setForm] = useState(empty)
  useEffect(() => {
    setForm(editing ? { factoryId: editing.factoryId, code: editing.code, name: editing.name, orgNodeId: editing.orgNodeId ?? '', isActive: editing.isActive, externalId: editing.externalId ?? '' } : { ...empty, factoryId: factories[0]?.id ?? '' })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing, factories])
  const save = async () => {
    const body = { factoryId: form.factoryId, code: form.code, name: form.name, orgNodeId: form.orgNodeId || null, isActive: form.isActive, externalId: form.externalId || null }
    try {
      await action.mutateAsync(editing ? { method: 'put', path: `/api/factory/costs/cost-centres/${editing.id}`, body } : { path: '/api/factory/costs/cost-centres', body })
      toast.success('Saved')
      setEditing(null)
      setForm({ ...empty, factoryId: form.factoryId })
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">A cost centre carries the jobs of its location and everything under it. Jobs elsewhere fall to the cost centre of the nearest location above them.</p>
      {(centres.data ?? []).length === 0 ? <EmptyState icon={Wallet} title="No cost centres" description="Add one per section, or one for the whole factory." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[620px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Code</th><th className="px-4 py-2">Name</th><th className="px-4 py-2">Factory</th><th className="px-4 py-2">Carries jobs of</th><th className="px-4 py-2">ERP id</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(centres.data ?? []).map((c) => (
                <tr key={c.id} className={cn(canManage && 'cursor-pointer hover:bg-slate-50', !c.isActive && 'opacity-60')} onClick={() => canManage && setEditing(c)}>
                  <td className="px-4 py-2 font-mono text-xs">{c.code}</td><td className="px-4 py-2">{c.name}{!c.isActive ? <Badge size="sm">Inactive</Badge> : null}</td><td className="px-4 py-2">{c.factoryName}</td><td className="px-4 py-2 text-slate-600">{c.orgNodeName ?? 'Budget only'}</td><td className="px-4 py-2 text-slate-600">{c.externalId ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {canManage ? (
        <div className="grid gap-2 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 sm:grid-cols-[10rem_7rem_1fr_1fr_8rem_auto] sm:items-end">
          <Field label="Factory"><select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          <Field label="Code"><input className={inputClass} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></Field>
          <Field label="Name"><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="Carries jobs of"><LocationSelect value={form.orgNodeId} onChange={(v) => setForm({ ...form, orgNodeId: v })} emptyLabel="None (budget only)" /></Field>
          <Field label="ERP id"><input className={inputClass} value={form.externalId} onChange={(e) => setForm({ ...form, externalId: e.target.value })} /></Field>
          <div className="flex gap-2">
            {editing ? <button type="button" className={secondaryButton} onClick={() => setEditing(null)}>Cancel</button> : null}
            <button type="button" className={primaryButton} disabled={!form.code || !form.name || !form.factoryId} onClick={() => void save()}>{editing ? 'Save' : 'Add'}</button>
          </div>
          {editing ? <label className="flex items-center gap-2 text-sm sm:col-span-6"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
        </div>
      ) : null}
    </div>
  )
}

function SettingsTab() {
  const canManage = usePermission('settings.manage')
  const settings = useCostSettings()
  const action = useFactoryAction()
  const [form, setForm] = useState({ value: '', vat: '16', months: [] as number[], retention: '7', residency: '' })
  useEffect(() => {
    const s = settings.data
    if (s) setForm({ value: s.productionUnitValue?.toString() ?? '', vat: String(s.vatRatePercent), months: s.peakSeasonMonths, retention: String(s.personalDataRetentionYears), residency: s.dataResidency ?? '' })
  }, [settings.data])
  if (!settings.data) return <Spinner />
  const s = settings.data
  const save = async () => {
    try {
      await action.mutateAsync({ method: 'put', path: '/api/factory/costs/settings', body: { productionUnitValue: form.value ? Number(form.value) : null, vatRatePercent: Number(form.vat), peakSeasonMonths: form.months.join(','), personalDataRetentionYears: Number(form.retention), dataResidency: form.residency || null } })
      toast.success('Settings saved')
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <fieldset disabled={!canManage} className="max-w-xl space-y-4 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <Field label={`Value of 1 ${s.productionUnit} (${s.currency})`} hint="Puts a money figure on production lost to downtime."><input type="number" min={0} step="any" className={inputClass} value={form.value} onChange={(e) => setForm({ ...form, value: e.target.value })} /></Field>
      <Field label="VAT rate (%)"><input type="number" min={0} max={50} step="any" className={inputClass} value={form.vat} onChange={(e) => setForm({ ...form, vat: e.target.value })} /></Field>
      <Field label="Peak season" hint="Downtime is reported for peak and low season separately.">
        <div className="flex flex-wrap gap-1">
          {MONTHS.map((m, i) => {
            const on = form.months.includes(i + 1)
            return <button key={m} type="button" aria-pressed={on} className={cn('h-9 w-12 rounded-md border text-sm', on ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-600')}
              onClick={() => setForm({ ...form, months: on ? form.months.filter((x) => x !== i + 1) : [...form.months, i + 1].sort((a, b) => a - b) })}>{m}</button>
          })}
        </div>
      </Field>
      <Field label="Keep personal data of people who have left (years)" hint="Then contractors' training and PPE records are anonymised (Data protection)."><input type="number" min={1} max={50} className={inputClass} value={form.retention} onChange={(e) => setForm({ ...form, retention: e.target.value })} /></Field>
      <Field label="Where data is hosted"><input className={inputClass} value={form.residency} placeholder="e.g. Nairobi, Kenya" onChange={(e) => setForm({ ...form, residency: e.target.value })} /></Field>
      {canManage ? <button type="button" className={primaryButton} disabled={action.isPending} onClick={() => void save()}>Save settings</button> : null}
    </fieldset>
  )
}
