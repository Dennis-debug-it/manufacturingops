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
    // Heading out is its own step (it also clocks the technician in on the server).
    await page.getByRole('button', { name: 'On My Way' }).click()
    await expect(page.getByText(/on the way/i).first()).toBeVisible()
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

  test('technician reaches time & leave, parts, guides, service report and account from /tech', async ({ page }) => {
    const state = createScenarioState()
    // A job already in progress, so parts can be requested.
    Object.assign(state.workOrder.technicianAssignments[0]!, { status: 'Arrived', acceptedAt: '2026-05-16T08:20:00.000Z', arrivalAt: '2026-05-16T08:35:00.000Z' })
    Object.assign(state.workOrder, { status: 'In Progress', arrivalAt: '2026-05-16T08:35:00.000Z', workStartedAt: '2026-05-16T08:40:00.000Z' })
    await mockTechnicianMode(page, state)
    const self = await mockTechnicianSelfService(page, state.workOrder.id)

    // Office links land on the technician equivalent instead of a 403 page.
    await page.goto('/team/time-attendance/leave')
    await expect(page).toHaveURL(/\/tech\/time$/)
    await expect(page.getByRole('heading', { name: 'My Time & Leave' })).toBeVisible()
    await page.getByRole('button', { name: /clock in/i }).click()
    await expect(page.getByText('Clocked in', { exact: true }).first()).toBeVisible()
    await page.getByLabel('Reason (optional)').fill('Family event')
    await page.getByRole('button', { name: 'Submit request' }).click()
    await expect(page.getByText('Leave request submitted')).toBeVisible()
    expect(self.leaveRequests).toHaveLength(1)
    expect(self.leaveRequests[0]).toMatchObject({ leaveType: 'ANNUAL', reason: 'Family event' })

    // Guides: search and open.
    await page.getByRole('link', { name: 'Guides' }).click()
    await expect(page).toHaveURL(/\/tech\/guides$/)
    await page.getByRole('link', { name: /Generator will not start/ }).click()
    await expect(page.getByRole('heading', { name: 'Generator will not start' })).toBeVisible()
    await expect(page.getByText('Check the starter relay first.')).toBeVisible()

    // Parts on the job: request a part, then return an unused one.
    await page.goto(`/tech/jobs/${state.workOrder.id}`)
    await expect(page.getByText('Guides for this job')).toBeVisible()
    await page.getByRole('link', { name: 'Parts', exact: true }).click()
    await page.getByRole('button', { name: 'Request parts' }).click()
    await page.getByLabel('Stock item', { exact: true }).selectOption({ index: 1 })
    await page.getByLabel('Quantity requested').fill('2')
    await page.getByRole('button', { name: 'Add to request' }).click()
    await page.getByRole('button', { name: 'Send request' }).click()
    await expect(page.getByText('Parts request sent to stores')).toBeVisible()
    expect(self.partsRequests[0]).toMatchObject({ items: [{ inventoryItemId: state.materials[0]!.id, quantityRequested: 2 }] })

    await page.getByRole('button', { name: 'Return parts' }).click()
    await page.getByLabel('Quantity of Starter relay to return').fill('1')
    await page.getByRole('button', { name: 'Send return' }).click()
    await expect(page.getByText('Return sent to stores')).toBeVisible()
    expect(self.partsReturns[0]).toMatchObject({ lines: [{ workOrderMaterialUsageId: 'usage-1', quantity: 1, condition: 'Unused' }] })

    // Service report PDF from the Finish tab.
    await page.getByRole('link', { name: 'Finish', exact: true }).click()
    const download = page.waitForEvent('download')
    await page.getByRole('button', { name: /download service report/i }).click()
    expect((await download).suggestedFilename()).toBe('WO-TECH-100-service-report.pdf')

    // Account: van stock, password change and notification preferences.
    await page.getByRole('link', { name: 'Me', exact: true }).click()
    await expect(page.getByText(/My van: Van 7/)).toBeVisible()
    await expect(page.getByText('Starter relay')).toBeVisible()
    await page.getByLabel('Current password', { exact: true }).fill('Old-pass-1')
    await page.getByLabel('New password', { exact: true }).fill('New-pass-22')
    await page.getByLabel('Confirm new password').fill('New-pass-22')
    await page.getByRole('button', { name: 'Change password' }).click()
    await expect(page.getByText('Your password has been changed successfully.')).toBeVisible()
    await page.getByRole('switch', { name: 'Work order assigned: email' }).click()
    await expect.poll(() => self.preferenceUpdates.length).toBe(1)
    expect(self.preferenceUpdates[0]).toEqual([{ notificationKey: 'workorder.assigned', emailEnabled: false, inAppEnabled: true }])
  })

  test('leaving site offers to clock out; captions and office notes can be edited and sent', async ({ page }) => {
    const state = createScenarioState()
    Object.assign(state.workOrder.technicianAssignments[0]!, { status: 'Arrived', acceptedAt: '2026-05-16T08:20:00.000Z', arrivalAt: '2026-05-16T08:35:00.000Z' })
    Object.assign(state.workOrder, { status: 'In Progress', arrivalAt: '2026-05-16T08:35:00.000Z', workStartedAt: '2026-05-16T08:40:00.000Z' })
    state.execution.photos.push({
      id: 'photo-9', attachmentId: 'attachment-9', fileName: 'panel.jpg', publicUrl: 'https://example.test/panel.jpg', caption: 'panel.jpg',
      category: 'During', includeInReport: true, uploadedByUserId: 'user-1', uploadedByName: 'Moses Otieno', uploadedAt: '2026-05-16T09:00:00.000Z',
    })
    await mockTechnicianMode(page, state)
    const self = await mockTechnicianSelfService(page, state.workOrder.id)
    self.clockedIn = true
    const sent = { notes: [] as Array<Record<string, unknown>>, captions: [] as Array<Record<string, unknown>>, clockedOut: false }
    const reply = async (route: import('@playwright/test').Route, body: unknown) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json(body))
    }
    await page.route(`${E2E_API_URL}/api/workorders/${state.workOrder.id}/notes`, async (route) => {
      if (route.request().method() === 'POST') sent.notes.push(route.request().postDataJSON() as Record<string, unknown>)
      await reply(route, route.request().method() === 'POST' ? { id: 'note-1', workOrderId: state.workOrder.id, content: 'Client wants a quote', isClientVisible: false, createdByUserId: 'user-1', createdByUserName: 'Moses Otieno', createdAt: '2026-05-16T10:00:00Z' } : [])
    })
    await page.route(`${E2E_API_URL}/api/workorders/${state.workOrder.id}/photos/photo-9`, async (route) => {
      if (route.request().method() === 'PUT') {
        const body = route.request().postDataJSON() as Record<string, unknown>
        sent.captions.push(body)
        state.execution.photos[0]!.caption = body.caption
      }
      await reply(route, state.execution)
    })
    await page.route(`${E2E_API_URL}/api/workorders/${state.workOrder.id}/departure`, async (route) => {
      state.workOrder.technicianAssignments[0]!.departureAt = '2026-05-16T11:00:00.000Z'
      await reply(route, state.workOrder)
    })
    await page.route(`${E2E_API_URL}/api/attendance/me/clock-out`, async (route) => {
      sent.clockedOut = true
      self.clockedIn = false
      await reply(route, { ok: true })
    })

    await page.goto(`/tech/jobs/${state.workOrder.id}`)
    await page.getByRole('radio', { name: 'Note to office' }).click()
    await page.getByLabel('Note to office').fill('Client wants a quote')
    await page.getByRole('button', { name: 'Send note' }).click()
    await expect(page.getByText('Note sent to the office')).toBeVisible()
    expect(sent.notes[0]).toEqual({ content: 'Client wants a quote', isClientVisible: false })

    await page.getByRole('link', { name: /Photos/ }).click()
    await page.getByRole('button', { name: 'Edit caption for panel.jpg' }).click()
    await page.getByLabel('Edit caption').fill('PV Panels')
    await page.getByRole('button', { name: 'Save', exact: true }).click()
    await expect(page.getByText('Caption updated')).toBeVisible()
    expect(sent.captions[0]).toMatchObject({ caption: 'PV Panels', category: 'During', includeInReport: true })

    await page.getByLabel('Open more actions').click()
    await page.getByRole('button', { name: 'Record Departure' }).click()
    await expect(page.getByRole('heading', { name: 'Clock out for the day?' })).toBeVisible()
    await page.getByRole('button', { name: 'Clock out', exact: true }).click()
    await expect(page.getByText('Clocked out for the day')).toBeVisible()
    expect(sent.clockedOut).toBe(true)
  })
})

