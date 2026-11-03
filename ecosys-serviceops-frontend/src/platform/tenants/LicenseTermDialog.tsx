import { useEffect, useState } from 'react'
import { ConfirmDialog } from '../../shared/components/ConfirmDialog'
import { useLicensePlans, type TenantLicenseTermInput } from '../api'

const BILLING_CYCLES = ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual', 'Custom'] as const

const TERM_SHORTCUTS = [
  { label: '1 month', months: 1 },
  { label: '3 months', months: 3 },
  { label: '6 months', months: 6 },
  { label: '1 year', months: 12 },
  { label: '2 years', months: 24 },
] as const

function billingCycleLabel(cycle: string): string {
  return cycle === 'SemiAnnual' ? 'Semi-Annual' : cycle
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

function addMonthsIso(iso: string, months: number): string {
  const date = iso ? new Date(iso) : new Date()
  date.setUTCMonth(date.getUTCMonth() + months)
  return date.toISOString().slice(0, 10)
}

export interface LicenseTermDialogProps {
  open: boolean
  title: string
  tenantName: string
  confirmLabel: string
  onCancel: () => void
  onConfirm: (term: TenantLicenseTermInput) => void
  submitting?: boolean
  /** Preselects the tenant's current plan; the admin can still change it. */
  currentPlanId?: string | null
}

/**
 * Captures an explicit license term (Phase 4/6 of the licensing cleanup): plan, billing frequency,
 * start and expiry dates (with shortcuts that compute a date rather than persisting a duration), next
 * billing date, grace period, and — as a deliberate, clearly-labelled exception — a perpetual/
 * internal license flag. Shared between Activate and Renew, which need the identical field set.
 */
export function LicenseTermDialog({ open, title, tenantName, confirmLabel, onCancel, onConfirm, submitting, currentPlanId }: LicenseTermDialogProps) {
  const { data: plans } = useLicensePlans()
  const [planId, setPlanId] = useState<string>('')
  const [startsAt, setStartsAt] = useState(todayIso())
  const [expiresAt, setExpiresAt] = useState(() => addMonthsIso(todayIso(), 12))
  const [isPerpetual, setIsPerpetual] = useState(false)
  const [billingCycle, setBillingCycle] = useState<string>('Annual')
  const [nextBillingDate, setNextBillingDate] = useState(() => addMonthsIso(todayIso(), 3))
  const [gracePeriodDays, setGracePeriodDays] = useState(7)

  useEffect(() => {
    if (!open) return
    const start = todayIso()
    setPlanId(currentPlanId ?? '')
    setStartsAt(start)
    setExpiresAt(addMonthsIso(start, 12))
    setIsPerpetual(false)
    setBillingCycle('Annual')
    setNextBillingDate(addMonthsIso(start, 3))
    setGracePeriodDays(7)
    // currentPlanId is read only at the moment the dialog opens, matching every other field here.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open])

  if (!open) {
    return null
  }

  const canConfirm = isPerpetual || Boolean(expiresAt)
  const selectedPlanName = plans?.find((plan) => plan.id === planId)?.name

  return (
    <ConfirmDialog
      open={open}
      title={title}
      message={selectedPlanName ? `${tenantName} — ${selectedPlanName}` : tenantName}
      confirmLabel={confirmLabel}
      confirmDisabled={!canConfirm || Boolean(submitting)}
      onCancel={onCancel}
      onConfirm={() =>
        onConfirm({
          startsAt: new Date(startsAt).toISOString(),
          expiresAt: isPerpetual ? null : new Date(expiresAt).toISOString(),
          isPerpetual,
          billingCycle,
          nextBillingDate: nextBillingDate ? new Date(nextBillingDate).toISOString() : null,
          gracePeriodDays,
          planId: planId || null,
        })
      }
    >
      <div className="mt-4 max-h-[60vh] space-y-4 overflow-y-auto pr-1">
        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Plan</label>
          <select
            value={planId}
            onChange={(event) => setPlanId(event.target.value)}
            className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          >
            <option value="">Keep current plan</option>
            {(plans ?? []).filter((plan) => plan.isActive).map((plan) => (
              <option key={plan.id} value={plan.id}>{plan.name}</option>
            ))}
          </select>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Start date</label>
            <input
              type="date"
              value={startsAt}
              onChange={(event) => setStartsAt(event.target.value)}
              className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Expiry date</label>
            <input
              type="date"
              value={expiresAt}
              disabled={isPerpetual}
              onChange={(event) => setExpiresAt(event.target.value)}
              className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50 disabled:text-slate-400"
            />
          </div>
        </div>

        <div className="flex flex-wrap gap-2">
          {TERM_SHORTCUTS.map((shortcut) => (
            <button
              key={shortcut.label}
              type="button"
              disabled={isPerpetual}
              onClick={() => setExpiresAt(addMonthsIso(startsAt, shortcut.months))}
              className="rounded-lg border border-slate-200 bg-white px-2.5 py-1 text-xs font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {shortcut.label}
            </button>
          ))}
        </div>

        <label className="flex items-start gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={isPerpetual}
            onChange={(event) => setIsPerpetual(event.target.checked)}
            className="mt-0.5 h-4 w-4 rounded border-slate-300"
          />
          <span>Perpetual / internal license — never expires. Platform-admin decision only; not the default.</span>
        </label>

        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Billing frequency</label>
            <select
              value={billingCycle}
              onChange={(event) => setBillingCycle(event.target.value)}
              className="h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
            >
              {BILLING_CYCLES.map((cycle) => (
                <option key={cycle} value={cycle}>{billingCycleLabel(cycle)}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-slate-700">Next billing date</label>
            <input
              type="date"
              value={nextBillingDate}
              onChange={(event) => setNextBillingDate(event.target.value)}
              className="h-10 w-full rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
            />
          </div>
        </div>

        <div>
          <label className="mb-1.5 block text-sm font-medium text-slate-700">Grace period (days)</label>
          <input
            type="number"
            min={0}
            value={gracePeriodDays}
            onChange={(event) => setGracePeriodDays(Number(event.target.value))}
            className="h-10 w-32 rounded-lg border border-slate-300 px-3 text-sm outline-none focus:border-[var(--app-primary)]"
          />
        </div>

        <div className="space-y-1 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2.5 text-xs text-slate-600">
          <p><span className="font-medium text-slate-700">Plan:</span> {selectedPlanName ?? 'Current plan (unchanged)'}</p>
          <p><span className="font-medium text-slate-700">License term:</span> {startsAt} – {isPerpetual ? 'Never (perpetual)' : expiresAt || '(not set)'}</p>
          <p><span className="font-medium text-slate-700">Billing:</span> {billingCycleLabel(billingCycle)}{nextBillingDate ? ` · Next billing ${nextBillingDate}` : ''}</p>
          <p><span className="font-medium text-slate-700">Grace period:</span> {gracePeriodDays} days</p>
        </div>
      </div>
    </ConfirmDialog>
  )
}
