import type { Page, Route } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './auth'
import { E2E_API_URL } from './test-data'

const API = E2E_API_URL

export function makeWorkOrder(overrides: Record<string, unknown> = {}) {
  return {
    id: 'wo-1',
    branchId: 'branch-1',
    branchName: 'Nairobi Service Hub',
    clientId: 'client-1',
    clientName: 'Acme Manufacturing',
    siteId: 'site-1',
    siteName: 'Plant 1',
    assetId: 'asset-1',
    assetName: 'Generator 02',
    assignmentGroupId: 'group-1',
    assignmentGroupName: 'Generator Team',
    workOrderNumber: 'WO-2026-0042',
    title: 'Generator transfer fault',
    description: 'Generator failed to transfer load after a mains outage.',
    priority: 'High',
    status: 'Assigned',
    workType: 'CorrectiveMaintenance',
    slaStatus: 'OnTrack',
    assignmentType: 'IndividualTechnician',
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
    workDoneNotes: null,
    jobCardNotes: null,
    slaResponseDeadline: null,
    slaResolutionDeadline: null,
    slaResponseBreached: false,
    slaResolutionBreached: false,
    slaResponseBreachedAt: null,
    slaResolutionBreachedAt: null,
    acknowledgedByName: null,
    acknowledgementComments: null,
    acknowledgementDate: null,
    isPreventiveMaintenance: false,
    holdReason: null,
    holdNote: null,
    assignmentStatus: 'Accepted',
    assignmentNotes: null,
    technicianAssignments: [
      {
        id: 'assign-1', technicianId: 'tech-1', technicianName: 'Moses Otieno', isLead: true, status: 'Arrived',
        assignedAt: '2026-05-13T08:05:00.000Z', acceptedAt: '2026-05-13T08:20:00.000Z', arrivalAt: '2026-05-13T08:40:00.000Z', departureAt: null, notes: null,
      },
    ],
    assignmentSummary: 'Moses Otieno',
    isUnassigned: false,
    pmTemplateId: null,
    pmTemplateName: null,
    preventiveMaintenancePlanId: null,
    systemId: null,
    systemName: null,
    checklistItems: [],
    pmTemplateVersionSnapshot: null,
    scheduledStartAt: null,
    scheduledEndAt: null,
    notifyClient: true,
    clientContactEmail: null,
    billingStatus: null,
    isBillable: false,
    clientContactPerson: 'Peter Kariuki',
    clientContactPhone: '+254 700 111 222',
    clientSlaPlan: null,
    siteCode: 'P1',
    siteContactPerson: 'Jane Achieng',
    siteContactPhone: '+254 722 333 444',
    siteOperatingHours: 'Mon–Fri 08:00–17:00',
    siteAccessNotes: 'Report to the gatehouse; PPE required.',
    siteLocation: 'Industrial Area, Nairobi',
    siteLatitude: null,
    siteLongitude: null,
    estimatedLabourHours: null,
    assetCode: 'GEN-02',
    assetType: 'Generator',
    assetCategoryName: 'Generator',
    assetSerialNumber: 'CAT-99812',
    assetManufacturer: 'Caterpillar',
    assetModel: 'C18',
    assetLocation: 'Plant room',
    assetWarrantyExpiryDate: null,
    assetLastServiceDate: '2026-03-01T00:00:00.000Z',
    assetNextServiceDate: '2026-06-01T00:00:00.000Z',
    assetWarrantyStatus: null,
    availableTransitions: [],
    workflowStatusId: null,
    contractId: null,
    contractNumber: null,
    contractStatus: null,
    coverageStatus: null,
    billingModel: null,
    rateCardName: null,
    contractEndDate: null,
    contractRenewalRisk: null,
    pricingNotes: null,
    estimatedCharge: null,
    finalCharge: null,
    appliedSlaSource: null,
    appliedRateSource: null,
    trackingToken: null,
    workOrderTemplateId: null,
    quoteId: null,
    quoteNumber: null,
    parentWorkOrderId: null,
    childWorkOrderCount: 0,
    holdType: null,
    holdSince: null,
    plannedMaterials: [],
    isRepeatVisit: false,
    repeatVisitCount: 0,
    ...overrides,
  }
}

