import { describe, expect, it } from 'vitest'
import { canAccessModulePath, requiredModulesForPath } from './entitlements'

describe('Sales CRM plan gate', () => {
  it('is a Suite feature: it needs both Operations and Finance', () => {
    expect(requiredModulesForPath('/crm')).toEqual(['operations', 'finance'])
    expect(requiredModulesForPath('/crm/tenders/abc')).toEqual(['operations', 'finance'])
    expect(canAccessModulePath('/crm', ['operations'])).toBe(false)
    expect(canAccessModulePath('/crm/opportunities/abc', ['finance'])).toBe(false)
    expect(canAccessModulePath('/crm', ['operations', 'finance'])).toBe(true)
  })

  it('matches whole path segments only', () => {
    expect(requiredModulesForPath('/crmx')).toEqual([])
  })
})
