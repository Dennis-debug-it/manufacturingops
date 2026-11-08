import { expect, test, type Page } from '@playwright/test'
import { E2E_API_URL } from './helpers/test-data'

/**
 * Tenant management UI (audit remediation, phases 2 and 5): seats are visible and editable, lifecycle
 * actions follow the tenant's real state, invitation state is visible, impersonation actually opens
 * a session. API responses are mocked; the backend rules are covered by the .NET suite.
 */

const headers = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Methods': '*', 'Access-Control-Allow-Headers': '*' }
const respond = (body: unknown, status = 200) => ({ status, contentType: 'application/json', headers, body: JSON.stringify(body) })

type TenantState = { status: string; licenseStatus: string; suspensionReason: string | null; seatLimit: number | null; fieldSeats?: number }

function tenantDetail(state: TenantState) {
  return {
    tenantId: 't1', name: 'Acme Engineering', slug: 'acme', contactName: 'John Kamau', contactEmail: 'john@acme.test',
    contactPhone: null, country: 'Kenya', county: null, city: null, address: null, taxPin: null,
    status: state.status, planName: 'Ecosys Factory Maintenance Professional', licenseStatus: state.licenseStatus,
    maxUsers: state.seatLimit, maxBranches: null, trialStartsAt: null, trialEndsAt: null, trialExtensionUsed: true,
    trialExtendedAt: null, trialDaysRemaining: null, trialStatus: 'PaidActive', subscriptionStartsAt: '2026-01-01T00:00:00Z',
    subscriptionEndsAt: '2027-01-01T00:00:00Z', createdAt: '2026-01-01T00:00:00Z', updatedAt: null, suspendedAt: null,
    deactivatedAt: null, createdByUserId: null, updatedByUserId: null, userCount: 3, branchCount: 1, workOrderCount: 12,
    activeUsersNow: 0, lastActivityAt: null, initialAdminInvitationSent: null, initialAdminInvitationMessage: null,
    timeZoneId: 'Africa/Nairobi', currency: 'KES', serviceOpsEmail: 'acme@serviceops.ecosysdigital.co.ke',
    seatsUsed: 3, seatLimit: state.seatLimit, modules: ['operations', 'finance', 'maintenance', 'inventory', 'advanced'], planCode: 'professional',
    officeSeats: state.seatLimit, fieldSeats: state.fieldSeats ?? 0, officeSeatsUsed: 1, fieldSeatsUsed: 0, whatsAppAddOn: null,
    licenseExpiresAt: '2027-01-01T00:00:00Z', isPerpetual: false, suspensionReason: state.suspensionReason,
  }
}

const users = [
  { id: 'u1', fullName: 'John Kamau', email: 'john@acme.test', role: 'Admin', isActive: true, jobTitle: null, createdAt: '2026-01-01T00:00:00Z', lockedOutUntil: null, mustChangePassword: false, lastCredentialSentAt: '2026-01-01T00:00:00Z', lastLoginAt: '2026-02-01T00:00:00Z', invitationStatus: 'Active', invitationExpiresAt: null },
  { id: 'u2', fullName: 'Mary Wanjiru', email: 'mary@acme.test', role: 'Viewer', isActive: true, jobTitle: null, createdAt: '2026-01-01T00:00:00Z', lockedOutUntil: null, mustChangePassword: true, lastCredentialSentAt: null, lastLoginAt: null, invitationStatus: 'NotSent', invitationExpiresAt: '2026-12-01T00:00:00Z' },
  { id: 'u3', fullName: 'Peter Otieno', email: 'peter@acme.test', role: 'Viewer', isActive: true, jobTitle: null, createdAt: '2026-01-01T00:00:00Z', lockedOutUntil: null, mustChangePassword: true, lastCredentialSentAt: '2026-01-02T00:00:00Z', lastLoginAt: null, invitationStatus: 'Expired', invitationExpiresAt: '2026-01-05T00:00:00Z' },
]

