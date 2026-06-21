import { expect, test } from '@playwright/test'
import { loginViaUi } from './helpers/auth'
import {
  E2E_API_BASE_URL,
  TENANT_EMAIL,
  TENANT_PASSWORD,
} from './helpers/env'
import { installPageGuards } from './helpers/networkGuards'

test('app root loads and routes to login', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.goto('/')
    await expect(page).toHaveURL(/\/login/)
    await expect(page.getByRole('heading', { name: /service operations, simplified\./i })).toBeVisible()
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('login page renders', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.goto('/login')
    await expect(page.getByRole('heading', { name: /service operations, simplified\./i })).toBeVisible()
    await expect(page.getByText('ServiceOps Suite').first()).toBeVisible()
    await expect(page.getByLabel('Email')).toBeVisible()
    await expect(page.getByLabel('Password')).toBeVisible()
    await expect(page.getByRole('button', { name: /login/i })).toBeVisible()
    await expect(page.getByRole('link', { name: /get started/i })).toBeVisible()
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('login logo uses dark high-contrast colors in light mode', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.setViewportSize({ width: 390, height: 844 })
    await page.addInitScript(() => {
      window.localStorage.setItem('ecosys-serviceops-theme', 'light')
    })
    await page.goto('/login')
    const markup = await page.locator('[data-testid="login-card-logo"]').evaluate((node) => node.innerHTML)
    expect(markup).toContain('#0C2F33')
    expect(markup).toContain('#214A4D')
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('login logo uses bright high-contrast colors in dark mode', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.addInitScript(() => {
      window.localStorage.setItem('ecosys-serviceops-theme', 'dark')
    })
    await page.goto('/login')
    const markup = await page.locator('[data-testid="login-brand-logo"]').evaluate((node) => node.innerHTML)
    expect(markup).toContain('text-[#F7F8F6]')
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('get started page renders', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.goto('/get-started')
    await expect(page.getByRole('heading', { name: /get started with ecosys/i })).toBeVisible()
    await expect(page.getByLabel('Company Name')).toBeVisible()
    await expect(page.getByLabel('Contact Person Name')).toBeVisible()
    await expect(page.getByLabel('Email Address')).toBeVisible()
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('public visitor can submit a get started request', async ({ page, baseURL }) => {
  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    await page.route(`${E2E_API_BASE_URL}/api/public/leads`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          success: true,
          message: 'Thank you. We have received your request.\n\nThe Ecosys team will contact you shortly to understand your needs and guide you through the next steps.',
        }),
      })
    })

    await page.goto('/get-started')
    await page.getByLabel('Company Name').fill('Acme Operations')
    await page.getByLabel('Contact Person Name').fill('Jane Doe')
    await page.getByLabel('Email Address').fill('jane@example.com')
    await page.getByLabel('Phone Number').fill('+254700000001')
    await page.getByLabel('Country').fill('Kenya')
    await page.getByLabel('Industry').fill('Facilities')
    await page.getByRole('button', { name: /submit request/i }).click()

    await expect(page.getByText(/thank you\. we have received your request\./i)).toBeVisible()
    await expect(page.getByText(/the ecosys team will contact you shortly/i)).toBeVisible()
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})

test('existing tenant can login and reach workspace', async ({ page, baseURL }) => {
  test.skip(!TENANT_EMAIL || !TENANT_PASSWORD, 'E2E_TENANT_EMAIL and E2E_TENANT_PASSWORD are required for login smoke.')

  const guards = installPageGuards(page, { appBaseUrl: baseURL, apiBaseUrl: E2E_API_BASE_URL })
  try {
    guards.markAuthenticated()
    await loginViaUi(page, {
      email: TENANT_EMAIL as string,
      password: TENANT_PASSWORD as string,
    })
    await expect(page).toHaveURL(/\/(dashboard|platform|command-centre)/)
  } finally {
    guards.assertNoViolations()
    guards.dispose()
  }
})
