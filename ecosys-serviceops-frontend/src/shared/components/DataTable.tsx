import type { ReactNode } from 'react'
import { useMemo, useRef, useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useVirtualizer } from '@tanstack/react-virtual'
import { cn } from '../utils/cn'
import { EmptyState } from './EmptyState'
import { Spinner } from './Spinner'

export interface DataTableColumn<T> {
  key: string
  header: string
  cell: (row: T) => ReactNode
  className?: string
  sortable?: boolean
}

export interface DataTablePagination {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
}

export interface DataTableProps<T extends { id?: string }> {
  columns: DataTableColumn<T>[]
  data: T[]
  loading?: boolean
  emptyTitle?: string
  emptyDescription?: string
  emptyActionLabel?: string
  onEmptyAction?: () => void
  pagination?: DataTablePagination
  virtualise?: boolean
  batchSelect?: boolean
  onSelectionChange?: (ids: string[]) => void
  className?: string
}

export function DataTable<T extends { id?: string }>({
  columns,
  data,
  loading = false,
  emptyTitle = 'No records found',
  emptyDescription = 'Try adjusting the filters or create a new record.',
  emptyActionLabel,
  onEmptyAction,
  pagination,
  virtualise = false,
  batchSelect = false,
  onSelectionChange,
  className,
}: DataTableProps<T>) {
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const parentRef = useRef<HTMLDivElement | null>(null)

  const canUseVirtualizer = virtualise && data.length > 100
  const rowVirtualizer = useVirtualizer({
    count: data.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 56,
    overscan: 8,
    enabled: canUseVirtualizer,
  })

  const paginatedData = useMemo(() => {
    if (!pagination) {
      return data
    }
    const start = (pagination.page - 1) * pagination.pageSize
    return data.slice(start, start + pagination.pageSize)
  }, [data, pagination])

  const allIds = paginatedData.flatMap((row) => (typeof row.id === 'string' ? [row.id] : [])).filter((id): id is string => typeof id === 'string')
  const allSelected = allIds.length > 0 && allIds.every((id) => selectedIds.includes(id))

  const setSelection = (ids: string[]) => {
    setSelectedIds(ids)
    onSelectionChange?.(ids)
  }

  if (loading) {
    return <Spinner label="Loading table" />
  }

  if (data.length === 0) {
    return (
      <EmptyState
        title={emptyTitle}
        description={emptyDescription}
        {...(emptyActionLabel ? { actionLabel: emptyActionLabel } : {})}
        {...(onEmptyAction ? { onAction: onEmptyAction } : {})}
      />
    )
  }

  const renderRows = (rows: T[]) =>
    rows.map((row) => (
      <tr key={row.id ?? JSON.stringify(row)}>
        {batchSelect ? (
          <td className="px-4 py-3">
            {row.id ? (
              (() => {
                const rowId = row.id
                return (
              <input
                type="checkbox"
                checked={selectedIds.includes(rowId)}
                onChange={(event) => {
                  setSelection(
                    event.target.checked
                      ? [...selectedIds, rowId]
                      : selectedIds.filter((value) => value !== rowId),
                  )
                }}
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
              />
                )
              })()
            ) : null}
          </td>
        ) : null}
        {columns.map((column) => (
          <td key={column.key} className={cn('px-4 py-3 align-top text-slate-700', column.className)}>
            {column.cell(row)}
          </td>
        ))}
      </tr>
    ))

  return (
    <div className={cn('space-y-4', className)}>
      <div
        ref={parentRef}
        className={cn(
          'rounded-2xl border border-slate-200 bg-white shadow-sm',
          canUseVirtualizer ? 'max-h-[70vh] overflow-auto' : 'overflow-x-auto',
        )}
      >
        {canUseVirtualizer ? (
          <div className="divide-y divide-slate-100">
            <div className="flex items-center gap-0 border-b border-slate-200 bg-slate-50 px-4 py-3 text-sm font-medium text-slate-600">
              {batchSelect ? (
                <div className="w-12">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    onChange={(event) => setSelection(event.target.checked ? allIds : [])}
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
                  />
                </div>
              ) : null}
              {columns.map((column) => (
                <div key={column.key} className={cn('flex-1 px-4', column.className)}>
                  {column.header}
                </div>
              ))}
            </div>
            <div style={{ height: `${rowVirtualizer.getTotalSize()}px` }} className="relative">
              {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                const row = data[virtualRow.index]
                if (!row) {
                  return null
                }
                const rowId = row.id
                return (
                  <div
                    key={virtualRow.key}
                    className="absolute left-0 right-0 flex items-start border-b border-slate-100 bg-white text-sm text-slate-700"
                    style={{ transform: `translateY(${virtualRow.start}px)` }}
                  >
                    {batchSelect ? (
                      <div className="w-12 px-4 py-3">
                        {rowId ? (
                          <input
                            type="checkbox"
                            checked={selectedIds.includes(rowId)}
                            onChange={(event) => {
                              setSelection(
                                event.target.checked
                                  ? [...selectedIds, rowId]
                                  : selectedIds.filter((value) => value !== rowId),
                              )
                            }}
                            className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
                          />
                        ) : null}
                      </div>
                    ) : null}
                    {columns.map((column) => (
                      <div key={column.key} className={cn('flex-1 px-4 py-3', column.className)}>
                        {column.cell(row)}
                      </div>
                    ))}
                  </div>
                )
              })}
            </div>
          </div>
        ) : (
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50">
              <tr>
                {batchSelect ? (
                  <th className="w-12 px-4 py-3">
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={(event) => setSelection(event.target.checked ? allIds : [])}
                      className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-900"
                    />
                  </th>
                ) : null}
                {columns.map((column) => (
                  <th key={column.key} className={cn('px-4 py-3 font-medium text-slate-600', column.className)}>
                    {column.header}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 bg-white">{renderRows(paginatedData)}</tbody>
          </table>
        )}
      </div>

      {pagination ? (
        <div className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-600 shadow-sm sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2">
            <span>Rows per page</span>
            <select
              value={pagination.pageSize}
              onChange={(event) => pagination.onPageSizeChange(Number(event.target.value))}
              className="h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-slate-900"
            >
              {[10, 25, 50].map((pageSize) => (
                <option key={pageSize} value={pageSize}>
                  {pageSize}
                </option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <span>
              Page {pagination.page} of {Math.max(1, Math.ceil(pagination.total / pagination.pageSize))}
            </span>
            <button
              type="button"
              disabled={pagination.page <= 1}
              onClick={() => pagination.onPageChange(Math.max(1, pagination.page - 1))}
              className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 px-3 text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ChevronLeft className="h-4 w-4" />
              Prev
            </button>
            <button
              type="button"
              disabled={pagination.page >= Math.ceil(pagination.total / pagination.pageSize)}
              onClick={() => pagination.onPageChange(Math.min(Math.ceil(pagination.total / pagination.pageSize), pagination.page + 1))}
              className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 px-3 text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Next
              <ChevronRight className="h-4 w-4" />
            </button>
          </div>
        </div>
      ) : null}
    </div>
  )
}