async function mockTechnicianSelfService(page: Page, workOrderId: string) {
  const baseApi = E2E_API_URL
  const self = {
    leaveRequests: [] as Array<Record<string, unknown>>,
    partsRequests: [] as Array<Record<string, unknown>>,
    partsReturns: [] as Array<Record<string, unknown>>,
    preferenceUpdates: [] as unknown[],
    clockedIn: false,
  }
  const handle = async (pattern: string, handler: (method: string, body: unknown) => unknown, contentType?: string) => {
    await page.route(pattern, async (route) => {
      const method = route.request().method()
      if (method === 'OPTIONS') {
        await route.fulfill({ status: 204, headers: CORS_HEADERS })
        return
      }
      const result = handler(method, method === 'GET' ? null : route.request().postDataJSON())
      if (contentType) {
        await route.fulfill({ status: 200, headers: { ...CORS_HEADERS, 'Content-Type': contentType }, body: result as string })
        return
      }
      await route.fulfill(result === undefined ? { status: 204, headers: CORS_HEADERS } : json(result))
    })
  }

  const article = { id: 'kb-1', title: 'Generator will not start', slug: 'gen-no-start', summary: 'Starter faults', categoryId: null, categoryName: 'Generators', status: 'Published', visibility: 'Internal', publishedAt: '2026-05-01T00:00:00Z', updatedAt: '2026-05-01T00:00:00Z', viewCount: 0, helpfulCount: 0, notHelpfulCount: 0, tags: [] }
  await handle(`${baseApi}/api/attendance/me/status`, () => ({ technicianId: 'tech-1', technicianName: 'Moses Otieno', status: self.clockedIn ? 'ClockedIn' : 'NotClockedIn', clockInAt: self.clockedIn ? '2026-05-16T07:00:00Z' : null, lastLocation: null }))
  await handle(`${baseApi}/api/attendance/me/clock-in`, () => { self.clockedIn = true; return { ok: true } })
  await handle(`${baseApi}/api/attendance/leave-types`, () => [{ code: 'ANNUAL', name: 'Annual leave', isPaid: true, annualAllowanceDays: 21 }])
  await handle(`${baseApi}/api/attendance/leave/mine`, (method, body) => {
    if (method === 'POST') self.leaveRequests.push(body as Record<string, unknown>)
    return method === 'POST' ? { id: 'leave-1' } : []
  })
  await handle(`${baseApi}/api/attendance/timesheets/mine**`, () => ({ periodLabel: 'This week', totalHours: 0, overtimeHours: 0, leaveHours: 0, dailyBreakdown: [] }))
  await handle(`${baseApi}/api/knowledge/search**`, () => [article])
  await handle(`${baseApi}/api/knowledge/suggestions/work-order/${workOrderId}`, () => [article])
  await handle(`${baseApi}/api/knowledge/articles/kb-1`, () => ({ ...article, body: 'Check the starter relay first.', createdByUserId: 'u', createdByName: null, updatedByUserId: null, updatedByName: null, createdAt: '2026-05-01T00:00:00Z', relatedArticles: [] }))
  await handle(`${baseApi}/api/workorders/${workOrderId}/parts-requests`, (method, body) => {
    if (method === 'POST') self.partsRequests.push(body as Record<string, unknown>)
    return method === 'POST' ? { id: 'pr-1' } : []
  })
  await handle(`${baseApi}/api/workorders/${workOrderId}/parts-returns/returnable`, () => [{ workOrderMaterialUsageId: 'usage-1', materialItemId: 'mat-1', itemCode: 'SR-1', itemName: 'Starter relay', unitOfMeasure: 'pcs', quantityUsed: 2, pendingReturnQuantity: 0, returnableQuantity: 2, usedAt: '2026-05-16T09:00:00Z' }])
  await handle(`${baseApi}/api/workorders/${workOrderId}/parts-returns`, (method, body) => {
    if (method === 'POST') self.partsReturns.push(body as Record<string, unknown>)
    return method === 'POST' ? { id: 'ret-1' } : []
  })
  await handle(`${baseApi}/api/workorders/${workOrderId}/service-report/pdf`, () => '%PDF-1.4 test', 'application/pdf')
  await handle(`${baseApi}/api/vans`, () => [{ id: 'van-1', name: 'Van 7', registrationNumber: 'KDA 123A', technicianId: 'tech-1', technicianName: 'Moses Otieno', homeBranchId: null, homeBranchName: null, isActive: true, distinctItemCount: 1, createdAt: '2026-05-01T00:00:00Z' }])
  await handle(`${baseApi}/api/vans/van-1/stock`, () => [{ materialId: 'mat-1', materialName: 'Starter relay', materialCode: 'SR-1', unitOfMeasure: 'pcs', quantityOnHand: 4, reorderLevel: 1, unitCost: 18 }])
  await handle(`${baseApi}/api/auth/change-password`, () => ({ message: 'Your password has been changed successfully.' }))
  await handle(`${baseApi}/api/me/notification-preferences`, (method, body) => {
    if (method === 'PUT') {
      self.preferenceUpdates.push(body)
      return undefined
    }
    return [{ notificationKey: 'workorder.assigned', label: 'Work order assigned', category: 'Work orders', emailEnabled: true, inAppEnabled: true }]
  })
  return self
}

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

  await page.route(`${baseApi}/api/workorders/${state.workOrder.id}/in-transit`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }
    state.workOrder.technicianAssignments[0]!.status = 'InTransit'
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


