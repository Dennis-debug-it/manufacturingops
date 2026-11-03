import type { ReactNode } from 'react'
import { useMemo, useRef, useState } from 'react'
import { ChevronLeft, ChevronRight, ChevronsUpDown, ChevronDown, ChevronUp } from 'lucide-react'
import { useVirtualizer } from '@tanstack/react-virtual'
import { cn } from '../utils/cn'
import { EmptyState } from './EmptyState'
import { Spinner } from './Spinner'

/** What a column sorts on. `null` sorts last in both directions — an absent value is not a small one. */
export type SortValue = string | number | null

export interface DataTableColumn<T> {
  key: string
  header: string
  cell: (row: T) => ReactNode
  className?: string
  /**
   * Makes the column sortable. There is deliberately no bare `sortable: boolean`: `cell` returns
   * a `ReactNode` (a link, a badge, a formatted amount), so the table cannot recover a sortable
   * value from what it renders, and `key` is a label rather than a guaranteed field of `T`. A
   * column declares how it sorts, or it does not sort — the old boolean flag could be set on a
   * column the table had no way to order, and was never read by anything.
   */
  sortValue?: (row: T) => SortValue
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
  emptyActionLabel?: string | undefined
  onEmptyAction?: (() => void) | undefined
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
  const [sort, setSort] = useState<{ key: string; direction: 'asc' | 'desc' } | null>(null)

  // Sorting is client-side, so it is only honest when the client holds the whole dataset. With
  // external (server-side) pagination `data` is one page, and reordering it would present "the
  // largest invoice" that is merely the largest on page 3.
  const sortingEnabled = !pagination
  const activeSortColumn = sortingEnabled && sort ? columns.find((column) => column.key === sort.key) : undefined
  const sortAccessor = activeSortColumn?.sortValue

