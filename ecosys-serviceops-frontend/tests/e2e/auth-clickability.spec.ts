import { expect, test, type Locator, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const BASE = 'http://localhost:5072'

async function mockPlatformAuthSession(page: Page) {
  const tenants = [
    {
      tenantId: 'tenant-1',
      name: 'Acme Facilities',
      slug: 'acme-facilities',
      contactName: 'Jane Doe',
      contactEmail: 'jane@acme.test',
      planName: 'Enterprise',
      licenseStatus: 'Active',
      userCount: 24,
      branchCount: 6,
      status: 'Active',
      createdAt: '2026-05-01T09:00:00.000Z',
    },
  ]

  await registerApiCatchAll(page)
  await loginWithMockSession(page, { role: 'platformOwner', email: 'superadmin@ecosys.local', fullName: 'Lena Atieno' })

  await page.route(`${BASE}/api/platform/tenants`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'GET') {
      await route.fulfill(json(tenants))
      return
    }
    await route.fulfill(json({ success: true }))
  })
}

async function openPlatformTenants(page: Page) {
  await page.goto('/platform/tenants')
  await expect(page).toHaveURL(/\/platform\/tenants/)
  await expect(page.getByTestId('tenants-page')).toBeVisible()
}

async function assertButtonIsClickable(page: Page, button: Locator) {
  await expect(button).toBeVisible()
  await expect(button).toBeEnabled()

  const box = await button.boundingBox()
  expect(box).not.toBeNull()

  const hitTarget = await page.evaluate(({ x, y }) => {
    const element = document.elementFromPoint(x, y)
    if (!(element instanceof HTMLElement)) {
      return { topElement: 'none', blockedByOverlay: false }
    }

    const topElement = [
      element.tagName.toLowerCase(),
      element.id ? `#${element.id}` : '',
      typeof element.className === 'string' && element.className.trim()
        ? `.${element.className.trim().split(/\s+/).slice(0, 3).join('.')}`
        : '',
    ].join('')

    return {
      topElement,
      blockedByOverlay: Boolean(element.closest('[data-ui-overlay="true"]')),
    }
  }, { x: box!.x + box!.width / 2, y: box!.y + box!.height / 2 })

  expect(hitTarget.blockedByOverlay, `Button is blocked by overlay: ${hitTarget.topElement}`).toBeFalsy()
}

async function assertNoBlockingOverlays(page: Page) {
  await expect(page.locator('[data-ui-overlay="true"]')).toHaveCount(0)
  await expect(page.locator('body')).not.toHaveClass(/overflow-hidden|pointer-events-none/)

  const bodyState = await page.evaluate(() => ({
    overflow: document.body.style.overflow,
    pointerEvents: document.body.style.pointerEvents,
  }))

  expect(bodyState.overflow).toBe('')
  expect(bodyState.pointerEvents).toBe('')
}

async function logoutFromTopbar(page: Page) {
  await page.getByTestId('user-menu-trigger').click()
  await page.getByTestId('logout-button').click()
  await expect(page).toHaveURL(/\/login/)
}

test.describe('auth clickability regression', () => {
  test.beforeEach(async ({ page }) => {
    await mockPlatformAuthSession(page)
  })

  test('platform UI stays clickable after logout and login again', async ({ page }) => {
    await openPlatformTenants(page)

    const addTenantButton = page.getByRole('button', { name: /add tenant/i })
    await assertButtonIsClickable(page, addTenantButton)
    await addTenantButton.click()
    await expect(page.getByRole('heading', { name: /add tenant/i })).toBeVisible()
    await page.getByRole('button', { name: /close modal/i }).click()
    await assertNoBlockingOverlays(page)

    await logoutFromTopbar(page)
    await loginWithMockSession(page, { role: 'platformOwner', email: 'superadmin@ecosys.local', fullName: 'Lena Atieno' })
    await openPlatformTenants(page)

    const addTenantButtonAgain = page.getByRole('button', { name: /add tenant/i })
    await assertNoBlockingOverlays(page)
    await assertButtonIsClickable(page, addTenantButtonAgain)
    await addTenantButtonAgain.click()
    await expect(page.getByRole('heading', { name: /add tenant/i })).toBeVisible()
    await page.getByRole('button', { name: /close modal/i }).click()
    await assertNoBlockingOverlays(page)
  })
})
