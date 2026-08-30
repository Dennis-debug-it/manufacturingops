import { RefreshCw } from 'lucide-react'
import { formatTime } from '../utils/dates'

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

  const time = formatTime(new Date(at), 'HH:mm:ss')
  return (
    <span className={`inline-flex items-center gap-1.5 text-xs text-slate-500 ${className ?? ''}`}>
      <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
      as of {time}
    </span>
  )
}
