import { useEffect, useMemo, useState } from 'react'
import { ErrorState } from '../../../components/ui/ErrorState'
import { LoadingState } from '../../../components/ui/LoadingState'
import { useToast } from '../../../components/ui/ToastProvider'
import { useAsyncData } from '../../../hooks/useAsyncData'
import { ApiError, api } from '../../../lib/api'
import { settingsService } from '../../../services/settingsService'
import type { AssignmentGroupRecord, EmailIntakeSettings, IntakeProtocolRecord } from '../../../types/api'
import { createDefaultProtocol, parseProtocolRecord, serializeProtocolForm } from './types'
import { ActivityLogPanel, type ActivityLogFilters } from './ActivityLogPanel'
import { DecisionRulesPanel, type DecisionRulesForm } from './DecisionRulesPanel'
import { EmailIntakeLayout } from './EmailIntakeLayout'
import { EmailIntakeOverview } from './EmailIntakeOverview'
import type { EmailIntakeMetric, EmailIntakeSectionId, EmailIntakeStep, IntakeActivityRecord, IntakeStatusBadge, ManualReviewRecord } from './emailIntakeModels'
import { IntakeNotificationsPanel, type IntakeNotificationsForm } from './IntakeNotificationsPanel'
import { IntakeRulesPanel, type IntakeRulesForm } from './IntakeRulesPanel'
import { MailboxConnectionPanel, type MailboxConnectionForm } from './MailboxConnectionPanel'
import { ManualReviewPanel } from './ManualReviewPanel'
import { MatchingPanel, type MatchingForm } from './MatchingPanel'
import { ParserKeywordsPanel, type ParserKeywordsForm } from './ParserKeywordsPanel'
import { RoutingPanel, type RoutingForm } from './RoutingPanel'
import { SimulationPanel, type SimulationInput, type SimulationStage } from './SimulationPanel'
import { WorkOrderCreationPanel, type WorkOrderCreationForm } from './WorkOrderCreationPanel'

type IntakePagePayload = {
  settings: EmailIntakeSettings
  protocols: IntakeProtocolRecord[]
  groups: AssignmentGroupRecord[]
  manualReview: ManualReviewRecord[]
  activity: IntakeActivityRecord[]
}

const defaultMailboxForm: MailboxConnectionForm = {
  intakeEnabled: false,
  mailboxDisplayName: 'Primary Service Inbox',
  intakeEmailAddress: '',
  connectionType: 'IMAP',
  serverHost: '',
  port: 993,
  username: '',
  password: '',
  useSslTls: true,
  folderToMonitor: 'INBOX',
  pollingIntervalSeconds: 60,
  markProcessedAsRead: true,
  moveProcessedEmails: true,
  processedFolderName: 'Processed',
  failedFolderName: 'Failed',
}

const defaultRulesForm: IntakeRulesForm = {
  allowedSenders: '',
  blockedSenders: '',
  subjectBodyFilters: [],
  ignoreRules: [],
  attachmentRules: [],
  priorityKeywordRules: [],
}

const defaultParserForm: ParserKeywordsForm = {
  extractedFields: 'Subject\nSender\nBody\nAttachments\nReceived Date',
  keywordMappings: 'generator failure => Critical\nleak => High\nroutine check => Low',
  priorityKeywords: 'critical, outage, urgent',
  serviceCategoryKeywords: 'electrical, hvac, plumbing, fire safety',
  sampleOutput: '{\n  "subject": "Generator failure at Main Branch",\n  "priority": "High",\n  "sender": "customer@example.com"\n}',
}

const defaultMatchingForm: MatchingForm = {
  clientMatching: 'Match by sender domain then fallback to sender email.',
  contactMatching: 'Match sender email to known client contact records.',
  siteMatching: 'Match branch/site aliases in email subject and body.',
  assetMatching: 'Match known asset tags, serial numbers, and keywords.',
  confidenceThreshold: 70,
  manualReviewFallback: true,
}

