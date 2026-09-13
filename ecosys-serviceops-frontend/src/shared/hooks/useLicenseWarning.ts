import { useSyncExternalStore } from 'react'
import { getLicenseWarning, subscribeLicenseWarning } from '../api/licenseWarningStore'

/** The most recent X-License-Warning message from the API, or null when the license is healthy. */
export function useLicenseWarning(): string | null {
  return useSyncExternalStore(subscribeLicenseWarning, getLicenseWarning, () => null)
}
