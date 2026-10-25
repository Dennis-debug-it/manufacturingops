export type StageCategory = 'Planning' | 'Active' | 'OnHold' | 'Handover' | 'Completed' | 'Closed' | 'Cancelled'

export const stageCategories: StageCategory[] = ['Planning', 'Active', 'OnHold', 'Handover', 'Completed', 'Closed', 'Cancelled']

export const stageCategoryLabels: Record<StageCategory, string> = {
  Planning: 'Planning',
  Active: 'Active',
  OnHold: 'On hold',
  Handover: 'Handover',
  Completed: 'Completed',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
}

export type MemberRole = 'ProjectManager' | 'Supervisor' | 'Member' | 'Viewer'

export const memberRoleLabels: Record<MemberRole, string> = {
  ProjectManager: 'Project manager',
  Supervisor: 'Supervisor',
  Member: 'Member',
  Viewer: 'Viewer',
}

export type MilestoneStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Verified'
export type TaskStatus = 'Open' | 'InProgress' | 'Done' | 'Cancelled'
export type VariationKind = 'Addition' | 'Omission'
export type VariationStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Void'

export interface ProjectListItem {
  id: string
  projectNumber: string
  name: string
  clientId: string
  clientName: string
  siteId: string | null
  siteName: string | null
  projectTypeId: string
  projectTypeName: string
  stageId: string
  stageName: string
  stageCategory: StageCategory
  projectManagerUserId: string | null
  projectManagerName: string | null
  currency: string
  originalValue: number
  revisedValue: number
  progressPercent: number
  progressOverridden: boolean
  plannedProgressPercent: number
  isBehindSchedule: boolean
  plannedStartDate: string | null
  plannedEndDate: string | null
  daysRemaining: number | null
}

export interface ProjectRegisterSummary {
  activeProjects: number
  openProjects: number
  totalRevisedValue: number
  behindSchedule: number
  pendingVariationsCount: number
  pendingVariationsValue: number
}

export interface ProjectMember {
  id: string
  userId: string | null
  technicianId: string | null
  name: string
  email: string | null
  role: MemberRole
  trade: string | null
  fromDate: string | null
  toDate: string | null
}

export interface ProjectConsultantLink {
  id: string
  consultantId: string
  firmName: string
  discipline: string | null
  roleOnProject: string | null
  contactPerson: string | null
  phone: string | null
  email: string | null
}

export interface ProjectDetail {
  summary: ProjectListItem
  description: string | null
  branchId: string | null
  branchName: string | null
  approvedAdditions: number
  approvedOmissions: number
  pendingVariationsCount: number
  pendingVariationsValue: number
  calculatedProgressPercent: number
  progressOverridePercent: number | null
  progressOverrideReason: string | null
  plannedProgressPercent: number
  actualStartDate: string | null
  actualEndDate: string | null
  handedOverAt: string | null
  closedAt: string | null
  closureNotes: string | null
  visibleInClientPortal: boolean
  openTasks: number
  workOrders: number
  materialIssues: number
  members: ProjectMember[]
  consultants: ProjectConsultantLink[]
  features: {
    milestones: boolean; tasks: boolean; variations: boolean; materials: boolean; documents: boolean; allowManualProgressOverride: boolean
    musterRoll: boolean; siteDiary: boolean; valuations: boolean; financials: boolean
  }
  abilities: {
    canManage: boolean; canSubmitVariations: boolean; canApproveVariations: boolean; canClose: boolean
    canRunSite: boolean; canViewFinancials: boolean; canManageFinancials: boolean
  }
  sourceQuotationId: string | null
  maintenanceContractId: string | null
}

export interface ProjectMilestone {
  id: string
  name: string
  sortOrder: number
  weight: number
  plannedDate: string | null
  forecastDate: string | null
  status: MilestoneStatus
  completedAt: string | null
  verifiedAt: string | null
  isOverdue: boolean
}

export interface ProjectTask {
  id: string
  milestoneId: string | null
  milestoneName: string | null
  title: string
  description: string | null
  assigneeUserId: string | null
  assigneeName: string | null
  dueDate: string | null
  priority: 'Low' | 'Medium' | 'High'
  status: TaskStatus
  completedAt: string | null
}

