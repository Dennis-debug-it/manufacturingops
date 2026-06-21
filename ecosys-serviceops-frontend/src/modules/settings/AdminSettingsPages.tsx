import { Copy, Mail, Plus, RefreshCw, Save, Wrench } from 'lucide-react'
import { useState, type ReactNode } from 'react'
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
    <InfoBanner title="SMTP sends email, IMAP receives email" description="Use this page for outbound SMTP delivery only. Email intake for work order creation is configured separately under Email Intake with IMAP mailbox settings." />
    <InfoBanner title="Notification coverage" description="Configure the email categories your team wants to use for invites, onboarding, work updates, alerts, and escalations." />
    <div className="grid gap-4 md:grid-cols-2">{renderEmailNotificationFields(current, setForm, data.hasSecret)}</div>
    <Field label="Test recipient email"><input value={testRecipientEmail} onChange={(event) => setTestRecipientEmail(event.target.value)} className="field-input" placeholder="Optional. Defaults to the From email." /></Field>
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
    <div className="grid gap-4 md:grid-cols-2">
      <Field label="Enable email intake"><input type="checkbox" checked={current.isEnabled} onChange={(event) => setFormState('isEnabled', event.target.checked)} /></Field>
      <Field label="Provider"><select value={current.mailboxProvider} onChange={(event) => setFormState('mailboxProvider', event.target.value)} className="field-input">{['IMAP', 'POP3', 'Other'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
      <Field label="Intake email address"><input value={current.intakeEmailAddress || ''} onChange={(event) => setFormState('intakeEmailAddress', event.target.value)} className="field-input" /></Field>
      <Field label="Host"><input value={current.host} onChange={(event) => setFormState('host', event.target.value)} className="field-input" /></Field>
      <Field label="Port"><input type="number" value={current.port} onChange={(event) => setFormState('port', Number(event.target.value) || 0)} className="field-input" /></Field>
      <Field label="Use SSL"><input type="checkbox" checked={current.useSsl} onChange={(event) => setFormState('useSsl', event.target.checked)} /></Field>
      <Field label="Username"><input value={current.username || ''} onChange={(event) => setFormState('username', event.target.value)} className="field-input" /></Field>
      <Field label={`Password ${data.setting.hasPassword ? '(leave blank to keep current)' : ''}`}><input type="password" value={current.password || ''} onChange={(event) => setFormState('password', event.target.value)} className="field-input" /></Field>
      <Field label="Default client">{renderSelect(current.defaultClientId, data.clients.map((client) => ({ value: client.id, label: client.clientName })), (value) => setFormState('defaultClientId', value))}</Field>
      <Field label="Default branch">{renderSelect(current.defaultBranchId, data.branches.map((branch) => ({ value: branch.id, label: branch.name })), (value) => setFormState('defaultBranchId', value))}</Field>
      <Field label="Default assignment group">{renderSelect(current.defaultAssignmentGroupId, data.groups.map((group) => ({ value: group.id, label: group.name })), (value) => setFormState('defaultAssignmentGroupId', value))}</Field>
      <Field label="Default priority"><select value={current.defaultPriority} onChange={(event) => setFormState('defaultPriority', event.target.value)} className="field-input">{['Low', 'Medium', 'High', 'Critical'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field>
      <Field label="Allow unknown senders"><input type="checkbox" checked={current.createWorkOrderFromUnknownSender} onChange={(event) => setFormState('createWorkOrderFromUnknownSender', event.target.checked)} /></Field>
      <Field label="Allowed sender domains"><input value={current.allowedSenderDomains || ''} onChange={(event) => setFormState('allowedSenderDomains', event.target.value)} className="field-input" placeholder="example.com, partner.org" /></Field>
      <Field label="Subject parsing rules"><textarea value={current.subjectParsingRules || ''} onChange={(event) => setFormState('subjectParsingRules', event.target.value)} className="field-input min-h-[110px]" /></Field>
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

export function NumberingRulesAdminPage() {
  const { pushToast } = useToast()
  const { data, loading, error, reload, setData } = useAsyncData<NumberingRuleRecord[]>((signal) => settingsService.listNumberingRules(signal), [], [])
  const [editing, setEditing] = useState<NumberingRuleRecord | null>(null)
  const [form, setForm] = useState<NumberingRuleRecord | null>(null)
  return <SettingsCard title="Numbering Rules" description="Manage prefixes, next counters, and previews for operational and finance document numbers." loading={loading} error={error} onRefresh={reload} actions={<button type="button" className="button-secondary" onClick={() => void seedDefaults()}><RefreshCw className="h-4 w-4" />Seed defaults</button>}>
    <DataTable rows={data} rowKey={(row) => row.id} emptyTitle="No numbering rules yet" emptyDescription="Set up the default numbering rules for your workspace." columns={[{ key: 'doc', header: 'Document Type', cell: (row) => row.documentType }, { key: 'prefix', header: 'Prefix', cell: (row) => row.prefix }, { key: 'next', header: 'Next Number', cell: (row) => row.nextNumber }, { key: 'reset', header: 'Reset Period', cell: (row) => row.resetPeriod }, { key: 'preview', header: 'Preview', cell: (row) => row.preview }, { key: 'status', header: 'Active', cell: (row) => <Badge tone={row.isActive ? 'success' : 'neutral'}>{row.isActive ? 'Active' : 'Inactive'}</Badge> }, { key: 'actions', header: 'Actions', cell: (row) => <div className="flex gap-2"><button type="button" className="button-secondary px-3 py-2" onClick={() => { setEditing(row); setForm(row) }}>Edit</button><button type="button" className="button-secondary px-3 py-2" onClick={() => void previewRule(row)}>Preview</button></div> }]} />
    <WebhookDrawer open={Boolean(editing && form)} title={editing ? `Edit ${editing.documentType}` : 'Edit numbering rule'} onClose={() => { setEditing(null); setForm(null) }}>
      {form ? <div className="grid gap-4 md:grid-cols-2"><Field label="Prefix"><input value={form.prefix} onChange={(event) => setForm({ ...form, prefix: event.target.value })} className="field-input" /></Field><Field label="Next number"><input type="number" value={form.nextNumber} onChange={(event) => setForm({ ...form, nextNumber: Number(event.target.value) || 1 })} className="field-input" /></Field><Field label="Padding"><input type="number" value={form.paddingLength} onChange={(event) => setForm({ ...form, paddingLength: Number(event.target.value) || 1 })} className="field-input" /></Field><Field label="Reset period"><select value={form.resetPeriod} onChange={(event) => setForm({ ...form, resetPeriod: event.target.value })} className="field-input">{['Never', 'Yearly', 'Monthly'].map((item) => <option key={item} value={item}>{item}</option>)}</select></Field><Field label="Active"><input type="checkbox" checked={form.isActive} onChange={(event) => setForm({ ...form, isActive: event.target.checked })} /></Field></div> : null}
      <div className="mt-4 flex justify-end gap-3"><button type="button" className="button-secondary" onClick={() => { setEditing(null); setForm(null) }}>Cancel</button><button type="button" className="button-primary" onClick={() => void save()}><Save className="h-4 w-4" />Save rule</button></div>
    </WebhookDrawer>
  </SettingsCard>

  async function seedDefaults() { try { const next = await settingsService.seedDefaultNumberingRules(); setData(next); pushToast({ title: 'Defaults seeded', description: 'Standard numbering rules were created or refreshed.', tone: 'success' }) } catch (nextError) { pushToast({ title: 'Seed failed', description: nextError instanceof Error ? nextError.message : 'Unable to seed numbering defaults.', tone: 'danger' }) } }
  async function save() { if (!editing || !form) return; try { await settingsService.updateNumberingRule(editing.id, { prefix: form.prefix, nextNumber: form.nextNumber, paddingLength: form.paddingLength, resetPeriod: form.resetPeriod, isActive: form.isActive }); pushToast({ title: 'Numbering rule saved', description: 'The numbering rule was updated.', tone: 'success' }); setEditing(null); setForm(null); await reload() } catch (nextError) { pushToast({ title: 'Save failed', description: nextError instanceof Error ? nextError.message : 'Unable to save the numbering rule.', tone: 'danger' }) } }
  async function previewRule(rule: NumberingRuleRecord) { try { const preview = await settingsService.previewNumberingRule(rule.id); pushToast({ title: `${rule.documentType} preview`, description: preview.preview, tone: 'success' }) } catch (nextError) { pushToast({ title: 'Preview failed', description: nextError instanceof Error ? nextError.message : 'Unable to preview the numbering rule.', tone: 'danger' }) } }
}

function renderEmailNotificationFields(current: UpdateEmailNotificationSettingsInput, setForm: (value: UpdateEmailNotificationSettingsInput) => void, hasSecret: boolean) {
  const update = <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => setForm({ ...current, [key]: value })
  const smtpMode = current.deliveryMode === 'Smtp'
  // 'Api' delivery mode is not yet implemented in the backend (throws NotSupportedException).
  // It is intentionally excluded from the dropdown until the integration is ready.
  return <>
    <Field label="Enable sending"><input type="checkbox" checked={current.isEnabled} onChange={(event) => update('isEnabled', event.target.checked)} /></Field>
    <Field label="Delivery method"><select value={current.deliveryMode === 'Api' ? 'Smtp' : current.deliveryMode} onChange={(event) => update('deliveryMode', event.target.value as UpdateEmailNotificationSettingsInput['deliveryMode'])} className="field-input">{['Smtp', 'Disabled'].map((item) => <option key={item} value={item}>{item === 'Smtp' ? 'SMTP' : 'Disabled'}</option>)}</select></Field>
    <Field label="From name"><input value={current.fromName} onChange={(event) => update('fromName', event.target.value)} className="field-input" /></Field>
    <Field label="From email"><input value={current.fromEmail} onChange={(event) => update('fromEmail', event.target.value)} className="field-input" /></Field>
    <Field label="Reply-to email"><input value={current.replyToEmail || ''} onChange={(event) => update('replyToEmail', event.target.value)} className="field-input" /></Field>
    <Field label="Secure mode"><select value={current.secureMode} onChange={(event) => update('secureMode', event.target.value as UpdateEmailNotificationSettingsInput['secureMode'])} className="field-input"><option value="Auto">Auto</option><option value="None">None</option><option value="StartTls">STARTTLS</option><option value="SslOnConnect">SSL on Connect</option></select><p className="text-xs text-muted">Port 465 uses SSL/TLS. Port 587 uses STARTTLS.</p></Field>
    <Field label="Enable SSL/TLS"><input type="checkbox" checked={current.enableSslTls} onChange={(event) => update('enableSslTls', event.target.checked)} /></Field>
    <Field label="Timeout (seconds)"><input type="number" min={5} value={current.timeoutSeconds} onChange={(event) => update('timeoutSeconds', Number(event.target.value) || 30)} className="field-input" /></Field>
    <Field label="Max retries"><input type="number" min={0} value={current.maxRetries} onChange={(event) => update('maxRetries', Number(event.target.value) || 0)} className="field-input" /></Field>
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
function applyRecommendedSecureMode(updatePort: number, update: <Key extends keyof UpdateEmailNotificationSettingsInput>(key: Key, value: UpdateEmailNotificationSettingsInput[Key]) => void) { update('smtpPort', updatePort); if (updatePort === 465) { update('enableSslTls', true); update('secureMode', 'SslOnConnect') } else if (updatePort === 587) { update('enableSslTls', true); update('secureMode', 'StartTls') } }
export { EmailTemplatesSettingsPage } from './EmailTemplatesSettingsPage'
