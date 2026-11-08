import { describe, expect, it } from 'vitest'
import { sampleLabel } from './SensorsPage'

describe('sampleLabel', () => {
  it('names the sample interval', () => {
    expect(sampleLabel(0)).toBe('Every reading')
    expect(sampleLabel(60)).toBe('1 min')
    expect(sampleLabel(300)).toBe('5 min')
    expect(sampleLabel(3600)).toBe('1 h')
    expect(sampleLabel(45)).toBe('45 s')
  })
})
