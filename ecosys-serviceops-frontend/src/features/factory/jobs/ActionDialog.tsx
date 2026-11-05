import { useState } from 'react'
import toast from 'react-hot-toast'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { SignaturePad } from '../../../shared/components/SignaturePad'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFailureCodes, useJobActions, useTechnicianOptions } from './api'
import { ACTION_LABELS, HOLD_REASONS, type ActionPayload, type JobAction, type JobDetail } from './types'

const NEEDS_REASON: JobAction[] = ['reject', 'cancel', 'return', 'reopen']

/** Collects what one action needs, then sends it (queued when offline). */
export function ActionDialog({ job, action, onClose }: { job: JobDetail; action: JobAction; onClose: () => void }) {
  const actions = useJobActions(job)
  const technicians = useTechnicianOptions()
  const codes = useFailureCodes(job.assetCategoryId)
  const [busy, setBusy] = useState(false)
  const [reason, setReason] = useState('')
  const [holdReason, setHoldReason] = useState<string>(HOLD_REASONS[0])
  const [team, setTeam] = useState<string[]>(job.team.map((x) => x.technicianId))
  const [start, setStart] = useState(job.scheduledStartAt?.slice(0, 16) ?? '')
  const [hours, setHours] = useState(job.estimatedHours?.toString() ?? '')
  const [workDone, setWorkDone] = useState('')
  const [problem, setProblem] = useState(job.problem?.id ?? '')
  const [cause, setCause] = useState('')
  const [remedy, setRemedy] = useState('')
  const [backInService, setBackInService] = useState(true)
  const [signature, setSignature] = useState<string | null>(null)

  const byKind = (kind: string) => (codes.data ?? []).filter((x) => x.kind === kind)
  const needsProblem = job.workType === 'Corrective' || job.isBreakdown

  const ready = (() => {
    if (NEEDS_REASON.includes(action)) return reason.trim().length > 0
    if (action === 'assign') return team.length > 0
    if (action === 'plan') return Boolean(start)
    if (action === 'complete') return workDone.trim().length > 0 && (!needsProblem || Boolean(problem))
    return true
  })()

  const submit = async () => {
    const payload: ActionPayload = { reason: reason.trim() || null }
    if (action === 'hold') payload.holdReason = holdReason
    if (action === 'assign') payload.technicianIds = team
    if (action === 'plan') { payload.scheduledStartAt = new Date(start).toISOString(); payload.estimatedHours = hours ? Number(hours) : null }
    if (action === 'complete') {
      Object.assign(payload, {
        workDone, problemCodeId: problem || null, causeCodeId: cause || null, remedyCodeId: remedy || null,
        assetBackInService: backInService, signatureDataUrl: signature,
      })
    }

    setBusy(true)
    try {
      const result = await actions.act(action, payload)
      toast.success(result.queued ? `${ACTION_LABELS[action]}: saved, will sync when online` : `${ACTION_LABELS[action]}: done`)
      onClose()
    } catch {
      // The API client shows the error.
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormDrawer open title={`${ACTION_LABELS[action]} · ${job.workOrderNumber}`} onClose={onClose} footer={
      <div className="flex justify-end gap-2">
        <button type="button" className={secondaryButton} onClick={onClose}>Back</button>
        <button type="button" className={primaryButton} disabled={!ready || busy} onClick={() => void submit()}>{ACTION_LABELS[action]}</button>
      </div>
    }>
      <div className="grid gap-4">
        {action === 'assign' ? (
          <Field label="Technicians" hint="The first one ticked leads the job.">
            <div className="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
              {(technicians.data ?? []).filter((t) => t.isActive).map((t) => (
                <label key={t.id} className="flex items-center gap-2 text-sm">
                  <input type="checkbox" checked={team.includes(t.id)} onChange={(event) => setTeam(event.target.checked ? [...team, t.id] : team.filter((x) => x !== t.id))} />
                  {t.fullName}{t.skillCategory ? <span className="text-xs text-slate-500">· {t.skillCategory}</span> : null}
                </label>
              ))}
            </div>
          </Field>
        ) : null}
        {action === 'plan' ? (
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Planned start" required><input type="datetime-local" className={inputClass} value={start} onChange={(event) => setStart(event.target.value)} /></Field>
            <Field label="Estimated hours"><input type="number" min={0} step={0.5} className={inputClass} value={hours} onChange={(event) => setHours(event.target.value)} /></Field>
          </div>
        ) : null}
        {action === 'hold' ? (
          <Field label="Waiting for">
            <select className={inputClass} value={holdReason} onChange={(event) => setHoldReason(event.target.value)}>
              {HOLD_REASONS.map((x) => <option key={x}>{x}</option>)}
            </select>
          </Field>
        ) : null}
        {action === 'complete' ? (
          <>
            <Field label="Work done" required><textarea className={textareaClass} value={workDone} onChange={(event) => setWorkDone(event.target.value)} /></Field>
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Problem found" required={needsProblem}>
                <select className={inputClass} value={problem} onChange={(event) => setProblem(event.target.value)}>
                  <option value="">—</option>
                  {byKind('Problem').map((x) => <option key={x.id} value={x.id}>{x.name} ({x.code})</option>)}
                </select>
              </Field>
              <Field label="Cause">
                <select className={inputClass} value={cause} onChange={(event) => setCause(event.target.value)}>
                  <option value="">—</option>
                  {byKind('Cause').map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                </select>
              </Field>
              <Field label="What was done">
                <select className={inputClass} value={remedy} onChange={(event) => setRemedy(event.target.value)}>
                  <option value="">—</option>
                  {byKind('Remedy').map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                </select>
              </Field>
            </div>
            {job.isBreakdown && !job.downtimeEndedAt ? (
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input type="checkbox" checked={backInService} onChange={(event) => setBackInService(event.target.checked)} />
                The machine is running again
              </label>
            ) : null}
            <Field label="Signature" hint="Sign with your finger.">
              <SignaturePad onChange={setSignature} className="h-32 rounded-lg border border-slate-200" />
            </Field>
          </>
        ) : null}
        <Field label={NEEDS_REASON.includes(action) ? 'Reason' : 'Note'} required={NEEDS_REASON.includes(action)}>
          <textarea className={textareaClass} value={reason} onChange={(event) => setReason(event.target.value)} />
        </Field>
      </div>
    </FormDrawer>
  )
}
