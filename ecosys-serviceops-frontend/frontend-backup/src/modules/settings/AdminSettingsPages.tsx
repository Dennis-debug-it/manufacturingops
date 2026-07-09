import { AlertTriangle, Copy, Eye, Mail, Plus, RefreshCw, RotateCcw, Save, Wrench } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Badge } from '../../components/ui/Badge'
import { DataTable } from '../../components/ui/DataTable'
import { Drawer } from '../../components/ui/Drawer'
import { ErrorState } from '../../components/ui/ErrorState'
import { LoadingState } from '../../components/ui/LoadingState'
import { PageHeader } from '../../components/ui/PageHeader'
import { useToast } from '../../components/ui/ToastProvider'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useUnsavedChangesGuard } from '../../hooks/useUnsavedChangesGuard'
import { assetService } from '../../services/assetService'
import { branchService } from '../../services/branchService'
import { clientService } from '../../services/clientService'
import { settingsService } from '../../services/settingsService'
import type {
  AssignmentGroupRecord,
  BranchRecord,
  ClientRecord,
  EmailNotificationSettings,
  MonitoringWebhookIntegrationRecord,
  NumberingRuleRecord,
  UpdateEmailIntakeSettingsInput,
  UpdateEmailNotificationSettingsInput,
  UpsertMonitoringWebhookIntegrationInput,
} from '../../types/api'

const notificationDefaults: UpdateEmailNotificationSettingsInput = {
  isEnabled: true,
  deliveryMode: 'Smtp',
  fromName: '',
  fromEmail: '',
  replyToEmail: '',
  smtpHost: '',
  smtpPort: 587,
  enableSslTls: true,
  secureMode: 'Auto',
  smtpUsername: '',
  smtpPasswordSecret: '',
  apiEndpoint: '',
  apiKeySecret: '',
  apiProviderName: '',
  timeoutSeconds: 30,
  maxRetries: 0,
}
const intakeDefaults: UpdateEmailIntakeSettingsInput = { isEnabled: false, intakeEmailAddress: '', mailboxProvider: 'IMAP', host: '', port: 993, useSsl: true, username: '', password: '', defaultClientId: '', defaultBranchId: '', defaultAssignmentGroupId: '', defaultPriority: 'Medium', createWorkOrderFromUnknownSender: false, subjectParsingRules: '', allowedSenderDomains: '' }
const webhookDefaults: UpsertMonitoringWebhookIntegrationInput = { name: '', toolType: 'Generic Webhook', isActive: true, defaultClientId: '', defaultAssetId: '', defaultBranchId: '', defaultAssignmentGroupId: '', defaultPriority: 'Medium', createWorkOrderOnAlert: true, payloadMappingJson: '' }

export function EmailNotificationsSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData<EmailNotificationSettings>((signal) => settingsService.getEmailNotifications(signal), { ...notificationDefaults, id: '', tenantId: '', hasSecret: false, isConfigured: false, lastTestedAt: null, lastError: null }, [])
  const [form, setForm] = useState<UpdateEmailNotificationSettingsInput | null>(null)
  const [testRecipientEmail, setTestRecipientEmail] = useState('')
  const current = form ?? { ...notificationDefaults, ...data, smtpPasswordSecret: '', apiKeySecret: '' }
  // Guard against navigating away with unsaved changes
  useUnsavedChangesGuard(form !== null)
  return <SettingsCard title="Email Notifications" description="Configure outbound email for invites, password resets, and workspace notifications." loading={loading} error={error} onRefresh={reload} actions={<button type="button" className="button-secondary" onClick={() => void testEmail()}><Mail className="h-4 w-4" />Send test email</button>}>
    <InfoBanner title="Outbound only" description="Use this page for SMTP delivery. Email intake for work order creation lives under Email Intake with IMAP mailbox settings." />
    <div className="space-y-4">
      <SettingsSection title="Delivery profile" description="Enable outbound mail and choose how messages are routed." compact>
        <div className="grid gap-4 md:grid-cols-2">{renderEmailNotificationDeliveryFields(current, setForm)}</div>
      </SettingsSection>
      <SettingsSection title="Sender identity" description="Set the visible sender details that recipients will see." compact>
        <div className="grid gap-4 md:grid-cols-2">{renderEmailNotificationSenderFields(current, setForm)}</div>
      </SettingsSection>
      <SettingsSection title="SMTP transport" description="Connection details for the mail server used when SMTP is active." compact>
        <div className="grid gap-4 md:grid-cols-2">{renderEmailNotificationTransportFields(current, setForm, data.hasSecret)}</div>
      </SettingsSection>
      <SettingsSection title="Delivery controls" description="Tune retries, timeout, and test delivery without stretching the form vertically." compact>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Timeout (seconds)"><input type="number" min={5} value={current.timeoutSeconds} onChange={(event) => setForm((currentForm) => ({ ...(currentForm ?? current), timeoutSeconds: Number(event.target.value) || 30 }))} className="field-input" /></Field>
          <Field label="Max retries"><input type="number" min={0} value={current.maxRetries} onChange={(event) => setForm((currentForm) => ({ ...(currentForm ?? current), maxRetries: Number(event.target.value) || 0 }))} className="field-input" /></Field>
          <Field label="Test recipient email"><input value={testRecipientEmail} onChange={(event) => setTestRecipientEmail(event.target.value)} className="field-input" placeholder="Optional. Defaults to the From email." /></Field>
        </div>
      </SettingsSection>
    </div>
    <StatusRow label="Configured" value={data.isConfigured ? 'Ready' : 'Needs attention'} tone={data.isConfigured ? 'success' : 'warning'} detail={data.lastError || (data.hasSecret ? 'A secret is already stored securely.' : 'No API key/password has been saved yet.')} />
    {form !== null ? <p className="mt-2 text-xs text-amber-400">You have unsaved changes.</p> : null}
    <div className="mt-4 flex justify-end"><button type="button" className="button-primary" onClick={() => void save()}><Save className="h-4 w-4" />Save settings</button></div>
  </SettingsCard>

  async function save() {
    try { await settingsService.updateEmailNotifications(current); pushToast({ title: 'Email notifications saved', description: 'Outbound email settings were updated.', tone: 'success' }); setForm(null); await reload() } catch (nextError) { pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save email settings.', tone: 'danger' }) }
  }
  async function testEmail() {
    try { await settingsService.testEmailNotifications(testRecipientEmail); pushToast({ title: 'Test email sent', description: 'The notification test request completed.', tone: 'success' }); await reload() } catch (nextError) { pushToast({ title: 'Test failed', description: nextError instanceof Error ? nextError.message : 'Unable to send test email.', tone: 'danger' }) }
  }
}

