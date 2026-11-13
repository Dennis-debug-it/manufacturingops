import { useEffect, useMemo, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import toast from 'react-hot-toast'
import { AlertOctagon, ArrowRightLeft, CheckCircle2, Circle, CircleDot, CloudOff, MapPin, Nfc, Play, QrCode, ShieldAlert, TriangleAlert } from 'lucide-react'
import { Badge, type BadgeTone } from '../../shared/components/Badge'
import { Spinner } from '../../shared/components/Spinner'
import { EmptyState } from '../../shared/components/EmptyState'
import { FormDrawer } from '../../shared/components/FormDrawer'
import { useAuth } from '../../shared/hooks/useAuth'
import { formatTime } from '../../shared/utils/dates'
import { cn } from '../../shared/utils/cn'
import { Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../factory/components/ui'
import { CameraScanner } from '../factory/components/CameraScanner'
import { performAction } from '../tech/offline/outbox'
import { refreshAfterSync, usePendingActions } from '../tech/offline/hooks'
import { startOfflinePersistence } from '../tech/offline/persist'
import { useStaff } from '../visitors/api'
import {
  GUARD_KEY, INCIDENT_KINDS, RUN_LABEL, SEVERITIES, VALIDATION_LABEL, deviceId, findCheckpoint, stopStates, useGuardBundle, useHandOver, usePanic, usePatrolAccess,
  useReportIncident, useStartOwnPatrol, useStartRun, type GuardBundle, type GuardCheckpoint, type GuardRun, type RunStatus, type ScanPayload, type ScanResult, type StopState,
} from './api'
import { freshPosition, nfcSupported, useGeoWatch, useNfcReader } from './device'

const RUN_TONE: Record<RunStatus, BadgeTone> = { Due: 'info', InProgress: 'warning', Completed: 'success', Partial: 'warning', Missed: 'danger' }

/**
 * The guard's phone: today's patrols, scanning checkpoints (QR or NFC), reporting incidents, the panic
 * button and the shift handover. Works with no signal: the patrol list and checkpoints are kept on the
 * phone, and scans are saved and sent when the signal is back, dated when they were made.
 */
export function PatrolGuardPage({ keepOffline = true }: { keepOffline?: boolean }) {
  const access = usePatrolAccess()
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const canPatrol = access.data?.canPatrol ?? false
  const bundle = useGuardBundle(canPatrol)
  const pending = usePendingActions()
  const [activeRunId, setActiveRunId] = useState<string | null>(null)
  const [sheet, setSheet] = useState<'incident' | 'handover' | 'panic' | null>(null)
  const userId = user?.userId

  // The technician app already keeps its data on the phone; elsewhere this page starts it.
  useEffect(() => (keepOffline && canPatrol && userId ? startOfflinePersistence(queryClient, userId) : undefined), [keepOffline, canPatrol, queryClient, userId])

  const data = bundle.data
  const runs = data?.runs ?? []
  const pendingScans = pending.filter((x) => x.kind === 'patrol-scan')
  const active = runs.find((x) => x.id === activeRunId) ?? runs.find((x) => x.status === 'InProgress' && x.guardName === user?.fullName) ?? null
  const factoryId = active?.factoryId ?? data?.routes[0]?.factoryId ?? data?.checkpoints[0]?.factoryId ?? ''
  const geo = useGeoWatch(Boolean(active) || sheet === 'incident' || sheet === 'panic')

  if (access.isLoading || (canPatrol && bundle.isLoading && !data)) return <Spinner label="Loading your patrols" />
  if (!canPatrol) return <EmptyState title="Security patrol" description="This is for security guards. Ask your supervisor for patrol access." />
  if (!data) return <EmptyState title="No patrol list on this phone" description="Connect once to load today's patrols; after that the app works with no signal." />

  return (
    <div className="mx-auto max-w-xl space-y-4 pb-24">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-slate-900">Patrol</h1>
          <p className="text-xs text-slate-500">List updated {formatTime(data.generatedAt)}{pendingScans.length ? ` · ${pendingScans.length} scan${pendingScans.length === 1 ? '' : 's'} waiting for signal` : ''}</p>
        </div>
        <button type="button" onClick={() => setSheet('panic')} className="inline-flex h-12 items-center gap-2 rounded-xl bg-rose-600 px-4 text-sm font-bold uppercase tracking-wide text-white shadow active:bg-rose-700">
          <AlertOctagon className="h-5 w-5" />Panic
        </button>
      </div>

      {pendingScans.length > 0 ? (
        <div className="flex items-center gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          <CloudOff className="h-4 w-4 shrink-0" />Scans are saved on this phone and sent when the signal is back, with the time they were made.
        </div>
      ) : null}

      {active ? (
        <ActivePatrol run={active} bundle={data} position={freshPosition(geo.position)} geoError={geo.error}
          pendingIds={pendingScans.filter((x) => (x.meta?.runId as string | null) === active.id).map((x) => x.meta?.checkpointId as string)}
          onLeave={() => setActiveRunId(null)} />
      ) : (
        <PatrolList runs={runs} bundle={data} onOpen={setActiveRunId} />
      )}

      <div className="grid grid-cols-2 gap-3">
        <button type="button" className={cn(secondaryButton, 'h-14')} onClick={() => setSheet('incident')}><ShieldAlert className="h-5 w-5" />Report incident</button>
        <button type="button" className={cn(secondaryButton, 'h-14')} onClick={() => setSheet('handover')}><ArrowRightLeft className="h-5 w-5" />Hand over</button>
      </div>

      {sheet === 'incident' ? <IncidentSheet factoryId={factoryId} runId={active?.id ?? null} position={freshPosition(geo.position)} onClose={() => setSheet(null)} /> : null}
      {sheet === 'panic' ? <PanicSheet factoryId={factoryId} position={freshPosition(geo.position)} onClose={() => setSheet(null)} /> : null}
      {sheet === 'handover' ? <HandoverSheet factoryId={factoryId} onClose={() => setSheet(null)} /> : null}
    </div>
  )
}

/* ── Patrol list ────────────────────────────────────────────────────────── */

function PatrolList({ runs, bundle, onOpen }: { runs: GuardRun[]; bundle: GuardBundle; onOpen: (id: string) => void }) {
  const start = useStartRun()
  const startOwn = useStartOwnPatrol()
  const [routeId, setRouteId] = useState('')
  // "Now", refreshed every half minute, so a patrol's Start button appears when it opens.
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 30_000)
    return () => window.clearInterval(timer)
  }, [])
  const begin = async (run: GuardRun) => {
    if (run.status === 'InProgress') return onOpen(run.id)
    if (!navigator.onLine) return onOpen(run.id) // No signal: walk it anyway; the first scan starts it on the server.
    try {
      await start.mutateAsync(run.id)
      onOpen(run.id)
    } catch { /* shown by the API client */ }
  }
  const own = async () => {
    try {
      const run = await startOwn.mutateAsync(routeId)
      onOpen(run.id)
    } catch { /* shown by the API client */ }
  }

  return (
    <section className="space-y-3">
      <h2 className="text-sm font-semibold text-slate-700">Patrols due</h2>
      {runs.length === 0 ? <p className="rounded-lg border border-slate-200 bg-white p-4 text-sm text-slate-600">No patrols due in the next 12 hours.</p> : runs.map((run) => {
        const soon = new Date(run.dueFrom).getTime() - now <= 30 * 60_000
        return (
          <article key={run.id} className="rounded-xl border border-slate-200 bg-white p-4">
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="text-base font-semibold text-slate-900">{run.routeName}</div>
                <div className="text-sm text-slate-600">{formatTime(run.dueFrom)}–{formatTime(run.dueBy)} · {run.factoryName}</div>
              </div>
              <Badge tone={RUN_TONE[run.status]}>{RUN_LABEL[run.status]}</Badge>
            </div>
            <Progress valid={run.validCount} required={run.requiredCount} />
            {soon || run.status === 'InProgress' ? (
              <button type="button" className={cn(primaryButton, 'mt-3 h-12 w-full text-base')} disabled={start.isPending} onClick={() => void begin(run)}>
                <Play className="h-5 w-5" />{run.status === 'InProgress' ? 'Continue patrol' : 'Start patrol'}
              </button>
            ) : <p className="mt-2 text-xs text-slate-500">Opens 30 minutes before it is due.</p>}
          </article>
        )
      })}
      {bundle.routes.length > 0 ? (
        <div className="space-y-2 rounded-xl border border-dashed border-slate-300 p-4">
          <div className="text-sm font-medium text-slate-700">Extra patrol</div>
          <div className="flex gap-2">
            <select className={inputClass} value={routeId} onChange={(e) => setRouteId(e.target.value)} aria-label="Route">
              <option value="">Choose a route</option>
              {bundle.routes.map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
            </select>
            <button type="button" className={secondaryButton} disabled={!routeId || startOwn.isPending} onClick={() => void own()}>Start</button>
          </div>
        </div>
      ) : null}
    </section>
  )
}

