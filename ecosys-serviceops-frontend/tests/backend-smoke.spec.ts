import { expect, test, type APIRequestContext } from '@playwright/test'
import { authenticatedApiCall, expectNoServerError } from './helpers/api'
import { loginViaApi, signupViaApi } from './helpers/auth'
import {
  DEFAULT_SIGNUP_PASSWORD,
  E2E_API_BASE_URL,
  SUPERADMIN_EMAIL,
  SUPERADMIN_PASSWORD,
  TENANT_EMAIL,
  TENANT_PASSWORD,
} from './helpers/env'
import { buildSignupData } from './helpers/random'

async function getTenantToken(request: APIRequestContext) {
  if (TENANT_EMAIL && TENANT_PASSWORD) {
    return loginViaApi(request, E2E_API_BASE_URL, {
      email: TENANT_EMAIL,
      password: TENANT_PASSWORD,
    })
  }

  const signupData = buildSignupData()
  signupData.password = DEFAULT_SIGNUP_PASSWORD
  return signupViaApi(request, E2E_API_BASE_URL, signupData)
}

function buildLeadPayload(suffix: string) {
  return {
    companyName: `Lead Prospect ${suffix}`,
    contactPersonName: `Lead Contact ${suffix}`,
    email: `lead.${suffix}@example.com`,
    phone: '+254722000000',
    country: 'Kenya',
    industry: 'Facilities',
    companySize: '50 users',
    message: 'Interested in a guided platform setup.',
    preferredContactMethod: 'Email',
  }
}

test('backend smoke: auth endpoints are reachable', async ({ request }) => {
  const response = await request.post(`${E2E_API_BASE_URL}/api/auth/login`, {
    data: { email: 'invalid@example.com', password: 'invalid' },
  })

  // Invalid credentials are expected, but backend must not crash.
  expect(response.status(), `Unexpected auth/login status: ${response.status()}`).toBeLessThan(500)
})

test('backend smoke: tenant critical endpoints return non-500 responses', async ({ request }) => {
  const token = await getTenantToken(request)
  const paths = [
    '/api/auth/me',
    '/api/dashboard/summary',
    '/api/clients',
    '/api/assets',
    '/api/workorders',
    '/api/materials',
    '/api/assignment-groups',
    '/api/settings/security',
  ]

  for (const path of paths) {
    const response = await authenticatedApiCall(
      request,
      E2E_API_BASE_URL,
      token,
      'GET',
      path,
    )
    await expectNoServerError(response, `Tenant smoke for ${path}`)
    expect(
      response.status(),
      `Authenticated request returned 401 for ${path}`,
    ).not.toBe(401)
  }
})

test('backend smoke: superadmin platform endpoints return non-500 responses', async ({ request }) => {
  test.skip(!SUPERADMIN_EMAIL || !SUPERADMIN_PASSWORD, 'Set E2E_SUPERADMIN_EMAIL and E2E_SUPERADMIN_PASSWORD to run platform backend smoke.')

  const token = await loginViaApi(request, E2E_API_BASE_URL, {
    email: SUPERADMIN_EMAIL as string,
    password: SUPERADMIN_PASSWORD as string,
  })

  const paths = [
    '/api/auth/me',
    '/api/platform/summary',
    '/api/platform/tenants',
    '/api/platform/reports/summary',
    '/api/platform/tenant-licenses',
  ]

  for (const path of paths) {
    const response = await authenticatedApiCall(
      request,
      E2E_API_BASE_URL,
      token,
      'GET',
      path,
    )
    await expectNoServerError(response, `Platform smoke for ${path}`)
    expect(
      response.status(),
      `Authenticated superadmin request returned 401 for ${path}`,
    ).not.toBe(401)
  }
})

test('backend smoke: POST /api/public/leads creates a lead', async ({ request }) => {
  const response = await request.post(`${E2E_API_BASE_URL}/api/public/leads`, {
    data: buildLeadPayload(Date.now().toString()),
  })

  await expectNoServerError(response, 'Public lead submission')
  expect(response.ok(), `Lead submission failed with status ${response.status()}`).toBeTruthy()

  const body = await response.json() as { success?: boolean; message?: string }
  expect(body.success).toBeTruthy()
  expect(body.message || '').toContain('Thank you. We have received your request.')
})

test('backend smoke: platform owner can list leads and update lead status', async ({ request }) => {
  test.skip(!SUPERADMIN_EMAIL || !SUPERADMIN_PASSWORD, 'Set E2E_SUPERADMIN_EMAIL and E2E_SUPERADMIN_PASSWORD to run lead management smoke.')

  const suffix = Date.now().toString()
  const createResponse = await request.post(`${E2E_API_BASE_URL}/api/public/leads`, {
    data: buildLeadPayload(suffix),
  })
  expect(createResponse.ok()).toBeTruthy()

  const token = await loginViaApi(request, E2E_API_BASE_URL, {
    email: SUPERADMIN_EMAIL as string,
    password: SUPERADMIN_PASSWORD as string,
  })

  const listResponse = await authenticatedApiCall(request, E2E_API_BASE_URL, token, 'GET', '/api/platform/leads')
  await expectNoServerError(listResponse, 'Platform leads list')
  expect(listResponse.ok()).toBeTruthy()

  const leads = await listResponse.json() as Array<{ id: string; companyName: string }>
  const createdLead = leads.find((item) => item.companyName === `Lead Prospect ${suffix}`)
  expect(createdLead, 'Expected created lead to appear in platform leads list').toBeTruthy()

  const updateResponse = await authenticatedApiCall(
    request,
    E2E_API_BASE_URL,
    token,
    'PUT',
    `/api/platform/leads/${createdLead?.id}/status`,
    { status: 'Qualified' },
  )
  await expectNoServerError(updateResponse, 'Platform lead status update')
  expect(updateResponse.ok()).toBeTruthy()

  const updated = await updateResponse.json() as { status?: string }
  expect(updated.status).toBe('Qualified')
})

test('backend smoke: public lead submission does not create a tenant', async ({ request }) => {
  test.skip(!SUPERADMIN_EMAIL || !SUPERADMIN_PASSWORD, 'Set E2E_SUPERADMIN_EMAIL and E2E_SUPERADMIN_PASSWORD to verify tenant count stability.')

  const token = await loginViaApi(request, E2E_API_BASE_URL, {
    email: SUPERADMIN_EMAIL as string,
    password: SUPERADMIN_PASSWORD as string,
  })

  const beforeResponse = await authenticatedApiCall(request, E2E_API_BASE_URL, token, 'GET', '/api/platform/tenants')
  expect(beforeResponse.ok()).toBeTruthy()
  const beforeTenants = await beforeResponse.json() as unknown[]

  const createResponse = await request.post(`${E2E_API_BASE_URL}/api/public/leads`, {
    data: buildLeadPayload(`no-tenant-${Date.now()}`),
  })
  expect(createResponse.ok()).toBeTruthy()

  const afterResponse = await authenticatedApiCall(request, E2E_API_BASE_URL, token, 'GET', '/api/platform/tenants')
  expect(afterResponse.ok()).toBeTruthy()
  const afterTenants = await afterResponse.json() as unknown[]

  expect(afterTenants.length).toBe(beforeTenants.length)
})
