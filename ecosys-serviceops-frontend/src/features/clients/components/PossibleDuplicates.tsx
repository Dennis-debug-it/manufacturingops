import { Link } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'
import { useDebounce } from '../../../shared/hooks/useDebounce'
import { usePossibleDuplicates } from '../api'
import type { PossibleDuplicateClient } from '../types'

interface PossibleDuplicatesProps {
  name?: string | undefined
  email?: string | undefined
  phone?: string | undefined
  excludeId?: string | undefined
  /** Offered as "Use this" next to each match; omit to only point at them. */
  onUse?: (client: PossibleDuplicateClient) => void
}

/** "Looks like Baraka Logistics Ltd is already here", while the company is being typed. */
export function PossibleDuplicates({ name, email, phone, excludeId, onUse }: PossibleDuplicatesProps) {
  const params = useDebounce({ name: name?.trim() || undefined, email: email?.trim() || undefined, phone: phone?.trim() || undefined, excludeId }, 400)
  const { data } = usePossibleDuplicates(params)
  if (!data || data.length === 0) return null

  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900" role="status">
      <div className="flex items-center gap-2 font-medium">
        <AlertTriangle className="h-4 w-4 shrink-0" />
        {data.length === 1 ? 'This may already be here:' : 'These may already be here:'}
      </div>
      <ul className="mt-1 space-y-1">
        {data.map((match) => (
          <li key={match.id} className="flex flex-wrap items-center gap-x-2">
            <Link to={`/clients/${match.id}`} target="_blank" className="font-medium underline underline-offset-2">{match.clientName}</Link>
            <span className="text-xs text-amber-800">{match.isProspect ? 'prospect · ' : ''}{match.reason}</span>
            {onUse ? (
              <button type="button" onClick={() => onUse(match)} className="text-xs font-semibold text-amber-900 underline underline-offset-2">Use this</button>
            ) : null}
          </li>
        ))}
      </ul>
    </div>
  )
}
