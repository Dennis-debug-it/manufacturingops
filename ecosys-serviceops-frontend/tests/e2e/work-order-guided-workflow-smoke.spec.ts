import { expect, test } from '@playwright/test'
import { makeScenario, mockOfficeWorkOrder, mockTechnicianWorkOrder } from './helpers/workOrderMocks'

/**
 * The work order page shows one main action that follows the job (Assign → Start → Complete →
 * Close, or Resume when on hold); everything else lives in the "More actions" menu.
 */
test.describe('work-order workflow actions', () => {
  test('assigned work order offers Start Work as the main action', async ({ page }) => {
    const state = makeScenario({ status: 'Assigned' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Start Work' })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Complete', exact: true })).toHaveCount(0)
    await actions.getByRole('button', { name: 'More actions' }).click()
    await expect(page.getByRole('menuitem', { name: 'Put On Hold' })).toBeVisible()
    await expect(page.getByRole('menuitem', { name: 'Cancel Work Order' })).toBeVisible()
  })

  test('unassigned work order offers Assign, which opens the assignment drawer', async ({ page }) => {
    const state = makeScenario({
      status: 'Open', isUnassigned: true, assignmentType: 'Unassigned', assignmentGroupId: null, assignmentGroupName: null,
      assignedTechnicianId: null, assignedTechnicianName: null, assignedTechnicianIds: [], leadTechnicianId: null,
      leadTechnicianName: null, technicianAssignments: [], assignmentSummary: 'Unassigned', arrivalAt: null,
    })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Assign', exact: true })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await actions.getByRole('button', { name: 'Assign', exact: true }).click()
    await expect(page.getByText('Reassign Work Order')).toBeVisible()
  })

  test('in progress work order offers Complete and keeps hold in the menu', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Complete', exact: true })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await actions.getByRole('button', { name: 'More actions' }).click()
    await expect(page.getByRole('menuitem', { name: 'Put On Hold' })).toBeVisible()
  })

  test('on hold work order offers Resume and shows the hold reason', async ({ page }) => {
    const state = makeScenario({ status: 'Waiting on Client', workStartedAt: '2026-05-13T08:45:00.000Z', holdReason: 'Waiting for client approval before restart.' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Resume' })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Complete', exact: true })).toHaveCount(0)
    await expect(page.getByText(/Waiting on Client.*Waiting for client approval before restart\./)).toBeVisible()
  })

  test('putting a work order on hold saves the type and reason', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'More actions' }).click()
    await page.getByRole('menuitem', { name: 'Put On Hold' }).click()
    await page.getByLabel('Hold Type').selectOption('WaitingOnParts')
    await page.getByLabel('Reason').fill('Waiting for a replacement breaker before resuming.')
    await page.getByRole('button', { name: 'Put On Hold', exact: true }).click()

    await expect(page.getByText('Work order put on hold')).toBeVisible()
    expect(state.requests).toContainEqual({ method: 'PATCH', path: '/hold', body: { holdType: 'WaitingOnParts', reason: 'Waiting for a replacement breaker before resuming.' } })
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Resume' })).toBeVisible()
    await expect(page.getByText(/Waiting on Parts.*replacement breaker/)).toBeVisible()
  })

  test('completed work order offers Close to an admin and nothing else in the flow', async ({ page }) => {
    const state = makeScenario({ status: 'Completed', workStartedAt: '2026-05-13T08:45:00.000Z', completedAt: '2026-05-13T10:00:00.000Z' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Close', exact: true })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await expect(actions.getByRole('button', { name: 'Complete', exact: true })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Report', exact: true })).toBeVisible()
  })

  test('technician bottom bar follows the same flow', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockTechnicianWorkOrder(page, state)
    await page.setViewportSize({ width: 412, height: 915 })
    await page.goto(`/tech/jobs/${state.workOrder.id}`)

    const bar = page.getByTestId('tech-bottom-bar')
    await expect(bar.getByRole('button', { name: 'Complete', exact: true })).toBeVisible()
    await page.getByLabel('Open more actions').click()
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toBeVisible()
  })
})
