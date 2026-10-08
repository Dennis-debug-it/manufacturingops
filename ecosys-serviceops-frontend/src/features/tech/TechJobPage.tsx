import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ChevronLeft, MoreHorizontal, X } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import {
  useCompleteWorkOrder,
  useExecutionBundle,
  useHoldWorkOrder,
  useRecordTechnicianArrival,
  useRecordTechnicianDeparture,
  useRecordTechnicianResponse,
  useResumeFromHold,
  useStartWorkOrder,
} from '../work-orders/api'
import { useMyTechnician, useTechJob } from './api'
import { jobPhase, myAssignmentOf, PHASE_LABEL, PHASE_TONE } from './jobState'
import { TechFinish } from './TechFinish'
import { TechJobGuides } from './TechGuides'
import { TechMaterials } from './TechMaterials'
import { TechOverview } from './TechOverview'
import { TechParts } from './TechParts'
import { TechPreviousVisit } from './TechPreviousVisit'
import { TechPhotos } from './TechPhotos'

type Section = 'overview' | 'photos' | 'parts' | 'complete'
const HOLD_TYPES = [
  { value: 'WaitingOnParts', label: 'Waiting on parts' },
  { value: 'WaitingOnClient', label: 'Waiting on client' },
  { value: 'OnHold', label: 'Other' },
] as const

/**
 * Best-effort location for arrival/departure; never blocks the action. The browser's own timeout
 * only starts once the permission prompt is answered, so an ignored prompt would hang forever —
 * hence the separate cap.
 */
function currentPosition(): Promise<{ latitude: number | null; longitude: number | null }> {
  const none = { latitude: null, longitude: null }
  return new Promise((resolve) => {
    if (!('geolocation' in navigator)) return resolve(none)
    const cap = window.setTimeout(() => resolve(none), 6000)
    navigator.geolocation.getCurrentPosition(
      (position) => { window.clearTimeout(cap); resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude }) },
      () => { window.clearTimeout(cap); resolve(none) },
      { timeout: 5000, maximumAge: 60_000 },
    )
  })
}