export function EmailIntakeSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({ setting: await settingsService.getEmailIntake(signal), branches: await branchService.list(signal), clients: await clientService.list({ signal }), groups: await settingsService.listAssignmentGroups(signal) }), { setting: { ...intakeDefaults, id: '', tenantId: '', hasPassword: false, lastCheckedAt: null, isConnectionHealthy: false, lastError: null }, branches: [] as BranchRecord[], clients: [] as ClientRecord[], groups: [] as AssignmentGroupRecord[] }, [])
  const [form, setForm] = useState<UpdateEmailIntakeSettingsInput | null>(null)
  const current = form ?? { ...intakeDefaults, ...data.setting, password: '' }
  return <SettingsCard title="Email Intake" description="Receive support emails and convert them into work orders with workspace-safe defaults." loading={loading} error={error} onRefresh={reload} actions={<button type="button" className="button-secondary" onClick={() => void testConnection()}><RefreshCw className="h-4 w-4" />Test connection</button>}>
    <InfoBanner title="Mailbox configuration" description="Use IMAP/POP3 mailbox settings to receive support mail and create work orders." />
    <div className="space-y-4">
      <SettingsSection title="Connection" description="Core mailbox credentials and protocol settings." compact>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Enable email intake"><input type="checkbox" checked={current.isEnabled} onChange={(event) => setFormState('isEnabled', event.target.checked)} /></Field>
          <Field label="Provider"><select value={current.mailboxProvider} onChange={(event) => setFormState('mailboxProvider', event.target.value)} className="field-input">{['IMAP', 'POP3', 'Other'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
          <Field label="Intake email address"><input value={current.intakeEmailAddress || ''} onChange={(event) => setFormState('intakeEmailAddress', event.target.value)} className="field-input" /></Field>
          <Field label="Host"><input value={current.host} onChange={(event) => setFormState('host', event.target.value)} className="field-input" /></Field>
          <Field label="Port"><input type="number" value={current.port} onChange={(event) => setFormState('port', Number(event.target.value) || 0)} className="field-input" /></Field>
          <Field label="Use SSL"><input type="checkbox" checked={current.useSsl} onChange={(event) => setFormState('useSsl', event.target.checked)} /></Field>
          <Field label="Username"><input value={current.username || ''} onChange={(event) => setFormState('username', event.target.value)} className="field-input" /></Field>
          <Field label={`Password ${data.setting.hasPassword ? '(leave blank to keep current)' : ''}`}><input type="password" value={current.password || ''} onChange={(event) => setFormState('password', event.target.value)} className="field-input" /></Field>
        </div>
      </SettingsSection>
      <SettingsSection title="Routing defaults" description="Where incoming messages should land by default." compact>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Default client">{renderSelect(current.defaultClientId, data.clients.map((client) => ({ value: client.id, label: client.clientName })), (value) => setFormState('defaultClientId', value))}</Field>
          <Field label="Default branch">{renderSelect(current.defaultBranchId, data.branches.map((branch) => ({ value: branch.id, label: branch.name })), (value) => setFormState('defaultBranchId', value))}</Field>
          <Field label="Default assignment group">{renderSelect(current.defaultAssignmentGroupId, data.groups.map((group) => ({ value: group.id, label: group.name })), (value) => setFormState('defaultAssignmentGroupId', value))}</Field>
          <Field label="Default priority"><select value={current.defaultPriority} onChange={(event) => setFormState('defaultPriority', event.target.value)} className="field-input">{['Low', 'Medium', 'High', 'Critical'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
        </div>
      </SettingsSection>
      <SettingsSection title="Parsing and safety" description="Keep intake rules predictable and reduce accidental ticket creation." compact>
        <div className="grid gap-4 md:grid-cols-2">
          <Field label="Allow unknown senders"><input type="checkbox" checked={current.createWorkOrderFromUnknownSender} onChange={(event) => setFormState('createWorkOrderFromUnknownSender', event.target.checked)} /></Field>
          <Field label="Allowed sender domains"><input value={current.allowedSenderDomains || ''} onChange={(event) => setFormState('allowedSenderDomains', event.target.value)} className="field-input" placeholder="example.com, partner.org" /></Field>
          <Field label="Subject parsing rules"><textarea value={current.subjectParsingRules || ''} onChange={(event) => setFormState('subjectParsingRules', event.target.value)} className="field-input min-h-[110px]" /></Field>
        </div>
      </SettingsSection>
    </div>
    <StatusRow label="Connection health" value={data.setting.isConnectionHealthy ? 'Healthy' : 'Not verified'} tone={data.setting.isConnectionHealthy ? 'success' : 'warning'} detail={data.setting.lastError || 'Run a test connection after saving mailbox settings.'} />
    <div className="mt-4 flex justify-end"><button type="button" className="button-primary" onClick={() => void save()}><Save className="h-4 w-4" />Save settings</button></div>
  </SettingsCard>

  function setFormState<Key extends keyof UpdateEmailIntakeSettingsInput>(key: Key, value: UpdateEmailIntakeSettingsInput[Key]) { setForm((currentForm) => ({ ...(currentForm ?? current), [key]: value })) }
  async function save() { try { await settingsService.updateEmailIntake(current); pushToast({ title: 'Email intake saved', description: 'Inbound email settings were updated.', tone: 'success' }); setForm(null); await reload() } catch (nextError) { pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save intake settings.', tone: 'danger' }) } }
  async function testConnection() { try { await settingsService.testEmailIntakeConnection(current.host, current.port); pushToast({ title: 'Connection test completed', description: 'Mailbox connectivity was checked.', tone: 'success' }); await reload() } catch (nextError) { pushToast({ title: 'Test failed', description: nextError instanceof Error ? nextError.message : 'Unable to test mailbox connectivity.', tone: 'danger' }) } }
}

export function MonitoringToolIntakeSettingsPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(async (signal) => ({ items: await settingsService.listMonitoringWebhooks(signal), branches: await branchService.list(signal), clients: await clientService.list({ signal }), assets: await assetService.list(undefined, signal), groups: await settingsService.listAssignmentGroups(signal) }), { items: [] as MonitoringWebhookIntegrationRecord[], branches: [] as BranchRecord[], clients: [] as ClientRecord[], assets: [] as Awaited<ReturnType<typeof assetService.list>>, groups: [] as AssignmentGroupRecord[] }, [])
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [editing, setEditing] = useState<MonitoringWebhookIntegrationRecord | null>(null)
  const [form, setForm] = useState<UpsertMonitoringWebhookIntegrationInput>(webhookDefaults)
  const [latestSecret, setLatestSecret] = useState<{ endpointUrl?: string | null; secret?: string | null } | null>(null)
  return <SettingsCard title="Monitoring Tool Intake" description="Create webhook endpoints for SolarWinds, Datadog, Grafana, and generic alert sources." loading={loading} error={error} onRefresh={reload} actions={<button type="button" className="button-primary" onClick={() => openEditor()}><Plus className="h-4 w-4" />Add Tool</button>}>
    <DataTable rows={data.items} rowKey={(row) => row.id} emptyTitle="No monitoring tools yet" emptyDescription="Add the first webhook tool to receive alerts automatically." columns={[{ key: 'name', header: 'Name', cell: (row) => <div><p className="font-semibold text-app">{row.name}</p><p className="mt-1 text-xs text-muted">{row.endpointUrl || row.endpointSlug}</p></div> }, { key: 'type', header: 'Tool Type', cell: (row) => row.toolType }, { key: 'status', header: 'Status', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.lastStatus || (row.isActive ? 'Active' : 'Inactive')}</Badge> }, { key: 'last', header: 'Last Received', cell: (row) => row.lastReceivedAt ? new Date(row.lastReceivedAt).toLocaleString() : 'Never' }, { key: 'actions', header: 'Actions', cell: (row) => <div className="flex gap-2"><button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row)}>Edit</button><button type="button" className="button-secondary px-3 py-2" onClick={() => void testWebhook(row.id)}>Test</button><button type="button" className="button-secondary px-3 py-2" onClick={() => void rotateSecret(row.id)}>Rotate secret</button><button type="button" className="button-secondary px-3 py-2" onClick={() => void deleteWebhook(row.id)}>Delete</button></div> }]} />
    {latestSecret ? <SecretPanel endpointUrl={latestSecret.endpointUrl} secret={latestSecret.secret} /> : null}
    <WebhookDrawer open={drawerOpen} title={editing ? 'Edit monitoring tool' : 'Add monitoring tool'} onClose={() => setDrawerOpen(false)}>
      <div className="grid gap-4 md:grid-cols-2">
        <Field label="Name"><input value={form.name} onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))} className="field-input" /></Field>
        <Field label="Tool type"><select value={form.toolType} onChange={(event) => setForm((current) => ({ ...current, toolType: event.target.value }))} className="field-input">{['SolarWinds', 'Datadog', 'PRTG', 'Zabbix', 'Grafana', 'Generic Webhook', 'Other'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
        <Field label="Default client">{renderSelect(form.defaultClientId, data.clients.map((client) => ({ value: client.id, label: client.clientName })), (value) => setForm((current) => ({ ...current, defaultClientId: value })))}</Field>
        <Field label="Default asset">{renderSelect(form.defaultAssetId, data.assets.map((asset) => ({ value: asset.id, label: asset.assetName })), (value) => setForm((current) => ({ ...current, defaultAssetId: value })))}</Field>
        <Field label="Default branch">{renderSelect(form.defaultBranchId, data.branches.map((branch) => ({ value: branch.id, label: branch.name })), (value) => setForm((current) => ({ ...current, defaultBranchId: value })))}</Field>
        <Field label="Default assignment group">{renderSelect(form.defaultAssignmentGroupId, data.groups.map((group) => ({ value: group.id, label: group.name })), (value) => setForm((current) => ({ ...current, defaultAssignmentGroupId: value })))}</Field>
        <Field label="Priority"><select value={form.defaultPriority} onChange={(event) => setForm((current) => ({ ...current, defaultPriority: event.target.value }))} className="field-input">{['Low', 'Medium', 'High', 'Critical'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
        <Field label="Active"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.checked }))} /></Field>
      </div>
      <Field label="Payload mapping JSON"><textarea value={form.payloadMappingJson || ''} onChange={(event) => setForm((current) => ({ ...current, payloadMappingJson: event.target.value }))} className="field-input min-h-[120px]" /></Field>
      <div className="mt-4 flex justify-end gap-3"><button type="button" className="button-secondary" onClick={() => setDrawerOpen(false)}>Cancel</button><button type="button" className="button-primary" onClick={() => void saveWebhook()}><Wrench className="h-4 w-4" />Save tool</button></div>
    </WebhookDrawer>
  </SettingsCard>

  function openEditor(item?: MonitoringWebhookIntegrationRecord) { setEditing(item ?? null); setLatestSecret(null); setForm(item ? { name: item.name, toolType: item.toolType, isActive: item.isActive, defaultClientId: item.defaultClientId || '', defaultAssetId: item.defaultAssetId || '', defaultBranchId: item.defaultBranchId || '', defaultAssignmentGroupId: item.defaultAssignmentGroupId || '', defaultPriority: item.defaultPriority, createWorkOrderOnAlert: item.createWorkOrderOnAlert, payloadMappingJson: item.payloadMappingJson || '' } : webhookDefaults); setDrawerOpen(true) }
  async function saveWebhook() { try { const saved = editing ? await settingsService.updateMonitoringWebhook(editing.id, form) : await settingsService.createMonitoringWebhook(form); setLatestSecret({ endpointUrl: saved.endpointUrl, secret: saved.generatedSecret }); pushToast({ title: 'Monitoring tool saved', description: 'Webhook integration details were updated.', tone: 'success' }); setDrawerOpen(false); await reload() } catch (nextError) { pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the webhook tool.', tone: 'danger' }) } }
  async function rotateSecret(id: string) { try { const rotated = await settingsService.rotateMonitoringWebhookSecret(id); setLatestSecret({ endpointUrl: rotated.endpointUrl, secret: rotated.generatedSecret }); pushToast({ title: 'Secret rotated', description: 'A new webhook secret was generated.', tone: 'success' }); await reload() } catch (nextError) { pushToast({ title: 'Rotation failed', description: nextError instanceof Error ? nextError.message : 'Unable to rotate webhook secret.', tone: 'danger' }) } }
  async function testWebhook(id: string) { try { await settingsService.testMonitoringWebhook(id); pushToast({ title: 'Webhook tested', description: 'The monitoring integration was pinged successfully.', tone: 'success' }); await reload() } catch (nextError) { pushToast({ title: 'Test failed', description: nextError instanceof Error ? nextError.message : 'Unable to test the webhook integration.', tone: 'danger' }) } }
  async function deleteWebhook(id: string) { try { await settingsService.deleteMonitoringWebhook(id); pushToast({ title: 'Monitoring tool deleted', description: 'The webhook integration was removed.', tone: 'success' }); await reload() } catch (nextError) { pushToast({ title: 'Delete failed', description: nextError instanceof Error ? nextError.message : 'Unable to delete the webhook integration.', tone: 'danger' }) } }
}

interface NumberingRuleForm {
  prefix: string
  suffix: string
  nextNumber: number
  paddingLength: number
  resetPeriod: string
  includeYear: boolean
  includeMonth: boolean
  yearFormat: string
  separator: string
  isActive: boolean
}

function numberingRuleToForm(rule: NumberingRuleRecord): NumberingRuleForm {
  return {
    prefix: rule.prefix ?? '',
    suffix: rule.suffix ?? '',
    nextNumber: rule.nextNumber ?? 1,
    paddingLength: rule.paddingLength ?? 6,
    resetPeriod: rule.resetPeriod ?? 'Never',
    includeYear: rule.includeYear ?? false,
    includeMonth: rule.includeMonth ?? false,
    yearFormat: rule.yearFormat ?? 'YYYY',
    separator: rule.separator ?? '-',
    isActive: rule.isActive ?? true,
  }
}

function defaultNumberingRuleForm(): NumberingRuleForm {
  return {
    prefix: '',
    suffix: '',
    nextNumber: 1,
    paddingLength: 6,
    resetPeriod: 'Never',
    includeYear: false,
    includeMonth: false,
    yearFormat: 'YYYY',
    separator: '-',
    isActive: true,
  }
}

function computeNumberingPreview(form: NumberingRuleForm, sequence = 1): string {
  const separator = form.separator || '-'
  const now = new Date()
  const parts: string[] = []

  if (form.prefix.trim()) {
    parts.push(form.prefix.trim().toUpperCase())
  }

  if (form.includeYear) {
    parts.push(form.yearFormat === 'YY' ? now.getFullYear().toString().slice(2) : now.getFullYear().toString())
  }

  if (form.includeMonth) {
    parts.push(String(now.getMonth() + 1).padStart(2, '0'))
  }

  parts.push(String(sequence).padStart(Math.max(1, form.paddingLength), '0'))

  if (form.suffix.trim()) {
    parts.push(form.suffix.trim().toUpperCase())
  }

  return parts.join(separator)
}

function NumberingPreviewBadge({ value }: { value: string }) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-cyan-500/20 bg-cyan-500/10 px-5 py-4">
      <Eye className="h-4 w-4 shrink-0 text-cyan-400" />
      <span className="font-mono text-2xl font-bold tracking-wider text-cyan-300">{value}</span>
    </div>
  )
}