export interface ProjectVariation {
  id: string
  variationNumber: string
  kind: VariationKind
  amount: number
  title: string
  description: string | null
  clientReference: string | null
  status: VariationStatus
  submittedAt: string | null
  decidedAt: string | null
  decisionNote: string | null
  createdAt: string
}

export interface ProjectWorkOrder {
  id: string
  workOrderNumber: string
  title: string
  status: string
  priority: string
  dueDate: string | null
  assignedTechnicianName: string | null
}

export interface ProjectMaterialIssue {
  id: string
  issueNumber: string
  status: string
  issuedAt: string | null
  totalIssuedCost: number
  lineCount: number
}

export interface ProjectDocument {
  id: string
  fileName: string
  fileSize: number
  mimeType: string
  downloadUrl: string
  sharedWithClient: boolean
  uploadedByName: string | null
  createdAt: string
}

export interface ProjectPeople {
  users: Array<{ id: string; fullName: string; role: string; jobTitle: string | null }>
  technicians: Array<{ id: string; userId: string | null; fullName: string; skillCategory: string | null }>
}

export interface Consultant {
  id: string
  firmName: string
  discipline: string | null
  contactPerson: string | null
  phone: string | null
  email: string | null
  physicalAddress: string | null
  notes: string | null
  isActive: boolean
  projectCount: number
}

export interface ProjectType {
  id: string
  name: string
  description: string | null
  defaultTemplateId: string | null
  isActive: boolean
  sortOrder: number
}

export interface ProjectStage {
  id: string
  name: string
  category: StageCategory
  sortOrder: number
  isActive: boolean
}

export interface ProjectTemplateTaskItem {
  title: string
  offsetDays: number | null
}

export interface ProjectTemplateItem {
  name: string
  weight: number
  offsetDays: number | null
  tasks: ProjectTemplateTaskItem[] | null
}

export interface ProjectTemplate {
  id: string
  name: string
  isActive: boolean
  items: ProjectTemplateItem[]
}

export interface ProjectSettings {
  milestones: boolean
  tasks: boolean
  variations: boolean
  materials: boolean
  documents: boolean
  visibility: 'AllProjects' | 'MembersOnly'
  clientPortal: boolean
  progressRequiresVerification: boolean
  allowManualProgressOverride: boolean
  trades: string[]
  musterRoll: boolean
  siteDiary: boolean
  valuations: boolean
  financials: boolean
  musterLockDays: number
  musterReminderHour: number
  reminderLeadDays: number
  closureUnbilledTolerancePercent: number | null
}

// ── Release 2: site work ──────────────────────────────────────────────────────

export type MusterStatus = 'Present' | 'HalfDay' | 'Absent'
export type WorkerEngagement = 'Casual' | 'Permanent' | 'Subcontractor'

export interface MySite {
  projectId: string
  projectNumber: string
  name: string
  clientName: string
  siteName: string | null
  roles: string[]
  canMarkMuster: boolean
  canWriteDiary: boolean
}

export interface ProjectWorker {
  id: string
  fullName: string
  phone: string | null
  trade: string | null
  engagementType: WorkerEngagement
  technicianId: string | null
  isActive: boolean
}

export interface MusterRow {
  workerId: string
  fullName: string
  trade: string | null
  engagementType: WorkerEngagement
  status: MusterStatus | null
  isSuggestion: boolean
  note: string | null
  isActive: boolean
}

export interface MusterDaySheet {
  workDate: string
  isSubmitted: boolean
  isLocked: boolean
  rows: MusterRow[]
  manDays: number
}

export interface DiaryEntry {
  id: string
  entryDate: string
  authorUserId: string
  authorName: string | null
  workCompleted: string | null
  issuesAndDelays: string | null
  instructionsReceived: string | null
  weather: string | null
  submittedAt: string | null
  headcount: number
  materialIssues: string[]
  photoCount: number
}

// ── Release 2: commercial ─────────────────────────────────────────────────────

export type CostCategory = 'Materials' | 'Labour' | 'Subcontractor' | 'Equipment' | 'Other'
export const costCategories: CostCategory[] = ['Materials', 'Labour', 'Subcontractor', 'Equipment', 'Other']

