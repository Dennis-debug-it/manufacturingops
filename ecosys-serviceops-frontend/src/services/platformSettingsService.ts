import { api } from '../lib/api'

export type PlatformSettingsSection =
  | 'general'
  | 'branding'
  | 'email'
  | 'numbering'
  | 'tax-finance'
  | 'security'
  | 'integrations'
  | 'system-preferences'

export type NumberingResetFrequency = 'Never' | 'Monthly' | 'Yearly'

export type PlatformTemplatePageSize = 'A4' | 'Letter' | 'Receipt'
export type PlatformTemplateOrientation = 'Portrait' | 'Landscape'

export type PlatformTemplateType =
  | 'Quotation template'
  | 'Invoice template'
  | 'Receipt template'
  | 'Payment receipt template'
  | 'Expense report template'
  | 'Work order report template'
  | 'Preventive maintenance report template'
  | 'Tenant onboarding email template'
  | 'SLA escalation email template'
  | 'Invoice overdue email template'
  | 'Quotation email template'
  | 'Payment confirmation email template'
  | string

export type PlatformNotificationPreference = {
  notificationKey: string
  emailEnabled: boolean
  inAppEnabled: boolean
  smsEnabled: boolean
  isActive: boolean
}

export type PlatformGeneralSettings = {
  platformName: string
  supportEmail: string
  defaultCountry: string
  defaultCurrency: string
  timezone: string
  companyLegalName: string
  companyRegistrationNumber: string
  companyPinTaxNumber: string
  defaultLanguage: string
}

export type PlatformBrandingSettings = {
  platformLogoUrl: string
  faviconUrl: string
  primaryColor: string
  secondaryColor: string
  accentColor: string
  showPoweredByEcosys: boolean
  loginPageBrandingPreview: boolean
  documentBrandingPreview: boolean
}

export type PlatformEmailSettings = {
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
  enableEmailNotifications: boolean
  enableSystemAlerts: boolean
  enableInvoiceEmails: boolean
  enableQuotationEmails: boolean
  enablePaymentReceiptEmails: boolean
  enableWorkOrderNotificationEmails: boolean
  enableSlaEscalationEmails: boolean
  enableTenantOnboardingEmails: boolean
  subjectPrefix: string
  subjectSuffix: string
  includeEnvironmentInSubject: boolean
  environmentLabel: string
  includeTenantNameInSubject: boolean
  enableEventSubjectTags: boolean
  notificationPreferences: PlatformNotificationPreference[]
  lastTestedAt?: string | null
  lastError?: string | null
}

export type PlatformNumberingRule = {
  id: string
  documentType: string
  prefix: string
  nextNumber: number
  paddingLength: number
  resetFrequency: NumberingResetFrequency
  preview: string
}

export type PlatformTaxFinanceSettings = {
  defaultVatRate: number
  enableVat: boolean
  taxName: string
  defaultPaymentTerms: string
  defaultInvoiceDueDays: number
  defaultQuotationValidityDays: number
  defaultExpenseApprovalRequired: boolean
  defaultCurrency: string
  invoiceNotes: string
  quotationTermsAndConditions: string
}

export type PlatformSecuritySettings = {
  requireStrongPasswords: boolean
  minimumPasswordLength: number
  requireEmailVerification: boolean
  sessionTimeoutMinutes: number
  failedLoginLockoutThreshold: number
  twoFactorAuthImplemented: boolean
  passwordResetExpiryMinutes: number
}

export type PlatformIntegrationsSettings = {
  mpesaDarajaEnabled: boolean
  mpesaConsumerKeyMasked: string
  mpesaConsumerSecretMasked: string
  mpesaConsumerKey?: string
  mpesaConsumerSecret?: string
  webhooksEnabled: boolean
  apiKeysEnabled: boolean
  futureMonitoringIntegrationsNotes: string
  emailSmtpEnabled: boolean
}

