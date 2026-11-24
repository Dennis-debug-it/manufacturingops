import { expect, test, type Page, type Route } from '@playwright/test'
import { CORS_HEADERS, json, mockAuthSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL
const FACTORY = 'factory-1'

const settings = {
  factoryId: FACTORY, defaultMeasureBasis: 'Weighbridge', moistureRule: 'DeductKg', baseMoisturePct: 25, rejectMoisturePct: 45, deductionPctPerPoint: 1.5,
  priceBands: [{ upToPct: 30, percentOfPrice: 100 }], defaultKgPerLoad: null, burnWindowDays: 14, lowCoverDays: 5, seasoningDays: 28, targetKgPerUnit: 1.6,
}

const lookups = {
  factories: [{ id: FACTORY, name: 'Chelal Tea Factory' }],
  factoryId: FACTORY,
  species: [{ id: 'sp-1', name: 'Eucalyptus', kgPerStackedM3: 380, energyMjPerKg: null, isActive: true }],
  suppliers: [{ id: 'sup-1', name: 'Kiprop Woodlot' }],
  stacks: [{ id: 'stack-1', name: 'Y1-S01' }],
  assets: [{ id: 'asset-1', name: 'Boiler 1 (BLR-1)' }],
  settings,
  measureBases: ['Weighbridge', 'StackedVolume', 'LoadCount'],
  moistureRules: ['DeductKg', 'PriceBands'],
  fuelForms: ['Logs', 'Splits', 'Briquettes'],
  supplierTypes: ['Farmer', 'Merchant'],
  stackStatuses: ['Receiving', 'Seasoning', 'Ready', 'Depleted'],
  adjustmentReasons: ['CountVariance', 'DryingLoss'],
  canManage: true, canReceive: true, canIssue: true,
}

const summary = {
  factoryId: FACTORY, from: '2026-11-01T00:00:00Z', to: '2026-12-01T00:00:00Z', stockKg: 6500, unstackedKg: 0, averageDailyBurnKg: 2000, daysOfCover: 3.3, lowCover: true, lowCoverDays: 5,
  openingKg: 0, receivedKg: 10500, issuedKg: 4000, adjustmentKg: 0, closingKg: 6500, rejectedKg: 0, deductedKg: 0, amountPayable: 52500, averageMoisturePct: 22, madeTeaKg: 2000,
  kgPerUnit: 2, costPerUnit: 10, targetKgPerUnit: 1.6, fuelUnit: 'kg',
  stacks: [{ id: 'stack-1', code: 'Y1-S01', status: 'Seasoning', species: 'Eucalyptus', kg: 6500, averageMoisturePct: 22, builtOn: '2026-11-01T00:00:00Z', readyFrom: '2026-12-10T00:00:00Z', isReady: false }],
  suppliers: [{ supplierId: 'sup-1', supplier: 'Kiprop Woodlot', deliveries: 2, netKg: 10500, averageMoisturePct: 22, rejected: 0, rejectedKg: 0, amount: 52500 }],
  daily: [{ date: '2026-11-20T00:00:00Z', receivedKg: 10500, issuedKg: 2000, madeTeaKg: 1000, kgPerUnit: 2 }],
}

async function preflight(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }
  return false
}

async function openYard(page: Page) {
  await registerApiCatchAll(page)
  // The first-login welcome tour would cover the page.
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
  const session = await mockAuthSession(page, { role: 'tenantAdmin', email: 'ops@tenant.test', fullName: 'Ops Admin', tenantName: 'Chelal Tea', tenantId: 'tenant-1' })
  await page.goto('/login')
  await page.getByLabel('Email').fill(session.loginEmail)
  await page.getByLabel('Password').fill(session.loginPassword)
  await page.getByRole('button', { name: /^sign in$/i }).click()
  await page.waitForURL(/\/dashboard/, { timeout: 20000 })
  // The plan includes the Wood Fuel add-on.
  await page.route(`${API}/api/tenant/license`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json({ planCode: 'professional', planName: 'Ecosys Factory Maintenance Professional', modules: ['operations', 'finance', 'maintenance', 'inventory', 'advanced', 'woodfuel'] }))
  })
  await page.route(`${API}/api/wood-fuel/lookups**`, async (route) => { if (!(await preflight(route))) await route.fulfill(json(lookups)) })
  await page.route(`${API}/api/wood-fuel/summary**`, async (route) => { if (!(await preflight(route))) await route.fulfill(json(summary)) })
  await page.route(`${API}/api/wood-fuel/counts**`, async (route) => { if (!(await preflight(route))) await route.fulfill(json([])) })
}

