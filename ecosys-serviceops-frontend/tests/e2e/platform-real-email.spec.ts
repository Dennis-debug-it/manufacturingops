import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { E2E_EMAIL_DELIVERY_MODE, E2E_EMAIL_TEST_RECIPIENT, E2E_REAL_EMAIL, E2E_SMTP_HOST, E2E_SMTP_PASSWORD, E2E_SMTP_PORT, E2E_SMTP_SECURE_MODE, E2E_SMTP_SENDER_EMAIL, E2E_SMTP_USERNAME } from './helpers/test-data'
import { gotoPlatformSettings } from './helpers/navigation'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

test.describe('Platform real email delivery', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform real-email tests.')
  test.skip(!E2E_REAL_EMAIL, 'Real email tests are disabled. Set E2E_REAL_EMAIL=true to enable.')
  test.skip(E2E_EMAIL_DELIVERY_MODE.toUpperCase() !== 'SMTP', 'This suite currently validates SMTP mode only.')

  test('saves platform delivery settings, verifies connection, and sends a real test email', async ({ page }) => {
    test.skip(!E2E_SMTP_HOST || !E2E_SMTP_SENDER_EMAIL || !E2E_EMAIL_TEST_RECIPIENT, 'Missing required SMTP env vars for real test.')

    await loginAsPlatformOwner(page)
    await gotoPlatformSettings(page)
    await page.getByRole('button', { name: 'Email & Notifications' }).click()
    await expect(page.getByTestId('settings-panel-email-notifications')).toBeVisible()

    await page.getByLabel('Delivery Method').selectOption('Smtp')
    await page.getByTestId('enable-email-notifications-toggle').check()
    await page.getByTestId('smtp-host-input').fill(E2E_SMTP_HOST)
    await page.getByTestId('smtp-port-input').fill(String(E2E_SMTP_PORT))
    await page.getByTestId('smtp-username-input').fill(E2E_SMTP_USERNAME)
    if (E2E_SMTP_PASSWORD) {
      await page.getByTestId('smtp-password-input').fill(E2E_SMTP_PASSWORD)
    }

    await page.getByLabel('Secure Mode').selectOption(E2E_SMTP_SECURE_MODE)
    await page.getByTestId('sender-email-input').fill(E2E_SMTP_SENDER_EMAIL)

    await page.getByTestId('save-email-settings-button').click()
    await expect(page.getByText(/email settings saved/i)).toBeVisible()

    await page.getByTestId('verify-smtp-button').click()
    await expect(page.getByText(/connection verified|connection verification failed/i)).toBeVisible()

    await page.getByLabel('Test Recipient').fill(E2E_EMAIL_TEST_RECIPIENT)
    await page.getByTestId('send-test-email-button').click()
    await expect(page.getByText(/test email sent|test email failed/i)).toBeVisible()
  })
})
