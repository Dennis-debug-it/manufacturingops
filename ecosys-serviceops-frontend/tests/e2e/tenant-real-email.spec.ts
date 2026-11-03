import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { E2E_EMAIL_DELIVERY_MODE, E2E_EMAIL_TEST_RECIPIENT, E2E_REAL_EMAIL, E2E_SMTP_HOST, E2E_SMTP_PASSWORD, E2E_SMTP_PORT, E2E_SMTP_SECURE_MODE, E2E_SMTP_SENDER_EMAIL, E2E_SMTP_USERNAME } from './helpers/test-data'
import { openFirstTenantDetails } from './helpers/navigation'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

test.describe('Tenant real email delivery', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run tenant real-email tests.')
  test.skip(!E2E_REAL_EMAIL, 'Real email tests are disabled. Set E2E_REAL_EMAIL=true to enable.')
  test.skip(E2E_EMAIL_DELIVERY_MODE.toUpperCase() !== 'SMTP', 'This suite currently validates SMTP mode only.')

  test('saves tenant delivery override, verifies connection, and sends a real test email', async ({ page }) => {
    test.skip(!E2E_SMTP_HOST || !E2E_SMTP_SENDER_EMAIL || !E2E_EMAIL_TEST_RECIPIENT, 'Missing required SMTP env vars for real test.')

    await loginAsPlatformOwner(page)
    await openFirstTenantDetails(page)

    await expect(page.getByTestId('tenant-email-notifications-section')).toBeVisible()
    await page.getByTestId('tenant-use-platform-defaults-toggle').uncheck()
    await page.getByTestId('tenant-override-smtp-toggle').check()

    await page.getByLabel('Delivery method').selectOption('Smtp')
    await page.getByTestId('tenant-smtp-host-input').fill(E2E_SMTP_HOST)
    await page.getByTestId('tenant-smtp-port-input').fill(String(E2E_SMTP_PORT))
    await page.getByLabel('SMTP username').fill(E2E_SMTP_USERNAME)
    if (E2E_SMTP_PASSWORD) {
      await page.getByLabel('SMTP password / masked secret').fill(E2E_SMTP_PASSWORD)
    }

    await page.getByLabel('Secure mode').selectOption(E2E_SMTP_SECURE_MODE)
    await page.getByTestId('tenant-sender-email-input').fill(E2E_SMTP_SENDER_EMAIL)

    await page.getByTestId('tenant-save-email-settings-button').click()
    await expect(page.getByText(/tenant email settings saved/i)).toBeVisible()

    await page.getByTestId('tenant-verify-smtp-button').click()
    await expect(page.getByText(/connection verified|connection verification failed/i)).toBeVisible()

    await page.getByLabel('Test recipient email').fill(E2E_EMAIL_TEST_RECIPIENT)
    await page.getByTestId('tenant-send-test-email-button').click()
    await expect(page.getByText(/test email sent|test email failed/i)).toBeVisible()
  })
})
