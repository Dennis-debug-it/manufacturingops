import { expect, test, type Page, type Route } from '@playwright/test'
import { CORS_HEADERS, json, loginWithMockSession, registerApiCatchAll } from './helpers/auth'

const API = 'http://localhost:5072'

type TenantRecord = {
  tenantId: string
  name: string
  slug: string
  contactName: string
  contactEmail: string
  contactPhone: string
  planName: string
  licenseStatus: string
  userCount: number
  branchCount: number
  status: string
  createdAt: string
  country?: string
  industry?: string
  maxUsers?: number | null
  maxBranches?: number | null
  trialStartsAt?: string | null
  trialEndsAt?: string | null
  trialExtensionUsed?: boolean
  trialExtendedAt?: string | null
  trialDaysRemaining?: number | null
  trialStatus?: string
  initialAdminInvitationSent?: boolean
  initialAdminInvitationMessage?: string | null
}

const platformEmailSettings = {
  deliveryMode: 'Smtp',
  smtpHost: 'smtp.ecosys.test',
  smtpPort: 587,
  smtpUsername: 'platform@ecosys.test',
  smtpPasswordMasked: '••••••••',
  smtpPasswordSecret: '',
  senderName: 'Ecosys Platform',
  senderEmail: 'platform@ecosys.test',
  replyToEmail: 'support@ecosys.test',
  enableSslTls: true,
  secureMode: 'StartTls',
  apiEndpoint: '',
  apiKeyMasked: '',
  apiKeySecret: '',
  apiProviderName: '',
  timeoutSeconds: 30,
  maxRetries: 1,
  enableEmailNotifications: true,
  enableSystemAlerts: true,
  enableInvoiceEmails: true,
  enableQuotationEmails: true,
  enablePaymentReceiptEmails: true,
  enableWorkOrderNotificationEmails: true,
  enableSlaEscalationEmails: false,
  enableTenantOnboardingEmails: true,
  subjectPrefix: '[Ecosys]',
  subjectSuffix: '',
  includeEnvironmentInSubject: false,
  environmentLabel: 'Production',
  includeTenantNameInSubject: false,
  enableEventSubjectTags: true,
  notificationPreferences: [],
  lastTestedAt: null,
  lastError: null,
}

const platformGeneralSettings = {
  platformName: 'Ecosys Platform',
  supportEmail: 'support@ecosys.test',
  defaultCountry: 'Kenya',
  defaultCurrency: 'KES',
  timezone: 'Africa/Nairobi',
  companyLegalName: 'Ecosys Digital Ltd',
  companyRegistrationNumber: 'PVT-123456',
  companyPinTaxNumber: 'P051234567X',
  defaultLanguage: 'en',
}

