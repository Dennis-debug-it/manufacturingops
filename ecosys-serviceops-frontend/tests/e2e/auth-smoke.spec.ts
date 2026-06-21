import { expect, test, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const API = 'http://localhost:5072'
const WORK_ORDER_ID = 'auth-smoke-wo-1'

async function mockProtectedWorkOrder(page: Page) {
  await page.route(`${API}/api/workorders/${WORK_ORDER_ID}`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json({
      id: WORK_ORDER_ID,
      workOrderNumber: 'WO-AUTH-001',
      title: 'Auth smoke work order',
      description: 'Protected route smoke test.',
      priority: 'Medium',
      status: 'Open',
      branchId: 'branch-1',
      branchName: 'Nairobi Service Hub',
      clientId: 'client-1',
      clientName: 'Acme Facilities',
      assetId: 'asset-1',
      assetName: 'Generator 01',
      workType: 'CorrectiveMaintenance',
      assignedTechnicianIds: [],
      technicianAssignments: [],
      checklistItems: [],
      isUnassigned: true,
      isPreventiveMaintenance: false,
    }))
  })
  await page.route(`${API}/api/workorders/${WORK_ORDER_ID}/execution`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json({ notes: { findings: '', workDone: '' }, photos: [], materialUsages: [], signatures: [], progressUpdates: [], reportPreview: null }))
  })
  await page.route(`${API}/api/workorders/${WORK_ORDER_ID}/events`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/workorders/${WORK_ORDER_ID}/assignment-history`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/materials**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/technicians**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/material-requests**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/assignment-groups**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/pm/templates**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
}

test.describe('auth smoke', () => {
  test('tenant admin can login and reach /dashboard', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 })
    await registerApiCatchAll(page)
    await loginWithMockSession(page, { role: 'tenantAdmin' })
    await expect(page).toHaveURL(/\/dashboard/)
    await expect(page.getByTestId('command-centre-sidebar')).toBeVisible()
    await expect(page.getByRole('button', { name: /open navigation menu/i })).toBeHidden()
    await expect(page.getByText(/operational overview/i)).toBeVisible()

    const sidebarBox = await page.getByTestId('command-centre-sidebar').boundingBox()
    const mainBox = await page.getByRole('main').boundingBox()
    expect(sidebarBox).not.toBeNull()
    expect(mainBox).not.toBeNull()
    expect(mainBox!.x).toBeGreaterThanOrEqual((sidebarBox?.x ?? 0) + (sidebarBox?.width ?? 0) - 1)
  })

  test('platform owner can login and reach /platform', async ({ page }) => {
    await registerApiCatchAll(page)
    await loginWithMockSession(page, { role: 'platformOwner' })
    await expect(page).toHaveURL(/\/platform/)
    await expect(page.getByTestId('command-centre-sidebar')).toBeVisible()
  })

  test('protected route redirects to /login when unauthenticated', async ({ page }) => {
    await page.goto('/work-orders/auth-smoke-wo-1')
    await expect(page).toHaveURL(/\/login/)
  })

  test('authenticated user can open a protected work order page without bouncing to /login', async ({ page }) => {
    await registerApiCatchAll(page)
    await mockProtectedWorkOrder(page)
    await loginWithMockSession(page, { role: 'tenantAdmin' })

    await page.goto(`/work-orders/${WORK_ORDER_ID}`)
    await expect(page).toHaveURL(new RegExp(`/work-orders/${WORK_ORDER_ID}$`))
    await expect(page.getByText(/auth smoke work order/i)).toBeVisible()
  })
})
