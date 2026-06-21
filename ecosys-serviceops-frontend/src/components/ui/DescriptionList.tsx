import type { ReactNode } from 'react'

export type DescriptionListItem = {
  label: string
  value: ReactNode
  helper?: ReactNode
}

export function DetailRows({
  items,
  columns = 1,
  className = '',
}: {
  items: DescriptionListItem[]
  columns?: 1 | 2 | 3
  className?: string
}) {
  const gridClass =
    columns === 1
      ? 'grid-cols-1'
      : columns === 3
        ? 'grid-cols-1 xl:grid-cols-3'
        : 'grid-cols-1 lg:grid-cols-2'

  return (
    <dl className={`detail-rows grid ${gridClass} ${className}`.trim()}>
      {items.map((item, index) => (
        <div key={`${item.label}-${index}`} className="detail-row">
          <dt className="detail-row-label">{item.label}</dt>
          <dd className="detail-row-value">{item.value}</dd>
          {item.helper ? <div className="detail-row-helper">{item.helper}</div> : null}
        </div>
      ))}
    </dl>
  )
}

export { DetailRows as DescriptionList }