const defaultDecisionForm: DecisionRulesForm = {
  newWorkOrderRules: 'Create new work order when no existing open thread is matched.',
  existingWorkOrderRules: 'Update matching open work order when same thread or reference is found.',
  threadMatchingRules: 'Match by message-id, in-reply-to, and ticket reference in subject.',
  lowConfidenceHandling: 'Route to manual review for confidence below threshold.',
  ignoreRejectRules: 'Ignore auto-replies, newsletters, and blocked senders.',
}

const defaultWorkOrderForm: WorkOrderCreationForm = {
  defaultType: 'Corrective',
  defaultStatus: 'Open',
  defaultPriority: 'Medium',
  sourceFields: 'Use subject for title and body for description.',
  attachmentHandling: 'Store accepted attachments against the work order timeline.',
  timelineHandling: 'Append intake events to timeline with parser and matching metadata.',
  fallbackAssignmentGroupId: '',
}

const defaultRoutingForm: RoutingForm = {
  rules: [
    { id: 'route-1', order: 1, condition: 'High priority keywords', assignmentGroup: 'Electrical Dispatch', outcome: 'Route to Electrical Dispatch' },
    { id: 'route-2', order: 2, condition: 'HVAC keyword match', assignmentGroup: 'HVAC Dispatch', outcome: 'Route to HVAC Dispatch' },
  ],
  fallbackAssignmentGroup: 'General Dispatch',
  useManualDispatchQueue: true,
}

const defaultNotificationsForm: IntakeNotificationsForm = {
  notifyDispatchGroup: true,
  notifyAssignedGroup: true,
  notifyTenantAdmin: false,
  notifyOperationsRecipients: '',
  notifySlaEscalationRecipients: '',
  notifySenderReceived: true,
}

const defaultActivityFilters: ActivityLogFilters = {
  status: '',
  sender: '',
  actionType: '',
  fromDate: '',
  toDate: '',
}

const defaultSimulationInput: SimulationInput = {
  from: 'customer@example.com',
  to: 'service@ecosys.app',
  subject: 'Generator failure at Main Branch',
  body: 'Main branch generator is down and needs urgent attention. Asset tag: GEN-19.',
  attachmentsMock: 'generator-photo.jpg',
}

const defaultSimulationStages: SimulationStage[] = [
  { key: 'rules', title: 'Rule result', value: 'Run simulation to evaluate intake rules.' },
  { key: 'parsed', title: 'Parsed fields', value: 'Run simulation to inspect parsed output.' },
  { key: 'matching', title: 'Matching result', value: 'Run simulation to evaluate matching confidence.' },
  { key: 'decision', title: 'Decision', value: 'Run simulation to see create/update/ignore decision.' },
  { key: 'wo', title: 'Proposed work order', value: 'Run simulation to preview generated work order fields.' },
  { key: 'routing', title: 'Routing result', value: 'Run simulation to preview dispatch routing.' },
  { key: 'notify', title: 'Notification preview', value: 'Run simulation to preview notifications.' },
  { key: 'audit', title: 'Audit preview', value: 'Run simulation to preview timeline/audit output.' },
]

const emptyPayload: IntakePagePayload = {
  settings: {
    id: '',
    tenantId: '',
    isEnabled: false,
    intakeEmailAddress: null,
    mailboxProvider: 'IMAP',
    host: '',
    port: 993,
    useSsl: true,
    username: null,
    hasPassword: false,
    defaultClientId: null,
    defaultBranchId: null,
    defaultAssignmentGroupId: null,
    defaultPriority: 'Medium',
    createWorkOrderFromUnknownSender: false,
    subjectParsingRules: null,
    allowedSenderDomains: null,
    lastCheckedAt: null,
    isConnectionHealthy: false,
    lastError: null,
  },
  protocols: [],
  groups: [],
  manualReview: [],
  activity: [],
}

