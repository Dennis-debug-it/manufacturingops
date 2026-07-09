import type { ReactNode } from 'react'
import { WidgetToolbar } from './WidgetToolbar'

export function WidgetCard({
  title,
  description,
  actions,
  children,
  className = '',
  showDragHandle = true,
}: {
  title: string
  description?: string
  actions?: ReactNode
  children: ReactNode
  className?: string
  showDragHandle?: boolean
  }) {
  return (
    <article className={`widget-card flex h-full min-h-0 flex-col ${className}`.trim()}>
      <WidgetToolbar title={title} description={description} actions={actions} showDragHandle={showDragHandle} />
      <div className="widget-card-body min-h-0 flex-1">{children}</div>
    </article>
  )
}
