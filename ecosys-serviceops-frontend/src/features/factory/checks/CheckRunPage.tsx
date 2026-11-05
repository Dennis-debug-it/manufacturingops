import { useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import toast from 'react-hot-toast'
import { AlertTriangle, CheckCircle2 } from 'lucide-react'
import { PageHeader } from '../../../shared/components/PageHeader'
import { Spinner } from '../../../shared/components/Spinner'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Badge } from '../../../shared/components/Badge'
import { SignaturePad } from '../../../shared/components/SignaturePad'
import { formatDateTime, formatTime } from '../../../shared/utils/dates'
import { cn } from '../../../shared/utils/cn'
import { Field, inputClass, primaryButton, textareaClass } from '../components/ui'
import { useFactoryLinks } from '../links'
import { useCheckRun, useSubmitCheck, type CheckQuestion } from './api'

const PASS_FAIL = ['passfail', 'yesno', 'boolean']
const NUMBER = ['number', 'reading']

interface Answer { value: string; note: string }

/** Whether an answer passes, as the server will score it (null: nothing to judge). */
export function scoreAnswer(question: Pick<CheckQuestion, 'type' | 'minValue' | 'maxValue'>, value: string): boolean | null {
  const type = question.type.toLowerCase()
  const v = value.trim().toLowerCase()
  if (!v) return null
  if (PASS_FAIL.includes(type) || type === 'checkbox' || type === 'condition') {
    if (['pass', 'yes', 'true', 'ok', 'good'].includes(v)) return true
    if (['fail', 'no', 'false', 'poor', 'bad', 'failed', 'not ok'].includes(v)) return false
    return null
  }
  if (NUMBER.includes(type)) {
    const n = Number(v)
    if (!Number.isFinite(n) || (question.minValue == null && question.maxValue == null)) return null
    return (question.minValue == null || n >= question.minValue) && (question.maxValue == null || n <= question.maxValue)
  }
  return null
}

const FAIL_ACTION_HINT: Record<string, string> = {
  WorkOrder: 'A fail raises a job straight away.',
  WindowDefect: 'A fail is logged for the weekly window.',
}

