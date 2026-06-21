import { useEffect, useMemo, useState } from 'react'
import { InfoAlert } from '../../../../components/ui/InfoAlert'
import { LoadingState } from '../../../../components/ui/LoadingState'
import { useToast } from '../../../../components/ui/ToastProvider'
import { FormSection, PageTabs, SectionCard, StickyActionFooter } from '../../../../components/ui/Workspace'
import { useAsyncData } from '../../../../hooks/useAsyncData'
import { Field, SectionTitle } from '../../../../modules/platform-v2/PlatformCommon'
import { platformSettingsService, type PlatformEmailSettings, type PlatformNotificationPreference } from '../../../../services/platformSettingsService'
import { toServiceError } from '../../../../services/platformService'
import { EmailDeliveryLogsPanel } from './EmailDeliveryLogsPanel'
import { EmailNotificationRulesPanel } from './EmailNotificationRulesPanel'
import { EmailTemplatesSettingsPanel } from './EmailTemplatesSettingsPanel'

type EmailWorkspaceTab = 'smtp' | 'templates' | 'rules' | 'logs'

const preferenceLabels: Record<string, string> = {
  'new-tenant-created': 'New tenant created',
  'tenant-deactivated': 'Tenant deactivated',
  'subscription-expiring': 'Subscription expiring',
  'invoice-overdue': 'Account attention needed',
  'payment-received': 'Payment confirmation received',
  'work-order-overdue': 'Work order overdue',
  'sla-breach': 'Priority response alert',
  'failed-login-attempts': 'Failed login attempts',
  'system-errors': 'System errors',
}

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const defaultSettings: PlatformEmailSettings = {
  deliveryMode: 'Smtp',
  smtpHost: '',
  smtpPort: 587,
  smtpUsername: '',
  smtpPasswordMasked: '',
  smtpPasswordSecret: '',
  senderName: 'Ecosys Platform',
  senderEmail: '',
  replyToEmail: '',
  enableSslTls: true,
  secureMode: 'StartTls',
  apiEndpoint: '',
  apiKeyMasked: '',
  apiKeySecret: '',
  apiProviderName: '',
  timeoutSeconds: 30,
  maxRetries: 0,
  enableEmailNotifications: true,
  enableSystemAlerts: true,
  enableInvoiceEmails: true,
  enableQuotationEmails: true,
  enablePaymentReceiptEmails: true,
  enableWorkOrderNotificationEmails: true,
  enableSlaEscalationEmails: true,
  enableTenantOnboardingEmails: true,
  subjectPrefix: '[Ecosys]',
  subjectSuffix: '',
  includeEnvironmentInSubject: false,
  environmentLabel: 'Production',
  includeTenantNameInSubject: false,
  enableEventSubjectTags: true,
  notificationPreferences: Object.keys(preferenceLabels).map((key) => ({
    notificationKey: key,
    emailEnabled: true,
    inAppEnabled: key === 'failed-login-attempts' || key === 'system-errors',
    smsEnabled: false,
    isActive: true,
  })),
  lastError: null,
  lastTestedAt: null,
}

const tabs: Array<{ id: EmailWorkspaceTab; label: string }> = [
  { id: 'smtp', label: 'SMTP Settings' },
  { id: 'templates', label: 'Email Templates' },
  { id: 'rules', label: 'Notification Rules' },
  { id: 'logs', label: 'Delivery Logs' },
]

