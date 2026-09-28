import { expect, test, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

// Must match the API origin the app under test calls (E2E_API_URL).
const BASE = E2E_API_URL

async function mockForcedPasswordChange(page: Page, role: 'tenantAdmin' | 'platformOwner' = 'tenantAdmin') {
  await registerApiCatchAll(page)
  await loginWithMockSession(page, { role, mustChangePassword: true })
}

test.describe('auth password flows', () => {
  test('wrong password shows clear feedback and keeps the page usable', async ({ page }) => {
    await page.route(`${BASE}/api/auth/login`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'Invalid email or password.' }, 401))
    })

    await page.goto('/login')
    await expect(page.getByRole('link', { name: /forgot password/i })).toBeVisible()

    await page.getByLabel('Email').fill('wrong@ecosys.test')
    await page.getByLabel('Password').fill('incorrect')
    await page.getByRole('button', { name: /^sign in$/i }).click()

    await expect(page.getByRole('alert')).toContainText('Invalid email or password.')
    await expect(page.getByRole('alert')).not.toContainText('traceId')
    await expect(page.locator('body')).not.toContainText('https://tools.ietf.org/html/rfc9110')
    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByRole('button', { name: /^sign in$/i })).toBeEnabled()
    await expect(page.getByLabel('Email')).toHaveValue('wrong@ecosys.test')
    await expect(page.getByLabel('Password')).toHaveValue('incorrect')
  })

  test('legacy 403 login failure still shows the same friendly message', async ({ page }) => {
    await page.route(`${BASE}/api/auth/login`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4',
        title: 'Invalid email or password.',
        status: 403,
        traceId: '00-test',
      }, 403))
    })

    await page.goto('/login')
    await page.getByLabel('Email').fill('wrong@ecosys.test')
    await page.getByLabel('Password').fill('incorrect')
    await page.getByRole('button', { name: /^sign in$/i }).click()

    await expect(page.getByRole('alert')).toContainText('Invalid email or password.')
    await expect(page.locator('body')).not.toContainText('traceId')
    await expect(page.locator('body')).not.toContainText('rfc9110')
  })

  test('network failure shows server unreachable message', async ({ page }) => {
    await page.route(`${BASE}/api/auth/login`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.abort('failed')
    })

    await page.goto('/login')
    await page.getByLabel('Email').fill('wrong@ecosys.test')
    await page.getByLabel('Password').fill('incorrect')
    await page.getByRole('button', { name: /^sign in$/i }).click()

    await expect(page.getByRole('alert')).toContainText("Can't reach the server. Check your connection and try again.")
  })

  test('successful SuperAdmin login routes to /platform', async ({ page }) => {
    await registerApiCatchAll(page)
    await loginWithMockSession(page, { role: 'platformOwner' })
    await expect(page).toHaveURL(/\/platform/)
  })

  test('successful tenant login routes to /dashboard', async ({ page }) => {
    await registerApiCatchAll(page)
    await loginWithMockSession(page, { role: 'tenantAdmin' })
    await expect(page).toHaveURL(/\/dashboard/)
    await expect(page.getByText(/Operational overview/i)).toBeVisible()
  })

  test('login with mustChangePassword true redirects to /change-password', async ({ page }) => {
    await mockForcedPasswordChange(page)
    await expect(page).toHaveURL(/\/change-password$/)
    await expect(page.getByRole('heading', { name: /change your temporary password/i })).toBeVisible()
  })

  test('user cannot access /dashboard while mustChangePassword is true', async ({ page }) => {
    await mockForcedPasswordChange(page)
    await page.goto('/dashboard')
    await expect(page).toHaveURL(/\/change-password$/)
  })

  test('user cannot access /platform while mustChangePassword is true', async ({ page }) => {
    await mockForcedPasswordChange(page, 'platformOwner')
    await page.goto('/platform')
    await expect(page).toHaveURL(/\/change-password$/)
  })

  test('change password page validates mismatch with a friendly error', async ({ page }) => {
    await mockForcedPasswordChange(page)
    await page.getByLabel('Current password').fill('TempPass123!')
    await page.getByLabel('New password', { exact: true }).fill('NewStrongPass123!')
    await page.getByLabel('Confirm new password', { exact: true }).fill('DifferentPass123!')
    await page.getByRole('button', { name: /change password/i }).click()

    await expect(page.getByRole('alert')).toContainText('Passwords do not match.')
  })

  test('successful password change redirects tenant user to /dashboard', async ({ page }) => {
    await registerApiCatchAll(page)
    await page.route(`${BASE}/api/auth/change-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'Your password has been changed successfully.' }))
    })

    await loginWithMockSession(page, { role: 'tenantAdmin', mustChangePassword: true })
    await page.getByLabel('Current password').fill('TempPass123!')
    await page.getByLabel('New password', { exact: true }).fill('NewStrongPass123!')
    await page.getByLabel('Confirm new password', { exact: true }).fill('NewStrongPass123!')
    await page.getByRole('button', { name: /change password/i }).click()

    await expect(page).toHaveURL(/\/dashboard$/)
  })

  test('successful password change redirects SuperAdmin to /platform', async ({ page }) => {
    await registerApiCatchAll(page)
    await page.route(`${BASE}/api/auth/change-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'Your password has been changed successfully.' }))
    })

    await loginWithMockSession(page, { role: 'platformOwner', mustChangePassword: true })
    await page.getByLabel('Current password').fill('TempPass123!')
    await page.getByLabel('New password', { exact: true }).fill('NewStrongPass123!')
    await page.getByLabel('Confirm new password', { exact: true }).fill('NewStrongPass123!')
    await page.getByRole('button', { name: /change password/i }).click()

    await expect(page).toHaveURL(/\/platform/)
  })

  test('forgot password page submits and shows generic success', async ({ page }) => {
    let requestCount = 0
    await page.route(`${BASE}/api/auth/forgot-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      requestCount += 1
      await route.fulfill(json({
        message: 'If an account exists for this email, password reset instructions will be sent shortly.',
      }))
    })

    await page.goto('/forgot-password')
    await page.getByLabel('Email address').fill('person@example.com')
    await page.getByRole('button', { name: /send reset link/i }).click()

    await expect(page.getByRole('status')).toContainText('If an account exists for this email, password reset instructions will be sent shortly.')
    expect(requestCount).toBe(1)
  })

  test('reset password validates mismatch before calling the API', async ({ page }) => {
    let wasCalled = false
    await page.route(`${BASE}/api/auth/reset-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      wasCalled = true
      await route.fulfill(json({ message: 'Your password has been reset. You can now sign in.' }))
    })

    await page.goto('/reset-password?token=sample-token')
    await page.getByLabel('New password').fill('NewStrongPass123!')
    await page.getByLabel('Confirm password').fill('DifferentPass123!')
    await page.getByRole('button', { name: /reset password/i }).click()

    await expect(page.getByText('Passwords do not match')).toBeVisible()
    expect(wasCalled).toBeFalsy()
  })

  test('reset password success links back to login', async ({ page }) => {
    await page.route(`${BASE}/api/auth/reset-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'Your password has been reset. You can now sign in.' }))
    })

    await page.goto('/reset-password?token=sample-token')
    await page.getByLabel('New password').fill('NewStrongPass123!')
    await page.getByLabel('Confirm password').fill('NewStrongPass123!')
    await page.getByRole('button', { name: /reset password/i }).click()

    await expect(page.getByText(/Your password has been reset\. Redirecting you to sign in/)).toBeVisible()
    await expect(page.getByRole('link', { name: /back to sign in/i })).toHaveCount(0)
  })

  test('invite mode shows account setup wording and success state', async ({ page }) => {
    await page.route(`${BASE}/api/auth/validate-invitation`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ status: 'VALID' }))
    })
    await page.route(`${BASE}/api/auth/reset-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'Your password has been reset. You can now sign in.' }))
    })

    await page.goto('/reset-password?token=invite-token&mode=invite')
    await expect(page.getByRole('heading', { name: /set your password/i })).toBeVisible()
    await expect(page.getByRole('button', { name: /^set password$/i })).toBeVisible()

    await page.getByLabel('New password').fill('NewStrongPass123!')
    await page.getByLabel('Confirm password').fill('NewStrongPass123!')
    await page.getByRole('button', { name: /^set password$/i }).click()

    await expect(page.getByText(/Your password has been reset\. Redirecting you to sign in/)).toBeVisible()
  })

  test('invalid or expired reset link shows clear message', async ({ page }) => {
    await page.route(`${BASE}/api/auth/reset-password`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      await route.fulfill(json({ message: 'This password reset link is invalid or has expired. Please request a new one.' }, 403))
    })

    await page.goto('/reset-password?token=expired-token')
    await page.getByLabel('New password').fill('NewStrongPass123!')
    await page.getByLabel('Confirm password').fill('NewStrongPass123!')
    await page.getByRole('button', { name: /reset password/i }).click()

    await expect(page.getByText('This password reset link is invalid or has expired. Please request a new one.')).toBeVisible()
  })
})
