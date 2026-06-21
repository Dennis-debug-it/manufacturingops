import { ApiError, api } from '../lib/api'
import type {
  PlatformReportsSummary,
  PlatformTenant,
  PlatformTenantDetail,
  PlatformTenantStatus,
  UpdatePlatformTenantStatusInput,
  UpsertPlatformTenantInput,
} from '../types/api'
import type {
  AuditLog,
  DocumentTemplate,
  Expense,
  FinanceData,
  FinanceSummary,
  Invoice,
  InvoiceStatus,
  Payment,
  PlatformRole,
  PlatformSettings,
  PlatformUser,
  PlatformUserStatus,
  Quotation,
  ReportSummary,
  ReportsMeta,
  ServiceResult,
  TaxMode,
  Tenant,
  TenantCommunicationActionResponse,
  TenantCommunicationSettings,
  TenantEmailSettings,
  TenantNotificationRecipient,
  TenantNotificationSetting,
  TenantStatus,
} from '../types/platform'
import { normalizePlatformRole } from '../utils/roles'

function safeMessage(error: unknown, fallback: string) {
  if (error instanceof ApiError || error instanceof Error) return error.message
  return fallback
}

function nowIso() {
  return new Date().toISOString()
}

function isGuid(value: string | undefined | null) {
  if (!value) return false
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value)
}

const tenantNotificationDefaults: TenantNotificationSetting[] = [
  'work-order.new-created',
  'work-order.assigned',
  'work-order.updated',
  'work-order.completed',
  'work-order.overdue',
  'sla.warning',
  'sla.breach',
  'pm.due',
  'pm.overdue',
  'pm.completed',
  'asset.created',
  'asset.updated',
  'asset.deactivated',
  'asset.maintenance-due',
  'materials.request-submitted',
  'materials.request-approved',
  'materials.request-rejected',
  'materials.request-issued',
  'materials.low-stock-alert',
  'users.invited',
  'users.activated',
  'users.deactivated',
  'users.role-changed',
  'tenant.profile-updated',
  'tenant.branding-updated',
  'tenant.module-access-changed',
  'tenant.subscription-status-changed',
  'system.alerts',
  'system.failed-login-attempts',
  'system.integration-errors',
  'system.background-job-failures',
].map((key) => ({
  notificationKey: key,
  emailEnabled: true,
  inAppEnabled: key.startsWith('system.'),
  smsEnabled: false,
  isActive: true,
}))

function toTenantStatus(status: string): TenantStatus {
  if (status === 'Active' || status === 'Trial' || status === 'Suspended') return status
  return 'Inactive'
}

function toLicenseStatus(status: string): Tenant['licenseStatus'] {
  if (status === 'Active' || status === 'Trial' || status === 'Expired' || status === 'Suspended') return status
  return 'Expired'
}

function mapTenant(item: PlatformTenant): Tenant {
  return {
    tenantId: item.tenantId,
    name: item.name,
    slug: item.slug,
    contactPerson: item.contactName ?? '',
    contactEmail: item.contactEmail ?? '',
    plan: item.planName || 'Starter',
    licenseStatus: toLicenseStatus(item.licenseStatus),
    users: item.userCount,
    branches: item.branchCount,
    status: toTenantStatus(item.status),
    trialStartDate: item.trialStartsAt,
    trialEndDate: item.trialEndsAt,
    trialExtensionUsed: item.trialExtensionUsed,
    trialExtendedAt: item.trialExtendedAt,
    trialDaysRemaining: item.trialDaysRemaining ?? null,
    trialStatus: item.trialStatus,
    createdAt: item.createdAt,
    subscriptionStartsAt: item.subscriptionStartsAt ?? null,
    subscriptionEndsAt: item.subscriptionEndsAt ?? null,
    workOrderCount: item.workOrderCount ?? null,
    assetCount: item.assetCount ?? null,
    lastActivityAt: item.lastActivityAt ?? null,
    modules: item.modules ?? null,
  }
}

function mapTenantDetail(item: PlatformTenantDetail): Tenant {
  return {
    ...mapTenant(item),
    country: item.country ?? '',
    industry: item.industry ?? '',
    contactPhone: item.contactPhone ?? '',
    maxUsers: item.maxUsers,
    maxBranches: item.maxBranches,
    trialStartDate: item.trialStartsAt,
    trialEndDate: item.trialEndsAt,
    trialExtensionUsed: item.trialExtensionUsed,
    trialExtendedAt: item.trialExtendedAt,
    trialDaysRemaining: item.trialDaysRemaining ?? null,
    trialStatus: item.trialStatus,
    initialAdminInvitationSent: item.initialAdminInvitationSent ?? null,
    initialAdminInvitationMessage: item.initialAdminInvitationMessage ?? null,
  }
}

function mapTenantEmailSettings(item: TenantEmailSettingsResponse): TenantEmailSettings {
  return {
    id: item.id,
    tenantId: item.tenantId,
    usePlatformDefaults: item.usePlatformDefaults,
    overrideSmtpSettings: item.overrideSmtpSettings,
    deliveryMode: item.deliveryMode || 'Smtp',
    smtpHost: item.smtpHost || '',
    smtpPort: item.smtpPort || 587,
    smtpUsername: item.smtpUsername || '',
    smtpPasswordMasked: item.smtpPasswordMasked || '',
    smtpPasswordSecret: '',
    senderName: item.senderName || '',
    senderEmail: item.senderEmail || '',
    replyToEmail: item.replyToEmail || '',
    enableSslTls: item.enableSslTls,
    secureMode: item.secureMode || 'Auto',
    apiEndpoint: item.apiEndpoint || '',
    apiKeyMasked: item.apiKeyMasked || '',
    apiKeySecret: '',
    apiProviderName: item.apiProviderName || '',
    timeoutSeconds: item.timeoutSeconds || 30,
    maxRetries: item.maxRetries || 0,
    enableTenantEmailNotifications: item.enableTenantEmailNotifications,
    lastTestedAt: item.lastTestedAt,
    lastError: item.lastError,
  }
}

