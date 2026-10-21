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
  features: { milestones: boolean; tasks: boolean; variations: boolean; materials: boolean; documents: boolean; allowManualProgressOverride: boolean }
  abilities: { canManage: boolean; canSubmitVariations: boolean; canApproveVariations: boolean; canClose: boolean }
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
}
