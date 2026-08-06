import { RefreshCw } from 'lucide-react'

export interface LastUpdatedProps {
  /** react-query `dataUpdatedAt` epoch (ms). 0/undefined renders nothing. */
  at?: number
  className?: string
}

/**
 * Freshness stamp for auto-refreshing operational pages — shows "as of HH:MM:SS" from a react-query
 * `dataUpdatedAt` timestamp so the operator can see the data is live and how recent it is.
 */
export function LastUpdated({ at, className }: LastUpdatedProps) {
  if (!at) {
    return null
  }

  const time = new Date(at).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit', second: '2-digit' })
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs text-slate-500 ${className ?? ''}`}>
      <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
      as of {time}
    </span>
  )
}
