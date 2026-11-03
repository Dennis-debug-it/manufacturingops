import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { openFirstTenantDetails } from './helpers/navigation'
import { mockTenantEmailTestSuccess, mockTenantEmailVerifySuccess } from './helpers/api-mocks'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

test.describe('Client/Tenant details and communication settings', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run tenant detail platform checks.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
    await mockTenantEmailVerifySuccess(page)
    await mockTenantEmailTestSuccess(page)
    await openFirstTenantDetails(page)
  })

  test('tenant detail sections and email notifications content are usable', async ({ page }) => {
    const sectionLabels = [
      'Overview',
      'Branding',
      'Users',
      'Modules',
      'Subscription / Licensing',
      'Email & Notifications',
      'Numbering',
      'Templates',
      'Audit Trail',
      'Danger Zone',
    ]

    for (const label of sectionLabels) {
      const sectionButton = page.getByRole('button', { name: label })
      if ((await sectionButton.count()) === 0) {
        test.info().annotations.push({ type: 'feature-flag', description: `${label} hidden by feature flag or build config.` })
        continue
      }
      await expect(sectionButton).toBeVisible()
    }

    await page.getByRole('button', { name: 'Email & Notifications' }).click()
    await expect(page.getByTestId('tenant-email-notifications-section')).toBeVisible()
    await expect(page.getByTestId('tenant-email-delivery-card')).toBeVisible()
    await expect(page.getByTestId('tenant-notification-rules-card')).toBeVisible()
    await expect(page.getByTestId('tenant-recipients-card')).toBeVisible()
    await expect(page.getByTestId('tenant-test-verification-card')).toBeVisible()

    await expect(page.getByTestId('tenant-use-platform-defaults-toggle')).toBeVisible()
    await expect(page.getByTestId('tenant-override-smtp-toggle')).toBeVisible()

    await expect(page.getByTestId('tenant-smtp-host-input')).toBeDisabled()
    await page.getByTestId('tenant-use-platform-defaults-toggle').uncheck()
    await page.getByTestId('tenant-override-smtp-toggle').check()
    await expect(page.getByTestId('tenant-smtp-host-input')).toBeEnabled()

    await expect(page.getByTestId('tenant-sender-email-input')).toBeVisible()
    await expect(page.getByTestId('tenant-verify-smtp-button')).toBeVisible()
    await expect(page.getByTestId('tenant-send-test-email-button')).toBeVisible()
    await expect(page.getByTestId('tenant-save-email-settings-button')).toBeVisible()

    const disallowedFinanceTerms = [
      /finance notification/i,
      /quotation sent/i,
      /invoice sent/i,
      /invoice overdue/i,
      /payment received/i,
      /finance emails/i,
      /invoice emails/i,
      /quotation emails/i,
      /payment emails/i,
    ]

    for (const term of disallowedFinanceTerms) {
      await expect(page.getByText(term)).toHaveCount(0)
    }

    const expectedGroups = [
      'Work Orders',
      'Priority Response',
      'Preventive Maintenance',
      'Assets',
      'Materials',
      'Users',
      'Tenant Administration',
      'System Alerts',
      'Admin notification emails',
      'Operations notification emails',
      'Priority response emails',
      'Work order dispatch emails',
      'Maintenance notification emails',
      'Asset notification emails',
      'Materials notification emails',
      'System alert emails',
    ]

    for (const label of expectedGroups) {
      await expect(page.getByText(label, { exact: false }).count()).resolves.toBeGreaterThan(0)
    }
  })

  test('tenant email API wiring: save/verify/test/reset', async ({ page }) => {
    await page.getByRole('button', { name: 'Email & Notifications' }).click()

    let saveCalled = false
    let resetCalled = false

    await page.route('**/api/platform/tenants/*/communication-settings', async (route) => {
      if (route.request().method().toUpperCase() === 'PUT') {
        saveCalled = true
      }
      await route.continue()
    })

    await page.route('**/api/platform/tenants/*/email-settings/reset-to-defaults', async (route) => {
      resetCalled = true
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'reset-id',
          tenantId: 'reset-tenant',
          usePlatformDefaults: true,
          overrideSmtpSettings: false,
          smtpHost: null,
          smtpPort: 587,
          smtpUsername: null,
          smtpPasswordMasked: '********',
          senderName: null,
          senderEmail: null,
          replyToEmail: null,
          enableSslTls: true,
          enableTenantEmailNotifications: true,
          lastTestedAt: null,
          lastError: null,
        }),
      })
    })

    await page.getByTestId('tenant-use-platform-defaults-toggle').uncheck()
    await page.getByTestId('tenant-override-smtp-toggle').check()
    await page.getByTestId('tenant-smtp-host-input').fill('smtp.example.com')
    await page.getByTestId('tenant-smtp-port-input').fill('587')
    await page.getByTestId('tenant-sender-email-input').fill('ops@example.com')

    await page.getByTestId('tenant-save-email-settings-button').click()
    await expect(page.getByText(/tenant email settings saved/i)).toBeVisible()

    await page.getByTestId('tenant-verify-smtp-button').click()
    await expect(page.getByText(/connection verified/i)).toBeVisible()

    await page.getByTestId('tenant-send-test-email-button').click()
    await expect(page.getByText(/test email sent/i).first()).toBeVisible()

    await page.getByTestId('tenant-reset-email-defaults-button').click()

    expect(saveCalled).toBeTruthy()
    expect(resetCalled).toBeTruthy()
  })
})
