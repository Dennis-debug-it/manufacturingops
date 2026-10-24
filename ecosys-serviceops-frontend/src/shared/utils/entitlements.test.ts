import { describe, expect, it } from 'vitest'
import { canAccessModulePath, requiredModulesForPath } from './entitlements'

const essentials = ['operations', 'finance']
const professional = [...essentials, 'maintenance', 'inventory', 'projects', 'advanced']
const enterprise = [...professional, 'enterprise_tools']

describe('edition entitlements', () => {
  it('gives Essentials the work order to invoice loop', () => {
    for (const path of ['/work-orders', '/dispatch/board', '/finance/invoices', '/finance/quotes', '/finance/billable-work', '/clients']) {
      expect(canAccessModulePath(path, essentials)).toBe(true)
    }
  })

  it('keeps maintenance, inventory, projects and the advanced tools in Professional', () => {
    for (const path of ['/maintenance/schedules', '/assets', '/contracts', '/inventory/stock', '/projects', '/finance/expenses', '/approvals', '/settings/email-intake']) {
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
    expect(requiredModulesForPath('/finance/expenses')).toEqual(['advanced'])
    expect(requiredModulesForPath('/finance/invoices')).toEqual(['finance'])
  })
})
