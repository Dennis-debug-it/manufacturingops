import { api } from '../lib/api'
import type {
  AssignmentGroupRecord,
  EmailIntakeSettings,
  EmailNotificationSettings,
  EmailTemplatePreviewResponse,
  EmailTemplateRecord,
  EmailTemplateTestResponse,
  IntakeProtocolRecord,
  IntakeProtocolTestResponse,
  MonitoringWebhookIntegrationRecord,
  MonitoringSettings,
  NumberingRuleRecord,
  NotificationSettings,
  TenantSecuritySettings,
  TenantOperationsPolicySettings,
  UpsertIntakeProtocolInput,
  UpdateEmailIntakeSettingsInput,
  UpdateEmailNotificationSettingsInput,
  UpdateEmailTemplateInput,
  UpdateNumberingRuleInput,
  UpsertMonitoringWebhookIntegrationInput,
  UpsertAssignmentGroupInput,
} from '../types/api'
import { asArray } from '../utils/apiDefaults'

export const settingsService = {
  async listAssignmentGroups(signal?: AbortSignal): Promise<AssignmentGroupRecord[]> {
    const response = await api.get<unknown>('/api/assignment-groups', { signal })
    return asArray<AssignmentGroupRecord>(response).map((group) => ({
      ...group,
      members: group.members ?? [],
      technicianIds: group.members?.map((member) => member.technicianId) ?? group.technicianIds ?? [],
    }))
  },
  getAssignmentGroup(id: string, signal?: AbortSignal) {
    return api.get<AssignmentGroupRecord>(`/api/assignment-groups/${id}`, { signal })
  },
  createAssignmentGroup(input: UpsertAssignmentGroupInput) {
    return api.post<AssignmentGroupRecord>('/api/assignment-groups', {
      ...input,
      members: input.members?.length ? input.members : (input.technicianIds ?? []).map((technicianId) => ({ technicianId, isLead: false })),
    })
  },
  updateAssignmentGroup(id: string, input: UpsertAssignmentGroupInput) {
    return api.put<AssignmentGroupRecord>(`/api/assignment-groups/${id}`, {
      ...input,
      members: input.members?.length ? input.members : (input.technicianIds ?? []).map((technicianId) => ({ technicianId, isLead: false })),
    })
  },
  deleteAssignmentGroup(id: string) {
    return api.delete<void>(`/api/assignment-groups/${id}`)
  },
  getAssignmentGroupMembers(id: string, signal?: AbortSignal) {
    return api.get<AssignmentGroupRecord['members']>(`/api/assignment-groups/${id}/members`, { signal })
  },
  addAssignmentGroupMember(id: string, input: { userId?: string | null; technicianId?: string | null; isLead: boolean }) {
    return api.post<AssignmentGroupRecord['members'][number]>(`/api/assignment-groups/${id}/members`, input)
  },
  removeAssignmentGroupMember(id: string, userId: string) {
    return api.delete<void>(`/api/assignment-groups/${id}/members/${userId}`)
  },
  getOperationsPolicy(signal?: AbortSignal) {
    return api.get<TenantOperationsPolicySettings>('/api/settings/operations-policy', { signal })
  },
  updateOperationsPolicy(input: TenantOperationsPolicySettings) {
    return api.put<TenantOperationsPolicySettings>('/api/settings/operations-policy', input)
  },
  getSecurity(signal?: AbortSignal) {
    return api.get<TenantSecuritySettings>('/api/settings/security', { signal })
  },
  updateSecurity(input: TenantSecuritySettings) {
    return api.put<TenantSecuritySettings>('/api/settings/security', input)
  },
  getNotifications(signal?: AbortSignal) {
    return api.get<NotificationSettings>('/api/settings/notifications', { signal })
  },
  updateNotifications(input: NotificationSettings) {
    return api.put<NotificationSettings>('/api/settings/notifications', input)
  },
  getEmailNotifications(signal?: AbortSignal) {
    return api.get<EmailNotificationSettings>('/api/settings/email-notifications', { signal })
  },
  updateEmailNotifications(input: UpdateEmailNotificationSettingsInput) {
    return api.put<EmailNotificationSettings>('/api/settings/email-notifications', input)
  },
  testEmailNotifications(testRecipientEmail: string) {
    return api.post<{ success: boolean; lastTestedAt?: string | null; lastError?: string | null }>('/api/settings/email-notifications/test', { testRecipientEmail })
  },
  listEmailTemplates(signal?: AbortSignal) {
    return api.get<EmailTemplateRecord[]>('/api/settings/email-templates', { signal })
  },
  getEmailTemplate(eventKey: string, signal?: AbortSignal) {
    return api.get<EmailTemplateRecord>(`/api/settings/email-templates/${eventKey}`, { signal })
  },
  updateEmailTemplate(eventKey: string, input: UpdateEmailTemplateInput) {
    return api.put<EmailTemplateRecord>(`/api/settings/email-templates/${eventKey}`, input)
  },
  previewEmailTemplate(eventKey: string, sampleData?: Record<string, string | null>) {
    return api.post<EmailTemplatePreviewResponse>(`/api/settings/email-templates/${eventKey}/preview`, { sampleData: sampleData || null })
  },
  testEmailTemplate(eventKey: string, testRecipientEmail?: string, sampleData?: Record<string, string | null>) {
    return api.post<EmailTemplateTestResponse>(`/api/settings/email-templates/${eventKey}/test`, {
      testRecipientEmail: testRecipientEmail || null,
      sampleData: sampleData || null,
    })
  },
  resetEmailTemplate(eventKey: string) {
    return api.post<EmailTemplateRecord>(`/api/settings/email-templates/${eventKey}/reset`, {})
  },
  getEmailIntake(signal?: AbortSignal) {
    return api.get<EmailIntakeSettings>('/api/settings/email-intake', { signal })
  },
  updateEmailIntake(input: UpdateEmailIntakeSettingsInput) {
    return api.put<EmailIntakeSettings>('/api/settings/email-intake', input)
  },
  testEmailIntakeConnection(host: string, port: number) {
    return api.post<{ success: boolean; lastCheckedAt?: string | null; lastError?: string | null }>('/api/settings/email-intake/test-connection', { host, port })
  },
  async listIntakeProtocols(signal?: AbortSignal): Promise<IntakeProtocolRecord[]> {
    const response = await api.get<unknown>('/api/settings/intake-protocols', { signal })
    return asArray<IntakeProtocolRecord>(response)
  },
  getIntakeProtocol(id: string, signal?: AbortSignal) {
    return api.get<IntakeProtocolRecord>(`/api/settings/intake-protocols/${id}`, { signal })
  },
  createIntakeProtocol(input: UpsertIntakeProtocolInput) {
    return api.post<IntakeProtocolRecord>('/api/settings/intake-protocols', input)
  },
  updateIntakeProtocol(id: string, input: UpsertIntakeProtocolInput) {
    return api.put<IntakeProtocolRecord>(`/api/settings/intake-protocols/${id}`, input)
  },
  deleteIntakeProtocol(id: string) {
    return api.delete<void>(`/api/settings/intake-protocols/${id}`)
  },
  testIntakeProtocol(id: string) {
    return api.post<IntakeProtocolTestResponse>(`/api/settings/intake-protocols/${id}/test`, {})
  },
  getMonitoring(signal?: AbortSignal) {
    return api.get<MonitoringSettings>('/api/settings/monitoring', { signal })
  },
  updateMonitoring(input: MonitoringSettings) {
    return api.put<MonitoringSettings>('/api/settings/monitoring', input)
  },
  async listMonitoringWebhooks(signal?: AbortSignal): Promise<MonitoringWebhookIntegrationRecord[]> {
    const response = await api.get<unknown>('/api/settings/monitoring-webhooks', { signal })
    return asArray<MonitoringWebhookIntegrationRecord>(response)
  },
  getMonitoringWebhook(id: string, signal?: AbortSignal) {
    return api.get<MonitoringWebhookIntegrationRecord>(`/api/settings/monitoring-webhooks/${id}`, { signal })
  },
  createMonitoringWebhook(input: UpsertMonitoringWebhookIntegrationInput) {
    return api.post<MonitoringWebhookIntegrationRecord>('/api/settings/monitoring-webhooks', input)
  },
  updateMonitoringWebhook(id: string, input: UpsertMonitoringWebhookIntegrationInput) {
    return api.put<MonitoringWebhookIntegrationRecord>(`/api/settings/monitoring-webhooks/${id}`, input)
  },
  deleteMonitoringWebhook(id: string) {
    return api.delete<void>(`/api/settings/monitoring-webhooks/${id}`)
  },
  rotateMonitoringWebhookSecret(id: string) {
    return api.post<MonitoringWebhookIntegrationRecord>(`/api/settings/monitoring-webhooks/${id}/rotate-secret`, {})
  },
  testMonitoringWebhook(id: string) {
    return api.post<{ success: boolean; lastReceivedAt?: string | null; lastStatus?: string | null; lastError?: string | null }>(`/api/settings/monitoring-webhooks/${id}/test`, {})
  },
  async listNumberingRules(signal?: AbortSignal): Promise<NumberingRuleRecord[]> {
    const response = await api.get<unknown>('/api/settings/numbering-rules', { signal })
    return asArray<NumberingRuleRecord>(response)
  },
  updateNumberingRule(id: string, input: UpdateNumberingRuleInput) {
    return api.put<NumberingRuleRecord>(`/api/settings/numbering-rules/${id}`, input)
  },
  resetNumberingRule(id: string, startFrom: number) {
    return api.post<NumberingRuleRecord>(`/api/settings/numbering-rules/${id}/reset`, { startFrom })
  },
  async seedDefaultNumberingRules() {
    const response = await api.post<unknown>('/api/settings/numbering-rules/seed-defaults', {})
    return asArray<NumberingRuleRecord>(response)
  },
  previewNumberingRule(ruleId?: string, documentType?: string) {
    return api.post<{ ruleId: string; documentType: string; preview: string }>('/api/settings/numbering-rules/preview', { ruleId, documentType })
  },
  getBranding(signal?: AbortSignal) {
    return api.get<TenantBrandingSettings>('/api/settings/branding', { signal })
  },
  updateBranding(input: UpdateTenantBrandingInput) {
    return api.put<TenantBrandingSettings>('/api/settings/branding', input)
  },
  getDocuments(signal?: AbortSignal) {
    return api.get<DocumentsSettingsBundle>('/api/settings/documents', { signal })
  },
  updateDocuments(input: UpdateDocumentsSettingsInput) {
    return api.put<DocumentsSettingsBundle>('/api/settings/documents', input)
  },
  uploadLogo(file: File, signal?: AbortSignal) {
    const form = new FormData()
    form.append('file', file)
    return api.post<{ logoUrl: string }>('/api/tenant/branding/logo', form, { signal })
  },
  getReportTemplate(signal?: AbortSignal) {
    return api.get<ReportTemplateConfig>('/api/settings/report-template', { signal })
  },
  saveReportTemplate(config: ReportTemplateConfig) {
    return api.put<ReportTemplateConfig>('/api/settings/report-template', config)
  },
}