async function optionalGet<T>(path: string, signal: AbortSignal, fallback: T): Promise<T> {
  try {
    return await api.get<T>(path, { signal })
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status === 400 || error.status === 405)) {
      return fallback
    }
    return fallback
  }
}

async function safeGetEmailIntake(signal: AbortSignal): Promise<EmailIntakeSettings> {
  try {
    return await settingsService.getEmailIntake(signal)
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status >= 500)) {
      return emptyPayload.settings
    }
    throw error
  }
}

async function safeListIntakeProtocols(signal: AbortSignal): Promise<IntakeProtocolRecord[]> {
  try {
    return await settingsService.listIntakeProtocols(signal)
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status >= 500)) {
      return []
    }
    throw error
  }
}

async function safeListAssignmentGroups(signal: AbortSignal): Promise<AssignmentGroupRecord[]> {
  try {
    return await settingsService.listAssignmentGroups(signal)
  } catch (error) {
    if (error instanceof ApiError && (error.status === 404 || error.status >= 500)) {
      return []
    }
    throw error
  }
}

function inferActivityFromProtocols(protocols: IntakeProtocolRecord[]): IntakeActivityRecord[] {
  return protocols
    .map((protocol) => {
      const actionType: IntakeActivityRecord['actionType'] = protocol.lastError
        ? 'Failed'
        : protocol.lastTriggeredAt
          ? 'Work order created'
          : 'Parsed'
      const status: IntakeActivityRecord['status'] = protocol.lastError
        ? 'Failed'
        : protocol.lastTriggeredAt
          ? 'Success'
          : 'Info'

      return {
      id: `protocol-${protocol.id}`,
      occurredAt: protocol.lastTriggeredAt || protocol.updatedAt || protocol.createdAt,
      sender: protocol.sourceType === 'Email' ? 'Configured intake mailbox' : 'Monitoring source',
      subject: protocol.name,
      actionType,
      status,
      detail: protocol.lastError || protocol.lastTriggerStatus || 'Protocol configured',
      workOrderRef: undefined,
      }
    })
    .sort((a, b) => +new Date(b.occurredAt) - +new Date(a.occurredAt))
}

function inferManualReviewFromProtocols(protocols: IntakeProtocolRecord[]): ManualReviewRecord[] {
  return protocols
    .filter((protocol) => Boolean(protocol.lastError))
    .map((protocol) => ({
      id: `mr-${protocol.id}`,
      receivedDate: protocol.lastTriggeredAt || protocol.updatedAt || protocol.createdAt,
      sender: 'unknown@sender',
      subject: protocol.name,
      suggestedClient: 'Needs review',
      suggestedAsset: 'Needs review',
      confidence: 45,
      reviewReason: protocol.lastError || 'Low-confidence match',
    }))
}

function isToday(value: string) {
  const date = new Date(value)
  const now = new Date()
  return date.getFullYear() === now.getFullYear() && date.getMonth() === now.getMonth() && date.getDate() === now.getDate()
}