function Progress({ valid, required, pending = 0 }: { valid: number; required: number; pending?: number }) {
  const done = Math.min(required, valid + pending)
  return (
    <div className="mt-3">
      <div className="flex h-2 overflow-hidden rounded-full bg-slate-100">
        <div className="h-full bg-emerald-500" style={{ width: `${required ? (Math.min(valid, required) / required) * 100 : 0}%` }} />
        <div className="h-full bg-sky-300" style={{ width: `${required ? ((done - Math.min(valid, required)) / required) * 100 : 0}%` }} title="Waiting for signal" />
      </div>
      <div className="mt-1 text-xs text-slate-500">{done} of {required} checkpoints{pending ? ` (${pending} waiting for signal)` : ''}</div>
    </div>
  )
}

/* ── Active patrol ──────────────────────────────────────────────────────── */

const STOP_ICON: Record<StopState, typeof Circle> = { done: CheckCircle2, flagged: TriangleAlert, 'pending-sync': CloudOff, next: CircleDot, todo: Circle, optional: Circle }
const STOP_TONE: Record<StopState, string> = {
  done: 'text-emerald-600', flagged: 'text-amber-600', 'pending-sync': 'text-sky-600', next: 'text-[var(--app-primary)]', todo: 'text-slate-300', optional: 'text-slate-300',
}

