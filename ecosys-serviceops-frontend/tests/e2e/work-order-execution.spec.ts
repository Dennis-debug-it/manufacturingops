import { expect, test } from '@playwright/test'
import { assertNoHorizontalOverflow } from './helpers/layout'
import { makeScenario, mockOfficeWorkOrder } from './helpers/workOrderMocks'

test.describe('work order execution vertical slice', () => {
  test('opens a work order from the list into the four-tab workspace', async ({ page }) => {
    const state = makeScenario()
    await mockOfficeWorkOrder(page, state)

    await page.goto('/work-orders')
    await page.getByRole('link', { name: state.workOrder.workOrderNumber }).first().click()
    await expect(page).toHaveURL(new RegExp(`/work-orders/${state.workOrder.id}$`))
    await expect(page.getByRole('heading', { name: state.workOrder.workOrderNumber })).toBeVisible()

    for (const tab of ['Overview', 'Work', 'Activity', 'Report']) {
      await expect(page.getByRole('button', { name: tab, exact: true })).toBeVisible()
    }
    // Overview carries the site and asset details a dispatcher needs.
    await expect(page.getByRole('link', { name: /\+254 722 333 444/ })).toHaveAttribute('href', 'tel:+254722333444')
    await expect(page.getByText('Report to the gatehouse; PPE required.')).toBeVisible()
    await expect(page.getByText('CAT-99812')).toBeVisible()
    await expect(page.getByText('01 Jun 2026')).toBeVisible()
    await assertNoHorizontalOverflow(page)
  })

  test('assigns a team crew with a lead from the unassigned state', async ({ page }) => {
    const state = makeScenario({
      status: 'Open', isUnassigned: true, assignmentType: 'Unassigned', assignmentGroupId: null, assignmentGroupName: null,
      assignedTechnicianId: null, assignedTechnicianName: null, assignedTechnicianIds: [], leadTechnicianId: null,
      leadTechnicianName: null, technicianAssignments: [], assignmentSummary: 'Unassigned', arrivalAt: null,
    })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'Assign', exact: true }).click()
    const technicianList = page.getByRole('group', { name: 'Technicians' })
    await expect(technicianList.getByRole('checkbox')).toHaveCount(3)

    // Picking a team narrows the list to its members.
    await page.getByLabel('Team').selectOption('group-1')
    await expect(technicianList.getByRole('checkbox')).toHaveCount(2)
    await technicianList.getByLabel('Moses Otieno').check()
    await technicianList.getByLabel('Grace Wanjiku').check()
    await page.getByLabel('Lead technician').selectOption({ label: 'Grace Wanjiku' })
    // The Overview card also has a Reassign button; submit the drawer's.
    await page.getByRole('button', { name: 'Reassign', exact: true }).last().click()

    await expect(page.getByText('Work order reassigned')).toBeVisible()
    expect(state.requests).toContainEqual({
      method: 'POST',
      path: '/reassign',
      body: { assignmentGroupId: 'group-1', technicianIds: ['tech-1', 'tech-2'], leadTechnicianId: 'tech-2', notes: null },
    })
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toBeVisible()
  })

  test('work tab shows readiness and saves the work performed', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}?tab=work`)

    const readiness = page.getByLabel('Completion readiness')
    await expect(readiness.getByRole('button', { name: 'Technician on site' })).toBeVisible()
    await expect(readiness.getByRole('button', { name: 'Work performed' })).toBeVisible()

    // With arrival recorded, the next unfinished step (work performed) is already expanded.
    await page.getByPlaceholder('Describe the work performed…').fill('Reterminated L2 and replaced the transfer contactor.')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await expect(page.getByText('Work performed notes saved')).toBeVisible()
    expect(state.requests).toContainEqual({ method: 'POST', path: '/execution-notes', body: { workDone: 'Reterminated L2 and replaced the transfer contactor.' } })
  })

  test('cancelling the hold drawer closes only the drawer', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)
    const detailUrl = page.url()

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'More actions' }).click()
    await page.getByRole('menuitem', { name: 'Put On Hold' }).click()
    await page.getByLabel('Reason').fill('Waiting for the facilities lead to confirm the shutdown window.')
    await page.getByRole('button', { name: 'Cancel', exact: true }).click()

    await expect(page.getByText('Put Work Order On Hold')).toHaveCount(0)
    await expect(page).toHaveURL(detailUrl)
    expect(state.requests.filter((request) => request.path === '/hold')).toHaveLength(0)
  })

  test('activity posts a client-visible note into the single feed', async ({ page }) => {
    const state = makeScenario()
    state.events.push({
      id: 'e1', eventType: 'NoteAdded', status: 'Assigned', message: 'Added an internal note.', actorUserId: null, actorUserName: 'Ops Admin',
      oldStatus: null, newStatus: null, reason: null, note: null, latitude: null, longitude: null, occurredAt: '2026-05-13T08:10:00.000Z', metadataJson: null,
    })
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}?tab=activity`)

    // The NoteAdded event duplicates the note itself, so it is not shown.
    await expect(page.getByText('Added an internal note.')).toHaveCount(0)
    await page.getByPlaceholder('Post an update or note…').fill('Client informed; part on order.')
    await page.getByRole('radio', { name: 'Client-visible' }).click()
    await page.getByRole('button', { name: 'Post', exact: true }).click()

    await expect(page.getByText('Client-visible note posted')).toBeVisible()
    expect(state.requests).toContainEqual({ method: 'POST', path: '/notes', body: { content: 'Client informed; part on order.', isClientVisible: true } })
    await expect(page.getByText('Client informed; part on order.')).toBeVisible()
  })

  test('completing prefills the saved work performed and completes the job', async ({ page }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    state.execution.notes.workDone = 'Replaced the contactor and verified load transfer.'
    await mockOfficeWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'Complete', exact: true }).click()
    await expect(page.getByLabel(/work done notes/i)).toHaveValue('Replaced the contactor and verified load transfer.')
    await page.getByRole('button', { name: 'Mark complete' }).click()

    await expect(page.getByText('Work order completed')).toBeVisible()
    expect(state.requests).toContainEqual({
      method: 'POST',
      path: '/complete',
      body: { workDoneNotes: 'Replaced the contactor and verified load transfer.', adminOverride: false, overrideReason: null },
    })
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Close', exact: true })).toBeVisible()
  })
})