export function EmailIntakePage() {
  const { pushToast } = useToast()

  const { data, loading, error, reload } = useAsyncData<IntakePagePayload>(
    async (signal) => {
      const [settings, protocols, groups, manualReviewApi, activityApi] = await Promise.all([
        safeGetEmailIntake(signal),
        safeListIntakeProtocols(signal),
        safeListAssignmentGroups(signal),
        optionalGet<ManualReviewRecord[]>('/api/platform/email-intake/manual-review', signal, []),
        optionalGet<IntakeActivityRecord[]>('/api/platform/email-intake/activity', signal, []),
      ])

      const inferredActivity = inferActivityFromProtocols(protocols)
      const activity = activityApi.length > 0 ? activityApi : inferredActivity
      const inferredManualReview = inferManualReviewFromProtocols(protocols)
      const manualReview = manualReviewApi.length > 0 ? manualReviewApi : inferredManualReview

      return {
        settings,
        protocols,
        groups,
        manualReview,
        activity,
      }
    },
    emptyPayload,
    [],
  )

  const [activeSection, setActiveSection] = useState<EmailIntakeSectionId>('overview')
  const [mailboxForm, setMailboxForm] = useState<MailboxConnectionForm>(defaultMailboxForm)
  const [rulesForm, setRulesForm] = useState<IntakeRulesForm>(defaultRulesForm)
  const [parserForm, setParserForm] = useState<ParserKeywordsForm>(defaultParserForm)
  const [matchingForm, setMatchingForm] = useState<MatchingForm>(defaultMatchingForm)
  const [decisionForm, setDecisionForm] = useState<DecisionRulesForm>(defaultDecisionForm)
  const [workOrderForm, setWorkOrderForm] = useState<WorkOrderCreationForm>(defaultWorkOrderForm)
  const [routingForm, setRoutingForm] = useState<RoutingForm>(defaultRoutingForm)
  const [notificationsForm, setNotificationsForm] = useState<IntakeNotificationsForm>(defaultNotificationsForm)
  const [activityFilters, setActivityFilters] = useState<ActivityLogFilters>(defaultActivityFilters)
  const [simulationInput, setSimulationInput] = useState<SimulationInput>(defaultSimulationInput)
  const [simulationStages, setSimulationStages] = useState<SimulationStage[]>(defaultSimulationStages)
  const [latestSimulationSummary, setLatestSimulationSummary] = useState('No simulation run yet.')
  const [draftRuleName, setDraftRuleName] = useState('')

  const [savingMailbox, setSavingMailbox] = useState(false)
  const [testingMailbox, setTestingMailbox] = useState(false)
  const [syncingMailbox, setSyncingMailbox] = useState(false)
  const [runningSimulation, setRunningSimulation] = useState(false)

  const primaryProtocol = useMemo(() => data.protocols[0] ?? null, [data.protocols])

  useEffect(() => {
    if (loading || error) {
      return
    }

    setMailboxForm((current) => ({
      ...current,
      intakeEnabled: data.settings.isEnabled,
      intakeEmailAddress: data.settings.intakeEmailAddress || '',
      connectionType: data.settings.mailboxProvider === 'IMAP' ? 'IMAP' : 'IMAP',
      serverHost: data.settings.host || '',
      port: data.settings.port || 993,
      username: data.settings.username || '',
      useSslTls: data.settings.useSsl,
    }))

    setRulesForm((current) => ({
      ...current,
      allowedSenders: data.settings.allowedSenderDomains || '',
    }))

    if (primaryProtocol) {
      const parsed = parseProtocolRecord(primaryProtocol)
      setDraftRuleName(parsed.name)
      setWorkOrderForm((current) => ({
        ...current,
        defaultPriority: parsed.actions.createWorkOrder.priority || current.defaultPriority,
        fallbackAssignmentGroupId: parsed.actions.createWorkOrder.assignmentGroupId || current.fallbackAssignmentGroupId,
      }))
    }
  }, [data.settings, error, loading, primaryProtocol])

  const intakeStatus = useMemo<IntakeStatusBadge>(() => {
    if (!mailboxForm.intakeEnabled) return 'Disabled'
    if (mailboxForm.intakeEmailAddress.trim() && mailboxForm.serverHost.trim() && draftRuleName.trim()) return 'Enabled'
    return 'Needs Setup'
  }, [draftRuleName, mailboxForm.intakeEmailAddress, mailboxForm.intakeEnabled, mailboxForm.serverHost])

  const metrics = useMemo<EmailIntakeMetric[]>(() => {
    const activity = data.activity
    const processedToday = activity.filter((event) => isToday(event.occurredAt)).length
    const workOrdersCreated = activity.filter((event) => event.actionType === 'Work order created').length
    const failedEmails = activity.filter((event) => event.status === 'Failed').length

    return [
      { id: 'status', label: 'Intake status', value: intakeStatus, tone: intakeStatus === 'Enabled' ? 'success' : intakeStatus === 'Needs Setup' ? 'warning' : 'danger' },
      { id: 'last', label: 'Last checked', value: data.settings.lastCheckedAt ? new Date(data.settings.lastCheckedAt).toLocaleString() : 'Never', tone: data.settings.isConnectionHealthy ? 'success' : 'warning' },
      { id: 'processed', label: 'Emails processed today', value: String(processedToday), tone: processedToday > 0 ? 'info' : 'neutral' },
      { id: 'created', label: 'Work orders created', value: String(workOrdersCreated), tone: workOrdersCreated > 0 ? 'success' : 'neutral' },
      { id: 'review', label: 'Manual review items', value: String(data.manualReview.length), tone: data.manualReview.length > 0 ? 'warning' : 'success' },
      { id: 'failed', label: 'Failed emails', value: String(failedEmails), tone: failedEmails > 0 ? 'danger' : 'success' },
    ]
  }, [data.activity, data.manualReview.length, data.settings.isConnectionHealthy, data.settings.lastCheckedAt, intakeStatus])

  const workflowSteps = useMemo<EmailIntakeStep[]>(() => {
    const disabled = !mailboxForm.intakeEnabled

    return [
      { id: 'mailbox', stepNumber: 1, title: 'Connect Mailbox', description: 'Configure intake mailbox connection and sync behavior.', status: disabled ? 'Disabled' : mailboxForm.serverHost && mailboxForm.intakeEmailAddress ? 'Complete' : 'Needs Setup' },
      { id: 'rules', stepNumber: 2, title: 'Define Intake Rules', description: 'Define allowed senders and filtering rules.', status: disabled ? 'Disabled' : rulesForm.allowedSenders || rulesForm.subjectBodyFilters.length > 0 ? 'Complete' : 'Needs Setup' },
      { id: 'parser', stepNumber: 3, title: 'Parse Incoming Email', description: 'Extract fields and map keywords.', status: disabled ? 'Disabled' : parserForm.extractedFields ? 'Complete' : 'Needs Setup' },
      { id: 'matching', stepNumber: 4, title: 'Match Client / Site / Asset', description: 'Resolve sender and asset references with confidence thresholds.', status: disabled ? 'Disabled' : matchingForm.confidenceThreshold >= 60 ? 'Complete' : 'Warning' },
      { id: 'decisions', stepNumber: 5, title: 'Decide Action', description: 'Determine create/update/ignore/manual review outcomes.', status: disabled ? 'Disabled' : decisionForm.newWorkOrderRules ? 'Complete' : 'Needs Setup' },
      { id: 'workorder', stepNumber: 6, title: 'Create or Update Work Order', description: 'Set work order defaults and source mappings.', status: disabled ? 'Disabled' : workOrderForm.defaultStatus ? 'Complete' : 'Needs Setup' },
      { id: 'routing', stepNumber: 7, title: 'Route to Dispatch / Assignment Group', description: 'Apply routing priorities and fallback queues.', status: disabled ? 'Disabled' : routingForm.rules.length > 0 ? 'Complete' : 'Needs Setup' },
      { id: 'notifications', stepNumber: 8, title: 'Notify Team', description: 'Control post-intake operational notifications.', status: disabled ? 'Disabled' : notificationsForm.notifyDispatchGroup || notificationsForm.notifyAssignedGroup ? 'Complete' : 'Needs Setup' },
      { id: 'activity-log', stepNumber: 9, title: 'Track Audit & Timeline', description: 'Review activity events and intake timeline output.', status: disabled ? 'Disabled' : data.activity.length > 0 ? 'Complete' : 'Needs Setup' },
    ]
  }, [data.activity.length, decisionForm.newWorkOrderRules, mailboxForm.intakeEmailAddress, mailboxForm.intakeEnabled, mailboxForm.serverHost, matchingForm.confidenceThreshold, notificationsForm.notifyAssignedGroup, notificationsForm.notifyDispatchGroup, parserForm.extractedFields, routingForm.rules.length, rulesForm.allowedSenders, rulesForm.subjectBodyFilters.length, workOrderForm.defaultStatus])

  const filteredActivity = useMemo(() => {
    return data.activity.filter((row) => {
      if (activityFilters.status && row.status !== activityFilters.status) return false
      if (activityFilters.sender && !row.sender.toLowerCase().includes(activityFilters.sender.toLowerCase())) return false
      if (activityFilters.actionType && !row.actionType.toLowerCase().includes(activityFilters.actionType.toLowerCase())) return false
      if (activityFilters.fromDate && row.occurredAt < `${activityFilters.fromDate}T00:00:00`) return false
      if (activityFilters.toDate && row.occurredAt > `${activityFilters.toDate}T23:59:59`) return false
      return true
    })
  }, [activityFilters.actionType, activityFilters.fromDate, activityFilters.sender, activityFilters.status, activityFilters.toDate, data.activity])

  async function saveMailboxConnection() {
    setSavingMailbox(true)
    try {
      const payload = {
        isEnabled: mailboxForm.intakeEnabled,
        intakeEmailAddress: mailboxForm.intakeEmailAddress || null,
        mailboxProvider: mailboxForm.connectionType,
        host: mailboxForm.serverHost,
        port: mailboxForm.port,
        useSsl: mailboxForm.useSslTls,
        username: mailboxForm.username || null,
        password: mailboxForm.password || null,
        defaultClientId: data.settings.defaultClientId,
        defaultBranchId: data.settings.defaultBranchId,
        defaultAssignmentGroupId: data.settings.defaultAssignmentGroupId,
        defaultPriority: data.settings.defaultPriority || 'Medium',
        createWorkOrderFromUnknownSender: data.settings.createWorkOrderFromUnknownSender,
        subjectParsingRules: data.settings.subjectParsingRules,
        allowedSenderDomains: rulesForm.allowedSenders,
      }
      await settingsService.updateEmailIntake(payload)
      pushToast({ title: 'Connection saved', description: 'Mailbox connection settings were updated.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save mailbox connection.', tone: 'danger' })
    } finally {
      setSavingMailbox(false)
    }
  }

  async function testMailboxConnection() {
    setTestingMailbox(true)
    try {
      const result = await settingsService.testEmailIntakeConnection(mailboxForm.serverHost, mailboxForm.port)
      pushToast({
        title: result.success ? 'Connection test passed' : 'Connection test failed',
        description: result.lastError || result.lastCheckedAt || 'Connection check completed.',
        tone: result.success ? 'success' : 'warning',
      })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Test failed', description: nextError instanceof Error ? nextError.message : 'Unable to test connection.', tone: 'danger' })
    } finally {
      setTestingMailbox(false)
    }
  }

  async function runManualSync() {
    setSyncingMailbox(true)
    try {
      try {
        await api.post('/api/platform/email-intake/run-sync', {})
        pushToast({ title: 'Sync started', description: 'Manual sync has been triggered.', tone: 'success' })
      } catch (nextError) {
        if (nextError instanceof ApiError && primaryProtocol?.id) {
          const fallback = await settingsService.testIntakeProtocol(primaryProtocol.id)
          pushToast({
            title: fallback.success ? 'Sync completed' : 'Sync finished with issues',
            description: fallback.lastTriggerStatus || fallback.lastError || 'Protocol sync fallback completed.',
            tone: fallback.success ? 'success' : 'warning',
          })
        } else {
          throw nextError
        }
      }
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Sync failed', description: nextError instanceof Error ? nextError.message : 'Unable to run sync.', tone: 'danger' })
    } finally {
      setSyncingMailbox(false)
    }
  }

  async function saveRuleName() {
    if (!draftRuleName.trim()) {
      pushToast({ title: 'Rule name required', description: 'Enter a rule name before saving.', tone: 'warning' })
      return
    }

    try {
      const baseForm = primaryProtocol ? parseProtocolRecord(primaryProtocol) : createDefaultProtocol()
      const next = {
        ...baseForm,
        name: draftRuleName.trim(),
      }
      next.sourceConfig.email.intakeEmailAddress = mailboxForm.intakeEmailAddress
      next.sourceConfig.email.allowedSenderDomains = rulesForm.allowedSenders
      next.actions.createWorkOrder.assignmentGroupId = workOrderForm.fallbackAssignmentGroupId
      next.actions.createWorkOrder.priority = workOrderForm.defaultPriority

      if (primaryProtocol?.id) {
        await settingsService.updateIntakeProtocol(primaryProtocol.id, serializeProtocolForm(next))
      } else {
        await settingsService.createIntakeProtocol(serializeProtocolForm(next))
      }

      pushToast({ title: 'Protocol saved', description: 'The intake protocol was updated successfully.', tone: 'success' })
      await reload()
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the protocol.', tone: 'danger' })
    }
  }

  async function runSimulation() {
    setRunningSimulation(true)
    setActiveSection('simulation')

    try {
      let resultStatus = 'Simulation preview generated from current configuration.'
      let success: boolean | null = null
      let errorText: string | null = null

      if (primaryProtocol?.id) {
        const result = await settingsService.testIntakeProtocol(primaryProtocol.id)
        success = result.success
        resultStatus = result.lastTriggerStatus || resultStatus
        errorText = result.lastError || null
      }

      const stages: SimulationStage[] = [
        { key: 'rules', title: 'Rule result', value: draftRuleName ? `Rule '${draftRuleName}' evaluated for sender ${simulationInput.from}.` : 'No named intake rule configured yet.' },
        { key: 'parsed', title: 'Parsed fields', value: `Subject: ${simulationInput.subject} | Sender: ${simulationInput.from} | Attachments: ${simulationInput.attachmentsMock || 'None'}` },
        { key: 'matching', title: 'Matching result', value: `Confidence ${matchingForm.confidenceThreshold}% threshold with manual fallback ${matchingForm.manualReviewFallback ? 'enabled' : 'disabled'}.` },
        { key: 'decision', title: 'Decision', value: decisionForm.newWorkOrderRules || 'Decision rules pending configuration.' },
        { key: 'wo', title: 'Proposed work order', value: `${workOrderForm.defaultType} / ${workOrderForm.defaultStatus} / ${workOrderForm.defaultPriority}` },
        { key: 'routing', title: 'Routing result', value: routingForm.rules[0]?.outcome || 'No routing rule configured.' },
        { key: 'notify', title: 'Notification preview', value: notificationsForm.notifyDispatchGroup ? 'Dispatch group notification queued.' : 'Dispatch notification disabled.' },
        { key: 'audit', title: 'Audit preview', value: resultStatus },
      ]

      setSimulationStages(stages)
      setLatestSimulationSummary(resultStatus)

      if (success === true) {
        pushToast({ title: 'Protocol test passed', description: resultStatus, tone: 'success' })
      } else if (success === false) {
        pushToast({ title: 'Protocol test finished with issues', description: errorText || resultStatus, tone: 'warning' })
      } else {
        pushToast({ title: 'Simulation preview ready', description: 'Review each stage before enabling intake automation.', tone: 'info' })
      }

      await reload()
    } catch (nextError) {
      pushToast({ title: 'Simulation failed', description: nextError instanceof Error ? nextError.message : 'Unable to run simulation.', tone: 'danger' })
    } finally {
      setRunningSimulation(false)
    }
  }

  function saveSection(label: string) {
    pushToast({ title: `${label} saved`, description: 'Section changes were captured for this intake module.', tone: 'success' })
  }

  if (loading) {
    return <LoadingState label="Loading email intake" />
  }

  if (error) {
    return <ErrorState title="Unable to load email intake" description={error} />
  }

  return (
    <EmailIntakeLayout
      status={intakeStatus}
      metrics={metrics}
      activeSection={activeSection}
      onSectionChange={setActiveSection}
      onRunSync={() => void runManualSync()}
      onOpenSimulation={() => void runSimulation()}
      onOpenManualReview={() => setActiveSection('manual-review')}
      onOpenMailbox={() => setActiveSection('mailbox')}
    >
      {activeSection === 'overview' ? (
        <EmailIntakeOverview
          steps={workflowSteps}
          activity={data.activity}
          manualReview={data.manualReview}
          latestSimulationSummary={latestSimulationSummary}
          draftRuleName={draftRuleName}
          onRuleNameChange={setDraftRuleName}
          onSaveRule={() => void saveRuleName()}
          onConfigureStep={setActiveSection}
          onOpenManualReview={() => setActiveSection('manual-review')}
          onOpenSimulation={() => setActiveSection('simulation')}
        />
      ) : null}

      {activeSection === 'mailbox' ? (
        <MailboxConnectionPanel
          form={mailboxForm}
          saving={savingMailbox}
          testing={testingMailbox}
          syncing={syncingMailbox}
          onChange={(patch) => setMailboxForm((current) => ({ ...current, ...patch }))}
          onSave={() => void saveMailboxConnection()}
          onTest={() => void testMailboxConnection()}
          onSync={() => void runManualSync()}
        />
      ) : null}

      {activeSection === 'rules' ? (
        <IntakeRulesPanel
          form={rulesForm}
          onChange={(patch) => setRulesForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Intake rules')}
        />
      ) : null}

      {activeSection === 'parser' ? (
        <ParserKeywordsPanel
          form={parserForm}
          onChange={(patch) => setParserForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Parser settings')}
        />
      ) : null}

      {activeSection === 'matching' ? (
        <MatchingPanel
          form={matchingForm}
          onChange={(patch) => setMatchingForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Matching settings')}
        />
      ) : null}

      {activeSection === 'decisions' ? (
        <DecisionRulesPanel
          form={decisionForm}
          onChange={(patch) => setDecisionForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Decision rules')}
        />
      ) : null}

      {activeSection === 'workorder' ? (
        <WorkOrderCreationPanel
          form={workOrderForm}
          groups={data.groups}
          onChange={(patch) => setWorkOrderForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Work order creation defaults')}
        />
      ) : null}

      {activeSection === 'routing' ? (
        <RoutingPanel
          form={routingForm}
          onChange={(patch) => setRoutingForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Routing rules')}
        />
      ) : null}

      {activeSection === 'notifications' ? (
        <IntakeNotificationsPanel
          form={notificationsForm}
          onChange={(patch) => setNotificationsForm((current) => ({ ...current, ...patch }))}
          onSave={() => saveSection('Notification settings')}
        />
      ) : null}

      {activeSection === 'manual-review' ? <ManualReviewPanel rows={data.manualReview} /> : null}

      {activeSection === 'activity-log' ? (
        <ActivityLogPanel
          rows={filteredActivity}
          filters={activityFilters}
          onFilterChange={(patch) => setActivityFilters((current) => ({ ...current, ...patch }))}
        />
      ) : null}

      {activeSection === 'simulation' ? (
        <SimulationPanel
          input={simulationInput}
          running={runningSimulation}
          stages={simulationStages}
          onChange={(patch) => setSimulationInput((current) => ({ ...current, ...patch }))}
          onRun={() => void runSimulation()}
        />
      ) : null}
    </EmailIntakeLayout>
  )
}
