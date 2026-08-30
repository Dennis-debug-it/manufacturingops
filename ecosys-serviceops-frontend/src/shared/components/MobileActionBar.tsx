import type { ReactNode } from 'react'
import { cn } from '../utils/cn'

export interface MobileActionBarAction {
  label: string
  onClick: () => void
  primary?: boolean
  icon?: ReactNode
}

export interface MobileActionBarProps {
  actions: MobileActionBarAction[]
  className?: string
}

export function MobileActionBar({ actions, className }: MobileActionBarProps) {
  if (actions.length === 0) {
    return null
  }

  return (
    <div className={cn('fixed inset-x-0 bottom-0 z-30 border-t border-slate-200 bg-white shadow-[0_-1px_3px_rgba(15,23,42,0.06)] px-4 py-3 md:hidden', className)}>
      <div className="grid grid-cols-2 gap-2">
        {actions.map((action) => (
          <button
            key={action.label}
            type="button"
            onClick={action.onClick}
            className={cn(
              'inline-flex h-12 items-center justify-center gap-2 rounded-lg border text-sm font-medium transition',
              action.primary
                ? 'border-[var(--app-primary)] bg-[var(--app-primary)] text-white hover:bg-[var(--app-secondary)]'
                : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50',
            )}
          >
            {action.icon}
            {action.label}
          </button>
        ))}
      </div>
    </div>
  )
}
