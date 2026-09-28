import { expect, request, type Page, type Route } from '@playwright/test'
import {
  E2E_API_URL,
  E2E_PLATFORM_EMAIL,
  E2E_PLATFORM_PASSWORD,
  E2E_TENANT_EMAIL,
  E2E_TENANT_PASSWORD,
} from './test-data'

const AUTH_STORAGE_KEY = 'ecosys.serviceops.auth'

type TenantSession = {
  token: string
}

let tenantSessionPromise: Promise<TenantSession> | undefined

export const CORS_HEADERS = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, PATCH, OPTIONS',
  'Access-Control-Allow-Headers': 'Content-Type, Authorization, Accept',
}

type MockRole = 'platformOwner' | 'tenantAdmin' | 'technician'

type MockAuthOptions = {
  role: MockRole
  mustChangePassword?: boolean
  email?: string
  fullName?: string
  tenantName?: string
  tenantId?: string
}

type LoginExpectation = {
  expectedUrl: RegExp
  visibleTestId?: string
  visibleHeading?: RegExp
  visibleText?: RegExp
}

type MockSession = {
  token: string
  loginEmail: string
  loginPassword: string
  me: {
    user: Record<string, unknown>
    tenant: Record<string, unknown>
    branches: Array<Record<string, unknown>>
    role?: string
    permissions?: Record<string, boolean>
  }
  loginResponse: {
    token: string
    user: Record<string, unknown>
    tenant: Record<string, unknown>
  }
  expectation: LoginExpectation
}

function requireCredentials(email: string, password: string, label: string) {
  if (!email || !password) {
    throw new Error(
      `${label} credentials are missing. Set environment variables before running this suite.`,
    )
  }
}

export function json(body: unknown, status = 200) {
  return {
    status,
    contentType: 'application/json',
    body: JSON.stringify(body),
    headers: CORS_HEADERS,
  }
}

export async function fulfillJson(route: Route, body: unknown, status = 200) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
    return
  }

  await route.fulfill(json(body, status))
}

export async function registerApiCatchAll(page: Page) {
  await page.route(`${E2E_API_URL}/api/**`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: CORS_HEADERS })
      return
    }

    // Un-mocked GETs are mostly list endpoints; an empty array is safe for both list and object
    // consumers (property reads yield undefined), whereas {} crashes any page that maps/slices it.
    await route.fulfill(json(route.request().method() === 'GET' ? [] : {}))
  })
}

function platformPermissions() {
  return {
    canViewPlatformTenants: true,
    canCreatePlatformTenants: true,
    canEditPlatformTenants: true,
    canUpdatePlatformTenantStatus: true,
    canDeactivatePlatformTenants: true,
  }
}

function tenantPermissions() {
  return {
    canViewWorkOrders: true,
    canCreateWorkOrders: true,
    canAssignWorkOrders: true,
    canCompleteWorkOrders: true,
    canApproveMaterials: true,
    canIssueMaterials: true,
    canManageAssets: true,
    canManageSettings: true,
    canViewReports: true,
  }
}

function technicianPermissions() {
  return {
    canViewWorkOrders: true,
    canCompleteWorkOrders: true,
    canManageAssets: false,
    canManageSettings: false,
    canViewReports: false,
  }
}

