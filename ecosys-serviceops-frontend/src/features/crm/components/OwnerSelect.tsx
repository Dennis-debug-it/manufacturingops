import { useSalesOwners } from '../api'

interface OwnerSelectProps {
  id?: string | undefined
  value: string
  onChange: (ownerUserId: string) => void
  disabled?: boolean | undefined
  className?: string | undefined
  /** Shown as the empty option; omit to require a pick. */
  emptyLabel?: string | undefined
  'aria-label'?: string
}

/** Picks a deal or RFP/RFQ owner from the people who can work the pipeline. */
export function OwnerSelect({ id, value, onChange, disabled, className, emptyLabel, 'aria-label': ariaLabel }: OwnerSelectProps) {
  const { data: owners } = useSalesOwners()
  const list = owners ?? []
  return (
    <select id={id} aria-label={ariaLabel} value={value} disabled={disabled} onChange={(event) => onChange(event.target.value)} className={className}>
      {emptyLabel !== undefined ? <option value="">{emptyLabel}</option> : null}
      {/* Keep a former owner visible (e.g. since deactivated) instead of showing a blank select. */}
      {value && !list.some((owner) => owner.id === value) ? <option value={value}>Current owner</option> : null}
      {list.map((owner) => (
        <option key={owner.id} value={owner.id}>{owner.fullName}</option>
      ))}
    </select>
  )
}
