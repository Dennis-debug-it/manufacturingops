export type LeadStatus = 'New' | 'Contacted' | 'Qualified' | 'Demo Scheduled' | 'Converted to Workspace' | 'Not a Fit'

export interface LeadListItem {
  id: string
  companyName: string
  contactPersonName: string
  email: string
  phone: string
  status: LeadStatus
  createdAt: string
  contactedAt: string | null
  convertedTenantId: string | null
}

export interface LeadDetail extends LeadListItem {
  country: string | null
  industry: string | null
  companySize: string | null
  message: string | null
  preferredContactMethod: string | null
  updatedAt: string | null
  notes: string | null
}

export interface PlatformDashboardSummary {
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
