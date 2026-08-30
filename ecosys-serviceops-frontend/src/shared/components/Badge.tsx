import type { ReactNode } from 'react'
import { cn } from '../utils/cn'

export type BadgeTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

const toneClasses: Record<BadgeTone, string> = {
  success: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  warning: 'bg-amber-50 text-amber-700 ring-amber-200',
  danger: 'bg-rose-50 text-rose-700 ring-rose-200',
  info: 'bg-sky-50 text-sky-700 ring-sky-200',
  neutral: 'bg-slate-100 text-slate-700 ring-slate-200',
}

const sizeClasses = {
  md: 'px-2.5 py-1 text-xs',
  sm: 'px-2 py-0.5 text-[11px]',
} as const

export interface BadgeProps {
  tone?: BadgeTone
  size?: keyof typeof sizeClasses
  dotClassName?: string
  className?: string
  children: ReactNode
}

// Shared pill primitive behind StatusBadge/SlaBadge/PriorityBadge. Status semantics stay
// on Tailwind's built-in emerald/amber/rose/sky/slate scale, not the teal/lime brand
// palette, so status stays instantly recognizable regardless of the surrounding theme.
export function Badge({ tone = 'neutral', size = 'md', dotClassName, className, children }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full font-medium ring-1 ring-inset',
        sizeClasses[size],
        toneClasses[tone],
        className,
      )}
    >
      {dotClassName ? <span className={cn('h-1.5 w-1.5 rounded-full', dotClassName)} /> : null}
      {children}
    </span>
  )
}
