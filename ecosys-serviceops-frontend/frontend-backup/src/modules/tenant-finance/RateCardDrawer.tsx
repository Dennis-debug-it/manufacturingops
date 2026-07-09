import { useState } from 'react'
import { Drawer } from '../../components/ui/Drawer'
import { useToast } from '../../components/ui/ToastProvider'
import { tenantFinanceService } from '../../services/tenantFinanceService'
import type { RateCardRecord, UpsertRateCardInput } from '../../types/api'

const CATEGORIES = ['Labour', 'Callout', 'Service', 'Transport', 'Material', 'Other'] as const
const TIME_TYPES = ['Standard', 'Overtime', 'Emergency', 'Weekend', 'Holiday'] as const
const SKILL_LEVELS = ['Junior', 'Mid', 'Senior', 'Specialist'] as const
const UNITS = ['hour', 'visit', 'km', 'unit', 'day', 'job'] as const

const emptyForm: UpsertRateCardInput = {
  name: '',
  category: 'Labour',
  description: '',
  unitRate: 0,
  unit: 'hour',
  skillLevel: null,
  timeType: 'Standard',
  isDefault: false,
  taxRate: null,
}

function toForm(card: RateCardRecord): UpsertRateCardInput {
  return {
    name: card.name,
    category: card.category,
    description: card.description ?? '',
    unitRate: card.unitRate,
    unit: card.unit,
    skillLevel: card.skillLevel ?? null,
    timeType: card.timeType,
    isDefault: card.isDefault,
    taxRate: card.taxRate ?? null,
  }
}

interface Props {
  open: boolean
  editing: RateCardRecord | null
  onClose: () => void
  onSaved: () => void
}

export function RateCardDrawer({ open, editing, onClose, onSaved }: Props) {
  const { pushToast } = useToast()
  const [form, setForm] = useState<UpsertRateCardInput>(() => editing ? toForm(editing) : emptyForm)
  const [saving, setSaving] = useState(false)

  // Sync form when editing prop changes
  if (open && editing && form.name !== editing.name && form.unitRate !== editing.unitRate) {
    setForm(toForm(editing))
  }

  function field<K extends keyof UpsertRateCardInput>(key: K, value: UpsertRateCardInput[K]) {
    setForm((prev) => ({ ...prev, [key]: value }))
  }

  async function handleSave() {
    if (!form.name.trim()) {
      pushToast({ title: 'Name required', description: 'Please enter a name for this rate card.', tone: 'danger' })
      return
    }
    setSaving(true)
    try {
      const payload: UpsertRateCardInput = {
        ...form,
        name: form.name.trim(),
        description: form.description?.trim() || null,
        skillLevel: form.skillLevel || null,
        taxRate: form.taxRate !== null && form.taxRate !== undefined && String(form.taxRate) !== '' ? form.taxRate : null,
      }
      if (editing) {
        await tenantFinanceService.updateRateCard(editing.id, payload)
        pushToast({ title: 'Rate card updated', description: `"${payload.name}" was saved.`, tone: 'success' })
      } else {
        await tenantFinanceService.createRateCard(payload)
        pushToast({ title: 'Rate card created', description: `"${payload.name}" is now available for billing.`, tone: 'success' })
      }
      onSaved()
      onClose()
    } catch (err) {
      pushToast({ title: 'Save failed', description: err instanceof Error ? err.message : 'Unable to save rate card.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  return (
    <Drawer
      open={open}
      title={editing ? 'Edit rate card' : 'Add rate card'}
      description="Rate cards define the unit prices applied when auto-populating billable items on work orders."
      onClose={onClose}
    >
      <div className="space-y-5">
        {/* Name */}
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium text-app" htmlFor="rc-name">Name <span className="text-danger">*</span></label>
          <input
            id="rc-name"
            type="text"
            className="input-field"
            placeholder="e.g. Standard Labour Rate"
            value={form.name}
            onChange={(e) => field('name', e.target.value)}
          />
        </div>

        {/* Category + Time Type */}
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app" htmlFor="rc-category">Category</label>
            <select id="rc-category" className="input-field" value={form.category} onChange={(e) => field('category', e.target.value)}>
              {CATEGORIES.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app" htmlFor="rc-timetype">Time Type</label>
            <select id="rc-timetype" className="input-field" value={form.timeType} onChange={(e) => field('timeType', e.target.value)}>
              {TIME_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
        </div>

        {/* Skill Level */}
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium text-app" htmlFor="rc-skill">Skill Level</label>
          <select
            id="rc-skill"
            className="input-field"
            value={form.skillLevel ?? ''}
            onChange={(e) => field('skillLevel', e.target.value || null)}
          >
            <option value="">Any skill level</option>
            {SKILL_LEVELS.map((s) => <option key={s} value={s}>{s}</option>)}
          </select>
        </div>

        {/* Unit Rate + Unit */}
        <div className="grid grid-cols-2 gap-4">
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app" htmlFor="rc-rate">Unit Rate (KES) <span className="text-danger">*</span></label>
            <input
              id="rc-rate"
              type="number"
              min="0"
              step="0.01"
              className="input-field"
              value={form.unitRate}
              onChange={(e) => field('unitRate', parseFloat(e.target.value) || 0)}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label className="text-sm font-medium text-app" htmlFor="rc-unit">Unit</label>
            <select id="rc-unit" className="input-field" value={form.unit} onChange={(e) => field('unit', e.target.value)}>
              {UNITS.map((u) => <option key={u} value={u}>{u}</option>)}
            </select>
          </div>
        </div>

        {/* Tax Rate */}
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium text-app" htmlFor="rc-tax">Tax Rate (%)</label>
          <input
            id="rc-tax"
            type="number"
            min="0"
            max="100"
            step="0.01"
            className="input-field"
            placeholder="Use tenant default"
            value={form.taxRate ?? ''}
            onChange={(e) => field('taxRate', e.target.value === '' ? null : parseFloat(e.target.value))}
          />
          <p className="text-xs text-muted">Leave blank to use the tenant default tax rate (16% VAT).</p>
        </div>

        {/* Description */}
        <div className="flex flex-col gap-1.5">
          <label className="text-sm font-medium text-app" htmlFor="rc-desc">Description</label>
          <textarea
            id="rc-desc"
            className="input-field min-h-[80px] resize-none"
            placeholder="Optional notes about this rate card…"
            value={form.description ?? ''}
            onChange={(e) => field('description', e.target.value)}
          />
        </div>

        {/* Is Default toggle */}
        <div className="flex items-center gap-3">
          <button
            type="button"
            role="switch"
            aria-checked={form.isDefault}
            className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors ${form.isDefault ? 'bg-accent' : 'bg-app-subtle'}`}
            onClick={() => field('isDefault', !form.isDefault)}
          >
            <span className={`pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow ring-0 transition-transform ${form.isDefault ? 'translate-x-5' : 'translate-x-0'}`} />
          </button>
          <span className="text-sm font-medium text-app">Default rate for category</span>
        </div>

        {/* Actions */}
        <div className="flex gap-3 pt-2">
          <button type="button" className="button-primary flex-1" onClick={handleSave} disabled={saving}>
            {saving ? 'Saving…' : editing ? 'Save changes' : 'Create rate card'}
          </button>
          <button type="button" className="button-secondary" onClick={onClose}>Cancel</button>
        </div>
      </div>
    </Drawer>
  )
}
