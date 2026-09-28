import { expect, test, type Page, type Route } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

// Must match the API origin the app under test calls (E2E_API_URL).
const API = E2E_API_URL

type CategoryField = {
  id: string
  assetCategoryId: string
  fieldName: string
  fieldLabel: string
  fieldType: string
  dropdownOptions?: string | null
  unit?: string | null
  isRequired: boolean
  displayOrder: number
}

type CategoryRecord = {
  id: string
  tenantId: string
  name: string
  parentCategoryName?: string | null
  icon?: string | null
  isDefault: boolean
  isActive: boolean
  displayOrder: number
  fields: CategoryField[]
}

type AssetRecord = {
  id: string
  branchId?: string | null
  branchName?: string | null
  clientId: string
  clientName?: string | null
  siteId?: string | null
  siteName?: string | null
  assetCategoryId?: string | null
  assetCategoryName?: string | null
  assetName: string
  assetCode: string
  assetType?: string | null
  location?: string | null
  serialNumber?: string | null
  manufacturer?: string | null
  model?: string | null
  installationDate?: string | null
  warrantyExpiryDate?: string | null
  recommendedPmFrequency?: string | null
  autoSchedulePm: boolean
  lastPmDate?: string | null
  nextPmDate?: string | null
  notes?: string | null
  status: string
  createdAt: string
  customFieldValues: Array<{
    fieldDefinitionId: string
    fieldName: string
    fieldLabel: string
    fieldType: string
    unit?: string | null
    value: string
  }>
}

async function fulfillOptions(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }

  return false
}

