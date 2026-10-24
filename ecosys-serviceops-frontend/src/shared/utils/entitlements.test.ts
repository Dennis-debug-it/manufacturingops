import { describe, expect, it } from 'vitest'
import { canAccessModulePath, requiredModulesForPath } from './entitlements'

describe('module entitlements', () => {
  it('puts Projects behind its own add-on module', () => {
    expect(requiredModulesForPath('/projects')).toEqual(['projects'])
    expect(requiredModulesForPath('/projects/abc/milestones')).toEqual(['projects'])
    expect(canAccessModulePath('/projects', ['operations', 'finance'])).toBe(false)
    expect(canAccessModulePath('/projects', ['finance', 'projects'])).toBe(true)
  })

  it('leaves the existing module boundaries alone', () => {
    expect(requiredModulesForPath('/contracts')).toEqual(['operations'])
    expect(requiredModulesForPath('/finance/invoices')).toEqual(['finance'])
    expect(requiredModulesForPath('/projectsish')).toEqual([])
  })
})
