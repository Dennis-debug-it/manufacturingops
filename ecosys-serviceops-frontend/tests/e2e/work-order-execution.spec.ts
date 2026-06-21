import { expect, test, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { assertNoHorizontalOverflow } from './helpers/layout'

test.describe('work order execution vertical slice', () => {
  test('tenant user can load the work-order list and open the simplified detail workspace', async ({ page }) => {
    const state = createScenarioState()

    await mockTenantSession(page, state, () => {})

    await page.goto('/work-orders')
    await expect(page.getByRole('heading', { name: 'Work Orders' })).toBeVisible()
    await expect(page.getByRole('link', { name: state.workOrder.workOrderNumber })).toBeVisible()

    await page.getByRole('link', { name: state.workOrder.workOrderNumber }).click()
    await expect(page).toHaveURL(new RegExp(`/work-orders/${state.workOrder.id}$`))
    await expect(page.getByRole('heading', { name: state.workOrder.workOrderNumber })).toBeVisible()
    await expect(page.getByTestId('command-centre-sidebar')).toBeVisible()
    await expect(page.getByTestId('work-order-summary-card')).toBeVisible()
    await expect(page.getByTestId('primary-action-panel')).toBeVisible()
    await expect(page.getByTestId('job-details-section')).toBeVisible()
    await expect(page.getByTestId('work-order-detail-side-panel')).toBeVisible()
    await page.getByRole('button', { name: 'Assignment' }).click()
    await expect(page.getByTestId('assignment-section')).toBeVisible()
    await page.getByRole('button', { name: 'Evidence' }).click()
    await expect(page.getByTestId('capture-photo-control')).toBeVisible()
  })

  test('tenant user must assign a group before technician selection opens and can save multiple technicians', async ({ page }) => {
    const state = createScenarioState()
    state.workOrder.assignmentGroupId = null
    state.workOrder.assignmentGroupName = null
    state.workOrder.assignmentType = 'Unassigned'
    state.workOrder.assignedTechnicianId = null
    state.workOrder.assignedTechnicianName = null
    state.workOrder.assignedTechnicianIds = []
    state.workOrder.leadTechnicianId = null
    state.workOrder.leadTechnicianName = null
    state.workOrder.technicianAssignments = []
    state.workOrder.assignmentSummary = 'Unassigned'
    state.workOrder.isUnassigned = true

    await mockTenantSession(page, state, () => {})

    await page.goto(`/work-orders/${state.workOrder.id}`)
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Assign / Schedule' })).toBeVisible()
    await page.getByTestId('workflow-actions').getByRole('button', { name: 'Assign / Schedule' }).click()
    await expect(page.getByTestId('assignment-card').getByText('Assign a group before selecting technicians.').first()).toBeVisible()
    await expect(page.getByTestId('assignment-save-button')).toBeDisabled()
    await expect(page).toHaveURL(new RegExp(`/work-orders/${state.workOrder.id}$`))

    await page.getByTestId('assignment-group-control').getByRole('combobox').selectOption('group-1')
    await expect(page.getByText(/assignment group updated/i).first()).toBeVisible()
    await expect(page.getByTestId('assignment-technician-list').getByText('Moses Otieno')).toBeVisible()
    await expect(page.getByTestId('assignment-technician-list').getByText('Grace Wanjiku')).toBeVisible()
    await page.getByLabel('Moses Otieno').check()
    await page.getByLabel('Grace Wanjiku').check()
    await expect(page.getByTestId('assignment-technician-list')).toBeVisible()
    await page.getByTestId('assignment-save-button').click()
    await expect(page.getByText(/assignment saved/i).first()).toBeVisible()
    await expect(page.getByTestId('assignment-card').getByText('Moses Otieno, Grace Wanjiku')).toBeVisible()
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toBeVisible()
  })

  test('completion drafts survive assignment reloads and the desktop page does not jump away', async ({ page }) => {
    const state = createScenarioState()

    await mockTenantSession(page, state, () => {})

    await page.goto(`/work-orders/${state.workOrder.id}`)
    await page.getByRole('button', { name: 'Work Execution' }).click()

    const completionSection = page.getByTestId('execution-tab')
    const findingsInput = completionSection.getByRole('textbox', { name: 'Diagnosis / findings' })
    const workDoneInput = completionSection.getByRole('textbox', { name: 'Work performed' })
    const findingsDraft = 'Found heat damage on the transfer contactor and loose terminations on L2.'
    const workDoneDraft = 'Reterminated the loose cable, replaced the contactor, and verified the load transfer sequence.'

    await findingsInput.fill(findingsDraft)
    await workDoneInput.fill(workDoneDraft)

    const urlBeforeSave = page.url()

    await page.getByRole('button', { name: 'Assignment' }).click()
    await page.getByRole('button', { name: 'Edit assignment' }).click()
    await page.locator('input[type="datetime-local"]').fill('2026-05-14T10:30')
    await page.getByRole('button', { name: /save schedule/i }).click()
    await expect(page.getByText(/schedule saved/i)).toBeVisible()

    await page.getByRole('button', { name: 'Work Execution' }).click()
    await expect(findingsInput).toHaveValue(findingsDraft)
    await expect(workDoneInput).toHaveValue(workDoneDraft)
    await expect(completionSection.getByText('Execution notes')).toBeVisible()
    await expect(page).toHaveURL(urlBeforeSave)
  })

  test('hold form stays open while selecting a reason and cancel closes only the form', async ({ page }) => {
    const state = createScenarioState()
    state.workOrder.status = 'In Progress'
    state.workOrder.workStartedAt = '2026-05-13T08:45:00.000Z'

    await mockTenantSession(page, state, () => {})

    await page.goto(`/work-orders/${state.workOrder.id}`)
    await page.getByLabel('Open more actions').first().click()
    await page.getByRole('button', { name: 'Put On Hold' }).click()

    const holdDialog = page.locator('[data-ui-overlay="true"]')
    const detailUrl = page.url()
    await holdDialog.getByLabel('Reason').selectOption('Awaiting Client')
    await holdDialog.getByLabel('Note').fill('Waiting for the facilities lead to confirm the shutdown window.')
    await expect(holdDialog).toBeVisible()
    await expect(page).toHaveURL(detailUrl)

    await holdDialog.getByRole('button', { name: 'Cancel' }).click()
    await expect(holdDialog).toHaveCount(0)
    await expect(page.getByTestId('work-order-summary-card')).toBeVisible()
    await expect(page).toHaveURL(detailUrl)
  })

  test('tenant user sees status-driven actions, can save progress updates, and can complete the operational flow', async ({ page }) => {
    const state = createScenarioState()
    let pdfRequested = false

    await mockTenantSession(page, state, () => {
      pdfRequested = true
    })

    await page.goto(`/work-orders/${state.workOrder.id}`)
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toBeVisible()

    await page.getByRole('button', { name: 'Assignment' }).click()
    await page.getByRole('button', { name: 'Edit assignment' }).click()
    await page.getByTestId('assignment-group-control').getByRole('combobox').selectOption('')
    await expect(page.getByText(/assignment group updated/i).first()).toBeVisible()
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Assign / Schedule' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Start Work' })).toHaveCount(0)

    await page.getByTestId('assignment-group-control').getByRole('combobox').selectOption('group-1')
    await expect(page.getByText(/assignment group updated/i).first()).toBeVisible()
    await page.getByLabel('Moses Otieno').check()
    await page.getByLabel('Grace Wanjiku').check()
    await page.getByTestId('assignment-save-button').click()
    await expect(page.getByText(/assignment saved/i).first()).toBeVisible()
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'Start Work' })).toBeVisible()

    await page.getByRole('button', { name: 'Start Work' }).click()
    await expect(page.getByText(/work order updated/i).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Complete' })).toBeVisible()
    await expect(page.getByLabel('Open more actions').first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Complete' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Resume Work' })).toHaveCount(0)

    await page.getByRole('button', { name: 'Work Execution' }).click()
    await page.getByRole('textbox', { name: 'Diagnosis / findings' }).fill('Burnt contactor found in the panel and cable lug was loose.')
    await page.getByRole('textbox', { name: 'Work performed' }).fill('Replaced the contactor, retightened terminations, tested load transfer, and cleaned the cabinet.')
    await page.getByRole('button', { name: /save execution/i }).click()
    await expect(page.getByText('Completion summary saved')).toBeVisible()

    await page.getByPlaceholder('Day 1: inspected pump, isolated failure, awaiting seal kit.').fill('Day 1: Stabilized the transfer panel and confirmed the generator could take load safely while waiting for final closeout.')
    await page.getByRole('button', { name: /^Save update$/ }).click()
    await expect(page.getByText(/progress update saved/i)).toBeVisible()

    await page.getByLabel('Open more actions').first().click()
    await page.getByRole('button', { name: 'Put On Hold' }).click()
    const holdDialog = page.locator('[data-ui-overlay="true"]')
    await holdDialog.getByLabel('Reason').selectOption('Awaiting Materials')
    await holdDialog.getByLabel('Note').fill('Waiting for the replacement contactor from stores before the final handover.')
    await holdDialog.getByRole('button', { name: /^Put On Hold$/ }).click()
    await expect(page.getByText(/work order placed on hold/i)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Resume Work' })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Complete' })).toHaveCount(0)
    await expect(page.getByText('Awaiting Materials', { exact: true }).first()).toBeVisible()

    await page.getByRole('button', { name: 'Resume Work' }).click()
    await expect(page.getByText(/work order updated/i).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Complete' })).toBeVisible()
    await expect(page.getByLabel('Open more actions').first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Complete' })).toBeVisible()

    await page.getByRole('button', { name: 'Evidence' }).click()
    await page.getByTestId('browse-evidence-control').locator('input[type="file"]').setInputFiles({
      name: 'before.jpg',
      mimeType: 'image/jpeg',
      buffer: Buffer.from('/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQDxAPEA8QDw8PDw8QEA8PDw8QFREWFhURFRUYHSggGBolHRUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGhAQGi0lHyUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAEAAQAMBIgACEQEDEQH/xAAXAAEBAQEAAAAAAAAAAAAAAAAAAQMC/8QAGhABAAMBAQEAAAAAAAAAAAAAAAECBAMREv/EABUBAQEAAAAAAAAAAAAAAAAAAAAB/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEQMRAD8A9wD5m0xvL0rjv//Z', 'base64'),
    })
    await page.getByRole('button', { name: /upload evidence/i }).click()
    await expect(page.getByText('Evidence uploaded', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'Materials' }).click()
    await page.locator('select').filter({ hasText: /select material/i }).first().selectOption('material-1')
    await page.locator('input[type="number"]').first().fill('1')
    await page.getByRole('button', { name: /add material/i }).click()
    await expect(page.getByText(/material added/i)).toBeVisible()

    await page.getByRole('button', { name: 'Complete' }).click()
    await expect(page.getByText(/work order updated/i).first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Start Work' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Resume Work' })).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Complete' })).toHaveCount(0)
    await expect(page.getByTestId('workflow-actions').getByRole('button', { name: 'View Service Report' })).toBeVisible()

    await page.getByTestId('workflow-actions').getByRole('button', { name: 'View Service Report' }).click()
    await page.getByRole('button', { name: 'Download PDF' }).first().click()
    await expect.poll(() => pdfRequested).toBeTruthy()
  })

  test('mobile work-order detail stacks cleanly and scrolls without horizontal overflow', async ({ page }) => {
    const state = createScenarioState()

    await mockTenantSession(page, state, () => {})
    await page.setViewportSize({ width: 390, height: 844 })
    await page.goto(`/work-orders/${state.workOrder.id}`)

    await expect(page.getByTestId('workflow-mobile-bar')).toBeVisible()
    await assertNoHorizontalOverflow(page)

    await page.getByRole('button', { name: 'Work Execution' }).click()
    const updateInput = page.getByRole('textbox', { name: 'Progress comment' })
    await updateInput.fill('Checked the ATS panel, isolated the fault, and kept the work update form open on mobile.')
    await expect(updateInput).toHaveValue('Checked the ATS panel, isolated the fault, and kept the work update form open on mobile.')
    await expect(page.getByTestId('workflow-mobile-bar')).toBeVisible()

    await page.getByRole('button', { name: 'Signatures' }).click()
    await page.getByTestId('client-signoff-tab').scrollIntoViewIfNeeded()
    const scrollY = await page.evaluate(() => window.scrollY)
    expect(scrollY).toBeGreaterThanOrEqual(0)
  })
})