const notificationRules = [
  {
    eventKey: 'tenant.onboarding',
    displayName: 'Tenant/workspace provisioned',
    templateKey: 'tenant-onboarding',
    recipientStrategy: 'Tenant primary contact or admin',
    senderScope: 'Platform SMTP',
    dispatchStatus: 'Active',
    description: 'Welcomes a new tenant workspace when provisioning is complete.',
    supportedChannels: ['Email'],
    notes: 'Currently available through platform provisioning flows.',
  },
  {
    eventKey: 'platform.lead.received',
    displayName: 'Workspace request received',
    templateKey: 'workspace-request-received',
    recipientStrategy: 'Platform support or admin email',
    senderScope: 'Platform SMTP',
    dispatchStatus: 'Active',
    description: 'Alerts platform staff when a workspace request is submitted.',
    supportedChannels: ['Email'],
    notes: 'Lead notification delivery is active.',
  },
  {
    eventKey: 'auth.password-reset.requested',
    displayName: 'Self-service password reset requested',
    templateKey: 'password-reset-link',
    recipientStrategy: 'Requesting user',
    senderScope: 'Platform or tenant SMTP',
    dispatchStatus: 'Active',
    description: 'Sends a self-service reset link when a user requests password recovery.',
    supportedChannels: ['Email'],
    notes: 'Currently used by the forgot-password flow.',
  },
  {
    eventKey: 'user.password-reset.admin',
    displayName: 'Admin password reset',
    templateKey: 'password-reset',
    recipientStrategy: 'Selected user',
    senderScope: 'Platform or tenant SMTP',
    dispatchStatus: 'Active',
    description: 'Sends an administrator-triggered password reset notification.',
    supportedChannels: ['Email'],
    notes: 'Currently used by admin reset actions.',
  },
  {
    eventKey: 'work-order.assigned',
    displayName: 'Work order assigned',
    templateKey: 'work-order-assigned',
    recipientStrategy: 'Assigned technician',
    senderScope: 'Tenant SMTP',
    dispatchStatus: 'Active',
    description: 'Sends an assignment notification to each newly assigned technician.',
    supportedChannels: ['Email', 'In-App'],
    notes: 'Wired to assignment workflow.',
  },
  {
    eventKey: 'work-order.completed',
    displayName: 'Work order completed',
    templateKey: 'work-order-completed',
    recipientStrategy: 'Relevant stakeholders',
    senderScope: 'Tenant SMTP',
    dispatchStatus: 'Active',
    description: 'Sends a completion notification when a work order is closed.',
    supportedChannels: ['Email', 'In-App'],
    notes: 'Wired to the work order lifecycle flow.',
  },
  {
    eventKey: 'work-order.overdue',
    displayName: 'Work order overdue',
    templateKey: 'work-order-overdue',
    recipientStrategy: 'Supervisor or admin',
    senderScope: 'Tenant SMTP',
    dispatchStatus: 'PendingHook',
    description: 'Escalation email for overdue work orders.',
    supportedChannels: ['Email', 'In-App'],
    notes: 'Template available, dispatch hook pending.',
  },
  {
    eventKey: 'pm.due',
    displayName: 'Preventive maintenance due',
    templateKey: 'pm-due',
    recipientStrategy: 'Assigned group or supervisor',
    senderScope: 'Tenant SMTP',
    dispatchStatus: 'PendingHook',
    description: 'Reminder for preventive maintenance due dates.',
    supportedChannels: ['Email', 'In-App'],
    notes: 'Template available, dispatch hook pending.',
  },
]

async function fulfillOptions(route: Route) {
  if (route.request().method() === 'OPTIONS') {
    await route.fulfill({ status: 204, headers: CORS_HEADERS })
    return true
  }

  return false
}

async function mockPlatformOwnerWorkspace(page: Page) {
  const tenants: TenantRecord[] = [
    {
      tenantId: 'tenant-existing-1',
      name: 'Acme Facilities',
      slug: 'acme-facilities',
      contactName: 'Jane Doe',
      contactEmail: 'jane@acme.test',
      contactPhone: '+254700000111',
      planName: 'Enterprise',
      licenseStatus: 'Active',
      userCount: 24,
      branchCount: 6,
      status: 'Active',
      createdAt: '2026-05-01T09:00:00.000Z',
      trialStatus: 'PaidActive',
    },
  ]

  await registerApiCatchAll(page)
  await loginWithMockSession(page, { role: 'platformOwner', email: 'superadmin@ecosys.local', fullName: 'Lena Atieno' })

  await page.route(`${API}/api/platform/tenants**`, async (route) => {
    if (await fulfillOptions(route)) return

    if (route.request().method() === 'GET') {
      await route.fulfill(json(tenants))
      return
    }

    const request = route.request().postDataJSON() as Record<string, unknown>
    const created: TenantRecord = {
      tenantId: 'tenant-created-1',
      name: String(request.name),
      slug: String(request.slug),
      contactName: String(request.primaryContactName),
      contactEmail: String(request.primaryContactEmail),
      contactPhone: String(request.primaryContactPhone ?? ''),
      planName: String(request.planName ?? 'Trial'),
      licenseStatus: 'Trial',
      userCount: 1,
      branchCount: 0,
      status: 'Trial',
      createdAt: '2026-05-16T09:15:00.000Z',
      country: String(request.country ?? 'Kenya'),
      industry: String(request.industry ?? ''),
      maxUsers: Number(request.maxUsers ?? 10),
      maxBranches: Number(request.maxBranches ?? 2),
      trialStartsAt: '2026-05-16T09:15:00.000Z',
      trialEndsAt: '2026-05-30T09:15:00.000Z',
      trialExtensionUsed: false,
      trialDaysRemaining: 14,
      trialStatus: 'TrialActive',
      initialAdminInvitationSent: true,
      initialAdminInvitationMessage: 'Workspace admin invited.',
    }

    tenants.unshift(created)
    await route.fulfill(json(created, 201))
  })

  await page.route(`${API}/api/platform/settings/email`, async (route) => {
    if (await fulfillOptions(route)) return
    await route.fulfill(json(platformEmailSettings))
  })

  await page.route(`${API}/api/platform/settings/general`, async (route) => {
    if (await fulfillOptions(route)) return
    await route.fulfill(json(platformGeneralSettings))
  })

  await page.route(`${API}/api/platform/settings/email/notification-rules`, async (route) => {
    if (await fulfillOptions(route)) return
    await route.fulfill(json(notificationRules))
  })
}

