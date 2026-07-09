import type { ReactNode } from 'react'

export function DataTableWrapper({
  children,
  className = '',
  variant = 'open',
}: {
  children: ReactNode
  className?: string
  variant?: 'open' | 'framed'
}) {
  return <div className={`data-table-wrapper ${variant === 'framed' ? 'data-table-wrapper-framed' : ''} ${className}`.trim()}>{children}</div>
}