export type PlatformSystemPreferences = {
  dateFormat: string
  timeFormat: string
  defaultPaginationSize: number
  enableDarkModeDefault: boolean
  maintenanceMode: boolean
  showBetaModules: boolean
  allowTenantSelfRegistration: boolean
}

export type PlatformTemplate = {
  id: string
  name: string
  type: PlatformTemplateType
  previewText: string
  isDefault: boolean
  isActive: boolean
  subject: string
  headerHtml: string
  bodyHtml: string
  footerHtml: string
  termsHtml: string
  signatureHtml: string
  pageSize: PlatformTemplatePageSize
  orientation: PlatformTemplateOrientation
  showLogo: boolean
  showTenantBranding: boolean
  showPoweredByEcosys: boolean
  createdByUserId?: string | null
  updatedByUserId?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type PlatformTemplatePreview = {
  id: string
  name: string
  type: string
  subject: string
  headerHtml: string
  bodyHtml: string
  footerHtml: string
  termsHtml: string
  signatureHtml: string
  renderedHtml: string
}

export type PlatformEmailActionResponse = {
  success: boolean
  lastTestedAt?: string | null
  lastError?: string | null
  message?: string | null
}

export type PlatformEmailTemplate = {
  eventKey: string
  templateName: string
  subject: string
  htmlBody: string
  textBody: string
  enabled: boolean
  senderNameOverride?: string | null
  replyToOverride?: string | null
  availablePlaceholders: string[]
  requiredPlaceholders: string[]
  supportsTenantOverride: boolean
  isOverride: boolean
  source: string
  lastUpdatedBy?: string | null
  lastUpdatedAt?: string | null
}

export type PlatformEmailTemplatePreview = {
  eventKey: string
  templateName: string
  templateSubject: string
  finalSubject: string
  htmlBody: string
  textBody: string
}

export type PlatformEmailTemplateTestResponse = {
  success: boolean
  message?: string | null
}

export type PlatformEmailNotificationRule = {
  eventKey: string
  displayName: string
  templateKey: string
  recipientStrategy: string
  senderScope: string
  dispatchStatus: string
  description: string
  supportedChannels: string[]
  notes: string
}

export type PlatformEmailDeliveryLog = {
  id: string
  tenantId?: string | null
  eventKey: string
  templateKey: string
  recipientEmail: string
  subject: string
  status: string
  attemptCount?: number | null
  lastAttemptAt?: string | null
  nextAttemptAt?: string | null
  errorCategory?: string | null
  errorMessage?: string | null
  triggeredByUserId?: string | null
  createdAt: string
  sentAt?: string | null
  providerMessageId?: string | null
}

export type PlatformEmailTemplatePayload = {
  templateName: string
  subject: string
  htmlBody: string
  textBody?: string | null
  enabled: boolean
  senderNameOverride?: string | null
  replyToOverride?: string | null
}

type PlatformTemplatePayload = {
  name: string
  type: string
  previewText?: string | null
  isDefault: boolean
  isActive: boolean
  subject?: string | null
  headerHtml?: string | null
  bodyHtml?: string | null
  footerHtml?: string | null
  termsHtml?: string | null
  signatureHtml?: string | null
  showLogo: boolean
  showTenantBranding: boolean
  showPoweredByEcosys: boolean
  pageSize: PlatformTemplatePageSize
  orientation: PlatformTemplateOrientation
}

function toPlainString(value: string | null | undefined) {
  return value ?? ''
}

export const platformSettingsService = {
  getGeneral() {
    return api.get<PlatformGeneralSettings>('/api/platform/settings/general')
  },
  updateGeneral(input: PlatformGeneralSettings) {
    return api.put<PlatformGeneralSettings>('/api/platform/settings/general', input)
  },

  getBranding() {
    return api.get<PlatformBrandingSettings>('/api/platform/settings/branding')
  },
  updateBranding(input: PlatformBrandingSettings) {
    return api.put<PlatformBrandingSettings>('/api/platform/settings/branding', input)
  },

  getEmail() {
    return api.get<PlatformEmailSettings>('/api/platform/settings/email')
  },
  updateEmail(input: PlatformEmailSettings) {
    return api.put<PlatformEmailSettings>('/api/platform/settings/email', {
      ...input,
      smtpPasswordSecret: input.smtpPasswordSecret || null,
      replyToEmail: input.replyToEmail || null,
      smtpUsername: input.smtpUsername || null,
      apiEndpoint: input.apiEndpoint || null,
      apiKeySecret: input.apiKeySecret || null,
      apiProviderName: input.apiProviderName || null,
      subjectSuffix: input.subjectSuffix || null,
      environmentLabel: input.environmentLabel || null,
    })
  },
  sendTestEmail(testRecipientEmail?: string) {
    return api.post<PlatformEmailActionResponse>('/api/platform/settings/email/test', { testRecipientEmail: testRecipientEmail || null })
  },
  verifySmtpConnection() {
    return api.post<PlatformEmailActionResponse>('/api/platform/settings/email/verify', {})
  },
  listNotificationRules() {
    return api.get<PlatformEmailNotificationRule[]>('/api/platform/settings/email/notification-rules')
  },
  listDeliveryLogs(filters?: {
    status?: string
    templateKey?: string
    eventKey?: string
    recipientEmail?: string
    dateFrom?: string
    dateTo?: string
  }) {
    const params = new URLSearchParams()
    Object.entries(filters || {}).forEach(([key, value]) => {
      if (value) params.set(key, value)
    })
    const query = params.toString()
    return api.get<PlatformEmailDeliveryLog[]>(`/api/platform/settings/email/delivery-logs${query ? `?${query}` : ''}`)
  },
  listEmailTemplates() {
    return api.get<PlatformEmailTemplate[]>('/api/platform/settings/email-templates')
  },
  getEmailTemplate(eventKey: string) {
    return api.get<PlatformEmailTemplate>(`/api/platform/settings/email-templates/${eventKey}`)
  },
  updateEmailTemplate(eventKey: string, input: PlatformEmailTemplatePayload) {
    return api.put<PlatformEmailTemplate>(`/api/platform/settings/email-templates/${eventKey}`, input)
  },
  previewEmailTemplate(eventKey: string, sampleData?: Record<string, string | null>) {
    return api.post<PlatformEmailTemplatePreview>(`/api/platform/settings/email-templates/${eventKey}/preview`, { sampleData: sampleData || null })
  },
  testEmailTemplate(eventKey: string, testRecipientEmail?: string, sampleData?: Record<string, string | null>) {
    return api.post<PlatformEmailTemplateTestResponse>(`/api/platform/settings/email-templates/${eventKey}/test`, {
      testRecipientEmail: testRecipientEmail || null,
      sampleData: sampleData || null,
    })
  },
  resetEmailTemplate(eventKey: string) {
    return api.post<PlatformEmailTemplate>(`/api/platform/settings/email-templates/${eventKey}/reset`, {})
  },

  getNumberingRules() {
    return api.get<PlatformNumberingRule[]>('/api/platform/settings/numbering/rules')
  },
  updateNumberingRules(input: PlatformNumberingRule[]) {
    return api.put<PlatformNumberingRule[]>('/api/platform/settings/numbering/rules', input)
  },

  getTaxFinance() {
    return api.get<PlatformTaxFinanceSettings>('/api/platform/settings/tax-finance')
  },
  updateTaxFinance(input: PlatformTaxFinanceSettings) {
    return api.put<PlatformTaxFinanceSettings>('/api/platform/settings/tax-finance', input)
  },

  getSecurity() {
    return api.get<PlatformSecuritySettings>('/api/platform/settings/security')
  },
  updateSecurity(input: PlatformSecuritySettings) {
    return api.put<PlatformSecuritySettings>('/api/platform/settings/security', input)
  },

  getIntegrations() {
    return api.get<PlatformIntegrationsSettings>('/api/platform/settings/integrations')
  },
  updateIntegrations(input: PlatformIntegrationsSettings) {
    return api.put<PlatformIntegrationsSettings>('/api/platform/settings/integrations', input)
  },

  getSystemPreferences() {
    return api.get<PlatformSystemPreferences>('/api/platform/settings/system-preferences')
  },
  updateSystemPreferences(input: PlatformSystemPreferences) {
    return api.put<PlatformSystemPreferences>('/api/platform/settings/system-preferences', input)
  },

  listTemplates() {
    return api.get<PlatformTemplate[]>('/api/platform/settings/templates')
  },
  getTemplate(id: string) {
    return api.get<PlatformTemplate>(`/api/platform/settings/templates/${id}`)
  },
  createTemplate(input: PlatformTemplatePayload) {
    return api.post<PlatformTemplate>('/api/platform/settings/templates', input)
  },
  updateTemplate(id: string, input: PlatformTemplatePayload) {
    return api.put<PlatformTemplate>(`/api/platform/settings/templates/${id}`, input)
  },
  duplicateTemplate(id: string) {
    return api.post<PlatformTemplate>(`/api/platform/settings/templates/${id}/duplicate`, {})
  },
  activateTemplate(id: string) {
    return api.post<PlatformTemplate>(`/api/platform/settings/templates/${id}/activate`, {})
  },
  deactivateTemplate(id: string) {
    return api.post<PlatformTemplate>(`/api/platform/settings/templates/${id}/deactivate`, {})
  },
  makeDefaultTemplate(id: string) {
    return api.post<PlatformTemplate>(`/api/platform/settings/templates/${id}/make-default`, {})
  },
  previewTemplate(id: string, sampleData?: Record<string, string>) {
    return api.post<PlatformTemplatePreview>(`/api/platform/settings/templates/${id}/preview`, { sampleData: sampleData || null })
  },
  deleteTemplate(id: string) {
    return api.delete<void>(`/api/platform/settings/templates/${id}`)
  },
}

export function createEmptyTemplateDraft(type: PlatformTemplateType = 'Invoice template'): PlatformTemplate {
  const now = new Date().toISOString()
  return {
    id: '',
    name: '',
    type,
    previewText: '',
    isDefault: false,
    isActive: true,
    subject: '',
    headerHtml: '',
    bodyHtml: '',
    footerHtml: '',
    termsHtml: '',
    signatureHtml: '',
    pageSize: 'A4',
    orientation: 'Portrait',
    showLogo: true,
    showTenantBranding: true,
    showPoweredByEcosys: true,
    createdAt: now,
    updatedAt: now,
  }
}

export function toTemplatePayload(template: PlatformTemplate): PlatformTemplatePayload {
  return {
    name: template.name.trim(),
    type: template.type.trim(),
    previewText: template.previewText?.trim() || null,
    isDefault: template.isDefault,
    isActive: template.isActive,
    subject: template.subject?.trim() || null,
    headerHtml: template.headerHtml?.trim() || null,
    bodyHtml: template.bodyHtml?.trim() || null,
    footerHtml: template.footerHtml?.trim() || null,
    termsHtml: template.termsHtml?.trim() || null,
    signatureHtml: template.signatureHtml?.trim() || null,
    pageSize: template.pageSize,
    orientation: template.orientation,
    showLogo: template.showLogo,
    showTenantBranding: template.showTenantBranding,
    showPoweredByEcosys: template.showPoweredByEcosys,
  }
}

export function normalizeTemplate(item: PlatformTemplate): PlatformTemplate {
  return {
    ...item,
    subject: toPlainString(item.subject),
    headerHtml: toPlainString(item.headerHtml),
    bodyHtml: toPlainString(item.bodyHtml),
    footerHtml: toPlainString(item.footerHtml),
    termsHtml: toPlainString(item.termsHtml),
    signatureHtml: toPlainString(item.signatureHtml),
    previewText: toPlainString(item.previewText),
    pageSize: (item.pageSize || 'A4') as PlatformTemplatePageSize,
    orientation: (item.orientation || 'Portrait') as PlatformTemplateOrientation,
  }
}
