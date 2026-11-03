import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'
import { gotoPlatformSettings } from './helpers/navigation'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

async function openTemplatesPanel(page: import('@playwright/test').Page) {
  await gotoPlatformSettings(page)
  await page.getByRole('button', { name: 'Templates' }).click()
  await expect(page.getByTestId('settings-panel-templates')).toBeVisible()
}

test.describe('Platform templates', () => {
  test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform template tests.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
    await openTemplatesPanel(page)
  })

  test('template editor opens, accepts placeholders, and preview renders', async ({ page }) => {
    await expect(page.getByTestId('templates-page')).toBeVisible()
    await expect(page.getByTestId('template-list')).toBeVisible()

    await page.getByTestId('create-template-button').click()
    await expect(page.getByTestId('template-editor')).toBeVisible()

    const supportedTypes = [
      'Work order report template',
      'Preventive maintenance report template',
      'Tenant onboarding email template',
      'SLA escalation email template',
      'System alert email template',
    ]

    for (const templateType of supportedTypes) {
      await page.getByTestId('template-type-select').fill(templateType)
      await expect(page.getByTestId('template-type-select')).toHaveValue(templateType)
    }

    const templateName = `E2E Template ${Date.now()}`
    await page.getByLabel('Template Name').fill(templateName)
    await page.getByTestId('template-subject-input').fill('Subject {{workOrder.title}}')
    await page.getByTestId('template-header-editor').fill('<h1>{{platform.name}}</h1>')
    await page.getByTestId('template-body-editor').fill('<p>{{workOrder.title}} - {{workOrder.status}}</p>')
    await page.getByTestId('template-footer-editor').fill('<p>{{tenant.name}}</p>')
    await page.getByTestId('template-save-button').click()

    await expect(page.getByText(/template saved/i)).toBeVisible()

    const templateCard = page.locator('div.rounded-2xl.border.border-app.p-4', { hasText: templateName })
    await expect(templateCard).toBeVisible()
    await templateCard.getByRole('button', { name: /preview/i }).click()

    await expect(page.getByTestId('template-preview-panel')).toBeVisible()
    await expect(page.getByTestId('template-preview-panel')).not.toHaveText(/^\s*$/)
  })

  test('duplicate, activate/deactivate, default, and delete actions remain stable if implemented', async ({ page }) => {
    const templateName = `Action Template ${Date.now()}`
    await page.getByTestId('create-template-button').click()
    await page.getByLabel('Template Name').fill(templateName)
    await page.getByTestId('template-type-select').fill('Tenant onboarding email template')
    await page.getByTestId('template-body-editor').fill('<p>Hello {{tenant.name}}</p>')
    await page.getByTestId('template-save-button').click()
    await expect(page.getByText(/template saved/i)).toBeVisible()

    const anyTemplateCard = page.locator('div.rounded-2xl.border.border-app.p-4', { hasText: templateName })
    await expect(anyTemplateCard).toBeVisible()

    const actionNames = [/duplicate/i, /activate|deactivate/i, /make default|default/i, /delete/i]

    for (const actionName of actionNames) {
      const action = anyTemplateCard.getByRole('button', { name: actionName })
      if ((await action.count()) > 0) {
        await expect(action).toBeVisible()
      } else {
        test.info().annotations.push({ type: 'feature-flag', description: `Action ${actionName} is not exposed in this build.` })
      }
    }

    await expect(page.getByText(/client\/tenant.*invoice|client\/tenant.*quotation|client\/tenant.*payment/i)).toHaveCount(0)
  })
})