test.describe('auth tenant onboarding', () => {
  test.beforeEach(async ({ page }) => {
    await mockPlatformOwnerWorkspace(page)
  })

  test('platform owner can create a tenant and see the trial/admin outcome', async ({ page }) => {
    await page.goto('/platform/tenants')
    await expect(page).toHaveURL(/\/platform\/tenants/)
    await expect(page.getByTestId('tenants-page')).toBeVisible()

    await page.getByRole('button', { name: /add tenant/i }).click()
    await expect(page.getByRole('heading', { name: /add tenant/i })).toBeVisible()

    await page.getByRole('textbox', { name: 'Company Name', exact: true }).fill('Northwind ServiceOps')
    await page.getByRole('textbox', { name: 'Company Email', exact: true }).fill('hello@northwind.test')
    await page.getByRole('textbox', { name: 'Company Phone', exact: true }).fill('+254700001234')
    await page.getByRole('textbox', { name: 'Country', exact: true }).fill('Kenya')
    await page.getByRole('textbox', { name: 'Industry', exact: true }).fill('Facilities')
    await page.getByRole('textbox', { name: 'Primary Contact Name', exact: true }).fill('Amina Hassan')
    await page.getByRole('textbox', { name: 'Primary Contact Email', exact: true }).fill('amina@northwind.test')
    await page.getByRole('textbox', { name: 'Primary Contact Phone', exact: true }).fill('+254700001235')
    await page.getByRole('textbox', { name: 'Plan', exact: true }).fill('Growth')
    await page.getByRole('spinbutton', { name: 'Max Users', exact: true }).fill('15')
    await page.getByRole('spinbutton', { name: 'Max Branches', exact: true }).fill('3')

    await page.getByRole('button', { name: /create tenant/i }).click()

    await expect(page.getByText('Tenant created', { exact: true })).toBeVisible()
    await expect(page.getByText(/workspace admin invited/i)).toBeVisible()
    await expect(page.getByRole('table').getByText('Northwind ServiceOps')).toBeVisible()
    await expect(page.getByRole('table').getByText(/Trial active/i)).toBeVisible()
  })

  test('platform notification rules expose only the approved active hooks', async ({ page }) => {
    await page.goto('/platform/settings')
    await expect(page).toHaveURL(/\/platform\/settings/)

    await page.getByTestId('settings-mini-sidebar-email').click()
    await expect(page.getByTestId('settings-panel-email-notifications')).toBeVisible()
    await page.getByTestId('platform-email-settings-form').getByRole('button', { name: 'Notification Rules', exact: true }).click()

    const resetRule = page.locator('article', { hasText: 'auth.password-reset.requested' })
    await expect(resetRule).toContainText('Active')

    const completedRule = page.locator('article', { hasText: 'work-order.completed' })
    await expect(completedRule).toContainText('Active')

    const overdueRule = page.locator('article', { hasText: 'work-order.overdue' })
    await expect(overdueRule).toContainText('PendingHook')

    const pmDueRule = page.locator('article', { hasText: 'pm.due' })
    await expect(pmDueRule).toContainText('PendingHook')

    await expect(page.locator('body')).not.toContainText('SMS (Future)')
  })
})
