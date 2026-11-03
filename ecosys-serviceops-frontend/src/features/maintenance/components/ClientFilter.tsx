import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useClients } from '../../clients/api'

/**
 * The client filter lives in the URL (?clientId=) so it survives a refresh, can be shared as a
 * link, and carries across the Maintenance tabs (MaintenanceTabNav forwards it).
 */
export function useMaintenanceClientFilter() {
  const [searchParams, setSearchParams] = useSearchParams()
  const clientId = searchParams.get('clientId') ?? ''
  const setClientId = useCallback(
    (next: string) => {
      setSearchParams(
        (current) => {
          const params = new URLSearchParams(current)
          if (next) params.set('clientId', next)
          else params.delete('clientId')
          return params
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )
  return { clientId, setClientId }
}

export function ClientFilterSelect({ value, onChange }: { value: string; onChange: (clientId: string) => void }) {
  const { data: clients, isLoading } = useClients()
  const sorted = [...(clients ?? [])].sort((a, b) => a.clientName.localeCompare(b.clientName))
  return (
    <select
      aria-label="Filter by client"
      value={value}
      onChange={(event) => onChange(event.target.value)}
      disabled={isLoading}
      className="h-11 min-w-[12rem] rounded-lg border border-slate-300 bg-white px-3 text-sm outline-none focus:border-[var(--app-primary)] disabled:opacity-60"
    >
      <option value="">All clients</option>
      {sorted.map((client) => (
        <option key={client.id} value={client.id}>{client.clientName}</option>
      ))}
    </select>
  )
}
