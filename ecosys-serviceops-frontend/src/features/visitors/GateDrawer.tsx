import { useEffect, useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { AlertTriangle, CheckCircle2, ShieldAlert } from 'lucide-react'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { Badge } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { formatDateTime } from '../../shared/utils/dates'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { useCheckIn, useGateCheck, useStaff, type Attendee, type GateConfirmation, type Visit } from './api'
import { CardInput } from './CardInput'

/**
 * The gate desk for one person: what their zones need (ID, PPE, induction, escort, permit), what is
 * still missing, and the check-in. A supervisor can let someone in past an overridable check with a reason.
 */
export function GateDrawer({ visit, attendee, canManage, onClose }: { visit: Visit; attendee: Attendee; canManage: boolean; onClose: () => void }) {
  const [confirmation, setConfirmation] = useState<GateConfirmation>({ idVerified: false, ppeConfirmed: false, recordInduction: false, escortUserId: null, overrideReason: null, cardCode: null })
  // The card is checked once typing pauses, not on every key.
  const [card, setCard] = useState('')
  useEffect(() => {
    const timer = window.setTimeout(() => setConfirmation((current) => ({ ...current, cardCode: card.trim() || null })), 400)
    return () => window.clearTimeout(timer)
  }, [card])
  // The reason does not change the checks; leaving it out stops a request on every keystroke.
  const check = useGateCheck(attendee.id, { ...confirmation, overrideReason: null })
  const staff = useStaff('')
  const checkIn = useCheckIn()
  const set = (patch: Partial<GateConfirmation>) => setConfirmation((current) => ({ ...current, ...patch }))
  const result = check.data
  const hard = useMemo(() => result?.issues.filter((x) => !x.overridable) ?? [], [result])
  const soft = useMemo(() => result?.issues.filter((x) => x.overridable) ?? [], [result])
  const canOverride = canManage && result !== undefined && !result.canEnter && result.overridable
  const ready = result !== undefined && (card.trim() || null) === (confirmation.cardCode ?? null)
    && (result.canEnter || (canOverride && Boolean(confirmation.overrideReason?.trim())))

  const submit = async () => {
    try {
      await checkIn.mutateAsync({ attendeeId: attendee.id, confirmation })
      toast.success(`${attendee.fullName} checked in`)
      onClose()
    } catch {
      // The API client shows the reason.
    }
  }

  return (
    <FormDrawer
      open
      title={`Check in ${attendee.fullName}`}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
          <button type="button" className={primaryButton} disabled={!ready || checkIn.isPending} onClick={() => void submit()}>
            {result && !result.canEnter && canOverride ? 'Let in anyway' : 'Check in'}
          </button>
        </div>
      }
    >
      <div className="space-y-5">
        <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm">
          <div className="font-semibold text-slate-900">{attendee.fullName}</div>
          <div className="text-slate-600">{[attendee.company, attendee.contractorName && attendee.contractorName !== attendee.company ? attendee.contractorName : null, attendee.phone].filter(Boolean).join(' · ') || '—'}</div>
          <div className="mt-2 text-slate-600">
            {visit.purpose} with <span className="font-medium text-slate-800">{visit.hostName}</span> · {visit.factoryName}
          </div>
          <div className="text-xs text-slate-500">Expected {formatDateTime(visit.expectedFrom)} to {formatDateTime(visit.expectedTo)} · {attendee.badgeNumber}</div>
          <div className="mt-2 flex flex-wrap gap-1">
            {visit.zones.length === 0 ? <Badge size="sm">Public areas only</Badge> : visit.zones.map((zone) => (
              <Badge key={zone.id} size="sm" tone={zone.level === 'Restricted' ? 'warning' : 'info'}>{zone.name}</Badge>
            ))}
            {visit.workOrderNumber ? <Badge size="sm">Job {visit.workOrderNumber}</Badge> : null}
            {visit.permitNumber ? <Badge size="sm">Permit {visit.permitNumber}</Badge> : null}
          </div>
        </div>

        <fieldset className="space-y-3">
          <legend className="text-sm font-semibold text-slate-800">Confirm at the gate</legend>
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-0.5" checked={confirmation.idVerified} onChange={(e) => set({ idVerified: e.target.checked })} />
            <span>
              ID checked
              <span className="block text-xs text-slate-500">
                {attendee.idType ? `${attendee.idType} ending ${attendee.idNumberMasked?.replace(/•/g, '') ?? ''}` : 'No ID recorded: look at their ID and tick when it matches the name.'}
              </span>
            </span>
          </label>
          {result && result.requirements.ppe.length > 0 ? (
            <label className="flex items-start gap-2 text-sm">
              <input type="checkbox" className="mt-0.5" checked={confirmation.ppeConfirmed} onChange={(e) => set({ ppeConfirmed: e.target.checked })} />
              <span>PPE issued or worn<span className="block text-xs text-slate-500">{result.requirements.ppe.join(', ')}</span></span>
            </label>
          ) : null}
          {result?.requirements.needsInduction ? (
            result.requirements.hasCurrentInduction ? (
              <div className="flex items-center gap-2 text-sm text-emerald-700"><CheckCircle2 className="h-4 w-4" />Current safety induction on record</div>
            ) : (
              <label className="flex items-start gap-2 text-sm">
                <input type="checkbox" className="mt-0.5" checked={confirmation.recordInduction} onChange={(e) => set({ recordInduction: e.target.checked })} />
                <span>Safety induction given now<span className="block text-xs text-slate-500">Recorded as a training record, valid a year.</span></span>
              </label>
            )
          ) : null}
          {result?.requirements.needsEscort ? (
            <Field label="Escort" required hint="The staff member who stays with them in the restricted zones.">
              <select className={inputClass} value={confirmation.escortUserId ?? ''} onChange={(e) => set({ escortUserId: e.target.value || null })}>
                <option value="">Choose the escort</option>
                {(staff.data ?? []).map((person) => <option key={person.id} value={person.id}>{person.fullName}{person.jobTitle ? ` · ${person.jobTitle}` : ''}</option>)}
              </select>
            </Field>
          ) : null}
          <Field label="Access card" hint="Optional. The card they wear inside and hand back at the gate.">
            <CardInput value={card} onChange={setCard} />
          </Field>
        </fieldset>

        {check.isLoading ? <Spinner label="Checking" /> : result ? (
          result.canEnter ? (
            <div className="flex items-center gap-2 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
              <CheckCircle2 className="h-4 w-4" />All checks met. They may go in.
            </div>
          ) : (
            <div className="space-y-2">
              {hard.map((issue) => (
                <div key={issue.code + issue.message} className="flex gap-2 rounded-lg border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800">
                  <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />{issue.message}
                </div>
              ))}
              {soft.map((issue) => (
                <div key={issue.code + issue.message} className="flex gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />{issue.message}
                </div>
              ))}
              {canOverride ? (
                <Field label="Reason for letting them in anyway" required hint="Recorded with your name in the visit and the audit log.">
                  <textarea className={textareaClass} value={confirmation.overrideReason ?? ''} onChange={(e) => set({ overrideReason: e.target.value || null })} />
                </Field>
              ) : hard.length === 0 ? (
                <p className="text-xs text-slate-500">A supervisor can let them in with a reason.</p>
              ) : null}
            </div>
          )
        ) : null}
      </div>
    </FormDrawer>
  )
}
