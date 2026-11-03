import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

const forbiddenTexts = [
  /API not available yet/i,
  /placeholder/i,
  /mock data/i,
  /coming soon/i,
  /Work order summary placeholder/i,
]

async function assertNoForbiddenText(page: import('@playwright/test').Page) {
  for (const pattern of forbiddenTexts) {
    await expect(page.getByText(pattern)).toHaveCount(0)
  }
}

test.describe('Platform Owner command centre', () => {
  test.skip(!hasPlatformCreds, 'Set E2E_PLATFORM_EMAIL or E2E_SUPERADMIN_EMAIL to run command centre tests.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
  })

  const smokeRoutes = [
    { path: '/platform', marker: '[data-testid="command-centre-dashboard"]', label: /platform owner overview/i },
    { path: '/platform/overview', marker: '[data-testid="command-centre-dashboard"]', label: /platform owner overview/i },
    { path: '/platform/tenants', marker: '[data-testid="tenants-page"]', label: /tenants/i },
    { path: '/platform/leads', marker: '[data-testid="leads-page"]', label: /leads/i },
    { path: '/platform/users', marker: 'text=Platform Users', label: /platform users/i },
    { path: '/platform/licensing', marker: 'text=Licensing', label: /licensing/i },
    { path: '/platform/finance', marker: 'text=Finance', label: /finance/i },
    { path: '/platform/reports', marker: 'text=Reports', label: /reports/i },
    { path: '/platform/audit', marker: 'text=Audit Logs', label: /audit/i },
    { path: '/platform/settings', marker: 'text=Platform Settings', label: /platform settings/i },
  ]

  for (const route of smokeRoutes) {
    test(`loads ${route.path}`, async ({ page }) => {
      await page.goto(route.path)
      await expect(page).toHaveURL(new RegExp(route.path.replace(/\//g, '\\/')))
      await expect(page.locator(route.marker)).toBeVisible()
      await expect(page.getByRole('link', { name: /overview/i })).toBeVisible()
      await assertNoForbiddenText(page)
    })
  }

  test('legacy command-centre routes redirect to platform overview', async ({ page }) => {
    await page.goto('/command-centre')
    await expect(page).toHaveURL(/\/platform\/overview/)
    await expect(page.getByTestId('command-centre-dashboard')).toBeVisible()
  })
})
