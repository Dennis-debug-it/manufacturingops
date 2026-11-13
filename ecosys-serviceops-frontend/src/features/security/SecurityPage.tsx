import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, ArrowDown, ArrowUp, CheckCircle2, Crosshair, MapPin, Nfc, Plus, Printer, RefreshCw, ShieldAlert, Trash2 } from 'lucide-react'
import { PageHeader } from '../../shared/components/PageHeader'
import { StatCard } from '../../shared/components/StatCard'
import { Badge, type BadgeTone } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { formatDate, formatDateTime, formatTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { FactorySelect, useOnlyFactory } from '../visitors/FactorySelect'
import { useStaff, useZones } from '../visitors/api'
import {
  CHECKPOINT_METHODS, RESPONSE_STATUSES, RUN_LABEL, VALIDATION_LABEL, downloadCheckpointLabels, kindLabel, useCheckpointNfc, useCheckpointPosition, useCheckpoints,
  useHandovers, useNewCheckpointLabel, usePatrolAccess, usePatrolBoard, usePatrolReport, useRoutes, useRunDetail, useSaveCheckpoint, useSaveRoute, useSaveSchedule,
  useSecurityIncidents, useSeedCheckpoints, useUpdateIncident, type CheckpointRow, type RouteRow, type RunStatus, type ScheduleRow, type SecurityIncident,
} from './api'
import { nfcSupported, useNfcReader } from './device'

const TABS = [
  { id: 'board', label: 'Board' },
  { id: 'incidents', label: 'Incidents' },
  { id: 'checkpoints', label: 'Checkpoints' },
  { id: 'routes', label: 'Routes' },
  { id: 'handovers', label: 'Handovers' },
  { id: 'report', label: 'Report' },
] as const
type Tab = (typeof TABS)[number]['id']

const RUN_TONE: Record<RunStatus, BadgeTone> = { Due: 'info', InProgress: 'warning', Completed: 'success', Partial: 'warning', Missed: 'danger' }
const mapLink = (lat: number, lng: number) => `https://www.openstreetmap.org/?mlat=${lat}&mlon=${lng}#map=18/${lat}/${lng}`

/** Security Patrol for supervisors: the night's patrols, flagged scans, incidents, and the set-up of checkpoints and routes. */
export function SecurityPage() {
  const access = usePatrolAccess()
  const [params, setParams] = useSearchParams()
  const onlyFactory = useOnlyFactory()
  const [factoryId, setFactoryId] = useState('')
  useEffect(() => { if (onlyFactory && !factoryId) setFactoryId(onlyFactory) }, [onlyFactory, factoryId])
  const tab = (TABS.find((x) => x.id === params.get('tab'))?.id ?? 'board') as Tab

  if (access.isLoading) return <Spinner label="Loading" />
  if (!access.data?.canManage) return <EmptyState title="Security" description="This is for security supervisors. Guards use Patrol." />

  return (
    <div className="space-y-4">
      <PageHeader title="Security" subtitle="Patrols checked against checkpoints, flagged scans, incidents and handovers."
        secondaryActions={<div className="w-56"><FactorySelect value={factoryId} onChange={setFactoryId} emptyLabel="All factories" /></div>} />
      <div role="tablist" className="inline-flex flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {TABS.map((item) => (
          <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setParams(item.id === 'board' ? {} : { tab: item.id }, { replace: true })}
            className={cn('h-9 rounded-md px-3 text-sm font-medium', tab === item.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600 hover:bg-slate-50')}>{item.label}</button>
        ))}
      </div>
      {tab === 'board' ? <BoardTab factoryId={factoryId} />
        : tab === 'incidents' ? <IncidentsTab factoryId={factoryId} />
          : tab === 'checkpoints' ? <CheckpointsTab factoryId={factoryId || onlyFactory} />
            : tab === 'routes' ? <RoutesTab factoryId={factoryId || onlyFactory} />
              : tab === 'handovers' ? <HandoversTab factoryId={factoryId} />
                : <ReportTab factoryId={factoryId} />}
    </div>
  )
}

/* ── Board ──────────────────────────────────────────────────────────────── */

