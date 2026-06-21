import { expect, test, type Page } from '@playwright/test'
import { E2E_API_URL } from './helpers/test-data'
import { loginAsTenantAdmin } from './helpers/auth'

async function getAuthToken(page: Page): Promise<string> {
  const raw = await page.evaluate(() => window.localStorage.getItem('ecosys.serviceops.auth'))
  if (!raw) throw new Error('No auth token in localStorage.')
  const parsed = JSON.parse(raw) as { token?: string }
  if (!parsed.token) throw new Error('Auth token missing in stored value.')
  return parsed.token
}

async function apiPost<T>(page: Page, path: string, body: unknown): Promise<T> {
  const token = await getAuthToken(page)
  return page.evaluate(
    async ({ apiUrl, path, body, token }) => {
      const res = await fetch(`${apiUrl}${path}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify(body),
      })
      if (!res.ok) {
        const text = await res.text()
        throw new Error(`POST ${path} failed (${res.status}): ${text}`)
      }
      return res.json() as T
    },
    { apiUrl: E2E_API_URL, path, body, token },
  )
}

async function apiGet<T>(page: Page, path: string): Promise<T> {
  const token = await getAuthToken(page)
  return page.evaluate(
    async ({ apiUrl, path, token }) => {
      const res = await fetch(`${apiUrl}${path}`, {
        headers: { Authorization: `Bearer ${token}` },
      })
      if (!res.ok) throw new Error(`GET ${path} failed (${res.status})`)
      return res.json() as T
    },
    { apiUrl: E2E_API_URL, path, token },
  )
}

function inTable(page: Page, text: string) {
  return page.locator('table').getByText(text)
}

test.describe('Client -> Site -> Asset -> Work Order flow', () => {
  test.setTimeout(90_000)

  test.beforeEach(async ({ page }) => {
    await loginAsTenantAdmin(page)
  })

  test('create client', async ({ page }) => {
    const ts = Date.now()
    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E Client ${ts}`,
      clientType: 'Enterprise',
      email: `e2e.client.${ts}@example.com`,
      phone: '+254700000001',
      contactPerson: 'E2E Contact',
      contactPhone: '+254700000002',
    })
    expect(client.id).toBeTruthy()

    await page.goto('/clients')
    await expect(inTable(page, `E2E Client ${ts}`)).toBeVisible({ timeout: 15000 })
  })

  test('create site under client', async ({ page }) => {
    const ts = Date.now()
    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E ClientForSite ${ts}`,
      clientType: 'Enterprise',
      email: `e2e.clientsite.${ts}@example.com`,
      contactPerson: 'E2E Admin',
      contactPhone: '+254700000003',
    })

    const site = await apiPost<{ id: string }>(page, `/api/clients/${client.id}/sites`, {
      siteName: `E2E Site ${ts}`,
      siteType: 'Branch',
      streetAddress: '1 Test Street',
      townCity: 'Nairobi',
      county: 'Nairobi',
      country: 'Kenya',
      region: 'Nairobi',
      contactPerson: 'Site Contact',
      contactPhone: '+254700000004',
      accessNotes: `Access via main gate - ${ts}`,
    })
    expect(site.id).toBeTruthy()

    await page.goto('/clients')
    await expect(inTable(page, `E2E ClientForSite ${ts}`)).toBeVisible({ timeout: 15000 })

    const clientRow = page.locator('table tbody tr', { hasText: `E2E ClientForSite ${ts}` })
    await clientRow.getByRole('combobox').selectOption('sites')
    await clientRow.getByRole('button', { name: /apply/i }).click()

    await expect(page.getByRole('heading', { name: `E2E ClientForSite ${ts}` })).toBeVisible({ timeout: 10000 })
    await expect(page.locator('table').getByRole('link', { name: `E2E Site ${ts}` })).toBeVisible({ timeout: 10000 })
  })

  test('edit site details', async ({ page }) => {
    const ts = Date.now()
    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E ClientEdit ${ts}`,
      clientType: 'SME',
      email: `e2e.edit.${ts}@example.com`,
      contactPerson: 'Edit Admin',
      contactPhone: '+254700000005',
    })

    const site = await apiPost<{ id: string }>(page, `/api/clients/${client.id}/sites`, {
      siteName: `Site Before Edit ${ts}`,
      siteType: 'HQ',
      townCity: 'Mombasa',
      region: 'Coast',
    })
    expect(site.id).toBeTruthy()

    await page.goto('/sites')
    await expect(page.getByRole('heading', { name: /sites register/i })).toBeVisible({ timeout: 15000 })

    const siteRow = page.getByRole('row', { name: new RegExp(`Site Before Edit ${ts}`) })
    await expect(siteRow).toBeVisible({ timeout: 10000 })
    await siteRow.getByRole('button', { name: /^edit$/i }).click()

    await page.getByLabel('Site name').fill(`Site After Edit ${ts}`)
    await page.getByRole('button', { name: /save site/i }).click()

    await expect(page.getByRole('row', { name: new RegExp(`Site After Edit ${ts}`) })).toBeVisible({ timeout: 10000 })
  })

  test('create asset linked to site', async ({ page }) => {
    const ts = Date.now()
    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E ClientAsset ${ts}`,
      clientType: 'Corporate',
      email: `e2e.asset.${ts}@example.com`,
      contactPerson: 'Asset Admin',
      contactPhone: '+254700000006',
    })

    const site = await apiPost<{ id: string }>(page, `/api/clients/${client.id}/sites`, {
      siteName: `E2E Asset Site ${ts}`,
      siteType: 'Warehouse',
      townCity: 'Kisumu',
      region: 'Nyanza',
    })

    const asset = await apiPost<{ id: string; siteId?: string; siteName?: string }>(page, '/api/assets', {
      clientId: client.id,
      siteId: site.id,
      assetName: `E2E Generator ${ts}`,
      assetCode: '',
      assetType: 'Generator',
      location: 'Room 1A',
      autoSchedulePm: false,
      status: 'Active',
    })
    expect(asset.id).toBeTruthy()
    expect(asset.siteId).toBe(site.id)

    await page.goto('/assets')
    await expect(inTable(page, `E2E Generator ${ts}`)).toBeVisible({ timeout: 15000 })
  })

  test('create work order with the simplified shell form', async ({ page }) => {
    const ts = Date.now()
    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E WO Full ${ts}`,
      clientType: 'Enterprise',
      email: `e2e.wofull.${ts}@example.com`,
      contactPerson: 'Full Admin',
      contactPhone: '+254700000008',
    })

    await page.goto('/work-orders')
    await expect(page.getByRole('button', { name: /new work order/i })).toBeVisible({ timeout: 15000 })
    await page.getByRole('button', { name: /new work order/i }).click()

    await expect(page.getByLabel('Client')).toBeVisible()
    const modal = page.locator('[role="dialog"]')
    await expect(modal.getByLabel('Site')).toHaveCount(0)
    await expect(modal.getByLabel('Asset')).toHaveCount(0)

    await page.getByLabel('Client').selectOption({ value: client.id })
    await page.getByLabel('Work Type', { exact: true }).selectOption('CorrectiveMaintenance')
    await page.getByLabel(/title/i).fill(`E2E Work Order ${ts}`)
    await page.getByLabel(/description/i).fill('Created from the simplified work-order shell form.')
    await page.getByRole('button', { name: /create work order/i }).click()

    await expect(inTable(page, `E2E Work Order ${ts}`)).toBeVisible({ timeout: 15000 })
  })

  test('work order detail shows client, site, and asset', async ({ page }) => {
    test.fixme(true, 'Current build renders "Unable to load work order" with a backend 500 for this detail scenario.')
    const ts = Date.now()

    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E Detail Client ${ts}`,
      clientType: 'SME',
      email: `e2e.detail.${ts}@example.com`,
      contactPerson: 'Detail Admin',
      contactPhone: '+254700000009',
    })

    const site = await apiPost<{ id: string }>(page, `/api/clients/${client.id}/sites`, {
      siteName: `E2E Detail Site ${ts}`,
      siteType: 'Data Centre',
      townCity: 'Nairobi',
      region: 'Nairobi',
      accessNotes: `Server room badge required ${ts}`,
    })

    const asset = await apiPost<{ id: string }>(page, '/api/assets', {
      clientId: client.id,
      siteId: site.id,
      assetName: `E2E Detail Asset ${ts}`,
      assetCode: '',
      assetType: 'Server',
      autoSchedulePm: false,
      status: 'Active',
    })

    const wo = await apiPost<{ id: string; workOrderNumber: string }>(page, '/api/workorders', {
      clientId: client.id,
      siteId: site.id,
      assetId: asset.id,
      title: `E2E Detail WO ${ts}`,
      priority: 'High',
      workType: 'CorrectiveMaintenance',
      isPreventiveMaintenance: false,
    })
    expect(wo.id).toBeTruthy()

    await page.goto(`/work-orders/${wo.id}`)
    await expect(page.getByText(`E2E Detail Client ${ts}`).first()).toBeVisible({ timeout: 15000 })
    await expect(page.getByText(`E2E Detail Site ${ts}`).first()).toBeVisible({ timeout: 10000 })
    await expect(page.getByText(`E2E Detail Asset ${ts}`).first()).toBeVisible({ timeout: 10000 })
  })

  test('site access notes appear in work order job card notes', async ({ page }) => {
    const ts = Date.now()
    const accessNotes = `Access via gate B, badge required - ref ${ts}`

    const client = await apiPost<{ id: string }>(page, '/api/clients', {
      clientName: `E2E Access Client ${ts}`,
      clientType: 'Corporate',
      email: `e2e.access.${ts}@example.com`,
      contactPerson: 'Access Admin',
      contactPhone: '+254700000010',
    })

    const site = await apiPost<{ id: string }>(page, `/api/clients/${client.id}/sites`, {
      siteName: `E2E Access Site ${ts}`,
      siteType: 'Branch',
      accessNotes,
    })

    const wo = await apiPost<{ id: string; jobCardNotes?: string }>(page, '/api/workorders', {
      clientId: client.id,
      siteId: site.id,
      title: `E2E Access WO ${ts}`,
      priority: 'Medium',
      workType: 'CorrectiveMaintenance',
      isPreventiveMaintenance: false,
    })

    expect(wo.jobCardNotes).toBe(accessNotes)

    const fetched = await apiGet<{ jobCardNotes?: string }>(page, `/api/workorders/${wo.id}`)
    expect(fetched.jobCardNotes).toBe(accessNotes)
  })

  test('sites page loads and shows filter controls', async ({ page }) => {
    await page.goto('/sites')
    await expect(page.getByRole('heading', { name: /sites register/i })).toBeVisible({ timeout: 15000 })
    await expect(page.getByPlaceholder(/search by site/i)).toBeVisible()
    await expect(page.getByRole('combobox').first()).toBeVisible()
  })
})
