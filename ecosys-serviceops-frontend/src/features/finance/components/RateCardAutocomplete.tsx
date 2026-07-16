import { useMemo } from 'react'
import { AutocompleteInput, type AutocompleteSuggestion } from '../../../shared/components/AutocompleteInput'
import { formatCurrency } from '../../../shared/utils/format'
import { useFinanceRateCards } from '../api'
import type { RateCard } from '../types'

export interface RateCardAutocompleteProps {
  value: string
  onChange: (value: string) => void
  /** Fired when the user picks a rate card (not on free-text entry). */
  onSelectRateCard?: (card: RateCard) => void
  placeholder?: string
  className?: string
  id?: string
  'aria-label'?: string
}

/**
 * Line-description input with type-ahead suggestions from active rate cards.
 * Thin wrapper over {@link AutocompleteInput}; selecting a card lets the caller
 * auto-fill rate, tax, unit, etc.
 */
export function RateCardAutocomplete({
  value,
  onChange,
  onSelectRateCard,
  placeholder = 'Description',
  className,
  id,
  'aria-label': ariaLabel,
}: RateCardAutocompleteProps) {
  const { data: rateCards } = useFinanceRateCards()
  const activeCards = useMemo(() => (rateCards ?? []).filter((card) => card.isActive), [rateCards])

  const suggestions = useMemo<AutocompleteSuggestion[]>(
    () =>
      activeCards.map((card) => ({
        id: card.id,
        label: card.description?.trim() || card.name,
        code: card.category || undefined,
        hint: `${formatCurrency(card.unitRate)} / ${card.unit}`,
      })),
    [activeCards],
  )

  const byId = useMemo(() => new Map(activeCards.map((card) => [card.id, card])), [activeCards])

  return (
    <AutocompleteInput
      value={value}
      onChange={onChange}
      suggestions={suggestions}
      onSelect={(s) => {
        const card = byId.get(s.id)
        if (card) onSelectRateCard?.(card)
      }}
      placeholder={placeholder}
      className={className}
      id={id}
      aria-label={ariaLabel}
    />
  )
}
