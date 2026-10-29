import { useState } from 'react'
import toast from 'react-hot-toast'
import { ArrowDown, ArrowUp, Plus, X } from 'lucide-react'
import { ConfirmDialog } from '../../../shared/components/ConfirmDialog'
import { usePermission } from '../../../shared/hooks/usePermission'
import { useDeleteSequence, useSaveSequence, useSequences } from '../../crm/api'
import type { SalesSequence, SalesSequenceStep, UpsertSequencePayload } from '../../crm/types'

const fieldClass = 'h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]'

type Editing = UpsertSequencePayload & { id?: string }

const newSequence = (): Editing => ({
  name: '',
  isActive: true,
  stopOnReply: true,
  steps: [
    { delayDays: 0, action: 'Task', subject: null, body: 'Call {{ContactName}} to introduce yourself' },
    { delayDays: 2, action: 'Email', subject: 'Following up: {{DealTitle}}', body: 'Hi {{ContactName}},\n\nI wanted to follow up on your enquiry. When would suit you for a short call?\n\n{{OwnerName}}' },
    { delayDays: 3, action: 'Task', subject: null, body: 'Call {{ContactName}} again' },
  ],
})

/** "Day 0: task · day 2: email · day 5: task" */
export function describeSteps(steps: SalesSequenceStep[]) {
  let day = 0
  return steps.map((step) => {
    day += step.delayDays
    return `day ${day}: ${step.action === 'Email' ? 'email' : 'task'}`
  }).join(' · ')
}

/**
 * Follow-up sequences: a run of tasks and emails a deal is put through until the client answers.
 * Admins and sales managers set them up; anyone working the pipeline starts one on a deal.
 */
