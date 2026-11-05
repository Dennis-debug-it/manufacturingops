import { useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, ArrowRightLeft, ClipboardList, FlaskConical, Package, Plus, Warehouse, Wrench } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { useAnyPermission, usePermission } from '../../../shared/hooks/usePermission'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { useSuppliers } from '../../inventory/purchaseOrdersApi'
import { useOrgNodes } from '../api'
import { Field, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { PartPicker, type PickedPart } from './PartPicker'
import {
  CHEMICAL_KINDS, FOOD_GRADES, STOCK_STATE_LABELS, UNIT_STATUS_LABELS,
  openAttachment, uploadAttachment, useChemicals, useMovements, useCount, useCounts, useLubricantLog, useRequisition, useRequisitions, useReorder, useSparePart, useSpareParts,
  useStock, useStoreBins, useStores, useStoresAction, useTransfers, useUnitHistory, useUnits, useValuation,
  type ReorderRow, type RequisitionStatus, type SavePartPayload, type SpareUnit, type StockRow, type StockState, type Store, type StoreTransfer,
} from './api'

const TABS = [
  { id: 'stock', label: 'Stock' }, { id: 'parts', label: 'Parts' }, { id: 'reorder', label: 'Reorder' }, { id: 'requisitions', label: 'Requisitions' },
  { id: 'transfers', label: 'Transfers' }, { id: 'counts', label: 'Counts' }, { id: 'repairables', label: 'Repairables' }, { id: 'chemicals', label: 'Chemicals' },
] as const
type Tab = (typeof TABS)[number]['id']

export const STOCK_TONES: Record<StockState, BadgeTone> = { Ok: 'success', Reorder: 'warning', BelowMin: 'danger', Out: 'danger', None: 'neutral' }
const REQ_TONES: Record<RequisitionStatus, BadgeTone> = { Draft: 'neutral', Submitted: 'info', Approved: 'success', Rejected: 'danger', Ordered: 'success', Cancelled: 'neutral' }
const TRANSFER_TONES: Record<StoreTransfer['status'], BadgeTone> = { Requested: 'info', Dispatched: 'warning', Received: 'success', Rejected: 'danger', Cancelled: 'neutral' }
const UNIT_TONES: Record<SpareUnit['status'], BadgeTone> = { InStore: 'success', Installed: 'info', AwaitingRepair: 'warning', AtRepair: 'warning', Scrapped: 'neutral' }

const tableShell = 'overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm'
const th = 'px-4 py-2'

/** Factory stores and spares: stock and bins, parts, reordering and buying, transfers, counts, repairables and chemicals. */
export function StoresPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'stock') as Tab
  return (
    <div className="space-y-4">
      <PageHeader title="Stores and spares" subtitle="Every part used on a job comes out of a store. Stock, reservations for planned work, reordering, transfers between factories, counts and repairable spares." />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'stock' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'stock' ? <StockTab /> : tab === 'parts' ? <PartsTab /> : tab === 'reorder' ? <ReorderTab /> : tab === 'requisitions' ? <RequisitionsTab />
        : tab === 'transfers' ? <TransfersTab /> : tab === 'counts' ? <CountsTab /> : tab === 'repairables' ? <RepairablesTab /> : <ChemicalsTab />}
    </div>
  )
}

function useFactories() {
  const nodes = useOrgNodes()
  return useMemo(() => (nodes.data ?? []).filter((x) => x.level === 'Factory' && x.isActive), [nodes.data])
}

async function attempt(fn: () => Promise<unknown>, message: string) {
  try {
    await fn()
    toast.success(message)
    return true
  } catch {
    // The API client shows the error.
    return false
  }
}

/* ── Stock ───────────────────────────────────────────────────────────────── */

function StockTab() {
  const stores = useStores()
  const canManage = usePermission('materials.manage')
  const canIssue = usePermission('materials.issue') || canManage
  const valuation = useValuation(canManage)
  const [storeId, setStoreId] = useState('')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const debounced = useDebounce(search, 250)
  const [storeDrawer, setStoreDrawer] = useState<Store | 'new' | null>(null)
  const [row, setRow] = useState<StockRow | null>(null)
  const [receiving, setReceiving] = useState(false)

  useEffect(() => {
    const first = stores.data?.[0]
    if (!storeId && first) setStoreId(first.id)
  }, [stores.data, storeId])

  const stock = useStock(storeId || null, debounced, filter)
  const store = stores.data?.find((x) => x.id === storeId)

  if (stores.isLoading) return <Spinner />
  if ((stores.data ?? []).length === 0) {
    return (
      <>
        <EmptyState icon={Warehouse} title="No stores yet" description="Set up each factory's stores room so parts can be received, reserved and issued to jobs."
          actionLabel={canManage ? 'Add a store' : undefined} onAction={canManage ? () => setStoreDrawer('new') : undefined} />
        <StoreDrawer store={storeDrawer} onClose={() => setStoreDrawer(null)} />
      </>
    )
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {(stores.data ?? []).map((s) => (
          <button key={s.id} type="button" onClick={() => setStoreId(s.id)}
            className={cn('rounded-[var(--radius-card)] border bg-white p-3 text-left shadow-sm', s.id === storeId ? 'border-[var(--app-primary)] ring-1 ring-[var(--app-primary)]' : 'border-slate-200')}>
            <span className="flex items-center justify-between text-sm font-semibold text-slate-900">{s.name}{s.isMain ? <Badge size="sm">Main</Badge> : null}</span>
            <span className="block text-xs text-slate-500">{s.factoryName} · {s.code}</span>
            <span className="mt-2 flex flex-wrap gap-x-3 text-xs text-slate-600">
              <span>{s.partsInStock} parts</span><span className="tabular-nums">{formatNumber(s.stockValue)}</span>
              {s.toReorder > 0 ? <span className="text-amber-700">{s.toReorder} to reorder</span> : null}
              {s.criticalOut > 0 ? <span className="font-medium text-rose-700">{s.criticalOut} critical out</span> : null}
            </span>
          </button>
        ))}
      </div>

      {valuation.data ? (
        <p className="text-sm text-slate-600">Stock value across your stores: <span className="font-semibold tabular-nums text-slate-900">{valuation.data.currency} {formatNumber(valuation.data.total)}</span>
          {valuation.data.categories.length ? <span className="text-xs text-slate-500"> · {valuation.data.categories.slice(0, 4).map((c) => `${c.category} ${formatNumber(c.value)}`).join(' · ')}</span> : null}</p>
      ) : null}

      <div className="flex flex-wrap items-end gap-2">
        <Field label="Search" className="w-64"><input className={inputClass} value={search} placeholder="Part, number, barcode or bin" onChange={(e) => setSearch(e.target.value)} /></Field>
        <Field label="Show" className="w-44">
          <select className={inputClass} value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="all">Everything</option><option value="low">To reorder</option><option value="critical">Critical spares</option><option value="reserved">Reserved for jobs</option>
          </select>
        </Field>
        <div className="ml-auto flex gap-2">
          {canIssue ? <button type="button" className={primaryButton} onClick={() => setReceiving(true)}><Plus className="h-4 w-4" />Receive</button> : null}
          {canManage ? <button type="button" className={secondaryButton} onClick={() => setStoreDrawer(store ?? 'new')}>Store settings</button> : null}
          {canManage ? <button type="button" className={secondaryButton} onClick={() => setStoreDrawer('new')}>Add store</button> : null}
        </div>
      </div>

      {stock.isLoading ? <Spinner /> : (stock.data ?? []).length === 0 ? <EmptyState icon={Package} title="Nothing here" description="Receive parts into this store to start." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[900px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr>
              <th className={th}>Part</th><th className={th}>Bin</th><th className={`${th} text-right`}>On hand</th><th className={`${th} text-right`}>Reserved</th><th className={`${th} text-right`}>Free</th>
              <th className={`${th} text-right`}>Min / reorder / max</th><th className={`${th} text-right`}>Avg cost</th><th className={`${th} text-right`}>Value</th><th className={th}>State</th>
            </tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(stock.data ?? []).map((x) => (
                <tr key={x.materialItemId} className="cursor-pointer hover:bg-slate-50" onClick={() => setRow(x)}>
                  <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{x.itemCode}</span> <span className="font-medium text-slate-900">{x.itemName}</span>
                    <span className="ml-1 inline-flex gap-1">{x.isCriticalSpare ? <Badge size="sm" tone="danger">Critical</Badge> : null}{x.isSerialised ? <Badge size="sm">Serialised</Badge> : null}</span></td>
                  <td className="px-4 py-2 text-slate-600">{x.bin ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(x.onHand)} <span className="text-xs text-slate-500">{x.unitOfMeasure}</span></td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{x.reserved ? formatNumber(x.reserved) : '—'}</td>
                  <td className="px-4 py-2 text-right font-medium tabular-nums">{formatNumber(x.available)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{formatNumber(x.minLevel)} / {formatNumber(x.reorderPoint)} / {x.maxLevel != null ? formatNumber(x.maxLevel) : '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{formatNumber(x.averageCost)}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(x.value)}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={STOCK_TONES[x.state]}>{STOCK_STATE_LABELS[x.state]}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <StoreDrawer store={storeDrawer} onClose={() => setStoreDrawer(null)} />
      {store ? <StockRowDrawer store={store} row={row} onClose={() => setRow(null)} /> : null}
      {store ? <ReceiveDrawer store={store} open={receiving} onClose={() => setReceiving(false)} /> : null}
    </div>
  )
}

function StoreDrawer({ store, onClose }: { store: Store | 'new' | null; onClose: () => void }) {
  const factories = useFactories()
  const action = useStoresAction()
  const bins = useStoreBins(store && store !== 'new' ? store.id : null)
  const [form, setForm] = useState({ factoryId: '', code: '', name: '', location: '', isMain: false, isActive: true })
  const [bin, setBin] = useState({ code: '', description: '' })
  useEffect(() => {
    if (store === 'new') setForm({ factoryId: factories[0]?.id ?? '', code: '', name: '', location: '', isMain: false, isActive: true })
    else if (store) setForm({ factoryId: store.factoryId, code: store.code, name: store.name, location: store.location ?? '', isMain: store.isMain, isActive: store.isActive })
  }, [store, factories])
  const save = async () => {
    const body = { ...form, location: form.location || null }
    if (await attempt(() => action.mutateAsync(store === 'new' ? { path: '', body } : { method: 'put', path: `/${(store as Store).id}`, body }), 'Store saved')) onClose()
  }
  const addBin = async () => {
    if (store && store !== 'new' && await attempt(() => action.mutateAsync({ path: `/${store.id}/bins`, body: { code: bin.code, description: bin.description || null } }), 'Bin added')) setBin({ code: '', description: '' })
  }
  return (
    <FormDrawer open={store !== null} title={store === 'new' ? 'New store' : store?.name ?? ''} onClose={onClose} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={!form.factoryId || !form.code || !form.name || action.isPending} onClick={() => void save()}>Save</button></div>
    )}>
      <div className="space-y-3">
        <Field label="Factory" required>
          <select className={inputClass} value={form.factoryId} onChange={(e) => setForm({ ...form, factoryId: e.target.value })}>
            <option value="">Choose a factory</option>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}
          </select>
        </Field>
        <div className="grid grid-cols-[8rem_1fr] gap-2">
          <Field label="Code" required><input className={inputClass} value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} /></Field>
          <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        </div>
        <Field label="Where"><input className={inputClass} value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} /></Field>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isMain} onChange={(e) => setForm({ ...form, isMain: e.target.checked })} />Main store: parts for the factory's jobs are issued from here</label>
        {store !== 'new' ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Open</label> : null}
        {store && store !== 'new' ? (
          <div className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Bins</h3>
            <p className="mb-2 text-xs text-slate-600">{(bins.data ?? []).map((b) => b.code).join(', ') || 'None yet.'}</p>
            <div className="grid grid-cols-[7rem_1fr_auto] items-end gap-2">
              <Field label="Code"><input className={inputClass} value={bin.code} placeholder="A-03-2" onChange={(e) => setBin({ ...bin, code: e.target.value })} /></Field>
              <Field label="Description"><input className={inputClass} value={bin.description} onChange={(e) => setBin({ ...bin, description: e.target.value })} /></Field>
              <button type="button" className={secondaryButton} disabled={!bin.code} onClick={() => void addBin()}>Add bin</button>
            </div>
          </div>
        ) : null}
      </div>
    </FormDrawer>
  )
}

