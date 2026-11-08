import { expect, test, type Page } from '@playwright/test'
import { formatDate, formatDateTime, setTenantTimeZone } from '../../src/shared/utils/dates'
import { E2E_API_URL } from './helpers/test-data'

const headers = { 'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Methods': '*', 'Access-Control-Allow-Headers': '*' }
const respond = (body: unknown) => ({ status: 200, contentType: 'application/json', headers, body: JSON.stringify(body) })

async function session(page: Page, role = 'admin', modules = ['operations', 'finance']) {
  const data = {
    token: 'test-token', branches: [],
    user: { userId: 'admin-1', role, fullName: 'Primary Admin', email: 'admin@acme.test', permissions: {}, mustChangePassword: false },
    tenant: { tenantId: 'acme', companyName: 'Acme Engineering', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
  }
  await page.addInitScript((value) => {
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
    // The first-login welcome tour is a full-screen overlay; these specs exercise the pages behind it.
    localStorage.setItem(`ecosys.first-login-shown.${value.user.userId}`, '1')
  }, data)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    if (route.request().method() === 'OPTIONS') { await route.fulfill({ status: 204, headers }); return }
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) { await route.continue(); return }
    const body = path === '/api/auth/me' ? data
      : path === '/api/tenant/license' ? { modules, planCode: 'essentials', planName: 'Ecosys Factory Maintenance Essentials' }
      : path === '/api/tenant/profile' ? { serviceOpsEmail: 'acme@serviceops.ecosysdigital.co.ke' }
      : path === '/api/platform/tenants' || path.endsWith('/branches') || path.endsWith('/notifications') ? [] : {}
    await route.fulfill(respond(body))
  })
}

test('tenant creation asks only essential fields and sends stable plan code', async ({ page }) => {
  await session(page, 'super_admin')
  await page.goto('/platform/tenants')
  await page.getByRole('button', { name: 'Create tenant', exact: true }).click()
  await page.getByLabel('Company Name', { exact: true }).fill('Acme Engineering Ltd')
  await expect(page.getByLabel('Tenant Slug', { exact: true })).toHaveValue('acme-engineering')
  await page.getByLabel('Tenant Slug', { exact: true }).fill('acme')
  await page.getByLabel('Company Name', { exact: true }).fill('Acme Engineering Limited')
  await expect(page.getByLabel('Tenant Slug', { exact: true })).toHaveValue('acme')
  await expect(page.getByText('acme@serviceops.ecosysdigital.co.ke', { exact: true })).toBeVisible()
  await page.getByRole('radio', { name: 'Essentials', exact: true }).check()
  await page.getByLabel('Office Seats', { exact: true }).fill('15')
  await page.getByLabel('Field Seats', { exact: true }).fill('4')
  await page.getByLabel('Primary Admin Name', { exact: true }).fill('John Kamau')
  await page.getByLabel('Primary Admin Email', { exact: true }).fill('john@acme.test')
  await expect(page.getByLabel('Timezone', { exact: true })).toHaveValue('Africa/Nairobi')
  await page.getByLabel('Country', { exact: true }).fill('Nigeria')
  await expect(page.getByLabel('Timezone', { exact: true })).toHaveValue('Africa/Lagos')
  await page.getByLabel('Timezone', { exact: true }).fill('Africa/Nairobi')
  await expect(page.getByLabel('Industry', { exact: true })).toHaveCount(0)
  await expect(page.getByLabel('Company Phone', { exact: true })).toHaveCount(0)
  await expect(page.getByText('SMTP', { exact: true })).toHaveCount(0)
  const request = page.waitForRequest((req) => req.method() === 'POST' && new URL(req.url()).pathname === '/api/platform/tenants')
  await page.getByRole('button', { name: 'Create tenant', exact: true }).last().click()
  expect((await request).postDataJSON()).toEqual({ companyName: 'Acme Engineering Limited', slug: 'acme', planCode: 'essentials', officeSeats: 15, fieldSeats: 4, adminFullName: 'John Kamau', adminEmail: 'john@acme.test', country: 'Nigeria', timeZoneId: 'Africa/Nairobi' })
})

