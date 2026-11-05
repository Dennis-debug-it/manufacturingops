export const WORK_TYPES = ['Corrective', 'Preventive', 'Inspection', 'Statutory', 'Modification', 'Safety'] as const
export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const
export const HOLD_REASONS = ['Awaiting Spares', 'Waiting for Window', 'Waiting for Shutdown', 'Waiting for Contractor', 'Other'] as const

export type JobAction = 'approve' | 'reject' | 'plan' | 'assign' | 'start' | 'hold' | 'resume' | 'complete' | 'verify' | 'return' | 'close' | 'cancel' | 'reopen'

export const ACTION_LABELS: Record<JobAction, string> = {
  approve: 'Approve',
  reject: 'Reject',
  plan: 'Plan',
  assign: 'Assign',
  start: 'Start work',
  hold: 'Put on hold',
  resume: 'Resume',
  complete: 'Complete',
  verify: 'Verify',
  return: 'Send back',
  close: 'Close',
  cancel: 'Cancel job',
  reopen: 'Reopen',
}

/** Actions a supervisor or planner takes; the rest are the technician's. */
export const SUPERVISORY: JobAction[] = ['approve', 'reject', 'plan', 'assign', 'verify', 'return', 'close', 'cancel', 'reopen']

export interface FailureCode {
  id: string
  kind: 'Problem' | 'Cause' | 'Remedy'
  code: string
  name: string
  description: string | null
  assetCategoryId: string | null
  isActive: boolean
}

export interface JobSummary {
  id: string
  workOrderNumber: string
  title: string
  workType: string
  priority: string
  status: string
  isBreakdown: boolean
  needsLineStop: boolean
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  assetCriticality: string | null
  orgNodeId: string | null
  orgNodeName: string | null
  leadTechnicianName: string | null
  dueDate: string | null
  repairDueAt: string | null
  createdAt: string
  completedAt: string | null
  downtimeStartedAt: string | null
  downtimeEndedAt: string | null
  isRework: boolean
  holdReason: string | null
}

export interface JobEvent { type: string; occurredAt: string; message: string; by: string | null; fromStatus: string | null; toStatus: string | null }
export interface JobLink { id: string; workOrderNumber: string; title: string; status: string }

export interface JobDetail {
  id: string
  workOrderNumber: string
  title: string
  description: string | null
  workType: string
  priority: string
  status: string
  isBreakdown: boolean
  needsLineStop: boolean
  requestSource: string | null
  requestedByName: string | null
  assetId: string | null
  assetCode: string | null
  assetName: string | null
  assetCriticality: string | null
  assetCategoryId: string | null
  orgNodeId: string | null
  orgNodeName: string | null
  dueDate: string | null
  scheduledStartAt: string | null
  scheduledEndAt: string | null
  estimatedHours: number | null
  createdAt: string
  approvedAt: string | null
  plannedAt: string | null
  startedAt: string | null
  completedAt: string | null
  verifiedAt: string | null
  closedAt: string | null
  holdReason: string | null
  holdNote: string | null
  downtimeStartedAt: string | null
  downtimeEndedAt: string | null
  downtimeHours: number | null
  workDone: string | null
  verificationNotes: string | null
  problem: FailureCode | null
  cause: FailureCode | null
  remedy: FailureCode | null
  responseDueAt: string | null
  repairDueAt: string | null
  parent: JobLink | null
  reworkOf: JobLink | null
  children: JobLink[]
  team: { technicianId: string; name: string; isLead: boolean }[]
  labour: { id: string; technicianName: string | null; startedAt: string; minutes: number; notes: string | null; overtime: boolean; cost: number | null }[]
  labourCost: number
  requiredCertificationId: string | null
  requiredCertificationName: string | null
  requiredPermitTypes: string[]
  permits: { id: string; permitNumber: string; permitType: string; status: 'Requested' | 'Approved' | 'Closed' | 'Rejected' | 'Cancelled'; validFrom: string; validTo: string }[]
  locks: { id: string; isolationPointId: string; pointCode: string; pointDescription: string; energyType: string; lockNumber: string; appliedByName: string; appliedAt: string; zeroEnergyVerified: boolean; removedByName: string | null; removedAt: string | null }[]
  parts: { id: string; materialItemId: string; partName: string; quantity: number; notes: string | null; usedAt: string; unitCost: number | null }[]
  partsCost: number
  contractorId: string | null
  contractorName: string | null
  agreementId: string | null
  agreementNumber: string | null
  signature: { signerName: string; capturedAt: string; dataUrl: string } | null
  handover: JobEvent[]
  history: JobEvent[]
  allowedActions: JobAction[]
  jobPlanId: string | null
  jobPlanName: string | null
  steps: JobStep[]
  resources: { kind: 'Tool' | 'Part'; description: string; materialItemId: string | null; quantity: number }[]
  responseBreached: boolean
  repairBreached: boolean
}

export interface JobPlan {
  id: string
  name: string
  description: string | null
  workType: string
  assetCategoryId: string | null
  assetCategoryName: string | null
  estimatedHours: number | null
  needsLineStop: boolean
  isActive: boolean
  steps: { instruction: string; estimatedMinutes: number | null }[]
  resources: { kind: string; description: string; materialItemId: string | null; quantity: number }[]
}

export interface ActionPayload {
  reason?: string | null
  holdReason?: string | null
  technicianIds?: string[] | null
  leadTechnicianId?: string | null
  scheduledStartAt?: string | null
  scheduledEndAt?: string | null
  estimatedHours?: number | null
  workDone?: string | null
  problemCodeId?: string | null
  causeCodeId?: string | null
  remedyCodeId?: string | null
  assetBackInService?: boolean | null
  signatureDataUrl?: string | null
  signerName?: string | null
}

export interface CreateJobPayload {
  title: string
  assetId?: string | null
  orgNodeId?: string | null
  description?: string | null
  workType?: string | null
  priority?: string | null
  isBreakdown?: boolean
  needsLineStop?: boolean
  dueDate?: string | null
  estimatedHours?: number | null
  jobPlanId?: string | null
}

export interface FactoryDashboard {
  jobs: { open: number; overdue: number; awaitingApproval: number; awaitingVerification: number; onHold: number; needLineStop: number }
  assets: number
  assetsDown: number
  openBreakdowns: { workOrderId: string; workOrderNumber: string; title: string; assetCode: string; assetName: string; downSince: string; hoursDown: number }[]
  downtimeHoursLast7Days: number
  productionLostLast7Days: number
  productionUnit: string
  topFailingAssets: { assetId: string; assetCode: string; assetName: string; jobs: number; breakdowns: number; rework: number }[]
  topFailureModes: { code: string; name: string; jobs: number }[]
  reactiveJobsLast30Days: number
  plannedJobsLast30Days: number
  runningChecksLast24Hours: { due: number; completed: number; missed: number; outstanding: number; failedItems: number }
}

/** A job plan task or a PM checklist item. Tasks and tick boxes are ticked; the rest need a value. */
export interface JobStep {
  id: string
  instruction: string
  hint: string | null
  done: boolean
  remarks: string | null
  section: string | null
  type: string
  unit: string | null
  minValue: number | null
  maxValue: number | null
  value: string | null
  required: boolean
}

export const isTickStep = (step: Pick<JobStep, 'type'>) => step.type === 'Task' || step.type === 'checkbox'
