import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'
import { makeScenario, mockOfficeWorkOrder } from './helpers/workOrderMocks'

const report = (findings: string, workDone: string) => ({
  companyName: 'Acme FM', workOrderNumber: 'WO-000078', title: 'AHU-03 tripping', clientName: 'Nairobi Hospital', siteLabel: 'Main Campus',
  assetName: 'AHU-03', assetDetails: null, technicianTeam: 'Moses Otieno', templateName: null, templateVersion: null,
  reportedProblem: 'AHU tripping', findings, workDone, generatedAtLabel: '10 Oct 2026 08:00 UTC',
  timestamps: [], checklistSections: [], materials: [], photoGroups: [], signatures: [], showPoweredByEcosys: true,
})

test('office staff edit the report wording without touching the technician notes', async ({ page }) => {
  const state = makeScenario({ status: 'Completed', completedAt: '2026-05-13T10:00:00.000Z' })
  await mockOfficeWorkOrder(page, state)
  const base = `${E2E_API_URL}/api/workorders/${state.workOrder.id}`
  let content = {
    reportStatus: 'Approved', lockedReason: null,
    reportedProblem: { original: 'AHU tripping', edited: null },
    findings: { original: 'filters v dirty', edited: null },
    workDone: { original: 'changed filters', edited: null },
    editedAt: null, editedByName: null,
  }
  let saved: Record<string, unknown> | null = null
  let preview = report('filters v dirty', 'changed filters')
  await page.route(`${base}/service-report`, (route) => route.request().method() === 'OPTIONS' ? route.fulfill({ status: 204, headers: CORS_HEADERS }) : route.fulfill(json(preview)))
  await page.route(`${base}/report/content`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON() as Record<string, unknown>
      content = {
        ...content, reportStatus: 'Draft', editedAt: '2026-10-10T09:00:00Z', editedByName: 'Ops Admin',
        findings: { ...content.findings, edited: saved.findings as string },
      }
      preview = report(saved.findings as string, 'changed filters')
    }
    return route.fulfill(json(content))
  })

  await page.goto(`/work-orders/${state.workOrder.id}`)
  await page.getByRole('button', { name: 'Report', exact: true }).click()
  await page.getByRole('button', { name: 'Edit report' }).click()
  await expect(page.getByText('Saving changes sends it back for approval')).toBeVisible()
  await expect(page.getByLabel('Findings')).toHaveValue('filters v dirty')

  await page.getByLabel('Findings').fill('Supply air filters were heavily loaded, causing the high static pressure trip.')
  await expect(page.getByText('From the technician\'s notes:')).toContainText('filters v dirty')
  await page.getByRole('button', { name: 'Save report' }).click()

  await expect(page.getByText('Report updated — it needs approving again')).toBeVisible()
  expect(saved).toEqual({
    reportedProblem: 'AHU tripping',
    findings: 'Supply air filters were heavily loaded, causing the high static pressure trip.',
    workDone: 'changed filters',
  })
  await expect(page.getByText('Wording edited by Ops Admin')).toBeVisible()
  await page.getByRole('button', { name: /^Findings & Work Done/ }).click()
  await expect(page.getByText('Supply air filters were heavily loaded')).toBeVisible()
})

test('a report already sent to the client cannot be edited', async ({ page }) => {
  const state = makeScenario({ status: 'Completed', completedAt: '2026-05-13T10:00:00.000Z' })
  await mockOfficeWorkOrder(page, state)
  const base = `${E2E_API_URL}/api/workorders/${state.workOrder.id}`
  await page.route(`${base}/service-report`, (route) => route.request().method() === 'OPTIONS' ? route.fulfill({ status: 204, headers: CORS_HEADERS }) : route.fulfill(json(report('x', 'y'))))
  await page.route(`${base}/report/content`, (route) => route.request().method() === 'OPTIONS' ? route.fulfill({ status: 204, headers: CORS_HEADERS }) : route.fulfill(json({
    reportStatus: 'Sent', lockedReason: 'This report has already been sent to the client and can no longer be edited.',
    reportedProblem: { original: 'a', edited: null }, findings: { original: 'x', edited: null }, workDone: { original: 'y', edited: null },
    editedAt: null, editedByName: null,
  })))

  await page.goto(`/work-orders/${state.workOrder.id}`)
  await page.getByRole('button', { name: 'Report', exact: true }).click()
  await expect(page.getByText('already been sent to the client')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Edit report' })).toHaveCount(0)
})
