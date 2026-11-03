import { expect, test, type Page } from '@playwright/test'
import { loginAsPlatformOwner, primeTenantSession } from './helpers/auth'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)
const forbiddenTerms = [
  /phase 2/i,
  /deferred/i,
  /backend integration pending/i,
  /api not wired/i,
  /coming soon/i,
  /not implemented/i,
  /placeholder/i,
  /mock data/i,
  /demo data/i,
  /test data/i,
  /developer note/i,
  /debug/i,
  /stub/i,
  /experimental/i,
  /temporary/i,
  /quotation/i,
  /invoice/i,
  /pricing rules/i,
  /contract pricing/i,
  /labour rates/i,
  /callout fees/i,
  /billing/i,
  /penalties/i,
  /breach handling/i,
  /service coverage rules/i,
  /commercial rules/i,
]

async function assertNoForbiddenCopy(page: Page) {
  for (const term of forbiddenTerms) {
    await expect(page.getByText(term)).toHaveCount(0)
  }
}

test.describe('Customer-facing copy audit', () => {
  test('tenant routes avoid internal and commercial copy', async ({ page }) => {
    await primeTenantSession(page)

    const routes = [
      '/dashboard',
      '/clients',
      '/assets',
      '/work-orders',
      '/materials',
      '/preventive-maintenance',
      '/reports',
      '/settings/company-profile',
      '/settings/email-intake',
    ]

    for (const route of routes) {
      await page.goto(route)
      await assertNoForbiddenCopy(page)
    }
  })

  test('platform routes avoid internal and commercial copy', async ({ page }) => {
    test.skip(!hasPlatformCreds, 'Set platform owner credentials to run platform route copy checks.')
    await loginAsPlatformOwner(page)

    const routes = [
      '/platform',
      '/platform/leads',
      '/platform/tenants',
      '/platform/users',
      '/platform/reports',
      '/platform/audit-logs',
      '/platform/settings',
    ]

    for (const route of routes) {
      await page.goto(route)
      await assertNoForbiddenCopy(page)
    }
  })
})
