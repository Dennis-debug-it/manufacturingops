import { useMemo } from 'react'
import { AutocompleteInput, type AutocompleteSuggestion } from '../../../shared/components/AutocompleteInput'
import { useMaterials } from '../api'
import type { Material } from '../types'

export interface MaterialAutocompleteProps {
  value: string
  onChange: (value: string) => void
  /** Fired when the user picks an existing catalogue item (not on free-text entry). */
  onSelectMaterial?: (material: Material) => void
  placeholder?: string
  className?: string
  id?: string
  'aria-label'?: string
}

/**
 * Item-description input with type-ahead suggestions from the material catalogue.
 * Thin wrapper over {@link AutocompleteInput} that supplies the suggestion source.
 */
export function MaterialAutocomplete({
  value,
  onChange,
  onSelectMaterial,
  placeholder = 'Item description',
  className,
  id,
  'aria-label': ariaLabel,
}: MaterialAutocompleteProps) {
  // Shared React Query cache — every line renders one of these but they dedupe.
  const { data: materials } = useMaterials({ status: 'active' })

  const suggestions = useMemo<AutocompleteSuggestion[]>(
    () =>
      (materials ?? []).map((m) => ({
        id: m.id,
        label: m.itemName,
        code: m.itemCode,
        hint: m.unitCost != null ? m.unitCost.toLocaleString() : undefined,
      })),
    [materials],
  )

  const byId = useMemo(() => new Map((materials ?? []).map((m) => [m.id, m])), [materials])

  return (
    <AutocompleteInput
      value={value}
      onChange={onChange}
      suggestions={suggestions}
      onSelect={(s) => {
        const material = byId.get(s.id)
        if (material) onSelectMaterial?.(material)
      }}
      placeholder={placeholder}
      className={className}
      id={id}
      aria-label={ariaLabel}
    />
  )
}
