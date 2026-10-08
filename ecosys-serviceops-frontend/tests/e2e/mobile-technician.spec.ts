import { expect, test, type Locator, type Page } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'

test.use({ viewport: { width: 412, height: 915 }, permissions: ['geolocation'], geolocation: { latitude: -1.2921, longitude: 36.8219 } })

test.describe('mobile technician mode', () => {
  test('technician can progress the simplified workflow from the /tech route group', async ({ page }) => {
    const state = createScenarioState()

    await mockTechnicianMode(page, state)

    await page.goto('/tech')
    await expect(page.getByTestId('tech-jobs-page')).toBeVisible()
    await expect(page.getByText('WO-TECH-100', { exact: true })).toBeVisible()

    await page.getByTestId(`tech-job-card-${state.workOrder.id}`).click()
    await expect(page.getByTestId('tech-job-page')).toBeVisible()
    await expect(page.getByTestId('tech-bottom-bar')).toBeVisible()
    await expect(page.getByTestId('assignment-group-control')).toHaveCount(0)

    // The server requires the technician to accept and record arrival before work can start.
    await page.getByRole('button', { name: 'Accept Job' }).click()
    await expect(page.getByText(/job accepted/i)).toBeVisible()
    await page.getByRole('button', { name: "I've Arrived" }).click()
    await expect(page.getByText(/arrival recorded/i)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Start Work' })).toBeVisible()
    await page.getByRole('button', { name: 'Start Work' }).click()
    await expect(page.getByText(/job updated/i)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Complete' })).toBeVisible()
    await page.getByLabel('Open more actions').first().click()
    await expect(page.getByRole('button', { name: 'Put On Hold' })).toBeVisible()
    await page.keyboard.press('Escape')

    const jobUrl = page.url()
    const progressDraft = 'Replaced the relay, tightened the lug, and tested the generator under load.'
    await page.getByPlaceholder('Day 1: Inspected generator, found faulty AVR, awaiting replacement.').fill('Replaced the relay, tightened the lug, and tested the generator under load.')
    await expect(page.getByPlaceholder('Day 1: Inspected generator, found faulty AVR, awaiting replacement.')).toHaveValue(progressDraft)
    await expect(page).toHaveURL(jobUrl)
    await page.getByRole('button', { name: /save update/i }).click()
    await expect(page.getByText(/update saved/i)).toBeVisible()

    await page.goto(`/tech/jobs/${state.workOrder.id}/photos`)
    await page.getByTestId('browse-evidence-control').locator('input[type="file"]').setInputFiles({
      name: 'repair.jpg',
      mimeType: 'image/jpeg',
      buffer: Buffer.from('/9j/4AAQSkZJRgABAQAAAQABAAD/2wCEAAkGBxAQDxAPEA8QDw8PDw8QEA8PDw8QFREWFhURFRUYHSggGBolHRUVITEhJSkrLi4uFx8zODMsNygtLisBCgoKDg0OGhAQGi0lHyUtLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLS0tLf/AABEIAEAAQAMBIgACEQEDEQH/xAAXAAEBAQEAAAAAAAAAAAAAAAAAAQMC/8QAGhABAAMBAQEAAAAAAAAAAAAAAAECBAMREv/EABUBAQEAAAAAAAAAAAAAAAAAAAAB/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAwDAQACEQMRAD8A9wD5m0xvL0rjv//Z', 'base64'),
    })
    await page.getByRole('button', { name: /upload evidence/i }).click()
    await expect(page.getByText('Evidence uploaded', { exact: true })).toBeVisible()

    await page.goto(`/tech/jobs/${state.workOrder.id}/complete`)
    await page.getByPlaceholder('Client name').fill('Alice Njeri')
    await page.getByPlaceholder('Role').fill('Facilities Manager')
    await drawSignature(page.locator('canvas').first())
    await expect(page.getByText(/signature captured/i)).toBeVisible()
    await page.getByRole('button', { name: /save signature/i }).click()
    await expect(page.getByText(/client sign-off saved/i)).toBeVisible()

    await page.getByLabel(/work done/i).fill('Replaced the starter relay and load-tested the generator.')
    await page.getByRole('button', { name: 'Complete' }).click()
    await expect(page.getByText(/job updated/i)).toBeVisible()
    await expect(page.getByTestId('tech-bottom-bar')).toHaveCount(0)
  })
})

function createScenarioState() {
  const workOrder = {
    id: 'tech-job-1',
    branchId: 'branch-1',
    branchName: 'Nairobi Service Hub',
    clientId: 'client-1',
    clientName: 'Acme Facilities',
    siteId: 'site-1',
    siteName: 'Nairobi HQ',
    assetId: 'asset-1',
    assetName: 'Generator 01',
    assignmentGroupId: 'group-1',
    assignmentGroupName: 'Generator Team',
    workOrderNumber: 'WO-TECH-100',
    title: 'Generator start failure',
    description: 'Generator is not starting after mains failure.',
    priority: 'High',
    status: 'Open',
    slaStatus: 'On Track',
    assignmentType: 'AssignmentGroup',
    assignedTechnicianId: 'tech-1',
    assignedTechnicianName: 'Moses Otieno',
    assignedTechnicianIds: ['tech-1'],
    leadTechnicianId: 'tech-1',
    leadTechnicianName: 'Moses Otieno',
    dueDate: '2026-05-16T12:00:00.000Z',
    createdAt: '2026-05-16T08:00:00.000Z',
    workStartedAt: null,
    arrivalAt: null,
    departureAt: null,
    completedAt: null,
    workDoneNotes: '',
    jobCardNotes: '',
    slaResponseDeadline: '2026-05-16T09:00:00.000Z',
    slaResolutionDeadline: '2026-05-16T16:00:00.000Z',
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
    assignmentNotes: 'Attend within 30 minutes.',
    technicianAssignments: [
      {
        id: 'assign-1',
        technicianId: 'tech-1',
        technicianName: 'Moses Otieno',
        isLead: true,
        status: 'Pending',
        assignedAt: '2026-05-16T08:05:00.000Z',
        acceptedAt: null,
        arrivalAt: null,
        departureAt: null,
        notes: 'Attend within 30 minutes.',
      },
    ],
    assignmentSummary: 'Generator Team -> Moses Otieno',
    isUnassigned: false,
    pmTemplateId: null,
    pmTemplateName: null,
    preventiveMaintenancePlanId: null,
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
        companyName: 'Acme Facilities',
        workOrderNumber: workOrder.workOrderNumber,
        title: workOrder.title,
        clientName: workOrder.clientName,
        siteLabel: workOrder.siteName,
        assetName: workOrder.assetName,
        assetDetails: 'Generator | Nairobi HQ',
        technicianTeam: workOrder.leadTechnicianName,
        reportedProblem: workOrder.description,
        findings: '',
        workDone: '',
        generatedAtLabel: '2026-05-16 10:30 UTC',
        timestamps: [
          { label: 'Created', value: '2026-05-16 08:00' },
          { label: 'Arrived', value: null },
          { label: 'Started', value: null },
          { label: 'Completed', value: null },
          { label: 'Departed', value: null },
        ],
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
        itemCode: 'RLY-24',
        itemName: 'Starter relay',
        category: 'Electrical',
        unitOfMeasure: 'pcs',
        quantityOnHand: 8,
        reorderLevel: 2,
        unitCost: 18,
        isActive: true,
        isLowStock: false,
        createdAt: '2026-05-10T09:00:00.000Z',
      },
    ],
  }
}

