import type { DealField } from '../types'

/** The input for one custom deal field. Values are strings: dates as yyyy-MM-dd, a ticked box as "Yes". */
export function DealFieldInput({ field, value, onChange, className }: { field: DealField; value: string; onChange: (value: string) => void; className: string }) {
  const id = `deal-field-${field.key}`
  switch (field.fieldType) {
    case 'Checkbox':
      return (
        <label htmlFor={id} className="flex h-11 items-center gap-2 text-sm text-slate-700">
          <input id={id} type="checkbox" checked={value === 'Yes'} onChange={(event) => onChange(event.target.checked ? 'Yes' : '')} />
          {field.label}
        </label>
      )
    case 'Dropdown':
      return (
        <select id={id} aria-label={field.label} value={value} onChange={(event) => onChange(event.target.value)} className={className}>
          <option value="">—</option>
          {/* A value from a choice since removed still shows, so saving does not silently drop it. */}
          {value && !field.options.includes(value) ? <option value={value}>{value}</option> : null}
          {field.options.map((option) => <option key={option} value={option}>{option}</option>)}
        </select>
      )
    case 'Number':
      return <input id={id} aria-label={field.label} type="number" value={value} onChange={(event) => onChange(event.target.value)} className={className} />
    case 'Date':
      return <input id={id} aria-label={field.label} type="date" value={value} onChange={(event) => onChange(event.target.value)} className={className} />
    default:
      return <input id={id} aria-label={field.label} value={value} onChange={(event) => onChange(event.target.value)} className={className} maxLength={1000} />
  }
}

/** A stored value as people read it. */
export function formatDealFieldValue(fieldType: string, value: string | null) {
  if (!value) return '—'
  if (fieldType === 'Number') return Number(value).toLocaleString()
  if (fieldType === 'Date') return new Date(`${value}T00:00:00`).toLocaleDateString()
  return value
}
