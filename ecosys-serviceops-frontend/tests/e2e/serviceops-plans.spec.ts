import { expect, test, type Page } from '@playwright/test'
import { canAccessModulePath } from '../../src/shared/utils/entitlements'
import { E2E_API_URL } from './helpers/test-data'

const editionModules = {
  essentials: ['operations', 'finance'],
  professional: ['operations', 'finance', 'maintenance', 'inventory', 'projects', 'advanced'],
  enterprise: ['operations', 'finance', 'maintenance', 'inventory', 'projects', 'advanced', 'enterprise_tools'],
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
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
  }, session)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) { await route.continue(); return }
    const body = path === '/api/auth/me' ? session : path === '/api/tenant/license' ? { planCode: code, planName: `Ecosys ServiceOps ${code}`, modules } : []
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } })
  })
  await page.goto('/settings')
  await expect(page.locator('aside')).toContainText('Plan Tenant')
}

for (const code of ['essentials', 'professional', 'enterprise'] as const) {
  test(`${code} navigation and settings reflect the edition`, async ({ page }) => {
    await openWorkspace(page, code)
    const nav = page.locator('aside nav')
    await expect(nav.getByRole('link', { name: 'Clients', exact: true })).toBeVisible()
    await expect(nav.getByRole('link', { name: 'Users & Access' })).toBeVisible()
    // Every edition runs the whole work order to invoice loop.
    await expect(nav.getByRole('link', { name: 'Work Orders', exact: true })).toHaveCount(1)
    await expect(nav.getByRole('link', { name: 'Finance', exact: true })).toHaveCount(1)
    await expect(page.getByRole('link', { name: /Payments M-Pesa/ })).toHaveCount(1)
    await expect(page.getByRole('link', { name: /PM Templates Preventive/ })).toHaveCount(code === 'essentials' ? 0 : 1)
    if (code === 'essentials') {
      await page.goto('/maintenance/schedules')
      await expect(page.getByText('This feature is unavailable on your ServiceOps plan.')).toBeVisible()
    }
  })
}

test('cross-module workflow paths require both modules and shared settings stay accessible', () => {
  expect(canAccessModulePath('/finance/billable-work', ['finance'])).toBe(false)
  expect(canAccessModulePath('/finance/billable-work', ['operations'])).toBe(false)
  expect(canAccessModulePath('/finance/billable-work', ['operations', 'finance'])).toBe(true)
  expect(canAccessModulePath('/settings/numbering', ['operations'])).toBe(true)
  expect(canAccessModulePath('/clients', ['finance'])).toBe(true)
})
