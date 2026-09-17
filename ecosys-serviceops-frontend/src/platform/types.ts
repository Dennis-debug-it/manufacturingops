export type PlatformSummary = {
  totalTenants: number
  activeTenants: number
  activeUsersNow: number
  loggedInToday: number
  tenantsWithActiveUsers: number
}

export type PlatformDashboardSummary = {
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
}

export type TenantStatus =
  | 'Active'
  | 'Trial'
  | 'Suspended'
  | 'Inactive'

export type LicenseStatus =
  | 'Trial'
  | 'TrialExpiringSoon'
  | 'TrialExpired'
  | 'Active'
  | 'Expired'
  | 'Suspended'
  | 'Cancelled'

export type TrialStatus =
  | 'NotConfigured'
  | 'TrialActive'
  | 'TrialExpiringSoon'
  | 'TrialExpired'
  | 'TrialExtended'
  | 'PaidActive'
  | 'Suspended'
  | 'Inactive'

export interface PlatformTenantListItem {
  id?: string
  tenantId: string
  name: string
  slug: string
  contactName: string | null
  contactEmail: string | null
  planName: string | null
  licenseStatus: LicenseStatus | string
  userCount: number
  branchCount: number
  status: TenantStatus | string
  trialStartsAt: string | null
  trialEndsAt: string | null
  trialExtensionUsed: boolean
  trialExtendedAt: string | null
  trialDaysRemaining: number | null
  trialStatus: TrialStatus | string
  createdAt: string
}

export interface PlatformTenantDetail extends PlatformTenantListItem {
  serviceOpsEmail?: string | null
  timeZoneId?: string | null
  currency?: string | null
  contactPhone: string | null
  country: string | null
  county: string | null
  city: string | null
  address: string | null
  taxPin: string | null
  maxUsers: number | null
  maxBranches: number | null
  subscriptionStartsAt: string | null
  subscriptionEndsAt: string | null
  updatedAt: string | null
  suspendedAt: string | null
  deactivatedAt: string | null
  createdByUserId: string | null
  updatedByUserId: string | null
  workOrderCount: number
  activeUsersNow: number
  lastActivityAt: string | null
  initialAdminInvitationSent: boolean | null
  initialAdminInvitationMessage: string | null
}

export interface PlatformTenantAuditLog {
  id: string
  action: string
  actor: string
  details: string | null
  createdAt: string
}

export interface PlatformLeadListItem {
  id: string
  companyName: string
  contactPersonName: string
  email: string
  phone: string
  status: string
  createdAt: string
  contactedAt: string | null
  convertedTenantId: string | null
}

export interface PlatformLeadDetail extends PlatformLeadListItem {
  country: string | null
  industry: string | null
  companySize: string | null
  message: string | null
  preferredContactMethod: string | null
  updatedAt: string | null
  notes: string | null
}

export interface PlatformUser {
  id: string
  fullName: string
  email: string
  phone: string | null
  role: string
  status: string
  lastLoginAt: string | null
  createdAt: string
  updatedAt: string | null
  mustChangePassword: boolean
  lastCredentialSentAt: string | null
  credentialDelivery: {
    success: boolean
    status: string
    message: string | null
  } | null
}

export interface PlatformLicensePlan {
  id: string
  name: string
  code: string
  monthlyPrice: number
  annualPrice: number
  maxUsers: number | null
  maxBranches: number | null
  maxAssets: number | null
  maxWorkOrders: number | null
  modulesIncluded: string[]
  isActive: boolean
}

export interface PlatformSubscription {
  id: string
  tenantId: string
  tenantName: string | null
  planId: string
  planName: string | null
  status: string
  billingCycle: string
  startsAt: string
  endsAt: string | null
  trialEndsAt: string | null
  nextBillingDate: string | null
}

export interface PlatformFinanceDashboard {
  totalRevenue: number
  outstandingInvoices: number
  overdueInvoices: number
  expensesThisMonth: number
  profitEstimate: number
  quotationConversionRate: number
  recentPayments: PlatformPayment[]
  recentInvoices: PlatformInvoice[]
  overdueAccounts: PlatformInvoice[]
}

export interface PlatformQuotationLine {
  id: string
  description: string
  quantity: number
  unitPrice: number
  taxable: boolean
  lineTotal: number
}

