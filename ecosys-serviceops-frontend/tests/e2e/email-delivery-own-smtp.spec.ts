import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL

const identity = {
  emailAddress: 'acme@serviceops.ecosysdigital.co.ke',
  displayName: 'Acme Ltd',
  replyToAddress: null,
  financeReplyToAddress: null,
  operationsReplyToAddress: null,
  inboundEnabled: true,
  outboundEnabled: true,
  emailSignature: null,
}

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

test('email settings are one page; mail-server fields show only when the workspace uses its own server', async ({ page }) => {
  await registerApiCatchAll(page)
  // The first-login welcome tour would cover the page.
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
  let savedServer: Record<string, unknown> | null = null
  let savedIdentity: Record<string, unknown> | null = null
  await page.route(`${API}/api/tenant/email-identity`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'PUT') {
      savedIdentity = route.request().postDataJSON() as Record<string, unknown>
      return route.fulfill(json({ ...identity, ...savedIdentity }))
    }
    return route.fulfill(json(identity))
  })
  await page.route(`${API}/api/tenant/email-delivery`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'PUT') {
      savedServer = route.request().postDataJSON() as Record<string, unknown>
      return route.fulfill(json({ ...platformMail, useCustomSmtp: true, activeMode: 'Custom', host: 'smtp.acme.test', fromAddress: 'service@acme.test' }))
    }
    return route.fulfill(json(platformMail))
  })
  await loginWithMockSession(page, { role: 'tenantAdmin' })

  // The old Email Delivery address now opens the single Email page.
  await page.goto('/settings/email-delivery')
  await expect(page).toHaveURL(/\/settings\/email$/)
  await expect(page.getByTestId('sending-from')).toHaveText('acme@serviceops.ecosysdigital.co.ke')
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)
  // Options that had no effect are gone.
  await expect(page.getByText('Email Signature')).toHaveCount(0)
  await expect(page.getByText('Enable inbound email')).toHaveCount(0)

  await page.getByLabel('Send from our own mail server (advanced)').check()
  await page.getByLabel('SMTP Host').fill('smtp.acme.test')
  await page.getByLabel('From Address').fill('service@acme.test')
  await page.getByRole('button', { name: 'Save Changes' }).click()

  await expect(page.getByTestId('sending-from')).toHaveText('service@acme.test')
  expect(savedServer).toMatchObject({ useCustomSmtp: true, host: 'smtp.acme.test', fromAddress: 'service@acme.test', port: 587 })
  // Hidden settings are sent back unchanged.
  expect(savedIdentity).toMatchObject({ displayName: 'Acme Ltd', inboundEnabled: true, outboundEnabled: true })

  await page.getByLabel('Send from our own mail server (advanced)').uncheck()
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)
})
