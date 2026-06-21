import { expect, test, type Page } from '@playwright/test'
import { loginAsTenantAdmin } from './helpers/auth'

function checklistCard(page: Page, templateName: string) {
  return page.locator('article').filter({ has: page.getByText(templateName, { exact: true }) }).first()
}

test.describe('service checklist templates', () => {
  test.setTimeout(120_000)

  test.fixme('Current build returns a backend 500 and shows "Unable to load templates" for the tenant templates page.')
  test('creates, edits, and validates duplicate checklist saves without closing the form', async ({ page }) => {
    const templateName = `Power Monthly PM ${Date.now()}`

    await loginAsTenantAdmin(page)
    await page.goto('/templates')
    await page.waitForLoadState('networkidle')

    await page.getByRole('button', { name: /service checklists/i }).click()
    await page.getByRole('button', { name: /new checklist/i }).click()

    await expect(page.getByRole('heading', { name: /create service checklist/i })).toBeVisible()
    await page.getByLabel('Checklist name').fill(templateName)
    await page.getByLabel('Service category').fill('Power')
    await page.getByLabel('Description').fill('Template save regression coverage.')
    await page.getByLabel('Section name').first().fill('Visual Checks')
    await page.getByLabel('Section order').first().fill('1')
    await page.getByLabel('Question label').first().fill('Check enclosure condition')
    await page.getByLabel('Answer type').first().selectOption('passfail')
    await page.getByLabel('Options').first().fill('Pass, Fail')

    await expect(page.getByRole('heading', { name: /create service checklist/i })).toBeVisible()
    await expect(page.getByLabel('Checklist name')).toHaveValue(templateName)

    const createResponsePromise = page.waitForResponse((response) =>
      response.url().endsWith('/api/pm/templates')
      && response.request().method() === 'POST')
    await page.getByRole('button', { name: /create checklist/i }).click()
    const createResponse = await createResponsePromise

    expect(createResponse.ok()).toBeTruthy()
    await expect(page.getByRole('heading', { name: /create service checklist/i })).toHaveCount(0, { timeout: 15000 })
    await expect(checklistCard(page, templateName)).toBeVisible()

    await checklistCard(page, templateName).getByRole('button', { name: /^edit$/i }).click()
    await expect(page.getByRole('heading', { name: /edit service checklist/i })).toBeVisible()
    await page.getByLabel('Description').fill('Edited template save regression coverage.')

    const updateResponsePromise = page.waitForResponse((response) =>
      response.url().includes('/api/pm/templates/')
      && response.request().method() === 'PUT')
    await page.getByRole('button', { name: /save changes/i }).click()
    const updateResponse = await updateResponsePromise

    expect(updateResponse.ok()).toBeTruthy()
    await expect(page.getByRole('heading', { name: /edit service checklist/i })).toHaveCount(0, { timeout: 15000 })
    await expect(checklistCard(page, templateName)).toContainText('Edited template save regression coverage.')

    await page.getByRole('button', { name: /new checklist/i }).click()
    await page.getByLabel('Checklist name').fill(templateName)
    await page.getByLabel('Service category').fill('Power')
    await page.getByLabel('Description').fill('Duplicate validation coverage.')
    await page.getByLabel('Section name').first().fill('Visual Checks')
    await page.getByLabel('Section order').first().fill('1')
    await page.getByLabel('Question label').first().fill('Check enclosure condition')
    await page.getByLabel('Answer type').first().selectOption('passfail')
    await page.getByLabel('Options').first().fill('Pass, Fail')

    const duplicateResponsePromise = page.waitForResponse((response) =>
      response.url().endsWith('/api/pm/templates')
      && response.request().method() === 'POST'
      && response.status() === 409)
    await page.getByRole('button', { name: /create checklist/i }).click()
    await duplicateResponsePromise

    await expect(page.getByText('A template with this name already exists.')).toBeVisible()
    await expect(page.getByRole('heading', { name: /create service checklist/i })).toBeVisible()
    await expect(page.getByLabel('Checklist name')).toHaveValue(templateName)
    await expect(page.getByLabel('Question label').first()).toHaveValue('Check enclosure condition')
  })
})
