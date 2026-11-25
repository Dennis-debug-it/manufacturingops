import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { useQueryClient } from '@tanstack/react-query'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { Badge } from '../../shared/components/Badge'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { numberOrNull, textOrNull, todayInput } from '../../shared/components/formStyles'
import { formatCurrency } from '../../shared/utils/format'
import {
  DECISION_LABEL, FORM_LABEL, MEASURE_LABEL, REASON_LABEL, STACK_STATUS_LABEL, SUPPLIER_TYPE_LABEL,
  submitDelivery, submitIssue, submitWeighOut, useCountStack, useGateVehicles, useSavePrice, useSaveStack, useSaveSupplier,
  type WoodDelivery, type WoodLookups, type WoodPrice, type WoodSettings, type WoodSupplier,
} from './api'
import { assess, averageMoisture, netKg, round } from './logic'

/** What the factory's rules are until it has saved its own (the server starts from the same numbers). */
const DEFAULT_SETTINGS: Pick<WoodSettings, 'defaultMeasureBasis' | 'moistureRule' | 'baseMoisturePct' | 'rejectMoisturePct' | 'deductionPctPerPoint' | 'priceBands' | 'defaultKgPerLoad'> = {
  defaultMeasureBasis: 'Weighbridge',
  moistureRule: 'DeductKg',
  baseMoisturePct: 25,
  rejectMoisturePct: 45,
  deductionPctPerPoint: 1.5,
  priceBands: [{ upToPct: 30, percentOfPrice: 100 }, { upToPct: 35, percentOfPrice: 90 }, { upToPct: 40, percentOfPrice: 80 }, { upToPct: 45, percentOfPrice: 65 }],
  defaultKgPerLoad: null,
}

const num = (value: string) => numberOrNull(value.replace(',', '.'))
const kg = (value: number) => `${round(value, 1).toLocaleString()} kg`

/* ── Receive wood ────────────────────────────────────────────────────────── */

