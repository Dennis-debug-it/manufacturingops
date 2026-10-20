export const SALES_STAGES = ['Lead', 'Qualified', 'SiteSurvey', 'Proposal', 'Negotiation', 'Won', 'Lost'] as const
export type SalesStage = (typeof SALES_STAGES)[number]

/** The stages a deal moves through before it is closed; Won and Lost are set from the deal itself. */
export const OPEN_STAGES: SalesStage[] = ['Lead', 'Qualified', 'SiteSurvey', 'Proposal', 'Negotiation']

export const stageLabels: Record<SalesStage, string> = {
  Lead: 'Lead',
  Qualified: 'Qualified',
  SiteSurvey: 'Site Survey',
  Proposal: 'Proposal',
  Negotiation: 'Negotiation',
  Won: 'Won',
  Lost: 'Lost',
}

export const sourceOptions = ['Referral', 'Website', 'Walk-in', 'Cold call', 'Tender', 'Existing client', 'Other'] as const

export const activityTypeOptions = [
  { value: 'Call', label: 'Call' },
  { value: 'Email', label: 'Email' },
  { value: 'Meeting', label: 'Meeting' },
  { value: 'SiteVisit', label: 'Site visit' },
  { value: 'Note', label: 'Note' },
  { value: 'FollowUp', label: 'Follow-up' },
] as const

export interface SalesOpportunityListItem {
  id: string
  title: string
  stage: SalesStage
  clientId: string | null
  clientName: string | null
  companyName: string | null
  contactName: string | null
  estimatedValue: number
  currency: string
  probability: number
  expectedCloseDate: string | null
  ownerUserId: string | null
  ownerName: string | null
  serviceType: string | null
  workOrderCount: number
  nextFollowUpAt: string | null
  lastActivityAt: string | null
  createdAt: string
}

export interface SalesActivity {
  id: string
  opportunityId: string
  type: string
  summary: string
  occurredAt: string
  dueAt: string | null
  completedAt: string | null
  loggedByName: string | null
}

export interface SalesOpportunityDetail {
  id: string
  title: string
  stage: SalesStage
  clientId: string | null
  clientName: string | null
  siteId: string | null
  siteName: string | null
  companyName: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  location: string | null
  source: string | null
  serviceType: string | null
  description: string | null
  estimatedValue: number
  currency: string
  probability: number
  expectedCloseDate: string | null
  ownerUserId: string | null
  ownerName: string | null
  lostReason: string | null
  wonAt: string | null
  lostAt: string | null
  createdAt: string
  updatedAt: string | null
  activities: SalesActivity[]
  workOrders: Array<{ id: string; workOrderNumber: string; title: string; status: string; createdAt: string }>
  quotations: Array<{ id: string; quoteNumber: string; status: string; totalAmount: number; currency: string; createdAt: string }>
}

export interface SalesPipelineSummary {
  openCount: number
  openValue: number
  weightedValue: number
  wonThisMonthCount: number
  wonThisMonthValue: number
  winRate: number | null
  overdueFollowUps: number
  stages: Array<{ stage: SalesStage; count: number; value: number }>
}

export interface UpsertOpportunityPayload {
  title: string
  clientId: string | null
  siteId: string | null
  companyName: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  location: string | null
  source: string | null
  serviceType: string | null
  description: string | null
  estimatedValue: number
  currency: string
  probability: number | null
  expectedCloseDate: string | null
  ownerUserId: string | null
  stage?: SalesStage
}

/** Display name for whoever the deal is with: the linked client, else the prospect. */
export function opportunityParty(item: { clientName: string | null; companyName: string | null; contactName: string | null }) {
  return item.clientName ?? item.companyName ?? item.contactName ?? '—'
}
