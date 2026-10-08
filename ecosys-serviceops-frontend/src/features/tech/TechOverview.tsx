import { useState, type ReactNode } from 'react'
import toast from 'react-hot-toast'
import { useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, MapPin, Phone, Star } from 'lucide-react'
import { cn } from '../../shared/utils/cn'
import { formatDate, formatDateTime, formatRelative } from '../../shared/utils/dates'
import { useExecutionBundle, useSaveExecutionNotes, useUpdateChecklistItem } from '../work-orders/api'
import type { WorkOrder, WorkOrderChecklistItem } from '../work-orders/types'

const cardClass = 'rounded-xl border border-slate-200 bg-white p-4'
const inputClass = 'w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className={cardClass}>
      <h2 className="mb-2 text-sm font-semibold text-slate-900">{title}</h2>
      {children}
    </section>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  if (children === null || children === undefined || children === '') return null
  return (
    <div className="flex gap-3 py-1 text-sm">
      <span className="w-24 shrink-0 text-slate-500">{label}</span>
      <span className="min-w-0 flex-1 break-words text-slate-900">{children}</span>
    </div>
  )
}

function ChecklistItem({ workOrderId, item, editable }: { workOrderId: string; item: WorkOrderChecklistItem; editable: boolean }) {
  const queryClient = useQueryClient()
  const update = useUpdateChecklistItem(workOrderId)
  const [value, setValue] = useState(item.responseValue ?? '')
  const [remarks, setRemarks] = useState(item.remarks ?? '')
  const isNumber = /number|numeric|reading/i.test(item.inputType)
  const choices = item.options.length > 0
    ? item.options
    : /pass|fail/i.test(item.inputType) ? ['Pass', 'Fail', 'N/A'] : /yes|no|bool/i.test(item.inputType) ? ['Yes', 'No', 'N/A'] : []

  const save = async (responseValue: string) => {
    try {
      await update.mutateAsync({ itemId: item.id, responseValue, remarks: remarks.trim() || null, isCompleted: Boolean(responseValue) })
      // The technician job carries the checklist, so refresh it too.
      void queryClient.invalidateQueries({ queryKey: ['work-orders', workOrderId, 'tech'] })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <li className="space-y-2 py-3">
      <div className="flex items-start gap-2">
        {item.isCompleted ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" /> : <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-slate-300" />}
        <p className="text-sm text-slate-900">
          {item.questionText}{item.isRequired ? <span className="text-rose-600"> *</span> : null}
          {item.unit ? <span className="text-slate-500"> ({item.unit})</span> : null}
        </p>
      </div>
      {choices.length > 0 ? (
        <div className="flex flex-wrap gap-1.5 pl-6">
          {choices.map((choice) => (
            <button
              key={choice}
              type="button"
              disabled={!editable || update.isPending}
              onClick={() => { setValue(choice); void save(choice) }}
              className={cn('h-9 rounded-lg border px-3 text-sm font-medium', value === choice ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-300 bg-white text-slate-700', 'disabled:opacity-60')}
            >
              {choice}
            </button>
          ))}
        </div>
      ) : (
        <div className="flex gap-2 pl-6">
          <input
            aria-label={item.questionText}
            type={isNumber ? 'number' : 'text'}
            inputMode={isNumber ? 'decimal' : undefined}
            value={value}
            disabled={!editable}
            onChange={(event) => setValue(event.target.value)}
            className={cn(inputClass, 'h-10')}
          />
          <button type="button" disabled={!editable || update.isPending || value === (item.responseValue ?? '')} onClick={() => void save(value.trim())} className="h-10 shrink-0 rounded-lg border border-slate-300 px-3 text-sm font-medium text-slate-700 disabled:opacity-50">Save</button>
        </div>
      )}
      {editable && (item.technicianRemarksAllowed || (item.requiresNoteOnFail && value === 'Fail')) ? (
        <input
          aria-label={`Remarks for ${item.questionText}`}
          placeholder={item.requiresNoteOnFail && value === 'Fail' ? 'Note required for a fail' : 'Remarks (optional)'}
          value={remarks}
          onChange={(event) => setRemarks(event.target.value)}
          onBlur={() => { if (remarks !== (item.remarks ?? '') && value) void save(value) }}
          className={cn(inputClass, 'ml-6 h-9 w-[calc(100%-1.5rem)]')}
        />
      ) : null}
    </li>
  )
}

export function TechOverview({ job, editable }: { job: WorkOrder; editable: boolean }) {
  const { data: bundle } = useExecutionBundle(job.id)
  const saveNotes = useSaveExecutionNotes(job.id)
  const [update, setUpdate] = useState('')

  const phone = job.siteContactPhone ?? job.clientContactPhone
  const mapUrl = job.siteLatitude != null && job.siteLongitude != null
    ? `https://www.google.com/maps/dir/?api=1&destination=${job.siteLatitude},${job.siteLongitude}`
    : job.siteLocation ? `https://www.google.com/maps/dir/?api=1&destination=${encodeURIComponent(job.siteLocation)}` : null
  const answered = job.checklistItems.filter((item) => item.isCompleted).length

  const postUpdate = async () => {
    if (!update.trim()) return
    try {
      await saveNotes.mutateAsync({ progressComment: update.trim() })
      toast.success('Update saved')
      setUpdate('')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className="space-y-3">
      {job.description ? (
        <Card title="The job">
          <p className="whitespace-pre-line text-sm leading-6 text-slate-700">{job.description}</p>
          <div className="mt-2">
            <Row label="Type">{job.workType.replace(/([a-z])([A-Z])/g, "$1 $2")}</Row>
            <Row label="Due">{job.dueDate ? formatDate(job.dueDate) : null}</Row>
          </div>
        </Card>
      ) : null}

      <Card title="Site">
        <Row label="Client">{job.clientName}</Row>
        <Row label="Site">{job.siteName}</Row>
        <Row label="Contact">{job.siteContactPerson ?? job.clientContactPerson}</Row>
        <Row label="Hours">{job.siteOperatingHours}</Row>
        <Row label="Access">{job.siteAccessNotes ?? job.jobCardNotes}</Row>
        {phone || mapUrl ? (
          <div className="mt-3 grid grid-cols-2 gap-2">
            {phone ? (
              <a href={`tel:${phone.replace(/\s+/g, '')}`} className="inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-slate-300 text-sm font-medium text-slate-800">
                <Phone className="h-4 w-4" />Call
              </a>
            ) : <span />}
            {mapUrl ? (
              <a href={mapUrl} target="_blank" rel="noreferrer" className="inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-slate-300 text-sm font-medium text-slate-800">
                <MapPin className="h-4 w-4" />Directions
              </a>
            ) : null}
          </div>
        ) : null}
      </Card>

      {job.assetName ? (
        <Card title="Asset">
          <Row label="Asset">{job.assetName}</Row>
          <Row label="Serial">{job.assetSerialNumber}</Row>
          <Row label="Model">{[job.assetManufacturer, job.assetModel].filter(Boolean).join(' ') || null}</Row>
          <Row label="Location">{job.assetLocation}</Row>
          <Row label="Last PM">{job.assetLastServiceDate ? formatDate(job.assetLastServiceDate) : null}</Row>
        </Card>
      ) : null}

      {job.technicianAssignments.length > 1 ? (
        <Card title="Crew">
          <ul className="space-y-1">
            {job.technicianAssignments.map((assignment) => (
              <li key={assignment.id} className="flex items-center justify-between text-sm">
                <span className="flex items-center gap-1.5 text-slate-900">
                  {assignment.technicianName ?? 'Technician'}
                  {assignment.isLead ? <span className="inline-flex items-center gap-0.5 text-xs text-amber-700"><Star className="h-3 w-3" />Lead</span> : null}
                </span>
                <span className="text-xs text-slate-500">{assignment.arrivalAt ? 'On site' : assignment.status}</span>
              </li>
            ))}
          </ul>
        </Card>
      ) : null}

      {job.checklistItems.length > 0 ? (
        <Card title={`Checklist · ${answered} of ${job.checklistItems.length}`}>
          {!editable ? <p className="text-xs text-slate-500">Start the job to fill in the checklist.</p> : null}
          <ul className="divide-y divide-slate-100">
            {[...job.checklistItems]
              .sort((a, b) => a.sectionOrder - b.sectionOrder || a.sortOrder - b.sortOrder)
              .map((item) => <ChecklistItem key={item.id} workOrderId={job.id} item={item} editable={editable} />)}
          </ul>
        </Card>
      ) : null}

      <Card title="Progress updates">
        <textarea
          value={update}
          onChange={(event) => setUpdate(event.target.value)}
          rows={3}
          placeholder="Day 1: Inspected generator, found faulty AVR, awaiting replacement."
          className={cn(inputClass, 'py-2')}
        />
        <button type="button" onClick={() => void postUpdate()} disabled={!update.trim() || saveNotes.isPending} className="mt-2 inline-flex h-10 items-center rounded-lg bg-slate-900 px-4 text-sm font-medium text-white disabled:opacity-50">
          {saveNotes.isPending ? 'Saving…' : 'Save update'}
        </button>
        {(bundle?.progressUpdates ?? []).length > 0 ? (
          <ul className="mt-3 space-y-2 border-t border-slate-100 pt-3">
            {bundle!.progressUpdates.slice(0, 5).map((entry) => (
              <li key={entry.id} className="text-sm">
                <p className="text-slate-800">{entry.message}</p>
                <p className="text-xs text-slate-500" title={formatDateTime(entry.occurredAt)}>{entry.actorUserName ?? 'Someone'} · {formatRelative(entry.occurredAt)}</p>
              </li>
            ))}
          </ul>
        ) : null}
      </Card>
    </div>
  )
}