function BoardTab({ factoryId }: { factoryId: string }) {
  const [date, setDate] = useState('')
  const board = usePatrolBoard(factoryId, date)
  const [open, setOpen] = useState<string | null>(null)
  const data = board.data
  if (board.isLoading || !data) return <Spinner label="Loading the patrol board" />
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <input type="date" className={cn(inputClass, 'w-48')} value={date} onChange={(e) => setDate(e.target.value)} aria-label="Night of" />
        <span className="text-sm text-slate-500">Night of {formatDate(data.day)} (noon to noon)</span>
      </div>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <StatCard label="Checkpoints reached" value={data.completionPercent === null ? '—' : `${data.completionPercent}%`} tone={data.completionPercent !== null && data.completionPercent < 90 ? 'warning' : 'default'} />
        <StatCard label="In progress" value={data.inProgress} />
        <StatCard label="Incomplete" value={data.partial} tone={data.partial > 0 ? 'warning' : 'default'} />
        <StatCard label="Missed" value={data.missed} tone={data.missed > 0 ? 'danger' : 'default'} />
        <StatCard label="Open incidents" value={data.openIncidents} tone={data.openIncidents > 0 ? 'warning' : 'default'} icon={<ShieldAlert className="h-4 w-4 text-slate-400" />} />
      </div>
      {data.lastScan ? (
        <p className="text-sm text-slate-600">
          Last scan: <span className="font-medium">{data.lastScan.code} {data.lastScan.name}</span> by {data.lastScan.guardName} at {formatTime(data.lastScan.scannedAt)}
          {data.lastScan.latitude !== null && data.lastScan.longitude !== null
            ? <a className="ml-2 inline-flex items-center gap-1 text-[var(--app-primary)] hover:underline" href={mapLink(data.lastScan.latitude, data.lastScan.longitude)} target="_blank" rel="noreferrer"><MapPin className="h-3.5 w-3.5" />map</a> : null}
        </p>
      ) : null}
      {data.runs.length === 0 ? <EmptyState title="No patrols this night" description="Patrols appear here from the routes' schedules. Set up checkpoints and routes first." /> : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[760px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Due</th><th className="px-3 py-2">Route</th><th className="px-3 py-2">Guard</th><th className="px-3 py-2">Status</th><th className="px-3 py-2">Checkpoints</th><th className="px-3 py-2">Flagged</th></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {data.runs.map((run) => (
                <tr key={run.id} className="cursor-pointer hover:bg-slate-50" onClick={() => setOpen(run.id)}>
                  <td className="px-3 py-2 tabular-nums">{formatTime(run.dueFrom)}–{formatTime(run.dueBy)}</td>
                  <td className="px-3 py-2">{run.routeName}{run.adHoc ? <span className="text-xs text-slate-500"> · extra</span> : null}<div className="text-xs text-slate-500">{run.factoryName}</div></td>
                  <td className="px-3 py-2 text-slate-600">{run.guardName ?? '—'}</td>
                  <td className="px-3 py-2"><Badge size="sm" tone={RUN_TONE[run.status]}>{RUN_LABEL[run.status]}</Badge></td>
                  <td className="px-3 py-2 tabular-nums">{run.validCount} / {run.requiredCount}</td>
                  <td className={cn('px-3 py-2 tabular-nums', run.flaggedCount > 0 && 'font-medium text-amber-700')}>{run.flaggedCount || '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data.flagged.length > 0 ? (
        <section className="rounded-lg border border-amber-200 bg-white p-4">
          <h3 className="mb-2 flex items-center gap-2 text-sm font-semibold text-amber-800"><AlertTriangle className="h-4 w-4" />Flagged scans, last 24 hours</h3>
          <ul className="divide-y divide-slate-100 text-sm">
            {data.flagged.map((x) => (
              <li key={x.id} className="flex flex-wrap justify-between gap-2 py-2">
                <span><span className="font-medium">{x.code} {x.name}</span> · {x.guardName} · {formatTime(x.scannedAt)}</span>
                <span className="text-amber-800">{VALIDATION_LABEL[x.validation]}{x.note ? `: ${x.note}` : ''}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      {open ? <RunDrawer id={open} onClose={() => setOpen(null)} /> : null}
    </div>
  )
}

function RunDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const detail = useRunDetail(id)
  const data = detail.data
  return (
    <FormDrawer open title={data ? `${data.run.routeName}, ${formatTime(data.run.dueFrom)}` : 'Patrol'} onClose={onClose}>
      {!data ? <Spinner /> : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone={RUN_TONE[data.run.status]}>{RUN_LABEL[data.run.status]}</Badge>
            <span>{data.run.validCount} of {data.run.requiredCount} checkpoints</span>
            {data.run.guardName ? <span className="text-slate-500">· {data.run.guardName}</span> : null}
          </div>
          <ol className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {data.stops.map((stop) => {
              const scans = data.scans.filter((x) => x.checkpointId === stop.checkpointId)
              const valid = scans.find((x) => x.validation === 'Valid')
              return (
                <li key={stop.checkpointId} className="p-3 text-sm">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">{stop.code} {stop.name}{stop.required ? '' : <span className="text-xs font-normal text-slate-500"> · optional</span>}</span>
                    {valid ? <span className="flex items-center gap-1 text-emerald-700"><CheckCircle2 className="h-4 w-4" />{formatTime(valid.scannedAt)}</span> : <span className="text-slate-400">Not reached</span>}
                  </div>
                  {scans.filter((x) => x.validation !== 'Valid').map((x) => (
                    <div key={x.id} className="mt-1 text-xs text-amber-700">{formatTime(x.scannedAt)} {VALIDATION_LABEL[x.validation]}{x.note ? `: ${x.note}` : ''}</div>
                  ))}
                  {scans.map((x) => x.latitude !== null && x.longitude !== null ? (
                    <a key={`m-${x.id}`} className="mr-3 inline-flex items-center gap-1 text-xs text-[var(--app-primary)] hover:underline" href={mapLink(x.latitude, x.longitude)} target="_blank" rel="noreferrer">
                      <MapPin className="h-3 w-3" />{x.distanceM !== null ? `${Math.round(x.distanceM)} m away` : 'where'}{x.method === 'Nfc' ? ' · NFC' : ''}
                    </a>
                  ) : null)}
                  {scans.some((x) => new Date(x.receivedAt).getTime() - new Date(x.scannedAt).getTime() > 5 * 60_000)
                    ? <div className="mt-1 text-xs text-slate-500">Scanned with no signal, synced later.</div> : null}
                </li>
              )
            })}
          </ol>
        </div>
      )}
    </FormDrawer>
  )
}

/* ── Incidents ──────────────────────────────────────────────────────────── */

const RESPONSE_TONE: Record<string, BadgeTone> = { Reported: 'danger', Responding: 'warning', Contained: 'info', Closed: 'neutral' }

function IncidentsTab({ factoryId }: { factoryId: string }) {
  const [openOnly, setOpenOnly] = useState(true)
  const incidents = useSecurityIncidents(factoryId, openOnly)
  const [editing, setEditing] = useState<SecurityIncident | null>(null)
  const rows = incidents.data ?? []
  return (
    <div className="space-y-3">
      <label className="flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" checked={openOnly} onChange={(e) => setOpenOnly(e.target.checked)} />Open only</label>
      {incidents.isLoading ? <Spinner /> : rows.length === 0 ? <EmptyState title="No security incidents" description="Guards report them from Patrol; a 'No' at a checkpoint raises one too." /> : (
        <div className="space-y-2">
          {rows.map((x) => (
            <button key={x.id} type="button" onClick={() => setEditing(x)} className="w-full rounded-lg border border-slate-200 bg-white p-4 text-left hover:bg-slate-50">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-mono text-xs text-slate-500">{x.incidentNumber}</span>
                <Badge size="sm" tone={x.kind === 'Emergency' ? 'danger' : 'neutral'}>{kindLabel(x.kind)}</Badge>
                <Badge size="sm" tone={RESPONSE_TONE[x.responseStatus ?? 'Reported'] ?? 'neutral'}>{x.responseStatus ?? 'Reported'}</Badge>
                <span className="text-xs text-slate-500">{x.severity}</span>
              </div>
              <div className="mt-1 font-medium text-slate-900">{x.title}</div>
              <div className="text-xs text-slate-500">{x.factoryName} · {formatDateTime(x.occurredAt)} · reported by {x.reportedByName}{x.ownerName ? ` · owner ${x.ownerName}` : ''}{x.policeObNumber ? ` · ${x.policeObNumber}` : ''}</div>
            </button>
          ))}
        </div>
      )}
      {editing ? <IncidentDrawer incident={editing} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}

function IncidentDrawer({ incident, onClose }: { incident: SecurityIncident; onClose: () => void }) {
  const update = useUpdateIncident()
  const staff = useStaff('')
  const [form, setForm] = useState({
    responseStatus: incident.responseStatus ?? 'Reported', ownerUserId: incident.ownerUserId ?? '', policeObNumber: incident.policeObNumber ?? '',
    reportedToPolice: incident.reportedToPolice, immediateAction: incident.immediateAction ?? '',
  })
  const submit = async () => {
    try {
      await update.mutateAsync({
        id: incident.id, responseStatus: form.responseStatus, ownerUserId: form.ownerUserId || null, policeObNumber: form.policeObNumber,
        reportedToPolice: form.reportedToPolice, immediateAction: form.immediateAction,
      })
      toast.success('Incident updated')
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title={`${incident.incidentNumber}: ${kindLabel(incident.kind)}`} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={update.isPending} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <div className="rounded-lg bg-slate-50 p-3 text-sm">
          <div className="font-medium text-slate-900">{incident.title}</div>
          <p className="mt-1 whitespace-pre-line text-slate-600">{incident.description}</p>
          <div className="mt-2 text-xs text-slate-500">{incident.factoryName} · {formatDateTime(incident.occurredAt)} · {incident.reportedByName}</div>
          {incident.latitude !== null && incident.longitude !== null
            ? <a className="mt-1 inline-flex items-center gap-1 text-xs text-[var(--app-primary)] hover:underline" href={mapLink(incident.latitude, incident.longitude)} target="_blank" rel="noreferrer"><MapPin className="h-3 w-3" />Where it was reported</a> : null}
        </div>
        <Field label="Response"><select className={inputClass} value={form.responseStatus} onChange={(e) => setForm({ ...form, responseStatus: e.target.value })}>{RESPONSE_STATUSES.map((x) => <option key={x}>{x}</option>)}</select></Field>
        <Field label="Owner"><select className={inputClass} value={form.ownerUserId} onChange={(e) => setForm({ ...form, ownerUserId: e.target.value })}><option value="">Nobody yet</option>{(staff.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}</select></Field>
        <Field label="What was done"><textarea className={textareaClass} value={form.immediateAction} onChange={(e) => setForm({ ...form, immediateAction: e.target.value })} /></Field>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.reportedToPolice} onChange={(e) => setForm({ ...form, reportedToPolice: e.target.checked })} />Reported to the police</label>
        <Field label="Police OB number"><input className={inputClass} value={form.policeObNumber} onChange={(e) => setForm({ ...form, policeObNumber: e.target.value })} placeholder="OB 12/13/11/2026" /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Checkpoints ────────────────────────────────────────────────────────── */

function CheckpointsTab({ factoryId }: { factoryId: string }) {
  const checkpoints = useCheckpoints(factoryId, true)
  const seed = useSeedCheckpoints()
  const [editing, setEditing] = useState<CheckpointRow | 'new' | null>(null)
  const rows = checkpoints.data ?? []
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap justify-end gap-2">
        {rows.length > 0 ? <button type="button" className={secondaryButton} onClick={() => void downloadCheckpointLabels(factoryId)}><Printer className="h-4 w-4" />Print labels</button> : null}
        {factoryId ? <button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add checkpoint</button> : null}
      </div>
      {checkpoints.isLoading ? <Spinner /> : rows.length === 0 ? (
        <EmptyState title="No checkpoints yet" description={factoryId ? 'Checkpoints are the points guards must reach. Start with the usual tea factory set and a night round every two hours.' : 'Choose a factory.'}
          actionLabel={factoryId ? 'Add the standard tea factory checkpoints' : undefined}
          onAction={factoryId ? () => void seed.mutateAsync(factoryId).then(() => toast.success('Checkpoints and a night round added')).catch(() => undefined) : undefined} />
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[820px] text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr><th className="px-3 py-2">Checkpoint</th><th className="px-3 py-2">Tag</th><th className="px-3 py-2">Position</th><th className="px-3 py-2">Questions</th><th className="px-3 py-2">Last scan</th><th className="px-3 py-2" /></tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((x) => (
                <tr key={x.id} className={cn(!x.isActive && 'text-slate-400')}>
                  <td className="px-3 py-2"><span className="font-mono text-xs text-slate-500">{x.code}</span> <span className="font-medium">{x.name}</span><div className="text-xs text-slate-500">{x.zoneName ?? x.factoryName}{x.isActive ? '' : ' · inactive'}</div></td>
                  <td className="px-3 py-2 text-xs">{CHECKPOINT_METHODS.find((m) => m.value === x.method)?.label}{x.nfcSerial ? <div className="text-emerald-700">NFC tag registered</div> : x.method !== 'Qr' ? <div className="text-amber-700">No NFC tag yet</div> : null}</td>
                  <td className="px-3 py-2 text-xs">{x.latitude !== null && x.longitude !== null ? <a className="text-[var(--app-primary)] hover:underline" href={mapLink(x.latitude, x.longitude)} target="_blank" rel="noreferrer">Set · {x.radiusM} m</a> : <span className="text-slate-400">Not set</span>}</td>
                  <td className="px-3 py-2 text-xs text-slate-600">{x.questions ? x.questions.split('\n').length : '—'}</td>
                  <td className="px-3 py-2 text-xs text-slate-600">{x.lastScanAt ? formatDateTime(x.lastScanAt) : '—'}</td>
                  <td className="px-3 py-2 text-right"><button type="button" className="text-sm font-medium text-[var(--app-primary)] hover:underline" onClick={() => setEditing(x)}>Edit</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {editing ? <CheckpointDrawer checkpoint={editing === 'new' ? null : editing} factoryId={factoryId} onClose={() => setEditing(null)} /> : null}
    </div>
  )
}

function CheckpointDrawer({ checkpoint, factoryId, onClose }: { checkpoint: CheckpointRow | null; factoryId: string; onClose: () => void }) {
  const save = useSaveCheckpoint()
  const position = useCheckpointPosition()
  const nfc = useCheckpointNfc()
  const relabel = useNewCheckpointLabel()
  const zones = useZones(checkpoint?.factoryId ?? factoryId)
  const [listening, setListening] = useState(false)
  const [form, setForm] = useState({
    name: checkpoint?.name ?? '', method: checkpoint?.method ?? 'QrOrNfc', accessZoneId: checkpoint?.accessZoneId ?? '', radiusM: String(checkpoint?.radiusM ?? 50),
    latitude: checkpoint?.latitude?.toString() ?? '', longitude: checkpoint?.longitude?.toString() ?? '', instructions: checkpoint?.instructions ?? '',
    questions: checkpoint?.questions ?? '', isActive: checkpoint?.isActive ?? true,
  })
  useNfcReader(listening, (serial) => {
    setListening(false)
    if (!checkpoint) return
    void nfc.mutateAsync({ id: checkpoint.id, serial }).then(() => toast.success('NFC tag registered')).catch(() => undefined)
  }, (reason) => { toast.error(reason); setListening(false) })

  const submit = async () => {
    try {
      await save.mutateAsync({
        id: checkpoint?.id, payload: {
          factoryId: checkpoint?.factoryId ?? factoryId, name: form.name.trim(), method: form.method, accessZoneId: form.accessZoneId || null,
          latitude: form.latitude ? Number(form.latitude) : null, longitude: form.longitude ? Number(form.longitude) : null, radiusM: Number(form.radiusM) || 50,
          instructions: form.instructions.trim() || null, questions: form.questions.trim() || null, isActive: form.isActive,
        },
      })
      toast.success('Checkpoint saved')
      onClose()
    } catch { /* shown by the API client */ }
  }
  const here = () => {
    if (!checkpoint || !('geolocation' in navigator)) return
    navigator.geolocation.getCurrentPosition(
      (fix) => void position.mutateAsync({ id: checkpoint.id, latitude: fix.coords.latitude, longitude: fix.coords.longitude, accuracyM: fix.coords.accuracy })
        .then((row) => { setForm((f) => ({ ...f, latitude: String(row.latitude), longitude: String(row.longitude) })); toast.success(`Position saved (±${Math.round(fix.coords.accuracy)} m)`) })
        .catch(() => undefined),
      () => toast.error('Could not get a GPS position.'),
      { enableHighAccuracy: true, timeout: 20_000 },
    )
  }

  return (
    <FormDrawer open title={checkpoint ? `${checkpoint.code} ${checkpoint.name}` : 'Add checkpoint'} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={save.isPending || !form.name.trim()} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Verified by"><select className={inputClass} value={form.method} onChange={(e) => setForm({ ...form, method: e.target.value })}>{CHECKPOINT_METHODS.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}</select></Field>
          <Field label="Zone"><select className={inputClass} value={form.accessZoneId} onChange={(e) => setForm({ ...form, accessZoneId: e.target.value })}><option value="">None</option>{(zones.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}</select></Field>
          <Field label="Latitude"><input className={inputClass} inputMode="decimal" value={form.latitude} onChange={(e) => setForm({ ...form, latitude: e.target.value })} /></Field>
          <Field label="Longitude"><input className={inputClass} inputMode="decimal" value={form.longitude} onChange={(e) => setForm({ ...form, longitude: e.target.value })} /></Field>
          <Field label="Allowed distance (m)" hint="A scan further away is flagged. GPS is weak indoors; allow more there."><input className={inputClass} inputMode="numeric" value={form.radiusM} onChange={(e) => setForm({ ...form, radiusM: e.target.value })} /></Field>
        </div>
        {checkpoint ? (
          <div className="flex flex-wrap gap-2">
            <button type="button" className={secondaryButton} onClick={here}><Crosshair className="h-4 w-4" />Use my position here</button>
            <button type="button" className={secondaryButton} disabled={!nfcSupported()} onClick={() => setListening(!listening)} title={nfcSupported() ? undefined : 'NFC works in Chrome on Android'}>
              <Nfc className="h-4 w-4" />{listening ? 'Hold the phone to the tag…' : checkpoint.nfcSerial ? 'Replace NFC tag' : 'Register NFC tag'}
            </button>
            <button type="button" className={secondaryButton} onClick={() => void downloadCheckpointLabels(checkpoint.factoryId, [checkpoint.id])}><Printer className="h-4 w-4" />Print label</button>
            <button type="button" className={secondaryButton} onClick={() => { if (window.confirm('Issue a new QR label? The old label stops working.')) void relabel.mutateAsync(checkpoint.id).then(() => toast.success('New label issued; print it.')) }}>
              <RefreshCw className="h-4 w-4" />New label
            </button>
          </div>
        ) : null}
        <Field label="What the guard checks here"><textarea className={textareaClass} value={form.instructions} onChange={(e) => setForm({ ...form, instructions: e.target.value })} /></Field>
        <Field label="Yes/no questions" hint="One per line, up to six. A 'No' raises an incident, e.g. 'Padlock intact?'"><textarea className={textareaClass} value={form.questions} onChange={(e) => setForm({ ...form, questions: e.target.value })} /></Field>
        {checkpoint ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
      </div>
    </FormDrawer>
  )
}

/* ── Routes ─────────────────────────────────────────────────────────────── */

function RoutesTab({ factoryId }: { factoryId: string }) {
  const routes = useRoutes(factoryId)
  const [editing, setEditing] = useState<RouteRow | 'new' | null>(null)
  const [scheduling, setScheduling] = useState<{ route: RouteRow; schedule: ScheduleRow | null } | null>(null)
  const rows = routes.data ?? []
  return (
    <div className="space-y-3">
      {factoryId ? <div className="flex justify-end"><button type="button" className={primaryButton} onClick={() => setEditing('new')}><Plus className="h-4 w-4" />Add route</button></div> : null}
      {routes.isLoading ? <Spinner /> : rows.length === 0 ? <EmptyState title="No routes" description="A route is the checkpoints a patrol walks, in order. Add checkpoints first." /> : rows.map((route) => (
        <article key={route.id} className={cn('rounded-lg border border-slate-200 bg-white p-4', !route.isActive && 'opacity-60')}>
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div>
              <div className="font-semibold text-slate-900">{route.name}{route.isActive ? '' : ' (inactive)'}</div>
              <div className="text-xs text-slate-500">{route.factoryName} · {route.ordered ? 'in order' : 'any order'} · {route.maxDurationMinutes} min · at least {route.minGapSeconds} s between points</div>
            </div>
            <div className="flex gap-2">
              <button type="button" className={secondaryButton} onClick={() => setScheduling({ route, schedule: null })}>Add schedule</button>
              <button type="button" className={secondaryButton} onClick={() => setEditing(route)}>Edit</button>
            </div>
          </div>
          <div className="mt-2 text-sm text-slate-600">{route.checkpoints.map((x) => `${x.code}${x.required ? '' : ' (opt.)'}`).join(' → ')}</div>
          <ul className="mt-2 space-y-1">
            {route.schedules.map((s) => (
              <li key={s.id} className="flex items-center justify-between text-sm">
                <span className={cn(!s.isActive && 'text-slate-400 line-through')}>{s.description}{s.assignedName ? ` · ${s.assignedName}` : ''} · {s.graceMinutes} min grace</span>
                <button type="button" className="text-xs font-medium text-[var(--app-primary)] hover:underline" onClick={() => setScheduling({ route, schedule: s })}>Edit</button>
              </li>
            ))}
            {route.schedules.length === 0 ? <li className="text-xs text-amber-700">No schedule: patrols are only walked when a guard starts one.</li> : null}
          </ul>
        </article>
      ))}
      {editing ? <RouteDrawer route={editing === 'new' ? null : editing} factoryId={factoryId} onClose={() => setEditing(null)} /> : null}
      {scheduling ? <ScheduleDrawer {...scheduling} onClose={() => setScheduling(null)} /> : null}
    </div>
  )
}

function RouteDrawer({ route, factoryId, onClose }: { route: RouteRow | null; factoryId: string; onClose: () => void }) {
  const save = useSaveRoute()
  const checkpoints = useCheckpoints(route?.factoryId ?? factoryId)
  const [form, setForm] = useState({
    name: route?.name ?? '', ordered: route?.ordered ?? true, maxDurationMinutes: String(route?.maxDurationMinutes ?? 60), minGapSeconds: String(route?.minGapSeconds ?? 20),
    isActive: route?.isActive ?? true, stops: route?.checkpoints.map((x) => ({ checkpointId: x.checkpointId, required: x.required })) ?? [] as { checkpointId: string; required: boolean }[],
  })
  const all = checkpoints.data ?? []
  const byId = new Map(all.map((x) => [x.id, x]))
  const available = all.filter((x) => x.isActive && !form.stops.some((s) => s.checkpointId === x.id))
  const move = (index: number, by: number) => {
    const stops = [...form.stops]
    const [item] = stops.splice(index, 1)
    stops.splice(index + by, 0, item!)
    setForm({ ...form, stops })
  }
  const submit = async () => {
    try {
      await save.mutateAsync({
        id: route?.id, payload: {
          factoryId: route?.factoryId ?? factoryId, name: form.name.trim(), checkpoints: form.stops, ordered: form.ordered,
          maxDurationMinutes: Number(form.maxDurationMinutes) || 60, minGapSeconds: Number(form.minGapSeconds) || 0, isActive: form.isActive,
        },
      })
      toast.success('Route saved')
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title={route ? `Edit ${route.name}` : 'Add route'} onClose={onClose} closeOnOverlayClick={false}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={save.isPending || !form.name.trim() || form.stops.length === 0} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <Field label="Name" required><input className={inputClass} value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} /></Field>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Minutes to walk it"><input className={inputClass} inputMode="numeric" value={form.maxDurationMinutes} onChange={(e) => setForm({ ...form, maxDurationMinutes: e.target.value })} /></Field>
          <Field label="Seconds at least between points" hint="Faster is flagged as a photographed label."><input className={inputClass} inputMode="numeric" value={form.minGapSeconds} onChange={(e) => setForm({ ...form, minGapSeconds: e.target.value })} /></Field>
        </div>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.ordered} onChange={(e) => setForm({ ...form, ordered: e.target.checked })} />Walked in this order</label>
        {route ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
        <section>
          <h4 className="mb-2 text-sm font-medium text-slate-700">Checkpoints, in walking order</h4>
          <ol className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {form.stops.map((stop, index) => (
              <li key={stop.checkpointId} className="flex items-center justify-between gap-2 px-3 py-2 text-sm">
                <span>{index + 1}. {byId.get(stop.checkpointId)?.code} {byId.get(stop.checkpointId)?.name}</span>
                <span className="flex items-center gap-2">
                  <label className="flex items-center gap-1 text-xs"><input type="checkbox" checked={stop.required} onChange={(e) => setForm({ ...form, stops: form.stops.map((s, i) => (i === index ? { ...s, required: e.target.checked } : s)) })} />Required</label>
                  <button type="button" aria-label="Move up" disabled={index === 0} onClick={() => move(index, -1)} className="text-slate-500 disabled:opacity-30"><ArrowUp className="h-4 w-4" /></button>
                  <button type="button" aria-label="Move down" disabled={index === form.stops.length - 1} onClick={() => move(index, 1)} className="text-slate-500 disabled:opacity-30"><ArrowDown className="h-4 w-4" /></button>
                  <button type="button" aria-label="Remove" onClick={() => setForm({ ...form, stops: form.stops.filter((_, i) => i !== index) })} className="text-slate-400 hover:text-rose-600"><Trash2 className="h-4 w-4" /></button>
                </span>
              </li>
            ))}
            {form.stops.length === 0 ? <li className="px-3 py-2 text-sm text-slate-500">Add checkpoints below.</li> : null}
          </ol>
          {available.length > 0 ? (
            <select className={cn(inputClass, 'mt-2')} value="" aria-label="Add a checkpoint" onChange={(e) => { if (e.target.value) setForm({ ...form, stops: [...form.stops, { checkpointId: e.target.value, required: true }] }) }}>
              <option value="">Add a checkpoint…</option>
              {available.map((x) => <option key={x.id} value={x.id}>{x.code} {x.name}</option>)}
            </select>
          ) : null}
        </section>
      </div>
    </FormDrawer>
  )
}

const WEEKDAYS = [[1, 'Mon'], [2, 'Tue'], [3, 'Wed'], [4, 'Thu'], [5, 'Fri'], [6, 'Sat'], [7, 'Sun']] as const

function ScheduleDrawer({ route, schedule, onClose }: { route: RouteRow; schedule: ScheduleRow | null; onClose: () => void }) {
  const save = useSaveSchedule()
  const staff = useStaff('')
  const [form, setForm] = useState({
    mode: schedule?.fixedTimes ? 'fixed' : 'every', everyMinutes: String(schedule?.everyMinutes ?? 120), fixedTimes: schedule?.fixedTimes ?? '22:00, 02:00',
    windowFrom: schedule?.windowFrom ?? '18:00', windowTo: schedule?.windowTo ?? '06:00', days: schedule?.days ?? [1, 2, 3, 4, 5, 6, 7],
    graceMinutes: String(schedule?.graceMinutes ?? 15), assignedUserId: schedule?.assignedUserId ?? '', isActive: schedule?.isActive ?? true,
  })
  const submit = async () => {
    try {
      await save.mutateAsync({
        id: schedule?.id, routeId: route.id, payload: {
          everyMinutes: form.mode === 'every' ? Number(form.everyMinutes) : null, fixedTimes: form.mode === 'fixed' ? form.fixedTimes : null,
          windowFrom: form.windowFrom, windowTo: form.windowTo, days: form.days, graceMinutes: Number(form.graceMinutes) || 0,
          assignedUserId: form.assignedUserId || null, isActive: form.isActive,
        },
      })
      toast.success('Schedule saved')
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title={`${schedule ? 'Edit' : 'Add'} schedule: ${route.name}`} onClose={onClose}
      footer={<div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button>
        <button type="button" className={primaryButton} disabled={save.isPending || form.days.length === 0} onClick={() => void submit()}>Save</button></div>}>
      <div className="space-y-4">
        <div role="radiogroup" aria-label="How" className="inline-flex overflow-hidden rounded-lg ring-1 ring-inset ring-slate-300">
          {[['every', 'Every N minutes'], ['fixed', 'Fixed times']].map(([value, label]) => (
            <button key={value} type="button" role="radio" aria-checked={form.mode === value} onClick={() => setForm({ ...form, mode: value! })}
              className={cn('h-9 px-3 text-sm font-medium', form.mode === value ? 'bg-[var(--app-primary)] text-white' : 'bg-white text-slate-600')}>{label}</button>
          ))}
        </div>
        {form.mode === 'every' ? (
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Every (minutes)"><input className={inputClass} inputMode="numeric" value={form.everyMinutes} onChange={(e) => setForm({ ...form, everyMinutes: e.target.value })} /></Field>
            <Field label="From"><input type="time" className={inputClass} value={form.windowFrom} onChange={(e) => setForm({ ...form, windowFrom: e.target.value })} /></Field>
            <Field label="Until" hint="Earlier than From means overnight."><input type="time" className={inputClass} value={form.windowTo} onChange={(e) => setForm({ ...form, windowTo: e.target.value })} /></Field>
          </div>
        ) : (
          <Field label="Times" hint="Comma separated, e.g. 22:00, 02:00 (after midnight belongs to the night before)."><input className={inputClass} value={form.fixedTimes} onChange={(e) => setForm({ ...form, fixedTimes: e.target.value })} /></Field>
        )}
        <fieldset>
          <legend className="text-sm font-medium text-slate-700">Nights starting on</legend>
          <div className="mt-1 flex flex-wrap gap-2">
            {WEEKDAYS.map(([day, label]) => (
              <label key={day} className="flex items-center gap-1 text-sm"><input type="checkbox" checked={form.days.includes(day)} onChange={(e) => setForm({ ...form, days: e.target.checked ? [...form.days, day].sort() : form.days.filter((x) => x !== day) })} />{label}</label>
            ))}
          </div>
        </fieldset>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label="Grace (minutes)" hint="After it is due, before it counts as missed."><input className={inputClass} inputMode="numeric" value={form.graceMinutes} onChange={(e) => setForm({ ...form, graceMinutes: e.target.value })} /></Field>
          <Field label="Guard"><select className={inputClass} value={form.assignedUserId} onChange={(e) => setForm({ ...form, assignedUserId: e.target.value })}><option value="">Any guard on duty</option>{(staff.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}</select></Field>
        </div>
        {schedule ? <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />Active</label> : null}
      </div>
    </FormDrawer>
  )
}

/* ── Handovers ──────────────────────────────────────────────────────────── */

function HandoversTab({ factoryId }: { factoryId: string }) {
  const handovers = useHandovers(factoryId)
  const rows = handovers.data ?? []
  if (handovers.isLoading) return <Spinner />
  if (rows.length === 0) return <EmptyState title="No handovers yet" description="Guards hand over their shift from Patrol." />
  return (
    <div className="space-y-2">
      {rows.map((x) => (
        <article key={x.id} className="rounded-lg border border-slate-200 bg-white p-4 text-sm">
          <div className="flex flex-wrap justify-between gap-2">
            <span className="font-medium">{x.shiftName ?? 'Shift'} · {x.outgoingName} → {x.incomingName ?? 'next guard'}</span>
            <span className="text-xs text-slate-500">{formatDateTime(x.handedOverAt)}{x.acknowledgedAt ? ` · acknowledged ${formatTime(x.acknowledgedAt)}` : ' · not acknowledged'}</span>
          </div>
          {x.openIssues ? <p className="mt-1 whitespace-pre-line text-slate-700"><span className="font-medium">Open issues:</span> {x.openIssues}</p> : null}
          {x.keysAndEquipment ? <p className="mt-1 text-slate-600"><span className="font-medium">Keys and equipment:</span> {x.keysAndEquipment}</p> : null}
        </article>
      ))}
    </div>
  )
}

/* ── Report ─────────────────────────────────────────────────────────────── */

function ReportTab({ factoryId }: { factoryId: string }) {
  const [days, setDays] = useState(30)
  const report = usePatrolReport(factoryId, days)
  const data = report.data
  const table = (title: string, rows: { name: string; patrols: number; completed: number; missed: number; checkpointPercent: number; flagged: number }[]) => (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-2 text-sm font-semibold text-slate-700">{title}</h3>
      {rows.length === 0 ? <p className="text-sm text-slate-500">Nothing in this period.</p> : (
        <table className="w-full text-sm">
          <thead className="text-left text-xs uppercase tracking-wide text-slate-500"><tr><th className="py-1">Name</th><th className="py-1 text-right">Patrols</th><th className="py-1 text-right">Done</th><th className="py-1 text-right">Missed</th><th className="py-1 text-right">Points reached</th><th className="py-1 text-right">Flagged</th></tr></thead>
          <tbody className="divide-y divide-slate-100">
            {rows.map((x) => (
              <tr key={x.name}><td className="py-1">{x.name}</td><td className="py-1 text-right tabular-nums">{x.patrols}</td><td className="py-1 text-right tabular-nums">{x.completed}</td>
                <td className={cn('py-1 text-right tabular-nums', x.missed > 0 && 'text-rose-700')}>{x.missed}</td><td className="py-1 text-right tabular-nums">{x.checkpointPercent}%</td><td className="py-1 text-right tabular-nums">{x.flagged}</td></tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
  return (
    <div className="space-y-4">
      <select className={cn(inputClass, 'w-48')} value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label="Period">
        <option value={7}>Last 7 days</option><option value={30}>Last 30 days</option><option value={90}>Last 90 days</option>
      </select>
      {report.isLoading || !data ? <Spinner /> : (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
            <StatCard label="Patrols" value={data.patrols} />
            <StatCard label="Completed" value={data.completed} />
            <StatCard label="Incomplete" value={data.partial} tone={data.partial > 0 ? 'warning' : 'default'} />
            <StatCard label="Missed" value={data.missed} tone={data.missed > 0 ? 'danger' : 'default'} />
            <StatCard label="Checkpoints reached" value={`${data.checkpointPercent}%`} />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            {table('By route', data.byRoute)}
            {table('By guard', data.byGuard)}
          </div>
          <section className="rounded-lg border border-slate-200 bg-white p-4">
            <h3 className="mb-2 text-sm font-semibold text-slate-700">Checkpoints most often not reached</h3>
            <ul className="divide-y divide-slate-100 text-sm">
              {data.byCheckpoint.slice(0, 12).map((x) => (
                <li key={x.checkpointId} className="flex justify-between py-1"><span>{x.code} {x.name}</span><span className={cn('tabular-nums', x.missed > 0 && 'text-rose-700')}>{x.missed} of {x.expected} missed</span></li>
              ))}
            </ul>
          </section>
          <section className="rounded-lg border border-slate-200 bg-white p-4">
            <h3 className="mb-2 text-sm font-semibold text-slate-700">Security incidents</h3>
            {data.incidents.length === 0 ? <p className="text-sm text-slate-500">None in this period.</p> : (
              <ul className="space-y-1 text-sm">{data.incidents.map((x) => <li key={x.kind} className="flex justify-between"><span>{x.kind}</span><span className="tabular-nums">{x.count}</span></li>)}</ul>
            )}
          </section>
        </>
      )}
    </div>
  )
}