export function EmailNotificationSettingsPanel() {
  const { pushToast } = useToast()
  const { data, loading, error, reload } = useAsyncData(() => platformSettingsService.getEmail(), defaultSettings, [])
  const [form, setForm] = useState<PlatformEmailSettings>(data)
  const [testEmail, setTestEmail] = useState('')
  const [saving, setSaving] = useState(false)
  const [testing, setTesting] = useState(false)
  const [verifying, setVerifying] = useState(false)
  const [activeTab, setActiveTab] = useState<EmailWorkspaceTab>('smtp')

  useEffect(() => {
    setForm({
      ...data,
      smtpPasswordSecret: '',
      apiKeySecret: '',
      secureMode: normalizeSecureMode(data.secureMode, data.smtpPort),
    })
  }, [data])

  const smtpMode = form.deliveryMode === 'Smtp'
  const disabledMode = form.deliveryMode === 'Disabled'
  const portGuidance = useMemo(() => getPortGuidance(form.smtpPort, form.secureMode), [form.smtpPort, form.secureMode])

  function validate(): string | null {
    if (form.enableEmailNotifications && form.deliveryMode === 'Smtp') {
      if (!form.smtpHost.trim()) return 'SMTP host is required when email notifications are enabled.'
      if (!Number.isFinite(form.smtpPort) || form.smtpPort <= 0) return 'SMTP port must be a positive number.'
      if (!form.senderEmail.trim()) return 'Sender email is required when email notifications are enabled.'
      if (!emailRegex.test(form.senderEmail.trim())) return 'Sender email must be a valid email address.'
    }

    if (form.replyToEmail && !emailRegex.test(form.replyToEmail.trim())) {
      return 'Reply-to email must be a valid email address.'
    }

    if (testEmail && !emailRegex.test(testEmail.trim())) {
      return 'Test recipient email must be a valid email address.'
    }

    return null
  }

  async function save() {
    const validationError = validate()
    if (validationError) {
      pushToast({ title: 'Validation failed', description: validationError, tone: 'warning' })
      return
    }

    setSaving(true)
    try {
      await platformSettingsService.updateEmail({ ...form, enableSslTls: form.secureMode !== 'None' })
      pushToast({ title: 'Email settings saved', description: 'Delivery and notification settings were updated.', tone: 'success' })
      await reload()
    } catch (saveError) {
      pushToast({ title: 'Save failed', description: toServiceError(saveError, 'Unable to save email settings.'), tone: 'danger' })
    } finally {
      setSaving(false)
    }
  }

  async function sendTest() {
    const validationError = validate()
    if (validationError) {
      pushToast({ title: 'Validation failed', description: validationError, tone: 'warning' })
      return
    }

    setTesting(true)
    try {
      const response = await platformSettingsService.sendTestEmail(testEmail || undefined)
      pushToast({
        title: response.success ? 'Test email sent' : 'Test email failed',
        description: response.success ? response.message || 'Check Delivery Logs for delivery status.' : describeEmailError(response.lastError),
        tone: response.success ? 'success' : 'danger',
      })
      await reload()
    } catch (testError) {
      pushToast({ title: 'Test failed', description: toServiceError(testError, 'Unable to send test email.'), tone: 'danger' })
    } finally {
      setTesting(false)
    }
  }

  async function verify() {
    const validationError = validate()
    if (validationError) {
      pushToast({ title: 'Validation failed', description: validationError, tone: 'warning' })
      return
    }

    setVerifying(true)
    try {
      const response = await platformSettingsService.verifySmtpConnection()
      pushToast({
        title: response.success ? 'Connection verified' : 'Connection verification failed',
        description: response.success ? 'SMTP delivery check completed successfully.' : describeEmailError(response.lastError),
        tone: response.success ? 'success' : 'danger',
      })
      await reload()
    } catch (verifyError) {
      pushToast({ title: 'Verification failed', description: toServiceError(verifyError, 'Unable to verify delivery connection.'), tone: 'danger' })
    } finally {
      setVerifying(false)
    }
  }

  function updatePreference(key: string, update: Partial<PlatformNotificationPreference>) {
    setForm((current) => ({
      ...current,
      notificationPreferences: current.notificationPreferences.map((item) => (item.notificationKey === key ? { ...item, ...update } : item)),
    }))
  }

  if (loading) return <LoadingState label="Loading email and notification settings" />
  if (error) return <InfoAlert title="Unable to load email settings" description={error} tone="danger" />

  return (
    <section data-testid="platform-email-settings-form" className="space-y-4">
      <SectionTitle title="Email & Notifications" description="Manage outbound delivery, governed templates, dispatch rules, and delivery visibility for platform activity." />

      <PageTabs tabs={tabs} activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'smtp' ? (
        <SectionCard title="SMTP Settings" description="Use SMTP for outbound credentials, onboarding, and governed notification delivery.">
          <InfoAlert title="SMTP sends mail only" description="Outbound SMTP covers test emails, user credentials, resend credentials, onboarding, lead notifications, and template test sends. Email intake for creating or updating work orders is configured separately." tone="info" />
          <InfoAlert title="SMTP-only delivery" description="Pilot delivery uses SMTP for outbound mail. Alternative delivery methods are hidden until the backend supports them fully." tone="warning" />

          <FormSection title="Delivery Configuration" description="Connection, sender identity, and subject behavior.">
            <Field label="Delivery Method">
              <select value={form.deliveryMode} onChange={(event) => setForm((current) => ({ ...current, deliveryMode: event.target.value as PlatformEmailSettings['deliveryMode'] }))} className="field-input">
                <option value="Smtp">SMTP</option>
                <option value="Disabled">Disabled</option>
              </select>
            </Field>

            <Field label="Security Mode">
              <select value={form.secureMode} onChange={(event) => setForm((current) => ({ ...current, secureMode: event.target.value as PlatformEmailSettings['secureMode'] }))} className="field-input" disabled={!smtpMode || disabledMode}>
                <option value="None">None</option>
                <option value="StartTls">STARTTLS</option>
                <option value="SslOnConnect">SSL/TLS</option>
              </select>
              <p className="text-xs text-muted">Use STARTTLS for port 587. Use SSL/TLS for port 465.</p>
            </Field>

            {smtpMode ? (
              <>
                <Field label="SMTP Host"><input data-testid="smtp-host-input" disabled={disabledMode} value={form.smtpHost || ''} onChange={(event) => setForm((current) => ({ ...current, smtpHost: event.target.value }))} className="field-input" /></Field>
                <Field label="SMTP Port"><input data-testid="smtp-port-input" disabled={disabledMode} type="number" min={1} value={form.smtpPort || 0} onChange={(event) => setForm((current) => ({ ...current, smtpPort: Number(event.target.value) || 1 }))} className="field-input" /></Field>
                <Field label="SMTP Username"><input data-testid="smtp-username-input" disabled={disabledMode} value={form.smtpUsername || ''} onChange={(event) => setForm((current) => ({ ...current, smtpUsername: event.target.value }))} className="field-input" /></Field>
                <Field label="SMTP Password / Secret">
                  <input data-testid="smtp-password-input" disabled={disabledMode} type="password" placeholder={form.smtpPasswordMasked || 'Leave blank to keep existing password'} value={form.smtpPasswordSecret || ''} onChange={(event) => setForm((current) => ({ ...current, smtpPasswordSecret: event.target.value }))} className="field-input" />
                  <p className="text-xs text-muted">{form.smtpPasswordMasked || 'Password saved. Leave blank to keep existing password.'}</p>
                </Field>
              </>
            ) : null}

            <Field label="Sender Name"><input data-testid="sender-name-input" value={form.senderName || ''} onChange={(event) => setForm((current) => ({ ...current, senderName: event.target.value }))} className="field-input" /></Field>
            <Field label="Sender Email"><input data-testid="sender-email-input" type="email" value={form.senderEmail || ''} onChange={(event) => setForm((current) => ({ ...current, senderEmail: event.target.value }))} className="field-input" /></Field>
            <Field label="Reply-to Email"><input data-testid="reply-to-email-input" type="email" value={form.replyToEmail || ''} onChange={(event) => setForm((current) => ({ ...current, replyToEmail: event.target.value }))} className="field-input" /></Field>
            <Field label="Timeout Seconds"><input type="number" min={5} value={form.timeoutSeconds || 30} onChange={(event) => setForm((current) => ({ ...current, timeoutSeconds: Number(event.target.value) || 30 }))} className="field-input" /></Field>
            <Field label="Max Retries"><input type="number" min={0} value={form.maxRetries || 0} onChange={(event) => setForm((current) => ({ ...current, maxRetries: Number(event.target.value) || 0 }))} className="field-input" /></Field>
            <Field label="Subject Prefix"><input value={form.subjectPrefix || ''} onChange={(event) => setForm((current) => ({ ...current, subjectPrefix: event.target.value }))} className="field-input" placeholder="[Ecosys]" /></Field>
            <Field label="Subject Suffix"><input value={form.subjectSuffix || ''} onChange={(event) => setForm((current) => ({ ...current, subjectSuffix: event.target.value }))} className="field-input" placeholder="Optional" /></Field>
            <Field label="Environment Label"><input value={form.environmentLabel || ''} onChange={(event) => setForm((current) => ({ ...current, environmentLabel: event.target.value }))} className="field-input" placeholder="Production" /></Field>
          </FormSection>

          <InfoAlert title="Subject rules" description="Subject rules help identify Ecosys emails in inboxes and delivery logs. Example: [Ecosys] [Security] Reset your password." tone="info" />

          {portGuidance ? <InfoAlert title="Connection check" description={portGuidance} tone="warning" /> : null}

          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable outgoing emails</span><input data-testid="enable-email-notifications-toggle" type="checkbox" checked={form.enableEmailNotifications} onChange={(event) => setForm((current) => ({ ...current, enableEmailNotifications: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable system alerts</span><input type="checkbox" checked={form.enableSystemAlerts} onChange={(event) => setForm((current) => ({ ...current, enableSystemAlerts: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable work order emails</span><input type="checkbox" checked={form.enableWorkOrderNotificationEmails} onChange={(event) => setForm((current) => ({ ...current, enableWorkOrderNotificationEmails: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable tenant onboarding emails</span><input type="checkbox" checked={form.enableTenantOnboardingEmails} onChange={(event) => setForm((current) => ({ ...current, enableTenantOnboardingEmails: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Include environment label</span><input type="checkbox" checked={form.includeEnvironmentInSubject} onChange={(event) => setForm((current) => ({ ...current, includeEnvironmentInSubject: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Include tenant name</span><input type="checkbox" checked={form.includeTenantNameInSubject} onChange={(event) => setForm((current) => ({ ...current, includeTenantNameInSubject: event.target.checked }))} /></label>
            <label className="panel-subtle flex items-center justify-between rounded-2xl px-4 py-3"><span className="text-sm text-app">Enable event subject tags</span><input type="checkbox" checked={form.enableEventSubjectTags} onChange={(event) => setForm((current) => ({ ...current, enableEventSubjectTags: event.target.checked }))} /></label>
          </div>

          <section className="space-y-3">
            <p className="text-sm font-semibold text-app">Notification Preferences</p>
            <div className="space-y-2">
              {form.notificationPreferences.map((item) => (
                <article key={item.notificationKey} className="panel-subtle rounded-2xl px-4 py-3">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <p className="text-sm font-medium text-app">{preferenceLabels[item.notificationKey] || item.notificationKey}</p>
                    <div className="flex flex-wrap gap-3 text-xs">
                      <label className="flex items-center gap-2"><input type="checkbox" checked={item.emailEnabled} onChange={(event) => updatePreference(item.notificationKey, { emailEnabled: event.target.checked })} />Email</label>
                      <label className="flex items-center gap-2"><input type="checkbox" checked={item.inAppEnabled} onChange={(event) => updatePreference(item.notificationKey, { inAppEnabled: event.target.checked })} />In-App</label>
                      <label className="flex items-center gap-2"><input type="checkbox" checked={item.smsEnabled} onChange={(event) => updatePreference(item.notificationKey, { smsEnabled: event.target.checked })} />SMS</label>
                      <label className="flex items-center gap-2"><input type="checkbox" checked={item.isActive} onChange={(event) => updatePreference(item.notificationKey, { isActive: event.target.checked })} />Active</label>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          </section>

          {form.lastError ? (
            <InfoAlert
              title="Last delivery error"
              description={`${describeEmailError(form.lastError)}${form.lastTestedAt ? ` Last attempt: ${new Date(form.lastTestedAt).toLocaleString()}.` : ''} Suggested checks: confirm host, port, security mode, credentials, certificate trust, and firewall access.`}
              tone="warning"
            />
          ) : null}

          <div className="grid gap-3 md:grid-cols-[1fr_auto_auto_auto]">
            <Field label="Test recipient email"><input type="email" value={testEmail} onChange={(event) => setTestEmail(event.target.value)} className="field-input" placeholder="name@example.com" /></Field>
            <button data-testid="send-test-email-button" type="button" className="button-secondary self-end" onClick={() => void sendTest()} disabled={testing}>{testing ? 'Queueing...' : 'Send Test Email'}</button>
            <button data-testid="verify-smtp-button" type="button" className="button-secondary self-end" onClick={() => void verify()} disabled={verifying}>{verifying ? 'Verifying...' : 'Verify Delivery Connection'}</button>
          </div>
          <StickyActionFooter>
            <button data-testid="save-email-settings-button" type="button" className="button-primary" onClick={() => void save()} disabled={saving}>{saving ? 'Saving...' : 'Save Email Settings'}</button>
          </StickyActionFooter>
        </SectionCard>
      ) : null}

      {activeTab === 'templates' ? <div data-testid="settings-panel-email-templates"><EmailTemplatesSettingsPanel /></div> : null}
      {activeTab === 'rules' ? <div data-testid="settings-panel-email-rules"><EmailNotificationRulesPanel /></div> : null}
      {activeTab === 'logs' ? <div data-testid="settings-panel-email-logs"><EmailDeliveryLogsPanel /></div> : null}
    </section>
  )
}

function normalizeSecureMode(mode: PlatformEmailSettings['secureMode'], port: number): PlatformEmailSettings['secureMode'] {
  if (mode !== 'Auto') {
    return mode
  }

  if (port === 465) return 'SslOnConnect'
  if (port === 587) return 'StartTls'
  return 'None'
}

function getPortGuidance(port: number, secureMode: PlatformEmailSettings['secureMode']) {
  if (port === 587 && secureMode !== 'StartTls') {
    return 'Port 587 usually expects STARTTLS. Review the selected security mode before saving.'
  }

  if (port === 465 && secureMode !== 'SslOnConnect') {
    return 'Port 465 usually expects SSL/TLS. Review the selected security mode before saving.'
  }

  return null
}

function describeEmailError(value?: string | null) {
  if (!value) return 'Unable to complete the email operation.'
  if (value === 'TLS/SSL failed') {
    return 'TLS/SSL handshake failed. Check SMTP port, security mode, host certificate, username/password, and firewall access.'
  }

  return value
}