  const sortedData = useMemo(() => {
    if (!sortAccessor || !sort) {
      return data
    }
    const direction = sort.direction === 'asc' ? 1 : -1
    return [...data].sort((left, right) => {
      const a = sortAccessor(left)
      const b = sortAccessor(right)
      if (a === null && b === null) return 0
      if (a === null) return 1
      if (b === null) return -1
      if (typeof a === 'number' && typeof b === 'number') {
        return (a - b) * direction
      }
      return String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: 'base' }) * direction
    })
  }, [data, sortAccessor, sort])

  const canUseVirtualizer = virtualise && data.length > 100
  const rowVirtualizer = useVirtualizer({
    count: sortedData.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 56,
    overscan: 8,
    enabled: canUseVirtualizer,
  })

  // Pagination is on by default so long lists never render unbounded; passing the
  // `pagination` prop switches to external (e.g. server-side) control.
  const [internalPage, setInternalPage] = useState(1)
  const [internalPageSize, setInternalPageSize] = useState(20)

  const effectivePagination: DataTablePagination | null = useMemo(() => {
    if (pagination) {
      return pagination
    }
    if (canUseVirtualizer) {
      return null
    }
    const totalPages = Math.max(1, Math.ceil(data.length / internalPageSize))
    return {
      page: Math.min(internalPage, totalPages),
      pageSize: internalPageSize,
      total: data.length,
      onPageChange: setInternalPage,
      onPageSizeChange: (pageSize: number) => {
        setInternalPageSize(pageSize)
        setInternalPage(1)
      },
    }
  }, [pagination, canUseVirtualizer, data.length, internalPage, internalPageSize])

  const paginatedData = useMemo(() => {
    if (!effectivePagination) {
      return sortedData
    }
    // External (server-side) pagination: `data` is already just the current page, so render it
    // as-is. Only the internal client-side pager slices the full dataset.
    if (pagination) {
      return sortedData
    }
    const start = (effectivePagination.page - 1) * effectivePagination.pageSize
    return sortedData.slice(start, start + effectivePagination.pageSize)
  }, [sortedData, effectivePagination, pagination])

  const toggleSort = (key: string) => {
    setSort((current) => (current?.key === key ? { key, direction: current.direction === 'asc' ? 'desc' : 'asc' } : { key, direction: 'asc' }))
    // A re-sorted list makes the current page number meaningless; start from the top.
    setInternalPage(1)
  }

  const renderHeaderLabel = (column: DataTableColumn<T>) => {
    if (!sortingEnabled || !column.sortValue) {
      return column.header
    }
    const isActive = sort?.key === column.key
    const Icon = isActive ? (sort.direction === 'asc' ? ChevronUp : ChevronDown) : ChevronsUpDown
    return (
      <button
        type="button"
        onClick={() => toggleSort(column.key)}
        className={cn('inline-flex items-center gap-1 rounded transition hover:text-slate-900', isActive && 'text-slate-900')}
      >
        {column.header}
        <Icon className={cn('h-3.5 w-3.5', isActive ? 'text-slate-700' : 'text-slate-400')} aria-hidden="true" />
      </button>
    )
  }

  const ariaSortFor = (column: DataTableColumn<T>) => {
    if (!sortingEnabled || !column.sortValue) {
      return undefined
    }
    if (sort?.key !== column.key) {
      return 'none' as const
    }
    return sort.direction === 'asc' ? ('ascending' as const) : ('descending' as const)
  }

  const showPaginationFooter = effectivePagination !== null && (Boolean(pagination) || effectivePagination.total > effectivePagination.pageSize)

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
      // perf: no backdrop-filter here — see the wrapper's comment above. Plain alpha only.
      <tr
        key={row.id ?? JSON.stringify(row)}
        className={cn('bg-white/70', row.id && selectedIds.includes(row.id) && 'bg-[var(--glass-lime)]')}
      >
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
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-[var(--app-primary)]"
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
          // perf: blur only this static, non-scrolling wrapper — a per-row backdrop-filter
          // below would force its own compositing layer per row and jank the virtualized list.
          'rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] shadow-[var(--shadow-card)] backdrop-blur-[var(--glass-blur-md)]',
          canUseVirtualizer ? 'max-h-[70vh] overflow-auto' : 'overflow-x-auto',
        )}
      >
        {canUseVirtualizer ? (
          <div className="divide-y divide-[var(--app-border)]/60">
            <div className="flex items-center gap-0 border-b border-[var(--app-border)] bg-[var(--app-primary-soft)] px-4 py-3 text-sm font-medium text-slate-600">
              {batchSelect ? (
                <div className="w-12">
                  <input
                    type="checkbox"
                    checked={allSelected}
                    onChange={(event) => setSelection(event.target.checked ? allIds : [])}
                    className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-[var(--app-primary)]"
                  />
                </div>
              ) : null}
              {columns.map((column) => (
                <div key={column.key} role="columnheader" aria-sort={ariaSortFor(column)} className={cn('flex-1 px-4', column.className)}>
                  {renderHeaderLabel(column)}
                </div>
              ))}
            </div>
            <div style={{ height: `${rowVirtualizer.getTotalSize()}px` }} className="relative">
              {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                const row = sortedData[virtualRow.index]
                if (!row) {
                  return null
                }
                const rowId = row.id
                const rowSelected = Boolean(rowId && selectedIds.includes(rowId))
                return (
                  // perf: no backdrop-filter on virtualized row elements — see wrapper comment above.
                  <div
                    key={virtualRow.key}
                    className={cn(
                      'absolute left-0 right-0 flex items-start border-b border-[var(--app-border)]/40 text-sm text-slate-700',
                      rowSelected ? 'bg-[var(--glass-lime)]' : 'bg-white/70',
                    )}
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
                            className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-[var(--app-primary)]"
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
          <table className="min-w-full divide-y divide-[var(--app-border)] text-left text-sm">
            <thead className="bg-[var(--app-primary-soft)]">
              <tr>
                {batchSelect ? (
                  <th className="w-12 px-4 py-3">
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={(event) => setSelection(event.target.checked ? allIds : [])}
                      className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-[var(--app-primary)]"
                    />
                  </th>
                ) : null}
                {columns.map((column) => (
                  <th key={column.key} aria-sort={ariaSortFor(column)} className={cn('px-4 py-3 font-medium text-slate-600', column.className)}>
                    {renderHeaderLabel(column)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-[var(--app-border)]/60">{renderRows(paginatedData)}</tbody>
          </table>
        )}
      </div>

      {showPaginationFooter && effectivePagination ? (
        <div className="flex flex-col gap-3 rounded-[var(--radius-card)] border border-[var(--glass-border)] bg-[var(--glass-white)] px-4 py-3 text-sm text-slate-600 shadow-[var(--shadow-card)] backdrop-blur-[var(--glass-blur-md)] sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2">
            <span>Rows per page</span>
            <select
              value={effectivePagination.pageSize}
              onChange={(event) => effectivePagination.onPageSizeChange(Number(event.target.value))}
              className="h-10 rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)]"
            >
              {[10, 20, 50, 100].map((pageSize) => (
                <option key={pageSize} value={pageSize}>
                  {pageSize}
                </option>
              ))}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <span>
              Page {effectivePagination.page} of {Math.max(1, Math.ceil(effectivePagination.total / effectivePagination.pageSize))}
            </span>
            <button
              type="button"
              disabled={effectivePagination.page <= 1}
              onClick={() => effectivePagination.onPageChange(Math.max(1, effectivePagination.page - 1))}
              className="inline-flex h-10 items-center gap-2 rounded-lg border border-slate-200 px-3 text-slate-700 transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <ChevronLeft className="h-4 w-4" />
              Prev
            </button>
            <button
              type="button"
              disabled={effectivePagination.page >= Math.ceil(effectivePagination.total / effectivePagination.pageSize)}
              onClick={() => effectivePagination.onPageChange(Math.min(Math.ceil(effectivePagination.total / effectivePagination.pageSize), effectivePagination.page + 1))}
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
