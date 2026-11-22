import { useEffect, useState } from 'react'
import toast from 'react-hot-toast'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ChevronLeft, MoreHorizontal, X } from 'lucide-react'
import { Spinner } from '../../shared/components/Spinner'
import { cn } from '../../shared/utils/cn'
import { useMyTechnician } from './api'
import { savedMessage, useTechActions } from './offline/actions'
import { useJobHasQueuedActions, useTechBundle, useTechJobView } from './offline/hooks'
import { jobPhase, myAssignmentOf, PHASE_LABEL, PHASE_TONE } from './jobState'
import { TechFinish } from './TechFinish'
import { TechMaterials } from './TechMaterials'
import { TechOverview } from './TechOverview'
import { TechParts } from './TechParts'
import { TechPreviousVisit } from './TechPreviousVisit'
import { TechPhotos } from './TechPhotos'

type Section = 'overview' | 'photos' | 'parts' | 'complete'
const HOLD_TYPES = [
  { value: 'WaitingOnParts', label: 'Waiting on parts' },
  { value: 'OnHold', label: 'Other' },
] as const

export function TechJobPage() {
  const { id = '', section: rawSection } = useParams<{ id: string; section?: string }>()
  const section: Section = rawSection === 'photos' || rawSection === 'parts' || rawSection === 'complete' ? rawSection : 'overview'
  const navigate = useNavigate()
  const { data: job, isLoading, isError } = useTechJobView(id)
  const { data: me } = useMyTechnician()
  const { data: bundle } = useTechBundle(id)
  const actions = useTechActions()
  const hasQueued = useJobHasQueuedActions(job)

  const [menuOpen, setMenuOpen] = useState(false)
  const [holdOpen, setHoldOpen] = useState(false)
  const [holdType, setHoldType] = useState<(typeof HOLD_TYPES)[number]['value']>('WaitingOnParts')
  const [holdReason, setHoldReason] = useState('')
  const [workDoneDraft, setWorkDoneDraft] = useState<string | null>(null)
  const [findingsDraft, setFindingsDraft] = useState<string | null>(null)
  const [acting, setActing] = useState(false)
  const workDone = workDoneDraft ?? bundle?.notes.workDone ?? job?.workDoneNotes ?? ''
  const savedFindings = bundle?.notes.findings ?? ''
  const findings = findingsDraft ?? savedFindings

  const sheetOpen = menuOpen || holdOpen
  const closeSheet = () => {
    setMenuOpen(false)
    setHoldOpen(false)
  }
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

  if (isLoading && !job) {
    return navigator.onLine
      ? <Spinner label="Loading job" />
      : <p className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">You're offline and this job hasn't been saved on this phone yet. Open it once with a signal to keep it for offline use.</p>
  }
  if ((isError && !job) || !job) {
    return (
      <div className="space-y-3">
        <Link to="/tech" className="inline-flex items-center gap-1 text-sm text-slate-600"><ChevronLeft className="h-4 w-4" />My Jobs</Link>
        <p className="rounded-xl border border-rose-200 bg-rose-50 p-4 text-sm text-rose-700">This job could not be loaded. It may have been reassigned.</p>
      </div>
    )
  }

  const mine = myAssignmentOf(job, me?.technicianId)
  const phase = jobPhase(job, mine)
  const busy = acting || actions.busy
  const editable = !['accept', 'declined', 'done'].includes(phase)

  const run = async (success: string, action: () => Promise<{ queued: boolean }>) => {
    setActing(true)
    try {
      toast.success(savedMessage(success, await action()))
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
    if (!findings.trim()) {
      toast.error('Add your findings first')
      document.getElementById('tech-findings')?.focus()
      return
    }
    if (!workDone.trim()) {
      toast.error('Add the work done notes first')
      document.getElementById('tech-work-done')?.focus()
      return
    }
    // Findings are saved first so they are on the job, and its report, when it completes.
    const completed = await run('Job updated', async () => {
      if (findings.trim() !== savedFindings.trim()) await actions.saveNotes(job, { findings: findings.trim() })
      return actions.complete(job, workDone.trim())
    })
    if (completed) {
      setWorkDoneDraft(null)
      setFindingsDraft(null)
    }
  }

  const primary: { label: string; onClick: () => void } | null = (() => {
    switch (phase) {
      case 'accept':
        return mine ? { label: 'Accept Job', onClick: () => void run('Job accepted', () => actions.respond(job, mine.technicianId, 'Accepted')) } : null
      case 'start':
        return { label: 'Start Work', onClick: () => void run('Job updated', () => actions.start(job)) }
      case 'work':
        return { label: 'Complete', onClick: () => void finish() }
      case 'on-hold':
        return { label: 'Resume', onClick: () => void run('Job updated', () => actions.resume(job)) }
      default:
        return null
    }
  })()

  const moreActions = [
    ...(phase === 'accept' && mine ? [{ label: 'Decline Job', danger: true, onClick: () => void run('Job declined', () => actions.respond(job, mine.technicianId, 'Declined')) }] : []),
    ...(['start', 'work'].includes(phase) ? [{ label: 'Put On Hold', onClick: () => setHoldOpen(true) }] : []),
  ]

  const submitHold = async () => {
    if (await run('Job updated', () => actions.hold(job, holdType, holdReason.trim() || null))) {
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
        {hasQueued ? <p className="text-xs font-medium text-sky-700" data-testid="job-waiting-to-sync">Changes on this job are saved on this phone and waiting to sync.</p> : null}
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
        </>
      ) : null}
      {section === 'photos' ? <TechPhotos job={job} editable={editable} /> : null}
      {section === 'parts' ? <TechParts workOrderId={id} editable={editable} /> : null}
      {section === 'complete' ? (
        <>
          <TechMaterials job={job} editable={editable} />
          <TechFinish workOrderId={id} workOrderNumber={job.workOrderNumber} editable={editable}
            reportedProblem={job.description?.trim() || job.title}
            findings={findings}
            onFindingsChange={setFindingsDraft}
            workDone={workDone}
            onWorkDoneChange={setWorkDoneDraft}
          />
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

      {sheetOpen ? (
        <div className="fixed inset-0 z-40 flex items-end bg-slate-900/40" onClick={closeSheet}>
          <div className="mx-auto w-full max-w-xl space-y-2 rounded-t-2xl bg-white p-4 pb-[max(env(safe-area-inset-bottom),1rem)]" onClick={(event) => event.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-semibold text-slate-900">{holdOpen ? 'Put job on hold' : 'More actions'}</h2>
              <button type="button" aria-label="Close" onClick={closeSheet} className="rounded-lg p-1.5 text-slate-500"><X className="h-4 w-4" /></button>
            </div>
            {holdOpen ? (
              <>
                <div className="grid grid-cols-2 gap-1 rounded-lg border border-slate-200 p-1" role="radiogroup" aria-label="Hold reason">
                  {HOLD_TYPES.map((option) => (
                    <button key={option.value} type="button" role="radio" aria-checked={holdType === option.value} onClick={() => setHoldType(option.value)} className={cn('h-9 rounded-md text-xs font-medium', holdType === option.value ? 'bg-[var(--app-primary)] text-white' : 'text-slate-600')}>
                      {option.label}
                    </button>
                  ))}
                </div>
                <textarea value={holdReason} onChange={(event) => setHoldReason(event.target.value)} rows={3} placeholder="What are you waiting for?" aria-label="Hold details" className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]" />
                <button type="button" onClick={() => void submitHold()} disabled={busy} className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-slate-900 text-sm font-semibold text-white disabled:opacity-60">
                  {busy ? 'Saving…' : 'Confirm hold'}
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
