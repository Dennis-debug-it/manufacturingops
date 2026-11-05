import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { HardHat, Lock, LockOpen, Plus, ShieldCheck } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { api } from '../../../shared/api/apiClient'
import { useQueryClient } from '@tanstack/react-query'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import type { JobDetail } from '../jobs/types'
import { ENERGY_TYPES, PERMIT_TYPE_LABELS, useCertificateTypes, useIsolationPoints, useSafetyAction, type IsolationPoint } from './api'
import { PERMIT_TONES, PermitDrawer } from './SafetyPage'

const finished = (job: JobDetail) => ['Completed', 'Verified', 'Closed', 'Cancelled', 'Rejected'].includes(job.status)

/** On a job: the certificate and permits it needs, its permits, and the isolation locks on it. */
export function JobSafetyCard({ job }: { job: JobDetail }) {
  const canSupervise = usePermission('work-orders.assign')
  const canWork = usePermission('work-orders.complete') || canSupervise
  const [drawer, setDrawer] = useState<'requirements' | 'permit' | 'lock' | null>(null)
  const [openPermit, setOpenPermit] = useState<string | null>(null)
  const action = useSafetyAction()
  const open = !finished(job)
  const live = job.locks.filter((x) => !x.removedAt)

  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-1.5 text-sm font-semibold text-slate-800"><HardHat className="h-4 w-4 text-slate-500" />Safety</h2>
        {canSupervise && open ? <button type="button" className="text-xs font-medium text-[var(--app-primary)] underline" onClick={() => setDrawer('requirements')}>Change</button> : null}
      </div>
      <dl className="space-y-1 text-sm">
        <div className="flex gap-2"><dt className="w-24 shrink-0 text-slate-500">Certificate</dt><dd>{job.requiredCertificationName ?? 'None needed'}</dd></div>
        <div className="flex gap-2"><dt className="w-24 shrink-0 text-slate-500">Permits</dt><dd>{job.requiredPermitTypes.length ? job.requiredPermitTypes.map((x) => PERMIT_TYPE_LABELS[x] ?? x).join(', ') : 'None needed'}</dd></div>
      </dl>

      {job.permits.length > 0 ? (
        <ul className="mt-3 space-y-1">
          {job.permits.map((p) => (
            <li key={p.id}>
              <button type="button" className="flex w-full items-center justify-between gap-2 rounded-lg border border-slate-200 px-3 py-2 text-left text-sm hover:bg-slate-50" onClick={() => setOpenPermit(p.id)}>
                <span><span className="font-mono text-xs">{p.permitNumber}</span> {PERMIT_TYPE_LABELS[p.permitType] ?? p.permitType}<span className="block text-xs text-slate-500">to {formatDateTime(p.validTo)}</span></span>
                <Badge size="sm" tone={PERMIT_TONES[p.status]}>{p.status === 'Approved' ? 'Authorised' : p.status === 'Requested' ? 'Waiting' : p.status}</Badge>
              </button>
            </li>
          ))}
        </ul>
      ) : null}

      {job.locks.length > 0 ? (
        <ul className="mt-3 space-y-1 text-sm">
          {job.locks.map((l) => (
            <li key={l.id} className="flex items-center justify-between gap-2">
              <span className="flex items-center gap-1.5">{l.removedAt ? <LockOpen className="h-4 w-4 text-slate-300" /> : <Lock className="h-4 w-4 text-rose-600" />}
                <span><span className="font-medium">{l.pointCode}</span> {l.pointDescription} · lock {l.lockNumber}<span className="block text-xs text-slate-500">{l.appliedByName}, {formatDateTime(l.appliedAt)}{l.removedAt ? ` · off ${formatDateTime(l.removedAt)}` : ''}</span></span>
              </span>
              {!l.removedAt && canWork ? <button type="button" className={cn(secondaryButton, 'h-8 px-2 text-xs')} onClick={() => void action.mutateAsync({ path: `/locks/${l.id}/remove`, body: {} }).then(() => toast.success('Lock removed')).catch(() => undefined)}>Remove</button> : null}
            </li>
          ))}
        </ul>
      ) : null}
      {live.length > 0 ? <p className="mt-2 text-xs font-medium text-rose-700">{live.length} lock{live.length === 1 ? '' : 's'} on: the job can't be completed until they are off.</p> : null}

      {open && canWork ? (
        <div className="mt-3 flex flex-wrap gap-2">
          <button type="button" className={secondaryButton} onClick={() => setDrawer('permit')}><Plus className="h-4 w-4" />Request permit</button>
          <button type="button" className={secondaryButton} onClick={() => setDrawer('lock')}><Lock className="h-4 w-4" />Apply lock</button>
        </div>
      ) : null}

      <RequirementsDrawer open={drawer === 'requirements'} job={job} onClose={() => setDrawer(null)} />
      <RequestPermitDrawer open={drawer === 'permit'} job={job} onClose={() => setDrawer(null)} />
      <ApplyLockDrawer open={drawer === 'lock'} job={job} onClose={() => setDrawer(null)} />
      <PermitDrawer id={openPermit} onClose={() => setOpenPermit(null)} />
    </section>
  )
}