async function platformSession(page: Page, state: TenantState) {
  const data = {
    token: 'test-token', branches: [],
    user: { userId: 'owner-1', role: 'super_admin', fullName: 'Lena Atieno', email: 'owner@ecosys.test', permissions: {}, mustChangePassword: false },
    tenant: { tenantId: 'root', companyName: 'Ecosys Platform', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
  }
  await page.addInitScript((value) => {
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
    localStorage.setItem(`ecosys.first-login-shown.${value.user.userId}`, '1')
  }, data)

  const calls: { method: string; path: string; body: unknown }[] = []
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    const request = route.request()
    if (request.method() === 'OPTIONS') { await route.fulfill({ status: 204, headers }); return }
    const path = new URL(request.url()).pathname
    calls.push({ method: request.method(), path, body: request.postData() ? request.postDataJSON() : null })

    if (path === '/api/auth/me') return route.fulfill(respond(data))
    if (path === '/api/platform/tenants' && request.method() === 'GET') {
      const detail = tenantDetail(state)
      return route.fulfill(respond([{ ...detail, primaryAdminEmail: 'john@acme.test' }]))
    }
    if (path === '/api/platform/tenants/t1') return route.fulfill(respond(tenantDetail(state)))
    if (path === '/api/platform/tenants/t1/users') return route.fulfill(respond(users))
    if (path === '/api/platform/tenants/t1/license/seats') {
      const seats = request.postDataJSON() as { officeSeats: number; fieldSeats: number }
      Object.assign(state, { seatLimit: seats.officeSeats, fieldSeats: seats.fieldSeats })
      return route.fulfill(respond(tenantDetail(state)))
    }
    if (path === '/api/platform/tenants/t1/license/reactivate') {
      Object.assign(state, { status: 'Active', licenseStatus: 'Active', suspensionReason: null })
      return route.fulfill(respond(tenantDetail(state)))
    }
    if (path === '/api/platform/tenants/t1/impersonate') {
      return route.fulfill(respond({
        token: 'impersonation-token',
        user: { userId: 'u1', fullName: 'John Kamau', email: 'john@acme.test', role: 'Admin', jobTitle: null, permissions: {}, mustChangePassword: false },
        tenant: { tenantId: 't1', companyName: 'Acme Engineering', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
        impersonatedByUserId: 'owner-1', impersonatedByName: 'Lena Atieno', startedAt: '2026-09-24T10:00:00Z',
      }))
    }
    if (path === '/api/platform/tenants/t1/communication-settings') {
      return route.fulfill(respond({ emailSettings: { senderName: 'Acme', senderEmail: 'acme@serviceops.ecosysdigital.co.ke', replyToEmail: null, enableTenantEmailNotifications: true }, recipients: [] }))
    }
    if (path === '/api/platform/tenants/t1/finance-summary') return route.fulfill(respond({ invoiceCount: 0, totalInvoiced: 0, totalPaid: 0, outstandingBalance: 0, overdueCount: 0 }))
    if (path === '/api/platform/whatsapp/tenants/t1') return route.fulfill(respond({ tenantId: 't1', enabled: false, usedThisMonth: 0, monthlyCap: 1000, configured: true, addOn: null }))
    if (path.startsWith('/api/platform/tenants/t1/')) return route.fulfill(respond([]))
    return route.fulfill(respond({}))
  })
  return calls
}

test('tenant list shows plan modules, licensed users and the primary admin, with lifecycle on the tenant page', async ({ page }) => {
  await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 20 })
  await page.goto('/platform/tenants')
  const row = page.getByRole('row', { name: /Acme Engineering/ })
  await expect(row).toContainText('3 / 20')
  await expect(row).toContainText('20 paid seats')
  await expect(row).toContainText('john@acme.test')
  await expect(row.getByRole('button', { name: 'Suspend' })).toHaveCount(0)
  await expect(row.getByRole('link', { name: 'Manage' })).toHaveAttribute('href', '/platform/tenants/t1')
})

test('create tenant requires office seats before submitting', async ({ page }) => {
  const calls = await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 20 })
  await page.goto('/platform/tenants')
  await page.getByRole('button', { name: 'Create tenant', exact: true }).click()
  await page.getByLabel('Company Name', { exact: true }).fill('Beta Ltd')
  await page.getByLabel('Primary Admin Name', { exact: true }).fill('Beta Admin')
  await page.getByLabel('Primary Admin Email', { exact: true }).fill('admin@beta.test')
  await page.getByLabel('Office Seats', { exact: true }).fill('')
  await page.getByRole('button', { name: 'Create tenant', exact: true }).last().click()
  await expect(page.getByText('Office seats are required.')).toBeVisible()
  expect(calls.some((call) => call.method === 'POST' && call.path === '/api/platform/tenants')).toBe(false)
})

