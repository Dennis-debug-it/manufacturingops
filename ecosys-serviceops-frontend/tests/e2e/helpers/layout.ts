import { expect, type Page } from '@playwright/test'

export async function assertNoHorizontalOverflow(page: Page, tolerance = 4) {
  const metrics = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }))

  expect(metrics.scrollWidth, `Expected no horizontal overflow, got scrollWidth=${metrics.scrollWidth} clientWidth=${metrics.clientWidth}`)
    .toBeLessThanOrEqual(metrics.clientWidth + tolerance)
}

export async function openAppNavigationIfCollapsed(page: Page) {
  const menuButton = page.getByRole('button', { name: /open navigation menu/i })
  if (await menuButton.isVisible().catch(() => false)) {
    await menuButton.click()
  }
}
