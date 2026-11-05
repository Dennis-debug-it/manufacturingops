import { useState, type ReactNode } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertOctagon, CheckCircle2, Circle, Clock, Repeat, Split } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { ErrorState } from '../../../shared/components/ErrorState'
import { Badge } from '../../../shared/components/Badge'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { CriticalityBadge, Field, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useJob, useJobActions, useSplitJob } from './api'
import { ActionDialog } from './ActionDialog'
import { DeferToOverhaulDrawer } from '../planning/OverhaulPage'
import { JobStatusBadge } from './JobStatusBadge'
import { ACTION_LABELS, SUPERVISORY, isTickStep, type JobAction, type JobDetail, type JobStep } from './types'

export function FactoryJobDetailPage() {
  const { id = '' } = useParams()
  const links = useFactoryLinks()
  const { data: job, isLoading, isError } = useJob(id)
  const canSupervise = usePermission('work-orders.assign')
  const [action, setAction] = useState<JobAction | null>(null)
  const [deferring, setDeferring] = useState(false)

  if (isLoading) return <p className="p-6 text-sm text-slate-500">Loading…</p>
  if (isError || !job) return <ErrorState title="Job not found" description="It may be in a factory you don't have access to." />

  const available = job.allowedActions.filter((x) => canSupervise || !SUPERVISORY.includes(x))
  const primary = available.find((x) => ['approve', 'start', 'complete', 'verify', 'close', 'assign', 'resume'].includes(x))

  return (
    <div className="space-y-4">
      <PageHeader
        title={job.title}
        subtitle={[job.workOrderNumber, job.workType, `${job.priority} priority`, job.orgNodeName].filter(Boolean).join(' · ')}
        breadcrumbs={[{ label: 'Jobs', to: links.jobs() }, { label: job.workOrderNumber }]}
        titleAdornment={<JobStatusBadge status={job.status} holdReason={job.holdReason} />}
        primaryAction={primary ? <button type="button" className={primaryButton} onClick={() => setAction(primary)}>{ACTION_LABELS[primary]}</button> : undefined}
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            {available.filter((x) => x !== primary).map((x) => (
              <button key={x} type="button" className={secondaryButton} onClick={() => setAction(x)}>{ACTION_LABELS[x]}</button>
            ))}
            {canSupervise && !links.tech && isOpen(job) && !['Requested', 'Completed', 'Verified'].includes(job.status) ? (
              <button type="button" className={secondaryButton} onClick={() => setDeferring(true)}>Defer to overhaul</button>
            ) : null}
          </div>
        }
      />

      <div className="flex flex-wrap gap-2">
        {job.isBreakdown ? <Badge tone="danger"><AlertOctagon className="h-3.5 w-3.5" />Breakdown{job.downtimeEndedAt ? '' : ' · machine down'}</Badge> : null}
        {job.reworkOf ? <Badge tone="warning"><Repeat className="h-3.5 w-3.5" />Repeat of <Link className="underline" to={links.job(job.reworkOf.id)}>{job.reworkOf.workOrderNumber}</Link></Badge> : null}
        {job.needsLineStop ? <Badge>Needs the line stopped</Badge> : null}
        {job.responseBreached || job.repairBreached ? <Badge tone="danger"><Clock className="h-3.5 w-3.5" />{job.repairBreached ? 'Repair target missed' : 'Response target missed'}</Badge> : null}
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
        <div className="space-y-4">
          <Card title="Details">
            <dl className="grid grid-cols-[minmax(0,9rem)_1fr] gap-x-4 gap-y-2 text-sm">
              <Row label="Asset">{job.assetId ? <Link className="underline" to={links.asset(job.assetId)}>{job.assetCode} · {job.assetName}</Link> : '—'} <CriticalityBadge value={job.assetCriticality as 'A' | 'B' | 'C' | null} /></Row>
              <Row label="Reported by">{job.requestedByName ?? '—'}{job.requestSource && job.requestSource !== 'Office' ? <span className="text-slate-500"> · {job.requestSource === 'RunningCheck' ? 'running check' : job.requestSource.toLowerCase()}</span> : null}</Row>
              <Row label="Raised">{formatDateTime(job.createdAt)}</Row>
              <Row label="Respond by">{job.responseDueAt ? formatDateTime(job.responseDueAt) : '—'}</Row>
              <Row label="Repair by">{job.repairDueAt ? formatDateTime(job.repairDueAt) : job.dueDate ? formatDate(job.dueDate) : '—'}</Row>
              {job.scheduledStartAt ? <Row label="Planned for">{formatDateTime(job.scheduledStartAt)}{job.estimatedHours ? ` · ${job.estimatedHours} h` : ''}</Row> : null}
              {job.downtimeStartedAt ? <Row label="Downtime">{formatDateTime(job.downtimeStartedAt)} → {job.downtimeEndedAt ? formatDateTime(job.downtimeEndedAt) : 'still down'} ({job.downtimeHours} h)</Row> : null}
              <Row label="Team">{job.team.length ? job.team.map((t) => `${t.name}${t.isLead ? ' (lead)' : ''}`).join(', ') : '—'}</Row>
            </dl>
            {job.description ? <p className="mt-3 whitespace-pre-wrap text-sm text-slate-700">{job.description}</p> : null}
          </Card>

          {job.steps.length > 0 ? <StepsCard job={job} /> : null}

          {job.workDone || job.problem ? (
            <Card title="Completion">
              <dl className="grid grid-cols-[minmax(0,9rem)_1fr] gap-x-4 gap-y-2 text-sm">
                <Row label="Work done">{job.workDone}</Row>
                <Row label="Problem">{job.problem ? `${job.problem.name} (${job.problem.code})` : '—'}</Row>
                <Row label="Cause">{job.cause?.name ?? '—'}</Row>
                <Row label="Done">{job.remedy?.name ?? '—'}</Row>
                {job.verificationNotes ? <Row label="Checked">{job.verificationNotes}</Row> : null}
              </dl>
              {job.signature ? (
                <figure className="mt-3">
                  <img src={job.signature.dataUrl} alt={`Signature of ${job.signature.signerName}`} className="h-20 rounded border border-slate-200 bg-white" />
                  <figcaption className="text-xs text-slate-500">{job.signature.signerName}, {formatDateTime(job.signature.capturedAt)}</figcaption>
                </figure>
              ) : null}
            </Card>
          ) : null}

          <WorkLogCard job={job} />
        </div>

        <aside className="space-y-4">
          <HandoverCard job={job} />
          {job.resources.length > 0 ? (
            <Card title={`Tools and parts${job.jobPlanName ? ` · ${job.jobPlanName}` : ''}`}>
              <ul className="space-y-1 text-sm">{job.resources.map((r, i) => <li key={i}><Badge size="sm">{r.kind}</Badge> {r.description}{r.kind === 'Part' ? ` × ${r.quantity}` : ''}</li>)}</ul>
            </Card>
          ) : null}
          <ChildrenCard job={job} canSplit={canSupervise} />
          <Card title="History">
            <ol className="space-y-2 text-sm">
              {job.history.map((e, i) => (
                <li key={i}><span className="text-slate-900">{e.message}</span><span className="block text-xs text-slate-500">{formatDateTime(e.occurredAt)}{e.by ? ` · ${e.by}` : ''}</span></li>
              ))}
            </ol>
          </Card>
        </aside>
      </div>

      {action ? <ActionDialog job={job} action={action} onClose={() => setAction(null)} /> : null}
      <DeferToOverhaulDrawer open={deferring} workOrderId={job.id} workOrderNumber={job.workOrderNumber} onClose={() => setDeferring(false)} />
    </div>
  )
}

