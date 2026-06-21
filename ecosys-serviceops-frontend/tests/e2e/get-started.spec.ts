import { expect, test } from '@playwright/test'
import { E2E_API_BASE_URL } from '../helpers/env'

test.describe('Get Started flow', () => {
  test('renders the CRM lead form with required fields and no payment prompts', async ({ page }) => {
    await page.goto('/get-started')

    await expect(page.locator('h1')).toContainText('Get Started with Ecosys')
    await expect(page.getByText(/tell us about your business/i).first()).toBeVisible()
    await expect(page.getByLabel('Company Name')).toBeVisible()
    await expect(page.getByLabel('Contact Person Name')).toBeVisible()
    await expect(page.getByLabel('Email Address')).toBeVisible()
    await expect(page.getByLabel('Phone Number')).toBeVisible()
    await expect(page.getByLabel('Country')).toBeVisible()
    await expect(page.getByLabel('Industry')).toBeVisible()
    await expect(page.getByLabel('Company Size / Number of Users')).toBeVisible()
    await expect(page.getByLabel('Message / Business Need')).toBeVisible()
    await expect(page.getByLabel('Preferred Contact Method')).toBeVisible()

    await expect(page.getByText(/payment/i)).toHaveCount(0)
    await expect(page.getByText(/subscription plan/i)).toHaveCount(0)
    await expect(page.getByText(/billing/i)).toHaveCount(0)
    await expect(page.getByText(/sla/i)).toHaveCount(0)
  })

  test('shows inline validation errors when required fields are missing', async ({ page }) => {
    await page.goto('/get-started')
    await page.getByRole('button', { name: /submit request/i }).click()

    await expect(page.getByText('Company name is required.')).toBeVisible()
    await expect(page.getByText('Contact person name is required.')).toBeVisible()
    await expect(page.getByText('Email address is required.')).toBeVisible()
    await expect(page.getByText('Phone number is required.')).toBeVisible()
  })

  test('shows the success confirmation after submission', async ({ page }) => {
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
    await page.getByLabel('Company Name').fill('Northwind Manufacturing')
    await page.getByLabel('Contact Person Name').fill('Amina Njoroge')
    await page.getByLabel('Email Address').fill('amina@example.com')
    await page.getByLabel('Phone Number').fill('+254711111111')
    await page.getByLabel('Message / Business Need').fill('We need guided onboarding for plant maintenance operations.')
    await page.getByRole('button', { name: /submit request/i }).click()

    await expect(page.getByText(/thank you\. we have received your request\./i)).toBeVisible()
    await expect(page.getByText(/understand your needs and guide you through the next steps/i)).toBeVisible()
  })
})
