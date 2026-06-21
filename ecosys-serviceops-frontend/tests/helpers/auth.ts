import { expect, type APIRequestContext, type Page } from '@playwright/test'

type LoginCredentials = {
  email: string
  password: string
}

type SignupInput = {
  companyName: string
  fullName: string
  email: string
  password: string
  industry?: string
  country: string
}

export async function loginViaUi(page: Page, credentials: LoginCredentials) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(credentials.email)
  await page.getByLabel('Password').fill(credentials.password)
  await page.getByRole('button', { name: /^login$/i }).click()
  await expect(page).toHaveURL(/\/(dashboard|platform|command-centre)/)
}

export async function signupViaUi(page: Page, payload: SignupInput) {
  await page.goto('/get-started')
  await page.getByLabel('Company Name').fill(payload.companyName)
  await page.getByLabel('Contact Person Name').fill(payload.fullName)
  await page.getByLabel('Email Address').fill(payload.email)
  await page.getByLabel('Phone Number').fill('+254700000000')
  await page.getByLabel('Industry').fill(payload.industry || '')
  await page.getByLabel('Country').fill(payload.country)
  await page.getByRole('button', { name: /submit request/i }).click()
  await expect(page.getByText(/thank you\. we have received your request\./i)).toBeVisible()
}

export async function loginViaApi(
  request: APIRequestContext,
  apiBaseUrl: string,
  credentials: LoginCredentials,
) {
  const response = await request.post(`${apiBaseUrl}/api/auth/login`, {
    data: credentials,
  })
  expect(response.ok(), `Login API failed with status ${response.status()}`).toBeTruthy()
  const data = (await response.json()) as { token?: string }
  expect(data.token, 'Missing token in login response').toBeTruthy()
  return data.token as string
}

export async function signupViaApi(
  request: APIRequestContext,
  apiBaseUrl: string,
  payload: SignupInput,
) {
  const response = await request.post(`${apiBaseUrl}/api/auth/signup`, {
    data: payload,
  })
  expect(response.ok(), `Signup API failed with status ${response.status()}`).toBeTruthy()
  const data = (await response.json()) as { token?: string }
  expect(data.token, 'Missing token in signup response').toBeTruthy()
  return data.token as string
}