function RequirementsDrawer({ open, job, onClose }: { open: boolean; job: JobDetail; onClose: () => void }) {
  const types = useCertificateTypes()
  const queryClient = useQueryClient()
  const [cert, setCert] = useState('')
  const [permits, setPermits] = useState<string[]>([])
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (open) { setCert(job.requiredCertificationId ?? ''); setPermits(job.requiredPermitTypes) } }, [open, job])
  const save = async () => {
    setBusy(true)
    try {
      await api.put(`/api/factory/work-orders/${job.id}`, {
        title: job.title, description: job.description, workType: job.workType, priority: job.priority, dueDate: job.dueDate, needsLineStop: job.needsLineStop,
        estimatedHours: job.estimatedHours, requiredCertificationId: cert || null, requiredPermitTypes: permits,
      })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'job', job.id] })
      toast.success('Saved')
      onClose()
    } catch {
      // The API client shows the error.
    } finally {
      setBusy(false)
    }
  }
  return (
    <FormDrawer open={open} title="Safety requirements" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={busy} onClick={() => void save()}>Save</button></div>
    }>
      <div className="space-y-4 text-sm">
        <Field label="Certificate the technician must hold" hint="Only people holding a valid one can be assigned.">
          <select className={inputClass} value={cert} onChange={(event) => setCert(event.target.value)}>
            <option value="">None</option>{(types.data ?? []).map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
          </select>
        </Field>
        <fieldset>
          <legend className="mb-1 text-sm font-medium text-slate-700">Permits needed before work starts</legend>
          {Object.entries(PERMIT_TYPE_LABELS).filter(([v]) => v !== 'General').map(([value, label]) => (
            <label key={value} className="flex items-center gap-2 py-1"><input type="checkbox" checked={permits.includes(value)} onChange={(event) => setPermits(event.target.checked ? [...permits, value] : permits.filter((x) => x !== value))} />{label}</label>
          ))}
        </fieldset>
      </div>
    </FormDrawer>
  )
}

