export type TenantStatus = 'Active' | 'Suspended' | 'Inactive' | 'Trial'

export type LicenseStatus = 'Active' | 'Trial' | 'Expired' | 'Suspended'

export type Plan = 'Trial' | 'Starter' | 'Growth' | 'Professional' | 'Enterprise' | string

export interface Tenant {
  tenantId: string
  name: string
  slug: string
  companyEmail?: string
  companyPhone?: string
  country?: string
  industry?: string
  contactPerson: string
  contactEmail: string
  contactPhone?: string
  plan: Plan
  licenseStatus: LicenseStatus
  users: number
  branches: number
  status: TenantStatus
  createdAt: string
  maxUsers?: number | null
  maxBranches?: number | null
  trialStartDate?: string | null
  trialEndDate?: string | null
  trialExtensionUsed?: boolean
  trialExtendedAt?: string | null
  trialDaysRemaining?: number | null
  trialStatus?: string
  createDefaultAdmin?: boolean
  usePrimaryContactAsWorkspaceAdmin?: boolean
  adminFullName?: string
  adminEmail?: string
  adminPhone?: string
  initialAdminInvitationSent?: boolean | null
  initialAdminInvitationMessage?: string | null
  /** Subscription (paid plan) start date */
  subscriptionStartsAt?: string | null
  /** Subscription (paid plan) end/renewal date */
  subscriptionEndsAt?: string | null
  /** Total work orders for this tenant (from list endpoint when available) */
  workOrderCount?: number | null
  /** Total assets for this tenant (from list endpoint when available) */
  assetCount?: number | null
  /** Last recorded user activity timestamp */
  lastActivityAt?: string | null
  /** Enabled module keys */
  modules?: string[] | null
}

export type TenantNotificationKey =
  | 'work-order.new-created'
  | 'work-order.assigned'
  | 'work-order.updated'
  | 'work-order.completed'
  | 'work-order.overdue'
  | 'sla.warning'
  | 'sla.breach'
  | 'pm.due'
  | 'pm.overdue'
  | 'pm.completed'
  | 'asset.created'
  | 'asset.updated'
  | 'asset.deactivated'
  | 'asset.maintenance-due'
  | 'materials.request-submitted'
  | 'materials.request-approved'
  | 'materials.request-rejected'
  | 'materials.request-issued'
  | 'materials.low-stock-alert'
  | 'users.invited'
  | 'users.activated'
  | 'users.deactivated'
  | 'users.role-changed'
  | 'tenant.profile-updated'
  | 'tenant.branding-updated'
  | 'tenant.module-access-changed'
  | 'tenant.subscription-status-changed'
  | 'system.alerts'
  | 'system.failed-login-attempts'
  | 'system.integration-errors'
  | 'system.background-job-failures'

export type TenantRecipientGroup =
  | 'Admin'
  | 'Operations'
  | 'SLAEscalation'
  | 'Dispatch'
  | 'Maintenance'
  | 'Assets'
  | 'Materials'
  | 'SystemAlerts'

export interface TenantEmailSettings {
  id: string
  tenantId: string
  usePlatformDefaults: boolean
  overrideSmtpSettings: boolean
  deliveryMode: 'Smtp' | 'Api' | 'Disabled'
  smtpHost: string
  smtpPort: number
  smtpUsername: string
  smtpPasswordMasked: string
  smtpPasswordSecret?: string
  senderName: string
  senderEmail: string
  replyToEmail: string
  enableSslTls: boolean
  secureMode: 'Auto' | 'None' | 'StartTls' | 'SslOnConnect'
  apiEndpoint: string
  apiKeyMasked: string
  apiKeySecret?: string
  apiProviderName: string
  timeoutSeconds: number
  maxRetries: number
  enableTenantEmailNotifications: boolean
  lastTestedAt?: string | null
  lastError?: string | null
}

export interface TenantNotificationSetting {
  notificationKey: TenantNotificationKey | string
  emailEnabled: boolean
  inAppEnabled: boolean
  smsEnabled: boolean
  isActive: boolean
}

export interface TenantNotificationRecipient {
  recipientGroup: TenantRecipientGroup | string
  email: string
  isActive: boolean
}

export interface TenantCommunicationSettings {
  emailSettings: TenantEmailSettings
  notificationSettings: TenantNotificationSetting[]
  recipients: TenantNotificationRecipient[]
}

export interface TenantCommunicationActionResponse {
  success: boolean
  lastTestedAt?: string | null
  lastError?: string | null
}

export type PlatformRole =
  | 'PlatformOwner'
  | 'PlatformAdmin'
  | 'SupportAdmin'
  | 'FinanceAdmin'
  | 'ReadOnlyAuditor'
  | 'Platform SuperAdmin'
  | 'Platform Admin'
  | 'SuperAdmin'
  | 'Support'
  | 'Finance'
  | 'Auditor'

export type PlatformUserStatus = 'Active' | 'Disabled' | 'Inactive'

export interface PlatformUser {
  id: string
  fullName: string
  email: string
  phone?: string | null
  role: PlatformRole
  status: PlatformUserStatus
  lastLogin?: string | null
  createdAt?: string
  updatedAt?: string | null
  mustChangePassword?: boolean
  lastCredentialSentAt?: string | null
  credentialDelivery?: {
    success: boolean
    status: string
    message?: string | null
  } | null
}

export type QuoteStatus = 'Draft' | 'Sent' | 'Accepted' | 'Rejected' | 'Expired' | 'Converted'

