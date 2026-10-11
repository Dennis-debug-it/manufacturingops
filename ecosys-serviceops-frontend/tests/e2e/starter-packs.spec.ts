import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

const pack = (installed: boolean) => ({
  id: 'pack-gen', name: 'Generator Contractor Starter Pack', code: 'generator-contractor-starter',
  description: 'Starter templates, asset datasheets, and service checklists for generator maintenance.',
  contractorType: 'Generator', isActive: true, version: 1, isInstalled: installed, installationStatus: installed ? 'Installed' : null,
  assetCategories: [
    { id: 'c1', name: 'Diesel Generator', fieldCount: 18, sortOrder: 1 },
    { id: 'c2', name: 'ATS / Changeover Panel', fieldCount: 0, sortOrder: 2 },
  ],
  checklistTemplates: [
    { id: 't1', name: 'Generator Preventive Maintenance', workType: 'Preventive', sectionCount: 5, itemCount: 25 },
    { id: 't2', name: 'Generator Breakdown / Corrective Maintenance', workType: 'Corrective', sectionCount: 1, itemCount: 8 },
  ],
})

test('an admin installs a trade starter pack from PM Templates', async ({ page }) => {
  await registerApiCatchAll(page)
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
  let installed = false
  await page.route(`${E2E_API_URL}/api/starter-packs`, (route) =>
    route.request().method() === 'OPTIONS' ? route.fulfill({ status: 204, headers: CORS_HEADERS }) : route.fulfill(json([pack(installed)])))
  await page.route(`${E2E_API_URL}/api/starter-packs/pack-gen/install`, (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    installed = true
    return route.fulfill(json(pack(true)))
  })
  await loginWithMockSession(page, { role: 'tenantAdmin' })

  await page.goto('/settings/pm-templates')
  const card = page.getByTestId('starter-pack-generator-contractor-starter')
  await expect(card).toContainText('Generator Preventive Maintenance')
  await expect(card).toContainText('5 sections, 25 items')
  await expect(card).toContainText('Diesel Generator')

  await card.getByRole('button', { name: 'Install Generator pack' }).click()
  await expect(page.getByText('Generator Contractor Starter Pack installed')).toBeVisible()
  await expect(card).toContainText('Installed')
  await expect(card.getByRole('button', { name: /install/i })).toHaveCount(0)
})