test('seats are shown as used / limit and can be edited without deactivating anyone', async ({ page }) => {
  const calls = await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 5 })
  await page.goto('/platform/tenants/t1')
  await page.getByRole('button', { name: 'Users', exact: true }).click()
  // One admin takes an office seat; the two viewers are free.
  await expect(page.getByTestId('tenant-seats-office')).toContainText('1 / 5')
  await expect(page.getByTestId('tenant-seats-field')).toContainText('0 / 0')
  await page.getByRole('button', { name: 'Edit seats' }).first().click()
  await page.getByLabel('Office seats').fill('3')
  await page.getByLabel('Field seats').fill('-1')
  await expect(page.getByText('Field seats must be a whole number of 0 or more.')).toBeVisible()
  await page.getByLabel('Field seats').fill('4')
  await page.getByRole('button', { name: 'Save', exact: true }).click()
  await expect(page.getByText('Seats updated')).toBeVisible()
  expect(calls.find((call) => call.path === '/api/platform/tenants/t1/license/seats')?.body).toEqual({ officeSeats: 3, fieldSeats: 4 })
})

test('workspace users show whether their invitation was delivered, expired or completed', async ({ page }) => {
  await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 5 })
  await page.goto('/platform/tenants/t1')
  await page.getByRole('button', { name: 'Users', exact: true }).click()
  await expect(page.getByRole('row', { name: /Mary Wanjiru/ })).toContainText('Invitation not delivered')
  await expect(page.getByRole('row', { name: /Peter Otieno/ })).toContainText('Setup link expired')
  await expect(page.getByRole('row', { name: /John Kamau/ })).toContainText('Set up')
})

test('an unpaid (non-payment) suspension offers Reactivate, not Suspend', async ({ page }) => {
  const calls = await platformSession(page, { status: 'Suspended', licenseStatus: 'Suspended', suspensionReason: 'NonPayment', seatLimit: 5 })
  await page.goto('/platform/tenants/t1')
  await page.getByRole('button', { name: 'Subscription', exact: true }).click()
  await expect(page.getByText('Unpaid subscription (automatic)')).toBeVisible()
  const actions = page.getByTestId('tenant-lifecycle-actions')
  await expect(actions.getByRole('button', { name: 'Suspend' })).toHaveCount(0)
  await expect(actions.getByRole('button', { name: 'Extend trial' })).toHaveCount(0)
  await actions.getByRole('button', { name: 'Reactivate' }).click()
  await expect(page.getByText('Tenant reactivated')).toBeVisible()
  expect(calls.some((call) => call.method === 'POST' && call.path === '/api/platform/tenants/t1/license/reactivate')).toBe(true)
})

test('suspending requires a reason that is sent to the API', async ({ page }) => {
  const calls = await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 5 })
  await page.goto('/platform/tenants/t1')
  await page.getByRole('button', { name: 'Subscription', exact: true }).click()
  await page.getByTestId('tenant-lifecycle-actions').getByRole('button', { name: 'Suspend' }).click()
  const confirm = page.getByRole('button', { name: 'Suspend', exact: true }).last()
  await expect(confirm).toBeDisabled()
  await page.getByPlaceholder('Why is this tenant being suspended?').fill('Invoice dispute INV-204')
  await confirm.click()
  await expect.poll(() => calls.find((call) => call.path === '/api/platform/tenants/t1/suspend')?.body).toEqual({ reason: 'Invoice dispute INV-204' })
})

test('impersonation asks for a reason and opens the tenant workspace in a new tab', async ({ page, context }) => {
  const calls = await platformSession(page, { status: 'Active', licenseStatus: 'Active', suspensionReason: null, seatLimit: 5 })
  await page.goto('/platform/tenants/t1')
  await page.getByRole('button', { name: 'Support & danger zone', exact: true }).click()
  await page.getByRole('button', { name: 'Impersonate admin' }).click()
  await page.getByPlaceholder(/Support ticket/).fill('Ticket 88: invoice layout')
  const newTab = context.waitForEvent('page')
  await page.getByRole('button', { name: 'Start session' }).click()
  const impersonationTab = await newTab
  await expect(impersonationTab).toHaveURL(/\/impersonate-entry#/)
  expect(calls.find((call) => call.path === '/api/platform/tenants/t1/impersonate')?.body).toEqual({ reason: 'Ticket 88: invoice layout' })
})
