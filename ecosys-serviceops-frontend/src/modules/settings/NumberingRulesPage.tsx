import { AlertTriangle, Eye, RotateCcw, Save } from 'lucide-react'
import { useState, useCallback } from 'react'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { settingsService } from '../../services/settingsService'
import type { NumberingRuleRecord } from '../../types/api'

// ─── Local types ──────────────────────────────────────────────────────────────

interface NumberingForm {
  prefix: string
  suffix: string
  paddingLength: number
  includeYear: boolean
  includeMonth: boolean
  resetPeriod: string        // 'Never' | 'Yearly' | 'Monthly'
  yearFormat: string         // 'YYYY' | 'YY'
  separator: string
  isActive: boolean
}

// ─── Preview formula (mirrors backend DocumentNumberingService.Format) ─────────

function computePreview(form: NumberingForm, sequence: number): string {
  const sep = form.separator || '-'
  const now = new Date()
  const parts: string[] = []

  if (form.prefix.trim()) parts.push(form.prefix.trim().toUpperCase())

  if (form.includeYear) {
    parts.push(
      form.yearFormat === 'YY'
        ? now.getFullYear().toString().slice(2)
        : now.getFullYear().toString(),
    )
  }

  if (form.includeMonth) {
    parts.push(String(now.getMonth() + 1).padStart(2, '0'))
  }

  parts.push(String(sequence).padStart(form.paddingLength, '0'))

  if (form.suffix.trim()) parts.push(form.suffix.trim().toUpperCase())

  return parts.join(sep)
}

function ruleToForm(rule: NumberingRuleRecord): NumberingForm {
  return {
    prefix: rule.prefix ?? 'WO',
    suffix: rule.suffix ?? '',
    paddingLength: rule.paddingLength ?? 6,
    includeYear: rule.includeYear ?? false,
    includeMonth: rule.includeMonth ?? false,
    resetPeriod: rule.resetPeriod ?? 'Never',
    yearFormat: rule.yearFormat ?? 'YYYY',
    separator: rule.separator ?? '-',
    isActive: rule.isActive ?? true,
  }
}

// ─── Sub-components ───────────────────────────────────────────────────────────

function FieldRow({
  label,
  description,
  children,
}: {
  label: string
  description?: string
  children: React.ReactNode
}) {
  return (
    <div className="flex items-center justify-between gap-6 py-4 border-b border-app last:border-0">
      <div className="min-w-0">
        <p className="text-sm font-semibold text-app">{label}</p>
        {description ? <p className="mt-0.5 text-xs text-muted">{description}</p> : null}
      </div>
      <div className="shrink-0">{children}</div>
    </div>
  )
}

function Toggle({
  checked,
  onChange,
  disabled,
}: {
  checked: boolean
  onChange: (v: boolean) => void
  disabled?: boolean
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      disabled={disabled}
      onClick={() => !disabled && onChange(!checked)}
      className={[
        'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400',
        checked ? 'bg-cyan-500' : 'bg-neutral-600',
        disabled ? 'opacity-40 cursor-not-allowed' : 'cursor-pointer',
      ].join(' ')}
    >
      <span
        className={[
          'inline-block h-4 w-4 rounded-full bg-white shadow-sm transition-transform',
          checked ? 'translate-x-6' : 'translate-x-1',
        ].join(' ')}
      />
    </button>
  )
}

function PreviewBadge({ value }: { value: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl bg-cyan-500/10 border border-cyan-500/20 px-5 py-4">
      <Eye className="h-4 w-4 text-cyan-400 shrink-0" />
      <span className="font-mono text-2xl font-bold tracking-wider text-cyan-300">{value}</span>
    </div>
  )
}

// ─── Reset Sequence Modal ──────────────────────────────────────────────────────