async function mockTechnicianMode(page: Page, state: ReturnType<typeof createScenarioState>) {
  const baseApi = E2E_API_URL

  await registerApiCatchAll(page)
  await loginWithMockSession(page, {
    role: 'technician',
    email: 'tech@tenant.test',
    fullName: 'Moses Otieno',
    tenantName: 'Acme Facilities',
    tenantId: 'tenant-1',
  })

  await page.route(`${baseApi}/api/technician/jobs/me`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json({ technicianId: 'tech-1', fullName: 'Moses Otieno' }))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/technician-response`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.workOrder.technicianAssignments[0]!.status = 'Accepted'
    state.workOrder.technicianAssignments[0]!.acceptedAt = '2026-05-16T08:20:00.000Z'
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/arrival`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.workOrder.technicianAssignments[0]!.status = 'Arrived'
    state.workOrder.technicianAssignments[0]!.arrivalAt = '2026-05-16T08:35:00.000Z'
    state.workOrder.arrivalAt = '2026-05-16T08:35:00.000Z' as unknown as null
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/technician/jobs`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json([state.workOrder]))
  })

  await page.route(`${baseApi}/api/technician/jobs/${state.workOrder.id}`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/execution`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/materials**`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    await route.fulfill(json(state.materials))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/start`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.workOrder.status = 'In Progress'
    state.workOrder.workStartedAt = '2026-05-16T08:40:00.000Z'
    state.execution.reportPreview.timestamps[2].value = '2026-05-16 08:40'
    await route.fulfill(json(state.workOrder))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/execution-notes`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    const body = route.request().postDataJSON() as { workDone?: string; progressComment?: string }
    state.execution.notes.workDone = body.workDone || state.execution.notes.workDone
    state.execution.reportPreview.workDone = state.execution.notes.workDone
    if (body.progressComment) {
      state.execution.progressUpdates.unshift({
        id: `progress-${state.execution.progressUpdates.length + 1}`,
        message: body.progressComment,
        actorUserId: 'user-1',
        actorUserName: 'Moses Otieno',
        occurredAt: '2026-05-16T08:50:00.000Z',
      })
    }
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/photos`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.execution.photos.push({
      id: 'photo-1',
      attachmentId: 'attachment-1',
      fileName: 'repair.jpg',
      publicUrl: 'https://example.test/repair.jpg',
      caption: 'repair.jpg',
      category: 'During',
      includeInReport: true,
      uploadedByUserId: 'user-1',
      uploadedByName: 'Moses Otieno',
      uploadedAt: '2026-05-16T09:00:00.000Z',
    })
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/signatures`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    const body = route.request().postDataJSON() as { signerName: string; signerRole?: string; signatureDataUrl: string }
    state.execution.signatures = [{
      id: 'sig-client',
      signatureType: 'Client',
      signerName: body.signerName,
      signerRole: body.signerRole || null,
      signatureDataUrl: body.signatureDataUrl,
      comment: null,
      capturedByUserId: 'user-1',
      capturedAt: '2026-05-16T09:05:00.000Z',
    }]
    await route.fulfill(json(state.execution))
  })

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/complete`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.workOrder.status = 'Completed'
    state.workOrder.completedAt = '2026-05-16T09:10:00.000Z'
    state.execution.reportPreview.timestamps[3].value = '2026-05-16 09:10'
    await route.fulfill(json(state.workOrder))
  })
}

async function drawSignature(locator: Locator) {
  await locator.scrollIntoViewIfNeeded()
  const box = await locator.boundingBox()
  if (!box) {
    throw new Error('Signature pad was not visible')
  }

  const page = locator.page()
  await page.mouse.move(box.x + 30, box.y + 50)
  await page.mouse.down()
  await page.mouse.move(box.x + 90, box.y + 75, { steps: 8 })
  await page.mouse.move(box.x + 155, box.y + 35, { steps: 8 })
  await page.mouse.up()
}