test.describe('mobile technician location prompt', () => {
  test.use({ permissions: [] })

  test('explains why location is used before the phone asks, then remembers the answer', async ({ page, context }) => {
    // Headless Chromium reports any ungranted permission as denied; a real phone says "prompt" until asked.
    await page.addInitScript(() => {
      const query = navigator.permissions.query.bind(navigator.permissions)
      navigator.permissions.query = async (descriptor) => {
        const result = await query(descriptor)
        return descriptor.name === 'geolocation' && result.state !== 'granted' ? ({ state: 'prompt' } as PermissionStatus) : result
      }
    })
    await mockTechnicianMode(page, createScenarioState())

    await page.goto('/tech')
    const card = page.getByRole('region').filter({ hasText: 'Confirm site arrivals with your location' })
    await expect(page.getByText('Confirm site arrivals with your location')).toBeVisible()
    await expect(page.getByText(/We don't track you in the background/)).toBeVisible()

    // Stands in for the technician tapping Allow on the phone's own prompt.
    await context.grantPermissions(['geolocation'])
    await context.setGeolocation({ latitude: -1.2921, longitude: 36.8219 })
    await page.getByRole('button', { name: 'Allow location' }).click()
    await expect(page.getByText(/location is on/i)).toBeVisible()
    await expect(card).toHaveCount(0)

    await page.goto('/tech/account')
    await expect(page.getByText('On. Arrivals and departures record where you are.')).toBeVisible()
    await expect(page.getByText('Confirm site arrivals with your location')).toHaveCount(0)
  })
})
