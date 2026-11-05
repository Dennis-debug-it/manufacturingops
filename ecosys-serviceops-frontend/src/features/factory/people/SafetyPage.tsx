import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, CheckCircle2, Circle, HardHat, Lock, Plus, ShieldAlert } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, LocationSelect, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { AssetPicker, type PickedAsset } from '../planning/AssetPicker'
import {
  INCIDENT_KIND_LABELS, PERMIT_TYPE_LABELS, useIncidentSummary, useIncidents, useLocks, usePermit, usePermits, usePpe, useSafetyAction, useTeam, useTraining,
  type Incident, type PermitSummary,
} from './api'

const TABS = [{ id: 'permits', label: 'Permits' }, { id: 'locks', label: 'Locks on' }, { id: 'incidents', label: 'Incidents' }, { id: 'training', label: 'Training' }, { id: 'ppe', label: 'PPE' }] as const
type Tab = (typeof TABS)[number]['id']

export const PERMIT_TONES: Record<PermitSummary['status'], BadgeTone> = { Requested: 'warning', Approved: 'success', Closed: 'neutral', Rejected: 'danger', Cancelled: 'neutral' }
const PERMIT_STATUS_LABELS: Record<PermitSummary['status'], string> = { Requested: 'Waiting for authorisation', Approved: 'Authorised', Closed: 'Closed out', Rejected: 'Refused', Cancelled: 'Cancelled' }

/** Safety: permits to work, isolations, incidents and near misses, training and PPE. */
export function SafetyPage() {
  const [params, setParams] = useSearchParams()
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'permits') as Tab
  const [reporting, setReporting] = useState(false)
  return (
    <div className="space-y-4">
      <PageHeader title="Safety" subtitle="Permits to work, isolations, incidents and near misses, training and protective equipment."
        primaryAction={<button type="button" className={primaryButton} onClick={() => setReporting(true)}><ShieldAlert className="h-4 w-4" />Report an incident</button>} />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'permits' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'permits' ? <PermitsTab openId={params.get('permit')} /> : tab === 'locks' ? <LocksTab /> : tab === 'incidents' ? <IncidentsTab openId={params.get('incident')} /> : tab === 'training' ? <TrainingTab /> : <PpeTab />}
      <ReportIncidentDrawer open={reporting} onClose={() => setReporting(false)} />
    </div>
  )
}

