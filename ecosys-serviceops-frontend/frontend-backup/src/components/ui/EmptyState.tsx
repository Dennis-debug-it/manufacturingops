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
    <div className="empty-state max-w-2xl">
      <div className="icon-accent rounded-[14px] p-2.5">
        <Sparkles className="h-4 w-4" />
      </div>
      <h3 className="mt-3 font-heading text-base font-semibold text-app sm:text-[1.04rem]">{title}</h3>
      <p className="mt-1.5 max-w-xl text-[0.85rem] leading-5 text-muted">{description}</p>
      {action ? <div className="mt-4">{action}</div> : null}
      {!action && actionLabel && onAction ? (
        <button type="button" className="button-secondary mt-4" onClick={onAction}>
          {actionLabel}
        </button>
      ) : null}
    </div>
  )
}
