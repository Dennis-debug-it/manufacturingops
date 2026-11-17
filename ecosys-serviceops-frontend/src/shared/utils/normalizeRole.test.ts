import { describe, expect, it } from 'vitest'
import { normalizeRole } from './normalizeRole'

describe('normalizeRole', () => {
  it('maps the stores role the API sends', () => {
    expect(normalizeRole('MaterialsController')).toBe('materials_controller')
    expect(normalizeRole('materials controller')).toBe('materials_controller')
  })

  it('keeps the other tenant roles', () => {
    expect(normalizeRole('Dispatcher')).toBe('dispatcher')
    expect(normalizeRole('Technician')).toBe('technician')
    expect(normalizeRole('finance')).toBe('finance')
  })

  it('treats an unknown role as read-only', () => {
    expect(normalizeRole('Storeroom wizard')).toBe('viewer')
  })

  it('keeps each kind of platform staff apart, so each sees only its own sections', () => {
    expect(normalizeRole('PlatformOwner')).toBe('super_admin')
    expect(normalizeRole('SuperAdmin')).toBe('super_admin')
    expect(normalizeRole('PlatformFinance')).toBe('platform_finance')
    expect(normalizeRole('FinanceAdmin')).toBe('platform_finance')
    expect(normalizeRole('SupportAdmin')).toBe('platform_support')
    expect(normalizeRole('Support')).toBe('platform_support')
    expect(normalizeRole('ReadOnlyAuditor')).toBe('platform_auditor')
    expect(normalizeRole('Auditor')).toBe('platform_auditor')
  })
})
