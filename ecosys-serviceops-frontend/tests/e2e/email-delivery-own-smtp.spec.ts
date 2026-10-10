import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL

const platformMail = {
  useCustomSmtp: false,
  activeMode: 'Platform',
  host: '',
  port: 587,
  useSsl: false,
  username: null,
  hasPassword: false,
  fromAddress: null,
  managedEmailAddress: 'acme@serviceops.ecosysdigital.co.ke',
  lastTestedAt: null,
  lastError: null,
}

test('the SMTP server fields show only when the workspace uses its own server', async ({ page }) => {
  await registerApiCatchAll(page)
  // The first-login welcome tour would cover the page.
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
  let saved: Record<string, unknown> | null = null
  await page.route(`${API}/api/tenant/email-delivery`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON() as Record<string, unknown>
      return route.fulfill(json({ ...platformMail, useCustomSmtp: true, activeMode: 'Custom', host: 'smtp.acme.test', fromAddress: 'service@acme.test' }))
    }
    return route.fulfill(json(platformMail))
  })
  await loginWithMockSession(page, { role: 'tenantAdmin' })

  await page.goto('/settings/email-delivery')
  await expect(page.getByTestId('platform-mail-note')).toContainText('acme@serviceops.ecosysdigital.co.ke')
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)

  await page.getByLabel('Use my own SMTP server').check()
  await expect(page.getByTestId('platform-mail-note')).toHaveCount(0)
  await page.getByLabel('SMTP Host').fill('smtp.acme.test')
  await page.getByLabel('From Address').fill('service@acme.test')
  await page.getByRole('button', { name: 'Save Changes' }).click()

  await expect(page.getByText('Sending through smtp.acme.test as service@acme.test')).toBeVisible()
  expect(saved).toMatchObject({ useCustomSmtp: true, host: 'smtp.acme.test', fromAddress: 'service@acme.test', port: 587 })

  await page.getByLabel('Use my own SMTP server').uncheck()
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)
  await expect(page.getByTestId('platform-mail-note')).toBeVisible()
})