function ResetNumberingDrawer({
  open,
  documentType,
  nextNumber,
  onClose,
  onConfirm,
  saving,
}: {
  open: boolean
  documentType: string
  nextNumber: number
  onClose: () => void
  onConfirm: (startFrom: number) => void
  saving: boolean
}) {
  const [startFrom, setStartFrom] = useState(nextNumber || 1)

  useEffect(() => {
    if (open) {
      setStartFrom(nextNumber || 1)
    }
  }, [open, nextNumber])

  return (
    <Drawer
      open={open}
      title="Reset sequence"
      description={`Set the next number to be issued for ${documentType}.`}
      onClose={onClose}
    >
      <div className="space-y-6">
        <div className="panel-subtle flex gap-3 rounded-2xl p-4">
          <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-400" />
          <div>
            <p className="text-sm font-semibold text-app">This cannot be undone</p>
            <p className="mt-1 text-sm text-muted">
              Resetting the sequence changes the next number issued. Numbers already issued are not
              affected, but duplicates may occur if you reset to a value already in use.
            </p>
          </div>
        </div>

        <label className="block space-y-2">
          <span className="text-sm font-semibold text-app">New start value</span>
          <input
            type="number"
            min={1}
            value={startFrom}
            onChange={(event) => setStartFrom(Math.max(1, Number(event.target.value) || 1))}
            className="field-input w-full"
          />
          <p className="text-xs text-muted">
            The very next {documentType.toLowerCase()} created will receive this sequence number.
          </p>
        </label>

        <div className="flex justify-end gap-3 pt-2">
          <button type="button" className="button-secondary" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button type="button" className="button-danger" onClick={() => onConfirm(startFrom)} disabled={saving}>
            <RotateCcw className="h-4 w-4" />
            {saving ? 'Resetting...' : 'Reset sequence'}
          </button>
        </div>
      </div>
    </Drawer>
  )
}

