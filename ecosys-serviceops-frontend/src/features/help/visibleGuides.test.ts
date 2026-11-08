import { describe, expect, it } from 'vitest'
import type { AuthUser, PermissionSet } from '../../shared/types/auth'
import { visibleGuides } from './HelpPage'
import { guides } from './guides'

const user = (role: string, permissions: Partial<PermissionSet>) => ({ role, permissions }) as unknown as AuthUser
const ids = (u: AuthUser) => visibleGuides(u).map((guide) => guide.id)

describe('visibleGuides', () => {
  it('gives the admin every office guide', () => {
    expect(ids(user('admin', {}))).toEqual(guides.filter((guide) => !guide.technicianOnly).map((guide) => guide.id))
  })

  it('shows each role only the areas it can open', () => {
    const dispatcher = ids(user('dispatcher', { canViewWorkOrders: true, canAssignWorkOrders: true, canViewMaterials: true }))
    expect(dispatcher).toEqual(expect.arrayContaining(['work-orders', 'inventory', 'time-attendance']))
    expect(dispatcher).not.toContain('finance')
    expect(dispatcher).not.toContain('approvals')
    expect(dispatcher).not.toContain('settings')

    const finance = ids(user('finance', { canViewWorkOrders: true, canViewFinance: true, canViewReports: true }))
    expect(finance).toEqual(expect.arrayContaining(['finance', 'contracts', 'reports', 'approvals']))
    expect(finance).not.toContain('inventory')
    expect(finance).not.toContain('time-attendance')
  })

  it('hides guides for areas the edition does not include, even from the admin', () => {
    const essentials = (path: string) => !['/maintenance', '/inventory', '/assets', '/contracts'].some((prefix) => path.startsWith(prefix))
    const visible = visibleGuides(user('admin', {}), essentials).map((guide) => guide.id)
    expect(visible).toContain('work-orders')
    expect(visible).toContain('finance')
    expect(visible).not.toContain('maintenance')
  })

  it("hides guides for screens the workspace's plan doesn't include", () => {
    const operationsOnly = (path: string) => !path.startsWith('/finance')
    const guidesOnOperations = visibleGuides(user('admin', {}), operationsOnly).map((guide) => guide.id)
    expect(guidesOnOperations).not.toContain('finance')
    expect(guidesOnOperations).toContain('work-orders')
  })

  it('returns nothing when signed out', () => {
    expect(visibleGuides(null)).toEqual([])
  })
})