function ResetDrawer({
  open,
  onClose,
  onConfirm,
  saving,
}: {
  open: boolean
  onClose: () => void
  onConfirm: (startFrom: number) => void
  saving: boolean
}) {
  const [startFrom, setStartFrom] = useState(1)

  const handleConfirm = useCallback(() => {
    onConfirm(startFrom)
  }, [onConfirm, startFrom])

  return (
    <Drawer
      open={open}
      title="Reset sequence"
      description="Set the next number to be issued for Work Order documents."
      onClose={onClose}
    >
      <div className="space-y-6">
        <div className="panel-subtle rounded-2xl p-4 flex gap-3">
          <AlertTriangle className="h-5 w-5 text-amber-400 shrink-0 mt-0.5" />
          <div>
            <p className="text-sm font-semibold text-app">This cannot be undone</p>
            <p className="mt-1 text-sm text-muted">
              Resetting the sequence changes the next number issued. Numbers already issued are
              not affected, but duplicates may occur if you reset to a value already in use.
            </p>
          </div>
        </div>

        <label className="block space-y-2">
          <span className="text-sm font-semibold text-app">New start value</span>
          <input
            type="number"
            min={1}
            value={startFrom}
            onChange={(e) => setStartFrom(Math.max(1, Number(e.target.value) || 1))}
            className="field-input w-full"
          />
          <p className="text-xs text-muted">
            The very next work order created will receive this sequence number.
          </p>
        </label>

        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="button-secondary" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button
            type="button"
            className="button-danger"
            onClick={handleConfirm}
            disabled={saving}
          >
            <RotateCcw className="h-4 w-4" />
            {saving ? 'Resetting…' : 'Reset sequence'}
          </button>
        </div>
      </div>
    </Drawer>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────

export function WorkOrderNumberingPage() {
  const { pushToast } = useToast()

  const {
    data: rules,
    loading,
    error,
    reload,
    setData,
  } = useAsyncData<NumberingRuleRecord[]>(
    (signal) => settingsService.listNumberingRules(signal),
    [],
    [],
  )

  // Filter to the Work Order rule (tenant-level, no branch)
  const woRule = rules.find((r) => r.documentType === 'WorkOrder') ?? null

  const [form, setForm] = useState<NumberingForm | null>(null)
  const [resetOpen, setResetOpen] = useState(false)
  const [resetting, setResetting] = useState(false)
  const [saving, setSaving] = useState(false)

  // Initialise form once rule is loaded
  const currentForm: NumberingForm = form ?? (woRule ? ruleToForm(woRule) : {
    prefix: 'WO',
    suffix: '',
    paddingLength: 6,
    includeYear: false,
    includeMonth: false,
    resetPeriod: 'Never',
    yearFormat: 'YYYY',
    separator: '-',
    isActive: true,
  })

  const isDirty = form !== null
  useUnsavedChangesGuard(isDirty)

  function patch<K extends keyof NumberingForm>(key: K, value: NumberingForm[K]) {
    setForm((prev) => ({ ...(prev ?? currentForm), [key]: value }))
  }

  const preview = computePreview(currentForm, woRule?.nextNumber ?? 1)

  // ── Actions ──

  async function handleSave() {
    if (!woRule) return
    setSaving(true)
    try {
      const updated = await settingsService.updateNumberingRule(woRule.id, {
        prefix: currentForm.prefix.trim().toUpperCase() || 'WO',
        suffix: currentForm.suffix.trim().toUpperCase() || null,
        nextNumber: woRule.nextNumber,
        paddingLength: currentForm.paddingLength,
        resetPeriod: currentForm.resetPeriod,
        includeYear: currentForm.includeYear,
        includeMonth: currentForm.includeMonth,
        yearFormat: currentForm.yearFormat,
        separator: currentForm.separator || '-',
        isActive: currentForm.isActive,
      })
      setData((prev) => prev.map((r) => (r.id === updated.id ? updated : r)))
      setForm(null)
      pushToast({ title: 'Numbering rule saved', description: 'Work order numbering was updated.', tone: 'success' })
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function handleReset(startFrom: number) {
    if (!woRule) return
    setResetting(true)
    try {
      const updated = await settingsService.resetNumberingRule(woRule.id, startFrom)
      setData((prev) => prev.map((r) => (r.id === updated.id ? updated : r)))
      setResetOpen(false)
      pushToast({ title: 'Sequence reset', description: `Next work order number starts at ${startFrom}.`, tone: 'success' })
    } catch (err) {
      pushToast({ title: 'Reset failed', description: err instanceof Error ? err.message : 'Unable to reset sequence.', tone: 'danger' })
    } finally {
      setResetting(false)
    }
  }

  async function handleSeedAndLoad() {
    try {
      const seeded = await settingsService.seedDefaultNumberingRules()
      setData(seeded)
      pushToast({ title: 'Defaults seeded', description: 'Work order numbering rule was initialised.', tone: 'success' })
    } catch (err) {
      pushToast({ title: 'Seed failed', description: err instanceof Error ? err.message : 'Unable to seed defaults.', tone: 'danger' })
    }
  }

  // ── Render states ──

  if (loading && rules.length === 0) {
    return (
      <div className="space-y-4">
        <PageHeader eyebrow="Settings" title="Work Order Numbering" description="Configure how work order numbers are generated for this workspace." />
        <section className="surface-card"><LoadingState label="Loading numbering rule" /></section>
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <PageHeader eyebrow="Settings" title="Work Order Numbering" description="Configure how work order numbers are generated for this workspace." />
        <section className="surface-card"><ErrorState title="Unable to load numbering rule" description={error} /></section>
      </div>
    )
  }

  if (!woRule) {
    return (
      <div className="space-y-4">
        <PageHeader eyebrow="Settings" title="Work Order Numbering" description="Configure how work order numbers are generated for this workspace." />
        <section className="surface-card">
          <p className="text-sm font-semibold text-app">No work order numbering rule found</p>
          <p className="mt-2 text-sm text-muted">A default rule has not been created yet for this workspace.</p>
          <div className="mt-4">
            <button type="button" className="button-primary" onClick={() => void handleSeedAndLoad()}>
              Initialise default rule
            </button>
          </div>
        </section>
      </div>
    )
  }

  // ── Full page ──

  return (
    <div className="space-y-4">
      <PageHeader
        eyebrow="Settings"
        title="Work Order Numbering"
        description="Configure how work order reference numbers are generated for this workspace."
        actions={
          <button type="button" className="button-secondary" onClick={() => void reload()}>
            Refresh
          </button>
        }
      />

      {/* Live preview card */}
      <section className="surface-card">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted mb-3">
          Live preview — next number
        </p>
        <PreviewBadge value={preview} />
        {isDirty ? (
          <p className="mt-3 text-xs text-amber-400">Preview reflects unsaved changes.</p>
        ) : null}
      </section>

      {/* Format settings */}
      <section className="surface-card">
        <h2 className="text-base font-semibold text-app mb-1">Number format</h2>
        <p className="text-sm text-muted mb-2">
          Changes apply to all new work orders. Existing numbers are not affected.
        </p>

        {/* Prefix */}
        <FieldRow label="Prefix" description="Added at the start of every number. Max 6 characters.">
          <input
            value={currentForm.prefix}
            maxLength={6}
            onChange={(e) => patch('prefix', e.target.value.toUpperCase().slice(0, 6))}
            className="field-input w-32 text-center font-mono uppercase"
            placeholder="WO"
          />
        </FieldRow>

        {/* Suffix */}
        <FieldRow label="Suffix" description="Optional. Appended after the sequence number. Max 6 characters.">
          <input
            value={currentForm.suffix}
            maxLength={6}
            onChange={(e) => patch('suffix', e.target.value.toUpperCase().slice(0, 6))}
            className="field-input w-32 text-center font-mono uppercase"
            placeholder="None"
          />
        </FieldRow>

        {/* Padding */}
        <FieldRow label="Sequence padding" description="Zero-pad the sequence number to this many digits.">
          <select
            value={currentForm.paddingLength}
            onChange={(e) => patch('paddingLength', Number(e.target.value))}
            className="field-input w-36"
          >
            <option value={3}>3 digits — 001</option>
            <option value={4}>4 digits — 0001</option>
            <option value={5}>5 digits — 00001</option>
            <option value={6}>6 digits — 000001</option>
          </select>
        </FieldRow>

        {/* Separator */}
        <FieldRow label="Separator" description="Character placed between parts of the number.">
          <select
            value={currentForm.separator}
            onChange={(e) => patch('separator', e.target.value)}
            className="field-input w-36"
          >
            <option value="-">Hyphen  —  WO-001</option>
            <option value="/">Slash  —  WO/001</option>
            <option value=".">Dot  —  WO.001</option>
            <option value="">None  —  WO001</option>
          </select>
        </FieldRow>

        {/* Include year */}
        <FieldRow label="Include year" description="Embed the current year in the number — WO-2026-00001.">
          <Toggle
            checked={currentForm.includeYear}
            onChange={(v) => {
              patch('includeYear', v)
              if (!v) {
                patch('resetPeriod', 'Never')
              }
            }}
          />
        </FieldRow>

        {/* Year format — only shown when include year is on */}
        {currentForm.includeYear ? (
          <FieldRow label="Year format" description="Use 4-digit (2026) or 2-digit (26) year.">
            <select
              value={currentForm.yearFormat}
              onChange={(e) => patch('yearFormat', e.target.value)}
              className="field-input w-36"
            >
              <option value="YYYY">4-digit — 2026</option>
              <option value="YY">2-digit — 26</option>
            </select>
          </FieldRow>
        ) : null}

        {/* Include month */}
        <FieldRow label="Include month" description="Embed the current month — WO-2026-06-00001.">
          <Toggle
            checked={currentForm.includeMonth}
            onChange={(v) => {
              patch('includeMonth', v)
              if (!v && currentForm.resetPeriod === 'Monthly') {
                patch('resetPeriod', currentForm.includeYear ? 'Yearly' : 'Never')
              }
            }}
          />
        </FieldRow>
      </section>

      {/* Reset settings */}
      <section className="surface-card">
        <h2 className="text-base font-semibold text-app mb-1">Auto-reset</h2>
        <p className="text-sm text-muted mb-2">
          Automatically restart the sequence counter at the start of each year or month.
        </p>

        {/* Reset yearly */}
        <FieldRow
          label="Reset yearly"
          description="Restart the counter at the beginning of each calendar year. Requires year in number."
        >
          <Toggle
            checked={currentForm.resetPeriod === 'Yearly' || currentForm.resetPeriod === 'Monthly'}
            disabled={!currentForm.includeYear}
            onChange={(v) => {
              if (v) {
                patch('resetPeriod', 'Yearly')
              } else {
                patch('resetPeriod', 'Never')
              }
            }}
          />
        </FieldRow>

        {/* Reset monthly */}
        <FieldRow
          label="Reset monthly"
          description="Restart the counter at the beginning of each calendar month. Requires month in number."
        >
          <Toggle
            checked={currentForm.resetPeriod === 'Monthly'}
            disabled={!currentForm.includeMonth}
            onChange={(v) => {
              patch('resetPeriod', v ? 'Monthly' : currentForm.includeYear ? 'Yearly' : 'Never')
            }}
          />
        </FieldRow>
      </section>

      {/* Sequence management */}
      <section className="surface-card">
        <h2 className="text-base font-semibold text-app mb-1">Sequence</h2>
        <p className="text-sm text-muted mb-4">
          The current counter determines what number will be issued next.
        </p>

        <div className="flex items-center justify-between gap-4 panel-subtle rounded-2xl px-4 py-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">
              Next sequence
            </p>
            <p className="mt-1 font-mono text-3xl font-bold text-app">
              {woRule.nextNumber.toLocaleString()}
            </p>
            <p className="mt-1 text-xs text-muted">
              The next work order created will use this sequence position.
            </p>
          </div>
          <button
            type="button"
            className="button-secondary shrink-0"
            onClick={() => setResetOpen(true)}
            disabled={woRule.isLocked}
          >
            <RotateCcw className="h-4 w-4" />
            Reset sequence…
          </button>
        </div>

        {woRule.isLocked ? (
          <p className="mt-3 text-xs text-amber-400">
            This rule is locked by the platform and the sequence cannot be manually reset.
          </p>
        ) : null}
      </section>

      {/* Save bar */}
      <section className="surface-card">
        <div className="flex items-center justify-between gap-4">
          <div>
            {isDirty ? (
              <p className="text-sm text-amber-400 font-medium">You have unsaved changes.</p>
            ) : (
              <p className="text-sm text-muted">All changes saved.</p>
            )}
          </div>
          <div className="flex gap-3">
            {isDirty ? (
              <button
                type="button"
                className="button-secondary"
                onClick={() => setForm(null)}
                disabled={saving}
              >
                Discard
              </button>
            ) : null}
            <button
              type="button"
              className="button-primary"
              onClick={() => void handleSave()}
              disabled={saving || !isDirty}
            >
              <Save className="h-4 w-4" />
              {saving ? 'Saving…' : 'Save changes'}
            </button>
          </div>
        </div>
      </section>

      {/* Reset drawer */}
      <ResetDrawer
        open={resetOpen}
        onClose={() => setResetOpen(false)}
        onConfirm={(startFrom) => void handleReset(startFrom)}
        saving={resetting}
      />
    </div>
  )
}