function PermitsTab({ openId }: { openId: string | null }) {
  const [status, setStatus] = useState('')
  const permits = usePermits(status || undefined)
  const [open, setOpen] = useState<string | null>(openId)
  return (
    <div className="space-y-3">
      <select aria-label="Status" className={cn(inputClass, 'w-60')} value={status} onChange={(event) => setStatus(event.target.value)}>
        <option value="">All permits</option>
        {Object.entries(PERMIT_STATUS_LABELS).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select>
      {permits.isLoading ? <Spinner /> : (permits.data ?? []).length === 0 ? <EmptyState icon={HardHat} title="No permits" description="Request a permit from the job that needs one." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Permit</th><th className="px-4 py-2">Job</th><th className="px-4 py-2">Valid</th><th className="px-4 py-2">Precautions</th><th className="px-4 py-2">Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(permits.data ?? []).map((p) => (
                <tr key={p.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(p.id)}>
                  <td className="px-4 py-2"><span className="font-mono text-xs">{p.permitNumber}</span><span className="block font-medium text-slate-900">{PERMIT_TYPE_LABELS[p.permitType] ?? p.permitType}</span></td>
                  <td className="px-4 py-2"><span className="font-mono text-xs">{p.workOrderNumber}</span><span className="block text-slate-700">{p.workOrderTitle}</span></td>
                  <td className="whitespace-nowrap px-4 py-2 text-xs text-slate-600">{formatDateTime(p.validFrom)}<span className="block">to {formatDateTime(p.validTo)}</span></td>
                  <td className="px-4 py-2 tabular-nums">{p.checksDone}/{p.checks}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={PERMIT_TONES[p.status]}>{PERMIT_STATUS_LABELS[p.status]}</Badge>{p.approvedByName && p.status !== 'Requested' ? <span className="block text-xs text-slate-500">{p.approvedByName}</span> : null}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <PermitDrawer id={open} onClose={() => setOpen(null)} />
    </div>
  )
}

/** One permit: precautions to tick, authorise or refuse, the locks on its job, and close-out. */
export function PermitDrawer({ id, onClose }: { id: string | null; onClose: () => void }) {
  const links = useFactoryLinks()
  const { user } = useAuth()
  const canAuthorise = usePermission('work-orders.assign')
  const { data } = usePermit(id)
  const action = useSafetyAction()
  const [note, setNote] = useState('')
  const [gas, setGas] = useState('')
  useEffect(() => { setNote(''); setGas('') }, [id])

  const run = async (path: string, done: string, body?: unknown) => {
    try {
      await action.mutateAsync({ path, body })
      toast.success(done)
      setNote('')
    } catch {
      // The API client shows the error.
    }
  }

  const permit = data?.permit
  const ticked = data ? data.checks.every((x) => x.done) : false
  const own = permit?.requestedByName === user?.fullName
  return (
    <FormDrawer open={Boolean(id)} title={permit ? `${permit.permitNumber} · ${PERMIT_TYPE_LABELS[permit.permitType] ?? permit.permitType}` : 'Permit'} onClose={onClose} footer={permit ? (
      <div className="flex flex-wrap justify-end gap-2">
        {permit.status === 'Requested' ? <button type="button" className={secondaryButton} onClick={() => void run(`/permits/${permit.id}/cancel`, 'Permit cancelled', { notes: note || null })}>Cancel permit</button> : null}
        {permit.status === 'Requested' && canAuthorise && !own ? (
          <>
            <button type="button" className={secondaryButton} disabled={!note.trim()} onClick={() => void run(`/permits/${permit.id}/decide`, 'Permit refused', { approve: false, note })}>Refuse</button>
            <button type="button" className={primaryButton} disabled={!ticked || action.isPending} onClick={() => void run(`/permits/${permit.id}/decide`, 'Permit authorised', { approve: true, note: note || null, gasTest: gas || null })}>Authorise</button>
          </>
        ) : null}
        {permit.status === 'Approved' ? <button type="button" className={primaryButton} disabled={!note.trim() || action.isPending} onClick={() => void run(`/permits/${permit.id}/close`, 'Permit closed out', { notes: note })}>Close out</button> : null}
      </div>
    ) : undefined}>
      {!data || !permit ? <Spinner /> : (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap items-center gap-2"><Badge tone={PERMIT_TONES[permit.status]}>{PERMIT_STATUS_LABELS[permit.status]}</Badge><span className="text-slate-600">{formatDateTime(permit.validFrom)} – {formatDateTime(permit.validTo)}</span></div>
          <dl className="grid grid-cols-[minmax(0,8rem)_1fr] gap-x-4 gap-y-2">
            <dt className="text-slate-500">Job</dt><dd><Link className="underline" to={links.job(permit.workOrderId)}>{permit.workOrderNumber}</Link> {permit.workOrderTitle}{permit.assetCode ? ` · ${permit.assetCode}` : ''}</dd>
            {data.workDescription ? <><dt className="text-slate-500">Work</dt><dd>{data.workDescription}</dd></> : null}
            {data.hazards ? <><dt className="text-slate-500">Hazards</dt><dd>{data.hazards}</dd></> : null}
            <dt className="text-slate-500">Issued to</dt><dd>{data.issuedToName ?? '—'}</dd>
            <dt className="text-slate-500">Requested by</dt><dd>{permit.requestedByName ?? '—'}</dd>
            {permit.approvedByName ? <><dt className="text-slate-500">{permit.status === 'Rejected' ? 'Refused by' : 'Authorised by'}</dt><dd>{permit.approvedByName}{permit.approvedAt ? `, ${formatDateTime(permit.approvedAt)}` : ''}{data.decisionNote ? ` — ${data.decisionNote}` : ''}</dd></> : null}
            {data.gasTest ? <><dt className="text-slate-500">Gas test</dt><dd>{data.gasTest}</dd></> : null}
            {data.closedAt ? <><dt className="text-slate-500">Closed out</dt><dd>{formatDateTime(data.closedAt)} by {data.closedByName}: {data.closeOutNotes}</dd></> : null}
          </dl>

          <section>
            <h3 className="mb-1 font-semibold text-slate-800">Precautions · {data.checks.filter((x) => x.done).length}/{data.checks.length}</h3>
            <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
              {data.checks.map((check) => (
                <li key={check.id}>
                  <button type="button" disabled={permit.status !== 'Requested'} className="flex w-full items-start gap-3 px-3 py-2 text-left hover:bg-slate-50 disabled:cursor-default disabled:hover:bg-transparent"
                    onClick={() => void action.mutateAsync({ path: `/permits/${permit.id}/checks/${check.id}`, body: { done: !check.done } }).catch(() => undefined)}>
                    {check.done ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" /> : <Circle className="mt-0.5 h-5 w-5 shrink-0 text-slate-300" />}
                    <span>{check.prompt}{check.doneByName ? <span className="block text-xs text-slate-500">{check.doneByName}</span> : null}</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>

          {data.locks.length > 0 ? (
            <section>
              <h3 className="mb-1 font-semibold text-slate-800">Isolations on the job</h3>
              <ul className="space-y-1">{data.locks.map((l) => <li key={l.id} className="flex items-center gap-2"><Lock className={cn('h-4 w-4', l.removedAt ? 'text-slate-300' : 'text-rose-600')} />{l.pointCode} {l.pointDescription} · lock {l.lockNumber}{l.removedAt ? ' · removed' : ' · on'}</li>)}</ul>
            </section>
          ) : null}

          {permit.status === 'Requested' && permit.permitType === 'ConfinedSpace' && canAuthorise && !own ? (
            <Field label="Gas test before entry" required hint="O₂ %, LEL %, CO ppm, H₂S ppm."><input className={inputClass} value={gas} onChange={(event) => setGas(event.target.value)} placeholder="O2 20.9%, LEL 0%, CO 2 ppm, H2S 0" /></Field>
          ) : null}
          {permit.status === 'Requested' || permit.status === 'Approved' ? (
            <Field label={permit.status === 'Approved' ? 'Close-out: how the area was left' : 'Note'} required={permit.status === 'Approved'}>
              <textarea className={textareaClass} value={note} onChange={(event) => setNote(event.target.value)} placeholder={permit.status === 'Approved' ? 'Work finished, guards back on, area clean, locks off' : undefined} />
            </Field>
          ) : null}
          {permit.status === 'Requested' && own ? <p className="rounded-lg bg-amber-50 p-3 text-amber-900">You requested this permit, so someone else must authorise it.</p> : null}
        </div>
      )}
    </FormDrawer>
  )
}

function LocksTab() {
  const locks = useLocks()
  const links = useFactoryLinks()
  if (locks.isLoading) return <Spinner />
  const rows = locks.data ?? []
  if (rows.length === 0) return <EmptyState icon={Lock} title="No locks on" description="Every isolation applied for a job shows here until it is removed." />
  return (
    <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
      <table className="w-full text-sm">
        <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Point</th><th className="px-4 py-2">Lock</th><th className="px-4 py-2">Job</th><th className="px-4 py-2">Applied</th></tr></thead>
        <tbody className="divide-y divide-slate-100">
          {rows.map((l) => (
            <tr key={l.id}>
              <td className="px-4 py-2"><span className="font-mono text-xs">{l.assetCode} · {l.pointCode}</span><span className="block text-slate-700">{l.pointDescription}</span><span className="text-xs text-slate-500">{l.energyType}</span></td>
              <td className="px-4 py-2 font-mono">{l.lockNumber}</td>
              <td className="px-4 py-2"><Link className="font-mono text-xs underline" to={links.job(l.workOrderId)}>{l.workOrderNumber}</Link><span className="block text-slate-700">{l.workOrderTitle}</span></td>
              <td className="px-4 py-2 text-xs text-slate-600">{l.appliedByName}<span className="block">{formatDateTime(l.appliedAt)}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const INCIDENT_TONES: Record<Incident['status'], BadgeTone> = { Open: 'danger', Investigating: 'warning', Closed: 'neutral' }

function IncidentsTab({ openId }: { openId: string | null }) {
  const [kind, setKind] = useState('')
  const [status, setStatus] = useState('')
  const incidents = useIncidents({ kind: kind || undefined, status: status || undefined })
  const summary = useIncidentSummary()
  const [open, setOpen] = useState<string | null>(openId)
  const selected = (incidents.data ?? []).find((x) => x.id === open) ?? null
  return (
    <div className="space-y-4">
      {summary.data ? (
        <div className="grid gap-3 sm:grid-cols-4">
          <Tile label="Days since a lost-time injury" value={summary.data.daysSinceLostTimeInjury == null ? 'None recorded' : String(summary.data.daysSinceLostTimeInjury)} />
          <Tile label="Injuries, 12 months" value={String(summary.data.injuries)} warn={summary.data.injuries > 0} />
          <Tile label="Near misses, 12 months" value={String(summary.data.nearMisses)} />
          <Tile label="Still open" value={String(summary.data.open)} warn={summary.data.open > 0} />
        </div>
      ) : null}
      <div className="flex flex-wrap gap-2">
        <select aria-label="Kind" className={cn(inputClass, 'w-44')} value={kind} onChange={(event) => setKind(event.target.value)}>
          <option value="">Any kind</option>{Object.entries(INCIDENT_KIND_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
        </select>
        <select aria-label="Status" className={cn(inputClass, 'w-40')} value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">Any status</option><option>Open</option><option>Investigating</option><option>Closed</option>
        </select>
      </div>
      {incidents.isLoading ? <Spinner /> : (incidents.data ?? []).length === 0 ? <EmptyState icon={ShieldAlert} title="No incidents" description="Report near misses too: they show where the next accident would happen." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Incident</th><th className="px-4 py-2">When</th><th className="px-4 py-2">Where</th><th className="px-4 py-2">Corrective job</th><th className="px-4 py-2">Status</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(incidents.data ?? []).map((i) => (
                <tr key={i.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(i.id)}>
                  <td className="px-4 py-2"><span className="font-medium text-slate-900">{i.title}</span><span className="block text-xs text-slate-500"><span className="font-mono">{i.incidentNumber}</span> · {INCIDENT_KIND_LABELS[i.kind] ?? i.kind} · {i.severity}</span></td>
                  <td className="whitespace-nowrap px-4 py-2 text-slate-600">{formatDateTime(i.occurredAt)}</td>
                  <td className="px-4 py-2 text-slate-600">{i.assetCode ? <span className="font-mono text-xs">{i.assetCode}</span> : i.orgNodeName}</td>
                  <td className="px-4 py-2 text-xs">{i.correctiveWorkOrderNumber ? `${i.correctiveWorkOrderNumber} · ${i.correctiveWorkOrderStatus}` : '—'}</td>
                  <td className="px-4 py-2"><Badge size="sm" tone={INCIDENT_TONES[i.status]}>{i.status}</Badge></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <IncidentDrawer incident={selected} onClose={() => setOpen(null)} />
    </div>
  )
}

function Tile({ label, value, warn }: { label: string; value: string; warn?: boolean | undefined }) {
  return (
    <div className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <p className="text-sm text-slate-600">{label}</p>
      <p className={cn('mt-1 text-2xl font-semibold tabular-nums', warn ? 'text-rose-700' : 'text-slate-900')}>{value}</p>
    </div>
  )
}

function IncidentDrawer({ incident, onClose }: { incident: Incident | null; onClose: () => void }) {
  const links = useFactoryLinks()
  const canManage = usePermission('work-orders.assign')
  const action = useSafetyAction()
  const [form, setForm] = useState({ rootCause: '', lessons: '', daysLost: '', authority: false })
  useEffect(() => {
    if (incident) setForm({ rootCause: incident.rootCause ?? '', lessons: incident.lessons ?? '', daysLost: incident.daysLost?.toString() ?? '', authority: incident.reportedToAuthority })
  }, [incident])
  const run = async (path: string, done: string, body?: unknown, method: 'post' | 'put' = 'post') => {
    try {
      await action.mutateAsync({ method, path, body })
      toast.success(done)
    } catch {
      // The API client shows the error.
    }
  }
  const save = () => incident && run(`/incidents/${incident.id}`, 'Saved', {
    kind: incident.kind, severity: incident.severity, occurredAt: incident.occurredAt, orgNodeId: incident.orgNodeId, assetId: incident.assetId, workOrderId: incident.workOrderId,
    title: incident.title, description: incident.description, personsInvolved: incident.personsInvolved, immediateAction: incident.immediateAction,
    rootCause: form.rootCause || null, lessons: form.lessons || null, daysLost: form.daysLost ? Number(form.daysLost) : null, reportedToAuthority: form.authority,
  }, 'put')
  const open = incident && incident.status !== 'Closed'
  return (
    <FormDrawer open={incident !== null} title={incident ? `${incident.incidentNumber} · ${incident.title}` : ''} onClose={onClose} footer={incident && canManage && open ? (
      <div className="flex flex-wrap justify-end gap-2">
        {!incident.correctiveWorkOrderId ? <button type="button" className={secondaryButton} onClick={() => void run(`/incidents/${incident.id}/corrective-job`, 'Corrective job raised', {})}>Raise corrective job</button> : null}
        <button type="button" className={secondaryButton} onClick={() => void save()}>Save investigation</button>
        <button type="button" className={primaryButton} onClick={() => void run(`/incidents/${incident.id}/close`, 'Incident closed')}>Close incident</button>
      </div>
    ) : undefined}>
      {incident ? (
        <div className="space-y-4 text-sm">
          <div className="flex flex-wrap gap-2"><Badge tone={INCIDENT_TONES[incident.status]}>{incident.status}</Badge><Badge tone={incident.kind === 'NearMiss' ? 'warning' : 'danger'}>{INCIDENT_KIND_LABELS[incident.kind] ?? incident.kind}</Badge><Badge>{incident.severity}</Badge></div>
          <dl className="grid grid-cols-[minmax(0,8rem)_1fr] gap-x-4 gap-y-2">
            <dt className="text-slate-500">When</dt><dd>{formatDateTime(incident.occurredAt)}</dd>
            <dt className="text-slate-500">Where</dt><dd>{incident.assetId ? <Link className="underline" to={links.asset(incident.assetId)}>{incident.assetCode} · {incident.assetName}</Link> : incident.orgNodeName}</dd>
            <dt className="text-slate-500">Reported by</dt><dd>{incident.reportedByName}</dd>
            {incident.personsInvolved ? <><dt className="text-slate-500">People</dt><dd>{incident.personsInvolved}</dd></> : null}
            {incident.immediateAction ? <><dt className="text-slate-500">Done at once</dt><dd>{incident.immediateAction}</dd></> : null}
            <dt className="text-slate-500">Corrective job</dt><dd>{incident.correctiveWorkOrderId ? <Link className="underline" to={links.job(incident.correctiveWorkOrderId)}>{incident.correctiveWorkOrderNumber}</Link> : '—'}{incident.correctiveWorkOrderStatus ? ` · ${incident.correctiveWorkOrderStatus}` : ''}</dd>
            {incident.closedAt ? <><dt className="text-slate-500">Closed</dt><dd>{formatDateTime(incident.closedAt)} by {incident.closedByName}</dd></> : null}
          </dl>
          <p className="whitespace-pre-wrap text-slate-700">{incident.description}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Root cause" className="sm:col-span-2" required><textarea className={textareaClass} value={form.rootCause} disabled={!open || !canManage} onChange={(event) => setForm({ ...form, rootCause: event.target.value })} /></Field>
            <Field label="What changes" className="sm:col-span-2"><textarea className={textareaClass} value={form.lessons} disabled={!open || !canManage} onChange={(event) => setForm({ ...form, lessons: event.target.value })} /></Field>
            {incident.kind === 'Injury' ? <Field label="Days lost"><input type="number" min={0} className={inputClass} value={form.daysLost} disabled={!open || !canManage} onChange={(event) => setForm({ ...form, daysLost: event.target.value })} /></Field> : null}
            <label className="flex items-center gap-2 text-sm sm:col-span-2"><input type="checkbox" checked={form.authority} disabled={!open || !canManage} onChange={(event) => setForm({ ...form, authority: event.target.checked })} />Reported to DOSH (injuries and dangerous occurrences)</label>
          </div>
        </div>
      ) : null}
    </FormDrawer>
  )
}

/** Report an accident, near miss or damage. Open to everyone at work: easy reporting is the point. */
export function ReportIncidentDrawer({ open, onClose, asset }: { open: boolean; onClose: () => void; asset?: PickedAsset | undefined }) {
  const action = useSafetyAction()
  const [picked, setPicked] = useState<PickedAsset | null>(null)
  const [form, setForm] = useState({ kind: 'NearMiss', severity: 'Minor', occurredAt: '', orgNodeId: '', title: '', description: '', people: '', immediate: '' })
  useEffect(() => {
    if (!open) return
    setPicked(asset ?? null)
    const now = new Date()
    setForm({ kind: 'NearMiss', severity: 'Minor', occurredAt: new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16), orgNodeId: '', title: '', description: '', people: '', immediate: '' })
  }, [open, asset])
  const submit = async () => {
    try {
      const result = await action.mutateAsync({ path: '/incidents', body: {
        kind: form.kind, severity: form.severity, occurredAt: new Date(form.occurredAt).toISOString(), orgNodeId: picked ? null : form.orgNodeId || null, assetId: picked?.id ?? null, workOrderId: null,
        title: form.title, description: form.description || form.title, personsInvolved: form.people || null, immediateAction: form.immediate || null, rootCause: null, lessons: null, daysLost: null, reportedToAuthority: false,
      } }) as Incident
      toast.success(`${result.incidentNumber} reported. Supervisors have been told.`)
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title="Report an incident or near miss" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={action.isPending || !form.title.trim() || (!picked && !form.orgNodeId)} onClick={() => void submit()}>Report</button></div>
    }>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="What happened">
          <select className={inputClass} value={form.kind} onChange={(event) => setForm({ ...form, kind: event.target.value })}>
            {Object.entries(INCIDENT_KIND_LABELS).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </select>
        </Field>
        <Field label="How serious">
          <select className={inputClass} value={form.severity} onChange={(event) => setForm({ ...form, severity: event.target.value })}>
            {['Minor', 'Moderate', 'Serious', 'Fatal'].map((s) => <option key={s}>{s}</option>)}
          </select>
        </Field>
        <Field label="When" className="sm:col-span-2"><input type="datetime-local" className={inputClass} value={form.occurredAt} onChange={(event) => setForm({ ...form, occurredAt: event.target.value })} /></Field>
        <Field label="Asset" className="sm:col-span-2"><AssetPicker value={picked} onChange={setPicked} /></Field>
        {!picked ? <Field label="Or where" className="sm:col-span-2"><LocationSelect value={form.orgNodeId} onChange={(value) => setForm({ ...form, orgNodeId: value })} includeGroups /></Field> : null}
        <Field label="In a few words" required className="sm:col-span-2"><input className={inputClass} value={form.title} onChange={(event) => setForm({ ...form, title: event.target.value })} placeholder="Guard left off dryer fan coupling" /></Field>
        <Field label="Details" className="sm:col-span-2"><textarea className={textareaClass} value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} /></Field>
        <Field label="People involved"><input className={inputClass} value={form.people} onChange={(event) => setForm({ ...form, people: event.target.value })} /></Field>
        <Field label="Done straight away"><input className={inputClass} value={form.immediate} onChange={(event) => setForm({ ...form, immediate: event.target.value })} placeholder="Machine stopped, area taped off" /></Field>
        {form.kind === 'Injury' ? <p className="flex items-start gap-2 rounded-lg bg-rose-50 p-3 text-sm text-rose-900 sm:col-span-2"><AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />Injuries must also be reported to DOSH under OSHA 2007.</p> : null}
      </div>
    </FormDrawer>
  )
}

function TrainingTab() {
  const canManage = usePermission('work-orders.assign')
  const training = useTraining()
  const team = useTeam()
  const action = useSafetyAction()
  const [adding, setAdding] = useState(false)
  const [form, setForm] = useState({ technicianId: '', personName: '', company: '', contractor: false, course: '', category: 'Induction', completedOn: '', expiresOn: '', trainer: '' })
  const today = new Date().toISOString().slice(0, 10)
  const submit = async () => {
    try {
      await action.mutateAsync({ path: '/training', body: { technicianId: form.technicianId || null, personName: form.personName || null, company: form.company || null, isContractor: form.contractor, course: form.course, category: form.category, completedOn: form.completedOn, expiresOn: form.expiresOn || null, trainer: form.trainer || null, notes: null } })
      toast.success('Training recorded')
      setAdding(false)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-3">
      {canManage ? <button type="button" className={secondaryButton} onClick={() => { setForm({ technicianId: '', personName: '', company: '', contractor: false, course: '', category: 'Induction', completedOn: today, expiresOn: '', trainer: '' }); setAdding(true) }}><Plus className="h-4 w-4" />Record training</button> : null}
      {training.isLoading ? <Spinner /> : (training.data ?? []).length === 0 ? <EmptyState title="No training recorded" description="Inductions, skills courses and refreshers for staff and contractors." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Person</th><th className="px-4 py-2">Course</th><th className="px-4 py-2">Done</th><th className="px-4 py-2">Expires</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(training.data ?? []).map((t) => {
                const expired = t.expiresOn && t.expiresOn.slice(0, 10) < today
                return (
                  <tr key={t.id}>
                    <td className="px-4 py-2"><span className="font-medium text-slate-900">{t.personName}</span>{t.isContractor ? <span className="block text-xs text-slate-500">Contractor{t.company ? ` · ${t.company}` : ''}</span> : null}</td>
                    <td className="px-4 py-2">{t.course}<span className="block text-xs text-slate-500">{t.category}{t.trainer ? ` · ${t.trainer}` : ''}</span></td>
                    <td className="px-4 py-2">{formatDate(t.completedOn)}</td>
                    <td className={cn('px-4 py-2', expired && 'font-medium text-rose-700')}>{t.expiresOn ? formatDate(t.expiresOn) : '—'}{expired ? ' · expired' : ''}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
      <FormDrawer open={adding} title="Record training" onClose={() => setAdding(false)} footer={
        <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={() => setAdding(false)}>Cancel</button><button type="button" className={primaryButton} disabled={!form.course.trim() || (!form.technicianId && !form.personName.trim()) || !form.completedOn} onClick={() => void submit()}>Save</button></div>
      }>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="flex items-center gap-2 text-sm sm:col-span-2"><input type="checkbox" checked={form.contractor} onChange={(event) => setForm({ ...form, contractor: event.target.checked, technicianId: '' })} />A contractor or visitor</label>
          {form.contractor ? (
            <>
              <Field label="Name" required><input className={inputClass} value={form.personName} onChange={(event) => setForm({ ...form, personName: event.target.value })} /></Field>
              <Field label="Company"><input className={inputClass} value={form.company} onChange={(event) => setForm({ ...form, company: event.target.value })} /></Field>
            </>
          ) : (
            <Field label="Technician" required className="sm:col-span-2">
              <select className={inputClass} value={form.technicianId} onChange={(event) => setForm({ ...form, technicianId: event.target.value })}>
                <option value="">Choose</option>{(team.data ?? []).map((m) => <option key={m.id} value={m.id}>{m.fullName}</option>)}
              </select>
            </Field>
          )}
          <Field label="Course" required className="sm:col-span-2"><input className={inputClass} value={form.course} onChange={(event) => setForm({ ...form, course: event.target.value })} placeholder="Site safety induction" /></Field>
          <Field label="Kind">
            <select className={inputClass} value={form.category} onChange={(event) => setForm({ ...form, category: event.target.value })}>{['Induction', 'Safety', 'Skills', 'Refresher', 'First aid', 'Fire'].map((c) => <option key={c}>{c}</option>)}</select>
          </Field>
          <Field label="Trainer"><input className={inputClass} value={form.trainer} onChange={(event) => setForm({ ...form, trainer: event.target.value })} /></Field>
          <Field label="Done on" required><input type="date" className={inputClass} value={form.completedOn} onChange={(event) => setForm({ ...form, completedOn: event.target.value })} /></Field>
          <Field label="Expires"><input type="date" className={inputClass} value={form.expiresOn} onChange={(event) => setForm({ ...form, expiresOn: event.target.value })} /></Field>
        </div>
      </FormDrawer>
    </div>
  )
}

function PpeTab() {
  const canManage = usePermission('work-orders.assign')
  const ppe = usePpe()
  const team = useTeam()
  const action = useSafetyAction()
  const [adding, setAdding] = useState(false)
  const [form, setForm] = useState({ technicianId: '', personName: '', item: 'Safety boots', size: '', quantity: '1', replaceBy: '' })
  const submit = async () => {
    try {
      await action.mutateAsync({ path: '/ppe', body: { technicianId: form.technicianId || null, personName: form.personName || null, item: form.item, size: form.size || null, quantity: Number(form.quantity) || 1, issuedOn: null, replaceBy: form.replaceBy || null, notes: null } })
      toast.success('Issue recorded')
      setAdding(false)
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <div className="space-y-3">
      {canManage ? <button type="button" className={secondaryButton} onClick={() => { setForm({ technicianId: '', personName: '', item: 'Safety boots', size: '', quantity: '1', replaceBy: '' }); setAdding(true) }}><Plus className="h-4 w-4" />Issue PPE</button> : null}
      {ppe.isLoading ? <Spinner /> : (ppe.data ?? []).length === 0 ? <EmptyState title="No PPE issued yet" description="Record boots, overalls, gloves, ear and eye protection, harnesses." /> : (
        <div className="overflow-x-auto rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs font-medium text-slate-500"><tr><th className="px-4 py-2">Person</th><th className="px-4 py-2">Item</th><th className="px-4 py-2">Issued</th><th className="px-4 py-2">Replace by</th></tr></thead>
            <tbody className="divide-y divide-slate-100">
              {(ppe.data ?? []).map((p) => (
                <tr key={p.id}>
                  <td className="px-4 py-2 font-medium text-slate-900">{p.personName}</td>
                  <td className="px-4 py-2">{p.item}{p.size ? ` (${p.size})` : ''}{p.quantity > 1 ? ` × ${p.quantity}` : ''}</td>
                  <td className="px-4 py-2">{formatDate(p.issuedOn)}<span className="block text-xs text-slate-500">{p.issuedByName}</span></td>
                  <td className="px-4 py-2">{p.replaceBy ? formatDate(p.replaceBy) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <FormDrawer open={adding} title="Issue PPE" onClose={() => setAdding(false)} footer={
        <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={() => setAdding(false)}>Cancel</button><button type="button" className={primaryButton} disabled={!form.item.trim() || (!form.technicianId && !form.personName.trim())} onClick={() => void submit()}>Save</button></div>
      }>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Technician" className="sm:col-span-2">
            <select className={inputClass} value={form.technicianId} onChange={(event) => setForm({ ...form, technicianId: event.target.value })}>
              <option value="">Someone else</option>{(team.data ?? []).map((m) => <option key={m.id} value={m.id}>{m.fullName}</option>)}
            </select>
          </Field>
          {!form.technicianId ? <Field label="Name" required className="sm:col-span-2"><input className={inputClass} value={form.personName} onChange={(event) => setForm({ ...form, personName: event.target.value })} /></Field> : null}
          <Field label="Item" required>
            <input className={inputClass} list="ppe-items" value={form.item} onChange={(event) => setForm({ ...form, item: event.target.value })} />
            <datalist id="ppe-items">{['Safety boots', 'Overalls', 'Hard hat', 'Safety glasses', 'Ear defenders', 'Dust mask', 'Leather gloves', 'Electrical gloves', 'Welding helmet', 'Full-body harness', 'Hi-vis vest'].map((x) => <option key={x} value={x} />)}</datalist>
          </Field>
          <Field label="Size"><input className={inputClass} value={form.size} onChange={(event) => setForm({ ...form, size: event.target.value })} /></Field>
          <Field label="Quantity"><input type="number" min={1} className={inputClass} value={form.quantity} onChange={(event) => setForm({ ...form, quantity: event.target.value })} /></Field>
          <Field label="Replace by"><input type="date" className={inputClass} value={form.replaceBy} onChange={(event) => setForm({ ...form, replaceBy: event.target.value })} /></Field>
        </div>
      </FormDrawer>
    </div>
  )
}
