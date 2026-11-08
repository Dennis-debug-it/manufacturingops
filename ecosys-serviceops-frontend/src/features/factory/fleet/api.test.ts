import { describe, expect, it } from 'vitest'
import { mapLink, serviceDueSoon, worstDocument, type FleetVehicle } from './api'

const vehicle = (patch: Partial<FleetVehicle>): FleetVehicle => ({
  assetId: 'a', assetCode: 'KCA123A', assetName: 'Isuzu', location: null, registrationNumber: 'KCA123A', vehicleType: 'Lorry', make: null, model: null, year: null,
  payloadKg: null, fuelType: 'Diesel', route: null, driverName: null, driverPhone: null, odometerMeterId: null, odometerKm: null, odometerAt: null,
  engineHoursMeterId: null, engineHours: null, kmToService: null, serviceDueOn: null, servicePlanName: null, openJobs: 0, documents: [], trackerDeviceId: null,
  lastLatitude: null, lastLongitude: null, lastSpeedKph: null, lastPositionAt: null, lastPositionSource: null,
  fuel: { latestKmPerLitre: null, averageKmPerLitre: null, usingMore: false }, condition: 'Normal', operationalStatus: 'Operational', ...patch,
})

describe('fleet helpers', () => {
  it('flags a service due within 1,000 km or two weeks', () => {
    const today = new Date('2026-11-08T00:00:00Z')
    expect(serviceDueSoon(vehicle({ kmToService: 800 }), today)).toBe(true)
    expect(serviceDueSoon(vehicle({ kmToService: 4000 }), today)).toBe(false)
    expect(serviceDueSoon(vehicle({ serviceDueOn: '2026-11-15T00:00:00Z' }), today)).toBe(true)
    expect(serviceDueSoon(vehicle({}), today)).toBe(false)
  })

  it('picks the paper that ends first within a month', () => {
    expect(worstDocument(vehicle({ documents: [{ type: 'Insurance', label: 'Insurance', expiresOn: '', daysLeft: 12 }] }))?.type).toBe('Insurance')
    expect(worstDocument(vehicle({ documents: [{ type: 'Inspection', label: 'Inspection', expiresOn: '', daysLeft: 200 }] }))).toBeNull()
  })

  it('links a position to the map', () => {
    expect(mapLink(-0.37, 35.28)).toBe('https://www.openstreetmap.org/?mlat=-0.37&mlon=35.28#map=15/-0.37/35.28')
  })
})
