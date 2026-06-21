import { useId, useState } from 'react'

export type RowActionOption = {
  value: string
  label: string
  disabled?: boolean
}

export function RowActionSelector({
  options,
  onApply,
  placeholder = 'Select action',
  buttonLabel = 'OK',
  selectTestId,
  applyTestId,
}: {
  options: RowActionOption[]
  onApply: (value: string) => void | Promise<void>
  placeholder?: string
  buttonLabel?: string
  selectTestId?: string
  applyTestId?: string
}) {
  const fallbackId = useId()
  const [selected, setSelected] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const selectedOption = options.find((option) => option.value === selected)

  async function handleApply() {
    if (!selected || selectedOption?.disabled || submitting) return

    setSubmitting(true)
    try {
      await onApply(selected)
      setSelected('')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-w-0 flex-col gap-2 sm:flex-row sm:items-center">
      <label className="sr-only" htmlFor={fallbackId}>
        Row action
      </label>
      <select
        id={fallbackId}
        data-testid={selectTestId}
        value={selected}
        onChange={(event) => setSelected(event.target.value)}
        className="field-input min-w-0 sm:min-w-[180px]"
      >
        <option value="">{placeholder}</option>
        {options.map((option) => (
          <option key={option.value} value={option.value} disabled={option.disabled}>
            {option.label}
          </option>
        ))}
      </select>
      <button type="button" data-testid={applyTestId} className="button-secondary min-h-[42px] px-4 py-2" onClick={() => void handleApply()} disabled={!selected || selectedOption?.disabled || submitting}>
        {submitting ? 'Working...' : buttonLabel}
      </button>
    </div>
  )
}