export function SequencesSection() {
  const { data: sequences } = useSequences()
  const save = useSaveSequence()
  const remove = useDeleteSequence()
  const canConfigure = usePermission('crm.view_all')
  const [editing, setEditing] = useState<Editing | null>(null)
  const [deleting, setDeleting] = useState<SalesSequence | null>(null)

  const setStep = (index: number, patch: Partial<SalesSequenceStep>) =>
    editing && setEditing({ ...editing, steps: editing.steps.map((s, i) => (i === index ? { ...s, ...patch } : s)) })
  const moveStep = (index: number, direction: -1 | 1) => {
    if (!editing) return
    const steps = [...editing.steps]
    const [step] = steps.splice(index, 1)
    if (!step) return
    steps.splice(index + direction, 0, step)
    setEditing({ ...editing, steps })
  }

  const submit = async () => {
    if (!editing) return
    if (!editing.name.trim()) return void toast.error('Name the sequence')
    if (editing.steps.length === 0) return void toast.error('Add at least one step')
    if (editing.steps.some((s) => !s.body.trim() || (s.action === 'Email' && !s.subject?.trim()))) return void toast.error('Fill in every step (emails need a subject)')
    const { id, ...payload } = editing
    try {
      await save.mutateAsync({ id, payload: { ...payload, name: payload.name.trim() } })
      toast.success('Sequence saved')
      setEditing(null)
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <section className="space-y-3 rounded-[var(--radius-card)] border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-slate-900">Follow-up sequences</h2>
          <p className="mt-1 text-sm text-slate-500">
            Start one on a deal and its steps come due on their own: tasks land on the owner's follow-up list, emails go to the deal's contact as the owner.
            A reply from the client, or closing the deal, stops it.
          </p>
        </div>
        {canConfigure && !editing ? (
          <button type="button" onClick={() => setEditing(newSequence())} className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-200 px-3 text-sm font-medium text-slate-700 hover:bg-slate-50">
            <Plus className="h-4 w-4" />Add sequence
          </button>
        ) : null}
      </div>

      {editing ? (
        <div className="space-y-3 rounded-xl border border-slate-200 p-3">
          <div className="grid gap-3 md:grid-cols-2">
            <input aria-label="Sequence name" placeholder="Name, e.g. New website lead" value={editing.name} onChange={(event) => setEditing({ ...editing, name: event.target.value })} className={fieldClass} />
            <div className="flex flex-wrap items-center gap-4 text-sm text-slate-700">
              <label className="flex items-center gap-2"><input type="checkbox" checked={editing.isActive} onChange={(event) => setEditing({ ...editing, isActive: event.target.checked })} />On</label>
              <label className="flex items-center gap-2"><input type="checkbox" checked={editing.stopOnReply} onChange={(event) => setEditing({ ...editing, stopOnReply: event.target.checked })} />Stop when the client replies</label>
            </div>
          </div>
          <ol className="space-y-3">
            {editing.steps.map((step, index) => (
              <li key={index} className="space-y-2 rounded-lg bg-slate-50 p-3">
                <div className="flex flex-wrap items-center gap-2 text-sm text-slate-700">
                  <span className="font-medium">Step {index + 1}:</span>
                  <input
                    aria-label={`Step ${index + 1} wait`}
                    type="number"
                    min={0}
                    max={365}
                    value={step.delayDays}
                    onChange={(event) => setStep(index, { delayDays: Math.max(0, Math.min(365, Number(event.target.value) || 0)) })}
                    className="h-9 w-16 rounded-lg border border-slate-300 px-2 text-sm"
                  />
                  <span>{index === 0 ? 'days after starting,' : 'days later,'}</span>
                  <select aria-label={`Step ${index + 1} action`} value={step.action} onChange={(event) => setStep(index, { action: event.target.value as SalesSequenceStep['action'] })} className="h-9 rounded-lg border border-slate-300 px-2 text-sm">
                    <option value="Task">add a task</option>
                    <option value="Email">send an email</option>
                  </select>
                  <span className="ml-auto flex items-center">
                    <button type="button" aria-label="Move step up" disabled={index === 0} onClick={() => moveStep(index, -1)} className="rounded p-1 text-slate-500 hover:bg-slate-200 disabled:opacity-30"><ArrowUp className="h-4 w-4" /></button>
                    <button type="button" aria-label="Move step down" disabled={index === editing.steps.length - 1} onClick={() => moveStep(index, 1)} className="rounded p-1 text-slate-500 hover:bg-slate-200 disabled:opacity-30"><ArrowDown className="h-4 w-4" /></button>
                    <button type="button" aria-label="Remove step" onClick={() => setEditing({ ...editing, steps: editing.steps.filter((_, i) => i !== index) })} className="rounded p-1 text-rose-500 hover:bg-rose-50"><X className="h-4 w-4" /></button>
                  </span>
                </div>
                {step.action === 'Email' ? (
                  <input aria-label={`Step ${index + 1} subject`} placeholder="Subject" value={step.subject ?? ''} onChange={(event) => setStep(index, { subject: event.target.value })} className={fieldClass} />
                ) : null}
                <textarea
                  aria-label={`Step ${index + 1} ${step.action === 'Email' ? 'email' : 'task'}`}
                  rows={step.action === 'Email' ? 5 : 2}
                  value={step.body}
                  onChange={(event) => setStep(index, { body: event.target.value })}
                  placeholder={step.action === 'Email' ? 'The email' : 'What to do'}
                  className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)]"
                />
              </li>
            ))}
          </ol>
          <button type="button" onClick={() => setEditing({ ...editing, steps: [...editing.steps, { delayDays: 2, action: 'Task', subject: null, body: '' }] })} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-dashed border-slate-300 px-3 text-sm font-medium text-slate-600 hover:bg-slate-50">
            <Plus className="h-4 w-4" />Add step
          </button>
          <p className="text-xs text-slate-500">
            {'{{ContactName}}'} (first name), {'{{CompanyName}}'}, {'{{DealTitle}}'} and {'{{OwnerName}}'} are filled in for each deal. An email step for a deal with no email address becomes a task instead.
          </p>
          <div className="flex gap-2">
            <button type="button" disabled={save.isPending} onClick={() => void submit()} className="inline-flex h-10 items-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white disabled:opacity-60">Save sequence</button>
            <button type="button" onClick={() => setEditing(null)} className="inline-flex h-10 items-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-700">Cancel</button>
          </div>
        </div>
      ) : null}

      {sequences && sequences.length === 0 && !editing ? <p className="text-sm text-slate-500">No sequences yet.</p> : null}
      <ul className="divide-y divide-slate-100">
        {(sequences ?? []).map((sequence) => (
          <li key={sequence.id} className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="min-w-0 text-sm">
              <div className="font-medium text-slate-900">
                {sequence.name} {sequence.isActive ? null : <span className="text-xs font-normal text-slate-400">(off)</span>}
              </div>
              <div className="text-slate-500">
                {describeSteps(sequence.steps)}
                {sequence.runningDeals ? ` · running on ${sequence.runningDeals} deal${sequence.runningDeals === 1 ? '' : 's'}` : ''}
              </div>
            </div>
            {canConfigure ? (
              <div className="flex shrink-0 items-center gap-3">
                <button type="button" onClick={() => setEditing({ id: sequence.id, name: sequence.name, isActive: sequence.isActive, stopOnReply: sequence.stopOnReply, steps: sequence.steps })} className="text-xs font-medium text-slate-600 hover:underline">Edit</button>
                {sequence.runningDeals === 0 ? (
                  <button type="button" onClick={() => setDeleting(sequence)} className="text-xs font-medium text-rose-600 hover:underline">Delete</button>
                ) : null}
              </div>
            ) : null}
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={Boolean(deleting)}
        title="Delete sequence"
        message={deleting ? `Delete ${deleting.name}?` : ''}
        confirmLabel="Delete"
        danger
        onCancel={() => setDeleting(null)}
        onConfirm={() => {
          if (!deleting) return
          remove.mutate(deleting.id, { onSuccess: () => toast.success('Sequence deleted') })
          setDeleting(null)
        }}
      />
    </section>
  )
}