export interface QuotationLine {
  id: string
  description: string
  quantity: number
  unitPrice: number
}

export interface Quotation {
  id: string
  number: string
  tenantId?: string
  tenantName: string
  issueDate: string
  dueDate?: string | null
  status: QuoteStatus
  taxRate: number
  taxMode: TaxMode
  lines: QuotationLine[]
  subtotal: number
  taxAmount: number
  total: number
  templateId?: string
  notes?: string
  convertedInvoiceId?: string | null
}

export type InvoiceStatus = 'Draft' | 'Sent' | 'PartiallyPaid' | 'Paid' | 'Overdue' | 'Void' | 'Issued' | 'Cancelled'

export interface InvoiceLine {
  id: string
  description: string
  quantity: number
  unitPrice: number
}

export interface Invoice {
  id: string
  number: string
  tenantId?: string
  tenantName: string
  issueDate: string
  dueDate?: string | null
  status: InvoiceStatus
  taxRate: number
  taxMode: TaxMode
  lines: InvoiceLine[]
  subtotal: number
  taxAmount: number
  total: number
  paidAmount: number
  balance: number
  templateId?: string
  notes?: string
}

export type PaymentMethod = 'Cash' | 'M-Pesa' | 'Bank Transfer' | 'Bank transfer' | 'Cheque' | 'Card' | 'Other'

export type PaymentStatus = 'Pending' | 'Paid' | 'Failed' | 'Reversed'

export interface Payment {
  id: string
  invoiceId?: string
  invoiceNumber?: string
  tenantId?: string
  tenantName?: string
  amount: number
  paymentDate: string
  method: PaymentMethod
  reference?: string
  status: PaymentStatus
}

export interface Expense {
  id: string
  expenseDate: string
  category: string
  vendor: string
  description: string
  amount: number
  taxAmount: number
  totalAmount?: number
  paymentMethod: PaymentMethod
  tenantId?: string
  tenantName?: string
  attachmentName?: string
  attachmentUrl?: string | null
  status?: string
  approvedAt?: string | null
}

export type TaxMode = 'Inclusive' | 'Exclusive'

export interface TaxSetting {
  id?: string
  name: string
  defaultRate: number
  mode: TaxMode
  isDefault?: boolean
}

export type DocumentTemplateType = 'Quotation' | 'Invoice' | 'Receipt'

export interface DocumentTemplate {
  id: string
  name: string
  type: DocumentTemplateType
  previewText: string
  isDefault: boolean
}

export interface FinanceSummary {
  totalRevenue: number
  outstandingInvoices: number
  overdueInvoices: number
  expensesThisMonth: number
  profitEstimate: number
  quotationConversionRate: number
  paidThisMonth?: number
  netPosition?: number
  activePaidTenants?: number
  trialTenants?: number
  expiredOrUnpaidTenants?: number
  recentPayments: Payment[]
  recentInvoices: Invoice[]
  overdueAccounts: Invoice[]
}

export interface FinanceData {
  quotations: Quotation[]
  invoices: Invoice[]
  payments: Payment[]
  expenses: Expense[]
  taxSetting: TaxSetting
  templates: DocumentTemplate[]
}

export interface AuditLog {
  id: string
  dateTime: string
  actor: string
  action: string
  entityType: string
  entityId: string
  entityName?: string
  tenant?: string
  ipAddress?: string
  severity?: string
}

export interface CompanyProfileSettings {
  platformName: string
  supportEmail: string
  supportPhone: string
  address: string
  website: string
}

export interface BrandingSettings {
  logoPlaceholder: string
  primaryColor: string
  accentColor: string
  poweredByText: string
}

export interface LicensingRulesSettings {
  defaultTrialDays: number
  licenseExpiryWarningDays: number
  gracePeriodDays: number
  autoSuspendExpiredTenant: boolean
}

export interface NumberingRulesSettings {
  tenantCodePrefix: string
  quotationPrefix: string
  invoicePrefix: string
  receiptPrefix: string
}

export interface FinanceSettings {
  defaultCurrency: string
  paymentMethodsEnabled: PaymentMethod[]
  invoiceDueDays: number
}

export interface SecuritySettings {
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireNumber: boolean
  requireSymbol: boolean
  passwordExpiryDays: number
  adminCanResetUserPassword: boolean
  otpTodo: boolean
  ssoTodo: boolean
}

export interface EmailSettings {
  senderName: string
  senderEmail: string
  smtpHost: string
  smtpPort: number
  useSsl: boolean
}

export interface NotificationSettings {
  emailAlertsEnabled: boolean
  licenseExpiryAlerts: boolean
  tenantStatusAlerts: boolean
  dailyDigestEnabled: boolean
}

export interface IntegrationsSettings {
  monitoringWebhookEnabled: boolean
  emailIntakeEnabled: boolean
  notes?: string
}

export interface PlatformSettings {
  companyProfile: CompanyProfileSettings
  branding: BrandingSettings
  licensingRules: LicensingRulesSettings
  numberingRules: NumberingRulesSettings
  financeSettings: FinanceSettings
  taxSettings: TaxSetting
  emailSettings: EmailSettings
  security: SecuritySettings
  notifications: NotificationSettings
  integrations: IntegrationsSettings
}

export interface ReportSummary {
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
  outstandingInvoices: number
  monthlyRecurringRevenue: number
  asAtMonthStartUtc: string
}

export interface ReportsMeta {
  apiAvailable: boolean
}

export interface ServiceResult<T> {
  data: T
  backendAvailable: boolean
  message?: string
}
