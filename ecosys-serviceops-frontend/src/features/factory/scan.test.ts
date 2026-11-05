import { describe, expect, it } from 'vitest'
import { findOffline, normalizeScannedCode } from './scan'
import type { OfflineScanEntry } from './types'

const entry = (patch: Partial<OfflineScanEntry>): OfflineScanEntry => ({
  assetId: 'a1', assetCode: 'FBD-01', assetName: 'Fluid bed dryer', location: 'Chelal › Drying', parentAssetCode: null,
  criticality: 'A', operationalStatus: 'Operational', manufacturer: null, model: null, serialNumber: null,
  tagNumber: 'TAG-000002', oldTagNumbers: ['TAG-000001'], meters: [], ...patch,
})

describe('scanning', () => {
  it('reads a tag number out of a scanned label link', () => {
    expect(normalizeScannedCode('https://app.example.com/scan/TAG-000123')).toBe('TAG-000123')
    expect(normalizeScannedCode('https://app.example.com/scan/tag%2D000124?x=1')).toBe('TAG-000124')
    expect(normalizeScannedCode('  tag-000125 ')).toBe('TAG-000125')
  })

  it('finds assets offline by current tag, replaced tag or asset number', () => {
    const index = [entry({}), entry({ assetId: 'a2', assetCode: 'PMP-01', tagNumber: null, oldTagNumbers: [] })]
    expect(findOffline(index, 'TAG-000002')).toEqual({ entry: index[0], warning: null })
    expect(findOffline(index, 'TAG-000001')?.warning).toContain('replaced by TAG-000002')
    expect(findOffline(index, 'PMP-01')?.warning).toBe('This asset has no tag yet.')
    expect(findOffline(index, 'NOPE')).toBeNull()
    expect(findOffline(undefined, 'TAG-000002')).toBeNull()
  })
})