function StockRowDrawer({ store, row, onClose }: { store: Store; row: StockRow | null; onClose: () => void }) {
  const canManage = usePermission('materials.manage')
  const action = useStoresAction()
  const bins = useStoreBins(store.id)
  const [levels, setLevels] = useState({ min: '0', reorder: '0', max: '', bin: '' })
  const [adjust, setAdjust] = useState({ onHand: '', reason: '' })
  useEffect(() => {
    if (row) {
      setLevels({ min: String(row.minLevel), reorder: String(row.reorderPoint), max: row.maxLevel?.toString() ?? '', bin: row.binId ?? '' })
      setAdjust({ onHand: String(row.onHand), reason: '' })
    }
  }, [row])
  if (!row) return null
  const saveLevels = () => attempt(() => action.mutateAsync({ method: 'put', path: `/${store.id}/stock/${row.materialItemId}`, body: { minLevel: Number(levels.min), reorderPoint: Number(levels.reorder), maxLevel: levels.max ? Number(levels.max) : null, binId: levels.bin || null } }), 'Levels saved')
  const saveAdjust = () => attempt(() => action.mutateAsync({ path: `/${store.id}/adjust`, body: { materialItemId: row.materialItemId, onHand: Number(adjust.onHand), reason: adjust.reason } }), 'Stock adjusted')
  return (
    <FormDrawer open title={`${row.itemName} · ${store.name}`} onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-slate-600">{formatNumber(row.onHand)} {row.unitOfMeasure} on hand, {formatNumber(row.reserved)} reserved for jobs, {formatNumber(row.available)} free. Average cost {formatNumber(row.averageCost)}.{row.lastCountedAt ? ` Last counted ${formatDate(row.lastCountedAt)}.` : ''}</p>
        {canManage ? (
          <section className="space-y-2">
            <h3 className="text-sm font-semibold text-slate-800">Levels</h3>
            <div className="grid grid-cols-3 gap-2">
              <Field label="Minimum"><input type="number" min={0} className={inputClass} value={levels.min} onChange={(e) => setLevels({ ...levels, min: e.target.value })} /></Field>
              <Field label="Reorder at"><input type="number" min={0} className={inputClass} value={levels.reorder} onChange={(e) => setLevels({ ...levels, reorder: e.target.value })} /></Field>
              <Field label="Maximum"><input type="number" min={0} className={inputClass} value={levels.max} onChange={(e) => setLevels({ ...levels, max: e.target.value })} /></Field>
            </div>
            <Field label="Bin">
              <select className={inputClass} value={levels.bin} onChange={(e) => setLevels({ ...levels, bin: e.target.value })}>
                <option value="">No bin</option>{(bins.data ?? []).map((b) => <option key={b.id} value={b.id}>{b.code}</option>)}
              </select>
            </Field>
            <button type="button" className={secondaryButton} onClick={() => void saveLevels()}>Save levels</button>
          </section>
        ) : null}
        {canManage && !row.isSerialised ? (
          <section className="space-y-2 border-t border-slate-100 pt-3">
            <h3 className="text-sm font-semibold text-slate-800">Adjust stock</h3>
            <div className="grid grid-cols-[8rem_1fr] gap-2">
              <Field label="On hand now"><input type="number" min={0} className={inputClass} value={adjust.onHand} onChange={(e) => setAdjust({ ...adjust, onHand: e.target.value })} /></Field>
              <Field label="Reason" required><input className={inputClass} value={adjust.reason} placeholder="Damaged, found, written off…" onChange={(e) => setAdjust({ ...adjust, reason: e.target.value })} /></Field>
            </div>
            <button type="button" className={secondaryButton} disabled={!adjust.reason.trim()} onClick={() => void saveAdjust()}>Adjust</button>
          </section>
        ) : null}
        <MovementsList storeId={store.id} materialItemId={row.materialItemId} />
      </div>
    </FormDrawer>
  )
}

function MovementsList({ storeId, materialItemId }: { storeId?: string; materialItemId: string }) {
  const { data } = useMovementsFor(storeId, materialItemId)
  return (
    <section className="border-t border-slate-100 pt-3">
      <h3 className="mb-2 text-sm font-semibold text-slate-800">Recent movements</h3>
      {(data ?? []).length === 0 ? <p className="text-sm text-slate-500">None yet.</p> : (
        <ul className="space-y-1 text-sm">
          {(data ?? []).slice(0, 25).map((m) => (
            <li key={m.id} className="flex justify-between gap-2"><span>{m.type} <span className={m.change < 0 ? 'text-rose-700' : 'text-emerald-700'}>{m.change > 0 ? '+' : ''}{formatNumber(m.change)}</span>{m.reference ? <span className="text-xs text-slate-500"> · {m.reference}</span> : null}{m.reason ? <span className="text-xs text-slate-500"> · {m.reason}</span> : null}</span>
              <span className="shrink-0 text-xs text-slate-500">{m.storeName} · {formatDateTime(m.at)}</span></li>
          ))}
        </ul>
      )}
    </section>
  )
}

function useMovementsFor(storeId: string | undefined, materialItemId: string) {
  return useMovements({ storeId: storeId ?? null, materialItemId })
}

