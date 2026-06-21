import { expect, test, type Page, type Route } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const API = 'http://localhost:5072'

test.describe('core reports', () => {
  test('reports page loads, filters data, and exports csv', async ({ page }) => {
    const state = createState()

    await registerApiCatchAll(page)
    await loginWithMockSession(page, {
      role: 'tenantAdmin',
      email: 'admin@acme.test',
      fullName: 'Alice Admin',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await mockReportApis(page, state)

    await page.goto('/reports')
    await expect(page.getByRole('heading', { name: 'Reports', exact: true })).toBeVisible()
    await expect(page.getByTestId('reports-filters')).toBeVisible()
    await expect(page.getByText('4', { exact: true }).first()).toBeVisible()

    await page.getByLabel('Client').selectOption('client-1')
    await page.getByLabel('Status').selectOption('Completed')
    await page.getByLabel('Priority').selectOption('High')
    await expect(page.getByText('1', { exact: true }).first()).toBeVisible()

    const workOrderExport = page.waitForResponse((response) =>
      response.url().includes('/api/reports/work-order-performance/export') && response.status() === 200,
    )
    await page.getByRole('button', { name: /export csv/i }).click()
    const workOrderExportResponse = await workOrderExport
    expect(workOrderExportResponse.url()).toContain('clientId=client-1')
    expect(workOrderExportResponse.url()).toContain('status=Completed')
    expect(workOrderExportResponse.url()).toContain('priority=High')

    await page.getByTestId('reports-tab-asset-reliability').click()
    await page.getByLabel('Client').selectOption('client-1')
    await page.getByLabel('Site').selectOption('site-1')
    await page.getByLabel('Category').selectOption('category-1')
    await expect(page.getByRole('table').getByText('Generator 01')).toBeVisible()
    await expect(page.getByRole('table').getByText('Nairobi HQ')).toBeVisible()

    const assetExport = page.waitForResponse((response) =>
      response.url().includes('/api/reports/asset-reliability/export') && response.status() === 200,
    )
    await page.getByRole('button', { name: /export csv/i }).click()
    const assetExportResponse = await assetExport
    expect(assetExportResponse.url()).toContain('siteId=site-1')
    expect(assetExportResponse.url()).toContain('categoryId=category-1')

    await page.getByTestId('reports-tab-pm-compliance').click()
    await expect(page.getByText(/overdue plans/i)).toBeVisible()
    await expect(page.getByRole('table').getByText('Generator 01')).toBeVisible()
  })
})

function createState() {
  return {
    clients: [
      { id: 'client-1', clientName: 'Acme Facilities', isActive: true, createdAt: '2026-05-16T09:00:00.000Z' },
      { id: 'client-2', clientName: 'Beta Towers', isActive: true, createdAt: '2026-05-16T09:00:00.000Z' },
    ],
    sites: [
      { id: 'site-1', clientId: 'client-1', tenantId: 'tenant-1', siteCode: 'SITE-1', siteName: 'Nairobi HQ', siteType: 'Office', status: 'Active', createdAt: '2026-05-16T09:00:00.000Z' },
      { id: 'site-2', clientId: 'client-2', tenantId: 'tenant-1', siteCode: 'SITE-2', siteName: 'Mombasa Depot', siteType: 'Depot', status: 'Active', createdAt: '2026-05-16T09:00:00.000Z' },
    ],
    categories: [
      { id: 'category-1', tenantId: 'tenant-1', name: 'Generator', isDefault: true, isActive: true, displayOrder: 1, fields: [] },
      { id: 'category-2', tenantId: 'tenant-1', name: 'UPS', isDefault: false, isActive: true, displayOrder: 2, fields: [] },
    ],
    workOrders: [
      { number: 'WO-REP-001', clientId: 'client-1', status: 'Completed', priority: 'High', created: '2026-05-10', completed: '2026-05-11' },
      { number: 'WO-REP-002', clientId: 'client-1', status: 'Completed', priority: 'Medium', created: '2026-05-12', completed: '2026-05-13' },
      { number: 'WO-REP-003', clientId: 'client-1', status: 'Open', priority: 'High', created: '2026-05-14', completed: null },
      { number: 'WO-REP-004', clientId: 'client-2', status: 'Cancelled', priority: 'Low', created: '2026-05-15', completed: null },
    ],
    technicianRows: [
      { TechnicianId: 'tech-1', Name: 'Moses Otieno', TotalJobs: 2, Completed: 2, OnTimeRate: 100, AvgTimeOnSiteHours: 3.5, PmJobs: 1, CorrectiveJobs: 1 },
      { TechnicianId: 'tech-2', Name: 'James Kariuki', TotalJobs: 1, Completed: 0, OnTimeRate: 0, AvgTimeOnSiteHours: 0, PmJobs: 0, CorrectiveJobs: 1 },
    ],
    assets: [
      { Id: 'asset-1', AssetName: 'Generator 01', AssetCode: 'GEN-01', ClientName: 'Acme Facilities', SiteName: 'Nairobi HQ', CorrectiveWos: 3, PmCompliance: 50, WarrantyExpiryDate: '2026-06-30T00:00:00Z', IsRecurringFault: true, ClientId: 'client-1', SiteId: 'site-1', CategoryId: 'category-1' },
      { Id: 'asset-2', AssetName: 'UPS 01', AssetCode: 'UPS-01', ClientName: 'Beta Towers', SiteName: 'Mombasa Depot', CorrectiveWos: 1, PmCompliance: 100, WarrantyExpiryDate: null, IsRecurringFault: false, ClientId: 'client-2', SiteId: 'site-2', CategoryId: 'category-2' },
    ],
    overduePlans: [
      { Id: 'plan-1', AssetName: 'Generator 01', AssetCode: 'GEN-01', ClientName: 'Acme Facilities', NextPmDate: '2026-05-14T00:00:00Z', DaysOverdue: 2, ClientId: 'client-1', SiteId: 'site-1', CategoryId: 'category-1' },
    ],
    lastExports: {
      workOrder: '',
      technician: '',
      asset: '',
      pm: '',
    },
  }
}

async function mockReportApis(page: Page, state: ReturnType<typeof createState>) {
  await page.route(`${API}/api/clients**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json(state.clients))
  })

  await page.route(`${API}/api/sites/search**`, async (route) => {
    if (handleOptions(route)) return
    const url = new URL(route.request().url())
    const clientId = url.searchParams.get('clientId')
    const rows = clientId ? state.sites.filter((site) => site.clientId === clientId) : state.sites
    await route.fulfill(json(rows))
  })

  await page.route(`${API}/api/asset-categories**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json(state.categories))
  })

  await page.route(`${API}/api/reports/work-order-performance/export**`, async (route) => {
    if (handleOptions(route)) return
    state.lastExports.workOrder = route.request().url()
    await route.fulfill(csv('WO Number,Title,Client\nWO-REP-001,Generator fault A,Acme Facilities\n', 'work-order-performance.csv'))
  })

  await page.route(`${API}/api/reports/work-order-performance**`, async (route) => {
    if (handleOptions(route)) return
    const url = new URL(route.request().url())
    const clientId = url.searchParams.get('clientId')
    const status = url.searchParams.get('status')
    const priority = url.searchParams.get('priority')
    const rows = state.workOrders.filter((item) => {
      const matchesClient = !clientId || item.clientId === clientId
      const matchesStatus = !status || item.status === status
      const matchesPriority = !priority || item.priority === priority
      return matchesClient && matchesStatus && matchesPriority
    })
    const completed = rows.filter((item) => item.status === 'Completed').length
    const completedOnTime = completed
    const overdue = rows.filter((item) => item.status === 'Open').length
    await route.fulfill(json({
      Total: rows.length,
      Completed: completed,
      CompletedOnTime: completedOnTime,
      Overdue: overdue,
      OnTimeRate: completed ? 100 : 0,
      AvgCompletionHours: completed ? 4.5 : 0,
      ByStatus: summarizeBy(rows, 'status', 'Status'),
      ByPriority: summarizeBy(rows, 'priority', 'Priority'),
      ByDay: rows.map((row) => ({ Date: row.created, Count: 1 })),
    }))
  })

  await page.route(`${API}/api/reports/technician-productivity/export**`, async (route) => {
    if (handleOptions(route)) return
    state.lastExports.technician = route.request().url()
    await route.fulfill(csv('Technician,Total Jobs\nMoses Otieno,2\n', 'technician-productivity.csv'))
  })

  await page.route(`${API}/api/reports/technician-productivity**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json({ Technicians: state.technicianRows }))
  })

  await page.route(`${API}/api/reports/asset-reliability/export**`, async (route) => {
    if (handleOptions(route)) return
    state.lastExports.asset = route.request().url()
    await route.fulfill(csv('Asset Code,Asset Name\nGEN-01,Generator 01\n', 'asset-reliability.csv'))
  })

  await page.route(`${API}/api/reports/asset-reliability**`, async (route) => {
    if (handleOptions(route)) return
    const url = new URL(route.request().url())
    const clientId = url.searchParams.get('clientId')
    const siteId = url.searchParams.get('siteId')
    const categoryId = url.searchParams.get('categoryId')
    const rows = state.assets.filter((item) => {
      const matchesClient = !clientId || item.ClientId === clientId
      const matchesSite = !siteId || item.SiteId === siteId
      const matchesCategory = !categoryId || item.CategoryId === categoryId
      return matchesClient && matchesSite && matchesCategory
    })
    await route.fulfill(json({
      Assets: rows,
      RecurringFaultCount: rows.filter((item) => item.IsRecurringFault).length,
    }))
  })

  await page.route(`${API}/api/reports/pm-compliance/export**`, async (route) => {
    if (handleOptions(route)) return
    state.lastExports.pm = route.request().url()
    await route.fulfill(csv('Asset,Asset Code\nGenerator 01,GEN-01\n', 'pm-compliance.csv'))
  })

  await page.route(`${API}/api/reports/pm-compliance**`, async (route) => {
    if (handleOptions(route)) return
    const url = new URL(route.request().url())
    const clientId = url.searchParams.get('clientId')
    const siteId = url.searchParams.get('siteId')
    const categoryId = url.searchParams.get('categoryId')
    const rows = state.overduePlans.filter((item) => {
      const matchesClient = !clientId || item.ClientId === clientId
      const matchesSite = !siteId || item.SiteId === siteId
      const matchesCategory = !categoryId || item.CategoryId === categoryId
      return matchesClient && matchesSite && matchesCategory
    })
    await route.fulfill(json({
      ActivePlans: 2,
      DueInPeriod: 2,
      CompletedOnTime: 1,
      Overdue: rows.length,
      ComplianceRate: 50,
      ByClient: [],
      OverduePlans: rows,
    }))
  })
}

function summarizeBy<T extends Record<string, string>>(rows: T[], key: keyof T, label: string) {
  const map = new Map<string, number>()
  for (const row of rows) {
    const value = row[key]
    map.set(value, (map.get(value) ?? 0) + 1)
  }

  return [...map.entries()].map(([value, count]) => ({ [label]: value, Count: count }))
}

function handleOptions(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    void route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }

  return false
}

function csv(body: string, fileName: string) {
  return {
    status: 200,
    body,
    contentType: 'text/csv',
    headers: {
      ...CORS_HEADERS,
      'content-disposition': `attachment; filename="${fileName}"`,
    },
  }
}
