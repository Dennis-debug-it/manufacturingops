import { describe, it, expect, vi } from 'vitest'
import { setLicenseWarning, getLicenseWarning, subscribeLicenseWarning, isLicenseWarningExemptPath } from './licenseWarningStore'

describe('licenseWarningStore', () => {
  it('stores the latest warning and notifies subscribers', () => {
    setLicenseWarning(null)
    const listener = vi.fn()
    const unsubscribe = subscribeLicenseWarning(listener)

    setLicenseWarning('License is suspended.')

    expect(getLicenseWarning()).toBe('License is suspended.')
    expect(listener).toHaveBeenCalledTimes(1)
    unsubscribe()
  })

  it('does not notify again for the identical warning text (no redundant re-renders)', () => {
    setLicenseWarning('License is suspended.')
    const listener = vi.fn()
    const unsubscribe = subscribeLicenseWarning(listener)

    setLicenseWarning('License is suspended.')

    expect(listener).not.toHaveBeenCalled()
    unsubscribe()
  })

  it('treats blank as no warning', () => {
    setLicenseWarning('   ')
    expect(getLicenseWarning()).toBeNull()
  })

  it('clears the warning and notifies once', () => {
    setLicenseWarning('License is suspended.')
    const listener = vi.fn()
    const unsubscribe = subscribeLicenseWarning(listener)

    setLicenseWarning(null)

    expect(getLicenseWarning()).toBeNull()
    expect(listener).toHaveBeenCalledTimes(1)
    unsubscribe()
  })

  describe('isLicenseWarningExemptPath', () => {
    it('exempts auth, platform, and health endpoints', () => {
      expect(isLicenseWarningExemptPath('/api/auth/login')).toBe(true)
      expect(isLicenseWarningExemptPath('/api/platform/tenants')).toBe(true)
      expect(isLicenseWarningExemptPath('/api/health')).toBe(true)
      expect(isLicenseWarningExemptPath('/health')).toBe(true)
    })

    it('does not exempt an ordinary tenant-scoped endpoint', () => {
      expect(isLicenseWarningExemptPath('/api/work-orders')).toBe(false)
    })

    it('exempts a missing or non-API path', () => {
      expect(isLicenseWarningExemptPath(undefined)).toBe(true)
      expect(isLicenseWarningExemptPath('/login')).toBe(true)
    })
  })
})
