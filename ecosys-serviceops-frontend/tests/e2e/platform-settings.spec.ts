import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { gotoPlatformSettings } from './helpers/navigation'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)
const forbiddenTexts = [/API not available yet/i, /coming soon/i, /quotation/i, /invoice/i, /billing/i, /tax & finance/i]

async function openPanel(page: import('@playwright/test').Page, sectionId: string, panelTestId: string) {
  await page.getByTestId(`settings-mini-sidebar-${sectionId}`).click()
  await expect(page.getByTestId(panelTestId)).toBeVisible()
}

test.describe('Platform settings', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform settings tests.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
    await gotoPlatformSettings(page)
  })

  test('settings mini-sidebar loads and panel switching works', async ({ page }) => {
    await expect(page.getByTestId('settings-mini-sidebar')).toBeVisible()

    const sections = [
      { id: 'general', panel: 'settings-panel-general' },
      { id: 'branding', panel: 'settings-panel-branding' },
      { id: 'email', panel: 'settings-panel-email-notifications' },
      { id: 'numbering', panel: 'settings-panel-numbering' },
      { id: 'security', panel: 'settings-panel-security' },
      { id: 'system-preferences', panel: 'settings-panel-system-preferences' },
    ]

    for (const section of sections) {
      await openPanel(page, section.id, section.panel)
      for (const forbidden of forbiddenTexts) {
        await expect(page.getByText(forbidden)).toHaveCount(0)
      }
    }

    await expect(page.locator('[data-testid^="settings-panel-"]:visible')).toHaveCount(1)
  })

  test('general and branding panels expose expected controls', async ({ page }) => {
    await openPanel(page, 'general', 'settings-panel-general')
    await expect(page.getByLabel('Platform Name')).toBeVisible()
    await expect(page.getByLabel('Support Email')).toBeVisible()
    await expect(page.getByRole('button', { name: /save general settings/i })).toBeVisible()

    await openPanel(page, 'branding', 'settings-panel-branding')
    await expect(page.getByLabel('Platform Logo URL')).toBeVisible()
    await expect(page.getByLabel('Favicon URL')).toBeVisible()
    await expect(page.getByRole('button', { name: /save branding settings/i })).toBeVisible()
  })

  test('email notifications panel exposes smtp controls and masked secret', async ({ page }) => {
    await openPanel(page, 'email', 'settings-panel-email-notifications')

    await expect(page.getByTestId('platform-email-settings-form')).toBeVisible()
    await expect(page.getByTestId('smtp-host-input')).toBeVisible()
    await expect(page.getByTestId('smtp-port-input')).toBeVisible()
    await expect(page.getByTestId('sender-email-input')).toBeVisible()
    await expect(page.getByTestId('enable-email-notifications-toggle')).toBeVisible()
    await expect(page.getByTestId('verify-smtp-button')).toBeVisible()
    await expect(page.getByTestId('send-test-email-button')).toBeVisible()
    await expect(page.getByTestId('save-email-settings-button')).toBeVisible()

    await expect(page.getByTestId('smtp-password-input')).toHaveAttribute('type', 'password')
  })

  test('numbering preview updates when prefix and next number change', async ({ page }) => {
    await openPanel(page, 'numbering', 'settings-panel-numbering')

    const workOrderRow = page.locator('table tbody tr', { hasText: 'WorkOrder' })
    await expect(workOrderRow).toHaveCount(1)

    const prefixInput = workOrderRow.locator('input').nth(0)
    const nextNumberInput = workOrderRow.locator('input').nth(1)

    await prefixInput.fill('E2EWO-')
    await nextNumberInput.fill('42')

    await expect(page.getByTestId('numbering-preview-workorder')).toContainText('E2EWO-')
    await expect(page.getByTestId('numbering-preview-workorder')).toContainText('42')
  })

  test('security and system preferences panels load expected controls', async ({ page }) => {
    await openPanel(page, 'security', 'settings-panel-security')
    await expect(page.getByLabel('Minimum Password Length')).toBeVisible()
    await expect(page.getByText(/not enabled/i)).toBeVisible()

    await openPanel(page, 'system-preferences', 'settings-panel-system-preferences')
    await expect(page.getByLabel('Date Format')).toBeVisible()
    await expect(page.getByLabel('Time Format')).toBeVisible()
    await expect(page.getByLabel('Default Pagination Size')).toBeVisible()
  })
})