test.describe('wood fuel', () => {
  test('the yard board shows stock, cover and wood per kg of made tea', async ({ page }) => {
    await openYard(page)
    await page.goto('/wood-fuel')

    await expect(page.getByRole('heading', { name: 'Wood fuel' })).toBeVisible()
    await expect(page.getByText('6.5 t').first()).toBeVisible()
    await expect(page.getByText('3.3 days').first()).toBeVisible()
    await expect(page.getByText('2 kg', { exact: true })).toBeVisible()
    await expect(page.getByText(/Only 3.3 days of wood is left/)).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Y1-S01' })).toBeVisible()
    await expect(page.getByRole('cell', { name: 'Kiprop Woodlot' })).toBeVisible()
  })

  test('a weighed delivery shows what it is worth before it is saved, then is sent', async ({ page }) => {
    await openYard(page)
    let posted: Record<string, unknown> | null = null
    await page.route(`${API}/api/wood-fuel/deliveries`, async (route) => {
      if (await preflight(route)) return
      posted = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill(json({ id: 'd-1', deliveryNumber: 'WF-000001', decision: 'AcceptedWithDeduction' }, 200))
    })
    await page.goto('/wood-fuel')

    await page.getByRole('button', { name: 'Receive wood' }).click()
    await page.getByLabel('Gross weight (kg)').fill('12500')
    await page.getByLabel('Tare weight (kg)').fill('5300')
    await expect(page.getByText('Received:')).toContainText('7,200 kg')
    await page.getByLabel('Moisture reading 1').fill('30')
    await page.getByLabel('Moisture reading 2').fill('32')
    await page.getByLabel('Moisture reading 3').fill('34')

    // 32 % is 7 points over 25 %; at 1.5 % a point, 10.5 % (756 kg) is not paid.
    await expect(page.getByText('Accepted with deduction').first()).toBeVisible()
    await expect(page.getByText('Payable 6,444 kg')).toBeVisible()
    await page.getByRole('button', { name: 'Save delivery' }).click()

    await expect(page.getByText('WF-000001: Accepted with deduction')).toBeVisible()
    expect(posted).toMatchObject({ factoryId: FACTORY, measureBasis: 'Weighbridge', grossKg: 12500, tareKg: 5300, moistureSamples: [30, 32, 34], reject: false })
  })

  test('wood that is too wet is shown as rejected', async ({ page }) => {
    await openYard(page)
    await page.goto('/wood-fuel')

    await page.getByRole('button', { name: 'Receive wood' }).click()
    await page.getByLabel('Gross weight (kg)').fill('9000')
    await page.getByLabel('Tare weight (kg)').fill('4000')
    await page.getByLabel('Moisture reading 1').fill('52')

    await expect(page.getByText('Rejected', { exact: true }).first()).toBeVisible()
    await expect(page.getByText('above the 45% limit')).toBeVisible()
  })

  test('with no signal a delivery is kept on the device', async ({ page, context }) => {
    await openYard(page)
    await page.goto('/wood-fuel')
    await expect(page.getByText('6.5 t').first()).toBeVisible()

    await context.setOffline(true)
    await page.getByRole('button', { name: 'Receive wood' }).click()
    await page.getByLabel('Gross weight (kg)').fill('8000')
    await page.getByLabel('Tare weight (kg)').fill('5000')
    await page.getByRole('button', { name: 'Save delivery' }).click()

    await expect(page.getByText('Saved on this device. It is sent when the signal is back.')).toBeVisible()
    await context.setOffline(false)
  })
})
