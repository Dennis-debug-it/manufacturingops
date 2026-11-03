import { expect, test } from '@playwright/test'
import { CORS_HEADERS, json } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'
import { makeScenario, makeWorkOrder, mockOfficeWorkOrder, mockTechnicianWorkOrder } from './helpers/workOrderMocks'

const API = E2E_API_URL
const finished = { workStartedAt: '2026-05-13T08:45:00.000Z', completedAt: '2026-05-13T10:00:00.000Z', workDoneNotes: 'Reset the ATS; the AVR needs replacing.' }

test.describe('follow-ups, reopen and closing with sub-tasks', () => {
  test('a completed job flagged for follow-up creates a linked follow-up prefilled from it', async ({ page }) => {
    const state = makeScenario({ status: 'Completed', ...finished, followUpRequested: true, followUps: [] })
    await mockOfficeWorkOrder(page, state)
    let created: Record<string, unknown> | null = null
    await page.route(`${API}/api/workorders`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      if (route.request().method() !== 'POST') return route.fulfill(json([state.workOrder]))
      created = route.request().postDataJSON() as Record<string, unknown>
      return route.fulfill(json(makeWorkOrder({ id: 'wo-2', workOrderNumber: 'WO-2026-0050', status: 'New', followUpOfWorkOrderId: 'wo-1', followUpOfWorkOrderNumber: 'WO-2026-0042' }), 201))
    })
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const banner = page.getByTestId('follow-up-requested')
    await expect(banner).toContainText('marked a follow-up visit as required')
    await banner.getByRole('button', { name: 'Create follow-up' }).click()

    await expect(page.getByText('Follow-up of WO-2026-0042')).toBeVisible()
    await expect(page.getByLabel('Title')).toHaveValue('Follow-up: Generator transfer fault')
    await expect(page.getByLabel('Description')).toHaveValue(/Work done last visit: Reset the ATS; the AVR needs replacing\./)
    await page.getByRole('button', { name: 'Create Follow-up', exact: true }).click()

    await expect(page.getByText('Follow-up WO-2026-0050 created')).toBeVisible()
    expect(created).toMatchObject({ clientId: 'client-1', siteId: 'site-1', assetId: 'asset-1', followUpOfWorkOrderId: 'wo-1', priority: 'High' })
    await expect(page).toHaveURL(/\/work-orders\/wo-2$/)
  })

  test('the follow-ups panel links both ways', async ({ page }) => {
    const state = makeScenario({
      status: 'Closed', ...finished,
      followUpOfWorkOrderId: 'wo-0', followUpOfWorkOrderNumber: 'WO-2026-0001',
      followUps: [{ id: 'wo-9', workOrderNumber: 'WO-2026-0099', status: 'Assigned', createdAt: '2026-05-14T08:00:00.000Z' }],
    })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const panel = page.getByTestId('follow-ups-panel')
    await expect(panel.getByRole('link', { name: 'WO-2026-0001' })).toHaveAttribute('href', '/work-orders/wo-0')
    await expect(panel.getByRole('link', { name: 'WO-2026-0099' })).toHaveAttribute('href', '/work-orders/wo-9')
    // No banner once a follow-up exists, even if the checklist asked for one.
    await expect(page.getByTestId('follow-up-requested')).toHaveCount(0)
  })

  test('an admin can reopen a closed job', async ({ page }) => {
    const state = makeScenario({ status: 'Closed', ...finished, availableTransitions: ['Reopened'] })
    await mockOfficeWorkOrder(page, state)
    await page.route(`${API}/api/workorders/${state.workOrder.id}/status`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      state.requests.push({ method: 'PATCH', path: '/status', body: route.request().postDataJSON() })
      Object.assign(state.workOrder, { status: 'Reopened', availableTransitions: ['Assigned', 'In Progress'] })
      return route.fulfill(json(state.workOrder))
    })
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'More actions' }).click()
    await page.getByRole('menuitem', { name: 'Reopen' }).click()
    await page.getByRole('button', { name: 'Reopen', exact: true }).click()

    await expect(page.getByText('Work order reopened')).toBeVisible()
    expect(state.requests).toContainEqual({ method: 'PATCH', path: '/status', body: { status: 'Reopened' } })
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toBeVisible()
  })

  test('closing waits for open sub-tasks', async ({ page }) => {
    const state = makeScenario({ status: 'Completed', ...finished, childWorkOrderCount: 1 })
    await mockOfficeWorkOrder(page, state)
    await page.route(`${API}/api/workorders/${state.workOrder.id}/children`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json([makeWorkOrder({ id: 'wo-sub', workOrderNumber: 'WO-SUB-1', status: 'Assigned', parentWorkOrderId: state.workOrder.id })]))
    })
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await expect(page.getByRole('link', { name: 'WO-SUB-1' })).toBeVisible()
    await page.getByTestId('workflow-actions').getByRole('button', { name: 'Close', exact: true }).click()
    await expect(page.getByText('Complete or cancel the open sub-task first: WO-SUB-1')).toBeVisible()
    await expect(page.getByText('Close Work Order', { exact: true })).toHaveCount(0)
    expect(state.requests.filter((request) => request.path === '/close')).toHaveLength(0)
  })

  test('the technician on a follow-up sees the previous visit', async ({ page }) => {
    const state = makeScenario({ status: 'Assigned', followUpOfWorkOrderId: 'wo-0', followUpOfWorkOrderNumber: 'WO-2026-0001' })
    await mockTechnicianWorkOrder(page, state)
    await page.route(`${API}/api/workorders/${state.workOrder.id}/follow-up-source/service-report`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json({
        companyName: 'Acme', workOrderNumber: 'WO-2026-0001', title: 'Generator transfer fault', clientName: 'Acme Manufacturing',
        siteLabel: null, assetName: null, assetDetails: null, technicianTeam: 'Grace Wanjiku', templateName: null, templateVersion: null,
        reportedProblem: null, findings: 'AVR output unstable.', workDone: 'Reset the ATS; the AVR needs replacing.', generatedAtLabel: '',
        timestamps: [], materials: [{ name: 'Fuse 10A', quantityUsed: 2, unitOfMeasure: 'pcs', unitCost: null, chargeable: true, notes: null }],
        checklistSections: [{ sectionName: 'Checks', items: [
          { question: 'AVR voltage stable', inputType: 'passfail', responseValue: 'Fail', unit: null, remarks: 'Drifts under load', isRequired: true, isCompleted: true, evidenceRequired: false },
          { question: 'Area left clean', inputType: 'passfail', responseValue: 'Pass', unit: null, remarks: null, isRequired: false, isCompleted: true, evidenceRequired: false },
        ] }],
        photoGroups: [], signatures: [], showPoweredByEcosys: false,
      }))
    })
    await page.setViewportSize({ width: 412, height: 915 })
    await page.goto('/tech')
    await expect(page.getByText('Follow-up of WO-2026-0001')).toBeVisible()

    await page.goto(`/tech/jobs/${state.workOrder.id}`)
    const previous = page.getByTestId('tech-previous-visit')
    await expect(previous).toContainText('Previous visit · WO-2026-0001')
    await expect(previous).toContainText('Reset the ATS; the AVR needs replacing.')
    await expect(previous).toContainText('AVR voltage stable: Fail (Drifts under load)')
    await expect(previous).not.toContainText('Area left clean')
    await expect(previous).toContainText('Fuse 10A × 2')
  })
})
