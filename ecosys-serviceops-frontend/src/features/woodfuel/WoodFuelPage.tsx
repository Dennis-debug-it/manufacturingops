import { useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { Download, Flame, Plus } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { Badge, type BadgeTone } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { formatDate, formatDateTime } from '../../shared/utils/dates'
import { formatCurrency, formatNumber } from '../../shared/utils/format'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton } from '../factory/components/ui'
import { numberOrNull, textOrNull, todayInput } from '../../shared/components/formStyles'
import {
  DECISION_LABEL, FORM_LABEL, MEASURE_LABEL, REASON_LABEL, RULE_LABEL, STACK_STATUS_LABEL, SUPPLIER_TYPE_LABEL,
  downloadDeliveries, useCounts, useDeliveries, useIssues, usePrices, useProduction, useSaveProduction, useSaveSettings, useSaveSpecies,
  useSuppliers, useWoodLookups, useYardSummary, type PriceBand, type WoodLookups, type WoodSettings,
} from './api'
import { CountStackDrawer, IssueWoodDrawer, PriceDrawer, ReceiveWoodDrawer, StackDrawer, SupplierDrawer } from './forms'
import { coverLabel, tonnes } from './logic'

const TABS = [
  { id: 'yard', label: 'Yard', manage: false },
  { id: 'deliveries', label: 'Deliveries', manage: false },
  { id: 'boilers', label: 'Boiler house', manage: false },
  { id: 'suppliers', label: 'Suppliers and prices', manage: true },
  { id: 'production', label: 'Made tea', manage: true },
  { id: 'settings', label: 'Settings', manage: true },
] as const
type Tab = (typeof TABS)[number]['id']

const DECISION_TONE: Record<string, BadgeTone> = { Accepted: 'success', AcceptedWithDeduction: 'warning', Rejected: 'danger' }
const STACK_TONE: Record<string, BadgeTone> = { Receiving: 'info', Seasoning: 'warning', Ready: 'success', Depleted: 'neutral' }
const kgText = (value: number) => `${formatNumber(Math.round(value * 10) / 10)} kg`

/**
 * Wood fuel: the yard board, deliveries weighed and tested for moisture, wood fed to the boilers, the
 * suppliers and their prices, the daily made-tea figure and the factory's rules. Part of the Wood Fuel
 * add-on. A yard clerk or boiler attendant can enter their part with no signal; managers see all of it.
 */
