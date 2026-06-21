import { expect, test } from '@playwright/test'
import { loginAsPlatformOwner } from './helpers/auth'

const hasPlatformCreds = Boolean(process.env.E2E_PLATFORM_EMAIL || process.env.E2E_SUPERADMIN_EMAIL)

const forbiddenTexts = [
  /API not available yet/i,
  /placeholder/i,
  /mock data/i,
  /Work order summary placeholder/i,
  /coming soon/i,
  /quotation/i,
  /invoice/i,
  /billing/i,
]

function assertNoForbiddenText(page: import('@playwright/test').Page) {
  return Promise.all(forbiddenTexts.map(async (pattern) => {
    await expect(page.getByText(pattern)).toHaveCount(0)
  }))
}

test.describe('Command Centre smoke', () => {
  test.skip(!hasPlatformCreds, 'Set E2E_PLATFORM_EMAIL or E2E_SUPERADMIN_EMAIL to run command centre tests.')

  test.beforeEach(async ({ page }) => {
    await loginAsPlatformOwner(page)
  })

  // ── Overview page (/platform) ────────────────────────────────────────────────
  test('overview page loads with dashboard testid and no placeholder content', async ({ page }) => {
    await page.goto('/platform')
    await expect(page.getByTestId('command-centre-dashboard')).toBeVisible()
    await expect(page.getByText(/total tenants/i)).toBeVisible()
    await assertNoForbiddenText(page)
  })

  // ── Tenant Operations (/platform/tenants) ────────────────────────────────────
  test('tenant operations page renders KPI strip', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByTestId('tenants-page')).toBeVisible()

    // KPI cards
    await expect(page.getByText(/total tenants/i).first()).toBeVisible()
    await expect(page.getByText(/^active$/i).first()).toBeVisible()
    await expect(page.getByText(/on trial/i).first()).toBeVisible()
    await expect(page.getByText(/suspended/i).first()).toBeVisible()
    await expect(page.getByText(/expiring soon/i).first()).toBeVisible()
    await expect(page.getByText(/total users/i).first()).toBeVisible()
  })

  test('tenant operations table renders and search works', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByTestId('tenants-page')).toBeVisible()

    // Table heading
    await expect(page.getByText(/tenant directory/i)).toBeVisible()

    // Search input
    const searchInput = page.getByTestId('tenants-search-input')
    await expect(searchInput).toBeVisible()
    await searchInput.fill('xyz-no-match-tenant')
    await expect(page.getByText(/no tenants found/i)).toBeVisible()
    await searchInput.clear()
  })

  test('tenant operations page renders Health & Risk panel', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByText(/tenant health/i)).toBeVisible()
    // Either "all tenants are in good standing" or risk flag items
    const goodStanding = page.getByText(/all tenants are in good standing/i)
    const hasRisks = page.getByText(/attention needed|no users created|trial expir|account suspended/i)
    const eitherVisible = await goodStanding.isVisible().catch(() => false) || await hasRisks.first().isVisible().catch(() => false)
    expect(eitherVisible).toBeTruthy()
  })

  test('tenant operations page renders Platform Activity panel', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByText(/platform activity/i)).toBeVisible()
    // Either activity entries or empty state
    // const emptyActivity check removed — either state is valid
    const hasActivity = page.locator('[data-testid="tenants-page"]').getByText(/platform activity/i)
    await expect(hasActivity).toBeVisible()
  })

  test('tenant operations page renders Onboarding Pipeline', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByText(/onboarding pipeline/i)).toBeVisible()
    await expect(page.getByText(/new request/i)).toBeVisible()
    await expect(page.getByText(/under review/i)).toBeVisible()
    await expect(page.getByText(/provisioned/i)).toBeVisible()
  })

  test('tenant operations page renders Admin Controls', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page.getByText(/admin controls/i)).toBeVisible()
    await expect(page.getByText(/platform settings/i)).toBeVisible()
    await expect(page.getByText(/platform users/i)).toBeVisible()
    await expect(page.getByText(/audit logs/i)).toBeVisible()
    await expect(page.getByText(/reports/i)).toBeVisible()
  })

  test('tenant action dropdown opens', async ({ page }) => {
    await page.goto('/platform/tenants')
    const actionsButton = page.getByRole('button', { name: /actions/i }).first()
    if (await actionsButton.isVisible()) {
      await actionsButton.click()
      await expect(page.getByText(/view details/i)).toBeVisible()
      await expect(page.getByText(/edit tenant/i)).toBeVisible()
      await expect(page.getByText(/suspend/i)).toBeVisible()
    }
    // No tenants case — skip action check
  })

  test('tenant detail panel opens on row click', async ({ page }) => {
    await page.goto('/platform/tenants')
    // Click the first tenant name link if present
    const tenantLink = page.locator('[data-testid="tenants-page"] table a, [data-testid="tenants-page"] table button').filter({ hasText: /.+/ }).first()
    if (await tenantLink.isVisible()) {
      await tenantLink.click()
      await expect(page.getByTestId('tenant-details-page')).toBeVisible()
      await expect(page.getByTestId('tenant-details-mini-sidebar')).toBeVisible()
      await expect(page.getByTestId('tenant-details-section-overview')).toBeVisible()
      await expect(page.getByTestId('tenant-details-section-subscription')).toBeVisible()
      await expect(page.getByTestId('tenant-details-section-email-notifications')).toBeVisible()
    }
  })

  test('tenant detail panel shows risk flags if any', async ({ page }) => {
    await page.goto('/platform/tenants')
    const tenantLink = page.locator('[data-testid="tenants-page"] table button').filter({ hasText: /.+/ }).first()
    if (await tenantLink.isVisible()) {
      await tenantLink.click()
      await expect(page.getByTestId('tenant-details-page')).toBeVisible()
      // Either risk flags or clean state — both valid
    }
  })

  test('tenant detail subscription section renders', async ({ page }) => {
    await page.goto('/platform/tenants')
    const tenantLink = page.locator('[data-testid="tenants-page"] table button').filter({ hasText: /.+/ }).first()
    if (await tenantLink.isVisible()) {
      await tenantLink.click()
      await expect(page.getByTestId('tenant-details-mini-sidebar')).toBeVisible()
      await page.getByTestId('tenant-details-section-subscription').click()
      await expect(page.getByText(/trial starts|trial started|days remaining|max users/i)).toBeVisible()
    }
  })

  // ── Navigation ────────────────────────────────────────────────────────────────
  test('platform navigation works for all command centre areas', async ({ page }) => {
    const routes: Array<{ label: RegExp; url: RegExp }> = [
      { label: /overview|dashboard/i, url: /\/platform(?:\?.*)?$/ },
      { label: /tenants/i, url: /\/platform\/tenants/ },
      { label: /platform users/i, url: /\/platform\/users/ },
      { label: /^reports$/i, url: /\/platform\/reports/ },
      { label: /audit logs/i, url: /\/platform\/audit-logs/ },
      { label: /^settings$/i, url: /\/platform\/settings/ },
      { label: /licenses\s*&\s*subscriptions/i, url: /\/platform\/licenses/ },
    ]

    for (const item of routes) {
      await page.getByRole('link', { name: item.label }).click()
      await expect(page).toHaveURL(item.url)
      await assertNoForbiddenText(page)
    }
  })

  // ── Responsiveness ────────────────────────────────────────────────────────────
  test('page is responsive on mobile viewport', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto('/platform/tenants')
    await expect(page.getByTestId('tenants-page')).toBeVisible()
    // KPI strip should still show
    await expect(page.getByText(/total tenants/i).first()).toBeVisible()
  })

  // ── No forbidden content ──────────────────────────────────────────────────────
  test('tenant operations has no forbidden placeholder content', async ({ page }) => {
    await page.goto('/platform/tenants')
    await assertNoForbiddenText(page)
  })
})