export function TechJobPage() {
  const { id = '', section: rawSection } = useParams<{ id: string; section?: string }>()
  const section: Section = rawSection === 'photos' || rawSection === 'parts' || rawSection === 'complete' ? rawSection : 'overview'
  const navigate = useNavigate()
  const { data: job, isLoading, isError } = useTechJob(id)
  const { data: me } = useMyTechnician()
  const { data: bundle } = useExecutionBundle(id)

  const respond = useRecordTechnicianResponse()
  const arrive = useRecordTechnicianArrival()
  const depart = useRecordTechnicianDeparture()
  const start = useStartWorkOrder(id)
  const hold = useHoldWorkOrder(id)
  const resume = useResumeFromHold(id)
  const complete = useCompleteWorkOrder(id)

  const [menuOpen, setMenuOpen] = useState(false)
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdType, setHoldType] = useState<(typeof HOLD_TYPES)[number]['value']>('WaitingOnParts')
  const [holdReason, setHoldReason] = useState('')
  const [workDoneDraft, setWorkDoneDraft] = useState<string | null>(null)
  // Covers the whole action, including the location lookup that runs before the request.
  const [acting, setActing] = useState(false)
  const workDone = workDoneDraft ?? bundle?.notes.workDone ?? job?.workDoneNotes ?? ''

  const sheetOpen = menuOpen || holdOpen
  useEffect(() => {
    if (!sheetOpen) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false)
        setHoldOpen(false)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [sheetOpen])

  if (isLoading) return <Spinner label="Loading job" />
  if (isError || !job) {
    return (
      <div className="space-y-3">
        <Link to="/tech" className="inline-flex items-center gap-1 text-sm text-slate-600"><ChevronLeft className="h-4 w-4" />My Jobs</Link>
        <p className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">This job could not be loaded. It may have been reassigned.</p>
      </div>
    )
  }

  const mine = myAssignmentOf(job, me?.technicianId)
  const phase = jobPhase(job, mine)
  const busy = acting || respond.isPending || arrive.isPending || depart.isPending || start.isPending || hold.isPending || resume.isPending || complete.isPending
  const editable = !['accept', 'declined', 'done'].includes(phase)

  const run = async (success: string, action: () => Promise<unknown>) => {
    setActing(true)
    try {
      await action()
      toast.success(success)
      return true
    } catch {
      // interceptor surfaces the error toast
      return false
    } finally {
      setActing(false)
    }
  }

  const finish = async () => {
    if (section !== 'complete') {
      navigate(`/tech/jobs/${id}/complete`)
      return
    }
    if (!workDone.trim()) {
      toast.error('Add the work done notes first')
      document.getElementById('tech-work-done')?.focus()
      return
    }
    if (await run('Job updated', () => complete.mutateAsync({ workDoneNotes: workDone.trim() }))) {
      setWorkDoneDraft(null)
    }
  }

  const primary: { label: string; onClick: () => void } | null = (() => {
    switch (phase) {
      case 'accept':
        return mine ? { label: 'Accept Job', onClick: () => void run('Job accepted', () => respond.mutateAsync({ id, technicianId: mine.technicianId, response: 'Accepted' })) } : null
      case 'arrive':
        return mine ? { label: "I've Arrived", onClick: () => void run('Arrival recorded', async () => arrive.mutateAsync({ id, technicianId: mine.technicianId, ...(await currentPosition()) })) } : null
      case 'start':
        return { label: 'Start Work', onClick: () => void run('Job updated', () => start.mutateAsync()) }
      case 'work':
        return { label: 'Complete', onClick: () => void finish() }
      case 'on-hold':
        return { label: 'Resume', onClick: () => void run('Job updated', () => resume.mutateAsync()) }
      default:
        return null
    }
  })()

  const moreActions = [
    ...(phase === 'accept' && mine ? [{ label: 'Decline Job', danger: true, onClick: () => void run('Job declined', () => respond.mutateAsync({ id, technicianId: mine.technicianId, response: 'Declined' })) }] : []),
    ...(['arrive', 'start', 'work'].includes(phase) ? [{ label: 'Put On Hold', onClick: () => setHoldOpen(true) }] : []),
    ...(mine?.arrivalAt && !mine.departureAt && phase !== 'done' ? [{ label: 'Record Departure', onClick: () => void run('Departure recorded', async () => depart.mutateAsync({ id, technicianId: mine.technicianId, ...(await currentPosition()) })) }] : []),
  ]

  const submitHold = async () => {
    if (await run('Job updated', () => hold.mutateAsync({ holdType, reason: holdReason.trim() || null }))) {
      setHoldOpen(false)
      setHoldReason('')
    }
  }

  const tabs: { id: Section; label: string; to: string }[] = [
    { id: 'overview', label: 'Overview', to: `/tech/jobs/${id}` },
    { id: 'photos', label: `Photos${bundle?.photos.length ? ` (${bundle.photos.length})` : ''}`, to: `/tech/jobs/${id}/photos` },
    { id: 'parts', label: 'Parts', to: `/tech/jobs/${id}/parts` },
    { id: 'complete', label: 'Finish', to: `/tech/jobs/${id}/complete` },
  ]

  return (
    <div className="space-y-4" data-testid="tech-job-page">
      <div className="space-y-1">
        <Link to="/tech" className="inline-flex items-center gap-1 text-sm text-slate-600"><ChevronLeft className="h-4 w-4" />My Jobs</Link>
        <p className="font-mono text-xs text-slate-500">{job.workOrderNumber} · {job.priority} priority</p>
        <h1 className="text-lg font-semibold leading-snug text-slate-900">{job.title}</h1>
        <span className={cn('inline-block rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset', PHASE_TONE[phase])}>{PHASE_LABEL[phase]}</span>
        {phase === 'on-hold' && (job.holdReason || job.holdNote) ? <p className="text-sm text-orange-700">{job.status}: {job.holdReason ?? job.holdNote}</p> : null}
        {phase === 'done' && job.status === 'Completed' ? <p className="text-sm text-slate-600">Completed. An administrator will review and close it.</p> : null}
      </div>

      <nav className="grid grid-cols-4 gap-1 rounded-lg border border-slate-200 bg-white p-1">
        {tabs.map((tab) => (
          <Link key={tab.id} to={tab.to} replace className={cn('flex h-9 items-center justify-center rounded-md text-sm font-medium', section === tab.id ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}>
            {tab.label}
          </Link>
        ))}
      </nav>

      {section === 'overview' ? (
        <>
          <TechPreviousVisit job={job} />
          <TechOverview job={job} editable={editable} />
          <TechJobGuides workOrderId={id} />
        </>
      ) : null}
      {section === 'photos' ? <TechPhotos workOrderId={id} editable={editable} /> : null}
      {section === 'parts' ? <TechParts workOrderId={id} editable={editable} /> : null}
      {section === 'complete' ? (
        <>
          <TechMaterials job={job} editable={editable} />
          <TechFinish workOrderId={id} workOrderNumber={job.workOrderNumber} editable={editable} workDone={workDone} onWorkDoneChange={setWorkDoneDraft} />
        </>
      ) : null}

      {primary || moreActions.length > 0 ? (
        <div data-testid="tech-bottom-bar" className="fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white/95 px-4 pb-[max(env(safe-area-inset-bottom),0.75rem)] pt-3 backdrop-blur">
          <div className="mx-auto flex max-w-xl gap-2">
            {primary ? (
              <button type="button" onClick={primary.onClick} disabled={busy} className="inline-flex h-12 flex-1 items-center justify-center rounded-lg bg-[var(--app-primary)] text-base font-semibold text-white disabled:opacity-60">
                {busy ? 'Working…' : primary.label}
              </button>
            ) : <span className="flex-1" />}
            {moreActions.length > 0 ? (
              <button type="button" aria-label="Open more actions" onClick={() => setMenuOpen(true)} className="inline-flex h-12 w-12 items-center justify-center rounded-lg border border-slate-300 bg-white text-slate-700">
                <MoreHorizontal className="h-5 w-5" />
              </button>
            ) : null}
          </div>
        </div>
      ) : null}

      {menuOpen || holdOpen ? (
        <div className="fixed inset-0 z-40 flex items-end bg-slate-900/40" onClick={() => { setMenuOpen(false); setHoldOpen(false) }}>
          <div className="mx-auto w-full max-w-xl space-y-2 rounded-t-2xl bg-white p-4 pb-[max(env(safe-area-inset-bottom),1rem)]" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-900">{holdOpen ? 'Put job on hold' : 'More actions'}</h2>
              <button type="button" aria-label="Close" onClick={() => { setMenuOpen(false); setHoldOpen(false) }} className="rounded-lg p-1.5 text-slate-500"><X className="h-4 w-4" /></button>
            </div>
            {holdOpen ? (
              <>
                <div className="grid grid-cols-3 gap-1 rounded-lg border border-slate-200 p-1" role="radiogroup" aria-label="Hold reason">
                  {HOLD_TYPES.map((option) => (
                    <button key={option.value} type="button" role="radio" aria-checked={holdType === option.value} onClick={() => setHoldType(option.value)} className={cn('h-9 rounded-md text-xs font-medium', holdType === option.value ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}>
                      {option.label}
                    </button>
                  ))}
                </div>
                <textarea value={holdReason} onChange={(event) => setHoldReason(event.target.value)} rows={3} placeholder="What are you waiting for?" aria-label="Hold details" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                <button type="button" onClick={() => void submitHold()} disabled={hold.isPending} className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-slate-900 text-sm font-semibold text-white disabled:opacity-60">
                  {hold.isPending ? 'Saving…' : 'Confirm hold'}
                </button>
              </>
            ) : (
              moreActions.map((action) => (
                <button
                  key={action.label}
                  type="button"
                  onClick={() => { setMenuOpen(false); action.onClick() }}
                  className={cn('flex h-12 w-full items-center rounded-lg border border-slate-200 px-4 text-left text-sm font-medium', 'danger' in action && action.danger ? 'text-rose-600' : 'text-slate-800')}
                >
                  {action.label}
                </button>
              ))
            )}
          </div>
        </div>
      ) : null}
    </div>
  )
}
