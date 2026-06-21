import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { EmptyState } from './EmptyState'
import { DataTableWrapper } from './DataTableWrapper'

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
  pageSize = 8,
  minTableWidth = 'min-w-[900px] w-full',
  mobileCard,
}: {
  columns: DataTableColumn<T>[]
  rows?: T[] | null
  emptyTitle?: string
  emptyDescription?: string
  rowKey?: (row: T, index: number) => string
  pageSize?: number
  minTableWidth?: string
  mobileCard?: (row: T, index: number) => ReactNode
}) {
  const safeRows = useMemo(() => (Array.isArray(rows) ? rows : []), [rows])
  const [page, setPage] = useState(1)
  const effectivePageSize = pageSize > 0 ? pageSize : Math.max(safeRows.length, 1)
  const pageCount = Math.max(1, Math.ceil(safeRows.length / effectivePageSize))
  const visibleRows = useMemo(() => {
    const start = (page - 1) * effectivePageSize
    return safeRows.slice(start, start + effectivePageSize)
  }, [effectivePageSize, page, safeRows])

  useEffect(() => {
    if (page > pageCount) {
      setPage(pageCount)
    }
  }, [page, pageCount])

  if (safeRows.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />
  }

  return (
    <DataTableWrapper>
      <div className="divide-app md:hidden">
        {visibleRows.map((row, index) => (
          <article key={rowKey ? rowKey(row, index) : index} className="space-y-4 px-4 py-4">
            {mobileCard ? mobileCard(row, index) : (
              <dl className="space-y-0">
                {columns.map((column) => (
                  <div key={column.key} className="border-t border-app py-3 first:border-t-0 first:pt-0 last:pb-0">
                    <dt className="text-secondary text-[11px] font-semibold uppercase tracking-[0.18em]">{column.header}</dt>
                    <dd className="mt-1.5 break-words text-sm text-app">{column.cell(row)}</dd>
                  </div>
                ))}
              </dl>
            )}
          </article>
        ))}
      </div>
      <div className="hidden w-full overflow-x-auto overscroll-x-contain rounded-[var(--radius-app)] md:block">
        <table className={`divide-app w-full ${minTableWidth}`}>
          <thead className="table-head">
            <tr>
              {columns.map((column) => (
                <th
                  key={column.key}
                  className={`text-secondary px-4 py-3.5 text-left text-[11px] font-semibold uppercase tracking-[0.18em] ${column.className ?? ''}`}
                >
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-app">
            {visibleRows.map((row, index) => (
              <tr key={rowKey ? rowKey(row, index) : index} className="table-row transition">
                {columns.map((column) => (
                  <td key={column.key} className={`px-4 py-3.5 align-top text-sm text-app ${column.className ?? ''}`}>
                    {column.cell(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {pageCount > 1 ? (
        <div className="flex flex-col gap-3 border-t border-app px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-muted">
            Showing {(page - 1) * effectivePageSize + 1}-{Math.min(page * effectivePageSize, safeRows.length)} of {safeRows.length}
          </p>
          <div className="flex flex-wrap items-center gap-2">
            <button type="button" className="button-secondary min-h-[40px] px-3 py-2" onClick={() => setPage((current) => Math.max(1, current - 1))} disabled={page === 1}>
              Previous
            </button>
            <p className="text-xs text-muted">
              Page {page} of {pageCount}
            </p>
            <button type="button" className="button-secondary min-h-[40px] px-3 py-2" onClick={() => setPage((current) => Math.min(pageCount, current + 1))} disabled={page === pageCount}>
              Next
            </button>
          </div>
        </div>
      ) : null}
    </DataTableWrapper>
  )
}
