import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '../../../shared/api/apiClient'
import { CheckCircle2, Circle, Plus, Printer, Repeat, Search, X } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { Badge } from '../../../shared/components/Badge'
import { Spinner } from '../../../shared/components/Spinner'
import { ErrorState } from '../../../shared/components/ErrorState'
import { SignaturePad } from '../../../shared/components/SignaturePad'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePermission } from '../../../shared/hooks/usePermission'
import { formatDate, formatDateTime, formatTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, formatNumber, inputClass, primaryButton, secondaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { JobStatusBadge } from '../jobs/JobStatusBadge'
import { WINDOW_STATUS_LABELS, useWindow, useWindowAction, useWindowCandidates, useWindowPack, type WindowDetail } from './api'
import { CapacityBar, WINDOW_TONES } from './WindowsPage'

/** One weekly window: its jobs and capacity, the pack, the day itself and the hand-back. */
export function WindowDetailPage() {
  const { id = '' } = useParams()
  const links = useFactoryLinks()
  const canPlan = usePermission('work-orders.assign')
  const canRun = usePermission('work-orders.complete') || canPlan
  const { data, isLoading, isError } = useWindow(id)
  // Keep the pack on the phone once the window is packed, for the day itself.
  useWindowPack(id, data ? ['Packed', 'InProgress'].includes(data.window.status) : false)
  const action = useWindowAction(id)
  const queryClient = useQueryClient()
  const [drawer, setDrawer] = useState<'add' | 'edit' | 'skip' | 'handback' | null>(null)

  if (isLoading) return <Spinner label="Loading window" />
  if (isError || !data) return <ErrorState title="Window not found" description="It may be in a factory you don't have access to." />
  const { window, jobs, handbackItems } = data
  const editable = window.status === 'Planned' || window.status === 'Packed'
  const ticked = handbackItems.filter((x) => x.done).length

  const run = async (path: string, done: string, method: 'post' | 'delete' = 'post', body?: unknown) => {
    try {
      await action.mutateAsync({ method, path, body })
      toast.success(done)
    } catch {
      // The API client shows the error.
    }
  }

  /** Holds each job's planned parts in the factory store, so they are there on the day. */
  const reserveSpares = async () => {
    try {
      const result = await api.post<{ linesReserved: number; shortages: { workOrderNumber: string; itemName: string; short: number; unitOfMeasure: string; isCritical: boolean }[] }>(`/api/factory/windows/${window.id}/reserve-spares`)
      if (result.shortages.length) toast(`Reserved ${result.linesReserved} line(s). Short: ${result.shortages.map((s) => `${s.itemName} ×${s.short} (${s.workOrderNumber})`).join('; ')}`, { icon: '⚠️', duration: 10_000 })
      else toast.success(result.linesReserved ? `Reserved ${result.linesReserved} line(s); nothing short` : 'Everything planned is already reserved')
      void queryClient.invalidateQueries({ queryKey: ['factory', 'window-pack'] })
      void queryClient.invalidateQueries({ queryKey: ['factory', 'stores'] })
    } catch {
      // The API client shows the error.
    }
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title={`${window.factoryName} · ${formatDate(window.windowDate, 'EEEE dd MMM')}`}
        subtitle={`${formatTime(window.startsAt)}–${formatTime(window.endsAt)}${window.movedFromDate ? ` · moved from ${formatDate(window.movedFromDate, 'EEE dd MMM')}` : ''}`}
        breadcrumbs={[{ label: 'Weekly window', to: links.windows() }, { label: formatDate(window.windowDate, 'dd MMM') }]}
        titleAdornment={<Badge tone={WINDOW_TONES[window.status]}>{WINDOW_STATUS_LABELS[window.status]}</Badge>}
        primaryAction={
          window.status === 'Planned' && canPlan ? <button type="button" className={primaryButton} onClick={() => void run('/pack', 'Window packed')}>Pack the window</button>
            : window.status === 'Packed' && canRun ? <button type="button" className={primaryButton} onClick={() => void run('/start', 'Window started')}>Start the window</button>
              : window.status === 'InProgress' && canRun ? <button type="button" className={primaryButton} disabled={ticked < handbackItems.length} onClick={() => setDrawer('handback')}>Hand back to production</button>
                : undefined
        }
        secondaryActions={
          <div className="flex flex-wrap gap-2">
            {window.status !== 'Skipped' ? <Link to={`${links.window(window.id)}/pack`} className={secondaryButton}><Printer className="h-4 w-4" />Job pack</Link> : null}
            {editable && canPlan ? <button type="button" className={secondaryButton} onClick={() => void reserveSpares()}>Reserve spares</button> : null}
            {editable && canPlan ? <button type="button" className={secondaryButton} onClick={() => setDrawer('edit')}>Move or edit</button> : null}
            {editable && canPlan ? <button type="button" className={secondaryButton} onClick={() => setDrawer('skip')}>Skip this week</button> : null}
          </div>
        }
      />

      {window.status === 'Skipped' ? <p className="rounded-lg bg-slate-100 p-3 text-sm text-slate-700">Skipped: {window.skipReason}. Its jobs moved to the next window.</p> : null}
      {window.status === 'HandedBack' ? (
        <p className="rounded-lg bg-emerald-50 p-3 text-sm text-emerald-900">Handed back {window.handedBackAt ? formatDateTime(window.handedBackAt) : ''}{window.handedBackByName ? ` by ${window.handedBackByName}` : ''}.{window.handbackNotes ? ` ${window.handbackNotes}` : ''}</p>
      ) : null}

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
        <section className="rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-4 py-3">
            <h2 className="text-sm font-semibold text-slate-800">Jobs · {jobs.length}</h2>
            {editable && canPlan ? <button type="button" className={secondaryButton} onClick={() => setDrawer('add')}><Plus className="h-4 w-4" />Add a job</button> : null}
          </header>
          {window.status !== 'Skipped' ? <div className="border-b border-slate-100 px-4 py-3"><CapacityBar planned={window.plannedHours} capacity={window.capacityHours} /></div> : null}
          {jobs.length === 0 ? <p className="p-4 text-sm text-slate-500">No jobs yet. Jobs that need the line stopped are added automatically once approved.</p> : (
            <ul className="divide-y divide-slate-100">
              {jobs.map((job) => (
                <li key={job.workOrderId} className="flex flex-wrap items-start gap-3 px-4 py-3">
                  <div className="min-w-0 flex-1">
                    <Link to={links.job(job.workOrderId)} className="font-medium text-slate-900 underline decoration-slate-300 underline-offset-4">{job.title}</Link>
                    <p className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-500">
                      <span className="font-mono">{job.workOrderNumber}</span>
                      {job.assetCode ? <span>{job.assetCode} · {job.assetName}</span> : null}
                      <span>{job.priority}</span>
                      {job.estimatedHours ? <span>{formatNumber(job.estimatedHours)} h</span> : null}
                      {job.isPm ? <Badge size="sm">PM</Badge> : null}
                      {job.carryoverCount > 0 ? <Badge size="sm" tone={job.carryoverCount >= 2 ? 'danger' : 'warning'}><Repeat className="h-3 w-3" />Carried over {job.carryoverCount}×</Badge> : null}
                    </p>
                    <p className="mt-0.5 text-xs text-slate-500">{job.team.length ? job.team.join(', ') : 'No team yet'}</p>
                  </div>
                  <div className="flex items-center gap-2">
                    {job.outcome ? <Badge size="sm" tone={job.outcome === 'Done' ? 'success' : job.outcome === 'CarriedOver' ? 'warning' : 'neutral'}>{job.outcome === 'CarriedOver' ? 'Carried over' : job.outcome}</Badge> : <JobStatusBadge status={job.status} />}
                    {editable && canPlan ? (
                      <button type="button" aria-label={`Take ${job.workOrderNumber} out of the window`} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                        onClick={() => { if (globalThis.confirm(`Take ${job.workOrderNumber} out of this window? It will no longer be marked as needing a line stop.`)) void run(`/jobs/${job.workOrderId}`, 'Taken out', 'delete') }}>
                        <X className="h-4 w-4" />
                      </button>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="h-fit rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          <header className="border-b border-slate-100 px-4 py-3">
            <h2 className="text-sm font-semibold text-slate-800">Start-up checks · {ticked}/{handbackItems.length}</h2>
            <p className="text-xs text-slate-500">Before the line goes back to production.</p>
          </header>
          <ul className="divide-y divide-slate-100">
            {handbackItems.map((item) => {
              const canTick = canRun && window.status === 'InProgress'
              return (
                <li key={item.id}>
                  <button type="button" disabled={!canTick} className="flex w-full items-start gap-3 px-4 py-2.5 text-left text-sm hover:bg-slate-50 disabled:cursor-default disabled:hover:bg-transparent"
                    onClick={() => void run(`/handback-items/${item.id}`, item.done ? 'Unticked' : 'Ticked', 'post', { done: !item.done, note: item.note })}>
                    {item.done ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" /> : <Circle className="mt-0.5 h-5 w-5 shrink-0 text-slate-300" />}
                    <span>{item.prompt}{item.doneByName ? <span className="block text-xs text-slate-500">{item.doneByName}{item.doneAt ? `, ${formatTime(item.doneAt)}` : ''}</span> : null}</span>
                  </button>
                </li>
              )
            })}
          </ul>
          {window.status === 'Planned' || window.status === 'Packed' ? <p className="px-4 py-3 text-xs text-slate-500">Ticked on the day, once the window has started.</p> : null}
        </section>
      </div>

      <AddJobDrawer open={drawer === 'add'} windowId={id} onClose={() => setDrawer(null)} />
      <EditWindowDrawer open={drawer === 'edit'} detail={data} onClose={() => setDrawer(null)} />
      <SkipDrawer open={drawer === 'skip'} windowId={id} onClose={() => setDrawer(null)} />
      <HandbackDrawer open={drawer === 'handback'} detail={data} onClose={() => setDrawer(null)} />
    </div>
  )
}

function AddJobDrawer({ open, windowId, onClose }: { open: boolean; windowId: string; onClose: () => void }) {
  const [search, setSearch] = useState('')
  const debounced = useDebounce(search, 300)
  const candidates = useWindowCandidates(windowId, debounced, open)
  const action = useWindowAction(windowId)
  return (
    <FormDrawer open={open} title="Add a job to the window" onClose={onClose}>
      <div className="space-y-3">
        <label className="flex h-10 items-center gap-2 rounded-lg border border-slate-200 bg-white px-3">
          <Search className="h-4 w-4 text-slate-400" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search open jobs in this factory" className="w-full border-0 bg-transparent text-sm outline-none" />
        </label>
        {candidates.isLoading ? <Spinner /> : (
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {(candidates.data ?? []).length === 0 ? <li className="p-3 text-sm text-slate-500">No open jobs to add.</li> : (candidates.data ?? []).map((job) => (
              <li key={job.workOrderId} className="flex items-center gap-3 p-3 text-sm">
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-slate-900">{job.title}</p>
                  <p className="text-xs text-slate-500"><span className="font-mono">{job.workOrderNumber}</span>{job.assetCode ? ` · ${job.assetCode}` : ''} · {job.priority}{job.estimatedHours ? ` · ${job.estimatedHours} h` : ''}{job.dueDate ? ` · due ${formatDate(job.dueDate)}` : ''}</p>
                </div>
                <button type="button" className={secondaryButton} disabled={action.isPending}
                  onClick={() => void action.mutateAsync({ path: '/jobs', body: { workOrderId: job.workOrderId } }).then(() => toast.success(`${job.workOrderNumber} added`)).catch(() => undefined)}>Add</button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </FormDrawer>
  )
}

function EditWindowDrawer({ open, detail, onClose }: { open: boolean; detail: WindowDetail; onClose: () => void }) {
  const action = useWindowAction(detail.window.id)
  const [form, setForm] = useState({ date: '', start: '', end: '', capacity: '', notes: '' })
  useEffect(() => {
    if (open) {
      const w = detail.window
      setForm({ date: w.windowDate.slice(0, 10), start: formatTime(w.startsAt), end: formatTime(w.endsAt), capacity: w.capacitySet ? String(w.capacityHours) : '', notes: w.notes ?? '' })
    }
  }, [open, detail.window])
  const submit = async () => {
    try {
      await action.mutateAsync({ method: 'put', path: '', body: { date: form.date, start: form.start, end: form.end, capacityHours: form.capacity ? Number(form.capacity) : null, notes: form.notes || null } })
      toast.success('Window saved')
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title="Move or edit the window" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Cancel</button><button type="button" className={primaryButton} disabled={action.isPending || !form.date} onClick={() => void submit()}>Save</button></div>
    }>
      <div className="grid gap-4 sm:grid-cols-3">
        <Field label="Date" className="sm:col-span-3"><input type="date" className={inputClass} value={form.date} onChange={(event) => setForm({ ...form, date: event.target.value })} /></Field>
        <Field label="From"><input type="time" className={inputClass} value={form.start} onChange={(event) => setForm({ ...form, start: event.target.value })} /></Field>
        <Field label="To"><input type="time" className={inputClass} value={form.end} onChange={(event) => setForm({ ...form, end: event.target.value })} /></Field>
        <Field label="Hours available" hint="Blank: length × technicians."><input type="number" min={0} className={inputClass} value={form.capacity} onChange={(event) => setForm({ ...form, capacity: event.target.value })} /></Field>
        <Field label="Notes" className="sm:col-span-3"><textarea className={textareaClass} value={form.notes} onChange={(event) => setForm({ ...form, notes: event.target.value })} /></Field>
      </div>
    </FormDrawer>
  )
}

function SkipDrawer({ open, windowId, onClose }: { open: boolean; windowId: string; onClose: () => void }) {
  const action = useWindowAction(windowId)
  const [reason, setReason] = useState('')
  return (
    <FormDrawer open={open} title="Skip this week's window" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Back</button>
        <button type="button" className={primaryButton} disabled={!reason.trim() || action.isPending}
          onClick={() => void action.mutateAsync({ path: '/skip', body: { reason } }).then(() => { toast.success('Window skipped'); onClose() }).catch(() => undefined)}>Skip</button></div>
    }>
      <div className="space-y-3">
        <p className="text-sm text-slate-600">The jobs move to the next window. This is not counted against them as a carryover.</p>
        <Field label="Reason" required><textarea className={textareaClass} value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Peak crop: factory running seven days" /></Field>
      </div>
    </FormDrawer>
  )
}

function HandbackDrawer({ open, detail, onClose }: { open: boolean; detail: WindowDetail; onClose: () => void }) {
  const action = useWindowAction(detail.window.id)
  const [notes, setNotes] = useState('')
  const [signature, setSignature] = useState<string | null>(null)
  const unfinished = detail.jobs.filter((x) => !['Completed', 'Verified', 'Closed', 'Cancelled', 'Rejected'].includes(x.status))
  const submit = async () => {
    try {
      const result = await action.mutateAsync({ path: '/handback', body: { notes: notes || null, signatureDataUrl: signature } }) as { result: { done: number; carriedOver: number; toOverhaul: number } }
      toast.success(`Handed back: ${result.result.done} done, ${result.result.carriedOver} carried over${result.result.toOverhaul ? `, ${result.result.toOverhaul} to the overhaul list` : ''}`)
      onClose()
    } catch {
      // The API client shows the error.
    }
  }
  return (
    <FormDrawer open={open} title="Hand back to production" onClose={onClose} footer={
      <div className="flex justify-end gap-2"><button type="button" className={secondaryButton} onClick={onClose}>Back</button><button type="button" className={primaryButton} disabled={action.isPending} onClick={() => void submit()}>Hand back</button></div>
    }>
      <div className="space-y-4 text-sm">
        {unfinished.length > 0 ? (
          <div className="rounded-lg bg-amber-50 p-3 text-amber-900">
            <p className="font-medium">{unfinished.length} job{unfinished.length === 1 ? '' : 's'} not finished will carry over to the next window:</p>
            <ul className="mt-1 list-disc pl-5">{unfinished.map((x) => <li key={x.workOrderId}>{x.workOrderNumber} {x.title}{x.carryoverCount ? ` (already ${x.carryoverCount}×)` : ''}</li>)}</ul>
          </div>
        ) : <p className="rounded-lg bg-emerald-50 p-3 text-emerald-900">All jobs are finished.</p>}
        <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(event) => setNotes(event.target.value)} placeholder="Line running at 17:40; dryer temperature normal" /></Field>
        <Field label="Signature" hint="Production supervisor accepting the line."><SignaturePad onChange={setSignature} className="h-32 rounded-lg border border-slate-200" /></Field>
      </div>
    </FormDrawer>
  )
}

/** The printable job pack (also kept on the phone for the day). */
export function WindowPackPage() {
  const { id = '' } = useParams()
  const links = useFactoryLinks()
  const pack = useWindowPack(id)
  if (pack.isLoading) return <Spinner label="Loading pack" />
  if (!pack.data) return <ErrorState title="Pack unavailable" description="Open it once with signal to keep it on this phone." />
  const { window, jobs, handbackItems } = pack.data
  return (
    <div className="mx-auto max-w-4xl space-y-4 print:max-w-none">
      <div className="flex items-center justify-between gap-2 print:hidden">
        <Link to={links.window(id)} className="text-sm underline">Back to the window</Link>
        <button type="button" className={primaryButton} onClick={() => globalThis.print()}><Printer className="h-4 w-4" />Print</button>
      </div>
      <header className="border-b border-slate-300 pb-3">
        <h1 className="text-xl font-semibold text-slate-900">Weekly window job pack · {window.factoryName}</h1>
        <p className="text-sm text-slate-600">{formatDate(window.windowDate, 'EEEE dd MMM yyyy')}, {formatTime(window.startsAt)}–{formatTime(window.endsAt)} · {jobs.length} jobs · {formatNumber(window.plannedHours)} h of {formatNumber(window.capacityHours)} h · printed {formatDateTime(pack.data.generatedAt)}</p>
      </header>
      {jobs.map(({ job, description, location, steps, resources, isolation, spares, shortages }, index) => (
        <article key={job.workOrderId} className={cn('rounded-lg border border-slate-200 bg-white p-4 print:break-inside-avoid print:rounded-none print:border-0 print:border-b print:border-slate-300')}>
          <h2 className="font-semibold text-slate-900">{index + 1}. {job.title}</h2>
          <p className="text-xs text-slate-600"><span className="font-mono">{job.workOrderNumber}</span>{job.assetCode ? ` · ${job.assetCode} ${job.assetName}` : ''}{location ? ` · ${location}` : ''} · {job.priority}{job.estimatedHours ? ` · ${formatNumber(job.estimatedHours)} h` : ''}</p>
          <p className="mt-1 text-sm"><span className="font-medium">Team:</span> {job.team.length ? job.team.join(', ') : '________________'}</p>
          <p className="mt-1 text-sm font-medium text-rose-800">{isolation}</p>
          {description ? <p className="mt-1 whitespace-pre-wrap text-sm text-slate-700">{description}</p> : null}
          {resources.filter((r) => r.kind !== 'Part').length > 0 ? <p className="mt-1 text-sm"><span className="font-medium">Tools:</span> {resources.filter((r) => r.kind !== 'Part').map((r) => `${r.description}${r.quantity !== 1 ? ` ×${r.quantity}` : ''}`).join('; ')}</p> : null}
          {spares.length > 0 ? (
            <div className="mt-1 text-sm"><span className="font-medium">Spares from stores:</span>
              <ul className="ml-4 list-disc">{spares.map((s) => <li key={s.reservationId}>{s.itemName} ×{s.quantity - s.issuedQuantity || s.quantity} {s.unitOfMeasure} · {s.status === 'Kitted' ? 'kitted' : s.status === 'Issued' ? 'issued' : 'reserved'} · collect from {s.location}</li>)}</ul>
            </div>
          ) : resources.some((r) => r.kind === 'Part') ? <p className="mt-1 text-sm"><span className="font-medium">Spares (not reserved):</span> {resources.filter((r) => r.kind === 'Part').map((r) => `${r.description}${r.quantity !== 1 ? ` ×${r.quantity}` : ''}`).join('; ')}</p> : null}
          {shortages.length > 0 ? <p className="mt-1 text-sm font-medium text-rose-800">Short in stores: {shortages.map((s) => `${s.description} ×${s.short}`).join('; ')}</p> : null}
          {steps.length > 0 ? (
            <ol className="mt-2 space-y-0.5 text-sm">
              {steps.map((step) => <li key={step.id} className="flex gap-2"><span aria-hidden>{step.done ? '☑' : '☐'}</span><span>{step.section && steps.some((x) => x.section !== step.section) ? <span className="text-slate-500">{step.section}: </span> : null}{step.instruction}{step.type !== 'Task' && step.type !== 'checkbox' ? ` ______ ${step.unit ?? ''}` : ''}</span></li>)}
            </ol>
          ) : null}
        </article>
      ))}
      <section className="rounded-lg border border-slate-200 bg-white p-4 print:break-inside-avoid print:border-0">
        <h2 className="font-semibold text-slate-900">Start-up checks before hand-back</h2>
        <ul className="mt-2 space-y-1 text-sm">{handbackItems.map((item) => <li key={item.id}>{item.done ? '☑' : '☐'} {item.prompt}</li>)}</ul>
        <p className="mt-4 text-sm">Accepted by production: ____________________ Time: ________</p>
      </section>
    </div>
  )
}