/** One machine's running check. Works offline: it is saved on the phone and sent when there is signal. */
export function CheckRunPage() {
  const { id } = useParams<{ id: string }>()
  const links = useFactoryLinks()
  const navigate = useNavigate()
  const { data, isLoading, isError } = useCheckRun(id)
  const submit = useSubmitCheck(data?.run)
  const [answers, setAnswers] = useState<Record<string, Answer>>({})
  const [notes, setNotes] = useState('')
  const [signature, setSignature] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const sections = useMemo(() => {
    const map = new Map<string, CheckQuestion[]>()
    for (const q of data?.questions ?? []) map.set(q.sectionName ?? '', [...(map.get(q.sectionName ?? '') ?? []), q])
    return [...map.entries()]
  }, [data?.questions])

  if (isLoading) return <Spinner label="Loading check" />
  if (isError || !data) return <EmptyState title="Check unavailable" description="It may have been removed, or it isn't in your area." />

  const { run, questions } = data
  const done = run.status === 'Completed'
  const get = (q: CheckQuestion) => answers[q.id] ?? { value: '', note: '' }
  const set = (q: CheckQuestion, patch: Partial<Answer>) => setAnswers((current) => ({ ...current, [q.id]: { ...get(q), ...patch } }))

  const problems = questions.filter((q) => {
    const answer = get(q)
    if (q.required && !answer.value.trim()) return true
    if (q.noteRequiredOnFail && scoreAnswer(q, answer.value) === false && !answer.note.trim()) return true
    return false
  })
  const failing = questions.filter((q) => scoreAnswer(q, get(q).value) === false).length

  const send = async () => {
    setBusy(true)
    try {
      const result = await submit({
        answers: questions.map((q) => ({ questionId: q.id, value: get(q).value.trim() || null, note: get(q).note.trim() || null })),
        completedAt: new Date().toISOString(),
        notes: notes.trim() || null,
        signatureDataUrl: signature,
      })
      if (result.queued) toast.success('Check saved. It will sync when online.')
      else {
        const raised = result.data?.raisedWorkOrders ?? []
        toast.success(raised.length ? `Check done. Raised ${raised.join(', ')}.` : 'Check done')
        for (const warning of result.data?.warnings ?? []) toast(warning, { icon: '⚠️' })
      }
      navigate(links.checks())
    } catch {
      // The API client shows the error.
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4 pb-24">
      <PageHeader
        title={run.assetName}
        subtitle={`${run.scheduleName}${run.shiftName ? ` · ${run.shiftName} shift` : ''} · due by ${formatTime(run.dueBy)}`}
        breadcrumbs={[{ label: 'Running checks', to: links.checks() }, { label: run.assetCode }]}
      />

      <div className="flex flex-wrap items-center gap-2 text-sm">
        <Link to={links.asset(run.assetId)} className="font-mono text-xs text-slate-500 underline decoration-slate-300 underline-offset-4">{run.assetCode}</Link>
        {run.status === 'Missed' ? <Badge tone="danger" size="sm">Missed</Badge> : null}
        {done ? <Badge tone="success" size="sm">Done {run.completedAt ? formatDateTime(run.completedAt) : ''}{run.completedByName ? ` · ${run.completedByName}` : ''}</Badge> : null}
      </div>

      {sections.map(([section, items]) => (
        <section key={section || 'main'} className="rounded-[var(--radius-card)] border border-slate-200 bg-white shadow-sm">
          {section ? <h2 className="border-b border-slate-100 px-4 py-2 text-sm font-semibold text-slate-800">{section}</h2> : null}
          <ul className="divide-y divide-slate-100">
            {items.map((q) => done ? <DoneRow key={q.id} q={q} links={links} /> : (
              <QuestionRow key={q.id} q={q} answer={get(q)} onChange={(patch) => set(q, patch)} />
            ))}
          </ul>
        </section>
      ))}

      {done ? (data.notes ? <p className="rounded-lg bg-slate-50 p-3 text-sm text-slate-700">{data.notes}</p> : null) : (
        <>
          <Field label="Notes"><textarea className={textareaClass} value={notes} onChange={(event) => setNotes(event.target.value)} /></Field>
          <Field label="Signature" hint="Optional. Sign with your finger."><SignaturePad onChange={setSignature} className="h-28 rounded-lg border border-slate-200 bg-white" /></Field>
          <div className="fixed inset-x-0 bottom-0 z-20 border-t border-slate-200 bg-white/95 p-3 backdrop-blur sm:static sm:border-0 sm:bg-transparent sm:p-0">
            <div className="mx-auto flex max-w-2xl items-center justify-between gap-3">
              <span className="text-sm text-slate-600">
                {problems.length ? `${problems.length} still to answer` : failing ? <span className="font-medium text-amber-700">{failing} failing</span> : 'All good'}
              </span>
              <button type="button" className={cn(primaryButton, 'h-11 px-6')} disabled={busy || problems.length > 0} onClick={() => void send()}>Finish check</button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

function QuestionRow({ q, answer, onChange }: { q: CheckQuestion; answer: Answer; onChange: (patch: Partial<Answer>) => void }) {
  const type = q.type.toLowerCase()
  const result = scoreAnswer(q, answer.value)
  const range = q.minValue != null || q.maxValue != null
    ? `${q.minValue ?? '…'}–${q.maxValue ?? '…'}${q.unit ? ` ${q.unit}` : ''}` : q.unit
  const yesNo = type === 'yesno'

  return (
    <li className="space-y-2 px-4 py-3">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm font-medium text-slate-900">{q.prompt}{q.required ? <span className="text-rose-600"> *</span> : null}</p>
        {result === false ? <AlertTriangle className="h-5 w-5 shrink-0 text-amber-600" aria-label="Fails" /> : result === true ? <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-label="Passes" /> : null}
      </div>
      {PASS_FAIL.includes(type) || type === 'checkbox' || type === 'condition' ? (
        <div className="grid grid-cols-2 gap-2">
          {[{ value: yesNo ? 'yes' : 'pass', label: yesNo ? 'Yes' : 'OK', good: true }, { value: yesNo ? 'no' : 'fail', label: yesNo ? 'No' : 'Not OK', good: false }].map((option) => (
            <button key={option.value} type="button" aria-pressed={answer.value === option.value} onClick={() => onChange({ value: answer.value === option.value ? '' : option.value })}
              className={cn('h-12 rounded-lg border text-sm font-medium transition',
                answer.value === option.value
                  ? option.good ? 'border-emerald-600 bg-emerald-600 text-white' : 'border-rose-600 bg-rose-600 text-white'
                  : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50')}>
              {option.label}
            </button>
          ))}
        </div>
      ) : NUMBER.includes(type) ? (
        <div className="flex items-center gap-2">
          <input type="number" inputMode="decimal" step="any" aria-label={q.prompt} className={cn(inputClass, 'h-12 max-w-40 text-base', result === false && 'border-amber-500')} value={answer.value} onChange={(event) => onChange({ value: event.target.value })} />
          {range ? <span className="text-sm text-slate-500">{range}</span> : null}
        </div>
      ) : (
        <input aria-label={q.prompt} className={cn(inputClass, 'h-11')} value={answer.value} onChange={(event) => onChange({ value: event.target.value })} />
      )}
      {result === false ? (
        <>
          <input aria-label="What is wrong" placeholder={q.noteRequiredOnFail ? 'What is wrong? (required)' : 'What is wrong?'} className={cn(inputClass, 'h-11')} value={answer.note} onChange={(event) => onChange({ note: event.target.value })} />
          {FAIL_ACTION_HINT[q.failAction] ? <p className="text-xs text-amber-700">{FAIL_ACTION_HINT[q.failAction]}</p> : null}
        </>
      ) : null}
    </li>
  )
}

function DoneRow({ q, links }: { q: CheckQuestion; links: ReturnType<typeof useFactoryLinks> }) {
  return (
    <li className="flex items-start justify-between gap-3 px-4 py-3 text-sm">
      <div>
        <p className="text-slate-800">{q.prompt}</p>
        {q.note ? <p className="text-xs text-slate-500">{q.note}</p> : null}
        {q.raisedWorkOrderId ? <Link to={links.job(q.raisedWorkOrderId)} className="text-xs font-medium text-[var(--app-primary)] underline underline-offset-4">Job raised</Link> : null}
      </div>
      <span className={cn('shrink-0 font-medium', q.passed === false ? 'text-rose-700' : q.passed ? 'text-emerald-700' : 'text-slate-600')}>
        {q.value ?? '—'}{q.unit && q.value ? ` ${q.unit}` : ''}
      </span>
    </li>
  )
}
