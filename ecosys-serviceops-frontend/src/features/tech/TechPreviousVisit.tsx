import { useState } from 'react'
import toast from 'react-hot-toast'
import { FileDown, History } from 'lucide-react'
import { AuthenticatedImage } from '../../shared/components/AuthenticatedImage'
import type { WorkOrder } from '../work-orders/types'
import { downloadFollowUpSourceReport, useFollowUpSourceReport } from './api'

/**
 * On a follow-up job: what was found and done on the original visit (notes, failed checklist
 * answers, parts, photos), read from that job's service report.
 */
export function TechPreviousVisit({ job }: { job: WorkOrder }) {
  const isFollowUp = Boolean(job.followUpOfWorkOrderId)
  const { data: report, isLoading, isError } = useFollowUpSourceReport(job.id, isFollowUp)
  const [downloading, setDownloading] = useState(false)
  if (!isFollowUp) return null

  const originalNumber = report?.workOrderNumber ?? job.followUpOfWorkOrderNumber ?? 'the original job'
  const flagged = (report?.checklistSections ?? [])
    .flatMap((section) => section.items)
    .filter((item) => /fail|^no$/i.test(item.responseValue ?? '') || item.remarks)
  const photos = (report?.photoGroups ?? []).flatMap((group) => group.photos.map((photo) => ({ ...photo, category: group.category }))).filter((photo) => photo.publicUrl)

  const download = async () => {
    setDownloading(true)
    try {
      await downloadFollowUpSourceReport(job.id, originalNumber)
    } catch {
      toast.error('Could not download the previous report')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <section className="space-y-3 rounded-xl border border-sky-200 bg-sky-50/60 p-4" data-testid="tech-previous-visit">
      <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
        <History className="h-4 w-4 text-sky-700" />Previous visit · {originalNumber}
      </h2>
      {isLoading ? <p className="text-sm text-slate-500">Loading the previous report…</p> : null}
      {isError ? <p className="text-sm text-rose-700">The previous visit's report could not be loaded.</p> : null}
      {report ? (
        <>
          {report.findings ? <p className="text-sm text-slate-800"><span className="font-medium">Found: </span>{report.findings}</p> : null}
          <p className="text-sm text-slate-800"><span className="font-medium">Work done: </span>{report.workDone || 'No notes recorded.'}</p>
          {report.technicianTeam ? <p className="text-xs text-slate-500">By {report.technicianTeam}</p> : null}
          {flagged.length > 0 ? (
            <div>
              <p className="text-xs font-medium text-slate-600">Checklist notes</p>
              <ul className="mt-1 space-y-0.5 text-sm text-slate-800">
                {flagged.map((item) => (
                  <li key={item.question}>{item.question}: <span className="font-medium">{item.responseValue ?? '—'}</span>{item.remarks ? ` (${item.remarks})` : ''}</li>
                ))}
              </ul>
            </div>
          ) : null}
          {report.materials.length > 0 ? (
            <p className="text-sm text-slate-800"><span className="font-medium">Parts used: </span>{report.materials.map((material) => `${material.name} × ${material.quantityUsed}`).join(', ')}</p>
          ) : null}
          {photos.length > 0 ? (
            <div className="grid grid-cols-3 gap-2">
              {photos.map((photo) => (
                <figure key={photo.publicUrl} className="overflow-hidden rounded-lg border border-slate-200 bg-white">
                  <AuthenticatedImage src={photo.publicUrl!} alt={photo.caption} className="h-20 w-full object-cover" />
                  <figcaption className="truncate px-1.5 py-0.5 text-[10px] text-slate-500">{photo.category}</figcaption>
                </figure>
              ))}
            </div>
          ) : null}
          <button type="button" onClick={() => void download()} disabled={downloading} className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-lg border border-slate-300 bg-white text-sm font-medium text-slate-800 disabled:opacity-60">
            <FileDown className="h-4 w-4" />{downloading ? 'Preparing PDF…' : 'Full previous report (PDF)'}
          </button>
        </>
      ) : null}
    </section>
  )
}