export function ReceiveWoodDrawer({ open, onClose, lookups, factoryId }: { open: boolean; onClose: () => void; lookups: WoodLookups; factoryId: string }) {
  const queryClient = useQueryClient()
  const settings = lookups.settings ?? { ...DEFAULT_SETTINGS }
  const [supplierId, setSupplierId] = useState('')
  const [speciesId, setSpeciesId] = useState('')
  const [form, setForm] = useState('Logs')
  const [basis, setBasis] = useState(settings.defaultMeasureBasis)
  const [gross, setGross] = useState('')
  const [tare, setTare] = useState('')
  const [m3, setM3] = useState('')
  const [loads, setLoads] = useState('')
  const [perLoad, setPerLoad] = useState(settings.defaultKgPerLoad ? String(settings.defaultKgPerLoad) : '')
  const [samples, setSamples] = useState(['', '', ''])
  const [reject, setReject] = useState(false)
  const [rejectReason, setRejectReason] = useState('')
  const [acceptWet, setAcceptWet] = useState(false)
  const [price, setPrice] = useState('')
  const [stackId, setStackId] = useState('')
  const [vehicle, setVehicle] = useState('')
  const [gateVehicleId, setGateVehicleId] = useState('')
  const [tareLater, setTareLater] = useState(false)
  const gate = useGateVehicles(factoryId)
  const [driver, setDriver] = useState('')
  const [note, setNote] = useState('')
  const [permit, setPermit] = useState('')
  const [source, setSource] = useState('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)

  const species = lookups.species.find((x) => x.id === speciesId)
  const waiting = tareLater && basis === 'Weighbridge'
  const measured = waiting
    ? { kg: null, problem: num(gross) === null || num(gross)! <= 0 ? 'Enter the gross weight.' : null }
    : netKg({ basis, grossKg: num(gross), tareKg: num(tare), stackedM3: num(m3), loads: num(loads), kgPerLoad: num(perLoad), densityKgPerM3: species?.kgPerStackedM3 ?? null })
  const moisture = averageMoisture(samples.map(num))
  const priceValue = num(price)
  const result = waiting || measured.kg === null ? null : assess(settings, measured.kg, moisture, priceValue ?? 0, reject, rejectReason, acceptWet)
  const overLimit = moisture !== null && moisture > settings.rejectMoisturePct

  const save = async () => {
    if (measured.kg === null && !waiting) return toast.error(measured.problem ?? 'Enter the weight.')
    if (waiting && measured.problem) return toast.error(measured.problem)
    setSaving(true)
    try {
      const supplier = lookups.suppliers.find((x) => x.id === supplierId)?.name ?? 'Unknown supplier'
      const outcome = await submitDelivery({
        factoryId, supplierId: supplierId || null, speciesId: speciesId || null, fuelForm: form, sourceArea: textOrNull(source), transportPermit: textOrNull(permit),
        supplierNoteNumber: textOrNull(note), vehicleRegistration: textOrNull(vehicle), driverName: textOrNull(driver), measureBasis: basis,
        grossKg: basis === 'Weighbridge' ? num(gross) : null, tareKg: basis === 'Weighbridge' && !waiting ? num(tare) : null, stackedM3: basis === 'StackedVolume' ? num(m3) : null,
        loads: basis === 'LoadCount' ? num(loads) : null, kgPerLoad: basis === 'LoadCount' ? num(perLoad) : null,
        moistureSamples: samples.map(num).filter((x): x is number => x !== null), reject, rejectReason: textOrNull(rejectReason), acceptAboveMoistureLimit: acceptWet && lookups.canManage,
        pricePerTonne: priceValue, stackId: waiting ? null : stackId || null, notes: textOrNull(notes), vehicleMovementId: gateVehicleId || null, tareLater: waiting,
      }, `Wood from ${supplier}, ${waiting ? `gross ${kg(num(gross)!)}` : kg(measured.kg!)}`, queryClient)
      toast.success(outcome.queued ? 'Saved on this device. It is sent when the signal is back.' : outcome.data ? `${outcome.data.deliveryNumber}: ${DECISION_LABEL[outcome.data.decision] ?? outcome.data.decision}${waiting ? '. Weigh the tare to finish it.' : ''}` : 'Delivery saved')
      onClose()
    } catch {
      // shown by the API client
    } finally {
      setSaving(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Receive wood"
      onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={saving || (measured.kg === null && !waiting)} onClick={() => void save()}>{saving ? 'Saving…' : waiting ? 'Save weigh-in' : 'Save delivery'}</button></div>}
    >
      <div className="space-y-5">
        {(gate.data ?? []).length > 0 ? (
          <Field label="Truck at the gate" hint="Picking the truck links this delivery to the gate log. The gate holds it until the tare is weighed.">
            <select className={inputClass} value={gateVehicleId} onChange={(e) => {
              const truck = (gate.data ?? []).find((x) => x.id === e.target.value)
              setGateVehicleId(e.target.value)
              if (truck) { setVehicle(truck.registration); setDriver(truck.driverName ?? '') }
            }}>
              <option value="">Not from the gate log</option>
              {(gate.data ?? []).filter((x) => !x.awaitingTare).map((x) => <option key={x.id} value={x.id}>{x.registration}{x.driverName ? ` · ${x.driverName}` : ''}{x.hasDelivery ? ' (already has a delivery)' : ''}</option>)}
            </select>
          </Field>
        ) : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Supplier">
            <select className={inputClass} value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
              <option value="">Not listed</option>
              {lookups.suppliers.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label="Kind of wood">
            <select className={inputClass} value={speciesId} onChange={(e) => setSpeciesId(e.target.value)}>
              <option value="">Not chosen</option>
              {lookups.species.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label="Form">
            <select className={inputClass} value={form} onChange={(e) => setForm(e.target.value)}>
              {lookups.fuelForms.map((x) => <option key={x} value={x}>{FORM_LABEL[x] ?? x}</option>)}
            </select>
          </Field>
          <Field label="Vehicle" hint="e.g. KCA 123A">
            <input className={inputClass} value={vehicle} onChange={(e) => setVehicle(e.target.value)} />
          </Field>
        </div>

        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-3">
          <legend className="px-1 text-sm font-semibold text-slate-800">How much came</legend>
          <div role="radiogroup" aria-label="How it was measured" className="flex flex-wrap gap-2">
            {lookups.measureBases.map((x) => (
              <button key={x} type="button" role="radio" aria-checked={basis === x} onClick={() => setBasis(x)}
                className={`rounded-full border px-3 py-1.5 text-sm ${basis === x ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700'}`}>
                {MEASURE_LABEL[x] ?? x}
              </button>
            ))}
          </div>
          {basis === 'Weighbridge' ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Gross weight (kg)" required><input className={inputClass} inputMode="decimal" value={gross} onChange={(e) => setGross(e.target.value)} /></Field>
              <Field label="Tare weight (kg)" required={!waiting}><input className={inputClass} inputMode="decimal" disabled={waiting} value={waiting ? '' : tare} onChange={(e) => setTare(e.target.value)} /></Field>
              <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2"><input type="checkbox" checked={tareLater} onChange={(e) => setTareLater(e.target.checked)} />The truck is still on the weighbridge: weigh the tare later</label>
            </div>
          ) : null}
          {basis === 'StackedVolume' ? (
            <Field label="Stacked volume (m³)" required hint={species ? `${species.name}: ${species.kgPerStackedM3} kg per stacked m³` : 'Choose the kind of wood so the volume can be turned into kg.'}>
              <input className={inputClass} inputMode="decimal" value={m3} onChange={(e) => setM3(e.target.value)} />
            </Field>
          ) : null}
          {basis === 'LoadCount' ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Number of loads" required><input className={inputClass} inputMode="decimal" value={loads} onChange={(e) => setLoads(e.target.value)} /></Field>
              <Field label="Weight of one load (kg)" required><input className={inputClass} inputMode="decimal" value={perLoad} onChange={(e) => setPerLoad(e.target.value)} /></Field>
            </div>
          ) : null}
          <p className="text-sm text-slate-600">{waiting ? (measured.problem ?? <>Gross <span className="font-semibold text-slate-900">{kg(num(gross)!)}</span> weighed in. Finish the delivery once the truck is weighed out.</>) : measured.kg === null ? measured.problem : <>Received: <span className="font-semibold text-slate-900">{kg(measured.kg)}</span></>}</p>
        </fieldset>

        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-3">
          <legend className="px-1 text-sm font-semibold text-slate-800">Moisture (%)</legend>
          <div className="grid grid-cols-3 gap-3">
            {samples.map((value, index) => (
              <input key={index} aria-label={`Moisture reading ${index + 1}`} className={inputClass} inputMode="decimal" placeholder={`Reading ${index + 1}`} value={value}
                onChange={(e) => setSamples((current) => current.map((x, i) => (i === index ? e.target.value : x)))} />
            ))}
          </div>
          <p className="text-sm text-slate-600">{moisture === null ? 'Take three readings with the moisture meter. Without one, the wood is paid in full.' : <>Average <span className="font-semibold text-slate-900">{moisture}%</span> (base {settings.baseMoisturePct}%, reject above {settings.rejectMoisturePct}%)</>}</p>
        </fieldset>

        {result ? (
          <div className="space-y-1 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm" aria-live="polite">
            <div className="flex items-center gap-2">
              <Badge tone={result.decision === 'Rejected' ? 'danger' : result.decision === 'Accepted' ? 'success' : 'warning'}>{DECISION_LABEL[result.decision]}</Badge>
              {result.reason ? <span className="text-slate-700">{result.reason}</span> : null}
            </div>
            {result.decision !== 'Rejected' ? <div>Payable {kg(result.payableKg)}{priceValue !== null ? <> · {formatCurrency(result.amount)}</> : <span className="text-slate-500"> · the amount is worked out when it is saved</span>}</div> : null}
          </div>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-2">
          {!waiting ? <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={reject} onChange={(e) => setReject(e.target.checked)} />Reject this load</label> : null}
          {overLimit && lookups.canManage ? <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={acceptWet} onChange={(e) => setAcceptWet(e.target.checked)} />Accept it although it is over the limit</label> : null}
        </div>
        {reject && !waiting ? <Field label="Why was it rejected?"><input className={inputClass} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} /></Field> : null}

        {!reject && !waiting ? (
          <Field label="Into stack" hint="Leave blank if the wood has not been stacked yet.">
            <select className={inputClass} value={stackId} onChange={(e) => setStackId(e.target.value)}>
              <option value="">No stack yet</option>
              {lookups.stacks.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
        ) : null}

        <details className="rounded-lg border border-slate-200 p-3">
          <summary className="cursor-pointer text-sm font-medium text-slate-800">Driver, papers and price</summary>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <Field label="Driver"><input className={inputClass} value={driver} onChange={(e) => setDriver(e.target.value)} /></Field>
            <Field label="Supplier's delivery note"><input className={inputClass} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
            <Field label="Transport permit"><input className={inputClass} value={permit} onChange={(e) => setPermit(e.target.value)} /></Field>
            <Field label="Where the wood was cut"><input className={inputClass} value={source} onChange={(e) => setSource(e.target.value)} /></Field>
            {lookups.canManage ? <Field label="Price per tonne" hint="Leave blank to use the agreed price."><input className={inputClass} inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} /></Field> : null}
            <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
          </div>
        </details>
      </div>
    </FormDrawer>
  )
}

/* ── Wood fed to a boiler ────────────────────────────────────────────────── */

export function IssueWoodDrawer({ open, onClose, lookups, factoryId }: { open: boolean; onClose: () => void; lookups: WoodLookups; factoryId: string }) {
  const queryClient = useQueryClient()
  const [assetId, setAssetId] = useState(lookups.assets.length === 1 ? lookups.assets[0]!.id : '')
  const [stackId, setStackId] = useState('')
  const [amount, setAmount] = useState('')
  const [loads, setLoads] = useState('')
  const [perLoad, setPerLoad] = useState(lookups.settings?.defaultKgPerLoad ? String(lookups.settings.defaultKgPerLoad) : '')
  const [shift, setShift] = useState('Day')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)

  const fromLoads = num(loads) !== null && num(perLoad) !== null ? round(num(loads)! * num(perLoad)!, 1) : null
  const total = num(amount) ?? fromLoads

  const save = async () => {
    if (!assetId) return toast.error('Choose the boiler or dryer.')
    if (total === null || total <= 0) return toast.error('Enter the kg fed, or the loads and the weight of a load.')
    setSaving(true)
    try {
      const asset = lookups.assets.find((x) => x.id === assetId)?.name ?? 'boiler'
      const outcome = await submitIssue({ factoryId, assetId, stackId: stackId || null, kg: num(amount), loads: num(loads), kgPerLoad: num(perLoad), shift, notes: textOrNull(notes) }, `${kg(total)} of wood to ${asset}`, queryClient)
      toast.success(outcome.queued ? 'Saved on this device. It is sent when the signal is back.' : `${kg(total)} recorded`)
      onClose()
    } catch {
      // shown by the API client
    } finally {
      setSaving(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Wood fed to a boiler"
      onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={saving} onClick={() => void save()}>{saving ? 'Saving…' : 'Save'}</button></div>}
    >
      <div className="space-y-4">
        {lookups.assets.length === 0 ? <p className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">No boiler or dryer was found in this factory. Add one under Assets, with “boiler” or “dryer” in its name or type, or give it a fuel meter.</p> : null}
        <Field label="Boiler or dryer" required>
          <select className={inputClass} value={assetId} onChange={(e) => setAssetId(e.target.value)}>
            <option value="">Choose</option>
            {lookups.assets.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field label="Taken from stack" hint="Leave blank only for wood that was received without a stack.">
          <select className={inputClass} value={stackId} onChange={(e) => setStackId(e.target.value)}>
            <option value="">No stack</option>
            {lookups.stacks.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label="Kg fed"><input className={inputClass} inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} /></Field>
          <Field label="or loads"><input className={inputClass} inputMode="decimal" value={loads} onChange={(e) => setLoads(e.target.value)} /></Field>
          <Field label="kg in a load"><input className={inputClass} inputMode="decimal" value={perLoad} onChange={(e) => setPerLoad(e.target.value)} /></Field>
        </div>
        {total !== null && total > 0 ? <p className="text-sm text-slate-600">Recording <span className="font-semibold text-slate-900">{kg(total)}</span></p> : null}
        <Field label="Shift">
          <select className={inputClass} value={shift} onChange={(e) => setShift(e.target.value)}>
            {['Day', 'Night', 'Morning', 'Afternoon'].map((x) => <option key={x}>{x}</option>)}
          </select>
        </Field>
        <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Count a stack ───────────────────────────────────────────────────────── */

export function CountStackDrawer({ open, onClose, lookups, stackId: initialStack, ledgerKg }: { open: boolean; onClose: () => void; lookups: WoodLookups; stackId: string; ledgerKg: number | null }) {
  const count = useCountStack()
  const [stackId, setStackId] = useState(initialStack)
  const [mode, setMode] = useState<'Weighed' | 'Measured'>('Weighed')
  const [value, setValue] = useState('')
  const [reason, setReason] = useState('CountVariance')
  const [notes, setNotes] = useState('')

  const save = async () => {
    const amount = num(value)
    if (!stackId) return toast.error('Choose the stack.')
    if (amount === null || amount < 0) return toast.error(mode === 'Weighed' ? 'Enter the weight counted.' : 'Enter the stacked volume.')
    try {
      const result = await count.mutateAsync({ stackId, countedKg: mode === 'Weighed' ? amount : null, stackedM3: mode === 'Measured' ? amount : null, reason, notes: textOrNull(notes) })
      toast.success(result.varianceKg === 0 ? 'The count matches the ledger.' : `Ledger adjusted by ${kg(result.varianceKg)}`)
      onClose()
    } catch {
      // shown by the API client
    }
  }

  return (
    <FormDrawer
      open={open}
      title="Count a stack"
      onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={count.isPending} onClick={() => void save()}>Post the count</button></div>}
    >
      <div className="space-y-4">
        <Field label="Stack" required>
          <select className={inputClass} value={stackId} onChange={(e) => setStackId(e.target.value)}>
            <option value="">Choose</option>
            {lookups.stacks.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        {ledgerKg !== null && stackId === initialStack ? <p className="text-sm text-slate-600">The ledger says <span className="font-semibold text-slate-900">{kg(ledgerKg)}</span>.</p> : null}
        <div role="radiogroup" aria-label="How it was counted" className="flex gap-2">
          {(['Weighed', 'Measured'] as const).map((x) => (
            <button key={x} type="button" role="radio" aria-checked={mode === x} onClick={() => setMode(x)}
              className={`rounded-full border px-3 py-1.5 text-sm ${mode === x ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700'}`}>{x === 'Weighed' ? 'Weighed (kg)' : 'Measured (stacked m³)'}</button>
          ))}
        </div>
        <Field label={mode === 'Weighed' ? 'Weight counted (kg)' : 'Stacked volume (m³)'} required hint={mode === 'Measured' ? 'The stack needs a kind of wood, so the volume can be turned into kg.' : undefined}>
          <input className={inputClass} inputMode="decimal" value={value} onChange={(e) => setValue(e.target.value)} />
        </Field>
        <Field label="Why does it differ?">
          <select className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)}>
            {lookups.adjustmentReasons.map((x) => <option key={x} value={x}>{REASON_LABEL[x] ?? x}</option>)}
          </select>
        </Field>
        <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── A stack ─────────────────────────────────────────────────────────────── */

export function StackDrawer({ open, onClose, lookups, factoryId, stack }: { open: boolean; onClose: () => void; lookups: WoodLookups; factoryId: string; stack?: { id: string; code: string; status: string } }) {
  const save = useSaveStack()
  const [code, setCode] = useState(stack?.code ?? '')
  const [speciesId, setSpeciesId] = useState('')
  const [status, setStatus] = useState(stack?.status ?? 'Receiving')
  const [notes, setNotes] = useState('')

  const submit = async () => {
    if (!code.trim()) return toast.error('Give the stack a code, e.g. Y1-S07.')
    try {
      await save.mutateAsync({ ...(stack ? { id: stack.id } : {}), factoryId, code: code.trim(), speciesId: speciesId || null, status, notes: textOrNull(notes) })
      toast.success(stack ? 'Stack updated' : 'Stack added')
      onClose()
    } catch {
      // shown by the API client
    }
  }

  return (
    <FormDrawer open={open} title={stack ? `Stack ${stack.code}` : 'New stack'} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <Field label="Code" required><input className={inputClass} value={code} onChange={(e) => setCode(e.target.value)} /></Field>
        <Field label="Kind of wood">
          <select className={inputClass} value={speciesId} onChange={(e) => setSpeciesId(e.target.value)}>
            <option value="">Mixed or not set</option>
            {lookups.species.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field label="Status">
          <select className={inputClass} value={status} onChange={(e) => setStatus(e.target.value)}>
            {lookups.stackStatuses.map((x) => <option key={x} value={x}>{STACK_STATUS_LABEL[x] ?? x}</option>)}
          </select>
        </Field>
        <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── A supplier ──────────────────────────────────────────────────────────── */

export function SupplierDrawer({ open, onClose, lookups, supplier }: { open: boolean; onClose: () => void; lookups: WoodLookups; supplier?: WoodSupplier }) {
  const save = useSaveSupplier()
  const [name, setName] = useState(supplier?.name ?? '')
  const [type, setType] = useState(supplier?.type ?? 'Farmer')
  const [phone, setPhone] = useState(supplier?.phone ?? '')
  const [payment, setPayment] = useState(supplier?.paymentDetails ?? '')
  const [pin, setPin] = useState(supplier?.kraPin ?? '')
  const [nationalId, setNationalId] = useState('')
  const [area, setArea] = useState(supplier?.sourceArea ?? '')
  const [permit, setPermit] = useState(supplier?.permitNumber ?? '')
  const [expiry, setExpiry] = useState(supplier?.permitExpiresOn?.slice(0, 10) ?? '')
  const [notes, setNotes] = useState(supplier?.notes ?? '')
  const [active, setActive] = useState(supplier?.isActive ?? true)

  const submit = async () => {
    if (name.trim().length < 2) return toast.error('Enter the supplier’s name.')
    try {
      await save.mutateAsync({
        ...(supplier ? { id: supplier.id } : {}), name: name.trim(), type, phone: textOrNull(phone), paymentDetails: textOrNull(payment), kraPin: textOrNull(pin), nationalId: textOrNull(nationalId),
        sourceArea: textOrNull(area), permitNumber: textOrNull(permit), permitExpiresOn: expiry || null, notes: textOrNull(notes), isActive: active,
      })
      toast.success('Supplier saved')
      onClose()
    } catch {
      // shown by the API client
    }
  }

  return (
    <FormDrawer open={open} title={supplier ? supplier.name : 'New supplier'} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}>Save</button></div>}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Name" required className="sm:col-span-2"><input className={inputClass} value={name} onChange={(e) => setName(e.target.value)} /></Field>
        <Field label="Type">
          <select className={inputClass} value={type} onChange={(e) => setType(e.target.value)}>
            {lookups.supplierTypes.map((x) => <option key={x} value={x}>{SUPPLIER_TYPE_LABEL[x] ?? x}</option>)}
          </select>
        </Field>
        <Field label="Phone"><input className={inputClass} value={phone} onChange={(e) => setPhone(e.target.value)} /></Field>
        <Field label="M-Pesa or bank details" className="sm:col-span-2"><input className={inputClass} value={payment} onChange={(e) => setPayment(e.target.value)} /></Field>
        <Field label="KRA PIN"><input className={inputClass} value={pin} onChange={(e) => setPin(e.target.value)} /></Field>
        <Field label="National ID" hint={supplier?.idLast4 ? `Only the last four are kept (ends ${supplier.idLast4}). Type it again to change.` : 'Only the last four characters are kept.'}><input className={inputClass} value={nationalId} onChange={(e) => setNationalId(e.target.value)} autoComplete="off" /></Field>
        <Field label="Where the wood is cut" className="sm:col-span-2"><input className={inputClass} value={area} onChange={(e) => setArea(e.target.value)} /></Field>
        <Field label="Transport permit"><input className={inputClass} value={permit} onChange={(e) => setPermit(e.target.value)} /></Field>
        <Field label="Permit expires"><input type="date" className={inputClass} value={expiry} onChange={(e) => setExpiry(e.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />Still supplying</label>
      </div>
    </FormDrawer>
  )
}

/* ── A price ─────────────────────────────────────────────────────────────── */

export function PriceDrawer({ open, onClose, lookups, price }: { open: boolean; onClose: () => void; lookups: WoodLookups; price?: WoodPrice }) {
  const save = useSavePrice()
  const today = useMemo(() => todayInput(), [])
  const [supplierId, setSupplierId] = useState(price?.supplierId ?? '')
  const [speciesId, setSpeciesId] = useState(price?.speciesId ?? '')
  const [fuelForm, setFuelForm] = useState(price?.fuelForm ?? '')
  const [amount, setAmount] = useState(price ? String(price.pricePerTonne) : '')
  const [from, setFrom] = useState(price?.validFrom.slice(0, 10) ?? today)
  const [to, setTo] = useState(price?.validTo?.slice(0, 10) ?? '')
  const [notes, setNotes] = useState(price?.notes ?? '')
  const [active, setActive] = useState(price?.isActive ?? true)

  const submit = async () => {
    const value = num(amount)
    if (value === null || value < 0) return toast.error('Enter the price per tonne.')
    try {
      await save.mutateAsync({
        ...(price ? { id: price.id } : {}), supplierId: supplierId || null, speciesId: speciesId || null, fuelForm: fuelForm || null, pricePerTonne: value, validFrom: from, validTo: to || null,
        notes: textOrNull(notes), isActive: active,
      })
      toast.success('Price saved')
      onClose()
    } catch {
      // shown by the API client
    }
  }

  return (
    <FormDrawer open={open} title={price ? 'Change price' : 'New price'} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={save.isPending} onClick={() => void submit()}>Save</button></div>}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Supplier" hint="Leave blank for every supplier." className="sm:col-span-2">
          <select className={inputClass} value={supplierId} onChange={(e) => setSupplierId(e.target.value)}>
            <option value="">Every supplier</option>
            {lookups.suppliers.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field label="Kind of wood">
          <select className={inputClass} value={speciesId} onChange={(e) => setSpeciesId(e.target.value)}>
            <option value="">Any</option>
            {lookups.species.filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </Field>
        <Field label="Form">
          <select className={inputClass} value={fuelForm} onChange={(e) => setFuelForm(e.target.value)}>
            <option value="">Any</option>
            {lookups.fuelForms.map((x) => <option key={x} value={x}>{FORM_LABEL[x] ?? x}</option>)}
          </select>
        </Field>
        <Field label="Price per tonne" required><input className={inputClass} inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} /></Field>
        <div className="hidden sm:block" />
        <Field label="From" required><input type="date" className={inputClass} value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label="Until" hint="Leave blank if it has no end."><input type="date" className={inputClass} value={to} onChange={(e) => setTo(e.target.value)} /></Field>
        <Field label="Notes" className="sm:col-span-2"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
        <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={active} onChange={(e) => setActive(e.target.checked)} />In use</label>
      </div>
    </FormDrawer>
  )
}

/* ── Weigh out a delivery ────────────────────────────────────────────────── */

export function WeighOutDrawer({ open, onClose, lookups, delivery }: { open: boolean; onClose: () => void; lookups: WoodLookups; delivery: WoodDelivery }) {
  const queryClient = useQueryClient()
  const settings = lookups.settings ?? { ...DEFAULT_SETTINGS }
  const [tare, setTare] = useState('')
  const [samples, setSamples] = useState(['', ''])
  const [reject, setReject] = useState(false)
  const [rejectReason, setRejectReason] = useState('')
  const [acceptWet, setAcceptWet] = useState(false)
  const [price, setPrice] = useState('')
  const [stackId, setStackId] = useState('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)

  const turnedAway = reject && num(tare) === null
  const measured = turnedAway ? { kg: 0, problem: null } : netKg({ basis: 'Weighbridge', grossKg: delivery.grossKg, tareKg: num(tare), stackedM3: null, loads: null, kgPerLoad: null, densityKgPerM3: null })
  const moisture = averageMoisture([...delivery.moistureSamples, ...samples.map(num)])
  const result = measured.kg === null ? null : assess(settings, measured.kg, moisture, num(price) ?? delivery.pricePerTonne, reject, rejectReason, acceptWet)
  const overLimit = moisture !== null && moisture > settings.rejectMoisturePct

  const save = async () => {
    if (measured.kg === null) return toast.error(measured.problem ?? 'Enter the tare weight.')
    setSaving(true)
    try {
      const outcome = await submitWeighOut(delivery.id, {
        tareKg: num(tare), moistureSamples: samples.map(num).filter((x): x is number => x !== null), reject, rejectReason: textOrNull(rejectReason),
        acceptAboveMoistureLimit: acceptWet && lookups.canManage, pricePerTonne: num(price), stackId: reject ? null : stackId || null, notes: textOrNull(notes),
      }, `Weigh out ${delivery.deliveryNumber}`, queryClient)
      toast.success(outcome.queued ? 'Saved on this device. It is sent when the signal is back.' : `${delivery.deliveryNumber}: ${DECISION_LABEL[outcome.data?.decision ?? ''] ?? 'done'}`)
      onClose()
    } catch {
      // shown by the API client
    } finally {
      setSaving(false)
    }
  }

  return (
    <FormDrawer
      open={open}
      title={`Weigh out ${delivery.deliveryNumber}`}
      onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={saving || measured.kg === null} onClick={() => void save()}>{saving ? 'Saving…' : 'Finish delivery'}</button></div>}
    >
      <div className="space-y-5">
        <p className="text-sm text-slate-700">{delivery.supplier || 'Unlisted supplier'} · {delivery.vehicleRegistration || 'no vehicle'} · gross <span className="font-semibold">{kg(delivery.grossKg ?? 0)}</span> weighed in.{delivery.moistureSamples.length > 0 ? ` Readings so far: ${delivery.moistureSamples.map((x) => `${x}%`).join(', ')}.` : ''}</p>
        <Field label="Tare weight (kg)" required={!reject} hint={reject ? 'A truck turned away can be closed with no tare.' : undefined}>
          <input className={inputClass} inputMode="decimal" value={tare} onChange={(e) => setTare(e.target.value)} />
        </Field>
        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-3">
          <legend className="px-1 text-sm font-semibold text-slate-800">More moisture readings (%)</legend>
          <div className="grid grid-cols-2 gap-3">
            {samples.map((value, index) => (
              <input key={index} aria-label={`More moisture reading ${index + 1}`} className={inputClass} inputMode="decimal" placeholder={`Reading ${index + 1}`} value={value}
                onChange={(e) => setSamples((current) => current.map((x, i) => (i === index ? e.target.value : x)))} />
            ))}
          </div>
          <p className="text-sm text-slate-600">{moisture === null ? 'No readings yet.' : <>Average <span className="font-semibold text-slate-900">{moisture}%</span> (base {settings.baseMoisturePct}%, reject above {settings.rejectMoisturePct}%)</>}</p>
        </fieldset>
        {measured.kg !== null && !turnedAway ? <p className="text-sm text-slate-600">Received: <span className="font-semibold text-slate-900">{kg(measured.kg)}</span></p> : null}
        {result ? (
          <div className="space-y-1 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm" aria-live="polite">
            <div className="flex items-center gap-2">
              <Badge tone={result.decision === 'Rejected' ? 'danger' : result.decision === 'Accepted' ? 'success' : 'warning'}>{DECISION_LABEL[result.decision]}</Badge>
              {result.reason ? <span className="text-slate-700">{result.reason}</span> : null}
            </div>
            {result.decision !== 'Rejected' ? <div>Payable {kg(result.payableKg)}</div> : null}
          </div>
        ) : null}
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={reject} onChange={(e) => setReject(e.target.checked)} />Reject this load</label>
          {overLimit && lookups.canManage ? <label className="flex items-center gap-2 text-sm text-slate-700"><input type="checkbox" checked={acceptWet} onChange={(e) => setAcceptWet(e.target.checked)} />Accept it although it is over the limit</label> : null}
        </div>
        {reject ? <Field label="Why was it rejected?"><input className={inputClass} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} /></Field> : (
          <Field label="Into stack">
            <select className={inputClass} value={stackId} onChange={(e) => setStackId(e.target.value)}>
              <option value="">No stack yet</option>
              {lookups.stacks.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
        )}
        {lookups.canManage ? <Field label="Price per tonne" hint="Leave blank to use the agreed price."><input className={inputClass} inputMode="decimal" value={price} onChange={(e) => setPrice(e.target.value)} /></Field> : null}
        <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}
