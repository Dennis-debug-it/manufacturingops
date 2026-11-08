import { describe, expect, it } from 'vitest'
import type { AuthUser, PermissionSet } from '../types/auth'
import { canOpenPath } from './routeAccess'

const user = (role: string, permissions: Partial<PermissionSet>) => ({ role, permissions }) as unknown as AuthUser

describe('canOpenPath', () => {
  const dispatcher = user('dispatcher', { canViewWorkOrders: true, canCreateWorkOrders: true, canAssignWorkOrders: true, canViewMaterials: true })
  const finance = user('finance', { canViewWorkOrders: true, canViewFinance: true, canViewReports: true })
  const stores = user('materials_controller', { canViewWorkOrders: true, canViewMaterials: true, canManageMaterials: true, canViewReports: true })

  it('lets admins open everything', () => {
    expect(canOpenPath(user('admin', {}), '/settings/security')).toBe(true)
    expect(canOpenPath(user('admin', {}), '/team/users')).toBe(true)
  })

  it('keeps settings and user management with the admin', () => {
    expect(canOpenPath(dispatcher, '/settings/branding')).toBe(false)
    expect(canOpenPath(dispatcher, '/team/users')).toBe(false)
    expect(canOpenPath(dispatcher, '/settings/account')).toBe(true)
    // Everyone chooses their own notifications; the workspace matrix stays with the admin.
    expect(canOpenPath(dispatcher, '/settings/notifications')).toBe(true)
    expect(canOpenPath(dispatcher, '/settings/workspace-notifications')).toBe(false)
    expect(canOpenPath(dispatcher, '/notifications')).toBe(true)
  })

  it('opens each area to the roles whose work it is', () => {
    expect(canOpenPath(dispatcher, '/work-orders')).toBe(true)
    expect(canOpenPath(dispatcher, '/finance/rate-cards')).toBe(false)
    expect(canOpenPath(finance, '/finance/rate-cards')).toBe(true)
    expect(canOpenPath(finance, '/inventory/stock')).toBe(false)
    expect(canOpenPath(stores, '/inventory/purchase-orders')).toBe(true)
    expect(canOpenPath(stores, '/finance')).toBe(false)
    expect(canOpenPath(stores, '/dashboard/executive')).toBe(false)
  })


  it('matches whole path segments only', () => {
    expect(canOpenPath(dispatcher, '/financed-thing')).toBe(true)
  })
})
