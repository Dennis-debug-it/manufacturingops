import { ShieldAlert } from 'lucide-react'

/**
 * The completion escape hatch. `WorkOrdersController.Complete` rejects completion when no
 * technician arrival is on record unless the payload carries `adminOverride: true`, the caller is
 * a tenant admin, and `overrideReason` is non-empty — all three, or the request 400s. Rendering it
 * as a deliberate opt-in with a mandatory reason keeps the override an exception rather than the
 * default path.
 */
export interface CompletionOverrideFieldsProps {
  idPrefix: string
  enabled: boolean
  reason: string
  onEnabledChange: (enabled: boolean) => void
  onReasonChange: (reason: string) => void
}

export function CompletionOverrideFields({ idPrefix, enabled, reason, onEnabledChange, onReasonChange }: CompletionOverrideFieldsProps) {
  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50 p-3">
      <div className="flex items-start gap-2">
        <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium text-amber-900">No arrival recorded for this work order</p>
          <p className="mt-1 text-xs leading-5 text-amber-800">
            Completion normally requires a technician arrival on site. As an admin you can override that, and the reason is
            written to the work order audit trail.
          </p>
          <label className="mt-3 flex items-center gap-2 text-sm font-medium text-amber-900">
            <input
              id={`${idPrefix}-admin-override`}
              type="checkbox"
              checked={enabled}
              onChange={(event) => onEnabledChange(event.target.checked)}
              className="h-4 w-4 rounded border-amber-300"
            />
            Complete without arrival (admin override)
          </label>
          {enabled ? (
            <div className="mt-3">
              <label className="mb-1.5 block text-xs font-medium text-amber-900" htmlFor={`${idPrefix}-override-reason`}>
                Override reason (required)
              </label>
              <textarea
                id={`${idPrefix}-override-reason`}
                value={reason}
                onChange={(event) => onReasonChange(event.target.value)}
                rows={2}
                placeholder="Why is this being completed without a recorded arrival?"
                className="w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-sm outline-none focus:border-amber-500"
              />
            </div>
          ) : null}
        </div>
      </div>
    </div>
  )
}
