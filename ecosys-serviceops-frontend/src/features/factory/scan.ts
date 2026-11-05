import type { OfflineScanEntry } from './types'

/**
 * Turns whatever the camera or keyboard gave us into a tag number or asset code: a label's
 * link (https://…/scan/TAG-000123) becomes its last path segment; everything is trimmed and
 * upper-cased, as the server does.
 */
export function normalizeScannedCode(raw: string): string {
  let value = raw.trim()
  try {
    const url = new URL(value)
    if (url.protocol === 'http:' || url.protocol === 'https:') {
      const segments = url.pathname.split('/').filter(Boolean)
      value = segments[segments.length - 1] ?? value
    }
  } catch {
    // Not a link: use as typed.
  }
  try {
    value = decodeURIComponent(value)
  } catch {
    // Leave malformed escapes as they are.
  }
  return value.trim().toUpperCase()
}

export interface OfflineMatch {
  entry: OfflineScanEntry
  warning: string | null
}

/** Finds an asset in the phone's copy of the register by active tag, old tag or asset code. */
export function findOffline(index: OfflineScanEntry[] | undefined, code: string): OfflineMatch | null {
  if (!index || !code) return null
  const byTag = index.find((entry) => entry.tagNumber?.toUpperCase() === code)
  if (byTag) return { entry: byTag, warning: null }
  const byOldTag = index.find((entry) => entry.oldTagNumbers.some((tag) => tag.toUpperCase() === code))
  if (byOldTag) return { entry: byOldTag, warning: `This tag was replaced${byOldTag.tagNumber ? ` by ${byOldTag.tagNumber}` : ''}. Remove the old label.` }
  const byCode = index.find((entry) => entry.assetCode.toUpperCase() === code)
  if (byCode) return { entry: byCode, warning: byCode.tagNumber ? `Found by asset number. Its tag is ${byCode.tagNumber}.` : 'This asset has no tag yet.' }
  return null
}