function mapTenantNotificationSettings(items: TenantNotificationSettingResponse[]): TenantNotificationSetting[] {
  return items.map((item) => ({
    notificationKey: item.notificationKey,
    emailEnabled: item.emailEnabled,
    inAppEnabled: item.inAppEnabled,
    smsEnabled: item.smsEnabled,
    isActive: item.isActive,
  }))
}

function mapTenantNotificationRecipients(items: TenantNotificationRecipientResponse[]): TenantNotificationRecipient[] {
  return items.map((item) => ({
    recipientGroup: item.recipientGroup,
    email: item.email,
    isActive: item.isActive,
  }))
}

function toTenantCommunicationPayload(input: TenantCommunicationSettings) {
  return {
    emailSettings: {
      usePlatformDefaults: input.emailSettings.usePlatformDefaults,
      overrideSmtpSettings: input.emailSettings.overrideSmtpSettings,
      deliveryMode: input.emailSettings.deliveryMode,
      smtpHost: input.emailSettings.smtpHost || null,
      smtpPort: input.emailSettings.smtpPort || null,
      smtpUsername: input.emailSettings.smtpUsername || null,
      smtpPasswordSecret: input.emailSettings.smtpPasswordSecret || null,
      senderName: input.emailSettings.senderName || null,
      senderEmail: input.emailSettings.senderEmail || null,
      replyToEmail: input.emailSettings.replyToEmail || null,
      enableSslTls: input.emailSettings.enableSslTls,
      secureMode: input.emailSettings.secureMode,
      apiEndpoint: input.emailSettings.apiEndpoint || null,
      apiKeySecret: input.emailSettings.apiKeySecret || null,
      apiProviderName: input.emailSettings.apiProviderName || null,
      timeoutSeconds: input.emailSettings.timeoutSeconds || 30,
      maxRetries: input.emailSettings.maxRetries || 0,
      enableTenantEmailNotifications: input.emailSettings.enableTenantEmailNotifications,
    },
    notificationSettings: input.notificationSettings.map((item) => ({
      notificationKey: item.notificationKey,
      emailEnabled: item.emailEnabled,
      inAppEnabled: item.inAppEnabled,
      smsEnabled: item.smsEnabled,
      isActive: item.isActive,
    })),
    recipients: input.recipients.map((item) => ({
      recipientGroup: item.recipientGroup,
      email: item.email,
      isActive: item.isActive,
    })),
  }
}

function toPlatformTenantStatus(status: TenantStatus): PlatformTenantStatus {
  if (status === 'Active') return 'Active'
  if (status === 'Trial') return 'Trial'
  if (status === 'Suspended') return 'Suspended'
  return 'Inactive'
}

function buildTenantPayload(input: Partial<Tenant> & { name: string; slug: string }): UpsertPlatformTenantInput {
  const usePrimaryContactAsWorkspaceAdmin = Boolean(input.usePrimaryContactAsWorkspaceAdmin)
  const primaryContactName = input.contactPerson || ''
  const primaryContactEmail = input.contactEmail || ''
  const primaryContactPhone = input.contactPhone || ''
  const adminFullName = usePrimaryContactAsWorkspaceAdmin ? primaryContactName : (input.adminFullName || '')
  const adminEmail = usePrimaryContactAsWorkspaceAdmin ? primaryContactEmail : (input.adminEmail || '')
  const adminPhone = usePrimaryContactAsWorkspaceAdmin ? primaryContactPhone : (input.adminPhone || '')
  const planName = typeof input.plan === 'string' && input.plan.trim() ? input.plan.trim() : null

  return {
    name: input.name,
    slug: input.slug,
    companyEmail: input.companyEmail || '',
    companyPhone: input.companyPhone || '',
    industry: input.industry || '',
    contactName: primaryContactName,
    contactEmail: primaryContactEmail,
    contactPhone: primaryContactPhone,
    primaryContactName,
    primaryContactEmail,
    primaryContactPhone,
    country: input.country || 'Kenya',
    county: '',
    city: '',
    address: '',
    taxPin: '',
    planName,
    maxUsers: input.maxUsers ?? null,
    maxBranches: input.maxBranches ?? null,
    trialEndsAt: input.trialEndDate || null,
    subscriptionEndsAt: null,
    status: toPlatformTenantStatus(input.status || 'Trial'),
    licenseStatus: input.licenseStatus || 'Trial',
    createDefaultAdmin: true,
    usePrimaryContactAsWorkspaceAdmin,
    adminFullName,
    adminEmail,
    adminPhone,
  }
}

function mapPlatformRole(role: string): PlatformRole {
  if (normalizePlatformRole(role) === 'PlatformOwner') return 'PlatformOwner'
  if (normalizePlatformRole(role) === 'PlatformAdmin') return 'PlatformAdmin'
  if (normalizePlatformRole(role) === 'SupportAdmin') return 'SupportAdmin'
  if (role === 'FinanceAdmin') return 'FinanceAdmin'
  if (role === 'ReadOnlyAuditor') return 'ReadOnlyAuditor'
  return 'PlatformAdmin'
}

function toPlatformRole(role: PlatformRole): 'PlatformOwner' | 'PlatformAdmin' | 'SupportAdmin' | 'FinanceAdmin' | 'ReadOnlyAuditor' {
  if (normalizePlatformRole(role) === 'PlatformOwner') return 'PlatformOwner'
  if (normalizePlatformRole(role) === 'PlatformAdmin') return 'PlatformAdmin'
  if (normalizePlatformRole(role) === 'SupportAdmin') return 'SupportAdmin'
  if (role === 'FinanceAdmin' || role === 'Finance') return 'FinanceAdmin'
  return 'ReadOnlyAuditor'
}

function mapPlatformUser(item: PlatformUserResponse): PlatformUser {
  return {
    id: item.id,
    fullName: item.fullName,
    email: item.email,
    phone: item.phone,
    role: mapPlatformRole(item.role),
    status: item.status === 'Active' ? 'Active' : 'Disabled',
    lastLogin: item.lastLoginAt,
    createdAt: item.createdAt,
    updatedAt: item.updatedAt,
    mustChangePassword: item.mustChangePassword,
    lastCredentialSentAt: item.lastCredentialSentAt,
    credentialDelivery: item.credentialDelivery ?? null,
  }
}