function RequestPermitDrawer({ open, job, onClose }: { open: boolean; job: JobDetail; onClose: () => void }) {
  const action = useSafetyAction()
  const [form, setForm] = useState({ type: 'Electrical', hazards: '', work: '', issuedTo: '', from: '', hours: '12' })
  useEffect(() => {
    if (!open) return
    const now = new Date()
    setForm({ type: job.requiredPermitTypes.find((t) => !job.permits.some((p) => p.permitType === t && ['Requested', 'Approved'].includes(p.status))) ?? 'Electrical', hazards: '', work: job.title, issuedTo: job.team.find((t) => t.isLead)?.name ?? '', from: new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16), hours: '12' })
  }, [open, job])
  const submit = async () => {
    const from = new Date(form.from)
    try {
      await action.mutateAsync({ path: '/permits', body: { workOrderId: job.id, permitType: form.type, hazards: form.hazards || null, workDescription: form.work || null, validFrom: from.toISOString(), validTo: new Date(from.getTime() + Number(form.hours) * 3_600_000).toISOString(), issuedToName: form.issuedTo || null } })
      toast.success('Permit requested. A supervisor must authorise it.')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title={`Request a permit · ${job.workOrderNumber}`} onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={action.isPending || !form.from} onClick={() => void submit()}>Request</button></div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Type" className="sm:col-span-2">
          <select className={inputClass} value={form.type} onChange={(event) => setForm({ ...form, type: event.target.value })}>
            {Object.entries(PERMIT_TYPE_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </Field>
        <Field label="Work" className="sm:col-span-2"><input className={inputClass} value={form.work} onChange={(event) => setForm({ ...form, work: event.target.value })} /></Field>
        <Field label="Hazards" className="sm:col-span-2"><textarea className={textareaClass} value={form.hazards} onChange={(event) => setForm({ ...form, hazards: event.target.value })} placeholder="415 V, rotating parts, hot surfaces, dust" /></Field>
        <Field label="Issued to"><input className={inputClass} value={form.issuedTo} onChange={(event) => setForm({ ...form, issuedTo: event.target.value })} /></Field>
        <Field label="From"><input type="datetime-local" className={inputClass} value={form.from} onChange={(event) => setForm({ ...form, from: event.target.value })} /></Field>
        <Field label="For" hint="At most 24 hours (normally one shift).">
          <select className={inputClass} value={form.hours} onChange={(event) => setForm({ ...form, hours: event.target.value })}>{['4', '8', '12', '24'].map((h) => <option key={h} value={h}>{h} hours</option>)}</select>
        </Field>
      </div>
    </FormDrawer>
  )
}

function ApplyLockDrawer({ open, job, onClose }: { open: boolean; job: JobDetail; onClose: () => void }) {
  const points = useIsolationPoints(open ? job.assetId : null)
  const action = useSafetyAction()
  const [pointId, setPointId] = useState('')
  const [lock, setLock] = useState('')
  const [verified, setVerified] = useState(false)
  useEffect(() => { if (open) { setPointId(''); setLock(''); setVerified(false) } }, [open])
  const live = new Set(job.locks.filter((x) => !x.removedAt).map((x) => x.isolationPointId))
  const point = (points.data ?? []).find((x) => x.id === pointId)
  return (
    <FormDrawer open={open} title={`Apply a lock · ${job.workOrderNumber}`} onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={!pointId || !lock.trim() || !verified || action.isPending}
          onClick={() => void action.mutateAsync({ path: '/locks', body: { workOrderId: job.id, isolationPointId: pointId, lockNumber: lock, zeroEnergyVerified: verified } }).then(() => { toast.success('Lock recorded'); onClose() }).catch(() => undefined)}>Record lock</button></div>
    }>
      <div className="space-y-4 text-sm">
        {!job.assetId ? <p className="text-slate-600">This job has no asset; isolation points are set up on assets.</p> : (points.data ?? []).length === 0 ? (
          <p className="rounded-lg bg-amber-50 p-3 text-amber-900">No isolation points on this asset yet. Add them on the asset's Isolation tab.</p>
        ) : (
          <>
            <Field label="Isolation point" required>
              <select className={inputClass} value={pointId} onChange={(event) => setPointId(event.target.value)}>
                <option value="">Choose</option>
                {(points.data ?? []).map((p) => <option key={p.id} value={p.id} disabled={live.has(p.id)}>{p.code} · {p.description} ({p.energyType}){live.has(p.id) ? ' — locked' : ''}</option>)}
              </select>
            </Field>
            {point?.method ? <p className="rounded-lg bg-slate-50 p-3 text-slate-700"><span className="font-medium">How:</span> {point.method}{point.location ? ` · ${point.location}` : ''}</p> : null}
            <Field label="Lock number" required><input className={inputClass} value={lock} onChange={(event) => setLock(event.target.value)} placeholder="L-07" /></Field>
            <label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={verified} onChange={(event) => setVerified(event.target.checked)} />I tried to start it and tested it dead: zero energy proved.</label>
          </>
        )}
      </div>
    </FormDrawer>
  )
}

/** On an asset: where its energy is isolated, for LOTO. */
export function IsolationPointsPanel({ assetId, canManage }: { assetId: string; canManage: boolean }) {
  const points = useIsolationPoints(assetId)
  const action = useSafetyAction()
  const [editing, setEditing] = useState<IsolationPoint | 'new' | null>(null)
  const [form, setForm] = useState({ code: '', energy: 'Electrical', description: '', location: '', method: '' })
  useEffect(() => {
    if (!editing) return
    setForm(editing === 'new' ? { code: `E${(points.data?.length ?? 0) + 1}`, energy: 'Electrical', description: '', location: '', method: '' }
      : { code: editing.code, energy: editing.energyType, description: editing.description, location: editing.location ?? '', method: editing.method ?? '' })
  }, [editing, points.data])
  const save = async () => {
    const body = { code: form.code, energyType: form.energy, description: form.description, location: form.location || null, method: form.method || null }
    try {
      await action.mutateAsync(editing === 'new' ? { path: `/assets/${assetId}/isolation-points`, body } : { method: 'put', path: `/isolation-points/${(editing as IsolationPoint).id}`, body })
      toast.success('Saved')
      setEditing(null)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Where this machine's energy is cut and locked before work: breakers, valves, pins.</p>
        {canManage ? <button type="button" className={secondaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add point</button> : null}
      </div>
      {(points.data ?? []).length === 0 ? <p className="rounded-lg border border-dashed border-slate-300 p-4 text-sm text-slate-500"><ShieldCheck className="mr-1 inline h-4 w-4" />No isolation points yet.</p> : (
        <ul className="divide-y divide-slate-100 rounded-[var(--radius-card)] border border-slate-200 bg-white">
          {(points.data ?? []).map((p) => (
            <li key={p.id} className="flex items-start gap-3 px-4 py-3 text-sm">
              <span className="w-12 shrink-0 font-mono font-semibold">{p.code}</span>
              <span className="flex-1"><span className="font-medium text-slate-900">{p.description}</span><span className="block text-xs text-slate-500">{p.energyType}{p.location ? ` · ${p.location}` : ''}</span>{p.method ? <span className="block text-xs text-slate-600">{p.method}</span> : null}</span>
              {canManage ? <button type="button" className="text-xs font-medium text-[var(--app-primary)] underline" onClick={() => setEditing(p)}>Edit</button> : null}
            </li>
          ))}
        </ul>
      )}
      <FormDrawer open={editing !== null} title={editing === 'new' ? 'New isolation point' : 'Edit isolation point'} onClose={() => setEditing(null)} footer={
        <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={() => setEditing(null)}>Cancel</button><button type="button" className={primaryButton} disabled={!form.code.trim() || !form.description.trim() || action.isPending} onClick={() => void save()}>Save</button></div>
      }>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Code" required><input className={inputClass} value={form.code} onChange={(event) => setForm({ ...form, code: event.target.value })} /></Field>
          <Field label="Energy"><select className={inputClass} value={form.energy} onChange={(event) => setForm({ ...form, energy: event.target.value })}>{ENERGY_TYPES.map((e) => <option key={e}>{e}</option>)}</select></Field>
          <Field label="What it is" required className="sm:col-span-2"><input className={inputClass} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} placeholder="Main isolator, MCC-2 panel 4" /></Field>
          <Field label="Where" className="sm:col-span-2"><input className={inputClass} value={form.location} onChange={(event) => setForm({ ...form, location: event.target.value })} placeholder="Dryer MCC room" /></Field>
          <Field label="How to isolate and prove dead" className="sm:col-span-2"><textarea className={textareaClass} value={form.method} onChange={(event) => setForm({ ...form, method: event.target.value })} /></Field>
        </div>
      </FormDrawer>
    </div>
  )
}