export function NumberingRulesAdminPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload, setData } = useAsyncData<NumberingRuleRecord[]>(
    (signal) => settingsService.listNumberingRules(signal),
    [],
    [],
  )
  const [editing, setEditing] = useState<NumberingRuleRecord | null>(null)
  const [form, setForm] = useState<NumberingRuleForm | null>(null)
  const [resetTarget, setResetTarget] = useState<NumberingRuleRecord | null>(null)
  const [saving, setSaving] = useState(false)
  const [resetting, setResetting] = useState(false)
  const currentRule = editing ?? resetTarget ?? data[0] ?? null
  const currentForm = form ?? (editing ? numberingRuleToForm(editing) : defaultNumberingRuleForm())
  const isDirty = form !== null
  useUnsavedChangesGuard(isDirty)

  return (
    <SettingsCard
      title="Numbering Rules"
      description="Manage prefixes, counters, and previews for all document numbers used across the workspace."
      loading={loading}
      error={error}
      onRefresh={reload}
      actions={
        <button type="button" className="button-secondary" onClick={() => void seedDefaults()}>
          <RefreshCw className="h-4 w-4" />
          Seed defaults
        </button>
      }
    >
      <div className="space-y-4">
        <DataTable
          rows={data}
          rowKey={(row) => row.id}
          emptyTitle="No numbering rules yet"
          emptyDescription="Set up the default numbering rules for your workspace."
          columns={[
            { key: 'doc', header: 'Document Type', cell: (row) => row.documentType },
            { key: 'prefix', header: 'Prefix', cell: (row) => row.prefix },
            { key: 'next', header: 'Next Number', cell: (row) => row.nextNumber },
            { key: 'reset', header: 'Reset Period', cell: (row) => row.resetPeriod },
            { key: 'preview', header: 'Preview', cell: (row) => row.preview },
            {
              key: 'status',
              header: 'Active',
              cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge>,
            },
            {
              key: 'actions',
              header: 'Actions',
              cell: (row) => (
                <div className="flex gap-2">
                  <button type="button" className="button-secondary px-3 py-2" onClick={() => openEditor(row)}>
                    Edit
                  </button>
                  <button
                    type="button"
                    className="button-secondary px-3 py-2"
                    onClick={() => void previewRule(row)}
                  >
                    Preview
                  </button>
                  <button
                    type="button"
                    className="button-secondary px-3 py-2"
                    onClick={() => setResetTarget(row)}
                    disabled={row.isLocked}
                  >
                    Reset
                  </button>
                </div>
              ),
            },
          ]}
        />

        {currentRule ? (
          <div className="panel-subtle rounded-[28px] p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-sm font-semibold text-app">Current rule snapshot</p>
                <p className="mt-1 text-sm text-muted">
                  {currentRule.documentType} uses {currentRule.prefix || 'no prefix'} and next
                  sequence {currentRule.nextNumber}.
                </p>
              </div>
              <Badge tone={currentRule.isLocked ? 'warning' : 'neutral'}>
                {currentRule.isLocked ? 'Locked' : 'Editable'}
              </Badge>
            </div>
            <div className="mt-4">
              <NumberingPreviewBadge value={computeNumberingPreview(numberingRuleToForm(currentRule), currentRule.nextNumber)} />
            </div>
          </div>
        ) : null}

        <div className="flex flex-wrap items-center justify-between gap-3 rounded-[28px] border border-[var(--app-divider)] bg-[var(--app-surface)] px-4 py-4">
          <div>
            {isDirty ? (
              <p className="text-sm font-medium text-amber-400">You have unsaved changes.</p>
            ) : (
              <p className="text-sm text-muted">All changes saved.</p>
            )}
          </div>
          <div className="flex gap-3">
            {isDirty ? (
              <button type="button" className="button-secondary" onClick={() => setForm(null)} disabled={saving}>
                Discard
              </button>
            ) : null}
            <button
              type="button"
              className="button-primary"
              onClick={() => void save()}
              disabled={saving || !isDirty || !editing}
            >
              <Save className="h-4 w-4" />
              {saving ? 'Saving...' : 'Save changes'}
            </button>
          </div>
        </div>
      </div>

      <Drawer
        open={Boolean(editing)}
        title={editing ? `Edit ${editing.documentType}` : 'Edit numbering rule'}
        description="Adjust the numbering format used when new documents are created."
        onClose={() => {
          setEditing(null)
          setForm(null)
        }}
      >
        {editing ? (
          <div className="space-y-6">
            <div className="grid gap-4 md:grid-cols-2">
              <Field label="Prefix">
                <input
                  value={currentForm.prefix}
                  onChange={(event) => patch('prefix', event.target.value.toUpperCase())}
                  className="field-input"
                />
              </Field>
              <Field label="Suffix">
                <input
                  value={currentForm.suffix}
                  onChange={(event) => patch('suffix', event.target.value.toUpperCase())}
                  className="field-input"
                />
              </Field>
              <Field label="Next number">
                <input
                  type="number"
                  min={1}
                  value={currentForm.nextNumber}
                  onChange={(event) => patch('nextNumber', Math.max(1, Number(event.target.value) || 1))}
                  className="field-input"
                />
              </Field>
              <Field label="Padding">
                <input
                  type="number"
                  min={1}
                  value={currentForm.paddingLength}
                  onChange={(event) =>
                    patch('paddingLength', Math.max(1, Number(event.target.value) || 1))
                  }
                  className="field-input"
                />
              </Field>
              <Field label="Reset period">
                <select
                  value={currentForm.resetPeriod}
                  onChange={(event) => patch('resetPeriod', event.target.value)}
                  className="field-input"
                >
                  <option value="Never">Never</option>
                  <option value="Yearly">Yearly</option>
                  <option value="Monthly">Monthly</option>
                </select>
              </Field>
              <Field label="Year format">
                <select
                  value={currentForm.yearFormat}
                  onChange={(event) => patch('yearFormat', event.target.value)}
                  className="field-input"
                  disabled={!currentForm.includeYear}
                >
                  <option value="YYYY">YYYY</option>
                  <option value="YY">YY</option>
                </select>
              </Field>
              <Field label="Separator">
                <select
                  value={currentForm.separator}
                  onChange={(event) => patch('separator', event.target.value)}
                  className="field-input"
                >
                  <option value="-">Hyphen</option>
                  <option value="/">Slash</option>
                  <option value=".">Dot</option>
                  <option value="">None</option>
                </select>
              </Field>
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <label className="flex items-center gap-2 text-sm text-app">
                <input
                  type="checkbox"
                  checked={currentForm.includeYear}
                  onChange={(event) => {
                    const next = event.target.checked
                    patch('includeYear', next)
                    if (!next && currentForm.resetPeriod === 'Yearly') {
                      patch('resetPeriod', currentForm.includeMonth ? 'Monthly' : 'Never')
                    }
                  }}
                />
                Include year
              </label>
              <label className="flex items-center gap-2 text-sm text-app">
                <input
                  type="checkbox"
                  checked={currentForm.includeMonth}
                  onChange={(event) => {
                    const next = event.target.checked
                    patch('includeMonth', next)
                    if (!next && currentForm.resetPeriod === 'Monthly') {
                      patch('resetPeriod', currentForm.includeYear ? 'Yearly' : 'Never')
                    }
                  }}
                />
                Include month
              </label>
              <label className="flex items-center gap-2 text-sm text-app">
                <input
                  type="checkbox"
                  checked={currentForm.isActive}
                  onChange={(event) => patch('isActive', event.target.checked)}
                />
                Active
              </label>
            </div>

            <div className="rounded-[24px] border border-[var(--app-divider)] bg-[var(--app-surface)] p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">
                Live preview
              </p>
              <div className="mt-3">
                <NumberingPreviewBadge value={computeNumberingPreview(currentForm, currentForm.nextNumber)} />
              </div>
              <p className="mt-3 text-xs text-muted">
                Changes apply to new documents only. Existing numbers are not affected.
              </p>
            </div>

            <div className="flex flex-wrap items-center justify-between gap-3 pt-2">
              <button
                type="button"
                className="button-secondary"
                onClick={() => setResetTarget(editing)}
                disabled={editing.isLocked}
              >
                <RotateCcw className="h-4 w-4" />
                Reset sequence
              </button>
              <div className="flex gap-3">
                <button type="button" className="button-secondary" onClick={() => { setEditing(null); setForm(null) }} disabled={saving}>
                  Cancel
                </button>
                <button type="button" className="button-primary" onClick={() => void save()} disabled={saving}>
                  <Save className="h-4 w-4" />
                  {saving ? 'Saving...' : 'Save rule'}
                </button>
              </div>
            </div>
          </div>
        ) : null}
      </Drawer>

      <ResetNumberingDrawer
        open={Boolean(resetTarget)}
        documentType={resetTarget?.documentType ?? 'this document'}
        nextNumber={resetTarget?.nextNumber ?? 1}
        onClose={() => setResetTarget(null)}
        onConfirm={(startFrom) => void resetSequence(startFrom)}
        saving={resetting}
      />
    </SettingsCard>
  )

  function openEditor(rule: NumberingRuleRecord) {
    setEditing(rule)
    setForm(numberingRuleToForm(rule))
  }

  function patch<Key extends keyof NumberingRuleForm>(key: Key, value: NumberingRuleForm[Key]) {
    setForm((current) => ({
      ...(current ?? (editing ? numberingRuleToForm(editing) : defaultNumberingRuleForm())),
      [key]: value,
    }))
  }

  async function seedDefaults() {
    try {
      const next = await settingsService.seedDefaultNumberingRules()
      setData(next)
      pushToast({ title: 'Defaults seeded', description: 'Standard numbering rules were created or refreshed.', tone: 'success' })
    } catch (nextError) {
      pushToast({ title: 'Seed failed', description: nextError instanceof Error ? nextError.message : 'Unable to seed numbering defaults.', tone: 'danger' })
    }
  }

  async function save() {
    if (!editing || !form) return
    try {
      setSaving(true)
      const updated = await settingsService.updateNumberingRule(editing.id, {
        prefix: form.prefix.trim().toUpperCase(),
        suffix: form.suffix.trim().toUpperCase() || null,
        nextNumber: Math.max(1, form.nextNumber),
        paddingLength: Math.max(1, form.paddingLength),
        resetPeriod: form.resetPeriod,
        includeYear: form.includeYear,
        includeMonth: form.includeMonth,
        yearFormat: form.includeYear ? form.yearFormat : 'YYYY',
        separator: form.separator,
        isActive: form.isActive,
      })
      setData((current) => current.map((item) => (item.id === updated.id ? updated : item)))
      setEditing(null)
      setForm(null)
      pushToast({ title: 'Numbering rule saved', description: 'The numbering rule was updated.', tone: 'success' })
    } catch (nextError) {
      pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the numbering rule.', tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function resetSequence(startFrom: number) {
    if (!resetTarget) return
    try {
      setResetting(true)
      const updated = await settingsService.resetNumberingRule(resetTarget.id, startFrom)
      setData((current) => current.map((item) => (item.id === updated.id ? updated : item)))
      setResetTarget(null)
      if (editing?.id === updated.id) {
        setEditing(updated)
        setForm(numberingRuleToForm(updated))
      }
      pushToast({
        title: 'Sequence reset',
        description: `Next ${updated.documentType} number starts at ${startFrom}.`,
        tone: 'success',
      })
    } catch (nextError) {
      pushToast({ title: 'Reset failed', description: nextError instanceof Error ? nextError.message : 'Unable to reset the numbering sequence.', tone: 'danger' })
    } finally {
      setResetting(false)
    }
  }

  async function previewRule(rule: NumberingRuleRecord) {
    try {
      const preview = await settingsService.previewNumberingRule(rule.id)
      pushToast({ title: `${rule.documentType} preview`, description: preview.preview, tone: 'success' })
    } catch (nextError) {
      pushToast({ title: 'Preview failed', description: nextError instanceof Error ? nextError.message : 'Unable to preview the numbering rule.', tone: 'danger' })
    }
  }
}