for (const modules of [['operations', 'finance', 'maintenance'], ['operations', 'finance']]) {
  test(`welcome checklist respects ${modules.join('+')} entitlement`, async ({ page }) => {
    await session(page, 'admin', modules)
    await page.goto('/onboarding')
    await expect(page.getByRole('heading', { name: 'Welcome to Ecosys Factory Maintenance' })).toBeVisible()
    await expect(page.getByText('acme@serviceops.ecosysdigital.co.ke', { exact: true })).toBeVisible()
    const factorySteps = modules.includes('maintenance') ? 1 : 0
    await expect(page.getByRole('link', { name: /Lay out your factory/ })).toHaveCount(factorySteps)
    await expect(page.getByRole('link', { name: /Add your assets/ })).toHaveCount(factorySteps)
    await expect(page.getByRole('link', { name: /Plan preventive maintenance/ })).toHaveCount(factorySteps)
    await expect(page.getByRole('button', { name: /Invite your team/ })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Go to Dashboard', exact: true })).toHaveAttribute('href', '/dashboard')
    // No service-company actions on a factory workspace.
    await expect(page.getByRole('button', { name: 'Add Customer', exact: true })).toHaveCount(0)
  })
}

for (const status of ['EXPIRED', 'SUPERSEDED', 'REVOKED', 'INVALID', 'USED']) {
  test(`invitation ${status} comes from server and offers appropriate recovery`, async ({ page }) => {
    await page.route('**/api/auth/validate-invitation', (route) => route.fulfill(respond({ status })))
    await page.route('**/api/auth/resend-invitation', (route) => route.fulfill(respond({ message: 'If a pending Ecosys invitation exists for this email, a new invitation has been sent.' })))
    await page.goto('/reset-password?mode=invite&token=opaque-token')
    await expect(page.getByLabel('New password', { exact: true })).toHaveCount(0)
    await expect(page.getByRole('link', { name: 'Back to sign in' })).toBeVisible()
    if (status === 'USED') {
      await expect(page.getByText(/already been used/)).toBeVisible()
      await expect(page.getByRole('button', { name: 'Request new invitation' })).toHaveCount(0)
    } else {
      await page.getByLabel('Your email address').fill('john@acme.test')
      await page.getByRole('button', { name: 'Request new invitation' }).click()
      await expect(page.getByRole('status')).toContainText('If a pending Ecosys invitation exists')
    }
  })
}

test('valid invitation preserves URL-safe token and reaches welcome sign-in', async ({ page }) => {
  const token = 'opaque+slash/value=token'
  let validatedToken: unknown
  await page.route('**/api/auth/validate-invitation', async (route) => {
    validatedToken = route.request().postDataJSON().token
    await route.fulfill(respond({ status: 'VALID' }))
  })
  await page.route('**/api/auth/reset-password', (route) => route.fulfill(respond({ message: 'Account ready' })))
  await page.goto(`/reset-password?mode=invite&token=${encodeURIComponent(token)}`)
  await page.getByLabel('New password', { exact: true }).fill('StrongPassword123!')
  await page.getByLabel('Confirm password', { exact: true }).fill('StrongPassword123!')
  expect(validatedToken).toBe(token)
  await page.getByRole('button', { name: 'Set password', exact: true }).click()
  await expect(page).toHaveURL(/\/login\?welcome=1$/)
})

test('pending and expired users both expose Resend Invite', async ({ page }) => {
  await session(page)
  const users = ['Invited', 'Expired', 'Active'].map((invitationStatus, index) => ({ id: `user-${index}`, fullName: `User ${index}`, email: `user${index}@acme.test`, invitationStatus, isActive: true, mustChangePassword: invitationStatus !== 'Active' }))
  await page.route('**/api/users', (route) => route.fulfill(respond(users)))
  await page.route('**/api/users/*/resend-credentials', (route) => route.fulfill(respond({ success: true })))
  await page.goto('/team/users')
  await expect(page.getByRole('button', { name: 'Resend Invite' })).toHaveCount(2)
  await page.getByRole('button', { name: 'Resend Invite' }).first().click()
  await expect(page.getByText('Invitation sent', { exact: true })).toBeVisible()
})

test('central dates use tenant timezone and preserve date-only calendar dates', () => {
  setTenantTimeZone('Africa/Nairobi')
  expect(formatDateTime('2026-09-05T06:00:00Z')).toContain('09:00')
  expect(formatDateTime('2026-09-05T06:00:00')).toContain('09:00')
  setTenantTimeZone('America/New_York')
  expect(formatDateTime('2026-09-05T06:00:00Z')).toContain('02:00')
  expect(formatDate('2026-09-05')).toBe('05 Sep 2026')
  expect(formatDate('2026-09-05T01:00:00Z')).toBe('04 Sep 2026')
})


test('managed workspace email saves only tenant-facing preferences', async ({ page }) => {
  await session(page, 'admin', ['finance'])
  const identity = { emailAddress: 'acme@serviceops.ecosysdigital.co.ke', displayName: 'Acme', replyToAddress: null, financeReplyToAddress: null, operationsReplyToAddress: null, emailSignature: null, inboundEnabled: true, outboundEnabled: true }
  await page.route('**/api/tenant/email-identity', (route) => route.fulfill(respond(identity)))
  await page.goto('/settings/email')
  await expect(page.getByText(identity.emailAddress, { exact: true })).toBeVisible()
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)
  // Sender name and the three reply-to addresses; mail server fields stay hidden until asked for.
  await expect(page.getByRole('textbox')).toHaveCount(4)
  await page.getByLabel('Sender name').fill('Acme Service Team')
  await page.getByLabel('Replies go to', { exact: true }).fill('replies@acme.test')
  await page.getByLabel('Finance replies go to').fill('accounts@acme.test')
  await page.getByLabel('Operations replies go to').fill('service@acme.test')
  const request = page.waitForRequest((req) => req.method() === 'PUT' && new URL(req.url()).pathname === '/api/tenant/email-identity')
  await page.getByRole('button', { name: 'Save Changes' }).click()
  expect((await request).postDataJSON()).toEqual({ displayName: 'Acme Service Team', replyToAddress: 'replies@acme.test', financeReplyToAddress: 'accounts@acme.test', operationsReplyToAddress: 'service@acme.test', emailSignature: null, inboundEnabled: true, outboundEnabled: true })
})

