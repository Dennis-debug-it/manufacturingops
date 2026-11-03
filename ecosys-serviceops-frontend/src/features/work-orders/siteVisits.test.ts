import { describe, expect, it } from 'vitest'
import { hasLeftSite, isOnSite } from './types'

const day1Arrival = '2026-10-08T05:00:00Z'
const day1Departure = '2026-10-08T14:00:00Z'
const day2Arrival = '2026-10-09T05:30:00Z'

describe('site visits over several days', () => {
  it('is not on site before arriving', () => {
    expect(isOnSite({ arrivalAt: null, departureAt: null })).toBe(false)
    expect(hasLeftSite({ arrivalAt: null, departureAt: null })).toBe(false)
  })

  it('is on site after the first arrival, and has left after the departure', () => {
    expect(isOnSite({ arrivalAt: day1Arrival, departureAt: null, lastArrivalAt: day1Arrival })).toBe(true)
    expect(isOnSite({ arrivalAt: day1Arrival, departureAt: day1Departure, lastArrivalAt: day1Arrival })).toBe(false)
    expect(hasLeftSite({ arrivalAt: day1Arrival, departureAt: day1Departure, lastArrivalAt: day1Arrival })).toBe(true)
  })

  it("is on site on day two even though day one's departure is on record", () => {
    const dayTwo = { arrivalAt: day1Arrival, departureAt: day1Departure, lastArrivalAt: day2Arrival }
    expect(isOnSite(dayTwo)).toBe(true)
    expect(hasLeftSite(dayTwo)).toBe(false)
  })

  it('falls back to the first arrival for records from before the latest arrival was kept', () => {
    expect(isOnSite({ arrivalAt: day1Arrival, departureAt: null })).toBe(true)
  })
})
