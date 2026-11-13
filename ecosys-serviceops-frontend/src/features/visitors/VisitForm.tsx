import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { Plus, Trash2 } from 'lucide-react'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useAuth } from '../../shared/hooks/useAuth'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { useContractors } from '../factory/stores/api'
import { usePermits } from '../factory/people/api'
import {
  ID_TYPES, VISIT_PURPOSES, toLocalInput, useCreateVisit, useStaff, useVisitorPeople, useZones,
  type VisitorDetailsPayload, type VisitorPerson,
} from './api'
import { FactorySelect } from './FactorySelect'

type PersonRow = VisitorDetailsPayload & { key: string; returning: VisitorPerson | null }

const blankPerson = (): PersonRow => ({ key: crypto.randomUUID(), fullName: '', phone: null, email: null, company: null, idType: null, idNumber: null, visitorId: null, returning: null })
const text = (value: string) => value.trim() || null

/**
 * Raises a visit. Hosts invite their own visitors ahead (approved at once); the gate registers
 * walk-ins for a host, who is asked to approve.
 */
export function VisitForm({ walkIn, canGate, defaultFactoryId, onClose }: { walkIn: boolean; canGate: boolean; defaultFactoryId: string; onClose: () => void }) {
  const { user } = useAuth()
  const create = useCreateVisit()
  const now = useMemo(() => new Date(), [])
  const [factoryId, setFactoryId] = useState(defaultFactoryId)
  const [purpose, setPurpose] = useState<string>(walkIn ? 'Delivery' : 'Meeting')
  const [hostUserId, setHostUserId] = useState(walkIn ? '' : user?.userId ?? '')
  const [from, setFrom] = useState(toLocalInput(now))
  const [to, setTo] = useState(toLocalInput(new Date(now.getTime() + 4 * 3_600_000)))
  const [people, setPeople] = useState<PersonRow[]>([blankPerson()])
  const [zoneIds, setZoneIds] = useState<string[]>([])
  const [contractorId, setContractorId] = useState('')
  const [permitId, setPermitId] = useState('')
  const [vehicle, setVehicle] = useState('')
  const [notes, setNotes] = useState('')
  const [search, setSearch] = useState('')

  const staff = useStaff('')
  const zones = useZones(factoryId)
  const contractors = useContractors()
  const permits = usePermits('Approved')
  const returning = useVisitorPeople(search, false, canGate && search.trim().length >= 2)
  const selectableZones = (zones.data ?? []).filter((x) => x.factoryId === factoryId && x.level !== 'Public')
  const contractorWork = purpose === 'Contractor work'

  const update = (key: string, patch: Partial<PersonRow>) => setPeople((rows) => rows.map((row) => (row.key === key ? { ...row, ...patch } : row)))
  const addReturning = (person: VisitorPerson) => {
    setPeople((rows) => [...rows.filter((row) => row.fullName.trim() || row.visitorId), {
      ...blankPerson(), fullName: person.fullName, phone: person.phone, company: person.company, visitorId: person.id, returning: person,
    }])
    if (person.contractorId && !contractorId) setContractorId(person.contractorId)
    setSearch('')
  }

  const submit = async () => {
    if (!factoryId) return toast.error('Choose the factory being visited.')
    if (!hostUserId) return toast.error('Choose the host.')
    const visitors = people.filter((row) => row.fullName.trim() || row.visitorId)
    if (visitors.length === 0) return toast.error('Add at least one visitor.')
    try {
      const visit = await create.mutateAsync({
        factoryId,
        purpose,
        visitors: visitors.map(({ fullName, phone, email, company, idType, idNumber, visitorId }) => ({
          fullName: fullName.trim(), phone: phone ? text(phone) : null, email: email ? text(email) : null, company: company ? text(company) : null,
          idType: idNumber ? idType ?? 'National ID' : null, idNumber: idNumber ? text(idNumber) : null, visitorId,
        })),
        hostUserId,
        expectedFrom: walkIn ? null : new Date(from).toISOString(),
        expectedTo: new Date(to).toISOString(),
        zoneIds,
        workPermitId: permitId || null,
        contractorId: contractorId || null,
        vehicleRegistration: text(vehicle),
        notes: text(notes),
        walkIn,
      })
      toast.success(visit.status === 'PendingApproval' ? `${visit.visitNumber} sent to ${visit.hostName} to approve` : `${visit.visitNumber} approved`)
      onClose()
    } catch {
      // The API client shows the reason.
    }
  }

  return (
    <FormDrawer
      open
      title={walkIn ? 'Register a walk-in' : 'Invite a visitor'}
      onClose={onClose}
      closeOnOverlayClick={false}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
          <button type="button" className={primaryButton} disabled={create.isPending} onClick={() => void submit()}>{walkIn ? 'Register and ask the host' : 'Save visit'}</button>
        </div>
      }
    >
      <div className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Factory" required><FactorySelect value={factoryId} onChange={(value) => { setFactoryId(value); setZoneIds([]) }} /></Field>
          <Field label="Purpose" required>
            <select className={inputClass} value={purpose} onChange={(e) => setPurpose(e.target.value)}>
              {VISIT_PURPOSES.map((x) => <option key={x}>{x}</option>)}
            </select>
          </Field>
        </div>

        {canGate ? (
          <Field label="Host" required hint={walkIn ? 'The host is asked to approve before the visitor goes in.' : undefined}>
            <select className={inputClass} value={hostUserId} onChange={(e) => setHostUserId(e.target.value)}>
              <option value="">Who are they visiting?</option>
              {(staff.data ?? []).map((person) => <option key={person.id} value={person.id}>{person.fullName}{person.jobTitle ? ` · ${person.jobTitle}` : ''}</option>)}
            </select>
          </Field>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-2">
          {walkIn ? null : <Field label="Expected from" required><input type="datetime-local" className={inputClass} value={from} onChange={(e) => setFrom(e.target.value)} /></Field>}
          <Field label={walkIn ? 'Expected to leave by' : 'Until'} required><input type="datetime-local" className={inputClass} value={to} onChange={(e) => setTo(e.target.value)} /></Field>
        </div>

        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-semibold text-slate-800">Visitors</h4>
            <button type="button" className="inline-flex items-center gap-1 text-sm font-medium text-[var(--app-primary)]" onClick={() => setPeople((rows) => [...rows, blankPerson()])}>
              <Plus className="h-4 w-4" />Add person
            </button>
          </div>
          {canGate ? (
            <div className="relative">
              <input aria-label="Find a returning visitor" className={inputClass} placeholder="Returning visitor? Search name, company, phone or ID number" value={search} onChange={(e) => setSearch(e.target.value)} />
              {search.trim().length >= 2 && (returning.data?.length ?? 0) > 0 ? (
                <ul className="absolute z-10 mt-1 max-h-56 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-lg">
                  {returning.data!.map((person) => (
                    <li key={person.id}>
                      <button type="button" className="w-full px-3 py-2 text-left text-sm hover:bg-slate-50" onClick={() => addReturning(person)}>
                        <span className="font-medium">{person.fullName}</span>
                        <span className="text-slate-500"> {[person.company, person.phone, person.idNumberMasked].filter(Boolean).join(' · ')}</span>
                        {person.isBlocked ? <span className="ml-2 text-xs font-semibold text-rose-700">Blocked</span> : null}
                      </button>
                    </li>
                  ))}
                </ul>
              ) : null}
            </div>
          ) : null}
          {people.map((row, index) => (
            <div key={row.key} className="space-y-2 rounded-lg border border-slate-200 p-3">
              <div className="flex items-center justify-between text-xs font-medium uppercase tracking-wide text-slate-500">
                <span>Person {index + 1}{row.returning ? ' · returning' : ''}</span>
                {people.length > 1 ? (
                  <button type="button" aria-label="Remove person" className="text-slate-400 hover:text-rose-600" onClick={() => setPeople((rows) => rows.filter((x) => x.key !== row.key))}>
                    <Trash2 className="h-4 w-4" />
                  </button>
                ) : null}
              </div>
              {row.returning?.isBlocked ? <p className="rounded bg-rose-50 px-2 py-1 text-xs text-rose-700">Blocked: {row.returning.blockedReason}</p> : null}
              <div className="grid gap-2 sm:grid-cols-2">
                <input className={inputClass} aria-label={`Person ${index + 1} full name`} placeholder="Full name *" value={row.fullName} onChange={(e) => update(row.key, { fullName: e.target.value })} />
                <input className={inputClass} aria-label={`Person ${index + 1} phone`} placeholder="Phone" value={row.phone ?? ''} onChange={(e) => update(row.key, { phone: e.target.value })} />
                <input className={inputClass} aria-label={`Person ${index + 1} company`} placeholder="Company" value={row.company ?? ''} onChange={(e) => update(row.key, { company: e.target.value })} />
                <input className={inputClass} aria-label={`Person ${index + 1} email`} placeholder="Email" type="email" value={row.email ?? ''} onChange={(e) => update(row.key, { email: e.target.value })} />
                {canGate ? (
                  <>
                    <select aria-label={`Person ${index + 1} ID type`} className={inputClass} value={row.idType ?? ''} onChange={(e) => update(row.key, { idType: e.target.value || null })}>
                      <option value="">ID type</option>
                      {ID_TYPES.map((x) => <option key={x}>{x}</option>)}
                    </select>
                    <input aria-label={`Person ${index + 1} ID number`} className={inputClass} placeholder={row.returning?.idNumberMasked ? `ID on file ${row.returning.idNumberMasked}` : 'ID number'} value={row.idNumber ?? ''}
                      onChange={(e) => update(row.key, { idNumber: e.target.value })} autoComplete="off" />
                  </>
                ) : null}
              </div>
            </div>
          ))}
          {canGate ? <p className="text-xs text-slate-500">ID numbers are not stored: only the last four characters and a fingerprint to recognise the person next time.</p> : null}
        </section>

        <fieldset className="space-y-1">
          <legend className="text-sm font-medium text-slate-700">Zones they may enter</legend>
          {selectableZones.length === 0 ? (
            <p className="text-sm text-slate-500">{factoryId ? 'This factory has no controlled zones set up.' : 'Choose the factory first.'}</p>
          ) : (
            <div className="grid gap-1 sm:grid-cols-2">
              {selectableZones.map((zone) => (
                <label key={zone.id} className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={zoneIds.includes(zone.id)} onChange={(e) => setZoneIds((ids) => (e.target.checked ? [...ids, zone.id] : ids.filter((x) => x !== zone.id)))} />
                  <span>{zone.name}</span>
                  {zone.level === 'Restricted' ? <span className="text-xs text-amber-700">restricted</span> : null}
                </label>
              ))}
            </div>
          )}
          <span className="block text-xs text-slate-500">Public areas (reception, car park) are always allowed. Restricted zones may need an induction, PPE, an escort or a permit at the gate.</span>
        </fieldset>

        {contractorWork ? (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Contractor" hint="Their insurance and tax compliance must be current to enter.">
              <select className={inputClass} value={contractorId} onChange={(e) => setContractorId(e.target.value)}>
                <option value="">Not a contractor firm</option>
                {(contractors.data ?? []).filter((x) => x.isActive).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
              </select>
            </Field>
            <Field label="Permit to work" hint="Needed for zones that require a permit; it names the job.">
              <select className={inputClass} value={permitId} onChange={(e) => setPermitId(e.target.value)}>
                <option value="">No permit</option>
                {(permits.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.permitNumber} · {x.workOrderNumber} {x.workOrderTitle}</option>)}
              </select>
            </Field>
          </div>
        ) : null}

        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Vehicle registration"><input className={inputClass} placeholder="KCA 123A" value={vehicle} onChange={(e) => setVehicle(e.target.value)} /></Field>
        </div>
        <Field label="Notes for the gate"><textarea className={textareaClass} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}
