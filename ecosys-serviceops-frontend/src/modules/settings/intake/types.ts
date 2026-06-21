import type { IntakeProtocolRecord, IntakeProtocolSourceType, UpsertIntakeProtocolInput } from '../../../types/api'
export type { IntakeProtocolSourceType }

export type CriteriaOperator =
  | 'Contains'
  | 'Does Not Contain'
  | 'Equals'
  | 'Not Equals'
  | 'Starts With'
  | 'Ends With'
  | 'Is Empty'
  | 'Is Not Empty'
  | 'In'
  | 'Not In'

export type GroupLogic = 'AND' | 'OR'

export interface IntakeCriterion {
  id: string
  field: string
  operator: CriteriaOperator
  value: string
  booleanValue: boolean
}

export interface IntakeCriteriaGroup {
  id: string
  joiner: GroupLogic
  logic: GroupLogic
  criteria: IntakeCriterion[]
}

export interface EmailSourceConfig {
  intakeEmailAddress: string
  monitoredMailbox: string
  allowedSenderDomains: string
  knownSenderOnly: boolean
  subjectParsingMode: string
  matchEmailBody: boolean
  attachmentParsing: boolean
  mailboxProvider: string
  host: string
  port: number
  useSsl: boolean
  username: string
  password: string
  pollingMode: string
}

export interface MonitoringSourceConfig {
  toolType: string
  webhookEndpoint: string
  secretStatus: string
  eventSourceMapping: string
  payloadMappingJson: string
}

export interface WorkOrderActionConfig {
  enabled: boolean
  workOrderTitleTemplate: string
  workOrderDescriptionTemplate: string
  workOrderType: string
  priority: string
  clientId: string
  branchId: string
  assetId: string
  assignmentGroupId: string
  assignedUserId: string
  dueDateRule: string
  tags: string
  autoCreateImmediately: boolean
}

export interface NotificationActionConfig {
  enabled: boolean
  notifyViaEmail: boolean
  notifyViaInApp: boolean
  notifyGroup: boolean
  notifyAssignedUser: boolean
  notifyBranchManager: boolean
  customEmailRecipients: string
  messageTemplate: string
}

export interface MetadataActionConfig {
  enabled: boolean
  labels: string
  storeSenderEmail: boolean
  storeSourceChannel: boolean
  storeMatchedRuleName: boolean
  storeSeverity: boolean
  storeRawPayloadReference: boolean
  attachOriginalMessageBody: boolean
  attachOriginalMonitoringJson: boolean
}

export interface IntakeActionsConfig {
  createWorkOrder: WorkOrderActionConfig
  sendNotification: NotificationActionConfig
  attachMetadata: MetadataActionConfig
}

export interface IntakeProtocolForm {
  id?: string
  name: string
  sourceType: IntakeProtocolSourceType
  isActive: boolean
  description: string
  criteriaGroups: IntakeCriteriaGroup[]
  sourceConfig: {
    email: EmailSourceConfig
    monitoring: MonitoringSourceConfig
  }
  actions: IntakeActionsConfig
  createdAt?: string
  updatedAt?: string | null
  lastTriggeredAt?: string | null
  lastTriggerStatus?: string | null
  lastError?: string | null
}

export const criteriaOperators: CriteriaOperator[] = [
  'Contains',
  'Does Not Contain',
  'Equals',
  'Not Equals',
  'Starts With',
  'Ends With',
  'Is Empty',
  'Is Not Empty',
  'In',
  'Not In',
]

export const sourceTypeCards: Array<{
  type: IntakeProtocolSourceType
  label: string
  description: string
  disabled?: boolean
}> = [
  { type: 'Email', label: 'Email Integration', description: 'Use a monitored mailbox, sender rules, and message parsing.' },
  { type: 'Monitoring', label: 'External Monitoring', description: 'Accept alerts from monitoring tools and webhook-driven payloads.' },
]

export const futureSourceCards = [
  { label: 'Manual Intake', description: 'Reserved for dispatcher-assisted capture workflows.' },
  { label: 'API / Webhook', description: 'Connect external systems through API and webhook integrations.' },
  { label: 'SMS / WhatsApp', description: 'Available when messaging channels are enabled.', disabled: true },
]

export const emailFieldOptions = [
  'Subject',
  'Body',
  'Sender',
  'Sender Domain',
  'Recipient',
  'Has Attachment',
  'Attachment Name',
  'Attachment Type',
  'Priority Keyword',
  'Severity Keyword',
]