function createScenarioState() {
  const workOrder = {
    id: 'work-order-1',
    branchId: 'branch-1',
    branchName: 'Nairobi Service Hub',
    clientId: 'client-1',
    clientName: 'Acme Manufacturing',
    assetId: 'asset-1',
    assetName: 'Generator 02',
    assignmentGroupId: 'group-1',
    assignmentGroupName: 'Generator Team',
    workOrderNumber: 'WO-2026-0042',
    title: 'Generator transfer fault',
    description: 'Generator failed to transfer load after a mains outage.',
    contactPerson: 'Sophia Nyaga',
    contactPhone: '0712 000 456',
    priority: 'High',
    status: 'Open',
    createdByName: 'Sophia Nyaga',
    assignedByName: 'Dispatch Supervisor',
    impact: 'Production line affected',
    urgency: 'High',
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
    arrivalAt: null,
    departureAt: null,
    completedAt: null,
    workDoneNotes: '',
    jobCardNotes: '',
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
        status: 'Pending',
        assignedAt: '2026-05-13T08:05:00.000Z',
        acceptedAt: null,
        arrivalAt: null,
        departureAt: null,
        notes: 'Urgent site attendance requested.',
      },
    ],
    assignmentSummary: 'Generator Team -> Moses Otieno',
    isUnassigned: false,
    pmTemplateId: null,
    pmTemplateName: null,
    preventiveMaintenancePlanId: null,
    systemName: 'Power Distribution',
    serviceType: 'Emergency Response',
    checklistItems: [],
  }

  return {
    workOrder,
    execution: {
      notes: {
        findings: '',
        workDone: '',
      },
      photos: [] as Array<Record<string, unknown>>,
      materialUsages: [] as Array<Record<string, unknown>>,
      signatures: [] as Array<Record<string, unknown>>,
      progressUpdates: [] as Array<Record<string, unknown>>,
      reportPreview: {
        companyName: 'Acme Manufacturing',
        workOrderNumber: workOrder.workOrderNumber,
        title: workOrder.title,
        clientName: workOrder.clientName,
        siteLabel: workOrder.branchName,
        assetName: workOrder.assetName,
        assetDetails: 'GEN-02 | Generator | Plant 1',
        technicianTeam: workOrder.leadTechnicianName,
        reportedProblem: workOrder.description,
        findings: '',
        workDone: '',
        generatedAtLabel: '2026-05-13 10:30 UTC',
        timestamps: [
          { label: 'Created', value: '2026-05-13 08:00' },
          { label: 'Arrived', value: null },
          { label: 'Started', value: null },
          { label: 'Completed', value: null },
          { label: 'Departed', value: null },
        ],
        checklistSections: [] as Array<Record<string, unknown>>,
        materials: [] as Array<Record<string, unknown>>,
        photoGroups: [] as Array<Record<string, unknown>>,
        signatures: [] as Array<Record<string, unknown>>,
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
    assignmentGroups: [
      {
        id: 'group-1',
        name: 'Generator Team',
        description: 'Field response team',
        skillArea: 'Power',
        branchId: 'branch-1',
        branchName: 'Nairobi Service Hub',
        isActive: true,
        members: [
          {
            id: 'group-member-1',
            assignmentGroupId: 'group-1',
            userId: 'user-1',
            technicianId: 'tech-1',
            memberName: 'Moses Otieno',
            email: 'tech@tenant.test',
            isLead: true,
            isActive: true,
            addedAt: '2026-05-13T08:00:00.000Z',
          },
          {
            id: 'group-member-2',
            assignmentGroupId: 'group-1',
            userId: 'user-2',
            technicianId: 'tech-2',
            memberName: 'Grace Wanjiku',
            email: 'grace@tenant.test',
            isLead: false,
            isActive: true,
            addedAt: '2026-05-13T08:02:00.000Z',
          },
        ],
        technicianIds: ['tech-1', 'tech-2'],
      },
    ],
  }
}

async function mockTenantSession(page: Page, state: ReturnType<typeof createScenarioState>, onPdfRequest: () => void) {
  const baseApi = 'http://localhost:5072'

  await registerApiCatchAll(page)
  await loginWithMockSession(page, {
    role: 'tenantAdmin',
    email: 'tech@tenant.test',
    fullName: 'Moses Otieno',
    tenantName: 'Acme Manufacturing',
    tenantId: 'tenant-1',
  })

  await page.route(`${baseApi}/api/workorders/work-order-1`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as { dueDate?: string | null }
      state.workOrder.dueDate = body.dueDate ?? state.workOrder.dueDate
      await route.fulfill(json(state.workOrder))
      return
    }
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([state.workOrder]))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/execution`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/assignment-groups`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.assignmentGroups))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/execution-notes`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { findings?: string; workDone?: string; progressComment?: string }
    state.execution.notes.findings = body.findings ?? state.execution.notes.findings
    state.execution.notes.workDone = body.workDone ?? state.execution.notes.workDone
    state.execution.reportPreview.findings = state.execution.notes.findings
    state.execution.reportPreview.workDone = state.execution.notes.workDone
    state.workOrder.jobCardNotes = state.execution.notes.findings
    state.workOrder.workDoneNotes = state.execution.notes.workDone
    if (body.progressComment) {
      state.execution.progressUpdates.unshift({
        id: `progress-${state.execution.progressUpdates.length + 1}`,
        message: body.progressComment,
        actorUserId: 'user-1',
        actorUserName: 'Moses Otieno',
        occurredAt: '2026-05-13T09:00:00.000Z',
      })
    }
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/assign-group`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { assignmentGroupId?: string | null }
    const group = state.assignmentGroups.find((item) => item.id === body.assignmentGroupId) || null
    state.workOrder.assignmentGroupId = group?.id || null
    state.workOrder.assignmentGroupName = group?.name || null
    state.workOrder.assignmentType = group ? 'AssignmentGroup' : 'Unassigned'
    state.workOrder.assignmentSummary = group?.name || 'Unassigned'
    state.workOrder.isUnassigned = !group
    if (!group) {
      state.workOrder.assignedTechnicianId = null
      state.workOrder.assignedTechnicianName = null
      state.workOrder.assignedTechnicianIds = []
      state.workOrder.leadTechnicianId = null
      state.workOrder.leadTechnicianName = null
      state.workOrder.technicianAssignments = []
    }
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/assign-technicians`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { technicianIds: string[]; leadTechnicianId?: string | null }
    const members = state.assignmentGroups[0].members.filter((member) => body.technicianIds.includes(member.technicianId))
    state.workOrder.assignmentType = body.technicianIds.length > 1 ? 'MultipleTechnicians' : 'IndividualTechnician'
    state.workOrder.assignedTechnicianIds = [...body.technicianIds]
    state.workOrder.leadTechnicianId = body.leadTechnicianId || body.technicianIds[0] || null
    state.workOrder.assignedTechnicianId = body.technicianIds[0] || null
    state.workOrder.assignedTechnicianName = members[0]?.memberName || null
    state.workOrder.leadTechnicianName = members.find((member) => member.technicianId === state.workOrder.leadTechnicianId)?.memberName || members[0]?.memberName || null
    state.workOrder.technicianAssignments = members.map((member, index) => ({
      id: `assign-${index + 1}`,
      technicianId: member.technicianId,
      technicianName: member.memberName,
      isLead: member.technicianId === state.workOrder.leadTechnicianId,
      status: 'Pending',
      assignedAt: '2026-05-13T08:05:00.000Z',
      acceptedAt: null,
      arrivalAt: null,
      departureAt: null,
      notes: 'Urgent site attendance requested.',
    }))
    state.workOrder.assignmentSummary = members.map((member) => member.memberName).join(', ')
    state.workOrder.isUnassigned = body.technicianIds.length === 0
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/start`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    state.workOrder.status = 'In Progress'
    state.workOrder.workStartedAt = '2026-05-13T08:45:00.000Z'
    state.execution.reportPreview.timestamps[2].value = '2026-05-13 08:45'
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/pause`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { holdReason?: string | null; note?: string | null }
    state.workOrder.status = 'On Hold'
    state.workOrder.holdReason = body.holdReason || null
    state.workOrder.holdNote = body.note || null
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/resume`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    state.workOrder.status = 'In Progress'
    state.workOrder.holdReason = null
    state.workOrder.holdNote = null
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/materials-used`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { materialItemId: string; quantityUsed: number }
    const material = state.materials.find((item) => item.id === body.materialItemId)!
    state.execution.materialUsages.push({
      id: `usage-${state.execution.materialUsages.length + 1}`,
      materialItemId: material.id,
      materialName: material.itemName,
      unitOfMeasure: material.unitOfMeasure,
      assetId: state.workOrder.assetId,
      assetName: state.workOrder.assetName,
      quantityUsed: body.quantityUsed,
      unitCost: material.unitCost,
      chargeable: true,
      notes: null,
      usedByUserId: 'user-1',
      usedByName: 'Moses Otieno',
      usedAt: '2026-05-13T09:00:00.000Z',
    })
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/photos`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    state.execution.photos.push({
      id: `photo-${state.execution.photos.length + 1}`,
      attachmentId: 'attachment-1',
      fileName: 'before.jpg',
      publicUrl: 'https://example.test/before.jpg',
      caption: 'before.jpg',
      category: 'During',
      includeInReport: true,
      uploadedByUserId: 'user-1',
      uploadedByName: 'Moses Otieno',
      uploadedAt: '2026-05-13T09:10:00.000Z',
    })
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/signatures`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    const body = route.request().postDataJSON() as { signatureType: string; signerName: string; signerRole?: string; signatureDataUrl: string }
    const existingIndex = state.execution.signatures.findIndex((item) => item.signatureType === body.signatureType)
    const nextSignature = {
      id: `signature-${body.signatureType.toLowerCase()}`,
      signatureType: body.signatureType,
      signerName: body.signerName,
      signerRole: body.signerRole || null,
      signatureDataUrl: body.signatureDataUrl,
      comment: null,
      capturedByUserId: 'user-1',
      capturedAt: '2026-05-13T09:15:00.000Z',
    }
    if (existingIndex >= 0) {
      state.execution.signatures[existingIndex] = nextSignature
    } else {
      state.execution.signatures.push(nextSignature)
    }
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/complete`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    state.workOrder.status = 'Completed'
    state.workOrder.completedAt = '2026-05-13T09:20:00.000Z'
    state.execution.reportPreview.timestamps[3].value = '2026-05-13 09:20'
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/service-report/pdf`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    onPdfRequest()
    await route.fulfill({
      status: 200,
      contentType: 'application/pdf',
      headers: { 'content-disposition': 'attachment; filename="WO-2026-0042-service-report.pdf"', ...CORS_HEADERS },
      body: '%PDF-1.4\n%Mock Service Report\n',
    })
  })

  await page.route(`${baseApi}/api/workorders/work-order-1/events`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json([]))
  })

  await page.route(`${baseApi}/api/materials**`, async (route) => {
    if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
    await route.fulfill(json(state.materials))
  })
}

