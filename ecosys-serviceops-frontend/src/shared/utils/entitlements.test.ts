import { describe, expect, it } from 'vitest'
import { canAccessModulePath, requiredModulesForPath } from './entitlements'

const essentials = ['operations', 'finance', 'maintenance']
const professional = [...essentials, 'inventory', 'advanced']
const enterprise = [...professional, 'enterprise_tools']

describe('edition entitlements', () => {
  it('gives Essentials the whole maintenance system', () => {
    for (const path of ['/maintenance/jobs', '/maintenance/pm', '/assets', '/maintenance/fleet', '/work-orders', '/finance/rate-cards']) {
      expect(canAccessModulePath(path, essentials)).toBe(true)
    }
  })

  it('keeps inventory and the advanced tools in Professional', () => {
    for (const path of ['/inventory/stock', '/inventory/purchase-orders', '/approvals', '/settings/email-intake']) {
      expect(canAccessModulePath(path, essentials)).toBe(false)
      expect(canAccessModulePath(path, professional)).toBe(true)
    }
  })

  it('keeps API access, webhooks and monitoring in Enterprise', () => {
    for (const path of ['/settings/integrations', '/settings/monitoring-intake']) {
      expect(canAccessModulePath(path, professional)).toBe(false)
      expect(canAccessModulePath(path, enterprise)).toBe(true)
    }
  })

  it('puts the WhatsApp inbox behind the Business add-on on any edition', () => {
    expect(canAccessModulePath('/whatsapp', enterprise)).toBe(false)
    expect(canAccessModulePath('/whatsapp', [...essentials, 'whatsapp_business'])).toBe(true)
  })

  it('matches whole path segments only', () => {
    expect(requiredModulesForPath('/projectsish')).toEqual([])
    expect(requiredModulesForPath('/finance/rate-cards')).toEqual(['finance'])
    expect(requiredModulesForPath('/crmx')).toEqual([])
  })

})
