import { expect, test, type Route } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const API = 'http://localhost:5072'

test.describe('knowledge centre', () => {
  test('create article, publish it, and search it', async ({ page }) => {
    const state = createState()

    await registerApiCatchAll(page)
    await loginWithMockSession(page, {
      role: 'tenantAdmin',
      email: 'admin@acme.test',
      fullName: 'Alice Admin',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await mockKnowledgeApi(page, state)
    await mockWorkOrderDetailApi(page, state)

    await page.goto('/knowledge')
    await expect(page.getByRole('heading', { name: /operational knowledge/i })).toBeVisible()

    await page.getByRole('button', { name: /new category/i }).click()
    await page.getByLabel('Category name').fill('Generator Guides')
    await page.getByLabel('Document description').fill('Generator troubleshooting references.')
    await page.getByRole('button', { name: /save category/i }).click()
    await expect(page.getByText(/category created/i)).toBeVisible()

    await page.getByRole('link', { name: /new article/i }).click()
    await page.getByLabel('Summary').fill('Reset steps for transfer faults and relay alarms.')
    await page.getByLabel('Category').selectOption({ label: 'Generator Guides' })
    await page.getByLabel('Tags').fill('Generator, ATS, relay')
    await page.getByLabel('Body').fill('Use the local reset, inspect the relay, and verify transfer sequencing before replacing modules.')
    await page.getByLabel('Title').fill('Generator ATS reset guide')
    await expect(page.getByLabel('Title')).toHaveValue('Generator ATS reset guide')
    const createResponsePromise = page.waitForResponse((response) =>
      response.url().endsWith('/api/knowledge/articles')
      && response.request().method() === 'POST')
    await page.getByRole('button', { name: /save article/i }).click()
    const createResponse = await createResponsePromise
    expect(createResponse.ok()).toBeTruthy()
    await page.waitForURL(/\/knowledge\/article-\d+$/)
    await expect(page.getByRole('heading', { name: 'Generator ATS reset guide' })).toBeVisible()

    await page.getByRole('button', { name: /publish/i }).click()
    await expect(page.getByText(/article published/i)).toBeVisible()

    await page.goto('/knowledge')
    await page.getByPlaceholder(/search titles, tags/i).fill('relay')
    await expect(page.getByText('Generator ATS reset guide')).toBeVisible()
  })

  test('suggests published knowledge on a work order and allows drafting from work order context', async ({ page }) => {
    test.fixme(true, 'Work-order knowledge suggestions and draft-from-work-order actions are not rendered in the current work-order detail UI.')
    const state = createState()

    await registerApiCatchAll(page)
    await loginWithMockSession(page, {
      role: 'tenantAdmin',
      email: 'admin@acme.test',
      fullName: 'Alice Admin',
      tenantId: 'tenant-1',
      tenantName: 'Acme Facilities',
    })

    await mockKnowledgeApi(page, state)
    await mockWorkOrderDetailApi(page, state)

    state.categories.push({
      id: 'category-1',
      name: 'Generator Guides',
      description: 'Generator troubleshooting references.',
      displayOrder: 1,
      isActive: true,
    })
    state.articles.push({
      id: 'article-1',
      title: 'Generator ATS reset guide',
      slug: 'generator-ats-reset-guide',
      summary: 'Reset steps for transfer faults and relay alarms.',
      body: 'Use the local reset, inspect the relay, and verify transfer sequencing before replacing modules.',
      categoryId: 'category-1',
      categoryName: 'Generator Guides',
      status: 'Published',
      visibility: 'Internal',
      createdAt: '2026-05-16T09:30:00.000Z',
      updatedAt: '2026-05-16T10:05:00.000Z',
      publishedAt: '2026-05-16T10:05:00.000Z',
      sourceWorkOrderId: null,
      tags: ['Generator', 'ATS', 'relay'],
      versions: [{ id: 'version-1', versionNumber: 1, title: 'Generator ATS reset guide', body: 'Use the local reset, inspect the relay, and verify transfer sequencing before replacing modules.', createdAt: '2026-05-16T09:30:00.000Z' }],
      relatedArticles: [],
    })

    await page.goto(`/work-orders/${state.workOrder.id}`)
    await expect(page.getByText(/suggested knowledge/i)).toBeVisible()
    await expect(page.getByText('Generator ATS reset guide')).toBeVisible()

    await page.getByRole('button', { name: /draft from work order/i }).click()
    await page.waitForURL(/\/knowledge\/.+\/edit$/)
    await expect(page.getByLabel('Title')).toHaveValue(/Draft from WO-500/)
  })
})

function createState() {
  const categories = [] as Array<{ id: string; name: string; description?: string | null; displayOrder: number; isActive: boolean }>
  const articles = [] as Array<{
    id: string
    title: string
    slug: string
    summary?: string | null
    body: string
    categoryId?: string | null
    categoryName?: string | null
    status: string
    visibility: string
    createdAt: string
    updatedAt?: string | null
    publishedAt?: string | null
    sourceWorkOrderId?: string | null
    tags: string[]
    versions: Array<{ id: string; versionNumber: number; title: string; body: string; createdAt: string }>
    relatedArticles: Array<Record<string, unknown>>
  }>

  return {
    categories,
    articles,
    workOrder: {
      id: 'knowledge-wo-1',
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
      workOrderNumber: 'WO-500',
      title: 'Generator start failure',
      description: 'Generator failed after mains outage.',
      priority: 'High',
      status: 'Completed',
      slaStatus: 'On Track',
      assignmentType: 'AssignmentGroup',
      assignedTechnicianId: 'tech-1',
      assignedTechnicianName: 'Moses Otieno',
      assignedTechnicianIds: ['tech-1'],
      leadTechnicianId: 'tech-1',
      leadTechnicianName: 'Moses Otieno',
      dueDate: '2026-05-16T12:00:00.000Z',
      createdAt: '2026-05-16T08:00:00.000Z',
      workStartedAt: '2026-05-16T08:30:00.000Z',
      arrivalAt: '2026-05-16T08:25:00.000Z',
      departureAt: '2026-05-16T09:40:00.000Z',
      completedAt: '2026-05-16T09:35:00.000Z',
      workDoneNotes: 'Replaced starter relay and reset ATS.',
      jobCardNotes: 'Starter relay failed and ATS alarm required reset.',
      slaResponseDeadline: '2026-05-16T09:00:00.000Z',
      slaResolutionDeadline: '2026-05-16T16:00:00.000Z',
      slaResponseBreached: false,
      slaResolutionBreached: false,
      acknowledgedByName: null,
      acknowledgementComments: null,
      acknowledgementDate: null,
      workType: 'CorrectiveMaintenance',
      isPreventiveMaintenance: false,
      assignmentStatus: 'AssignedToTechnician',
      assignmentNotes: 'Urgent fault response.',
      technicianAssignments: [
        {
          id: 'assign-1',
          technicianId: 'tech-1',
          technicianName: 'Moses Otieno',
          isLead: true,
          status: 'Accepted',
          assignedAt: '2026-05-16T08:05:00.000Z',
          acceptedAt: '2026-05-16T08:10:00.000Z',
          arrivalAt: '2026-05-16T08:25:00.000Z',
          departureAt: '2026-05-16T09:40:00.000Z',
          notes: 'Urgent fault response.',
        },
      ],
      assignmentSummary: 'Generator Team -> Moses Otieno',
      isUnassigned: false,
      pmTemplateId: null,
      pmTemplateName: null,
      preventiveMaintenancePlanId: null,
      checklistItems: [],
    },
  }
}

async function mockKnowledgeApi(page: import('@playwright/test').Page, state: ReturnType<typeof createState>) {
  await page.route(`${API}/api/knowledge/categories`, async (route) => {
    if (handleOptions(route)) return

    if (route.request().method() === 'GET') {
      await route.fulfill(json(state.categories))
      return
    }

    const body = route.request().postDataJSON() as { name: string; description?: string | null; displayOrder: number }
    const category = {
      id: `category-${state.categories.length + 1}`,
      name: body.name,
      description: body.description ?? null,
      displayOrder: body.displayOrder,
      isActive: true,
    }
    state.categories.push(category)
    await route.fulfill(json(category))
  })

  await page.route(`${API}/api/knowledge/articles/from-work-order/*`, async (route) => {
    if (handleOptions(route)) return
    const article = {
      id: `article-${state.articles.length + 1}`,
      title: `Draft from ${state.workOrder.workOrderNumber} - ${state.workOrder.title}`,
      slug: `draft-${state.workOrder.workOrderNumber.toLowerCase()}`,
      summary: 'Drafted from completed work order.',
      body: `# ${state.workOrder.title}\n\n## Problem\n${state.workOrder.description}\n\n## Findings\n${state.workOrder.jobCardNotes}\n\n## Resolution\n${state.workOrder.workDoneNotes}`,
      categoryId: state.categories[0]?.id ?? null,
      categoryName: state.categories[0]?.name ?? null,
      status: 'Draft',
      visibility: 'Internal',
      createdAt: '2026-05-16T10:00:00.000Z',
      updatedAt: '2026-05-16T10:00:00.000Z',
      publishedAt: null,
      sourceWorkOrderId: state.workOrder.id,
      tags: ['Generator', 'Draft from work order'],
      versions: [{ id: 'version-1', versionNumber: 1, title: `Draft from ${state.workOrder.workOrderNumber} - ${state.workOrder.title}`, body: 'Draft body', createdAt: '2026-05-16T10:00:00.000Z' }],
      relatedArticles: [],
    }
    state.articles.push(article)
    await route.fulfill(json(article, 201))
  })

  await page.route(`${API}/api/knowledge/articles/*/publish`, async (route) => {
    if (handleOptions(route)) return
    const id = route.request().url().split('/articles/')[1].split('/publish')[0]
    const article = state.articles.find((item) => item.id === id)!
    article.status = 'Published'
    article.publishedAt = '2026-05-16T10:05:00.000Z'
    article.updatedAt = article.publishedAt
    await route.fulfill(json(article))
  })

  await page.route(`${API}/api/knowledge/articles/*/archive`, async (route) => {
    if (handleOptions(route)) return
    const id = route.request().url().split('/articles/')[1].split('/archive')[0]
    const article = state.articles.find((item) => item.id === id)!
    article.status = 'Archived'
    await route.fulfill(json(article))
  })

  await page.route(`${API}/api/knowledge/articles`, async (route) => {
    if (handleOptions(route)) return

    if (route.request().method() === 'GET') {
      const url = new URL(route.request().url())
      const q = (url.searchParams.get('q') || '').toLowerCase()
      const status = url.searchParams.get('status')
      const categoryId = url.searchParams.get('categoryId')
      const results = state.articles.filter((article) => {
        const matchesQ = !q || `${article.title} ${article.summary || ''} ${article.body} ${article.tags.join(' ')}`.toLowerCase().includes(q)
        const matchesStatus = !status || article.status === status
        const matchesCategory = !categoryId || article.categoryId === categoryId
        return matchesQ && matchesStatus && matchesCategory
      }).map(toListItem)
      await route.fulfill(json(results))
      return
    }

    const body = route.request().postDataJSON() as Record<string, unknown>
    const category = state.categories.find((item) => item.id === body.categoryId)
    const article = {
      id: `article-${state.articles.length + 1}`,
      title: String(body.title),
      slug: String(body.title).toLowerCase().replace(/\s+/g, '-'),
      summary: String(body.summary ?? '') || null,
      body: String(body.body),
      categoryId: String(body.categoryId || '') || null,
      categoryName: category?.name ?? null,
      status: String(body.status),
      visibility: String(body.visibility),
      createdAt: '2026-05-16T09:30:00.000Z',
      updatedAt: '2026-05-16T09:30:00.000Z',
      publishedAt: null,
      sourceWorkOrderId: null,
      tags: ((body.tags ?? []) as string[]).map(String),
      versions: [{ id: 'version-1', versionNumber: 1, title: String(body.title), body: String(body.body), createdAt: '2026-05-16T09:30:00.000Z' }],
      relatedArticles: [],
    }
    state.articles.push(article)
    await route.fulfill(json(article, 201))
  })

  await page.route(`${API}/api/knowledge/articles/*/related`, async (route) => {
    if (handleOptions(route)) return
    const id = route.request().url().split('/articles/')[1].split('/related')[0]
    const related = state.articles.filter((item) => item.id !== id && item.status === 'Published').map(toListItem)
    await route.fulfill(json(related))
  })

  await page.route(`${API}/api/knowledge/articles/*`, async (route) => {
    if (handleOptions(route)) return
    const id = route.request().url().split('/articles/')[1]
    const article = state.articles.find((item) => item.id === id)!

    if (route.request().method() === 'GET') {
      article.relatedArticles = state.articles.filter((item) => item.id !== id && item.status === 'Published').map(toListItem)
      await route.fulfill(json(article))
      return
    }

    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as Record<string, unknown>
      const category = state.categories.find((item) => item.id === body.categoryId)
      article.title = String(body.title)
      article.summary = String(body.summary ?? '') || null
      article.body = String(body.body)
      article.categoryId = String(body.categoryId || '') || null
      article.categoryName = category?.name ?? null
      article.status = String(body.status)
      article.visibility = String(body.visibility)
      article.tags = ((body.tags ?? []) as string[]).map(String)
      article.updatedAt = '2026-05-16T10:10:00.000Z'
      article.versions.unshift({ id: `version-${article.versions.length + 1}`, versionNumber: article.versions.length + 1, title: article.title, body: article.body, createdAt: article.updatedAt })
      await route.fulfill(json(article))
      return
    }

    state.articles.splice(state.articles.findIndex((item) => item.id === id), 1)
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
  })

  await page.route(`${API}/api/knowledge/search**`, async (route) => {
    if (handleOptions(route)) return
    const url = new URL(route.request().url())
    const q = (url.searchParams.get('q') || '').toLowerCase()
    const results = state.articles.filter((article) => article.status === 'Published' && `${article.title} ${article.summary || ''} ${article.body} ${article.tags.join(' ')}`.toLowerCase().includes(q)).map(toListItem)
    await route.fulfill(json(results))
  })

  await page.route(`${API}/api/knowledge/suggestions/work-order/${state.workOrder.id}`, async (route) => {
    if (handleOptions(route)) return
    const results = state.articles.filter((article) => article.status === 'Published').map(toListItem)
    await route.fulfill(json(results))
  })
}

async function mockWorkOrderDetailApi(page: import('@playwright/test').Page, state: ReturnType<typeof createState>) {
  await page.route(`${API}/api/workorders/${state.workOrder.id}`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json(state.workOrder))
  })
  await page.route(`${API}/api/workorders/${state.workOrder.id}/execution`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json({
      notes: { findings: state.workOrder.jobCardNotes, workDone: state.workOrder.workDoneNotes },
      photos: [],
      materialUsages: [],
      signatures: [],
      progressUpdates: [],
      reportPreview: {
        companyName: 'Acme Facilities',
        workOrderNumber: state.workOrder.workOrderNumber,
        title: state.workOrder.title,
        clientName: state.workOrder.clientName,
        siteLabel: state.workOrder.siteName,
        assetName: state.workOrder.assetName,
        generatedAtLabel: '2026-05-16 10:30 UTC',
        reportedProblem: state.workOrder.description,
        findings: state.workOrder.jobCardNotes,
        workDone: state.workOrder.workDoneNotes,
        technicianTeam: state.workOrder.leadTechnicianName,
        timestamps: [],
        materials: [],
        photoGroups: [],
        signatures: [],
        showPoweredByEcosys: true,
      },
    }))
  })
  await page.route(`${API}/api/materials**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/assignment-groups`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/technicians**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/material-requests**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/workorders/${state.workOrder.id}/events`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/workorders/${state.workOrder.id}/assignment-history`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/assignment-groups**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
  await page.route(`${API}/api/pm/templates**`, async (route) => {
    if (handleOptions(route)) return
    await route.fulfill(json([]))
  })
}

function handleOptions(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    void route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }

  return false
}

function toListItem(article: ReturnType<typeof createState>['articles'][number]) {
  return {
    id: article.id,
    title: article.title,
    slug: article.slug,
    summary: article.summary,
    categoryId: article.categoryId,
    categoryName: article.categoryName,
    status: article.status,
    visibility: article.visibility,
    publishedAt: article.publishedAt,
    updatedAt: article.updatedAt || article.createdAt,
    tags: article.tags,
  }
}
