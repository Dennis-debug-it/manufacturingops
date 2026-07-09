import { api } from '../lib/api'

export const preferredContactMethodOptions = [
  'Phone',
  'Email',
  'WhatsApp',
] as const

export const platformLeadStatuses = [
  'New',
  'Contacted',
  'Qualified',
  'Demo Scheduled',
  'Converted to Workspace',
  'Not a Fit',
] as const

export type PreferredContactMethod = (typeof preferredContactMethodOptions)[number]
export type PlatformLeadStatus = (typeof platformLeadStatuses)[number]

export type CreatePlatformLeadInput = {
  companyName: string
  contactPersonName: string
  email: string
  phone: string
  country?: string | null
  industry?: string | null
  companySize?: string | null
  message?: string | null
  preferredContactMethod?: PreferredContactMethod | null
}

export type PublicLeadSubmissionResult = {
  success: boolean
  message: string
}

export type PlatformLeadSummary = {
  id: string
  companyName: string
  contactPersonName: string
  email: string
  phone: string
  status: PlatformLeadStatus | string
  createdAt: string
  contactedAt?: string | null
  convertedTenantId?: string | null
}

export type PlatformLeadDetail = PlatformLeadSummary & {
  country?: string | null
  industry?: string | null
  companySize?: string | null
  message?: string | null
  preferredContactMethod?: PreferredContactMethod | string | null
  updatedAt?: string | null
  notes?: string | null
}

export const platformLeadService = {
  submitPublicLead(input: CreatePlatformLeadInput) {
    return api.post<PublicLeadSubmissionResult>('/api/public/leads', input)
  },
  listLeads() {
    return api.get<PlatformLeadSummary[]>('/api/platform/leads')
  },
  getLead(id: string) {
    return api.get<PlatformLeadDetail>(`/api/platform/leads/${id}`)
  },
  updateLeadStatus(id: string, status: PlatformLeadStatus) {
    return api.put<PlatformLeadDetail>(`/api/platform/leads/${id}/status`, { status })
  },
  updateLeadNotes(id: string, notes: string) {
    return api.put<PlatformLeadDetail>(`/api/platform/leads/${id}/notes`, { notes })
  },
}
