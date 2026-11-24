import { describe, expect, it } from 'vitest'
import { assess, averageMoisture, coverLabel, netKg, tonnes } from './logic'

const settings = {
  moistureRule: 'DeductKg',
  baseMoisturePct: 25,
  rejectMoisturePct: 45,
  deductionPctPerPoint: 1.5,
  priceBands: [{ upToPct: 30, percentOfPrice: 100 }, { upToPct: 35, percentOfPrice: 90 }, { upToPct: 45, percentOfPrice: 65 }],
}

const measure = { basis: 'Weighbridge', grossKg: null, tareKg: null, stackedM3: null, loads: null, kgPerLoad: null, densityKgPerM3: null }

describe('netKg', () => {
  it('takes the tare off the gross', () => {
    expect(netKg({ ...measure, grossKg: 12_500, tareKg: 5_300 })).toEqual({ kg: 7200, problem: null })
  })

  it('works a stacked volume or a count of loads into kg', () => {
    expect(netKg({ ...measure, basis: 'StackedVolume', stackedM3: 10, densityKgPerM3: 380 }).kg).toBe(3800)
    expect(netKg({ ...measure, basis: 'LoadCount', loads: 3, kgPerLoad: 2000 }).kg).toBe(6000)
  })

  it('says what is missing instead of guessing', () => {
    expect(netKg({ ...measure, grossKg: 5_000, tareKg: 6_000 }).problem).toMatch(/lower than the gross/)
    expect(netKg({ ...measure, basis: 'StackedVolume', stackedM3: 10 }).problem).toMatch(/kind of wood/)
    expect(netKg({ ...measure, basis: 'LoadCount', loads: 2 }).problem).toMatch(/weight of one load/)
    expect(netKg({ ...measure, basis: 'Guess' }).kg).toBeNull()
  })
})

describe('averageMoisture', () => {
  it('averages the samples taken and ignores the blanks', () => {
    expect(averageMoisture([30, 32, 34])).toBe(32)
    expect(averageMoisture([30, null, NaN])).toBe(30)
    expect(averageMoisture([])).toBeNull()
  })
})

describe('assess', () => {
  it('pays dry wood in full', () => {
    expect(assess(settings, 10_000, 22, 5000, false, null)).toMatchObject({ decision: 'Accepted', payableKg: 10_000, amount: 50_000 })
  })

  it('takes a share off the weight for each point over the base', () => {
    const result = assess(settings, 10_000, 33, 5000, false, null)
    expect(result).toMatchObject({ decision: 'AcceptedWithDeduction', deductedKg: 1200, payableKg: 8800, amount: 44_000 })
    expect(result.reason).toMatch(/1200 kg not paid/)
  })

  it('pays the whole load at a share of the price when the factory uses bands', () => {
    const banded = { ...settings, moistureRule: 'PriceBands' }
    expect(assess(banded, 10_000, 28, 5000, false, null)).toMatchObject({ decision: 'Accepted', pricePercent: 100 })
    expect(assess(banded, 10_000, 33, 5000, false, null)).toMatchObject({ decision: 'AcceptedWithDeduction', pricePercent: 90, deductedKg: 0, amount: 45_000 })
  })

  it('rejects wood over the limit unless a manager lets it in', () => {
    const wet = assess(settings, 10_000, 52, 5000, false, null)
    expect(wet).toMatchObject({ decision: 'Rejected', rejectedKg: 10_000, amount: 0 })
    expect(wet.reason).toMatch(/above the 45% limit/)
    expect(assess(settings, 10_000, 52, 5000, false, null, true).decision).not.toBe('Rejected')
    expect(assess(settings, 10_000, 20, 5000, true, 'Full of soil')).toMatchObject({ decision: 'Rejected', reason: 'Full of soil' })
  })
})

describe('labels', () => {
  it('writes tonnes and cover plainly', () => {
    expect(tonnes(6500)).toBe('6.5 t')
    expect(coverLabel(null)).toBe('No burn recorded yet')
    expect(coverLabel(0.4)).toBe('Under a day')
    expect(coverLabel(3.25)).toBe('3.3 days')
  })
})
