import { expect, type Page } from '@playwright/test'

export async function gotoCommandCentre(page: Page) {
  await page.goto('/platform')
  await expect(page.getByTestId('command-centre-dashboard')).toBeVisible()
}

export async function gotoPlatformSettings(page: Page) {
  await page.goto('/platform/settings')
  await expect(page.getByTestId('platform-settings-page')).toBeVisible()
}

export async function gotoTenants(page: Page) {
  await page.goto('/platform/tenants')
  await expect(page.getByTestId('tenants-page')).toBeVisible()
}

export async function openFirstTenantDetails(page: Page) {
  await gotoTenants(page)

  const tenantName = `E2E Tenant ${Date.now()}`
  const tenantSlug = `e2e-tenant-${Date.now()}`
  const tenantEmail = `${tenantSlug}@example.com`

  await page.getByRole('button', { name: /add tenant/i }).click()
  await page.getByTestId('tenant-form-company-name').fill(tenantName)
  await page.getByTestId('tenant-form-slug').fill(tenantSlug)
  await page.getByTestId('tenant-form-company-email').fill(tenantEmail)
  await page.getByTestId('tenant-form-company-phone').fill('+254700000000')
  await page.getByTestId('tenant-form-country').fill('Kenya')
  await page.getByTestId('tenant-form-contact-name').fill('E2E Tenant Contact')
  await page.getByTestId('tenant-form-contact-email').fill(tenantEmail)
  await page.getByRole('button', { name: /create tenant/i }).click()

  await expect(page.getByText(/^tenant created$/i)).toBeVisible()

  await page.getByTestId('tenants-search-input').fill(tenantName)
  const targetRow = page.locator('table tbody tr', { hasText: tenantName })
  await expect(targetRow).toHaveCount(1)
  await targetRow.getByRole('combobox').selectOption('view')
  await targetRow.getByRole('button', { name: /apply/i }).click()

  await expect(page.getByTestId('tenant-details-page')).toBeVisible()
}

export async function gotoEmailIntake(page: Page) {
  await page.goto('/settings')
  await expect(page).toHaveURL(/\/settings\/company-profile/)
  await page.locator('a[href="/settings/email-intake"]:visible').first().click()
  await expect(page).toHaveURL(/\/settings\/email-intake/)
  await expect(page.getByTestId('email-intake-page')).toBeVisible()
}
