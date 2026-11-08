import { describe, expect, it } from 'vitest'
import type { AppRole } from '../types/auth'
import { navGroups, tenantNavItems } from './navigation'

const forRole = (role: AppRole) => tenantNavItems.filter((item) => item.roles.includes(role))

describe('tenant navigation', () => {
  it('puts every item in a known section', () => {
    for (const item of tenantNavItems) expect(navGroups).toContain(item.group)
  })

  it('lists each screen once', () => {
    const paths = tenantNavItems.map((item) => item.to)
    expect(new Set(paths).size).toBe(paths.length)
  })

  it('leaves the service-company screens out of the sidebar', () => {
    const paths = tenantNavItems.map((item) => item.to)
    for (const path of ['/work-orders', '/clients', '/contracts', '/calendar', '/action-items', '/inventory']) {
      expect(paths).not.toContain(path)
    }
  })

  it('keeps each role to a short list', () => {
    expect(forRole('technician').length).toBeLessThanOrEqual(14)
    expect(forRole('materials_controller').length).toBeLessThanOrEqual(8)
    expect(forRole('finance').length).toBeLessThanOrEqual(6)
    // Even the admin sees sections, not one long list.
    const adminGroups = new Set(forRole('admin').map((item) => item.group))
    expect(adminGroups.size).toBe(navGroups.length)
  })

  it('gives each section at most six items', () => {
    for (const group of navGroups) expect(tenantNavItems.filter((item) => item.group === group).length).toBeLessThanOrEqual(6)
  })
})
