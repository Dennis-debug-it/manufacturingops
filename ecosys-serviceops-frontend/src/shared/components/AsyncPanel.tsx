import type { ReactNode } from 'react'
import { Spinner } from './Spinner'
import { EmptyState } from './EmptyState'

/** Minimal shape of a react-query result this panel needs — keeps callers from importing types. */
interface QueryLike<T> {
  isLoading: boolean
  isError: boolean
  data: T | undefined
  refetch?: () => unknown
}

export interface AsyncPanelProps<T> {
  query: QueryLike<T>
  children: (data: T) => ReactNode
  loadingLabel?: string
  loading?: ReactNode
  errorTitle?: string
  errorDescription?: string
}

/**
 * Renders one panel's own loading / error / data states in isolation. Wrapping each panel in its
 * own <AsyncPanel> replaces the "combine every query into one page-level spinner + one blank error"
 * pattern, so a single slow or failing query only affects its own panel — the rest of the page still
 * renders. Shows a Retry action when the query exposes `refetch`.
 */
export function AsyncPanel<T>({
  query,
  children,
  loadingLabel = 'Loading…',
  loading,
  errorTitle = 'Couldn’t load this section',
  errorDescription = 'Something went wrong while fetching this data. Try again.',
}: AsyncPanelProps<T>) {
  if (query.isError) {
    return (
      <EmptyState
        title={errorTitle}
        description={errorDescription}
        {...(query.refetch ? { actionLabel: 'Retry', onAction: () => { query.refetch?.() } } : {})}
      />
    )
  }

  if (query.isLoading || query.data === undefined) {
    return <>{loading ?? <Spinner label={loadingLabel} />}</>
  }

  return <>{children(query.data)}</>
}