function ActivePatrol({ run, bundle, position, geoError, pendingIds, onLeave }: {
  run: GuardRun; bundle: GuardBundle; position: ReturnType<typeof freshPosition>; geoError: string | null; pendingIds: string[]; onLeave: () => void
}) {
  const queryClient = useQueryClient()
  const route = bundle.routes.find((x) => x.id === run.routeId)
  const [mode, setMode] = useState<'qr' | 'nfc' | null>(null)
  const [asking, setAsking] = useState<{ checkpoint: GuardCheckpoint; code: string; byNfc: boolean } | null>(null)
  const [last, setLast] = useState<{ text: string; tone: 'ok' | 'warn' | 'queued' } | null>(null)
  const [cameraOff, setCameraOff] = useState<string | null>(null)
  // The checkpoint just recorded and when, so the same label still in view is not recorded twice.
  const [recent, setRecent] = useState<{ checkpointId: string; at: number } | null>(null)
  const states = useMemo(() => (route ? stopStates(route, run.scans, pendingIds) : new Map<string, StopState>()), [route, run.scans, pendingIds])
  const checkpoints = new Map(bundle.checkpoints.map((x) => [x.id, x]))

  useNfcReader(mode === 'nfc', (serial) => onCode(serial, true), (reason) => { toast.error(reason); setMode(null) })

  function onCode(code: string, byNfc: boolean) {
    // While the questions are open the camera keeps running; ignore what it sees.
    if (asking) return
    const checkpoint = findCheckpoint(bundle, code, byNfc)
    if (!checkpoint && !navigator.onLine) {
      setLast({ text: 'Not one of our checkpoints (or added since this phone last had signal).', tone: 'warn' })
      return
    }
    if (checkpoint && recent?.checkpointId === checkpoint.id && Date.now() - recent.at < 60_000) return
    if (checkpoint) setRecent({ checkpointId: checkpoint.id, at: Date.now() })
    // The camera stays on for the next checkpoint; NFC listens again on the next tap.
    if (byNfc) setMode(null)
    if (checkpoint && checkpoint.questions.length > 0) setAsking({ checkpoint, code, byNfc })
    else void send(checkpoint, code, byNfc, null, null)
  }

  async function send(checkpoint: GuardCheckpoint | null, code: string, byNfc: boolean, answers: { question: string; ok: boolean }[] | null, note: string | null) {
    const payload: ScanPayload = {
      clientScanId: crypto.randomUUID(), code, scannedAt: new Date().toISOString(), runId: run.id,
      latitude: position?.latitude ?? null, longitude: position?.longitude ?? null, accuracyM: position ? Math.round(position.accuracyM) : null,
      deviceId: deviceId(), answers, note, byNfc,
    }
    const label = checkpoint ? `${checkpoint.code} ${checkpoint.name}` : 'Checkpoint'
    try {
      const outcome = await performAction<ScanResult>({
        kind: 'patrol-scan', label: `${label} scanned on ${run.routeName}`, workOrderId: null, method: 'POST', url: '/api/security/scans', body: payload,
        meta: { checkpointId: checkpoint?.id ?? null, runId: run.id },
      }, () => refreshAfterSync(queryClient))
      if (outcome.queued || !outcome.data) {
        setLast({ text: `${label}: saved on the phone, sent when there is signal.`, tone: 'queued' })
      } else {
        const result = outcome.data
        setLast(result.validation === 'Valid'
          ? { text: `${result.checkpointCode} ${result.checkpointName} ✓${result.validCount !== null ? ` · ${result.validCount} of ${result.requiredCount}` : ''}`, tone: 'ok' }
          : { text: `${result.checkpointCode}: ${VALIDATION_LABEL[result.validation]}. ${result.note ?? ''}`, tone: 'warn' })
        if (result.incidentId) toast('Incident raised for the supervisor.')
        if (result.runStatus === 'Completed') toast.success('Patrol complete')
        void queryClient.invalidateQueries({ queryKey: GUARD_KEY })
      }
      navigator.vibrate?.(resultVibration(outcome.data?.validation))
    } catch { /* shown by the API client */ }
  }

  if (!route) return <p className="text-sm text-slate-500">This patrol's route is not on the phone yet.</p>
  const ordered = [...route.stops].sort((a, b) => a.sequence - b.sequence)
  const pending = new Set(pendingIds.filter((id) => !run.scans.some((s) => s.checkpointId === id && s.validation === 'Valid'))).size

  return (
    <section className="space-y-3">
      <div className="rounded-xl border border-slate-200 bg-white p-4">
        <div className="flex items-start justify-between">
          <div>
            <div className="text-base font-semibold text-slate-900">{run.routeName}</div>
            <div className="text-sm text-slate-600">Due by {formatTime(run.dueBy)}{run.ordered ? ' · in order' : ''}</div>
          </div>
          <button type="button" className="text-sm text-[var(--app-primary)]" onClick={onLeave}>All patrols</button>
        </div>
        <Progress valid={run.validCount} required={run.requiredCount} pending={pending} />
        <div className="mt-2 flex items-center gap-1 text-xs text-slate-500">
          <MapPin className="h-3.5 w-3.5" />{position ? `GPS ±${Math.round(position.accuracyM)} m` : geoError ?? 'Waiting for GPS…'}
        </div>
      </div>

      {last ? (
        <div className={cn('rounded-lg border p-3 text-sm font-medium', last.tone === 'ok' ? 'border-emerald-200 bg-emerald-50 text-emerald-800' : last.tone === 'warn' ? 'border-amber-200 bg-amber-50 text-amber-800' : 'border-sky-200 bg-sky-50 text-sky-800')}>
          {last.text}
        </div>
      ) : null}

      {mode === 'qr' ? (
        cameraOff ? <p className="text-sm text-slate-500">{cameraOff}</p> : (
          <div className="space-y-1">
            <CameraScanner onCode={(code) => onCode(code, false)} onUnavailable={setCameraOff} />
            <p className="text-center text-xs text-slate-500">The camera stays on: walk to the next checkpoint and point it at the label.</p>
          </div>
        )
      ) : null}
      {mode === 'nfc' ? <div className="rounded-xl border-2 border-dashed border-[var(--app-primary)] p-6 text-center text-sm text-slate-700"><Nfc className="mx-auto mb-2 h-8 w-8" />Hold the phone against the tag…</div> : null}

      <div className="grid grid-cols-2 gap-3">
        <button type="button" className={cn(primaryButton, 'h-14 text-base')} onClick={() => setMode(mode === 'qr' ? null : 'qr')}><QrCode className="h-5 w-5" />{mode === 'qr' ? 'Stop' : 'Scan QR'}</button>
        <button type="button" className={cn(primaryButton, 'h-14 text-base')} disabled={!nfcSupported()} onClick={() => setMode(mode === 'nfc' ? null : 'nfc')}
          title={nfcSupported() ? undefined : 'NFC works in Chrome on Android'}><Nfc className="h-5 w-5" />{mode === 'nfc' ? 'Stop' : 'Tap NFC'}</button>
      </div>

      <ol className="divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
        {ordered.map((stop) => {
          const checkpoint = checkpoints.get(stop.checkpointId)
          const state = states.get(stop.checkpointId) ?? 'todo'
          const Icon = STOP_ICON[state]
          const flagged = run.scans.filter((x) => x.checkpointId === stop.checkpointId && x.validation !== 'Valid' && x.validation !== 'Repeat').at(-1)
          return (
            <li key={stop.checkpointId} className={cn('flex items-start gap-3 p-3', state === 'next' && 'bg-[var(--app-primary-soft)]')}>
              <Icon className={cn('mt-0.5 h-5 w-5 shrink-0', STOP_TONE[state])} />
              <div className="min-w-0">
                <div className="text-sm font-medium text-slate-900">{checkpoint?.code} {checkpoint?.name}{stop.required ? '' : <span className="text-xs font-normal text-slate-500"> · optional</span>}</div>
                {state === 'next' && checkpoint?.instructions ? <div className="text-xs text-slate-600">{checkpoint.instructions}</div> : null}
                {state === 'flagged' && flagged ? <div className="text-xs text-amber-700">{VALIDATION_LABEL[flagged.validation]}{flagged.note ? `: ${flagged.note}` : ''}</div> : null}
                {state === 'pending-sync' ? <div className="text-xs text-sky-700">Scanned, waiting for signal</div> : null}
                {checkpoint?.method === 'Nfc' && state !== 'done' ? <div className="text-xs text-slate-500">Tap the NFC tag here</div> : null}
              </div>
            </li>
          )
        })}
      </ol>

      {asking ? (
        <QuestionSheet checkpoint={asking.checkpoint} onCancel={() => setAsking(null)}
          onDone={(answers, note) => { const a = asking; setAsking(null); void send(a.checkpoint, a.code, a.byNfc, answers, note) }} />
      ) : null}
    </section>
  )
}