function renderEmailNotificationDeliveryFields(current: UpdateEmailNotificationSettingsInput, setForm: (value: UpdateEmailNotificationSettingsInput) => void) {
  const update = <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => setForm({ ...current, [key]: value })
  return <>
    <Field label="Enable sending"><input type="checkbox" checked={current.isEnabled} onChange={(event) => update('isEnabled', event.target.checked)} /></Field>
    <Field label="Delivery method"><select value={current.deliveryMode === 'Api' ? 'Smtp' : current.deliveryMode} onChange={(event) => update('deliveryMode', event.target.value as UpdateEmailNotificationSettingsInput['deliveryMode'])} className="field-input">{['Smtp', 'Disabled'].map((item) => <option key={item} value={item}>{item === 'Smtp' ? 'SMTP' : 'Disabled'}</option>)}</select></Field>
  </>
}

function renderEmailNotificationSenderFields(current: UpdateEmailNotificationSettingsInput, setForm: (value: UpdateEmailNotificationSettingsInput) => void) {
  const update = <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => setForm({ ...current, [key]: value })
  return <>
    <Field label="From name"><input value={current.fromName} onChange={(event) => update('fromName', event.target.value)} className="field-input" /></Field>
    <Field label="From email"><input value={current.fromEmail} onChange={(event) => update('fromEmail', event.target.value)} className="field-input" /></Field>
    <Field label="Reply-to email"><input value={current.replyToEmail || ''} onChange={(event) => update('replyToEmail', event.target.value)} className="field-input" /></Field>
  </>
}