function ReceiveDrawer({ store, open, onClose }: { store: Store; open: boolean; onClose: () => void }) {
  const action = useStoresAction()
  const part = useState<PickedPart | null>(null)
  const [picked, setPicked] = part
  const detail = useSparePart(picked?.id ?? null)
  const [form, setForm] = useState({ quantity: '', unitCost: '', currency: '', reference: '', serials: '', inPurchaseUnits: false })
  const close = () => {
    setPicked(null)
    setForm({ quantity: '', unitCost: '', currency: '', reference: '', serials: '', inPurchaseUnits: false })
    onClose()
  }
  const serialList = form.serials.split(/[\n,]/).map((x) => x.trim()).filter(Boolean)
  const submit = async () => {
    if (!picked) return
    const quantity = picked.isSerialised ? serialList.length : Number(form.quantity)
    if (await attempt(() => action.mutateAsync({ path: `/${store.id}/receive`, body: {
      materialItemId: picked.id, quantity, unitCost: form.unitCost ? Number(form.unitCost) : null, currency: form.currency || null, reference: form.reference || null,
      serialNumbers: picked.isSerialised ? serialList : null, inPurchaseUnits: form.inPurchaseUnits,
    } }), 'Received')) close()
  }
  const purchaseUnit = detail.data?.purchaseUnitOfMeasure
  return (
    <FormDrawer open={open} title={`Receive into ${store.name}`} onClose={close} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={close}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!picked || action.isPending || (picked.isSerialised ? serialList.length === 0 : !(Number(form.quantity) > 0))} onClick={() => void submit()}>Receive</button></div>
    )}>
      <div className="space-y-3">
        <p className="text-xs text-slate-600">For stock bought on a purchase order, receive it from the requisition instead so the order is updated.</p>
        <Field label="Part" required><PartPicker value={picked} onChange={setPicked} /></Field>
        {picked?.isSerialised ? (
          <Field label="Serial numbers" required hint="One per line"><textarea className={textareaClass} value={form.serials} onChange={(e) => setForm({ ...form, serials: e.target.value })} /></Field>
        ) : (
          <div className="grid grid-cols-2 gap-2">
            <Field label={`Quantity (${form.inPurchaseUnits && purchaseUnit ? purchaseUnit : picked?.unitOfMeasure ?? 'units'})`} required><input type="number" min={0} step="any" className={inputClass} value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} /></Field>
            {purchaseUnit ? <label className="mt-6 flex items-center gap-2 text-sm"><input type="checkbox" checked={form.inPurchaseUnits} onChange={(e) => setForm({ ...form, inPurchaseUnits: e.target.checked })} />In {purchaseUnit}s of {detail.data?.purchaseUnitFactor}</label> : null}
          </div>
        )}
        <div className="grid grid-cols-[1fr_6rem] gap-2">
          <Field label="Unit cost" hint="Blank: at the current average"><input type="number" min={0} step="any" className={inputClass} value={form.unitCost} onChange={(e) => setForm({ ...form, unitCost: e.target.value })} /></Field>
          <Field label="Currency"><input className={inputClass} maxLength={3} placeholder="KES" value={form.currency} onChange={(e) => setForm({ ...form, currency: e.target.value.toUpperCase() })} /></Field>
        </div>
        <Field label="Reference" hint="Delivery note or invoice number"><input className={inputClass} value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Parts ───────────────────────────────────────────────────────────────── */

