/**
 * Envelope returned by list endpoints when a request includes `page`/`pageSize` query params.
 * Without those params the same endpoints return a bare `T[]` (backward compatible), so a consumer
 * opts into pagination by sending the params and reading this shape.
 */
export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface PageRequest {
  page?: number
  pageSize?: number
}