export interface TenantBrandingSettings {
  logoUrl: string | null
  primaryColor: string
  secondaryColor: string
  showPoweredByEcosys: boolean
}

export interface UpdateTenantBrandingInput {
  logoUrl?: string | null
  primaryColor?: string | null
  secondaryColor?: string | null
  showPoweredByEcosys: boolean
}

export interface ReportTemplateSection {
  id: string
  label: string
  order: number
}

export interface ReportTemplateConfig {
  layoutStyle: string
  headerStyle: string
  footerStyle: string
  sections: ReportTemplateSection[]
  fontFamily: string
  reportPrimaryColor: string | null
  reportSecondaryColor: string | null
  footerText: string | null
  watermarkText: string | null
  logoAlignment: 'Left' | 'Center' | 'Right'
  showCompanyName: boolean
  showCompanyContact: boolean
  showLogo: boolean
  showTaxBreakdown: boolean
  showBankDetails: boolean
  showSignatureBlock: boolean
  showTerms: boolean
  defaultFooterNote: string | null
  defaultTermsText: string | null
  showStampArea: boolean
  pageDensity: string
  quoteTerms: string | null
  invoiceTerms: string | null
  warrantyNote: string | null
  latePaymentNote: string | null
  serviceReportDisclaimer: string | null
  footerLegalText: string | null
  bankName: string | null
  accountName: string | null
  accountNumber: string | null
  branch: string | null
  swift: string | null
  paybill: string | null
  paymentInstructions: string | null
  defaultCurrency: string
}

export interface DocumentsSettingsBundle {
  profile: {
    companyName: string
    legalName: string
    tradingName: string
    postalAddress: string
    physicalAddress: string
    website: string
    vatNumber: string
    registrationNumber: string
    email: string
    phone: string | null
    country: string
    industry: string | null
    address: string | null
    logoUrl: string | null
    primaryColor: string
    secondaryColor: string
    showPoweredByEcosys: boolean
  }
  branding: TenantBrandingSettings
  template: ReportTemplateConfig
}

export interface UpdateDocumentsSettingsInput {
  profile: {
    companyName: string
    legalName: string
    tradingName: string
    postalAddress: string
    physicalAddress: string
    website: string
    vatNumber: string
    registrationNumber: string
    email: string
    phone: string | null
    country: string
    industry: string | null
    address?: string | null
    logoUrl: string | null
    primaryColor: string
    secondaryColor: string
    showPoweredByEcosys: boolean
  }
  branding: UpdateTenantBrandingInput
  template: ReportTemplateConfig
}