function PartsTab() {
  const canManage = usePermission('materials.manage')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState('all')
  const debounced = useDebounce(search, 250)
  const parts = useSpareParts(debounced, filter)
  const [open, setOpen] = useState<string | 'new' | null>(null)
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Search" className="w-64"><input className={inputClass} value={search} onChange={(e) => setSearch(e.target.value)} /></Field>
        <Field label="Show" className="w-44">
          <select className={inputClass} value={filter} onChange={(e) => setFilter(e.target.value)}>
            <option value="all">All parts</option><option value="critical">Critical spares</option><option value="serialised">Serialised</option><option value="repairable">Repairable</option><option value="chemical">Chemicals</option>
          </select>
        </Field>
        {canManage ? <button type="button" className={cn(primaryButton, 'ml-auto')} onClick={() => setOpen('new')}><Plus className="h-4 w-4" />New part</button> : null}
      </div>
      {parts.isLoading ? <Spinner /> : (parts.data ?? []).length === 0 ? <EmptyState icon={Package} title="No parts" description="Add the spares the factory keeps." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Part</th><th className={th}>Category</th><th className={`${th} text-right`}>Group stock</th><th className={`${th} text-right`}>Free</th><th className={`${th} text-right`}>Stores</th><th className={`${th} text-right`}>Last cost</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(parts.data ?? []).map((p) => (
                <tr key={p.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(p.id)}>
                  <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{p.itemCode}</span> <span className="font-medium text-slate-900">{p.itemName}</span>
                    <span className="ml-1 inline-flex gap-1">{p.isCriticalSpare || p.onCriticalAsset ? <Badge size="sm" tone="danger">{p.isCriticalSpare ? 'Critical' : 'On A-class asset'}</Badge> : null}{p.isSerialised ? <Badge size="sm">Serialised</Badge> : null}{p.isRepairable ? <Badge size="sm" tone="info">Repairable</Badge> : null}{p.isChemical ? <Badge size="sm" tone="warning">Chemical</Badge> : null}</span></td>
                  <td className="px-4 py-2 text-slate-600">{p.category ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(p.groupOnHand)} <span className="text-xs text-slate-500">{p.unitOfMeasure}</span></td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(p.groupOnHand - p.groupReserved)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{p.storesHolding}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{p.unitCost != null ? formatNumber(p.unitCost) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <PartDrawer id={open} onClose={() => setOpen(null)} />
    </div>
  )
}

const emptyPart: SavePartPayload = { itemCode: '', itemName: '', unitOfMeasure: 'each', category: '', purchaseUnitOfMeasure: '', purchaseUnitFactor: 1, barcode: '', reorderQuantity: null, isCriticalSpare: false, isSerialised: false, isRepairable: false, isChemical: false, chemicalKind: null, hazardClass: '', foodGradeClass: null, sdsAttachmentId: null, sdsIssuedOn: null, externalId: '', externalSystem: '' }

function PartDrawer({ id, onClose }: { id: string | 'new' | null; onClose: () => void }) {
  const canManage = usePermission('materials.manage')
  const detail = useSparePart(id && id !== 'new' ? id : null)
  const suppliers = useSuppliers()
  const action = useStoresAction()
  const [form, setForm] = useState<SavePartPayload>(emptyPart)
  const [price, setPrice] = useState({ supplierId: '', unitPrice: '', currency: '', vendorPartNumber: '', leadTimeDays: '', isPreferred: false })
  const [uploading, setUploading] = useState(false)
  useEffect(() => {
    if (id === 'new') setForm(emptyPart)
    else if (detail.data) {
      const d = detail.data
      setForm({ itemCode: d.itemCode, itemName: d.itemName, unitOfMeasure: d.unitOfMeasure, category: d.category ?? '', purchaseUnitOfMeasure: d.purchaseUnitOfMeasure ?? '', purchaseUnitFactor: d.purchaseUnitFactor, barcode: d.barcode ?? '',
        preferredSupplierId: d.preferredSupplierId, isCriticalSpare: d.isCriticalSpare, isSerialised: d.isSerialised, isRepairable: d.isRepairable, isChemical: d.isChemical, chemicalKind: d.chemicalKind, hazardClass: d.hazardClass ?? '',
        foodGradeClass: d.foodGradeClass, sdsAttachmentId: d.sdsAttachmentId, sdsIssuedOn: d.sdsIssuedOn?.slice(0, 10) ?? null, externalId: d.externalId ?? '', externalSystem: d.externalSystem ?? '' })
    }
  }, [id, detail.data])
  const set = (patch: Partial<SavePartPayload>) => setForm((f) => ({ ...f, ...patch }))
  const save = async () => {
    const body = { ...form, category: form.category || null, purchaseUnitOfMeasure: form.purchaseUnitOfMeasure || null, barcode: form.barcode || null, hazardClass: form.hazardClass || null, externalId: form.externalId || null, externalSystem: form.externalSystem || null, sdsIssuedOn: form.sdsIssuedOn || null }
    if (await attempt(() => action.mutateAsync(id === 'new' ? { path: '/parts', body } : { method: 'put', path: `/parts/${id}`, body }), 'Part saved') && id === 'new') onClose()
  }
  const addPrice = async () => {
    if (!detail.data) return
    if (await attempt(() => action.mutateAsync({ path: '/catalogue', body: { supplierId: price.supplierId, materialItemId: detail.data!.id, unitPrice: Number(price.unitPrice), currency: price.currency || null, vendorPartNumber: price.vendorPartNumber || null, leadTimeDays: price.leadTimeDays ? Number(price.leadTimeDays) : null, isPreferred: price.isPreferred } }), 'Price added'))
      setPrice({ supplierId: '', unitPrice: '', currency: '', vendorPartNumber: '', leadTimeDays: '', isPreferred: false })
  }
  const uploadSds = async (file: File | undefined) => {
    if (!file || !detail.data) return
    setUploading(true)
    try {
      const attachmentId = await uploadAttachment('MaterialItem', detail.data.id, file)
      set({ sdsAttachmentId: attachmentId, sdsIssuedOn: form.sdsIssuedOn ?? new Date().toISOString().slice(0, 10) })
      toast.success('Safety data sheet uploaded. Save to keep it.')
    } catch {
      // The API client shows the error.
    } finally {
      setUploading(false)
    }
  }
  const d = detail.data
  return (
    <FormDrawer open={id !== null} title={id === 'new' ? 'New part' : d ? `${d.itemCode} ${d.itemName}` : ''} onClose={onClose} footer={canManage ? (
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Close</button><button type="button" className={primaryButton} disabled={!form.itemCode || !form.itemName || action.isPending} onClick={() => void save()}>Save part</button></div>
    ) : undefined}>
      <div className="space-y-4">
        {d ? (
          <section>
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Stock across the group</h3>
            {d.stores.length === 0 ? <p className="text-sm text-slate-500">Not stocked anywhere.</p> : (
              <ul className="space-y-1 text-sm">{d.stores.map((s) => <li key={s.storeId} className="flex justify-between gap-2"><span>{s.factoryName} · {s.storeName}{s.bin ? <span className="text-xs text-slate-500"> ({s.bin})</span> : null}</span><span className="tabular-nums">{formatNumber(s.available)} free of {formatNumber(s.onHand)}</span></li>)}</ul>
            )}
            {d.whereUsed.length > 0 ? <p className="mt-2 text-xs text-slate-600">Fitted to: {d.whereUsed.map((w) => `${w.assetCode}${w.criticality === 'A' ? ' (A)' : ''}${w.position ? ` ${w.position}` : ''} ×${w.quantityFitted}`).join(', ')}</p> : null}
            {d.onCriticalAsset && !d.isCriticalSpare ? <p className="mt-1 text-xs text-amber-700">On an A-class asset: consider flagging it critical so running short raises an alert.</p> : null}
          </section>
        ) : null}

        <fieldset disabled={!canManage} className="space-y-3 border-t border-slate-100 pt-3">
          <div className="grid grid-cols-[9rem_1fr] gap-2">
            <Field label="Part number" required><input className={inputClass} value={form.itemCode} onChange={(e) => set({ itemCode: e.target.value })} /></Field>
            <Field label="Name" required><input className={inputClass} value={form.itemName} onChange={(e) => set({ itemName: e.target.value })} /></Field>
          </div>
          <div className="grid grid-cols-3 gap-2">
            <Field label="Unit" required><input className={inputClass} value={form.unitOfMeasure} onChange={(e) => set({ unitOfMeasure: e.target.value })} /></Field>
            <Field label="Bought in"><input className={inputClass} placeholder="drum, box…" value={form.purchaseUnitOfMeasure ?? ''} onChange={(e) => set({ purchaseUnitOfMeasure: e.target.value })} /></Field>
            <Field label="Units per"><input type="number" min={0} step="any" className={inputClass} value={form.purchaseUnitFactor ?? 1} onChange={(e) => set({ purchaseUnitFactor: Number(e.target.value) })} /></Field>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Field label="Category"><input className={inputClass} value={form.category ?? ''} onChange={(e) => set({ category: e.target.value })} /></Field>
            <Field label="Barcode"><input className={inputClass} value={form.barcode ?? ''} onChange={(e) => set({ barcode: e.target.value })} /></Field>
          </div>
          <Field label="Usual supplier">
            <select className={inputClass} value={form.preferredSupplierId ?? ''} onChange={(e) => set({ preferredSupplierId: e.target.value || null })}>
              <option value="">None</option>{(suppliers.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}
            </select>
          </Field>
          <div className="flex flex-wrap gap-x-4 gap-y-2 text-sm">
            <label className="flex items-center gap-2"><input type="checkbox" checked={form.isCriticalSpare} onChange={(e) => set({ isCriticalSpare: e.target.checked })} />Critical spare</label>
            <label className="flex items-center gap-2"><input type="checkbox" checked={form.isSerialised} onChange={(e) => set({ isSerialised: e.target.checked })} />Tracked by serial number</label>
            <label className="flex items-center gap-2"><input type="checkbox" checked={form.isRepairable} onChange={(e) => set({ isRepairable: e.target.checked })} />Repairable</label>
            <label className="flex items-center gap-2"><input type="checkbox" checked={form.isChemical} onChange={(e) => set({ isChemical: e.target.checked, chemicalKind: e.target.checked ? form.chemicalKind ?? 'Lubricant' : null })} />Chemical</label>
          </div>
          {form.isChemical ? (
            <div className="space-y-2 rounded-lg bg-amber-50 p-3">
              <div className="grid grid-cols-2 gap-2">
                <Field label="Kind"><select className={inputClass} value={form.chemicalKind ?? ''} onChange={(e) => set({ chemicalKind: e.target.value })}>{CHEMICAL_KINDS.map((k) => <option key={k}>{k}</option>)}</select></Field>
                {form.chemicalKind === 'Lubricant' ? <Field label="Food grade (NSF)"><select className={inputClass} value={form.foodGradeClass ?? ''} onChange={(e) => set({ foodGradeClass: e.target.value || null })}><option value="">Not food grade</option>{FOOD_GRADES.map((g) => <option key={g}>{g}</option>)}</select></Field> : null}
              </div>
              <Field label="Hazard class"><input className={inputClass} value={form.hazardClass ?? ''} onChange={(e) => set({ hazardClass: e.target.value })} /></Field>
              <div className="grid grid-cols-2 gap-2">
                <Field label="Safety data sheet">{d ? <input type="file" accept=".pdf,image/*" disabled={uploading} className="text-sm" onChange={(e) => void uploadSds(e.target.files?.[0])} /> : <span className="text-xs text-slate-500">Save the part first</span>}</Field>
                <Field label="SDS issued"><input type="date" className={inputClass} value={form.sdsIssuedOn ?? ''} onChange={(e) => set({ sdsIssuedOn: e.target.value || null })} /></Field>
              </div>
              {form.sdsAttachmentId ? <button type="button" className="text-xs text-[var(--app-primary)] underline" onClick={() => void openAttachment(form.sdsAttachmentId!)}>Open the safety data sheet</button> : null}
            </div>
          ) : null}
          <div className="grid grid-cols-2 gap-2">
            <Field label="ERP id"><input className={inputClass} value={form.externalId ?? ''} onChange={(e) => set({ externalId: e.target.value })} /></Field>
            <Field label="ERP system"><input className={inputClass} value={form.externalSystem ?? ''} onChange={(e) => set({ externalSystem: e.target.value })} /></Field>
          </div>
        </fieldset>

        {d ? (
          <section className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Supplier prices</h3>
            {d.prices.length === 0 ? <p className="text-sm text-slate-500">None yet.</p> : (
              <ul className="space-y-1 text-sm">{d.prices.map((p) => <li key={p.id} className="flex justify-between gap-2"><span>{p.supplierName}{p.vendorPartNumber ? <span className="text-xs text-slate-500"> · {p.vendorPartNumber}</span> : null}{p.isPreferred ? <Badge size="sm" tone="success">Preferred</Badge> : null}</span><span className="tabular-nums">{p.currency} {formatNumber(p.unitPrice)}{p.leadTimeDays ? <span className="text-xs text-slate-500"> · {p.leadTimeDays} d</span> : null}</span></li>)}</ul>
            )}
            {canManage ? (
              <div className="mt-2 grid grid-cols-2 gap-2">
                <Field label="Supplier"><select className={inputClass} value={price.supplierId} onChange={(e) => setPrice({ ...price, supplierId: e.target.value })}><option value="">Choose</option>{(suppliers.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}</select></Field>
                <Field label="Their part number"><input className={inputClass} value={price.vendorPartNumber} onChange={(e) => setPrice({ ...price, vendorPartNumber: e.target.value })} /></Field>
                <Field label="Price"><input type="number" min={0} step="any" className={inputClass} value={price.unitPrice} onChange={(e) => setPrice({ ...price, unitPrice: e.target.value })} /></Field>
                <div className="grid grid-cols-2 gap-2">
                  <Field label="Currency"><input className={inputClass} maxLength={3} placeholder="KES" value={price.currency} onChange={(e) => setPrice({ ...price, currency: e.target.value.toUpperCase() })} /></Field>
                  <Field label="Lead days"><input type="number" min={0} className={inputClass} value={price.leadTimeDays} onChange={(e) => setPrice({ ...price, leadTimeDays: e.target.value })} /></Field>
                </div>
                <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={price.isPreferred} onChange={(e) => setPrice({ ...price, isPreferred: e.target.checked })} />Preferred</label>
                <button type="button" className={secondaryButton} disabled={!price.supplierId || !price.unitPrice} onClick={() => void addPrice()}>Add price</button>
              </div>
            ) : null}
          </section>
        ) : null}

        {d && d.units.length > 0 ? (
          <section className="border-t border-slate-100 pt-3">
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Units</h3>
            <ul className="space-y-1 text-sm">{d.units.map((u) => <li key={u.id} className="flex justify-between"><span className="font-mono">{u.serialNumber}</span><span><Badge size="sm" tone={UNIT_TONES[u.status]}>{UNIT_STATUS_LABELS[u.status]}</Badge> <span className="text-xs text-slate-500">{u.assetCode ?? u.storeName ?? u.repairSupplierName ?? ''}</span></span></li>)}</ul>
          </section>
        ) : null}
        {d ? <MovementsList materialItemId={d.id} /> : null}
      </div>
    </FormDrawer>
  )
}

/* ── Reorder ─────────────────────────────────────────────────────────────── */

function ReorderTab() {
  const factories = useFactories()
  const [factoryId, setFactoryId] = useState('')
  const rows = useReorder(factoryId)
  const canRequest = useAnyPermission('materials.request', 'materials.manage', 'work-orders.assign')
  const canTransfer = useAnyPermission('materials.issue', 'materials.manage')
  const action = useStoresAction()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [, setParams] = useSearchParams()
  const key = (r: ReorderRow) => `${r.storeId}:${r.materialItemId}`
  const chosen = (rows.data ?? []).filter((r) => selected.has(key(r)))
  const oneFactory = new Set(chosen.map((r) => r.factoryId)).size === 1

  const raise = async () => {
    const first = chosen[0]
    if (!first) return
    const lines = chosen.map((r) => ({ materialItemId: r.materialItemId, quantity: Math.max(r.suggestedQuantity - r.onRequisition, 1), supplierId: r.supplierId, unitPrice: r.unitPrice, currency: r.currency }))
    if (await attempt(() => action.mutateAsync({ path: '/requisitions', body: { factoryId: first.factoryId, storeId: first.storeId, reason: 'Below reorder point', lines } }), 'Requisition drafted')) {
      setSelected(new Set())
      setParams({ tab: 'requisitions' }, { replace: true })
    }
  }

  const borrow = (r: ReorderRow) => attempt(() => action.mutateAsync({ path: '/transfers', body: { fromStoreId: r.donorStoreId, toStoreId: r.storeId, materialItemId: r.materialItemId, quantity: Math.min(r.donorSpare ?? 0, Math.max(r.reorderPoint - r.available + 1, 1)), reason: 'Below reorder point' } }), 'Transfer requested')

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Factory" className="w-56"><select className={inputClass} value={factoryId} onChange={(e) => setFactoryId(e.target.value)}><option value="">All my factories</option>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
        {canRequest ? <button type="button" className={cn(primaryButton, 'ml-auto')} disabled={chosen.length === 0 || !oneFactory} onClick={() => void raise()}><ClipboardList className="h-4 w-4" />Raise requisition ({chosen.length})</button> : null}
      </div>
      {chosen.length > 0 && !oneFactory ? <p className="text-xs text-amber-700">Choose lines from one factory per requisition.</p> : null}
      {rows.isLoading ? <Spinner /> : (rows.data ?? []).length === 0 ? <EmptyState icon={Package} title="Nothing to reorder" description="Every part is above its reorder point." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[980px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="w-8 px-3 py-2" /><th className={th}>Part</th><th className={th}>Store</th><th className={`${th} text-right`}>Free</th><th className={`${th} text-right`}>Reorder at</th><th className={`${th} text-right`}>Order</th><th className={th}>Best price</th><th className={th}>Already coming</th><th className={th}>Elsewhere</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(rows.data ?? []).map((r) => (
                <tr key={key(r)}>
                  <td className="px-3 py-2"><input type="checkbox" aria-label={`Select ${r.itemName}`} checked={selected.has(key(r))} onChange={(e) => setSelected((s) => { const n = new Set(s); if (e.target.checked) n.add(key(r)); else n.delete(key(r)); return n })} /></td>
                  <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{r.itemCode}</span> {r.itemName} {r.isCriticalSpare ? <Badge size="sm" tone="danger">Critical</Badge> : null} <Badge size="sm" tone={STOCK_TONES[r.state]}>{STOCK_STATE_LABELS[r.state]}</Badge></td>
                  <td className="px-4 py-2 text-slate-600">{r.storeName}<span className="block text-xs text-slate-500">{r.factoryName}</span></td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(r.available)}</td>
                  <td className="px-4 py-2 text-right tabular-nums text-slate-600">{formatNumber(r.reorderPoint)}</td>
                  <td className="px-4 py-2 text-right font-medium tabular-nums">{formatNumber(r.suggestedQuantity)} <span className="text-xs text-slate-500">{r.unitOfMeasure}</span></td>
                  <td className="px-4 py-2 text-slate-600">{r.supplierName ? <>{r.supplierName}<span className="block text-xs text-slate-500">{r.currency} {formatNumber(r.unitPrice)}{r.leadTimeDays ? ` · ${r.leadTimeDays} d` : ''}</span></> : '—'}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{r.onRequisition ? `${formatNumber(r.onRequisition)} requisitioned` : ''}{r.onRequisition && r.onOrder ? ' · ' : ''}{r.onOrder ? `${formatNumber(r.onOrder)} on order` : ''}{!r.onRequisition && !r.onOrder ? '—' : ''}</td>
                  <td className="px-4 py-2 text-xs">{r.donorStoreName ? <>{r.donorStoreName}: {formatNumber(r.donorSpare)} spare {canTransfer ? <button type="button" className="ml-1 font-medium text-[var(--app-primary)] underline" onClick={() => void borrow(r)}>Borrow</button> : null}</> : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}

/* ── Requisitions ────────────────────────────────────────────────────────── */

function RequisitionsTab() {
  const [status, setStatus] = useState('open')
  const list = useRequisitions(status)
  const [open, setOpen] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const canRequest = useAnyPermission('materials.request', 'materials.manage', 'work-orders.assign')
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Show" className="w-44"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="open">Open</option><option value="Ordered">Ordered</option><option value="Rejected">Rejected</option><option value="">All</option></select></Field>
        {canRequest ? <button type="button" className={cn(primaryButton, 'ml-auto')} onClick={() => setCreating(true)}><Plus className="h-4 w-4" />New requisition</button> : null}
      </div>
      {list.isLoading ? <Spinner /> : (list.data ?? []).length === 0 ? <EmptyState icon={ClipboardList} title="No requisitions" description="Raise one from the reorder list, or start one here." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Number</th><th className={th}>Factory</th><th className={th}>Reason</th><th className={`${th} text-right`}>Lines</th><th className={`${th} text-right`}>Value</th><th className={th}>Raised</th><th className={th}>Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(list.data ?? []).map((r) => (
                <tr key={r.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(r.id)}>
                  <td className="px-4 py-2 font-mono text-xs">{r.requisitionNumber}</td>
                  <td className="px-4 py-2">{r.factoryName}{r.storeName ? <span className="block text-xs text-slate-500">{r.storeName}</span> : null}</td>
                  <td className="px-4 py-2 text-slate-600">{r.reason ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{r.lines}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{r.totals.map((t) => `${t.currency} ${formatNumber(t.amount)}`).join(' + ') || '—'}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{r.requestedByName} · {formatDate(r.createdAt)}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={REQ_TONES[r.status]}>{r.status}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <RequisitionDrawer id={open} onClose={() => setOpen(null)} />
      <NewRequisitionDrawer open={creating} onClose={() => setCreating(false)} />
    </div>
  )
}

function NewRequisitionDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const factories = useFactories()
  const stores = useStores()
  const suppliers = useSuppliers()
  const action = useStoresAction()
  const [factoryId, setFactoryId] = useState('')
  const [storeId, setStoreId] = useState('')
  const [reason, setReason] = useState('')
  const [neededBy, setNeededBy] = useState('')
  const [lines, setLines] = useState<{ part: PickedPart | null; description: string; quantity: string; supplierId: string }[]>([{ part: null, description: '', quantity: '1', supplierId: '' }])
  const close = () => {
    setFactoryId(''); setStoreId(''); setReason(''); setNeededBy(''); setLines([{ part: null, description: '', quantity: '1', supplierId: '' }])
    onClose()
  }
  const submit = async () => {
    const body = { factoryId, storeId: storeId || null, reason: reason || null, neededBy: neededBy || null, lines: lines.filter((l) => l.part || l.description).map((l) => ({ materialItemId: l.part?.id ?? null, description: l.description || null, quantity: Number(l.quantity), supplierId: l.supplierId || null })) }
    if (await attempt(() => action.mutateAsync({ path: '/requisitions', body }), 'Requisition drafted')) close()
  }
  return (
    <FormDrawer open={open} title="New requisition" onClose={close} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={close}>Cancel</button><button type="button" className={primaryButton} disabled={!factoryId || action.isPending} onClick={() => void submit()}>Save draft</button></div>
    )}>
      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-2">
          <Field label="Factory" required><select className={inputClass} value={factoryId} onChange={(e) => { setFactoryId(e.target.value); setStoreId('') }}><option value="">Choose</option>{factories.map((f) => <option key={f.id} value={f.id}>{f.name}</option>)}</select></Field>
          <Field label="For store"><select className={inputClass} value={storeId} onChange={(e) => setStoreId(e.target.value)}><option value="">—</option>{(stores.data ?? []).filter((s) => s.factoryId === factoryId).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
        </div>
        <div className="grid grid-cols-[1fr_10rem] gap-2">
          <Field label="Reason"><input className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
          <Field label="Needed by"><input type="date" className={inputClass} value={neededBy} onChange={(e) => setNeededBy(e.target.value)} /></Field>
        </div>
        {lines.map((line, i) => (
          <div key={i} className="space-y-2 rounded-lg border border-slate-200 p-2">
            <PartPicker value={line.part} onChange={(part) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, part } : l)))} />
            {!line.part ? <input className={inputClass} placeholder="…or describe a non-stock item" value={line.description} onChange={(e) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, description: e.target.value } : l)))} /> : null}
            <div className="grid grid-cols-[6rem_1fr] gap-2">
              <input type="number" min={0} step="any" aria-label="Quantity" className={inputClass} value={line.quantity} onChange={(e) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, quantity: e.target.value } : l)))} />
              <select aria-label="Supplier" className={inputClass} value={line.supplierId} onChange={(e) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, supplierId: e.target.value } : l)))}><option value="">Supplier (choose before ordering)</option>{(suppliers.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}</select>
            </div>
          </div>
        ))}
        <button type="button" className={secondaryButton} onClick={() => setLines((ls) => [...ls, { part: null, description: '', quantity: '1', supplierId: '' }])}>Add line</button>
      </div>
    </FormDrawer>
  )
}

function RequisitionDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const detail = useRequisition(id)
  const action = useStoresAction()
  const canApprove = usePermission('materials.approve')
  const canOrder = usePermission('materials.manage')
  const canReceive = usePermission('materials.issue') || canOrder
  const canRequest = useAnyPermission('materials.request', 'materials.manage', 'work-orders.assign')
  const [reason, setReason] = useState('')
  const [receipt, setReceipt] = useState<Record<string, string>>({})
  const d = detail.data
  const act = (verb: string, message: string, body?: unknown) => attempt(() => action.mutateAsync({ path: `/requisitions/${id}/${verb}`, body }), message)
  const receive = async (poId: string, lines: { id: string; outstanding: number }[]) => {
    const storeId = d?.requisition.storeId
    if (!storeId) return toast.error('Choose the store on the requisition first.')
    const payload = lines.map((l) => ({ lineId: l.id, quantity: Number(receipt[l.id] ?? l.outstanding) })).filter((l) => l.quantity > 0)
    if (await attempt(() => action.mutateAsync({ path: `/api/factory/purchase-orders/${poId}/receive`, body: { storeId, lines: payload } }), 'Received into stores')) setReceipt({})
  }
  const status = d?.requisition.status
  return (
    <FormDrawer open={id !== null} title={d ? `${d.requisition.requisitionNumber} · ${d.requisition.factoryName}` : ''} onClose={onClose} footer={d ? (
      <div className="flex flex-wrap justify-end gap-2">
        {status === 'Draft' && canRequest ? <button type="button" className={primaryButton} onClick={() => void act('submit', 'Submitted for approval')}>Submit</button> : null}
        {status === 'Submitted' && canApprove ? <><button type="button" className={secondaryButton} disabled={!reason.trim()} onClick={() => void act('reject', 'Rejected', { reason })}>Reject</button><button type="button" className={primaryButton} onClick={() => void act('approve', 'Approved')}>Approve</button></> : null}
        {status === 'Approved' && canOrder ? <button type="button" className={primaryButton} onClick={() => void act('order', 'Purchase orders raised')}>Raise purchase orders</button> : null}
        {status && ['Draft', 'Submitted', 'Approved'].includes(status) && canRequest ? <button type="button" className={secondaryButton} onClick={() => void act('cancel', 'Cancelled')}>Cancel requisition</button> : null}
      </div>
    ) : undefined}>
      {!d ? <Spinner /> : (
        <div className="space-y-4">
          <p className="text-sm text-slate-600"><Badge size="sm" tone={REQ_TONES[d.requisition.status]}>{d.requisition.status}</Badge> Raised by {d.requisition.requestedByName} on {formatDate(d.requisition.createdAt)}{d.requisition.neededBy ? `, needed by ${formatDate(d.requisition.neededBy)}` : ''}.{d.requisition.decidedByName ? ` Decided by ${d.requisition.decidedByName}.` : ''}</p>
          {d.rejectReason ? <p className="rounded-lg bg-rose-50 p-2 text-sm text-rose-800">{d.rejectReason}</p> : null}
          <ul className="divide-y divide-slate-100 text-sm">
            {d.lines.map((l) => <li key={l.id} className="flex justify-between gap-2 py-1.5"><span>{l.itemCode ? <span className="font-mono text-xs text-slate-500">{l.itemCode} </span> : null}{l.description} × {formatNumber(l.quantity)}<span className="block text-xs text-slate-500">{l.supplierName ?? 'No supplier yet'}</span></span><span className="tabular-nums">{l.unitPrice != null ? `${l.currency} ${formatNumber(l.unitPrice * l.quantity)}` : '—'}</span></li>)}
          </ul>
          {status === 'Submitted' && canApprove ? <Field label="Reason, if rejecting"><input className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)} /></Field> : null}
          {d.purchaseOrders.map((po) => {
            const outstanding = po.lines.map((l) => ({ ...l, outstanding: l.quantityOrdered - l.quantityReceived })).filter((l) => l.outstanding > 0)
            return (
              <section key={po.id} className="rounded-lg border border-slate-200 p-3">
                <p className="text-sm font-semibold text-slate-800">{po.poNumber} · {po.supplierName} <Badge size="sm">{po.status}</Badge> <span className="font-normal tabular-nums text-slate-600">{po.currency} {formatNumber(po.total)}</span></p>
                <ul className="mt-2 space-y-1 text-sm">{po.lines.map((l) => <li key={l.id} className="flex items-center justify-between gap-2"><span>{l.itemName}: {formatNumber(l.quantityReceived)} of {formatNumber(l.quantityOrdered)} received</span>
                  {l.quantityReceived < l.quantityOrdered && canReceive ? <input type="number" min={0} aria-label={`Receive ${l.itemName}`} className={cn(inputClass, 'h-8 w-24')} value={receipt[l.id] ?? String(l.quantityOrdered - l.quantityReceived)} onChange={(e) => setReceipt({ ...receipt, [l.id]: e.target.value })} /> : null}</li>)}</ul>
                {outstanding.length > 0 && canReceive ? <button type="button" className={cn(secondaryButton, 'mt-2')} onClick={() => void receive(po.id, outstanding)}>Receive into {d.requisition.storeName ?? 'store'}</button> : null}
              </section>
            )
          })}
        </div>
      )}
    </FormDrawer>
  )
}