function buildMockSession(options: MockAuthOptions): MockSession {
  const mustChangePassword = options.mustChangePassword ?? false

  if (options.role === 'platformOwner') {
    const permissions = platformPermissions()
    const user = {
      id: 'platform-owner-1',
      userId: 'platform-owner-1',
      fullName: options.fullName ?? 'Platform Owner',
      email: options.email ?? 'superadmin@ecosys.local',
      role: 'SuperAdmin',
      mustChangePassword,
      permissions,
    }

    return {
      token: mustChangePassword ? 'platform-temporary-token' : 'platform-token',
      loginEmail: user.email,
      loginPassword: 'SuperAdmin123!',
      me: {
        user,
        tenant: {
          tenantId: 'platform-root',
          companyName: 'Ecosys Platform',
          country: 'Kenya',
        },
        branches: [],
        role: 'SuperAdmin',
        permissions,
      },
      loginResponse: {
        token: mustChangePassword ? 'platform-temporary-token' : 'platform-token',
        user: {
          role: 'SuperAdmin',
          mustChangePassword,
          permissions,
        },
        tenant: {
          companyName: 'Ecosys Platform',
        },
      },
      expectation: mustChangePassword
        ? { expectedUrl: /\/change-password$/, visibleHeading: /change your temporary password/i }
        : {
            expectedUrl: /\/(platform|command-centre)/,
            visibleTestId: 'command-centre-sidebar',
          },
    }
  }

  if (options.role === 'technician') {
    const permissions = technicianPermissions()
    const user = {
      id: 'technician-1',
      userId: 'technician-1',
      fullName: options.fullName ?? 'Field Technician',
      email: options.email ?? 'tech@tenant.test',
      role: 'Technician',
      mustChangePassword,
      hasAllBranchAccess: false,
      permissions,
    }

    return {
      token: mustChangePassword ? 'technician-temporary-token' : 'technician-token',
      loginEmail: user.email,
      loginPassword: 'Password123!',
      me: {
        user,
        tenant: {
          tenantId: options.tenantId ?? 'tenant-1',
          companyName: options.tenantName ?? 'Acme Facilities',
          country: 'Kenya',
        },
        branches: [{ id: 'branch-1', name: 'Nairobi Service Hub', code: 'NRB', location: 'Nairobi', isActive: true }],
      },
      loginResponse: {
        token: mustChangePassword ? 'technician-temporary-token' : 'technician-token',
        user: {
          role: 'Technician',
          mustChangePassword,
          permissions,
        },
        tenant: {
          companyName: options.tenantName ?? 'Acme Facilities',
        },
      },
      expectation: mustChangePassword
        ? { expectedUrl: /\/change-password$/, visibleHeading: /change your temporary password/i }
        : {
            expectedUrl: /\/tech/,
          },
    }
  }

  const permissions = tenantPermissions()
  const user = {
    id: 'tenant-admin-1',
    userId: 'tenant-admin-1',
    fullName: options.fullName ?? 'Tenant Admin',
    email: options.email ?? 'admin@acme.test',
    role: 'Admin',
    mustChangePassword,
    hasAllBranchAccess: true,
    permissions,
  }

  return {
    token: mustChangePassword ? 'tenant-temporary-token' : 'tenant-token',
    loginEmail: user.email,
    loginPassword: 'TenantAdmin123!',
    me: {
      user,
      tenant: {
        tenantId: options.tenantId ?? 'tenant-1',
        companyName: options.tenantName ?? 'Acme Facilities',
        country: 'Kenya',
      },
      branches: [{ id: 'branch-1', name: 'Nairobi Service Hub', code: 'NRB', location: 'Nairobi', isActive: true }],
    },
    loginResponse: {
      token: mustChangePassword ? 'tenant-temporary-token' : 'tenant-token',
      user: {
        role: 'Admin',
        mustChangePassword,
        permissions,
      },
      tenant: {
        companyName: options.tenantName ?? 'Acme Facilities',
      },
    },
    expectation: mustChangePassword
      ? { expectedUrl: /\/change-password$/, visibleHeading: /change your temporary password/i }
      : {
          expectedUrl: /\/dashboard/,
          visibleText: /operational command centre/i,
        },
  }
}

export async function mockAuthSession(page: Page, options: MockAuthOptions) {
  const session = buildMockSession(options)

  await page.route(`${E2E_API_URL}/api/tenant/license`, async (route) => {
    await fulfillJson(route, { planCode: 'suite', planName: 'Ecosys ServiceOps Suite', modules: ['operations', 'finance'] })
  })

  await page.route(`${E2E_API_URL}/api/auth/login`, async (route) => {
    await fulfillJson(route, session.loginResponse)
  })

  await page.route(`${E2E_API_URL}/api/auth/me`, async (route) => {
    await fulfillJson(route, session.me)
  })

  await page.route(`${E2E_API_URL}/api/auth/logout`, async (route) => {
    await fulfillJson(route, { success: true })
  })

  if (options.role !== 'platformOwner' && !options.mustChangePassword) {
    await page.route(`${E2E_API_URL}/api/dashboard/summary**`, async (route) => {
      await fulfillJson(route, {
        openWorkOrders: 0,
        closedWorkOrders: 0,
        overdueWorkOrders: 0,
        assets: 0,
        clients: 0,
        materialsLowStock: 0,
        unassignedWorkOrders: 0,
        assignedToGroup: 0,
        assignedToTechnicians: 0,
        awaitingAcceptance: 0,
        techniciansOnSite: 0,
        workOrdersByGroup: [],
        technicianWorkload: [],
      })
    })
  }

  return session
}