export interface PlatformQuotation {
  id: string
  quotationNumber: string
  tenantId: string | null
  customerName: string
  customerEmail: string | null
  currency: string
  subtotal: number
  discountRate: number
  discountAmount: number
  taxRate: number
  taxAmount: number
  total: number
  status: string
  validUntil: string | null
  notes: string | null
  convertedInvoiceId: string | null
  lines: PlatformQuotationLine[]
  createdAt: string
  updatedAt: string | null
}

export interface PlatformInvoiceLine {
  id: string
  description: string
  quantity: number
  unitPrice: number
  taxable: boolean
  lineTotal: number
}

export interface PlatformInvoice {
  id: string
  invoiceNumber: string
  tenantId: string | null
  quotationId: string | null
  customerName: string
  customerEmail: string | null
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
  dueDate: string | null
  notes: string | null
  lines: PlatformInvoiceLine[]
  createdAt: string
  updatedAt: string | null
}

export interface PlatformPayment {
  id: string
  paymentNumber: string
  invoiceId: string | null
  tenantId: string | null
  amount: number
  currency: string
  method: string
  status: string
  reference: string | null
  paidAt: string
  notes: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PlatformExpense {
  id: string
  expenseDate: string
  category: string
  vendor: string | null
  description: string
  amount: number
  taxAmount: number
  totalAmount: number
  currency: string
  paymentMethod: string
  attachmentUrl: string | null
  status: string
  approvedAt: string | null
  tenantId: string | null
  createdAt: string
  updatedAt: string | null
}

export interface PlatformAuditLog {
  id: string
  tenantId: string | null
  actorUserId: string | null
  actorName: string
  action: string
  entityType: string
  entityId: string
  description: string | null
  ipAddress: string | null
  userAgent: string | null
  severity: string
  createdAt: string
  tenantName: string | null
}

export interface PlatformReportsSummary {
  totalTenants: number
  activeTenants: number
  totalRevenue: number
  outstandingBalance: number
  totalExpenses: number
  netPosition: number
}

export interface PlatformReportsTenantRow {
  id: string
  name: string
  status: string
  isActive: boolean
  createdAt: string
}

export interface PlatformReportsSubscriptionRow {
  id: string
  tenantName: string | null
  planName: string | null
  status: string
  billingCycle: string
}

export interface PlatformReportsRevenueRow {
  id: string
  period: string
  revenue: number
  expenses: number
  net: number
  outstandingBalance: number
  totalRevenue?: number
  totalExpenses?: number
  netPosition?: number
}

export interface PlatformReportsAuditRow {
  id: string
  createdAt: string
  actorName: string | null
  action: string
  entityType: string
  entityId: string
  severity: string
}

/**
 * Identity only — SMTP transport is global platform infrastructure with no per-tenant override, so
 * there is nothing transport-related left here. What differentiates a tenant's outbound mail is this
 * sender identity, layered on the platform's transport.
 */
export interface TenantEmailSettings {
  id: string
  tenantId: string
  senderName: string | null
  senderEmail: string | null
  replyToEmail: string | null
  enableTenantEmailNotifications: boolean
  lastTestedAt: string | null
  lastError: string | null
}

export interface TenantNotificationSetting {
  notificationKey: string
  emailEnabled: boolean
  inAppEnabled: boolean
  smsEnabled: boolean
  isActive: boolean
}

export interface TenantNotificationRecipient {
  recipientGroup: string
  email: string
  isActive: boolean
}

export interface TenantCommunicationSettings {
  emailSettings: TenantEmailSettings
  notificationSettings: TenantNotificationSetting[]
  recipients: TenantNotificationRecipient[]
}

export interface PlatformSettingsTemplate {
  id: string
  name: string
  type: string
  previewText: string
  isDefault: boolean
  isActive: boolean
  subject: string | null
  headerHtml: string | null
  bodyHtml: string | null
  footerHtml: string | null
  termsHtml: string | null
  signatureHtml: string | null
  pageSize: string
  orientation: string
  showLogo: boolean
  showTenantBranding: boolean
  showPoweredByEcosys: boolean
  createdByUserId: string | null
  updatedByUserId: string | null
  createdAt: string
  updatedAt: string | null
}