function normalizeInvoiceStatus(status: string): InvoiceStatus {
  if (status === 'Draft' || status === 'Sent' || status === 'PartiallyPaid' || status === 'Paid' || status === 'Overdue' || status === 'Void') return status
  if (status === 'Issued') return 'Sent'
  if (status === 'Cancelled') return 'Void'
  return 'Draft'
}

function normalizePaymentMethod(method: string): Payment['method'] {
  if (method === 'Cash' || method === 'M-Pesa' || method === 'Bank Transfer' || method === 'Cheque' || method === 'Card' || method === 'Other') return method
  if (method === 'Bank transfer') return 'Bank Transfer'
  return 'Other'
}

function toApiInvoiceStatus(status: InvoiceStatus): string {
  if (status === 'Issued') return 'Sent'
  if (status === 'Cancelled') return 'Void'
  return status
}

function toApiPaymentMethod(method: Payment['method']): string {
  if (method === 'Bank transfer') return 'Bank Transfer'
  return method
}

type PlatformFinanceLineResponse = {
  id: string
  description: string
  quantity: number
  unitPrice: number
  taxable: boolean
  lineTotal: number
}

type PlatformQuotationResponse = {
  id: string
  quotationNumber: string
  tenantId?: string | null
  customerName: string
  currency: string
  subtotal: number
  discountRate: number
  discountAmount: number
  taxRate: number
  taxAmount: number
  total: number
  status: string
  validUntil?: string | null
  notes?: string | null
  convertedInvoiceId?: string | null
  lines: PlatformFinanceLineResponse[]
  createdAt: string
}

type PlatformInvoiceResponse = {
  id: string
  invoiceNumber: string
  tenantId?: string | null
  customerName: string
  currency: string
  subtotal: number
  discountRate: number
  discountAmount: number
  taxRate: number
  taxAmount: number
  total: number
  amountPaid: number
  balance: number
  status: string
  issueDate: string
  dueDate?: string | null
  notes?: string | null
  lines: PlatformFinanceLineResponse[]
  createdAt: string
}

type PlatformPaymentResponse = {
  id: string
  paymentNumber: string
  invoiceId?: string | null
  tenantId?: string | null
  amount: number
  currency: string
  method: string
  status: string
  reference?: string | null
  paidAt: string
}

type PlatformExpenseResponse = {
  id: string
  expenseDate: string
  category: string
  vendor?: string | null
  description: string
  amount: number
  taxAmount: number
  totalAmount: number
  currency: string
  paymentMethod: string
  attachmentUrl?: string | null
  status: string
  approvedAt?: string | null
  tenantId?: string | null
}

type PlatformTaxResponse = {
  id: string
  name: string
  rate: number
  mode: TaxMode
  isDefault: boolean
}

type PlatformTemplateResponse = {
  id: string
  name: string
  type: DocumentTemplate['type']
  previewText: string
  isDefault: boolean
  isActive: boolean
}

type PlatformFinanceDashboardResponse = {
  totalRevenue: number
  outstandingInvoices: number
  overdueInvoices: number
  expensesThisMonth: number
  profitEstimate: number
  quotationConversionRate: number
  recentPayments: PlatformPaymentResponse[]
  recentInvoices: PlatformInvoiceResponse[]
  overdueAccounts: PlatformInvoiceResponse[]
}

function mapQuotation(item: PlatformQuotationResponse): Quotation {
  return {
    id: item.id,
    number: item.quotationNumber,
    tenantId: item.tenantId ?? undefined,
    tenantName: item.customerName,
    issueDate: item.createdAt,
    dueDate: item.validUntil,
    status: item.status as Quotation['status'],
    taxRate: item.taxRate,
    taxMode: 'Exclusive',
    lines: item.lines.map((line) => ({ id: line.id, description: line.description, quantity: line.quantity, unitPrice: line.unitPrice })),
    subtotal: item.subtotal,
    taxAmount: item.taxAmount,
    total: item.total,
    notes: item.notes ?? undefined,
    convertedInvoiceId: item.convertedInvoiceId ?? undefined,
  }
}

function mapInvoice(item: PlatformInvoiceResponse): Invoice {
  return {
    id: item.id,
    number: item.invoiceNumber,
    tenantId: item.tenantId ?? undefined,
    tenantName: item.customerName,
    issueDate: item.issueDate,
    dueDate: item.dueDate,
    status: normalizeInvoiceStatus(item.status),
    taxRate: item.taxRate,
    taxMode: 'Exclusive',
    lines: item.lines.map((line) => ({ id: line.id, description: line.description, quantity: line.quantity, unitPrice: line.unitPrice })),
    subtotal: item.subtotal,
    taxAmount: item.taxAmount,
    total: item.total,
    paidAmount: item.amountPaid,
    balance: item.balance,
    notes: item.notes ?? undefined,
  }
}

function mapPayment(item: PlatformPaymentResponse, invoices: Invoice[]): Payment {
  return {
    id: item.id,
    invoiceId: item.invoiceId ?? undefined,
    invoiceNumber: invoices.find((invoice) => invoice.id === item.invoiceId)?.number,
    tenantId: item.tenantId ?? undefined,
    amount: item.amount,
    paymentDate: item.paidAt,
    method: normalizePaymentMethod(item.method),
    reference: item.reference ?? undefined,
    status: item.status as Payment['status'],
  }
}

function mapExpense(item: PlatformExpenseResponse): Expense {
  return {
    id: item.id,
    expenseDate: item.expenseDate,
    category: item.category,
    vendor: item.vendor ?? '',
    description: item.description,
    amount: item.amount,
    taxAmount: item.taxAmount,
    totalAmount: item.totalAmount,
    paymentMethod: normalizePaymentMethod(item.paymentMethod),
    tenantId: item.tenantId ?? undefined,
    attachmentUrl: item.attachmentUrl,
    status: item.status,
    approvedAt: item.approvedAt,
  }
}

