import { useState } from 'react'
import toast from 'react-hot-toast'
import { Lock, Plus } from 'lucide-react'
import { FormDrawer } from '../../../shared/components/FormDrawer'
import { useAuth } from '../../../shared/hooks/useAuth'
import { formatDate, formatDateTime } from '../../../shared/utils/dates'
import { useDiary, useSaveDiary, useSubmitDiary } from '../siteApi'
import type { DiaryEntry, ProjectDetail } from '../types'
import { fieldClass, labelClass, primaryButtonClass, secondaryButtonClass, smallButtonClass, textAreaClass, textOrNull, todayInput } from './formStyles'

interface Draft {
  id?: string
  entryDate: string
  workCompleted: string
  issuesAndDelays: string
  instructionsReceived: string
  weather: string
}

function Section({ label, text }: { label: string; text: string | null }) {
  if (!text) return null
  return (
    <div>
      <div className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">{label}</div>
      <p className="mt-1 whitespace-pre-line text-sm text-slate-800">{text}</p>
    </div>
  )
}

/**
 * The daily site diary. Headcount and material issues for each day come from the muster roll and
 * stores, so they're shown rather than typed. A submitted entry is locked; corrections are new entries.
 */
export function DiaryTab({ project }: { project: ProjectDetail }) {
  const projectId = project.summary.id
  const { user } = useAuth()
  const isFinal = project.summary.stageCategory === 'Closed' || project.summary.stageCategory === 'Cancelled'
  const canWrite = project.abilities.canRunSite && !isFinal
  const { data: entries, isLoading } = useDiary(projectId)
  const saveDiary = useSaveDiary(projectId)
  const submitDiary = useSubmitDiary(projectId)
  const [draft, setDraft] = useState<Draft | null>(null)

  const save = async (andSubmit: boolean) => {
    if (!draft) return
    try {
      const saved = await saveDiary.mutateAsync({
        ...(draft.id ? { entryId: draft.id } : {}),
        entryDate: draft.entryDate,
        workCompleted: textOrNull(draft.workCompleted),
        issuesAndDelays: textOrNull(draft.issuesAndDelays),
        instructionsReceived: textOrNull(draft.instructionsReceived),
        weather: textOrNull(draft.weather),
      })
      if (andSubmit) await submitDiary.mutateAsync(saved.id)
      toast.success(andSubmit ? 'Diary entry submitted' : 'Diary entry saved as draft')
      setDraft(null)
    } catch {
      // toast from the API client
    }
  }

  const edit = (entry: DiaryEntry) => setDraft({
    id: entry.id,
    entryDate: entry.entryDate,
    workCompleted: entry.workCompleted ?? '',
    issuesAndDelays: entry.issuesAndDelays ?? '',
    instructionsReceived: entry.instructionsReceived ?? '',
    weather: entry.weather ?? '',
  })

  const hasContent = draft && [draft.workCompleted, draft.issuesAndDelays, draft.instructionsReceived].some((value) => value.trim())

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-slate-600">Work done, delays and site instructions, day by day. Instructions received here are the starting point for variations.</p>
        {canWrite ? (
          <button type="button" className={primaryButtonClass} onClick={() => setDraft({ entryDate: todayInput(), workCompleted: '', issuesAndDelays: '', instructionsReceived: '', weather: '' })}>
            <Plus className="h-4 w-4" />New entry
          </button>
        ) : null}
      </div>

      {isLoading ? <p className="text-sm text-slate-500">Loading the diary…</p> : (entries ?? []).length === 0 ? (
        <div className="rounded-xl border border-dashed border-slate-300 p-6 text-center text-sm text-slate-600">No diary entries yet.</div>
      ) : (
        <div className="space-y-3">
          {(entries ?? []).map((entry) => {
            const canEdit = canWrite && !entry.submittedAt && (entry.authorUserId === user?.userId || project.abilities.canManage)
            return (
              <article key={entry.id} className="space-y-3 rounded-xl border border-slate-200 bg-white p-4">
                <header className="flex flex-wrap items-start justify-between gap-2">
                  <div>
                    <div className="font-semibold text-slate-900">{formatDate(entry.entryDate)}</div>
                    <div className="text-xs text-slate-500">
                      {entry.authorName ?? 'Unknown'} · {entry.headcount} on site{entry.weather ? ` · ${entry.weather}` : ''}
                      {entry.photoCount > 0 ? ` · ${entry.photoCount} photo${entry.photoCount === 1 ? '' : 's'}` : ''}
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {entry.submittedAt
                      ? <span className="inline-flex items-center gap-1 text-xs text-slate-500"><Lock className="h-3.5 w-3.5" />Submitted {formatDateTime(entry.submittedAt)}</span>
                      : <span className="rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700">Draft</span>}
                    {canEdit ? (
                      <>
                        <button type="button" className={smallButtonClass} onClick={() => edit(entry)}>Edit</button>
                        <button type="button" className={smallButtonClass} disabled={submitDiary.isPending} onClick={() => submitDiary.mutate(entry.id, { onSuccess: () => toast.success('Diary entry submitted') })}>Submit</button>
                      </>
                    ) : null}
                  </div>
                </header>
                <Section label="Work completed" text={entry.workCompleted} />
                <Section label="Issues and delays" text={entry.issuesAndDelays} />
                <Section label="Instructions received" text={entry.instructionsReceived} />
                {entry.materialIssues.length > 0 ? <p className="text-xs text-slate-500">Materials issued: {entry.materialIssues.join(', ')}</p> : null}
              </article>
            )
          })}
        </div>
      )}

      <FormDrawer
        open={draft !== null}
        title={draft?.id ? 'Edit diary entry' : 'New diary entry'}
        onClose={() => setDraft(null)}
        footer={
          <div className="flex flex-wrap justify-end gap-2">
            <button type="button" className={secondaryButtonClass} onClick={() => setDraft(null)}>Cancel</button>
            <button type="button" className={secondaryButtonClass} disabled={!hasContent || saveDiary.isPending} onClick={() => void save(false)}>Save draft</button>
            <button type="button" className={primaryButtonClass} disabled={!hasContent || saveDiary.isPending} onClick={() => void save(true)}>Save and submit</button>
          </div>
        }
      >
        {draft ? (
          <div className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div>
                <label className={labelClass} htmlFor="diary-date">Date</label>
                <input id="diary-date" type="date" max={todayInput()} value={draft.entryDate} onChange={(event) => setDraft({ ...draft, entryDate: event.target.value })} className={fieldClass} />
              </div>
              <div>
                <label className={labelClass} htmlFor="diary-weather">Weather</label>
                <input id="diary-weather" value={draft.weather} onChange={(event) => setDraft({ ...draft, weather: event.target.value })} placeholder="e.g. Heavy rain after 2pm" className={fieldClass} />
              </div>
            </div>
            <div>
              <label className={labelClass} htmlFor="diary-work">Work completed</label>
              <textarea id="diary-work" rows={4} value={draft.workCompleted} onChange={(event) => setDraft({ ...draft, workCompleted: event.target.value })} className={textAreaClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="diary-issues">Issues and delays</label>
              <textarea id="diary-issues" rows={3} value={draft.issuesAndDelays} onChange={(event) => setDraft({ ...draft, issuesAndDelays: event.target.value })} className={textAreaClass} />
            </div>
            <div>
              <label className={labelClass} htmlFor="diary-instructions">Instructions received</label>
              <textarea id="diary-instructions" rows={3} value={draft.instructionsReceived} onChange={(event) => setDraft({ ...draft, instructionsReceived: event.target.value })} placeholder="From the client, consultant or engineer" className={textAreaClass} />
            </div>
            <p className="text-xs text-slate-500">Headcount comes from the muster roll for this date. A submitted entry can't be changed.</p>
          </div>
        ) : null}
      </FormDrawer>
    </div>
  )
}
