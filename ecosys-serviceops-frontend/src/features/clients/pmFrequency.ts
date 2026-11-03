export type PmFrequencyUnit = 'days' | 'weeks' | 'months' | 'quarters' | 'years'

const NAMED_FREQUENCIES: Record<string, { value: number; unit: PmFrequencyUnit }> = {
  daily: { value: 1, unit: 'days' },
  weekly: { value: 1, unit: 'weeks' },
  fortnightly: { value: 2, unit: 'weeks' },
  biweekly: { value: 2, unit: 'weeks' },
  monthly: { value: 1, unit: 'months' },
  bimonthly: { value: 2, unit: 'months' },
  quarterly: { value: 1, unit: 'quarters' },
  semiannual: { value: 6, unit: 'months' },
  semiannually: { value: 6, unit: 'months' },
  biannual: { value: 6, unit: 'months' },
  biannually: { value: 6, unit: 'months' },
  halfyearly: { value: 6, unit: 'months' },
  sixmonthly: { value: 6, unit: 'months' },
  annual: { value: 1, unit: 'years' },
  annually: { value: 1, unit: 'years' },
  yearly: { value: 1, unit: 'years' },
}

/**
 * Converts a free-text frequency ("Monthly", "Semi-Annual", "Every 2 weeks") into value + unit.
 * Mirrors the backend's PmFrequencyLabelParser.
 */
export function parsePmFrequencyLabel(label: string | null | undefined): { value: number; unit: PmFrequencyUnit } | null {
  if (!label?.trim()) return null
  const named = NAMED_FREQUENCIES[label.replace(/[^a-zA-Z]/g, '').toLowerCase()]
  if (named) return named
  const match = /^(?:every\s+)?(\d{1,4})\s*-?\s*(day|week|month|quarter|year)s?$/i.exec(label.trim())
  if (!match) return null
  const value = Number(match[1])
  return value > 0 ? { value, unit: `${match[2]!.toLowerCase()}s` as PmFrequencyUnit } : null
}
