import type { ReactNode } from 'react'
import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { cn } from '../../../shared/utils/cn'
import { useOrgNodes } from '../api'
import { buildTree, canHoldAssets, flatten } from '../tree'
import { LEVEL_LABELS, type Criticality, type OperationalStatus } from '../types'

export const inputClass = 'h-10 w-full rounded-lg border border-slate-200 bg-white px-3 text-sm text-slate-800 outline-none focus:border-[var(--app-primary)] disabled:bg-slate-50 disabled:text-slate-400'
export const textareaClass = 'min-h-20 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-slate-800 outline-none focus:border-[var(--app-primary)]'
export const primaryButton = 'inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-[var(--app-primary)] px-4 text-sm font-medium text-white transition hover:bg-[var(--app-secondary)] disabled:cursor-not-allowed disabled:opacity-60'
export const secondaryButton = 'inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-sm font-medium text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60'
export const dangerButton = 'inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-rose-200 bg-white px-4 text-sm font-medium text-rose-700 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60'

export function Field({ label, hint, required, children, className }: { label: string; hint?: string | undefined; required?: boolean | undefined; children: ReactNode; className?: string | undefined }) {
  return (
    <label className={cn('block space-y-1', className)}>
      <span className="text-sm font-medium text-slate-700">{label}{required ? <span className="text-rose-600"> *</span> : null}</span>
      {children}
      {hint ? <span className="block text-xs text-slate-500">{hint}</span> : null}
    </label>
  )
}

const CRITICALITY_TONE: Record<Criticality, BadgeTone> = { A: 'danger', B: 'warning', C: 'neutral' }

export function CriticalityBadge({ value }: { value: Criticality | null | undefined }) {
  if (!value) return <span className="text-slate-400">—</span>
  return <Badge tone={CRITICALITY_TONE[value]} size="sm">{value}</Badge>
}

const STATUS_TONE: Record<OperationalStatus, BadgeTone> = { Operational: 'success', Standby: 'info', Down: 'danger' }

export function OperationalBadge({ value, retired }: { value: OperationalStatus; retired?: boolean | undefined }) {
  if (retired) return <Badge tone="neutral" size="sm">Retired</Badge>
  return <Badge tone={STATUS_TONE[value] ?? 'neutral'} size="sm">{value}</Badge>
}

/**
 * Picks a factory, section or sub-section. Groups and places outside the user's access are
 * shown for orientation but can't be chosen.
 */
export function LocationSelect({ value, onChange, allowEmpty, emptyLabel = 'Choose a location', id, includeGroups }: {
  value: string
  onChange: (value: string) => void
  allowEmpty?: boolean | undefined
  emptyLabel?: string | undefined
  id?: string | undefined
  /** For filters: lets a group be chosen too. */
  includeGroups?: boolean | undefined
}) {
  const { data } = useOrgNodes()
  const rows = flatten(buildTree(data ?? []))
  return (
    <select id={id} value={value} onChange={(event) => onChange(event.target.value)} className={inputClass}>
      {allowEmpty !== false ? <option value="">{emptyLabel}</option> : null}
      {rows.map((node) => (
        <option key={node.id} value={node.id} disabled={!(includeGroups ? node.canAccess : canHoldAssets(node))}>
          {`${'   '.repeat(node.depth)}${node.name} (${node.code})${node.level === 'Group' ? ` · ${LEVEL_LABELS.Group}` : ''}`}
        </option>
      ))}
    </select>
  )
}

export function formatNumber(value: number | null | undefined, unit?: string) {
  if (value === null || value === undefined) return '—'
  const text = value.toLocaleString(undefined, { maximumFractionDigits: 2 })
  return unit ? `${text} ${unit}` : text
}
