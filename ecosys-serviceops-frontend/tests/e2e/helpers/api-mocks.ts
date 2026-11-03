import type { Page } from '@playwright/test'

function json(body: unknown) {
  return {
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  }
}

export async function mockPlatformEmailVerifySuccess(page: Page) {
  await page.route('**/api/platform/settings/email/verify', async (route) => {
    await route.fulfill(
      json({
        success: true,
        lastTestedAt: new Date().toISOString(),
        lastError: null,
      }),
    )
  })
}

export async function mockPlatformEmailTestSuccess(page: Page) {
  await page.route('**/api/platform/settings/email/test', async (route) => {
    await route.fulfill(
      json({
        success: true,
        lastTestedAt: new Date().toISOString(),
        lastError: null,
      }),
    )
  })
}

export async function mockTenantEmailVerifySuccess(page: Page) {
  await page.route('**/api/platform/tenants/*/email-settings/verify', async (route) => {
    await route.fulfill(
      json({
        success: true,
        lastTestedAt: new Date().toISOString(),
        lastError: null,
      }),
    )
  })
}

export async function mockTenantEmailTestSuccess(page: Page) {
  await page.route('**/api/platform/tenants/*/email-settings/test', async (route) => {
    await route.fulfill(
      json({
        success: true,
        lastTestedAt: new Date().toISOString(),
        lastError: null,
      }),
    )
  })
}

export async function mockEmailIntakeSimulationSuccess(page: Page) {
  await page.route('**/api/settings/intake-protocols/*/test', async (route) => {
    await route.fulfill(
      json({
        success: true,
        lastTriggeredAt: new Date().toISOString(),
        lastTriggerStatus: 'Parsed subject: Generator failure at Main Branch | Sender: customer@example.com | Priority: High',
        lastError: null,
      }),
    )
  })
}
