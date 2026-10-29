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

export const sourceOptions = ['Referral', 'Website', 'Walk-in', 'Cold call', 'Tender', 'Existing client', 'Contract renewal', 'Technician', 'Other'] as const

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
  /** `estimatedValue` in the reporting currency; null when the deal's currency has no rate. */
  convertedValue: number | null
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

/** An open follow-up on an open deal, as listed across the whole pipeline. */
export interface SalesFollowUpItem {
  id: string
  type: string
  summary: string
  dueAt: string
  loggedByName: string | null
  opportunityId: string
  opportunityTitle: string
  opportunityStage: SalesStage
  partyName: string | null
  ownerUserId: string | null
  ownerName: string | null
}

/** A sent quotation still waiting for the client; `sentAt` falls back to the quote date for older quotes. */
export interface SalesQuoteAwaitingAnswer {
  id: string
  quoteNumber: string
  subject: string | null
  clientName: string | null
  totalAmount: number
  currency: string
  sentAt: string
  validUntil: string
  opportunityId: string | null
  opportunityTitle: string | null
  ownerUserId: string | null
  ownerName: string | null
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
  tenders: Array<{ id: string; type: TenderType; reference: string | null; title: string; status: TenderStatus; submissionDeadline: string }>
  convertedValue: number | null
  reportingCurrency: string
  /** Set on a renewal deal: the contract it renews. */
  renewalOfContractId?: string | null
  renewalContractNumber?: string | null
  /** Set on a lead a technician flagged from a job. */
  sourceWorkOrderId?: string | null
  sourceWorkOrderNumber?: string | null
}

/** Every value is in `reportingCurrency`; deals in a currency with no rate are listed in `unconvertedCurrencies` instead. */
export interface SalesPipelineSummary {
  reportingCurrency: string
  openCount: number
  openValue: number
  weightedValue: number
  wonThisMonthCount: number
  wonThisMonthValue: number
  winRate: number | null
  overdueFollowUps: number
  openTenders: number
  tendersDueThisWeek: number
  stages: Array<{ stage: SalesStage; count: number; value: number }>
  unconvertedCurrencies: Array<{ currency: string; count: number; value: number }>
}

/** `key` is 'overdue', 'later', 'none' or the month as yyyy-MM. */
export interface SalesForecastBucket {
  key: string
  month: string | null
  count: number
  value: number
  weightedValue: number
}

export interface SalesOwnerPerformance {
  ownerUserId: string | null
  ownerName: string | null
  openCount: number
  openValue: number
  weightedValue: number
  wonCount: number
  wonValue: number
  lostCount: number
  winRate: number | null
}

export interface SalesSourcePerformance {
  source: string | null
  newDeals: number
  wonCount: number
  wonValue: number
  lostCount: number
  winRate: number | null
}

/** Results over a period, every value in `reportingCurrency`; `from` and `to` are inclusive days. */
export interface SalesReport {
  reportingCurrency: string
  from: string
  to: string
  newDeals: number
  wonCount: number
  wonValue: number
  lostCount: number
  lostValue: number
  winRate: number | null
  averageWonValue: number | null
  averageDaysToWin: number | null
  openCount: number
  openValue: number
  weightedValue: number
  forecast: SalesForecastBucket[]
  owners: SalesOwnerPerformance[]
  sources: SalesSourcePerformance[]
  lostReasons: Array<{ reason: string; count: number; value: number }>
  tenders: { submitted: number; won: number; lost: number; noBid: number; cancelled: number; winRate: number | null }
  /** Deals in the report with no exchange rate: counted, but left out of every value. */
  unconvertedDeals: number
}

export type DealVisibility = 'All' | 'Own'

export interface SalesSettings {
  /** Gets system-created deals when no better owner is found; null picks automatically. */
  defaultOwnerUserId: string | null
  defaultOwnerName: string | null
  dealVisibility: DealVisibility
  createRenewalDeals: boolean
}

