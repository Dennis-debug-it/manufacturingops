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
  }, data)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    if (route.request().method() === 'OPTIONS') { await route.fulfill({ status: 204, headers }); return }
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) { await route.continue(); return }
    const body = path === '/api/auth/me' ? data
      : path === '/api/tenant/license' ? { modules, planCode: modules.length === 2 ? 'suite' : modules[0], planName: 'ServiceOps ' + (modules.length === 2 ? 'Suite' : modules[0] === 'finance' ? 'Finance' : 'Operations') }
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
  await page.getByRole('radio', { name: 'ServiceOps Finance', exact: true }).check()
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
  expect((await request).postDataJSON()).toEqual({ companyName: 'Acme Engineering Limited', slug: 'acme', planCode: 'finance', adminFullName: 'John Kamau', adminEmail: 'john@acme.test', country: 'Nigeria', timeZoneId: 'Africa/Nairobi' })
})

for (const modules of [['operations'], ['finance'], ['operations', 'finance']]) {
  test(`welcome actions respect ${modules.join('+')} entitlement`, async ({ page }) => {
    await session(page, 'admin', modules)
    await page.goto('/onboarding')
    await expect(page.getByRole('heading', { name: 'Welcome to Ecosys ServiceOps' })).toBeVisible()
    await expect(page.getByText('acme@serviceops.ecosysdigital.co.ke', { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Add Customer', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'Create Work Order', exact: true })).toHaveCount(modules.includes('operations') ? 1 : 0)
    await expect(page.getByRole('button', { name: 'Create Invoice', exact: true })).toHaveCount(modules.includes('finance') ? 1 : 0)
    await expect(page.getByRole('link', { name: 'Go to Dashboard', exact: true })).toHaveAttribute('href', modules.includes('operations') ? '/dashboard' : '/finance')
    await expect(page.getByText('Required Steps')).toHaveCount(0)
  })
}

for (const status of ['EXPIRED', 'SUPERSEDED', 'REVOKED', 'INVALID', 'USED']) {
  test(`invitation ${status} comes from server and offers appropriate recovery`, async ({ page }) => {
    await page.route('**/api/auth/validate-invitation', (route) => route.fulfill(respond({ status })))
    await page.route('**/api/auth/resend-invitation', (route) => route.fulfill(respond({ message: 'If a pending ServiceOps invitation exists for this email, a new invitation has been sent.' })))
    await page.goto('/reset-password?mode=invite&token=opaque-token')
    await expect(page.getByLabel('New password', { exact: true })).toHaveCount(0)
    await expect(page.getByRole('link', { name: 'Back to sign in' })).toBeVisible()
    if (status === 'USED') {
      await expect(page.getByText(/already been used/)).toBeVisible()
      await expect(page.getByRole('button', { name: 'Request new invitation' })).toHaveCount(0)
    } else {
      await page.getByLabel('Your email address').fill('john@acme.test')
      await page.getByRole('button', { name: 'Request new invitation' }).click()
      await expect(page.getByRole('status')).toContainText('If a pending ServiceOps invitation exists')
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
  const identity = { emailAddress: 'acme@serviceops.ecosysdigital.co.ke', displayName: 'Acme', replyToAddress: null, emailSignature: null, inboundEnabled: true, outboundEnabled: true }
  await page.route('**/api/tenant/email-identity', (route) => route.fulfill(respond(identity)))
  await page.goto('/settings/email')
  await expect(page.getByText(identity.emailAddress, { exact: true })).toBeVisible()
  await expect(page.getByLabel('SMTP Host')).toHaveCount(0)
  await expect(page.getByRole('textbox')).toHaveCount(3)
  await page.getByLabel('Display Name').fill('Acme Service Team')
  await page.getByLabel('Reply-to Address').fill('replies@acme.test')
  const request = page.waitForRequest((req) => req.method() === 'PUT' && new URL(req.url()).pathname === '/api/tenant/email-identity')
  await page.getByRole('button', { name: 'Save Changes' }).click()
  expect((await request).postDataJSON()).toEqual({ displayName: 'Acme Service Team', replyToAddress: 'replies@acme.test', emailSignature: null, inboundEnabled: true, outboundEnabled: true })
})
