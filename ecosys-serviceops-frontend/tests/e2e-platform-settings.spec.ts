import { expect, test } from '@playwright/test'
import { loginViaUi } from './helpers/auth'
import { E2E_API_BASE_URL, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/env'
import { installPageGuards } from './helpers/networkGuards'

const hasSuperadminCreds = Boolean(SUPERADMIN_EMAIL && SUPERADMIN_PASSWORD)

test.describe('platform settings module smoke', () => {
  test.skip(!hasSuperadminCreds, 'Set E2E_SUPERADMIN_EMAIL and E2E_SUPERADMIN_PASSWORD to run platform settings smoke tests.')

  test('settings sections, template editor/preview, numbering preview, and save actions are stable', async ({ page, baseURL }) => {
    const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })

    try {
      guards.markAuthenticated()
      await loginViaUi(page, {
        email: SUPERADMIN_EMAIL as string,
        password: SUPERADMIN_PASSWORD as string,
      })

      await page.goto('/platform/settings')
      await expect(page.getByRole('heading', { name: /^settings$/i })).toBeVisible()
      await expect(page.getByRole('button', { name: 'General' })).toBeVisible()
      await expect(page.getByRole('button', { name: 'Email & Notifications' })).toBeVisible()
      await expect(page.getByRole('button', { name: 'Security' })).toBeVisible()

      await page.getByRole('button', { name: 'General' }).click()
      await expect(page.getByRole('button', { name: 'Save General Settings' })).toBeVisible()

      await page.getByRole('button', { name: 'Email & Notifications' }).click()
      await expect(page.getByRole('button', { name: 'Save Email Settings' })).toBeVisible()

      await page.getByRole('button', { name: 'Numbering' }).click()
      await expect(page.getByRole('button', { name: 'Save Numbering Settings' })).toBeVisible()
      await expect(page.locator('text=/[A-Z]{2,4}-\\d{3,}/').first()).toBeVisible()

      await page.getByRole('button', { name: 'Security' }).click()
      await page.getByRole('button', { name: 'Save Security Settings' }).click()

      await page.getByRole('button', { name: 'System Preferences' }).click()
      await page.getByRole('button', { name: 'Save System Preferences' }).click()
    } finally {
      guards.assertNoViolations()
      guards.dispose()
    }
  })
})