export function WoodFuelPage() {
  const [chosen, setChosen] = useState('')
  const lookups = useWoodLookups(chosen || undefined)
  const [tab, setTab] = useState<Tab>('yard')

  if (lookups.isLoading) return <Spinner label="Loading wood fuel" />
  if (lookups.isError || !lookups.data) return <EmptyState title="Wood fuel is not available" description="It could not be loaded. Check that your workspace has the Wood Fuel add-on and that you have been given access." />
  const data = lookups.data
  const factoryId = chosen || data.factoryId || ''
  if (data.factories.length === 0) return <EmptyState icon={Flame} title="No factory yet" description="Add a factory under Settings → Organisation, then come back to set up its wood yard." />

  const tabs = TABS.filter((x) => !x.manage || data.canManage)
  const activeTab = tabs.some((x) => x.id === tab) ? tab : 'yard'

  return (
    <div className="space-y-6">
      <PageHeader title="Wood fuel" subtitle="Deliveries, the wood yard, and what the boilers burn for every kg of made tea." />
      <div className="flex flex-wrap items-center gap-3">
        {data.factories.length > 1 ? (
          <select aria-label="Factory" className={cn(inputClass, 'w-auto min-w-48')} value={factoryId} onChange={(e) => setChosen(e.target.value)}>
            {data.factories.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        ) : <span className="text-sm font-medium text-slate-700">{data.factories[0]!.name}</span>}
        <div role="tablist" aria-label="Wood fuel" className="flex flex-wrap gap-1.5">
          {tabs.map((x) => (
            <button key={x.id} type="button" role="tab" aria-selected={activeTab === x.id} onClick={() => setTab(x.id)}
              className={cn('rounded-full border px-3.5 py-1.5 text-sm font-medium', activeTab === x.id ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
              {x.label}
            </button>
          ))}
        </div>
      </div>

      {activeTab === 'yard' ? <YardTab lookups={data} factoryId={factoryId} /> : null}
      {activeTab === 'deliveries' ? <DeliveriesTab lookups={data} factoryId={factoryId} /> : null}
      {activeTab === 'boilers' ? <BoilersTab lookups={data} factoryId={factoryId} /> : null}
      {activeTab === 'suppliers' ? <SuppliersTab lookups={data} /> : null}
      {activeTab === 'production' ? <ProductionTab factoryId={factoryId} /> : null}
      {activeTab === 'settings' ? <SettingsTab lookups={data} factoryId={factoryId} key={factoryId} /> : null}
    </div>
  )
}

/* ── Shared bits ─────────────────────────────────────────────────────────── */

function Table({ head, children, empty }: { head: string[]; children: ReactNode; empty?: string | undefined }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
      <table className="min-w-full text-sm">
        <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
          <tr>{head.map((x) => <th key={x} className="px-3 py-2.5">{x}</th>)}</tr>
        </thead>
        <tbody className="divide-y divide-slate-100">{children}</tbody>
      </table>
      {empty ? <p className="px-4 py-6 text-center text-sm text-slate-500">{empty}</p> : null}
    </div>
  )
}

const Cell = ({ children, right }: { children?: ReactNode; right?: boolean }) => <td className={cn('px-3 py-2.5 text-slate-800', right && 'text-right tabular-nums')}>{children}</td>

function Section({ title, action, children }: { title: string; action?: ReactNode; children: ReactNode }) {
  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2"><h2 className="text-base font-semibold text-slate-900">{title}</h2>{action}</div>
      {children}
    </section>
  )
}

function Periods({ days, onChange }: { days: number; onChange: (days: number) => void }) {
  return (
    <div role="radiogroup" aria-label="Period" className="flex gap-1.5">
      {[7, 30, 90].map((x) => (
        <button key={x} type="button" role="radio" aria-checked={days === x} onClick={() => onChange(x)}
          className={cn('rounded-full border px-3 py-1 text-xs font-medium', days === x ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-600')}>Last {x} days</button>
      ))}
    </div>
  )
}

/* ── Yard ────────────────────────────────────────────────────────────────── */

function YardTab({ lookups, factoryId }: { lookups: WoodLookups; factoryId: string }) {
  const [days, setDays] = useState(30)
  const summary = useYardSummary(factoryId, days)
  const counts = useCounts(factoryId)
  const [receiving, setReceiving] = useState(false)
  const [counting, setCounting] = useState<{ stackId: string; ledgerKg: number | null } | null>(null)
  const [stackForm, setStackForm] = useState<{ id: string; code: string; status: string } | 'new' | null>(null)

  if (summary.isLoading) return <Spinner label="Loading the yard" />
  if (summary.isError || !summary.data) return <EmptyState title="The yard board did not load" description="Try again in a moment." />
  const s = summary.data
  const overTarget = s.kgPerUnit !== null && s.targetKgPerUnit !== null && s.kgPerUnit > s.targetKgPerUnit

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Periods days={days} onChange={setDays} />
        <div className="flex gap-2">
          {lookups.canReceive ? <button type="button" className={primaryButton} onClick={() => setReceiving(true)}><Plus className="h-4 w-4" />Receive wood</button> : null}
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard label="In the yard" value={tonnes(s.stockKg)} tone={s.lowCover ? 'warning' : 'default'} />
        <StatCard label="Days of cover" value={coverLabel(s.daysOfCover)} tone={s.lowCover ? 'danger' : 'default'} />
        <StatCard label={`Wood per ${s.madeTeaKg > 0 ? 'kg made tea' : 'unit made'}`} value={s.kgPerUnit === null ? '—' : `${s.kgPerUnit} kg`} tone={overTarget ? 'warning' : 'default'} />
        <StatCard label="Wood cost per kg made tea" value={s.costPerUnit === null ? '—' : formatCurrency(s.costPerUnit)} />
        <StatCard label="Received" value={tonnes(s.receivedKg)} />
        <StatCard label="Burnt" value={tonnes(s.issuedKg)} />
        <StatCard label="Average moisture" value={s.averageMoisturePct === null ? '—' : `${s.averageMoisturePct}%`} />
        <StatCard label="Rejected" value={tonnes(s.rejectedKg)} tone={s.rejectedKg > 0 ? 'warning' : 'default'} />
      </div>
      {s.lowCover ? <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">Only {coverLabel(s.daysOfCover)} of wood is left at the recent burn of {kgText(s.averageDailyBurnKg ?? 0)} a day. Order more or bring forward the next delivery.</p> : null}
      {s.madeTeaKg === 0 ? <p className="text-sm text-slate-500">Enter the days’ made tea under “Made tea” to see wood per kg of made tea and its cost.</p> : null}
      {s.unstackedKg !== 0 ? <p className="text-sm text-slate-500">{kgText(s.unstackedKg)} was received without a stack.</p> : null}

      <Section title="Stacks" action={lookups.canReceive ? <button type="button" className={secondaryButton} onClick={() => setStackForm('new')}><Plus className="h-4 w-4" />New stack</button> : null}>
        <Table head={['Stack', 'Status', 'Wood', 'Kg now', 'Moisture', 'Ready from', '']} empty={s.stacks.length === 0 ? 'No stacks yet. Add one so deliveries can be put somewhere.' : undefined}>
          {s.stacks.map((x) => (
            <tr key={x.id}>
              <Cell><span className="font-medium">{x.code}</span></Cell>
              <Cell><Badge tone={x.isReady && x.status !== 'Ready' ? 'success' : STACK_TONE[x.status] ?? 'neutral'}>{x.isReady && x.status !== 'Ready' && x.status !== 'Depleted' ? 'Ready (dry enough)' : STACK_STATUS_LABEL[x.status] ?? x.status}</Badge></Cell>
              <Cell>{x.species ?? '—'}</Cell>
              <Cell right>{formatNumber(x.kg)}</Cell>
              <Cell right>{x.averageMoisturePct === null ? '—' : `${x.averageMoisturePct}%`}</Cell>
              <Cell>{x.readyFrom ? formatDate(x.readyFrom) : '—'}</Cell>
              <Cell>
                <div className="flex justify-end gap-1.5">
                  {lookups.canReceive ? <button type="button" className={secondaryButton} onClick={() => setCounting({ stackId: x.id, ledgerKg: x.kg })}>Count</button> : null}
                  {lookups.canReceive ? <button type="button" className={secondaryButton} onClick={() => setStackForm({ id: x.id, code: x.code, status: x.status })}>Edit</button> : null}
                </div>
              </Cell>
            </tr>
          ))}
        </Table>
      </Section>

      <Section title="Where the wood went">
        <p className="text-sm text-slate-600">
          Opening {kgText(s.openingKg)} + received {kgText(s.receivedKg)} − burnt {kgText(s.issuedKg)} {s.adjustmentKg >= 0 ? '+' : '−'} adjustments {kgText(Math.abs(s.adjustmentKg))} = closing <span className="font-semibold text-slate-900">{kgText(s.closingKg)}</span>
        </p>
        {s.deductedKg > 0 ? <p className="text-sm text-slate-500">{kgText(s.deductedKg)} of wet wood was not paid for. {formatCurrency(s.amountPayable)} is payable for the period.</p> : s.amountPayable > 0 ? <p className="text-sm text-slate-500">{formatCurrency(s.amountPayable)} is payable for the period.</p> : null}
      </Section>

      <Section title="Suppliers">
        <Table head={['Supplier', 'Loads', 'Kg accepted', 'Moisture', 'Rejected loads', 'Payable']} empty={s.suppliers.length === 0 ? 'No deliveries in this period.' : undefined}>
          {s.suppliers.map((x) => (
            <tr key={x.supplierId ?? x.supplier}>
              <Cell>{x.supplier}</Cell><Cell right>{x.deliveries}</Cell><Cell right>{formatNumber(x.netKg)}</Cell>
              <Cell right>{x.averageMoisturePct === null ? '—' : `${x.averageMoisturePct}%`}</Cell>
              <Cell right>{x.rejected}</Cell><Cell right>{formatCurrency(x.amount)}</Cell>
            </tr>
          ))}
        </Table>
      </Section>

      <Section title="Day by day">
        <Table head={['Day', 'Received kg', 'Burnt kg', 'Made tea kg', 'Wood per kg tea']} empty={s.daily.length === 0 ? 'Nothing recorded in this period.' : undefined}>
          {[...s.daily].reverse().map((x) => (
            <tr key={x.date}>
              <Cell>{formatDate(x.date)}</Cell><Cell right>{formatNumber(x.receivedKg)}</Cell><Cell right>{formatNumber(x.issuedKg)}</Cell>
              <Cell right>{x.madeTeaKg ? formatNumber(x.madeTeaKg) : '—'}</Cell><Cell right>{x.kgPerUnit ?? '—'}</Cell>
            </tr>
          ))}
        </Table>
      </Section>

      <Section title="Recent counts">
        <Table head={['When', 'Stack', 'Ledger kg', 'Counted kg', 'Difference', 'Why', 'By']} empty={(counts.data ?? []).length === 0 ? 'No counts yet.' : undefined}>
          {(counts.data ?? []).slice(0, 10).map((x) => (
            <tr key={x.id}>
              <Cell>{formatDateTime(x.countedAt)}</Cell><Cell>{x.stack}</Cell><Cell right>{formatNumber(x.ledgerKg)}</Cell><Cell right>{formatNumber(x.countedKg)}</Cell>
              <Cell right><span className={x.varianceKg < 0 ? 'text-rose-600' : ''}>{formatNumber(x.varianceKg)}</span></Cell><Cell>{REASON_LABEL[x.reason] ?? x.reason}</Cell><Cell>{x.countedBy}</Cell>
            </tr>
          ))}
        </Table>
      </Section>

      {receiving ? <ReceiveWoodDrawer open onClose={() => setReceiving(false)} lookups={lookups} factoryId={factoryId} /> : null}
      {counting ? <CountStackDrawer open onClose={() => setCounting(null)} lookups={lookups} stackId={counting.stackId} ledgerKg={counting.ledgerKg} /> : null}
      {stackForm ? <StackDrawer open onClose={() => setStackForm(null)} lookups={lookups} factoryId={factoryId} {...(stackForm === 'new' ? {} : { stack: stackForm })} /> : null}
    </div>
  )
}

/* ── Deliveries ──────────────────────────────────────────────────────────── */

function DeliveriesTab({ lookups, factoryId }: { lookups: WoodLookups; factoryId: string }) {
  const [days, setDays] = useState(30)
  const [q, setQ] = useState('')
  const [decision, setDecision] = useState('')
  const [receiving, setReceiving] = useState(false)
  const [open, setOpen] = useState<string | null>(null)
  const deliveries = useDeliveries({ factoryId, days, q, decision })

  const exportCsv = async () => {
    try {
      await downloadDeliveries(factoryId, days)
    } catch {
      toast.error('Could not download the list')
    }
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <Periods days={days} onChange={setDays} />
          <input aria-label="Find a delivery" className={cn(inputClass, 'w-52')} placeholder="Supplier, vehicle, number" value={q} onChange={(e) => setQ(e.target.value)} />
          <select aria-label="Decision" className={cn(inputClass, 'w-auto')} value={decision} onChange={(e) => setDecision(e.target.value)}>
            <option value="">All decisions</option>
            {Object.entries(DECISION_LABEL).map(([key, label]) => <option key={key} value={key}>{label}</option>)}
          </select>
        </div>
        <div className="flex gap-2">
          {lookups.canManage ? <button type="button" className={secondaryButton} onClick={() => void exportCsv()}><Download className="h-4 w-4" />Export for finance</button> : null}
          {lookups.canReceive ? <button type="button" className={primaryButton} onClick={() => setReceiving(true)}><Plus className="h-4 w-4" />Receive wood</button> : null}
        </div>
      </div>

      {deliveries.isLoading ? <Spinner label="Loading deliveries" /> : (
        <Table head={['Number', 'Received', 'Supplier', 'Vehicle', 'Kg', 'Moisture', 'Decision', 'Payable kg', 'Amount']} empty={(deliveries.data ?? []).length === 0 ? 'No deliveries in this period.' : undefined}>
          {(deliveries.data ?? []).map((x) => (
            <>
              <tr key={x.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(open === x.id ? null : x.id)}>
                <Cell><span className="font-mono text-xs">{x.deliveryNumber}</span></Cell>
                <Cell>{formatDateTime(x.receivedAt)}</Cell>
                <Cell>{x.supplier || '—'}</Cell>
                <Cell>{x.vehicleRegistration || '—'}</Cell>
                <Cell right>{formatNumber(x.netKg)}</Cell>
                <Cell right>{x.averageMoisturePct === null ? '—' : `${x.averageMoisturePct}%`}</Cell>
                <Cell><Badge tone={DECISION_TONE[x.decision] ?? 'neutral'}>{DECISION_LABEL[x.decision] ?? x.decision}</Badge></Cell>
                <Cell right>{formatNumber(x.payableKg)}</Cell>
                <Cell right>{formatCurrency(x.amountPayable)}</Cell>
              </tr>
              {open === x.id ? (
                <tr key={`${x.id}-detail`} className="bg-slate-50">
                  <td colSpan={9} className="px-4 py-3 text-sm text-slate-700">
                    <dl className="grid gap-x-6 gap-y-1 sm:grid-cols-2 lg:grid-cols-3">
                      <div>Measured: {MEASURE_LABEL[x.measureBasis] ?? x.measureBasis}{x.grossKg !== null ? ` (gross ${formatNumber(x.grossKg)}, tare ${formatNumber(x.tareKg ?? 0)} kg)` : ''}{x.stackedM3 !== null ? ` (${x.stackedM3} m³)` : ''}{x.loads !== null ? ` (${x.loads} loads of ${formatNumber(x.kgPerLoad ?? 0)} kg)` : ''}</div>
                      <div>Wood: {x.species || '—'} · {FORM_LABEL[x.fuelForm] ?? x.fuelForm}</div>
                      <div>Stack: {x.stack || 'none'}</div>
                      <div>Moisture readings: {x.moistureSamples.length ? x.moistureSamples.map((v) => `${v}%`).join(', ') : 'none taken'}</div>
                      <div>Price: {formatCurrency(x.pricePerTonne)} a tonne{x.pricePercent < 100 ? ` at ${x.pricePercent}%` : ''}</div>
                      <div>Received by {x.receivedBy}</div>
                      {x.sourceArea ? <div>Cut at {x.sourceArea}</div> : null}
                      {x.transportPermit ? <div>Permit {x.transportPermit}</div> : null}
                      {x.supplierNoteNumber ? <div>Supplier note {x.supplierNoteNumber}</div> : null}
                      {x.driverName ? <div>Driver {x.driverName}</div> : null}
                      {x.decisionReason ? <div className="sm:col-span-2 lg:col-span-3 font-medium">{x.decisionReason}</div> : null}
                      {x.notes ? <div className="sm:col-span-2 lg:col-span-3 text-slate-500">{x.notes}</div> : null}
                    </dl>
                  </td>
                </tr>
              ) : null}
            </>
          ))}
        </Table>
      )}
      {receiving ? <ReceiveWoodDrawer open onClose={() => setReceiving(false)} lookups={lookups} factoryId={factoryId} /> : null}
    </div>
  )
}

/* ── Boiler house ────────────────────────────────────────────────────────── */

function BoilersTab({ lookups, factoryId }: { lookups: WoodLookups; factoryId: string }) {
  const issues = useIssues(factoryId)
  const [feeding, setFeeding] = useState(false)
  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        {lookups.canIssue ? <button type="button" className={primaryButton} onClick={() => setFeeding(true)}><Plus className="h-4 w-4" />Wood fed to a boiler</button> : null}
      </div>
      {issues.isLoading ? <Spinner label="Loading" /> : (
        <Table head={['When', 'Boiler or dryer', 'Stack', 'Shift', 'Kg', 'By', 'On meter']} empty={(issues.data ?? []).length === 0 ? 'Nothing recorded in the last two weeks.' : undefined}>
          {(issues.data ?? []).map((x) => (
            <tr key={x.id}>
              <Cell>{formatDateTime(x.issuedAt)}</Cell><Cell>{x.asset}</Cell><Cell>{x.stack ?? '—'}</Cell><Cell>{x.shift ?? '—'}</Cell>
              <Cell right>{formatNumber(x.kg)}</Cell><Cell>{x.issuedBy}</Cell><Cell>{x.onMeter ? 'Yes' : 'No'}</Cell>
            </tr>
          ))}
        </Table>
      )}
      <p className="text-xs text-slate-500">Each entry also adds to the boiler’s wood meter, so its fuel history and the maintenance figures stay in step without a second entry.</p>
      {feeding ? <IssueWoodDrawer open onClose={() => setFeeding(false)} lookups={lookups} factoryId={factoryId} /> : null}
    </div>
  )
}

/* ── Suppliers and prices ────────────────────────────────────────────────── */

function SuppliersTab({ lookups }: { lookups: WoodLookups }) {
  const suppliers = useSuppliers()
  const prices = usePrices()
  const species = useSaveSpecies()
  const [supplierForm, setSupplierForm] = useState<'new' | string | null>(null)
  const [priceForm, setPriceForm] = useState<'new' | string | null>(null)

  const editSupplier = supplierForm && supplierForm !== 'new' ? (suppliers.data ?? []).find((x) => x.id === supplierForm) : undefined
  const editPrice = priceForm && priceForm !== 'new' ? (prices.data ?? []).find((x) => x.id === priceForm) : undefined

  const changeDensity = async (id: string, name: string, value: string, energy: number | null) => {
    const kg = numberOrNull(value)
    if (kg === null || kg <= 0) return toast.error('Enter the weight of a stacked cubic metre in kg.')
    try {
      await species.mutateAsync({ id, name, kgPerStackedM3: kg, energyMjPerKg: energy })
      toast.success(`${name} saved`)
    } catch {
      // shown by the API client
    }
  }

  return (
    <div className="space-y-8">
      <Section title="Suppliers" action={<button type="button" className={secondaryButton} onClick={() => setSupplierForm('new')}><Plus className="h-4 w-4" />New supplier</button>}>
        <Table head={['Supplier', 'Type', 'Phone', 'Pays to', 'Permit', '']} empty={(suppliers.data ?? []).length === 0 ? 'No suppliers yet.' : undefined}>
          {(suppliers.data ?? []).map((x) => (
            <tr key={x.id} className={x.isActive ? '' : 'opacity-60'}>
              <Cell><span className="font-medium">{x.name}</span>{x.idLast4 ? <span className="ml-2 text-xs text-slate-500">ID …{x.idLast4}</span> : null}</Cell>
              <Cell>{SUPPLIER_TYPE_LABEL[x.type] ?? x.type}</Cell><Cell>{x.phone ?? '—'}</Cell><Cell>{x.paymentDetails ?? '—'}</Cell>
              <Cell>{x.permitNumber ? <>{x.permitNumber}{x.permitExpiresOn ? <> · {x.permitExpired ? <Badge tone="danger" size="sm">Expired {formatDate(x.permitExpiresOn)}</Badge> : `to ${formatDate(x.permitExpiresOn)}`}</> : null}</> : '—'}</Cell>
              <Cell><div className="flex justify-end"><button type="button" className={secondaryButton} onClick={() => setSupplierForm(x.id)}>Edit</button></div></Cell>
            </tr>
          ))}
        </Table>
      </Section>

      <Section title="Prices" action={<button type="button" className={secondaryButton} onClick={() => setPriceForm('new')}><Plus className="h-4 w-4" />New price</button>}>
        <p className="text-sm text-slate-600">The most specific live price is used: a supplier’s own, then one for the kind of wood, then the form, then the general price.</p>
        <Table head={['Supplier', 'Wood', 'Form', 'Per tonne', 'From', 'Until', '']} empty={(prices.data ?? []).length === 0 ? 'No prices yet. Without one, deliveries are saved with no amount.' : undefined}>
          {(prices.data ?? []).map((x) => (
            <tr key={x.id} className={x.isActive ? '' : 'opacity-60'}>
              <Cell>{x.supplier ?? 'Every supplier'}</Cell><Cell>{x.species ?? 'Any'}</Cell><Cell>{x.fuelForm ? FORM_LABEL[x.fuelForm] ?? x.fuelForm : 'Any'}</Cell>
              <Cell right>{formatCurrency(x.pricePerTonne)}</Cell><Cell>{formatDate(x.validFrom)}</Cell><Cell>{x.validTo ? formatDate(x.validTo) : '—'}</Cell>
              <Cell><div className="flex justify-end"><button type="button" className={secondaryButton} onClick={() => setPriceForm(x.id)}>Edit</button></div></Cell>
            </tr>
          ))}
        </Table>
      </Section>

      <Section title="Kinds of wood">
        <p className="text-sm text-slate-600">What a stacked cubic metre weighs, at the base moisture. These are starting figures: weigh a few loads and correct them.</p>
        <Table head={['Wood', 'Kg per stacked m³']}>
          {lookups.species.map((x) => (
            <tr key={`${x.id}-${x.kgPerStackedM3}`}>
              <Cell>{x.name}</Cell>
              <Cell>
                <input aria-label={`Kg per stacked cubic metre of ${x.name}`} className={cn(inputClass, 'w-28')} inputMode="decimal" defaultValue={x.kgPerStackedM3}
                  onBlur={(e) => { if (Number(e.target.value) !== x.kgPerStackedM3) void changeDensity(x.id, x.name, e.target.value, x.energyMjPerKg) }} />
              </Cell>
            </tr>
          ))}
        </Table>
      </Section>

      {supplierForm ? <SupplierDrawer key={supplierForm} open onClose={() => setSupplierForm(null)} lookups={lookups} {...(editSupplier ? { supplier: editSupplier } : {})} /> : null}
      {priceForm ? <PriceDrawer key={priceForm} open onClose={() => setPriceForm(null)} lookups={lookups} {...(editPrice ? { price: editPrice } : {})} /> : null}
    </div>
  )
}

/* ── Made tea ────────────────────────────────────────────────────────────── */

function ProductionTab({ factoryId }: { factoryId: string }) {
  const days = useProduction(factoryId)
  const save = useSaveProduction()
  const [date, setDate] = useState(todayInput())
  const [made, setMade] = useState('')
  const [leaf, setLeaf] = useState('')
  const [notes, setNotes] = useState('')

  const submit = async () => {
    const tea = numberOrNull(made)
    if (tea === null || tea < 0) return toast.error('Enter the made tea in kg.')
    try {
      await save.mutateAsync({ factoryId, date, madeTeaKg: tea, greenLeafKg: numberOrNull(leaf), notes: textOrNull(notes) })
      toast.success('Saved')
      setMade('')
      setLeaf('')
      setNotes('')
    } catch {
      // shown by the API client
    }
  }

  return (
    <div className="space-y-5">
      <p className="text-sm text-slate-600">Enter the day’s made tea. Wood per kg of made tea divides by it. Entering a day again replaces it.</p>
      <div className="grid gap-3 rounded-xl border border-slate-200 bg-white p-4 sm:grid-cols-5">
        <Field label="Day"><input type="date" className={inputClass} value={date} onChange={(e) => setDate(e.target.value)} /></Field>
        <Field label="Made tea (kg)" required><input className={inputClass} inputMode="decimal" value={made} onChange={(e) => setMade(e.target.value)} /></Field>
        <Field label="Green leaf (kg)"><input className={inputClass} inputMode="decimal" value={leaf} onChange={(e) => setLeaf(e.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-1"><input className={inputClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
        <div className="flex items-end"><button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}>Save the day</button></div>
      </div>
      <Table head={['Day', 'Made tea kg', 'Green leaf kg', 'Notes', 'Entered by']} empty={(days.data ?? []).length === 0 ? 'No days entered yet.' : undefined}>
        {(days.data ?? []).map((x) => (
          <tr key={x.id}>
            <Cell>{formatDate(x.date)}</Cell><Cell right>{formatNumber(x.madeTeaKg)}</Cell><Cell right>{x.greenLeafKg === null ? '—' : formatNumber(x.greenLeafKg)}</Cell><Cell>{x.notes ?? ''}</Cell><Cell>{x.enteredBy}</Cell>
          </tr>
        ))}
      </Table>
    </div>
  )
}

/* ── Settings ────────────────────────────────────────────────────────────── */

function SettingsTab({ lookups, factoryId }: { lookups: WoodLookups; factoryId: string }) {
  const save = useSaveSettings()
  const s = lookups.settings
  const [basis, setBasis] = useState(s?.defaultMeasureBasis ?? 'Weighbridge')
  const [rule, setRule] = useState(s?.moistureRule ?? 'DeductKg')
  const [base, setBase] = useState(String(s?.baseMoisturePct ?? 25))
  const [reject, setReject] = useState(String(s?.rejectMoisturePct ?? 45))
  const [deduction, setDeduction] = useState(String(s?.deductionPctPerPoint ?? 1.5))
  const [bands, setBands] = useState<PriceBand[]>(s?.priceBands ?? [{ upToPct: 30, percentOfPrice: 100 }, { upToPct: 35, percentOfPrice: 90 }, { upToPct: 40, percentOfPrice: 80 }, { upToPct: 45, percentOfPrice: 65 }])
  const [perLoad, setPerLoad] = useState(s?.defaultKgPerLoad ? String(s.defaultKgPerLoad) : '')
  const [window, setWindow] = useState(String(s?.burnWindowDays ?? 14))
  const [cover, setCover] = useState(String(s?.lowCoverDays ?? 5))
  const [seasoning, setSeasoning] = useState(String(s?.seasoningDays ?? 28))
  const [target, setTarget] = useState(s?.targetKgPerUnit ? String(s.targetKgPerUnit) : '')

  const submit = async () => {
    const payload: WoodSettings = {
      factoryId, defaultMeasureBasis: basis, moistureRule: rule, baseMoisturePct: Number(base), rejectMoisturePct: Number(reject), deductionPctPerPoint: Number(deduction),
      priceBands: bands, defaultKgPerLoad: numberOrNull(perLoad), burnWindowDays: Number(window), lowCoverDays: Number(cover), seasoningDays: Number(seasoning), targetKgPerUnit: numberOrNull(target),
    }
    try {
      await save.mutateAsync(payload)
      toast.success('Settings saved')
    } catch {
      // shown by the API client
    }
  }

  return (
    <div className="max-w-3xl space-y-6">
      <p className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-600">These are starting figures, not fixed rules. Set them to how this factory buys wood today.</p>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Usually measured by">
          <select className={inputClass} value={basis} onChange={(e) => setBasis(e.target.value)}>{lookups.measureBases.map((x) => <option key={x} value={x}>{MEASURE_LABEL[x] ?? x}</option>)}</select>
        </Field>
        <Field label="Weight of one load (kg)" hint="For wood bought by the tractor or lorry load."><input className={inputClass} inputMode="decimal" value={perLoad} onChange={(e) => setPerLoad(e.target.value)} /></Field>
        <Field label="Price assumes moisture of (%)"><input className={inputClass} inputMode="decimal" value={base} onChange={(e) => setBase(e.target.value)} /></Field>
        <Field label="Reject wood wetter than (%)"><input className={inputClass} inputMode="decimal" value={reject} onChange={(e) => setReject(e.target.value)} /></Field>
        <Field label="Wet wood is" className="sm:col-span-2">
          <select className={inputClass} value={rule} onChange={(e) => setRule(e.target.value)}>{lookups.moistureRules.map((x) => <option key={x} value={x}>{RULE_LABEL[x] ?? x}</option>)}</select>
        </Field>
      </div>

      {rule === 'DeductKg' ? (
        <Field label="Weight not paid for each point over the base (%)" hint={`At ${deduction}% a point, wood ${Number(base) + 8}% wet loses ${Math.round(Number(deduction) * 8 * 10) / 10}% of its weight.`}>
          <input className={cn(inputClass, 'max-w-40')} inputMode="decimal" value={deduction} onChange={(e) => setDeduction(e.target.value)} />
        </Field>
      ) : (
        <fieldset className="space-y-2 rounded-lg border border-slate-200 p-3">
          <legend className="px-1 text-sm font-semibold text-slate-800">Price by moisture</legend>
          {bands.map((band, index) => (
            <div key={index} className="flex flex-wrap items-center gap-2 text-sm">
              <span>Up to</span>
              <input aria-label={`Band ${index + 1} moisture`} className={cn(inputClass, 'w-24')} inputMode="decimal" value={band.upToPct} onChange={(e) => setBands((current) => current.map((x, i) => (i === index ? { ...x, upToPct: Number(e.target.value) } : x)))} />
              <span>% moisture: paid at</span>
              <input aria-label={`Band ${index + 1} share of price`} className={cn(inputClass, 'w-24')} inputMode="decimal" value={band.percentOfPrice} onChange={(e) => setBands((current) => current.map((x, i) => (i === index ? { ...x, percentOfPrice: Number(e.target.value) } : x)))} />
              <span>% of the price</span>
              <button type="button" className={secondaryButton} onClick={() => setBands((current) => current.filter((_, i) => i !== index))}>Remove</button>
            </div>
          ))}
          <button type="button" className={secondaryButton} onClick={() => setBands((current) => [...current, { upToPct: Math.min(100, (current[current.length - 1]?.upToPct ?? 40) + 5), percentOfPrice: 50 }])}>Add a band</button>
        </fieldset>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Average the burn over (days)"><input className={inputClass} inputMode="numeric" value={window} onChange={(e) => setWindow(e.target.value)} /></Field>
        <Field label="Warn when cover falls below (days)"><input className={inputClass} inputMode="numeric" value={cover} onChange={(e) => setCover(e.target.value)} /></Field>
        <Field label="A new stack seasons for (days)"><input className={inputClass} inputMode="numeric" value={seasoning} onChange={(e) => setSeasoning(e.target.value)} /></Field>
        <Field label="Aim to burn (kg wood per kg made tea)"><input className={inputClass} inputMode="decimal" value={target} onChange={(e) => setTarget(e.target.value)} /></Field>
      </div>
      <button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}>Save settings</button>
    </div>
  )
}
