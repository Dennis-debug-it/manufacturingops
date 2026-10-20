import { describe, expect, it } from 'vitest'
import type { AuthUser, PermissionSet } from '../types/auth'
import { canOpenPath } from './routeAccess'

const user = (role: string, permissions: Partial<PermissionSet>) => ({ role, permissions }) as unknown as AuthUser

describe('canOpenPath', () => {
  const dispatcher = user('dispatcher', { canViewWorkOrders: true, canCreateWorkOrders: true, canAssignWorkOrders: true, canViewMaterials: true, canViewKnowledge: true })
  const finance = user('finance', { canViewWorkOrders: true, canViewFinance: true, canViewReports: true, canViewKnowledge: true })
  const stores = user('materials_controller', { canViewWorkOrders: true, canViewMaterials: true, canManageMaterials: true, canViewReports: true, canViewKnowledge: true })

  it('lets admins open everything', () => {
    expect(canOpenPath(user('admin', {}), '/settings/security')).toBe(true)
    expect(canOpenPath(user('admin', {}), '/team/users')).toBe(true)
  })

  it('keeps settings and user management with the admin', () => {
    expect(canOpenPath(dispatcher, '/settings/branding')).toBe(false)
    expect(canOpenPath(dispatcher, '/team/users')).toBe(false)
    expect(canOpenPath(dispatcher, '/settings/account')).toBe(true)
  })

  it('opens each area to the roles whose work it is', () => {
    expect(canOpenPath(dispatcher, '/dispatch/board')).toBe(true)
    expect(canOpenPath(dispatcher, '/finance/invoices')).toBe(false)
    expect(canOpenPath(finance, '/finance/payables')).toBe(true)
    expect(canOpenPath(finance, '/inventory/stock')).toBe(false)
    expect(canOpenPath(finance, '/dispatch/board')).toBe(false)
    expect(canOpenPath(stores, '/inventory/purchase-orders')).toBe(true)
    expect(canOpenPath(stores, '/finance')).toBe(false)
    expect(canOpenPath(stores, '/dashboard/executive')).toBe(false)
  })

  it('opens the sales pipeline to whoever holds the CRM capability, whatever their role', () => {
    expect(canOpenPath(user('finance', { canViewCrm: true }), '/crm')).toBe(true)
    expect(canOpenPath(user('dispatcher', { canViewCrm: true }), '/crm/tenders/abc')).toBe(true)
    expect(canOpenPath(user('manager', { canViewCrm: false }), '/crm/opportunities/abc')).toBe(false)
    expect(canOpenPath(dispatcher, '/crm')).toBe(false)
    expect(canOpenPath(stores, '/crm')).toBe(false)
  })

  it('matches whole path segments only', () => {
    expect(canOpenPath(dispatcher, '/financed-thing')).toBe(true)
  })
})
