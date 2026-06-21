import { expect, test, type Page } from '@playwright/test'
import { loginAsPlatformOwner, primeTenantSession } from './helpers/auth'
import { assertNoHorizontalOverflow, openAppNavigationIfCollapsed } from './helpers/layout'
import { E2E_API_URL } from './helpers/test-data'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)
const hasTenantAuth =
  Boolean(process.env.E2E_TENANT_EMAIL && process.env.E2E_TENANT_PASSWORD) ||
  Boolean(process.env.E2E_API_URL || process.env.E2E_API_BASE_URL || process.env.VITE_API_BASE_URL || E2E_API_URL)

const tenantViewports = [
  { name: 'mobile', width: 360, height: 800 },
  { name: 'mobile-large', width: 430, height: 932 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'laptop', width: 1024, height: 768 },
  { name: 'desktop', width: 1366, height: 768 },
] as const

async function expectHeading(page: Page, name: RegExp) {
  await expect(page.getByRole('heading', { name })).toBeVisible()
  await assertNoHorizontalOverflow(page)
}

async function openTenantNav(page: Page, itemName: RegExp) {
  await openAppNavigationIfCollapsed(page)
  await page.getByRole('link', { name: itemName }).first().click()
}

test.describe('Responsive layout hardening', () => {
  test.describe.configure({ mode: 'serial' })

  for (const viewport of tenantViewports) {
    test(`tenant workspace remains usable at ${viewport.name} ${viewport.width}x${viewport.height}`, async ({ page }) => {
      test.skip(!hasTenantAuth, 'Set tenant credentials or API base URL to run responsive tenant viewport tests.')
      await page.setViewportSize({ width: viewport.width, height: viewport.height })
      await primeTenantSession(page)
      await page.goto('/dashboard')
      await expect(page).toHaveURL(/\/dashboard/)

      await expectHeading(page, /operational overview|dashboard/i)

      await openTenantNav(page, /dashboard/i)
      await expect(page).toHaveURL(/\/dashboard/)
      await expectHeading(page, /operational overview|dashboard/i)

      await openTenantNav(page, /work orders/i)
      await expect(page).toHaveURL(/\/work-orders/)
      await expectHeading(page, /work orders/i)

      await openTenantNav(page, /clients/i)
      await expect(page).toHaveURL(/\/clients/)
      await expectHeading(page, /client register/i)

      await openTenantNav(page, /assets/i)
      await expect(page).toHaveURL(/\/assets/)
      await expectHeading(page, /asset register/i)

      await openTenantNav(page, /materials/i)
      await expect(page).toHaveURL(/\/materials/)
      await expectHeading(page, /materials and stock/i)

      await openTenantNav(page, /^settings$/i)
      await expect(page).toHaveURL(/\/settings\/company-profile/)
      await page.locator('a[href="/settings/email-intake"]:visible').first().click()
      await expect(page).toHaveURL(/\/settings\/email-intake/)
      await expect(page.getByTestId('email-intake-page')).toBeVisible()
      await assertNoHorizontalOverflow(page)
    })

    test(`platform workspace remains usable at ${viewport.name} ${viewport.width}x${viewport.height}`, async ({ page }) => {
      test.skip(!hasPlatformCreds, 'Set platform owner credentials to run responsive platform viewport tests.')
      await page.setViewportSize({ width: viewport.width, height: viewport.height })
      await loginAsPlatformOwner(page)

      await page.goto('/platform')
      await expect(page.getByTestId('command-centre-dashboard')).toBeVisible()
      await assertNoHorizontalOverflow(page)

      await openAppNavigationIfCollapsed(page)
      await page.getByRole('link', { name: /tenants/i }).click()
      await expect(page).toHaveURL(/\/platform\/tenants/)
      await assertNoHorizontalOverflow(page)

      await openAppNavigationIfCollapsed(page)
      await page.getByRole('link', { name: /platform users/i }).click()
      await expect(page).toHaveURL(/\/platform\/users/)
      await assertNoHorizontalOverflow(page)

      await openAppNavigationIfCollapsed(page)
      await page.getByRole('link', { name: /audit logs/i }).click()
      await expect(page).toHaveURL(/\/platform\/audit-logs/)
      await assertNoHorizontalOverflow(page)

      await openAppNavigationIfCollapsed(page)
      await page.getByRole('link', { name: /^settings$/i }).click()
      await expect(page).toHaveURL(/\/platform\/settings/)
      await expect(page.getByTestId('platform-settings-page')).toBeVisible()
      await assertNoHorizontalOverflow(page)
    })
  }

  test('logout and login again does not freeze the UI', async ({ page }) => {
    test.skip(!hasPlatformCreds, 'Set platform owner credentials to run logout/login responsive verification.')
    await page.setViewportSize({ width: 390, height: 844 })
    await loginAsPlatformOwner(page)

    await openAppNavigationIfCollapsed(page)
    await page.getByTestId('user-menu-trigger').click()
    await page.getByTestId('logout-button').click()
    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByRole('button', { name: /^login$/i })).toBeVisible()
    await assertNoHorizontalOverflow(page)

    await loginAsPlatformOwner(page)
    await page.goto('/platform')
    await expect(page.getByTestId('command-centre-dashboard')).toBeVisible()
    await assertNoHorizontalOverflow(page)
  })
})