function renderEmailNotificationTransportFields(current: UpdateEmailNotificationSettingsInput, setForm: (value: UpdateEmailNotificationSettingsInput) => void, hasSecret: boolean) {
  const update = <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => setForm({ ...current, [key]: value })
  const smtpMode = current.deliveryMode === 'Smtp'
  // 'Api' delivery mode is not yet implemented in the backend (throws NotSupportedException).
  // It is intentionally excluded from the dropdown until the integration is ready.
  return <>
    <Field label="Secure mode"><select value={current.secureMode} onChange={(event) => update('secureMode', event.target.value as UpdateEmailNotificationSettingsInput['secureMode'])} className="field-input"><option value="Auto">Auto</option><option value="None">None</option><option value="StartTls">STARTTLS</option><option value="SslOnConnect">SSL on Connect</option></select><p className="text-xs text-muted">Port 465 uses SSL/TLS. Port 587 uses STARTTLS.</p></Field>
    <Field label="Enable SSL/TLS"><input type="checkbox" checked={current.enableSslTls} onChange={(event) => update('enableSslTls', event.target.checked)} /></Field>
    <Field label="SMTP host"><input disabled={!smtpMode} value={current.smtpHost} onChange={(event) => update('smtpHost', event.target.value)} className="field-input" /></Field>
    <Field label="SMTP port"><input disabled={!smtpMode} type="number" value={current.smtpPort} onChange={(event) => applyRecommendedSecureMode(Number(event.target.value) || 0, update)} className="field-input" /></Field>
    <Field label="SMTP username"><input disabled={!smtpMode} value={current.smtpUsername || ''} onChange={(event) => update('smtpUsername', event.target.value)} className="field-input" /></Field>
    <Field label={`SMTP password / secret ${hasSecret ? '(leave blank to keep current)' : ''}`}><input disabled={!smtpMode} type="password" value={current.smtpPasswordSecret || ''} onChange={(event) => update('smtpPasswordSecret', event.target.value)} className="field-input" /></Field>
  </>
}
function renderSelect(value: string | null | undefined, options: Array<{ value: string; label: string }>, onChange: (value: string) => void) { return <select value={value || ''} onChange={(event) => onChange(event.target.value)} className="field-input"><option value="">None</option>{options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}</select> }
function SecretPanel({ endpointUrl, secret }: { endpointUrl?: string | null; secret?: string | null }) { return <div className="panel-subtle mt-4 rounded-[28px] p-5"><p className="text-sm font-semibold text-app">New webhook secret</p><p className="mt-2 text-sm text-muted">Copy these now. The secret is only shown when created or rotated.</p>{endpointUrl ? <CopyRow label="Endpoint URL" value={endpointUrl} /> : null}{secret ? <CopyRow label="Secret" value={secret} /> : null}</div> }
function CopyRow({ label, value }: { label: string; value: string }) { return <div className="mt-3 flex items-center gap-3 rounded-2xl border border-app px-4 py-3"><div className="min-w-0 flex-1"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted">{label}</p><p className="mt-1 truncate text-sm text-app">{value}</p></div><button type="button" className="button-secondary px-3 py-2" onClick={() => void navigator.clipboard.writeText(value)}><Copy className="h-4 w-4" />Copy</button></div> }
function StatusRow({ label, value, tone, detail }: { label: string; value: string; tone: 'success' | 'warning' | 'neutral'; detail: string }) { return <div className="panel-subtle mt-4 rounded-[28px] p-4"><div className="flex items-center justify-between gap-3"><p className="text-sm font-semibold text-app">{label}</p><Badge tone={tone}>{value}</Badge></div><p className="mt-2 text-sm text-muted">{detail}</p></div> }
function SettingsCard({ title, description, loading, error, onRefresh, actions, children }: { title: string; description: string; loading: boolean; error: string | null; onRefresh: () => void | Promise<void>; actions?: ReactNode; children: ReactNode }) { return <div className="space-y-4"><PageHeader eyebrow="Settings" title={title} description={description} actions={actions} /><section className="surface-card">{loading ? <LoadingState label={`Loading ${title.toLowerCase()}`} /> : null}{!loading && error ? <ErrorState title={`Unable to load ${title.toLowerCase()}`} description={error} /> : null}{!loading && !error ? children : null}{!loading && !error ? <div className="mt-4 flex justify-end"><button type="button" className="button-secondary" onClick={() => void onRefresh()}>Refresh</button></div> : null}</section></div> }
function WebhookDrawer({ open, title, onClose, children }: { open: boolean; title: string; onClose: () => void; children: ReactNode }) { return <Drawer open={open} title={title} description="Manage the active settings for this module." onClose={onClose}>{children}</Drawer> }
function Field({ label, children }: { label: string; children: ReactNode }) { return <label className="block space-y-2"><span className="text-sm font-medium text-app">{label}</span>{children}</label> }
function InfoBanner({ title, description }: { title: string; description: string }) { return <div className="panel-subtle rounded-[28px] p-4"><p className="text-sm font-semibold text-app">{title}</p><p className="mt-2 text-sm text-muted">{description}</p></div> }
function SettingsSection({ title, description, compact = false, children }: { title: string; description?: string; compact?: boolean; children: ReactNode }) { return <section className={`panel-subtle rounded-[28px] ${compact ? 'p-4' : 'p-5'}`}><div className={description ? 'mb-4' : undefined}><p className="text-sm font-semibold text-app">{title}</p>{description ? <p className="mt-1 text-sm text-muted">{description}</p> : null}</div>{children}</section> }
function applyRecommendedSecureMode(updatePort: number, update: <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => void) { update('smtpPort', updatePort); if (updatePort === 465) { update('enableSslTls', true); update('secureMode', 'SslOnConnect') } else if (updatePort === 587) { update('enableSslTls', true); update('secureMode', 'StartTls') } }
export { EmailTemplatesSettingsPage } from './EmailTemplatesSettingsPage'
