import { describe, expect, it } from 'vitest'
import { niceTicks } from './ReadingsChart'

describe('niceTicks', () => {
  it('picks round values inside the range', () => {
    expect(niceTicks(21_390, 22_110)).toEqual([21_400, 21_600, 21_800, 22_000])
    expect(niceTicks(108.2, 136.8)).toEqual([110, 120, 130])
    expect(niceTicks(0.1, 0.9)).toEqual([0.2, 0.4, 0.6, 0.8])
  })
})