export type MockWorkOrder = ReturnType<typeof makeWorkOrder>

/** Everything the work order screens read and write, plus a log of what the page sent. */
export function makeScenario(overrides: Record<string, unknown> = {}) {
  return {
    workOrder: makeWorkOrder(overrides),
    execution: {
      notes: { findings: null as string | null, workDone: null as string | null },
      photos: [] as unknown[],
      materialUsages: [] as unknown[],
      signatures: [] as unknown[],
      progressUpdates: [] as unknown[],
      reportPreview: {},
    },
    events: [] as unknown[],
    notes: [] as Array<Record<string, unknown>>,
    requests: [] as Array<{ method: string; path: string; body: unknown }>,
  }
}

export type Scenario = ReturnType<typeof makeScenario>

const technicians = [
  { id: 'tech-1', fullName: 'Moses Otieno', skillCategory: 'Electrical' },
  { id: 'tech-2', fullName: 'Grace Wanjiku', skillCategory: 'Mechanical' },
  { id: 'tech-3', fullName: 'Brian Mutua', skillCategory: 'HVAC' },
]

const groups = [
  {
    id: 'group-1',
    name: 'Generator Team',
    isActive: true,
    members: [
      { technicianId: 'tech-1', memberName: 'Moses Otieno', isLead: true, isActive: true },
      { technicianId: 'tech-2', memberName: 'Grace Wanjiku', isLead: false, isActive: true },
    ],
  },
]

async function preflight(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }
  return false
}

function body(route: Route): unknown {
  try {
    return route.request().postDataJSON()
  } catch {
    return null
  }
}

/** Routes for one work order; status-changing calls update the scenario so refetches see them. */
async function routeWorkOrder(page: Page, state: Scenario, detailPrefix: string) {
  const wo = state.workOrder
  await page.route(`${API}/api/workorders/${wo.id}**`, async (route) => {
    if (await preflight(route)) return
    const method = route.request().method()
    const path = new URL(route.request().url()).pathname.replace(`/api/workorders/${wo.id}`, '') || '/'
    if (method !== 'GET') state.requests.push({ method, path, body: body(route) })

    switch (`${method} ${path}`) {
      case 'GET /execution': return route.fulfill(json(state.execution))
      case 'GET /events': return route.fulfill(json(state.events))
      case 'GET /notes': return route.fulfill(json(state.notes))
      case 'GET /sla': return route.fulfill(json({ workOrderId: wo.id, priority: wo.priority, policyName: null, responseDeadline: null, resolutionDeadline: null, responseBreached: false, resolutionBreached: false, responseBreachedAt: null, resolutionBreachedAt: null, responseMinutesRemaining: null, resolutionMinutesRemaining: null, status: 'NoSla', isPaused: false, pauseReason: null }))
      case 'POST /notes': {
        const note = { id: `note-${state.notes.length + 1}`, workOrderId: wo.id, createdByUserId: 'u1', createdByUserName: 'Ops Admin', createdAt: new Date().toISOString(), ...(body(route) as object) }
        state.notes.unshift(note)
        return route.fulfill(json(note))
      }
      case 'POST /execution-notes': {
        const payload = body(route) as { workDone?: string }
        if (payload.workDone) state.execution.notes.workDone = payload.workDone
        return route.fulfill(json(state.execution))
      }
      case 'POST /start':
        Object.assign(wo, { status: 'In Progress', workStartedAt: '2026-05-13T09:00:00.000Z' })
        return route.fulfill(json(wo))
      case 'PATCH /hold': {
        const payload = body(route) as { holdType: string; reason?: string | null }
        const status = payload.holdType === 'WaitingOnParts' ? 'Waiting on Parts' : payload.holdType === 'WaitingOnClient' ? 'Waiting on Client' : 'On Hold'
        Object.assign(wo, { status, holdReason: payload.reason ?? null })
        return route.fulfill(json(wo))
      }
      case 'PATCH /resume':
        Object.assign(wo, { status: 'In Progress', holdReason: null })
        return route.fulfill(json(wo))
      case 'POST /complete': {
        const payload = body(route) as { workDoneNotes: string }
        Object.assign(wo, { status: 'Completed', completedAt: '2026-05-13T10:00:00.000Z', workDoneNotes: payload.workDoneNotes })
        return route.fulfill(json(wo))
      }
      case 'POST /reassign': {
        const payload = body(route) as { technicianIds: string[] | null; leadTechnicianId: string | null; assignmentGroupId: string | null }
        const ids = payload.technicianIds ?? []
        Object.assign(wo, {
          status: ids.length > 0 ? 'Assigned' : wo.status,
          isUnassigned: ids.length === 0 && !payload.assignmentGroupId,
          assignmentGroupId: payload.assignmentGroupId,
          assignmentSummary: ids.map((id) => technicians.find((t) => t.id === id)?.fullName).join(', ') || 'Unassigned',
          technicianAssignments: ids.map((id, index) => ({
            id: `assign-${index + 1}`, technicianId: id, technicianName: technicians.find((t) => t.id === id)?.fullName ?? id,
            isLead: id === (payload.leadTechnicianId ?? ids[0]), status: 'Pending', assignedAt: '2026-05-13T08:30:00.000Z',
            acceptedAt: null, arrivalAt: null, departureAt: null, notes: null,
          })),
        })
        return route.fulfill(json(wo))
      }
      case 'GET /':
        return route.fulfill(json(wo))
      default:
        return route.fulfill(json(method === 'GET' ? [] : wo))
    }
  })
  await page.route(`${API}${detailPrefix}${wo.id}`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json(wo))
  })
}

