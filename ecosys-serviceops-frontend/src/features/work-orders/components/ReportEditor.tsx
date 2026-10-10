import { useState } from 'react'
import toast from 'react-hot-toast'
import { formatDateTime } from '../../../shared/utils/dates'
import { useUpdateReportContent } from '../api'
import type { ReportContent, ReportTextField } from '../types'

type Key = 'reportedProblem' | 'findings' | 'workDone'

const fields: Array<{ key: Key; label: string; source: string }> = [
  { key: 'reportedProblem', label: 'Problem reported', source: "the job's description" },
  { key: 'findings', label: 'Findings', source: "the technician's notes" },
  { key: 'workDone', label: 'Work done', source: "the technician's notes" },
]

const current = (field: ReportTextField) => field.edited ?? field.original ?? ''

/**
 * Edits the wording that goes on the service report. The job's own description and the technician's
 * notes are kept as they were; each section can be put back to them.
 */
export function ReportEditor({ workOrderId, content, onClose }: { workOrderId: string; content: ReportContent; onClose: () => void }) {
  const update = useUpdateReportContent(workOrderId)
  const [values, setValues] = useState<Record<Key, string>>({
    reportedProblem: current(content.reportedProblem),
    findings: current(content.findings),
    workDone: current(content.workDone),
  })

  const save = async () => {
    try {
      const saved = await update.mutateAsync({
        reportedProblem: values.reportedProblem,
        findings: values.findings,
        workDone: values.workDone,
      })
      toast.success(content.reportStatus === 'Approved' && saved.reportStatus !== 'Approved' ? 'Report updated — it needs approving again' : 'Report updated')
      onClose()
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-4 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] p-5 shadow-sm backdrop-blur-[var(--glass-blur-md)]">
      <div>
        <h3 className="text-sm font-semibold text-slate-900">Edit report</h3>
        <p className="mt-1 text-xs text-slate-500">Changes the wording on the service report only. The job's description and the technician's notes stay as they were.</p>
      </div>
      {content.reportStatus === 'Approved' ? (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800">This report is approved. Saving changes sends it back for approval before it can be sent.</p>
      ) : null}

      {fields.map(({ key, label, source }) => {
        const field = content[key]
        const original = field.original ?? ''
        const differs = values[key].trim() !== original.trim()
        return (
          <div key={key} className="space-y-1.5">
            <label htmlFor={`report-${key}`} className="block text-sm font-medium text-slate-700">{label}</label>
            <textarea
              id={`report-${key}`}
              rows={4}
              maxLength={4000}
              value={values[key]}
              onChange={(event) => setValues((prev) => ({ ...prev, [key]: event.target.value }))}
              className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm leading-6 outline-none focus:border-[var(--app-primary)]"
            />
            {differs ? (
              <div className="flex flex-wrap items-start justify-between gap-2 text-xs text-slate-500">
                <span className="min-w-0 flex-1">From {source}: {original ? <span className="italic">“{original}”</span> : 'nothing recorded'}</span>
                <button type="button" onClick={() => setValues((prev) => ({ ...prev, [key]: original }))} className="shrink-0 font-medium text-slate-700 underline underline-offset-2 hover:text-slate-900">
                  Use original
                </button>
              </div>
            ) : null}
          </div>
        )
      })}

      <div className="flex flex-wrap gap-2">
        <button type="button" onClick={() => void save()} disabled={update.isPending} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:opacity-60">
          {update.isPending ? 'Saving…' : 'Save report'}
        </button>
        <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 hover:bg-slate-50">
          Cancel
        </button>
      </div>
    </div>
  )
}

/** One line under the report header saying who last edited the wording, or why it can't be edited. */
export function ReportEditNote({ content }: { content: ReportContent }) {
  if (content.lockedReason) return <p className="text-xs text-slate-500">{content.lockedReason}</p>
  if (!content.editedAt) return null
  return <p className="text-xs text-slate-500">Wording edited{content.editedByName ? ` by ${content.editedByName}` : ''} · {formatDateTime(content.editedAt)}</p>
}
