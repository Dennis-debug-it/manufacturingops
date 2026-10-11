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
    const dispatcher = ids(user('dispatcher', { canViewWorkOrders: true, canAssignWorkOrders: true, canViewMaterials: true, canViewKnowledge: true }))
    expect(dispatcher).toEqual(expect.arrayContaining(['work-orders', 'dispatch', 'inventory', 'knowledge', 'time-attendance']))
    expect(dispatcher).not.toContain('finance')
    expect(dispatcher).not.toContain('approvals')
    expect(dispatcher).not.toContain('settings')

    const finance = ids(user('finance', { canViewWorkOrders: true, canViewFinance: true, canViewReports: true, canViewKnowledge: true }))
    expect(finance).toEqual(expect.arrayContaining(['finance', 'contracts', 'reports', 'approvals']))
    expect(finance).not.toContain('dispatch')
    expect(finance).not.toContain('inventory')
    expect(finance).not.toContain('time-attendance')
    expect(finance).not.toContain('client-portal')
  })

  it('returns nothing when signed out', () => {
    expect(visibleGuides(null)).toEqual([])
  })
})
