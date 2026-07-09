import { useMemo, type ReactNode } from 'react'
import { EmptyState } from './EmptyState'
import { DataTableWrapper } from './DataTableWrapper'
import { Pagination } from './Pagination'
import { usePagination } from '../../hooks/usePagination'

export type DataTableColumn<T> = {
  key: string
  header: string
  className?: string
  cell: (row: T) => ReactNode
}

export function DataTable<T>({
  columns,
  rows,
  emptyTitle = 'No records yet',
  emptyDescription = 'This section is waiting for the next record.',
  rowKey,
  pageSize = 10,
  minTableWidth = 'min-w-[900px] w-full',
  mobileCard,
  onRowClick,
  variant = 'open',
  framed,
}: {
  columns: DataTableColumn<T>[]
  rows?: T[] | null
  emptyTitle?: string
  emptyDescription?: string
  rowKey?: (row: T, index: number) => string
  pageSize?: number
  minTableWidth?: string
  mobileCard?: (row: T, index: number) => ReactNode
  onRowClick?: (row: T, index: number) => void
  variant?: 'open' | 'framed'
  framed?: boolean
}) {
  const safeRows = useMemo(() => (Array.isArray(rows) ? rows : []), [rows])
  const effectivePageSize = pageSize > 0 ? pageSize : 10
  const pagination = usePagination(safeRows, effectivePageSize)
  const resolvedVariant = framed === true ? 'framed' : framed === false ? 'open' : variant

  if (safeRows.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />
  }

  return (
    <DataTableWrapper variant={resolvedVariant}>
      <div className="divide-app md:hidden">
        {pagination.pageItems.map((row, index) => (
          <article
            key={rowKey ? rowKey(row, index) : index}
            className={`space-y-3 px-4 py-3.5 ${onRowClick ? 'cursor-pointer' : ''}`.trim()}
            role={onRowClick ? 'button' : undefined}
            tabIndex={onRowClick ? 0 : undefined}
            onClick={onRowClick ? () => onRowClick(row, index) : undefined}
            onKeyDown={onRowClick ? (event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault()
                onRowClick(row, index)
              }
            } : undefined}
          >
            {mobileCard ? mobileCard(row, index) : (
              <dl className="space-y-0">
                {columns.map((column) => (
                  <div key={column.key} className="border-t border-app py-2.5 first:border-t-0 first:pt-0 last:pb-0">
                    <dt className="text-secondary text-[11px] font-semibold uppercase tracking-[0.18em]">{column.header}</dt>
                    <dd className="mt-1 break-words text-sm text-app">{column.cell(row)}</dd>
                  </div>
                ))}
              </dl>
            )}
          </article>
        ))}
      </div>
      <div className={`hidden w-full overflow-x-auto overscroll-x-contain md:block ${resolvedVariant === 'framed' ? 'rounded-[0.95rem] bg-[var(--app-table)]' : ''}`}>
        <table className={`divide-app w-full ${minTableWidth}`}>
          <thead className="table-head">
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={`text-secondary px-4 py-2.5 text-left text-[11px] font-semibold uppercase tracking-[0.18em] ${column.className ?? ''}`}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-app">
            {pagination.pageItems.map((row, index) => (
              <tr
                key={rowKey ? rowKey(row, index) : index}
                className={`table-row transition ${onRowClick ? 'cursor-pointer hover:bg-[var(--app-subtle)]' : ''}`.trim()}
                role={onRowClick ? 'button' : undefined}
                tabIndex={onRowClick ? 0 : undefined}
                onClick={onRowClick ? () => onRowClick(row, index) : undefined}
                onKeyDown={onRowClick ? (event) => {
                  if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault()
                    onRowClick(row, index)
                  }
                } : undefined}
              >
                {columns.map((column) => (
                  <td key={column.key} className={`px-4 py-3 align-top text-sm text-app ${column.className ?? ''}`}>
                    {column.cell(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pagination
        page={pagination.page}
        pageCount={pagination.pageCount}
        totalItems={pagination.totalItems}
        startItem={pagination.startItem}
        endItem={pagination.endItem}
        canPrevious={pagination.canPrevious}
        canNext={pagination.canNext}
        onPrevious={pagination.previousPage}
        onNext={pagination.nextPage}
      />
    </DataTableWrapper>
  )
}
