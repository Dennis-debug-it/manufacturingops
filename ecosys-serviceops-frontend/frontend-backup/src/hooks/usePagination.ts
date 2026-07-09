import { useEffect, useMemo, useState } from 'react'

export function usePagination<T>(
  items: T[],
  pageSize = 10,
) {
  const safePageSize = pageSize > 0 ? pageSize : Math.max(items.length, 1)
  const [page, setPage] = useState(1)

  useEffect(() => {
    setPage(1)
  }, [items])

  const pageCount = Math.max(1, Math.ceil(items.length / safePageSize))

  useEffect(() => {
    if (page > pageCount) {
      setPage(pageCount)
    }
  }, [page, pageCount])

  const pageItems = useMemo(() => {
    const start = (page - 1) * safePageSize
    return items.slice(start, start + safePageSize)
  }, [items, page, safePageSize])

  const startItem = items.length === 0 ? 0 : (page - 1) * safePageSize + 1
  const endItem = items.length === 0 ? 0 : Math.min(page * safePageSize, items.length)

  return {
    page,
    pageCount,
    pageItems,
    totalItems: items.length,
    startItem,
    endItem,
    canPrevious: page > 1,
    canNext: page < pageCount,
    previousPage: () => setPage((current) => Math.max(1, current - 1)),
    nextPage: () => setPage((current) => Math.min(pageCount, current + 1)),
    setPage,
  }
}