function createDefaultSettings(): PlatformSettings {
  return {
    companyProfile: {
      platformName: 'Ecosys ServiceOps Platform',
      supportEmail: 'support@ecosys.app',
      supportPhone: '',
      address: '',
      website: '',
    },
    branding: {
      logoPlaceholder: '/ecosys-logo-horizontal-dark.svg',
      primaryColor: '#0C2F33',
      accentColor: '#B7E26D',
      poweredByText: 'Powered by Ecosys',
    },
    licensingRules: {
      defaultTrialDays: 14,
      licenseExpiryWarningDays: 14,
      gracePeriodDays: 7,
      autoSuspendExpiredTenant: true,
    },
    numberingRules: {
      tenantCodePrefix: 'TEN',
      quotationPrefix: 'QUO',
      invoicePrefix: 'INV',
      receiptPrefix: 'RCT',
    },
    financeSettings: {
      defaultCurrency: 'KES',
      paymentMethodsEnabled: ['Cash', 'M-Pesa', 'Bank Transfer', 'Cheque', 'Card', 'Other'],
      invoiceDueDays: 30,
    },
    taxSettings: {
      name: 'VAT',
      defaultRate: 16,
      mode: 'Exclusive',
    },
    emailSettings: {
      senderName: 'Ecosys Platform',
      senderEmail: 'noreply@ecosys.app',
      smtpHost: '',
      smtpPort: 587,
      useSsl: true,
    },
    security: {
      minPasswordLength: 10,
      requireUppercase: true,
      requireLowercase: true,
      requireNumber: true,
      requireSymbol: true,
      passwordExpiryDays: 90,
      adminCanResetUserPassword: true,
      otpTodo: false,
      ssoTodo: false,
    },
    notifications: {
      emailAlertsEnabled: true,
      licenseExpiryAlerts: true,
      tenantStatusAlerts: true,
      dailyDigestEnabled: false,
    },
    integrations: {
      monitoringWebhookEnabled: true,
      emailIntakeEnabled: true,
      notes: '',
    },
  }
}

function createDefaultTenantCommunicationSettings(tenantId: string): TenantCommunicationSettings {
  return {
    emailSettings: {
      id: '',
      tenantId,
      usePlatformDefaults: true,
      overrideSmtpSettings: false,
      deliveryMode: 'Smtp',
      smtpHost: '',
      smtpPort: 587,
      smtpUsername: '',
      smtpPasswordMasked: '',
      smtpPasswordSecret: '',
      senderName: '',
      senderEmail: '',
      replyToEmail: '',
      enableSslTls: true,
      secureMode: 'Auto',
      apiEndpoint: '',
      apiKeyMasked: '',
      apiKeySecret: '',
      apiProviderName: '',
      timeoutSeconds: 30,
      maxRetries: 0,
      enableTenantEmailNotifications: true,
      lastTestedAt: null,
      lastError: null,
    },
    notificationSettings: tenantNotificationDefaults.map((item) => ({ ...item })),
    recipients: [],
  }
}

