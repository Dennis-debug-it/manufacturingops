import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { E2E_REAL_EMAIL } from './helpers/test-data'
import { gotoPlatformSettings } from './helpers/navigation'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

async function openEmailPanel(page: import('@playwright/test').Page) {
  await gotoPlatformSettings(page)
  await page.getByRole('button', { name: 'Email & Notifications' }).click()
  await expect(page.getByTestId('settings-panel-email-notifications')).toBeVisible()
}

test.describe('Platform email notifications API wiring', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform email-notification tests.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
    await openEmailPanel(page)
  })

  test('validates required fields and invalid values', async ({ page }) => {
    await page.getByTestId('enable-email-notifications-toggle').check()
    await page.getByTestId('smtp-host-input').fill('')
    await page.getByTestId('smtp-port-input').fill('0')
    await page.getByTestId('sender-email-input').fill('not-an-email')
    await page.getByTestId('save-email-settings-button').click()

    await expect(page.getByText(/validation failed/i)).toBeVisible()
    await expect(page.getByText(/SMTP host is required|SMTP port must be a positive number|Sender email must be a valid email address/i)).toBeVisible()
  })

  test('verify, test, and save call expected endpoints and show success/error states', async ({ page }) => {
    let verifyCalls = 0
    let testCalls = 0
    let saveCalls = 0

    await page.route('**/api/platform/settings/email/verify', async (route) => {
      verifyCalls += 1
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          verifyCalls === 1
            ? { success: true, lastTestedAt: new Date().toISOString(), lastError: null }
            : { success: false, lastTestedAt: new Date().toISOString(), lastError: 'Unable to connect to SMTP host' },
        ),
      })
    })

    await page.route('**/api/platform/settings/email/test', async (route) => {
      testCalls += 1
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          testCalls === 1
            ? { success: true, lastTestedAt: new Date().toISOString(), lastError: null }
            : { success: false, lastTestedAt: new Date().toISOString(), lastError: 'Mailbox unavailable' },
        ),
      })
    })

    await page.route('**/api/platform/settings/email', async (route) => {
      if (route.request().method().toUpperCase() === 'PUT') {
        saveCalls += 1
      }
      await route.continue()
    })

    await page.getByTestId('enable-email-notifications-toggle').check()
    await page.getByTestId('smtp-host-input').fill('smtp.example.com')
    await page.getByTestId('smtp-port-input').fill('587')
    await page.getByTestId('sender-email-input').fill('alerts@example.com')

    if (!E2E_REAL_EMAIL) {
      await page.getByTestId('verify-smtp-button').click()
      await expect(page.getByText(/connection verified/i)).toBeVisible()

      await page.getByTestId('send-test-email-button').click()
      await expect(page.getByText(/test email sent/i)).toBeVisible()

      await page.getByTestId('verify-smtp-button').click()
      await expect(page.getByText(/connection verification failed/i)).toBeVisible()

      await page.getByTestId('send-test-email-button').click()
      await expect(page.getByText(/test email failed/i)).toBeVisible()
    }

    await page.getByTestId('save-email-settings-button').click()
    await expect(page.getByText(/email settings saved/i)).toBeVisible()

    if (!E2E_REAL_EMAIL) {
      expect(verifyCalls).toBeGreaterThan(0)
      expect(testCalls).toBeGreaterThan(0)
    }
    expect(saveCalls).toBeGreaterThan(0)
  })
})