function resultVibration(validation: string | undefined): number | number[] {
  return validation && validation !== 'Valid' ? [80, 60, 80] : 60
}

function QuestionSheet({ checkpoint, onCancel, onDone }: {
  checkpoint: GuardCheckpoint; onCancel: () => void; onDone: (answers: { question: string; ok: boolean }[], note: string | null) => void
}) {
  const [answers, setAnswers] = useState<Record<string, boolean>>({})
  const [note, setNote] = useState('')
  const complete = checkpoint.questions.every((q) => answers[q] !== undefined)
  const anyNo = Object.values(answers).some((x) => x === false)
  return (
    <FormDrawer open title={`${checkpoint.code} ${checkpoint.name}`} onClose={onCancel}
      footer={<button type="button" className={cn(primaryButton, 'h-12 w-full text-base')} disabled={!complete} onClick={() => onDone(checkpoint.questions.map((q) => ({ question: q, ok: answers[q]! })), note.trim() || null)}>Record checkpoint</button>}>
      <div className="space-y-4">
        {checkpoint.instructions ? <p className="text-sm text-slate-600">{checkpoint.instructions}</p> : null}
        {checkpoint.questions.map((question) => (
          <fieldset key={question} className="space-y-2">
            <legend className="text-sm font-medium text-slate-800">{question}</legend>
            <div className="grid grid-cols-2 gap-2">
              {[true, false].map((value) => (
                <button key={String(value)} type="button" aria-pressed={answers[question] === value}
                  className={cn('h-12 rounded-lg border text-base font-medium', answers[question] === value ? (value ? 'border-emerald-600 bg-emerald-600 text-white' : 'border-rose-600 bg-rose-600 text-white') : 'border-slate-300 bg-white text-slate-700')}
                  onClick={() => setAnswers({ ...answers, [question]: value })}>{value ? 'Yes' : 'No'}</button>
              ))}
            </div>
          </fieldset>
        ))}
        {anyNo ? <p className="text-sm text-rose-700">A "No" sends an incident to the supervisor.</p> : null}
        <Field label="Note (optional)"><textarea className={textareaClass} value={note} onChange={(e) => setNote(e.target.value)} /></Field>
      </div>
    </FormDrawer>
  )
}

