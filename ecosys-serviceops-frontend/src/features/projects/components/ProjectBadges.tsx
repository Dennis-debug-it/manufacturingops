import { Badge, type BadgeTone } from '../../../shared/components/Badge'
import { cn } from '../../../shared/utils/cn'
import type { StageCategory } from '../types'

const stageTones: Record<StageCategory, BadgeTone> = {
  Planning: 'info',
  Active: 'success',
  OnHold: 'warning',
  Handover: 'info',
  Completed: 'success',
  Closed: 'neutral',
  Cancelled: 'danger',
}

/** Stage names are tenant-defined, so the colour comes from the stage's category. */
export function StageBadge({ name, category }: { name: string; category: StageCategory | string }) {
  return <Badge tone={stageTones[category as StageCategory] ?? 'neutral'}>{name}</Badge>
}

/** Actual progress as a bar, with a tick where the plan says the project should be by now. */
export function ProgressBar({ actual, planned, behind, compact = false }: { actual: number; planned: number; behind: boolean; compact?: boolean }) {
  const clamp = (value: number) => Math.max(0, Math.min(100, value))
  return (
    <div className={cn('min-w-[7rem]', compact ? 'space-y-1' : 'space-y-1.5')}>
      <div className="flex items-center justify-between text-xs">
        <span className={cn('font-semibold', behind ? 'text-amber-700' : 'text-slate-900')}>{Math.round(actual)}%</span>
        <span className="text-slate-500">plan {Math.round(planned)}%</span>
      </div>
      <div
        className={cn('relative w-full overflow-hidden rounded-full bg-slate-200', compact ? 'h-1.5' : 'h-2.5')}
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(actual)}
        aria-label={`Progress ${Math.round(actual)}%, planned ${Math.round(planned)}%`}
      >
        <div className={cn('h-full rounded-full', behind ? 'bg-amber-500' : 'bg-[var(--app-primary)]')} style={{ width: `${clamp(actual)}%` }} />
        <div className="absolute inset-y-0 w-0.5 bg-slate-700/60" style={{ left: `${clamp(planned)}%` }} />
      </div>
    </div>
  )
}
