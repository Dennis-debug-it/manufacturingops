import { expect, test, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

test.describe('work-order workflow actions', () => {
  test('assigned or open work order shows Start Work only', async ({ page }) => {
    const state = createTenantState({
      status: 'Open',
      workStartedAt: null,
      completedAt: null,
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Start Work' })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(actions.getByRole('button', { name: 'Complete' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Continue' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Follow Up' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Escalate' })).toHaveCount(0)
  })

  test('unassigned open work order shows Assign / Schedule only', async ({ page }) => {
    const state = createTenantState({
      status: 'Open',
      assignmentGroupId: null,
      assignmentGroupName: null,
      assignmentType: 'Unassigned',
      assignedTechnicianId: null,
      assignedTechnicianName: null,
      assignedTechnicianIds: [],
      leadTechnicianId: null,
      leadTechnicianName: null,
      technicianAssignments: [],
      assignmentSummary: 'Unassigned',
      isUnassigned: true,
      workStartedAt: null,
      completedAt: null,
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Assign / Schedule' })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await expect(actions.getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(actions.getByRole('button', { name: 'Complete' })).toHaveCount(0)
  })

  test('in progress work order shows Complete as the primary action and keeps hold in the overflow menu', async ({ page }) => {
    const state = createTenantState({
      status: 'In Progress',
      workStartedAt: '2026-05-13T08:45:00.000Z',
      completedAt: null,
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Complete' })).toBeVisible()
    await expect(page.getByLabel('Open more actions').first()).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await page.getByLabel('Open more actions').first().click()
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Continue' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Follow Up' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Escalate' })).toHaveCount(0)
  })

  test('on hold work order shows Resume only and displays the hold reason', async ({ page }) => {
    const state = createTenantState({
      status: 'On Hold',
      workStartedAt: '2026-05-13T08:45:00.000Z',
      holdReason: 'Awaiting Client',
      holdNote: 'Waiting for client approval before restart.',
      completedAt: null,
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    const actions = page.getByTestId('workflow-actions')
    await expect(actions.getByRole('button', { name: 'Resume Work' })).toBeVisible()
    await expect(actions.getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(actions.getByRole('button', { name: 'Complete' })).toHaveCount(0)
    await expect(page.getByTestId('work-order-summary-card').getByTestId('hold-reason-summary')).toBeVisible()
    await expect(page.getByTestId('work-order-summary-card').getByTestId('hold-reason-value')).toHaveText('Awaiting Client')
  })

  test('placing a work order on hold requires a reason and saves the note', async ({ page }) => {
    const state = createTenantState({
      status: 'In Progress',
      workStartedAt: '2026-05-13T08:45:00.000Z',
      completedAt: null,
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await page.getByLabel('Open more actions').first().click()
    await page.getByRole('button', { name: 'Put On Hold' }).click()
    const holdDialog = page.locator('[data-ui-overlay="true"]')
    await expect(holdDialog.getByText('Put Work Order On Hold')).toBeVisible()
    await expect(holdDialog.getByLabel('Reason')).toContainText('Awaiting Client')
    await expect(holdDialog.getByLabel('Reason')).toContainText('Awaiting Materials')

    await holdDialog.getByLabel('Reason').selectOption('Awaiting Materials')
    await holdDialog.getByLabel('Note').fill('Waiting for a replacement breaker before resuming.')
    await holdDialog.getByRole('button', { name: /^Put On Hold$/ }).click()

    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Resume Work' })).toBeVisible()
    await expect(page.getByTestId('work-order-summary-card').getByTestId('hold-reason-summary')).toBeVisible()
    await expect(page.getByTestId('work-order-summary-card').getByTestId('hold-reason-value')).toHaveText('Awaiting Materials')
    await expect(page.getByTestId('work-order-summary-card').getByTestId('hold-reason-note')).toHaveText('Waiting for a replacement breaker before resuming.')
  })

  test('completed work order shows View Service Report only', async ({ page }) => {
    const state = createTenantState({
      status: 'Completed',
      workStartedAt: '2026-05-13T08:45:00.000Z',
      completedAt: '2026-05-13T10:00:00.000Z',
    })

    await mockTenantWorkOrder(page, state)
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'View Service Report' })).toBeVisible()
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Complete' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Continue' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Follow Up' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Escalate' })).toHaveCount(0)
  })

  test('technician sticky action bar follows the same simplified workflow', async ({ page }) => {
    const state = createTenantState({
      status: 'In Progress',
      workStartedAt: '2026-05-13T08:45:00.000Z',
      completedAt: null,
    })

    await mockTechnicianWorkOrder(page, state)
    await page.setViewportSize({ width: 412, height: 915 })
    await page.goto(`/tech/jobs/${state.workOrder.id}`)

    const bar = page.getByTestId('workflow-action-bar')
    await expect(page.getByTestId('tech-bottom-bar')).toBeVisible()
    await expect(bar.getByRole('button', { name: 'Complete' })).toBeVisible()
    await page.getByLabel('Open more actions').first().click()
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toBeVisible()
    await expect(bar.getByRole('button', { name: 'Continue' })).toHaveCount(0)
  })
})

function createTenantState(overrides?: Partial<typeof baseWorkOrder>) {
  const workOrder = {
    ...baseWorkOrder,
    ...overrides,
  }

  return {
    workOrder,
    execution: {
      notes: {
        findings: '',
        workDone: workOrder.workDoneNotes || '',
      },
      photos: [],
      materialUsages: [],
      signatures: [],
      progressUpdates: [],
      reportPreview: {
        companyName: 'Acme Manufacturing',
        workOrderNumber: workOrder.workOrderNumber,
        title: workOrder.title,
        clientName: workOrder.clientName,
        siteLabel: workOrder.siteName,
        assetName: workOrder.assetName,
        assetDetails: 'GEN-02 | Generator | Plant 1',
        technicianTeam: workOrder.leadTechnicianName,
        reportedProblem: workOrder.description,
        findings: '',
        workDone: workOrder.workDoneNotes || '',
        generatedAtLabel: '2026-05-13 10:30 UTC',
        timestamps: [],
        materials: [],
        photoGroups: [],
        signatures: [],
        showPoweredByEcosys: true,
      },
    },
    materials: [
      {
        id: 'material-1',
        branchId: 'branch-1',
        branchName: 'Nairobi Service Hub',
        itemCode: 'CT-25A',
        itemName: '25A Contactor',
        category: 'Electrical',
        unitOfMeasure: 'pcs',
        quantityOnHand: 12,
        reorderLevel: 2,
        unitCost: 45,
        isActive: true,
        isLowStock: false,
        createdAt: '2026-05-10T09:00:00.000Z',
      },
    ],
  }
}

const baseWorkOrder = {
  id: 'guided-work-order-1',
  branchId: 'branch-1',
  branchName: 'Nairobi Service Hub',
  clientId: 'client-1',
  clientName: 'Acme Manufacturing',
  siteId: 'site-1',
  siteName: 'Plant 1',
  systemId: 'system-1',
  systemName: 'Generator System',
  assetId: 'asset-1',
  assetName: 'Generator 02',
  assignmentGroupId: 'group-1',
  assignmentGroupName: 'Generator Team',
  workOrderNumber: 'WO-2026-0042',
  title: 'Generator transfer fault',
  description: 'Generator failed to transfer load after a mains outage.',
  priority: 'High',
  status: 'Open',
  slaStatus: 'On Track',
  assignmentType: 'AssignmentGroup',
  assignedTechnicianId: 'tech-1',
  assignedTechnicianName: 'Moses Otieno',
  assignedTechnicianIds: ['tech-1'],
  leadTechnicianId: 'tech-1',
  leadTechnicianName: 'Moses Otieno',
  dueDate: '2026-05-13T12:00:00.000Z',
  createdAt: '2026-05-13T08:00:00.000Z',
  workStartedAt: null,
  arrivalAt: '2026-05-13T08:40:00.000Z',
  departureAt: null,
  completedAt: null,
  workDoneNotes: '',
  jobCardNotes: '',
    holdReason: null,
    holdNote: null,
  slaResponseDeadline: '2026-05-13T09:00:00.000Z',
  slaResolutionDeadline: '2026-05-13T16:00:00.000Z',
  slaResponseBreached: false,
  slaResolutionBreached: false,
  slaResponseBreachedAt: null,
  slaResolutionBreachedAt: null,
  acknowledgedByName: null,
  acknowledgementComments: null,
  acknowledgementDate: null,
  workType: 'CorrectiveMaintenance',
  isPreventiveMaintenance: false,
  assignmentStatus: 'AssignedToTechnician',
  assignmentNotes: 'Urgent site attendance requested.',
  technicianAssignments: [
    {
      id: 'assign-1',
      technicianId: 'tech-1',
      technicianName: 'Moses Otieno',
      isLead: true,
      status: 'Accepted',
      assignedAt: '2026-05-13T08:05:00.000Z',
      acceptedAt: '2026-05-13T08:20:00.000Z',
      arrivalAt: '2026-05-13T08:40:00.000Z',
      departureAt: null,
      notes: 'Urgent site attendance requested.',
    },
  ],
  assignmentSummary: 'Generator Team -> Moses Otieno',
  isUnassigned: false,
  pmTemplateId: null,
  pmTemplateName: null,
  preventiveMaintenancePlanId: null,
  checklistItems: [],
}

async function mockTenantWorkOrder(page: Page, state: ReturnType<typeof createTenantState>) {
  const baseApi = 'http://localhost:5072'

  await registerApiCatchAll(page)
  await loginWithMockSession(page, {
    role: 'tenantAdmin',
    email: 'ops@tenant.test',
    fullName: 'Moses Otieno',
    tenantName: 'Acme Manufacturing',
    tenantId: 'tenant-1',
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.workOrder))
  })
  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/execution`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.execution))
  })
  await page.route(`${baseApi}/api/assignment-groups`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([
      {
        id: 'group-1',
        name: state.workOrder.assignmentGroupName,
        description: 'Generator response team',
        skillArea: 'Power',
        branchId: state.workOrder.branchId,
        branchName: state.workOrder.branchName,
        isActive: true,
        members: [
          {
            id: 'group-member-1',
            assignmentGroupId: 'group-1',
            userId: 'user-1',
            technicianId: 'tech-1',
            memberName: state.workOrder.leadTechnicianName,
            email: 'ops@tenant.test',
            isLead: true,
            isActive: true,
            addedAt: '2026-05-13T08:00:00.000Z',
          },
        ],
        technicianIds: ['tech-1'],
      },
    ]))
  })
  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/events`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })
  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/pause`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { holdReason?: string | null; note?: string | null }
    state.workOrder.status = 'On Hold'
    state.workOrder.holdReason = body.holdReason || null
    state.workOrder.holdNote = body.note || null
    await route.fulfill(json(state.workOrder))
  })
  await page.route(`${baseApi}/api/materials**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.materials))
  })
}

async function mockTechnicianWorkOrder(page: Page, state: ReturnType<typeof createTenantState>) {
  const baseApi = 'http://localhost:5072'

  await registerApiCatchAll(page)
  await loginWithMockSession(page, {
    role: 'technician',
    email: 'tech@tenant.test',
    fullName: 'Moses Otieno',
    tenantName: 'Acme Manufacturing',
    tenantId: 'tenant-1',
  })

  await page.route(`${baseApi}/api/technician/jobs/${state.workOrder.id}`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.workOrder))
  })
  await page.route(`${baseApi}/api/technician/jobs`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([state.workOrder]))
  })
  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/execution`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.execution))
  })
  await page.route(`${baseApi}/api/materials**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.materials))
  })
}

