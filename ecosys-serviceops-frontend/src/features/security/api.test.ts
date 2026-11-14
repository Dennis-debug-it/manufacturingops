import { describe, expect, it } from 'vitest'
import { findCheckpoint, normaliseSerial, stopStates, type GuardBundle, type GuardRoute } from './api'

const bundle: GuardBundle = {
  generatedAt: '2026-11-13T18:00:00Z',
  runs: [],
  routes: [],
  checkpoints: [
    { id: 'c1', code: 'CP-001', name: 'Main gate', factoryId: 'f', method: 'QrOrNfc', tagToken: 'tokenGate1234567', nfcSerial: '04A22B9C1180', latitude: null, longitude: null, radiusM: 50, instructions: null, questions: [], requirePhoto: false },
    { id: 'c2', code: 'CP-002', name: 'Fuel store', factoryId: 'f', method: 'Qr', tagToken: 'tokenFuel1234567', nfcSerial: null, latitude: null, longitude: null, radiusM: 50, instructions: null, questions: ['Padlock intact?'], requirePhoto: false },
  ],
}

describe('guard app helpers', () => {
  it('finds a checkpoint from its QR label or its NFC tag, with no signal', () => {
    expect(findCheckpoint(bundle, 'CHECKPOINT:tokenFuel1234567', false)?.code).toBe('CP-002')
    expect(findCheckpoint(bundle, '04:a2:2b:9c:11:80', true)?.code).toBe('CP-001')
    expect(findCheckpoint(bundle, 'CHECKPOINT:somethingElse', false)).toBeNull()
    expect(normaliseSerial('04:a2:2b')).toBe('04A22B')
  })

  it('shows where the guard is on the route, counting scans still waiting for signal', () => {
    const route: GuardRoute = { id: 'r', name: 'Night', factoryId: 'f', ordered: true, minGapSeconds: 20, stops: [
      { checkpointId: 'c1', sequence: 1, required: true }, { checkpointId: 'c2', sequence: 2, required: true },
      { checkpointId: 'c3', sequence: 3, required: false }, { checkpointId: 'c4', sequence: 4, required: true },
    ] }
    const states = stopStates(route, [{ clientScanId: 's1', checkpointId: 'c1', scannedAt: '', validation: 'Valid', note: null }], ['c2'])
    expect([...states.values()]).toEqual(['done', 'pending-sync', 'optional', 'next'])
    const flagged = stopStates(route, [{ clientScanId: 's1', checkpointId: 'c1', scannedAt: '', validation: 'OutsideGeofence', note: null }], [])
    expect(flagged.get('c1')).toBe('flagged')
    expect(flagged.get('c2')).toBe('next')
  })
})
