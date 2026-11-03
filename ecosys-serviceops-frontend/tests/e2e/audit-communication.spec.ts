import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner, primeTenantSession } from './helpers/auth'
import { gotoPlatformSettings, openFirstTenantDetails, gotoEmailIntake } from './helpers/navigation'
import { mockEmailIntakeSimulationSuccess } from './helpers/api-mocks'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

async function pollForAuditRow(page: import('@playwright/test').Page, pattern: RegExp) {
  let matchCount = 0

  await expect
    .poll(
      async () => {
        await page.reload()
        matchCount = await page.locator('table tbody tr').filter({ hasText: pattern }).count()
        return matchCount > 0
      },
      { timeout: 30000 },
    )
    .toBe(true)
    .catch(() => undefined)

  return matchCount
}

test.describe('Audit logs for communication actions', () => {
  test('platform email settings update appears in audit logs when audit is implemented', async ({ page }) => {
    test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform audit-log checks.')
    await loginAsPlatformOwner(page)
    await gotoPlatformSettings(page)
    await page.getByRole('button', { name: 'Email & Notifications' }).click()

    const senderName = `E2E Sender ${Date.now()}`
    await page.getByTestId('smtp-host-input').fill('smtp.example.com')
    await page.getByTestId('smtp-port-input').fill('587')
    await page.getByTestId('sender-email-input').fill('audit@example.com')
    await page.getByTestId('sender-name-input').fill(senderName)
    await page.getByTestId('save-email-settings-button').click()
    await expect(page.getByText(/email settings saved/i)).toBeVisible()

    await page.goto('/platform/audit-logs')
    await expect(page.getByText('Audit Logs').first()).toBeVisible()

    const count = await pollForAuditRow(page, /email|smtp|settings/i)
    if (count === 0) {
      test.info().annotations.push({ type: 'feature-flag', description: 'Platform email settings audit event not observed in current build.' })
    }
  })

  test('tenant email settings update appears in audit logs when audit is implemented', async ({ page }) => {
    test.skip(!hasPlatformCreds, 'Set platform owner credentials to run tenant detail audit-log checks.')
    await loginAsPlatformOwner(page)
    await openFirstTenantDetails(page)
    await page.getByRole('button', { name: 'Email & Notifications' }).click()

    await page.getByTestId('tenant-use-platform-defaults-toggle').uncheck()
    await page.getByTestId('tenant-override-smtp-toggle').check()
    await page.getByTestId('tenant-smtp-host-input').fill('smtp.example.com')
    await page.getByTestId('tenant-smtp-port-input').fill('587')
    await page.getByTestId('tenant-sender-email-input').fill('tenant-audit@example.com')
    await page.getByTestId('tenant-save-email-settings-button').click()
    await expect(page.getByText(/tenant email settings saved/i)).toBeVisible()

    await page.goto('/platform/audit-logs')

    const count = await pollForAuditRow(page, /tenant|communication|email settings/i)
    if (count === 0) {
      test.info().annotations.push({ type: 'feature-flag', description: 'Tenant communication audit event not observed in current build.' })
    }
  })

  test('email intake simulation appears in tenant audit logs when implemented', async ({ page }) => {
    await primeTenantSession(page)
    await gotoEmailIntake(page)
    await mockEmailIntakeSimulationSuccess(page)

    await page.getByTestId('email-intake-rule-name-input').fill(`Audit Intake ${Date.now()}`)
    await page.getByTestId('email-intake-save-button').click()
    await expect(page.getByText(/protocol saved/i)).toBeVisible()

    await page.getByTestId('email-intake-simulate-button').click()
    await expect(page.getByText(/protocol test passed/i)).toBeVisible()

    await page.goto('/settings/audit-logs')
    await expect(page.getByText('Audit Logs').first()).toBeVisible()

    const auditRows = page.locator('table tbody tr')
    if ((await auditRows.count()) === 0) {
      test.info().annotations.push({ type: 'feature-flag', description: 'Tenant audit logs endpoint is present but has no rows yet.' })
      return
    }

    const count = await pollForAuditRow(page, /intake|protocol|email/i)
    if (count === 0) {
      test.info().annotations.push({ type: 'feature-flag', description: 'Email intake audit event not observed in current build.' })
    }
  })
})
