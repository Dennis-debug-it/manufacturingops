import type { ClientContact } from '../clients/types'

/** The stages of the built-in Sales pipeline; workspaces can add pipelines with their own (Settings › Sales). */
export const SALES_STAGES = ['Lead', 'Qualified', 'SiteSurvey', 'Proposal', 'Negotiation', 'Won', 'Lost'] as const

/** A stage key. Each pipeline has its own open stages; every pipeline ends in Won and Lost. */
export type SalesStage = string

/** The open stages of the built-in Sales pipeline. */
export const OPEN_STAGES: SalesStage[] = ['Lead', 'Qualified', 'SiteSurvey', 'Proposal', 'Negotiation']

export const stageLabels: Record<string, string> = {
  Lead: 'Lead',
  Qualified: 'Qualified',
  SiteSurvey: 'Site Survey',
  Proposal: 'Proposal',
  Negotiation: 'Negotiation',
  Won: 'Won',
  Lost: 'Lost',
}

export function isClosedStage(stage: SalesStage) {
  return stage === 'Won' || stage === 'Lost'
}

export interface SalesPipelineStage {
  key: string
  name: string
  probability: number
  isClosed: boolean
  /** Deals in this stage right now. */
  dealCount: number
}

export interface SalesPipeline {
  id: string
  name: string
  sortOrder: number
  /** Only shown when the workspace's plan includes this module; null for every plan. */
  module: string | null
  isDefault: boolean
  isActive: boolean
  /** False when the workspace's plan does not include `module`. */
  available: boolean
  stages: SalesPipelineStage[]
}

export interface UpsertPipelinePayload {
  name: string
  module: string | null
  isActive: boolean
  isDefault: boolean
  /** Open stages in order; Won and Lost are added after them. Omit `key` for a new stage. */
  stages: Array<{ key?: string | null; name: string; probability: number }>
  sortOrder?: number
}

/** The pipeline a deal belongs to: the one named, else the default. */
export function pipelineFor(pipelines: SalesPipeline[] | undefined, pipelineId: string | null | undefined) {
  const list = pipelines ?? []
  return list.find((p) => p.id === pipelineId) ?? list.find((p) => p.isDefault) ?? list[0]
}

/** A stage's display name, looked up in the deal's pipeline (or any pipeline) before falling back to the key. */
export function stageName(stage: SalesStage, pipelines?: SalesPipeline[], pipelineId?: string | null) {
  const own = pipelineFor(pipelines, pipelineId)?.stages.find((s) => s.key === stage)
  const any = own ?? pipelines?.flatMap((p) => p.stages).find((s) => s.key === stage)
  return any?.name ?? stageLabels[stage] ?? stage
}

/** Open stages of a pipeline in order; the built-in ones while pipelines load. */
export function openStagesOf(pipeline: SalesPipeline | undefined): Array<{ key: string; name: string }> {
  return pipeline
    ? pipeline.stages.filter((s) => !s.isClosed).map((s) => ({ key: s.key, name: s.name }))
    : OPEN_STAGES.map((key) => ({ key, name: stageLabels[key] ?? key }))
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
  /** The deal's client is a prospect, not a customer yet. */
  clientIsProspect?: boolean
  /** Whole days since anything was logged on the deal. */
  daysIdle?: number
  /** Open and idle for the workspace's stall period (Sales settings). */
  isStalled?: boolean
  /** When the deal entered its current stage. */
  stageChangedAt?: string | null
  pipelineId?: string | null
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
  clientIsProspect?: boolean
  /** People at the client involved in this deal. */
  contacts?: ClientContact[]
  pipelineId?: string | null
  pipelineName?: string | null
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
  /** Open deals with nothing logged for `stalledAfterDays` (0 = stall alerts off). */
  stalledDeals?: number
  stalledAfterDays?: number
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
  /** Target for the period (pro rata for part months); null when none is set. */
  targetValue?: number | null
}

export interface SalesStageFunnelRow {
  stage: SalesStage
  entered: number
  movedOn: number
  won: number
  movedOnRate: number | null
  winRateFromHere: number | null
  averageDaysInStage: number | null
  nowInStage: number
  averageDaysNowInStage: number | null
  stageName?: string | null
}

export interface SalesTargetRow {
  /** Null for the team's own target. */
  ownerUserId: string | null
  /** First day of the month. */
  month: string
  amount: number
}

export interface SalesTargets {
  year: number
  reportingCurrency: string
  owners: SalesOwnerOption[]
  targets: SalesTargetRow[]
  canEdit: boolean
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
  funnel?: SalesStageFunnelRow[]
  /** Target for the period; null when none is set. */
  targetValue?: number | null
  pipelineId?: string | null
  pipelineName?: string | null
}

export type DealVisibility = 'All' | 'Own'

export interface SalesSettings {
  /** Gets system-created deals when no better owner is found; null picks automatically. */
  defaultOwnerUserId: string | null
  defaultOwnerName: string | null
  dealVisibility: DealVisibility
  createRenewalDeals: boolean
  /** An open deal with nothing logged for this many days is stalled; 0 turns it off. */
  stalledAfterDays: number
  /** Monday-morning email of each person's past week (and the team's for managers). */
  sendWeeklySummary: boolean
}

export interface SalesAssignmentRule {
  id: string
  name: string
  sortOrder: number
  isActive: boolean
  source: string | null
  serviceType: string | null
  locationContains: string | null
  method: 'Fixed' | 'RoundRobin'
  owners: Array<{ id: string; fullName: string }>
}

export interface UpsertAssignmentRulePayload {
  name: string
  isActive: boolean
  source: string | null
  serviceType: string | null
  locationContains: string | null
  method: 'Fixed' | 'RoundRobin'
  ownerUserIds: string[]
  sortOrder?: number
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
  /** Omit for the default pipeline (new deals) or to keep the current one. */
  pipelineId?: string | null
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
