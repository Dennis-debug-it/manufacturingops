import { useEffect, useState } from 'react'
import { X } from 'lucide-react'

export interface CompleteWorkOrderSubmission {
  workDoneNotes: string
}

export interface CompleteWorkOrderDialogProps {
  open: boolean
  woNumber?: string | undefined
  submitting?: boolean
  /** Work-done notes already recorded during execution, used to prefill the dialog. */
  initialNotes?: string | null | undefined
  onClose: () => void
  onSubmit: (submission: CompleteWorkOrderSubmission) => void
}

export function CompleteWorkOrderDialog({
  open,
  woNumber,
  submitting,
  initialNotes,
  onClose,
  onSubmit,
}: CompleteWorkOrderDialogProps) {
  const [notes, setNotes] = useState('')

  useEffect(() => {
    if (open) {
      setNotes(initialNotes ?? '')
    } else {
      setNotes('')
    }
  }, [open, initialNotes])

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center px-4 py-6">
      <button type="button" aria-label="Close dialog" className="absolute inset-0 bg-[var(--app-primary)]/40" onClick={onClose} />
      <div className="relative w-full max-w-md rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] backdrop-blur-[var(--glass-blur-md)] p-6 shadow-xl">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h3 className="text-lg font-semibold text-slate-900">Complete work order</h3>
            <p className="mt-1 text-sm leading-6 text-slate-600">
              {woNumber ? `Record the work done for ${woNumber} before marking it complete.` : 'Record the work done before marking this work order complete.'}
            </p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-2 text-slate-500 transition hover:bg-slate-100 hover:text-slate-900">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="mt-4">
          <label className="mb-1.5 block text-sm font-medium text-slate-700" htmlFor="complete-dialog-notes">Work done notes</label>
          <textarea
            id="complete-dialog-notes"
            className="min-h-[120px] w-full rounded-lg border border-slate-300 px-3 py-2 text-sm outline-none transition focus:border-[var(--app-primary)]"
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
            placeholder="Describe the work performed"
            autoFocus
          />
        </div>

        <div className="mt-6 flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="button"
            disabled={!notes.trim() || submitting}
            onClick={() =>
              onSubmit({ workDoneNotes: notes.trim() })
            }
            className="inline-flex h-11 items-center justify-center rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:bg-slate-300"
          >
            Mark complete
          </button>
        </div>
      </div>
    </div>
  )
}