/* ── Incident, panic, handover ──────────────────────────────────────────── */

function IncidentSheet({ factoryId, runId, position, onClose }: { factoryId: string; runId: string | null; position: ReturnType<typeof freshPosition>; onClose: () => void }) {
  const report = useReportIncident()
  const [form, setForm] = useState({ kind: 'SuspiciousPerson', severity: 'Moderate', title: '', description: '' })
  const submit = async () => {
    try {
      const incident = await report.mutateAsync({
        factoryId, kind: form.kind, severity: form.severity, title: form.title.trim(), description: form.description.trim() || null, checkpointId: null, runId,
        latitude: position?.latitude ?? null, longitude: position?.longitude ?? null,
      })
      toast.success(`${incident.incidentNumber} sent to the supervisor`)
      onClose()
    } catch { /* shown by the API client; needs a signal */ }
  }
  return (
    <FormDrawer open title="Report an incident" onClose={onClose}
      footer={<button type="button" className={cn(primaryButton, 'h-12 w-full text-base')} disabled={report.isPending || !form.title.trim() || !factoryId} onClick={() => void submit()}>Send to supervisor</button>}>
      <div className="space-y-4">
        <Field label="What happened">
          <select className={inputClass} value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value })}>
            {INCIDENT_KINDS.filter((x) => x.value !== 'Emergency' && x.value !== 'CheckpointIssue').map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
          </select>
        </Field>
        <Field label="How serious"><select className={inputClass} value={form.severity} onChange={(e) => setForm({ ...form, severity: e.target.value })}>{SEVERITIES.map((x) => <option key={x}>{x}</option>)}</select></Field>
        <Field label="In a few words" required><input className={inputClass} value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} placeholder="e.g. Fence cut behind the boiler house" /></Field>
        <Field label="Details"><textarea className={textareaClass} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} /></Field>
        <p className="text-xs text-slate-500">{position ? 'Your location is sent with it.' : 'No GPS position: say where it is in the details.'}</p>
      </div>
    </FormDrawer>
  )
}