export interface BillingTerms {
  currency: string
  retentionPercent: number | null
  retentionCapAmount: number | null
  retentionReleaseAtCompletionPercent: number
  defectsLiabilityMonths: number | null
  advancePaymentAmount: number | null
  advanceRecoveryPercent: number | null
  advanceInvoiceId: string | null
  advanceInvoiceNumber: string | null
  advanceInvoiceStatus: string | null
  calculationTermsLocked: boolean
}

export interface WorkerRate {
  id: string
  effectiveFrom: string
  dailyRate: number
  note: string | null
  setByUserId: string
}

export interface WorkerRates {
  workerId: string
  fullName: string
  phone: string | null
  trade: string | null
  engagementType: WorkerEngagement
  isActive: boolean
  currentRate: number | null
  manDays: number
  uncostedDays: number
  labourCost: number
  rates: WorkerRate[]
}

export interface WageSheetRow {
  workerId: string
  fullName: string
  phone: string | null
  trade: string | null
  engagementType: WorkerEngagement
  fullDays: number
  halfDays: number
  manDays: number
  currentRate: number | null
  amount: number
  uncostedDays: number
}

export interface WageSheet {
  projectNumber: string
  projectName: string
  currency: string
  from: string
  to: string
  rows: WageSheetRow[]
  totalManDays: number
  totalAmount: number
  workersWithUncostedDays: number
}

export interface BudgetLine {
  id: string
  category: CostCategory
  description: string | null
  amount: number
}

export interface CostEntry {
  id: string
  category: CostCategory
  description: string
  amount: number
  incurredOn: string
  recordedByUserId: string
}

export interface FinancialsCategory {
  category: CostCategory
  budget: number
  actual: number
  source: string
}

export interface ProjectFinancials {
  currency: string
  originalValue: number
  revisedValue: number
  pendingVariations: number
  budgetTotal: number
  categories: FinancialsCategory[]
  manDays: number
  uncostedDays: number
  actualCost: number
  committedCost: number
  invoiced: number
  cashReceived: number
  taxWithheld: number
  outstanding: number
  overdue: number
  certifiedToDate: number
  retentionHeld: number
  retentionDueNow: number
  advanceOutstanding: number
  unbilledValue: number
  indicativeMarginPercent: number | null
  forecastMarginPercent: number | null
  /** Currencies of linked costs no exchange rate converts into the project's; those costs are left out. */
  unconvertedCurrencies?: string[]
}

export type ValuationStatus = 'Draft' | 'Submitted' | 'Certified' | 'Invoiced' | 'Void'

export interface Valuation {
  id: string
  valuationNumber: string
  periodEnd: string
  workDoneToDate: number
  materialsOnSite: number
  grossValueToDate: number
  retentionToDate: number
  advanceRecoveredToDate: number
  previouslyCertified: number
  amountDue: number
  status: ValuationStatus
  certifiedByConsultantId: string | null
  certifiedByConsultantName: string | null
  certificateReference: string | null
  certifiedOn: string | null
  certifiedAmount: number | null
  invoiceId: string | null
  invoiceNumber: string | null
  invoiceStatus: string | null
  notes: string | null
  createdAt: string
}

export interface ValuationPreview {
  suggestedWorkDoneToDate: number
  workDoneToDate: number
  materialsOnSite: number
  grossValueToDate: number
  retentionToDate: number
  advanceRecoveredToDate: number
  previouslyCertified: number
  amountDue: number
  previousWorkDoneToDate: number
}

export type RetentionStage = 'PracticalCompletion' | 'EndOfDefectsLiability'

export interface RetentionRelease {
  id: string
  stage: RetentionStage
  amount: number
  releasedOn: string
  invoiceId: string | null
  invoiceNumber: string | null
  invoiceStatus: string | null
}

export interface RetentionPosition {
  currency: string
  retentionPercent: number | null
  retentionReleaseAtCompletionPercent: number
  held: number
  completionDue: number
  finalDue: number
  handedOverAt: string | null
  defectsLiabilityEnds: string | null
  releases: RetentionRelease[]
}

export interface WithholdingCertificate {
  id: string
  invoiceId: string
  invoiceNumber: string | null
  clientId: string
  clientName: string | null
  taxType: 'VAT' | 'IncomeTax'
  ratePercent: number | null
  amount: number
  currency: string
  certificateNumber: string
  certificateDate: string
  status: 'Confirmed' | 'Reversed'
  notes: string | null
  createdAt: string
}
