export function Pagination({
  page,
  pageCount,
  totalItems,
  startItem,
  endItem,
  canPrevious,
  canNext,
  onPrevious,
  onNext,
  className = '',
  labelPrefix = 'Showing',
}: {
  page: number
  pageCount: number
  totalItems: number
  startItem: number
  endItem: number
  canPrevious: boolean
  canNext: boolean
  onPrevious: () => void
  onNext: () => void
  className?: string
  labelPrefix?: string
}) {
  if (totalItems <= 0) {
    return null
  }

  const showControls = pageCount > 1

  return (
    <div className={`flex flex-col gap-3 border-t border-[var(--app-divider)] px-4 py-3 sm:flex-row sm:items-center sm:justify-between ${className}`.trim()}>
      <p className="text-xs text-muted">
        {labelPrefix} {startItem}{"\u2013"}{endItem} of {totalItems}
      </p>
      {showControls ? (
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="button-secondary min-h-[40px] px-3 py-2"
            onClick={onPrevious}
            disabled={!canPrevious}
          >
            Previous
          </button>
          <p className="text-xs text-muted">
            Page {page} of {pageCount}
          </p>
          <button
            type="button"
            className="button-secondary min-h-[40px] px-3 py-2"
            onClick={onNext}
            disabled={!canNext}
          >
            Next
          </button>
        </div>
      ) : null}
    </div>
  )
}
