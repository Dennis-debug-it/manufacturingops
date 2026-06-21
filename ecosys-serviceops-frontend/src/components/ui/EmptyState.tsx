import { Sparkles } from 'lucide-react'
import type { ReactNode } from 'react'

export function EmptyState({
  title,
  description,
  actionLabel,
  onAction,
  action,
}: {
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
  action?: ReactNode
}) {
  return (
    <div className="empty-state">
      <div className="icon-accent rounded-[16px] p-3.5">
        <Sparkles className="h-6 w-6" />
      </div>
      <h3 className="mt-4 font-heading text-lg font-semibold text-app">{title}</h3>
      <p className="mt-2 max-w-xl text-sm leading-6 text-muted">{description}</p>
      {action ? <div className="mt-5">{action}</div> : null}
      {!action && actionLabel && onAction ? (
        <button type="button" className="button-secondary mt-5" onClick={onAction}>
          {actionLabel}
        </button>
      ) : null}
    </div>
  )
}
