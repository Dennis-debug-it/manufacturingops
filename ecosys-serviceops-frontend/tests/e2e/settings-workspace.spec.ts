import { expect, test } from '@playwright/test'
import { primeTenantSession } from './helpers/auth'

test.describe('settings workspace', () => {
  test('shows grouped rows and account management actions at the root settings hub', async ({ page }) => {
    await primeTenantSession(page)

    await page.goto('/settings')
    await expect(page.getByRole('heading', { name: /settings workspace/i })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'General' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'People & Access' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Operations' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Finance' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Communication' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Automation' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Data & System' })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Account Management' })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Email-to-Work Order', exact: true })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Account Management', exact: true })).toBeVisible()
  })
})
