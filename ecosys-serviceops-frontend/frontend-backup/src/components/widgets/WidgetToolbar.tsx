import { GripVertical, RotateCcw } from 'lucide-react'
import type { ReactNode } from 'react'

export function WidgetToolbar({
  title,
  description,
  actions,
  showDragHandle = true,
}: {
  title: string
  description?: string
  actions?: ReactNode
  showDragHandle?: boolean
}) {
  return (
    <div className="widget-card-header flex items-start justify-between gap-3">
      <div className="min-w-0">
        <div className="flex min-w-0 items-center gap-2">
          {showDragHandle ? (
            <span
              className="widget-drag-handle inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--app-subtle)] text-muted transition hover:bg-[var(--app-primary-soft)] hover:text-app"
              aria-hidden="true"
            >
              <GripVertical className="size-3.5" />
            </span>
          ) : null}
          <h3 className="truncate font-heading text-[0.95rem] font-semibold tracking-[-0.02em] text-app sm:text-[1.02rem]">{title}</h3>
        </div>
        {description ? <p className="mt-1 text-xs leading-5 text-muted">{description}</p> : null}
      </div>
      {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
    </div>
  )
}

export function ResetLayoutButton({
  onClick,
  label = 'Reset layout',
}: {
  onClick: () => void
  label?: string
}) {
  return (
    <button type="button" className="button-secondary h-9 px-3 py-2 text-xs" onClick={onClick}>
      <RotateCcw className="h-3.5 w-3.5" />
      {label}
    </button>
  )
}