export const monitoringFieldOptions = [
  'Alert Name',
  'Alert Description',
  'Hostname',
  'Device Name',
  'Severity',
  'Status',
  'Metric Name',
  'Source IP',
  'Payload Contains',
]

export const sourceChannelLabel: Record<IntakeProtocolSourceType, string> = {
  Email: 'Email Integration',
  Monitoring: 'External Monitoring',
}

const fieldValueOptionsMap: Partial<Record<string, string[]>> = {
  Severity: ['Critical', 'High', 'Medium', 'Low', 'Warning', 'Info'],
  Status: ['Critical', 'Warning', 'Down', 'Up', 'Alarm', 'Resolved'],
  'Priority Keyword': ['Critical', 'High', 'Medium', 'Low'],
  'Severity Keyword': ['Critical', 'High', 'Medium', 'Low', 'Warning', 'Info'],
}

function createId() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2)}`
}

export function isBooleanField(field: string) {
  return field === 'Has Attachment'
}

export function operatorNeedsValue(operator: CriteriaOperator) {
  return operator !== 'Is Empty' && operator !== 'Is Not Empty'
}

export function getCriterionValueOptions(field: string) {
  return fieldValueOptionsMap[field] ?? null
}

export function getCriterionPlaceholder(field: string) {
  switch (field) {
    case 'Sender Domain':
      return 'e.g. britam.com'
    case 'Recipient':
      return 'e.g. support@ecosys.app'
    case 'Source IP':
      return 'e.g. 10.10.1.24'
    case 'Payload Contains':
      return 'Enter text found in the payload'
    case 'Attachment Type':
      return 'e.g. pdf or image/png'
    default:
      return 'Enter match value'
  }
}

export function summarizeCriterion(criterion: IntakeCriterion) {
  if (isBooleanField(criterion.field)) {
    return `${criterion.field} ${criterion.operator} ${criterion.booleanValue ? 'Yes' : 'No'}`
  }

  return `${criterion.field} ${criterion.operator}${criterion.value ? ` ${criterion.value}` : ''}`
}

export function createEmptyCriterion(field = 'Subject'): IntakeCriterion {
  return {
    id: createId(),
    field,
    operator: isBooleanField(field) ? 'Equals' : 'Contains',
    value: '',
    booleanValue: false,
  }
}

export function createEmptyGroup(): IntakeCriteriaGroup {
  return {
    id: createId(),
    joiner: 'AND',
    logic: 'AND',
    criteria: [createEmptyCriterion()],
  }
}

export function createDefaultProtocol(): IntakeProtocolForm {
  return {
    name: '',
    sourceType: 'Email',
    isActive: true,
    description: 'Automated Work Order Generation',
    criteriaGroups: [createEmptyGroup()],
    sourceConfig: {
      email: {
        intakeEmailAddress: '',
        monitoredMailbox: '',
        allowedSenderDomains: '',
        knownSenderOnly: false,
        subjectParsingMode: 'Structured Keywords',
        matchEmailBody: true,
        attachmentParsing: true,
        mailboxProvider: 'IMAP',
        host: '',
        port: 993,
        useSsl: true,
        username: '',
        password: '',
        pollingMode: 'Mailbox Listener',
      },
      monitoring: {
        toolType: 'Generic Webhook',
        webhookEndpoint: '',
        secretStatus: 'Generate on save',
        eventSourceMapping: '',
        payloadMappingJson: '',
      },
    },
    actions: {
      createWorkOrder: {
        enabled: true,
        workOrderTitleTemplate: '{{subject}}',
        workOrderDescriptionTemplate: '{{body}}',
        workOrderType: 'Corrective',
        priority: 'Medium',
        clientId: '',
        branchId: '',
        assetId: '',
        assignmentGroupId: '',
        assignedUserId: '',
        dueDateRule: 'None',
        tags: '',
        autoCreateImmediately: true,
      },
      sendNotification: {
        enabled: false,
        notifyViaEmail: true,
        notifyViaInApp: true,
        notifyGroup: true,
        notifyAssignedUser: true,
        notifyBranchManager: false,
        customEmailRecipients: '',
        messageTemplate: 'Work order {{workOrderNumber}} created for {{clientName}} at {{siteName}}.',
      },
      attachMetadata: {
        enabled: true,
        labels: '',
        storeSenderEmail: true,
        storeSourceChannel: true,
        storeMatchedRuleName: true,
        storeSeverity: true,
        storeRawPayloadReference: true,
        attachOriginalMessageBody: true,
        attachOriginalMonitoringJson: true,
      },
    },
  }
}

export function parseProtocolRecord(record: IntakeProtocolRecord): IntakeProtocolForm {
  const fallback = createDefaultProtocol()
  const sourceConfig = safeJsonParse(record.sourceConfigJson, {})
  const actions = safeJsonParse<Partial<IntakeActionsConfig>>(record.actionsJson, {})
  const criteriaGroups = hydrateCriteriaGroups(safeJsonParse(record.criteriaJson, []), record.sourceType)

  const emailSourceConfig =
    record.sourceType === 'Email'
      ? {
          ...fallback.sourceConfig.email,
          ...sourceConfig,
        }
      : fallback.sourceConfig.email

  const monitoringSourceConfig =
    record.sourceType === 'Monitoring'
      ? {
          ...fallback.sourceConfig.monitoring,
          ...sourceConfig,
        }
      : fallback.sourceConfig.monitoring

  return {
    id: record.id,
    name: record.name,
    sourceType: record.sourceType,
    isActive: record.isActive,
    description: record.description ?? fallback.description,
    criteriaGroups,
    sourceConfig: {
      email: emailSourceConfig,
      monitoring: monitoringSourceConfig,
    },
    actions: {
      createWorkOrder: {
        ...fallback.actions.createWorkOrder,
        ...(actions.createWorkOrder ?? {}),
      },
      sendNotification: {
        ...fallback.actions.sendNotification,
        ...(actions.sendNotification ?? {}),
      },
      attachMetadata: {
        ...fallback.actions.attachMetadata,
        ...(actions.attachMetadata ?? {}),
      },
    },
    createdAt: record.createdAt,
    updatedAt: record.updatedAt,
    lastTriggeredAt: record.lastTriggeredAt,
    lastTriggerStatus: record.lastTriggerStatus,
    lastError: record.lastError,
  }
}

export function serializeProtocolForm(form: IntakeProtocolForm): UpsertIntakeProtocolInput {
  return {
    name: form.name.trim(),
    sourceType: form.sourceType,
    isActive: form.isActive,
    description: form.description.trim() || null,
    criteriaJson: JSON.stringify(form.criteriaGroups),
    actionsJson: JSON.stringify(form.actions),
    sourceConfigJson: JSON.stringify(form.sourceType === 'Email' ? form.sourceConfig.email : form.sourceConfig.monitoring),
  }
}

function safeJsonParse<T>(value: string, fallback: T): T {
  try {
    return JSON.parse(value) as T
  } catch {
    return fallback
  }
}

function hydrateCriteriaGroups(rawGroups: unknown, sourceType: IntakeProtocolSourceType): IntakeCriteriaGroup[] {
  const fieldFallback = sourceType === 'Email' ? 'Subject' : 'Alert Name'
  if (!Array.isArray(rawGroups) || rawGroups.length === 0) {
    return [
      {
        ...createEmptyGroup(),
        criteria: [createEmptyCriterion(fieldFallback)],
      },
    ]
  }

  const groups = rawGroups
    .map((group): IntakeCriteriaGroup | null => {
      if (!group || typeof group !== 'object') return null
      const typedGroup = group as Partial<IntakeCriteriaGroup> & { criteria?: Array<Partial<IntakeCriterion>> }
      const criteria = Array.isArray(typedGroup.criteria)
        ? typedGroup.criteria.map((criterion) => ({
            id: criterion.id || createId(),
            field: criterion.field || fieldFallback,
            operator: (criterion.operator as CriteriaOperator) || (isBooleanField(criterion.field || fieldFallback) ? 'Equals' : 'Contains'),
            value: typeof criterion.value === 'string' ? criterion.value : '',
            booleanValue: Boolean(criterion.booleanValue),
          }))
        : []

      return {
        id: typedGroup.id || createId(),
        joiner: typedGroup.joiner === 'OR' ? 'OR' : 'AND',
        logic: typedGroup.logic === 'OR' ? 'OR' : 'AND',
        criteria: criteria.length > 0 ? criteria : [createEmptyCriterion(fieldFallback)],
      }
    })
    .filter((group): group is IntakeCriteriaGroup => Boolean(group))

  return groups.length > 0 ? groups : [createEmptyGroup()]
}
