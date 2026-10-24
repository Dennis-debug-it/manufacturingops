import { expect, test } from '@playwright/test'
import { E2E_API_URL } from './helpers/test-data'

test('tenant delivery logs screen exposes safe filters and retry-ready empty state', async ({ page }) => {
  const session = {
    token: 'delivery-log-test-token',
    user: {
      userId: 'delivery-log-admin',
      fullName: 'Delivery Log Admin',
      email: 'admin@example.test',
      role: 'admin',
      mustChangePassword: false,
      permissions: { canManageSettings: true, canManageFinance: true, canViewFinance: true },
      hasAllBranchAccess: true,
    },
    tenant: { tenantId: 'delivery-log-tenant', companyName: 'Delivery Log Tenant', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
    branches: [],
  }

  await page.addInitScript((value) => {
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
  }, session)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: { 'Access-Control-Allow-Origin': '*' } })
      return
    }
    const path = new URL(route.request().url()).pathname
    const body = path === '/api/auth/me'
      ? session
      : path === '/api/tenant/license'
        ? { planCode: 'professional', planName: 'ServiceOps Professional', modules: ['operations', 'finance', 'maintenance', 'inventory', 'projects', 'advanced'] }
        : []
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } })
  })

  await page.goto('/settings/email/delivery-logs')
  await expect(page.getByRole('heading', { name: 'Email Delivery Logs' })).toBeVisible()
  await expect(page.getByRole('combobox').nth(0)).toHaveValue('')
  await expect(page.getByRole('combobox').nth(1)).toHaveValue('')
  await expect(page.getByLabel('From date')).toBeVisible()
  await expect(page.getByLabel('To date')).toBeVisible()
  await expect(page.getByText('No delivery logs')).toBeVisible()
})

test('platform user actions distinguish initial invite from password reset', async ({ page }) => {
  page.on('pageerror', (error) => console.log(`PAGEERROR: ${error.message}`))
  const session = {
    token: 'platform-user-test-token',
    user: { userId: 'platform-admin', fullName: 'Platform Admin', email: 'admin@platform.test', role: 'super_admin', mustChangePassword: false, permissions: {} },
    tenant: { tenantId: 'root', companyName: 'Ecosys Platform', country: 'Kenya', timeZoneId: 'Africa/Nairobi' },
    branches: [],
  }
  const users = [
    { id: 'pending-user', fullName: 'Pending User', email: 'pending@example.test', phone: null, role: 'PlatformAdmin', status: 'Active', mustChangePassword: true, lastLoginAt: null, lastCredentialSentAt: null },
    { id: 'active-user', fullName: 'Active User', email: 'active@example.test', phone: null, role: 'PlatformAdmin', status: 'Active', mustChangePassword: false, lastLoginAt: null, lastCredentialSentAt: null },
  ]
  await page.addInitScript((value) => {
    localStorage.setItem('ecosys.serviceops.session', JSON.stringify(value))
    localStorage.setItem('ecosys.serviceops.token', value.token)
  }, session)
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: { 'Access-Control-Allow-Origin': '*' } })
      return
    }
    const path = new URL(route.request().url()).pathname
    const body = path === '/api/auth/me' ? session : path === '/api/platform/users' ? users : []
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body), headers: { 'Access-Control-Allow-Origin': '*' } })
  })

  await page.goto('/platform/users')
  await expect(page.getByRole('button', { name: 'Resend Initial Invite' })).toHaveCount(1)
  await expect(page.getByRole('button', { name: 'Send Password Reset' })).toHaveCount(1)
})