export interface SalesOwnerOption {
  id: string
  fullName: string
  role: string
}

export interface SalesCurrencyRate {
  currency: string
  /** Units of the reporting currency one unit of `currency` buys. */
  rateToReporting: number
  effectiveFrom: string
  updatedByName: string
}

export interface SalesCurrencySettings {
  reportingCurrency: string
  rates: SalesCurrencyRate[]
  /** Currencies open deals or RFP/RFQs use that have no rate yet. */
  missingCurrencies: string[]
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

// ── RFPs & RFQs ──────────────────────────────────────────────────────────────

export const TENDER_TYPES = ['RFQ', 'RFP'] as const
export type TenderType = (typeof TENDER_TYPES)[number]

export const tenderTypeLabels: Record<TenderType, string> = {
  RFQ: 'RFQ — request for quotation',
  RFP: 'RFP — request for proposal',
}

export const TENDER_STATUSES = ['Received', 'Reviewing', 'Preparing', 'Submitted', 'Won', 'Lost', 'NoBid', 'Cancelled'] as const
export type TenderStatus = (typeof TENDER_STATUSES)[number]
export const OPEN_TENDER_STATUSES: TenderStatus[] = ['Received', 'Reviewing', 'Preparing', 'Submitted']
/** Outcomes that must say why. */
export const TENDER_REASON_STATUSES: TenderStatus[] = ['Lost', 'NoBid', 'Cancelled']

export const tenderStatusLabels: Record<TenderStatus, string> = {
  Received: 'Received',
  Reviewing: 'Reviewing',
  Preparing: 'Preparing bid',
  Submitted: 'Submitted',
  Won: 'Won',
  Lost: 'Lost',
  NoBid: 'No bid',
  Cancelled: 'Cancelled',
}

export interface SalesTenderListItem {
  id: string
  type: TenderType
  reference: string | null
  title: string
  status: TenderStatus
  clientId: string | null
  clientName: string | null
  issuerName: string | null
  receivedAt: string
  clarificationDeadline: string | null
  submissionDeadline: string
  estimatedValue: number
  submittedValue: number | null
  currency: string
  convertedValue: number | null
  ownerUserId: string | null
  ownerName: string | null
  opportunityId: string | null
  bidBondRequired: boolean
}

export interface SalesTenderDetail {
  id: string
  type: TenderType
  reference: string | null
  title: string
  status: TenderStatus
  clientId: string | null
  clientName: string | null
  issuerName: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  receivedAt: string
  clarificationDeadline: string | null
  submissionDeadline: string
  submissionMethod: string | null
  bidBondRequired: boolean
  bidBondAmount: number | null
  description: string | null
  estimatedValue: number
  currency: string
  submittedValue: number | null
  submittedAt: string | null
  decidedAt: string | null
  outcomeNotes: string | null
  ownerUserId: string | null
  ownerName: string | null
  opportunityId: string | null
  opportunityTitle: string | null
  opportunityStage: SalesStage | null
  convertedValue: number | null
  reportingCurrency: string
  createdAt: string
  updatedAt: string | null
}

export interface UpsertTenderPayload {
  type: TenderType
  reference: string | null
  title: string
  clientId: string | null
  issuerName: string | null
  contactName: string | null
  contactEmail: string | null
  contactPhone: string | null
  receivedAt: string | null
  clarificationDeadline: string | null
  submissionDeadline: string
  submissionMethod: string | null
  bidBondRequired: boolean
  bidBondAmount: number | null
  description: string | null
  estimatedValue: number
  currency: string
  ownerUserId: string | null
}

/** Whole days until a deadline (negative once it has passed). */
export function daysUntilDeadline(deadline: string, now = new Date()) {
  const end = new Date(deadline)
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const startOfDeadline = new Date(end.getFullYear(), end.getMonth(), end.getDate())
  return Math.round((startOfDeadline.getTime() - startOfToday.getTime()) / 86_400_000)
}
