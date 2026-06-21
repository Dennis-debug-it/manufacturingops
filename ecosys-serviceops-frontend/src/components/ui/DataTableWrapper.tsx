import type { ReactNode } from 'react'

export function DataTableWrapper({
  children,
  className = '',
}: {
  children: ReactNode
  className?: string
}) {
  return <div className={`data-table-wrapper ${className}`.trim()}>{children}</div>
}
