import { expect, test } from '@playwright/test'
import { loginViaUi } from './helpers/auth'
import {
  E2E_API_BASE_URL,
  SUPERADMIN_EMAIL,
  SUPERADMIN_PASSWORD,
} from './helpers/env'
import { installPageGuards } from './helpers/networkGuards'
import { uniqueSuffix } from './helpers/random'

const hasSuperadminCreds = Boolean(SUPERADMIN_EMAIL && SUPERADMIN_PASSWORD)

const platformRoutes = [
  { name: 'platform overview', path: '/platform', heading: /overview/i },
  { name: 'platform tenants', path: '/platform/tenants', heading: /tenants/i },
  { name: 'platform licenses', path: '/platform/licenses', heading: /licenses & subscriptions/i },
  { name: 'platform users', path: '/platform/users', heading: /platform users/i },
  { name: 'platform audit logs', path: '/platform/audit-logs', heading: /audit logs/i },
  { name: 'platform settings', path: '/platform/settings', heading: /^settings$/i },
  { name: 'finance dashboard', path: '/platform/finance', heading: /^finance$/i },
  { name: 'finance quotations', path: '/platform/finance/quotations', heading: /^finance$/i },
  { name: 'finance invoices', path: '/platform/finance/invoices', heading: /^finance$/i },
  { name: 'finance payments', path: '/platform/finance/payments', heading: /^finance$/i },
  { name: 'finance revenue', path: '/platform/finance/revenue', heading: /^finance$/i },
  { name: 'finance expenses', path: '/platform/finance/expenses', heading: /^finance$/i },
  { name: 'platform reports', path: '/platform/reports', heading: /^reports$/i },
] as const

test.describe('superadmin command centre smoke', () => {
  test.skip(!hasSuperadminCreds, 'Set E2E_SUPERADMIN_EMAIL and E2E_SUPERADMIN_PASSWORD to run platform/finance smoke tests.')

  for (const route of platformRoutes) {
    test(`${route.name} page loads`, async ({ page, baseURL }) => {
      const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
      try {
        guards.markAuthenticated()
        await loginViaUi(page, {
          email: SUPERADMIN_EMAIL as string,
          password: SUPERADMIN_PASSWORD as string,
        })
        await page.goto(route.path)
        await expect(page).toHaveURL(new RegExp(`${route.path.replace('/', '\\/')}($|\\?)`))
        await expect(page.getByText(/Command Centre v2 loaded/i)).toBeVisible()
        await expect(page.getByRole('heading', { name: route.heading })).toBeVisible()
      } finally {
        guards.assertNoViolations()
        guards.dispose()
      }
    })
  }

  test('tenant detail email and notifications section is available and finance toggles are absent', async ({ page, baseURL }) => {
    const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
    let verifyCalls = 0
    let testCalls = 0

    try {
      guards.markAuthenticated()
      await loginViaUi(page, {
        email: SUPERADMIN_EMAIL as string,
        password: SUPERADMIN_PASSWORD as string,
      })

      await page.route('**/api/platform/tenants/*/email-settings/verify', async (route) => {
        verifyCalls += 1
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true, lastTestedAt: new Date().toISOString(), lastError: null }),
        })
      })

      await page.route('**/api/platform/tenants/*/email-settings/test', async (route) => {
        testCalls += 1
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true, lastTestedAt: new Date().toISOString(), lastError: null }),
        })
      })

      await page.goto('/platform/tenants')
      await expect(page.getByRole('heading', { name: /tenants/i })).toBeVisible()

      if (await page.getByText('No tenants found').isVisible()) {
        const suffix = uniqueSuffix()
        await page.getByRole('button', { name: /add tenant/i }).click()
        await page.getByLabel('Company Name').fill(`QA Tenant ${suffix}`)
        await page.getByLabel('Slug').fill(`qa-tenant-${suffix}`)
        await page.getByLabel('Contact Email').fill(`qa-tenant-${suffix}@example.com`)
        await page.getByRole('button', { name: /create tenant/i }).click()
      }

      await page.getByRole('button', { name: /^view$/i }).first().click()
      await page.getByRole('button', { name: 'Email & Notifications' }).click()

      await expect(page.getByText('Email Delivery')).toBeVisible()
      await expect(page.getByText('Notification Rules')).toBeVisible()
      await expect(page.getByText('Recipients')).toBeVisible()
      await expect(page.getByText('Test & Verification')).toBeVisible()

      await expect(page.getByText('Finance notification emails')).toHaveCount(0)
      await expect(page.getByText('Invoice notification')).toHaveCount(0)
      await expect(page.getByText('Quotation notification')).toHaveCount(0)
      await expect(page.getByText('Payment notification')).toHaveCount(0)

      await expect(page.getByLabel('SMTP host')).toBeDisabled()
      await page.getByText('Use platform default email settings').locator('..').getByRole('checkbox').click()
      await page.getByText('Override SMTP settings').locator('..').getByRole('checkbox').click()
      await expect(page.getByLabel('SMTP host')).toBeEnabled()

      await page.getByLabel('SMTP host').fill('smtp.example.com')
      await page.getByLabel('SMTP port').fill('587')
      await page.getByLabel('Sender email').fill('alerts@example.com')
      await page.getByLabel('Admin notification emails').fill('ops@example.com, admin@example.com')

      await page.getByRole('button', { name: /save tenant email settings/i }).click()
      await expect(page.getByText('Tenant email settings saved')).toBeVisible()

      await page.getByRole('button', { name: /verify delivery connection/i }).click()
      await expect(page.getByText('Connection verified')).toBeVisible()

      await page.getByRole('button', { name: /send test email/i }).click()
      await expect(page.getByText('Test email sent')).toBeVisible()

      expect(verifyCalls).toBeGreaterThan(0)
      expect(testCalls).toBeGreaterThan(0)
    } finally {
      guards.assertNoViolations()
      guards.dispose()
    }
  })
})
