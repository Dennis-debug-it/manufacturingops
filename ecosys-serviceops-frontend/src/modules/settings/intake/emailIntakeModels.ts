export type EmailIntakeSectionId =
  | 'overview'
  | 'mailbox'
  | 'rules'
  | 'parser'
  | 'matching'
  | 'decisions'
  | 'workorder'
  | 'routing'
  | 'notifications'
  | 'manual-review'
  | 'activity-log'
  | 'simulation'

export type WorkflowStepStatus = 'Complete' | 'Needs Setup' | 'Warning' | 'Disabled'

export type EmailIntakeStep = {
  id: EmailIntakeSectionId
  stepNumber: number
  title: string
  description: string
  status: WorkflowStepStatus
}

export type EmailIntakeNavItem = {
  id: EmailIntakeSectionId
  label: string
}

export type EmailIntakeMetric = {
  id: string
  label: string
  value: string
  tone: 'neutral' | 'success' | 'warning' | 'danger' | 'info'
}

export type IntakeStatusBadge = 'Enabled' | 'Disabled' | 'Needs Setup'

export type IntakeActivityRecord = {
  id: string
  occurredAt: string
  sender: string
  subject: string
  actionType: 'Email received' | 'Work order created' | 'Sent to manual review' | 'Ignored' | 'Failed' | 'Parsed' | 'Matched' | 'Work order updated' | 'Routed' | 'Notification queued'
  status: 'Success' | 'Warning' | 'Failed' | 'Info'
  workOrderRef?: string
  detail?: string
}

export type ManualReviewRecord = {
  id: string
  receivedDate: string
  sender: string
  subject: string
  suggestedClient: string
  suggestedAsset: string
  confidence: number
  reviewReason: string
}

export const EMAIL_INTAKE_NAV_ITEMS: EmailIntakeNavItem[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'mailbox', label: 'Mailbox Connection' },
  { id: 'rules', label: 'Intake Rules' },
  { id: 'parser', label: 'Parser & Keywords' },
  { id: 'matching', label: 'Matching' },
  { id: 'decisions', label: 'Decisions' },
  { id: 'workorder', label: 'Work Order Creation' },
  { id: 'routing', label: 'Routing' },
  { id: 'notifications', label: 'Notifications' },
  { id: 'manual-review', label: 'Manual Review' },
  { id: 'activity-log', label: 'Activity Log' },
  { id: 'simulation', label: 'Simulation' },
]