async function performLogin(page: Page, email: string, password: string, expectation: LoginExpectation) {
  await page.goto('/login')
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Password').fill(password)
  await page.getByRole('button', { name: /^sign in$/i }).click()

  await page.waitForURL(expectation.expectedUrl, { timeout: 20000 })

  if (expectation.visibleTestId) {
    await expect(page.getByTestId(expectation.visibleTestId)).toBeVisible({ timeout: 20000 })
  }
  if (expectation.visibleHeading) {
    await expect(page.getByRole('heading', { name: expectation.visibleHeading })).toBeVisible({ timeout: 20000 })
  }
  if (expectation.visibleText) {
    await expect(page.getByText(expectation.visibleText)).toBeVisible({ timeout: 20000 })
  }
}

export async function loginWithMockSession(page: Page, options: MockAuthOptions) {
  const session = await mockAuthSession(page, options)
  await performLogin(page, session.loginEmail, session.loginPassword, session.expectation)
  return session
}

export async function loginAsPlatformOwner(page: Page) {
  requireCredentials(E2E_PLATFORM_EMAIL, E2E_PLATFORM_PASSWORD, 'Platform owner')
  await performLogin(page, E2E_PLATFORM_EMAIL, E2E_PLATFORM_PASSWORD, {
    expectedUrl: /\/(platform|command-centre)/,
    visibleTestId: 'command-centre-sidebar',
  })
}

export async function loginAsTenantAdmin(page: Page) {
  await seedTenantSession(page)
}

export async function loginAsTechnician(page: Page, credentials?: { email: string; password: string }) {
  const email = credentials?.email ?? process.env.E2E_TECHNICIAN_EMAIL ?? ''
  const password = credentials?.password ?? process.env.E2E_TECHNICIAN_PASSWORD ?? ''
  requireCredentials(email, password, 'Technician')
  await performLogin(page, email, password, {
    expectedUrl: /\/tech/,
    visibleTestId: 'tech-jobs-page',
  })
}

export async function seedAuthenticatedStorage(page: Page, token: string) {
  const authState = JSON.stringify({ token, mode: 'local' })
  await page.addInitScript(([storageKey, serializedAuthState]) => {
    window.localStorage.setItem(storageKey, serializedAuthState)
  }, [AUTH_STORAGE_KEY, authState])
  await page.goto('/login')
  await page.evaluate(([storageKey, serializedAuthState]) => {
    window.localStorage.setItem(storageKey, serializedAuthState)
  }, [AUTH_STORAGE_KEY, authState])
}

export async function primeTenantSession(page: Page) {
  const session = await getTenantSession()
  const authState = JSON.stringify({ token: session.token, mode: 'local' })
  await page.addInitScript(([storageKey, serializedAuthState]) => {
    window.localStorage.setItem(storageKey, serializedAuthState)
  }, [AUTH_STORAGE_KEY, authState])
}

export async function seedTenantSession(page: Page) {
  const session = await getTenantSession()
  await seedAuthenticatedStorage(page, session.token)
}

async function getTenantSession(): Promise<TenantSession> {
  tenantSessionPromise ??= (async () => {
    const apiContext = await request.newContext({ baseURL: E2E_API_URL })

    try {
      if (E2E_TENANT_EMAIL && E2E_TENANT_PASSWORD) {
        const loginRes = await apiContext.post('/api/auth/login', {
          data: {
            emailOrUsername: E2E_TENANT_EMAIL,
            password: E2E_TENANT_PASSWORD,
          },
        })
        if (!loginRes.ok()) {
          throw new Error(`Tenant login failed with status ${loginRes.status()}: ${await loginRes.text()}`)
        }

        const loginBody = await loginRes.json() as { token?: string }
        if (!loginBody.token) {
          throw new Error('Tenant login response did not include a token.')
        }

        return { token: loginBody.token }
      }

      const suffix = Date.now()
      const signupEmail = `e2e.tenant.${suffix}@example.com`
      const signupPassword = `E2E!Tenant${suffix}`

      const signupRes = await apiContext.post('/api/auth/signup', {
        data: {
          companyName: `E2E Tenant ${suffix}`,
          fullName: `E2E Admin ${suffix}`,
          email: signupEmail,
          password: signupPassword,
          industry: 'Facilities',
          country: 'Kenya',
        },
      })
      if (!signupRes.ok()) {
        throw new Error(`Signup failed with status ${signupRes.status()}: ${await signupRes.text()}`)
      }

      const signupBody = await signupRes.json() as { token?: string }
      if (!signupBody.token) {
        throw new Error('Tenant signup response did not include a token.')
      }

      return { token: signupBody.token }
    } finally {
      await apiContext.dispose()
    }
  })()

  return tenantSessionPromise
}