async function mockAssetModule(page: Page) {
  const categories: CategoryRecord[] = [
    {
      id: 'cat-generator',
      tenantId: 'tenant-1',
      name: 'Generator',
      icon: 'tool',
      isDefault: true,
      isActive: true,
      displayOrder: 1,
      fields: [
        {
          id: 'field-capacity',
          assetCategoryId: 'cat-generator',
          fieldName: 'capacity_kva',
          fieldLabel: 'Capacity KVA',
          fieldType: 'Number',
          unit: 'kVA',
          isRequired: true,
          displayOrder: 1,
        },
        {
          id: 'field-fuel',
          assetCategoryId: 'cat-generator',
          fieldName: 'fuel_type',
          fieldLabel: 'Fuel Type',
          fieldType: 'Dropdown',
          dropdownOptions: 'Diesel,Petrol',
          isRequired: false,
          displayOrder: 2,
        },
      ],
    },
    {
      id: 'cat-hvac',
      tenantId: 'tenant-1',
      name: 'HVAC',
      icon: 'tool',
      isDefault: true,
      isActive: true,
      displayOrder: 2,
      fields: [],
    },
  ]

  const clients = [
    { id: 'client-1', clientName: 'Acme Facilities' },
    { id: 'client-2', clientName: 'Beta Energy' },
  ]

  const sitesByClient: Record<string, Array<{ id: string; siteName: string }>> = {
    'client-1': [
      { id: 'site-1', siteName: 'Nairobi HQ' },
      { id: 'site-2', siteName: 'Mombasa Depot' },
    ],
    'client-2': [
      { id: 'site-3', siteName: 'Kisumu Plant' },
    ],
  }

  const assets: AssetRecord[] = [
    {
      id: 'asset-1',
      clientId: 'client-1',
      clientName: 'Acme Facilities',
      siteId: 'site-1',
      siteName: 'Nairobi HQ',
      assetCategoryId: 'cat-generator',
      assetCategoryName: 'Generator',
      assetName: 'Generator 01',
      assetCode: 'GEN-001',
      assetType: 'Generator',
      autoSchedulePm: true,
      nextPmDate: '2026-06-20T00:00:00.000Z',
      status: 'Active',
      createdAt: '2026-05-10T08:00:00.000Z',
      customFieldValues: [
        {
          fieldDefinitionId: 'field-capacity',
          fieldName: 'capacity_kva',
          fieldLabel: 'Capacity KVA',
          fieldType: 'Number',
          unit: 'kVA',
          value: '250',
        },
      ],
    },
    {
      id: 'asset-2',
      clientId: 'client-2',
      clientName: 'Beta Energy',
      siteId: 'site-3',
      siteName: 'Kisumu Plant',
      assetCategoryId: 'cat-hvac',
      assetCategoryName: 'HVAC',
      assetName: 'Chiller 01',
      assetCode: 'HVC-001',
      assetType: 'HVAC',
      autoSchedulePm: true,
      nextPmDate: '2026-06-25T00:00:00.000Z',
      status: 'Active',
      createdAt: '2026-05-11T08:00:00.000Z',
      customFieldValues: [],
    },
  ]

  const assetAttachments = [
    {
      id: 'asset-att-1',
      entityType: 'Asset',
      entityId: 'asset-1',
      fileName: 'generator-manual.pdf',
      fileSize: 1200,
      mimeType: 'application/pdf',
      publicUrl: `${API}/api/attachments/asset-att-1/download`,
      uploadedByUserId: 'tenant-admin-1',
      createdAt: '2026-05-12T07:00:00.000Z',
    },
  ]

  await registerApiCatchAll(page)
  await loginWithMockSession(page, { role: 'tenantAdmin', email: 'admin@acme.test', fullName: 'Alice Admin', tenantId: 'tenant-1', tenantName: 'Acme Facilities' })

  await page.route(`${API}/api/asset-categories`, async (route) => {
    if (await fulfillOptions(route)) return

    if (route.request().method() === 'GET') {
      await route.fulfill(json(categories))
      return
    }

    const body = route.request().postDataJSON() as Record<string, unknown>
    const created: CategoryRecord = {
      id: 'cat-pump',
      tenantId: 'tenant-1',
      name: String(body.name),
      parentCategoryName: String(body.parentCategoryName ?? '') || null,
      icon: String(body.icon ?? 'tool'),
      isDefault: false,
      isActive: true,
      displayOrder: Number(body.displayOrder ?? categories.length + 1),
      fields: [],
    }
    categories.push(created)
    await route.fulfill(json(created))
  })

  await page.route(`${API}/api/asset-categories/*/fields`, async (route) => {
    if (await fulfillOptions(route)) return

    const categoryId = route.request().url().split('/api/asset-categories/')[1].split('/fields')[0]
    const category = categories.find((item) => item.id === categoryId)
    if (!category) {
      await route.fulfill(json({ message: 'Not found' }, 404))
      return
    }

    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON() as Record<string, unknown>
      const created: CategoryField = {
        id: `field-${category.fields.length + 1}`,
        assetCategoryId: categoryId,
        fieldName: String(body.fieldLabel).toLowerCase().replace(/\s+/g, '_'),
        fieldLabel: String(body.fieldLabel),
        fieldType: String(body.fieldType),
        dropdownOptions: String(body.dropdownOptions ?? '') || null,
        unit: String(body.unit ?? '') || null,
        isRequired: Boolean(body.isRequired),
        displayOrder: Number(body.displayOrder ?? category.fields.length + 1),
      }
      category.fields.push(created)
      await route.fulfill(json(created))
      return
    }

    await route.fulfill(json(category.fields))
  })

  await page.route(`${API}/api/asset-categories/*/fields/*`, async (route) => {
    if (await fulfillOptions(route)) return
    if (route.request().method() === 'DELETE') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json({}))
  })

  await page.route(`${API}/api/clients**`, async (route) => {
    if (await fulfillOptions(route)) return
    await route.fulfill(json(clients))
  })

  await page.route(`${API}/api/clients/*/sites**`, async (route) => {
    if (await fulfillOptions(route)) return
    const clientId = route.request().url().split('/api/clients/')[1].split('/sites')[0]
    await route.fulfill(json(sitesByClient[clientId] ?? []))
  })

  await page.route(`${API}/api/assets**`, async (route) => {
    if (await fulfillOptions(route)) return

    const url = new URL(route.request().url())
    if (route.request().method() === 'GET') {
      const clientId = url.searchParams.get('clientId')
      const siteId = url.searchParams.get('siteId')
      const categoryId = url.searchParams.get('categoryId')
      const status = url.searchParams.get('status')
      const search = url.searchParams.get('search')?.toLowerCase() ?? ''

      const filtered = assets.filter((asset) => {
        if (clientId && asset.clientId !== clientId) return false
        if (siteId && asset.siteId !== siteId) return false
        if (categoryId && asset.assetCategoryId !== categoryId) return false
        if (status === 'active' && asset.status === 'Inactive') return false
        if (status === 'inactive' && asset.status !== 'Inactive') return false
        if (search && !`${asset.assetName} ${asset.assetCode} ${asset.clientName} ${asset.siteName}`.toLowerCase().includes(search)) return false
        return true
      })

      await route.fulfill(json(filtered))
      return
    }

    const body = route.request().postDataJSON() as Record<string, unknown>
    const category = categories.find((item) => item.id === body.assetCategoryId)
    const created: AssetRecord = {
      id: `asset-${assets.length + 1}`,
      clientId: String(body.clientId),
      clientName: clients.find((item) => item.id === body.clientId)?.clientName,
      siteId: body.siteId ? String(body.siteId) : null,
      siteName: body.siteId ? Object.values(sitesByClient).flat().find((item) => item.id === body.siteId)?.siteName : null,
      assetCategoryId: body.assetCategoryId ? String(body.assetCategoryId) : null,
      assetCategoryName: category?.name ?? null,
      assetName: String(body.assetName),
      assetCode: String(body.assetCode || `AUTO-${assets.length + 1}`),
      assetType: String(body.assetType ?? ''),
      location: String(body.location ?? ''),
      serialNumber: String(body.serialNumber ?? ''),
      manufacturer: String(body.manufacturer ?? ''),
      model: String(body.model ?? ''),
      installationDate: body.installationDate ? String(body.installationDate) : null,
      warrantyExpiryDate: body.warrantyExpiryDate ? String(body.warrantyExpiryDate) : null,
      recommendedPmFrequency: body.recommendedPmFrequency ? String(body.recommendedPmFrequency) : null,
      autoSchedulePm: Boolean(body.autoSchedulePm),
      lastPmDate: body.lastPmDate ? String(body.lastPmDate) : null,
      nextPmDate: body.nextPmDate ? String(body.nextPmDate) : null,
      notes: body.notes ? String(body.notes) : null,
      status: String(body.status ?? 'Active'),
      createdAt: '2026-05-16T09:00:00.000Z',
      customFieldValues: ((body.customFieldValues ?? []) as Array<Record<string, unknown>>)
        .filter((item) => String(item.value ?? '').trim().length > 0)
        .map((item) => {
          const definition = category?.fields.find((field) => field.id === item.fieldDefinitionId)
          return {
            fieldDefinitionId: String(item.fieldDefinitionId),
            fieldName: definition?.fieldName ?? '',
            fieldLabel: definition?.fieldLabel ?? '',
            fieldType: definition?.fieldType ?? 'Text',
            unit: definition?.unit ?? null,
            value: String(item.value),
          }
        }),
    }
    assets.push(created)
    await route.fulfill(json(created, 201))
  })

  await page.route(`${API}/api/attachments/entity/Asset/*`, async (route) => {
    if (await fulfillOptions(route)) return
    const assetId = route.request().url().split('/api/attachments/entity/Asset/')[1]
    await route.fulfill(json(assetAttachments.filter((item) => item.entityId === assetId)))
  })
}

