import { expect, test, type Page } from '@playwright/test'
import { canAccessModulePath } from '../../src/shared/utils/entitlements'
import { E2E_API_URL } from './helpers/test-data'

const editionModules = {
  essentials: ['operations', 'finance', 'maintenance'],
  professional: ['operations', 'finance', 'maintenance', 'inventory', 'advanced'],
  enterprise: ['operations', 'finance', 'maintenance', 'inventory', 'advanced', 'enterprise_tools'],
} as const

async function openWorkspace(page: Page, code: keyof typeof editionModules) {
  const modules = editionModules[code]
  const session = {
    token: 'test-session',
    user: { userId: 'plan-admin', fullName: 'Plan Admin', email: 'admin@example.test', role: 'admin', mustChangePassword: false, permissions: {}, hasAllBranchAccess: true },
    tenant: { tenantId: 'plan-tenant', companyName: 'Plan Tenant', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
    branches: [],
  }
  await page.addInitScript((value) => {
    // Skip the first-login welcome tour; this spec looks at the page behind it.
    localStorage.setItem(`ecosys.first-login-shown.${value.user.userId}`, '1')
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
  }, session)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) { await route.continue(); return }
    const body = path === '/api/auth/me' ? session : path === '/api/tenant/license' ? { planCode: code, planName: `Ecosys Factory Maintenance ${code}`, modules } : []
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } })
  })
  await page.goto('/settings')
  await expect(page.locator('aside')).toContainText('Plan Tenant')
}

for (const code of ['essentials', 'professional', 'enterprise'] as const) {
  test(`${code} navigation and settings reflect the edition`, async ({ page }) => {
    await openWorkspace(page, code)
    const nav = page.locator('aside nav')
    // The maintenance system is in every edition; the sidebar is grouped into sections.
    for (const section of ['Daily work', 'Planning', 'Assets', 'Compliance', 'Team and admin']) {
      await expect(nav.getByRole('button', { name: section })).toBeVisible()
    }
    await expect(nav.getByRole('link', { name: 'Jobs', exact: true })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Planned maintenance' })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Users and access' })).toBeVisible()
    // Service-company screens are not in the sidebar.
    for (const name of ['Work Orders', 'Clients', 'Contracts', 'Sales']) {
      await expect(nav.getByRole('link', { name, exact: true })).toHaveCount(0)
    }
    // Stores purchasing comes with Professional.
    await expect(nav.getByRole('link', { name: 'Purchasing' })).toHaveCount(code === 'essentials' ? 0 : 1)
    // A section folds away and stays folded.
    await nav.getByRole('button', { name: 'Planning' }).click()
    await expect(nav.getByRole('link', { name: 'Planned maintenance' })).toHaveCount(0)
    await page.reload()
    await expect(nav.getByRole('link', { name: 'Planned maintenance' })).toHaveCount(0)
  })
}

test('shared settings stay reachable on every edition', () => {
  expect(canAccessModulePath('/settings/numbering', ['operations'])).toBe(true)
  expect(canAccessModulePath('/clients', ['finance'])).toBe(true)
  expect(canAccessModulePath('/maintenance/jobs', ['operations', 'finance', 'maintenance'])).toBe(true)
})
