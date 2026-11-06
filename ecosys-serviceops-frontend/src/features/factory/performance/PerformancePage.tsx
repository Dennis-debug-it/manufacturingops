import { useMemo, useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { FileDown, FileSpreadsheet, Mail, Trash2 } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { Spinner } from '../../../shared/components/Spinner'
import { ErrorState } from '../../../shared/components/ErrorState'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, LocationSelect, formatNumber, inputClass, primaryButton, secondaryButton } from '../components/ui'
import {
  PERIOD_LABELS, downloadFile, periodDates, useComparison, useFactoryAction, useKpiSchedules, useKpis, usePilot,
  type CostBreakdown, type FactoryKpiReport, type PeriodPreset,
} from './api'

const pct = (value: number | null | undefined) => (value == null ? '—' : `${formatNumber(value)}%`)
const num = (value: number | null | undefined, digits = 0) => (value == null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: digits, minimumFractionDigits: 0 }))

/** Scope (group, factory or section) and period, shared by the dashboard sections. */
export function useScopeAndPeriod(defaultPreset: PeriodPreset = 'month') {
  const [orgNodeId, setOrgNodeId] = useState('')
  const [preset, setPreset] = useState<PeriodPreset>(defaultPreset)
  const [custom, setCustom] = useState({ from: '', to: '' })
  const { from, to } = useMemo(() => periodDates(preset, custom), [preset, custom])
  const picker = (
    <div className="flex flex-wrap items-end gap-2">
      <Field label="Where" className="w-64"><LocationSelect value={orgNodeId} onChange={setOrgNodeId} emptyLabel="All my factories" includeGroups /></Field>
      <Field label="Period" className="w-44">
        <select className={inputClass} value={preset} onChange={(e) => setPreset(e.target.value as PeriodPreset)}>
          {Object.entries(PERIOD_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
      </Field>
      {preset === 'custom' ? (
        <>
          <Field label="From" className="w-40"><input type="date" className={inputClass} value={custom.from} onChange={(e) => setCustom({ ...custom, from: e.target.value })} /></Field>
          <Field label="To" className="w-40"><input type="date" className={inputClass} value={custom.to} onChange={(e) => setCustom({ ...custom, to: e.target.value })} /></Field>
        </>
      ) : null}
    </div>
  )
  return { orgNodeId, from, to, picker }
}

/** Maintenance performance: every KPI category for the group, a factory or a section, the factory comparison, exports, the pilot report and emailed board reports. */
export function PerformancePage() {
  const { orgNodeId, from, to, picker } = useScopeAndPeriod()
  const kpis = useKpis(orgNodeId, from, to)
  const [busy, setBusy] = useState<string | null>(null)

  const exportAs = async (format: 'xlsx' | 'pdf') => {
    setBusy(format)
    try {
      await downloadFile('/api/factory/kpis/export', { orgNodeId: orgNodeId || undefined, from, to, format }, `maintenance-report.${format}`)
    } catch {
      // The API client shows the error.
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader title="Performance" subtitle="Downtime, reliability, planning, costs, spares, people, energy and compliance for the group, a factory or a section."
        secondaryActions={(
          <div className="flex gap-2">
            <button type="button" className={secondaryButton} disabled={busy !== null || !from} onClick={() => void exportAs('xlsx')}><FileSpreadsheet className="h-4 w-4" />Excel</button>
            <button type="button" className={secondaryButton} disabled={busy !== null || !from} onClick={() => void exportAs('pdf')}><FileDown className="h-4 w-4" />Board report PDF</button>
          </div>
        )} />
      {picker}
      {kpis.isLoading ? <Spinner label="Working out the figures" /> : kpis.isError || !kpis.data ? <ErrorState title="Couldn't load the figures" description="Check the period and try again." /> : <Kpis k={kpis.data} />}
      {!orgNodeId ? <Comparison from={from} to={to} /> : null}
      <PilotSection orgNodeId={orgNodeId} />
      <Schedules />
    </div>
  )
}

function Section({ title, children, className }: { title: string; children: ReactNode; className?: string }) {
  return (
    <section className={cn('rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm', className)}>
      <h2 className="mb-3 text-sm font-semibold text-slate-800">{title}</h2>
      {children}
    </section>
  )
}

function Figure({ label, value, hint, tone }: { label: string; value: string; hint?: string | undefined; tone?: 'bad' | 'warn' | undefined }) {
  return (
    <div>
      <p className="text-xs text-slate-500">{label}</p>
      <p className={cn('text-xl font-semibold tabular-nums', tone === 'bad' ? 'text-rose-700' : tone === 'warn' ? 'text-amber-700' : 'text-slate-900')}>{value}</p>
      {hint ? <p className="text-xs text-slate-500">{hint}</p> : null}
    </div>
  )
}

function Kpis({ k }: { k: FactoryKpiReport }) {
  const unit = k.productionUnit
  const money = (v: number | null | undefined) => (v == null ? '—' : `${k.currency} ${num(v)}`)
  return (
    <div className="space-y-4">
      <p className="text-sm text-slate-600">{k.scopeName} · {formatDate(k.from)} – {formatDate(new Date(new Date(k.to).getTime() - 864e5).toISOString())}</p>
      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Downtime">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <Figure label="Hours lost" value={num(k.downtime.hours, 1)} tone={k.downtime.hours > 0 ? 'warn' : undefined} />
            <Figure label={`${unit} lost`} value={num(k.downtime.productionLost)} hint={k.downtime.valueLost != null ? money(k.downtime.valueLost) : undefined} />
            <Figure label="Peak / low season" value={`${num(k.downtime.peakHours, 1)} / ${num(k.downtime.lowHours, 1)} h`} />
          </div>
        </Section>
        <Section title="Costs">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
            <Figure label="Maintenance cost" value={money(k.costs.total)} hint={`Labour ${num(k.costs.labour)} · parts ${num(k.costs.parts)} · contractors ${num(k.costs.contractor)}`} />
            <Figure label={`Cost per ${unit}`} value={k.costs.costPerUnit == null ? '—' : `${k.currency} ${num(k.costs.costPerUnit, 2)}`} hint={`${num(k.costs.production)} ${unit} made`} />
            <Figure label="Against budget" value={k.costs.budget.variancePercent == null ? 'No budget' : `${k.costs.budget.variancePercent > 0 ? '+' : ''}${num(k.costs.budget.variancePercent, 1)}%`}
              hint={k.costs.budget.budgetForPeriod ? `${money(k.costs.budget.actual)} of ${money(k.costs.budget.budgetForPeriod)}` : undefined}
              tone={(k.costs.budget.variancePercent ?? 0) > 10 ? 'bad' : (k.costs.budget.variancePercent ?? 0) > 0 ? 'warn' : undefined} />
          </div>
        </Section>
        <Section title="Reliability">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label="Breakdowns" value={num(k.reliability.breakdowns)} />
            <Figure label="MTBF" value={k.reliability.mtbfHours == null ? '—' : `${num(k.reliability.mtbfHours, 0)} h`} />
            <Figure label="MTTR" value={k.reliability.mttrHours == null ? '—' : `${num(k.reliability.mttrHours, 1)} h`} />
            <Figure label="Repeat failures" value={num(k.reliability.repeatFailures)} tone={k.reliability.repeatFailures > 0 ? 'warn' : undefined} />
          </div>
          {k.reliability.topFailing.length > 0 ? (
            <ul className="mt-3 space-y-1 text-sm">{k.reliability.topFailing.map((a) => <li key={a.assetId} className="flex justify-between gap-2"><span><span className="font-mono text-xs text-slate-500">{a.assetCode}</span> {a.assetName}</span><span className="tabular-nums text-slate-600">{a.breakdowns} · {num(a.downtimeHours, 1)} h</span></li>)}</ul>
          ) : null}
        </Section>
        <Section title="Planning">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label="PM compliance" value={pct(k.planning.pmCompliancePercent)} hint={k.planning.pmDue ? `${k.planning.pmOnTime} of ${k.planning.pmDue} on time` : 'None due'} tone={(k.planning.pmCompliancePercent ?? 100) < 80 ? 'bad' : undefined} />
            <Figure label="Overdue jobs" value={num(k.planning.overdueJobs)} tone={k.planning.overdueJobs > 0 ? 'warn' : undefined} />
            <Figure label="Backlog" value={`${num(k.planning.backlogHours)} h`} hint={k.planning.backlogWeeks != null ? `${num(k.planning.backlogWeeks, 1)} weeks of the team` : undefined} />
            <Figure label="Planned work" value={pct(k.planning.plannedPercent)} hint={`${k.planning.plannedJobs} planned · ${k.planning.reactiveJobs} reactive`} />
          </div>
        </Section>
        <Section title="Weekly window and overhaul">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label="Window plan done" value={pct(k.window.completionPercent)} hint={`${k.window.windows} window${k.window.windows === 1 ? '' : 's'}`} />
            <Figure label="Carried over" value={num(k.window.carriedOver)} hint={k.window.oldestCarryover ? `oldest ${k.window.oldestCarryover}×` : undefined} tone={k.window.oldestCarryover >= 2 ? 'warn' : undefined} />
            <Figure label="Breakdowns on deferred assets" value={num(k.window.breakdownsOnDeferredAssets)} tone={k.window.breakdownsOnDeferredAssets > 0 ? 'bad' : undefined} />
            <Figure label="Overhaul done" value={pct(k.overhaul.scopeCompletePercent)} hint={k.overhaul.budget != null ? `${num(k.overhaul.actualCost)} of ${num(k.overhaul.budget)} budget` : undefined} />
          </div>
        </Section>
        <Section title="Spares and people">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label="Spares issued" value={money(k.spares.consumptionValue)} hint={`stock ${money(k.spares.stockValue)}`} />
            <Figure label="Critical out / below min" value={`${k.spares.criticalStockOuts} / ${k.spares.criticalBelowMinimum}`} tone={k.spares.criticalStockOuts > 0 ? 'bad' : k.spares.criticalBelowMinimum > 0 ? 'warn' : undefined} />
            <Figure label="Utilisation" value={pct(k.people.utilisationPercent)} hint={`${num(k.people.loggedHours)} h logged · overtime ${pct(k.people.overtimePercent)}`} />
            <Figure label="Certificates" value={`${k.people.certificatesExpiring} expiring`} hint={k.people.certificatesExpired ? `${k.people.certificatesExpired} expired` : undefined} tone={k.people.certificatesExpired > 0 ? 'bad' : undefined} />
          </div>
        </Section>
        <Section title="Energy">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label={`kWh per ${unit}`} value={num(k.energy.kwhPerUnit, 3)} hint={`${num(k.energy.energyKwh)} kWh`} />
            <Figure label={`Fuel per ${unit}`} value={num(k.energy.fuelPerUnit, 4)} hint={`${num(k.energy.fuel, 1)} ${k.energy.fuelUnit ?? ''}`} />
          </div>
          {k.energy.production === 0 ? <p className="mt-2 text-xs text-slate-500">Record production on a Production meter (and kWh / firewood on Energy and Fuel meters) to see use per {unit}.</p> : null}
        </Section>
        <Section title="Assets and compliance">
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Figure label="Assets" value={num(k.assets.total)} hint={`${k.assets.down} down · ${Object.entries(k.assets.byCriticality).map(([c, n]) => `${c}: ${n}`).join(' ')}`} tone={k.assets.down > 0 ? 'warn' : undefined} />
            <Figure label="Statutory overdue" value={num(k.compliance.statutoryOverdue)} hint={`${k.compliance.statutoryDueSoon} due in 30 days`} tone={k.compliance.statutoryOverdue > 0 ? 'bad' : undefined} />
            <Figure label="Open findings" value={num(k.compliance.nonConformitiesOpen)} hint={k.compliance.nonConformitiesOverdue ? `${k.compliance.nonConformitiesOverdue} overdue` : undefined} tone={k.compliance.nonConformitiesOverdue > 0 ? 'bad' : undefined} />
            <Figure label="Next audit" value={k.compliance.nextAuditOn ? formatDate(k.compliance.nextAuditOn) : '—'} hint={k.compliance.nextAudit ?? undefined} />
          </div>
        </Section>
      </div>

      <Section title="Cost and downtime by section">
        <BreakdownTable rows={k.costs.bySection} unit={unit} />
      </Section>
      {k.costs.topAssets.length > 0 ? <Section title="Costliest assets"><BreakdownTable rows={k.costs.topAssets} unit={unit} /></Section> : null}
      {k.costs.budget.lines.length > 0 ? (
        <Section title="Budget vs actual (pro rata for the period)">
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Factory</th><th className="py-1">Cost centre</th><th className="py-1">Category</th><th className="py-1 text-right">Budget</th><th className="py-1 text-right">Actual</th></tr></thead>
            <tbody className="divide-y divide-slate-100">{k.costs.budget.lines.map((l, i) => <tr key={i}><td className="py-1">{l.factory}</td><td className="py-1">{l.costCentre ?? 'All'}</td><td className="py-1">{l.category}</td><td className="py-1 text-right tabular-nums">{num(l.budgetForPeriod)}</td><td className={cn('py-1 text-right tabular-nums', l.actual > l.budgetForPeriod ? 'text-rose-700' : '')}>{num(l.actual)}</td></tr>)}</tbody>
          </table>
        </Section>
      ) : null}
    </div>
  )
}

export function BreakdownTable({ rows, unit }: { rows: CostBreakdown[]; unit: string }) {
  if (rows.length === 0) return <p className="text-sm text-slate-500">No costs or downtime in the period.</p>
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[640px] text-sm">
        <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Name</th><th className="py-1 text-right">Labour</th><th className="py-1 text-right">Parts</th><th className="py-1 text-right">Contractor</th><th className="py-1 text-right">Total</th><th className="py-1 text-right">Downtime h</th><th className="py-1 text-right">{unit} lost</th></tr></thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((r, i) => <tr key={r.id ?? i}><td className="py-1">{r.name}</td><td className="py-1 text-right tabular-nums">{num(r.labour)}</td><td className="py-1 text-right tabular-nums">{num(r.parts)}</td><td className="py-1 text-right tabular-nums">{num(r.contractor)}</td><td className="py-1 text-right font-medium tabular-nums">{num(r.total)}</td><td className="py-1 text-right tabular-nums">{num(r.downtimeHours, 1)}</td><td className="py-1 text-right tabular-nums">{num(r.productionLost)}</td></tr>)}
        </tbody>
      </table>
    </div>
  )
}

function Comparison({ from, to }: { from: string; to: string }) {
  const rows = useComparison(from, to, true)
  if (!rows.data || rows.data.length < 2) return null
  return (
    <Section title="Factories side by side">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[860px] text-sm">
          <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Factory</th><th className="py-1 text-right">Downtime h</th><th className="py-1 text-right">Prod. lost</th><th className="py-1 text-right">Breakdowns</th><th className="py-1 text-right">MTTR h</th><th className="py-1 text-right">PM %</th><th className="py-1 text-right">Cost</th><th className="py-1 text-right">Cost / unit</th><th className="py-1 text-right">kWh / unit</th><th className="py-1 text-right">Critical out</th><th className="py-1 text-right">Statutory overdue</th></tr></thead>
          <tbody className="divide-y divide-slate-100">
            {rows.data.map((r) => (
              <tr key={r.factoryId}>
                <td className="py-1 font-medium">{r.factoryName}</td><td className="py-1 text-right tabular-nums">{num(r.downtimeHours, 1)}</td><td className="py-1 text-right tabular-nums">{num(r.productionLost)}</td>
                <td className="py-1 text-right tabular-nums">{r.breakdowns}</td><td className="py-1 text-right tabular-nums">{num(r.mttrHours, 1)}</td><td className="py-1 text-right tabular-nums">{pct(r.pmCompliancePercent)}</td>
                <td className="py-1 text-right tabular-nums">{num(r.costTotal)}</td><td className="py-1 text-right tabular-nums">{num(r.costPerUnit, 2)}</td><td className="py-1 text-right tabular-nums">{num(r.kwhPerUnit, 3)}</td>
                <td className={cn('py-1 text-right tabular-nums', r.criticalStockOuts > 0 && 'text-rose-700')}>{r.criticalStockOuts}</td><td className={cn('py-1 text-right tabular-nums', r.statutoryOverdue > 0 && 'text-rose-700')}>{r.statutoryOverdue}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  )
}

function PilotSection({ orgNodeId }: { orgNodeId: string }) {
  const [form, setForm] = useState({ baselineFrom: '', baselineTo: '', from: '', to: '' })
  const [params, setParams] = useState<typeof form | null>(null)
  const pilot = usePilot(params ? { orgNodeId, ...params } : null)
  const ready = form.baselineFrom && form.baselineTo && form.from && form.to
  return (
    <Section title="Pilot results">
      <p className="mb-3 text-sm text-slate-600">Compare a baseline before the system with the pilot period: downtime, breakdowns, repair time, PM compliance, cost and energy per unit.</p>
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Baseline from" className="w-40"><input type="date" className={inputClass} value={form.baselineFrom} onChange={(e) => setForm({ ...form, baselineFrom: e.target.value })} /></Field>
        <Field label="Baseline to" className="w-40"><input type="date" className={inputClass} value={form.baselineTo} onChange={(e) => setForm({ ...form, baselineTo: e.target.value })} /></Field>
        <Field label="Pilot from" className="w-40"><input type="date" className={inputClass} value={form.from} onChange={(e) => setForm({ ...form, from: e.target.value })} /></Field>
        <Field label="Pilot to" className="w-40"><input type="date" className={inputClass} value={form.to} onChange={(e) => setForm({ ...form, to: e.target.value })} /></Field>
        <button type="button" className={secondaryButton} disabled={!ready} onClick={() => setParams(form)}>Compare</button>
        {params ? <button type="button" className={secondaryButton} onClick={() => void downloadFile('/api/factory/kpis/pilot', { ...params, orgNodeId: orgNodeId || undefined, format: 'pdf' }, 'pilot-results.pdf').catch(() => undefined)}><FileDown className="h-4 w-4" />PDF</button> : null}
      </div>
      {pilot.isFetching ? <Spinner /> : pilot.data ? (
        <table className="mt-3 w-full text-sm">
          <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Measure</th><th className="py-1 text-right">Baseline</th><th className="py-1 text-right">Pilot</th><th className="py-1 text-right">Change</th></tr></thead>
          <tbody className="divide-y divide-slate-100">
            {pilot.data.measures.map((m) => {
              const better = m.changePercent != null && m.changePercent !== 0 && (m.changePercent < 0) === m.lowerIsBetter
              return (
                <tr key={m.measure}><td className="py-1">{m.measure}{m.unit ? <span className="text-xs text-slate-500"> ({m.unit})</span> : null}</td><td className="py-1 text-right tabular-nums">{num(m.baseline, 2)}</td><td className="py-1 text-right tabular-nums">{num(m.current, 2)}</td>
                  <td className={cn('py-1 text-right tabular-nums', m.changePercent == null || m.changePercent === 0 ? '' : better ? 'text-emerald-700' : 'text-rose-700')}>{m.changePercent == null ? '—' : `${m.changePercent > 0 ? '+' : ''}${num(m.changePercent, 1)}%`}</td></tr>
              )
            })}
          </tbody>
        </table>
      ) : null}
    </Section>
  )
}

function Schedules() {
  const canManage = usePermission('settings.manage')
  const schedules = useKpiSchedules()
  const action = useFactoryAction()
  const [form, setForm] = useState({ name: '', orgNodeId: '', recipients: '', frequency: 'Monthly' })
  if (!canManage && !(schedules.data ?? []).length) return null
  const add = async () => {
    try {
      await action.mutateAsync({ path: '/api/factory/kpis/schedules', body: { name: form.name || null, orgNodeId: form.orgNodeId || null, recipients: form.recipients, frequency: form.frequency, dateRangePreset: form.frequency === 'Weekly' ? 'LastWeek' : form.frequency === 'Quarterly' ? 'LastQuarter' : 'LastMonth', dayOfWeek: form.frequency === 'Weekly' ? 'Monday' : null } })
      toast.success('Board report scheduled')
      setForm({ name: '', orgNodeId: '', recipients: '', frequency: 'Monthly' })
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <Section title="Board reports by email">
      {(schedules.data ?? []).length === 0 ? <p className="text-sm text-slate-500">None scheduled.</p> : (
        <ul className="mb-3 divide-y divide-slate-100 text-sm">
          {(schedules.data ?? []).map((s) => (
            <li key={s.id} className="flex items-center justify-between gap-2 py-1.5">
              <span><Mail className="mr-1 inline h-4 w-4 text-slate-400" />{s.name} · {s.orgNodeName ?? 'All factories'} · {s.frequency}<span className="block text-xs text-slate-500">To {s.recipients}{s.nextRunAt ? ` · next ${formatDateTime(s.nextRunAt)}` : ''}{s.lastRunStatus ? ` · last ${s.lastRunStatus}` : ''}</span></span>
              {canManage ? <button type="button" aria-label={`Stop ${s.name}`} className="text-slate-400 hover:text-rose-600" onClick={() => void action.mutateAsync({ method: 'delete', path: `/api/factory/kpis/schedules/${s.id}` }).then(() => toast.success('Stopped')).catch(() => undefined)}><Trash2 className="h-4 w-4" /></button> : null}
            </li>
          ))}
        </ul>
      )}
      {canManage ? (
        <div className="grid gap-2 sm:grid-cols-[1fr_1fr_8rem_1.5fr_auto] sm:items-end">
          <Field label="Name"><input className={inputClass} value={form.name} placeholder="Monthly board report" onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
          <Field label="For"><LocationSelect value={form.orgNodeId} onChange={(v) => setForm({ ...form, orgNodeId: v })} emptyLabel="All factories" includeGroups /></Field>
          <Field label="Every"><select className={inputClass} value={form.frequency} onChange={(e) => setForm({ ...form, frequency: e.target.value })}><option>Weekly</option><option>Monthly</option><option>Quarterly</option></select></Field>
          <Field label="Send to"><input className={inputClass} value={form.recipients} placeholder="gm@factory.co.ke, board@…" onChange={(e) => setForm({ ...form, recipients: e.target.value })} /></Field>
          <button type="button" className={primaryButton} disabled={!form.recipients.trim() || action.isPending} onClick={() => void add()}>Schedule</button>
        </div>
      ) : null}
    </Section>
  )
}