test.describe('asset categories and assets', () => {
  test.beforeEach(async ({ page }) => {
    await mockAssetModule(page)
  })

  test('create category and add custom field', async ({ page }) => {
    await page.goto('/settings/asset-categories')
    await expect(page.getByRole('heading', { name: 'Asset Categories', level: 1 })).toBeVisible()

    await page.getByRole('button', { name: /add category/i }).click()
    await page.getByLabel('Category name').fill('Pump')
    await page.getByLabel('Parent category (optional)').fill('Mechanical')
    await page.getByRole('button', { name: /save category/i }).click()

    await expect(page.getByText('Pump')).toBeVisible()
    await page.getByRole('button', { name: /pump/i }).click()
    await page.getByRole('button', { name: /add field/i }).click()
    await page.getByLabel('Display label').fill('Flow Rate')
    await page.getByLabel('Field type').selectOption('Number')
    await page.getByLabel('Unit (optional)').fill('L/min')
    await page.getByRole('button', { name: /save field/i }).click()

    await expect(page.getByText('Flow Rate')).toBeVisible()
  })

  test('create asset with custom fields and filter by category', async ({ page }) => {
    await page.goto('/assets')
    await expect(page.getByRole('heading', { name: /asset register/i })).toBeVisible()

    await page.getByRole('button', { name: /add asset/i }).click()
    await page.getByRole('combobox', { name: 'Client', exact: true }).selectOption('client-1')
    await page.getByRole('combobox', { name: 'Site', exact: true }).selectOption('site-1')
    await page.getByTestId('asset-category-select').selectOption('cat-generator')
    await page.getByRole('textbox', { name: 'Asset name', exact: true }).fill('Generator 02')
    await page.getByRole('textbox', { name: 'Asset code', exact: true }).fill('GEN-002')
    await page.getByRole('textbox', { name: 'Asset type', exact: true }).fill('Generator')
    await page.getByRole('textbox', { name: 'Location', exact: true }).fill('Engine room')
    await expect(page.getByTestId('asset-custom-fields-panel')).toBeVisible()
    await page.getByRole('spinbutton', { name: /capacity kva \* unit: kva/i }).fill('500')
    await page.getByRole('combobox', { name: 'Fuel Type', exact: true }).selectOption('Diesel')
    await page.getByRole('button', { name: /save asset/i }).click()

    await expect(page.getByRole('table').getByText('Generator 02')).toBeVisible()
    await page.getByTestId('asset-category-filter').selectOption('cat-generator')
    await expect(page.getByRole('table').getByText('Generator 02')).toBeVisible()
    await expect(page.getByRole('table').getByText('Chiller 01')).not.toBeVisible()
  })

  test('asset documents panel still works from asset actions', async ({ page }) => {
    await page.goto('/assets')
    await expect(page.getByRole('heading', { name: /asset register/i })).toBeVisible()

    const generatorRow = page.getByRole('row', { name: /generator 01/i })
    await generatorRow.getByTestId('asset-action-select-asset-1').selectOption('documents')
    await generatorRow.getByTestId('asset-action-apply-asset-1').click()
    await expect(page.getByTestId('attachment-panel')).toBeVisible()
    await expect(page.getByText('generator-manual.pdf')).toBeVisible()
  })
})
