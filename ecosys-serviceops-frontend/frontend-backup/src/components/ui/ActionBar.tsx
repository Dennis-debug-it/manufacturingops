import { Children, type ReactNode } from 'react'

function normalizeChildren(children?: ReactNode) {
  if (!children) {
    return []
  }

  return Children.toArray(children)
}

export function ActionBar({
  primaryAction,
  secondaryActions,
  className = '',
}: {
  primaryAction?: ReactNode
  secondaryActions?: ReactNode
  className?: string
}) {
  const primary = normalizeChildren(primaryAction)
  const secondary = normalizeChildren(secondaryActions)

  if (primary.length === 0 && secondary.length === 0) {
    return null
  }

  return (
    <div className={`action-bar ${className}`.trim()}>
      {secondary.length > 0 ? <div className="action-bar-secondary">{secondary}</div> : null}
      {primary.length > 0 ? <div className="action-bar-primary">{primary}</div> : null}
    </div>
  )
}