function PanicSheet({ factoryId, position, onClose }: { factoryId: string; position: ReturnType<typeof freshPosition>; onClose: () => void }) {
  const panic = usePanic()
  const press = async () => {
    try {
      await panic.mutateAsync({ factoryId, latitude: position?.latitude ?? null, longitude: position?.longitude ?? null, note: null })
      toast.success('Supervisors alerted')
      onClose()
    } catch {
      toast.error('No signal: the alert could not be sent. Use the radio or call the supervisor.')
    }
  }
  return (
    <FormDrawer open title="Panic" onClose={onClose}>
      <div className="space-y-4 text-center">
        <p className="text-sm text-slate-700">Alert every supervisor now, by app, SMS and WhatsApp, with your location.</p>
        <button type="button" disabled={panic.isPending || !factoryId} onClick={() => void press()}
          className="mx-auto flex h-40 w-40 items-center justify-center rounded-full bg-rose-600 text-xl font-bold uppercase text-white shadow-lg active:bg-rose-700 disabled:opacity-60">
          Send alert
        </button>
        <p className="text-xs text-slate-500">{position ? `Location ±${Math.round(position.accuracyM)} m` : 'No GPS position yet.'} Needs a signal; otherwise use the radio.</p>
      </div>
    </FormDrawer>
  )
}

function HandoverSheet({ factoryId, onClose }: { factoryId: string; onClose: () => void }) {
  const handOver = useHandOver()
  const staff = useStaff('')
  const [form, setForm] = useState({ shiftName: '', incomingUserId: '', openIssues: '', keysAndEquipment: '' })
  const submit = async () => {
    try {
      await handOver.mutateAsync({
        factoryId, shiftName: form.shiftName.trim() || null, incomingUserId: form.incomingUserId || null,
        openIssues: form.openIssues.trim() || null, keysAndEquipment: form.keysAndEquipment.trim() || null,
      })
      toast.success('Shift handed over')
      onClose()
    } catch { /* shown by the API client */ }
  }
  return (
    <FormDrawer open title="Hand over the shift" onClose={onClose}
      footer={<button type="button" className={cn(primaryButton, 'h-12 w-full text-base')} disabled={handOver.isPending || !factoryId} onClick={() => void submit()}>Hand over</button>}>
      <div className="space-y-4">
        <Field label="Shift"><input className={inputClass} placeholder="Night" value={form.shiftName} onChange={(e) => setForm({ ...form, shiftName: e.target.value })} /></Field>
        <Field label="Incoming guard">
          <select className={inputClass} value={form.incomingUserId} onChange={(e) => setForm({ ...form, incomingUserId: e.target.value })}>
            <option value="">Choose</option>
            {(staff.data ?? []).map((x) => <option key={x.id} value={x.id}>{x.fullName}</option>)}
          </select>
        </Field>
        <Field label="Open issues" hint="Anything still going on that the next shift must watch."><textarea className={textareaClass} value={form.openIssues} onChange={(e) => setForm({ ...form, openIssues: e.target.value })} /></Field>
        <Field label="Keys and equipment handed over"><textarea className={textareaClass} value={form.keysAndEquipment} onChange={(e) => setForm({ ...form, keysAndEquipment: e.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}

/** The guard app inside the technician app, which already keeps its data on the phone. */
export function TechPatrolPage() {
  return <PatrolGuardPage keepOffline={false} />
}
