import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL
const URL = `${API}/api/settings/email-intake`

const off = {
  isEnabled: false, intakeEmailAddress: null, host: '', port: 993, useSsl: true, username: null, hasPassword: false,
  defaultClientId: null, defaultBranchId: null, defaultAssignmentGroupId: null, defaultPriority: 'Medium',
  createWorkOrderFromUnknownSender: false, subjectParsingRules: null, allowedSenderDomains: null,
  lastCheckedAt: null, isConnectionHealthy: false, lastError: null, autoReplyEnabled: true, isReading: false,
}

test('a workspace connects its mailbox, and emails show what became of them', async ({ page }) => {
  await registerApiCatchAll(page)
  // The first-login welcome tour would cover the page.
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
  let current: Record<string, unknown> = { ...off }
  let saved: Record<string, unknown> | null = null
  let tested: Record<string, unknown> | null = null
  const preflight = async (route: import('@playwright/test').Route) => route.request().method() === 'OPTIONS' && (await route.fulfill({ status: 204, headers: CORS_HEADERS }), true)

  await page.route(URL, async (route) => {
    if (await preflight(route)) return
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON() as Record<string, unknown>
      current = { ...current, ...saved, hasPassword: true, isReading: true, lastCheckedAt: '2026-10-10T08:00:00Z', isConnectionHealthy: true }
    }
    return route.fulfill(json(current))
  })
  await page.route(`${URL}/test-connection`, async (route) => {
    if (await preflight(route)) return
    tested = route.request().postDataJSON() as Record<string, unknown>
    return route.fulfill(json({ success: true, lastCheckedAt: '2026-10-10T08:00:00Z', lastError: null }))
  })
  await page.route(`${URL}/messages`, async (route) => {
    if (await preflight(route)) return
    return route.fulfill(json([
      { id: 'm2', receivedAt: '2026-10-10T08:05:00Z', fromAddress: 'someone@stranger.test', fromName: null, subject: 'Quote please', outcome: 'Rejected', detail: "The sender isn't a known client contact.", workOrderId: null, workOrderNumber: null, autoReplySent: true },
      { id: 'm1', receivedAt: '2026-10-10T08:04:00Z', fromAddress: 'ann@acme.test', fromName: 'Ann Acme', subject: 'Lift stuck - URGENT', outcome: 'WorkOrderCreated', detail: 'Created WO-000002.', workOrderId: 'wo-2', workOrderNumber: 'WO-000002', autoReplySent: true },
    ]))
  })
  await loginWithMockSession(page, { role: 'tenantAdmin' })

  await page.goto('/settings')
  await page.locator('a[href="/settings/email-intake"]:visible').first().click()
  await expect(page.getByRole('heading', { name: 'Email Intake' })).toBeVisible()
  await expect(page.getByTestId('intake-status')).toContainText('Email intake is off')

  await page.getByLabel('Turn emails into work orders').check()
  await page.getByLabel('Service email address').fill('service@acme-fm.test')
  await page.getByLabel('Incoming mail server (IMAP)').fill('imap.acme-fm.test')
  await page.getByLabel('Mailbox username').fill('service@acme-fm.test')
  await page.getByLabel('Mailbox password').fill('app-password')
  await page.getByRole('button', { name: 'Test connection' }).click()
  await expect(page.getByText('Connected — the mailbox can be read.')).toBeVisible()
  expect(tested).toMatchObject({ host: 'imap.acme-fm.test', port: 993, useSsl: true, username: 'service@acme-fm.test', password: 'app-password' })

  // Only shown once unknown senders are allowed.
  await expect(page.getByLabel('Log them under this client')).toHaveCount(0)
  await page.getByRole('button', { name: 'Save Changes' }).click()

  await expect(page.getByTestId('intake-status')).toContainText('Reading service@acme-fm.test every minute')
  expect(saved).toMatchObject({ isEnabled: true, mailboxProvider: 'IMAP', host: 'imap.acme-fm.test', port: 993, username: 'service@acme-fm.test', password: 'app-password', autoReplyEnabled: true })

  const recent = page.getByTestId('intake-messages')
  await expect(recent).toContainText('Lift stuck - URGENT')
  await expect(recent).toContainText('Work order created')
  await expect(recent).toContainText('Not recognised')
  await expect(recent.getByRole('link', { name: 'WO-000002' })).toHaveAttribute('href', '/work-orders/wo-2')
})
