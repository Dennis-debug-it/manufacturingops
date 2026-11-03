import { useEffect, useState } from 'react'
import { X } from 'lucide-react'
import { CompletionOverrideFields } from './CompletionOverrideFields'

export interface CompleteWorkOrderSubmission {
  workDoneNotes: string
  adminOverride: boolean
  overrideReason: string | null
}

export interface CompleteWorkOrderDialogProps {
  open: boolean
  woNumber?: string | undefined
  submitting?: boolean
  /**
   * True once a technician arrival is on record. When false the server refuses completion unless
   * the payload carries an admin override, so the dialog asks for one instead of firing a request
   * that is guaranteed to 400.
   */
  arrivalRecorded: boolean
  /** Whether the signed-in user holds the tenant `admin` role the server requires for an override. */
  canAdminOverride: boolean
  /** Work-done notes already recorded during execution, used to prefill the dialog. */
  initialNotes?: string | null | undefined
  onClose: () => void
  onSubmit: (submission: CompleteWorkOrderSubmission) => void
}

export function CompleteWorkOrderDialog({
  open,
  woNumber,
  submitting,
  arrivalRecorded,
  canAdminOverride,
  initialNotes,
  onClose,
  onSubmit,
}: CompleteWorkOrderDialogProps) {
  const [notes, setNotes] = useState('')
  const [overrideEnabled, setOverrideEnabled] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')

  useEffect(() => {
    if (open) {
      setNotes(initialNotes ?? '')
    } else {
      setNotes('')
      setOverrideEnabled(false)
      setOverrideReason('')
    }
  }, [open, initialNotes])

  if (!open) {
    return null
  }

  const overrideOffered = canAdminOverride && !arrivalRecorded
  // Without an arrival the server needs all three of adminOverride, the admin role and a reason,
  // so the submit stays disabled until the operator has deliberately supplied the first and third.
  const overrideSatisfied = arrivalRecorded || (overrideEnabled && overrideReason.trim().length > 0)

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

        {overrideOffered ? (
          <div className="mt-4">
            <CompletionOverrideFields
              idPrefix="complete-dialog"
              enabled={overrideEnabled}
              reason={overrideReason}
              onEnabledChange={setOverrideEnabled}
              onReasonChange={setOverrideReason}
            />
          </div>
        ) : null}

        <div className="mt-6 flex items-center justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50">
            Cancel
          </button>
          <button
            type="button"
            disabled={!notes.trim() || !overrideSatisfied || submitting}
            onClick={() =>
              onSubmit({
                workDoneNotes: notes.trim(),
                adminOverride: overrideOffered && overrideEnabled,
                overrideReason: overrideOffered && overrideEnabled ? overrideReason.trim() : null,
              })
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
