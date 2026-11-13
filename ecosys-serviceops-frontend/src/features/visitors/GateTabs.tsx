import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, Car, LogIn, LogOut, Plus, Printer } from 'lucide-react'
import { Badge } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate, formatDateTime, formatDuration, formatTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { useContractors } from '../factory/stores/api'
import {
  ID_TYPES, VEHICLE_PURPOSES, VISIT_PURPOSES, downloadPassBadge, useCreatePass, usePassToday, usePasses, useRevokePass, useStaff, useVehicleEntry, useVehicleExit,
  useVehicles, useZones, type VisitorPass,
} from './api'
import { FactorySelect } from './FactorySelect'

const text = (value: string) => value.trim() || null

function useDebounced(value: string, ms = 300) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), ms)
    return () => window.clearTimeout(timer)
  }, [value, ms])
  return debounced
}

/* ── Vehicles ────────────────────────────────────────────────────────────── */

export function VehiclesTab({ factoryId }: { factoryId: string }) {
  const [insideOnly, setInsideOnly] = useState(true)
  const [query, setQuery] = useState('')
  const q = useDebounced(query)
  const vehicles = useVehicles(factoryId, insideOnly, q)
  const exit = useVehicleExit()
  const [entering, setEntering] = useState(false)
  const rows = vehicles.data ?? []

  const goOut = async (id: string, registration: string) => {
    try {
      await exit.mutateAsync({ id, notes: null })
      toast.success(`${registration} went out`)
    } catch { /* shown by the API client */ }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <input className={cn(inputClass, 'w-72')} placeholder="Registration, driver or load" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Search vehicles" />
          <div role="radiogroup" aria-label="Show" className="inline-flex overflow-hidden rounded-lg ring-1 ring-inset ring-slate-300">
            {[{ value: true, label: 'Inside now' }, { value: false, label: 'Last 30 days' }].map((option) => (
              <button key={option.label} type="button" role="radio" aria-checked={insideOnly === option.value} onClick={() => setInsideOnly(option.value)}
                className={cn('h-9 px-3 text-sm font-medium', insideOnly === option.value ? 'bg-[var(--app-primary)] text-white' : 'bg-white text-slate-600')}>{option.label}</button>
            ))}
          </div>
        </div>
        <button type="button" className={primaryButton} onClick={() => setEntering(true)}><LogIn className="h-4 w-4" />Vehicle in</button>
      </div>
      {vehicles.isLoading ? <Spinner /> : rows.length === 0 ? (
        <EmptyState title={insideOnly ? 'No vehicles inside' : 'No vehicle movements'} description="Record each vehicle at the gate. A visitor's vehicle is added when they check in." />
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Vehicle</th><th className="px-3 py-2">Driver</th><th className="px-3 py-2">In</th><th className="px-3 py-2">Out</th><th className="px-3 py-2">Notes</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((row) => (
                <tr key={row.id} className={cn(row.visitOver && 'bg-amber-50/60')}>
                  <td className="px-3 py-2">
                    <div className="flex items-center gap-2"><Car className="h-4 w-4 text-slate-400" /><span className="font-mono font-semibold text-slate-900">{row.registration}</span><Badge size="sm">{row.purpose}</Badge></div>
                    <div className="text-xs text-slate-500">{[row.description, row.visitNumber, factoryId ? null : row.factoryName].filter(Boolean).join(' · ')}</div>
                    {row.visitOver ? <div className="mt-1 flex items-center gap-1 text-xs font-medium text-amber-700"><AlertTriangle className="h-3 w-3" />Visit is over but the vehicle is still inside</div> : null}
                  </td>
                  <td className="px-3 py-2 text-slate-600">{row.driverName ?? '—'}<div className="text-xs text-slate-500">{row.driverPhone ?? ''}</div></td>
                  <td className="px-3 py-2 text-slate-600">{insideOnly ? formatTime(row.enteredAt) : formatDateTime(row.enteredAt)}<div className="text-xs text-slate-500">{row.enteredByName}</div></td>
                  <td className="px-3 py-2 text-slate-600">
                    {row.exitedAt ? <>{formatTime(row.exitedAt)}<div className="text-xs text-slate-500">{row.exitedByName}</div></> : <span className="text-xs">Inside {formatDuration(row.minutesInside)}</span>}
                  </td>
                  <td className="px-3 py-2 text-xs text-slate-600">{[row.notes, row.exitNotes].filter(Boolean).join(' · ') || '—'}</td>
                  <td className="px-3 py-2 text-right">
                    {row.exitedAt ? null : <button type="button" className={secondaryButton} aria-label={`${row.registration} went out`} disabled={exit.isPending} onClick={() => void goOut(row.id, row.registration)}><LogOut className="h-4 w-4" />Went out</button>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {entering ? <VehicleEntryDrawer factoryId={factoryId} onClose={() => setEntering(false)} /> : null}
    </div>
  )
}

function VehicleEntryDrawer({ factoryId, onClose }: { factoryId: string; onClose: () => void }) {
  const enter = useVehicleEntry()
  const [form, setForm] = useState({ factoryId, registration: '', purpose: 'Delivery', driverName: '', driverPhone: '', description: '', notes: '' })
  const submit = async () => {
    try {
      const row = await enter.mutateAsync({
        factoryId: form.factoryId, registration: form.registration, purpose: form.purpose, driverName: text(form.driverName), driverPhone: text(form.driverPhone),
        description: text(form.description), notes: text(form.notes),
      })
      toast.success(`${row.registration} came in`)
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title="Vehicle in" onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={enter.isPending || !form.factoryId || form.registration.trim().length < 3} onClick={() => void submit()}>Record entry</button></div>}>
      <div className="space-y-4">
        <Field label="Factory gate" required><FactorySelect value={form.factoryId} onChange={(value) => setForm({ ...form, factoryId: value })} /></Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Registration" required><input className={inputClass} placeholder="KCA 123A" value={form.registration} onChange={(e) => setForm({ ...form, registration: e.target.value })} /></Field>
          <Field label="Purpose">
            <select className={inputClass} value={form.purpose} onChange={(e) => setForm({ ...form, purpose: e.target.value })}>{VEHICLE_PURPOSES.map((x) => <option key={x}>{x}</option>)}</select>
          </Field>
          <Field label="Driver"><input className={inputClass} value={form.driverName} onChange={(e) => setForm({ ...form, driverName: e.target.value })} /></Field>
          <Field label="Driver's phone"><input className={inputClass} value={form.driverPhone} onChange={(e) => setForm({ ...form, driverPhone: e.target.value })} /></Field>
        </div>
        <Field label="Vehicle and load" hint="e.g. Isuzu FRR, 40 bags of fertiliser"><input className={inputClass} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        <Field label="Notes" hint="Seal number, search done, anything the gate noticed."><textarea className={textareaClass} value={form.notes} onChange={(e) => setForm({ ...form, notes: e.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Standing passes ─────────────────────────────────────────────────────── */

export function PassesTab({ factoryId, canManage, onCheckIn }: { factoryId: string; canManage: boolean; onCheckIn: (visitId: string, attendeeId: string) => void }) {
  const [query, setQuery] = useState('')
  const [includeEnded, setIncludeEnded] = useState(false)
  const passes = usePasses(factoryId, includeEnded, useDebounced(query))
  const today = usePassToday()
  const revoke = useRevokePass()
  const [issuing, setIssuing] = useState(false)
  const rows = passes.data ?? []

  const arrive = async (pass: VisitorPass) => {
    try {
      const result = await today.mutateAsync(pass.id)
      onCheckIn(result.visitId, result.attendeeId)
    } catch { /* shown by the API client */ }
  }
  const end = async (pass: VisitorPass) => {
    const reason = window.prompt(`Why is ${pass.passNumber} (${pass.fullName}) revoked? The gate will see this.`)
    if (!reason?.trim()) return
    try {
      await revoke.mutateAsync({ id: pass.id, reason })
      toast.success(`${pass.passNumber} revoked`)
    } catch { /* shown by the API client */ }
  }

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <input className={cn(inputClass, 'w-72')} placeholder="Name, company, contractor or pass number" value={query} onChange={(e) => setQuery(e.target.value)} aria-label="Search passes" />
          <label className="flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={includeEnded} onChange={(e) => setIncludeEnded(e.target.checked)} />Show ended and revoked</label>
        </div>
        {canManage ? <button type="button" className={primaryButton} onClick={() => setIssuing(true)}><Plus className="h-4 w-4" />Issue pass</button> : null}
      </div>
      <p className="text-xs text-slate-500">A standing pass lets someone who comes regularly in on the days and hours it allows, without a new invitation each time. Scanning its badge raises the day's visit; the gate checks (papers, induction, permit, blocklist) still run every day.</p>
      {passes.isLoading ? <Spinner /> : rows.length === 0 ? <EmptyState title="No standing passes" description={canManage ? 'Issue one for a contractor on an annual agreement or a regular supplier.' : 'Visitor managers issue standing passes.'} /> : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[900px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Holder</th><th className="px-3 py-2">When</th><th className="px-3 py-2">Zones</th><th className="px-3 py-2">Host</th><th className="px-3 py-2">Now</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((pass) => (
                <tr key={pass.id} className={cn(pass.status === 'Revoked' && 'text-slate-400')}>
                  <td className="px-3 py-2">
                    <div className="font-medium text-slate-900">{pass.fullName}{pass.isBlocked ? <Badge size="sm" tone="danger" className="ml-2">Blocked</Badge> : null}</div>
                    <div className="text-xs text-slate-500">{[pass.passNumber, pass.contractorName ?? pass.company, pass.vehicleRegistration].filter(Boolean).join(' · ')}</div>
                  </td>
                  <td className="px-3 py-2 text-slate-600">{pass.daysLabel} {pass.dailyFrom}–{pass.dailyTo}<div className="text-xs text-slate-500">{formatDate(pass.validFrom)} to {formatDate(pass.validTo)}</div></td>
                  <td className="px-3 py-2 text-slate-600">{pass.zones.length === 0 ? 'Public areas' : pass.zones.map((z) => z.name).join(', ')}</td>
                  <td className="px-3 py-2 text-slate-600">{pass.hostName}</td>
                  <td className="px-3 py-2">
                    {pass.status === 'Revoked' ? <span className="text-xs">Revoked: {pass.revokedReason}</span>
                      : pass.usableNow ? <Badge size="sm" tone="success">Good now</Badge> : <span className="text-xs text-slate-500">{pass.notUsableReason}</span>}
                  </td>
                  <td className="px-3 py-2">
                    <div className="flex justify-end gap-2">
                      {pass.status === 'Active' && pass.usableNow ? <button type="button" className={secondaryButton} disabled={today.isPending} onClick={() => void arrive(pass)}><LogIn className="h-4 w-4" />Arrived</button> : null}
                      {pass.status === 'Active' ? <button type="button" className={secondaryButton} aria-label={`Print ${pass.passNumber} badge`} onClick={() => void downloadPassBadge(pass)}><Printer className="h-4 w-4" /></button> : null}
                      {pass.status === 'Active' && canManage ? <button type="button" className="text-sm font-medium text-rose-700 hover:underline" onClick={() => void end(pass)}>Revoke</button> : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {issuing ? <PassDrawer factoryId={factoryId} onClose={() => setIssuing(false)} /> : null}
    </div>
  )
}

const WEEKDAYS = [[1, 'Mon'], [2, 'Tue'], [3, 'Wed'], [4, 'Thu'], [5, 'Fri'], [6, 'Sat'], [7, 'Sun']] as const

function PassDrawer({ factoryId, onClose }: { factoryId: string; onClose: () => void }) {
  const create = useCreatePass()
  const staff = useStaff('')
  const contractors = useContractors()
  const [form, setForm] = useState(() => ({
    factoryId, fullName: '', phone: '', company: '', idType: '', idNumber: '', hostUserId: '', contractorId: '', purpose: 'Contractor work',
    validFrom: new Date().toISOString().slice(0, 10), validTo: new Date(Date.now() + 90 * 86_400_000).toISOString().slice(0, 10), days: [1, 2, 3, 4, 5] as number[], dailyFrom: '07:00', dailyTo: '17:00',
    zoneIds: [] as string[], vehicle: '', notes: '',
  }))
  const zones = useZones(form.factoryId)
  const selectable = (zones.data ?? []).filter((x) => x.factoryId === form.factoryId && x.level !== 'Public')
  const set = (patch: Partial<typeof form>) => setForm((current) => ({ ...current, ...patch }))

  const submit = async () => {
    try {
      const pass = await create.mutateAsync({
        factoryId: form.factoryId,
        person: { fullName: form.fullName.trim(), phone: text(form.phone), email: null, company: text(form.company), idType: form.idNumber ? form.idType || 'National ID' : null, idNumber: text(form.idNumber), visitorId: null },
        hostUserId: form.hostUserId || null, validFrom: form.validFrom, validTo: form.validTo, days: form.days, dailyFrom: form.dailyFrom, dailyTo: form.dailyTo,
        zoneIds: form.zoneIds, contractorId: form.contractorId || null, purpose: form.purpose, vehicleRegistration: text(form.vehicle), notes: text(form.notes),
      })
      toast.success(`${pass.passNumber} issued`)
      void downloadPassBadge(pass)
      onClose()
    } catch { /* shown by the API client */ }
  }

  return (
    <FormDrawer open title="Issue a standing pass" onClose={onClose} closeOnOverlayClick={false}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={create.isPending || !form.factoryId || form.fullName.trim().length < 2 || form.days.length === 0} onClick={() => void submit()}>Issue and print badge</button></div>}>
      <div className="space-y-4">
        <Field label="Factory" required><FactorySelect value={form.factoryId} onChange={(value) => set({ factoryId: value, zoneIds: [] })} /></Field>
        <section className="grid gap-2 sm:grid-cols-2">
          <Field label="Holder's full name" required><input className={inputClass} value={form.fullName} onChange={(e) => set({ fullName: e.target.value })} /></Field>
          <Field label="Phone"><input className={inputClass} value={form.phone} onChange={(e) => set({ phone: e.target.value })} /></Field>
          <Field label="Company"><input className={inputClass} value={form.company} onChange={(e) => set({ company: e.target.value })} /></Field>
          <Field label="Contractor firm">
            <select className={inputClass} value={form.contractorId} onChange={(e) => set({ contractorId: e.target.value })}>
              <option value="">None</option>
              {(contractors.data ?? []).filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
          </Field>
          <Field label="ID type"><select className={inputClass} value={form.idType} onChange={(e) => set({ idType: e.target.value })}><option value="">ID type</option>{ID_TYPES.map((x) => <option key={x}>{x}</option>)}</select></Field>
          <Field label="ID number" hint="Only the last four characters are kept."><input className={inputClass} autoComplete="off" value={form.idNumber} onChange={(e) => set({ idNumber: e.target.value })} /></Field>
        </section>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Host" hint="Responsible for the holder; defaults to you.">
            <select className={inputClass} value={form.hostUserId} onChange={(e) => set({ hostUserId: e.target.value })}>
              <option value="">Me</option>
              {(staff.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}
            </select>
          </Field>
          <Field label="Purpose"><select className={inputClass} value={form.purpose} onChange={(e) => set({ purpose: e.target.value })}>{VISIT_PURPOSES.map((x) => <option key={x}>{x}</option>)}</select></Field>
          <Field label="First day" required><input type="date" className={inputClass} value={form.validFrom} onChange={(e) => set({ validFrom: e.target.value })} /></Field>
          <Field label="Last day" required hint="At most a year."><input type="date" className={inputClass} value={form.validTo} onChange={(e) => set({ validTo: e.target.value })} /></Field>
          <Field label="From"><input type="time" className={inputClass} value={form.dailyFrom} onChange={(e) => set({ dailyFrom: e.target.value })} /></Field>
          <Field label="Until"><input type="time" className={inputClass} value={form.dailyTo} onChange={(e) => set({ dailyTo: e.target.value })} /></Field>
        </div>
        <fieldset>
          <legend className="text-sm font-medium text-slate-700">Days</legend>
          <div className="mt-1 flex flex-wrap gap-2">
            {WEEKDAYS.map(([day, label]) => (
              <label key={day} className="flex items-center gap-1 text-sm">
                <input type="checkbox" checked={form.days.includes(day)} onChange={(e) => set({ days: e.target.checked ? [...form.days, day].sort() : form.days.filter((x) => x !== day) })} />{label}
              </label>
            ))}
          </div>
        </fieldset>
        <fieldset>
          <legend className="text-sm font-medium text-slate-700">Zones</legend>
          {selectable.length === 0 ? <p className="text-sm text-slate-500">Public areas only.</p> : (
            <div className="mt-1 grid gap-1 sm:grid-cols-2">
              {selectable.map((zone) => (
                <label key={zone.id} className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={form.zoneIds.includes(zone.id)} onChange={(e) => set({ zoneIds: e.target.checked ? [...form.zoneIds, zone.id] : form.zoneIds.filter((x) => x !== zone.id) })} />{zone.name}
                </label>
              ))}
            </div>
          )}
        </fieldset>
        <Field label="Vehicle registration"><input className={inputClass} placeholder="KCA 123A" value={form.vehicle} onChange={(e) => set({ vehicle: e.target.value })} /></Field>
        <Field label="Notes"><textarea className={textareaClass} value={form.notes} onChange={(e) => set({ notes: e.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}