/* ── Transfers ───────────────────────────────────────────────────────────── */

function TransfersTab() {
  const [status, setStatus] = useState('open')
  const transfers = useTransfers(status)
  const stores = useStores()
  const action = useStoresAction()
  const canIssue = useAnyPermission('materials.issue', 'materials.manage')
  const [creating, setCreating] = useState(false)
  const mine = new Set((stores.data ?? []).map((s) => s.id))
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-2">
        <Field label="Show" className="w-44"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="open">Open</option><option value="Received">Received</option><option value="">All</option></select></Field>
        {canIssue ? <button type="button" className={cn(primaryButton, 'ml-auto')} onClick={() => setCreating(true)}><ArrowRightLeft className="h-4 w-4" />Request a transfer</button> : null}
      </div>
      {transfers.isLoading ? <Spinner /> : (transfers.data ?? []).length === 0 ? <EmptyState icon={ArrowRightLeft} title="No transfers" description="Borrow stock from another store or factory when yours runs short." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[860px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Number</th><th className={th}>Part</th><th className={th}>From</th><th className={th}>To</th><th className={th}>Status</th><th className={th} /></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(transfers.data ?? []).map((t) => (
                <tr key={t.id}>
                  <td className="px-4 py-2 font-mono text-xs">{t.transferNumber}{t.isInterFactory ? <Badge size="sm" tone="info">Inter-factory</Badge> : null}</td>
                  <td className="px-4 py-2">{t.itemName} × {formatNumber(t.quantity)} {t.unitOfMeasure}{t.serialNumbers ? <span className="block text-xs text-slate-500">{t.serialNumbers}</span> : null}{t.reason ? <span className="block text-xs text-slate-500">{t.reason}</span> : null}</td>
                  <td className="px-4 py-2 text-slate-600">{t.fromStoreName}<span className="block text-xs text-slate-500">{t.fromFactoryName}</span></td>
                  <td className="px-4 py-2 text-slate-600">{t.toStoreName}<span className="block text-xs text-slate-500">{t.toFactoryName}</span></td>
                  <td className="px-4 py-2"><Badge size="sm" tone={TRANSFER_TONES[t.status]}>{t.status}</Badge><span className="block text-xs text-slate-500">{t.receivedAt ? `${t.receivedByName} · ${formatDate(t.receivedAt)}` : t.dispatchedAt ? `Sent ${formatDate(t.dispatchedAt)}` : `${t.requestedByName ?? ''} · ${formatDate(t.requestedAt)}`}</span></td>
                  <td className="px-4 py-2 text-right text-xs">
                    {canIssue && t.status === 'Requested' && mine.has(t.fromStoreId) ? <TransferDispatch transfer={t} /> : null}
                    {canIssue && t.status === 'Dispatched' && mine.has(t.toStoreId) ? <button type="button" className="font-medium text-[var(--app-primary)] underline" onClick={() => void attempt(() => action.mutateAsync({ path: `/transfers/${t.id}/receive` }), 'Booked in')}>Receive</button> : null}
                    {canIssue && t.status === 'Requested' ? <button type="button" className="ml-2 text-slate-500 underline" onClick={() => void attempt(() => action.mutateAsync({ path: `/transfers/${t.id}/reject`, body: { reason: null } }), 'Closed')}>{mine.has(t.fromStoreId) && !mine.has(t.toStoreId) ? 'Refuse' : 'Cancel'}</button> : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <NewTransferDrawer open={creating} onClose={() => setCreating(false)} />
    </div>
  )
}

function TransferDispatch({ transfer }: { transfer: StoreTransfer }) {
  const action = useStoresAction()
  const units = useUnits('InStore')
  const [serials, setSerials] = useState<string[]>([])
  const serialised = (units.data ?? []).some((u) => u.materialItemId === transfer.materialItemId)
  const available = (units.data ?? []).filter((u) => u.materialItemId === transfer.materialItemId && u.storeId === transfer.fromStoreId)
  const send = () => attempt(() => action.mutateAsync({ path: `/transfers/${transfer.id}/dispatch`, body: { serialNumbers: serialised ? serials : null } }), 'Dispatched')
  if (!serialised) return <button type="button" className="font-medium text-[var(--app-primary)] underline" onClick={() => void send()}>Dispatch</button>
  return (
    <span className="inline-flex items-center gap-1">
      <select multiple aria-label="Serial numbers to send" className="h-16 rounded border border-slate-200 text-xs" value={serials} onChange={(e) => setSerials(Array.from(e.target.selectedOptions).map((o) => o.value))}>
        {available.map((u) => <option key={u.id} value={u.serialNumber}>{u.serialNumber}</option>)}
      </select>
      <button type="button" className="font-medium text-[var(--app-primary)] underline" disabled={serials.length !== transfer.quantity} onClick={() => void send()}>Dispatch</button>
    </span>
  )
}

function NewTransferDrawer({ open, onClose }: { open: boolean; onClose: () => void }) {
  const stores = useStores()
  const action = useStoresAction()
  const [part, setPart] = useState<PickedPart | null>(null)
  const detail = useSparePart(part?.id ?? null)
  const [form, setForm] = useState({ from: '', to: '', quantity: '1', reason: '' })
  const close = () => { setPart(null); setForm({ from: '', to: '', quantity: '1', reason: '' }); onClose() }
  const submit = async () => {
    if (part && await attempt(() => action.mutateAsync({ path: '/transfers', body: { fromStoreId: form.from, toStoreId: form.to, materialItemId: part.id, quantity: Number(form.quantity), reason: form.reason || null } }), 'Transfer requested')) close()
  }
  return (
    <FormDrawer open={open} title="Request a transfer" onClose={close} footer={(
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={close}>Cancel</button><button type="button" className={primaryButton} disabled={!part || !form.from || !form.to || action.isPending} onClick={() => void submit()}>Request</button></div>
    )}>
      <div className="space-y-3">
        <Field label="Part" required><PartPicker value={part} onChange={setPart} /></Field>
        <Field label="From" required hint="Any store in the group holding it">
          <select className={inputClass} value={form.from} onChange={(e) => setForm({ ...form, from: e.target.value })}>
            <option value="">Choose</option>{(detail.data?.stores ?? []).filter((s) => s.available > 0).map((s) => <option key={s.storeId} value={s.storeId}>{s.factoryName} · {s.storeName} ({formatNumber(s.available)} free)</option>)}
          </select>
        </Field>
        <Field label="To" required><select className={inputClass} value={form.to} onChange={(e) => setForm({ ...form, to: e.target.value })}><option value="">Choose</option>{(stores.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.factoryName} · {s.name}</option>)}</select></Field>
        <div className="grid grid-cols-[8rem_1fr] gap-2">
          <Field label="Quantity" required><input type="number" min={0} step="any" className={inputClass} value={form.quantity} onChange={(e) => setForm({ ...form, quantity: e.target.value })} /></Field>
          <Field label="Why"><input className={inputClass} value={form.reason} onChange={(e) => setForm({ ...form, reason: e.target.value })} /></Field>
        </div>
      </div>
    </FormDrawer>
  )
}

/* ── Cycle counts ────────────────────────────────────────────────────────── */

function CountsTab() {
  const counts = useCounts()
  const stores = useStores()
  const action = useStoresAction()
  const canIssue = useAnyPermission('materials.issue', 'materials.manage')
  const [open, setOpen] = useState<string | null>(null)
  const [form, setForm] = useState({ storeId: '', criticalOnly: false, blind: true })
  const start = async () => {
    try {
      const created = (await action.mutateAsync({ path: '/counts', body: { storeId: form.storeId, criticalOnly: form.criticalOnly, blind: form.blind } })) as { count: { id: string } }
      toast.success('Count opened')
      setOpen(created.count.id)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-4">
      {canIssue ? (
        <div className="flex flex-wrap items-end gap-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
          <Field label="Store" className="w-56"><select className={inputClass} value={form.storeId} onChange={(e) => setForm({ ...form, storeId: e.target.value })}><option value="">Choose</option>{(stores.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
          <label className="flex items-center gap-2 pb-2 text-sm"><input type="checkbox" checked={form.criticalOnly} onChange={(e) => setForm({ ...form, criticalOnly: e.target.checked })} />Critical spares only</label>
          <label className="flex items-center gap-2 pb-2 text-sm"><input type="checkbox" checked={form.blind} onChange={(e) => setForm({ ...form, blind: e.target.checked })} />Blind (counters don't see the book figure)</label>
          <button type="button" className={primaryButton} disabled={!form.storeId} onClick={() => void start()}>Start a count</button>
        </div>
      ) : null}
      {counts.isLoading ? <Spinner /> : (counts.data ?? []).length === 0 ? <EmptyState icon={ClipboardList} title="No counts yet" description="Count each store regularly; critical spares more often." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[720px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Count</th><th className={th}>Store</th><th className={th}>Progress</th><th className={th}>Started</th><th className={`${th} text-right`}>Variance</th><th className={th}>Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(counts.data ?? []).map((c) => (
                <tr key={c.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(c.id)}>
                  <td className="px-4 py-2 font-mono text-xs">{c.countNumber}{c.criticalOnly ? <Badge size="sm" tone="danger">Critical</Badge> : null}</td>
                  <td className="px-4 py-2">{c.storeName}</td>
                  <td className="px-4 py-2 tabular-nums">{c.counted} of {c.lines}</td>
                  <td className="px-4 py-2 text-xs text-slate-600">{c.startedByName} · {formatDate(c.startedAt)}</td>
                  <td className={cn('px-4 py-2 text-right tabular-nums', (c.varianceValue ?? 0) < 0 ? 'text-rose-700' : '')}>{c.varianceValue != null ? formatNumber(c.varianceValue) : '—'}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={c.status === 'Open' ? 'info' : c.status === 'Posted' ? 'success' : 'neutral'}>{c.status}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <CountDrawer id={open} onClose={() => setOpen(null)} />
    </div>
  )
}

function CountDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const count = useCount(id)
  const action = useStoresAction()
  const canPost = usePermission('materials.manage')
  const [values, setValues] = useState<Record<string, string>>({})
  const d = count.data
  const isOpen = d?.count.status === 'Open'
  const saveLine = (lineId: string) => {
    const value = values[lineId]
    if (value === undefined || value === '') return
    void action.mutateAsync({ method: 'put', path: `/counts/${id}/lines/${lineId}`, body: { countedQuantity: Number(value) } }).catch(() => undefined)
  }
  return (
    <FormDrawer open={id !== null} title={d ? `${d.count.countNumber} · ${d.count.storeName}` : ''} onClose={onClose} footer={d && isOpen ? (
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={() => void attempt(() => action.mutateAsync({ path: `/counts/${id}/cancel` }), 'Count cancelled')}>Cancel count</button>
        {canPost ? <button type="button" className={primaryButton} disabled={d.count.counted === 0} onClick={() => void attempt(() => action.mutateAsync({ path: `/counts/${id}/post` }), 'Count posted')}>Post count</button> : null}
      </div>
    ) : undefined}>
      {!d ? <Spinner /> : (
        <table className="w-full text-sm">
          <thead className="text-left text-xs text-slate-500"><tr><th className="py-1">Part</th>{!d.count.isBlind || !isOpen ? <th className="py-1 text-right">Book</th> : null}<th className="py-1 text-right">Counted</th>{!isOpen ? <th className="py-1 text-right">Variance</th> : null}</tr></thead>
          <tbody className="divide-y divide-slate-100">
            {d.lines.map((l) => (
              <tr key={l.id}>
                <td className="py-1.5"><span className="font-mono text-xs text-slate-500">{l.itemCode}</span> {l.itemName}</td>
                {!d.count.isBlind || !isOpen ? <td className="py-1.5 text-right tabular-nums text-slate-600">{l.expected != null ? formatNumber(l.expected) : ''}</td> : null}
                <td className="py-1.5 text-right">{isOpen ? (
                  <input type="number" min={0} step="any" aria-label={`Count of ${l.itemName}`} className={cn(inputClass, 'ml-auto h-8 w-24 text-right')} value={values[l.id] ?? (l.counted?.toString() ?? '')}
                    onChange={(e) => setValues({ ...values, [l.id]: e.target.value })} onBlur={() => saveLine(l.id)} />
                ) : <span className="tabular-nums">{l.counted != null ? formatNumber(l.counted) : '—'}</span>}</td>
                {!isOpen ? <td className={cn('py-1.5 text-right tabular-nums', (l.variance ?? 0) < 0 ? 'text-rose-700' : (l.variance ?? 0) > 0 ? 'text-emerald-700' : '')}>{l.variance != null ? formatNumber(l.variance) : ''}</td> : null}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </FormDrawer>
  )
}

/* ── Repairables ─────────────────────────────────────────────────────────── */

function RepairablesTab() {
  const [status, setStatus] = useState('')
  const units = useUnits(status || null)
  const [open, setOpen] = useState<SpareUnit | null>(null)
  return (
    <div className="space-y-4">
      <Field label="Show" className="w-52"><select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}><option value="">Every unit</option>{Object.entries(UNIT_STATUS_LABELS).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></Field>
      {units.isLoading ? <Spinner /> : (units.data ?? []).length === 0 ? <EmptyState icon={Wrench} title="No serialised spares" description="Mark parts like CTC segments, gearboxes or motors as serialised to track each unit." /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Part</th><th className={th}>Serial</th><th className={th}>Status</th><th className={th}>Where</th><th className={`${th} text-right`}>Repairs</th><th className={`${th} text-right`}>Repair cost</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(units.data ?? []).map((u) => (
                <tr key={u.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(u)}>
                  <td className="px-4 py-2">{u.itemName}</td>
                  <td className="px-4 py-2 font-mono text-xs">{u.serialNumber}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={UNIT_TONES[u.status]}>{UNIT_STATUS_LABELS[u.status]}</Badge></td>
                  <td className="px-4 py-2 text-slate-600">{u.status === 'Installed' ? `${u.assetCode} ${u.assetName}` : u.status === 'AtRepair' ? `${u.repairSupplierName}${u.expectedBackAt ? `, back ${formatDate(u.expectedBackAt)}` : ''}` : u.storeName ?? '—'}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{u.repairCount}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(u.totalRepairCost)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <UnitDrawer unit={open} onClose={() => setOpen(null)} />
    </div>
  )
}

function UnitDrawer({ unit, onClose }: { unit: SpareUnit | null; onClose: () => void }) {
  const history = useUnitHistory(unit?.id ?? null)
  const suppliers = useSuppliers()
  const stores = useStores()
  const action = useStoresAction()
  const canIssue = useAnyPermission('materials.issue', 'materials.manage')
  const [repair, setRepair] = useState({ supplierId: '', reference: '', expectedBackAt: '' })
  const [back, setBack] = useState({ storeId: '', cost: '', currency: '', note: '' })
  const [scrapReason, setScrapReason] = useState('')
  if (!unit) return null
  const go = async (path: string, body: unknown, message: string) => { if (await attempt(() => action.mutateAsync({ path: `/units/${unit.id}/${path}`, body }), message)) onClose() }
  return (
    <FormDrawer open title={`${unit.itemName} ${unit.serialNumber}`} onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm"><Badge size="sm" tone={UNIT_TONES[unit.status]}>{UNIT_STATUS_LABELS[unit.status]}</Badge> {unit.repairCount} repair{unit.repairCount === 1 ? '' : 's'}, {formatNumber(unit.totalRepairCost)} spent.</p>
        {canIssue && (unit.status === 'AwaitingRepair' || unit.status === 'InStore') ? (
          <section className="space-y-2 rounded-lg border border-slate-200 p-3">
            <h3 className="text-sm font-semibold text-slate-800">Send for repair</h3>
            <Field label="Repairer"><select className={inputClass} value={repair.supplierId} onChange={(e) => setRepair({ ...repair, supplierId: e.target.value })}><option value="">Choose</option>{(suppliers.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.supplierName}</option>)}</select></Field>
            <div className="grid grid-cols-2 gap-2">
              <Field label="Their reference"><input className={inputClass} value={repair.reference} onChange={(e) => setRepair({ ...repair, reference: e.target.value })} /></Field>
              <Field label="Expected back"><input type="date" className={inputClass} value={repair.expectedBackAt} onChange={(e) => setRepair({ ...repair, expectedBackAt: e.target.value })} /></Field>
            </div>
            <button type="button" className={secondaryButton} disabled={!repair.supplierId} onClick={() => void go('repair', { supplierId: repair.supplierId, reference: repair.reference || null, expectedBackAt: repair.expectedBackAt || null }, 'Sent for repair')}>Send</button>
          </section>
        ) : null}
        {canIssue && unit.status === 'AtRepair' ? (
          <section className="space-y-2 rounded-lg border border-slate-200 p-3">
            <h3 className="text-sm font-semibold text-slate-800">Back from repair</h3>
            <Field label="Into store"><select className={inputClass} value={back.storeId} onChange={(e) => setBack({ ...back, storeId: e.target.value })}><option value="">Where it came from</option>{(stores.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
            <div className="grid grid-cols-[1fr_6rem] gap-2">
              <Field label="Repair cost"><input type="number" min={0} step="any" className={inputClass} value={back.cost} onChange={(e) => setBack({ ...back, cost: e.target.value })} /></Field>
              <Field label="Currency"><input className={inputClass} maxLength={3} placeholder="KES" value={back.currency} onChange={(e) => setBack({ ...back, currency: e.target.value.toUpperCase() })} /></Field>
            </div>
            <Field label="Note"><input className={inputClass} value={back.note} placeholder="e.g. re-cut 0.4 mm" onChange={(e) => setBack({ ...back, note: e.target.value })} /></Field>
            <button type="button" className={primaryButton} onClick={() => void go('repair-return', { storeId: back.storeId || null, cost: back.cost ? Number(back.cost) : null, currency: back.currency || null, note: back.note || null }, 'Back in stock')}>Book back in</button>
          </section>
        ) : null}
        {canIssue && (unit.status === 'AwaitingRepair' || unit.status === 'InStore') ? (
          <section className="flex items-end gap-2">
            <Field label="Scrap: why" className="flex-1"><input className={inputClass} value={scrapReason} onChange={(e) => setScrapReason(e.target.value)} /></Field>
            <button type="button" className={secondaryButton} disabled={!scrapReason.trim()} onClick={() => void go('scrap', { reason: scrapReason }, 'Scrapped')}>Scrap</button>
          </section>
        ) : null}
        <section className="border-t border-slate-100 pt-3">
          <h3 className="mb-2 text-sm font-semibold text-slate-800">History</h3>
          <ol className="space-y-1.5 text-sm">{(history.data ?? []).map((h, i) => <li key={i}>{h.type}{h.workOrderNumber ? ` · ${h.workOrderNumber}` : ''}{h.assetCode ? ` · ${h.assetCode}` : ''}{h.storeName ? ` · ${h.storeName}` : ''}{h.cost ? ` · ${formatNumber(h.cost)}` : ''}{h.note ? ` · ${h.note}` : ''}<span className="block text-xs text-slate-500">{h.by} · {formatDateTime(h.at)}</span></li>)}</ol>
        </section>
      </div>
    </FormDrawer>
  )
}

/* ── Chemicals ───────────────────────────────────────────────────────────── */

function ChemicalsTab() {
  const chemicals = useChemicals()
  const log = useLubricantLog()
  const [, setParams] = useSearchParams()
  if (chemicals.isLoading) return <Spinner />
  const rows = chemicals.data ?? []
  const missing = rows.filter((x) => x.sdsStatus !== 'Current').length
  return (
    <div className="space-y-4">
      {missing > 0 ? <p className="flex items-center gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800"><AlertTriangle className="h-4 w-4" />{missing} chemical{missing === 1 ? '' : 's'} without a current safety data sheet. Upload them from the part.</p> : null}
      {rows.length === 0 ? <EmptyState icon={FlaskConical} title="No chemicals registered" description="Mark lubricants, boiler water treatment and cleaning agents as chemicals on the part." actionLabel="Go to parts" onAction={() => setParams({ tab: 'parts' }, { replace: true })} /> : (
        <div className={tableShell}>
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>Chemical</th><th className={th}>Kind</th><th className={th}>Hazard</th><th className={th}>Food grade</th><th className={th}>Safety data sheet</th><th className={`${th} text-right`}>Held</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((c) => (
                <tr key={c.id}>
                  <td className="px-4 py-2"><span className="font-mono text-xs text-slate-500">{c.itemCode}</span> {c.itemName}</td>
                  <td className="px-4 py-2 text-slate-600">{c.kind ?? '—'}</td>
                  <td className="px-4 py-2 text-slate-600">{c.hazardClass ?? '—'}</td>
                  <td className="px-4 py-2">{c.foodGradeClass ? <Badge size="sm" tone={c.foodGradeClass === 'H1' ? 'success' : 'warning'}>{c.foodGradeClass}</Badge> : c.kind === 'Lubricant' ? <Badge size="sm" tone="danger">Not food grade</Badge> : '—'}</td>
                  <td className="px-4 py-2">{c.sdsAttachmentId ? <button type="button" className="text-[var(--app-primary)] underline" onClick={() => void openAttachment(c.sdsAttachmentId!)}>SDS</button> : null} <Badge size="sm" tone={c.sdsStatus === 'Current' ? 'success' : c.sdsStatus === 'Missing' ? 'danger' : 'warning'}>{c.sdsStatus}</Badge>{c.sdsIssuedOn ? <span className="text-xs text-slate-500"> {formatDate(c.sdsIssuedOn)}</span> : null}</td>
                  <td className="px-4 py-2 text-right tabular-nums">{formatNumber(c.onHand)} {c.unitOfMeasure}<span className="block text-xs text-slate-500">{c.stores.join(', ')}</span></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <section>
        <h2 className="mb-2 text-sm font-semibold text-slate-800">Lubricants used on machines (last 90 days)</h2>
        {(log.data ?? []).length === 0 ? <p className="text-sm text-slate-500">None issued yet.</p> : (
          <div className={tableShell}>
            <table className="w-full min-w-[720px] text-sm">
              <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className={th}>When</th><th className={th}>Lubricant</th><th className={th}>Grade</th><th className={`${th} text-right`}>Quantity</th><th className={th}>Machine</th><th className={th}>Job</th><th className={th}>By</th></tr></thead>
              <tbody className="divide-y divide-slate-100">
                {(log.data ?? []).map((r, i) => (
                  <tr key={i}>
                    <td className="px-4 py-2 text-xs text-slate-600">{formatDateTime(r.at)}</td>
                    <td className="px-4 py-2">{r.lubricant}</td>
                    <td className="px-4 py-2">{r.foodGradeClass ?? <span className="text-rose-700">None</span>}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{formatNumber(r.quantity)} {r.unitOfMeasure}</td>
                    <td className="px-4 py-2">{r.assetCode ? `${r.assetCode} ${r.assetName}` : '—'}</td>
                    <td className="px-4 py-2 font-mono text-xs">{r.workOrderNumber ?? '—'}</td>
                    <td className="px-4 py-2 text-slate-600">{r.by ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