test('a set-up user shows their role and it can be changed', async ({ page }) => {
  await session(page)
  let user = {
    id: 'user-9', fullName: 'Ken Mwangi', email: 'ken@acme.test', hasEmail: true, username: 'ken', phoneNumber: null,
    role: 'Technician', jobTitle: 'Lift technician', department: null, isActive: true, mustChangePassword: false, invitationStatus: 'Active',
    hasAllBranchAccess: false, defaultBranchId: null, defaultBranchName: null, branchIds: [], assignmentGroups: [],
    permissions: { canViewWorkOrders: true, canCompleteWorkOrders: true }, lastCredentialSentAt: null, createdAt: '2026-10-01T08:00:00Z',
  }
  let saved: Record<string, unknown> | null = null
  await page.route('**/api/users', (route) => route.fulfill(respond([user])))
  // The access panel on the user page lists scopes and groups.
  await page.route('**/api/access/**', (route) => route.fulfill(respond([])))
  await page.route('**/api/users/user-9', async (route) => {
    if (route.request().method() === 'PUT') {
      saved = route.request().postDataJSON() as Record<string, unknown>
      user = { ...user, role: saved.role as string }
    }
    await route.fulfill(respond(user))
  })

  await page.goto('/team/users')
  await expect(page.getByRole('row').filter({ hasText: 'Ken Mwangi' })).toContainText('Technician')

  await page.getByRole('link', { name: 'Ken Mwangi' }).click()
  await page.getByRole('button', { name: 'Change Role' }).click()
  await expect(page.getByLabel('Role')).toBeFocused()
  await page.getByLabel('Role').selectOption('Dispatcher')
  await page.getByRole('button', { name: 'Save Changes' }).click()

  await expect(page.getByText('User details updated')).toBeVisible()
  // A new role starts from that role's default capabilities.
  expect(saved).toMatchObject({ role: 'Dispatcher', permissions: null, isActive: true })
  await expect(page.getByText('Dispatcher · Lift technician')).toBeVisible()
})

test('after accepting an invite, only the admin is taken to workspace setup', async ({ page }) => {
  await session(page, 'Dispatcher')
  const adminOnlyCalls: string[] = []
  page.on('request', (request) => { if (new URL(request.url()).pathname === '/api/tenant/profile') adminOnlyCalls.push(request.url()) })
  await page.goto('/login?welcome=1')
  await expect(page).toHaveURL(/\/dashboard$/)
  // Opening the setup page directly sends a non-admin home too, without calling admin-only services.
  await page.goto('/onboarding')
  await expect(page).toHaveURL(/\/dashboard$/)
  expect(adminOnlyCalls).toEqual([])
})

test('the workspace admin still lands on workspace setup after accepting an invite', async ({ page }) => {
  await session(page, 'admin')
  await page.goto('/login?welcome=1')
  await expect(page).toHaveURL(/\/onboarding$/)
  await expect(page.getByRole('heading', { name: 'Welcome to Ecosys Factory Maintenance' })).toBeVisible()
})
