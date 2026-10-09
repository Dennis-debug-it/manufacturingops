import { expect, test } from '@playwright/test'
import { CORS_HEADERS, fulfillJson, json, mockAuthSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL

test.describe('start a free trial', () => {
  test('the form sends the details and asks the person to check their email', async ({ page }) => {
    let sent: Record<string, unknown> | null = null
    await page.route(`${API}/api/auth/signup/start`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      sent = route.request().postDataJSON() as Record<string, unknown>
      return route.fulfill(json({ message: 'Check your email.' }))
    })

    await page.goto('/login')
    await page.getByRole('link', { name: 'Start a free trial' }).click()
    await expect(page.getByRole('heading', { name: 'Start your free trial' })).toBeVisible()

    await page.getByLabel('Company name').fill('Rift Cooling Ltd')
    await page.getByLabel('Your name').fill('Amina Otieno')
    await page.getByLabel('Work email').fill('amina@riftcool.test')
    await page.getByLabel('Phone').fill('+254700000001')
    await page.getByLabel('Choose a password').fill('short')
    await page.getByRole('button', { name: 'Start free trial' }).click()
    await expect(page.getByText('At least 8 characters')).toBeVisible()
    expect(sent).toBeNull()

    await page.getByLabel('Choose a password').fill('Trial-Signup-Pass-2026')
    await page.getByRole('button', { name: 'Start free trial' }).click()

    await expect(page.getByTestId('signup-sent')).toContainText('amina@riftcool.test')
    expect(sent).toMatchObject({
      companyName: 'Rift Cooling Ltd',
      fullName: 'Amina Otieno',
      email: 'amina@riftcool.test',
      phone: '+254700000001',
      country: 'Kenya',
      password: 'Trial-Signup-Pass-2026',
    })
  })

  test('the emailed link signs the owner in and opens setup', async ({ page }) => {
    await registerApiCatchAll(page)
    const session = await mockAuthSession(page, { role: 'tenantAdmin' })
    let token: string | null = null
    await page.route(`${API}/api/auth/signup/verify`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      token = (route.request().postDataJSON() as { token: string }).token
      return fulfillJson(route, session.loginResponse)
    })

    await page.goto('/signup/verify#link-token-123')

    await page.waitForURL('**/onboarding')
    expect(token).toBe('link-token-123')
    // The token doesn't stay in the address bar.
    expect(page.url()).not.toContain('link-token-123')
  })

  test('an expired link explains what to do', async ({ page }) => {
    await page.route(`${API}/api/auth/signup/verify`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json({ message: 'This sign-up link is invalid or has expired. Please sign up again.', errorCode: 'BUSINESS_RULE' }, 400))
    })

    await page.goto('/signup/verify#old-token')

    await expect(page.getByRole('alert')).toContainText('invalid or has expired')
    await expect(page.getByRole('link', { name: 'Sign up again' })).toHaveAttribute('href', '/signup')
  })
})