/** The first-login welcome tour is a full-screen overlay; these specs exercise the page behind it. */
async function skipWelcomeTour(page: Page) {
  await page.addInitScript(() => {
    const getItem = Storage.prototype.getItem
    Storage.prototype.getItem = function (key: string) {
      return key.startsWith('ecosys.first-login-shown.') ? '1' : getItem.call(this, key)
    }
  })
}

/** Office user (tenant admin) viewing /work-orders. */
export async function mockOfficeWorkOrder(page: Page, state: Scenario) {
  await registerApiCatchAll(page)
  await skipWelcomeTour(page)
  await loginWithMockSession(page, { role: 'tenantAdmin', email: 'ops@tenant.test', fullName: 'Ops Admin', tenantName: 'Acme Manufacturing', tenantId: 'tenant-1' })
  await page.route(`${API}/api/technicians**`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json(technicians))
  })
  await page.route(`${API}/api/assignment-groups**`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json(groups))
  })
  await page.route(`${API}/api/workorders?**`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json({ items: [state.workOrder], totalCount: 1, page: 1, pageSize: 50 }))
  })
  await page.route(`${API}/api/workorders`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json([state.workOrder]))
  })
  await routeWorkOrder(page, state, '/api/workorders/')
}

/** Field technician on the /tech app; their technician id is tech-1. */
export async function mockTechnicianWorkOrder(page: Page, state: Scenario) {
  await registerApiCatchAll(page)
  await skipWelcomeTour(page)
  await loginWithMockSession(page, { role: 'technician', email: 'tech@tenant.test', fullName: 'Moses Otieno', tenantName: 'Acme Manufacturing', tenantId: 'tenant-1' })
  await page.route(`${API}/api/technician/jobs`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json([state.workOrder]))
  })
  await page.route(`${API}/api/technician/jobs/me`, async (route) => {
    if (await preflight(route)) return
    await route.fulfill(json({ technicianId: 'tech-1', fullName: 'Moses Otieno' }))
  })
  await routeWorkOrder(page, state, '/api/technician/jobs/')
}