type PlatformSettingsResponse = {
  platformName: string
  supportEmail?: string | null
  defaultCurrency: string
  taxName: string
  defaultTaxRate: number
  taxMode: TaxMode
  invoiceNumberingPrefix: string
  quotationNumberingPrefix: string
  receiptNumberingPrefix: string
  platformLogoUrl?: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

type PlatformUserResponse = {
  id: string
  fullName: string
  email: string
  phone?: string | null
  role: string
  status: string
  lastLoginAt?: string | null
  createdAt: string
  updatedAt?: string | null
  mustChangePassword: boolean
  lastCredentialSentAt?: string | null
  credentialDelivery?: {
    success: boolean
    status: string
    message?: string | null
  } | null
}

type PlatformAuditLogResponse = {
  id: string
  tenantId?: string | null
  actorUserId?: string | null
  actorName: string
  action: string
  entityType: string
  entityId: string
  description?: string | null
  ipAddress?: string | null
  severity: string
  createdAt: string
  tenantName?: string | null
}

type TenantEmailSettingsResponse = {
  id: string
  tenantId: string
  usePlatformDefaults: boolean
  overrideSmtpSettings: boolean
  deliveryMode?: 'Smtp' | 'Api' | 'Disabled'
  smtpHost?: string | null
  smtpPort?: number | null
  smtpUsername?: string | null
  smtpPasswordMasked?: string | null
  senderName?: string | null
  senderEmail?: string | null
  replyToEmail?: string | null
  enableSslTls: boolean
  secureMode?: 'Auto' | 'None' | 'StartTls' | 'SslOnConnect'
  apiEndpoint?: string | null
  apiKeyMasked?: string | null
  apiProviderName?: string | null
  timeoutSeconds?: number | null
  maxRetries?: number | null
  enableTenantEmailNotifications: boolean
  lastTestedAt?: string | null
  lastError?: string | null
}

type TenantNotificationSettingResponse = {
  notificationKey: string
  emailEnabled: boolean
  inAppEnabled: boolean
  smsEnabled: boolean
  isActive: boolean
}

type TenantNotificationRecipientResponse = {
  recipientGroup: string
  email: string
  isActive: boolean
}

type TenantCommunicationSettingsResponse = {
  emailSettings: TenantEmailSettingsResponse
  notificationSettings: TenantNotificationSettingResponse[]
  recipients: TenantNotificationRecipientResponse[]
}

export const tenantsApi = {
  async list(): Promise<ServiceResult<Tenant[]>> {
    const tenants = await api.get<PlatformTenant[]>('/api/platform/tenants')
    return { data: tenants.map(mapTenant), backendAvailable: true }
  },
  async get(tenantId: string): Promise<ServiceResult<Tenant>> {
    const tenant = await api.get<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}`)
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async create(input: Partial<Tenant> & { name: string; slug: string }): Promise<ServiceResult<Tenant>> {
    const tenant = await api.post<PlatformTenantDetail>('/api/platform/tenants', buildTenantPayload(input))
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async update(tenantId: string, input: Partial<Tenant> & { name: string; slug: string }): Promise<ServiceResult<Tenant>> {
    const tenant = await api.put<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}`, buildTenantPayload(input))
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async updateStatus(tenantId: string, status: TenantStatus, reason?: string): Promise<ServiceResult<Tenant>> {
    const payload: UpdatePlatformTenantStatusInput = { status: toPlatformTenantStatus(status), reason: reason || null }
    const tenant = await api.patch<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}/status`, payload)
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async activate(tenantId: string): Promise<ServiceResult<Tenant>> {
    const tenant = await api.post<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}/activate`)
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async deactivate(tenantId: string, reason?: string): Promise<ServiceResult<Tenant>> {
    const tenant = await api.post<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}/deactivate`, { reason: reason || null })
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async extendTrial(tenantId: string): Promise<ServiceResult<Tenant>> {
    const tenant = await api.post<PlatformTenantDetail>(`/api/platform/tenants/${tenantId}/extend-trial`)
    return { data: mapTenantDetail(tenant), backendAvailable: true }
  },
  async getCommunicationSettings(tenantId: string): Promise<ServiceResult<TenantCommunicationSettings>> {
    const response = await api.get<TenantCommunicationSettingsResponse>(`/api/platform/tenants/${tenantId}/communication-settings`)
    return {
      data: {
        emailSettings: mapTenantEmailSettings(response.emailSettings),
        notificationSettings: mapTenantNotificationSettings(response.notificationSettings),
        recipients: mapTenantNotificationRecipients(response.recipients),
      },
      backendAvailable: true,
    }
  },
  async saveCommunicationSettings(tenantId: string, input: TenantCommunicationSettings): Promise<ServiceResult<TenantCommunicationSettings>> {
    const response = await api.put<TenantCommunicationSettingsResponse>(
      `/api/platform/tenants/${tenantId}/communication-settings`,
      toTenantCommunicationPayload(input),
    )
    return {
      data: {
        emailSettings: mapTenantEmailSettings(response.emailSettings),
        notificationSettings: mapTenantNotificationSettings(response.notificationSettings),
        recipients: mapTenantNotificationRecipients(response.recipients),
      },
      backendAvailable: true,
    }
  },
  async verifyCommunicationSmtp(tenantId: string): Promise<ServiceResult<TenantCommunicationActionResponse>> {
    const response = await api.post<TenantCommunicationActionResponse>(`/api/platform/tenants/${tenantId}/email-settings/verify`, {})
    return { data: response, backendAvailable: true }
  },
  async sendCommunicationTestEmail(tenantId: string, testRecipientEmail?: string): Promise<ServiceResult<TenantCommunicationActionResponse>> {
    const response = await api.post<TenantCommunicationActionResponse>(`/api/platform/tenants/${tenantId}/email-settings/test`, { testRecipientEmail: testRecipientEmail || null })
    return { data: response, backendAvailable: true }
  },
  async resetCommunicationToDefaults(tenantId: string): Promise<ServiceResult<TenantCommunicationSettings>> {
    await api.post<TenantEmailSettingsResponse>(`/api/platform/tenants/${tenantId}/email-settings/reset-to-defaults`, {})
    return this.getCommunicationSettings(tenantId)
  },
  createDefaultCommunicationSettings(tenantId: string): TenantCommunicationSettings {
    return createDefaultTenantCommunicationSettings(tenantId)
  },
}

export const licensesApi = {
  async list(): Promise<ServiceResult<Array<Record<string, unknown>>>> {
    const rows = await api.get<Array<Record<string, unknown>>>('/api/platform/tenant-licenses')
    return { data: rows, backendAvailable: true }
  },
  async getPlans() {
    return api.get<Array<Record<string, unknown>>>('/api/platform/licensing/plans')
  },
  async savePlan(input: Record<string, unknown> & { id?: string }) {
    if (input.id && isGuid(input.id)) {
      return api.put<Record<string, unknown>>(`/api/platform/licensing/plans/${input.id}`, input)
    }
    return api.post<Record<string, unknown>>('/api/platform/licensing/plans', input)
  },
  async getSubscriptions() {
    return api.get<Array<Record<string, unknown>>>('/api/platform/licensing/subscriptions')
  },
  async saveSubscription(input: Record<string, unknown> & { id?: string }) {
    if (input.id && isGuid(input.id)) {
      return api.put<Record<string, unknown>>(`/api/platform/licensing/subscriptions/${input.id}`, input)
    }
    return api.post<Record<string, unknown>>('/api/platform/licensing/subscriptions', input)
  },
  async activateSubscription(id: string) {
    return api.post<Record<string, unknown>>(`/api/platform/licensing/subscriptions/${id}/activate`)
  },
  async suspendSubscription(id: string) {
    return api.post<Record<string, unknown>>(`/api/platform/licensing/subscriptions/${id}/suspend`)
  },
  async cancelSubscription(id: string) {
    return api.post<Record<string, unknown>>(`/api/platform/licensing/subscriptions/${id}/cancel`)
  },
}

async function fetchFinanceData(): Promise<FinanceData> {
  const [quotationsResponse, invoicesResponse, paymentsResponse, expensesResponse, taxesResponse, templatesResponse] = await Promise.all([
    api.get<PlatformQuotationResponse[]>('/api/platform/finance/quotations'),
    api.get<PlatformInvoiceResponse[]>('/api/platform/finance/invoices'),
    api.get<PlatformPaymentResponse[]>('/api/platform/finance/payments'),
    api.get<PlatformExpenseResponse[]>('/api/platform/finance/expenses'),
    api.get<PlatformTaxResponse[]>('/api/platform/finance/taxes'),
    api.get<PlatformTemplateResponse[]>('/api/platform/finance/templates'),
  ])

  const quotations = quotationsResponse.map(mapQuotation)
  const invoices = invoicesResponse.map(mapInvoice)
  const payments = paymentsResponse.map((item) => mapPayment(item, invoices))
  const expenses = expensesResponse.map(mapExpense)

  const defaultTax = taxesResponse.find((item) => item.isDefault) ?? taxesResponse[0] ?? { id: undefined, name: 'VAT', rate: 16, mode: 'Exclusive', isDefault: true }
  const templates: DocumentTemplate[] = templatesResponse
    .filter((item) => item.isActive)
    .map((item) => ({
      id: item.id,
      name: item.name,
      type: item.type,
      previewText: item.previewText,
      isDefault: item.isDefault,
    }))

  return {
    quotations,
    invoices,
    payments,
    expenses,
    taxSetting: {
      id: defaultTax.id,
      name: defaultTax.name,
      defaultRate: defaultTax.rate,
      mode: defaultTax.mode,
      isDefault: defaultTax.isDefault,
    },
    templates,
  }
}

export const financeApi = {
  async getAll(): Promise<ServiceResult<FinanceData>> {
    return { data: await fetchFinanceData(), backendAvailable: true }
  },
  async getSummary(): Promise<ServiceResult<FinanceSummary>> {
    const dashboard = await api.get<PlatformFinanceDashboardResponse>('/api/platform/finance/summary')
    const data = await fetchFinanceData()
    const now = new Date()
    const monthStart = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))
    const paidThisMonth = data.payments
      .filter((item) => item.status === 'Paid' && new Date(item.paymentDate) >= monthStart)
      .reduce((sum, item) => sum + item.amount, 0)

    return {
      data: {
        totalRevenue: dashboard.totalRevenue,
        outstandingInvoices: dashboard.outstandingInvoices,
        overdueInvoices: dashboard.overdueInvoices,
        expensesThisMonth: dashboard.expensesThisMonth,
        profitEstimate: dashboard.profitEstimate,
        quotationConversionRate: dashboard.quotationConversionRate,
        paidThisMonth,
        netPosition: dashboard.totalRevenue - dashboard.expensesThisMonth,
        recentPayments: dashboard.recentPayments.map((item) => mapPayment(item, data.invoices)),
        recentInvoices: dashboard.recentInvoices.map(mapInvoice),
        overdueAccounts: dashboard.overdueAccounts.map(mapInvoice),
      },
      backendAvailable: true,
    }
  },
  async saveQuotation(input: Quotation): Promise<ServiceResult<Quotation>> {
    const payload = {
      tenantId: isGuid(input.tenantId) ? input.tenantId : null,
      customerName: input.tenantName,
      customerEmail: null,
      currency: 'KES',
      taxRate: input.taxRate,
      discountRate: 0,
      status: input.status,
      validUntil: input.dueDate || null,
      notes: input.notes || null,
      lines: input.lines.map((line) => ({ description: line.description, quantity: line.quantity, unitPrice: line.unitPrice, taxable: true })),
    }

    if (isGuid(input.id)) {
      const updated = await api.put<PlatformQuotationResponse>(`/api/platform/finance/quotations/${input.id}`, payload)
      return { data: mapQuotation(updated), backendAvailable: true }
    }

    const created = await api.post<PlatformQuotationResponse>('/api/platform/finance/quotations', payload)
    return { data: mapQuotation(created), backendAvailable: true }
  },
  async saveInvoice(input: Invoice): Promise<ServiceResult<Invoice>> {
    const payload = {
      tenantId: isGuid(input.tenantId) ? input.tenantId : null,
      quotationId: null,
      customerName: input.tenantName,
      customerEmail: null,
      currency: 'KES',
      taxRate: input.taxRate,
      discountRate: 0,
      status: toApiInvoiceStatus(input.status),
      issueDate: input.issueDate || nowIso(),
      dueDate: input.dueDate || null,
      notes: input.notes || null,
      lines: input.lines.map((line) => ({ description: line.description, quantity: line.quantity, unitPrice: line.unitPrice, taxable: true })),
    }

    if (isGuid(input.id)) {
      const updated = await api.put<PlatformInvoiceResponse>(`/api/platform/finance/invoices/${input.id}`, payload)
      return { data: mapInvoice(updated), backendAvailable: true }
    }

    const created = await api.post<PlatformInvoiceResponse>('/api/platform/finance/invoices', payload)
    return { data: mapInvoice(created), backendAvailable: true }
  },
  async sendInvoice(id: string): Promise<ServiceResult<Invoice>> {
    const response = await api.post<PlatformInvoiceResponse>(`/api/platform/finance/invoices/${id}/send`)
    return { data: mapInvoice(response), backendAvailable: true }
  },
  async markInvoicePaid(id: string, amountPaid?: number): Promise<ServiceResult<Invoice>> {
    const response = await api.post<PlatformInvoiceResponse>(`/api/platform/finance/invoices/${id}/mark-paid`, amountPaid ? { amountPaid } : null)
    return { data: mapInvoice(response), backendAvailable: true }
  },
  async voidInvoice(id: string): Promise<ServiceResult<Invoice>> {
    const response = await api.post<PlatformInvoiceResponse>(`/api/platform/finance/invoices/${id}/void`)
    return { data: mapInvoice(response), backendAvailable: true }
  },
  async savePayment(input: Payment): Promise<ServiceResult<Payment>> {
    const payload = {
      invoiceId: isGuid(input.invoiceId) ? input.invoiceId : null,
      tenantId: isGuid(input.tenantId) ? input.tenantId : null,
      amount: input.amount,
      currency: 'KES',
      method: toApiPaymentMethod(input.method),
      status: input.status,
      reference: input.reference || null,
      paidAt: input.paymentDate,
      notes: null,
    }

    const created = await api.post<PlatformPaymentResponse>('/api/platform/finance/payments', payload)
    return { data: mapPayment(created, []), backendAvailable: true }
  },
  async saveExpense(input: Expense): Promise<ServiceResult<Expense>> {
    const payload = {
      expenseDate: input.expenseDate,
      category: input.category,
      vendor: input.vendor || null,
      description: input.description,
      amount: input.amount,
      taxAmount: input.taxAmount,
      currency: 'KES',
      paymentMethod: toApiPaymentMethod(input.paymentMethod),
      attachmentUrl: input.attachmentUrl || null,
      status: input.status || 'Draft',
      tenantId: isGuid(input.tenantId) ? input.tenantId : null,
    }

    if (isGuid(input.id)) {
      const updated = await api.put<PlatformExpenseResponse>(`/api/platform/finance/expenses/${input.id}`, payload)
      return { data: mapExpense(updated), backendAvailable: true }
    }

    const created = await api.post<PlatformExpenseResponse>('/api/platform/finance/expenses', payload)
    return { data: mapExpense(created), backendAvailable: true }
  },
  async approveExpense(id: string): Promise<ServiceResult<Expense>> {
    const response = await api.post<PlatformExpenseResponse>(`/api/platform/finance/expenses/${id}/approve`)
    return { data: mapExpense(response), backendAvailable: true }
  },
  async sendQuotation(id: string): Promise<ServiceResult<Quotation>> {
    const response = await api.post<PlatformQuotationResponse>(`/api/platform/finance/quotations/${id}/send`)
    return { data: mapQuotation(response), backendAvailable: true }
  },
  async approveQuotation(id: string): Promise<ServiceResult<Quotation>> {
    const response = await api.post<PlatformQuotationResponse>(`/api/platform/finance/quotations/${id}/approve`)
    return { data: mapQuotation(response), backendAvailable: true }
  },
  async convertQuotationToInvoice(quotationId: string): Promise<ServiceResult<Invoice>> {
    const invoice = await api.post<PlatformInvoiceResponse>(`/api/platform/finance/quotations/${quotationId}/convert-to-invoice`)
    return { data: mapInvoice(invoice), backendAvailable: true }
  },
  async saveTaxSetting(name: string, defaultRate: number, mode: TaxMode) {
    const taxes = await api.get<PlatformTaxResponse[]>('/api/platform/finance/taxes')
    const currentDefault = taxes.find((item) => item.isDefault) ?? taxes[0]

    if (currentDefault) {
      await api.put<PlatformTaxResponse>(`/api/platform/finance/taxes/${currentDefault.id}`, {
        name,
        rate: defaultRate,
        mode,
        isDefault: true,
      })
    } else {
      await api.post<PlatformTaxResponse>('/api/platform/finance/taxes', {
        name,
        rate: defaultRate,
        mode,
        isDefault: true,
      })
    }

    return this.getAll()
  },
  async saveTemplates(templates: DocumentTemplate[]) {
    for (const template of templates) {
      const payload = {
        name: template.name,
        type: template.type,
        previewText: template.previewText,
        isDefault: template.isDefault,
        isActive: true,
      }

      if (isGuid(template.id)) {
        await api.put<PlatformTemplateResponse>(`/api/platform/finance/templates/${template.id}`, payload)
      } else {
        await api.post<PlatformTemplateResponse>('/api/platform/finance/templates', payload)
      }
    }

    return this.getAll()
  },
}

export const platformUsersApi = {
  async list(): Promise<ServiceResult<PlatformUser[]>> {
    const users = await api.get<PlatformUserResponse[]>('/api/platform/users')
    return { data: users.map(mapPlatformUser), backendAvailable: true }
  },
  async save(input: PlatformUser): Promise<ServiceResult<PlatformUser>> {
    const payload = {
      fullName: input.fullName,
      email: input.email,
      phone: input.phone || null,
      role: toPlatformRole(input.role),
      status: input.status === 'Active' ? 'Active' : 'Inactive',
      password: null,
    }

    const response = isGuid(input.id)
      ? await api.put<PlatformUserResponse>(`/api/platform/users/${input.id}`, payload)
      : await api.post<PlatformUserResponse>('/api/platform/users', payload)

    return { data: mapPlatformUser(response), backendAvailable: true }
  },
  async updateStatus(id: string, status: PlatformUserStatus): Promise<ServiceResult<PlatformUser>> {
    const response = status === 'Active'
      ? await api.post<PlatformUserResponse>(`/api/platform/users/${id}/activate`)
      : await api.post<PlatformUserResponse>(`/api/platform/users/${id}/deactivate`)

    return { data: mapPlatformUser(response), backendAvailable: true }
  },
  async assignRole(id: string, role: PlatformRole): Promise<ServiceResult<PlatformUser>> {
    const response = await api.post<PlatformUserResponse>(`/api/platform/users/${id}/assign-role`, { role: toPlatformRole(role) })
    return { data: mapPlatformUser(response), backendAvailable: true }
  },
  async resetPassword(id: string, temporaryPassword?: string): Promise<ServiceResult<{ id: string; email: string; success: boolean; lastCredentialSentAt?: string | null; status: string; message?: string | null }>> {
    const response = await api.post<{ id: string; email: string; success: boolean; lastCredentialSentAt?: string | null; status: string; message?: string | null }>(`/api/platform/users/${id}/reset-password`, {
      temporaryPassword: temporaryPassword || null,
    })
    return { data: response, backendAvailable: true }
  },
  async resendCredentials(id: string): Promise<ServiceResult<{ id: string; email: string; success: boolean; lastCredentialSentAt?: string | null; status: string; message?: string | null }>> {
    const response = await api.post<{ id: string; email: string; success: boolean; lastCredentialSentAt?: string | null; status: string; message?: string | null }>(`/api/platform/users/${id}/resend-credentials`, {})
    return { data: response, backendAvailable: true }
  },
}

export const reportsApi = {
  async summary(): Promise<ServiceResult<ReportSummary & ReportsMeta>> {
    const summary = await api.get<PlatformReportsSummary>('/api/platform/reports/summary')
    const dashboard = await api.get<{
      totalTenants: number
      activeTenants: number
      deactivatedTenants: number
      suspendedTenants: number
      totalUsers: number
      openWorkOrders: number
      closedWorkOrders: number
      overdueWorkOrders: number
      totalInvoices: number
      overdueInvoices: number
      totalRevenue: number
      outstandingBalance: number
      monthlyRecurringRevenue: number
    }>('/api/platform/dashboard/summary')

    return {
      data: {
        totalTenants: dashboard.totalTenants ?? summary.totalTenants,
        activeTenants: dashboard.activeTenants ?? summary.activeTenants,
        deactivatedTenants: dashboard.deactivatedTenants ?? summary.deactivatedTenants ?? summary.inactiveTenants ?? 0,
        suspendedTenants: dashboard.suspendedTenants ?? summary.suspendedTenants ?? 0,
        totalUsers: dashboard.totalUsers ?? summary.totalUsers,
        openWorkOrders: dashboard.openWorkOrders ?? summary.openWorkOrders,
        closedWorkOrders: dashboard.closedWorkOrders ?? summary.closedWorkOrders,
        overdueWorkOrders: dashboard.overdueWorkOrders ?? summary.overdueWorkOrders,
        totalInvoices: dashboard.totalInvoices ?? summary.totalInvoices ?? 0,
        overdueInvoices: dashboard.overdueInvoices ?? summary.overdueInvoices ?? 0,
        totalRevenue: dashboard.totalRevenue ?? summary.totalRevenue ?? summary.monthlyRevenue ?? 0,
        outstandingInvoices: dashboard.outstandingBalance ?? summary.outstandingBalance ?? summary.outstandingInvoices ?? 0,
        monthlyRecurringRevenue: dashboard.monthlyRecurringRevenue ?? summary.monthlyRecurringRevenue ?? 0,
        asAtMonthStartUtc: summary.asAtMonthStartUtc,
        apiAvailable: true,
      },
      backendAvailable: true,
    }
  },
  async dashboardSummary() {
    return api.get<Record<string, unknown>>('/api/platform/dashboard/summary')
  },
  async tenants() {
    return api.get<Array<Record<string, unknown>>>('/api/platform/reports/tenants')
  },
  async revenue() {
    return api.get<Record<string, unknown>>('/api/platform/reports/revenue')
  },
  async subscriptions() {
    return api.get<Array<Record<string, unknown>>>('/api/platform/reports/subscriptions')
  },
  async workOrders() {
    return api.get<Record<string, unknown>>('/api/platform/reports/work-orders')
  },
  async finance() {
    return api.get<Record<string, unknown>>('/api/platform/reports/finance')
  },
  async audit() {
    return api.get<Array<Record<string, unknown>>>('/api/platform/reports/audit')
  },
}

export const auditLogsApi = {
  async list(filters?: {
    from?: string
    to?: string
    actorUserId?: string
    tenantId?: string
    action?: string
    entityType?: string
    severity?: string
  }): Promise<ServiceResult<AuditLog[]>> {
    const data = await api.get<PlatformAuditLogResponse[]>('/api/platform/audit-logs', {
      query: {
        from: filters?.from,
        to: filters?.to,
        actorUserId: filters?.actorUserId,
        tenantId: filters?.tenantId,
        action: filters?.action,
        entityType: filters?.entityType,
        severity: filters?.severity,
      },
    })

    return {
      data: data.map((item) => ({
        id: item.id,
        dateTime: item.createdAt,
        actor: item.actorName,
        action: item.action,
        entityType: item.entityType,
        entityId: item.entityId,
        entityName: item.description || undefined,
        tenant: item.tenantName || undefined,
        ipAddress: item.ipAddress || undefined,
        severity: item.severity,
      })),
      backendAvailable: true,
    }
  },
  async get(id: string) {
    return api.get<PlatformAuditLogResponse>(`/api/platform/audit-logs/${id}`)
  },
}

export const platformSettingsApi = {
  async get(): Promise<ServiceResult<PlatformSettings>> {
    const base = createDefaultSettings()
    const settings = await api.get<PlatformSettingsResponse>('/api/platform/settings')

    return {
      data: {
        ...base,
        companyProfile: {
          ...base.companyProfile,
          platformName: settings.platformName,
          supportEmail: settings.supportEmail || '',
        },
        branding: {
          ...base.branding,
          logoPlaceholder: settings.platformLogoUrl || '',
          primaryColor: settings.primaryColor,
          accentColor: settings.secondaryColor,
          poweredByText: settings.showPoweredByEcosys ? 'Powered by Ecosys' : '',
        },
        numberingRules: {
          ...base.numberingRules,
          quotationPrefix: settings.quotationNumberingPrefix,
          invoicePrefix: settings.invoiceNumberingPrefix,
          receiptPrefix: settings.receiptNumberingPrefix,
        },
        financeSettings: {
          ...base.financeSettings,
          defaultCurrency: settings.defaultCurrency,
        },
        taxSettings: {
          name: settings.taxName,
          defaultRate: settings.defaultTaxRate,
          mode: settings.taxMode,
        },
      },
      backendAvailable: true,
    }
  },
  async save(input: PlatformSettings): Promise<ServiceResult<PlatformSettings>> {
    await api.put<PlatformSettingsResponse>('/api/platform/settings', {
      platformName: input.companyProfile.platformName,
      supportEmail: input.companyProfile.supportEmail || null,
      defaultCurrency: input.financeSettings.defaultCurrency,
      taxName: input.taxSettings.name,
      defaultTaxRate: input.taxSettings.defaultRate,
      taxMode: input.taxSettings.mode,
      invoiceNumberingPrefix: input.numberingRules.invoicePrefix,
      quotationNumberingPrefix: input.numberingRules.quotationPrefix,
      receiptNumberingPrefix: input.numberingRules.receiptPrefix,
      platformLogoUrl: input.branding.logoPlaceholder || null,
      primaryColor: input.branding.primaryColor,
      secondaryColor: input.branding.accentColor,
      showPoweredByEcosys: Boolean(input.branding.poweredByText),
    })

    return this.get()
  },
}

export const platformService = {
  tenantsApi,
  licensesApi,
  financeApi,
  platformUsersApi,
  reportsApi,
  auditLogsApi,
  platformSettingsApi,
}

export function generateId(prefix: string) {
  return `${prefix}-${Math.random().toString(36).slice(2, 10)}`
}

export function toServiceError(error: unknown, fallback: string) {
  return safeMessage(error, fallback)
}
