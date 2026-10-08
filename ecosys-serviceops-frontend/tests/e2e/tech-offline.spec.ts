import { expect, test, type Page, type Request } from '@playwright/test'
import { CORS_HEADERS, json } from './helpers/auth'
import { E2E_API_URL } from './helpers/test-data'
import { makeScenario, mockTechnicianWorkOrder } from './helpers/workOrderMocks'

const API = E2E_API_URL

/** Writes the page sends to the API, with the headers the offline queue adds. */
function recordWrites(page: Page) {
  const writes: Array<{ method: string; path: string; idempotencyKey: string | null; occurredAt: string | null; at: number }> = []
  page.on('request', (request: Request) => {
    if (!request.url().startsWith(API) || ['GET', 'OPTIONS'].includes(request.method())) return
    const headers = request.headers()
    writes.push({
      method: request.method(),
      path: new URL(request.url()).pathname,
      idempotencyKey: headers['idempotency-key'] ?? null,
      occurredAt: headers['x-occurred-at'] ?? null,
      at: Date.now(),
    })
  })
  return writes
}

test.use({ viewport: { width: 412, height: 915 } })

test.describe('technician app offline', () => {
  test('job actions taken offline show at once and sync in order, dated when they happened', async ({ page, context }) => {
    const state = makeScenario({ status: 'Assigned' })
    await mockTechnicianWorkOrder(page, state)
    const writes = recordWrites(page)

    await page.goto(`/tech/jobs/${state.workOrder.id}`)
    const bar = page.getByTestId('tech-bottom-bar')
    await expect(bar.getByRole('button', { name: 'Start Work' })).toBeVisible()

    await context.setOffline(true)
    await expect(page.getByTestId('offline-status')).toHaveText('Offline')

    await bar.getByRole('button', { name: 'Start Work' }).click()
    await expect(page.getByText(/Job updated\. Saved on this phone/)).toBeVisible()
    await expect(bar.getByRole('button', { name: 'Complete', exact: true })).toBeVisible()
    await expect(page.getByTestId('job-waiting-to-sync')).toBeVisible()

    await page.getByPlaceholder('Day 1: Inspected generator, found faulty AVR, awaiting replacement.').fill('Replaced the AVR.')
    await page.getByRole('button', { name: /save update/i }).click()
    await expect(page.getByText('Replaced the AVR.')).toBeVisible()

    await page.getByRole('link', { name: 'Finish', exact: true }).click()
    await page.getByLabel(/work done/i).fill('Replaced the AVR and load-tested the generator.')
    await bar.getByRole('button', { name: 'Complete', exact: true }).click()
    await expect(page.getByTestId('offline-status')).toHaveText('Offline · 3 waiting')
    await expect(bar).toHaveCount(0)

    await page.getByTestId('offline-status').click()
    await expect(page.getByTestId('outbox-entry')).toHaveCount(3)
    await expect(page.getByTestId('outbox-entry').first()).toContainText('Started work · WO-2026-0042')
    await page.getByRole('button', { name: 'Close', exact: true }).click()

    // Nothing left the phone while offline.
    expect(writes).toHaveLength(0)
    const backOnline = Date.now()
    await context.setOffline(false)

    await expect(page.getByTestId('offline-status')).toHaveCount(0)
    expect(writes.map((write) => `${write.method} ${write.path}`)).toEqual([
      `POST /api/workorders/${state.workOrder.id}/start`,
      `POST /api/workorders/${state.workOrder.id}/execution-notes`,
      `POST /api/workorders/${state.workOrder.id}/complete`,
    ])
    for (const write of writes) {
      expect(write.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/)
      expect(new Date(write.occurredAt ?? '').getTime()).toBeLessThan(backOnline)
    }
    expect(new Set(writes.map((write) => write.idempotencyKey)).size).toBe(3)
    expect(state.requests.find((request) => request.path === '/complete')?.body).toEqual({ workDoneNotes: 'Replaced the AVR and load-tested the generator.' })
    await expect(page.getByText('Completed. An administrator will review and close it.')).toBeVisible()
  })

  test('a photo taken offline is kept on the phone and uploaded when back online', async ({ page, context }) => {
    const state = makeScenario({ status: 'In Progress', workStartedAt: '2026-05-13T08:45:00.000Z' })
    await mockTechnicianWorkOrder(page, state)
    let uploaded: { contentType: string; body: string } | null = null
    await page.route(`${API}/api/workorders/${state.workOrder.id}/photos`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      uploaded = { contentType: route.request().headers()['content-type'] ?? '', body: route.request().postDataBuffer()?.toString('latin1') ?? '' }
      return route.fulfill(json(state.execution))
    })
    await page.goto(`/tech/jobs/${state.workOrder.id}/photos`)
    await expect(page.getByText('No photos yet.')).toBeVisible()

    await context.setOffline(true)
    await page.getByTestId('browse-evidence-control').locator('input[type="file"]').setInputFiles({ name: 'avr.jpg', mimeType: 'image/jpeg', buffer: Buffer.from('fake-jpeg-bytes') })
    await page.getByLabel('Photo caption').fill('Burnt AVR')
    await page.getByRole('button', { name: /upload evidence/i }).click()
    await expect(page.getByText(/Evidence uploaded\. Saved on this phone/)).toBeVisible()
    await expect(page.getByText('During · waiting to upload')).toBeVisible()
    expect(uploaded).toBeNull()

    await context.setOffline(false)
    await expect(page.getByTestId('offline-status')).toHaveCount(0)
    expect(uploaded!.contentType).toContain('multipart/form-data')
    expect(uploaded!.body).toContain('fake-jpeg-bytes')
    expect(uploaded!.body).toContain('Burnt AVR')
  })

  test('clock in offline, and an action the server refuses is listed with its reason', async ({ page, context }) => {
    const state = makeScenario({ status: 'Assigned' })
    await mockTechnicianWorkOrder(page, state)
    await page.route(`${API}/api/attendance/me/status`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json({ technicianId: 'tech-1', technicianName: 'Moses Otieno', status: 'NotClockedIn', clockInAt: null, lastLocation: null }))
    })
    await page.route(`${API}/api/attendance/me/clock-in`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      // The office clocked them in while they were offline.
      return route.fulfill(json({ message: 'You are already clocked in.', errorCode: 'BUSINESS_RULE' }, 400))
    })

    await page.route(`${API}/api/attendance/timesheets/mine**`, async (route) => {
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS_HEADERS })
      return route.fulfill(json({ periodLabel: 'This week', totalHours: 0, overtimeHours: 0, leaveHours: 0, dailyBreakdown: [] }))
    })
    await page.goto('/tech/time')
    await expect(page.getByRole('button', { name: /clock in/i })).toBeVisible()
    await context.setOffline(true)
    await page.getByRole('button', { name: /clock in/i }).click()
    await expect(page.getByText(/Clocked in\. Saved on this phone/)).toBeVisible()
    await expect(page.getByText('Clocked in', { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: /clock out/i })).toBeVisible()

    await context.setOffline(false)
    const status = page.getByTestId('offline-status')
    await expect(status).toHaveText("1 didn't sync")
    await status.click()
    await expect(page.getByTestId('outbox-entry')).toContainText('You are already clocked in.')
    await page.getByRole('button', { name: 'Discard' }).click()
    // Nothing left on the phone: the indicator goes away.
    await expect(status).toHaveCount(0)
  })

  test('the app starts offline with the stored session and jobs', async ({ page, context }) => {
    const state = makeScenario({ status: 'Assigned' })
    await mockTechnicianWorkOrder(page, state)
    await page.goto('/tech')
    await expect(page.getByText('WO-2026-0042')).toBeVisible()
    // Give the stored copy a moment to be written (throttled to once a second).
    await page.waitForTimeout(1500)

    // Offline start-up: the "who am I" check fails, the job feed fails; the stored copies are used.
    await page.route(`${API}/api/**`, (route) => route.abort('internetdisconnected'))
    await page.reload()
    await context.setOffline(true)
    await expect(page.getByTestId('tech-jobs-page')).toBeVisible()
    await expect(page.getByText('WO-2026-0042')).toBeVisible()
    await page.getByText('WO-2026-0042').click()
    await expect(page.getByTestId('tech-bottom-bar').getByRole('button', { name: 'Start Work' })).toBeVisible()
    await context.setOffline(false)
  })
})