function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white p-4 shadow-sm">
      <h2 className="mb-3 text-sm font-semibold text-slate-900">{title}</h2>
      {children}
    </section>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return <div className="contents"><dt className="text-slate-500">{label}</dt><dd className="text-slate-900">{children}</dd></div>
}

const isOpen = (job: JobDetail) => !['Completed', 'Verified', 'Closed', 'Cancelled', 'Rejected'].includes(job.status)

function StepsCard({ job }: { job: JobDetail }) {
  const done = job.steps.filter((x) => x.done).length
  const sections = job.steps.reduce<{ name: string | null; steps: JobStep[] }[]>((groups, step) => {
    const last = groups[groups.length - 1]
    if (last && last.name === step.section) last.steps.push(step)
    else groups.push({ name: step.section, steps: [step] })
    return groups
  }, [])
  return (
    <Card title={`Steps · ${done} of ${job.steps.length} done`}>
      <div className="space-y-4">
        {sections.map((group, index) => (
          <div key={`${group.name ?? ''}-${index}`}>
            {group.name && sections.length > 1 ? <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">{group.name}</h3> : null}
            <ol className="space-y-1">
              {group.steps.map((step) => <StepRow key={step.id} job={job} step={step} />)}
            </ol>
          </div>
        ))}
      </div>
    </Card>
  )
}

function StepRow({ job, step }: { job: JobDetail; step: JobStep }) {
  const actions = useJobActions(job)
  const [value, setValue] = useState(step.value ?? '')
  const editable = isOpen(job)
  const save = (done: boolean, next: string | null) => void actions.step(step.id, done, step.remarks, next).catch(() => undefined)
  const number = step.type === 'number' || step.type === 'reading'
  const outOfRange = number && value !== '' && ((step.minValue != null && Number(value) < step.minValue) || (step.maxValue != null && Number(value) > step.maxValue))
  const icon = step.done ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" /> : <Circle className="mt-0.5 h-5 w-5 shrink-0 text-slate-300" />

  if (isTickStep(step)) {
    return (
      <li>
        <button type="button" disabled={!editable} className="flex w-full items-start gap-3 rounded-lg p-2 text-left hover:bg-slate-50 disabled:cursor-default" onClick={() => save(!step.done, null)}>
          {icon}
          <span className={cn('text-sm', step.done && 'text-slate-500 line-through')}>
            {step.instruction}{step.required ? null : <span className="text-xs text-slate-400"> (optional)</span>}
            {step.hint ? <span className="block text-xs text-slate-400 no-underline">{step.hint}</span> : null}
          </span>
        </button>
      </li>
    )
  }

  const passFail = ['passfail', 'yesno', 'boolean'].includes(step.type)
  return (
    <li className="flex flex-wrap items-start gap-3 rounded-lg p-2">
      {icon}
      <div className="min-w-0 flex-1">
        <p className="text-sm text-slate-800">{step.instruction}{step.required ? null : <span className="text-xs text-slate-400"> (optional)</span>}</p>
        {step.minValue != null || step.maxValue != null ? <p className="text-xs text-slate-500">Limits {step.minValue ?? '…'}–{step.maxValue ?? '…'} {step.unit}</p> : null}
        {editable ? (
          passFail ? (
            <div className="mt-1 flex gap-2">
              {[{ v: step.type === 'yesno' ? 'yes' : 'pass', label: step.type === 'yesno' ? 'Yes' : 'OK' }, { v: step.type === 'yesno' ? 'no' : 'fail', label: step.type === 'yesno' ? 'No' : 'Not OK' }].map((option) => (
                <button key={option.v} type="button" aria-pressed={step.value === option.v} onClick={() => save(true, option.v)}
                  className={cn('h-10 rounded-lg border px-4 text-sm font-medium', step.value === option.v ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700')}>
                  {option.label}
                </button>
              ))}
            </div>
          ) : (
            <div className="mt-1 flex items-center gap-2">
              <input aria-label={step.instruction} type={number ? 'number' : 'text'} inputMode={number ? 'decimal' : undefined} step="any"
                className={cn(inputClass, number ? 'max-w-32' : 'max-w-sm', outOfRange && 'border-amber-500')} value={value} onChange={(event) => setValue(event.target.value)} />
              {step.unit ? <span className="text-sm text-slate-500">{step.unit}</span> : null}
              <button type="button" className={secondaryButton} disabled={!value.trim() || value === step.value} onClick={() => save(true, value.trim())}>Save</button>
            </div>
          )
        ) : <p className="text-sm font-medium text-slate-900">{step.value ?? '—'} {step.value ? step.unit : null}</p>}
        {outOfRange ? <p className="mt-1 text-xs text-amber-700">Outside the limits: say what you found in a handover note or raise a job.</p> : null}
      </div>
    </li>
  )
}

function WorkLogCard({ job }: { job: JobDetail }) {
  const actions = useJobActions(job)
  const [minutes, setMinutes] = useState('')
  const [notes, setNotes] = useState('')
  // Blank means "me" (the signed-in technician); a supervisor logging for the team picks the person.
  const [who, setWho] = useState('')
  const total = job.labour.reduce((sum, x) => sum + x.minutes, 0)
  const add = async () => {
    try {
      const result = await actions.labour(Number(minutes), notes || null, who || null)
      toast.success(result.queued ? 'Time saved, will sync' : 'Time recorded')
      setMinutes('')
      setNotes('')
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <Card title={`Labour · ${(total / 60).toFixed(1)} h`}>
      {job.labour.length > 0 ? (
        <ul className="mb-3 space-y-1 text-sm">{job.labour.map((x) => <li key={x.id}>{x.technicianName}: {(x.minutes / 60).toFixed(1)} h <span className="text-xs text-slate-500">{formatDateTime(x.startedAt)}{x.notes ? ` · ${x.notes}` : ''}</span></li>)}</ul>
      ) : null}
      {job.parts.length > 0 ? (
        <ul className="mb-3 space-y-1 text-sm">{job.parts.map((x) => <li key={x.id}>{x.partName} × {x.quantity}</li>)}</ul>
      ) : null}
      {isOpen(job) ? (
        <div className="grid gap-2 sm:grid-cols-[10rem_8rem_1fr_auto] sm:items-end">
          <Field label="Whose time">
            <select className={inputClass} value={who} onChange={(event) => setWho(event.target.value)}>
              <option value="">Mine</option>
              {job.team.map((x) => <option key={x.technicianId} value={x.technicianId}>{x.name}</option>)}
            </select>
          </Field>
          <Field label="Minutes"><input type="number" min={1} className={inputClass} value={minutes} onChange={(event) => setMinutes(event.target.value)} /></Field>
          <Field label="What you did"><input className={inputClass} value={notes} onChange={(event) => setNotes(event.target.value)} /></Field>
          <button type="button" className={secondaryButton} disabled={!minutes || Number(minutes) <= 0} onClick={() => void add()}>Add time</button>
        </div>
      ) : null}
    </Card>
  )
}

function HandoverCard({ job }: { job: JobDetail }) {
  const actions = useJobActions(job)
  const [note, setNote] = useState('')
  const save = async () => {
    try {
      const result = await actions.handover(note)
      toast.success(result.queued ? 'Note saved, will sync' : 'Handover note added')
      setNote('')
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <Card title="Shift handover">
      {job.handover.length === 0 ? <p className="text-sm text-slate-500">No notes yet.</p> : (
        <ul className="space-y-2 text-sm">{job.handover.map((h, i) => <li key={i} className="rounded-lg bg-amber-50 p-2">{h.message}<span className="block text-xs text-slate-500">{h.by} · {formatDateTime(h.occurredAt)}</span></li>)}</ul>
      )}
      {isOpen(job) ? (
        <div className="mt-3 space-y-2">
          <textarea className={textareaClass} value={note} onChange={(event) => setNote(event.target.value)} placeholder="What the next shift needs to know" />
          <button type="button" className={secondaryButton} disabled={!note.trim()} onClick={() => void save()}>Add note</button>
        </div>
      ) : null}
    </Card>
  )
}

function ChildrenCard({ job, canSplit }: { job: JobDetail; canSplit: boolean }) {
  const links = useFactoryLinks()
  const split = useSplitJob(job.id)
  const [title, setTitle] = useState('')
  const add = async () => {
    try {
      const child = await split.mutateAsync({ title })
      toast.success(`${child.workOrderNumber} raised`)
      setTitle('')
    } catch {
      // The API client shows the error.
    }
  }
  if (!job.parent && job.children.length === 0 && !(canSplit && isOpen(job))) return null
  return (
    <Card title="Linked jobs">
      {job.parent ? <p className="text-sm">Part of <Link className="underline" to={links.job(job.parent.id)}>{job.parent.workOrderNumber}</Link> · {job.parent.title}</p> : null}
      <ul className="space-y-1 text-sm">{job.children.map((c) => <li key={c.id}><Link className="underline" to={links.job(c.id)}>{c.workOrderNumber}</Link> {c.title} <span className="text-xs text-slate-500">· {c.status}</span></li>)}</ul>
      {canSplit && isOpen(job) ? (
        <div className="mt-3 flex gap-2">
          <input className={inputClass} value={title} onChange={(event) => setTitle(event.target.value)} placeholder="Split off a job, e.g. electrical work" />
          <button type="button" className={secondaryButton} disabled={!title.trim() || split.isPending} onClick={() => void add()} aria-label="Split off a job"><Split className="h-4 w-4" /></button>
        </div>
      ) : null}
    </Card>
  )
}
