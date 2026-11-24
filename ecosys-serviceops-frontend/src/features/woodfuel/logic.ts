import type { PriceBand, WoodSettings } from './api'

/**
 * The same arithmetic the server does when a delivery is saved, so the clerk sees what a load is
 * worth before saving it. The server's answer is the one that is kept; if the two ever differ, the
 * delivery shows the server's figures after saving.
 */

export interface MeasureInput {
  basis: string
  grossKg: number | null
  tareKg: number | null
  stackedM3: number | null
  loads: number | null
  kgPerLoad: number | null
  /** Weight of a stacked cubic metre of the chosen wood. */
  densityKgPerM3: number | null
}

/** The weight received, or a sentence saying what is missing. */
export function netKg(input: MeasureInput): { kg: number | null; problem: string | null } {
  const positive = (value: number | null): value is number => value !== null && Number.isFinite(value) && value > 0
  switch (input.basis) {
    case 'Weighbridge': {
      if (!positive(input.grossKg)) return { kg: null, problem: 'Enter the gross weight.' }
      if (input.tareKg === null || !Number.isFinite(input.tareKg) || input.tareKg < 0) return { kg: null, problem: 'Enter the tare weight.' }
      if (input.tareKg >= input.grossKg) return { kg: null, problem: 'The tare must be lower than the gross.' }
      return { kg: round(input.grossKg - input.tareKg, 1), problem: null }
    }
    case 'StackedVolume': {
      if (!positive(input.stackedM3)) return { kg: null, problem: 'Enter the stacked volume.' }
      if (!positive(input.densityKgPerM3)) return { kg: null, problem: 'Choose the kind of wood.' }
      return { kg: round(input.stackedM3 * input.densityKgPerM3, 1), problem: null }
    }
    case 'LoadCount': {
      if (!positive(input.loads)) return { kg: null, problem: 'Enter how many loads.' }
      if (!positive(input.kgPerLoad)) return { kg: null, problem: 'Enter the weight of one load.' }
      return { kg: round(input.loads * input.kgPerLoad, 1), problem: null }
    }
    default:
      return { kg: null, problem: 'Choose how it was measured.' }
  }
}

export function averageMoisture(samples: Array<number | null>): number | null {
  const values = samples.filter((value): value is number => value !== null && Number.isFinite(value))
  return values.length === 0 ? null : round(values.reduce((sum, value) => sum + value, 0) / values.length, 2)
}

export interface Assessment {
  decision: 'Accepted' | 'AcceptedWithDeduction' | 'Rejected'
  rejectedKg: number
  deductedKg: number
  payableKg: number
  pricePercent: number
  amount: number
  reason: string | null
}

export function assess(settings: Pick<WoodSettings, 'moistureRule' | 'baseMoisturePct' | 'rejectMoisturePct' | 'deductionPctPerPoint' | 'priceBands'>, netKgValue: number, moisture: number | null, pricePerTonne: number, rejected: boolean, rejectReason: string | null, allowAboveRejectLimit = false): Assessment {
  let reason = rejectReason
  if (!rejected && !allowAboveRejectLimit && moisture !== null && moisture > settings.rejectMoisturePct) {
    rejected = true
    reason = `Moisture ${trim(moisture)}% is above the ${trim(settings.rejectMoisturePct)}% limit.`
  }
  if (rejected) return { decision: 'Rejected', rejectedKg: netKgValue, deductedKg: 0, payableKg: 0, pricePercent: 0, amount: 0, reason: reason?.trim() || 'Rejected.' }

  let deducted = 0
  let percent = 100
  let note: string | null = null
  if (moisture !== null && moisture > settings.baseMoisturePct) {
    if (settings.moistureRule === 'PriceBands') {
      const bands: PriceBand[] = [...settings.priceBands].sort((a, b) => a.upToPct - b.upToPct)
      const band = bands.find((item) => moisture <= item.upToPct) ?? bands[bands.length - 1]
      percent = band ? band.percentOfPrice : 100
      if (percent < 100) note = `Moisture ${trim(moisture)}%: paid at ${trim(percent)}% of the price.`
    } else {
      const cut = Math.min(100, (moisture - settings.baseMoisturePct) * settings.deductionPctPerPoint)
      deducted = round((netKgValue * cut) / 100, 1)
      if (deducted > 0) note = `Moisture ${trim(moisture)}% is ${trim(moisture - settings.baseMoisturePct)} points over ${trim(settings.baseMoisturePct)}%: ${trim(deducted)} kg not paid.`
    }
  }
  const payable = round(netKgValue - deducted, 1)
  return {
    decision: deducted > 0 || percent < 100 ? 'AcceptedWithDeduction' : 'Accepted',
    rejectedKg: 0,
    deductedKg: deducted,
    payableKg: payable,
    pricePercent: percent,
    amount: round((payable / 1000) * pricePerTonne * (percent / 100), 2),
    reason: note,
  }
}

/** Days of cover, written the way a manager says it. */
export function coverLabel(days: number | null): string {
  if (days === null) return 'No burn recorded yet'
  if (days < 1) return 'Under a day'
  return `${trim(days)} days`
}

export function tonnes(kg: number): string {
  return `${trim(round(kg / 1000, 2))} t`
}

export function round(value: number, places: number): number {
  const factor = 10 ** places
  return Math.round((value + Number.EPSILON) * factor) / factor
}

function trim(value: number): string {
  return Number.isInteger(value) ? String(value) : String(round(value, 1))
}
