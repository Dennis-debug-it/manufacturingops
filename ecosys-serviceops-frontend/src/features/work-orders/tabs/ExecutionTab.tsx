import { useMemo, useState } from 'react'
import toast from 'react-hot-toast'
import { CheckCircle2, XCircle, MinusCircle } from 'lucide-react'
import { EmptyState } from '../../../shared/components/EmptyState'
import { Spinner } from '../../../shared/components/Spinner'
import { usePermission } from '../../../shared/hooks/usePermission'
import { cn } from '../../../shared/utils/cn'
import { useChecklist, useExecutionBundle, useSaveExecutionNotes, useUpdateChecklistItem } from '../api'
import type { WorkOrderChecklistItem } from '../types'

const passFailOptions: Array<{ value: string; label: string; icon: typeof CheckCircle2 }> = [
  { value: 'Pass', label: 'Pass', icon: CheckCircle2 },
  { value: 'Fail', label: 'Fail', icon: XCircle },
  { value: 'N/A', label: 'N/A', icon: MinusCircle },
]
// Checkbox and yes/no questions (e.g. the starter packs' "Follow-up required") answer Yes or No.
const yesNoOptions: typeof passFailOptions = [
  { value: 'Yes', label: 'Yes', icon: CheckCircle2 },
  { value: 'No', label: 'No', icon: XCircle },
]

function ChecklistRow({ item, workOrderId, canExecute }: { item: WorkOrderChecklistItem; workOrderId: string; canExecute: boolean }) {
  const updateItem = useUpdateChecklistItem(workOrderId)
  const [remarks, setRemarks] = useState(item.remarks ?? '')

  const isPassFail = ['passfail', 'pass/fail', 'boolean'].includes(item.inputType.toLowerCase())
  const isYesNo = ['checkbox', 'yesno', 'yes/no'].includes(item.inputType.toLowerCase())
  const choiceOptions = isYesNo ? yesNoOptions : passFailOptions
  const isMeasurement = ['number', 'measurement'].includes(item.inputType.toLowerCase())

  const setResponse = async (value: string) => {
    try {
      await updateItem.mutateAsync({ itemId: item.id, responseValue: value, isCompleted: true })
    } catch {
      // interceptor surfaces the error toast
    }
  }

  const saveRemarks = async () => {
    try {
      await updateItem.mutateAsync({ itemId: item.id, remarks })
      toast.success('Note saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  return (
    <div className={cn('rounded-xl border p-4', item.isCompleted ? 'border-emerald-200 bg-emerald-50/40' : 'border-slate-200 bg-white')}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-slate-900">{item.questionText}{item.isRequired ? <span className="text-rose-600"> *</span> : null}</p>
          {item.helperText ? <p className="mt-1 text-xs text-slate-500">{item.helperText}</p> : null}
        </div>
        {item.isCompleted ? <span className="text-xs font-medium text-emerald-700">Completed</span> : null}
      </div>

      <div className="mt-3">
        {isPassFail || isYesNo ? (
          <div className="flex gap-2">
            {choiceOptions.map((option) => {
              const Icon = option.icon
              const selected = item.responseValue === option.value
              return (
                <button
                  key={option.value}
                  type="button"
                  disabled={!canExecute || updateItem.isPending}
                  onClick={() => setResponse(option.value)}
                  className={cn(
                    'inline-flex h-10 items-center gap-1.5 rounded-lg border px-3 text-sm font-medium transition disabled:cursor-not-allowed disabled:opacity-50',
                    selected ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
                  )}
                >
                  <Icon className="h-4 w-4" />
                  {option.label}
                </button>
              )
            })}
          </div>
        ) : isMeasurement ? (
          <div className="flex items-center gap-2">
            <input
              type="number"
              disabled={!canExecute}
              defaultValue={item.responseValue ?? ''}
              onBlur={(event) => setResponse(event.target.value)}
              min={item.minValue ?? undefined}
              max={item.maxValue ?? undefined}
              className="h-10 w-40 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
            />
            {item.unit ? <span className="text-sm text-slate-500">{item.unit}</span> : null}
          </div>
        ) : (
          <input
            type="text"
            disabled={!canExecute}
            defaultValue={item.responseValue ?? ''}
            onBlur={(event) => setResponse(event.target.value)}
            className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
        )}
      </div>

      {item.technicianRemarksAllowed ? (
        <div className="mt-3 flex items-center gap-2">
          <input
            type="text"
            disabled={!canExecute}
            value={remarks}
            onChange={(event) => setRemarks(event.target.value)}
            placeholder="Add a note…"
            className="h-9 flex-1 rounded-lg border border-slate-300 px-3 text-xs outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
          {canExecute ? (
            <button type="button" onClick={saveRemarks} className="h-9 rounded-lg border border-slate-200 px-3 text-xs font-medium text-slate-700 hover:bg-slate-50">Save</button>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}

export function ExecutionTab({ workOrderId }: { workOrderId: string }) {
  const canExecute = usePermission('work-orders.execute')
  const { data: checklist, isLoading, isError } = useChecklist(workOrderId)
  const saveNotes = useSaveExecutionNotes(workOrderId)
  const { data: bundle } = useExecutionBundle(workOrderId)
  const savedWorkDone = bundle?.notes.workDone ?? ''
  const [workDone, setWorkDone] = useState<string | null>(null)
  // Show what was saved until the user starts editing.
  const workDoneValue = workDone ?? savedWorkDone

  const sections = useMemo(() => {
    const items = checklist ?? []
    const grouped = new Map<string, WorkOrderChecklistItem[]>()
    for (const item of items) {
      const key = item.sectionName ?? 'General'
      const bucket = grouped.get(key) ?? []
      bucket.push(item)
      grouped.set(key, bucket)
    }
    return Array.from(grouped.entries())
  }, [checklist])

  const submitWorkDone = async () => {
    if (!workDoneValue.trim()) return
    try {
      await saveNotes.mutateAsync({ workDone: workDoneValue.trim() })
      setWorkDone(null)
      toast.success('Work performed notes saved')
    } catch {
      // interceptor surfaces the error toast
    }
  }

  if (isLoading) {
    return <Spinner label="Loading checklist" />
  }

  if (isError) {
    return <EmptyState title="Execution data unavailable" description="The checklist could not be loaded." />
  }

  if (sections.length === 0) {
    return (
      <div className="space-y-4">
        <EmptyState title="No checklist attached" description="This work order has no PM checklist. Record the work performed instead." />
        <div className="rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-4 shadow-sm">
          <label className="mb-2 block text-sm font-medium text-slate-700" htmlFor="work-done">Work Performed</label>
          <textarea
            id="work-done"
            disabled={!canExecute}
            value={workDoneValue}
            onChange={(event) => setWorkDone(event.target.value)}
            rows={5}
            placeholder="Describe the work performed…"
            className="w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50"
          />
          {canExecute ? (
            <button
              type="button"
              onClick={submitWorkDone}
              disabled={saveNotes.isPending || !workDoneValue.trim() || workDoneValue === savedWorkDone}
              className="mt-3 inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60"
            >
              {saveNotes.isPending ? 'Saving…' : 'Save'}
            </button>
          ) : null}
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {sections.map(([sectionName, items]) => (
        <div key={sectionName}>
          <div className="mb-3 text-sm font-semibold text-slate-900">{sectionName}</div>
          <div className="space-y-3">
            {items.map((item) => (
              <ChecklistRow key={item.id} item={item} workOrderId={workOrderId} canExecute={canExecute} />
            ))}
          </div>
        </div>
      ))}
    </div>
  )
}
