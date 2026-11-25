import { expect, test, type Page, type Route } from '@playwright/test'
import { CORS_HEADERS, json, mockAuthSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const API = E2E_API_URL
const FACTORY = 'factory-1'

const settings = {
  factoryId: FACTORY, defaultMeasureBasis: 'Weighbridge', moistureRule: 'DeductKg', baseMoisturePct: 25, rejectMoisturePct: 45, deductionPctPerPoint: 1.5,
  priceBands: [{ upToPct: 30, percentOfPrice: 100 }], defaultKgPerLoad: null, burnWindowDays: 14, lowCoverDays: 5, seasoningDays: 28, targetKgPerUnit: 1.6, wetAlertMoisturePct: 35,
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

  test('a truck from the gate is weighed in, and its tare weighed out later', async ({ page }) => {
    await openYard(page)
    await page.route(`${API}/api/wood-fuel/gate-vehicles**`, async (route) => {
      if (await preflight(route)) return
      await route.fulfill(json([{ id: 'veh-1', registration: 'KCA123A', driverName: 'Peter Otieno', driverPhone: null, enteredAt: '2026-11-24T07:00:00Z', purpose: 'Delivery', description: null, deliveryNumber: null, pendingDeliveryId: null, awaitingTare: false, hasDelivery: false }]))
    })
    let weighedIn: Record<string, unknown> | null = null
    await page.route(`${API}/api/wood-fuel/deliveries`, async (route) => {
      if (await preflight(route)) return
      weighedIn = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill(json({ id: 'd-9', deliveryNumber: 'WF-000009', decision: 'Pending' }))
    })
    await page.goto('/wood-fuel')

    await page.getByRole('button', { name: 'Receive wood' }).click()
    await page.getByLabel('Truck at the gate').selectOption('veh-1')
    await page.getByLabel('Gross weight (kg)').fill('12500')
    await page.getByLabel(/weigh the tare later/).check()
    await expect(page.getByLabel('Tare weight (kg)')).toBeDisabled()
    await expect(page.getByText('weighed in. Finish the delivery once the truck is weighed out.')).toBeVisible()
    await page.getByRole('button', { name: 'Save weigh-in' }).click()

    await expect(page.getByText('WF-000009: Awaiting tare. Weigh the tare to finish it.')).toBeVisible()
    expect(weighedIn).toMatchObject({ vehicleMovementId: 'veh-1', tareLater: true, grossKg: 12500, tareKg: null, vehicleRegistration: 'KCA123A', driverName: 'Peter Otieno', stackId: null })
  })

  test('a delivery waiting for its tare is weighed out from the Deliveries tab', async ({ page }) => {
    await openYard(page)
    const waiting = {
      id: 'd-9', deliveryNumber: 'WF-000009', factoryId: FACTORY, factory: 'Chelal Tea Factory', receivedAt: '2026-11-24T07:30:00Z', supplierId: 'sup-1', supplier: 'Kiprop Woodlot', supplierPhone: '', supplierPaymentDetails: '',
      speciesId: null, species: '', fuelForm: 'Logs', sourceArea: null, transportPermit: null, supplierNoteNumber: null, vehicleRegistration: 'KCA123A', driverName: 'Peter Otieno', measureBasis: 'Weighbridge',
      grossKg: 12500, tareKg: null, stackedM3: null, loads: null, kgPerLoad: null, netKg: 0, averageMoisturePct: 30, moistureSamples: [30], decision: 'Pending', decisionReason: 'Gross weighed. Waiting for the tare.',
      rejectedKg: 0, deductedKg: 0, payableKg: 0, pricePerTonne: 5000, pricePercent: 100, amountPayable: 0, stackId: null, stack: '', receivedBy: 'Yard Clerk', notes: null, vehicleMovementId: 'veh-1',
    }
    await page.route(`${API}/api/wood-fuel/deliveries?**`, async (route) => {
      if (await preflight(route)) return
      const wanted = new URL(route.request().url()).searchParams.get('decision')
      await route.fulfill(json(wanted === 'Pending' || !wanted ? [waiting] : []))
    })
    let weighedOut: Record<string, unknown> | null = null
    await page.route(`${API}/api/wood-fuel/deliveries/d-9/weigh-out`, async (route) => {
      if (await preflight(route)) return
      weighedOut = route.request().postDataJSON() as Record<string, unknown>
      await route.fulfill(json({ ...waiting, decision: 'AcceptedWithDeduction', netKg: 7200 }))
    })
    await page.goto('/wood-fuel')
    await page.getByRole('tab', { name: 'Deliveries' }).click()

    await expect(page.getByText('Waiting for the tare')).toBeVisible()
    await page.getByRole('button', { name: 'Weigh out' }).click()
    await page.getByLabel('Tare weight (kg)').fill('5300')
    await page.getByLabel('More moisture reading 1').fill('34')
    await expect(page.getByText('Received:')).toContainText('7,200 kg')
    // (The decision filter behind the drawer also lists this word, so look inside the drawer.)
    await expect(page.locator('aside').getByText('Accepted with deduction')).toBeVisible()
    await page.getByRole('button', { name: 'Finish delivery' }).click()

    await expect(page.getByText('WF-000009: Accepted with deduction')).toBeVisible()
    expect(weighedOut).toMatchObject({ tareKg: 5300, moistureSamples: [34], reject: false })
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
